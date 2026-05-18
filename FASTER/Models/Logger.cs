using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FASTER.Models
{
    public static class Logger
    {
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FASTER");

        private static readonly string LogPath      = Path.Combine(AppDataDir, "faster.log");
        private static readonly string CheckpointPath = Path.Combine(AppDataDir, "downloading.tmp");
        private static readonly string CrashBlacklistPath = Path.Combine(AppDataDir, "crash_blacklist.txt");

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

        // Called before starting a mod download — if we crash, this file will remain
        public static void SetDownloadCheckpoint(uint modId, string modName)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                File.WriteAllText(CheckpointPath, $"{modId}|{modName}");
            }
            catch { }
        }

        // Called after a successful download — removes the checkpoint
        public static void ClearDownloadCheckpoint()
        {
            try { File.Delete(CheckpointPath); } catch { }
        }

        // On startup: if a checkpoint exists, that mod crashed — add it to blacklist
        public static void ProcessCrashCheckpoint()
        {
            try
            {
                if (!File.Exists(CheckpointPath)) return;

                var content = File.ReadAllText(CheckpointPath).Trim();
                var parts = content.Split('|');
                if (parts.Length < 1 || !uint.TryParse(parts[0], out uint crashedModId)) return;

                var modName = parts.Length > 1 ? parts[1] : crashedModId.ToString();
                File.Delete(CheckpointPath);

                // Add to blacklist
                var blacklist = LoadCrashBlacklist();
                if (!blacklist.Contains(crashedModId))
                {
                    blacklist.Add(crashedModId);
                    File.WriteAllLines(CrashBlacklistPath, blacklist.Select(id => id.ToString()));
                    Log($"[CRASH] Mod {crashedModId} ({modName}) added to crash blacklist after unrecoverable crash.");
                }
            }
            catch { }
        }

        public static HashSet<uint> LoadCrashBlacklist()
        {
            try
            {
                if (!File.Exists(CrashBlacklistPath)) return new HashSet<uint>();
                return File.ReadAllLines(CrashBlacklistPath)
                    .Where(l => uint.TryParse(l.Trim(), out _))
                    .Select(l => uint.Parse(l.Trim()))
                    .ToHashSet();
            }
            catch { return new HashSet<uint>(); }
        }

        public static void RemoveFromCrashBlacklist(uint modId)
        {
            try
            {
                var blacklist = LoadCrashBlacklist();
                blacklist.Remove(modId);
                File.WriteAllLines(CrashBlacklistPath, blacklist.Select(id => id.ToString()));
            }
            catch { }
        }
    }
}
