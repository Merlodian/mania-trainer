using Microsoft.WindowsAPICodePack.Dialogs;
using osu_trainer.Controls;
using osu_trainer.Mania;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace osu_trainer
{
    /// <summary>
    /// Rates every 4K chart in the osu! Songs folder and writes osu! (stable) collections, one per dan:
    /// "xdc1 01 - 1st Dan" ... "xdc1 20 - Kappa", and "xdc1 00 - Below 1st Dan" if wanted.
    /// </summary>
    public class DanCollectionsForm : Form
    {
        private static readonly Color PanelBg = Color.FromArgb(31, 29, 44);
        private static readonly Color Muted = Color.FromArgb(150, 146, 170);

        private readonly TextBox osuFolderBox;
        private readonly Button browseButton;
        private readonly Label songsLabel;
        private readonly OsuCheckBox belowCheck;
        private readonly OsuCheckBox splitCheck;
        private readonly ListView list;
        private readonly Label statusLabel;
        private readonly OsuButton scanButton;
        private readonly OsuButton writeButton;
        private readonly OsuButton closeButton;
        private readonly System.Windows.Forms.Timer progressTimer;

        private DanCollectionScanner scanner;
        private CancellationTokenSource cancel;
        private Task<DanScanResult> scanTask;
        private bool closeWhenStopped;

        // rated charts by dan index (-1: below 1st dan), from the last finished scan
        private readonly SortedDictionary<int, List<RatedChart>> groups = new SortedDictionary<int, List<RatedChart>>();
        private string scannedOsuFolder;

        public DanCollectionsForm(string songsFolderHint)
        {
            Text = "Dan collections";
            Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Colors.FormBg;
            ForeColor = Colors.TextBoxFg;
            ClientSize = new Size(580, 600);
            MinimumSize = new Size(480, 500);
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var intro = new Label
            {
                Location = new Point(14, 12),
                Size = new Size(552, 58),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = "Rates every 4K chart in your osu! Songs folder (at 1.00x) and puts them into osu! collections, "
                     + "one for each dan. Your other collections are kept, and collection.db is backed up first.",
            };

            var folderLabel = new Label { Location = new Point(14, 76), AutoSize = true, Text = "osu! folder", ForeColor = Colors.PaleBlue };
            osuFolderBox = new TextBox
            {
                Location = new Point(14, 98),
                Size = new Size(458, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Colors.TextBoxBg,
                ForeColor = Colors.TextBoxFg,
                BorderStyle = BorderStyle.FixedSingle,
            };
            browseButton = new Button
            {
                Location = new Point(480, 96),
                Size = new Size(86, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = "Browse…",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Colors.PaleBlue,
                BackColor = PanelBg,
            };
            browseButton.FlatAppearance.BorderColor = Color.FromArgb(70, 66, 96);
            songsLabel = new Label
            {
                Location = new Point(14, 128),
                Size = new Size(552, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                ForeColor = Muted,
            };
            belowCheck = new OsuCheckBox
            {
                Location = new Point(12, 152),
                Size = new Size(380, 24),
                Text = "Also make a \"Below 1st Dan\" collection",
                TextOnRight = true,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                ForeColor = Colors.PaleBlue,
            };
            // the check box right-aligns its text, so it has to be just wide enough for it
            belowCheck.Width = 40 + TextRenderer.MeasureText(belowCheck.Text, belowCheck.Font).Width;
            splitCheck = new OsuCheckBox
            {
                Location = new Point(12, 182),
                Size = new Size(380, 24),
                Text = "Split each dan into Low, Mid and High",
                TextOnRight = true,
                Font = belowCheck.Font,
                ForeColor = Colors.PaleBlue,
            };
            splitCheck.Width = 40 + TextRenderer.MeasureText(splitCheck.Text, splitCheck.Font).Width;
            var splitNote = new Label
            {
                Location = new Point(40, 206),
                Size = new Size(526, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                ForeColor = Muted,
                Text = "By thirds of the way to the next dan. Kappa and Below 1st Dan stay whole.",
            };

            list = new ListView
            {
                Location = new Point(14, 234),
                Size = new Size(552, 254),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BackColor = PanelBg,
                ForeColor = Colors.TextBoxFg,
                BorderStyle = BorderStyle.None,
                OwnerDraw = true,
                MultiSelect = false,
            };
            list.Columns.Add("Collection", 250);
            list.Columns.Add("Charts", 90, HorizontalAlignment.Right);
            list.Columns.Add("Ratings", 180, HorizontalAlignment.Right);
            list.DrawColumnHeader += DrawHeader;
            list.DrawItem += (s, e) => e.DrawDefault = true;
            list.DrawSubItem += (s, e) => e.DrawDefault = true;
            list.Resize += (s, e) => FitLastColumn();
            list.HandleCreated += (s, e) => FitLastColumn();

            statusLabel = new Label
            {
                Location = new Point(14, 496),
                Size = new Size(552, 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Muted,
            };

            scanButton = MakeButton("Scan", Color.SteelBlue, new Point(14, 552), new Size(120, 34), AnchorStyles.Bottom | AnchorStyles.Left);
            scanButton.ProgressColor = Color.FromArgb(90, 170, 255);
            writeButton = MakeButton("Write collections", Colors.AccentPink2, new Point(142, 552), new Size(170, 34), AnchorStyles.Bottom | AnchorStyles.Left);
            closeButton = MakeButton("Close", Color.DimGray, new Point(466, 552), new Size(100, 34), AnchorStyles.Bottom | AnchorStyles.Right);
            writeButton.Enabled = false;

            Controls.AddRange(new Control[] { intro, folderLabel, osuFolderBox, browseButton, songsLabel, belowCheck, splitCheck, splitNote, list, statusLabel, scanButton, writeButton, closeButton });

            progressTimer = new System.Windows.Forms.Timer { Interval = 100 };
            progressTimer.Tick += (s, e) => ShowProgress();

            browseButton.Click += Browse;
            osuFolderBox.TextChanged += (s, e) => FolderChanged();
            belowCheck.CheckedChanged += (s, e) => ShowGroups();
            splitCheck.CheckedChanged += (s, e) => ShowGroups();
            scanButton.Click += ScanOrStop;
            writeButton.Click += Write;
            closeButton.Click += (s, e) => Close();
            CancelButton = closeButton;

            var saved = DanCollectionsSettings.Load();
            belowCheck.Checked = saved.Below;
            splitCheck.Checked = saved.Split;
            string folder = OsuCollections.IsOsuFolder(saved.OsuFolder) ? saved.OsuFolder : OsuCollections.DetectOsuFolders(songsFolderHint).FirstOrDefault();
            osuFolderBox.Text = folder ?? "";
            FolderChanged(); // also when the text stayed empty, which raises no TextChanged
            if (folder == null)
                statusLabel.Text = "Couldn't find osu! on its own. Choose its folder with Browse.";
        }

        private static OsuButton MakeButton(string text, Color color, Point location, Size size, AnchorStyles anchor) => new OsuButton
        {
            Text = text,
            Color = color,
            Location = location,
            Size = size,
            Anchor = anchor,
            ForeColor = Color.White,
            BrightnessRange = 0.05F,
            TriangleCount = 30,
            Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
            Subtext = "",
            ProgressColor = Color.Transparent,
        };

        // the last column fills the list, so no unpainted header shows to its right
        private void FitLastColumn()
        {
            if (list.Columns.Count == 0)
                return;
            int others = 0;
            for (int i = 0; i < list.Columns.Count - 1; i++)
                others += list.Columns[i].Width;
            // leaving room for the vertical scroll bar, which comes after this runs: an exact fit to the client width
            // would then bring up a horizontal one
            list.Columns[list.Columns.Count - 1].Width = Math.Max(80, list.Width - SystemInformation.VerticalScrollBarWidth - others - 2);
        }

        private void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var bg = new SolidBrush(Colors.FormBg))
                e.Graphics.FillRectangle(bg, e.Bounds);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                      | (e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
            var bounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Font, bounds, Colors.PaleBlue, flags);
        }

        private string OsuFolder => osuFolderBox.Text.Trim().Length == 0 ? "" : OsuCollections.NormalizeFolder(osuFolderBox.Text.Trim());

        private void Browse(object sender, EventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Choose your osu! folder" })
            {
                if (Directory.Exists(OsuFolder))
                    dialog.InitialDirectory = OsuFolder;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    osuFolderBox.Text = dialog.FileName;
            }
        }

        private void FolderChanged()
        {
            groups.Clear();
            list.Items.Clear();
            writeButton.Enabled = false;
            statusLabel.Text = "";
            string folder = OsuFolder;
            if (!OsuCollections.IsOsuFolder(folder))
            {
                songsLabel.Text = folder.Length == 0 ? "" : "This doesn't look like an osu! folder (no osu!.exe or osu!.db).";
                scanButton.Enabled = false;
                return;
            }
            string songs = OsuCollections.SongsFolder(folder);
            bool ok = Directory.Exists(songs);
            songsLabel.Text = "Songs folder: " + songs + (ok ? "" : "  (not found)");
            scanButton.Enabled = ok;
        }

        // ---- scanning -------------------------------------------------------------------------------------------

        private async void ScanOrStop(object sender, EventArgs e)
        {
            if (scanTask != null)
            {
                cancel.Cancel();
                scanButton.Enabled = false;
                statusLabel.Text = "Stopping…";
                return;
            }

            string folder = OsuFolder;
            DanCollectionsSettings.Save(new DanCollectionsSettings { OsuFolder = folder, Below = belowCheck.Checked, Split = splitCheck.Checked });
            groups.Clear();
            list.Items.Clear();
            writeButton.Enabled = false;
            osuFolderBox.Enabled = browseButton.Enabled = false;
            scanButton.Text = "Stop";
            scanButton.Progress = 0;

            scanner = new DanCollectionScanner(OsuCollections.SongsFolder(folder));
            cancel = new CancellationTokenSource();
            var token = cancel.Token;
            var current = scanner;
            scanTask = Task.Run(() => current.Run(token));
            progressTimer.Start();
            ShowProgress();

            DanScanResult result = null;
            string error = null;
            try
            {
                result = await scanTask;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            progressTimer.Stop();
            scanTask = null;
            cancel.Dispose();
            cancel = null;
            if (closeWhenStopped)
            {
                Close();
                return;
            }
            scanButton.Text = "Scan";
            scanButton.Progress = 0;
            scanButton.Enabled = true;
            osuFolderBox.Enabled = browseButton.Enabled = true;

            if (error != null)
            {
                statusLabel.Text = "The scan failed: " + error;
                return;
            }
            if (result.Cancelled)
            {
                statusLabel.Text = "Stopped. Ratings found so far are saved, so the next scan carries on from here.";
                return;
            }

            scannedOsuFolder = folder;
            foreach (var chart in result.Charts)
            {
                int index = DanTable.Classify(chart.Rating)?.Index ?? -1;
                if (!groups.TryGetValue(index, out var charts))
                    groups[index] = charts = new List<RatedChart>();
                charts.Add(chart);
            }
            foreach (var charts in groups.Values)
                charts.Sort((a, b) => a.Rating.CompareTo(b.Rating));

            var inv = CultureInfo.InvariantCulture;
            string time = result.Elapsed.TotalSeconds < 90
                ? string.Format(inv, "{0:0}s", Math.Max(1, result.Elapsed.TotalSeconds))
                : string.Format(inv, "{0}m {1:00}s", (int)result.Elapsed.TotalMinutes, result.Elapsed.Seconds);
            statusLabel.Text = string.Format(inv, "{0:N0} 4K charts rated in {1}.", result.Charts.Count, time)
                + (result.Unrated > 0 ? string.Format(inv, " {0:N0} couldn't be rated and are left out.", result.Unrated) : "");
            ShowGroups();
        }

        private void ShowProgress()
        {
            var s = scanner;
            if (s == null || scanTask == null)
                return;
            if (cancel != null && cancel.IsCancellationRequested)
                return;
            int total = s.Total, done = s.Done;
            if (total > 0)
            {
                scanButton.Progress = (float)done / total;
                statusLabel.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1:N0} / {2:N0} new or changed files", s.Phase, done, total);
            }
            else
                statusLabel.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1:N0} .osu files", s.Phase, s.Found);
        }

        // ---- the collections ------------------------------------------------------------------------------------

        private List<(string name, List<string> hashes, List<RatedChart> charts, DanLevel dan)> DanCollections()
        {
            var result = new List<(string, List<string>, List<RatedChart>, DanLevel)>();
            foreach (var kv in groups)
            {
                if (kv.Key < 0 && !belowCheck.Checked)
                    continue;
                var dan = kv.Key < 0 ? null : DanTable.Levels[kv.Key];
                // Kappa has no next dan to measure towards, and below 1st dan isn't a dan: both stay whole
                if (splitCheck.Checked && dan != null && DanTable.Next(dan) != null)
                {
                    for (int tier = 0; tier < OsuCollections.TierNames.Length; tier++)
                    {
                        var charts = kv.Value.Where(c => OsuCollections.Tier(c.Rating) == tier).ToList();
                        if (charts.Count > 0)
                            result.Add((OsuCollections.DanTierCollectionName(dan, tier), Hashes(charts), charts, dan));
                    }
                }
                else
                    result.Add((OsuCollections.DanCollectionName(dan), Hashes(kv.Value), kv.Value, dan));
            }
            return result;
        }

        private static List<string> Hashes(IEnumerable<RatedChart> charts) =>
            charts.Select(c => c.Md5).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        private void ShowGroups()
        {
            list.BeginUpdate();
            list.Items.Clear();
            var cols = DanCollections();
            foreach (var c in cols)
            {
                string range = c.charts.Count == 0 ? "" :
                    DanTable.FormatRating(c.charts[0].Rating) + " – " + DanTable.FormatRating(c.charts[c.charts.Count - 1].Rating);
                var item = new ListViewItem(new[] { c.name, c.hashes.Count.ToString("N0", CultureInfo.InvariantCulture), range })
                {
                    ForeColor = c.dan?.TextColor ?? Muted,
                    BackColor = PanelBg,
                };
                list.Items.Add(item);
            }
            list.EndUpdate();
            FitLastColumn(); // the scroll bar may have come or gone
            writeButton.Enabled = cols.Count > 0 && scanTask == null;
        }

        private void Write(object sender, EventArgs e)
        {
            string folder = scannedOsuFolder;
            if (folder == null)
                return;
            if (OsuCollections.IsOsuRunningFrom(folder))
            {
                MessageBox.Show(this, "osu! is running.\n\nClose it first: osu! keeps its collections in memory and saves them over "
                    + "collection.db when it exits, which would undo this.", "Dan collections", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var cols = DanCollections();
            int charts = cols.Sum(c => c.hashes.Count);
            string path = Path.Combine(folder, "collection.db");
            if (MessageBox.Show(this, string.Format(CultureInfo.InvariantCulture, "Write {0} collections ({1:N0} charts) to\n{2}?", cols.Count, charts, path),
                    "Dan collections", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            DanCollectionsSettings.Save(new DanCollectionsSettings { OsuFolder = folder, Below = belowCheck.Checked, Split = splitCheck.Checked });
            string backup;
            int kept;
            try
            {
                (backup, kept) = OsuCollections.SaveDanCollections(folder, cols.Select(c => new OsuCollection(c.name, c.hashes)).ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't write collection.db:\n" + ex.Message + "\n\nNothing was changed.", "Dan collections",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string message = string.Format(CultureInfo.InvariantCulture, "Wrote {0} dan collections ({1:N0} charts).", cols.Count, charts);
            if (kept > 0)
                message += string.Format(CultureInfo.InvariantCulture, "\nYour other {0} collection{1} were kept.", kept, kept == 1 ? "" : "s");
            if (backup != null)
                message += "\n\nThe previous collection.db was saved as\n" + Path.GetFileName(backup);
            message += "\n\nStart osu! to see them.";
            MessageBox.Show(this, message, "Dan collections", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (scanTask != null)
            {
                // let the scan stop and save its cache, then close
                closeWhenStopped = true;
                cancel?.Cancel();
                e.Cancel = true;
                statusLabel.Text = "Stopping…";
                return;
            }
            progressTimer.Stop();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                progressTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>The Dan collections window's choices, kept in %LOCALAPPDATA%\mania-trainer\dan-collections.txt.</summary>
    internal sealed class DanCollectionsSettings
    {
        public string OsuFolder = "";
        public bool Below = true;
        public bool Split = false;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mania-trainer", "dan-collections.txt");

        public static DanCollectionsSettings Load()
        {
            var s = new DanCollectionsSettings();
            try
            {
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0)
                        continue;
                    string key = line.Substring(0, eq), value = line.Substring(eq + 1);
                    if (key == "osu")
                        s.OsuFolder = value;
                    else if (key == "below")
                        s.Below = value != "0";
                    else if (key == "split")
                        s.Split = value == "1";
                }
            }
            catch (Exception) { }
            return s;
        }

        public static void Save(DanCollectionsSettings s)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, new[] { "osu=" + s.OsuFolder, "below=" + (s.Below ? "1" : "0"), "split=" + (s.Split ? "1" : "0") });
            }
            catch (Exception) { }
        }
    }
}
