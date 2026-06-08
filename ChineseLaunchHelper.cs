using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PCLCS
{
    public static class ChineseLaunchHelper
    {
        public static LanguageResult SetupChineseLanguage(
            string optionsPath,
            int minecraftMajorVersion,
            bool preferChinese = true,
            Action<string> onLog = null,
            bool forceLanguage = false)
        {
            var result = new LanguageResult();

            try
            {
                EnsureOptionsFile(optionsPath);

                string currentLang = ReadIni(optionsPath, "lang", "none");
                result.CurrentLanguage = currentLang;

                // 如果强制设置语言，则忽略当前语言设置
                string requiredLang = DetermineLanguageCode(minecraftMajorVersion, preferChinese, 
                    forceLanguage ? "none" : currentLang);
                result.TargetLanguage = requiredLang;

                if (currentLang == requiredLang)
                {
                    result.LanguageChanged = false;
                    onLog?.Invoke($"需要的语言为 {requiredLang}，当前语言为 {currentLang}，无需修改");
                    result.Success = true;
                    return result;
                }

                WriteIni(optionsPath, "lang", "-");
                WriteIni(optionsPath, "lang", requiredLang);

                result.LanguageChanged = true;
                result.NewLanguage = requiredLang;
                onLog?.Invoke($"已将语言从 {currentLang} 修改为 {requiredLang}");
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                onLog?.Invoke($"设置语言失败: {ex.Message}");
            }

            return result;
        }

        public static string DetermineLanguageCode(
            int minecraftMajorVersion,
            bool preferChinese,
            string currentLang = "none")
        {
            string baseLang = preferChinese ? "zh_cn" : "en_us";

            if (!string.IsNullOrEmpty(currentLang) && currentLang != "none")
            {
                baseLang = currentLang.ToLower();
            }

            if (minecraftMajorVersion >= 1 && minecraftMajorVersion <= 10)
            {
                return NormalizeLanguageCodeOld(baseLang);
            }

            return baseLang;
        }

        private static string NormalizeLanguageCodeOld(string lang)
        {
            if (lang.Length < 2)
                return lang;

            return lang.Substring(0, lang.Length - 2) + lang.Substring(lang.Length - 2).ToUpper();
        }

        private static void EnsureOptionsFile(string optionsPath)
        {
            string directory = Path.GetDirectoryName(optionsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(optionsPath))
            {
                File.Create(optionsPath).Close();
            }
        }

        public static string ReadIni(string filePath, string key, string defaultValue = "")
        {
            if (!File.Exists(filePath))
                return defaultValue;

            foreach (string line in File.ReadAllLines(filePath))
            {
                if (line.StartsWith(key + ":"))
                {
                    return line.Substring(key.Length + 1).Trim();
                }
            }

            return defaultValue;
        }

        public static void WriteIni(string filePath, string key, string value)
        {
            if (!File.Exists(filePath))
            {
                using (File.Create(filePath)) { }
            }

            var lines = File.Exists(filePath) ? File.ReadAllLines(filePath).ToList() : new List<string>();

            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith(key + ":"))
                {
                    lines[i] = $"{key}:{value}";
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                lines.Add($"{key}:{value}");
            }

            File.WriteAllLines(filePath, lines);
        }

        public static string GetLanguageDisplayName(string langCode)
        {
            return langCode.ToLower() switch
            {
                "zh_cn" => "简体中文",
                "zh_tw" => "繁体中文",
                "en_us" => "English",
                "ja_jp" => "日本語",
                "ko_kr" => "한국어",
                "fr_fr" => "Français",
                "de_de" => "Deutsch",
                "es_es" => "Español",
                _ => langCode
            };
        }

        public static bool IsChineseLanguage(string langCode)
        {
            return !string.IsNullOrEmpty(langCode) &&
                   (langCode.StartsWith("zh_", StringComparison.OrdinalIgnoreCase) ||
                    langCode.StartsWith("zh-", StringComparison.OrdinalIgnoreCase));
        }

        public static void SetGameWindowMode(string optionsPath, WindowMode mode)
        {
            EnsureOptionsFile(optionsPath);

            switch (mode)
            {
                case WindowMode.Fullscreen:
                    WriteIni(optionsPath, "fullscreen", "true");
                    break;
                case WindowMode.Windowed:
                    WriteIni(optionsPath, "fullscreen", "false");
                    break;
            }
        }

        public static void SetUnicodeFont(string optionsPath, bool enable)
        {
            EnsureOptionsFile(optionsPath);
            WriteIni(optionsPath, "forceUnicodeFont", enable ? "true" : "false");
        }
    }

    public class LanguageResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string CurrentLanguage { get; set; }
        public string TargetLanguage { get; set; }
        public string NewLanguage { get; set; }
        public bool LanguageChanged { get; set; }
    }

    public enum WindowMode
    {
        Default = 0,
        Fullscreen = 1,
        Windowed = 2
    }
}