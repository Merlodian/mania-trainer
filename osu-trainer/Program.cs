using System;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace osu_trainer
{
    public static class Program
    {
        public static PrivateFontCollection FontCollection { get; } = new PrivateFontCollection();

        [STAThread]
        public static void Main()
        {
            // mania-trainer doesn't run upstream's updater.exe: an update would replace it with the regular
            // osu-trainer release.

            // settings saved by this program under its old name (the osu-trainer 1.8.0 mania build), on first run
            SettingsImport.ImportOnFirstRun();

            AddFont(FontCollection, Properties.Resources.Comfortaa_Bold);

            Application.CurrentCulture = new CultureInfo("en-US", false);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void AddFont(PrivateFontCollection collection, byte[] bytes)
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            IntPtr pointer = handle.AddrOfPinnedObject();
            try
            {
                collection.AddMemoryFont(pointer, bytes.Length);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}