using System;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace osu_trainer
{
    /// <summary>
    /// Windows keeps a program's saved settings under its exe name, folder and version, so a renamed, moved or newer
    /// mania trainer (mania-trainer.exe in mania-trainer-v1.0.0, then mania-trainer-v1.0.1.exe in
    /// mania-trainer-v1.0.1, ...) starts with none of its own. On that first run this copies them over from the
    /// newest earlier mania trainer, or failing that from the osu-trainer 1.8.0 mania build it was made from: the dan
    /// buttons, hotkeys, songs folder, rate and toggles. The earlier settings file is only read, never changed.
    /// Upstream osu-trainer's own settings are skipped: only a file with the dan buttons setting (which upstream
    /// doesn't have) is taken.
    /// </summary>
    internal static class SettingsImport
    {
        private const string SectionName = "osu_trainer.Properties.Settings";

        private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        /// <summary>Where the osu-trainer mania build's settings are: %LOCALAPPDATA%\osu_trainer\osu-trainer.exe_Url_...</summary>
        internal static string OldSettingsRoot => Path.Combine(LocalAppData, "osu_trainer");

        /// <summary>Where every mania trainer's settings are: %LOCALAPPDATA%\mania_trainer\mania-trainer*.exe_Url_...</summary>
        internal static string ManiaSettingsRoot => Path.Combine(LocalAppData, "mania_trainer");

        /// <summary>
        /// Copies earlier settings in when this program has no settings file of its own yet, then saves them (so it
        /// happens once). Never throws: a failed import just leaves the defaults.
        /// </summary>
        public static void ImportOnFirstRun()
        {
            try
            {
                string own = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
                if (File.Exists(own))
                    return;
                string previous = FindPreviousManiaSettings(ManiaSettingsRoot, "mania-trainer*.exe_Url_*")
                               ?? FindPreviousManiaSettings(OldSettingsRoot);
                if (previous != null && Import(previous, Properties.Settings.Default) > 0)
                    Properties.Settings.Default.Save();
            }
            catch
            {
            }
        }

        /// <summary>
        /// The newest user.config under <paramref name="root"/>\(<paramref name="folders"/>) that has the dan buttons
        /// setting, or null.
        /// </summary>
        internal static string FindPreviousManiaSettings(string root, string folders = "osu-trainer.exe_Url_*")
        {
            try
            {
                if (!Directory.Exists(root))
                    return null;
                return Directory.GetDirectories(root, folders)
                    .SelectMany(d => Directory.GetFiles(d, "user.config", SearchOption.AllDirectories))
                    .Where(HasDanButtons)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sets every setting in <paramref name="file"/> that <paramref name="settings"/> also has, converted to the
        /// setting's own type. A value that doesn't convert is skipped, and the rest still come over. Returns how
        /// many were set. Doesn't save.
        /// </summary>
        internal static int Import(string file, ApplicationSettingsBase settings)
        {
            XElement section;
            try
            {
                section = Section(XDocument.Load(file));
            }
            catch
            {
                return 0;
            }
            if (section == null)
                return 0;
            int imported = 0;
            foreach (var setting in section.Elements("setting"))
            {
                try
                {
                    string name = (string)setting.Attribute("name");
                    if (name == null || (string)setting.Attribute("serializeAs") != "String")
                        continue;
                    SettingsProperty property = settings.Properties[name];
                    if (property == null)
                        continue;
                    string value = (string)setting.Element("value") ?? "";
                    settings[name] = TypeDescriptor.GetConverter(property.PropertyType).ConvertFromInvariantString(value);
                    imported++;
                }
                catch
                {
                    // a value this build can't read: keep the default for that one
                }
            }
            return imported;
        }

        private static XElement Section(XDocument doc) => doc.Root?.Element("userSettings")?.Element(SectionName);

        private static bool HasDanButtons(string file)
        {
            try
            {
                return Section(XDocument.Load(file))?.Elements("setting").Any(s => (string)s.Attribute("name") == "DanButtons") == true;
            }
            catch
            {
                return false;
            }
        }
    }
}
