using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace osu_trainer.Mania
{
    /// <summary>
    /// Result of one xdc1 analysis. Ratings are on xdc1's own scale (roughly 28 for 1st dan up to 108 for Kappa).
    /// </summary>
    public sealed class Xdc1Result
    {
        public bool Ok { get; private set; }
        public string Error { get; private set; }
        public double Rating { get; private set; }
        public double RatingRice { get; private set; }
        public double[] Skillsets { get; private set; }
        public int Notes { get; private set; }
        public int Holds { get; private set; }

        internal static Xdc1Result Failed(string error) => new Xdc1Result { Ok = false, Error = error, Skillsets = new double[0] };

        internal static Xdc1Result Success(double rating, double ratingRice, double[] skillsets, int notes, int holds) => new Xdc1Result
        {
            Ok = true,
            Rating = rating,
            RatingRice = ratingRice,
            Skillsets = skillsets,
            Notes = notes,
            Holds = holds,
        };
    }

    /// <summary>
    /// P/Invoke wrapper around xdc1.dll, TheBagelOfMan's 4K osu!mania difficulty algorithm (XDC1, MIT licence).
    /// The DLL ships next to the mania trainer exe; its C side is "Diff Calc/xdc1_dll.cpp".
    /// </summary>
    public static class Xdc1
    {
        public const int SkillsetCount = 7;
        private const int ErrorLength = 256;
        private const int ExpectedResultSize = 376; // sizeof(xdc1_result) on x64
        private const string Dll = "xdc1.dll";

        // Mirrors struct xdc1_result in xdc1_dll.cpp. The layout depends on SkillsetCount, which
        // CheckLibrary() verifies against the DLL before anything is analysed.
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeResult
        {
            public double rating;
            public double rating_rice;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = SkillsetCount)]
            public double[] skillsets;
            public double first_ms;
            public double last_ms;
            public int n_notes;
            public int n_holds;
            public int n_points;
            public IntPtr grid;
            public IntPtr graph;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ErrorLength)]
            public byte[] error;
        }

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int xdc1_skillset_count();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr xdc1_skillset_name(int i);

        // want_graph is always 0 here, so the result owns no memory and xdc1_free_result is never needed.
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int xdc1_analyse(byte[] text, long len, double rate, int wantGraph, out NativeResult result);

        private static readonly string[] fallbackSkillsetNames =
            { "stream", "jumpstream", "handstream", "speedjack", "chordjack", "technical", "release" };

        private static readonly Lazy<string> loadError = new Lazy<string>(CheckLibrary);
        private static string[] skillsetNames = fallbackSkillsetNames;

        /// <summary>True when xdc1.dll was found and matches the layout this build was written for.</summary>
        public static bool IsAvailable => loadError.Value == null;

        /// <summary>Why the library can't be used, or null when it can.</summary>
        public static string LoadError => loadError.Value;

        public static string[] SkillsetNames
        {
            get
            {
                _ = loadError.Value;
                return skillsetNames;
            }
        }

        private static string CheckLibrary()
        {
            try
            {
                int count = xdc1_skillset_count();
                if (count != SkillsetCount)
                    return $"xdc1.dll reports {count} skillsets but this build expects {SkillsetCount}";
                if (Marshal.SizeOf(typeof(NativeResult)) != ExpectedResultSize)
                    return "xdc1 result layout mismatch (mania-trainer must run as a 64-bit process)";

                var names = new string[SkillsetCount];
                for (int i = 0; i < SkillsetCount; i++)
                {
                    string name = Marshal.PtrToStringAnsi(xdc1_skillset_name(i));
                    names[i] = string.IsNullOrEmpty(name) ? fallbackSkillsetNames[i] : name;
                }
                skillsetNames = names;
                return null;
            }
            catch (DllNotFoundException)
            {
                return "xdc1.dll was not found next to the mania trainer exe";
            }
            catch (BadImageFormatException)
            {
                return "xdc1.dll could not be loaded (it must be the 64-bit build)";
            }
            catch (EntryPointNotFoundException)
            {
                return "xdc1.dll is missing an expected function";
            }
        }

        /// <summary>
        /// The latest note time (after the rate) a chart may reach and still be rated. xdc1 works on a grid that
        /// runs from 0 ms to the chart's last note, so its memory grows with that time (about 13 MB per 1,000 s):
        /// a corrupt or joke time far in the future would make every rating call allocate gigabytes.
        /// </summary>
        public const long MaxChartTimeMs = 6L * 60 * 60 * 1000; // 6 hours

        /// <summary>
        /// Analyses the text of a 4K .osu file. Thread-safe (the DLL allows concurrent calls).
        /// </summary>
        /// <param name="osuText">Full .osu file contents.</param>
        /// <param name="rate">Music rate; 1.0 when the text has already been rate-adjusted.</param>
        public static Xdc1Result Analyse(string osuText, double rate = 1.0)
        {
            if (!IsAvailable)
                return Xdc1Result.Failed(LoadError);
            if (string.IsNullOrEmpty(osuText))
                return Xdc1Result.Failed("empty beatmap");
            long latest = LatestHitObjectTime(osuText);
            if ((rate > 0 ? latest / rate : latest) > MaxChartTimeMs)
                return Xdc1Result.Failed("the chart is too long to rate (a note is past the 6 hour mark)");

            byte[] bytes = Encoding.UTF8.GetBytes(osuText);
            int status = xdc1_analyse(bytes, bytes.LongLength, rate, 0, out NativeResult result);
            if (status != 0)
                return Xdc1Result.Failed(DecodeError(result.error));

            var skillsets = new double[SkillsetCount];
            if (result.skillsets != null)
                Array.Copy(result.skillsets, skillsets, Math.Min(SkillsetCount, result.skillsets.Length));
            return Xdc1Result.Success(result.rating, result.rating_rice, skillsets, result.n_notes, result.n_holds);
        }

        /// <summary>
        /// The latest note head or hold end in [HitObjects], read the way xdc1 reads the numbers. xdc1 then narrows
        /// them to int, so a time outside the int range would wrap (a hold ending at -2,294,967,296 ms lands at
        /// +2,000,000,000 ms); such a time counts as later than any limit.
        /// </summary>
        private static long LatestHitObjectTime(string osuText)
        {
            long latest = 0;
            bool inHitObjects = false;
            using (var reader = new StringReader(osuText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (t.StartsWith("["))
                    {
                        inHitObjects = t == "[HitObjects]";
                        continue;
                    }
                    if (!inHitObjects)
                        continue;
                    string[] f = t.Split(',');
                    if (f.Length < 4)
                        continue;
                    if (LeadingInteger(f[2], out long time))
                        latest = Math.Max(latest, AsXdc1Time(time));
                    if (f.Length > 5 && LeadingInteger(f[3], out long type) && (type & 128) != 0 && LeadingInteger(f[5], out long end))
                        latest = Math.Max(latest, AsXdc1Time(end));
                }
            }
            return latest;
        }

        private static long AsXdc1Time(long time) => time < int.MinValue || time > int.MaxValue ? long.MaxValue : time;

        /// <summary>
        /// Like C's strtoll, which xdc1 uses: optional whitespace and sign, then digits, stopping at the first other
        /// character ("1250.5" reads as 1250). Saturates instead of overflowing.
        /// </summary>
        private static bool LeadingInteger(string s, out long value)
        {
            value = 0;
            int i = 0;
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
            bool negative = false;
            if (i < s.Length && (s[i] == '+' || s[i] == '-'))
            {
                negative = s[i] == '-';
                i++;
            }
            int start = i;
            long v = 0;
            for (; i < s.Length && s[i] >= '0' && s[i] <= '9'; i++)
            {
                int digit = s[i] - '0';
                v = v > (long.MaxValue - digit) / 10 ? long.MaxValue : v * 10 + digit;
            }
            if (i == start)
                return false;
            value = negative ? -v : v;
            return true;
        }

        private static string DecodeError(byte[] error)
        {
            if (error == null)
                return "unknown error";
            int length = Array.IndexOf(error, (byte)0);
            if (length < 0)
                length = error.Length;
            string message = Encoding.UTF8.GetString(error, 0, length).Trim();
            return message.Length == 0 ? "unknown error" : message;
        }
    }
}
