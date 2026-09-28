using System;
using System.IO;

namespace NetFreeWidget.Core
{
    public static class Log
    {
        private const long MaxBytes = 256 * 1024;
        private static readonly object Gate = new();
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetFreeWidget", "error_log.txt");

        public static void Error(string source, Exception ex) => Error(source, $"{ex.GetType().Name}: {ex.Message}");

        public static void Error(string source, string message)
        {
            try
            {
                lock (Gate)
                {
                    var info = new FileInfo(LogPath);
                    if (!info.Exists)
                        Directory.CreateDirectory(info.DirectoryName!);
                    else if (info.Length > MaxBytes)
                        File.WriteAllText(LogPath, "");

                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {message}\n");
                }
            }
            catch { }
        }
    }
}
