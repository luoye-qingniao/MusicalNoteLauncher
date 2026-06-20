using System;
using System.IO;

namespace MusicalNoteLauncher.Core
{
    public static class Logger
    {
        private static readonly string LogDirectory;
        private static readonly string LogFilePath;
        private static readonly object _lock = new object();

        static Logger()
        {
            LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            string logFileName = $"launcher_{DateTime.Now:yyyy-MM-dd}.log";
            LogFilePath = Path.Combine(LogDirectory, logFileName);

            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }

        public static void Info(string message)
        {
            string logLine = $"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            Console.WriteLine(logLine);
            WriteToFile(logLine);
        }

        public static void Warning(string message)
        {
            string logLine = $"[WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            Console.WriteLine(logLine);
            WriteToFile(logLine);
        }

        public static void Error(string message)
        {
            string logLine = $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            Console.WriteLine(logLine);
            WriteToFile(logLine);
        }

        public static void Error(string message, Exception ex)
        {
            string logLine = $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}, Exception: {ex.ToString()}";
            Console.WriteLine(logLine);
            WriteToFile(logLine);
        }

        private static void WriteToFile(string logLine)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
                }
            }
            catch
            {
            }
        }
    }
}