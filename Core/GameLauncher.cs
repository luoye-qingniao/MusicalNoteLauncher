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
using PCLCS;

namespace MusicalNoteLauncher.Core
{
    public class GameLauncher
    {
        public event Action<string> LaunchStatusChanged;
        public event Action<string> LaunchLogReceived;
        public event Action<bool> LaunchCompleted;

        private readonly string _minecraftPath;
        private readonly string _javaPath;
        private Process _gameProcess;
        private bool _hasExited = false;
        private string _logFilePath;
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
                        File.WriteAllText(optionsPath, content);
                        Log($"[选项] 更新全屏设置: {newValue}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[警告] 无法更新 options.txt: {ex.Message}");
            }
        }

        private void SetupChineseLanguageForVersion(string versionId)
        {
            try
            {
                // 解析版本号，获取主版本号
                int majorVersion = 1;
                try
                {
                    string[] parts = versionId.Split('.');
                    if (parts.Length >= 1)
                    {
                        majorVersion = int.Parse(parts[0]);
                        if (parts.Length >= 2 && parts[0] == "1")
                        {
                            majorVersion = int.Parse(parts[1]);
                        }
                    }
                }
                catch
                {
                    // 默认使用1
                }

                // 获取游戏的options.txt路径（使用版本隔离的游戏目录）
                string gameDir = _minecraftPath;
                if (SettingsManager.Settings.EnableVersionIsolation)
                {
                    gameDir = Path.Combine(_minecraftPath, "versions", versionId, "game");
                }
                
                string optionsPath = Path.Combine(gameDir, "options.txt");
                
                // 确保options.txt存在
                if (!File.Exists(optionsPath))
                {
                    // 尝试从主目录复制
                    string mainOptionsPath = Path.Combine(_minecraftPath, "options.txt");
                    if (File.Exists(mainOptionsPath))
                    {
                        File.Copy(mainOptionsPath, optionsPath);
                        Log($"[语言] 已从主目录复制 options.txt 到版本隔离目录");
                    }
                }

                // 使用ChineseLaunchHelper设置中文语言（强制设置）
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
                }
                else
                {
                    Log($"[语言] 中文语言设置失败: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Log($"[语言] 设置中文语言时发生错误: {ex.Message}");
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

            // 根据用户选择修改 options.txt 中的全屏设置
            UpdateFullscreenOption(isFullscreen);

            // 设置中文语言
            SetupChineseLanguageForVersion(versionId);

            return await Task.Run(async () =>
            {
                try
                {
                    _hasExited = false;

                    Log("【步骤0】清理临时文件...");
                    await Task.Run(() => CleanupTempClasspathFiles());
                    Log("【步骤0】清理完成");

                    Log("【步骤1】验证Java路径...");
                    await PostStatusAsync("正在验证Java路径...");
                    string validatedJavaPath = await Task.Run(() => ValidateAndFixJavaPath(_javaPath));
                    Log($"[Java] 验证的Java路径: {validatedJavaPath}");

                    if (string.IsNullOrEmpty(validatedJavaPath))
                    {
                        await PostStatusAsync("无法找到有效的Java安装。请安装Java 17或更高版本，或在设置中手动指定Java路径。");
                        Log("[错误] 无法找到有效的Java安装");
                        PostCompleted(false);
                        return false;
                    }

                    Log($"[Java] Java版本检查:");
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
                        Arguments = launchInfo.FullCommand,
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = false,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

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
                                PostStatus("游戏正常关闭");
                                PostCompleted(true);
                            }
                            else
                            {
                                PostStatus($"游戏异常退出 (退出码: {exitCode})");
                                Log($"游戏进程异常退出，退出码: {exitCode}");
                                PostCompleted(false);
                            }
                            Log("═══════════════════════════════════════════════════════════════");
                        }
                        catch (Exception ex)
                        {
                            Log($"获取退出码失败: {ex.Message}");
                            PostCompleted(false);
                        }
                    };

                    Log("[启动] 执行 Process.Start()...");
                    bool started = _gameProcess.Start();
                    Log($"[启动] Process.Start() 返回: {started}");

                    if (!started)
                    {
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
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"【异常】启动失败: {ex.Message}");
                    Log($"【异常】堆栈: {ex.StackTrace}");
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
                }, null);
            }
            else
            {
                LaunchCompleted?.Invoke(success);
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
                            // 检查windows natives
                            if (classifiers.TryGetProperty("natives-windows", out JsonElement windowsNative))
                            {
                                isNatives = true;
                                classifier = "natives-windows";
                            }
                            else if (classifiers.TryGetProperty("natives-windows-64", out JsonElement windows64Native))
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

            if (!File.Exists(jarFile))
            {
                Log($"Jar文件不存在: {jarFile}");
                return false;
            }

            if (!File.Exists(jsonFile))
            {
                Log($"JSON文件不存在: {jsonFile}");
                return false;
            }

            Log($"版本 {versionId} 校验通过");
            return true;
        }

        private JsonElement LoadVersionInfo(string versionId)
        {
            try
            {
                string jsonPath = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.json");
                string jsonContent = File.ReadAllText(jsonPath);

                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    return doc.RootElement.Clone();
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

            launchInfo.FullCommand = await BuildFullCommandAsync(launchInfo.JvmArgs, classpath, mainClass, launchInfo.GameArgs);

            await ValidateLaunchInfoAsync(launchInfo);

            string shortCommand = launchInfo.FullCommand;
            if (shortCommand.Length > 500)
            {
                shortCommand = shortCommand.Substring(0, 500) + "...";
            }
            Log($"[启动命令] {shortCommand}");

            return launchInfo;
        }

        private async Task<string> BuildFullCommandAsync(List<string> jvmArgs, string classpath, string mainClass, List<string> gameArgs)
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

            AddArg("-XX:+UnlockExperimentalVMOptions");
            AddArg("-XX:+UseG1GC");
            AddArg("-XX:G1NewSizePercent=20");
            AddArg("-XX:G1ReservePercent=20");
            AddArg("-XX:MaxGCPauseMillis=50");
            AddArg("-XX:G1HeapRegionSize=32M");
            AddArg("-XX:+DisableExplicitGC");
            AddArg("-XX:+AlwaysPreTouch");
            AddArg("-XX:+ParallelRefProcEnabled");

            string jarPath = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.jar");
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
                }
            }
        }

        private void BuildGameArgs(LaunchInfo launchInfo, string versionId, JsonElement versionInfo, 
            string username, bool offlineMode, string additionalArgs, string resolution = "1920x1080")
        {
            Dictionary<string, string> argsDict = new Dictionary<string, string>();

            string uuid = GenerateUUID();
            string accessToken = offlineMode ? GenerateOfflineToken(uuid) : "invalid";
            string assetIndexId = GetAssetIndexId(versionInfo);

            argsDict["--username"] = username;
            argsDict["--version"] = versionId;

            // 版本隔离功能：如果启用，每个版本使用独立的游戏目录
            string gameDir = _minecraftPath;
            if (SettingsManager.Settings.EnableVersionIsolation)
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
                
                Log($"[版本隔离] 已启用，游戏目录: {gameDir}");
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
                        argStr = ReplaceArgumentPlaceholders(argStr, versionId, username, uuid, accessToken, assetIndexId);

                        if (argStr.StartsWith("--"))
                        {
                            string[] parts = argStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 1)
                            {
                                string key = parts[0];
                                string value = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";

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
                        launchInfo.FullCommand = await BuildFullCommandAsync(launchInfo.JvmArgs, launchInfo.Classpath, launchInfo.MainClass, launchInfo.GameArgs);
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

            string jarPath = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.jar");
            if (File.Exists(jarPath))
            {
                classpathList.Add(jarPath);
                Log($"[CP] 添加主JAR: {jarPath}");
            }
            else
            {
                Log($"[CP] 错误: 主JAR不存在: {jarPath}");
                throw new FileNotFoundException($"游戏主JAR文件不存在: {jarPath}");
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

                // 仅保留 natives-windows-amd64 架构的库
                if (hasX86)
                {
                    return false;
                }

                if (hasArm64)
                {
                    return false;
                }

                // 确保只有 natives-windows-amd64 的库才被允许
                if ((hasX86 || hasArm64) && !hasAmd64)
                {
                    return false;
                }

                if (hasAmd64 || hasX64)
                {
                    return true;
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

        private string GenerateUUID()
        {
            return Guid.NewGuid().ToString("N");
        }

        private string GenerateOfflineToken(string uuid)
        {
            return Guid.NewGuid().ToString("N");
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

            if (lowerPath.Contains("jdk-17") || lowerPath.Contains("jdk17") || lowerPath.Contains("java17"))
                score = 100;
            else if (lowerPath.Contains("jdk-21") || lowerPath.Contains("jdk21") || lowerPath.Contains("java21"))
                score = 90;
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
    }
}
