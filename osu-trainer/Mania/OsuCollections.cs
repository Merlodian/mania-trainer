using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace osu_trainer.Mania
{
    /// <summary>One collection in osu! stable's collection.db: a name and the MD5s of the .osu files in it.</summary>
    public sealed class OsuCollection
    {
        public OsuCollection(string name, IList<string> hashes)
        {
            Name = name;
            Hashes = hashes;
        }

        /// <summary>May be null: the file can hold a null string here.</summary>
        public string Name { get; }

        /// <summary>Lower-case hex MD5 of each beatmap's .osu file; an entry may be null.</summary>
        public IList<string> Hashes { get; }
    }

    /// <summary>
    /// osu! stable's collection.db, and finding the osu! folder.
    ///
    /// The file is an int32 version, an int32 count, then for each collection an osu! string (its name), an int32
    /// count and that many osu! strings (beatmap MD5s). An osu! string is 0x00 for null, or 0x0b, the UTF-8 length as
    /// a ULEB128 and the UTF-8 bytes. All integers are little-endian.
    ///
    /// The same format and rules are used by the XDC1 Analyzer ("Diff Calc/xdc1_gui.py"), so the two programs replace
    /// each other's 4K dan collections rather than adding a second set. (The Analyzer knows nothing of the 7K ones,
    /// and keeps them like any other collection.)
    /// </summary>
    public static class OsuCollections
    {
        /// <summary>The version a new collection.db gets when osu!.db can't be read either.</summary>
        public const int FallbackVersion = 20150203;

        /// <summary>
        /// Names of the collections this program writes. 4K: "xdc1 00 - Below 1st Dan" ... "xdc1 20 - Kappa", and with
        /// the dans split in thirds "xdc1 14.1 - Low Delta", "xdc1 14.2 - Mid Delta", "xdc1 14.3 - High Delta".
        /// 7K the same way with its own calculator's name and dans: "xdc7 00 - Below 0th Dan", "xdc7 01 - 0th Dan"
        /// ... "xdc7 15 - Stellium", "xdc7 12.1 - Low Gamma".
        /// </summary>
        private static readonly Regex ourCollection = new Regex(@"^xdc[17] \d\d(\.\d)? - ", RegexOptions.CultureInvariant);

        public static readonly string[] TierNames = { "Low", "Mid", "High" };

        /// <summary>
        /// The collection name for a dan, or for below the table's lowest dan when <paramref name="dan"/> is null. The
        /// number (the dan's place in its table) keeps osu!'s alphabetical collection list in dan order, and the
        /// calculator's name keeps the 4K collections ahead of the 7K ones.
        /// </summary>
        public static string DanCollectionName(DanSystem system, DanLevel dan) =>
            dan == null
                ? system.Engine + " 00 - " + system.BelowLowest
                : string.Format(CultureInfo.InvariantCulture, "{0} {1:00} - {2}", system.Engine, dan.Index + 1, dan.DisplayName);

        /// <summary>
        /// The collection name for a third of a dan (tier 0 Low, 1 Mid, 2 High): "xdc1 14.2 - Mid Delta". The number
        /// keeps the thirds in order in osu!'s alphabetical list.
        /// </summary>
        public static string DanTierCollectionName(DanLevel dan, int tier) =>
            string.Format(CultureInfo.InvariantCulture, "{0} {1:00}.{2} - {3} {4}", dan.System.Engine, dan.Index + 1, tier + 1, TierNames[tier], dan.DisplayName);

        /// <summary>
        /// Which third of its dan a rating is in: 0 for the first third of the way to the next dan's threshold, 1 for
        /// the second and 2 for the last. Measured like the progress bar in the main window.
        /// </summary>
        public static int Tier(DanSystem system, double rating)
        {
            float progress = system.ProgressToNext(rating);
            return progress < 1f / 3 ? 0 : progress < 2f / 3 ? 1 : 2;
        }

        public static bool IsDanCollection(string name) => name != null && ourCollection.IsMatch(name);

        // ---- reading and writing ------------------------------------------------------------------------------

        public static void Read(string path, out int version, out List<OsuCollection> collections)
        {
            byte[] data = File.ReadAllBytes(path);
            int pos = 0;
            version = ReadInt32(data, ref pos);
            int count = ReadInt32(data, ref pos);
            if (count < 0)
                throw new InvalidDataException("collection.db has a negative collection count");
            collections = new List<OsuCollection>();
            for (int i = 0; i < count; i++)
            {
                string name = ReadString(data, ref pos);
                int n = ReadInt32(data, ref pos);
                if (n < 0)
                    throw new InvalidDataException("collection.db has a negative beatmap count");
                var hashes = new List<string>(Math.Min(n, 1 << 16));
                for (int j = 0; j < n; j++)
                    hashes.Add(ReadString(data, ref pos));
                collections.Add(new OsuCollection(name, hashes));
            }
            if (pos != data.Length)
                throw new InvalidDataException($"collection.db has {data.Length - pos} unexpected bytes at the end");
        }

        public static byte[] Serialize(int version, IEnumerable<OsuCollection> collections)
        {
            var list = collections.ToList();
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(version);
                w.Write(list.Count);
                foreach (var c in list)
                {
                    WriteString(w, c.Name);
                    w.Write(c.Hashes.Count);
                    foreach (var h in c.Hashes)
                        WriteString(w, h);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Puts <paramref name="danCollections"/> into osuFolder's collection.db in place of any dan collections
        /// written before, keeping every other collection as it is. The old file is copied to
        /// collection.db.xdc1-backup-(time) first, and the new one is written to a temporary file and swapped in, so
        /// a failure part way leaves the old file. A collection.db that can't be read is left alone (this throws).
        /// </summary>
        /// <returns>The backup's path (null when there was no collection.db), and how many collections were kept.</returns>
        public static (string backup, int kept) SaveDanCollections(string osuFolder, IList<OsuCollection> danCollections)
        {
            string path = Path.Combine(osuFolder, "collection.db");
            int version;
            List<OsuCollection> existing;
            string backup = null;
            if (File.Exists(path))
            {
                Read(path, out version, out existing);
                backup = path + ".xdc1-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                File.Copy(path, backup, false);
            }
            else
            {
                existing = new List<OsuCollection>();
                version = OsuDbVersion(osuFolder) ?? FallbackVersion;
            }

            var kept = existing.Where(c => !IsDanCollection(c.Name)).ToList();
            byte[] bytes = Serialize(version, kept.Concat(danCollections));

            string temp = path + ".xdc1-tmp";
            using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                f.Write(bytes, 0, bytes.Length);
                f.Flush(true);
            }
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
            return (backup, kept.Count);
        }

        private static int? OsuDbVersion(string osuFolder)
        {
            try
            {
                using (var f = File.OpenRead(Path.Combine(osuFolder, "osu!.db")))
                {
                    var b = new byte[4];
                    if (f.Read(b, 0, 4) == 4)
                        return BitConverter.ToInt32(b, 0);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        private static int ReadInt32(byte[] data, ref int pos)
        {
            if (pos + 4 > data.Length)
                throw new InvalidDataException("collection.db ends part way through");
            int v = BitConverter.ToInt32(data, pos);
            pos += 4;
            return v;
        }

        private static string ReadString(byte[] data, ref int pos)
        {
            if (pos >= data.Length)
                throw new InvalidDataException("collection.db ends part way through");
            byte tag = data[pos++];
            if (tag == 0x00)
                return null;
            if (tag != 0x0b)
                throw new InvalidDataException($"collection.db has an unexpected string marker 0x{tag:x2}");
            long length = 0;
            int shift = 0;
            while (true)
            {
                if (pos >= data.Length || shift > 35)
                    throw new InvalidDataException("collection.db has a bad string length");
                byte b = data[pos++];
                length |= (long)(b & 0x7f) << shift;
                if ((b & 0x80) == 0)
                    break;
                shift += 7;
            }
            if (length > data.Length - pos)
                throw new InvalidDataException("collection.db ends part way through");
            string s = Encoding.UTF8.GetString(data, pos, (int)length);
            pos += (int)length;
            return s;
        }

        private static void WriteString(BinaryWriter w, string s)
        {
            if (s == null)
            {
                w.Write((byte)0x00);
                return;
            }
            byte[] b = Encoding.UTF8.GetBytes(s);
            w.Write((byte)0x0b);
            uint n = (uint)b.Length;
            do
            {
                byte x = (byte)(n & 0x7f);
                n >>= 7;
                if (n != 0)
                    x |= 0x80;
                w.Write(x);
            } while (n != 0);
            w.Write(b);
        }

        // ---- finding osu! -------------------------------------------------------------------------------------

        /// <summary>True when the folder has osu!.exe or osu!.db.</summary>
        public static bool IsOsuFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return false;
            try
            {
                return File.Exists(Path.Combine(folder, "osu!.exe")) || File.Exists(Path.Combine(folder, "osu!.db"));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// The paths of the running osu!.exe processes. An osu! whose path can't be read comes back as null, so a
        /// caller can still tell osu! is running.
        /// </summary>
        public static List<string> RunningOsuExes()
        {
            var paths = new List<string>();
            foreach (var p in Process.GetProcessesByName("osu!"))
            {
                try
                {
                    paths.Add(p.MainModule.FileName);
                }
                catch (Exception)
                {
                    paths.Add(null);
                }
                finally
                {
                    p.Dispose();
                }
            }
            return paths;
        }

        /// <summary>True when an osu! running from this folder (or one whose folder can't be read) is open.</summary>
        public static bool IsOsuRunningFrom(string osuFolder)
        {
            string wanted = NormalizeFolder(osuFolder);
            return RunningOsuExes().Any(p => p == null || string.Equals(NormalizeFolder(Path.GetDirectoryName(p)), wanted, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Likely osu! stable folders, best first: a running osu!, the folder above mania trainer's Songs folder,
        /// the default install folder, and the folder osu! registered itself under.
        /// </summary>
        public static List<string> DetectOsuFolders(string songsFolderHint)
        {
            var candidates = new List<string>();
            foreach (var exe in RunningOsuExes())
                if (exe != null)
                    candidates.Add(Path.GetDirectoryName(exe));
            try
            {
                if (!string.IsNullOrWhiteSpace(songsFolderHint))
                    candidates.Add(Path.GetDirectoryName(songsFolderHint.TrimEnd('\\', '/')));
            }
            catch (ArgumentException) { }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local))
                candidates.Add(Path.Combine(local, "osu!"));
            foreach (var key in new[] { @"osu\shell\open\command", @"osustable.File.osz\shell\open\command", @"osu!\shell\open\command" })
            {
                try
                {
                    using (var k = Registry.ClassesRoot.OpenSubKey(key))
                    {
                        string exe = CommandExe(k?.GetValue(null) as string);
                        if (exe != null)
                            candidates.Add(Path.GetDirectoryName(exe));
                    }
                }
                catch (Exception) { }
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in candidates)
            {
                if (string.IsNullOrEmpty(c) || !IsOsuFolder(c))
                    continue;
                string n = NormalizeFolder(c);
                if (seen.Add(n))
                    result.Add(n);
            }
            return result;
        }

        /// <summary>The program in a shell command line: "C:\x\osu!.exe" "%1" or C:\x\osu!.exe "%1".</summary>
        public static string CommandExe(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return null;
            var m = Regex.Match(command, "^\\s*\"([^\"]+)\"|^\\s*(\\S+)");
            if (!m.Success)
                return null;
            return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        }

        /// <summary>The Songs folder osu! reads: BeatmapDirectory in osu!.(user).cfg, else Songs.</summary>
        public static string SongsFolder(string osuFolder)
        {
            string folder = "Songs";
            try
            {
                foreach (var cfg in Directory.GetFiles(osuFolder, "osu!.*.cfg"))
                {
                    if (string.Equals(Path.GetFileName(cfg), "osu!.cfg", StringComparison.OrdinalIgnoreCase))
                        continue; // the shared config: BeatmapDirectory lives in the per-user one
                    try
                    {
                        foreach (var line in File.ReadLines(cfg, Encoding.UTF8))
                        {
                            int eq = line.IndexOf('=');
                            if (eq > 0 && line.Substring(0, eq).Trim() == "BeatmapDirectory" && line.Substring(eq + 1).Trim().Length > 0)
                                folder = line.Substring(eq + 1).Trim();
                        }
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (Exception) { }
            try
            {
                return Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(osuFolder, folder));
            }
            catch (Exception)
            {
                return Path.Combine(osuFolder, "Songs");
            }
        }

        public static string NormalizeFolder(string folder)
        {
            try
            {
                return Path.GetFullPath(folder).TrimEnd('\\', '/');
            }
            catch (Exception)
            {
                return folder;
            }
        }
    }
}
