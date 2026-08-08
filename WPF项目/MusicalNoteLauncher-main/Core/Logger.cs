using System;
using System.IO;

namespace MusicalNoteLauncher.Core
{
    public static class Logger
    {
        private static readonly string LogDirectory;
        private static readonly string LogFilePath;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取日志目录（参考 PCL：{exe}\MNL\logs\）
        /// </summary>
        private static string GetLogDirectory()
        {
            // 参考 PCL：日志与程序同级目录
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string preferredDir = Path.Combine(appDir, "MNL", "logs");

            try
            {
                if (!Directory.Exists(preferredDir))
                    Directory.CreateDirectory(preferredDir);

                // 验证可写
                string testFile = Path.Combine(preferredDir, ".write_test");
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
                return preferredDir;
            }
            catch
            {
                // 回退到用户临时目录
                string fallbackDir = Path.Combine(Path.GetTempPath(), "MNL", "logs");
                Directory.CreateDirectory(fallbackDir);
                return fallbackDir;
            }
        }

        static Logger()
        {
            LogDirectory = GetLogDirectory();
            string logFileName = $"launcher_{DateTime.Now:yyyy-MM-dd}.log";
            LogFilePath = Path.Combine(LogDirectory, logFileName);
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