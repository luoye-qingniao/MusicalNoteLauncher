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
        // 用户协议
        public bool TermsOfServiceAccepted { get; set; } = false;

        // 陶瓦联机用户协议
        public bool TerracottaAgreementAccepted { get; set; } = false;

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

        // 自定义下载保存路径（空 = 默认 Downloads 目录）
        public string CustomDownloadPath { get; set; } = "";

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

            // 1. 优先检查官方启动器目录
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string defaultVanillaPath = Path.Combine(appData, ".minecraft");
            if (Directory.Exists(defaultVanillaPath))
            {
                GameFolders.Add(new GameFolderEntry("官方启动器文件夹", defaultVanillaPath));
            }

            // 2. MNL 游戏目录
            string mnlMc = MNLEnvironment.MinecraftPath;
            if (Directory.Exists(mnlMc) &&
                !string.Equals(mnlMc.TrimEnd('\\'), defaultVanillaPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                GameFolders.Add(new GameFolderEntry("MNL 游戏目录", mnlMc));
            }

            // 3. 检查 exe 同级 .minecraft（旧版兼容）
            string localMc = Path.Combine(exeDir, ".minecraft");
            if (Directory.Exists(localMc) &&
                !string.Equals(localMc.TrimEnd('\\'), defaultVanillaPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(localMc.TrimEnd('\\'), mnlMc.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                string folderName = Path.GetFileName(exeDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(folderName)) folderName = "启动器目录";
                GameFolders.Add(new GameFolderEntry(folderName, localMc));
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

        // 个性化设置
        /// <summary>是否启用毛玻璃效果</summary>
        public bool GlassEnabled { get; set; } = true;

        /// <summary>是否启用聚光灯效果</summary>
        public bool SpotlightEnabled { get; set; } = false;

        /// <summary>主题模式 (light / dark)</summary>
        public string ThemeMode { get; set; } = "light";

        // 青鸟账号持久化
        /// <summary>已登录青鸟用户 ID</summary>
        public string QingniaoId { get; set; } = "";
        /// <summary>已登录青鸟用户名</summary>
        public string QingniaoName { get; set; } = "";
        /// <summary>青鸟登录邮箱</summary>
        public string QingniaoEmail { get; set; } = "";
        /// <summary>青鸟登录密码</summary>
        public string QingniaoPass { get; set; } = "";
        /// <summary>青鸟服务器地址</summary>
        public string QingniaoServer { get; set; } = "";
        /// <summary>青鸟客户端令牌</summary>
        public string QingniaoClientToken { get; set; } = "";
        /// <summary>青鸟用户头像</summary>
        public string QingniaoAvatar { get; set; } = "";

        // 壁纸设置
        /// <summary>背景模式 (mica/image/video)</summary>
        public string BgMode { get; set; } = "mica";

        /// <summary>背景透明度 (0-100)</summary>
        public int BgOpacity { get; set; } = 60;

        /// <summary>背景高斯模糊强度 (0-20)</summary>
        public int BgBlur { get; set; } = 0;

        /// <summary>背景覆盖层强度 (0-100)</summary>
        public int BgOverlay { get; set; } = 0;

        /// <summary>背景文件路径（显示用）</summary>
        public string BgFilePath { get; set; } = string.Empty;

        /// <summary>当前选中的壁纸 ID</summary>
        public string SelectedWallpaperId { get; set; } = string.Empty;

        /// <summary>当前选择的壁纸文件名</summary>
        public string WallpaperFileName { get; set; } = string.Empty;

        /// <summary>当前选择的视频背景文件名</summary>
        public string VideoBgFileName { get; set; } = string.Empty;

        // AI 助手设置
        /// <summary>AI 接口地址</summary>
        public string AiEndpoint { get; set; } = string.Empty;

        /// <summary>AI API Key</summary>
        public string AiApiKey { get; set; } = string.Empty;

        /// <summary>AI 模型名称</summary>
        public string AiModel { get; set; } = string.Empty;

        /// <summary>AI 预设服务 ID</summary>
        public string AiPreset { get; set; } = string.Empty;

        // 全景背景设置
        /// <summary>全景主题 (overworld/nether/end/wild/...)</summary>
        public string PanoramaTheme { get; set; } = "overworld";

        /// <summary>全景旋转速度 (0-20, 默认5)</summary>
        public int PanoramaSpeed { get; set; } = 5;

        /// <summary>全景鼠标跟随</summary>
        public bool PanoramaMouseFollow { get; set; } = false;

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
            catch (Exception ex)
            {
                // 配置文件损坏时创建备份
                try
                {
                    string backupPath = SettingsPath + ".bak";
                    if (File.Exists(SettingsPath))
                        File.Copy(SettingsPath, backupPath, true);
                    Logger.Warning($"[Settings] 配置文件损坏，已备份至 {backupPath}: {ex.Message}");
                }
                catch { }
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
                // 先写临时文件，再原子替换
                string tmpPath = path + ".tmp";
                File.WriteAllText(tmpPath, json);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmpPath, path);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Settings] 保存配置失败: {ex.Message}");
                return false;
            }
        }

        public static void ResetSettings()
        {
            _settings = new LauncherSettings();
            SaveSettings();
        }

        /// <summary>便捷方法：获取指定键的字符串值</summary>
        public static string? Get(string key)
        {
            return key switch
            {
                "GamePath" => Settings.GamePath,
                "JavaPath" => Settings.JavaPath,
                "JavaArgs" => Settings.JavaArgs,
                "CustomDownloadPath" => Settings.CustomDownloadPath,
                "Resolution" => Settings.Resolution,
                "BgMode" => Settings.BgMode,
                "ThemeMode" => Settings.ThemeMode,
                "AiEndpoint" => Settings.AiEndpoint,
                "AiApiKey" => Settings.AiApiKey,
                "AiModel" => Settings.AiModel,
                "PanoramaTheme" => Settings.PanoramaTheme,
                "WallpaperFileName" => Settings.WallpaperFileName,
                "VideoBgFileName" => Settings.VideoBgFileName,
                "SelectedWallpaperId" => Settings.SelectedWallpaperId,
                "QingniaoId" => Settings.QingniaoId,
                "QingniaoName" => Settings.QingniaoName,
                "QingniaoEmail" => Settings.QingniaoEmail,
                "QingniaoPass" => Settings.QingniaoPass,
                "QingniaoServer" => Settings.QingniaoServer,
                "QingniaoClientToken" => Settings.QingniaoClientToken,
                _ => null,
            };
        }

        /// <summary>便捷方法：设置指定键的值</summary>
        public static void Set(string key, object? value)
        {
            var s = Settings;
            switch (key)
            {
                case "GamePath": s.GamePath = value?.ToString() ?? ""; break;
                case "JavaPath": s.JavaPath = value?.ToString() ?? ""; break;
                case "JavaArgs": s.JavaArgs = value?.ToString() ?? ""; break;
                case "CustomDownloadPath": s.CustomDownloadPath = value?.ToString() ?? ""; break;
                case "Resolution": s.Resolution = value?.ToString() ?? "1920x1080"; break;
                case "BgMode": s.BgMode = value?.ToString() ?? "mica"; break;
                case "ThemeMode": s.ThemeMode = value?.ToString() ?? "light"; break;
                case "AiEndpoint": s.AiEndpoint = value?.ToString() ?? ""; break;
                case "AiApiKey": s.AiApiKey = value?.ToString() ?? ""; break;
                case "AiModel": s.AiModel = value?.ToString() ?? ""; break;
                case "PanoramaTheme": s.PanoramaTheme = value?.ToString() ?? "overworld"; break;
                case "WallpaperFileName": s.WallpaperFileName = value?.ToString() ?? ""; break;
                case "VideoBgFileName": s.VideoBgFileName = value?.ToString() ?? ""; break;
                case "SelectedWallpaperId": s.SelectedWallpaperId = value?.ToString() ?? ""; break;
                case "QingniaoId": s.QingniaoId = value?.ToString() ?? ""; break;
                case "QingniaoName": s.QingniaoName = value?.ToString() ?? ""; break;
                case "QingniaoEmail": s.QingniaoEmail = value?.ToString() ?? ""; break;
                case "QingniaoPass": s.QingniaoPass = value?.ToString() ?? ""; break;
                case "QingniaoServer": s.QingniaoServer = value?.ToString() ?? ""; break;
                case "QingniaoClientToken": s.QingniaoClientToken = value?.ToString() ?? ""; break;
                case "AutoLogin": s.AutoLogin = Convert.ToBoolean(value); break;
                case "CheckUpdate": s.CheckUpdate = Convert.ToBoolean(value); break;
                case "GlassEnabled": s.GlassEnabled = Convert.ToBoolean(value); break;
                case "SpotlightEnabled": s.SpotlightEnabled = Convert.ToBoolean(value); break;
                case "HardwareAcceleration": s.HardwareAcceleration = Convert.ToBoolean(value); break;
                case "BgOpacity": s.BgOpacity = Convert.ToInt32(value); break;
                case "BgBlur": s.BgBlur = Convert.ToInt32(value); break;
                case "BgOverlay": s.BgOverlay = Convert.ToInt32(value); break;
                case "MaxMemory": s.MaxMemory = Convert.ToInt32(value); break;
                case "MinMemory": s.MinMemory = Convert.ToInt32(value); break;
                case "DownloadThreads": s.DownloadThreads = Convert.ToInt32(value); break;
                case "Fullscreen": s.Fullscreen = Convert.ToBoolean(value); break;
                case "PanoramaSpeed": s.PanoramaSpeed = Convert.ToInt32(value); break;
                case "PanoramaMouseFollow": s.PanoramaMouseFollow = Convert.ToBoolean(value); break;
            }
        }

        /// <summary>便捷方法：保存设置</summary>
        public static void Save()
        {
            SaveSettings();
        }
    }
}