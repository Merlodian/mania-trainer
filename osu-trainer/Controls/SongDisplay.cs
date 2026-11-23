using FsBeatmapProcessor;
using osu_trainer.Mania;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace osu_trainer.Controls
{
    public class SongDisplay : Control
    {
        private Font _difficultyFont;
        private Font _artistFont;
        private Font _titleFont;
        private Font _starsFont;
        private Font _danNameFont;
        private Font _danNumberFont;

        public string Artist { get; set; }

        public string Title { get; set; }

        public string Difficulty { get; set; }

        public string ErrorMessage { get; set; }

        #region Star rating display
        private float _stars;
        private float _targetStars;
        public float Stars
        {
            get => _stars;
            set
            {
                _targetStars = value;
                _timer.Start();
                Invalidate(false);
            }
        }
        private GameMode gameMode = GameMode.osu;
        public GameMode GameMode
        {
            get => gameMode;
            set
            {
                updateIcon(gameMode = value, Enabled ? Colors.GetDifficultyColor(_stars) : Colors.Disabled);
                Invalidate(false);
            }
        }
        private Image _icon;
        private Image glow;

        private const float AnimationSpeed = .4f;
        private Timer _timer;

        private Color _lastColor = Color.Transparent;
        private GameMode _lastMode = GameMode.CatchtheBeat;
        #endregion

        #region Dan display (osu!mania)
        private ManiaRating _maniaRating;
        private float _maniaValue;       // animated towards _targetManiaValue
        private float _targetManiaValue;
        private readonly Dictionary<Color, Image> _tintedManiaIcons = new Dictionary<Color, Image>();

        /// <summary>
        /// Dan rating of the edited osu!mania map. When set on a mania map it replaces the star rating badge.
        /// </summary>
        public ManiaRating ManiaRating
        {
            get => _maniaRating;
            set
            {
                _maniaRating = value;
                _targetManiaValue = value != null && value.Status == ManiaRatingStatus.Ok ? (float)value.Rating : 0f;
                if (_maniaValue <= 0)
                    _maniaValue = _targetManiaValue * 0.9f; // first rating: count up quickly from just below
                _timer.Start();
                Invalidate(false);
            }
        }
        #endregion

        private Image _cover;

        public SongDisplay()
        {
            updateFonts();
            DoubleBuffered = true;

            glow = Properties.Resources.glow;

            // Ticks run on the UI thread (SynchronizingObject), like the setters that restart the timer, so a tick
            // can't stop the timer just as a new value arrives.
            _timer = new Timer()
            {
                Interval = 33,
                SynchronizingObject = this,
            };
            _timer.Elapsed += TimerOnElapsed;

            updateIcon(GameMode.osu, Enabled ? Colors.GetDifficultyColor(0) : Colors.Disabled);
        }

        public Image Cover
        {
            get => _cover;
            set
            {
                _cover = value;
                Invalidate(false);
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            updateFonts();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                DrawErrorMessage(e.Graphics);
                return;
            }

            var height = Width / 4;

            // background
            if (Cover == null)
            {
                using (var brush = new SolidBrush(Colors.ReadOnlyBg))
                    e.Graphics.FillRectangle(brush, 0, 0, Width, height);
            }
            else
            {
                using (var background = prepareImage(Cover, Width, height))
                    e.Graphics.DrawImage(background, 0, 0, background.Width, background.Height);
            }
            // background dim
            using (var darkenBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                e.Graphics.FillRectangle(darkenBrush, 0, 0, Width, height);

            var bottom = height - 5;

            // the dan badge is wider than the star badge, so keep the artist line clear of it
            bool showDan = GameMode == GameMode.Mania && ManiaRating != null;
            float badgeWidth = showDan ? MeasureDanBadge(e.Graphics).Width : 0;

            // map info
            var shadowColor = Color.FromArgb(128, 0, 0, 0);
            using (var shadowBrush = new SolidBrush(shadowColor))
            {
                // artist
                var artistHeight = (int)e.Graphics.MeasureString(Artist, _artistFont).Height;
                var artistY = bottom - artistHeight;
                if (showDan)
                {
                    var artistFormat = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
                    var artistRectangle = new RectangleF(5, artistY + 1, Math.Max(0, Width - 5 - badgeWidth - 10), artistHeight);
                    e.Graphics.DrawString(Artist, _artistFont, shadowBrush, artistRectangle, artistFormat);
                    artistRectangle.Y--;
                    e.Graphics.DrawString(Artist, _artistFont, Brushes.White, artistRectangle, artistFormat);
                }
                else
                {
                    e.Graphics.DrawString(Artist, _artistFont, shadowBrush, 5, artistY + 1);
                    e.Graphics.DrawString(Artist, _artistFont, Brushes.White, 5, artistY);
                }

                // title
                var titleFormat = new StringFormat()
                {
                    Trimming = StringTrimming.EllipsisCharacter
                };
                var titleHeight = e.Graphics.MeasureString(Title, _titleFont).Height;
                var titleY = artistY - titleHeight;
                var titleRectangle = new RectangleF(5, titleY, Width - 5, titleHeight);
                titleRectangle.Y++;
                e.Graphics.DrawString(Title, _titleFont, shadowBrush, titleRectangle, titleFormat);
                titleRectangle.Y--;
                e.Graphics.DrawString(Title, _titleFont, Brushes.White, titleRectangle, titleFormat);

                DrawDifficultyBadge(e.Graphics, shadowBrush);
                if (showDan)
                    DrawDanBadge(e.Graphics);
                else
                    DrawStarRatingBadge(e.Graphics);
            }
        }

        private void DrawErrorMessage(Graphics graphics)
        {
            using (var textBrush = new SolidBrush(Colors.Salmon))
            {
                var rectangle = new RectangleF(0, 0, Width, Height);
                var format = new StringFormat()
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                graphics.DrawString(ErrorMessage, _titleFont, textBrush, rectangle, format);
            }
        }

        private void DrawDifficultyBadge(Graphics graphics, Brush shadowBrush)
        {
            if (string.IsNullOrWhiteSpace(Difficulty))
                return;

            var difficultySize = graphics.MeasureString(Difficulty, _difficultyFont);
            var difficultyWidth = difficultySize.Width + 30;
            var rectangle = new RectangleF(Width - 5 - difficultyWidth, 5, difficultyWidth, difficultySize.Height + 12);
            using (var path = JunUtils.RoundedRect(rectangle, 14))
            {
                graphics.FillPath(shadowBrush, path);
            }

            var difficultyFormat = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            rectangle.Y += 3;
            graphics.DrawString(Difficulty, _difficultyFont, shadowBrush, rectangle, difficultyFormat);

            rectangle.Y -= 1;
            graphics.DrawString(Difficulty, _difficultyFont, Brushes.White, rectangle, difficultyFormat);
        }

        private Image prepareImage(Image image, int width, int height)
        {
            var bitmap = new Bitmap(width, height);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                var scaleFactor = width / (float)image.Width;
                var newHeight = image.Height * scaleFactor;
                graphics.DrawImage(image, 0, (height / 2) - (newHeight / 2), width, newHeight);
            }

            return bitmap;
        }

        private void updateFonts()
        {
            _difficultyFont = new Font(Font.FontFamily, 12, FontStyle.Bold, GraphicsUnit.Pixel);
            _artistFont = new Font(Font.FontFamily, 14, FontStyle.Bold, GraphicsUnit.Pixel);
            _titleFont = new Font(Font.FontFamily, 20.8f, FontStyle.Bold, GraphicsUnit.Pixel);
            _starsFont = new Font(Font.FontFamily, 17f, FontStyle.Bold, GraphicsUnit.Pixel);
            _danNameFont = new Font(Font.FontFamily, 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            _danNumberFont = new Font(Font.FontFamily, 13f, FontStyle.Bold, GraphicsUnit.Pixel);
        }

        #region Dan display (osu!mania)
        private const float DanBadgeHeight = 24;
        private const float DanIconSize = 20;
        private const float DanPadLeft = 5, DanPadRight = 9, DanIconGap = 5, DanNumberGap = 6;

        // What the badge shows for the current rating.
        private struct DanBadgeContent
        {
            public Image Icon;
            public string Name;
            public Color NameColor;
            public string Number;    // null for no number
            public float Progress;   // < 0 for no progress line
            public Color ProgressColor;
        }

        private DanBadgeContent GetDanBadgeContent()
        {
            var rating = ManiaRating;
            var muted = Colors.Disabled;
            switch (rating?.Status)
            {
                case ManiaRatingStatus.Ok:
                    var dan = rating.Dan;
                    if (dan == null)
                    {
                        return new DanBadgeContent
                        {
                            Icon = TintedManiaIcon(muted),
                            Name = rating.System.BelowLowestShort,
                            NameColor = muted,
                            Number = ManiaNumber(rating),
                            Progress = rating.Progress,
                            ProgressColor = rating.System.Levels[0].TextColor,
                        };
                    }
                    var color = Enabled ? dan.TextColor : muted;
                    return new DanBadgeContent
                    {
                        Icon = DanTable.GetIcon(dan) ?? TintedManiaIcon(color),
                        Name = dan.DisplayName,
                        NameColor = color,
                        Number = ManiaNumber(rating),
                        Progress = rating.NextDan == null ? -1 : rating.Progress,
                        ProgressColor = (rating.NextDan ?? dan).TextColor,
                    };
                case ManiaRatingStatus.UnsupportedKeys:
                    return new DanBadgeContent { Icon = TintedManiaIcon(muted), Name = $"{rating.Keys}K", NameColor = Colors.TextBoxFg, Number = "4K and 7K only", Progress = -1 };
                default:
                    return new DanBadgeContent { Icon = TintedManiaIcon(muted), Name = "No rating", NameColor = muted, Progress = -1 };
            }
        }

        // While counting, the animated value; once settled, the rating rounded exactly as DanSystem.Classify rounds
        // it (the float can be a hair off), so the number always agrees with the dan and the tooltip.
        // A 7K chart with a good share of long notes says so: xdc7 rated them as taps.
        private string ManiaNumber(ManiaRating rating) =>
            (_maniaValue == _targetManiaValue
                ? DanTable.FormatRating(rating.Rating)
                : _maniaValue.ToString("0.00", CultureInfo.InvariantCulture))
            + (rating.LongNoteHeavy ? " as rice" : "");

        private SizeF MeasureDanBadge(Graphics graphics) => MeasureDanBadge(graphics, GetDanBadgeContent(), out _, out _);

        private SizeF MeasureDanBadge(Graphics graphics, DanBadgeContent content, out SizeF nameSize, out SizeF numberSize)
        {
            nameSize = graphics.MeasureString(content.Name, _danNameFont);
            numberSize = content.Number == null ? SizeF.Empty : graphics.MeasureString(content.Number, _danNumberFont);
            float width = DanPadLeft + DanIconSize + DanIconGap + nameSize.Width + DanPadRight;
            if (content.Number != null)
                width += DanNumberGap + numberSize.Width;
            return new SizeF(width, DanBadgeHeight);
        }

        // Bottom-right badge in the style of the star rating badge: [dan icon] Dan name  rating
        // with a thin line along the bottom showing progress towards the next dan.
        private void DrawDanBadge(Graphics graphics)
        {
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var content = GetDanBadgeContent();
            var size = MeasureDanBadge(graphics, content, out var nameSize, out var numberSize);
            var badge = new RectangleF(Width - 3 - size.Width, Height - 3 - DanBadgeHeight, size.Width, DanBadgeHeight);

            using (var path = JunUtils.RoundedRect(badge, 11))
            {
                using (var shadowBrush = new SolidBrush(Color.FromArgb(240, 0, 0, 0)))
                    graphics.FillPath(shadowBrush, path);

                if (content.Progress >= 0)
                {
                    var clip = graphics.Clip;
                    graphics.SetClip(path, CombineMode.Intersect);
                    var track = new RectangleF(badge.X, badge.Bottom - 2, badge.Width, 2);
                    using (var trackBrush = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
                        graphics.FillRectangle(trackBrush, track);
                    track.Width = badge.Width * content.Progress;
                    using (var progressBrush = new SolidBrush(Color.FromArgb(220, content.ProgressColor)))
                        graphics.FillRectangle(progressBrush, track);
                    graphics.Clip = clip;
                }
            }

            float x = badge.X + DanPadLeft;
            if (content.Icon != null)
                graphics.DrawImage(content.Icon, x, badge.Y + (DanBadgeHeight - DanIconSize) / 2, DanIconSize, DanIconSize);
            x += DanIconSize + DanIconGap;

            var format = new StringFormat(StringFormatFlags.NoWrap) { LineAlignment = StringAlignment.Center };
            using (var nameBrush = new SolidBrush(content.NameColor))
                graphics.DrawString(content.Name, _danNameFont, nameBrush, new RectangleF(x, badge.Y + 1, nameSize.Width + 2, DanBadgeHeight), format);
            x += nameSize.Width + DanNumberGap;

            if (content.Number != null)
            {
                using (var numberBrush = new SolidBrush(Color.FromArgb(200, Colors.TextBoxFg)))
                    graphics.DrawString(content.Number, _danNumberFont, numberBrush, new RectangleF(x, badge.Y + 1, numberSize.Width + 2, DanBadgeHeight), format);
            }
        }

        private Image TintedManiaIcon(Color color)
        {
            if (!_tintedManiaIcons.TryGetValue(color, out var icon))
            {
                icon = Icons.GenerateIcon(GameMode.Mania, color);
                _tintedManiaIcons[color] = icon;
            }
            return icon;
        }
        #endregion

        #region Star rating display
        private void DrawStarRatingBadge(Graphics graphics)
        {
            if (Stars <= 0)
                return;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // shadow
            var shadowBrush = new SolidBrush(Color.FromArgb(240, 0, 0, 0));
            var shadowRectangle = new RectangleF(Width - 80, Height - 27, 77, 24);
            using (var path = JunUtils.RoundedRect(shadowRectangle, 11))
            {
                graphics.FillPath(shadowBrush, path);
            }

            // draw glow for hardest difficulties
            if (Stars >= 6.5)
            {
                graphics.DrawImage(glow, Width - 28, Height - 24);
            }

            var x = Width;

            if (_icon != null)
            {
                var size = 16;

                x -= size;
                graphics.DrawImage(_icon, x - 10, (Height - 6) - size, size, size);
                x -= 4;
            }

            var difficultyColor = Colors.GetDifficultyColor(Stars);
            if (Stars >= 6.5)
                difficultyColor = Color.White;

            using (var textBrush = new SolidBrush(Enabled ? difficultyColor : Colors.Disabled))
            {
                var text = $"{Stars:0.00}";
                var rectangle = new RectangleF(4, Height - 47, x - 14, Height/2 - 3);
                var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Far,
                    Alignment = StringAlignment.Far,
                };

                graphics.DrawString(text, _starsFont, textBrush, rectangle, format);
            }
        }
        private void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            bool starsChanged = _stars != _targetStars;
            bool danChanged = _maniaValue != _targetManiaValue;
            if (!starsChanged && !danChanged)
            {
                _timer.Stop();
                return;
            }

            if (starsChanged)
            {
                _stars = Approach(_stars, _targetStars, 0.005f);
                updateIcon(gameMode, Enabled ? Colors.GetDifficultyColor(_stars) : Colors.Disabled);
            }
            if (danChanged)
                _maniaValue = Approach(_maniaValue, _targetManiaValue, 0.005f);

            Invalidate(false);
        }

        // eases towards the target and snaps once close (a float can otherwise stay one step away forever)
        private static float Approach(float value, float target, float snap)
        {
            value += (target - value) * AnimationSpeed;
            return Math.Abs(target - value) < snap ? target : value;
        }
        private void updateIcon(GameMode mode, Color color)
        {
            if (mode == _lastMode && color == _lastColor)
                return;

            if (_icon == null)
            {
                _icon = Icons.GenerateIcon(mode, color);
            }
            else
            {
                lock (_icon)
                {
                    _icon = Icons.GenerateIcon(mode, color);
                }
            }
            Invalidate(false);
        }
        #endregion
    }
}