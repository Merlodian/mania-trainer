using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;

namespace osu_trainer.Mania
{
    /// <summary>One osu!mania 4K dan (1st to 10th, then the EXTRA dans Alpha to Kappa).</summary>
    public sealed class DanLevel
    {
        internal DanLevel(int index, string name, string displayName, double threshold, Color color, string iconResource)
        {
            Index = index;
            Name = name;
            DisplayName = displayName;
            Threshold = threshold;
            Color = color;
            TextColor = ReadableOnDark(color);
            IconResource = iconResource;
        }

        // A colour too dark to read on the dark window (Kappa's black) is lightened towards white, keeping its hue,
        // until its perceived brightness (0-255) reaches TextMinBrightness: black becomes a dark grey.
        private const double TextMinBrightness = 105;
        private const double TextLiftBelow = 60;
        private static Color ReadableOnDark(Color c)
        {
            double brightness = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            if (brightness >= TextLiftBelow)
                return c;
            double t = (TextMinBrightness - brightness) / (255 - brightness);
            int Lift(int v) => (int)Math.Round(v + (255 - v) * t);
            return Color.FromArgb(Lift(c.R), Lift(c.G), Lift(c.B));
        }

        /// <summary>0 for 1st dan, 19 for Kappa.</summary>
        public int Index { get; }

        /// <summary>"1st" ... "10th", "Alpha" ... "Kappa".</summary>
        public string Name { get; }

        /// <summary>"1st Dan" ... "10th Dan", "Alpha" ... "Kappa".</summary>
        public string DisplayName { get; }

        /// <summary>Lowest xdc1 rating (at 1.0x) that counts as this dan.</summary>
        public double Threshold { get; }

        /// <summary>The dan's colour, as its dan button is filled.</summary>
        public Color Color { get; }

        /// <summary>
        /// The dan's colour for text and bars on the dark window: <see cref="Color"/>, except that a colour too
        /// dark to read there (Kappa's black) is shown as a dark grey.
        /// </summary>
        public Color TextColor { get; }

        internal string IconResource { get; }

        /// <summary>True for the EXTRA (Greek letter) dans, which have their own icons.</summary>
        public bool IsExtra => IconResource != null;

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Maps an xdc1 rating to a dan.
    ///
    /// Each threshold is the mean xdc1 rating (at 1.0x) of every chart of that dan in the user's
    /// "Dan Beatmaps" folder (1st to Eta) and "More Dans" folder (Theta, Iota and Kappa, from Dan ~ REFORM ~
    /// FINAL (Ext.)): the full dan course, its Jack, Tech, Speed and Stamina maps, and every sample
    /// course and sample map for that dan. Intro dans are not included. 246 charts were rated in total.
    /// A chart is shown as the highest dan whose threshold its rating reaches.
    ///
    /// Regenerate with tools/calibrate_dans.py; per-chart ratings are in tools/dan_calibration.csv.
    /// </summary>
    public static class DanTable
    {
        public static readonly IReadOnlyList<DanLevel> Levels = new[]
        {
            new DanLevel(0,  "1st",     "1st Dan",  28.7147, Color.FromArgb(66, 144, 251),  null),
            new DanLevel(1,  "2nd",     "2nd Dan",  32.2334, Color.FromArgb(79, 192, 255),  null),
            new DanLevel(2,  "3rd",     "3rd Dan",  33.5844, Color.FromArgb(79, 255, 213),  null),
            new DanLevel(3,  "4th",     "4th Dan",  37.3902, Color.FromArgb(124, 255, 79),  null),
            new DanLevel(4,  "5th",     "5th Dan",  42.4576, Color.FromArgb(246, 240, 92),  null),
            new DanLevel(5,  "6th",     "6th Dan",  45.8667, Color.FromArgb(255, 184, 98),  null),
            new DanLevel(6,  "7th",     "7th Dan",  47.7026, Color.FromArgb(255, 128, 104), null),
            new DanLevel(7,  "8th",     "8th Dan",  51.5435, Color.FromArgb(255, 78, 111),  null),
            new DanLevel(8,  "9th",     "9th Dan",  54.0190, Color.FromArgb(198, 69, 184),  null),
            new DanLevel(9,  "10th",    "10th Dan", 56.4295, Color.FromArgb(133, 120, 240), null),
            // EXTRA dan colours chosen by the user: pink, dark blue, dark green, orange, purple, light blue, red,
            // dark red, grey, black
            new DanLevel(10, "Alpha",   "Alpha",    59.1441, Color.FromArgb(255, 120, 200), "alpha"),
            new DanLevel(11, "Beta",    "Beta",     61.3607, Color.FromArgb(48, 84, 220),   "beta"),
            new DanLevel(12, "Gamma",   "Gamma",    64.8469, Color.FromArgb(40, 160, 72),   "gamma"),
            new DanLevel(13, "Delta",   "Delta",    70.5580, Color.FromArgb(255, 146, 60),  "delta"),
            new DanLevel(14, "Epsilon", "Epsilon",  79.3652, Color.FromArgb(168, 96, 240),  "epsilon"),
            new DanLevel(15, "Zeta",    "Zeta",     84.8299, Color.FromArgb(128, 200, 255), "zeta"),
            new DanLevel(16, "Eta",     "Eta",      90.1403, Color.FromArgb(236, 54, 70),   "eta"),
            new DanLevel(17, "Theta",   "Theta",    98.1759, Color.FromArgb(184, 26, 40),   "theta"),
            new DanLevel(18, "Iota",    "Iota",    104.1413, Color.FromArgb(160, 160, 168), "iota"),
            new DanLevel(19, "Kappa",   "Kappa",   108.2573, Color.FromArgb(16, 16, 20),    "kappa"),
        };

        /// <summary>A rating (or threshold) rounded to the two decimals the UI shows.</summary>
        public static double RoundForDisplay(double rating) => Math.Round(rating, 2, MidpointRounding.AwayFromZero);

        /// <summary>A rating (or threshold) as the UI shows it, e.g. "42.46". Classify works from the same value.</summary>
        public static string FormatRating(double rating) => RoundForDisplay(rating).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>
        /// The dan a rating corresponds to, or null when it is below the 1st dan threshold.
        /// The rating is rounded to the two decimals the UI shows, so the dan always agrees with the number.
        /// </summary>
        public static DanLevel Classify(double rating)
        {
            if (double.IsNaN(rating) || double.IsInfinity(rating))
                return null;
            double shown = RoundForDisplay(rating);
            DanLevel result = null;
            foreach (var level in Levels)
            {
                if (shown >= RoundForDisplay(level.Threshold))
                    result = level;
                else
                    break;
            }
            return result;
        }

        /// <summary>The dan with this name ("1st" ... "Kappa", or "1st Dan" ...; any case), or null.</summary>
        public static DanLevel FindByName(string name)
        {
            string wanted = name?.Trim();
            foreach (var level in Levels)
                if (string.Equals(level.Name, wanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(level.DisplayName, wanted, StringComparison.OrdinalIgnoreCase))
                    return level;
            return null;
        }

        /// <summary>The next harder dan, the first dan when <paramref name="level"/> is null, or null after Kappa.</summary>
        public static DanLevel Next(DanLevel level)
        {
            int next = level == null ? 0 : level.Index + 1;
            return next < Levels.Count ? Levels[next] : null;
        }

        /// <summary>
        /// How far a rating has got from its dan's threshold towards the next one, 0 to 1.
        /// 1 at Kappa and above; for ratings below 1st dan, measured from 0.
        /// </summary>
        public static float ProgressToNext(double rating)
        {
            var current = Classify(rating);
            var next = Next(current);
            if (next == null)
                return 1f;
            double from = current?.Threshold ?? 0;
            double span = next.Threshold - from;
            if (span <= 0)
                return 0f;
            return (float)Math.Max(0, Math.Min(1, (rating - from) / span));
        }

        private static readonly ConcurrentDictionary<string, Image> iconCache = new ConcurrentDictionary<string, Image>();

        /// <summary>The Greek-letter icon of an EXTRA dan (64x64, transparent), or null for 1st to 10th dan.</summary>
        public static Image GetIcon(DanLevel level)
        {
            if (level?.IconResource == null)
                return null;
            return iconCache.GetOrAdd(level.IconResource, LoadIcon);
        }

        private static Image LoadIcon(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream($"osu_trainer.resources.dans.{name}.png"))
            {
                if (stream == null)
                    return null;
                // Bitmap needs its stream for its whole life, so copy it into a stream-independent bitmap.
                using (var loaded = new Bitmap(stream))
                    return new Bitmap(loaded);
            }
        }
    }
}
