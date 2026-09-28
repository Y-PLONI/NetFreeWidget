using System;
using System.IO;
using Newtonsoft.Json;
using NetFreeWidget.Core.Models;

namespace NetFreeBoardWidgetProvider
{
    public class SettingsStore
    {
        public static WidgetSettings Load()
        {
            try
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonConvert.DeserializeObject<WidgetSettings>(json);
                    if (loaded != null)
                        return loaded;
                }
            }
            catch { }

            return new WidgetSettings
            {
                PackageStartDate = "",
                PackageQuotaGb = 0,
                WeekendMode = "two"
            };
        }

        public static void Save(WidgetSettings settings)
        {
            try
            {
                string path = GetSettingsPath();
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private static string GetSettingsPath()
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(folder, "NetFreeWidget");
            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, "settings.json");
        }
    }
}
