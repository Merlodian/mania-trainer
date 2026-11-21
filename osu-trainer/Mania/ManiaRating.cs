using FsBeatmapProcessor;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace osu_trainer.Mania
{
    public enum ManiaRatingStatus
    {
        /// <summary>Rated: <see cref="ManiaRating.Rating"/> and <see cref="ManiaRating.Dan"/> are set.</summary>
        Ok,
        /// <summary>A mania map with a key count other than 4; xdc1 only rates 4K.</summary>
        NotFourKey,
        /// <summary>xdc1 could not rate the map (or the library is missing); see <see cref="ManiaRating.Error"/>.</summary>
        Failed,
    }

    /// <summary>The dan rating of the edited beatmap as it would be exported.</summary>
    public sealed class ManiaRating
    {
        public ManiaRatingStatus Status { get; private set; }
        public int Keys { get; private set; }
        public double Rating { get; private set; }
        public double[] Skillsets { get; private set; } = new double[0];
        public int Notes { get; private set; }
        public int Holds { get; private set; }
        public string Error { get; private set; }

        /// <summary>The dan reached, or null when the rating is below 1st dan (or not rated).</summary>
        public DanLevel Dan { get; private set; }

        /// <summary>The next dan up, or null at Kappa.</summary>
        public DanLevel NextDan { get; private set; }

        /// <summary>0 to 1: how far the rating is from <see cref="Dan"/>'s threshold to <see cref="NextDan"/>'s.</summary>
        public float Progress { get; private set; }

        public static ManiaRating Rated(Xdc1Result result, int keys)
        {
            var dan = DanTable.Classify(result.Rating);
            return new ManiaRating
            {
                Status = ManiaRatingStatus.Ok,
                Keys = keys,
                Rating = result.Rating,
                Skillsets = result.Skillsets,
                Notes = result.Notes,
                Holds = result.Holds,
                Dan = dan,
                NextDan = DanTable.Next(dan),
                Progress = DanTable.ProgressToNext(result.Rating),
            };
        }

        public static ManiaRating NotFourKey(int keys) => new ManiaRating { Status = ManiaRatingStatus.NotFourKey, Keys = keys };

        public static ManiaRating Failed(string error, int keys) => new ManiaRating { Status = ManiaRatingStatus.Failed, Keys = keys, Error = error };

        /// <summary>Multi-line description for the tooltip over the song display.</summary>
        public string Describe()
        {
            var inv = CultureInfo.InvariantCulture;
            switch (Status)
            {
                case ManiaRatingStatus.NotFourKey:
                    return $"{Keys}K map: the dan rating (xdc1) only supports 4K.";
                case ManiaRatingStatus.Failed:
                    return $"Couldn't rate this map: {Error}";
            }

            // ratings and thresholds go through DanTable.FormatRating, the rounding Classify uses
            var sb = new StringBuilder();
            sb.Append(Dan == null
                ? string.Format(inv, "xdc1 rating {0}: below 1st Dan ({1})", DanTable.FormatRating(Rating), DanTable.FormatRating(DanTable.Levels[0].Threshold))
                : string.Format(inv, "xdc1 rating {0}: {1} (from {2})", DanTable.FormatRating(Rating), Dan.DisplayName, DanTable.FormatRating(Dan.Threshold)));
            if (NextDan != null)
                sb.Append(string.Format(inv, "\nNext: {0} at {1}", NextDan.DisplayName, DanTable.FormatRating(NextDan.Threshold)));
            sb.Append(string.Format(inv, "\n{0} notes, {1} long notes", Notes, Holds));

            var names = Xdc1.SkillsetNames;
            if (Skillsets.Length == names.Length && Skillsets.Length > 0)
            {
                var ranked = Enumerable.Range(0, Skillsets.Length).OrderByDescending(i => Skillsets[i]);
                sb.Append("\n").Append(string.Join("  ", ranked.Select(i => string.Format(inv, "{0} {1:0.0}", names[i], Skillsets[i]))));
            }
            return sb.ToString();
        }
    }

    public static class ManiaDifficulty
    {
        /// <summary>
        /// osu!mania key count (CircleSize), rounded the way osu! and xdc1 read it. Never throws: a CircleSize too
        /// big for an int (a broken map; the window asks this on every state change) counts as -1.
        /// </summary>
        public static int KeyCount(Beatmap map)
        {
            decimal keys = Math.Round(map.CircleSize, MidpointRounding.AwayFromZero);
            return keys >= int.MinValue && keys <= int.MaxValue ? (int)keys : -1;
        }

        /// <summary>
        /// Rates a snapshot of the edited beatmap (rate already applied) after the long note conversion.
        /// Never throws; failures come back as <see cref="ManiaRatingStatus.Failed"/>.
        /// </summary>
        public static ManiaRating Calculate(Beatmap snapshot, LnConversion conversion)
        {
            int keys = -1;
            try
            {
                keys = KeyCount(snapshot);
                if (keys != 4)
                    return ManiaRating.NotFourKey(keys);
                if (!Xdc1.IsAvailable)
                    return ManiaRating.Failed(Xdc1.LoadError, keys);

                string text = ManiaTransforms.Apply(Serialize(snapshot), conversion);
                var result = Xdc1.Analyse(text);
                return result.Ok ? ManiaRating.Rated(result, keys) : ManiaRating.Failed(result.Error, keys);
            }
            catch (Exception e)
            {
                return ManiaRating.Failed(e.Message, keys);
            }
        }

        /// <summary>The .osu text the beatmap would be saved as.</summary>
        public static string Serialize(Beatmap map)
        {
            string temp = Path.Combine(Path.GetTempPath(), $"mania-trainer-{Guid.NewGuid():N}.osu");
            try
            {
                map.Save(temp);
                return File.ReadAllText(temp, Encoding.UTF8);
            }
            finally
            {
                try { File.Delete(temp); } catch { }
            }
        }
    }
}
