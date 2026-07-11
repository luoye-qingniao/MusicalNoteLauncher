using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MusicalNoteLauncher.Controls;
using PCLCS;

namespace MusicalNoteLauncher.Core
{
    public class GameLauncher
    {
        public event Action<string> LaunchStatusChanged;
        public event Action<string> LaunchLogReceived;
        public event Action<bool> LaunchCompleted;
        /// <summary>启动完成（附带完整详情）</summary>
        public event Action<GameLaunchInfo> LaunchResultCompleted;

        /// <summary>最近一次启动的完整详情</summary>
        public GameLaunchInfo LastLaunchInfo { get; private set; }

        private readonly string _minecraftPath;
        private readonly string _javaPath;
        private Process _gameProcess;
        private bool _hasExited = false;
        private string _logFilePath;
        private bool _hasLoggedBadOption = false;
        private readonly object _logFileLock = new object();
        private readonly SemaphoreSlim _nativesLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _cpFileLock = new SemaphoreSlim(1, 1);
        private SynchronizationContext _syncContext;

        public GameLauncher(string minecraftPath, string javaPath = null)
        {
            _minecraftPath = minecraftPath;
            _syncContext = SynchronizationContext.Current;
            string detectedPath = javaPath ?? TryFindJavaPath();
            _javaPath = detectedPath;
            InitializeLogFile();
        }

        public GameLauncher(string minecraftPath, JavaConfigManager javaConfig)
        {
            _minecraftPath = minecraftPath;
            _syncContext = SynchronizationContext.Current;
            string javaFromConfig = javaConfig?.GetJavaPath();
            string detectedPath = !string.IsNullOrEmpty(javaFromConfig) ? javaFromConfig : TryFindJavaPath();
            _javaPath = detectedPath;
            InitializeLogFile();
        }

        private void InitializeLogFile()
        {
            try
            {
                string logsDir = Path.Combine(_minecraftPath, "launcher-logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _logFilePath = Path.Combine(logsDir, $"launch_{timestamp}.log");
                
                WriteLogToFile($"========== 启动器日志开始 {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==========");
                WriteLogToFile($"Minecraft路径: {_minecraftPath}");
                WriteLogToFile($"Java路径: {_javaPath}");
                WriteLogToFile($"系统架构: {(Environment.Is64BitOperatingSystem ? "64位" : "32位")}");
                WriteLogToFile($"操作系统: {Environment.OSVersion.VersionString}");
                WriteLogToFile("============================================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化日志文件失败: {ex.Message}");
            }
        }

        private void WriteLogToFile(string log)
        {
            if (string.IsNullOrEmpty(_logFilePath))
                return;

            try
            {
                lock (_logFileLock)
                {
                    File.AppendAllText(_logFilePath, $"[{DateTime.Now:HH:mm:ss}] {log}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"写入日志文件失败: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            WriteLogToFile(message);
            LaunchLogReceived?.Invoke(message);
        }

        private string ValidateAndFixJavaPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Equals("java.exe", StringComparison.OrdinalIgnoreCase))
            {
                Log($"[Java] 输入的Java路径无效，重新查找完整路径...");
                path = TryFindJavaPath();
            }

            if (!string.IsNullOrEmpty(path))
            {
                if (Path.GetFileName(path).Equals("java.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Path.IsPathRooted(path))
                    {
                        Log($"[Java] 路径 '{path}' 不是完整路径，尝试查找完整路径...");
                        
                        string fullPath = FindJavaExeInSystem();
                        if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                        {
                            Log($"[Java] 找到完整路径: {fullPath}");
                            return fullPath;
                        }
                    }
                    else if (File.Exists(path))
                    {
                        Log($"[Java] 使用Java路径: {path}");
                        return path;
                    }
                }
                else if (Path.IsPathRooted(path))
                {
                    if (File.Exists(path))
                    {
                        Log($"[Java] 使用Java路径: {path}");
                        return path;
                    }
                    else if (Directory.Exists(path))
                    {
                        string javaExePath = Path.Combine(path, "bin", "java.exe");
                        if (File.Exists(javaExePath))
                        {
                            Log($"[Java] 从JDK目录找到java.exe: {javaExePath}");
                            return javaExePath;
                        }
                        
                        foreach (string subDir in Directory.GetDirectories(path))
                        {
                            javaExePath = Path.Combine(subDir, "bin", "java.exe");
                            if (File.Exists(javaExePath))
                            {
                                Log($"[Java] 从子目录找到java.exe: {javaExePath}");
                                return javaExePath;
                            }
                        }
                        
                        Log($"[Java] 警告: JDK目录 '{path}' 中未找到java.exe");
                    }
                }
            }

            Log($"[Java] 警告: 无法验证路径 '{path}'，尝试最后查找...");
            string lastAttempt = TryFindJavaPath();
            if (!string.IsNullOrEmpty(lastAttempt) && File.Exists(lastAttempt))
            {
                Log($"[Java] 最后找到有效路径: {lastAttempt}");
                return lastAttempt;
            }

            return null;
        }

        private string TryFindJavaPath()
        {
            try
            {
                Log($"[Java] 开始检测Java路径...");
                Log($"[Java] 系统架构: {(Environment.Is64BitOperatingSystem ? "64位" : "32位")}");

                var prioritizedPaths = new SortedDictionary<string, List<string>>(Comparer<string>.Create((a, b) =>
                {
                    int scoreA = GetJavaVersionScore(a);
                    int scoreB = GetJavaVersionScore(b);
                    return scoreB.CompareTo(scoreA);
                }));

                string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrEmpty(javaHome))
                {
                    AddJavaPath(prioritizedPaths, javaHome, "JAVA_HOME");
                }

                DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Eclipse Adoptium", "Eclipse Adoptium");
                DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Temurin", "Temurin");
                DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Microsoft\jdk", "Microsoft JDK");
                DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Amazon Corretto", "Amazon Corretto");
                DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Java", "Java");

                // 优先检查 MNL 游戏目录下的 Java 和运行时
                string mnlJavaDir = Path.Combine(AppContext.MinecraftPath, "java");
                if (Directory.Exists(mnlJavaDir))
                {
                    try
                    {
                        foreach (string subDir in Directory.GetDirectories(mnlJavaDir))
                        {
                            AddJavaPath(prioritizedPaths, Path.Combine(subDir, "bin"), "MNL/java");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"扫描 MNL Java 目录失败: {ex.Message}");
                    }
                }

                string mnlRuntimeDir = Path.Combine(AppContext.MinecraftPath, "runtime");
                if (Directory.Exists(mnlRuntimeDir))
                {
                    try
                    {
                        foreach (string subDir in Directory.GetDirectories(mnlRuntimeDir))
                        {
                            AddJavaPath(prioritizedPaths, Path.Combine(subDir, "bin"), "MNL/runtime");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"扫描 MNL 运行时失败: {ex.Message}");
                    }
                }

                string userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime");
                if (Directory.Exists(userDir))
                {
                    try
                    {
                        foreach (string subDir in Directory.GetDirectories(userDir))
                        {
                            AddJavaPath(prioritizedPaths, Path.Combine(subDir, "bin"), ".minecraft/runtime");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[Java] 检查runtime目录失败: {ex.Message}");
                    }
                }

                DetectJavaFromRegistry(prioritizedPaths);

                var commonPaths = new List<string> {
                    @"C:\Program Files\Eclipse Adoptium\jdk-17.0.9.9-hotspot\bin\java.exe",
                    @"C:\Program Files\Eclipse Adoptium\jdk-17.0.8.8-hotspot\bin\java.exe",
                    @"C:\Program Files\Java\jdk-17\bin\java.exe",
                    @"C:\Program Files\Java\jdk-21\bin\java.exe",
                    @"C:\Program Files\Java\jdk1.8.0_401\bin\java.exe",
                    @"C:\Program Files\Java\jdk1.8.0_301\bin\java.exe"
                };
                foreach (var path in commonPaths)
                {
                    AddJavaPath(prioritizedPaths, path, "常见路径");
                }

                foreach (var kvp in prioritizedPaths)
                {
                    foreach (string source in kvp.Value)
                    {
                        string javaExe = FindJavaExeInPath(source);
                        if (!string.IsNullOrEmpty(javaExe) && File.Exists(javaExe))
                        {
                            string version = GetJavaVersionFromExe(javaExe);
                            Log($"[Java] 找到Java: {javaExe}");
                            Log($"[Java] 版本: {version}");
                            Log($"[Java] 来源: {source}");
                            return javaExe;
                        }
                    }
                }

                Log($"[Java] 未能找到预定义路径，尝试系统PATH...");

                try
                {
                    var pathEnv = Environment.GetEnvironmentVariable("PATH");
                    if (!string.IsNullOrEmpty(pathEnv))
                    {
                        foreach (string pathDir in pathEnv.Split(Path.PathSeparator))
                        {
                            if (string.IsNullOrEmpty(pathDir))
                                continue;

                            string javaExePath = Path.Combine(pathDir, "java.exe");
                            if (File.Exists(javaExePath))
                            {
                                string version = GetJavaVersionFromExe(javaExePath);
                                Log($"[Java] 从系统PATH找到: {javaExePath}");
                                Log($"[Java] 版本: {version}");
                                return javaExePath;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Java] 查找PATH中的Java失败: {ex.Message}");
                }

                string systemJava = FindJavaExeInSystem();
                if (!string.IsNullOrEmpty(systemJava) && File.Exists(systemJava))
                {
                    Log($"[Java] 通过系统命令找到: {systemJava}");
                    return systemJava;
                }

                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string[] commonJavaPaths = {
                    Path.Combine(programFiles, "Eclipse Adoptium", "jdk-17.0.9.9-hotspot", "bin", "java.exe"),
                    Path.Combine(programFiles, "Eclipse Adoptium", "jdk-17.0.8.8-hotspot", "bin", "java.exe"),
                    Path.Combine(programFiles, "Java", "jdk-17", "bin", "java.exe"),
                    Path.Combine(programFiles, "Java", "jdk-21", "bin", "java.exe")
                };

                foreach (string path in commonJavaPaths)
                {
                    if (File.Exists(path))
                    {
                        string version = GetJavaVersionFromExe(path);
                        Log($"[Java] 最后尝试找到: {path}");
                        Log($"[Java] 版本: {version}");
                        return path;
                    }
                }

                Log($"[Java] 警告: 未能找到有效的Java安装");
                return null;
            }
            catch (Exception ex)
            {
                Log($"[Java] 检测Java路径时发生异常: {ex.Message}");
                return null;
            }
        }

        private string FindJavaExeInSystem()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "java",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        string result = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        foreach (string line in result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string trimmedLine = line.Trim();
                            if (!string.IsNullOrEmpty(trimmedLine) && File.Exists(trimmedLine))
                            {
                                return trimmedLine;
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                var psInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"Get-Command java -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psInfo))
                {
                    if (process != null)
                    {
                        string result = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (!string.IsNullOrEmpty(result) && File.Exists(result))
                        {
                            return result;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private void UpdateFullscreenOption(bool isFullscreen)
        {
            try
            {
                string optionsPath = Path.Combine(_minecraftPath, "options.txt");
                if (File.Exists(optionsPath))
                {
                    string content = File.ReadAllText(optionsPath);
                    string oldValue = isFullscreen ? "fullscreen:false" : "fullscreen:true";
                    string newValue = isFullscreen ? "fullscreen:true" : "fullscreen:false";
                    
                    if (content.Contains(oldValue))
                    {
                        content = content.Replace(oldValue, newValue);
                        File.WriteAllText(optionsPath, content, Encoding.UTF8);
                        Log($"[选项] 更新全屏设置: {newValue}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[警告] 无法更新 options.txt: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断版本是否为继承版本（如整合包版本，有 inheritsFrom 字段）
        /// </summary>
        private bool IsInheritedVersion(string versionId)
        {
            try
            {
                string jsonFile = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.json");
                if (!File.Exists(jsonFile)) return false;
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonFile)))
                {
                    return doc.RootElement.TryGetProperty("inheritsFrom", out _);
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// 判断版本是否可安装 Mod（有 Forge/Fabric/NeoForge 等加载器）
        /// </summary>
        private bool IsVersionModable(string versionId)
        {
            try
            {
                return ModLoaderDetector.DetectModLoader(_minecraftPath, versionId) != ModLoaderDetector.ModLoaderType.None;
            }
            catch { return false; }
        }

        /// <summary>
        /// 判断版本是否为正式版（非快照、非旧版、非愚人节版本）
        /// </summary>
        private bool IsVersionRelease(string versionId)
        {
            try
            {
                string jsonFile = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.json");
                if (!File.Exists(jsonFile)) return true; // 没有 JSON 视为正式版
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonFile)))
                {
                    if (doc.RootElement.TryGetProperty("type", out var typeProp))
                    {
                        string type = typeProp.GetString() ?? "";
                        return type == "release";
                    }
                }
                return true; // 没有 type 字段视为正式版
            }
            catch { return true; }
        }

        private void SetupChineseLanguageForVersion(string versionId)
        {
            try
            {
                // 步骤1：获取实际的 Minecraft 版本号（处理 Fabric/Forge 等继承版本）
                // 例如 fabric-loader-0.19.3-1.20.1 的 inheritsFrom 是 "1.20.1"
                string actualVersion = versionId;
                try
                {
                    string jsonFile = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.json");
                    if (File.Exists(jsonFile))
                    {
                        string jsonContent = File.ReadAllText(jsonFile);
                        using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                        {
                            if (doc.RootElement.TryGetProperty("inheritsFrom", out var inhProp))
                            {
                                string inheritName = inhProp.GetString();
                                if (!string.IsNullOrEmpty(inheritName))
                                {
                                    actualVersion = inheritName;
                                    Log($"[语言] 检测到继承版本，实际 Minecraft 版本: {actualVersion}");
                                }
                            }
                        }
                    }
                }
                catch { }

                // 步骤2：解析实际的 Minecraft 版本号，获取次版本号
                // 例如 "1.20.1" -> 20, "1.16.5" -> 16
                int majorVersion = 20; // 默认 1.20 兼容大多数版本
                try
                {
                    string[] parts = actualVersion.Split('.');
                    if (parts.Length >= 2 && parts[0] == "1")
                    {
                        // 1.xx.y 格式
                        if (int.TryParse(parts[1], out int v))
                        {
                            majorVersion = v;
                        }
                    }
                    else if (parts.Length >= 1)
                    {
                        // 其他格式，尝试直接解析第一个
                        int.TryParse(parts[0], out majorVersion);
                    }
                }
                catch
                {
                    // 保留默认值
                }

                Log($"[语言] 使用的 Minecraft 次版本号: {majorVersion}");

                // 步骤3：获取游戏的 options.txt 路径（使用版本隔离的游戏目录）
                string gameDir2 = _minecraftPath;
                bool isInheritedForLang = IsInheritedVersion(versionId);
                bool isModableForLang = IsVersionModable(versionId);
                bool isReleaseForLang = IsVersionRelease(versionId);
                if (SettingsManager.Settings.ShouldIsolateVersion(isModableForLang, isReleaseForLang, isInheritedForLang))
                {
                    gameDir2 = Path.Combine(_minecraftPath, "versions", versionId, "game");
                    Directory.CreateDirectory(gameDir2);
                }
                
                string optionsPath = Path.Combine(gameDir2, "options.txt");
                
                // 步骤4：确保 options.txt 存在
                if (!File.Exists(optionsPath))
                {
                    // 尝试从主目录复制
                    string mainOptionsPath = Path.Combine(_minecraftPath, "options.txt");
                    if (File.Exists(mainOptionsPath))
                    {
                        File.Copy(mainOptionsPath, optionsPath, true);
                        Log($"[语言] 已从主目录复制 options.txt 到游戏目录");
                    }
                    else
                    {
                        // 创建最小的 options.txt，至少包含 lang 键
                        Directory.CreateDirectory(Path.GetDirectoryName(optionsPath));
                        File.WriteAllText(optionsPath, "lang:zh_cn\n", Encoding.UTF8);
                        Log($"[语言] 主目录没有 options.txt，已创建最小的 options.txt");
                    }
                }

                // 步骤5：使用 ChineseLaunchHelper 设置中文语言（强制设置）
                var result = PCLCS.ChineseLaunchHelper.SetupChineseLanguage(
                    optionsPath,
                    majorVersion,
                    preferChinese: true,
                    onLog: (msg) => Log($"[语言] {msg}"),
                    forceLanguage: true
                );

                if (result.Success)
                {
                    Log($"[语言] 中文语言设置成功（当前: {result.CurrentLanguage} -> 目标: {result.TargetLanguage}）");
                    
                    // 步骤6：双重保险——直接修改 options.txt 的 lang 键，确保值正确
                    try
                    {
                        string targetLang = majorVersion >= 19 ? "zh_cn" : "zh_cn";
                        string content = File.ReadAllText(optionsPath);
                        if (content.Contains("lang:"))
                        {
                            // 替换现有 lang 键
                            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
                            bool found = false;
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].StartsWith("lang:"))
                                {
                                    lines[i] = "lang:" + targetLang;
                                    found = true;
                                    break;
                                }
                            }
                            if (found)
                            {
                                File.WriteAllText(optionsPath, string.Join("\n", lines), Encoding.UTF8);
                                Log($"[语言] 已强制设置 lang={targetLang}");
                            }
                        }
                        else
                        {
                            // 追加 lang 键
                            File.AppendAllText(optionsPath, "\nlang:" + targetLang + "\n");
                            Log($"[语言] 已追加 lang={targetLang}");
                        }
                    }
                    catch (Exception ex2)
                    {
                        Log($"[语言] 手动设置 lang 键时出错: {ex2.Message}");
                    }
                }
                else
                {
                    Log($"[语言] ChineseLaunchHelper 设置失败: {result.ErrorMessage}");
                    
                    // 备用方案：直接写入 options.txt
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(optionsPath));
                        string content = File.Exists(optionsPath) ? File.ReadAllText(optionsPath) : "";
                        if (content.Contains("lang:"))
                        {
                            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].StartsWith("lang:"))
                                {
                                    lines[i] = "lang:zh_cn";
                                    break;
                                }
                            }
                            File.WriteAllText(optionsPath, string.Join("\n", lines), Encoding.UTF8);
                        }
                        else
                        {
                            File.WriteAllText(optionsPath, content + "\nlang:zh_cn\n", Encoding.UTF8);
                        }
                        Log($"[语言] 备用方案：已直接写入 lang:zh_cn");
                    }
                    catch (Exception ex2)
                    {
                        Log($"[语言] 备用方案也失败: {ex2.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[语言] 设置中文语言时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动前校验 options.txt 配置文件完整性。
        /// 若文件损坏或包含大量无效配置，弹窗提示用户可一键重置。
        /// </summary>
        private void ValidateAndPromptOptionsReset(string versionId)
        {
            try
            {
                // 确定 options.txt 路径（处理版本隔离）
                string gameDir = _minecraftPath;
                bool isInherited = IsInheritedVersion(versionId);
                bool isModable = IsVersionModable(versionId);
                bool isRelease = IsVersionRelease(versionId);
                if (SettingsManager.Settings.ShouldIsolateVersion(isModable, isRelease, isInherited))
                {
                    gameDir = Path.Combine(_minecraftPath, "versions", versionId, "game");
                }

                string optionsPath = Path.Combine(gameDir, "options.txt");
                var result = OptionsFileValidator.Validate(optionsPath);

                if (!result.ShouldSuggestReset)
                {
                    Log($"[配置校验] options.txt 状态正常 ({result.ValidLines} 条有效配置)");
                    return;
                }

                // 构建提示消息
                string message;
                if (!result.Exists)
                {
                    message = "未检测到游戏配置文件 (options.txt)。\n\n" +
                              "这通常不影响游戏启动，Minecraft 会自动创建默认配置。";
                    Log($"[配置校验] options.txt 不存在，将跳过校验");
                    return; // 不存在不算错误，游戏会自动创建
                }

                if (result.BadEntries.Count > 0)
                {
                    string badSample = string.Join("\n  • ", result.BadEntries.Take(3));
                    message = $"检测到游戏配置文件 (options.txt) 包含 {result.BadLineCount} 条损坏/无效配置项：\n\n" +
                              $"  • {badSample}\n\n" +
                              $"这可能导致游戏中出现 \"Skipping bad option\" 警告。\n" +
                              $"是否立即重置配置文件？（原文件会自动备份）";
                }
                else
                {
                    message = $"游戏配置文件 (options.txt) 可能存在问题。\n\n" +
                              $"是否立即重置配置文件？（原文件会自动备份）";
                }

                Log($"[配置校验] options.txt 需要修复: {result.BadLineCount} 条损坏 / {result.TotalLines} 总行");

                // 弹窗询问用户
                bool shouldReset = ModernMessageBox.ShowConfirm(message, "配置文件校验");
                if (shouldReset)
                {
                    string backupPath = OptionsFileValidator.ResetWithBackup(optionsPath, keepExistingValid: true);
                    Log($"[配置重置] 已重置 options.txt，备份: {backupPath}");
                    ModernMessageBox.ShowInfo(
                        $"游戏配置已重置！\n\n备份文件: {Path.GetFileName(backupPath)}",
                        "重置成功");
                }
                else
                {
                    Log("[配置校验] 用户选择跳过重置");
                }
            }
            catch (Exception ex)
            {
                Log($"[配置校验] 校验过程出错: {ex.Message}");
            }
        }

        public async Task<bool> LaunchGameAsync(string versionId, string username, int minMemoryMb, int maxMemoryMb,
            string additionalArgs = "", bool offlineMode = true, string resolution = "1920x1080", bool isFullscreen = false)
        {
            Log("═══════════════════════════════════════════════════════════════");
            Log("【启动流程开始】");
            Log($"版本: {versionId}, 用户: {username}, 内存: {minMemoryMb}M-{maxMemoryMb}M");
            Log($"离线模式: {offlineMode}, 分辨率: {resolution}, 全屏: {isFullscreen}");
            Log("═══════════════════════════════════════════════════════════════");

            // ★ 启动前校验 options.txt 配置文件
            ValidateAndPromptOptionsReset(versionId);

            // 根据用户选择修改 options.txt 中的全屏设置
            UpdateFullscreenOption(isFullscreen);

            // 设置中文语言
            SetupChineseLanguageForVersion(versionId);

            return await Task.Run(async () =>
            {
                var result = new GameLaunchInfo
                {
                    VersionId = versionId,
                    Username = username,
                    Memory = $"{minMemoryMb}M - {maxMemoryMb}M",
                    Resolution = resolution,
                    JavaPath = _javaPath,
                    LogFilePath = _logFilePath,
                };

                try
                {
                    _hasExited = false;

                    Log("【步骤0】清理临时文件...");
                    await Task.Run(() => CleanupTempClasspathFiles());
                    Log("【步骤0】清理完成");

                    // 内存优化 (PCL风格)
                    if (SettingsManager.Settings.LaunchArgumentRam)
                    {
                        Log("【内存优化】启动游戏前执行内存优化...");
                        await PostStatusAsync("正在进行内存优化...");
                        try
                        {
                            var memResult = await MemoryOptimizer.OptimizeAsync(null);
                            Log($"[内存优化] 完成，释放内存: {memResult.FreedMb} MB, 修剪进程: {memResult.ProcessesTrimmed}");
                        }
                        catch (Exception memEx)
                        {
                            Log($"[内存优化] 警告 - 优化失败: {memEx.Message}（将跳过继续启动）");
                        }
                    }

                    Log("【步骤1】验证Java路径...");
                    await PostStatusAsync("正在验证Java路径...");
                    string validatedJavaPath = await Task.Run(() => ValidateAndFixJavaPath(_javaPath));
                    Log($"[Java] 验证的Java路径: {validatedJavaPath}");

                    if (string.IsNullOrEmpty(validatedJavaPath))
                    {
                        await PostStatusAsync("无法找到有效的Java安装。请安装Java 17或更高版本，或在设置中手动指定Java路径。");
                        Log("[错误] 无法找到有效的Java安装");
                        result.IsSuccess = false;
                        result.ErrorMessage = "无法找到有效的Java安装。请安装Java 17或更高版本。";
                        LastLaunchInfo = result;
                        PostCompleted(false);
                        return false;
                    }

                    Log($"[Java] Java版本检查:");
                    int detectedJavaMajor = 0;
                    try
                    {
                        var javaVersionInfo = new ProcessStartInfo
                        {
                            FileName = validatedJavaPath,
                            Arguments = "-version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        using (var proc = Process.Start(javaVersionInfo))
                        {
                            string javaVersion = proc.StandardError.ReadToEnd();
                            proc.WaitForExit();
                            // 解析主版本号，如 "java version \"21.0.1\" 2023-10-17 LTS"
                            var match = System.Text.RegularExpressions.Regex.Match(javaVersion, @"version\s+""(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int major))
                            {
                                detectedJavaMajor = major;
                            }
                            foreach (var line in javaVersion.Split('\n').Take(3))
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                    Log($"[Java] {line.Trim()}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[Java] 获取版本失败: {ex.Message}");
                    }

                    Log("【步骤2】校验版本完整性...");
                    await PostStatusAsync("正在校验版本完整性...");
                    bool versionValid = await VerifyVersionAsync(versionId);
                    if (!versionValid)
                    {
                        await PostStatusAsync($"版本 {versionId} 校验失败，请重新下载");
                        result.IsSuccess = false;
                        result.ErrorMessage = $"版本 {versionId} 校验失败，请重新下载";
                        LastLaunchInfo = result;
                        PostCompleted(false);
                        return false;
                    }
                    Log($"[版本] {versionId} 校验通过");

                    Log("【步骤3】检查依赖库完整性...");
                    await PostStatusAsync("正在检查依赖库完整性...");
                    var repairService = new VersionRepairService(_minecraftPath);
                    repairService.StatusChanged += (status) => Log($"[修复] {status}");

                    var repairResult = await repairService.RepairVersionAsync(versionId);
                    if (!repairResult.IsSuccess)
                    {
                        await PostStatusAsync($"依赖库修复失败: {repairResult.ErrorMessage}");
                        result.IsSuccess = false;
                        result.ErrorMessage = $"依赖库修复失败: {repairResult.ErrorMessage}";
                        LastLaunchInfo = result;
                        PostCompleted(false);
                        return false;
                    }

                    if (repairResult.MissingLibraries.Count > 0 || repairResult.MissingNatives.Count > 0)
                    {
                        await PostStatusAsync("依赖库修复完成，正在重新校验...");
                        Log($"[修复] 已下载 {repairResult.MissingLibraries.Count} 个库文件");
                        Log($"[修复] 已下载 {repairResult.MissingNatives.Count} 个原生库文件");
                    }
                    else
                    {
                        Log($"[修复] 依赖库完整性校验通过，无需下载");
                    }

                    Log("【步骤4】加载版本信息...");
                    await PostStatusAsync("正在加载版本信息...");
                    var versionInfo = await Task.Run(() => LoadVersionInfo(versionId));
                    if (versionInfo.ValueKind == JsonValueKind.Undefined)
                    {
                        await PostStatusAsync("无法读取版本信息");
                        result.IsSuccess = false;
                        result.ErrorMessage = "无法读取版本信息，版本JSON加载失败";
                        LastLaunchInfo = result;
                        PostCompleted(false);
                        return false;
                    }
                    Log($"[版本] 版本JSON加载成功");

                    if (versionInfo.TryGetProperty("mainClass", out var mainClassElement))
                    {
                        Log($"[版本] 主类: {mainClassElement.GetString()}");
                    }

                    if (versionInfo.TryGetProperty("inheritsFrom", out var inheritsElement))
                    {
                        Log($"[版本] 继承自: {inheritsElement.GetString()}");
                    }

                    // 检测版本所需的最低 Java 版本
                    int requiredJavaMajor = 0;
                    if (versionInfo.TryGetProperty("javaVersion", out var javaVersionElement))
                    {
                        if (javaVersionElement.TryGetProperty("majorVersion", out var majorEl))
                        {
                            requiredJavaMajor = majorEl.GetInt32();
                            Log($"[Java] 版本要求: 需要 Java {requiredJavaMajor}（来自版本JSON）");
                        }
                        else
                        {
                            string versionStr = javaVersionElement.GetString();
                            if (!string.IsNullOrEmpty(versionStr))
                            {
                                var m = System.Text.RegularExpressions.Regex.Match(versionStr, @"(\d+)");
                                if (m.Success && int.TryParse(m.Groups[1].Value, out int v))
                                {
                                    requiredJavaMajor = v;
                                    Log($"[Java] 版本要求: 需要 Java {requiredJavaMajor}（来自版本JSON）");
                                }
                            }
                        }
                    }

                    if (requiredJavaMajor > 0 && detectedJavaMajor > 0 && detectedJavaMajor < requiredJavaMajor)
                    {
                        Log($"[警告] 当前 Java 版本 ({detectedJavaMajor}) 低于版本要求 ({requiredJavaMajor})，可能导致游戏启动失败！");
                        Log($"[警告] 建议安装 Java {requiredJavaMajor} 或更高版本");
                    }

                    Log("【步骤4.5】提取Natives库...");
                    await PostStatusAsync("正在提取Natives库...");
                    string nativesDir = await ExtractNativesAsync(versionId, versionInfo, repairService);
                    Log($"[Natives] natives目录: {nativesDir}");

                    Log("【步骤4.6】检查资源文件完整性...");
                    await PostStatusAsync("正在检查资源文件完整性...");
                    string versionJsonPath = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.json");
                    
                    // 暂时禁用资源文件检查，因为可能需要很长时间
                    // 资源文件通常在版本下载时就已经下载了
                    Log($"[资源] 跳过资源文件完整性检查（资源文件在版本下载时已下载）");
                    
                    /* 原始检查逻辑（暂时禁用）
                    var assetsResult = AssetsIntegrityChecker.CheckAssetsIntegrity(
                        _minecraftPath,
                        versionJsonPath,
                        checkHash: true,
                        ignoreFileCheck: false,
                        onLog: (msg) => Log($"[资源] {msg}"),
                        onProgress: (progress) => { }
                    );
                    
                    if (assetsResult.HasIssues)
                    {
                        Log($"[资源] 发现 {assetsResult.MissingAssetsCount} 个缺失文件, {assetsResult.HashMismatchCount} 个哈希不匹配文件");
                        await PostStatusAsync($"资源文件不完整，缺失 {assetsResult.TotalIssues} 个文件");
                        
                        // 获取缺失资源的下载列表
                        var missingAssets = AssetsIntegrityChecker.GetMissingAssetsDownloadList(
                            _minecraftPath,
                            versionJsonPath,
                            checkHash: true,
                            onLog: (msg) => Log($"[资源下载] {msg}")
                        );
                        
                        if (missingAssets.Count > 0)
                        {
                            Log($"[资源] 需要下载 {missingAssets.Count} 个资源文件");
                            await PostStatusAsync($"正在下载 {missingAssets.Count} 个缺失资源文件...");
                            
                            // 这里可以添加资源文件下载逻辑
                            // 目前先记录日志，资源文件下载可以后续实现
                        }
                    }
                    else
                    {
                        Log($"[资源] 资源文件完整性校验通过");
                    }
                    */

                    Log("【步骤5】构建启动参数...");
                    await PostStatusAsync("正在构建启动参数...");
                    var launchInfo = await BuildLaunchArgumentsAsync(versionId, versionInfo, username, minMemoryMb, maxMemoryMb, additionalArgs, offlineMode, resolution, nativesDir);
                    Log($"[参数] 启动参数构建完成");
                    Log($"[参数] JVM参数数: {launchInfo.JvmArgs.Count}");
                    Log($"[参数] 游戏参数数: {launchInfo.GameArgs.Count}");

                    string workingDir = _minecraftPath;
                    Log($"[参数] 工作目录: {workingDir}");

                    Log("【步骤7】准备启动游戏...");
                    await PostStatusAsync("正在启动游戏...");
                    Log($"[启动] ═══════════════════════════════════════════════");
                    Log($"[启动] Java路径: {validatedJavaPath}");
                    Log($"[启动] 工作目录: {workingDir}");
                    Log($"[启动] 主类: {launchInfo.MainClass}");
                    Log($"[启动] JVM参数数量: {launchInfo.JvmArgs.Count}");
                    Log($"[启动] 游戏参数数量: {launchInfo.GameArgs.Count}");
                    Log($"[启动] 命令长度: {launchInfo.FullCommand.Length} 字符");

                    if (launchInfo.FullCommand.Length > 8000)
                    {
                        Log($"[启动] 命令过长({launchInfo.FullCommand.Length}字符)，使用类路径文件");
                    }
                    else
                    {
                        Log($"[启动] 命令预览(前500字符): {launchInfo.FullCommand.Substring(0, Math.Min(500, launchInfo.FullCommand.Length))}...");
                    }
                    Log($"[启动] ═══════════════════════════════════════════════");

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = validatedJavaPath,
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    if (launchInfo.ArgumentList != null && launchInfo.ArgumentList.Count > 0)
                    {
                        startInfo.Arguments = BuildArgumentString(launchInfo.ArgumentList);
                        Log($"[启动] 使用参数列表模式传递 {launchInfo.ArgumentList.Count} 个参数（避免含空格参数被错误拆分）");
                    }
                    else
                    {
                        startInfo.Arguments = launchInfo.FullCommand;
                        Log($"[启动] 使用字符串模式传递命令（fallback）");
                    }

                    _gameProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                    _gameProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Log(e.Data);
                        }
                    };

                    _gameProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            string errorData = e.Data;
                            if (errorData.Contains("401") || errorData.Contains("Unauthorized"))
                            {
                                Log($"[认证] 离线模式：忽略认证错误");
                                return;
                            }
                            // ★ 过滤 "Skipping bad option" 重复刷屏警告，仅首次记录
                            if (errorData.Contains("Skipping bad option"))
                            {
                                if (!_hasLoggedBadOption)
                                {
                                    _hasLoggedBadOption = true;
                                    Log($"[警告] 检测到无效的 options.txt 配置项，已自动跳过");
                                    Log($"[提示] 可在「设置→游戏设置→重置游戏配置」一键修复");
                                }
                                return;
                            }
                            Log($"[ERROR] {errorData}");
                        }
                    };

                    _gameProcess.Exited += (sender, e) =>
                    {
                        if (_hasExited) return;
                        _hasExited = true;

                        try
                        {
                            int exitCode = _gameProcess.ExitCode;
                            Log("═══════════════════════════════════════════════════════════════");
                            if (exitCode == 0)
                            {
                                Log("游戏正常关闭");
                                PostCompleted(true);
                            }
                            else
                            {
                                result.ExitCode = exitCode;
                                result.ErrorMessage = $"游戏异常退出 (退出码: {exitCode})";
                                PostStatus($"游戏异常退出 (退出码: {exitCode})");
                                Log($"游戏进程异常退出，退出码: {exitCode}");
                                PostCompleted(false);
                            }
                            Log("═══════════════════════════════════════════════════════════════");
                        }
                        catch (Exception ex)
                        {
                            result.ErrorMessage = $"获取退出码失败: {ex.Message}";
                            Log($"获取退出码失败: {ex.Message}");
                            PostCompleted(false);
                        }
                    };

                    Log("[启动] 执行 Process.Start()...");
                    bool started = _gameProcess.Start();
                    Log($"[启动] Process.Start() 返回: {started}");

                    if (!started)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = "游戏进程启动失败，请检查Java路径是否正确";
                        LastLaunchInfo = result;
                        await PostStatusAsync("游戏进程启动失败");
                        Log("【错误】无法启动游戏进程，请检查Java路径是否正确");
                        PostCompleted(false);
                        return false;
                    }

                    _gameProcess.BeginOutputReadLine();
                    _gameProcess.BeginErrorReadLine();

                    Log($"[启动] 游戏进程已启动，PID: {_gameProcess.Id}");
                    await PostStatusAsync($"游戏进程已启动 (PID: {_gameProcess.Id})...");

                    await Task.Delay(3000);

                    if (_gameProcess.HasExited)
                    {
                        int exitCode = _gameProcess.ExitCode;
                        result.IsSuccess = false;
                        result.ExitCode = exitCode;
                        result.ErrorMessage = $"游戏进程在启动后立即退出 (退出码: {exitCode})";
                        LastLaunchInfo = result;
                        await PostStatusAsync($"游戏启动失败 (退出码: {exitCode})");
                        Log($"【错误】游戏进程在启动后立即退出");
                        Log($"【错误】退出码: {exitCode}");
                        Log($"【错误】可能原因: Java版本不兼容、内存不足、缺少依赖库");
                        PostCompleted(false);
                        return false;
                    }

                    Log("【启动成功】游戏进程运行中...");
                    await PostStatusAsync("游戏启动成功！");
                    Log("═══════════════════════════════════════════════════════════════");
                    Log("【启动流程完成】");
                    Log("═══════════════════════════════════════════════════════════════");

                    result.IsSuccess = true;
                    LastLaunchInfo = result;
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"【异常】启动失败: {ex.Message}");
                    Log($"【异常】堆栈: {ex.StackTrace}");
                    result.IsSuccess = false;
                    result.ErrorMessage = $"启动异常: {ex.Message}";
                    LastLaunchInfo = result;
                    await PostStatusAsync($"启动失败: {ex.Message}");
                    PostCompleted(false);
                    return false;
                }
            });
        }

        private async Task PostStatusAsync(string status)
        {
            if (_syncContext != null)
            {
                await Task.Run(() =>
                {
                    _syncContext.Post(_ =>
                    {
                        LaunchStatusChanged?.Invoke(status);
                    }, null);
                });
            }
            else
            {
                LaunchStatusChanged?.Invoke(status);
            }
        }

        private void PostStatus(string status)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(_ =>
                {
                    LaunchStatusChanged?.Invoke(status);
                }, null);
            }
            else
            {
                LaunchStatusChanged?.Invoke(status);
            }
        }

        private void PostCompleted(bool success)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(_ =>
                {
                    LaunchCompleted?.Invoke(success);
                    LaunchResultCompleted?.Invoke(LastLaunchInfo);
                }, null);
            }
            else
            {
                LaunchCompleted?.Invoke(success);
                LaunchResultCompleted?.Invoke(LastLaunchInfo);
            }
        }

        private async Task<string> ExtractNativesAsync(string versionId, JsonElement versionInfo, VersionRepairService repairService)
        {
            // 使用版本独立的natives目录，避免版本冲突
            string versionPath = Path.Combine(_minecraftPath, "versions", versionId);
            string nativesDir = Path.Combine(versionPath, $"{versionId}-natives");
            nativesDir = Path.GetFullPath(nativesDir);

            Log($"[Natives] 版本: {versionId}");
            Log($"[Natives] natives 目录: {nativesDir}");

            await _nativesLock.WaitAsync();
            try
            {
                if (!Directory.Exists(nativesDir))
                {
                    Directory.CreateDirectory(nativesDir);
                    Log($"[Natives] 创建 natives 目录: {nativesDir}");
                }

                // 解析需要提取的natives库
                var nativeLibraries = ParseNativeLibraries(versionInfo);
                
                if (!nativeLibraries.Any())
                {
                    Log($"[Natives] 没有需要处理的 Natives 库文件");
                    return nativesDir;
                }

                Log($"[Natives] 需要处理 {nativeLibraries.Count} 个 Natives 库文件");

                // 检查是否需要重新提取
                bool needsExtraction = !ValidateNativesIntegrity(nativesDir, nativeLibraries);

                if (needsExtraction)
                {
                    Log($"[Natives] natives 完整性校验失败，重新提取...");

                    // 清空目录
                    foreach (string file in Directory.GetFiles(nativesDir))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    foreach (string dir in Directory.GetDirectories(nativesDir))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                    Log($"[Natives] 已清空 natives 目录");

                    // 提取所有natives文件
                    var extractedFiles = new List<string>();
                    foreach (var lib in nativeLibraries)
                    {
                        try
                        {
                            var files = ExtractNativesFromLibrary(lib, nativesDir);
                            extractedFiles.AddRange(files);
                        }
                        catch (InvalidDataException ex)
                        {
                            Log($"[Natives] ✗ 无法打开 Natives 文件（{lib.LocalPath}），该文件可能已损坏: {ex.Message}");
                            // 尝试重新下载
                            if (repairService != null)
                            {
                                Log($"[Natives] 调用修复服务重新下载...");
                                await repairService.RepairVersionAsync(versionId);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"[Natives] ✗ 解压失败: {lib.Name} - {ex.Message}");
                        }
                    }

                    // 清理多余的文件
                    CleanupOrphanFiles(nativesDir, extractedFiles);

                    Log($"[Natives] 已提取 {extractedFiles.Count} 个 DLL 文件");
                }
                else
                {
                    Log($"[Natives] natives 完整性校验通过，无需重新提取");
                }

                // 输出最终状态
                string[] dllFiles = Directory.GetFiles(nativesDir, "*.dll");
                Log($"[Natives] 最终 natives 路径: {nativesDir} ({dllFiles.Length} 个dll文件)");
                foreach (string dll in dllFiles.Take(5))
                {
                    Log($"[Natives] DLL文件: {Path.GetFileName(dll)}");
                }
            }
            catch (Exception ex)
            {
                Log($"[Natives] 提取Natives时出错: {ex.Message}");
                Log($"[Natives] 异常详情: {ex.ToString()}");
            }
            finally
            {
                _nativesLock.Release();
            }

            return nativesDir;
        }

        private List<NativeLibraryInfo> ParseNativeLibraries(JsonElement versionInfo)
        {
            var nativeLibraries = new List<NativeLibraryInfo>();

            if (!versionInfo.TryGetProperty("libraries", out JsonElement libraries))
                return nativeLibraries;

            foreach (JsonElement lib in libraries.EnumerateArray())
            {
                try
                {
                    // 检查是否有natives规则
                    bool isNatives = false;
                    string classifier = null;

                    if (lib.TryGetProperty("downloads", out JsonElement downloads))
                    {
                        if (downloads.TryGetProperty("classifiers", out JsonElement classifiers))
                        {
                            // 检查 windows natives：1.21+ 使用 natives-windows-x64，之前版本使用 natives-windows-amd64
                            if (classifiers.TryGetProperty("natives-windows-amd64", out _))
                            {
                                isNatives = true;
                                classifier = "natives-windows-amd64";
                            }
                            else if (classifiers.TryGetProperty("natives-windows-x64", out _))
                            {
                                isNatives = true;
                                classifier = "natives-windows-x64";
                            }
                            else if (classifiers.TryGetProperty("natives-windows", out _))
                            {
                                isNatives = true;
                                classifier = "natives-windows";
                            }
                            else if (classifiers.TryGetProperty("natives-windows-64", out _))
                            {
                                isNatives = true;
                                classifier = "natives-windows-64";
                            }
                        }
                    }

                    // 检查rules
                    if (lib.TryGetProperty("rules", out JsonElement rules))
                    {
                        bool allowed = true;
                        foreach (JsonElement rule in rules.EnumerateArray())
                        {
                            if (rule.TryGetProperty("action", out JsonElement action))
                            {
                                string actionValue = action.GetString();
                                if (actionValue == "allow")
                                {
                                    if (rule.TryGetProperty("os", out JsonElement os))
                                    {
                                        if (os.TryGetProperty("name", out JsonElement osName))
                                        {
                                            if (osName.GetString() != "windows")
                                            {
                                                allowed = false;
                                            }
                                        }
                                    }
                                }
                                else if (actionValue == "disallow")
                                {
                                    if (rule.TryGetProperty("os", out JsonElement os))
                                    {
                                        if (os.TryGetProperty("name", out JsonElement osName))
                                        {
                                            if (osName.GetString() == "windows")
                                            {
                                                allowed = false;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (!allowed) continue;
                    }

                    if (isNatives && lib.TryGetProperty("name", out JsonElement nameElement))
                    {
                        string libName = nameElement.GetString();
                        string[] parts = libName.Split(':');
                        if (parts.Length >= 3)
                        {
                            string groupId = parts[0];
                            string artifactId = parts[1];
                            string version = parts[2];

                            // 构建本地路径
                            string relativePath = $"{groupId.Replace('.', '/')}/{artifactId}/{version}/{artifactId}-{version}-{classifier}.jar";
                            string localPath = Path.Combine(_minecraftPath, "libraries", relativePath);

                            nativeLibraries.Add(new NativeLibraryInfo
                            {
                                Name = libName,
                                LocalPath = localPath,
                                ArtifactId = artifactId,
                                Version = version,
                                Classifier = classifier
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Natives] 解析库信息失败: {ex.Message}");
                }
            }

            return nativeLibraries;
        }

        private List<string> ExtractNativesFromLibrary(NativeLibraryInfo lib, string targetFolder)
        {
            var extracted = new List<string>();

            if (!File.Exists(lib.LocalPath))
            {
                Log($"[Natives] ⚠️ 文件不存在: {lib.LocalPath}");
                return extracted;
            }

            using var zip = ZipFile.OpenRead(lib.LocalPath);
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                // 只提取根目录下的dll文件
                if (entry.FullName.Contains("/"))
                    continue;

                string filePath = Path.Combine(targetFolder, entry.Name);
                extracted.Add(filePath);

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists && fileInfo.Length == entry.Length)
                {
                    Log($"[Natives] 无需解压：{entry.Name}");
                    continue;
                }

                if (fileInfo.Exists)
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Log($"[Natives] 删除原dll访问被拒绝，跳过解压：{entry.Name}");
                        Log($"[Natives] 错误信息：{ex.Message}");
                        break;
                    }
                }

                entry.ExtractToFile(filePath, true);
                Log($"[Natives] 已解压：{entry.Name}");
            }

            return extracted;
        }

        private void CleanupOrphanFiles(string nativesFolder, List<string> validFiles)
        {
            foreach (var file in Directory.GetFiles(nativesFolder, "*.dll"))
            {
                if (validFiles.Contains(file))
                    continue;

                try
                {
                    Log($"[Natives] 删除多余文件：{Path.GetFileName(file)}");
                    File.Delete(file);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log($"[Natives] 删除多余文件访问被拒绝，跳过：{ex.Message}");
                    return;
                }
            }
        }

        private bool ValidateNativesIntegrity(string nativesDir, List<NativeLibraryInfo> nativeLibraries)
        {
            if (!Directory.Exists(nativesDir))
                return false;

            var dllFiles = Directory.GetFiles(nativesDir, "*.dll");
            if (dllFiles.Length == 0)
            {
                Log($"[Natives] natives 目录下没有 .dll 文件");
                return false;
            }

            Log($"[Natives] natives 目录下有 {dllFiles.Length} 个 .dll 文件");

            // 检查所有dll文件是否有效
            foreach (string dllFile in dllFiles)
            {
                FileInfo fi = new FileInfo(dllFile);
                if (fi.Length == 0)
                {
                    Log($"[Natives] 无效的 dll 文件（空文件）: {dllFile}");
                    return false;
                }
            }

            // 检查是否所有需要的库都已提取
            foreach (var lib in nativeLibraries)
            {
                if (!File.Exists(lib.LocalPath))
                {
                    Log($"[Natives] 缺少库文件: {lib.Name}");
                    return false;
                }
            }

            return true;
        }

        private string GetLwjglVersion(JsonElement versionInfo)
        {
            // 从 libraries 中查找 lwjgl 版本
            if (versionInfo.TryGetProperty("libraries", out JsonElement libraries))
            {
                foreach (JsonElement lib in libraries.EnumerateArray())
                {
                    if (lib.TryGetProperty("name", out JsonElement name))
                    {
                        string libName = name.GetString();
                        if (libName != null && libName.StartsWith("org.lwjgl:lwjgl:"))
                        {
                            // 提取版本号，格式: org.lwjgl:lwjgl:3.3.1
                            string[] parts = libName.Split(':');
                            if (parts.Length >= 3)
                            {
                                Log($"[Natives] 从库信息中检测到 LWJGL 版本: {parts[2]}");
                                return parts[2];
                            }
                        }
                    }
                }
            }

            // 默认返回 3.3.1 (适用于 Minecraft 1.19+)
            Log($"[Natives] 未能检测到 LWJGL 版本，使用默认版本 3.3.1");
            return "3.3.1";
        }

        private class NativeLibraryInfo
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string LibPath { get; set; }
            public string Url { get; set; }
            public string LocalPath { get; set; }
            public string ArtifactId { get; set; }
            public string Version { get; set; }
            public string Classifier { get; set; }
        }

        private class NativeDownloadResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string LibPath { get; set; }
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
        }

        private async Task<NativeDownloadResult> DownloadNativeAsync(NativeLibraryInfo info)
        {
            Log($"[Natives] 线程开始下载: {info.Name}");
            
            try
            {
                if (string.IsNullOrEmpty(info.Url))
                {
                    return new NativeDownloadResult
                    {
                        Name = info.Name,
                        Path = info.Path,
                        LibPath = info.LibPath,
                        Success = false,
                        ErrorMessage = "没有下载URL"
                    };
                }

                string dirPath = Path.GetDirectoryName(info.LibPath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                using (var client = new System.Net.WebClient())
                {
                    await client.DownloadFileTaskAsync(info.Url, info.LibPath);
                }

                if (File.Exists(info.LibPath))
                {
                    Log($"[Natives] 线程完成下载: {info.Name}");
                    return new NativeDownloadResult
                    {
                        Name = info.Name,
                        Path = info.Path,
                        LibPath = info.LibPath,
                        Success = true
                    };
                }
                else
                {
                    return new NativeDownloadResult
                    {
                        Name = info.Name,
                        Path = info.Path,
                        LibPath = info.LibPath,
                        Success = false,
                        ErrorMessage = "下载后文件不存在"
                    };
                }
            }
            catch (Exception ex)
            {
                return new NativeDownloadResult
                {
                    Name = info.Name,
                    Path = info.Path,
                    LibPath = info.LibPath,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private bool ValidateNativesIntegrity(string nativesDir, JsonElement versionInfo)
        {
            if (!Directory.Exists(nativesDir))
                return false;

            string[] dllFiles = Directory.GetFiles(nativesDir, "*.dll");
            if (dllFiles.Length == 0)
            {
                Log($"[Natives] natives 目录下没有 .dll 文件");
                return false;
            }

            Log($"[Natives] natives 目录下有 {dllFiles.Length} 个 .dll 文件");

            foreach (string dllFile in dllFiles)
            {
                FileInfo fi = new FileInfo(dllFile);
                if (fi.Length == 0)
                {
                    Log($"[Natives] 无效的 dll 文件（空文件）: {dllFile}");
                    return false;
                }
            }

            // 检查版本标记文件，验证natives文件是否为当前版本所需
            string versionMarkerFile = Path.Combine(nativesDir, ".version_marker");
            string currentVersionId = GetVersionIdFromVersionInfo(versionInfo);
            
            if (File.Exists(versionMarkerFile))
            {
                string markerVersion = File.ReadAllText(versionMarkerFile).Trim();
                if (markerVersion != currentVersionId)
                {
                    Log($"[Natives] 版本不匹配: 当前={currentVersionId}, 缓存={markerVersion}，需要重新提取");
                    return false;
                }
                Log($"[Natives] 版本匹配: {currentVersionId}，跳过提取");
            }
            else
            {
                Log($"[Natives] 缺少版本标记文件，需要重新提取");
                return false;
            }

            return true;
        }

        private string GetVersionIdFromVersionInfo(JsonElement versionInfo)
        {
            if (versionInfo.TryGetProperty("id", out JsonElement id))
            {
                return id.GetString() ?? "unknown";
            }
            return "unknown";
        }

        private bool ValidateFileChecksum(string filePath, string expectedChecksum)
        {
            if (string.IsNullOrEmpty(expectedChecksum))
                return true;

            try
            {
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        byte[] hash = sha1.ComputeHash(stream);
                        string actualChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        return actualChecksum.Equals(expectedChecksum.ToLowerInvariant());
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[校验] 校验文件哈希失败 {filePath}: {ex.Message}");
                return false;
            }
        }

        private void CleanupTempClasspathFiles()
        {
            try
            {
                string tempPath = Path.GetTempPath();
                var tempFiles = Directory.GetFiles(tempPath, "mc_cp_*.txt");

                foreach (string file in tempFiles)
                {
                    try
                    {
                        File.Delete(file);
                        Log($"[清理] 已删除临时类路径文件: {Path.GetFileName(file)}");
                    }
                    catch { }
                }

                if (tempFiles.Length > 0)
                {
                    Log($"[清理] 共清理 {tempFiles.Length} 个临时类路径文件");
                }
            }
            catch (Exception ex)
            {
                Log($"[清理] 清理临时文件失败: {ex.Message}");
            }
        }

        private void ExtractZipToDirectory(string zipPath, string destDir)
        {
            try
            {
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, destDir);

                foreach (string file in Directory.GetFiles(destDir))
                {
                    if (!file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { }
                    }
                }

                Log($"已提取 natives: {Path.GetFileName(zipPath)} -> {destDir}");
            }
            catch (Exception ex)
            {
                Log($"解压 {Path.GetFileName(zipPath)} 失败: {ex.Message}");
            }
        }

        private async Task<bool> VerifyVersionAsync(string versionId)
        {
            string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
            string jarFile = Path.Combine(versionDir, $"{versionId}.jar");
            string jsonFile = Path.Combine(versionDir, $"{versionId}.json");

            if (!Directory.Exists(versionDir))
            {
                Log($"版本目录不存在: {versionDir}");
                return false;
            }

            if (!File.Exists(jsonFile))
            {
                Log($"JSON文件不存在: {jsonFile}");
                return false;
            }

            // 检查是否有 inheritsFrom（Fabric/Forge/NeoForge 等加载器版本）
            // 这类版本没有自己的独立 jar 文件，它们的 jar 来自继承的父版本
            string inheritsFrom = null;
            try
            {
                string jsonContent = File.ReadAllText(jsonFile);
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    if (doc.RootElement.TryGetProperty("inheritsFrom", out var inhProp))
                    {
                        inheritsFrom = inhProp.GetString();
                    }
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(inheritsFrom))
            {
                // 这是一个继承版本（如 Fabric），通过 FindMainJarPath 沿继承链查找主 jar（支持多级继承）
                Log($"[版本] {versionId} 是继承版本，继承自: {inheritsFrom}");
                string parentJar = FindMainJarPath(versionId, "[版本]");
                string parentJson = Path.Combine(_minecraftPath, "versions", inheritsFrom, $"{inheritsFrom}.json");
                if (parentJar == null)
                {
                    Log($"[版本] 错误: 无法在继承链中找到主 jar 文件。请确保原版 {inheritsFrom} 版本已正确安装（包含 jar 和 json）。");
                    return false;
                }
                if (!File.Exists(parentJson))
                {
                    Log($"[版本] 警告: 父版本 {inheritsFrom} 的 json 文件不存在: {parentJson}");
                }

                // 检查加载器库文件是否存在（至少检查前 3 个）
                int missingLibs = 0;
                try
                {
                    string jsonContent = File.ReadAllText(jsonFile);
                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                    {
                        if (doc.RootElement.TryGetProperty("libraries", out var libraries))
                        {
                            int checkedCount = 0;
                            foreach (var lib in libraries.EnumerateArray())
                            {
                                if (checkedCount >= 3) break;
                                string libPath = GetLibraryPath(lib);
                                if (!string.IsNullOrEmpty(libPath))
                                {
                                    string fullPath = Path.Combine(_minecraftPath, "libraries", libPath);
                                    if (!File.Exists(fullPath))
                                    {
                                        Log($"[版本] 警告: 加载器库文件不存在: {fullPath}");
                                        missingLibs++;
                                    }
                                }
                                checkedCount++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[版本] 检查库文件时出错: {ex.Message}");
                }

                if (missingLibs > 0)
                {
                    Log($"[版本] 错误: {versionId} 缺少 {missingLibs} 个加载器库文件，请重新安装 Fabric/Forge/NeoForge");
                    return false;
                }

                Log($"版本 {versionId} 继承校验通过（使用主 JAR: {parentJar}）");
                return true;
            }

            // 普通版本，需要自己的 jar
            if (!File.Exists(jarFile))
            {
                Log($"Jar文件不存在: {jarFile}");
                return false;
            }

            Log($"版本 {versionId} 校验通过");
            return true;
        }

        /// <summary>
        /// 沿 inheritsFrom 链查找主 JAR 文件路径。支持 Fabric/Forge/NeoForge 等继承版本。
        /// 返回找到的 JAR 路径，如果都找不到则返回 null。
        /// </summary>
        private string FindMainJarPath(string versionId, string logPrefix = "[CP]")
        {
            // 先找当前版本的 jar
            string jarPath = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.jar");
            if (File.Exists(jarPath))
            {
                return jarPath;
            }

            // 当前版本没有 jar，检查 inheritsFrom，递归查找父版本的 jar
            string currentCheck = versionId;
            int checkDepth = 0;
            while (checkDepth < 10)
            {
                string jsonPath = Path.Combine(_minecraftPath, "versions", currentCheck, $"{currentCheck}.json");
                if (!File.Exists(jsonPath)) break;

                try
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    using (var doc = JsonDocument.Parse(jsonContent))
                    {
                        if (!doc.RootElement.TryGetProperty("inheritsFrom", out var inh))
                        {
                            break;
                        }
                        string parent = inh.GetString();
                        if (string.IsNullOrEmpty(parent)) break;

                        string parentJar = Path.Combine(_minecraftPath, "versions", parent, $"{parent}.jar");
                        if (File.Exists(parentJar))
                        {
                            Log($"{logPrefix} 使用继承版本的主JAR: {parent}");
                            return parentJar;
                        }
                        currentCheck = parent;
                    }
                }
                catch
                {
                    break;
                }
                checkDepth++;
            }

            return null;
        }

        private JsonElement LoadVersionInfo(string versionId)
        {
            try
            {
                // 收集继承链上的所有版本 JSON（从当前版本到最顶层的父版本）
                var chain = new List<JsonDocument>();
                string currentId = versionId;
                var visited = new HashSet<string>();
                int depth = 0;
                const int maxDepth = 10;

                while (!string.IsNullOrEmpty(currentId) && depth < maxDepth)
                {
                    if (!visited.Add(currentId))
                    {
                        Log($"[版本] 警告: 检测到循环继承: {currentId}，已停止");
                        break;
                    }

                    string jsonPath = Path.Combine(_minecraftPath, "versions", currentId, $"{currentId}.json");
                    if (!File.Exists(jsonPath))
                    {
                        Log($"[版本] 警告: 继承链中的版本 JSON 不存在: {jsonPath}");
                        break;
                    }

                    string jsonContent = File.ReadAllText(jsonPath);
                    var doc = JsonDocument.Parse(jsonContent);
                    chain.Add(doc);

                    // 检查是否有 inheritsFrom
                    if (doc.RootElement.TryGetProperty("inheritsFrom", out var inhProp))
                    {
                        string next = inhProp.GetString();
                        if (!string.IsNullOrEmpty(next))
                        {
                            Log($"[版本] {currentId} 继承自: {next}");
                            currentId = next;
                            depth++;
                            continue;
                        }
                    }
                    break;
                }

                if (chain.Count == 1)
                {
                    // 没有继承关系，直接返回原始 JSON
                    var result = chain[0].RootElement.Clone();
                    chain[0].Dispose();
                    Log($"[版本] 版本 JSON 加载成功（无继承）");
                    return result;
                }

                // 合并继承链：libraries 合并（子版本覆盖父版本同名校），其他属性子版本覆盖父版本
                // chain[0] 是当前版本，chain[1] 是父版本，chain[2] 是祖父版本...
                Log($"[版本] 合并继承链，共 {chain.Count} 个版本");

                using (var mergedStream = new MemoryStream())
                using (var writer = new Utf8JsonWriter(mergedStream))
                {
                    writer.WriteStartObject();

                    var currentDoc = chain[0];

                    // 收集所有库文件（从最底层父版本到当前版本，后处理的同名库覆盖先处理的）
                    var allLibraries = new List<JsonElement>();
                    var libNameSet = new HashSet<string>(StringComparer.Ordinal);

                    // 从最顶层父版本开始，最后是当前版本，这样当前版本的同名库会覆盖父版本
                    for (int i = chain.Count - 1; i >= 0; i--)
                    {
                        var doc = chain[i];
                        if (doc.RootElement.TryGetProperty("libraries", out var libs))
                        {
                            foreach (var lib in libs.EnumerateArray())
                            {
                                string libName = null;
                                if (lib.TryGetProperty("name", out var nameProp))
                                {
                                    libName = nameProp.GetString();
                                }

                                if (!string.IsNullOrEmpty(libName))
                                {
                                    if (libNameSet.Contains(libName))
                                    {
                                        // 移除已有的旧条目
                                        allLibraries.RemoveAll(l =>
                                        {
                                            if (l.TryGetProperty("name", out var ln))
                                            {
                                                return ln.GetString() == libName;
                                            }
                                            return false;
                                        });
                                    }
                                    libNameSet.Add(libName);
                                }
                                allLibraries.Add(lib.Clone());
                            }
                        }
                    }

                    // mainClass - 当前版本优先，父版本补充
                    string finalMainClass = null;
                    for (int i = 0; i < chain.Count; i++)
                    {
                        if (chain[i].RootElement.TryGetProperty("mainClass", out var mc))
                        {
                            finalMainClass = mc.GetString();
                            if (!string.IsNullOrEmpty(finalMainClass)) break;
                        }
                    }

                    // inheritsFrom - 保留当前版本的
                    string finalInheritsFrom = null;
                    if (currentDoc.RootElement.TryGetProperty("inheritsFrom", out var inh))
                    {
                        finalInheritsFrom = inh.GetString();
                    }

                    // assetIndex - 当前版本优先
                    JsonElement finalAssetIndex = default;
                    for (int i = 0; i < chain.Count; i++)
                    {
                        if (chain[i].RootElement.TryGetProperty("assetIndex", out var ai))
                        {
                            finalAssetIndex = ai.Clone();
                            break;
                        }
                    }

                    // arguments - 合并所有版本的 jvm 和 game（父版本在前，子版本在后）
                    var jvmArgsList = new List<JsonElement>();
                    var gameArgsList = new List<JsonElement>();
                    for (int i = chain.Count - 1; i >= 0; i--)
                    {
                        if (chain[i].RootElement.TryGetProperty("arguments", out var args))
                        {
                            if (args.TryGetProperty("jvm", out var jvmArr))
                            {
                                foreach (var a in jvmArr.EnumerateArray())
                                {
                                    jvmArgsList.Add(a.Clone());
                                }
                            }
                            if (args.TryGetProperty("game", out var gameArr))
                            {
                                foreach (var a in gameArr.EnumerateArray())
                                {
                                    gameArgsList.Add(a.Clone());
                                }
                            }
                        }
                    }

                    // downloads - 当前版本优先
                    JsonElement finalDownloads = default;
                    for (int i = 0; i < chain.Count; i++)
                    {
                        if (chain[i].RootElement.TryGetProperty("downloads", out var dl))
                        {
                            finalDownloads = dl.Clone();
                            break;
                        }
                    }

                    // id - 使用当前版本的 id
                    if (currentDoc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        writer.WriteString("id", idProp.GetString());
                    }
                    else
                    {
                        writer.WriteString("id", versionId);
                    }

                    // inheritsFrom
                    if (!string.IsNullOrEmpty(finalInheritsFrom))
                    {
                        writer.WriteString("inheritsFrom", finalInheritsFrom);
                    }

                    // 其他简单属性 - 从当前版本优先，父版本补充
                    string[] simpleProps = { "type", "releaseTime", "time", "minimumLauncherVersion", "complianceLevel" };
                    foreach (var prop in simpleProps)
                    {
                        if (currentDoc.RootElement.TryGetProperty(prop, out var val))
                        {
                            writer.WritePropertyName(prop);
                            val.WriteTo(writer);
                        }
                        else
                        {
                            for (int i = 1; i < chain.Count; i++)
                            {
                                if (chain[i].RootElement.TryGetProperty(prop, out var pval))
                                {
                                    writer.WritePropertyName(prop);
                                    pval.WriteTo(writer);
                                    break;
                                }
                            }
                        }
                    }

                    // mainClass
                    if (!string.IsNullOrEmpty(finalMainClass))
                    {
                        writer.WriteString("mainClass", finalMainClass);
                    }

                    // assetIndex
                    if (finalAssetIndex.ValueKind != JsonValueKind.Undefined)
                    {
                        writer.WritePropertyName("assetIndex");
                        finalAssetIndex.WriteTo(writer);
                    }

                    // downloads
                    if (finalDownloads.ValueKind != JsonValueKind.Undefined)
                    {
                        writer.WritePropertyName("downloads");
                        finalDownloads.WriteTo(writer);
                    }

                    // libraries - 合并后的完整库列表
                    writer.WritePropertyName("libraries");
                    writer.WriteStartArray();
                    foreach (var lib in allLibraries)
                    {
                        lib.WriteTo(writer);
                    }
                    writer.WriteEndArray();
                    Log($"[版本] 合并后共 {allLibraries.Count} 个库文件");

                    // arguments - 合并后的参数
                    if (jvmArgsList.Count > 0 || gameArgsList.Count > 0)
                    {
                        writer.WritePropertyName("arguments");
                        writer.WriteStartObject();

                        if (jvmArgsList.Count > 0)
                        {
                            writer.WritePropertyName("jvm");
                            writer.WriteStartArray();
                            foreach (var a in jvmArgsList)
                            {
                                a.WriteTo(writer);
                            }
                            writer.WriteEndArray();
                        }

                        if (gameArgsList.Count > 0)
                        {
                            writer.WritePropertyName("game");
                            writer.WriteStartArray();
                            foreach (var a in gameArgsList)
                            {
                                a.WriteTo(writer);
                            }
                            writer.WriteEndArray();
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                    writer.Flush();

                    mergedStream.Position = 0;
                    var mergedDoc = JsonDocument.Parse(mergedStream);

                    // 释放原始文档
                    foreach (var doc in chain)
                    {
                        doc.Dispose();
                    }

                    Log($"[版本] 版本 JSON 加载并合并成功（继承链共 {chain.Count} 层）");
                    return mergedDoc.RootElement.Clone();
                }
            }
            catch (Exception ex)
            {
                Log($"读取版本信息失败: {ex.Message}");
                return default;
            }
        }

        private async Task<LaunchInfo> BuildLaunchArgumentsAsync(string versionId, JsonElement versionInfo, string username,
            int minMemoryMb, int maxMemoryMb, string additionalArgs, bool offlineMode, string resolution = "1920x1080", string nativesDir = null)
        {
            LaunchInfo launchInfo = new LaunchInfo();
            
            // 如果没有传入nativesDir，则使用版本独立的natives目录
            if (string.IsNullOrEmpty(nativesDir))
            {
                string versionPath = Path.Combine(_minecraftPath, "versions", versionId);
                nativesDir = Path.Combine(versionPath, $"{versionId}-natives");
                nativesDir = Path.GetFullPath(nativesDir);
            }
            
            string mainClass = GetMainClass(versionInfo);
            launchInfo.MainClass = mainClass;

            BuildJvmArgs(launchInfo, minMemoryMb, maxMemoryMb, nativesDir, versionInfo, versionId);
            
            string classpath = BuildClasspath(versionId, versionInfo);
            if (string.IsNullOrEmpty(classpath))
            {
                throw new InvalidOperationException("类路径为空，无法启动游戏");
            }
            launchInfo.Classpath = classpath;
            Log($"[CP] 类路径已构建，长度: {classpath.Length}");

            BuildGameArgs(launchInfo, versionId, versionInfo, username, offlineMode, additionalArgs, resolution);

            launchInfo.FullCommand = await BuildFullCommandAsync(launchInfo.JvmArgs, classpath, mainClass, launchInfo.GameArgs, launchInfo);

            await ValidateLaunchInfoAsync(launchInfo);

            string shortCommand = launchInfo.FullCommand;
            if (shortCommand.Length > 500)
            {
                shortCommand = shortCommand.Substring(0, 500) + "...";
            }
            Log($"[启动命令] {shortCommand}");

            return launchInfo;
        }

        private string BuildArgumentString(List<string> args)
        {
            if (args == null || args.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < args.Count; i++)
            {
                if (i > 0)
                    sb.Append(' ');

                string arg = args[i] ?? string.Empty;

                if (arg.Length == 0 || arg.IndexOfAny(new char[] { ' ', '\t', '"', '\n', '\r' }) >= 0)
                {
                    sb.Append('"');
                    for (int j = 0; j < arg.Length; j++)
                    {
                        int numBackslashes = 0;
                        while (j < arg.Length && arg[j] == '\\')
                        {
                            numBackslashes++;
                            j++;
                        }
                        if (j < arg.Length && arg[j] == '"')
                        {
                            sb.Append('\\', numBackslashes * 2 + 1);
                            sb.Append('"');
                        }
                        else
                        {
                            sb.Append('\\', numBackslashes);
                            if (j < arg.Length)
                                sb.Append(arg[j]);
                        }
                    }
                    sb.Append('"');
                }
                else
                {
                    sb.Append(arg);
                }
            }
            return sb.ToString();
        }
        private async Task<string> BuildFullCommandAsync(List<string> jvmArgs, string classpath, string mainClass, List<string> gameArgs, LaunchInfo launchInfo)
        {
            Log($"[CMD] ============== 开始构建启动命令 ==============");
            Log($"[CMD] 类路径长度: {classpath.Length}");
            Log($"[CMD] JVM参数数量: {jvmArgs.Count}");
            Log($"[CMD] 游戏参数数量: {gameArgs.Count}");
            Log($"[CMD] JVM参数列表: {string.Join(" | ", jvmArgs)}");
            
            if (string.IsNullOrEmpty(classpath))
            {
                Log($"[CMD] 错误: 类路径为空！");
                throw new InvalidOperationException("类路径为空，无法构建启动命令");
            }
            
            string classpathArg;
            string cpFilePath = null;
            
            if (classpath.Length > 4096)
            {
                await _cpFileLock.WaitAsync();
                string cpFileName = $"mc_cp_{Guid.NewGuid():N}.txt";
                cpFilePath = Path.Combine(Path.GetTempPath(), cpFileName);
                try
                {
                    Log($"[CP] 准备写入类路径文件: {cpFilePath}");
                    
                    string classpathContent = classpath.Replace("/", "\\");
                    
                    using (var writer = new StreamWriter(cpFilePath, false, new UTF8Encoding(false)))
                    {
                        writer.Write(classpathContent);
                    }
                    Log($"[CP] 类路径过长({classpath.Length}字符)，已写入类路径文件");
                    Log($"[CP] 使用UTF-8无BOM编码写入，确保Java正确读取");
                    
                    if (File.Exists(cpFilePath))
                    {
                        long fileSize = new FileInfo(cpFilePath).Length;
                        Log($"[CP] 类路径文件大小: {fileSize} 字节");
                        
                        string fileContent = File.ReadAllText(cpFilePath);
                        if (!string.IsNullOrEmpty(fileContent))
                        {
                            Log($"[CP] 类路径文件内容长度: {fileContent.Length} 字符");
                            Log($"[CP] 类路径文件内容预览: {fileContent.Substring(0, Math.Min(150, fileContent.Length))}...");
                            
                            string[] paths = fileContent.Split(';');
                            Log($"[CP] 类路径包含 {paths.Length} 个条目");
                            Log($"[CP] 第一个条目(主JAR): {paths.FirstOrDefault()}");
                        }
                        else
                        {
                            Log($"[CP] 警告: 类路径文件内容为空！");
                        }
                    }
                    else
                    {
                        Log($"[CP] 错误: 类路径文件创建失败！");
                        throw new IOException("无法创建类路径文件");
                    }
                }
                catch (Exception ex)
                {
                    Log($"[CP] 写入类路径文件失败: {ex.Message}");
                    throw;
                }
                finally
                {
                    _cpFileLock.Release();
                }
                
                classpathArg = $"@{cpFilePath}";
                Log($"[CP] 最终classpathArg: {classpathArg}");
            }
            else
            {
                classpathArg = classpath.Replace("/", "\\");
                Log($"[CP] 类路径直接传递，长度: {classpath.Length}");
            }
            
            Log($"[CMD] 准备构建命令参数...");
            Log($"[CMD] 添加 {jvmArgs.Count} 个JVM参数");
            Log($"[CMD] 添加 -cp 参数");
            Log($"[CMD] 添加类路径: {classpathArg}");
            Log($"[CMD] 添加主类: {mainClass}");
            Log($"[CMD] 添加 {gameArgs.Count} 个游戏参数");
            
            List<string> finalArgs = new List<string>();
            finalArgs.AddRange(jvmArgs);
            finalArgs.Add("-cp");
            finalArgs.Add(classpathArg);
            finalArgs.Add(mainClass);
            finalArgs.AddRange(gameArgs);
            
            launchInfo.ArgumentList = finalArgs;
            
            Log($"[CMD] 参数列表总数量: {finalArgs.Count}");
            Log($"[CMD] 参数列表内容: {string.Join(" | ", finalArgs)}");
            
            string fullCommand = string.Join(" ", finalArgs);
            Log($"[CMD] 完整命令长度: {fullCommand.Length}");
            
            int cpIndex = fullCommand.IndexOf("-cp ");
            if (cpIndex >= 0)
            {
                int afterCp = cpIndex + 4;
                int nextSpace = fullCommand.IndexOf(" ", afterCp);
                if (nextSpace < 0) nextSpace = fullCommand.Length;
                string cpValue = fullCommand.Substring(afterCp, nextSpace - afterCp);
                Log($"[CMD] -cp 参数值: '{cpValue}'");
                Log($"[CMD] -cp 参数值长度: {cpValue.Length}");
                
                if (cpValue.StartsWith("@"))
                {
                    string filePath = cpValue.Substring(1);
                    Log($"[CMD] 验证类路径文件是否存在: {filePath}");
                    if (File.Exists(filePath))
                    {
                        Log($"[CMD] ✓ 类路径文件存在");
                        long size = new FileInfo(filePath).Length;
                        Log($"[CMD] ✓ 类路径文件大小: {size} 字节");
                    }
                    else
                    {
                        Log($"[CMD] ✗ 类路径文件不存在！");
                    }
                }
                else if (string.IsNullOrEmpty(cpValue))
                {
                    Log($"[CMD] ✗ -cp 参数值为空！");
                }
            }
            else
            {
                Log($"[CMD] ✗ 未找到 -cp 参数！");
            }
            
            Log($"[CMD] ============== 构建启动命令完成 ==============");
            
            return fullCommand;
        }

        private void BuildJvmArgs(LaunchInfo launchInfo, int minMemoryMb, int maxMemoryMb, string nativesDir, JsonElement versionInfo, string versionId)
        {
            HashSet<string> addedArgs = new HashSet<string>();

            void AddArg(string arg)
            {
                if (addedArgs.Add(arg))
                {
                    launchInfo.JvmArgs.Add(arg);
                }
            }

            AddArg($"-Xms{minMemoryMb}M");
            AddArg($"-Xmx{maxMemoryMb}M");
            
            AddArg("-Dfile.encoding=UTF-8");
            AddArg("-Dstdout.encoding=UTF-8");
            AddArg("-Dstderr.encoding=UTF-8");
            
            // 中文语言支持：确保 Java 系统语言环境为中文
            AddArg("-Duser.language=zh");
            AddArg("-Duser.country=CN");

            AddArg("-XX:+UnlockExperimentalVMOptions");
            AddArg("-XX:+UseG1GC");
            AddArg("-XX:G1NewSizePercent=20");
            AddArg("-XX:G1ReservePercent=20");
            AddArg("-XX:MaxGCPauseMillis=50");
            AddArg("-XX:G1HeapRegionSize=32M");
            AddArg("-XX:+DisableExplicitGC");
            AddArg("-XX:+AlwaysPreTouch");
            AddArg("-XX:+ParallelRefProcEnabled");

            // 主 JAR 路径：优先使用当前版本的 jar；如果不存在且有 inheritsFrom，使用父版本的 jar
            string jarPath = FindMainJarPath(versionId, "[JVM]");
            AddArg($"-Dminecraft.client.jar={jarPath}");
            AddArg("-Dminecraft.launcher.brand=MusicalNoteLauncher");
            AddArg("-Dminecraft.launcher.version=1.0");

            if (Directory.Exists(nativesDir))
            {
                string normalizedNativesDir = nativesDir.Replace(Path.DirectorySeparatorChar, '/');
                AddArg($"-Djava.library.path={normalizedNativesDir}");
            }

            if (versionInfo.TryGetProperty("arguments", out JsonElement arguments) &&
                arguments.TryGetProperty("jvm", out JsonElement jvmArgs))
            {
                foreach (JsonElement arg in jvmArgs.EnumerateArray())
                {
                    if (arg.ValueKind == JsonValueKind.String)
                    {
                        string argStr = arg.GetString();
                        
                        if (argStr.Contains("${natives_directory}"))
                        {
                            argStr = argStr.Replace("${natives_directory}", nativesDir);
                        }
                        if (argStr.Contains("${launcher_name}"))
                        {
                            argStr = argStr.Replace("${launcher_name}", "MusicalNoteLauncher");
                        }
                        if (argStr.Contains("${launcher_version}"))
                        {
                            argStr = argStr.Replace("${launcher_version}", "1.0");
                        }
                        if (argStr.Contains("${classpath}"))
                        {
                            continue;
                        }
                        
                        if (argStr.Trim() == "-cp" || argStr.Trim() == "-classpath")
                        {
                            Log($"[JVM] 跳过JSON中的 -cp 参数（将在代码中单独设置）");
                            continue;
                        }
                        
                        if (!argStr.StartsWith("-Djava.library.path") && 
                            !argStr.StartsWith("-Dminecraft.client.jar") &&
                            !argStr.StartsWith("-Dminecraft.launcher"))
                        {
                            AddArg(argStr);
                        }
                    }
                    else if (arg.ValueKind == JsonValueKind.Object)
                    {
                        // 处理带 rules 的对象参数（1.21+ 中常见）
                        if (!EvaluateArgumentRules(arg))
                            continue;

                        if (arg.TryGetProperty("value", out JsonElement value))
                        {
                            // value 可能是字符串或数组
                            if (value.ValueKind == JsonValueKind.String)
                            {
                                string argStr = value.GetString();
                                if (argStr.Contains("${natives_directory}"))
                                    argStr = argStr.Replace("${natives_directory}", nativesDir);
                                if (argStr.Contains("${launcher_name}"))
                                    argStr = argStr.Replace("${launcher_name}", "MusicalNoteLauncher");
                                if (argStr.Contains("${launcher_version}"))
                                    argStr = argStr.Replace("${launcher_version}", "1.0");
                                if (argStr.Contains("${classpath}"))
                                    continue;
                                if (!string.IsNullOrWhiteSpace(argStr) && !argStr.StartsWith("-Djava.library.path") &&
                                    !argStr.StartsWith("-Dminecraft.client.jar") &&
                                    !argStr.StartsWith("-Dminecraft.launcher"))
                                {
                                    AddArg(argStr);
                                }
                            }
                            else if (value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var v in value.EnumerateArray())
                                {
                                    if (v.ValueKind == JsonValueKind.String)
                                    {
                                        string argStr = v.GetString();
                                        if (argStr.Contains("${natives_directory}"))
                                            argStr = argStr.Replace("${natives_directory}", nativesDir);
                                        if (argStr.Contains("${launcher_name}"))
                                            argStr = argStr.Replace("${launcher_name}", "MusicalNoteLauncher");
                                        if (argStr.Contains("${launcher_version}"))
                                            argStr = argStr.Replace("${launcher_version}", "1.0");
                                        if (argStr.Contains("${classpath}"))
                                            continue;
                                        if (!string.IsNullOrWhiteSpace(argStr) && !argStr.StartsWith("-Djava.library.path") &&
                                            !argStr.StartsWith("-Dminecraft.client.jar") &&
                                            !argStr.StartsWith("-Dminecraft.launcher"))
                                        {
                                            AddArg(argStr);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // 评估一个带 rules 的参数对象，返回是否应被包含
        private bool EvaluateArgumentRules(JsonElement arg)
        {
            if (!arg.TryGetProperty("rules", out JsonElement rules))
                return true; // 没有 rules 默认包含

            // 默认不包含，只有当有 allow 规则且满足时才包含
            bool allowed = false;

            foreach (JsonElement rule in rules.EnumerateArray())
            {
                string action = null;
                if (rule.TryGetProperty("action", out JsonElement actionEl))
                    action = actionEl.GetString();

                // 检查 os 规则
                bool osMatch = true;
                if (rule.TryGetProperty("os", out JsonElement os))
                {
                    osMatch = false;
                    if (os.TryGetProperty("name", out JsonElement osName))
                    {
                        if (osName.GetString() == "windows")
                            osMatch = true;
                    }
                    if (os.TryGetProperty("arch", out JsonElement archName))
                    {
                        string arch = archName.GetString();
                        // Windows 64 位 x86_64；如果指定为 x86 则在 64 位系统上不匹配
                        if (arch == "x86" && Environment.Is64BitOperatingSystem)
                            osMatch = false;
                        if (arch == "x86_64" && !Environment.Is64BitOperatingSystem)
                            osMatch = false;
                    }
                }

                // 检查 features 规则（如 "has_custom_resolution"、"has_demo_account"、"is_quick_play_*"）
                if (rule.TryGetProperty("features", out JsonElement features))
                {
                    // 根据启动器的实际能力评估各 feature
                    //  - has_demo_account: 离线模式下没有 Demo 账号，false
                    //  - has_custom_resolution: 启动器支持自定义分辨率，true
                    //  - is_quick_play_singleplayer/multiplayer/realms: 未启用 quick play，false
                    //  - 其他未识别的 feature: 若值为 true 则通过（兼容未知 feature）
                    bool allFeaturesMatched = true;
                    foreach (var feat in features.EnumerateObject())
                    {
                        string featName = feat.Name;
                        bool expectedTrue = feat.Value.ValueKind == JsonValueKind.True;

                        bool actualValue;
                        switch (featName)
                        {
                            case "has_demo_account":
                                actualValue = false; // 离线模式没有 Demo 账号
                                break;
                            case "has_custom_resolution":
                                actualValue = true; // 启动器支持自定义分辨率
                                break;
                            case "is_quick_play_singleplayer":
                            case "is_quick_play_multiplayer":
                            case "is_quick_play_realms":
                                actualValue = false; // 未启用 quick play
                                break;
                            default:
                                // 未知 feature：若 JSON 期望 true 则默认通过以兼容
                                actualValue = expectedTrue;
                                break;
                        }

                        if (actualValue != expectedTrue)
                        {
                            allFeaturesMatched = false;
                            break;
                        }
                    }
                    if (!allFeaturesMatched)
                        continue;
                }

                if (action == "allow" && osMatch)
                    allowed = true;
                else if (action == "disallow" && osMatch)
                    allowed = false;
            }

            return allowed;
        }

        private void BuildGameArgs(LaunchInfo launchInfo, string versionId, JsonElement versionInfo, 
            string username, bool offlineMode, string additionalArgs, string resolution = "1920x1080")
        {
            Dictionary<string, string> argsDict = new Dictionary<string, string>();

            // ====== 身份参数合法性校验 ======
            // 1. username 必须合法（非空、长度、只含字母/数字/下划线）
            string cleanUsername = username?.Trim();
            if (!IsValidUsername(cleanUsername))
            {
                Log($"[身份] ⚠️  username 非法（'{username}'）");
                Log($"[身份] 使用默认玩家名 'Player'（建议在设置中修改）");
                cleanUsername = "Player";
            }

            // 2. uuid：强制 36 位 RFC 4122 格式（8-4-4-4-12）
            string uuid = GenerateUUID();
            if (!IsValidRFCUUID(uuid))
            {
                Log($"[身份] ⚠️  刚生成的 UUID 格式异常（{uuid}）");
                uuid = Guid.NewGuid().ToString("D");
                Log($"[身份] 已重新生成：{uuid}");
            }

            // 3. accessToken
            string accessToken = offlineMode ? GenerateOfflineToken(uuid) : "invalid";

            Log($"[身份] username={cleanUsername}");
            Log($"[身份]     uuid={uuid}");
            Log($"[身份] accessToken={new string('*', Math.Min(accessToken.Length, 8))}...");
            // =========================================================

            string assetIndexId = GetAssetIndexId(versionInfo);

            // 身份参数必须先写入，避免后续从 version.json 解析出的占位符覆盖
            argsDict["--username"] = cleanUsername;
            argsDict["--uuid"] = uuid;
            argsDict["--accessToken"] = accessToken;
            argsDict["--version"] = versionId;

            // 版本隔离功能：如果启用，每个版本使用独立的游戏目录
            // 整合包版本（有 inheritsFrom）自动启用，避免文件污染其他版本
            string gameDir = _minecraftPath;
            bool isInherited = versionInfo.TryGetProperty("inheritsFrom", out _);
            bool isModable = IsVersionModable(versionId);
            bool isRelease = IsVersionRelease(versionId);
            if (SettingsManager.Settings.ShouldIsolateVersion(isModable, isRelease, isInherited))
            {
                gameDir = Path.Combine(_minecraftPath, "versions", versionId, "game");
                // 确保目录存在
                if (!Directory.Exists(gameDir))
                {
                    Directory.CreateDirectory(gameDir);
                }
                
                // 如果版本隔离目录中没有 options.txt，从主目录复制
                string versionOptionsPath = Path.Combine(gameDir, "options.txt");
                string mainOptionsPath = Path.Combine(_minecraftPath, "options.txt");
                
                if (!File.Exists(versionOptionsPath) && File.Exists(mainOptionsPath))
                {
                    File.Copy(mainOptionsPath, versionOptionsPath);
                    Log($"[版本隔离] 已复制主目录的 options.txt 到版本隔离目录");
                }
                
                string reason = isInherited ? " (自动，整合包版本)" : "";
                Log($"[版本隔离] 已启用{reason}，等级: {SettingsManager.Settings.VersionIsolationLevel}，游戏目录: {gameDir}");
            }
            argsDict["--gameDir"] = gameDir;
            argsDict["--assetsDir"] = Path.Combine(_minecraftPath, "assets");
            argsDict["--assetIndex"] = assetIndexId;
            argsDict["--uuid"] = uuid;
            argsDict["--accessToken"] = accessToken;
            argsDict["--userType"] = "mojang";
            argsDict["--versionType"] = "release";

            // 解析分辨率设置（支持 "x" 和 "×" 分隔符）
            if (!string.IsNullOrEmpty(resolution))
            {
                // 将乘号替换为字母 x
                resolution = resolution.Replace("×", "x").Replace("＊", "x");
                if (resolution.Contains("x"))
                {
                    string[] parts = resolution.Split('x');
                    if (parts.Length == 2)
                    {
                        argsDict["--width"] = parts[0].Trim();
                        argsDict["--height"] = parts[1].Trim();
                        Log($"[参数] 分辨率: {parts[0].Trim()}x{parts[1].Trim()}");
                    }
                }
            }

            if (versionInfo.TryGetProperty("arguments", out JsonElement arguments) &&
                arguments.TryGetProperty("game", out JsonElement gameArgs))
            {
                foreach (JsonElement arg in gameArgs.EnumerateArray())
                {
                    if (arg.ValueKind == JsonValueKind.String)
                    {
                        string argStr = arg.GetString();
                        argStr = ReplaceArgumentPlaceholders(argStr, versionId, cleanUsername, uuid, accessToken, assetIndexId);

                        if (argStr.StartsWith("--"))
                        {
                            string[] parts = argStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 1)
                            {
                                string key = parts[0];
                                string value = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";

                                // ====== 安全层：离线模式下跳过与 Demo/QuickPlay 相关的参数 ======
                                // 这些参数会让 Minecraft 认为用户没有完整账号，从而进入 Demo 模式
                                if (key == "--demo" || key == "--clientId" || key == "--xuid" ||
                                    key.StartsWith("--quickPlay"))
                                {
                                    continue;
                                }

                                if (!argsDict.ContainsKey(key))
                                {
                                    argsDict[key] = value.Trim('"');
                                }
                                else
                                {
                                    if (key == "--version")
                                    {
                                        Log($"[参数] 跳过JSON中的重复--version参数（已在代码中设置）");
                                    }
                                    else
                                    {
                                        Log($"[参数] 跳过JSON中的重复参数: {key}");
                                    }
                                }
                            }
                        }
                    }
                    else if (arg.ValueKind == JsonValueKind.Object)
                    {
                        // 处理带 rules 的对象参数
                        if (!EvaluateArgumentRules(arg))
                            continue;

                        if (arg.TryGetProperty("value", out JsonElement value))
                        {
                            var argsFromObject = new List<string>();
                            if (value.ValueKind == JsonValueKind.String)
                            {
                                string s = ReplaceArgumentPlaceholders(value.GetString(), versionId, cleanUsername, uuid, accessToken, assetIndexId);
                                argsFromObject.Add(s);
                            }
                            else if (value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var v in value.EnumerateArray())
                                {
                                    if (v.ValueKind == JsonValueKind.String)
                                    {
                                        string s = ReplaceArgumentPlaceholders(v.GetString(), versionId, cleanUsername, uuid, accessToken, assetIndexId);
                                        argsFromObject.Add(s);
                                    }
                                }
                            }

                            // 解析每个字符串，处理 key 和 value
                            for (int i = 0; i < argsFromObject.Count; i++)
                            {
                                string s = argsFromObject[i];
                                if (string.IsNullOrWhiteSpace(s))
                                    continue;

                                if (s.StartsWith("--") && !s.Contains(" "))
                                {
                                    // 这是一个 key，下一个元素可能是 value
                                    string key = s;

                                    // 安全层：跳过 --demo、--clientId、--xuid、--quickPlay*
                                    if (key == "--demo" || key == "--clientId" || key == "--xuid" ||
                                        key.StartsWith("--quickPlay"))
                                    {
                                        // 同时跳过可能跟在后面的 value（如 "${quickPlayPath}" 等）
                                        if (i + 1 < argsFromObject.Count && !argsFromObject[i + 1].StartsWith("--"))
                                        {
                                            i++;
                                        }
                                        continue;
                                    }

                                    if (!argsDict.ContainsKey(key))
                                    {
                                        if (i + 1 < argsFromObject.Count && !argsFromObject[i + 1].StartsWith("--"))
                                        {
                                            argsDict[key] = argsFromObject[i + 1].Trim('"');
                                            i++;
                                        }
                                        else
                                        {
                                            argsDict[key] = "";
                                        }
                                    }
                                }
                                else if (s.StartsWith("--"))
                                {
                                    // "--key value" 格式
                                    string[] parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length >= 1)
                                    {
                                        string key = parts[0];

                                        // 安全层：跳过 --demo、--clientId、--xuid、--quickPlay*
                                        if (key == "--demo" || key == "--clientId" || key == "--xuid" ||
                                            key.StartsWith("--quickPlay"))
                                        {
                                            continue;
                                        }

                                        if (!argsDict.ContainsKey(key))
                                        {
                                            argsDict[key] = parts.Length > 1 ? string.Join(" ", parts.Skip(1)).Trim('"') : "";
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (var kvp in argsDict)
            {
                string key = kvp.Key;
                string value = kvp.Value;

                launchInfo.GameArgs.Add(key);
                
                if (!string.IsNullOrEmpty(value))
                {
                    if (value.Contains(" ") || value.Contains("\"") || value.Contains("'"))
                    {
                        value = "\"" + value.Replace("\"", "\\\"") + "\"";
                    }
                    launchInfo.GameArgs.Add(value);
                }
            }

            if (!string.IsNullOrEmpty(additionalArgs))
            {
                string[] additionalParts = additionalArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < additionalParts.Length; i++)
                {
                    string part = additionalParts[i];
                    if (part.StartsWith("--"))
                    {
                        if (!argsDict.ContainsKey(part))
                        {
                            launchInfo.GameArgs.Add(part);
                            if (i + 1 < additionalParts.Length && !additionalParts[i + 1].StartsWith("--"))
                            {
                                string value = additionalParts[i + 1];
                                if (value.Contains(" ") || value.Contains("\"") || value.Contains("'"))
                                {
                                    value = "\"" + value.Replace("\"", "\\\"") + "\"";
                                }
                                launchInfo.GameArgs.Add(value);
                                i++;
                            }
                        }
                        else
                        {
                            Log($"[参数] 跳过额外参数中的重复参数: {part}");
                            if (i + 1 < additionalParts.Length && !additionalParts[i + 1].StartsWith("--"))
                            {
                                i++;
                            }
                        }
                    }
                    else
                    {
                        launchInfo.GameArgs.Add(part);
                    }
                }
            }
        }

        private async Task ValidateLaunchInfoAsync(LaunchInfo launchInfo)
        {
            Log($"[校验] 主类: {launchInfo.MainClass}");
            Log($"[校验] 类路径长度: {launchInfo.Classpath.Length}");
            Log($"[校验] JVM参数数量: {launchInfo.JvmArgs.Count}");
            Log($"[校验] 游戏参数数量: {launchInfo.GameArgs.Count}");

            string command = launchInfo.FullCommand;

            string[] requiredArgs = { "--version", "--username", "--gameDir", "--assetsDir", "--assetIndex", "--uuid", "--accessToken" };
            foreach (string arg in requiredArgs)
            {
                if (!command.Contains(arg))
                {
                    Log($"[警告] 缺少必需参数: {arg}");
                }
            }

            var versionRegex = new System.Text.RegularExpressions.Regex(@"--version\s+(\S+)");
            var match = versionRegex.Match(command);
            if (match.Success)
            {
                string versionValue = match.Groups[1].Value;
                Log($"[校验] --version 值: {versionValue}");
            }
            else
            {
                Log($"[错误] 未找到--version参数");
            }

            foreach (string arg in requiredArgs)
            {
                int count = System.Text.RegularExpressions.Regex.Matches(command, System.Text.RegularExpressions.Regex.Escape(arg)).Count;
                if (count > 1)
                {
                    Log($"[警告] 参数 {arg} 重复出现 {count} 次");
                    
                    if (arg == "--version")
                    {
                        Log($"[修复] 移除重复的--version参数...");
                        launchInfo.GameArgs = RemoveDuplicateArgs(launchInfo.GameArgs, "--version");
                        launchInfo.FullCommand = await BuildFullCommandAsync(launchInfo.JvmArgs, launchInfo.Classpath, launchInfo.MainClass, launchInfo.GameArgs, launchInfo);
                        Log($"[修复] 重复参数已移除");
                    }
                }
            }

            if (!command.Contains("-cp"))
            {
                Log($"[错误] 缺少-cp参数");
            }

            if (!command.Contains(launchInfo.MainClass))
            {
                Log($"[错误] 缺少主类: {launchInfo.MainClass}");
            }
        }

        private List<string> RemoveDuplicateArgs(List<string> args, string argName)
        {
            List<string> result = new List<string>();
            bool found = false;

            for (int i = 0; i < args.Count; i++)
            {
                if (args[i] == argName)
                {
                    if (!found)
                    {
                        result.Add(args[i]);
                        if (i + 1 < args.Count && !args[i + 1].StartsWith("--"))
                        {
                            result.Add(args[i + 1]);
                            i++;
                        }
                        found = true;
                    }
                    else
                    {
                        Log($"[修复] 跳过重复的 {argName} 参数");
                        if (i + 1 < args.Count && !args[i + 1].StartsWith("--"))
                        {
                            i++;
                        }
                    }
                }
                else
                {
                    result.Add(args[i]);
                }
            }

            return result;
        }

        private string ReplaceArgumentPlaceholders(string arg, string versionId, string username, string uuid, string accessToken, string assetIndexId)
        {
            return arg
                .Replace("${auth_player_name}", username)
                .Replace("${version_name}", versionId)
                .Replace("${game_directory}", _minecraftPath)
                .Replace("${assets_root}", Path.Combine(_minecraftPath, "assets"))
                .Replace("${assets_index_name}", assetIndexId)
                .Replace("${auth_uuid}", uuid)
                .Replace("${auth_access_token}", accessToken)
                .Replace("${user_type}", "mojang")
                .Replace("${version_type}", "release");
        }

        private string BuildClasspath(string versionId, JsonElement versionInfo)
        {
            List<string> classpathList = new List<string>();

            // 主 JAR 路径：优先使用当前版本的 jar；如果不存在且有 inheritsFrom，使用父版本的 jar
            string jarPath = FindMainJarPath(versionId, "[CP]");

            if (jarPath != null)
            {
                classpathList.Add(jarPath);
                Log($"[CP] 添加主JAR: {jarPath}");
            }
            else
            {
                string expectedJar = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.jar");
                Log($"[CP] 错误: 主JAR不存在: {expectedJar}");
                Log($"[CP]   如果这是 Fabric/Forge/NeoForge 等继承版本，请确保父版本（如 1.21）的 jar 文件存在。");
                Log($"[CP]   解决方法：先下载原版 {versionId} 基础版本（即 inheritsFrom 指向的版本），再启动本版本。");
                throw new FileNotFoundException($"游戏主JAR文件不存在: {expectedJar}。如果这是加载器版本（Fabric/Forge/NeoForge），请先安装原版 {versionId} 版本。");
            }

            if (versionInfo.TryGetProperty("libraries", out JsonElement libraries))
            {
                int libCount = 0;
                int filteredCount = 0;
                int missingCount = 0;
                
                foreach (JsonElement library in libraries.EnumerateArray())
                {
                    if (!IsLibraryApplicable(library))
                    {
                        string libName = library.TryGetProperty("name", out var name) ? name.GetString() : "unknown";
                        Log($"[CP] 过滤非Windows库: {libName}");
                        filteredCount++;
                        continue;
                    }

                    string libPath = GetLibraryPath(library);
                    if (!string.IsNullOrEmpty(libPath))
                    {
                        string fullPath = Path.Combine(_minecraftPath, "libraries", libPath);
                        if (File.Exists(fullPath))
                        {
                            classpathList.Add(fullPath);
                            libCount++;
                        }
                        else
                        {
                            Log($"[CP] 警告: 库文件不存在: {fullPath}");
                            missingCount++;
                        }
                    }
                }
                Log($"[CP] 从JSON添加 {libCount} 个库（过滤 {filteredCount} 个非Windows库，缺失 {missingCount} 个库）");

                // 对于 Fabric/Forge/NeoForge 等有 inheritsFrom 的版本，缺失库文件是致命错误
                if (missingCount > 0 && versionInfo.TryGetProperty("inheritsFrom", out _))
                {
                    Log($"[CP] 错误: Fabric/Forge/NeoForge 版本缺少 {missingCount} 个库文件，请重新安装加载器");
                    throw new InvalidOperationException(
                        $"检测到 Fabric/Forge/NeoForge 版本缺少 {missingCount} 个库文件。\n" +
                        $"请确保已通过启动器正确安装了加载器。如果使用的是已存在的 .minecraft 目录，请尝试以下方法之一：\n" +
                        $"  1. 在启动器中选择原版版本启动一次，然后再安装 Fabric/Forge\n" +
                        $"  2. 或从官方启动器中安装必要组件后再使用本启动器");
                }
            }

            if (classpathList.Count == 0)
            {
                Log($"[CP] 错误: 类路径为空");
                throw new InvalidOperationException("类路径为空，无法启动游戏");
            }

            string classpath = string.Join(";", classpathList);
            Log($"[CP] 类路径总长度: {classpath.Length} 字符");
            Log($"[CP] 类路径库数量: {classpathList.Count}");

            return classpath;
        }

        private bool IsLibraryApplicable(JsonElement library)
        {
            // 获取库名称用于日志和过滤
            string libName = library.TryGetProperty("name", out var name) ? name.GetString() : "unknown";

            // 过滤 linux、macos 相关库
            if (libName.Contains("linux") || libName.Contains("macos"))
            {
                return false;
            }

            // 过滤 ca.weblite:java-objc-bridge 相关库
            if (libName.Contains("ca.weblite:java-objc-bridge"))
            {
                return false;
            }

            // 新增：过滤名称包含 windows-x86 的所有LWJGL依赖包
            if (libName.Contains("windows-x86"))
            {
                return false;
            }

            // 新增：过滤名称包含 windows-arm64 的所有LWJGL依赖包
            if (libName.Contains("windows-arm64"))
            {
                return false;
            }

            if (library.TryGetProperty("rules", out JsonElement rules))
            {
                bool allow = true;
                foreach (JsonElement rule in rules.EnumerateArray())
                {
                    if (rule.TryGetProperty("action", out JsonElement action))
                    {
                        string actionStr = action.GetString();
                        if (actionStr == "disallow")
                        {
                            if (rule.TryGetProperty("os", out JsonElement os))
                            {
                                if (os.TryGetProperty("name", out JsonElement osName))
                                {
                                    string osNameStr = osName.GetString();
                                    if (osNameStr == "windows")
                                    {
                                        allow = false;
                                    }
                                }
                            }
                        }
                        else if (actionStr == "allow")
                        {
                            if (rule.TryGetProperty("os", out JsonElement os))
                            {
                                if (os.TryGetProperty("name", out JsonElement osName))
                                {
                                    string osNameStr = osName.GetString();
                                    if (osNameStr != "windows" && osNameStr != null)
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }
                if (!allow) return false;
            }

            if (library.TryGetProperty("natives", out JsonElement natives))
            {
                JsonElement windowsNative;
                if (natives.TryGetProperty("windows", out windowsNative))
                {
                    string nativeKey = windowsNative.GetString();
                    if (nativeKey != null && nativeKey.Contains("x86"))
                    {
                        Log($"[CP] 过滤32位Windows库: {nativeKey}");
                        return false;
                    }
                }
            }

            if (library.TryGetProperty("downloads", out JsonElement downloads) &&
                downloads.TryGetProperty("classifiers", out JsonElement classifiers))
            {
                bool hasAmd64 = classifiers.TryGetProperty("natives-windows-amd64", out _);
                bool hasX64 = classifiers.TryGetProperty("natives-windows-x64", out _);
                bool hasX86 = classifiers.TryGetProperty("natives-windows-x86", out _);
                bool hasArm64 = classifiers.TryGetProperty("natives-windows-arm64", out _);

                // 1.21+ 使用 natives-windows-x64，之前版本使用 natives-windows-amd64
                // 只要存在 amd64 或 x64 的 classifier，就允许该库进入类路径
                if (hasAmd64 || hasX64)
                {
                    return true;
                }

                // 只有 x86 或 arm64 的 natives，则在 64 位 Windows 上过滤
                if (hasX86 || hasArm64)
                {
                    return false;
                }
            }

            return true;
        }

        private string GetLibraryPath(JsonElement library)
        {
            if (library.TryGetProperty("downloads", out JsonElement downloads) &&
                downloads.TryGetProperty("artifact", out JsonElement artifact))
            {
                if (artifact.TryGetProperty("path", out JsonElement pathElement))
                {
                    return pathElement.GetString();
                }
            }

            if (library.TryGetProperty("name", out JsonElement nameElement))
            {
                string name = nameElement.GetString();
                return ConvertMavenToPath(name);
            }

            return null;
        }

        private string ConvertMavenToPath(string mavenName)
        {
            if (string.IsNullOrEmpty(mavenName))
                return null;

            string[] parts = mavenName.Split(':');
            if (parts.Length < 3)
                return null;

            string groupId = parts[0].Replace('.', Path.DirectorySeparatorChar);
            string artifactId = parts[1];
            string version = parts[2];

            string fileName = $"{artifactId}-{version}.jar";
            return Path.Combine(groupId, artifactId, version, fileName);
        }

        private string GetMainClass(JsonElement versionInfo)
        {
            if (versionInfo.ValueKind != JsonValueKind.Undefined &&
                versionInfo.TryGetProperty("mainClass", out JsonElement mainClass))
            {
                return mainClass.GetString();
            }
            return "net.minecraft.client.main.Main";
        }

        private string GetAssetIndexId(JsonElement versionInfo)
        {
            if (versionInfo.ValueKind != JsonValueKind.Undefined &&
                versionInfo.TryGetProperty("assetIndex", out JsonElement assetIndex) &&
                assetIndex.TryGetProperty("id", out JsonElement id))
            {
                return id.GetString();
            }
            return "legacy";
        }

        /// <summary>
        /// 生成一个标准的 RFC 4122 36 位 UUID（格式：xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx）。
        /// 注意：必须带 4 个横杠，否则游戏会识别为非法身份，进入 Demo 模式。
        /// </summary>
        private string GenerateUUID()
        {
            return Guid.NewGuid().ToString("D");
        }

        /// <summary>
        /// 生成离线模式 accessToken（同样使用 36 位格式，保持与 uuid 一致）。
        /// 游戏对 accessToken 的格式不像 uuid 那么严格，但仍推荐长度一致以避免被某些 Mod 校验拒绝。
        /// </summary>
        private string GenerateOfflineToken(string uuid)
        {
            // 基于 uuid 派生一个确定性的 accessToken：去掉横杠 + 小写
            string baseToken = uuid.Replace("-", string.Empty).ToLowerInvariant();
            // 末尾追加一个小后缀，保证长度 ≥ 32 且与 uuid 不同，避免某些启动器把 accessToken 当作 uuid
            return baseToken + "00000000000000000000000000000000".Substring(0, 32 - Math.Min(32, baseToken.Length));
        }

        /// <summary>
        /// 校验字符串是否为标准 RFC 4122 格式 UUID（36 位，含 4 个 '-'）。
        /// 合法示例：01234567-89ab-cdef-0123-456789abcdef
        /// </summary>
        private static bool IsValidRFCUUID(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) return false;
            if (uuid.Length != 36) return false;
            // 验证横杠位置：8-4-4-4-12
            if (uuid[8] != '-' || uuid[13] != '-' || uuid[18] != '-' || uuid[23] != '-') return false;

            // 其余字符必须是 0-9 / a-f / A-F
            for (int i = 0; i < uuid.Length; i++)
            {
                if (i == 8 || i == 13 || i == 18 || i == 23) continue;
                char c = uuid[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 校验 username 是否合法：非空、非空白、长度 3-16、不含非法字符。
        /// 游戏启动后 username 会被用来做玩家显示名，包含空格或特殊字符会导致 auth 失败。
        /// </summary>
        private static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (username.Length < 3 || username.Length > 16) return false;
            // Minecraft 正版允许：字母、数字、下划线；离线模式也同样推荐。
            foreach (char c in username)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_'))
                    return false;
            }
            return true;
        }

        private string FindJavaPath()
        {
            Log($"[Java] 开始检测Java路径...");
            Log($"[Java] 系统架构: {(Environment.Is64BitOperatingSystem ? "64位" : "32位")}");

            var prioritizedPaths = new SortedDictionary<string, List<string>>(Comparer<string>.Create((a, b) =>
            {
                int scoreA = GetJavaVersionScore(a);
                int scoreB = GetJavaVersionScore(b);
                return scoreB.CompareTo(scoreA);
            }));

            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                AddJavaPath(prioritizedPaths, javaHome, "JAVA_HOME");
            }

            DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Eclipse Adoptium", "Eclipse Adoptium");
            DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Temurin", "Temurin");
            DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Microsoft\jdk", "Microsoft JDK");
            DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Amazon Corretto", "Amazon Corretto");
            DetectJavaInDirectory(prioritizedPaths, @"C:\Program Files\Java", "Java");

            // 优先检查 MNL 游戏目录下的运行时，再检查官方启动器目录
            string mnlRuntimeDir = Path.Combine(AppContext.MinecraftPath, "runtime");
            if (Directory.Exists(mnlRuntimeDir))
            {
                try
                {
                    foreach (string subDir in Directory.GetDirectories(mnlRuntimeDir))
                    {
                        AddJavaPath(prioritizedPaths, Path.Combine(subDir, "bin"), "MNL/runtime");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"扫描 MNL 运行时失败: {ex.Message}");
                }
            }

            string userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime");
            if (Directory.Exists(userDir))
            {
                try
                {
                    foreach (string subDir in Directory.GetDirectories(userDir))
                    {
                        AddJavaPath(prioritizedPaths, Path.Combine(subDir, "bin"), ".minecraft/runtime");
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Java] 检查runtime目录失败: {ex.Message}");
                }
            }

            DetectJavaFromRegistry(prioritizedPaths);

            var commonPaths = new List<string> {
                @"C:\Program Files\Eclipse Adoptium\jdk-17.0.9.9-hotspot\bin\java.exe",
                @"C:\Program Files\Eclipse Adoptium\jdk-17.0.8.8-hotspot\bin\java.exe",
                @"C:\Program Files\Java\jdk-17\bin\java.exe",
                @"C:\Program Files\Java\jdk-21\bin\java.exe",
                @"C:\Program Files\Java\jdk1.8.0_401\bin\java.exe",
                @"C:\Program Files\Java\jdk1.8.0_301\bin\java.exe"
            };
            foreach (var path in commonPaths)
            {
                AddJavaPath(prioritizedPaths, path, "常见路径");
            }

            foreach (var kvp in prioritizedPaths)
            {
                foreach (string source in kvp.Value)
                {
                    string javaExe = FindJavaExeInPath(source);
                    if (!string.IsNullOrEmpty(javaExe) && File.Exists(javaExe))
                    {
                        string version = GetJavaVersionFromExe(javaExe);
                        Log($"[Java] 找到Java: {javaExe}");
                        Log($"[Java] 版本: {version}");
                        Log($"[Java] 来源: {source}");
                        return javaExe;
                    }
                }
            }

            Log($"[Java] 未能找到预定义路径，尝试系统PATH...");

            try
            {
                var pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    foreach (string pathDir in pathEnv.Split(Path.PathSeparator))
                    {
                        if (string.IsNullOrEmpty(pathDir))
                            continue;

                        string javaExePath = Path.Combine(pathDir, "java.exe");
                        if (File.Exists(javaExePath))
                        {
                            string version = GetJavaVersionFromExe(javaExePath);
                            Log($"[Java] 从系统PATH找到: {javaExePath}");
                            Log($"[Java] 版本: {version}");
                            return javaExePath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[Java] 查找PATH中的Java失败: {ex.Message}");
            }

            string systemJava = FindJavaExeInSystem();
            if (!string.IsNullOrEmpty(systemJava) && File.Exists(systemJava))
            {
                Log($"[Java] 通过系统命令找到: {systemJava}");
                return systemJava;
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string[] commonJavaPaths = {
                Path.Combine(programFiles, "Eclipse Adoptium", "jdk-17.0.9.9-hotspot", "bin", "java.exe"),
                Path.Combine(programFiles, "Eclipse Adoptium", "jdk-17.0.8.8-hotspot", "bin", "java.exe"),
                Path.Combine(programFiles, "Java", "jdk-17", "bin", "java.exe"),
                Path.Combine(programFiles, "Java", "jdk-21", "bin", "java.exe")
            };

            foreach (string path in commonJavaPaths)
            {
                if (File.Exists(path))
                {
                    string version = GetJavaVersionFromExe(path);
                    Log($"[Java] 最后尝试找到: {path}");
                    Log($"[Java] 版本: {version}");
                    return path;
                }
            }

            Log($"[Java] 错误: 未能找到有效的Java安装");
            throw new FileNotFoundException("无法找到有效的Java安装。请安装Java 17或更高版本，或在设置中手动指定Java路径。");
        }

        private void AddJavaPath(SortedDictionary<string, List<string>> dict, string path, string source)
        {
            if (string.IsNullOrEmpty(path))
                return;

            string key = path.ToLower();
            if (!dict.ContainsKey(key))
            {
                dict[key] = new List<string>();
            }
            if (!dict[key].Contains(source))
            {
                dict[key].Add(source);
            }
        }

        private void DetectJavaInDirectory(SortedDictionary<string, List<string>> dict, string baseDir, string source)
        {
            if (!Directory.Exists(baseDir))
                return;

            try
            {
                foreach (string subDir in Directory.GetDirectories(baseDir))
                {
                    AddJavaPath(dict, subDir, source);
                    AddJavaPath(dict, Path.Combine(subDir, "bin"), source);
                }
            }
            catch (Exception ex)
            {
                Log($"[Java] 检测{source}目录失败: {ex.Message}");
            }
        }

        private void DetectJavaFromRegistry(SortedDictionary<string, List<string>> dict)
        {
            try
            {
                string[] registryPaths = {
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\JavaSoft\Java Runtime Environment",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\JavaSoft\Java Runtime Environment",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\JavaSoft\Java Development Kit",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\JavaSoft\Java Development Kit"
                };

                foreach (string registryPath in registryPaths)
                {
                    try
                    {
                        string javaVersion = (string)Microsoft.Win32.Registry.GetValue(registryPath, "CurrentVersion", null);
                        if (!string.IsNullOrEmpty(javaVersion))
                        {
                            string javaHome = (string)Microsoft.Win32.Registry.GetValue(registryPath + "\\" + javaVersion, "JavaHome", null);
                            if (!string.IsNullOrEmpty(javaHome))
                            {
                                AddJavaPath(dict, javaHome, "注册表");
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log($"[Java] 检测注册表失败: {ex.Message}");
            }
        }

        private string FindJavaExeInPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string javaExe = Path.Combine(path, "java.exe");
            if (File.Exists(javaExe))
                return javaExe;

            if (Path.GetFileName(path).Equals("java.exe", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(path))
                    return path;
            }

            string binPath = Path.Combine(path, "bin", "java.exe");
            if (File.Exists(binPath))
                return binPath;

            return null;
        }

        private int GetJavaVersionScore(string path)
        {
            string lowerPath = path.ToLower();
            int score = 0;

            // 最新版本 Java 25 优先级最高
            if (lowerPath.Contains("jdk-25") || lowerPath.Contains("jdk25") || lowerPath.Contains("java25"))
                score = 130;
            else if (lowerPath.Contains("jdk-21") || lowerPath.Contains("jdk21") || lowerPath.Contains("java21"))
                score = 120;
            else if (lowerPath.Contains("jdk-17") || lowerPath.Contains("jdk17") || lowerPath.Contains("java17"))
                score = 100;
            else if (lowerPath.Contains("jdk-11") || lowerPath.Contains("jdk11") || lowerPath.Contains("java11"))
                score = 50;
            else if (lowerPath.Contains("jdk1.8") || lowerPath.Contains("java8") || lowerPath.Contains("jdk-8"))
                score = 30;
            else if (lowerPath.Contains("jdk"))
                score = 20;
            else if (lowerPath.Contains("jre"))
                score = 10;

            if (lowerPath.Contains("x64") || lowerPath.Contains("x86_64") || lowerPath.Contains("amd64"))
                score += 5;

            if (Environment.Is64BitOperatingSystem && (lowerPath.Contains("x86") || lowerPath.Contains("32")))
                score -= 20;

            return score;
        }

        private string GetJavaVersionFromExe(string javaExe)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        string errorOutput = process.StandardError.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (!string.IsNullOrEmpty(errorOutput))
                        {
                            var lines = errorOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            if (lines.Length > 0)
                                return lines[0];
                        }
                    }
                }
            }
            catch { }

            return "未知版本";
        }

        public List<string> GetInstalledVersions()
        {
            List<string> versions = new List<string>();
            string versionsDir = Path.Combine(_minecraftPath, "versions");

            if (Directory.Exists(versionsDir))
            {
                foreach (string dir in Directory.GetDirectories(versionsDir))
                {
                    string versionId = Path.GetFileName(dir);
                    string jarFile = Path.Combine(dir, $"{versionId}.jar");
                    string jsonFile = Path.Combine(dir, $"{versionId}.json");

                    if (File.Exists(jarFile) && File.Exists(jsonFile))
                    {
                        versions.Add(versionId);
                    }
                }
            }

            return versions.OrderBy(v => v).ToList();
        }

        public void KillGame()
        {
            try
            {
                if (_gameProcess != null && !_gameProcess.HasExited)
                {
                    _gameProcess.Kill();
                    Log("游戏进程已被终止");
                }
            }
            catch (Exception ex)
            {
                Log($"终止游戏进程失败: {ex.Message}");
            }
        }
    }

    internal class LaunchInfo
    {
        public string MainClass { get; set; }
        public string Classpath { get; set; }
        public List<string> JvmArgs { get; set; } = new List<string>();
        public List<string> GameArgs { get; set; } = new List<string>();
        public string FullCommand { get; set; }
        public List<string> ArgumentList { get; set; } = new List<string>();
    }
}
