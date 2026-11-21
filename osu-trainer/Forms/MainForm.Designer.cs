using osu_trainer.Controls;

namespace osu_trainer
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.BeatmapUpdateTimer = new System.Windows.Forms.Timer(this.components);
            this.middlePanel = new System.Windows.Forms.Panel();
            this.showExtrasButton = new System.Windows.Forms.Button();
            this.danButton4 = new osu_trainer.Controls.OsuButton();
            this.danButton3 = new osu_trainer.Controls.OsuButton();
            this.danButton2 = new osu_trainer.Controls.OsuButton();
            this.danMenuButton4 = new System.Windows.Forms.Button();
            this.danButton1 = new osu_trainer.Controls.OsuButton();
            this.danMenuButton3 = new System.Windows.Forms.Button();
            this.danMenuButton2 = new System.Windows.Forms.Button();
            this.danMenuButton1 = new System.Windows.Forms.Button();
            this.BpmLockCheck = new osu_trainer.Controls.ToggleIconButton();
            this.BpmSlider = new osu_trainer.OptionSlider();
            this.FullLNCheck = new osu_trainer.Controls.OsuCheckBox();
            this.NoLNCheck = new osu_trainer.Controls.OsuCheckBox();
            this.BpmMultiplierTextBox = new System.Windows.Forms.TextBox();
            this.NewBpmTextBox = new osu_trainer.Controls.ScrollableTextBox();
            this.NewBpmRangeTextBox = new System.Windows.Forms.TextBox();
            this.OriginalBpmRangeTextBox = new System.Windows.Forms.TextBox();
            this.OriginalBpmTextBox = new System.Windows.Forms.TextBox();
            this.OriginalBpmLabel = new osu_trainer.Controls.AntiAliasedLabel();
            this.NewBpmLabel = new osu_trainer.Controls.AntiAliasedLabel();
            this.BpmMultiplierLabel = new osu_trainer.Controls.AntiAliasedLabel();
            this.editHotkeysButton = new System.Windows.Forms.Button();
            this.Middle1Panel = new System.Windows.Forms.Panel();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.ODLockCheck = new osu_trainer.Controls.ToggleIconButton();
            this.odlabel = new osu_trainer.Controls.AntiAliasedLabel();
            this.ODDisplay = new System.Windows.Forms.TextBox();
            this.ODSlider = new osu_trainer.OptionSlider();
            this.hplabel = new osu_trainer.Controls.AntiAliasedLabel();
            this.HPDisplay = new System.Windows.Forms.TextBox();
            this.HPSlider = new osu_trainer.OptionSlider();
            this.HPLockCheck = new osu_trainer.Controls.ToggleIconButton();
            this.BottomPanel = new System.Windows.Forms.Panel();
            this.ResetButton = new osu_trainer.Controls.OsuButton();
            this.GenerateMapButton = new osu_trainer.Controls.OsuButton();
            this.SongsFolderButton = new osu_trainer.Controls.OsuButton();
            this.TopPanel = new System.Windows.Forms.Panel();
            this.SongDisplay = new osu_trainer.Controls.SongDisplay();
            this.OsuRunningTimer = new System.Windows.Forms.Timer(this.components);
            this.closeButton = new System.Windows.Forms.Button();
            this.minimizeButton = new System.Windows.Forms.Button();
            this.titlePanel = new System.Windows.Forms.Panel();
            this.BackgroundWorker = new System.ComponentModel.BackgroundWorker();
            this.formAnimationTimer = new System.Windows.Forms.Timer(this.components);
            this.extrasPanel = new System.Windows.Forms.Panel();
            this.ChangePitchCheck = new osu_trainer.Controls.OsuCheckBox();
            this.highQualityMp3Check = new osu_trainer.Controls.OsuCheckBox();
            this.NoSpinnersCheck = new osu_trainer.Controls.OsuCheckBox();
            this.button1 = new System.Windows.Forms.Button();
            this.middlePanel.SuspendLayout();
            this.Middle1Panel.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.BottomPanel.SuspendLayout();
            this.TopPanel.SuspendLayout();
            this.titlePanel.SuspendLayout();
            this.extrasPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // BeatmapUpdateTimer
            // 
            this.BeatmapUpdateTimer.Interval = 20;
            this.BeatmapUpdateTimer.Tick += new System.EventHandler(this.BeatmapUpdateTimer_Tick);
            // 
            // middlePanel
            // 
            this.middlePanel.Controls.Add(this.showExtrasButton);
            this.middlePanel.Controls.Add(this.danButton4);
            this.middlePanel.Controls.Add(this.danButton3);
            this.middlePanel.Controls.Add(this.danButton2);
            this.middlePanel.Controls.Add(this.danMenuButton4);
            this.middlePanel.Controls.Add(this.danButton1);
            this.middlePanel.Controls.Add(this.danMenuButton3);
            this.middlePanel.Controls.Add(this.danMenuButton2);
            this.middlePanel.Controls.Add(this.danMenuButton1);
            this.middlePanel.Controls.Add(this.BpmLockCheck);
            this.middlePanel.Controls.Add(this.BpmSlider);
            this.middlePanel.Controls.Add(this.FullLNCheck);
            this.middlePanel.Controls.Add(this.NoLNCheck);
            this.middlePanel.Controls.Add(this.BpmMultiplierTextBox);
            this.middlePanel.Controls.Add(this.NewBpmTextBox);
            this.middlePanel.Controls.Add(this.NewBpmRangeTextBox);
            this.middlePanel.Controls.Add(this.OriginalBpmRangeTextBox);
            this.middlePanel.Controls.Add(this.OriginalBpmTextBox);
            this.middlePanel.Controls.Add(this.OriginalBpmLabel);
            this.middlePanel.Controls.Add(this.NewBpmLabel);
            this.middlePanel.Controls.Add(this.BpmMultiplierLabel);
            this.middlePanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.middlePanel.Location = new System.Drawing.Point(0, 216);
            this.middlePanel.Name = "middlePanel";
            this.middlePanel.Padding = new System.Windows.Forms.Padding(10, 5, 10, 5);
            this.middlePanel.Size = new System.Drawing.Size(427, 162);
            this.middlePanel.TabIndex = 7;
            this.middlePanel.Click += new System.EventHandler(this.Unfocus);
            // 
            // showExtrasButton
            // 
            this.showExtrasButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.showExtrasButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.showExtrasButton.FlatAppearance.BorderSize = 0;
            this.showExtrasButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.showExtrasButton.Font = new System.Drawing.Font("Segoe UI", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.showExtrasButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.showExtrasButton.Location = new System.Drawing.Point(0, 142);
            this.showExtrasButton.Margin = new System.Windows.Forms.Padding(0);
            this.showExtrasButton.Name = "showExtrasButton";
            this.showExtrasButton.Size = new System.Drawing.Size(427, 20);
            this.showExtrasButton.TabIndex = 26;
            this.showExtrasButton.Text = "▶ More!";
            this.showExtrasButton.UseVisualStyleBackColor = false;
            this.showExtrasButton.Click += new System.EventHandler(this.showExtrasButton_Click);
            // 
            // danButton4
            // 
            this.danButton4.BrightnessRange = 0.01F;
            this.danButton4.Color = System.Drawing.Color.MediumPurple;
            this.danButton4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.danButton4.ForeColor = System.Drawing.Color.White;
            this.danButton4.Location = new System.Drawing.Point(316, 92);
            this.danButton4.Name = "danButton4";
            this.danButton4.Progress = 0F;
            this.danButton4.ProgressColor = System.Drawing.Color.Transparent;
            this.danButton4.Size = new System.Drawing.Size(96, 24);
            this.danButton4.Subtext = "";
            this.danButton4.SubtextColor = System.Drawing.Color.Empty;
            this.danButton4.TabIndex = 24;
            this.danButton4.Text = "Delta";
            this.danButton4.TextYOffset = 0;
            this.danButton4.TriangleCount = 30;
            this.danButton4.UseVisualStyleBackColor = true;
            this.danButton4.Click += new System.EventHandler(this.danButton_Click);
            // 
            // danButton3
            // 
            this.danButton3.BrightnessRange = 0.01F;
            this.danButton3.Color = System.Drawing.Color.MediumPurple;
            this.danButton3.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.danButton3.ForeColor = System.Drawing.Color.White;
            this.danButton3.Location = new System.Drawing.Point(214, 92);
            this.danButton3.Name = "danButton3";
            this.danButton3.Progress = 0F;
            this.danButton3.ProgressColor = System.Drawing.Color.Transparent;
            this.danButton3.Size = new System.Drawing.Size(96, 24);
            this.danButton3.Subtext = "";
            this.danButton3.SubtextColor = System.Drawing.Color.Empty;
            this.danButton3.TabIndex = 24;
            this.danButton3.Text = "Gamma";
            this.danButton3.TextYOffset = 0;
            this.danButton3.TriangleCount = 30;
            this.danButton3.UseVisualStyleBackColor = true;
            this.danButton3.Click += new System.EventHandler(this.danButton_Click);
            // 
            // danButton2
            // 
            this.danButton2.BrightnessRange = 0.01F;
            this.danButton2.Color = System.Drawing.Color.MediumPurple;
            this.danButton2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.danButton2.ForeColor = System.Drawing.Color.White;
            this.danButton2.Location = new System.Drawing.Point(112, 92);
            this.danButton2.Name = "danButton2";
            this.danButton2.Progress = 0F;
            this.danButton2.ProgressColor = System.Drawing.Color.Transparent;
            this.danButton2.Size = new System.Drawing.Size(96, 24);
            this.danButton2.Subtext = "";
            this.danButton2.SubtextColor = System.Drawing.Color.Empty;
            this.danButton2.TabIndex = 24;
            this.danButton2.Text = "Beta";
            this.danButton2.TextYOffset = 0;
            this.danButton2.TriangleCount = 30;
            this.danButton2.UseVisualStyleBackColor = true;
            this.danButton2.Click += new System.EventHandler(this.danButton_Click);
            // 
            // danMenuButton4
            // 
            this.danMenuButton4.FlatAppearance.BorderSize = 0;
            this.danMenuButton4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.danMenuButton4.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.danMenuButton4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(134)))));
            this.danMenuButton4.Location = new System.Drawing.Point(316, 115);
            this.danMenuButton4.Name = "danMenuButton4";
            this.danMenuButton4.Size = new System.Drawing.Size(96, 20);
            this.danMenuButton4.TabIndex = 25;
            this.danMenuButton4.Text = "Change dan ▼";
            this.danMenuButton4.UseVisualStyleBackColor = true;
            this.danMenuButton4.Click += new System.EventHandler(this.danMenuButton_Click);
            // 
            // danButton1
            // 
            this.danButton1.BrightnessRange = 0.01F;
            this.danButton1.Color = System.Drawing.Color.MediumPurple;
            this.danButton1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.danButton1.ForeColor = System.Drawing.Color.White;
            this.danButton1.Location = new System.Drawing.Point(10, 92);
            this.danButton1.Name = "danButton1";
            this.danButton1.Progress = 0F;
            this.danButton1.ProgressColor = System.Drawing.Color.Transparent;
            this.danButton1.Size = new System.Drawing.Size(96, 24);
            this.danButton1.Subtext = "";
            this.danButton1.SubtextColor = System.Drawing.Color.Empty;
            this.danButton1.TabIndex = 24;
            this.danButton1.Text = "Alpha";
            this.danButton1.TextYOffset = 0;
            this.danButton1.TriangleCount = 30;
            this.danButton1.UseVisualStyleBackColor = true;
            this.danButton1.Click += new System.EventHandler(this.danButton_Click);
            // 
            // danMenuButton3
            // 
            this.danMenuButton3.FlatAppearance.BorderSize = 0;
            this.danMenuButton3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.danMenuButton3.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.danMenuButton3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(134)))));
            this.danMenuButton3.Location = new System.Drawing.Point(214, 115);
            this.danMenuButton3.Name = "danMenuButton3";
            this.danMenuButton3.Size = new System.Drawing.Size(96, 20);
            this.danMenuButton3.TabIndex = 25;
            this.danMenuButton3.Text = "Change dan ▼";
            this.danMenuButton3.UseVisualStyleBackColor = true;
            this.danMenuButton3.Click += new System.EventHandler(this.danMenuButton_Click);
            // 
            // danMenuButton2
            // 
            this.danMenuButton2.FlatAppearance.BorderSize = 0;
            this.danMenuButton2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.danMenuButton2.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.danMenuButton2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(134)))));
            this.danMenuButton2.Location = new System.Drawing.Point(112, 115);
            this.danMenuButton2.Name = "danMenuButton2";
            this.danMenuButton2.Size = new System.Drawing.Size(96, 20);
            this.danMenuButton2.TabIndex = 25;
            this.danMenuButton2.Text = "Change dan ▼";
            this.danMenuButton2.UseVisualStyleBackColor = true;
            this.danMenuButton2.Click += new System.EventHandler(this.danMenuButton_Click);
            // 
            // danMenuButton1
            // 
            this.danMenuButton1.FlatAppearance.BorderSize = 0;
            this.danMenuButton1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.danMenuButton1.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.danMenuButton1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(134)))));
            this.danMenuButton1.Location = new System.Drawing.Point(10, 115);
            this.danMenuButton1.Name = "danMenuButton1";
            this.danMenuButton1.Size = new System.Drawing.Size(96, 20);
            this.danMenuButton1.TabIndex = 25;
            this.danMenuButton1.Text = "Change dan ▼";
            this.danMenuButton1.UseVisualStyleBackColor = true;
            this.danMenuButton1.Click += new System.EventHandler(this.danMenuButton_Click);
            // 
            // BpmLockCheck
            // 
            this.BpmLockCheck.CheckedImage = ((System.Drawing.Image)(resources.GetObject("BpmLockCheck.CheckedImage")));
            this.BpmLockCheck.Location = new System.Drawing.Point(136, 62);
            this.BpmLockCheck.Name = "BpmLockCheck";
            this.BpmLockCheck.Size = new System.Drawing.Size(21, 21);
            this.BpmLockCheck.TabIndex = 11;
            this.BpmLockCheck.UncheckedImage = ((System.Drawing.Image)(resources.GetObject("BpmLockCheck.UncheckedImage")));
            this.BpmLockCheck.UseVisualStyleBackColor = true;
            this.BpmLockCheck.CheckedChanged += new System.EventHandler(this.BpmLockCheck_CheckedChanged);
            // 
            // BpmSlider
            // 
            this.BpmSlider.BodyColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.BpmSlider.FillDraggingNipple = false;
            this.BpmSlider.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this.BpmSlider.Location = new System.Drawing.Point(136, 2);
            this.BpmSlider.MaxValue = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.BpmSlider.MinValue = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.BpmSlider.Name = "BpmSlider";
            this.BpmSlider.NippleColor = System.Drawing.Color.White;
            this.BpmSlider.NippleDiameter = 15;
            this.BpmSlider.NippleExpandedDiameter = 18;
            this.BpmSlider.NippleIdleColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(128)))), ((int)(((byte)(255)))));
            this.BpmSlider.NippleStrokeWidth = 0;
            this.BpmSlider.Size = new System.Drawing.Size(131, 23);
            this.BpmSlider.TabIndex = 23;
            this.BpmSlider.Text = "bpmSlider";
            this.BpmSlider.Thickness = 4;
            this.BpmSlider.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.BpmSlider.ValueChanged += new System.EventHandler(this.BpmSlider_ValueChanged);
            // 
            // FullLNCheck
            // 
            this.FullLNCheck.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.FullLNCheck.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(136)))), ((int)(((byte)(134)))), ((int)(((byte)(144)))));
            this.FullLNCheck.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.FullLNCheck.Location = new System.Drawing.Point(315, 4);
            this.FullLNCheck.Name = "FullLNCheck";
            this.FullLNCheck.Size = new System.Drawing.Size(102, 24);
            this.FullLNCheck.TabIndex = 20;
            this.FullLNCheck.Text = "Full LN";
            this.FullLNCheck.UseVisualStyleBackColor = true;
            this.FullLNCheck.CheckedChanged += new System.EventHandler(this.FullLNCheck_CheckedChanged);
            // 
            // NoLNCheck
            // 
            this.NoLNCheck.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.NoLNCheck.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(136)))), ((int)(((byte)(134)))), ((int)(((byte)(144)))));
            this.NoLNCheck.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.NoLNCheck.Location = new System.Drawing.Point(315, 33);
            this.NoLNCheck.Name = "NoLNCheck";
            this.NoLNCheck.Size = new System.Drawing.Size(102, 24);
            this.NoLNCheck.TabIndex = 20;
            this.NoLNCheck.Text = "No LN";
            this.NoLNCheck.UseVisualStyleBackColor = true;
            this.NoLNCheck.CheckedChanged += new System.EventHandler(this.NoLNCheck_CheckedChanged);
            // 
            // BpmMultiplierTextBox
            // 
            this.BpmMultiplierTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(23)))), ((int)(((byte)(16)))), ((int)(((byte)(25)))));
            this.BpmMultiplierTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.BpmMultiplierTextBox.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BpmMultiplierTextBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.BpmMultiplierTextBox.Location = new System.Drawing.Point(90, 5);
            this.BpmMultiplierTextBox.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.BpmMultiplierTextBox.Name = "BpmMultiplierTextBox";
            this.BpmMultiplierTextBox.Size = new System.Drawing.Size(42, 20);
            this.BpmMultiplierTextBox.TabIndex = 10;
            this.BpmMultiplierTextBox.Text = "1.00";
            this.BpmMultiplierTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.BpmMultiplierTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.BpmMultiplierTextBox_KeyDown);
            this.BpmMultiplierTextBox.Leave += new System.EventHandler(this.BpmMultiplierTextBox_Submit);
            // 
            // NewBpmTextBox
            // 
            this.NewBpmTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(23)))), ((int)(((byte)(16)))), ((int)(((byte)(25)))));
            this.NewBpmTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.NewBpmTextBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.NewBpmTextBox.Location = new System.Drawing.Point(89, 64);
            this.NewBpmTextBox.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.NewBpmTextBox.Name = "NewBpmTextBox";
            this.NewBpmTextBox.Size = new System.Drawing.Size(42, 18);
            this.NewBpmTextBox.TabIndex = 10;
            this.NewBpmTextBox.Text = "200";
            this.NewBpmTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.NewBpmTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.NewBpmTextBox_KeyDown);
            this.NewBpmTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.NewBpmTextBox_KeyPress);
            this.NewBpmTextBox.Leave += new System.EventHandler(this.NewBpmTextBox_Leave);
            // 
            // NewBpmRangeTextBox
            // 
            this.NewBpmRangeTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(35)))), ((int)(((byte)(53)))));
            this.NewBpmRangeTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.NewBpmRangeTextBox.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NewBpmRangeTextBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(134)))));
            this.NewBpmRangeTextBox.Location = new System.Drawing.Point(163, 64);
            this.NewBpmRangeTextBox.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.NewBpmRangeTextBox.Name = "NewBpmRangeTextBox";
            this.NewBpmRangeTextBox.ReadOnly = true;
            this.NewBpmRangeTextBox.Size = new System.Drawing.Size(85, 18);
            this.NewBpmRangeTextBox.TabIndex = 10;
            this.NewBpmRangeTextBox.Text = "(180 - 210)";
            // 
            // OriginalBpmRangeTextBox
            // 
            this.OriginalBpmRangeTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(35)))), ((int)(((byte)(53)))));
            this.OriginalBpmRangeTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.OriginalBpmRangeTextBox.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OriginalBpmRangeTextBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(134)))));
            this.OriginalBpmRangeTextBox.Location = new System.Drawing.Point(163, 35);
            this.OriginalBpmRangeTextBox.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.OriginalBpmRangeTextBox.Name = "OriginalBpmRangeTextBox";
            this.OriginalBpmRangeTextBox.ReadOnly = true;
            this.OriginalBpmRangeTextBox.Size = new System.Drawing.Size(85, 18);
            this.OriginalBpmRangeTextBox.TabIndex = 20;
            this.OriginalBpmRangeTextBox.Text = "(180 - 210)";
            // 
            // OriginalBpmTextBox
            // 
            this.OriginalBpmTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(24)))), ((int)(((byte)(38)))));
            this.OriginalBpmTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.OriginalBpmTextBox.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OriginalBpmTextBox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.OriginalBpmTextBox.Location = new System.Drawing.Point(89, 35);
            this.OriginalBpmTextBox.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.OriginalBpmTextBox.Name = "OriginalBpmTextBox";
            this.OriginalBpmTextBox.ReadOnly = true;
            this.OriginalBpmTextBox.Size = new System.Drawing.Size(42, 18);
            this.OriginalBpmTextBox.TabIndex = 10;
            this.OriginalBpmTextBox.Text = "200";
            this.OriginalBpmTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.OriginalBpmTextBox.Enter += new System.EventHandler(this.Unfocus);
            // 
            // OriginalBpmLabel
            // 
            this.OriginalBpmLabel.AutoSize = true;
            this.OriginalBpmLabel.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.OriginalBpmLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.OriginalBpmLabel.Location = new System.Drawing.Point(18, 36);
            this.OriginalBpmLabel.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.OriginalBpmLabel.Name = "OriginalBpmLabel";
            this.OriginalBpmLabel.Size = new System.Drawing.Size(62, 17);
            this.OriginalBpmLabel.TabIndex = 9;
            this.OriginalBpmLabel.Text = "Old BPM";
            this.OriginalBpmLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // NewBpmLabel
            // 
            this.NewBpmLabel.AutoSize = true;
            this.NewBpmLabel.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NewBpmLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.NewBpmLabel.Location = new System.Drawing.Point(15, 65);
            this.NewBpmLabel.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.NewBpmLabel.Name = "NewBpmLabel";
            this.NewBpmLabel.Size = new System.Drawing.Size(67, 17);
            this.NewBpmLabel.TabIndex = 9;
            this.NewBpmLabel.Text = "New BPM";
            this.NewBpmLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // BpmMultiplierLabel
            // 
            this.BpmMultiplierLabel.AutoSize = true;
            this.BpmMultiplierLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.BpmMultiplierLabel.Location = new System.Drawing.Point(47, 7);
            this.BpmMultiplierLabel.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.BpmMultiplierLabel.Name = "BpmMultiplierLabel";
            this.BpmMultiplierLabel.Size = new System.Drawing.Size(35, 17);
            this.BpmMultiplierLabel.TabIndex = 1;
            this.BpmMultiplierLabel.Text = "Rate";
            this.BpmMultiplierLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // editHotkeysButton
            // 
            this.editHotkeysButton.FlatAppearance.BorderSize = 0;
            this.editHotkeysButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.editHotkeysButton.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.editHotkeysButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.editHotkeysButton.Location = new System.Drawing.Point(328, 11);
            this.editHotkeysButton.Name = "editHotkeysButton";
            this.editHotkeysButton.Size = new System.Drawing.Size(89, 23);
            this.editHotkeysButton.TabIndex = 25;
            this.editHotkeysButton.Text = "Edit Hotkeys";
            this.editHotkeysButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.editHotkeysButton.UseVisualStyleBackColor = true;
            this.editHotkeysButton.Click += new System.EventHandler(this.editHotkeysButton_Click);
            // 
            // Middle1Panel
            // 
            this.Middle1Panel.Controls.Add(this.tableLayoutPanel1);
            this.Middle1Panel.Dock = System.Windows.Forms.DockStyle.Top;
            this.Middle1Panel.Location = new System.Drawing.Point(0, 151);
            this.Middle1Panel.Margin = new System.Windows.Forms.Padding(10, 10, 10, 0);
            this.Middle1Panel.Name = "Middle1Panel";
            this.Middle1Panel.Padding = new System.Windows.Forms.Padding(10, 3, 15, 5);
            this.Middle1Panel.Size = new System.Drawing.Size(427, 65);
            this.Middle1Panel.TabIndex = 10;
            this.Middle1Panel.Click += new System.EventHandler(this.Unfocus);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 41F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tableLayoutPanel1.Controls.Add(this.ODLockCheck, 3, 1);
            this.tableLayoutPanel1.Controls.Add(this.odlabel, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.ODDisplay, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.ODSlider, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.hplabel, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.HPDisplay, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.HPSlider, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.HPLockCheck, 3, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(10, 3);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(402, 57);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // ODLockCheck
            // 
            this.ODLockCheck.CheckedImage = ((System.Drawing.Image)(resources.GetObject("ODLockCheck.CheckedImage")));
            this.ODLockCheck.Location = new System.Drawing.Point(379, 31);
            this.ODLockCheck.Name = "ODLockCheck";
            this.ODLockCheck.Size = new System.Drawing.Size(20, 21);
            this.ODLockCheck.TabIndex = 15;
            this.ODLockCheck.UncheckedImage = ((System.Drawing.Image)(resources.GetObject("ODLockCheck.UncheckedImage")));
            this.ODLockCheck.UseVisualStyleBackColor = true;
            this.ODLockCheck.CheckedChanged += new System.EventHandler(this.OdLockCheck_CheckedChanged);
            // 
            // odlabel
            // 
            this.odlabel.AutoSize = true;
            this.odlabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.odlabel.Font = new System.Drawing.Font("Microsoft New Tai Lue", 9.75F, System.Drawing.FontStyle.Bold);
            this.odlabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.odlabel.Location = new System.Drawing.Point(9, 37);
            this.odlabel.Margin = new System.Windows.Forms.Padding(9, 9, 3, 0);
            this.odlabel.Name = "odlabel";
            this.odlabel.Size = new System.Drawing.Size(26, 20);
            this.odlabel.TabIndex = 9;
            this.odlabel.Text = "OD";
            this.odlabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ODDisplay
            // 
            this.ODDisplay.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.ODDisplay.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(24)))), ((int)(((byte)(38)))));
            this.ODDisplay.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.ODDisplay.Enabled = false;
            this.ODDisplay.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.ODDisplay.Location = new System.Drawing.Point(41, 33);
            this.ODDisplay.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.ODDisplay.Name = "ODDisplay";
            this.ODDisplay.ReadOnly = true;
            this.ODDisplay.Size = new System.Drawing.Size(35, 22);
            this.ODDisplay.TabIndex = 10;
            this.ODDisplay.Text = "0.0";
            this.ODDisplay.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.ODDisplay.Enter += new System.EventHandler(this.Unfocus);
            // 
            // ODSlider
            // 
            this.ODSlider.BodyColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.ODSlider.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ODSlider.FillDraggingNipple = false;
            this.ODSlider.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.ODSlider.Location = new System.Drawing.Point(82, 31);
            this.ODSlider.MaxValue = new decimal(new int[] {
            11,
            0,
            0,
            0});
            this.ODSlider.MinValue = new decimal(new int[] {
            0,
            0,
            0,
            0});
            this.ODSlider.Name = "ODSlider";
            this.ODSlider.NippleColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(255)))));
            this.ODSlider.NippleDiameter = 15;
            this.ODSlider.NippleExpandedDiameter = 18;
            this.ODSlider.NippleIdleColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(128)))), ((int)(((byte)(255)))));
            this.ODSlider.NippleStrokeWidth = 0;
            this.ODSlider.Size = new System.Drawing.Size(291, 23);
            this.ODSlider.TabIndex = 11;
            this.ODSlider.Text = "HPSlider";
            this.ODSlider.Thickness = 4;
            this.ODSlider.Value = new decimal(new int[] {
            0,
            0,
            0,
            0});
            this.ODSlider.ValueChanged += new System.EventHandler(this.OdSlider_ValueChanged);
            // 
            // hplabel
            // 
            this.hplabel.AutoSize = true;
            this.hplabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hplabel.Font = new System.Drawing.Font("Microsoft New Tai Lue", 9.75F, System.Drawing.FontStyle.Bold);
            this.hplabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.hplabel.Location = new System.Drawing.Point(9, 9);
            this.hplabel.Margin = new System.Windows.Forms.Padding(9, 9, 3, 0);
            this.hplabel.Name = "hplabel";
            this.hplabel.Size = new System.Drawing.Size(26, 19);
            this.hplabel.TabIndex = 0;
            this.hplabel.Text = "HP";
            this.hplabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // HPDisplay
            // 
            this.HPDisplay.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.HPDisplay.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(24)))), ((int)(((byte)(38)))));
            this.HPDisplay.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.HPDisplay.Enabled = false;
            this.HPDisplay.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HPDisplay.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.HPDisplay.Location = new System.Drawing.Point(41, 5);
            this.HPDisplay.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.HPDisplay.Name = "HPDisplay";
            this.HPDisplay.ReadOnly = true;
            this.HPDisplay.Size = new System.Drawing.Size(35, 22);
            this.HPDisplay.TabIndex = 1;
            this.HPDisplay.Text = "0.0";
            this.HPDisplay.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.HPDisplay.Enter += new System.EventHandler(this.Unfocus);
            // 
            // HPSlider
            // 
            this.HPSlider.BodyColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.HPSlider.Dock = System.Windows.Forms.DockStyle.Fill;
            this.HPSlider.FillDraggingNipple = false;
            this.HPSlider.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.HPSlider.Location = new System.Drawing.Point(82, 3);
            this.HPSlider.Margin = new System.Windows.Forms.Padding(3, 3, 28, 3);
            this.HPSlider.MaxValue = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.HPSlider.MinValue = new decimal(new int[] {
            0,
            0,
            0,
            0});
            this.HPSlider.Name = "HPSlider";
            this.HPSlider.NippleColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(255)))));
            this.HPSlider.NippleDiameter = 15;
            this.HPSlider.NippleExpandedDiameter = 18;
            this.HPSlider.NippleIdleColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(128)))), ((int)(((byte)(255)))));
            this.HPSlider.NippleStrokeWidth = 0;
            this.HPSlider.Size = new System.Drawing.Size(266, 22);
            this.HPSlider.TabIndex = 2;
            this.HPSlider.Text = "HPSlider";
            this.HPSlider.Thickness = 4;
            this.HPSlider.Value = new decimal(new int[] {
            0,
            0,
            0,
            0});
            this.HPSlider.ValueChanged += new System.EventHandler(this.HpSlider_ValueChanged);
            // 
            // HPLockCheck
            // 
            this.HPLockCheck.CheckedImage = ((System.Drawing.Image)(resources.GetObject("HPLockCheck.CheckedImage")));
            this.HPLockCheck.Location = new System.Drawing.Point(379, 3);
            this.HPLockCheck.Name = "HPLockCheck";
            this.HPLockCheck.Size = new System.Drawing.Size(20, 21);
            this.HPLockCheck.TabIndex = 12;
            this.HPLockCheck.UncheckedImage = ((System.Drawing.Image)(resources.GetObject("HPLockCheck.UncheckedImage")));
            this.HPLockCheck.UseVisualStyleBackColor = true;
            this.HPLockCheck.CheckedChanged += new System.EventHandler(this.HpLockCheck_CheckedChanged);
            // 
            // BottomPanel
            // 
            this.BottomPanel.Controls.Add(this.ResetButton);
            this.BottomPanel.Controls.Add(this.GenerateMapButton);
            this.BottomPanel.Controls.Add(this.SongsFolderButton);
            this.BottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.BottomPanel.Location = new System.Drawing.Point(0, 552);
            this.BottomPanel.Name = "BottomPanel";
            this.BottomPanel.Padding = new System.Windows.Forms.Padding(10, 5, 10, 10);
            this.BottomPanel.Size = new System.Drawing.Size(427, 111);
            this.BottomPanel.TabIndex = 11;
            this.BottomPanel.Click += new System.EventHandler(this.Unfocus);
            // 
            // ResetButton
            // 
            this.ResetButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.ResetButton.BrightnessRange = 0.01F;
            this.ResetButton.Color = System.Drawing.Color.SteelBlue;
            this.ResetButton.FlatAppearance.BorderColor = System.Drawing.Color.DarkGray;
            this.ResetButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.ResetButton.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.ResetButton.ForeColor = System.Drawing.Color.White;
            this.ResetButton.Location = new System.Drawing.Point(10, 37);
            this.ResetButton.Name = "ResetButton";
            this.ResetButton.Progress = 0F;
            this.ResetButton.ProgressColor = System.Drawing.Color.Transparent;
            this.ResetButton.Size = new System.Drawing.Size(130, 61);
            this.ResetButton.Subtext = "";
            this.ResetButton.SubtextColor = System.Drawing.Color.Empty;
            this.ResetButton.TabIndex = 1;
            this.ResetButton.Text = "Reset";
            this.ResetButton.TextYOffset = 0;
            this.ResetButton.TriangleCount = 30;
            this.ResetButton.UseVisualStyleBackColor = true;
            this.ResetButton.Click += new System.EventHandler(this.ResetButton_Click);
            // 
            // GenerateMapButton
            // 
            this.GenerateMapButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.GenerateMapButton.BackColor = System.Drawing.Color.Transparent;
            this.GenerateMapButton.BrightnessRange = 0.01F;
            this.GenerateMapButton.Color = System.Drawing.Color.FromArgb(((int)(((byte)(197)))), ((int)(((byte)(51)))), ((int)(((byte)(131)))));
            this.GenerateMapButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.GenerateMapButton.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GenerateMapButton.ForeColor = System.Drawing.Color.White;
            this.GenerateMapButton.Location = new System.Drawing.Point(146, 37);
            this.GenerateMapButton.Margin = new System.Windows.Forms.Padding(30, 3, 30, 3);
            this.GenerateMapButton.Name = "GenerateMapButton";
            this.GenerateMapButton.Progress = 0F;
            this.GenerateMapButton.ProgressColor = System.Drawing.Color.SpringGreen;
            this.GenerateMapButton.Size = new System.Drawing.Size(271, 62);
            this.GenerateMapButton.Subtext = "Ctrl+Shift+X";
            this.GenerateMapButton.SubtextColor = System.Drawing.Color.Plum;
            this.GenerateMapButton.TabIndex = 0;
            this.GenerateMapButton.Text = "Create Map";
            this.GenerateMapButton.TextYOffset = -3;
            this.GenerateMapButton.TriangleCount = 30;
            this.GenerateMapButton.UseVisualStyleBackColor = false;
            this.GenerateMapButton.Click += new System.EventHandler(this.GenerateMapButton_Click);
            // 
            // SongsFolderButton
            // 
            this.SongsFolderButton.BrightnessRange = 0.01F;
            this.SongsFolderButton.Color = System.Drawing.Color.DarkGoldenrod;
            this.SongsFolderButton.FlatAppearance.BorderColor = System.Drawing.Color.DarkGray;
            this.SongsFolderButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.SongsFolderButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SongsFolderButton.ForeColor = System.Drawing.Color.White;
            this.SongsFolderButton.Location = new System.Drawing.Point(10, 3);
            this.SongsFolderButton.Name = "SongsFolderButton";
            this.SongsFolderButton.Progress = 0F;
            this.SongsFolderButton.ProgressColor = System.Drawing.Color.Transparent;
            this.SongsFolderButton.Size = new System.Drawing.Size(407, 28);
            this.SongsFolderButton.Subtext = "";
            this.SongsFolderButton.SubtextColor = System.Drawing.Color.Empty;
            this.SongsFolderButton.TabIndex = 1;
            this.SongsFolderButton.Text = "Songs Folder";
            this.SongsFolderButton.TextYOffset = 0;
            this.SongsFolderButton.TriangleCount = 30;
            this.SongsFolderButton.UseVisualStyleBackColor = true;
            this.SongsFolderButton.Click += new System.EventHandler(this.SongsFolderButton_Click);
            // 
            // TopPanel
            // 
            this.TopPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.TopPanel.Controls.Add(this.SongDisplay);
            this.TopPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.TopPanel.Location = new System.Drawing.Point(0, 30);
            this.TopPanel.Name = "TopPanel";
            this.TopPanel.Padding = new System.Windows.Forms.Padding(10);
            this.TopPanel.Size = new System.Drawing.Size(427, 121);
            this.TopPanel.TabIndex = 12;
            // 
            // SongDisplay
            // 
            this.SongDisplay.Artist = null;
            this.SongDisplay.Cover = null;
            this.SongDisplay.Difficulty = null;
            this.SongDisplay.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SongDisplay.ErrorMessage = null;
            this.SongDisplay.GameMode = FsBeatmapProcessor.GameMode.osu;
            this.SongDisplay.Location = new System.Drawing.Point(10, 10);
            this.SongDisplay.Name = "SongDisplay";
            this.SongDisplay.Size = new System.Drawing.Size(407, 101);
            this.SongDisplay.Stars = 0F;
            this.SongDisplay.TabIndex = 7;
            this.SongDisplay.Text = "songDisplay1";
            this.SongDisplay.Title = null;
            // 
            // OsuRunningTimer
            // 
            this.OsuRunningTimer.Interval = 500;
            this.OsuRunningTimer.Tick += new System.EventHandler(this.OsuRunningTimer_Tick);
            // 
            // closeButton
            // 
            this.closeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.closeButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.closeButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.closeButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(139)))), ((int)(((byte)(10)))), ((int)(((byte)(20)))));
            this.closeButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(17)))), ((int)(((byte)(35)))));
            this.closeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.closeButton.Font = new System.Drawing.Font("Segoe UI Semilight", 9F);
            this.closeButton.ForeColor = System.Drawing.SystemColors.ButtonFace;
            this.closeButton.Location = new System.Drawing.Point(382, 0);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(45, 30);
            this.closeButton.TabIndex = 3;
            this.closeButton.Text = "✕";
            this.closeButton.UseVisualStyleBackColor = false;
            this.closeButton.Click += new System.EventHandler(this.closeButton_Click);
            // 
            // minimizeButton
            // 
            this.minimizeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.minimizeButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.minimizeButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.minimizeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.minimizeButton.Font = new System.Drawing.Font("Segoe UI Semilight", 9F);
            this.minimizeButton.ForeColor = System.Drawing.SystemColors.ButtonFace;
            this.minimizeButton.Location = new System.Drawing.Point(337, 0);
            this.minimizeButton.Name = "minimizeButton";
            this.minimizeButton.Size = new System.Drawing.Size(45, 30);
            this.minimizeButton.TabIndex = 3;
            this.minimizeButton.Text = "🗕";
            this.minimizeButton.UseVisualStyleBackColor = false;
            this.minimizeButton.Click += new System.EventHandler(this.minimizeButton_Click);
            // 
            // titlePanel
            // 
            this.titlePanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(27)))), ((int)(((byte)(47)))));
            this.titlePanel.Controls.Add(this.minimizeButton);
            this.titlePanel.Controls.Add(this.closeButton);
            this.titlePanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.titlePanel.Location = new System.Drawing.Point(0, 0);
            this.titlePanel.Name = "titlePanel";
            this.titlePanel.Size = new System.Drawing.Size(427, 30);
            this.titlePanel.TabIndex = 14;
            this.titlePanel.Paint += new System.Windows.Forms.PaintEventHandler(this.titlePanel_Paint);
            this.titlePanel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.PanelMove_MouseDown);
            this.titlePanel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.PanelMove_MouseMove);
            this.titlePanel.MouseUp += new System.Windows.Forms.MouseEventHandler(this.PanelMove_MouseUp);
            // 
            // BackgroundWorker
            // 
            this.BackgroundWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BackgroundWorker_DoWork);
            this.BackgroundWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BackgroundWorker_RunWorkerCompleted);
            // 
            // formAnimationTimer
            // 
            this.formAnimationTimer.Interval = 16;
            this.formAnimationTimer.Tick += new System.EventHandler(this.formAnimationTimer_Tick);
            // 
            // extrasPanel
            // 
            this.extrasPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(29)))), ((int)(((byte)(44)))));
            this.extrasPanel.Controls.Add(this.ChangePitchCheck);
            this.extrasPanel.Controls.Add(this.highQualityMp3Check);
            this.extrasPanel.Controls.Add(this.NoSpinnersCheck);
            this.extrasPanel.Controls.Add(this.button1);
            this.extrasPanel.Controls.Add(this.editHotkeysButton);
            this.extrasPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.extrasPanel.Location = new System.Drawing.Point(0, 395);
            this.extrasPanel.Name = "extrasPanel";
            this.extrasPanel.Size = new System.Drawing.Size(427, 102);
            this.extrasPanel.TabIndex = 15;
            this.extrasPanel.Visible = false;
            // 
            // ChangePitchCheck
            // 
            this.ChangePitchCheck.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ChangePitchCheck.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(136)))), ((int)(((byte)(134)))), ((int)(((byte)(144)))));
            this.ChangePitchCheck.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.ChangePitchCheck.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.ChangePitchCheck.Location = new System.Drawing.Point(2, 9);
            this.ChangePitchCheck.Name = "ChangePitchCheck";
            this.ChangePitchCheck.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.ChangePitchCheck.Size = new System.Drawing.Size(130, 24);
            this.ChangePitchCheck.TabIndex = 22;
            this.ChangePitchCheck.Text = "Change pitch";
            this.ChangePitchCheck.TextOnRight = true;
            this.ChangePitchCheck.UseVisualStyleBackColor = true;
            this.ChangePitchCheck.CheckedChanged += new System.EventHandler(this.ChangePitchButton_CheckedChanged);
            // 
            // highQualityMp3Check
            // 
            this.highQualityMp3Check.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(136)))), ((int)(((byte)(134)))), ((int)(((byte)(144)))));
            this.highQualityMp3Check.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.highQualityMp3Check.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.highQualityMp3Check.Location = new System.Drawing.Point(2, 69);
            this.highQualityMp3Check.Name = "highQualityMp3Check";
            this.highQualityMp3Check.Size = new System.Drawing.Size(155, 24);
            this.highQualityMp3Check.TabIndex = 20;
            this.highQualityMp3Check.Text = "High quality mp3";
            this.highQualityMp3Check.TextOnRight = true;
            this.highQualityMp3Check.UseVisualStyleBackColor = true;
            this.highQualityMp3Check.CheckedChanged += new System.EventHandler(this.highQualityCheckBox_CheckedChanged);
            // 
            // NoSpinnersCheck
            // 
            this.NoSpinnersCheck.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(136)))), ((int)(((byte)(134)))), ((int)(((byte)(144)))));
            this.NoSpinnersCheck.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.NoSpinnersCheck.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.NoSpinnersCheck.Location = new System.Drawing.Point(2, 39);
            this.NoSpinnersCheck.Name = "NoSpinnersCheck";
            this.NoSpinnersCheck.Size = new System.Drawing.Size(124, 24);
            this.NoSpinnersCheck.TabIndex = 20;
            this.NoSpinnersCheck.Text = "No spinners";
            this.NoSpinnersCheck.TextOnRight = true;
            this.NoSpinnersCheck.UseVisualStyleBackColor = true;
            this.NoSpinnersCheck.CheckedChanged += new System.EventHandler(this.NoSpinnerCheckBox_CheckedChanged);
            // 
            // button1
            // 
            this.button1.FlatAppearance.BorderSize = 0;
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(167)))), ((int)(((byte)(154)))), ((int)(((byte)(233)))));
            this.button1.Location = new System.Drawing.Point(280, 38);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(137, 23);
            this.button1.TabIndex = 25;
            this.button1.Text = "Delete Unused MP3s";
            this.button1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.DeleteButton_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(35)))), ((int)(((byte)(53)))));
            this.ClientSize = new System.Drawing.Size(427, 607);
            this.Controls.Add(this.extrasPanel);
            this.Controls.Add(this.middlePanel);
            this.Controls.Add(this.Middle1Panel);
            this.Controls.Add(this.BottomPanel);
            this.Controls.Add(this.TopPanel);
            this.Controls.Add(this.titlePanel);
            this.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "mania trainer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.middlePanel.ResumeLayout(false);
            this.middlePanel.PerformLayout();
            this.Middle1Panel.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.BottomPanel.ResumeLayout(false);
            this.TopPanel.ResumeLayout(false);
            this.titlePanel.ResumeLayout(false);
            this.extrasPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private AntiAliasedLabel BpmMultiplierLabel;
        private System.Windows.Forms.Timer BeatmapUpdateTimer;
        private AntiAliasedLabel OriginalBpmLabel;
        private AntiAliasedLabel NewBpmLabel;
        private OsuButton GenerateMapButton;
        private System.Windows.Forms.Panel BottomPanel;
        private System.Windows.Forms.Panel TopPanel;
        private System.Windows.Forms.Panel middlePanel;
        private System.Windows.Forms.Panel Middle1Panel;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private AntiAliasedLabel hplabel;
        private System.Windows.Forms.TextBox HPDisplay;
        private OptionSlider HPSlider;
        private AntiAliasedLabel odlabel;
        private System.Windows.Forms.TextBox ODDisplay;
        private OptionSlider ODSlider;
        private System.Windows.Forms.TextBox NewBpmRangeTextBox;
        private System.Windows.Forms.TextBox OriginalBpmRangeTextBox;
        private System.Windows.Forms.TextBox OriginalBpmTextBox;
        private OsuButton ResetButton;
        private System.Windows.Forms.Timer OsuRunningTimer;
        private OsuCheckBox NoLNCheck;
        private OsuCheckBox ChangePitchCheck;
        private ToggleIconButton ODLockCheck;
        private ToggleIconButton HPLockCheck;
        private SongDisplay SongDisplay;
        private OsuCheckBox NoSpinnersCheck;
        private System.Windows.Forms.Button closeButton;
        private System.Windows.Forms.Button minimizeButton;
        private System.Windows.Forms.Panel titlePanel;
        private OptionSlider BpmSlider;
        private ToggleIconButton BpmLockCheck;
        private OsuCheckBox FullLNCheck;
        private OsuButton SongsFolderButton;
        private ScrollableTextBox NewBpmTextBox;
        private System.Windows.Forms.TextBox BpmMultiplierTextBox;
        private System.Windows.Forms.Button danMenuButton1;
        private OsuButton danButton3;
        private OsuButton danButton2;
        private OsuButton danButton1;
        private System.Windows.Forms.Button danMenuButton3;
        private System.Windows.Forms.Button danMenuButton2;
        private OsuButton danButton4;
        private System.Windows.Forms.Button danMenuButton4;
        private System.Windows.Forms.Button editHotkeysButton;
        public System.ComponentModel.BackgroundWorker BackgroundWorker;
        private System.Windows.Forms.Timer formAnimationTimer;
        private System.Windows.Forms.Button showExtrasButton;
        private System.Windows.Forms.Panel extrasPanel;
        private System.Windows.Forms.Button button1;
        private OsuCheckBox highQualityMp3Check;
    }
}