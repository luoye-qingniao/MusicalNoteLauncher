using System;
using System.IO;
using System.Text.Json;

namespace MusicalNoteLauncher.Core
{
    public class LauncherSettings
    {
        // 启动设置
        public bool AutoLogin { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public bool CheckUpdate { get; set; } = true;

        // 界面设置
        public bool ShowSplash { get; set; } = true;
        public bool HardwareAcceleration { get; set; } = true;

        // 游戏设置
        public string GamePath { get; set; } = "%appdata%\\.minecraft";
        public int MaxMemory { get; set; } = 4096;
        public int MinMemory { get; set; } = 2048;
        public string Resolution { get; set; } = "1920x1080";
        public bool Fullscreen { get; set; } = false;

        // Java设置
        public string JavaPath { get; set; } = string.Empty;
        public string JavaArgs { get; set; } = "-XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32M";

        // 下载设置
        private int _downloadThreads = 64;
        public int DownloadThreads 
        { 
            get => _downloadThreads; 
            set => _downloadThreads = value < 1 ? 1 : (value > 200 ? 200 : value); 
        }
        public string DownloadPath { get; set; } = string.Empty;
        public bool AutoInstallDependencies { get; set; } = true;

        // 版本隔离设置
        public bool EnableVersionIsolation { get; set; } = false;
        
        // 下载源配置
        // 0 = 自动模式：优先BMCLAPI（超时30s），失败自动切换官方源（超时60s）
        // 1 = 官方优先：优先Mojang官方源（超时5s），失败自动切换BMCLAPI（超时30s）
        // 2 = 仅官方源：只使用Mojang官方源（超时60s）
        public int ToolDownloadVersion { get; set; } = 0;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private static LauncherSettings _settings;

        public static LauncherSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    LoadSettings();
                }
                return _settings;
            }
        }

        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    _settings = JsonSerializer.Deserialize<LauncherSettings>(json);
                }
                else
                {
                    _settings = new LauncherSettings();
                    SaveSettings();
                }
            }
            catch
            {
                _settings = new LauncherSettings();
            }
        }

        public static void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // 忽略保存错误
            }
        }

        public static void ResetSettings()
        {
            _settings = new LauncherSettings();
            SaveSettings();
        }
    }
}