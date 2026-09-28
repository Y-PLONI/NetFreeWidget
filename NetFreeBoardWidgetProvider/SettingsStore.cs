using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetFreeWidget.Core.Models;

namespace NetFreeBoardWidgetProvider
{
    [JsonSerializable(typeof(WidgetSettings))]
    internal partial class SettingsJsonContext : JsonSerializerContext { }

    public static class SettingsStore
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetFreeWidget", "settings.json");

        private static readonly object Gate = new();
        private static WidgetSettings? _cached;
        private static DateTime _cachedWriteTime;

        /// <summary>Returns the settings, re-reading the file only when it was modified since the last load.</summary>
        public static WidgetSettings Load()
        {
            lock (Gate)
            {
                try
                {
                    DateTime writeTime = File.GetLastWriteTimeUtc(SettingsPath); // 1601-01-01 when missing
                    if (_cached != null && writeTime == _cachedWriteTime)
                        return _cached;

                    _cachedWriteTime = writeTime;
                    if (File.Exists(SettingsPath))
                    {
                        using var stream = File.OpenRead(SettingsPath);
                        _cached = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.WidgetSettings);
                    }
                }
                catch { }

                return _cached ??= new WidgetSettings();
            }
        }
    }
}
