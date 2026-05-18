using System;
using System.IO;

namespace FASTER.Models
{
    public static class Logger
    {
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FASTER");

        private static readonly string LogPath = Path.Combine(AppDataDir, "faster.log");

        public static bool IsEnabled => Properties.Settings.Default.enableDebugLog;
        public static string LogFilePath => LogPath;

        public static void Log(string message)
        {
            if (!IsEnabled) return;
            try
            {
                Directory.CreateDirectory(AppDataDir);
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
