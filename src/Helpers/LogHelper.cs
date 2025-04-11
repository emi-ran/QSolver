using System;
using System.IO;
using System.Text;
using System.Linq;
using Timer = System.Timers.Timer;

namespace QSolver.Helpers
{
    public static class LogHelper
    {
        private static readonly object LockObject = new();
        private static string LogFolder { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QSolver"
        );
        public static string LogFile { get; private set; } = Path.Combine(LogFolder, "logs.txt");
        private static readonly Timer CleanupTimer = new(CLEANUP_INTERVAL_HOURS * 60 * 60 * 1000); // 3 saat
        private const int LOG_RETENTION_HOURS = 24;
        private const int CLEANUP_INTERVAL_HOURS = 3;

        static LogHelper()
        {
            try
            {
                LogFolder = Path.Combine(Path.GetTempPath(), "QSolver");
                LogFile = Path.Combine(LogFolder, "logs.txt");

                if (!Directory.Exists(LogFolder))
                {
                    Directory.CreateDirectory(LogFolder);
                }

                // İlk başlangıçta temizlik yap
                CleanupOldLogs();

                // Timer'ı yapılandır ve başlat
                CleanupTimer.Elapsed += (s, e) => CleanupOldLogs();
                CleanupTimer.Start();
            }
            catch (Exception ex)
            {
                LogFolder = AppDomain.CurrentDomain.BaseDirectory;
                LogFile = Path.Combine(LogFolder, "logs.txt");
                Log($"Log klasörü oluşturulurken hata: {ex.Message}", LogType.Error);
            }
        }

        private static void CleanupOldLogs()
        {
            lock (LockObject)
            {
                try
                {
                    if (!File.Exists(LogFile)) return;

                    var lines = File.ReadAllLines(LogFile, Encoding.UTF8);
                    var now = DateTime.Now;
                    var filteredLines = lines.Where(line =>
                    {
                        if (TryParseLogDate(line, out DateTime logDate))
                        {
                            return (now - logDate).TotalHours <= LOG_RETENTION_HOURS;
                        }
                        return true;
                    }).ToList();

                    if (filteredLines.Count < lines.Length)
                    {
                        File.WriteAllLines(LogFile, filteredLines, Encoding.UTF8);
                        Log($"{lines.Length - filteredLines.Count} eski log temizlendi", LogType.Info);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Log temizleme hatası: {ex.Message}", LogType.Error);
                }
            }
        }

        private static bool TryParseLogDate(string logLine, out DateTime date)
        {
            date = DateTime.MinValue;
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(logLine, @"\[([\d-]+ [\d:\.]+)\]");
                if (match.Success)
                {
                    return DateTime.TryParse(match.Groups[1].Value, out date);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void Log(string message, LogType type = LogType.Info)
        {
            lock (LockObject)
            {
                try
                {
                    var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{type}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFile, logMessage, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Log yazma hatası: {ex.Message}");
                    Console.WriteLine($"Yazılmaya çalışılan mesaj: {message}");
                }
            }
        }

        public static void LogError(string message, Exception? ex = null)
        {
            var logMessage = message;
            if (ex != null)
            {
                logMessage += $"\nHata Detayı: {ex.Message}\nStack Trace: {ex.StackTrace}";
            }
            Log(logMessage, LogType.Error);
        }

        public static void LogWarning(string message)
        {
            Log(message, LogType.Warning);
        }

        public static void LogInfo(string message)
        {
            Log(message, LogType.Info);
        }

        public static void LogDebug(string message)
        {
            Log(message, LogType.Debug);
        }
    }

    public enum LogType
    {
        Info,
        Warning,
        Error,
        Debug
    }
}