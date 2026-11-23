using System;
using System.Runtime.InteropServices;
using System.Text;

namespace osu_trainer.Mania
{
    /// <summary>
    /// Result of one rice7 analysis. Ratings are on xdc1's scale; a 7K chart runs from about 29 at 0th dan to 78 at
    /// Stellium.
    /// </summary>
    public sealed class Rice7Result
    {
        public bool Ok { get; private set; }
        public string Error { get; private set; }

        /// <summary>The easier of the two ways to hold the middle key.</summary>
        public double Rating { get; private set; }

        /// <summary>The rating with the middle key on the left thumb.</summary>
        public double ThumbLeft { get; private set; }

        /// <summary>The rating with the middle key on the right thumb.</summary>
        public double ThumbRight { get; private set; }

        public int Notes { get; private set; }

        /// <summary>How many of the notes are long notes. rice7 reads each as a tap on its head.</summary>
        public int Holds { get; private set; }

        internal static Rice7Result Failed(string error) => new Rice7Result { Ok = false, Error = error };

        internal static Rice7Result Success(double rating, double thumbLeft, double thumbRight, int notes, int holds) => new Rice7Result
        {
            Ok = true,
            Rating = rating,
            ThumbLeft = thumbLeft,
            ThumbRight = thumbRight,
            Notes = notes,
            Holds = holds,
        };
    }

    /// <summary>
    /// P/Invoke wrapper around rice7.dll: xdc1 (TheBagelOfMan's 4K algorithm, MIT licence) widened to the three and
    /// four fingers a hand uses in 7K. It rates rice only: a long note counts as a tap on its head.
    /// The DLL ships next to the mania trainer exe; its source is "Diff Calc/rice7.cpp" and "Diff Calc/rice7_dll.cpp".
    /// </summary>
    public static class Rice7
    {
        private const int ErrorLength = 256;
        private const int ExpectedParamCount = 14;
        private const int ExpectedResultSize = 328; // sizeof(rice7_result) on x64
        private const string Dll = "rice7.dll";

        /// <summary>
        /// A chart with more than this share of long notes is "long note heavy": rating it as rice says little about
        /// it. rice7 was only checked against charts with at most 5% long notes; 10% is a judgement, not a measurement.
        /// </summary>
        public const double LongNoteHeavyShare = 0.10;

        /// <summary>True when long notes are more than <see cref="LongNoteHeavyShare"/> of the chart's notes.</summary>
        public static bool IsLongNoteHeavy(int notes, int holds) => notes > 0 && holds > LongNoteHeavyShare * notes;

        // Mirrors struct rice7_result in rice7_dll.cpp.
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeResult
        {
            public double rating;
            public double thumb_left;
            public double thumb_right;
            public double first_ms;
            public double last_ms;
            public int keys;
            public int n_notes;
            public int n_holds;
            public int n_points;
            public IntPtr grid;
            public IntPtr graph;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ErrorLength)]
            public byte[] error;
        }

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int rice7_param_count();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr rice7_param_name(int i);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern double rice7_param_default(int i);

        // parameters is always null (the settings the ratings were published with), thumb 0 (the easier hold) and
        // want_graph 0, so the result owns no memory and rice7_free_result is never needed.
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int rice7_analyse(byte[] text, long len, double rate, IntPtr parameters, int nParams, int thumb, int wantGraph, out NativeResult result);

        private static readonly Lazy<string> loadError = new Lazy<string>(CheckLibrary);

        /// <summary>True when rice7.dll was found and is the build the 7K dan thresholds were made with.</summary>
        public static bool IsAvailable => loadError.Value == null;

        /// <summary>Why the library can't be used, or null when it can.</summary>
        public static string LoadError => loadError.Value;

        private static string CheckLibrary()
        {
            try
            {
                int count = rice7_param_count();
                if (count != ExpectedParamCount)
                    return $"rice7.dll reports {count} settings but this build expects {ExpectedParamCount}";
                if (Marshal.SizeOf(typeof(NativeResult)) != ExpectedResultSize)
                    return "rice7 result layout mismatch (mania-trainer must run as a 64-bit process)";

                // The dan thresholds belong to the settings the ratings were published with: xdc1's own, with the
                // roll on. A DLL built with another default would rate on another scale.
                for (int i = 0; i < count; i++)
                {
                    if (Marshal.PtrToStringAnsi(rice7_param_name(i)) == "roll")
                        return rice7_param_default(i) == 1.0 ? null : "rice7.dll was built with other settings than the 7K dan thresholds";
                }
                return "rice7.dll is missing the roll setting";
            }
            catch (DllNotFoundException)
            {
                return "rice7.dll was not found next to the mania trainer exe";
            }
            catch (BadImageFormatException)
            {
                return "rice7.dll could not be loaded (it must be the 64-bit build)";
            }
            catch (EntryPointNotFoundException)
            {
                return "rice7.dll is missing an expected function";
            }
        }

        /// <summary>
        /// Analyses the text of a 7K .osu file. Thread-safe (the DLL allows concurrent calls).
        /// </summary>
        /// <param name="osuText">Full .osu file contents.</param>
        /// <param name="rate">Music rate; 1.0 when the text has already been rate-adjusted.</param>
        public static Rice7Result Analyse(string osuText, double rate = 1.0)
        {
            if (!IsAvailable)
                return Rice7Result.Failed(LoadError);
            if (string.IsNullOrEmpty(osuText))
                return Rice7Result.Failed("empty beatmap");
            // rice7 keeps xdc1's time grid, so the same limit on a chart's length applies (see Xdc1.MaxChartTimeMs)
            long latest = Xdc1.LatestHitObjectTime(osuText);
            if ((rate > 0 ? latest / rate : latest) > Xdc1.MaxChartTimeMs)
                return Rice7Result.Failed("the chart is too long to rate (a note is past the 6 hour mark)");

            byte[] bytes = Encoding.UTF8.GetBytes(osuText);
            int status = rice7_analyse(bytes, bytes.LongLength, rate, IntPtr.Zero, 0, 0, 0, out NativeResult result);
            if (status != 0)
                return Rice7Result.Failed(Xdc1.DecodeError(result.error));
            if (result.keys != 7)
                return Rice7Result.Failed($"not a 7K chart ({result.keys}K)");
            return Rice7Result.Success(result.rating, result.thumb_left, result.thumb_right, result.n_notes, result.n_holds);
        }
    }
}
