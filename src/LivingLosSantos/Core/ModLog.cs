using System;
using System.IO;

namespace LivingLosSantos.Core
{
    internal static class ModLog
    {
        private static string _path;

        public static void Initialize(string directory)
        {
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, "living-los-santos.log");
            Info("Logger initialized.");
        }

        public static void Info(string message) { Write("INFO", message); }
        public static void Warn(string message) { Write("WARN", message); }
        public static void Error(string message, Exception exception = null)
        {
            Write("ERROR", message + (exception == null ? string.Empty : Environment.NewLine + exception));
        }

        private static void Write(string level, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_path))
                    return;
                File.AppendAllText(_path, string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}{3}",
                    DateTime.Now, level, message, Environment.NewLine));
            }
            catch
            {
                // Logging must never interrupt the game script.
            }
        }
    }
}
