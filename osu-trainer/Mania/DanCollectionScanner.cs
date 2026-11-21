using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace osu_trainer.Mania
{
    /// <summary>A 4K chart found by <see cref="DanCollectionScanner"/>, rated at 1.0x.</summary>
    public sealed class RatedChart
    {
        public RatedChart(string path, string md5, double rating)
        {
            Path = path;
            Md5 = md5;
            Rating = rating;
        }

        public string Path { get; }

        /// <summary>Lower-case hex MD5 of the .osu file, which is how osu! collections refer to a beatmap.</summary>
        public string Md5 { get; }

        public double Rating { get; }
    }

    public sealed class DanScanResult
    {
        public List<RatedChart> Charts { get; } = new List<RatedChart>();

        /// <summary>4K charts xdc1 couldn't rate.</summary>
        public int Unrated { get; set; }

        /// <summary>.osu files found, of any mode.</summary>
        public int Files { get; set; }

        /// <summary>Files read and rated this time rather than taken from the cache.</summary>
        public int Analysed { get; set; }

        public TimeSpan Elapsed { get; set; }

        public bool Cancelled { get; set; }
    }

    /// <summary>
    /// Finds every 4K osu!mania chart under a Songs folder and rates it with xdc1 at 1.0x, in parallel.
    ///
    /// Results are cached in %LOCALAPPDATA%\mania-trainer\dan-collections-cache.tsv, keyed by path, modified time and
    /// size, for the xdc1.dll they were made with, so a second scan only reads new or changed files.
    /// A chart is the raw .osu file as osu! has it, the way the XDC1 Analyzer rates it.
    /// </summary>
    public sealed class DanCollectionScanner
    {
        private const string CacheMagic = "mania-trainer dan cache";
        private const int CacheFormat = 2; // 2: charts past 260 characters are read (1 cached them as skipped)

        private readonly string songsFolder;
        private int found, done, total;
        private volatile string phase = "Starting…";

        public DanCollectionScanner(string songsFolder)
        {
            this.songsFolder = songsFolder;
        }

        /// <summary>What the scan is doing, for the window to show (read from any thread).</summary>
        public string Phase => phase;
        public int Found => Volatile.Read(ref found);
        public int Done => Volatile.Read(ref done);
        public int Total => Volatile.Read(ref total);

        public static string CachePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mania-trainer", "dan-collections-cache.tsv");

        private enum Kind { Chart, Skip, Failed }

        private sealed class Entry
        {
            public long Ticks;
            public long Size;
            public Kind Kind;
            public string Md5;
            public double Rating;
        }

        private struct FileItem
        {
            public string Path;
            public long Ticks;
            public long Size;
        }

        /// <summary>Runs the scan on the calling thread. Never throws for a single bad file.</summary>
        public DanScanResult Run(CancellationToken cancel)
        {
            var clock = Stopwatch.StartNew();
            var result = new DanScanResult();
            if (!Xdc1.IsAvailable)
                throw new InvalidOperationException(Xdc1.LoadError);

            string fingerprint = DllFingerprint();
            phase = "Reading the cache…";
            var cache = LoadCache(fingerprint);

            phase = "Finding charts…";
            var files = Walk(cancel);
            result.Files = files.Count;

            var entries = new ConcurrentDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            var todo = new List<FileItem>();
            foreach (var f in files)
            {
                if (cache.TryGetValue(f.Path, out Entry e) && e.Ticks == f.Ticks && e.Size == f.Size)
                    entries[f.Path] = e;
                else
                    todo.Add(f);
            }

            phase = "Rating charts…";
            Volatile.Write(ref total, todo.Count);
            if (!cancel.IsCancellationRequested && todo.Count > 0)
            {
                var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount) };
                try
                {
                    Parallel.ForEach(todo, options, (f, state) =>
                    {
                        if (cancel.IsCancellationRequested)
                        {
                            state.Stop();
                            return;
                        }
                        entries[f.Path] = Analyse(f);
                        Interlocked.Increment(ref done);
                    });
                }
                catch (AggregateException) { }
            }
            result.Cancelled = cancel.IsCancellationRequested;
            result.Analysed = Done;

            phase = "Saving the cache…";
            if (result.Cancelled)
            {
                // the walk or the rating stopped part way: keep what the old cache knew about the rest (an entry
                // is only used while the file's time and size still match, so a stale one does no harm)
                foreach (var kv in cache)
                    entries.TryAdd(kv.Key, kv.Value);
            }
            SaveCache(fingerprint, entries);

            foreach (var f in files)
            {
                if (!entries.TryGetValue(f.Path, out Entry e))
                    continue;
                if (e.Kind == Kind.Chart)
                    result.Charts.Add(new RatedChart(f.Path, e.Md5, e.Rating));
                else if (e.Kind == Kind.Failed)
                    result.Unrated++;
            }
            result.Elapsed = clock.Elapsed;
            return result;
        }

        private List<FileItem> Walk(CancellationToken cancel)
        {
            var files = new List<FileItem>();
            var stack = new Stack<DirectoryInfo>();
            stack.Push(new DirectoryInfo(songsFolder));
            while (stack.Count > 0 && !cancel.IsCancellationRequested)
            {
                var dir = stack.Pop();
                try
                {
                    foreach (var info in dir.EnumerateFileSystemInfos())
                    {
                        try
                        {
                            if ((info.Attributes & FileAttributes.Directory) != 0)
                            {
                                if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                                    stack.Push((DirectoryInfo)info);
                            }
                            else if (info.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                            {
                                files.Add(new FileItem { Path = info.FullName, Ticks = info.LastWriteTimeUtc.Ticks, Size = ((FileInfo)info).Length });
                                Interlocked.Increment(ref found);
                            }
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (System.Security.SecurityException) { }
            }
            return files;
        }

        private static Entry Analyse(FileItem f)
        {
            var e = new Entry { Ticks = f.Ticks, Size = f.Size, Kind = Kind.Skip };
            byte[] data;
            try
            {
                data = ReadIf4K(f.Path);
            }
            catch (Exception)
            {
                return e; // unreadable: not counted, like any other file that isn't a 4K chart
            }
            if (data == null)
                return e;

            using (var md5 = MD5.Create())
                e.Md5 = string.Concat(md5.ComputeHash(data).Select(b => b.ToString("x2")));
            try
            {
                var r = Xdc1.Analyse(Encoding.UTF8.GetString(data));
                if (r.Ok && !double.IsNaN(r.Rating) && !double.IsInfinity(r.Rating))
                {
                    e.Kind = Kind.Chart;
                    e.Rating = r.Rating;
                }
                else
                    e.Kind = Kind.Failed;
            }
            catch (Exception)
            {
                e.Kind = Kind.Failed;
            }
            return e;
        }

        // ---- telling 4K mania apart (the same test as the XDC1 Analyzer) -------------------------------------

        private const int HeadBytes = 16384;
        private static readonly Regex modeRe = new Regex(@"^[ \t]*Mode[ \t]*:[ \t]*(\d+)", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        private static readonly Regex csRe = new Regex(@"^[ \t]*CircleSize[ \t]*:[ \t]*([-+0-9.]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        private static readonly Regex bodyRe = new Regex(@"^[ \t]*\[(?:Events|TimingPoints|HitObjects)\]", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>The file's bytes when it's a 4K osu!mania chart, else null. Reads only the header of anything else.</summary>
        public static byte[] ReadIf4K(string path)
        {
            using (var f = OpenRead(path))
            {
                var head = new byte[HeadBytes];
                int n = 0, r;
                while (n < HeadBytes && (r = f.Read(head, n, HeadBytes - n)) > 0)
                    n += r;
                bool full = n < HeadBytes;
                var ms = new MemoryStream();
                ms.Write(head, 0, n);
                // ASCII-compatible reading is all the header test needs; Latin-1 maps every byte to one char
                string text = Latin1.GetString(head, 0, n);
                if (!full && !bodyRe.IsMatch(text))
                {
                    f.CopyTo(ms);
                    full = true;
                    text = Latin1.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                }
                if (!HeaderIs4K(text))
                    return null;
                if (!full)
                    f.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        /// <summary>
        /// The path with the \\?\ prefix when it's too long for the classic 260-character limit, which .NET Framework
        /// keeps unless Windows has long paths turned on. Songs folders often have charts past it.
        /// </summary>
        private static FileStream OpenRead(string path)
        {
            const FileShare share = FileShare.ReadWrite | FileShare.Delete;
            string prefixed = LongPath(path);
            if (!ReferenceEquals(prefixed, path))
            {
                try
                {
                    return new FileStream(prefixed, FileMode.Open, FileAccess.Read, share);
                }
                catch (ArgumentException) { }      // legacy path handling, which doesn't take \\?\
                catch (NotSupportedException) { }
            }
            return new FileStream(path, FileMode.Open, FileAccess.Read, share);
        }

        private static string LongPath(string path)
        {
            if (path.Length < 240 || path.StartsWith(@"\\?\", StringComparison.Ordinal))
                return path;
            return path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path.Substring(2) : @"\\?\" + path;
        }

        public static bool HeaderIs4K(string head)
        {
            var body = bodyRe.Match(head);
            if (body.Success)
                head = head.Substring(0, body.Index);
            var mode = modeRe.Match(head);
            var cs = csRe.Match(head);
            if (!mode.Success || !cs.Success)
                return false;
            if (!long.TryParse(mode.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long m) || m != 3)
                return false;
            if (!double.TryParse(cs.Groups[1].Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double keys))
                return false;
            return Math.Floor(keys + 0.5) == 4;
        }

        // ---- cache ----------------------------------------------------------------------------------------------

        private static string DllFingerprint()
        {
            string dll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xdc1.dll");
            try
            {
                using (var md5 = MD5.Create())
                using (var f = File.OpenRead(dll))
                    return string.Concat(md5.ComputeHash(f).Select(b => b.ToString("x2")));
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private static Dictionary<string, Entry> LoadCache(string fingerprint)
        {
            var cache = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var reader = new StreamReader(CachePath, Encoding.UTF8))
                {
                    string header = reader.ReadLine();
                    if (header != $"{CacheMagic}\t{CacheFormat}\t{fingerprint}")
                        return cache; // another format or another xdc1.dll: rate everything again
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var p = line.Split('\t');
                        if (p.Length != 6
                            || !long.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)
                            || !long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long size))
                            continue;
                        var e = new Entry { Ticks = ticks, Size = size, Md5 = p[4].Length == 0 ? null : p[4] };
                        switch (p[3])
                        {
                            case "c":
                                if (!double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out e.Rating))
                                    continue;
                                e.Kind = Kind.Chart;
                                break;
                            case "f":
                                e.Kind = Kind.Failed;
                                break;
                            case "s":
                                e.Kind = Kind.Skip;
                                break;
                            default:
                                continue;
                        }
                        cache[p[0]] = e;
                    }
                }
            }
            catch (Exception)
            {
                cache.Clear();
            }
            return cache;
        }

        private static void SaveCache(string fingerprint, IDictionary<string, Entry> entries)
        {
            string path = CachePath;
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var w = new StreamWriter(temp, false, new UTF8Encoding(false)))
                {
                    w.WriteLine($"{CacheMagic}\t{CacheFormat}\t{fingerprint}");
                    foreach (var kv in entries)
                    {
                        var e = kv.Value;
                        string kind = e.Kind == Kind.Chart ? "c" : e.Kind == Kind.Failed ? "f" : "s";
                        string rating = e.Kind == Kind.Chart ? e.Rating.ToString("R", CultureInfo.InvariantCulture) : "";
                        w.Write(kv.Key);
                        w.Write('\t');
                        w.Write(e.Ticks.ToString(CultureInfo.InvariantCulture));
                        w.Write('\t');
                        w.Write(e.Size.ToString(CultureInfo.InvariantCulture));
                        w.Write('\t');
                        w.Write(kind);
                        w.Write('\t');
                        w.Write(e.Md5 ?? "");
                        w.Write('\t');
                        w.WriteLine(rating);
                    }
                }
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            catch (Exception)
            {
                try { File.Delete(temp); } catch { }
                // the cache only saves time; a scan that can't save it still worked
            }
        }
    }
}
