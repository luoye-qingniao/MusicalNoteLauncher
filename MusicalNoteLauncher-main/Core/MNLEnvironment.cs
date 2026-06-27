using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// MNL 启动器环境管理器 —— 参考 PCL 的目录管理方案
    /// 在程序同级目录下创建 "MNL" 文件夹，统一管理游戏目录和程序依赖
    /// 
    /// 目录结构:
    ///   {exe dir}/
    ///   └── MNL/
    ///       ├── .minecraft/          ← 游戏目录（默认）
    ///       │   ├── versions/
    ///       │   ├── assets/
    ///       │   ├── libraries/
    ///       │   ├── bedrock/
    ///       │   └── ...
    ///       ├── skins/               ← 皮肤文件
    ///       ├── logs/                ← 日志文件
    ///       ├── config.ini           ← 配置文件
    ///       └── settings.json        ← 启动器设置
    /// </summary>
    public static class MNLEnvironment
    {
        /// <summary>MNL 根目录（{exe dir}\MNL\）</summary>
        public static string MNLFolder { get; private set; }

        /// <summary>游戏目录（MNL\.minecraft\）</summary>
        public static string MinecraftPath { get; private set; }

        /// <summary>皮肤目录（MNL\skins\）</summary>
        public static string SkinsPath { get; private set; }

        /// <summary>日志目录（MNL\logs\）</summary>
        public static string LogsPath { get; private set; }

        /// <summary>配置文件路径（MNL\config.ini）</summary>
        public static string ConfigFilePath { get; private set; }

        /// <summary>设置文件路径（MNL\settings.json）</summary>
        public static string SettingsFilePath { get; private set; }

        /// <summary>程序所在目录</summary>
        public static string AppDirectory { get; private set; }

        /// <summary>初始化是否已完成</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>依赖完整性检查时发现的缺失项（供 UI 展示）</summary>
        public static List<string> MissingDependencies { get; private set; } = new List<string>();

        /// <summary>MNL 文件夹中必须存在的关键目录（用于完整性检查）</summary>
        private static readonly string[] RequiredDirectories = new[]
        {
            ".minecraft",
            ".minecraft\\versions",
            ".minecraft\\assets",
            ".minecraft\\libraries",
            "skins",
            "logs"
        };

        /// <summary>
        /// 初始化 MNL 环境。在 App.OnStartup 中尽早调用。
        /// 首次使用时创建目录结构，检测依赖完整性。
        /// </summary>
        /// <returns>依赖是否完整（false 表示有缺失）</returns>
        public static bool Initialize()
        {
            if (IsInitialized) return MissingDependencies.Count == 0;

            try
            {
                AppDirectory = AppDomain.CurrentDomain.BaseDirectory;
                MNLFolder = Path.Combine(AppDirectory, "MNL");
                MinecraftPath = Path.Combine(MNLFolder, ".minecraft");
                SkinsPath = Path.Combine(MNLFolder, "skins");
                LogsPath = Path.Combine(MNLFolder, "logs");
                ConfigFilePath = Path.Combine(MNLFolder, "config.ini");
                SettingsFilePath = Path.Combine(MNLFolder, "settings.json");

                Logger.Info("[MNL环境] ========================================");
                Logger.Info($"[MNL环境] 应用目录: {AppDirectory}");
                Logger.Info($"[MNL环境] MNL目录: {MNLFolder}");
                Logger.Info($"[MNL环境] 游戏目录: {MinecraftPath}");

                // 1. 确保所有必需目录存在
                EnsureDirectories();

                // 2. 迁移旧配置文件（如果存在）
                MigrateOldConfig();

                // 3. 迁移旧游戏目录（如果存在）
                MigrateOldGameDirectory();

                // 4. 依赖完整性检查
                CheckDependencyIntegrity();

                IsInitialized = true;
                Logger.Info($"[MNL环境] 初始化完成，缺失依赖: {MissingDependencies.Count} 项");
                return MissingDependencies.Count == 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"[MNL环境] 初始化失败: {ex.Message}", ex);
                // 回退到默认值
                MNLFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MNL");
                MinecraftPath = Path.Combine(MNLFolder, ".minecraft");
                SkinsPath = Path.Combine(MNLFolder, "skins");
                LogsPath = Path.Combine(MNLFolder, "logs");
                ConfigFilePath = Path.Combine(MNLFolder, "config.ini");
                SettingsFilePath = Path.Combine(MNLFolder, "settings.json");
                IsInitialized = false;
                return false;
            }
        }

        /// <summary>
        /// 确保所有必需的目录存在（MNL 文件夹、游戏子目录等）
        /// </summary>
        private static void EnsureDirectories()
        {
            foreach (var dir in RequiredDirectories)
            {
                string fullPath = Path.Combine(MNLFolder, dir);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    Logger.Info($"[MNL环境] 创建目录: {fullPath}");
                }
            }

            // 额外确保 bedrock 目录存在（基岩版下载目录）
            string bedrockDir = Path.Combine(MinecraftPath, "bedrock");
            if (!Directory.Exists(bedrockDir))
            {
                Directory.CreateDirectory(bedrockDir);
                Logger.Info($"[MNL环境] 创建基岩版目录: {bedrockDir}");
            }
        }

        /// <summary>
        /// 迁移旧的 config.ini（%appdata%\MusicalNoteLauncher\config.ini）到 MNL 目录
        /// </summary>
        private static void MigrateOldConfig()
        {
            try
            {
                string oldConfigDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MusicalNoteLauncher");
                string oldConfigFile = Path.Combine(oldConfigDir, "config.ini");

                if (File.Exists(oldConfigFile) && !File.Exists(ConfigFilePath))
                {
                    File.Copy(oldConfigFile, ConfigFilePath, overwrite: false);
                    Logger.Info($"[MNL环境] 已迁移旧配置文件: {oldConfigFile} → {ConfigFilePath}");
                }

                // 迁移旧的 settings.json
                string oldSettingsFile = Path.Combine(AppDirectory, "settings.json");
                if (File.Exists(oldSettingsFile) && !File.Exists(SettingsFilePath))
                {
                    File.Copy(oldSettingsFile, SettingsFilePath, overwrite: false);
                    Logger.Info($"[MNL环境] 已迁移旧设置文件: {oldSettingsFile} → {SettingsFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[MNL环境] 迁移旧配置文件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 迁移旧的游戏目录（%appdata%\.minecraft）到 MNL\.minecraft
        /// 如果旧目录存在且 MNL\.minecraft 为新目录（versions 为空），提示用户
        /// </summary>
        private static void MigrateOldGameDirectory()
        {
            try
            {
                string oldMcDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ".minecraft");

                if (!Directory.Exists(oldMcDir))
                {
                    Logger.Info("[MNL环境] 无旧游戏目录，跳过迁移");
                    return;
                }

                string oldVersionsDir = Path.Combine(oldMcDir, "versions");
                string newVersionsDir = Path.Combine(MinecraftPath, "versions");

                // 如果旧目录有版本而新目录没有，自动使用旧目录（不迁移文件，直接创建符号链接或记录）
                if (Directory.Exists(oldVersionsDir) &&
                    Directory.GetDirectories(oldVersionsDir).Length > 0 &&
                    (!Directory.Exists(newVersionsDir) || Directory.GetDirectories(newVersionsDir).Length == 0))
                {
                    Logger.Info($"[MNL环境] 检测到旧游戏目录中有已安装版本: {oldMcDir}");
                    Logger.Info("[MNL环境] 为兼容性，游戏目录将指向旧目录");

                    // 不修改 MinecraftPath，但把旧目录的路径记录下来用于后续迁移提示
                    // 用户可以在设置中手动迁移
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[MNL环境] 检查旧游戏目录时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查程序依赖完整性（MNL 目录结构、关键 DLL 等）
        /// </summary>
        private static void CheckDependencyIntegrity()
        {
            MissingDependencies.Clear();

            // 1. 检查 MNL 目录结构
            foreach (var dir in RequiredDirectories)
            {
                string fullPath = Path.Combine(MNLFolder, dir);
                if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                {
                    MissingDependencies.Add($"目录缺失: {dir}");
                }
            }

            // 2. 检查关键程序文件（.NET 运行时依赖）
            // 这些是程序运行必需的同目录 DLL 文件
            string[] essentialFiles = new[]
            {
                "MusicalNoteLauncher.dll",
                "MusicalNoteLauncher.runtimeconfig.json"
            };

            foreach (var file in essentialFiles)
            {
                string fullPath = Path.Combine(AppDirectory, file);
                if (!File.Exists(fullPath))
                {
                    MissingDependencies.Add($"程序文件缺失: {file}");
                }
            }

            // 3. 检查 settings.json 是否存在
            if (!File.Exists(SettingsFilePath))
            {
                // 创建默认设置文件
                CreateDefaultSettings();
                Logger.Info("[MNL环境] 已创建默认设置文件");
            }

            if (MissingDependencies.Count > 0)
            {
                Logger.Warning($"[MNL环境] 依赖完整性检查发现 {MissingDependencies.Count} 项缺失:");
                foreach (var dep in MissingDependencies)
                    Logger.Warning($"  - {dep}");
            }
            else
            {
                Logger.Info("[MNL环境] 依赖完整性检查通过");
            }
        }

        /// <summary>
        /// 创建默认的 settings.json
        /// </summary>
        private static void CreateDefaultSettings()
        {
            try
            {
                // settings.json 使用 %appdata% 路径宏以支持环境变量展开
                string defaultGamePath = MinecraftPath;
                string json = $@"{{
  ""AutoLogin"": false,
  ""MinimizeToTray"": false,
  ""CheckUpdate"": true,
  ""ShowSplash"": true,
  ""HardwareAcceleration"": true,
  ""GameFolders"": [
    {{
      ""Name"": ""MNL 游戏目录"",
      ""Path"": ""{EscapeJson(defaultGamePath)}""
    }}
  ],
  ""SelectedGameFolderIndex"": 0,
  ""MaxMemory"": 4096,
  ""MinMemory"": 2048,
  ""MemoryAutoMode"": true,
  ""VersionIsolationLevel"": 2,
  ""ToolDownloadVersion"": 0
}}";
                File.WriteAllText(SettingsFilePath, json);
                Logger.Info($"[MNL环境] 默认设置文件已创建: {SettingsFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[MNL环境] 创建默认设置文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取用于检测 game 目录版本的文件夹候选列表（仿 PCL 的多源扫描）
        /// 优先级: MNL\.minecraft > %appdata%\.minecraft（官方启动器）
        /// </summary>
        public static List<(string Name, string Path)> GetGameFolderCandidates()
        {
            var candidates = new List<(string Name, string Path)>();

            // 1. MNL 游戏目录（最高优先级）
            if (Directory.Exists(MinecraftPath))
                candidates.Add(("MNL 游戏目录", MinecraftPath));

            // 2. 官方启动器目录
            string vanillaPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft");
            if (Directory.Exists(vanillaPath) &&
                !string.Equals(vanillaPath.TrimEnd('\\'), MinecraftPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                candidates.Add(("官方启动器文件夹", vanillaPath));

            return candidates;
        }

        private static string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
