using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace osu_trainer.Mania
{
    public enum LnConversion
    {
        None,
        /// <summary>Every tap becomes a long note; existing long notes are kept.</summary>
        FullLn,
        /// <summary>Every long note becomes a tap at its head.</summary>
        NoLn,
    }

    /// <summary>
    /// Long note conversions for osu!mania, applied to the text of a .osu file after every other edit
    /// (rate change, spinner removal), so the dan rating and the exported map always see the same chart.
    ///
    /// Full LN: in each column, a tap becomes a hold that ends shortly before the next object in that column.
    /// The gap left before the next object is a quarter beat (at the next object's timing point), but at most half
    /// the time between the two notes: osu!lazer's Invert rule. The quarter beat is never taken as less than
    /// 25 ms, so timing points with absurdly short beats (used for visual effects) can't leave an unplayable gap.
    /// The last note of a column becomes a one-beat hold, clamped to 100-1000 ms. A note followed by a break
    /// period is treated the same way and its hold ends before the break starts, rather than lasting through it.
    /// A tap whose next object is too close to fit a hold (under 2 ms) stays a tap.
    ///
    /// No LN: a hold becomes a tap at the hold's head, keeping its hitsounds.
    /// </summary>
    public static class ManiaTransforms
    {
        private const int TypeCircle = 1;
        private const int TypeHold = 128;
        private const string DefaultHitSample = "0:0:0:0:";
        private const double LastNoteMinMs = 100;
        private const double LastNoteMaxMs = 1000;
        private const double MinReleaseGapMs = 25;
        private const double FallbackBeatLength = 500; // 120 BPM, for maps without a usable timing point

        private sealed class HitObjectLine
        {
            public int LineIndex;
            public int Column;
            public int Time;
            public int Type;
            public string[] Fields;
            public bool IsHold => (Type & TypeHold) != 0;
            public bool IsTap => (Type & TypeCircle) != 0 && !IsHold;
        }

        /// <summary>Applies <paramref name="conversion"/> to the contents of a .osu file.</summary>
        public static string Apply(string osuText, LnConversion conversion)
        {
            if (conversion == LnConversion.None || string.IsNullOrEmpty(osuText))
                return osuText;

            var lines = SplitLines(osuText, out string newline);
            int keys = ReadKeyCount(lines);
            if (keys < 1)
                return osuText;

            var objects = ReadHitObjects(lines, keys);
            if (objects.Count == 0)
                return osuText;

            if (conversion == LnConversion.NoLn)
            {
                foreach (var obj in objects.Where(o => o.IsHold))
                    lines[obj.LineIndex] = HoldToTap(obj);
            }
            else
            {
                var timing = ReadBeatLengths(lines);
                var breakStarts = ReadBreakStarts(lines);
                foreach (var column in objects.GroupBy(o => o.Column))
                {
                    // stable sort: objects at the same time keep their file order
                    var ordered = column.OrderBy(o => o.Time).ToList();
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        var obj = ordered[i];
                        if (!obj.IsTap)
                            continue;
                        int? next = i + 1 < ordered.Count ? ordered[i + 1].Time : (int?)null;
                        int? end = FullLnEnd(obj.Time, next, NextBreakStart(breakStarts, obj.Time), timing);
                        if (end.HasValue)
                            lines[obj.LineIndex] = TapToHold(obj, end.Value);
                    }
                }
            }

            return string.Join(newline, lines);
        }

        /// <summary>Rewrites a .osu file in place with <paramref name="conversion"/> applied.</summary>
        public static void ApplyToFile(string path, LnConversion conversion)
        {
            if (conversion == LnConversion.None)
                return;
            string text = File.ReadAllText(path, Encoding.UTF8);
            File.WriteAllText(path, Apply(text, conversion), new UTF8Encoding(true));
        }

        /// <summary>
        /// End time of the hold a tap at <paramref name="time"/> turns into, or null to leave it a tap.
        /// <paramref name="nextTime"/> is the next object in the same column; <paramref name="breakStart"/> is the
        /// first break period starting after the tap.
        /// </summary>
        private static int? FullLnEnd(int time, int? nextTime, int? breakStart, List<KeyValuePair<int, double>> timing)
        {
            bool beforeBreak = breakStart.HasValue && (!nextTime.HasValue || breakStart.Value < nextTime.Value);
            if (nextTime.HasValue && !beforeBreak)
            {
                int gap = nextTime.Value - time;
                if (gap < 2)
                    return null;
                double beatLength = BeatLengthAt(timing, nextTime.Value);
                double release = Math.Min(gap / 2.0, Math.Max(beatLength / 4.0, MinReleaseGapMs));
                int end = time + (int)Math.Floor(gap - release);
                end = Math.Min(end, nextTime.Value - 1);
                return end > time ? end : (int?)null;
            }

            // the column's last note, or the last one before a break
            double beat = BeatLengthAt(timing, time);
            int last = time + (int)Math.Round(Math.Max(LastNoteMinMs, Math.Min(LastNoteMaxMs, beat)));
            if (beforeBreak)
                last = Math.Min(last, breakStart.Value - 1);
            return last > time ? last : (int?)null;
        }

        private static string TapToHold(HitObjectLine obj, int endTime)
        {
            // tap:  x,y,time,type,hitSound[,hitSample]
            // hold: x,y,time,type,hitSound,endTime:hitSample
            var f = obj.Fields;
            string hitSample = f.Length > 5 ? string.Join(",", f, 5, f.Length - 5) : "";
            if (string.IsNullOrWhiteSpace(hitSample))
                hitSample = DefaultHitSample;
            int type = (obj.Type & ~TypeCircle) | TypeHold;
            return string.Join(",", f[0], f[1], f[2], type.ToString(CultureInfo.InvariantCulture), f[4],
                endTime.ToString(CultureInfo.InvariantCulture) + ":" + hitSample);
        }

        private static string HoldToTap(HitObjectLine obj)
        {
            var f = obj.Fields;
            string hitSample = "";
            if (f.Length > 5)
            {
                string endAndSample = string.Join(",", f, 5, f.Length - 5);
                int colon = endAndSample.IndexOf(':');
                hitSample = colon >= 0 ? endAndSample.Substring(colon + 1) : "";
            }
            if (string.IsNullOrWhiteSpace(hitSample))
                hitSample = DefaultHitSample;
            int type = (obj.Type & ~TypeHold) | TypeCircle;
            return string.Join(",", f[0], f[1], f[2], type.ToString(CultureInfo.InvariantCulture), f[4], hitSample);
        }

        private static List<string> SplitLines(string text, out string newline)
        {
            newline = text.Contains("\r\n") ? "\r\n" : "\n";
            return text.Replace("\r\n", "\n").Split('\n').ToList();
        }

        private static string SectionOf(string line)
        {
            string t = line.Trim();
            return t.StartsWith("[") && t.EndsWith("]") ? t : null;
        }

        /// <summary>
        /// Reads a time field. Times are normally whole milliseconds; a decimal one is truncated, which is how
        /// xdc1 reads it, so the conversion and the rating agree on where every note is.
        /// </summary>
        private static bool TryReadTime(string field, out int time)
        {
            time = 0;
            if (!double.TryParse(field, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return false;
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > int.MaxValue)
                return false;
            time = (int)value;
            return true;
        }

        /// <summary>Key count from [Difficulty] CircleSize, rounded the way osu! and xdc1 read it.</summary>
        private static int ReadKeyCount(List<string> lines)
        {
            string section = null;
            foreach (var line in lines)
            {
                section = SectionOf(line) ?? section;
                if (section != "[Difficulty]")
                    continue;
                string t = line.Trim();
                if (!t.StartsWith("CircleSize:", StringComparison.Ordinal))
                    continue;
                if (double.TryParse(t.Substring("CircleSize:".Length).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double cs))
                    return (int)Math.Round(cs, MidpointRounding.AwayFromZero);
            }
            return -1;
        }

        private static List<HitObjectLine> ReadHitObjects(List<string> lines, int keys)
        {
            var objects = new List<HitObjectLine>();
            string section = null;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                section = SectionOf(line) ?? section;
                if (section != "[HitObjects]" || SectionOf(line) != null)
                    continue;
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                    continue;

                string[] f = line.Split(',');
                if (f.Length < 5)
                    continue;
                if (!double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
                    continue;
                if (!TryReadTime(f[2], out int time))
                    continue;
                if (!int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type))
                    continue;

                int column = (int)Math.Floor(x * keys / 512.0);
                column = Math.Max(0, Math.Min(keys - 1, column));
                objects.Add(new HitObjectLine { LineIndex = i, Column = column, Time = time, Type = type, Fields = f });
            }
            return objects;
        }

        /// <summary>Uninherited timing points as (time, beat length), sorted by time.</summary>
        private static List<KeyValuePair<int, double>> ReadBeatLengths(List<string> lines)
        {
            var points = new List<KeyValuePair<int, double>>();
            string section = null;
            foreach (var line in lines)
            {
                section = SectionOf(line) ?? section;
                if (section != "[TimingPoints]" || SectionOf(line) != null)
                    continue;
                string[] f = line.Split(',');
                if (f.Length < 2)
                    continue;
                if (!double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double time))
                    continue;
                if (!double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double beatLength))
                    continue;
                // field 7 is "uninherited"; old maps without it mark inherited points with a negative beat length
                bool uninherited = f.Length > 6 ? f[6].Trim() == "1" : beatLength > 0;
                if (uninherited && beatLength > 0 && !double.IsInfinity(beatLength))
                    points.Add(new KeyValuePair<int, double>((int)Math.Floor(time), beatLength));
            }
            return points.OrderBy(p => p.Key).ToList();
        }

        /// <summary>Start times of the break periods in [Events] ("2,start,end" or "Break,start,end"), sorted.</summary>
        private static List<int> ReadBreakStarts(List<string> lines)
        {
            var starts = new List<int>();
            string section = null;
            foreach (var line in lines)
            {
                section = SectionOf(line) ?? section;
                if (section != "[Events]" || SectionOf(line) != null)
                    continue;
                string[] f = line.Split(',');
                if (f.Length < 3)
                    continue;
                string kind = f[0].Trim();
                if (kind != "2" && kind != "Break")
                    continue;
                if (TryReadTime(f[1], out int start))
                    starts.Add(start);
            }
            starts.Sort();
            return starts;
        }

        /// <summary>The first break start after <paramref name="time"/>, or null if no break follows.</summary>
        private static int? NextBreakStart(List<int> breakStarts, int time)
        {
            // binary search for the first start greater than time
            int lo = 0, hi = breakStarts.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (breakStarts[mid] > time)
                    hi = mid;
                else
                    lo = mid + 1;
            }
            return lo < breakStarts.Count ? breakStarts[lo] : (int?)null;
        }

        /// <summary>Beat length in effect at <paramref name="time"/> (the first timing point applies before it starts).</summary>
        private static double BeatLengthAt(List<KeyValuePair<int, double>> timing, int time)
        {
            if (timing.Count == 0)
                return FallbackBeatLength;
            // binary search for the last point at or before time
            int lo = 0, hi = timing.Count - 1, found = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (timing[mid].Key <= time)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return timing[found].Value;
        }
    }
}
