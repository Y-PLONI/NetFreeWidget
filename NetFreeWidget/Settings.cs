using System;
using Newtonsoft.Json;

namespace NetFreeWidget
{
    public class Settings
    {
        public string PackageStartDate { get; set; } = "";
        public double PackageQuotaGb { get; set; } = 0;
        public string WeekendMode { get; set; } = "two"; // "two" or "one"

        public static Settings Load()
        {
            try
            {
                string path = GetSettingsPath();
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    return JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
                }
            }
            catch { }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                string path = GetSettingsPath();
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                System.IO.File.WriteAllText(path, json);
            }
            catch { }
        }

        private static string GetSettingsPath()
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = System.IO.Path.Combine(folder, "NetFreeWidget");
            if (!System.IO.Directory.Exists(appFolder))
                System.IO.Directory.CreateDirectory(appFolder);
            return System.IO.Path.Combine(appFolder, "settings.json");
        }
    }
}
