using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// MNL 启动器环境管理器 —— 参考 PCL 的目录管理方案
    /// 优先使用官方 .minecraft 文件夹（%appdata%\.minecraft）；
    /// 若不存在则在程序同级 "MNL" 文件夹下创建新的游戏目录。
    /// 
    /// 目录结构:
    ///   {exe dir}/
    ///   └── MNL/
    ///       ├── .minecraft/          ← 游戏目录（无官方目录时使用）
    ///       │   ├── versions/
    ///       │   ├── assets/
    ///       │   │   └── indexes/
    ///       │   ├── libraries/
    ///       │   ├── saves/
    ///       │   ├── mods/
    ///       │   ├── resourcepacks/
    ///       │   ├── shaderpacks/
    ///       │   ├── screenshots/
    ///       │   ├── crash-reports/
    ///       │   ├── server-resource-packs/
    ///       │   ├── logs/
    ///       │   ├── bedrock/
    ///       │   ├── java/
    ///       │   ├── runtime/
    ///       │   ├── config/
    ///       │   └── launcher-logs/
    ///       ├── skins/               ← 皮肤文件
    ///       ├── logs/                ← 启动器日志
    ///       ├── backgrounds/         ← 背景图片素材库
    ///       ├── config.ini           ← 配置文件
    ///       └── settings.json        ← 启动器设置
    /// </summary>
    public static class MNLEnvironment
    {
        /// <summary>MNL 根目录（{exe dir}\MNL\）</summary>
        public static string MNLFolder { get; private set; }

        /// <summary>游戏目录（优先官方 %appdata%\.minecraft，否则 MNL\.minecraft）</summary>
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

        /// <summary>MNL 文件夹中必须存在的关键目录（用于完整性检查，相对于 MNLFolder）</summary>
        private static readonly string[] RequiredMNLDirectories = new[]
        {
            "skins",
            "backgrounds"
        };

        /// <summary>游戏目录下必须存在的子目录（相对于 MinecraftPath，与官方 .minecraft 结构一致）</summary>
        private static readonly string[] RequiredGameDirectories = new[]
        {
            "versions",
            "assets",
            "assets\\indexes",
            "libraries",
            "saves",
            "mods",
            "resourcepacks",
            "shaderpacks",
            "screenshots",
            "crash-reports",
            "server-resource-packs",
            "logs",
            "java",
            "runtime",
            "config",
            "launcher-logs"
        };

        /// <summary>
        /// 获取应用程序所在目录。
        /// Environment.ProcessPath 在所有发布模式下都返回真实的 exe 路径。
        /// 同时写入诊断文件到 %TEMP%\mnl_path_diag.txt 用于排查。
        /// </summary>
        private static string GetAppDirectory()
        {
            string exePath = Environment.ProcessPath;
            string exeDir = !string.IsNullOrEmpty(exePath)
                ? Path.GetDirectoryName(exePath)
                : null;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 诊断：写入 %TEMP% 确保无论何种模式都能看到实际路径
            try
            {
                string diagFile = Path.Combine(Path.GetTempPath(), "mnl_path_diag.txt");
                File.WriteAllText(diagFile,
                    $"ProcessPath: {exePath ?? "(null)"}\n" +
                    $"ProcessPathDir: {exeDir ?? "(null)"}\n" +
                    $"BaseDirectory: {baseDir}\n" +
                    $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                Console.WriteLine($"[MNL Diag] 诊断文件: {diagFile}");
            }
            catch { }

            if (!string.IsNullOrEmpty(exeDir))
                return exeDir;

            return baseDir;
        }

        /// <summary>
        /// 初始化 MNL 环境。在 App.OnStartup 中尽早调用。
        /// 优先扫描官方 .minecraft 文件夹（%appdata%\.minecraft），
        /// 如果存在则直接使用；否则在 MNL 目录下创建新的游戏文件夹。
        /// </summary>
        /// <returns>依赖是否完整（false 表示有缺失）</returns>
        public static bool Initialize()
        {
            if (IsInitialized) return MissingDependencies.Count == 0;

            try
            {
                AppDirectory = GetAppDirectory();
                // 参考 PCL：工作目录与程序同级（{exe_dir}\MNL\），而非 %appdata%\MNL
                MNLFolder = Path.Combine(AppDirectory, "MNL");
                SkinsPath = Path.Combine(MNLFolder, "skins");
                LogsPath = Path.Combine(MNLFolder, "logs");
                ConfigFilePath = Path.Combine(MNLFolder, "config.ini");
                SettingsFilePath = Path.Combine(MNLFolder, "settings.json");

                // ★ 优先扫描官方 .minecraft 文件夹
                string officialMcPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ".minecraft");

                if (Directory.Exists(officialMcPath))
                {
                    // 检查路径是否含非 ASCII 字符（中文用户名等）
                    // JDK-8214514: Java @file 对非 ASCII 路径编码处理有历史缺陷
                    bool hasNonAscii = false;
                    foreach (char c in officialMcPath)
                    {
                        if (c > 127) { hasNonAscii = true; break; }
                    }

                    if (hasNonAscii)
                    {
                        // 官方路径含中文，为避免编码问题，改用 MNL 本地目录
                        MinecraftPath = Path.Combine(MNLFolder, ".minecraft");
                        Logger.Warning($"[MNL环境] 官方游戏目录含非ASCII字符（可能是中文用户名），已自动切换到 MNL 本地目录");
                        Logger.Warning($"[MNL环境] 原始路径: {officialMcPath}");
                        Logger.Warning($"[MNL环境] 使用路径: {MinecraftPath}");
                        Logger.Warning($"[MNL环境] 如需使用官方目录，请在设置中手动切换");
                    }
                    else
                    {
                        MinecraftPath = officialMcPath;
                        Logger.Info("[MNL环境] 检测到官方游戏目录，直接使用");
                    }
                }
                else
                {
                    MinecraftPath = Path.Combine(MNLFolder, ".minecraft");
                    Logger.Info("[MNL环境] 未检测到官方游戏目录，将在 MNL 目录下创建");
                }

                Logger.Info("[MNL环境] ========================================");
                Logger.Info($"[MNL环境] 应用目录: {AppDirectory}");
                Logger.Info($"[MNL环境] MNL目录: {MNLFolder}");
                Logger.Info($"[MNL环境] 游戏目录: {MinecraftPath}");

                // 诊断：控制台输出路径信息（单文件模式下帮助排查）
                Console.WriteLine("====== MNL 环境初始化 ======");
                Console.WriteLine($"ProcessPath: {Environment.ProcessPath}");
                Console.WriteLine($"AppDirectory: {AppDirectory}");
                Console.WriteLine($"MNLFolder: {MNLFolder}");
                Console.WriteLine($"MinecraftPath: {MinecraftPath}");
                Console.WriteLine("============================");

                // 1. 确保所有必需目录存在
                EnsureDirectories();

                // 2. 迁移旧配置文件（如果存在）
                MigrateOldConfig();

                // 3. 依赖完整性检查
                CheckDependencyIntegrity();

                IsInitialized = true;
                Logger.Info($"[MNL环境] 初始化完成，缺失依赖: {MissingDependencies.Count} 项");
                return MissingDependencies.Count == 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"[MNL环境] 初始化失败: {ex.Message}", ex);
                // 回退到默认值
                AppDirectory = GetAppDirectory();
                MNLFolder = Path.Combine(AppDirectory, "MNL");
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
        /// 确保所有必需的目录存在（MNL 辅助目录 + 游戏子目录）
        /// </summary>
        private static void EnsureDirectories()
        {
            // 0. 显式确保 MNL 根文件夹存在
            if (!Directory.Exists(MNLFolder))
            {
                try
                {
                    Directory.CreateDirectory(MNLFolder);
                    Logger.Info($"[MNL环境] 创建 MNL 根目录: {MNLFolder}");
                    Console.WriteLine($"[MNL环境] 创建 MNL 根目录: {MNLFolder}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[MNL环境] 创建 MNL 根目录失败: {ex.Message}", ex);
                    Console.WriteLine($"[MNL环境] 创建 MNL 根目录失败: {ex.Message}");
                    throw;
                }
            }

            // 1. 确保 MNL 辅助目录存在（skins、logs、backgrounds）
            foreach (var dir in RequiredMNLDirectories)
            {
                string fullPath = Path.Combine(MNLFolder, dir);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    Logger.Info($"[MNL环境] 创建目录: {fullPath}");
                }
            }

            // 2. 确保游戏目录及其子目录存在
            if (!Directory.Exists(MinecraftPath))
            {
                Directory.CreateDirectory(MinecraftPath);
                Logger.Info($"[MNL环境] 创建游戏目录: {MinecraftPath}");
            }

            foreach (var dir in RequiredGameDirectories)
            {
                string fullPath = Path.Combine(MinecraftPath, dir);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    Logger.Info($"[MNL环境] 创建游戏子目录: {fullPath}");
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
        /// 检查程序依赖完整性（MNL 目录结构、关键 DLL 等）
        /// </summary>
        private static void CheckDependencyIntegrity()
        {
            MissingDependencies.Clear();

            // 1. 检查 MNL 辅助目录结构
            foreach (var dir in RequiredMNLDirectories)
            {
                string fullPath = Path.Combine(MNLFolder, dir);
                if (!Directory.Exists(fullPath))
                {
                    MissingDependencies.Add($"目录缺失: {dir}");
                }
            }

            // 2. 检查游戏目录结构
            foreach (var dir in RequiredGameDirectories)
            {
                string fullPath = Path.Combine(MinecraftPath, dir);
                if (!Directory.Exists(fullPath))
                {
                    MissingDependencies.Add($"游戏目录缺失: {dir}");
                }
            }

            // 2. 检查关键程序文件（.NET 运行时依赖）
            // 单文件发布模式下，DLL 和 runtimeconfig 被打包进 EXE 中，磁盘上不存在是正常的
            // 检测：AppDirectory（EXE实际目录）与 BaseDirectory（.NET运行时目录）不同即为单文件模式
            bool isSingleFile = !string.Equals(
                (AppDirectory ?? "").TrimEnd(Path.DirectorySeparatorChar),
                AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
            
            if (!isSingleFile)
            {
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
            }
            else
            {
                Logger.Info("[MNL环境] 检测到单文件发布模式，跳过 DLL/runtimeconfig 检查");
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
        /// 优先级: 官方启动器文件夹 > MNL 游戏目录
        /// </summary>
        public static List<(string Name, string Path)> GetGameFolderCandidates()
        {
            var candidates = new List<(string Name, string Path)>();

            // 1. 官方启动器目录（最高优先级）
            string vanillaPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft");
            if (Directory.Exists(vanillaPath))
                candidates.Add(("官方启动器文件夹", vanillaPath));

            // 2. MNL 游戏目录（如果不同于官方目录）
            if (Directory.Exists(MinecraftPath) &&
                !string.Equals(MinecraftPath.TrimEnd('\\'), vanillaPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                candidates.Add(("MNL 游戏目录", MinecraftPath));

            return candidates;
        }

        private static string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
