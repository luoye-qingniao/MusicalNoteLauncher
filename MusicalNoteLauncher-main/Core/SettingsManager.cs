using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace MusicalNoteLauncher.Core
{
    public class GameFolderEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;

        public GameFolderEntry() { }

        public GameFolderEntry(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string NormalizedPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Path)) return string.Empty;
                string expanded = Environment.ExpandEnvironmentVariables(Path);
                if (!expanded.EndsWith("\\")) expanded += "\\";
                return expanded;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is GameFolderEntry other)
                return string.Equals(NormalizedPath, other.NormalizedPath, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public override int GetHashCode()
        {
            return NormalizedPath.ToLowerInvariant().GetHashCode();
        }
    }

    public class LauncherSettings
    {
        // 启动设置
        public bool AutoLogin { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public bool CheckUpdate { get; set; } = true;

        // 界面设置
        public bool ShowSplash { get; set; } = true;
        public bool HardwareAcceleration { get; set; } = true;

        // 游戏设置 - 文件夹列表
        public List<GameFolderEntry> GameFolders { get; set; } = new List<GameFolderEntry>();
        public int SelectedGameFolderIndex { get; set; } = 0;

        public string GamePath
        {
            get
            {
                if (GameFolders != null && GameFolders.Count > 0 && SelectedGameFolderIndex >= 0 && SelectedGameFolderIndex < GameFolders.Count)
                    return GameFolders[SelectedGameFolderIndex].Path;
                return "%appdata%\\.minecraft";
            }
            set
            {
                string val = value ?? "%appdata%\\.minecraft";
                if (GameFolders == null || GameFolders.Count == 0)
                {
                    InitializeDefaultFolders(val);
                }
                else
                {
                    for (int i = 0; i < GameFolders.Count; i++)
                    {
                        if (string.Equals(GameFolders[i].NormalizedPath, Environment.ExpandEnvironmentVariables(val).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                        {
                            SelectedGameFolderIndex = i;
                            return;
                        }
                    }
                    var parts = val.TrimEnd('\\').Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string defaultName = parts.LastOrDefault() ?? "游戏文件夹";
                    if (defaultName == ".minecraft" && parts.Length >= 2)
                        defaultName = parts[parts.Length - 2];
                    GameFolders.Add(new GameFolderEntry(defaultName, val));
                    SelectedGameFolderIndex = GameFolders.Count - 1;
                }
            }
        }

        internal void InitializeDefaultFolders(string gamePath = null)
        {
            string defaultPath = gamePath ?? MNLEnvironment.MinecraftPath;

            GameFolders = new List<GameFolderEntry>();

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. 优先检查 MNL 游戏目录
            string mnlMc = MNLEnvironment.MinecraftPath;
            if (Directory.Exists(mnlMc))
            {
                GameFolders.Add(new GameFolderEntry("MNL 游戏目录", mnlMc));
            }

            // 2. 检查 exe 同级 .minecraft（旧版兼容）
            string localMc = Path.Combine(exeDir, ".minecraft");
            if (Directory.Exists(localMc) && !string.Equals(localMc.TrimEnd('\\'), mnlMc.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                string folderName = Path.GetFileName(exeDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(folderName)) folderName = "启动器目录";
                GameFolders.Add(new GameFolderEntry(folderName, localMc));
            }

            // 3. 官方启动器目录
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string defaultVanillaPath = Path.Combine(appData, ".minecraft");
            if (Directory.Exists(defaultVanillaPath))
            {
                bool alreadyHas = GameFolders.Any(f => string.Equals(f.NormalizedPath, defaultVanillaPath + "\\", StringComparison.OrdinalIgnoreCase));
                if (!alreadyHas)
                    GameFolders.Add(new GameFolderEntry("官方启动器文件夹", defaultVanillaPath));
            }

            if (GameFolders.Count == 0)
            {
                GameFolders.Add(new GameFolderEntry("MNL 游戏目录", defaultPath));
            }

            if (!string.IsNullOrWhiteSpace(gamePath))
            {
                string expanded = Environment.ExpandEnvironmentVariables(gamePath);
                bool found = false;
                for (int i = 0; i < GameFolders.Count; i++)
                {
                    if (string.Equals(GameFolders[i].NormalizedPath, expanded.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedGameFolderIndex = i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    var parts = expanded.TrimEnd('\\').Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string name = parts.LastOrDefault() ?? "游戏文件夹";
                    if (name == ".minecraft" && parts.Length >= 2)
                        name = parts[parts.Length - 2];
                    GameFolders.Add(new GameFolderEntry(name, gamePath));
                    SelectedGameFolderIndex = GameFolders.Count - 1;
                }
            }
            else
            {
                SelectedGameFolderIndex = 0;
            }
        }

        public int MaxMemory { get; set; } = 4096;
        public int MinMemory { get; set; } = 2048;

        // 内存分配模式: true=自动分配, false=自定义
        public bool MemoryAutoMode { get; set; } = true;

        // 自定义模式下分配的内存大小 (GB)
        public double MemoryCustomGB { get; set; } = 4.0;

        /// <summary>
        /// 根据当前模式获取实际分配的内存 (MB)
        /// </summary>
        public int GetMemoryMB()
        {
            if (MemoryAutoMode)
            {
                return CalculateAutoMemoryMB();
            }
            else
            {
                return (int)(MemoryCustomGB * 1024);
            }
        }

        /// <summary>
        /// 自动计算推荐内存分配 (MB)，模仿PCL的多阶段递减算法
        /// </summary>
        public static int CalculateAutoMemoryMB()
        {
            double totalGB = GetTotalMemoryGB();
            double availableGB = GetAvailableMemoryGB();

            double usableGB = Math.Min(totalGB * 0.8, availableGB);

            int modCount = EstimateModCount();

            double ramMinimum = 0.5 + modCount / 150.0;
            double ramTarget1 = 1.5 + modCount / 90.0;
            double ramTarget2 = 2.7 + modCount / 50.0;
            double ramTarget3 = 4.5 + modCount / 25.0;

            double ramGive;
            if (usableGB <= 0) usableGB = 2.0;

            if (usableGB <= ramTarget1)
            {
                ramGive = Math.Max(ramMinimum, usableGB);
            }
            else if (usableGB <= ramTarget2)
            {
                ramGive = ramTarget1 + (usableGB - ramTarget1) * 0.7;
            }
            else if (usableGB <= ramTarget3)
            {
                ramGive = ramTarget2 + (usableGB - ramTarget2) * 0.4;
            }
            else
            {
                ramGive = ramTarget3 + (usableGB - ramTarget3) * 0.15;
            }

            ramGive = Math.Max(ramGive, ramMinimum);
            ramGive = Math.Min(ramGive, usableGB);

            return (int)(ramGive * 1024);
        }

        private static double GetTotalMemoryGB()
        {
            try
            {
                var status = new MEMORYSTATUSEX();
                status.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
                if (GlobalMemoryStatusEx(ref status))
                    return status.ullTotalPhys / (1024.0 * 1024 * 1024);
            }
            catch { }
            return 8.0;
        }

        public static double GetTotalSystemMemoryGB()
        {
            return GetTotalMemoryGB();
        }

        private static double GetAvailableMemoryGB()
        {
            try
            {
                var status = new MEMORYSTATUSEX();
                status.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
                if (GlobalMemoryStatusEx(ref status))
                    return status.ullAvailPhys / (1024.0 * 1024 * 1024);
            }
            catch { }
            return 4.0;
        }

        public static double GetAvailableSystemMemoryGB()
        {
            return GetAvailableMemoryGB();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private static int EstimateModCount()
        {
            try
            {
                string gamePath = Environment.ExpandEnvironmentVariables(
                    SettingsManager.Settings?.GamePath ?? "%appdata%\\.minecraft");
                string modsPath = System.IO.Path.Combine(gamePath, "mods");
                if (System.IO.Directory.Exists(modsPath))
                {
                    return System.IO.Directory.GetFiles(modsPath, "*.jar", System.IO.SearchOption.TopDirectoryOnly).Length;
                }
            }
            catch { }
            return 0;
        }
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

        // 版本隔离设置 (PCL风格)
        // 0 = 关闭, 1 = 仅隔离可安装Mod的版本, 2 = 仅隔离非正式版
        // 3 = 隔离Mod版本+非正式版, 4 = 隔离所有版本(默认)
        public int VersionIsolationLevel { get; set; } = 4;

        // 内存优化设置 (PCL风格)
        public bool LaunchArgumentRam { get; set; } = false;

        // 皮肤资源包设置 (PCL风格)
        /// <summary>
        /// 是否启用离线皮肤资源包注入（将自定义皮肤打包为资源包并自动在游戏中启用）
        /// </summary>
        public bool EnableSkinResourcePack { get; set; } = true;

        /// <summary>
        /// 判断指定版本是否应启用版本隔离（PCL风格核心逻辑）
        /// </summary>
        /// <param name="isModable">版本是否可安装Mod（有Forge/Fabric等加载器）</param>
        /// <param name="isRelease">版本是否为正式版</param>
        /// <param name="isInherited">版本是否为整合包（有inheritsFrom）</param>
        public bool ShouldIsolateVersion(bool isModable, bool isRelease, bool isInherited)
        {
            // 整合包始终隔离
            if (isInherited) return true;
            
            switch (VersionIsolationLevel)
            {
                case 0: return false;                    // 关闭
                case 1: return isModable;                // 仅隔离可安装Mod的版本
                case 2: return !isRelease;               // 仅隔离非正式版
                case 3: return isModable || !isRelease;  // 隔离Mod版本+非正式版
                case 4:                                  // 隔离所有版本
                default: return true;
            }
        }

        /// <summary>
        /// 便捷方法：直接根据 minecraft 路径和版本 ID 判断是否应隔离
        /// </summary>
        public bool ShouldIsolateVersionForVersion(string minecraftPath, string versionId)
        {
            if (string.IsNullOrEmpty(versionId)) return false;
            try
            {
                string jsonFile = Path.Combine(minecraftPath, "versions", versionId, $"{versionId}.json");
                if (!File.Exists(jsonFile)) return VersionIsolationLevel >= 4;

                bool isInherited = false;
                bool isRelease = true;

                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonFile)))
                {
                    isInherited = doc.RootElement.TryGetProperty("inheritsFrom", out _);
                    if (doc.RootElement.TryGetProperty("type", out var typeProp))
                    {
                        string type = typeProp.GetString() ?? "";
                        isRelease = type == "release";
                    }
                }

                // 检查是否有 Mod 加载器
                bool isModable = false;
                try
                {
                    isModable = MusicalNoteLauncher.Core.ModLoaderDetector.DetectModLoader(
                        minecraftPath, versionId) != MusicalNoteLauncher.Core.ModLoaderDetector.ModLoaderType.None;
                }
                catch { }

                return ShouldIsolateVersion(isModable, isRelease, isInherited);
            }
            catch
            {
                return VersionIsolationLevel >= 4;
            }
        }
        
        // 下载源配置
        // 0 = 自动模式：优先BMCLAPI（超时30s），失败自动切换官方源（超时60s）
        // 1 = 官方优先：优先Mojang官方源（超时5s），失败自动切换BMCLAPI（超时30s）
        // 2 = 仅官方源：只使用Mojang官方源（超时60s）
        public int ToolDownloadVersion { get; set; } = 0;
    }

    public static class SettingsManager
    {
        private static string SettingsPath => MNLEnvironment.SettingsFilePath;
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
                    _settings.InitializeDefaultFolders();
                    SaveSettings();
                }

                if (_settings.GameFolders == null || _settings.GameFolders.Count == 0)
                {
                    string oldPath = _settings.GamePath;
                    _settings.InitializeDefaultFolders(oldPath);
                }

                if (_settings.SelectedGameFolderIndex < 0 || _settings.SelectedGameFolderIndex >= _settings.GameFolders.Count)
                    _settings.SelectedGameFolderIndex = 0;
            }
            catch
            {
                _settings = new LauncherSettings();
                _settings.InitializeDefaultFolders();
            }
        }

        public static bool SaveSettings()
        {
            try
            {
                string path = SettingsPath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[SettingsManager] 保存设置失败: {ex.Message}", ex);
                return false;
            }
        }

        public static void ResetSettings()
        {
            _settings = new LauncherSettings();
            SaveSettings();
        }
    }
}