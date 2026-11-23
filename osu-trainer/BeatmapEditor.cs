using FsBeatmapProcessor;
using osu_trainer.Mania;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace osu_trainer
{
    public enum EditorState
    {
        NOT_READY,
        READY,
        GENERATING_BEATMAP
    }

    public enum BadBeatmapReason
    {
        NO_BEATMAP_LOADED,
        ERROR_LOADING_BEATMAP,
        EMPTY_MAP
    }

    public class BeatmapEditor
    {
        private MainForm mainform;
        public BadBeatmapReason NotReadyReason;

        public Beatmap OriginalBeatmap;
        public Beatmap NewBeatmap;

        public decimal StarRating { get; set; }
        public decimal AimRating { get; set; }
        public decimal SpeedRating { get; set; }

        /// <summary>Dan rating of the edited map (osu!mania only), or null when not calculated.</summary>
        public ManiaRating ManiaRating { get; private set; }

        private decimal lockedHP = 0M;
        private decimal lockedOD = 0M;
        private int lockedBpm = 200;

        private class ConcurrentRequest
        {
            private static int globalRequestCounter = -1;
            public int RequestNumber { get; set; }

            public ConcurrentRequest()
            {
                RequestNumber = ++globalRequestCounter;
            }
        }

        private class BeatmapRequest : ConcurrentRequest
        {
            public string Name { get; set; }

            public BeatmapRequest(string name) : base()
            {
                Name = name;
            }
        }

        // A copy of the edited beatmap taken on the UI thread, so the calculation never reads a map
        // that is being changed underneath it.
        private class DiffCalcRequest : ConcurrentRequest
        {
            public Beatmap Snapshot { get; }
            public LnConversion LnConversion { get; }

            public DiffCalcRequest(Beatmap snapshot, LnConversion lnConversion) : base()
            {
                Snapshot = snapshot;
                LnConversion = lnConversion;
            }
        }

        private List<BeatmapRequest> mapChangeRequests = new List<BeatmapRequest>();
        private BeatmapRequest completedBeatmapRequest = null;
        private ConcurrentRequest completedDiffCalcRequest = null;
        private bool serviceBeatmapRequestLocked = false;  // mutex for serviceBeatmapChangeRequest()
        private bool serviceDiffCalcRequestLocked = false;  // mutex for serviceDiffCalcRequest()
        private DiffCalcRequest latestDiffCalcRequest = null; // only the newest request matters

        // Dan buttons (see SetRateForDan)
        private DanRateCache danRates;                  // ratings of the loaded map at the rates searched so far
        private CancellationTokenSource danRateSearch;  // the search running now, if any
        private int danRateSearchNumber;
        private decimal? danRate;                       // the rate the last dan button set, while it is still the rate (see LockedBpmFor)

        // public getters only
        // to set, call set methods
        public bool HpIsLocked { get; private set; } = false;
        public bool OdIsLocked { get; private set; } = false;
        public bool BpmIsLocked { get; private set; } = false;

        /// <summary>The lowest rate the rate box takes. A lower one is turned down, and leaves everything as it was.</summary>
        internal const decimal MinRate = 0.1M;
        internal EditorState State { get; private set; }
        public decimal BpmRate { get; set; } = 1.0M;

        /// <summary>
        /// True while a map osu! has switched to is being read. The editor stays READY on the previous map until
        /// the new one is in, so a rate set meanwhile would be for the map on its way out.
        /// </summary>
        public bool IsLoadingBeatmap => serviceBeatmapRequestLocked;

        // Circle size is never edited: in osu!mania it is the key count.
        // Approach rate has no control either: it follows the rate for osu!standard and catch maps only
        // (see UsesApproachRate), and is left alone for mania and taiko, which don't use it.

        // Toggles
        public bool NoSpinners { get; private set; }
        public bool ChangePitch { get; private set; }
        public bool HighQualityMp3s { get; private set; }

        // osu!mania long note conversions (mutually exclusive)
        public bool FullLN { get; private set; }
        public bool NoLN { get; private set; }
        public LnConversion LnConversion => FullLN ? LnConversion.FullLn : NoLN ? LnConversion.NoLn : LnConversion.None;

        /// <summary>The long note conversion that applies to the loaded map (none unless it is osu!mania).</summary>
        private LnConversion ActiveLnConversion => OriginalBeatmap?.Mode == GameMode.Mania ? LnConversion : LnConversion.None;

        /// <summary>
        /// The long note conversion the dan rating and the dan buttons go by. Full LN doesn't count: how much harder
        /// a chart gets when every note becomes a long note is disputed among osu!mania players, so a Full LN chart
        /// is rated as the original. No LN does count. Create Map still exports the Full LN chart.
        /// </summary>
        internal LnConversion RatingLnConversion => ActiveLnConversion == LnConversion.FullLn ? LnConversion.None : ActiveLnConversion;

        private static bool UsesApproachRate(GameMode mode) => mode == GameMode.osu || mode == GameMode.CatchtheBeat;

        public BeatmapEditor(MainForm f)
        {
            mainform = f;

            // Load previously saved settings
            HpIsLocked = Properties.Settings.Default.HPLockedState;
            OdIsLocked = Properties.Settings.Default.ODLockedState;

            lockedHP = Properties.Settings.Default.LockedHPSetting;
            lockedOD = Properties.Settings.Default.LockedODSetting;

            BpmIsLocked = Properties.Settings.Default.BpmLockedState;
            if (BpmIsLocked)
                lockedBpm = Properties.Settings.Default.LockedBpmSetting;
            else if (Properties.Settings.Default.BpmRate >= MinRate)
                BpmRate = Properties.Settings.Default.BpmRate;

            ChangePitch = Properties.Settings.Default.ChangePitch;
            NoSpinners = Properties.Settings.Default.NoSpinners;
            HighQualityMp3s = Properties.Settings.Default.HighQualityMp3s;
            FullLN = Properties.Settings.Default.FullLN;
            NoLN = !FullLN && Properties.Settings.Default.NoLN;

            SetState(EditorState.NOT_READY);
            NotReadyReason = BadBeatmapReason.NO_BEATMAP_LOADED;
        }

        public void SaveSettings()
        {
            Properties.Settings.Default.LockedHPSetting = lockedHP;
            Properties.Settings.Default.LockedODSetting = lockedOD;

            Properties.Settings.Default.HPLockedState = HpIsLocked;
            Properties.Settings.Default.ODLockedState = OdIsLocked;

            Properties.Settings.Default.BpmLockedState = BpmIsLocked;
            if (BpmIsLocked)
                Properties.Settings.Default.LockedBpmSetting = lockedBpm;
            else
                Properties.Settings.Default.BpmRate = BpmRate;

            Properties.Settings.Default.ChangePitch = ChangePitch;
            Properties.Settings.Default.NoSpinners = NoSpinners;
            Properties.Settings.Default.HighQualityMp3s = HighQualityMp3s;
            Properties.Settings.Default.FullLN = FullLN;
            Properties.Settings.Default.NoLN = NoLN;
            Properties.Settings.Default.Save();
        }

        public event EventHandler StateChanged;

        public event EventHandler BeatmapSwitched;

        public event EventHandler BeatmapModified;

        public event EventHandler ControlsModified;

        public void ForceEventStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        public void ForceEventBeatmapSwitched() => BeatmapSwitched?.Invoke(this, EventArgs.Empty);

        public void ForceEventBeatmapModified() => BeatmapModified?.Invoke(this, EventArgs.Empty);

        public void ForceEventControlsModified() => ControlsModified?.Invoke(this, EventArgs.Empty);

        private Beatmap BeatmapConstructorWrapper(string beatmapFilename)
        {
            Beatmap newBeatmap = new Beatmap(beatmapFilename);
            if (newBeatmap.ApproachRate == -1M)
            {
                newBeatmap.ApproachRate = newBeatmap.OverallDifficulty; // i can't believe this is how old maps used to work...
            }
            return newBeatmap;
        }
        public void GenerateBeatmap()
        {
            if (State != EditorState.READY)
                return;

            SetState(EditorState.GENERATING_BEATMAP);
            try
            {
                (string osuFile, string newMp3) = WriteExportFiles();

                // create and execute osz
                AddNewBeatmapToSongFolder(Path.GetDirectoryName(OriginalBeatmap.Filename), osuFile, newMp3);

                // clean up .osu
                File.Delete(osuFile);
            }
            finally
            {
                // post. Also after a failed export (MainForm shows the error): otherwise the editor would stay in
                // GENERATING_BEATMAP, with Create Map stuck on "Working...", until mania-trainer is restarted.
                SetState(EditorState.READY);
            }
        }

        /// <summary>
        /// Writes the edited map as it will be exported: the .osu file (into the working directory) and, when the
        /// rate changes, the new mp3. Returns their file names; newMp3 is "" when the existing audio is reused.
        /// Separate from GenerateBeatmap so the export can be checked without opening osu!.
        /// </summary>
        internal (string osuFile, string newMp3) WriteExportFiles()
        {
            bool usesAR = UsesApproachRate(OriginalBeatmap.Mode);
            bool compensateForDT = ((usesAR && NewBeatmap.ApproachRate > 10) || NewBeatmap.OverallDifficulty > 10);
            LnConversion lnConversion = ActiveLnConversion;

            // Set metadata
            Beatmap exportBeatmap = new Beatmap(NewBeatmap);
            ModifyBeatmapMetadata(exportBeatmap, BpmRate, ChangePitch, compensateForDT, lnConversion);

            // Slow down map by 1.5x
            if (compensateForDT)
            {
                if (usesAR)
                    exportBeatmap.ApproachRate = DifficultyCalculator.CalculateMultipliedAR(NewBeatmap, 1 / 1.5M);
                exportBeatmap.OverallDifficulty = DifficultyCalculator.CalculateMultipliedOD(NewBeatmap, 1 / 1.5M);
                decimal compensatedRate = (NewBeatmap.Bpm / OriginalBeatmap.Bpm) / 1.5M;
                exportBeatmap.SetRate(compensatedRate);
            }

            // remove spinners
            if (NoSpinners)
                exportBeatmap.RemoveSpinners();

            // Generate new mp3
            var audioFilePath = Path.Combine(JunUtils.GetBeatmapDirectoryName(OriginalBeatmap), exportBeatmap.AudioFilename);
            var newMp3 = "";
            if (!File.Exists(audioFilePath))
            {
                string inFile = Path.Combine(Path.GetDirectoryName(OriginalBeatmap.Filename), OriginalBeatmap.AudioFilename);
                string outFile = exportBeatmap.AudioFilename;

                SongSpeedChanger.GenerateAudioFile(inFile, outFile, BpmRate, ChangePitch, compensateForDT, HighQualityMp3s);
                newMp3 = outFile;

                // take note of this mp3 in a text file so we can clean it up later
                string mp3ManifestFile = GetMp3ListFilePath();
                List<string> manifest = File.ReadAllLines(mp3ManifestFile).ToList();
                // the original's folder, which is also the export's: Path.GetDirectoryName on the export's own path
                // throws PathTooLongException once its longer name (rate, OD, FullLN...) takes it past 260 characters
                string beatmapFolder = JunUtils.GetBeatmapDirectoryName(OriginalBeatmap).Replace(Properties.Settings.Default.SongsFolder + "\\", "");
                string mp3RelativePath = Path.Combine(beatmapFolder, exportBeatmap.AudioFilename);
                manifest.Add(mp3RelativePath + " | " + exportBeatmap.Filename);
                File.WriteAllLines(mp3ManifestFile, manifest);
            }
            // save .osu to temp location (do not directly put into any song folder)
            exportBeatmap.Filename = Path.GetFileName(exportBeatmap.Filename);
            exportBeatmap.Save();

            // osu!mania long note conversion. Done on the saved file, after the rate change and spinner
            // removal, so it always sees the final timing (SetRate rebuilds hit objects from the original map).
            ManiaTransforms.ApplyToFile(exportBeatmap.Filename, lnConversion);

            return (exportBeatmap.Filename, newMp3);
        }

        private void AddNewBeatmapToSongFolder(string songFolder, string newBeatmapFile, string newMp3)
        {
            // 1. Create osz (just a regular zip file with file ext. renamed to .osz)
            string outputOsz = Path.GetFileNameWithoutExtension(songFolder) + ".osz";
            if (File.Exists(outputOsz))
                File.Delete(outputOsz);
            try
            {
                ZipFile.CreateFromDirectory(songFolder, outputOsz);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Failed to create {outputOsz} {Environment.NewLine}{Environment.NewLine}{e.Message}", "Error");
            }
            // 2. Add new files to zip/osz
            using (ZipArchive archive = ZipFile.Open(outputOsz, ZipArchiveMode.Update))
            {
                archive.CreateEntryFromFile(newBeatmapFile, Path.GetFileName(newBeatmapFile));
                if (newMp3 != "")
                {
                    archive.CreateEntryFromFile(newMp3, Path.GetFileName(newMp3));
                    File.Delete(newMp3);
                }
            }
            // 3. Run the .osz
            Process proc = new Process();
            proc.StartInfo.FileName = outputOsz;
            proc.StartInfo.UseShellExecute = true;
            try
            {
                proc.Start();
            }
            catch
            {
                MessageBox.Show("There was an error opening the generated .osz file. This is probably because .osz files have not been configured to open with osu!.exe on this system." + Environment.NewLine + Environment.NewLine +
                    "To fix this, download any map from the website, right click the .osz file, click properties, beside Opens with... click Change..., and select osu!. " +
                    "You'll know the problem is fixed when you can double click .osz files to open them with osu!", "Error");
            }
        }

        private string MatchGroup(string text, string re, int group)
        {
            foreach (Match m in Regex.Matches(text, re))
                return m.Groups[group].Value;
            return "";
        }

        private string GetMp3ListFilePath()
        {
            string manifest = Path.Combine(Properties.Settings.Default.SongsFolder, "modified_mp3_list.txt");
            if (!File.Exists(manifest))
            {
                FileStream fs = File.Open(manifest, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                fs.Close();
                fs.Dispose();
            }
            return manifest;
        }

        public List<string> GetUnusedMp3s()
        {
            // read manifest file
            List<string> lines = new List<string>();
            string mp3ManifestFile = GetMp3ListFilePath();

            if (!File.Exists(mp3ManifestFile))
                return new List<string>();

            using (var reader = File.OpenText(mp3ManifestFile))
            {
                string line = "";
                while ((line = reader.ReadLine()) != null)
                    lines.Add(line);
            }

            // convert that shit into a dictionary
            var mp3Dict = new Dictionary<string, List<string>>();
            string pattern = @"(.+) \| (.+)";
            string parseMp3(string line) => MatchGroup(line, pattern, 1);
            string parseOsu(string line) => MatchGroup(line, pattern, 2);

            // create dictionary keys
            lines
                .Select(line => parseMp3(line)).ToList()
                .Distinct()
                .ToList()
                .ForEach(mp3 => mp3Dict.Add(mp3, new List<string>()));

            // populate dictionary values
            foreach ((string mp3, string osu) in lines.Select(line => (parseMp3(line), parseOsu(line))))
                mp3Dict[mp3].Add(osu);

            // find all keys where none of the associated beatmaps exist, but the mp3 still exists
            bool noFilesExist(bool acc, string file) => acc && !File.Exists(file);
            return lines
                .Select(line => parseMp3(line))
                .Where(mp3 => mp3Dict[mp3].Aggregate(true, noFilesExist))
                .Where(mp3 => File.Exists(JunUtils.FullPathFromSongsFolder(mp3)))
                .ToList();
        }

        public void CleanUpManifestFile()
        {
            // read file
            string mp3ManifestFile = GetMp3ListFilePath();
            List<string> lines = File.ReadAllText(mp3ManifestFile).Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();
            string pattern = @"(.+) \| (.+)";
            string parseMp3(string line) => MatchGroup(line, pattern, 1);

            // filter out lines whose mp3s no longer exist
            List<string> keepLines = new List<string>();
            foreach (string line in lines)
            {
                var relMp3 = parseMp3(line);
                var absMp3 = JunUtils.FullPathFromSongsFolder(relMp3);
                if (File.Exists(absMp3))
                    keepLines.Add(line);
            }

            // write to file
            File.WriteAllText(mp3ManifestFile, String.Join(Environment.NewLine, keepLines));
        }

        public void RequestBeatmapLoad(string beatmapPath)
        {
            mapChangeRequests.Add(new BeatmapRequest(beatmapPath));

            // acquire mutually exclusive entry into this method
            if (!serviceBeatmapRequestLocked)
                ServiceBeatmapChangeRequest();
            else return; // this method is already being run in another async "thread"
        }

        private async void ServiceBeatmapChangeRequest()
        {
            // acquire mutually exclusive entry into this method
            serviceBeatmapRequestLocked = true;

            Beatmap candidateOriginalBeatmap = null, candidateNewBeatmap = null;
            while (completedBeatmapRequest == null || completedBeatmapRequest.RequestNumber != mapChangeRequests.Last().RequestNumber)
            {
                completedBeatmapRequest = mapChangeRequests.Last();
                candidateOriginalBeatmap = await Task.Run(() => LoadBeatmap(mapChangeRequests.Last().Name));

                if (candidateOriginalBeatmap != null)
                {
                    candidateNewBeatmap = new Beatmap(candidateOriginalBeatmap);
                }

                // if a new request came in, invalidate candidate beatmap and service the new request
            }

            // no new requests, we can commit to using this beatmap
            OriginalBeatmap = candidateOriginalBeatmap;
            NewBeatmap = candidateNewBeatmap;
            CancelDanRateSearch();
            danRates = null;
            if (OriginalBeatmap == null)
            {
                ClearDifficulty();
                SetState(EditorState.NOT_READY);
                NotReadyReason = BadBeatmapReason.ERROR_LOADING_BEATMAP;
                BeatmapSwitched?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                if (OriginalBeatmap.HitObjectCount == 0)
                {
                    ClearDifficulty();
                    SetState(EditorState.NOT_READY);
                    NotReadyReason = BadBeatmapReason.EMPTY_MAP;
                    BeatmapSwitched?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // Apply multiplier
                    if (BpmIsLocked)
                        SetBpm(lockedBpm);
                    NewBeatmap.SetRate(BpmRate);

                    // Apply bpm scaled settings (OD keeps the map's value: it doesn't scale with the rate)
                    if (UsesApproachRate(candidateOriginalBeatmap.Mode)) NewBeatmap.ApproachRate = DifficultyCalculator.CalculateMultipliedAR(candidateOriginalBeatmap, BpmRate);

                    // Apply locked settings to new map
                    if (HpIsLocked) NewBeatmap.HPDrainRate = lockedHP;
                    if (OdIsLocked) NewBeatmap.OverallDifficulty = lockedOD;

                    SetState(EditorState.READY);
                    RequestDiffCalc();
                    BeatmapSwitched?.Invoke(this, EventArgs.Empty);
                    BeatmapModified?.Invoke(this, EventArgs.Empty);
                }
            }
            ControlsModified?.Invoke(this, EventArgs.Empty);
            serviceBeatmapRequestLocked = false;
        }

        // Must be called on the UI thread: it snapshots NewBeatmap and the long note setting.
        public void RequestDiffCalc()
        {
            if (NewBeatmap == null)
                return;
            latestDiffCalcRequest = new DiffCalcRequest(new Beatmap(NewBeatmap), RatingLnConversion);

            // acquire mutually exclusive entry into this method
            if (!serviceDiffCalcRequestLocked)
                ServiceDiffCalcRequest();
            else return; // this method is already being run in another async "thread"
        }

        private async void ServiceDiffCalcRequest()
        {
            // acquire mutually exclusive entry into this method
            serviceDiffCalcRequestLocked = true;
            try
            {
                decimal stars = 0.0M, aim = 0.0M, speed = 0.0M;
                ManiaRating maniaRating = null;
                while (completedDiffCalcRequest == null || completedDiffCalcRequest.RequestNumber != latestDiffCalcRequest.RequestNumber)
                {
                    var request = latestDiffCalcRequest;
                    completedDiffCalcRequest = request;
                    if (request.Snapshot.Mode == GameMode.Mania)
                    {
                        // osu!mania: xdc1 (4K) or xdc7 (7K) rating -> dan (never throws)
                        (stars, aim, speed) = (0, 0, 0);
                        maniaRating = await Task.Run(() => ManiaDifficulty.Calculate(request.Snapshot, request.LnConversion));
                    }
                    else
                    {
                        maniaRating = null;
                        try
                        {
                            (stars, aim, speed) = await Task.Run(() => DifficultyCalculator.CalculateStarRating(request.Snapshot));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            (stars, aim, speed) = (0, 0, 0);
                        }
                    }
                    // if a new request came in, invalidate the diffcalc result and service the new request
                }
                // We serviced the last request, so we can commit to the diffcalc result. If the map failed to load
                // in the meantime, the result belongs to a map that's gone: keep ClearDifficulty's empty values
                // (and there's no NewBeatmap for the BeatmapModified handlers to read).
                if (State != EditorState.NOT_READY)
                {
                    StarRating = stars;
                    AimRating = aim;
                    SpeedRating = speed;
                    ManiaRating = maniaRating;
                    BeatmapModified?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                // released even if an event handler throws, so later calculations still run
                serviceDiffCalcRequestLocked = false;
            }
        }

        private void ClearDifficulty()
        {
            StarRating = AimRating = SpeedRating = 0;
            ManiaRating = null;
        }

        public (decimal, decimal, decimal) GetOriginalBpmData() => GetBpmData(OriginalBeatmap);

        public (decimal, decimal, decimal) GetNewBpmData() => GetBpmData(NewBeatmap);

        private (decimal, decimal, decimal) GetBpmData(Beatmap map) => (map?.Bpm ?? 0, map?.MinBpm ?? 0, map?.MaxBpm ?? 0);

        private void SetState(EditorState s)
        {
            State = s;
            Action action = () => StateChanged?.Invoke(this, EventArgs.Empty);

            if (mainform.InvokeRequired)
                mainform.Invoke(action);
            else
                action.Invoke();
        }

        public void SetHP(decimal value)
        {
            if (State != EditorState.READY)
                return;

            NewBeatmap.HPDrainRate = value;
            if (HpIsLocked)
                lockedHP = value;
            BeatmapModified?.Invoke(this, EventArgs.Empty);
        }

        public void SetOD(decimal value)
        {
            if (State != EditorState.READY)
                return;

            NewBeatmap.OverallDifficulty = value;
            if (OdIsLocked)
                lockedOD = value;

            BeatmapModified?.Invoke(this, EventArgs.Empty);
            ControlsModified?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleHpLock()
        {
            HpIsLocked = !HpIsLocked;
            if (HpIsLocked)
            {
                lockedHP = NewBeatmap.HPDrainRate;
            }
            else
            {
                NewBeatmap.HPDrainRate = OriginalBeatmap.HPDrainRate;
                BeatmapModified?.Invoke(this, EventArgs.Empty);
            }
            ControlsModified?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleBpmLock()
        {
            BpmIsLocked = !BpmIsLocked;
            if (BpmIsLocked && NewBeatmap != null)
            {
                lockedBpm = LockedBpmFor(BpmRate);
                BeatmapModified?.Invoke(this, EventArgs.Empty);
            }
            ControlsModified?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleOdLock()
        {
            OdIsLocked = !OdIsLocked;
            if (OdIsLocked)
            {
                lockedOD = NewBeatmap.OverallDifficulty;
            }
            else if (State != EditorState.NOT_READY)
            {
                // back to the map's own OD, as unlocking HP does
                NewBeatmap.OverallDifficulty = OriginalBeatmap.OverallDifficulty;
                BeatmapModified?.Invoke(this, EventArgs.Empty);
            }
            ControlsModified?.Invoke(this, EventArgs.Empty);
        }

        public void SetBpmMultiplier(decimal multiplier)
        {
            if (multiplier < MinRate)
                return; // turned down before anything changes (the locked BPM, the dan button's mark, a dan search)
            if (BpmIsLocked)
                lockedBpm = LockedBpmFor(multiplier);
            ApplyBpmMultiplier(multiplier);
        }

        /// <summary>
        /// The whole BPM the BPM lock keeps for <paramref name="rate"/>: the map's BPM times the rate as the New BPM box
        /// shows it, rounded to the nearest BPM. A dan button's rate is rounded up instead, so loading a map at the
        /// locked BPM never makes it slower than that rate, and the map still reaches the dan. Before that rounding up,
        /// the product is rounded to 9 decimals. A beat length is stored with about 15 significant digits (180 BPM is
        /// 333.333333333333 ms), and the noise that leaves (180 BPM x 1.50 = 270.00000000000027) mustn't add a whole
        /// BPM.
        /// </summary>
        private int LockedBpmFor(decimal rate)
        {
            if (OriginalBeatmap == null)
                return lockedBpm;
            decimal bpm = OriginalBeatmap.Bpm * rate;
            return (int)(rate == danRate ? Math.Ceiling(Math.Round(bpm, 9)) : Math.Round(bpm));
        }

        public void SetBpm(int bpm)
        {
            if (BpmIsLocked)
                lockedBpm = bpm;

            decimal originalBpm = GetOriginalBpmData().Item1;
            if (originalBpm == 0)
                return;

            decimal newMultiplier = bpm / originalBpm;
            newMultiplier = JunUtils.Clamp(newMultiplier, 0.1M, 5.0M);
            ApplyBpmMultiplier(newMultiplier);
        }

        private void ApplyBpmMultiplier(decimal multiplier)
        {
            if (BpmRate == multiplier || multiplier < MinRate)
                return; // upstream "rejected" a rate under 0.10x but applied it anyway, and 0 divided by zero
            if (multiplier != danRate)
                danRate = null; // set some other way: no longer a dan button's rate
            CancelDanRateSearch(); // a dan button's answer would undo this rate

            BpmRate = multiplier;

            // make no changes
            if (State == EditorState.NOT_READY)
                return;

            // scale AR (not applicable for taiko or mania)
            if (UsesApproachRate(NewBeatmap.Mode))
                NewBeatmap.ApproachRate = DifficultyCalculator.CalculateMultipliedAR(OriginalBeatmap, BpmRate);

            // modify beatmap timing
            NewBeatmap.SetRate(BpmRate);

            RequestDiffCalc();
            BeatmapModified?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleChangePitchSetting()
        {
            ChangePitch = !ChangePitch;
            ControlsModified?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleNoSpinners()
        {
            NoSpinners = !NoSpinners;
            ControlsModified?.Invoke(this, EventArgs.Empty);
            if (State != EditorState.NOT_READY)
                BeatmapModified?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Full LN: turn every tap into a long note. Turns No LN off.</summary>
        public void ToggleFullLN()
        {
            var rating = RatingLnConversion;
            FullLN = !FullLN;
            if (FullLN)
                NoLN = false;
            OnLnConversionChanged(rating);
        }

        /// <summary>No LN: turn every long note into a tap. Turns Full LN off.</summary>
        public void ToggleNoLN()
        {
            var rating = RatingLnConversion;
            NoLN = !NoLN;
            if (NoLN)
                FullLN = false;
            OnLnConversionChanged(rating);
        }

        // ratingBefore: the conversion the rating went by before the toggle. A dan button search only goes stale when
        // that changes (Full LN on or off alone doesn't change the rating).
        private void OnLnConversionChanged(LnConversion ratingBefore)
        {
            if (RatingLnConversion != ratingBefore)
            {
                CancelDanRateSearch();
                if (State != EditorState.NOT_READY)
                    RequestDiffCalc();
            }
            ControlsModified?.Invoke(this, EventArgs.Empty);
            if (State != EditorState.NOT_READY)
                BeatmapModified?.Invoke(this, EventArgs.Empty);
        }

        public GameMode? GetMode()
        {
            return OriginalBeatmap?.Mode;
        }

        public decimal GetScaledAR() => DifficultyCalculator.CalculateMultipliedAR(OriginalBeatmap, BpmRate);

        /// <summary>The tag in every map mania-trainer makes: searching "maniatrainer" in osu! lists them.</summary>
        internal const string GeneratedMapTag = "maniatrainer";
        /// <summary>The tag osu-trainer puts in the maps it makes; mania-trainer adds it as well.</summary>
        internal const string OsuTrainerTag = "osutrainer";
        internal static bool IsGeneratedMap(Beatmap map) => map.Tags.Contains(GeneratedMapTag) || map.Tags.Contains(OsuTrainerTag);

        public bool NewMapIsDifferent()
        {
            return (
                NewBeatmap.HPDrainRate != OriginalBeatmap.HPDrainRate ||
                NewBeatmap.OverallDifficulty != OriginalBeatmap.OverallDifficulty ||
                Math.Abs(BpmRate - 1.0M) > 0.001M ||
                NoSpinners ||
                ActiveLnConversion != LnConversion.None
            );
        }

        // return the new beatmap object if success
        // return null on failure
        private Beatmap LoadBeatmap(string beatmapPath)
        {
            // test if the beatmap is valid before committing to using it
            Beatmap retMap;
            try
            {
                retMap = BeatmapConstructorWrapper(beatmapPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while reading {beatmapPath}");
                Console.WriteLine("Exception Type: " + ex.GetType().Name);
                Console.WriteLine("Message: " + ex.Message);
                Console.WriteLine("Stack Trace: " + ex.StackTrace);

                // Don't clear OriginalBeatmap / NewBeatmap here: this runs on a worker thread while the editor is
                // still READY, so a rating finishing meanwhile would hand them to the UI as null.
                // ServiceBeatmapChangeRequest commits the null result (and NOT_READY) on the UI thread.
                return null;
            }
            // Check if beatmap was loaded successfully
            if (!retMap.Valid || retMap.Filename == null || retMap.Title == null)
            {
                Console.WriteLine("Bad .osu file format");
                return null;
            }

            // Check if this map was generated by mania-trainer (or osu-trainer, which tags "osutrainer" only)
            if (IsGeneratedMap(retMap))
            {
                // Try to find original unmodified version
                foreach (string diff in Directory.GetFiles(Path.GetDirectoryName(retMap.Filename), "*.osu"))
                {
                    Beatmap map = BeatmapConstructorWrapper(diff);
                    // FIXME: Earlier beatmaps will not contain a beatmap ID tag, and BeatmapID will be 0.
                    // What is the method for determining the original diff in this case?
                    if (!IsGeneratedMap(map) && map.BeatmapID == retMap.BeatmapID)
                    {
                        retMap = map;
                        break;
                    }
                }
            }

            return retMap;
        }

        // OUT: beatmap.Version
        // OUT: beatmap.Filename
        // OUT: beatmap.AudioFilename
        // OUT: beatmap.Tags
        private void ModifyBeatmapMetadata(Beatmap map, decimal multiplier, bool changePitch, bool preDT, LnConversion lnConversion)
        {
            // Difficulty Name and AudioFilename
            if (preDT)
            {
                string bpm = map.Bpm.ToString("0");
                map.Version += $" {multiplier:0.##}x ({bpm}bpm)";
                map.AudioFilename = $"{Path.GetFileNameWithoutExtension(map.AudioFilename)} {multiplier:0.000}x withDT";
                if (changePitch && Math.Abs(multiplier - 1M) > 0.001M)
                    map.AudioFilename += $" (pitch {(multiplier < 1 ? "lowered" : "raised")})";
                map.AudioFilename += ".mp3";
            }
            else if (Math.Abs(multiplier - 1M) > 0.001M)
            {
                string bpm = map.Bpm.ToString("0");
                map.Version += $" {multiplier:0.##}x ({bpm}bpm)";
                map.AudioFilename = $"{Path.GetFileNameWithoutExtension(map.AudioFilename)} {multiplier:0.000}x";
                if (changePitch)
                    map.AudioFilename += $" (pitch {(multiplier < 1 ? "lowered" : "raised")})";
                map.AudioFilename += ".mp3";
            }

            // Difficulty Name - Difficulty Settings
            string HPAROD = "";
            if (NewBeatmap.HPDrainRate != OriginalBeatmap.HPDrainRate)
                HPAROD += $" HP{NewBeatmap.HPDrainRate:0.#}";

            if (   UsesApproachRate(OriginalBeatmap.Mode)
                && (NewBeatmap.ApproachRate != GetScaledAR() || NewBeatmap.ApproachRate > 10M))
                HPAROD += $" AR{NewBeatmap.ApproachRate:0.#}";

            if (NewBeatmap.OverallDifficulty != OriginalBeatmap.OverallDifficulty || NewBeatmap.OverallDifficulty > 10M)
                HPAROD += $" OD{NewBeatmap.OverallDifficulty:0.#}";

            map.Version += HPAROD;

            // Long note conversion. This also keeps the file name (built from Version below) different
            // from the original's when the conversion is the only change.
            if (lnConversion == LnConversion.FullLn)
                map.Version += " FullLN";
            else if (lnConversion == LnConversion.NoLn)
                map.Version += " NoLN";

            //if (NoSpinners)
            //    map.Version += " nospin";

            // Beatmap File Name
            string artist  = JunUtils.NormalizeText(map.Artist);
            string title   = JunUtils.NormalizeText(map.Title);
            string creator = JunUtils.NormalizeText(map.Creator);
            string diff    = JunUtils.NormalizeText(map.Version);
            map.Filename   = Path.GetDirectoryName(map.Filename) + $"\\{artist} - {title} ({creator}) [{diff}].osu";

            // make this map searchable in the in-game menus
            // ("osutrainer" too, so osu-trainer and an "osutrainer" search in osu! still know these maps)
            var tags = map.Tags;
            foreach (string tag in new[] { GeneratedMapTag, OsuTrainerTag })
                if (!tags.Contains(tag))
                    tags.Add(tag);
            map.Tags = tags; // need to assign like this because Tags is an immutable list
        }

        // dominant, min, max

        public void ResetBeatmap()
        {
            if (State != EditorState.READY)
                return;
            NewBeatmap.HPDrainRate = OriginalBeatmap.HPDrainRate;
            NewBeatmap.ApproachRate = OriginalBeatmap.ApproachRate;
            NewBeatmap.OverallDifficulty = OriginalBeatmap.OverallDifficulty;
            HpIsLocked = false;
            OdIsLocked = false;
            BpmIsLocked = false;
            FullLN = false;
            NoLN = false;
            CancelDanRateSearch();
            BpmRate = 1.0M;
            danRate = null;
            NewBeatmap.SetRate(1.0M);
            RequestDiffCalc();
            ControlsModified?.Invoke(this, EventArgs.Empty);
            BeatmapModified?.Invoke(this, EventArgs.Empty);
        }

        internal void ToggleHighQualityMp3s()
        {
            HighQualityMp3s = !HighQualityMp3s;
            ControlsModified?.Invoke(this, EventArgs.Empty);
        }

#region Dan buttons
        /// <summary>
        /// The dan table of the loaded map: 4K's for a 4K osu!mania map, 7K's for a 7K one, null for anything else.
        /// Decided from the map itself, not from the rating on show, which can still be the previous map's.
        /// </summary>
        public DanSystem DanSystem => OriginalBeatmap?.Mode == GameMode.Mania ? ManiaDifficulty.SystemFor(OriginalBeatmap) : null;

        /// <summary>
        /// True when the dan buttons can be used: a 4K or 7K osu!mania map is loaded, its calculator is there and
        /// nothing is being exported.
        /// </summary>
        public bool CanSetRateForDan => State == EditorState.READY && ManiaDifficulty.IsAvailable(DanSystem);

        /// <summary>
        /// A dan button: sets the rate to the lowest one, in 0.01x steps from 0.50x to 3.00x, at which the map (with the
        /// current long note setting) rates as <paramref name="target"/>, the way the window shows ratings. Leaves the
        /// rate alone when no rate in that range does, and does nothing for a dan of another key mode than the map's.
        /// Call on the UI thread; the ratings are worked out on the thread pool.
        /// </summary>
        /// <returns>
        /// What the search found; null when it no longer applies because, before it finished, it was cancelled
        /// (<see cref="CancelDanRateSearch"/>), another dan button was pressed, the map, the long note setting or the
        /// rate changed, another map started loading, or the editor stopped being ready.
        /// </returns>
        public async Task<DanRateResult> SetRateForDan(DanLevel target)
        {
            if (target == null || !CanSetRateForDan || !ReferenceEquals(target.System, DanSystem))
                return null;
            var map = OriginalBeatmap;
            var conversion = RatingLnConversion;
            decimal rate = BpmRate;
            if (danRates == null || !danRates.IsFor(map, conversion))
                danRates = new DanRateCache(map, conversion);
            var cache = danRates;
            int guess = GuessDanRateStep(target);

            danRateSearch?.Cancel();
            var search = danRateSearch = new CancellationTokenSource();
            int number = ++danRateSearchNumber;
            DanRateResult result;
            try
            {
                result = await Task.Run(() => DanRateFinder.Find(cache.RatingAt, target, guess, search.Token));
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                if (danRateSearch == search)
                    danRateSearch = null;
            }

            // Only the newest search, not cancelled (Find checks only between ratings, so a cancel during the last
            // one gets here), for the map and settings it started on, with no other map on its way in (the editor
            // stays READY on the old map while a new one loads). Exactly READY: during an export NewBeatmap is being
            // written out and must not change.
            if (search.IsCancellationRequested || number != danRateSearchNumber || serviceBeatmapRequestLocked
                || State != EditorState.READY || !ReferenceEquals(OriginalBeatmap, map)
                || RatingLnConversion != conversion || BpmRate != rate)
                return null;
            if (result.Outcome == DanRateOutcome.Reached)
            {
                // A dan button's rate: the BPM lock keeps it rounded up, now or when it's turned on later (LockedBpmFor).
                // This holds until the rate is set some other way. A map loaded with the lock off keeps the rate, and
                // with it this mark: the export osu! selects after Create Map is such a load.
                danRate = result.Rate;
                SetBpmMultiplier(result.Rate);
            }
            return result;
        }

        /// <summary>Stops a running dan button search (it then returns null).</summary>
        public void CancelDanRateSearch()
        {
            danRateSearch?.Cancel();
            danRateSearch = null;
        }

        // Where the search starts: the current rate, scaled by how far the rating on show is from the dan's threshold.
        // Only a starting point, since the rating on show can lag behind the map; the search rates every step it uses.
        private int GuessDanRateStep(DanLevel target)
        {
            double step = (double)BpmRate * 100;
            var shown = ManiaRating;
            if (shown?.Status == ManiaRatingStatus.Ok && shown.Rating > 0 && ReferenceEquals(shown.System, target.System))
                step *= Math.Pow(DanTable.RoundForDisplay(target.Threshold) / shown.Rating, 1 / DanRateFinder.TypicalExponent);
            return (int)Math.Ceiling(Math.Max(0, Math.Min(10000, step)));
        }
#endregion
    }
}
