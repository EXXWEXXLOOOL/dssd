using System;
using System.Collections.Generic;
using System.IO;

namespace LivingLosSantos.Core
{
    internal static class ModLog
    {
        private static readonly List<string> _paths = new List<string>();

        public static void Initialize(string directory)
        {
            _paths.Clear();

            // First try beside the mod DLL. This may be blocked when GTA V is
            // installed under Program Files and the game is not elevated.
            if (!string.IsNullOrEmpty(directory))
                TryAddPath(Path.Combine(directory, "living-los-santos.log"));

            // Always try a per-user writable location as a reliable fallback.
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
                TryAddPath(Path.Combine(localAppData, "LivingLosSantos", "living-los-santos.log"));

            // Last resort: the current user's temporary directory.
            string temp = Path.GetTempPath();
            if (!string.IsNullOrEmpty(temp))
                TryAddPath(Path.Combine(temp, "LivingLosSantos", "living-los-santos.log"));

            Info("Logger initialized. Writable log destinations: " +
                (_paths.Count == 0 ? "(none)" : string.Join(" | ", _paths.ToArray())));
        }

        public static void Info(string message) { Write("INFO", message); }
        public static void Warn(string message) { Write("WARN", message); }

        public static void Error(string message, Exception exception = null)
        {
            Write("ERROR", message + (exception == null ? string.Empty : Environment.NewLine + exception));
        }

        private static void TryAddPath(string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.AppendAllText(path, string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [INFO] Logger opened.{1}",
                    DateTime.Now, Environment.NewLine));

                _paths.Add(path);
            }
            catch
            {
                // Try the next writable location.
            }
        }

        private static void Write(string level, string message)
        {
            string entry = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}{3}",
                DateTime.Now, level, message, Environment.NewLine);

            foreach (string path in _paths)
            {
                try
                {
                    File.AppendAllText(path, entry);
                }
                catch
                {
                    // Logging must never interrupt the game script.
                }
            }
        }
    }
}
