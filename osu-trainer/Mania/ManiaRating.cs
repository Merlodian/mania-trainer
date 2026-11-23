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
        /// <summary>A mania map with a key count no calculator here rates: anything but 4K (xdc1) and 7K (xdc7).</summary>
        UnsupportedKeys,
        /// <summary>The calculator could not rate the map (or its library is missing); see <see cref="ManiaRating.Error"/>.</summary>
        Failed,
    }

    /// <summary>The dan rating of the edited beatmap as it would be exported.</summary>
    public sealed class ManiaRating
    {
        public ManiaRatingStatus Status { get; private set; }
        public int Keys { get; private set; }

        /// <summary>
        /// The dan table the rating is read against (4K with xdc1, or 7K with xdc7); null for other key counts.
        /// <see cref="Dan"/> and <see cref="NextDan"/> are dans of this table.
        /// </summary>
        public DanSystem System { get; private set; }

        public double Rating { get; private set; }

        /// <summary>xdc1's skillsets (4K); empty for a 7K rating, which has none.</summary>
        public double[] Skillsets { get; private set; } = new double[0];

        public int Notes { get; private set; }
        public int Holds { get; private set; }
        public string Error { get; private set; }

        /// <summary>
        /// True for a 7K rating: xdc7 rates rice only, so every long note was counted as a tap on its head.
        /// </summary>
        public bool RiceOnly { get; private set; }

        /// <summary>
        /// True when the rating is rice only and long notes are a good share of the chart
        /// (<see cref="Xdc7.IsLongNoteHeavy"/>): the number then says little about the chart as it is played.
        /// </summary>
        public bool LongNoteHeavy => RiceOnly && Xdc7.IsLongNoteHeavy(Notes, Holds);

        /// <summary>7K: the rating with the middle key on the left thumb, and on the right. <see cref="Rating"/> is the lower.</summary>
        public double ThumbLeft { get; private set; }
        public double ThumbRight { get; private set; }

        /// <summary>The dan reached, or null when the rating is below the table's lowest dan (or not rated).</summary>
        public DanLevel Dan { get; private set; }

        /// <summary>The next dan up, or null at the table's highest dan.</summary>
        public DanLevel NextDan { get; private set; }

        /// <summary>0 to 1: how far the rating is from <see cref="Dan"/>'s threshold to <see cref="NextDan"/>'s.</summary>
        public float Progress { get; private set; }

        /// <summary>A 4K rating from xdc1.</summary>
        public static ManiaRating Rated(Xdc1Result result)
        {
            var rating = Rated(DanTable.FourKey, result.Rating, result.Notes, result.Holds);
            rating.Skillsets = result.Skillsets;
            return rating;
        }

        /// <summary>A 7K rating from xdc7.</summary>
        public static ManiaRating Rated(Xdc7Result result)
        {
            var rating = Rated(DanTable.SevenKey, result.Rating, result.Notes, result.Holds);
            rating.RiceOnly = true;
            rating.ThumbLeft = result.ThumbLeft;
            rating.ThumbRight = result.ThumbRight;
            return rating;
        }

        private static ManiaRating Rated(DanSystem system, double value, int notes, int holds)
        {
            var dan = system.Classify(value);
            return new ManiaRating
            {
                Status = ManiaRatingStatus.Ok,
                Keys = system.Keys,
                System = system,
                Rating = value,
                Notes = notes,
                Holds = holds,
                Dan = dan,
                NextDan = system.Next(dan),
                Progress = system.ProgressToNext(value),
            };
        }

        public static ManiaRating UnsupportedKeys(int keys) => new ManiaRating { Status = ManiaRatingStatus.UnsupportedKeys, Keys = keys };

        public static ManiaRating Failed(string error, int keys) =>
            new ManiaRating { Status = ManiaRatingStatus.Failed, Keys = keys, System = DanTable.ForKeys(keys), Error = error };

        /// <summary>Multi-line description for the tooltip over the song display.</summary>
        public string Describe()
        {
            var inv = CultureInfo.InvariantCulture;
            switch (Status)
            {
                case ManiaRatingStatus.UnsupportedKeys:
                    return $"{Keys}K map: the dan rating only supports 4K (xdc1) and 7K (xdc7).";
                case ManiaRatingStatus.Failed:
                    return $"Couldn't rate this map: {Error}";
            }

            // ratings and thresholds go through DanTable.FormatRating, the rounding Classify uses
            var sb = new StringBuilder();
            sb.Append(Dan == null
                ? string.Format(inv, "{0} rating {1}: below {2} ({3})", System.Engine, DanTable.FormatRating(Rating), System.Levels[0].DisplayName, DanTable.FormatRating(System.Levels[0].Threshold))
                : string.Format(inv, "{0} rating {1}: {2} (from {3})", System.Engine, DanTable.FormatRating(Rating), Dan.DisplayName, DanTable.FormatRating(Dan.Threshold)));
            if (NextDan != null)
                sb.Append(string.Format(inv, "\nNext: {0} at {1}", NextDan.DisplayName, DanTable.FormatRating(NextDan.Threshold)));
            sb.Append(string.Format(inv, "\n{0} notes, {1} long notes", Notes, Holds));

            if (RiceOnly)
            {
                // xdc7 rates both ways to hold the middle key and keeps the easier one
                sb.Append(string.Format(inv, "\nMiddle key on the left thumb {0}, on the right thumb {1}",
                    DanTable.FormatRating(ThumbLeft), DanTable.FormatRating(ThumbRight)));
                if (Holds > 0)
                {
                    sb.Append(LongNoteHeavy
                        ? string.Format(inv, "\nRated as rice: {0:0}% of this chart is long notes, which xdc7 counts as taps.\nThe chart as it is played may be a good deal harder.", 100.0 * Holds / Math.Max(1, Notes))
                        : "\nRated as rice: xdc7 counts a long note as a tap.");
                }
            }

            var names = Xdc1.SkillsetNames;
            if (!RiceOnly && Skillsets.Length == names.Length && Skillsets.Length > 0)
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
        /// osu!mania key count (CircleSize), rounded the way osu!, xdc1 and xdc7 read it. Never throws: a CircleSize
        /// too big for an int (a broken map; the window asks this on every state change) counts as -1.
        /// </summary>
        public static int KeyCount(Beatmap map)
        {
            decimal keys = Math.Round(map.CircleSize, MidpointRounding.AwayFromZero);
            return keys >= int.MinValue && keys <= int.MaxValue ? (int)keys : -1;
        }

        /// <summary>
        /// The dan table a map is rated against, by its key count: 4K or 7K, null for anything else (and for null).
        /// </summary>
        public static DanSystem SystemFor(Beatmap map) => map == null ? null : DanTable.ForKeys(KeyCount(map));

        /// <summary>True when the calculator behind a dan table (xdc1.dll or xdc7.dll) can be used.</summary>
        public static bool IsAvailable(DanSystem system) =>
            ReferenceEquals(system, DanTable.FourKey) ? Xdc1.IsAvailable
            : ReferenceEquals(system, DanTable.SevenKey) && Xdc7.IsAvailable;

        /// <summary>Why a table's calculator can't be used, or null when it can.</summary>
        public static string LoadError(DanSystem system) =>
            ReferenceEquals(system, DanTable.FourKey) ? Xdc1.LoadError
            : ReferenceEquals(system, DanTable.SevenKey) ? Xdc7.LoadError
            : "no calculator for this key count";

        /// <summary>
        /// Rates a snapshot of the edited beatmap (rate already applied) after the long note conversion: a 4K map
        /// with xdc1, a 7K map with xdc7. xdc7 is rice only: it counts every long note as a tap, so there the
        /// conversion all but drops out (it takes the chart's end from the last tail too, which moved the 7K dan
        /// charts' ratings by 0.001 at most with No LN). Never throws; failures come back as
        /// <see cref="ManiaRatingStatus.Failed"/>.
        /// </summary>
        public static ManiaRating Calculate(Beatmap snapshot, LnConversion conversion)
        {
            int keys = -1;
            try
            {
                keys = KeyCount(snapshot);
                var system = DanTable.ForKeys(keys);
                if (system == null)
                    return ManiaRating.UnsupportedKeys(keys);
                if (!IsAvailable(system))
                    return ManiaRating.Failed(LoadError(system), keys);

                string text = ManiaTransforms.Apply(Serialize(snapshot), conversion);
                if (ReferenceEquals(system, DanTable.SevenKey))
                {
                    var result7 = Xdc7.Analyse(text);
                    return result7.Ok ? ManiaRating.Rated(result7) : ManiaRating.Failed(result7.Error, keys);
                }
                var result = Xdc1.Analyse(text);
                return result.Ok ? ManiaRating.Rated(result) : ManiaRating.Failed(result.Error, keys);
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
