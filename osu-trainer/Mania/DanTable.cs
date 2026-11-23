using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;

namespace osu_trainer.Mania
{
    /// <summary>
    /// One dan of a key mode's dan table: 1st to 10th and the EXTRA dans Alpha to Kappa in 4K; 0th to 10th, then
    /// Gamma, Azimuth, Zenith and Stellium in 7K. A dan belongs to its table: 4K's Gamma and 7K's Gamma are two dans.
    /// </summary>
    public sealed class DanLevel
    {
        internal DanLevel(DanSystem system, int index, string name, string displayName, double threshold, Color color, string iconResource, bool isExtra)
        {
            System = system;
            Index = index;
            Name = name;
            DisplayName = displayName;
            Threshold = threshold;
            Color = color;
            TextColor = ReadableOnDark(color);
            IconResource = iconResource;
            IsExtra = isExtra;
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

        /// <summary>The table this dan is in (<see cref="DanTable.FourKey"/> or <see cref="DanTable.SevenKey"/>).</summary>
        public DanSystem System { get; }

        /// <summary>Position in its table: 0 for the lowest dan (4K's 1st, 7K's 0th).</summary>
        public int Index { get; }

        /// <summary>"1st" ... "10th", "Alpha" ... "Kappa"; in 7K "0th" ... "10th", "Gamma" ... "Stellium".</summary>
        public string Name { get; }

        /// <summary>"1st Dan" ... "10th Dan", "Alpha" ... "Kappa".</summary>
        public string DisplayName { get; }

        /// <summary>Lowest rating (at 1.0x, from the table's calculator) that counts as this dan.</summary>
        public double Threshold { get; }

        /// <summary>The dan's colour, as its dan button is filled.</summary>
        public Color Color { get; }

        /// <summary>
        /// The dan's colour for text and bars on the dark window: <see cref="Color"/>, except that a colour too
        /// dark to read there (Kappa's black) is shown as a dark grey.
        /// </summary>
        public Color TextColor { get; }

        internal string IconResource { get; }

        /// <summary>True for the dans after 10th, which the dan menu lists apart.</summary>
        public bool IsExtra { get; }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// The dans of one key mode and the calculator its ratings come from. Every question about a dan ("which dan is
    /// this rating", "what comes next") is asked of a table, so a 4K rating can't be read against 7K dans.
    /// </summary>
    public sealed class DanSystem
    {
        internal struct Spec
        {
            public string Name, DisplayName, Icon;
            public double Threshold;
            public Color Color;
            public bool Extra;

            public Spec(string name, string displayName, double threshold, Color color, string icon = null, bool extra = false)
            {
                Name = name;
                DisplayName = displayName;
                Threshold = threshold;
                Color = color;
                Icon = icon;
                Extra = extra;
            }
        }

        internal DanSystem(int keys, string engine, Spec[] specs)
        {
            Keys = keys;
            Engine = engine;
            var levels = new DanLevel[specs.Length];
            for (int i = 0; i < specs.Length; i++)
                levels[i] = new DanLevel(this, i, specs[i].Name, specs[i].DisplayName, specs[i].Threshold, specs[i].Color, specs[i].Icon, specs[i].Extra);
            Levels = levels;
        }

        /// <summary>The key count this table is for: 4 or 7.</summary>
        public int Keys { get; }

        /// <summary>"xdc1" (4K) or "rice7" (7K): the calculator, as the window and the collection names call it.</summary>
        public string Engine { get; }

        /// <summary>The dans, easiest first.</summary>
        public IReadOnlyList<DanLevel> Levels { get; }

        /// <summary>What a rating under the lowest dan is called: "Below 1st Dan" (4K), "Below 0th Dan" (7K).</summary>
        public string BelowLowest => "Below " + Levels[0].DisplayName;

        /// <summary>The same for the badge in the main window: "Below 1st", "Below 0th".</summary>
        public string BelowLowestShort => "Below " + Levels[0].Name;

        /// <summary>
        /// The dan a rating corresponds to, or null when it is below the lowest dan's threshold.
        /// The rating is rounded to the two decimals the UI shows, so the dan always agrees with the number.
        /// </summary>
        public DanLevel Classify(double rating)
        {
            if (double.IsNaN(rating) || double.IsInfinity(rating))
                return null;
            double shown = DanTable.RoundForDisplay(rating);
            DanLevel result = null;
            foreach (var level in Levels)
            {
                if (shown >= DanTable.RoundForDisplay(level.Threshold))
                    result = level;
                else
                    break;
            }
            return result;
        }

        /// <summary>The dan with this name ("1st" ... "Kappa", or "1st Dan" ...; any case), or null.</summary>
        public DanLevel FindByName(string name)
        {
            string wanted = name?.Trim();
            foreach (var level in Levels)
                if (string.Equals(level.Name, wanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(level.DisplayName, wanted, StringComparison.OrdinalIgnoreCase))
                    return level;
            return null;
        }

        /// <summary>
        /// The next harder dan, the lowest dan when <paramref name="level"/> is null, or null after the highest (and
        /// for a dan of another table).
        /// </summary>
        public DanLevel Next(DanLevel level)
        {
            if (level != null && !ReferenceEquals(level.System, this))
                return null;
            int next = level == null ? 0 : level.Index + 1;
            return next < Levels.Count ? Levels[next] : null;
        }

        /// <summary>
        /// How far a rating has got from its dan's threshold towards the next one, 0 to 1.
        /// 1 at the highest dan and above; for ratings below the lowest dan, measured from 0.
        /// </summary>
        public float ProgressToNext(double rating)
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

        public override string ToString() => Keys + "K";
    }

    /// <summary>
    /// The dan tables.
    ///
    /// 4K (xdc1): each threshold is the mean xdc1 rating (at 1.0x) of every chart of that dan in the user's
    /// "Dan Beatmaps" folder (1st to Eta) and "More Dans" folder (Theta, Iota and Kappa, from Dan ~ REFORM ~
    /// FINAL (Ext.)): the full dan course, its Jack, Tech, Speed and Stamina maps, and every sample
    /// course and sample map for that dan. Intro dans are not included. 246 charts were rated in total.
    /// Regenerate with tools/calibrate_dans.py; per-chart ratings are in tools/dan_calibration.csv.
    ///
    /// 7K (rice7, rice only): each threshold is where a smooth curve (a cubic in the dan number, least squares)
    /// through the rice7 ratings of the 60 charts of the 7K Regular Dan practice packs (0th to Stellium, one Jack,
    /// Speed, Stream and Tech chart for each dan) passes that dan. With four charts to a dan the plain means
    /// wobble, so the curve stands in for them. Regenerate with tools/calibrate_dans7.py, which writes the
    /// thresholds to tools/dan7_curve.json and the per-chart ratings to tools/dan7_calibration.csv.
    ///
    /// In both, a chart is shown as the highest dan whose threshold its rating reaches: a chart rated like the
    /// average chart of a dan sits at the very start of that dan.
    /// </summary>
    public static class DanTable
    {
        public static readonly DanSystem FourKey = new DanSystem(4, "xdc1", new[]
        {
            new DanSystem.Spec("1st",     "1st Dan",  28.7147, Color.FromArgb(66, 144, 251)),
            new DanSystem.Spec("2nd",     "2nd Dan",  32.2334, Color.FromArgb(79, 192, 255)),
            new DanSystem.Spec("3rd",     "3rd Dan",  33.5844, Color.FromArgb(79, 255, 213)),
            new DanSystem.Spec("4th",     "4th Dan",  37.3902, Color.FromArgb(124, 255, 79)),
            new DanSystem.Spec("5th",     "5th Dan",  42.4576, Color.FromArgb(246, 240, 92)),
            new DanSystem.Spec("6th",     "6th Dan",  45.8667, Color.FromArgb(255, 184, 98)),
            new DanSystem.Spec("7th",     "7th Dan",  47.7026, Color.FromArgb(255, 128, 104)),
            new DanSystem.Spec("8th",     "8th Dan",  51.5435, Color.FromArgb(255, 78, 111)),
            new DanSystem.Spec("9th",     "9th Dan",  54.0190, Color.FromArgb(198, 69, 184)),
            new DanSystem.Spec("10th",    "10th Dan", 56.4295, Color.FromArgb(133, 120, 240)),
            // EXTRA dan colours chosen by the user: pink, dark blue, dark green, orange, purple, light blue, red,
            // dark red, grey, black
            new DanSystem.Spec("Alpha",   "Alpha",    59.1441, Color.FromArgb(255, 120, 200), "alpha",   true),
            new DanSystem.Spec("Beta",    "Beta",     61.3607, Color.FromArgb(48, 84, 220),   "beta",    true),
            new DanSystem.Spec("Gamma",   "Gamma",    64.8469, Color.FromArgb(40, 160, 72),   "gamma",   true),
            new DanSystem.Spec("Delta",   "Delta",    70.5580, Color.FromArgb(255, 146, 60),  "delta",   true),
            new DanSystem.Spec("Epsilon", "Epsilon",  79.3652, Color.FromArgb(168, 96, 240),  "epsilon", true),
            new DanSystem.Spec("Zeta",    "Zeta",     84.8299, Color.FromArgb(128, 200, 255), "zeta",    true),
            new DanSystem.Spec("Eta",     "Eta",      90.1403, Color.FromArgb(236, 54, 70),   "eta",     true),
            new DanSystem.Spec("Theta",   "Theta",    98.1759, Color.FromArgb(184, 26, 40),   "theta",   true),
            new DanSystem.Spec("Iota",    "Iota",    104.1413, Color.FromArgb(160, 160, 168), "iota",    true),
            new DanSystem.Spec("Kappa",   "Kappa",   108.2573, Color.FromArgb(16, 16, 20),    "kappa",   true),
        });

        public static readonly DanSystem SevenKey = new DanSystem(7, "rice7", new[]
        {
            // 1st to 10th keep their 4K colours. 0th and the four dans after 10th are placeholders until the user
            // picks their own (Gamma borrows 4K Gamma's green and its icon; the other three have no icon yet).
            new DanSystem.Spec("0th",      "0th Dan",  29.0978, Color.FromArgb(160, 176, 204)),
            new DanSystem.Spec("1st",      "1st Dan",  33.3770, Color.FromArgb(66, 144, 251)),
            new DanSystem.Spec("2nd",      "2nd Dan",  36.8943, Color.FromArgb(79, 192, 255)),
            new DanSystem.Spec("3rd",      "3rd Dan",  39.8096, Color.FromArgb(79, 255, 213)),
            new DanSystem.Spec("4th",      "4th Dan",  42.2828, Color.FromArgb(124, 255, 79)),
            new DanSystem.Spec("5th",      "5th Dan",  44.4739, Color.FromArgb(246, 240, 92)),
            new DanSystem.Spec("6th",      "6th Dan",  46.5428, Color.FromArgb(255, 184, 98)),
            new DanSystem.Spec("7th",      "7th Dan",  48.6493, Color.FromArgb(255, 128, 104)),
            new DanSystem.Spec("8th",      "8th Dan",  50.9535, Color.FromArgb(255, 78, 111)),
            new DanSystem.Spec("9th",      "9th Dan",  53.6151, Color.FromArgb(198, 69, 184)),
            new DanSystem.Spec("10th",     "10th Dan", 56.7942, Color.FromArgb(133, 120, 240)),
            new DanSystem.Spec("Gamma",    "Gamma",    60.6506, Color.FromArgb(40, 160, 72),   "gamma", true),
            new DanSystem.Spec("Azimuth",  "Azimuth",  65.3443, Color.FromArgb(255, 146, 60),  null,    true),
            new DanSystem.Spec("Zenith",   "Zenith",   71.0352, Color.FromArgb(236, 54, 70),   null,    true),
            new DanSystem.Spec("Stellium", "Stellium", 77.8831, Color.FromArgb(168, 96, 240),  null,    true),
        });

        /// <summary>Every table, 4K first.</summary>
        public static readonly IReadOnlyList<DanSystem> Systems = new[] { FourKey, SevenKey };

        /// <summary>The table for a key count, or null when that key count has no calculator (anything but 4K and 7K).</summary>
        public static DanSystem ForKeys(int keys) => keys == 4 ? FourKey : keys == 7 ? SevenKey : null;

        /// <summary>A rating (or threshold) rounded to the two decimals the UI shows.</summary>
        public static double RoundForDisplay(double rating) => Math.Round(rating, 2, MidpointRounding.AwayFromZero);

        /// <summary>A rating (or threshold) as the UI shows it, e.g. "42.46". Classify works from the same value.</summary>
        public static string FormatRating(double rating) => RoundForDisplay(rating).ToString("0.00", CultureInfo.InvariantCulture);

        private static readonly ConcurrentDictionary<string, Image> iconCache = new ConcurrentDictionary<string, Image>();

        /// <summary>A dan's own icon (64x64, transparent), or null for a dan without one.</summary>
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
