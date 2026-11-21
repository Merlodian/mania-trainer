using System.Drawing;
using System.Windows.Forms;

namespace osu_trainer.Controls
{
    /// <summary>
    /// Drop-down menus in the main window's colours (used for the dan buttons' "Change dan" menus). Items keep their
    /// own ForeColor; the text on the right of an item (its ShortcutKeyDisplayString) is drawn muted.
    /// </summary>
    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public static readonly Color Background = Color.FromArgb(36, 27, 47);   // the title bar's colour
        public static readonly Color Border = Color.FromArgb(23, 16, 25);
        public static readonly Color Highlight = Color.FromArgb(62, 54, 92);
        public static readonly Color HighlightBorder = Color.FromArgb(99, 89, 150);
        public static readonly Color Checked = Color.FromArgb(78, 68, 116);
        public static readonly Color MutedText = Color.FromArgb(136, 134, 144);

        public DarkMenuRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled)
                e.TextColor = MutedText;
            else if (e.Item is ToolStripMenuItem item && !string.IsNullOrEmpty(item.ShortcutKeyDisplayString) && e.Text == item.ShortcutKeyDisplayString)
                e.TextColor = MutedText;
            else
                e.TextColor = e.Item.ForeColor;
            base.OnRenderItemText(e);
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public DarkColorTable()
            {
                UseSystemColors = false;
            }

            public override Color ToolStripDropDownBackground => Background;
            public override Color ImageMarginGradientBegin => Background;
            public override Color ImageMarginGradientMiddle => Background;
            public override Color ImageMarginGradientEnd => Background;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => HighlightBorder;
            public override Color MenuItemSelected => Highlight;
            public override Color MenuItemSelectedGradientBegin => Highlight;
            public override Color MenuItemSelectedGradientEnd => Highlight;
            public override Color MenuItemPressedGradientBegin => Highlight;
            public override Color MenuItemPressedGradientMiddle => Highlight;
            public override Color MenuItemPressedGradientEnd => Highlight;
            public override Color SeparatorDark => Color.FromArgb(70, 62, 98);
            public override Color SeparatorLight => Background;
            public override Color CheckBackground => Checked;
            public override Color CheckSelectedBackground => Checked;
            public override Color CheckPressedBackground => Checked;
            public override Color ButtonSelectedBorder => HighlightBorder;
        }
    }
}
