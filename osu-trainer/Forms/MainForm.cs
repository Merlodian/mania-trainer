using FsBeatmapProcessor;
using osu_trainer.Controls;
using osu_trainer.Forms;
using osu_trainer.Mania;
using OsuMemoryDataProvider;
using OsuMemoryDataProvider.OsuMemoryModels.Direct;
using ProcessMemoryDataFinder.API;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utilities;

namespace osu_trainer
{
    public partial class MainForm : Form
    {
        // Beatmap
        private string userSongsFolder = null;

        private StructuredOsuMemoryReader osuReader;

        // Common Control Lists
        private List<Label> dumbLabels;
        private List<TextBox> diffDisplays;
        private List<OptionSlider> diffSliders;
        private List<OsuCheckBox> checkControls;

        // Hotkeys
        private globalKeyboardHook kbhook;
        private bool hooked = true;
        private List<Keys> Hotkeys;

        // Single object instances
        private readonly SoundPlayer sound = new SoundPlayer();
        private readonly BeatmapEditor editor;

        // other
        private string previousBeatmapRead;

        private bool? gameLoaded = null;
        private bool mapSelectScreen = false;

        // Form heights without the extras panel. Upstream used 493 / 531; the CS and AR rows (56 px) are gone, and
        // the dan buttons have one row of small buttons under them where the profile buttons had two (17 px).
        private const int NotReadyHeight = 437;
        private const int ReadyHeight = 458;

        private readonly ToolTip danToolTip;

        public MainForm()
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            InitializeComponent();
            Height = NotReadyHeight;
            danToolTip = new ToolTip(components) { AutoPopDelay = 20000, InitialDelay = 400, ReshowDelay = 200 };

            // Read version number from version.txt
            try
            {
                string[] lines = File.ReadAllLines("version.txt");
                foreach (string line in lines)
                {
                    string attribute = line.Split(':')[0].Trim();
                    string value = line.Split(':')[1].Trim();
                    if (attribute == "current version")
                    {
                        this.Text = $"mania trainer v{value}"; // set window title
                    }
                }
            }
            catch {}

            // init control lists
            dumbLabels = new List<Label>
            {
                hplabel, odlabel,
                BpmMultiplierLabel, OriginalBpmLabel, NewBpmLabel
            };
            diffDisplays = new List<TextBox>
            {
                HPDisplay, ODDisplay
            };
            diffSliders = new List<OptionSlider>
            {
                HPSlider, ODSlider
            };
            checkControls = new List<OsuCheckBox>
            {
                NoSpinnersCheck, FullLNCheck, NoLNCheck, ChangePitchCheck, highQualityMp3Check
            };

            // dan buttons
            danButtons = new[] { danButton1, danButton2, danButton3, danButton4 };
            danMenuButtons = new[] { danMenuButton1, danMenuButton2, danMenuButton3, danMenuButton4 };
            danTargets = ParseDanButtons(Properties.Settings.Default.DanButtons);
            danMenu = BuildDanMenu();
            danButtonToolTip = new ToolTip(components) { AutoPopDelay = 10000, InitialDelay = 500, ReshowDelay = 200 };

            ApplyFonts();

            // load user settings
            if (Directory.Exists(Properties.Settings.Default.SongsFolder))
                userSongsFolder = Properties.Settings.Default.SongsFolder;

            // load hotkeys: Create Map, then the four dan buttons (saved under upstream's profile hotkey settings)
            Hotkeys = new List<Keys>();
            Hotkeys.Add(Properties.Settings.Default.HotkeyCreateMap);
            Hotkeys.Add(Properties.Settings.Default.HotkeyProfile1);
            Hotkeys.Add(Properties.Settings.Default.HotkeyProfile2);
            Hotkeys.Add(Properties.Settings.Default.HotkeyProfile3);
            Hotkeys.Add(Properties.Settings.Default.HotkeyProfile4);

            // Init object instances
            osuReader = new StructuredOsuMemoryReader();
            editor = new BeatmapEditor(this);

            // Add event handlers (observers)
            editor.StateChanged += ToggleDumbLabels;
            editor.StateChanged += TogglePrettyButtons;
            editor.StateChanged += ToggleHpOdDisplay;
            editor.StateChanged += ToggleBpmInputControls;
            editor.StateChanged += ToggleBpmDisplay;
            editor.StateChanged += ToggleLockButtons;
            editor.StateChanged += UpdateDanButtons;
            middlePanel.MaximumSize = new Size(999, 999);
            BottomPanel.MaximumSize = new Size(999, 999);
            editor.StateChanged += RearrangeLayout;
            editor.BeatmapSwitched += UpdateSongDisplay;
            editor.BeatmapSwitched += UpdateDifficultyDisplay;
            editor.BeatmapModified += UpdateBpmDisplay;
            editor.BeatmapModified += UpdateHpOdDisplay;
            editor.BeatmapModified += UpdateDifficultyDisplay;
            editor.BeatmapModified += TogglePrettyButtons;
            editor.BeatmapModified += UpdateRateInputControls;
            editor.ControlsModified += UpdateLockButtons;
            editor.ControlsModified += UpdateCheckBoxes;
            editor.BeatmapSwitched += UpdateDanButtons;
            editor.BeatmapSwitched += (s, e) => ClearDanStatuses(); // what the dan buttons found was for the old map

            // Install keyboard hooks
            // Notice: The only purpose of the keyboard hook is for shortcuts. Nothing else!
            ApplyHotkeys();

            // need controls to show up as initially disabled
            editor.ForceEventStateChanged();
            editor.ForceEventBeatmapSwitched();
            editor.ForceEventControlsModified();

            BeatmapUpdateTimer.Start();
            OsuRunningTimer.Start();
            formAnimationTimer.Start();

            Focus();
        }

        private void ApplyHotkeys()
        {
            // restart keyboard hook with new hotkeys
            if (kbhook != null)
                kbhook.unhook();
            kbhook = new globalKeyboardHook();
            kbhook.HookedKeys = Hotkeys;
            kbhook.KeyDown += new KeyEventHandler(CreateMapHotkeyHandler);
            kbhook.KeyDown += new KeyEventHandler(DanHotkeyHandler);
            kbhook.KeyUp += new KeyEventHandler(DanHotkeyReleased);
            GenerateMapButton.Subtext = $"Ctrl+Shift+{HotkeyForm.KeyName(Hotkeys[0])}";
        }

        private void ApplyFonts()
        {
            var comforta = Program.FontCollection.Families[0];

            foreach (var textbox in diffDisplays)
                textbox.Font = new Font(comforta, 11, FontStyle.Bold);
            foreach (var label in dumbLabels)
                label.Font = new Font(comforta, 10, FontStyle.Bold);
            foreach (var check in checkControls)
                check.Font = new Font(comforta, 9, FontStyle.Bold);

            BpmMultiplierTextBox.Font = new Font(comforta, 11, FontStyle.Bold);
            OriginalBpmTextBox.Font = new Font(comforta, 10, FontStyle.Bold);
            NewBpmTextBox.Font = new Font(comforta, 10, FontStyle.Bold);

            OriginalBpmRangeTextBox.Font = new Font(comforta, 9, FontStyle.Bold);
            NewBpmRangeTextBox.Font = new Font(comforta, 9, FontStyle.Bold);
        }

#region Callbacks for updating GUI controls

        private void UpdateRateInputControls(object sender, EventArgs e)
        {
            BpmMultiplierTextBox.Text = editor.BpmRate.ToString("0.00");
        }

        private void UpdateSongDisplay(object sender, EventArgs e)
        {
            switch (editor.State)
            {
                case EditorState.NOT_READY:
                    switch (editor.NotReadyReason)
                    {
                        case BadBeatmapReason.NO_BEATMAP_LOADED:
                            break;

                        case BadBeatmapReason.ERROR_LOADING_BEATMAP:
                            SongDisplay.Artist = "failed to load beatmap";
                            SongDisplay.Title = "";
                            SongDisplay.Difficulty = "";
                            SongDisplay.Cover = null;
                            break;

                        case BadBeatmapReason.EMPTY_MAP:
                            SongDisplay.Artist = editor.OriginalBeatmap.Artist;
                            SongDisplay.Title = editor.OriginalBeatmap.Title;
                            SongDisplay.Difficulty = "empty map";
                            SongDisplay.Cover = GetSongBackground(editor.NewBeatmap);
                            break;

                        default:
                            break;
                    }
                    break;

                case EditorState.READY:
                case EditorState.GENERATING_BEATMAP:
                    SongDisplay.Artist = editor.OriginalBeatmap.Artist;
                    SongDisplay.Title = editor.OriginalBeatmap.Title;
                    SongDisplay.Difficulty = editor.OriginalBeatmap.Version;
                    SongDisplay.Cover = GetSongBackground(editor.NewBeatmap);
                    break;
            }
        }

        private Image GetSongBackground(Beatmap beatmap)
        {
            if (string.IsNullOrWhiteSpace(beatmap.Background))
                return Properties.Resources.nobg;

            string imageAbsolutePath = Path.Combine(Path.GetDirectoryName(beatmap.Filename), beatmap.Background);
            if (!File.Exists(imageAbsolutePath))
                return Properties.Resources.nobg;

            return Image.FromFile(imageAbsolutePath);
        }

        private void ToggleDumbLabels(object sender, EventArgs e)
        {
            Color labelColor = Colors.PaleBlue;
            switch (editor.State)
            {
                case EditorState.NOT_READY:
                    labelColor = Colors.Disabled;
                    break;

                case EditorState.READY:
                case EditorState.GENERATING_BEATMAP:
                    labelColor = Colors.PaleBlue;
                    break;
            }

            foreach (var label in dumbLabels)
                label.ForeColor = labelColor;
        }

        private void ToggleBpmDisplay(object sender, EventArgs e)
        {
            switch (editor.State)
            {
                case EditorState.NOT_READY:
                    OriginalBpmRangeTextBox.Visible = false;
                    NewBpmRangeTextBox.Visible = false;
                    NewBpmTextBox.Enabled = false;
                    NewBpmTextBox.BackColor = Colors.Disabled;
                    OriginalBpmTextBox.Enabled = false;
                    OriginalBpmTextBox.BackColor = Colors.Disabled;
                    break;

                case EditorState.READY:
                case EditorState.GENERATING_BEATMAP:
                    OriginalBpmTextBox.Enabled = true;
                    OriginalBpmTextBox.BackColor = Colors.ReadOnlyBg;
                    NewBpmTextBox.Enabled = true;
                    NewBpmTextBox.BackColor = Colors.TextBoxBg;
                    break;
            }
        }

        private void UpdateBpmDisplay(object sender, EventArgs e)
        {
            switch (editor.State)
            {
                case EditorState.NOT_READY:
                    break;

                case EditorState.READY:
                case EditorState.GENERATING_BEATMAP:
                    (decimal oldbpm, decimal oldmin, decimal oldmax) = editor.GetOriginalBpmData();
                    decimal newbpm, newmin, newmax;
                    if (System.Math.Abs(editor.BpmRate - 1.0M) > 0.001M)
                        (newbpm, newmin, newmax) = editor.GetNewBpmData();
                    else
                        (newbpm, newmin, newmax) = (oldbpm, oldmin, oldmax);

                    // bpm textboxes
                    OriginalBpmTextBox.Text = System.Math.Round(oldbpm).ToString("0");
                    NewBpmTextBox.Text = System.Math.Round(newbpm).ToString("0");
                    if (newbpm > oldbpm + 0.001M)
                        NewBpmTextBox.ForeColor = Colors.AccentRed;
                    else if (newbpm < oldbpm - 0.001M)
                        NewBpmTextBox.ForeColor = Colors.Easier;
                    else
                        NewBpmTextBox.ForeColor = Colors.TextBoxFg;

                    // bpm range
                    OriginalBpmRangeTextBox.Text = $"({System.Math.Round(oldmin).ToString("0")} - {System.Math.Round(oldmax).ToString("0")})";
                    NewBpmRangeTextBox.Text = $"({System.Math.Round(newmin).ToString("0")} - {System.Math.Round(newmax).ToString("0")})";
                    OriginalBpmRangeTextBox.Visible = (oldmin != oldmax);
                    NewBpmRangeTextBox.Visible = (oldmin != oldmax);

                    // bpm slider
                    BpmSlider.Value = editor.BpmRate;

                    break;
            }
        }

        private void ToggleLockButtons(object sender, EventArgs e)
        {
            bool not_ready = (editor.State == EditorState.NOT_READY);
            foreach (var check in checkControls)
                check.Enabled = not_ready ? false : true;
            highQualityMp3Check.ForeColor = not_ready ? highQualityMp3Check.DisabledColor : Color.FromArgb(167, 154, 233);
            UpdateLnCheckBoxesEnabled();
        }

        // Full LN / No LN only mean something for osu!mania maps
        private void UpdateLnCheckBoxesEnabled()
        {
            bool enabled = editor.State != EditorState.NOT_READY && editor.GetMode() == GameMode.Mania;
            foreach (var check in new[] { FullLNCheck, NoLNCheck })
            {
                check.Enabled = enabled;
                check.ForeColor = enabled ? Colors.PaleBlue : Colors.Disabled;
            }
        }

        private void UpdateLockButtons(object sender, EventArgs e)
        {
            // change checked state without raising any events
            HPLockCheck.CheckedChanged -= HpLockCheck_CheckedChanged;
            ODLockCheck.CheckedChanged -= OdLockCheck_CheckedChanged;
            BpmLockCheck.CheckedChanged -= BpmLockCheck_CheckedChanged;

            HPLockCheck.Checked = editor.HpIsLocked;
            ODLockCheck.Checked = editor.OdIsLocked;
            BpmLockCheck.Checked = editor.BpmIsLocked;

            HPLockCheck.CheckedChanged += HpLockCheck_CheckedChanged;
            ODLockCheck.CheckedChanged += OdLockCheck_CheckedChanged;
            BpmLockCheck.CheckedChanged += BpmLockCheck_CheckedChanged;
        }

        private void UpdateCheckBoxes(object sender, EventArgs e)
        {
            var enabled = editor.State != EditorState.NOT_READY;
            foreach (var check in checkControls)
            {
                check.Enabled = enabled;
                check.ForeColor = enabled ? Colors.PaleBlue : Colors.Disabled;
            }
            UpdateLnCheckBoxesEnabled();

            // change checked state without raising any events
            NoSpinnersCheck.CheckedChanged         -= NoSpinnerCheckBox_CheckedChanged;
            FullLNCheck.CheckedChanged             -= FullLNCheck_CheckedChanged;
            NoLNCheck.CheckedChanged               -= NoLNCheck_CheckedChanged;
            ChangePitchCheck.CheckedChanged        -= ChangePitchButton_CheckedChanged;
            highQualityMp3Check.CheckedChanged     -= highQualityCheckBox_CheckedChanged;

            NoSpinnersCheck.Checked            = editor.NoSpinners;
            FullLNCheck.Checked                = editor.FullLN;
            NoLNCheck.Checked                  = editor.NoLN;
            ChangePitchCheck.Checked           = editor.ChangePitch;
            highQualityMp3Check.Checked        = editor.HighQualityMp3s;

            NoSpinnersCheck.CheckedChanged         += NoSpinnerCheckBox_CheckedChanged;
            FullLNCheck.CheckedChanged             += FullLNCheck_CheckedChanged;
            NoLNCheck.CheckedChanged               += NoLNCheck_CheckedChanged;
            ChangePitchCheck.CheckedChanged        += ChangePitchButton_CheckedChanged;
            highQualityMp3Check.CheckedChanged     += highQualityCheckBox_CheckedChanged;
        }

        private void ToggleHpOdDisplay(object sender, EventArgs e)
        {
            bool enabled = (editor.State != EditorState.NOT_READY);
            HPSlider.Enabled = enabled;
            HPDisplay.Enabled = enabled ? true : false;
            HPDisplay.BackColor = enabled ? Colors.ReadOnlyBg : SystemColors.ControlDark;
            HPDisplay.ForeColor = Colors.ReadOnlyFg;
            HPDisplay.Font = new Font(HPDisplay.Font, FontStyle.Bold);

            enabled = (editor.State != EditorState.NOT_READY);
            ODSlider.Enabled = enabled;
            ODDisplay.Enabled = enabled ? true : false;
            ODDisplay.BackColor = enabled ? Colors.ReadOnlyBg : SystemColors.ControlDark;
            ODDisplay.ForeColor = Colors.ReadOnlyFg;
            ODDisplay.Font = new Font(ODDisplay.Font, FontStyle.Bold);
        }

        private void UpdateHpOdDisplay(object sender, EventArgs e)
        {
            // HP
            decimal newHP = editor.NewBeatmap.HPDrainRate;
            decimal originalHP = editor.OriginalBeatmap.HPDrainRate;
            HPDisplay.Text = newHP.ToString("0.#");
            HPSlider.Value = (decimal)newHP;
            if (newHP > originalHP)
            {
                HPDisplay.ForeColor = Colors.AccentRed;
                HPDisplay.Font = new Font(HPDisplay.Font, FontStyle.Bold);
            }
            else if (newHP < originalHP)
            {
                HPDisplay.ForeColor = Colors.Easier;
                HPDisplay.Font = new Font(HPDisplay.Font, FontStyle.Bold);
            }
            else
            {
                HPDisplay.ForeColor = Colors.TextBoxFg;
                HPDisplay.Font = new Font(HPDisplay.Font, FontStyle.Bold);
            }

            // OD
            decimal newOD = editor.NewBeatmap.OverallDifficulty;
            decimal originalOD = editor.OriginalBeatmap.OverallDifficulty;
            ODDisplay.Text = newOD.ToString("0.#");
            ODSlider.Value = (decimal)newOD;
            if (newOD > originalOD)
            {
                ODDisplay.ForeColor = Colors.AccentRed;
                ODDisplay.Font = new Font(ODDisplay.Font, FontStyle.Bold);
            }
            else if (newOD < originalOD)
            {
                ODDisplay.ForeColor = Colors.Easier;
                ODDisplay.Font = new Font(ODDisplay.Font, FontStyle.Bold);
            }
            else
            {
                ODDisplay.ForeColor = Colors.TextBoxFg;
                ODDisplay.Font = new Font(ODDisplay.Font, FontStyle.Bold);
            }
            if (newOD > 10)
            {
                ODDisplay.ForeColor = Color.Magenta;
                ODDisplay.Font = new Font(ODDisplay.Font, FontStyle.Bold);
            }
        }

        private void ToggleBpmInputControls(object sender, EventArgs e)
        {
            bool enabled = (editor.State != EditorState.NOT_READY);
            BpmSlider.Enabled = enabled ? true : false;
            BpmMultiplierTextBox.Enabled = enabled ? true : false;
            BpmMultiplierTextBox.BackColor = enabled ? Colors.TextBoxBg : SystemColors.ControlDark;
        }

        private void UpdateDifficultyDisplay(object sender, EventArgs e)
        {
            var mode = editor.GetMode();
            if (mode.HasValue)
            {
                SongDisplay.GameMode = mode.Value;
            }

            // osu!mania maps show their dan; everything else keeps the star rating
            var mania = editor.State == EditorState.NOT_READY ? null : editor.ManiaRating;
            SongDisplay.ManiaRating = mania;
            SongDisplay.Stars = (float)editor.StarRating;
            string tip = mania?.Describe();
            if (tip != null && mania.Status == ManiaRatingStatus.Ok && editor.LnConversion == LnConversion.FullLn)
                tip += "\nFull LN isn't counted: rated as the original chart";
            danToolTip.SetToolTip(SongDisplay, tip);
        }

        private void TogglePrettyButtons(object sender, EventArgs e)
        {
            bool enabled = false;
            switch (editor.State)
            {
                case EditorState.READY:
                    enabled = editor.NewMapIsDifferent() ? true : false;
                    SongsFolderButton.Visible = false;
                    break;
                case EditorState.NOT_READY:
                    enabled = false;
                    SongsFolderButton.Visible = true;
                    break;
                case EditorState.GENERATING_BEATMAP:
                    enabled = false;
                    break;
            }
            GenerateMapButton.Enabled = enabled;
            GenerateMapButton.ForeColor = enabled ? Color.White : Colors.Disabled;
            GenerateMapButton.Color = enabled ? Colors.AccentPink2 : Colors.TextBoxBg;
            GenerateMapButton.Text = editor.State == EditorState.GENERATING_BEATMAP ? "Working..." : "Create Map";

            ResetButton.Enabled = enabled;
            ResetButton.ForeColor = enabled ? Color.White : Colors.Disabled;
            ResetButton.Color = enabled ? Color.SteelBlue : Colors.TextBoxBg;
        }

#endregion Callbacks for updating GUI controls

#region User input event handlers

        private void HpSlider_ValueChanged(object sender, EventArgs e) => editor.SetHP(HPSlider.Value);

        private void OdSlider_ValueChanged(object sender, EventArgs e) => editor.SetOD(ODSlider.Value);

        private void HpLockCheck_CheckedChanged(object sender, EventArgs e) => editor.ToggleHpLock();

        private void OdLockCheck_CheckedChanged(object sender, EventArgs e) => editor.ToggleOdLock();

        private void BpmLockCheck_CheckedChanged(object sender, EventArgs e) => editor.ToggleBpmLock();

        // What the dan buttons found was for the old long note setting, if the rating goes by another one now (Full
        // LN on or off alone doesn't change the rating: see RatingLnConversion).
        private void FullLNCheck_CheckedChanged(object sender, EventArgs e)
        {
            var rating = editor.RatingLnConversion;
            editor.ToggleFullLN();
            if (editor.RatingLnConversion != rating)
                ClearDanStatuses();
        }
        private void NoLNCheck_CheckedChanged(object sender, EventArgs e)
        {
            var rating = editor.RatingLnConversion;
            editor.ToggleNoLN();
            if (editor.RatingLnConversion != rating)
                ClearDanStatuses();
        }

        private void ChangePitchButton_CheckedChanged(object sender, EventArgs e) => editor.ToggleChangePitchSetting();
        private void NoSpinnerCheckBox_CheckedChanged(object sender, EventArgs e) => editor.ToggleNoSpinners();
        private void highQualityCheckBox_CheckedChanged(object sender, EventArgs e) => editor.ToggleHighQualityMp3s();

        // Leaving the rate box or the New BPM box, or Enter in it, applies only what was typed there. Each shows a
        // rounded value: the rate to 0.01x, the BPM to a whole number. Applying an untyped box would move the rate.
        // A dan button's 1.56x on a 180 BPM map is 280.8 BPM, shown as 281, and 300 BPM typed is 1.6667x, shown as
        // 1.67. Modified is set by typing (the New BPM box's mouse wheel sets it too). The program setting different
        // text clears it but the same text doesn't, so each submit clears it itself.
        private void BpmMultiplierTextBox_Submit(object sender, EventArgs e)
        {
            if (!BpmMultiplierTextBox.Modified)
                return;
            BpmMultiplierTextBox.Modified = false;
            decimal mult;
            if (Decimal.TryParse(BpmMultiplierTextBox.Text, out mult) && mult >= BeatmapEditor.MinRate)
                editor.SetBpmMultiplier(mult);
            else // not a number, or under 0.10x: turned down, and the box shows the rate again
                BpmMultiplierTextBox.Text = editor.BpmRate.ToString("0.00");
        }

        private void NewBpmTextBox_Submit()
        {
            if (!NewBpmTextBox.Modified)
                return;
            NewBpmTextBox.Modified = false;
            int bpm;
            if (int.TryParse(NewBpmTextBox.Text, out bpm))
                editor.SetBpm(bpm);
        }

        private void NewBpmTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                NewBpmTextBox_Submit();
        }

        private void NewBpmTextBox_Leave(object sender, EventArgs e)
        {
            NewBpmTextBox_Submit();
        }
        private void NewBpmTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar.ToString() == " ")
            {
                e.Handled = true;
                BpmLockCheck.Checked = !BpmLockCheck.Checked;
            }
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            editor.ResetBeatmap();
            ClearDanStatuses(); // the rate and long note setting the dan buttons worked with are gone
        }

        // rates the whole Songs folder and writes osu! collections by dan; the window checks osu! is closed before writing
        private void DanCollectionsButton_Click(object sender, EventArgs e)
        {
            using (var form = new DanCollectionsForm(userSongsFolder))
                form.ShowDialog(this);
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (gameLoaded == true)
            {
                MessageBox.Show("Please close osu! first then try again.", "osu! is running");
                return;
            }
            var mp3List = editor.GetUnusedMp3s();
            if (new DeleteMp3sForm(mp3List).ShowDialog() == DialogResult.OK)
            {
                mp3List
                    .Select(relativeMp3 => JunUtils.FullPathFromSongsFolder(relativeMp3))
                    .Select(absMp3 => new FileInfo(absMp3))
                    .ToList()
                    .ForEach(file => file.Delete());
                if (mp3List.Count > 0)
                    MessageBox.Show($"Deleted {mp3List.Count} file(s).", "Success");
                editor.CleanUpManifestFile();
            }
        }

        private void GenerateMapButton_Click(object sender, EventArgs e)
        {
            var mode = editor.GetMode();
            bool highAR = (mode == GameMode.osu || mode == GameMode.CatchtheBeat) && editor.NewBeatmap.ApproachRate > 10M;
            if ( !Properties.Settings.Default.HighARODMessageShown && (highAR || editor.NewBeatmap.OverallDifficulty > 10M) )
            {
                MessageBox.Show("You have chosen an AR or OD greater than 10. After this map gets created, make sure to play it with Doubletime.", "Note");
                Properties.Settings.Default.HighARODMessageShown = true;
                Properties.Settings.Default.Save();
            }
            BackgroundWorker.RunWorkerAsync();
        }

        private void Unfocus(object sender, EventArgs e) => ActiveControl = middlePanel;

        private void CreateMapHotkeyHandler(object sender, EventArgs e)
        {
            if (!GenerateMapButton.Enabled)
                return;

            var k = (KeyEventArgs)e;
            // The key set in the hotkey window (upstream always checked X), with Ctrl+Shift and not Alt: AltGr is
            // Ctrl+Alt, and AltGr+Shift+key types a character on some keyboard layouts.
            if (k.Control && k.Shift && !k.Alt && k.KeyCode == Hotkeys[0])
                CreateMapFromHotkey();
        }

        // the hotkey sound, then Create Map
        protected virtual void CreateMapFromHotkey()
        {
            sound.Stream = Properties.Resources.hotkey;
            sound.Play();
            GenerateMapButton_Click(this, EventArgs.Empty);
        }

#endregion User input event handlers

#region Timer events

        private void BeatmapUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (gameLoaded == false || gameLoaded == null || !mapSelectScreen || editor.State == EditorState.GENERATING_BEATMAP)
                return;

            // this can be cleaned up...
            // Read memory for current map
            osuReader.TryRead(osuReader.OsuMemoryAddresses.Beatmap);
            string beatmapFilename = osuReader.OsuMemoryAddresses.Beatmap.OsuFileName;
            string beatmapFolder = osuReader.OsuMemoryAddresses.Beatmap.FolderName;

            var invalidChars = Path.GetInvalidPathChars();

            // Read unsuccessful
            if (string.IsNullOrWhiteSpace(beatmapFilename) || beatmapFilename.Any(c => invalidChars.Contains(c)))
                return;
            if (string.IsNullOrWhiteSpace(beatmapFolder) || beatmapFolder.Any(c => invalidChars.Contains(c)))
                return;

            // Beatmap name hasn't changed since last read
            if (previousBeatmapRead != null && previousBeatmapRead == beatmapFilename)
                return;
            previousBeatmapRead = beatmapFilename;

            // Try to locate the beatmap
            string absoluteFilename = Path.Combine(userSongsFolder, beatmapFolder.TrimEnd(), beatmapFilename);
            if (!File.Exists(absoluteFilename))
                return; 

            // signal the editor class to load this beatmap sometime in the future
            editor.RequestBeatmapLoad(absoluteFilename);
        }

        private async void OsuRunningTimer_Tick(object sender, EventArgs e)
        {
            // check if osu!.exe is running
            var processes = Process.GetProcessesByName("osu!");
            if (processes.Length == 0)
                gameLoaded = false;
            else
            {
                if (gameLoaded == false) // @ posedge gameLoaded
                {
                    await Task.Run(() => Thread.Sleep(5000));
                    gameLoaded = true;
                }
                else if (gameLoaded == null)
                    gameLoaded = true;

                if (userSongsFolder == null || userSongsFolder == "")
                {
                    // Try to get osu songs folder
                    // TODO: wrap with try catch
                    var osuExePath = processes[0].MainModule.FileName;
                    userSongsFolder = Path.Combine(Path.GetDirectoryName(osuExePath), "Songs");
                    Properties.Settings.Default.SongsFolder = userSongsFolder;
                    Properties.Settings.Default.Save();
                }
            }
            osuReader.TryRead(osuReader.OsuMemoryAddresses.GeneralData);
            OsuMemoryStatus status = (OsuMemoryStatus)(osuReader.OsuMemoryAddresses.GeneralData.OsuStatus);

            if (status == OsuMemoryStatus.SongSelect || status == OsuMemoryStatus.MultiplayerRoom || status == OsuMemoryStatus.MultiplayerSongSelect)
                mapSelectScreen = true;
            else
                mapSelectScreen = false;

            if (mapSelectScreen && !hooked)
            {
                kbhook.hook();
                hooked = true;
            }
            if (!mapSelectScreen && hooked)
            {
                kbhook.unhook();
                hooked = false;
            }
        }

#endregion Timer events

#region borderless window title bar

        private bool Drag;
        private int MouseX;
        private int MouseY;

        private void PanelMove_MouseDown(object sender, MouseEventArgs e)
        {
            Drag = true;
            MouseX = Cursor.Position.X - this.Left;
            MouseY = Cursor.Position.Y - this.Top;
        }

        private void PanelMove_MouseMove(object sender, MouseEventArgs e)
        {
            if (Drag)
            {
                Top = Cursor.Position.Y - MouseY;
                Left = Cursor.Position.X - MouseX;
            }
        }

        private void PanelMove_MouseUp(object sender, MouseEventArgs e) => Drag = false;

        private void closeButton_Click(object sender, EventArgs e) => Close();

        private void minimizeButton_Click(object sender, EventArgs e) => WindowState = FormWindowState.Minimized;

#endregion borderless window title bar

#region Background Worker

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            editor.GenerateBeatmap();
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show($"Your map couldn't be generated because of the following error:\n{e.Error.ToString()}", "Generating Map failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // TODO: Catch UnauthorizedAccessException
            }
            else
            {
                //GenerateMapButton.Progress = 0f;
            }
        }

#endregion Background Worker

        private void titlePanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Icon smallIcon = new Icon(this.Icon, 16, 16);
            e.Graphics.DrawIcon(smallIcon, 10, 10);
            e.Graphics.DrawString(Text, new Font(Font, FontStyle.Regular), Brushes.White, 10 + 16 + 4, 10);
        }


        private void BpmMultiplierTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                BpmMultiplierTextBox_Submit(sender, e);
                e.Handled = e.SuppressKeyPress = true; // silence annoying windows bell
            }
        }

        private void BpmSlider_ValueChanged(object sender, EventArgs e)
        {
            editor.SetBpmMultiplier(BpmSlider.Value);
        }

        private void SongsFolderButton_Click(object sender, EventArgs e)
        {
            var songsFolderForm = new SongsFolderForm(userSongsFolder);
            if (songsFolderForm.ShowDialog() == DialogResult.OK)
            {
                if (!Directory.Exists(songsFolderForm.SongsFolder))
                {
                    MessageBox.Show("Could not find that directory. Make sure it was typed correctly and that it actually exists.");
                    return;
                }
                userSongsFolder = songsFolderForm.SongsFolder.TrimEnd(Path.DirectorySeparatorChar);
                Properties.Settings.Default.SongsFolder = userSongsFolder;
                Properties.Settings.Default.Save();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            editor.SaveSettings();
            Properties.Settings.Default.DanButtons = FormatDanButtons(danTargets);
            // save hotkeys
            Properties.Settings.Default.HotkeyCreateMap = Hotkeys[0];
            Properties.Settings.Default.HotkeyProfile1 = Hotkeys[1];
            Properties.Settings.Default.HotkeyProfile2 = Hotkeys[2];
            Properties.Settings.Default.HotkeyProfile3 = Hotkeys[3];
            Properties.Settings.Default.HotkeyProfile4 = Hotkeys[4];
            Properties.Settings.Default.Save();
        }

#region Dan buttons
        // Four buttons that set the rate to where the map reaches a dan (BeatmapEditor.SetRateForDan), each with a
        // "Change dan" menu under it. Their dans are the DanButtons setting; Alt+Shift+(hotkey) presses one.
        private static readonly string[] DefaultDanButtons = { "Alpha", "Beta", "Gamma", "Delta" };
        private const string DanMenuButtonText = "Change dan ▼";
        private static readonly Color DanMenuButtonColor = Color.FromArgb(90, 90, 134);
        private static readonly TimeSpan DanStatusHold = TimeSpan.FromSeconds(1.6);
        private static readonly TimeSpan DanStatusFade = TimeSpan.FromSeconds(0.5);

        private OsuButton[] danButtons;
        private Button[] danMenuButtons;
        private DanLevel[] danTargets;
        private ContextMenuStrip danMenu;
        private int danMenuSlot; // the button the menu was opened for
        private readonly ToolTip danButtonToolTip;
        private readonly DanButtonStatus[] danStatus = { new DanButtonStatus(), new DanButtonStatus(), new DanButtonStatus(), new DanButtonStatus() };
        private int danStatusCount;

        // What a dan button's menu button says: "Finding rate..." while the search runs, then what it found. A rate
        // that was set fades back to "Change dan" the way upstream's "Saved!" did (the rate box shows the rate); a dan
        // out of reach, or a map that can't be rated, stays until the next press, map, long note setting or Reset.
        private sealed class DanButtonStatus
        {
            public int Id;        // the press it belongs to
            public string Text;   // null when there's nothing to show
            public Color Color;
            public bool Busy;     // stays until the search ends
            public bool Keep;     // doesn't fade
            public DateTime Shown;
        }

        /// <summary>
        /// The DanButtons setting, e.g. "Alpha,Beta,Gamma,Delta": one dan per button. A missing or unknown name falls
        /// back to that button's default.
        /// </summary>
        internal static DanLevel[] ParseDanButtons(string setting)
        {
            var names = (setting ?? "").Split(',');
            var targets = new DanLevel[DefaultDanButtons.Length];
            for (int i = 0; i < targets.Length; i++)
                targets[i] = (i < names.Length ? DanTable.FindByName(names[i]) : null) ?? DanTable.FindByName(DefaultDanButtons[i]);
            return targets;
        }

        internal static string FormatDanButtons(DanLevel[] targets) => string.Join(",", targets.Select(t => t.Name));

        private void UpdateDanButtons(object sender, EventArgs e)
        {
            bool visible = editor.State != EditorState.NOT_READY;
            bool usable = editor.CanSetRateForDan; // 4K osu!mania, and not while a map is being created
            for (int i = 0; i < danButtons.Length; i++)
            {
                var button = danButtons[i];
                var target = danTargets[i];
                button.Visible = visible;
                danMenuButtons[i].Visible = visible;
                button.Text = target.DisplayName;
                button.Enabled = usable;
                button.ForeColor = usable ? Color.White : Colors.Disabled;
                var color = usable ? DanButtonColor(target.Color) : Colors.TextBoxBg;
                if (button.Color != color)
                    button.Color = color;
                danButtonToolTip.SetToolTip(button, string.Format(CultureInfo.InvariantCulture,
                    "Sets the rate to the lowest at which this map rates as {0} (xdc1 {1} or more).\nAlt+Shift+{2} does the same, then Create Map.",
                    target.DisplayName, DanTable.FormatRating(target.Threshold), HotkeyForm.KeyName(Hotkeys[i + 1])));
                danButtonToolTip.SetToolTip(danMenuButtons[i], "Choose this button's dan");
            }
        }

        private async void danButton_Click(object sender, EventArgs e) => await PressDanButton(Array.IndexOf(danButtons, sender));

        // A dan button is filled with its dan's colour (the colour its name has everywhere else). Colours brighter
        // than DanButtonMaxBrightness (perceived brightness, 0-255; Zeta's light blue the most) are
        // darkened to it, keeping their hue, so the white name on the button stays readable.
        internal const double DanButtonMaxBrightness = 150;
        internal static Color DanButtonColor(Color dan)
        {
            double brightness = 0.299 * dan.R + 0.587 * dan.G + 0.114 * dan.B;
            if (brightness <= DanButtonMaxBrightness)
                return dan;
            double scale = DanButtonMaxBrightness / brightness;
            return Color.FromArgb((int)System.Math.Round(dan.R * scale), (int)System.Math.Round(dan.G * scale), (int)System.Math.Round(dan.B * scale));
        }

        // Alt+Shift+(hotkey): that dan button, then Create Map if the map now differs, as upstream's profile hotkeys
        // did. The player is in osu! then, so a press that can't set a rate says so with a sound.
        private async void DanHotkeyHandler(object sender, EventArgs e)
        {
            var k = (KeyEventArgs)e;
            int slot = DanButtonForHotkey(k);
            if (slot < 0 || IsDanKeyRepeat(k.KeyCode))
                return;
            // the program the key was typed into: the hook sees the key before that window does
            uint keyProcess = ForegroundProcessId();
            // no 4K osu!mania map loaded, a map being created, or a map osu! has just switched to still being read
            // (the press would be for the map on its way out; pressing again once it's in works)
            if (!editor.CanSetRateForDan || editor.IsLoadingBeatmap)
            {
                DanHotkeyFailed(keyProcess);
                return;
            }
            var outcome = await PressDanButton(slot);
            if (outcome == DanRateOutcome.Reached)
            {
                if (GenerateMapButton.Enabled)
                    CreateMapFromHotkey();
            }
            else if (outcome != null)
                DanHotkeyFailed(keyProcess); // out of reach between 0.50x and 3.00x, or no rating (null: overtaken, say nothing)
        }

        // A held key repeats its key-down: only the first one presses the button (a repeat would otherwise press it
        // again, sound the warning while the map is being created, or create it twice). A key-down long after the
        // last one is a new press even without a key-up in between, which can be missed while the hook is off.
        private Keys heldDanKey = Keys.None;
        private DateTime heldDanKeyTime;
        private static readonly TimeSpan DanKeyRepeatGap = TimeSpan.FromSeconds(1.5);

        private bool IsDanKeyRepeat(Keys key)
        {
            var now = DateTime.Now;
            bool repeat = key == heldDanKey && now - heldDanKeyTime < DanKeyRepeatGap;
            heldDanKey = key;
            heldDanKeyTime = now;
            return repeat;
        }

        // by key alone: Alt and Shift are often let go first
        private void DanHotkeyReleased(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == heldDanKey)
                heldDanKey = Keys.None;
        }

        // What a dan hotkey that set no rate sounds like: Windows' warning sound. Only when the key was typed into osu!
        // or mania-trainer (keyProcess: the active program at the key press, not when the search ends). The keyboard
        // hook also sees keys typed into other programs while osu! sits at song select (Alt+Shift+F is Format
        // Document in VS Code), and a warning there would be about nothing.
        protected virtual void DanHotkeyFailed(uint keyProcess)
        {
            if (IsOsuOrTrainer(keyProcess))
                SystemSounds.Exclamation.Play();
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        private static uint ForegroundProcessId()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return 0;
            GetWindowThreadProcessId(window, out uint processId);
            return processId;
        }

        internal static bool IsOsuOrTrainer(uint processId)
        {
            if (processId == 0)
                return false;
            using (var self = Process.GetCurrentProcess())
                if (self.Id == processId)
                    return true;
            bool osu = false;
            foreach (var process in Process.GetProcessesByName("osu!"))
                using (process)
                    osu |= process.Id == processId;
            return osu;
        }

        private int DanButtonForHotkey(KeyEventArgs k)
        {
            // Alt+Shift without Ctrl: AltGr reads as Ctrl+Alt, and AltGr+Shift+key types a character on some
            // layouts (Polish: Ś, Ą), which mustn't press a dan button and create a map
            if (!k.Alt || !k.Shift || k.Control)
                return -1;
            for (int i = 0; i < danButtons.Length; i++)
                if (Hotkeys[i + 1] == k.KeyCode)
                    return i;
            return -1;
        }

        /// <summary>
        /// Dan button <paramref name="slot"/>: finds and sets its rate. Returns what the search found (Reached: the rate
        /// was set), or null when the button can't be used now (also while a map osu! has just switched to is still
        /// being read) or the press was overtaken (by another press, or a change of map or setting).
        /// </summary>
        private async Task<DanRateOutcome?> PressDanButton(int slot)
        {
            if (slot < 0 || slot >= danButtons.Length || !editor.CanSetRateForDan || editor.IsLoadingBeatmap)
                return null;
            var target = danTargets[slot];
            // what the other buttons said last is about a rate this press replaces (a search of theirs is cancelled)
            ClearDanStatuses(except: slot);
            int id = ShowDanStatus(slot, "Finding rate...", Colors.PaleBlue, busy: true);
            var result = await editor.SetRateForDan(target);
            if (IsDisposed)
                return null;
            if (result == null)
            {
                // overtaken by another press, or the map or a setting changed while it ran
                ClearDanStatus(slot, id);
                return null;
            }
            var inv = CultureInfo.InvariantCulture;
            switch (result.Outcome)
            {
                case DanRateOutcome.Reached:
                    ShowDanStatus(slot, string.Format(inv, "Set to {0:0.00}x", result.Rate), target.TextColor);
                    break;
                case DanRateOutcome.NeedsLowerRate:
                    ShowDanStatus(slot, string.Format(inv, "Needs < {0:0.00}x", DanRateFinder.MinRate), Colors.Salmon, keep: true);
                    break;
                case DanRateOutcome.NeedsHigherRate:
                    ShowDanStatus(slot, string.Format(inv, "Needs > {0:0.00}x", DanRateFinder.MaxRate), Colors.Salmon, keep: true);
                    break;
                default:
                    ShowDanStatus(slot, "No rating", Colors.Salmon, keep: true);
                    break;
            }
            return result.Outcome;
        }

        private int ShowDanStatus(int slot, string text, Color color, bool busy = false, bool keep = false)
        {
            var status = danStatus[slot];
            status.Id = ++danStatusCount;
            status.Text = text;
            status.Color = color;
            status.Busy = busy;
            status.Keep = keep;
            status.Shown = DateTime.Now;
            danMenuButtons[slot].Text = text;
            danMenuButtons[slot].ForeColor = color;
            return status.Id;
        }

        // back to "Change dan", unless the status has been replaced since (by a later press)
        private void ClearDanStatus(int slot, int id)
        {
            var status = danStatus[slot];
            if (status.Id != id)
                return;
            status.Text = null;
            status.Busy = false;
            status.Keep = false;
            danMenuButtons[slot].Text = DanMenuButtonText;
            danMenuButtons[slot].ForeColor = DanMenuButtonColor;
        }

        // every button's status but <paramref name="except"/>'s: what they found was for another press, map or setting
        private void ClearDanStatuses(int except = -1)
        {
            for (int i = 0; i < danStatus.Length; i++)
                if (i != except)
                    ClearDanStatus(i, danStatus[i].Id);
        }

        // fades each finished status back to "Change dan" (formAnimationTimer, every 16 ms)
        private void formAnimationTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            for (int i = 0; i < danStatus.Length; i++)
            {
                var status = danStatus[i];
                if (status.Text == null || status.Busy || status.Keep)
                    continue;
                var age = now - status.Shown;
                if (age < DanStatusHold)
                    continue;
                double fade = (age - DanStatusHold).TotalMilliseconds / DanStatusFade.TotalMilliseconds;
                if (fade >= 1)
                    ClearDanStatus(i, status.Id);
                else
                    danMenuButtons[i].ForeColor = JunUtils.LerpColor(status.Color, DanMenuButtonColor, (decimal)fade);
            }
        }

        // One menu for all four buttons: every dan with its icon and threshold, the button's current dan ticked.
        private ContextMenuStrip BuildDanMenu()
        {
            var menu = new ContextMenuStrip(components)
            {
                Renderer = new DarkMenuRenderer(),
                ShowCheckMargin = false,
                ShowImageMargin = true,
                ImageScalingSize = new Size(18, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = DarkMenuRenderer.Background,
            };
            foreach (var level in DanTable.Levels)
            {
                if (level.IsExtra && level.Index > 0 && !DanTable.Levels[level.Index - 1].IsExtra)
                    menu.Items.Add(new ToolStripSeparator());
                var item = new ToolStripMenuItem(level.DisplayName)
                {
                    Tag = level,
                    Image = DanTable.GetIcon(level) ?? Icons.GenerateIcon(GameMode.Mania, level.Color),
                    ImageScaling = ToolStripItemImageScaling.SizeToFit,
                    ForeColor = level.TextColor,
                    ShortcutKeyDisplayString = DanTable.FormatRating(level.Threshold),
                };
                item.Click += DanMenuItem_Click;
                menu.Items.Add(item);
            }
            return menu;
        }

        private void danMenuButton_Click(object sender, EventArgs e)
        {
            int slot = Array.IndexOf(danMenuButtons, sender);
            if (slot < 0)
                return;
            PrepareDanMenu(slot);
            danMenu.Show(danMenuButtons[slot], new Point(0, danMenuButtons[slot].Height));
        }

        // aims the menu at a button and ticks that button's dan
        private void PrepareDanMenu(int slot)
        {
            danMenuSlot = slot;
            foreach (var item in danMenu.Items.OfType<ToolStripMenuItem>())
                item.Checked = item.Tag == danTargets[slot];
        }

        private void DanMenuItem_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item) || !(item.Tag is DanLevel level))
                return;
            int slot = danMenuSlot;
            // a search still running for this button's old dan no longer applies
            if (danStatus[slot].Busy)
                editor.CancelDanRateSearch();
            danTargets[slot] = level;
            Properties.Settings.Default.DanButtons = FormatDanButtons(danTargets);
            Properties.Settings.Default.Save();
            ClearDanStatus(slot, danStatus[slot].Id);
            UpdateDanButtons(this, EventArgs.Empty);
        }
#endregion

        private void RearrangeLayout(object sender, EventArgs e)
        {
            bool danButtonsVisible = editor.State != EditorState.NOT_READY;
            if (!danButtonsVisible)
            {
                // not ready layout
                middlePanel.Height = 110;
                BottomPanel.Height = 111;
                Height = NotReadyHeight + (extrasPanel.Visible ? extrasPanel.Height : 0);
            }
            else
            {
                // ready layout (with the dan buttons)
                middlePanel.Height = 161;
                BottomPanel.Height = 111 - 33;
                Height = ReadyHeight + (extrasPanel.Visible ? extrasPanel.Height : 0);
            }
        }
        private void showExtrasButton_Click(object sender, EventArgs e)
        {
            if (!extrasPanel.Visible)
            {
                extrasPanel.Visible = true;
                showExtrasButton.Text = "▼ Less";
            }
            else
            {
                extrasPanel.Visible = false;
                showExtrasButton.Text = "▶ More!";
            }
            RearrangeLayout(this, EventArgs.Empty);
        }
        private void editHotkeysButton_Click(object sender, EventArgs e)
        {
            int centerX = DesktopLocation.X + (Width / 2);
            int centerY = DesktopLocation.Y + (Height / 2);
            var hotkeyForm = new HotkeyForm(centerX - 321/2, centerY - 217/2, Hotkeys, danTargets.Select(t => t.DisplayName).ToList());
            hotkeyForm.ShowDialog();
            Hotkeys = hotkeyForm.Hotkeys;
            ApplyHotkeys();
            UpdateDanButtons(this, EventArgs.Empty); // the tooltips name the hotkeys
        }


        private void spectrogramButton_Click(object sender, EventArgs e)
        {
        }
    }
}
