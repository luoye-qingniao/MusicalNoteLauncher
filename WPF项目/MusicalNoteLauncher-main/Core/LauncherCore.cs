using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MusicalNoteLauncher.Core
{
    public class JavaInfo
    {
        public string Path { get; set; }
        public string Version { get; set; }
        public string DisplayName { get; set; }
    }

    public class LauncherCore
    {
        private readonly ConfigManager _config;

        public LauncherCore(ConfigManager config)
        {
            _config = config;
        }

        public List<JavaInfo> DetectJavaVersions()
        {
            List<JavaInfo> javaList = new List<JavaInfo>();

            foreach (JavaInfo java in DetectFromRegistry())
            {
                if (!javaList.Exists(j => j.Path == java.Path))
                {
                    javaList.Add(java);
                }
            }

            foreach (JavaInfo java in DetectFromProgramFiles())
            {
                if (!javaList.Exists(j => j.Path == java.Path))
                {
                    javaList.Add(java);
                }
            }

            foreach (JavaInfo java in DetectFromEnvironment())
            {
                if (!javaList.Exists(j => j.Path == java.Path))
                {
                    javaList.Add(java);
                }
            }

            return javaList;
        }

        private List<JavaInfo> DetectFromRegistry()
        {
            List<JavaInfo> javaList = new List<JavaInfo>();
            string[] registryPaths = {
                @"SOFTWARE\JavaSoft\Java Runtime Environment",
                @"SOFTWARE\JavaSoft\Java Development Kit"
            };

            foreach (string registryPath in registryPaths)
            {
                try
                {
                    using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(registryPath))
                    {
                        if (baseKey != null)
                        {
                            string currentVersion = baseKey.GetValue("CurrentVersion")?.ToString();
                            if (!string.IsNullOrEmpty(currentVersion))
                            {
                                using (RegistryKey versionKey = baseKey.OpenSubKey(currentVersion))
                                {
                                    string javaHome = versionKey?.GetValue("JavaHome")?.ToString();
                                    if (!string.IsNullOrEmpty(javaHome))
                                    {
                                        string javaExePath = Path.Combine(javaHome, "bin", "java.exe");
                                        if (File.Exists(javaExePath))
                                        {
                                            string version = GetJavaVersionFromPath(javaHome);
                                            javaList.Add(new JavaInfo
                                            {
                                                Path = javaExePath,
                                                Version = version,
                                                DisplayName = $"Java {version} (绯荤粺娉ㄥ唽琛?"
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            return javaList;
        }

        private List<JavaInfo> DetectFromProgramFiles()
        {
            List<JavaInfo> javaList = new List<JavaInfo>();
            string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            try
            {
                if (Directory.Exists(programFilesPath))
                {
                    string javaDir = Path.Combine(programFilesPath, "Java");
                    if (Directory.Exists(javaDir))
                    {
                        foreach (string dir in Directory.GetDirectories(javaDir))
                        {
                            string javaExePath = Path.Combine(dir, "bin", "java.exe");
                            if (File.Exists(javaExePath))
                            {
                                string version = GetJavaVersionFromPath(dir);
                                javaList.Add(new JavaInfo
                                {
                                    Path = javaExePath,
                                    Version = version,
                                    DisplayName = $"Java {version} (鏈湴瀹夎)"
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return javaList;
        }

        private List<JavaInfo> DetectFromEnvironment()
        {
            List<JavaInfo> javaList = new List<JavaInfo>();

            // 1. JAVA_HOME 环境变量
            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                TryAddJavaFromHome(javaList, javaHome, "JAVA_HOME");
            }

            // 2. PATH 环境变量中的 java.exe / javaw.exe
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (string dir in pathEnv.Split(';'))
                {
                    string trimmed = dir.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    string javaExe = Path.Combine(trimmed, "java.exe");
                    if (File.Exists(javaExe))
                    {
                        string parent = Directory.GetParent(trimmed)?.FullName;
                        if (parent != null)
                        {
                            TryAddJavaFromHome(javaList, parent, "PATH");
                        }
                        else
                        {
                            string version = GetJavaVersionFromPath(trimmed);
                            javaList.Add(new JavaInfo
                            {
                                Path = javaExe,
                                Version = version,
                                DisplayName = $"Java {version} (PATH)"
                            });
                        }
                    }
                }
            }

            return javaList;
        }

        private void TryAddJavaFromHome(List<JavaInfo> list, string home, string source)
        {
            if (string.IsNullOrEmpty(home)) return;
            string javaExe = Path.Combine(home, "bin", "java.exe");
            if (File.Exists(javaExe))
            {
                string version = GetJavaVersionFromPath(home);
                list.Add(new JavaInfo
                {
                    Path = javaExe,
                    Version = version,
                    DisplayName = $"Java {version} ({source})"
                });
            }
        }

        private string GetJavaVersionFromPath(string path)
        {
            try
            {
                string dirName = Path.GetFileName(path);
                Match match = Regex.Match(dirName, @"(jdk|jre)[-_]?(\d+)(?:\.\d+)?(?:\.\d+)?");
                if (match.Success && match.Groups.Count >= 3)
                {
                    string majorVersion = match.Groups[2].Value;
                    if (majorVersion == "1")
                    {
                        Match subMatch = Regex.Match(dirName, @"1\.(\d+)");
                        if (subMatch.Success && subMatch.Groups.Count >= 2)
                        {
                            return "1." + subMatch.Groups[1].Value;
                        }
                    }
                    return majorVersion;
                }
            }
            catch
            {
            }
            return "Unknown";
        }

        public string AutoDetectBestJava()
        {
            List<JavaInfo> javaList = DetectJavaVersions();

            if (javaList.Count == 0)
            {
                return string.Empty;
            }

            string[] preferredVersions = { "17", "11", "1.8", "8" };
            foreach (string preferred in preferredVersions)
            {
                foreach (JavaInfo java in javaList)
                {
                    if (java.Version.StartsWith(preferred) ||
                        (preferred == "1.8" && (java.Version == "1.8" || java.Version == "8")))
                    {
                        return java.Path;
                    }
                }
            }

            return javaList[0].Path;
        }

        public List<string> GetInstalledGameVersions()
        {
            List<string> versions = new List<string>();
            try
            {
                string minecraftPath = _config.GetMinecraftPath();
                string versionsPath = Path.Combine(minecraftPath, "versions");

                if (!Directory.Exists(versionsPath))
                {
                    return versions;
                }

                foreach (string dir in Directory.GetDirectories(versionsPath))
                {
                    string versionName = Path.GetFileName(dir);
                    string jarPath = Path.Combine(dir, versionName + ".jar");
                    string jsonPath = Path.Combine(dir, versionName + ".json");

                    if (File.Exists(jsonPath))
                    {
                        versions.Add(versionName);
                    }
                }
            }
            catch
            {
            }

            versions.Sort((a, b) => CompareVersions(b, a));
            return versions;
        }

        private int CompareVersions(string v1, string v2)
        {
            try
            {
                string[] parts1 = Regex.Split(v1, @"\D+");
                string[] parts2 = Regex.Split(v2, @"\D+");

                int len = Math.Max(parts1.Length, parts2.Length);
                for (int i = 0; i < len; i++)
                {
                    int p1 = i < parts1.Length && !string.IsNullOrEmpty(parts1[i]) ? int.Parse(parts1[i]) : 0;
                    int p2 = i < parts2.Length && !string.IsNullOrEmpty(parts2[i]) ? int.Parse(parts2[i]) : 0;

                    if (p1 != p2)
                    {
                        return p1.CompareTo(p2);
                    }
                }
            }
            catch
            {
            }
            return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
        }

        public bool ValidateGameVersion(string version, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrEmpty(version))
            {
                errorMessage = "璇烽€夋嫨娓告垙鐗堟湰";
                return false;
            }

            string minecraftPath = _config.GetMinecraftPath();
            string versionsPath = Path.Combine(minecraftPath, "versions");
            string versionPath = Path.Combine(versionsPath, version);

            if (!Directory.Exists(versionPath))
            {
                errorMessage = $"游戏版本不存在：{version}`n 请确认版本号是否正确，或确保该版本已下载。";
                return false;
            }

            string jsonPath = Path.Combine(versionPath, version + ".json");
            if (!File.Exists(jsonPath))
            {
                errorMessage = $"游戏版本文件不完整：{version}`n 请重新下载该版本。";
                return false;
            }

            return true;
        }

        /// 获取版本隔离的游戏目录（对接 SettingsManager 版本隔离设置）
        /// 参考 PCL PathIndie
        public string GetGameDirectory(string minecraftPath, string gameVersion)
        {
            try
            {
                if (SettingsManager.Settings.ShouldIsolateVersionForVersion(minecraftPath, gameVersion))
                {
                    string versionDir = Path.Combine(minecraftPath, "versions", gameVersion);
                    return versionDir;
                }
            }
            catch { /* 设置读取失败时回退到全局目录 */ }

            return minecraftPath;
        }

        public bool LaunchGame(string javaPath, string gameVersion, int memoryMB, string username, bool offlineMode, Action<string> logCallback = null, string resolution = "1920x1080", bool fullscreen = false)
        {
            try
            {
                if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
                {
                    throw new Exception("Java 路径无效：" + javaPath);
                }

                string errorMessage;
                if (!ValidateGameVersion(gameVersion, out errorMessage))
                {
                    throw new Exception(errorMessage);
                }

                string minecraftPath = _config.GetMinecraftPath();
                string versionsPath = Path.Combine(minecraftPath, "versions", gameVersion);

                // ★ 版本隔离：游戏数据目录（mods/saves/config/options.txt 等）
                string gameDir = GetGameDirectory(minecraftPath, gameVersion);
                bool isIsolated = gameDir != minecraftPath;
                if (isIsolated)
                {
                    logCallback?.Invoke($"[MNL] 检测到版本隔离目录: {gameDir}");
                    // 确保隔离目录下的必要子文件夹存在
                    try { Directory.CreateDirectory(Path.Combine(gameDir, "mods")); } catch { }
                    try { Directory.CreateDirectory(Path.Combine(gameDir, "saves")); } catch { }
                    try { Directory.CreateDirectory(Path.Combine(gameDir, "config")); } catch { }
                }

                string nativesPath = Path.Combine(versionsPath, $"{gameVersion}-natives");
                string librariesPath = Path.Combine(minecraftPath, "libraries");
                string assetsPath = Path.Combine(minecraftPath, "assets");

                // ★ 前置校验：扫描 mods 文件夹，防止损坏文件导致 Forge 崩溃
                ValidateModsFolder(gameDir, logCallback);

                // ★ 从版本 JSON 读取实际客户端 JAR（处理 inheritsFrom）
                string clientJar = gameVersion + ".jar";
                string jarVersionDir = versionsPath;
                try
                {
                    string jsonPath = Path.Combine(versionsPath, gameVersion + ".json");
                    if (File.Exists(jsonPath))
                    {
                        string jsonContent = File.ReadAllText(jsonPath);
                        using (JsonDocument jarDoc = JsonDocument.Parse(jsonContent))
                        {
                            if (jarDoc.RootElement.TryGetProperty("jar", out JsonElement jarField))
                            {
                                string jarId = jarField.GetString();
                                clientJar = jarId + ".jar";
                                jarVersionDir = Path.Combine(minecraftPath, "versions", jarId);
                            }
                            // inheritsFrom：使用父版本的 jar
                            else if (jarDoc.RootElement.TryGetProperty("inheritsFrom", out JsonElement inherits))
                            {
                                string parentVersion = inherits.GetString();
                                clientJar = parentVersion + ".jar";
                                jarVersionDir = Path.Combine(minecraftPath, "versions", parentVersion);
                            }
                        }
                    }
                }
                catch { }
                string jarPath = Path.Combine(jarVersionDir, clientJar);
                logCallback?.Invoke($"[MNL] 客户端 JAR: {jarPath}");
                if (!File.Exists(jarPath))
                {
                    logCallback?.Invoke($"[MNL] 警告: JAR 不存在: {jarPath}");
                }

                // ★ 每次启动都重新提取 natives（参考 PCL，避免 DLL 版本混杂/损坏）
                if (Directory.Exists(nativesPath))
                {
                    try
                    {
                        foreach (string file in Directory.GetFiles(nativesPath, "*", SearchOption.TopDirectoryOnly))
                        {
                            try { File.Delete(file); } catch (UnauthorizedAccessException) { /* MC 正在运行 */ } catch { }
                        }
                    }
                    catch { }
                }
                else
                {
                    Directory.CreateDirectory(nativesPath);
                }
                ExtractNatives(librariesPath, nativesPath, gameVersion, versionsPath);

                string assetIndex = GetAssetIndex(gameVersion, versionsPath);
                string librariesClassPath = GetLibrariesClassPath(librariesPath, gameVersion, versionsPath, logCallback);

                string uuid = offlineMode ? Guid.NewGuid().ToString("N") : GenerateUuid();
                string accessToken = offlineMode ? Guid.NewGuid().ToString("N") : "invalid";

                // 日志目录：MNL\logs（参考 PCL {exe}\PCL\ 同级目录结构）
                string launcherLogDir = MNLEnvironment.LogsPath;
                string gameLogDir = Path.Combine(minecraftPath, "logs");
                try { if (!Directory.Exists(launcherLogDir)) Directory.CreateDirectory(launcherLogDir); } catch { }
                try { if (!Directory.Exists(gameLogDir)) Directory.CreateDirectory(gameLogDir); } catch { }

                // ★ 构建按行分隔的 classpath（不做命令行拼接，写入文件）
                string fullClasspath = jarPath + ";" + librariesClassPath;
                string cpArg = fullClasspath.Replace("/", "\\");
                logCallback?.Invoke($"[MNL] Classpath 长度: {fullClasspath.Length} 字符");

                // 检测 Java 主版本号（用于编码参数选择）
                int javaMajorVersion = GetJavaMajorVersion(javaPath);
                logCallback?.Invoke($"[MNL] Java 主版本: {javaMajorVersion}");

                // 启动前设置 Minecraft 语言为中文（参考 PCL ChineseLaunchHelper）
                SetupChineseLanguage(gameDir, gameVersion, logCallback);

                // 加载合并后的版本 JSON（含 inheritsFrom 链）
                StringBuilder arguments = new StringBuilder();
                using (var mergedDoc = LoadMergedVersionJson(gameVersion, versionsPath, logCallback))
                {
                    if (mergedDoc == null)
                        throw new Exception("无法加载版本 JSON：" + gameVersion);

                    var mergedRoot = mergedDoc.RootElement;

                    // 获取 mainClass（合并后的 JSON）
                    string mainClass = "net.minecraft.client.main.Main";
                    if (mergedRoot.TryGetProperty("mainClass", out JsonElement mc))
                        mainClass = mc.GetString();

                    // 构建 token 替换表（参考 PCL）
                    string width = "1920", height = "1080";
                    if (!string.IsNullOrEmpty(resolution))
                    {
                        string[] wh = resolution.Replace('x', 'x').Replace('X', 'x').Split('x');
                        if (wh.Length == 2) { width = wh[0].Trim(); height = wh[1].Trim(); }
                    }

                    var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["${auth_player_name}"] = username,
                        ["${auth_uuid}"] = uuid,
                        ["${auth_access_token}"] = accessToken,
                        ["${user_type}"] = "mojang",
                        ["${version_name}"] = gameVersion,
                        ["${game_directory}"] = gameDir,
                        ["${assets_root}"] = assetsPath,
                        ["${assets_index_name}"] = assetIndex,
                        ["${game_assets}"] = assetsPath,
                        ["${library_directory}"] = librariesPath,
                        ["${natives_directory}"] = nativesPath,
                        ["${classpath}"] = $"\"{cpArg}\"",
                        ["${launcher_name}"] = "MNL",
                        ["${launcher_version}"] = "1.0",
                        ["${resolution_width}"] = width,
                        ["${resolution_height}"] = height,
                        ["${version_type}"] = "MNL",
                        ["${user_properties}"] = "{}",
                        ["${classpath_separator}"] = ";",
                        ["${clientid}"] = "",
                        ["${auth_xuid}"] = "",
                        ["${quickPlayPath}"] = "",
                        ["${quickPlaySingleplayer}"] = "",
                        ["${quickPlayMultiplayer}"] = "",
                        ["${quickPlayRealms}"] = "",
                    };

                    // 用 HashSet 去重 JVM 参数（参考 PCL DeduplicateJavaArguments）
                    var jvmArgsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    void AddJvmArg(string arg)
                    {
                        // 去重后添加
                        string trimmed = arg.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && jvmArgsSeen.Add(trimmed))
                            arguments.Append(trimmed + " ");
                    }

                    // === Log4j 安全修复（参考 PCL） ===
                    AddJvmArg("-Dlog4j2.formatMsgNoLookups=true ");
                    // === MNL 自身 JVM 参数 ===
                    AddJvmArg($"-Xmx{memoryMB}M");
                    AddJvmArg($"-Xms{memoryMB}M");
                    if (javaMajorVersion > 8)
                    {
                        // 参考 PCL：Java 9+ 使用 UTF-8 输出/文件编码
                        // 不使用 COMPAT（JDK-8214514：COMPAT=GBK 与 UTF-8 路径冲突）
                        AddJvmArg("-Dstdout.encoding=UTF-8");
                        AddJvmArg("-Dstderr.encoding=UTF-8");
                    }
                    if (javaMajorVersion > 8)
                    {
                        AddJvmArg("-Dfile.encoding=UTF-8");
                    }
                    AddJvmArg("-XX:+UnlockExperimentalVMOptions");
                    AddJvmArg("-XX:+UseG1GC");
                    AddJvmArg("-XX:G1NewSizePercent=20");
                    AddJvmArg("-XX:G1ReservePercent=20");
                    AddJvmArg("-XX:MaxGCPauseMillis=50");
                    AddJvmArg("-XX:G1HeapRegionSize=32M");
                    AddJvmArg("-XX:+DisableExplicitGC");

                    // 显式设置 natives 路径（MC 1.19+ JSON 不再包含此参数）
                    if (Directory.Exists(nativesPath) && !nativesPath.Contains(" "))
                    {
                        string normalizedNatives = nativesPath.Replace('\\', '/');
                        AddJvmArg($"-Djava.library.path={normalizedNatives}");
                    }

                    // === Forge/NeoForge/Fabric 需要继承父版本 jar 路径（参考 PCL -Dminecraft.client.jar） ===
                    if (mergedRoot.TryGetProperty("inheritsFrom", out JsonElement inheritsFromEl))
                    {
                        string parentVer = inheritsFromEl.GetString();
                        if (!string.IsNullOrEmpty(parentVer))
                        {
                            string parentJar = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(versionsPath), parentVer, parentVer + ".jar"));
                            if (File.Exists(parentJar))
                            {
                                AddJvmArg($"-Dminecraft.client.jar={parentJar.Replace('\\', '/')}");
                            }
                        }
                    }

                    // === 从版本 JSON 读取 JVM 参数（参考 PCL） ===
                    if (mergedRoot.TryGetProperty("arguments", out JsonElement args) &&
                        args.TryGetProperty("jvm", out JsonElement jvmArgs))
                    {
                        bool skipNextJvm = false;
                        foreach (JsonElement arg in jvmArgs.EnumerateArray())
                        {
                            if (!CheckJsonRules(arg)) continue;

                            if (arg.ValueKind == JsonValueKind.String)
                            {
                                string jvmArg = arg.GetString()?.Trim();
                                if (!string.IsNullOrEmpty(jvmArg))
                                {
                                    if (jvmArg == "-cp" || jvmArg == "-classpath")
                                    { skipNextJvm = true; continue; }
                                    if (skipNextJvm) { skipNextJvm = false; continue; }
                                    AddJvmArg(QuoteIfNeeded(ReplaceTokens(jvmArg, tokens)));
                                }
                            }
                            else if (arg.ValueKind == JsonValueKind.Object)
                            {
                                if (arg.TryGetProperty("value", out JsonElement val))
                                {
                                    AppendArgValueDedup(val, arguments, jvmArgsSeen, tokens);
                                }
                            }
                        }
                    }

                    // === 旧版 minecraftArguments（1.12 及之前） ===
                    if (!mergedRoot.TryGetProperty("arguments", out _) &&
                        mergedRoot.TryGetProperty("minecraftArguments", out JsonElement oldArgs))
                    {
                        string oldArgsStr = oldArgs.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(oldArgsStr))
                            AddJvmArg(QuoteIfNeeded(ReplaceTokens(oldArgsStr, tokens)));
                    }

                    // === -cp 和 mainClass（排在 JVM 参数最后） ===
                    jvmArgsSeen.Remove("-cp");
                    jvmArgsSeen.Remove("-classpath");
                    arguments.Append($"-cp \"{cpArg}\" ");
                    arguments.Append($"{mainClass} ");

                    // === 从版本 JSON 读取游戏参数 ===
                    if (mergedRoot.TryGetProperty("arguments", out JsonElement args2) &&
                        args2.TryGetProperty("game", out JsonElement gameArgs))
                    {
                        foreach (JsonElement arg in gameArgs.EnumerateArray())
                        {
                            if (!CheckJsonRules(arg)) continue;

                            if (arg.ValueKind == JsonValueKind.String)
                            {
                                string gameArg = arg.GetString()?.Trim();
                                if (!string.IsNullOrEmpty(gameArg))
                                    arguments.Append(QuoteIfNeeded(ReplaceTokens(gameArg, tokens)) + " ");
                            }
                            else if (arg.ValueKind == JsonValueKind.Object)
                            {
                                if (arg.TryGetProperty("value", out JsonElement val))
                                {
                                    AppendArgValue(val, arguments, tokens);
                                }
                            }
                        }
                    }
                    else
                    {
                        // 无 arguments.game 时，使用默认游戏参数
                        arguments.Append($"--username \"{username}\" ");
                        arguments.Append($"--version \"{gameVersion}\" ");
                        arguments.Append($"--gameDir \"{gameDir}\" ");
                        arguments.Append($"--assetsDir \"{assetsPath}\" ");
                        arguments.Append($"--assetIndex {assetIndex} ");
                        arguments.Append($"--uuid {uuid} ");
                        arguments.Append($"--accessToken {accessToken} ");
                        arguments.Append("--userType mojang ");
                        arguments.Append($"--width {width} ");
                        arguments.Append($"--height {height} ");
                        arguments.Append("--versionType MNL");
                        if (fullscreen) arguments.Append(" --fullscreen");
                    }

                    logCallback?.Invoke($"[MNL] 使用 Java: {javaPath}");
                    logCallback?.Invoke($"[MNL] 参数总长度: {arguments.Length} 字符");

                } // using mergedDoc

                // 保存调试文件到启动器自身日志目录
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string cpFile = Path.Combine(launcherLogDir, $"mnl-cp-{timestamp}.txt");
                string logFile = Path.Combine(launcherLogDir, $"mnl-launch-{timestamp}.log");

                // 保存 classpath 到独立文件（调试用）
                try { File.WriteAllText(cpFile, cpArg, Encoding.UTF8); } catch { }

                var startInfo = new ProcessStartInfo
                {
                    FileName = javaPath,
                    WorkingDirectory = gameDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true,
                };
                startInfo.Arguments = arguments.ToString();

                // ★ Path 环境变量（添加 Java bin 目录，参考 PCL）
                try
                {
                    string javaBinDir = Path.GetDirectoryName(javaPath);
                    string existingPath = startInfo.EnvironmentVariables["PATH"];
                    startInfo.EnvironmentVariables["PATH"] = string.IsNullOrEmpty(existingPath)
                        ? javaBinDir
                        : existingPath + ";" + javaBinDir;
                }
                catch { }

                Process process = Process.Start(startInfo);

                if (process == null)
                {
                    logCallback?.Invoke("[MNL] Process.Start 返回 null");
                    return false;
                }

                // ★ 崩溃检测状态（参考 PCL ModWatcher）
                bool crashDetected = false;
                string crashReason = null;
                var recentLogs = new List<string>();
                var recentLogsLock = new object();

                var logBuilder = new StringBuilder();
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        logBuilder.AppendLine(e.Data);
                        try { File.AppendAllText(logFile, "[OUT] " + e.Data + Environment.NewLine, Encoding.UTF8); } catch { }

                        // 崩溃关键字检测（参考 PCL ModWatcher.GameLog）
                        string reason;
                        if (CheckCrashKeyword(e.Data, out reason))
                        {
                            crashDetected = true;
                            if (crashReason == null) crashReason = reason;
                            logCallback?.Invoke($"[MNL] ⚠ 检测到崩溃信号: {reason}");
                        }

                        lock (recentLogsLock)
                        {
                            recentLogs.Add(e.Data);
                            if (recentLogs.Count > 200) recentLogs.RemoveAt(0);
                        }
                        logCallback?.Invoke(e.Data);
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        logBuilder.AppendLine(e.Data);
                        try { File.AppendAllText(logFile, "[ERR] " + e.Data + Environment.NewLine, Encoding.UTF8); } catch { }

                        string reason;
                        if (CheckCrashKeyword(e.Data, out reason))
                        {
                            crashDetected = true;
                            if (crashReason == null) crashReason = reason;
                            logCallback?.Invoke($"[MNL] ⚠ 检测到崩溃信号: {reason}");
                        }

                        lock (recentLogsLock)
                        {
                            recentLogs.Add(e.Data);
                            if (recentLogs.Count > 200) recentLogs.RemoveAt(0);
                        }
                        logCallback?.Invoke(e.Data);
                    }
                };
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    try
                    {
                        int exitCode = 0;
                        try { exitCode = process.ExitCode; } catch { }

                        if (crashDetected || exitCode != 0)
                        {
                            logCallback?.Invoke($"[MNL] ───── 游戏异常退出 (退出码: {exitCode}) ─────");

                            // 收集并分析崩溃日志（参考 PCL CrashAnalyzer）
                            string[] logsCopy;
                            lock (recentLogsLock) { logsCopy = recentLogs.ToArray(); }
                            var analysis = AnalyzeGameCrash(minecraftPath, gameVersion, crashReason, logsCopy);
                            foreach (var line in analysis)
                                logCallback?.Invoke(line);

                            logCallback?.Invoke("[MNL] ───── 崩溃分析完成，如需帮助请查看上方诊断信息 ─────");
                            // 发送崩溃标记，让前端更新状态
                            logCallback?.Invoke($"__MNL_CRASH__{exitCode}__{crashReason ?? "未知原因"}");
                        }
                        else
                        {
                            logCallback?.Invoke("[MNL] 游戏正常退出");
                        }
                    }
                    catch (Exception ex)
                    {
                        logCallback?.Invoke($"[MNL] 崩溃分析出错: {ex.Message}");
                    }
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 短暂等待，让 Java 进程真正初始化
                System.Threading.Thread.Sleep(2000);

                if (process.HasExited)
                {
                    // 立即退出 = 启动失败（Exited 事件可能已在上方处理）
                    int code = process.ExitCode;
                    logCallback?.Invoke($"[MNL] 进程已退出，退出码 {code}");
                    return false;
                }

                // 进程仍在运行，认为启动成功
                logCallback?.Invoke($"[MNL] 游戏进程已成功启动 (PID {process.Id})");
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("启动游戏失败：" + ex.Message, ex);
            }
        }

        /// <summary>拆分参数字符串（支持引号），用于 ArgumentList</summary>
        private static List<string> SplitArgs(string raw)
        {
            var args = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (!inQuotes && c == ' ')
                {
                    if (current.Length > 0) { args.Add(current.ToString()); current.Clear(); }
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0) args.Add(current.ToString());
            return args;
        }

        private void ExtractNatives(string librariesPath, string nativesPath, string gameVersion, string versionsPath)
        {
            try
            {
                // ★ 使用合并后的 JSON（含 inheritsFrom 链），确保 Forge/Fabric 等加载器版本的 natives 也被提取
                using (var mergedDoc = LoadMergedVersionJson(gameVersion, versionsPath, null))
                {
                    if (mergedDoc == null) return;

                    if (mergedDoc.RootElement.TryGetProperty("libraries", out JsonElement libraries))
                    {
                        bool is64Bit = Environment.Is64BitOperatingSystem;
                        foreach (JsonElement library in libraries.EnumerateArray())
                        {
                            if (!library.TryGetProperty("downloads", out JsonElement downloads))
                                continue;

                            string jarPath = null;

                            // 方式1: MC 1.19+ - natives 是独立条目，classifier 嵌在 name 里 (GAVC 格式)
                            if (library.TryGetProperty("name", out JsonElement libNameElement))
                            {
                                string libName = libNameElement.GetString() ?? "";
                                bool isWinNative = false;
                                if (is64Bit && libName.EndsWith(":natives-windows-x64")) isWinNative = true;
                                else if (libName.EndsWith(":natives-windows")) isWinNative = true;
                                else if (!is64Bit && libName.EndsWith(":natives-windows-x86")) isWinNative = true;

                                if (isWinNative && downloads.TryGetProperty("artifact", out JsonElement artifact) &&
                                    artifact.TryGetProperty("path", out JsonElement artPath))
                                {
                                    jarPath = artPath.GetString();
                                }

                                // arm64/x86 不需要提取（已在 IsLibraryApplicable 过滤），但也要跳过
                                if ((libName.EndsWith(":natives-windows-arm64") || libName.EndsWith(":natives-windows-x86")))
                                {
                                    if (!isWinNative) continue; // 非目标平台，跳过
                                }
                            }

                            // 方式2: MC < 1.19 - classifiers 中有 natives-windows 等键
                            if (jarPath == null && downloads.TryGetProperty("classifiers", out JsonElement classifiers))
                            {
                                JsonElement native = default;
                                if (is64Bit && classifiers.TryGetProperty("natives-windows-x64", out var n64)) native = n64;
                                else if (classifiers.TryGetProperty("natives-windows", out var n32)) native = n32;
                                else if (classifiers.TryGetProperty("natives-windows-x86", out var n86)) native = n86;

                                if (native.ValueKind != JsonValueKind.Undefined &&
                                    native.TryGetProperty("path", out JsonElement pathElement))
                                {
                                    jarPath = pathElement.GetString();
                                }
                            }

                            // 提取 DLL
                            if (!string.IsNullOrEmpty(jarPath))
                            {
                                string libPath = Path.Combine(librariesPath, jarPath);
                                if (File.Exists(libPath))
                                {
                                    try
                                    {
                                        using (var archive = System.IO.Compression.ZipFile.OpenRead(libPath))
                                        {
                                            foreach (var entry in archive.Entries)
                                            {
                                                if (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    string destPath = Path.Combine(nativesPath, entry.Name);
                                                    try { entry.ExtractToFile(destPath, true); } catch { }
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private string GetMainClass(string gameVersion, string versionsPath)
        {
            try
            {
                string jsonPath = Path.Combine(versionsPath, gameVersion + ".json");
                if (File.Exists(jsonPath))
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                    {
                        if (doc.RootElement.TryGetProperty("mainClass", out JsonElement mainClass))
                        {
                            return mainClass.GetString();
                        }
                    }
                }
            }
            catch { }
            return "net.minecraft.client.main.Main";
        }

        private string GenerateUuid()
        {
            return Guid.NewGuid().ToString();
        }

        private string GetAssetIndex(string gameVersion, string versionsPath)
        {
            try
            {
                // versionsPath = {minecraft}/versions/{gameVersion}，取上一层查找父版本 JSON
                string baseVersionsPath = Path.GetDirectoryName(versionsPath);
                if (string.IsNullOrEmpty(baseVersionsPath))
                    baseVersionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "versions");

                // 沿 inheritsFrom 链查找 assetIndex（Forge 版本通常继承自原版）
                string currentVersion = gameVersion;
                int depth = 0;
                while (depth < 20)
                {
                    string jsonPath = Path.Combine(baseVersionsPath, currentVersion, currentVersion + ".json");
                    if (!File.Exists(jsonPath)) break;

                    string jsonContent = File.ReadAllText(jsonPath);
                    Match match = Regex.Match(jsonContent, "\"assetIndex\"\\s*:\\s*\\{[^}]*\"id\"\\s*:\\s*\"([^\"]+)\"");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }

                    // 未找到，检查 inheritsFrom
                    Match inheritMatch = Regex.Match(jsonContent, "\"inheritsFrom\"\\s*:\\s*\"([^\"]+)\"");
                    if (inheritMatch.Success)
                    {
                        currentVersion = inheritMatch.Groups[1].Value;
                        depth++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch { }
            return gameVersion;
        }

        /// <summary>加载版本 JSON，递归合并 inheritsFrom 链（参考 PCL ModMinecraft.vb）</summary>
        public JsonDocument LoadMergedVersionJson(string gameVersion, string versionsPath, Action<string> logCallback)
        {
            // versionsPath = {minecraft}/versions/{gameVersion}，取上一层作为所有版本的根目录
            string baseVersionsPath = Path.GetDirectoryName(versionsPath);
            if (string.IsNullOrEmpty(baseVersionsPath))
                baseVersionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "versions");

            string currentVersion = gameVersion;
            var allLibraries = new List<JsonElement>();
            // 收集所有层级的 arguments（子版本覆盖父版本同名参数）
            var allJvmArgs = new List<JsonElement>();
            var allGameArgs = new List<JsonElement>();
            JsonDocument merged = null;
            int depth = 0;

            while (depth < 20) // 防止无限递归
            {
                string jsonPath = Path.Combine(baseVersionsPath, currentVersion, currentVersion + ".json");
                if (!File.Exists(jsonPath))
                {
                    logCallback?.Invoke($"[MNL] 继承版本 JSON 未找到: {jsonPath}");
                    break;
                }

                string jsonContent = File.ReadAllText(jsonPath);
                var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                // 收集当前版本的 libraries（子版本在前，父版本在后）
                if (root.TryGetProperty("libraries", out JsonElement libs))
                {
                    foreach (JsonElement lib in libs.EnumerateArray())
                    {
                        allLibraries.Add(lib); // Add 保证子版本在前（PCL 方式）
                    }
                }

                // 收集当前版本的 arguments（父版本在前，子版本在后追加/覆盖 — PCL 方式）
                if (root.TryGetProperty("arguments", out JsonElement args))
                {
                    if (args.TryGetProperty("jvm", out JsonElement jvm))
                    {
                        foreach (JsonElement arg in jvm.EnumerateArray())
                            allJvmArgs.Insert(0, arg); // 父版本 JVM 参数在前（子版本 Insert 到前面则后加）
                    }
                    if (args.TryGetProperty("game", out JsonElement game))
                    {
                        foreach (JsonElement arg in game.EnumerateArray())
                            allGameArgs.Insert(0, arg);
                    }
                }

                // 第一个版本（最子版本）的 mainClass 等字段保留
                if (merged == null)
                {
                    merged = doc;
                }

                // 检查 inheritsFrom
                if (root.TryGetProperty("inheritsFrom", out JsonElement inherits))
                {
                    string parent = inherits.GetString();
                    if (string.IsNullOrEmpty(parent) || parent == currentVersion)
                        break;
                    currentVersion = parent;
                    depth++;
                    if (merged != doc) doc.Dispose();
                }
                else
                {
                    break;
                }
            }

            // 将合并后的 libraries 写回 merged JSON（通过修改 JSON 字符串）
            if (merged != null && allLibraries.Count > 0)
            {
                try
                {
                    string mergedJson = merged.RootElement.GetRawText();
                    // 构建合并后的 libraries JSON 数组
                    var libArray = new StringBuilder();
                    libArray.Append('[');
                    for (int i = 0; i < allLibraries.Count; i++)
                    {
                        if (i > 0) libArray.Append(',');
                        libArray.Append(allLibraries[i].GetRawText());
                    }
                    libArray.Append(']');

                    // 替换 libraries 字段
                    string oldLibs = "\"libraries\"";
                    int libsIdx = mergedJson.IndexOf(oldLibs);
                    if (libsIdx >= 0)
                    {
                        // 找到 libraries 数组的结束位置
                        int start = mergedJson.IndexOf('[', libsIdx);
                        if (start >= 0)
                        {
                            int depth2 = 0;
                            int end = start;
                            bool inString = false;
                            for (int i = start; i < mergedJson.Length; i++)
                            {
                                char c = mergedJson[i];
                                if (c == '"' && (i == 0 || mergedJson[i - 1] != '\\')) inString = !inString;
                                if (!inString)
                                {
                                    if (c == '[') depth2++;
                                    else if (c == ']') { depth2--; if (depth2 == 0) { end = i + 1; break; } }
                                }
                            }
                            mergedJson = mergedJson.Substring(0, start) + libArray.ToString() + mergedJson.Substring(end);
                        }
                    }

                    merged.Dispose();
                    merged = JsonDocument.Parse(mergedJson);
                    logCallback?.Invoke($"[MNL] 库合并完成，总计 {allLibraries.Count} 个库");
                }
                catch (Exception ex)
                {
                    // 合并失败，使用原始 merged JSON（至少能启动）
                    logCallback?.Invoke($"[MNL] 库合并不完全: {ex.Message}");
                }
            }

            // === 合并 arguments（父版本在前，子版本在后，参考 PCL） ===
            if (merged != null && (allJvmArgs.Count > 0 || allGameArgs.Count > 0))
            {
                try
                {
                    string mergedJson = merged.RootElement.GetRawText();
                    // 防止 merged 内容不包含 "arguments" 键
                    if (!mergedJson.Contains("\"arguments\""))
                    {
                        // 在 libraries 后面插入 arguments
                        int insertIdx = mergedJson.LastIndexOf("]");
                        if (insertIdx > 0)
                        {
                            var sb = new StringBuilder();
                            sb.Append(mergedJson.Substring(0, insertIdx + 1));
                            sb.Append(",\"arguments\":{");
                            // JVM args
                            sb.Append("\"jvm\":[");
                            for (int i = 0; i < allJvmArgs.Count; i++)
                            {
                                if (i > 0) sb.Append(',');
                                sb.Append(allJvmArgs[i].GetRawText());
                            }
                            sb.Append("],");
                            // Game args
                            sb.Append("\"game\":[");
                            for (int i = 0; i < allGameArgs.Count; i++)
                            {
                                if (i > 0) sb.Append(',');
                                sb.Append(allGameArgs[i].GetRawText());
                            }
                            sb.Append("]}");
                            sb.Append(mergedJson.Substring(insertIdx + 1));
                            merged.Dispose();
                            merged = JsonDocument.Parse(sb.ToString());
                        }
                    }
                    else
                    {
                        // 替换现有 arguments 字段
                        // 找到 "arguments":{ ... }
                        int argIdx = mergedJson.IndexOf("\"arguments\"");
                        if (argIdx >= 0)
                        {
                            int braceStart = mergedJson.IndexOf('{', argIdx);
                            if (braceStart >= 0)
                            {
                                int braceDepth = 0;
                                int braceEnd = braceStart;
                                for (int i = braceStart; i < mergedJson.Length; i++)
                                {
                                    if (mergedJson[i] == '{') braceDepth++;
                                    else if (mergedJson[i] == '}') { braceDepth--; if (braceDepth == 0) { braceEnd = i + 1; break; } }
                                }
                                var sb = new StringBuilder();
                                sb.Append("{\"jvm\":[");
                                for (int i = 0; i < allJvmArgs.Count; i++)
                                {
                                    if (i > 0) sb.Append(',');
                                    sb.Append(allJvmArgs[i].GetRawText());
                                }
                                sb.Append("],\"game\":[");
                                for (int i = 0; i < allGameArgs.Count; i++)
                                {
                                    if (i > 0) sb.Append(',');
                                    sb.Append(allGameArgs[i].GetRawText());
                                }
                                sb.Append("]}");
                                mergedJson = mergedJson.Substring(0, braceStart) + sb.ToString() + mergedJson.Substring(braceEnd);
                                merged.Dispose();
                                merged = JsonDocument.Parse(mergedJson);
                            }
                        }
                    }
                    logCallback?.Invoke($"[MNL] 参数合并完成，JVM: {allJvmArgs.Count} Game: {allGameArgs.Count}");
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"[MNL] 参数合并不完全: {ex.Message}");
                }
            }

            logCallback?.Invoke($"[MNL] 版本 JSON 合并完成，继承深度: {depth}");
            return merged;
        }

        /// <summary>从合并后的版本 JSON 构建 classpath（参考 PCL，不使用文件系统扫描）</summary>
        private string GetLibrariesClassPath(string librariesPath, string gameVersion, string versionsPath, Action<string> logCallback)
        {
            var classPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNames = new HashSet<string>(); // 按 library name 去重（PCL 方式）

            using (var mergedDoc = LoadMergedVersionJson(gameVersion, versionsPath, logCallback))
            {
                if (mergedDoc == null) return string.Empty;

                if (mergedDoc.RootElement.TryGetProperty("libraries", out JsonElement libraries))
                {
                    foreach (JsonElement library in libraries.EnumerateArray())
                    {
                        // 过滤 native jar（只含 DLL，无 Java 类，由 ExtractNatives 处理）
                        if (IsNativeLibrary(library))
                            continue;

                        // PCL 去重：按 name 字段去重，保留先出现的（子版本优先）
                        string name = null;
                        if (library.TryGetProperty("name", out JsonElement nameEl))
                            name = nameEl.GetString();

                        if (!string.IsNullOrEmpty(name))
                        {
                            if (!seenNames.Add(name))
                                continue; // 已存在，跳过
                        }

                        string libPath = GetLibraryPathFromJson(library);
                        if (!string.IsNullOrEmpty(libPath))
                        {
                            string fullPath = Path.GetFullPath(Path.Combine(librariesPath, libPath));
                            if (File.Exists(fullPath))
                            {
                                classPathSet.Add(fullPath);
                            }
                        }
                    }
                }
            }

            return string.Join(";", classPathSet);
        }

        /// <summary>判断 library 是否为 native jar（只含 DLL，不应加入 classpath）</summary>
        private static bool IsNativeLibrary(JsonElement library)
        {
            string libName = library.TryGetProperty("name", out var name) ? name.GetString() : "";

            // MC 1.19+ 格式：classifier 嵌入 name 如 org.lwjgl:lwjgl:3.3.1:natives-windows-x86
            // 所有 natives-* jar 都只含 DLL，需要过滤（DLL 由 ExtractNatives 处理）
            if (libName != null && libName.Contains(":natives-"))
                return true;

            // MC < 1.19 格式：检查 natives 属性
            if (library.TryGetProperty("natives", out _))
                return true;

            return false;
        }

        private string GetLibraryPathFromJson(JsonElement library)
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
                return ConvertLibraryNameToPath(nameElement.GetString());
            }

            return null;
        }

        private string ConvertLibraryNameToPath(string libName)
        {
            string[] parts = libName.Split(':');
            if (parts.Length != 3)
            {
                return string.Empty;
            }

            string groupId = parts[0].Replace('.', Path.DirectorySeparatorChar);
            string artifactId = parts[1];
            string version = parts[2];

            return Path.Combine(groupId, artifactId, version, $"{artifactId}-{version}.jar");
        }

        /// <summary>通过运行 java -version 获取 Java 主版本号</summary>
        public int GetJavaMajorVersion(string javaPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return 0;
                    string output = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(5000);
                    // java -version 输出到 stderr，格式如: openjdk version "21.0.1" 2023-10-17
                    Match m = Regex.Match(output, @"version\s+""?(\d+)");
                    if (m.Success) return int.Parse(m.Groups[1].Value);
                    // Java 1.8 格式: java version "1.8.0_401"
                    m = Regex.Match(output, @"version\s+""1\.(\d+)");
                    if (m.Success) return int.Parse(m.Groups[1].Value);
                }
            }
            catch { }
            // 退而求其次：从路径名推测
            return ParseJavaMajorFromPath(javaPath);
        }

        /// <summary>从 Java 路径名推测主版本号</summary>
        private int ParseJavaMajorFromPath(string javaPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(javaPath); // bin目录
                string home = Directory.GetParent(dir)?.FullName ?? "";
                string dirName = Path.GetFileName(home);
                Match m = Regex.Match(dirName, @"(?:jdk|jre)[-_]?(\d+)");
                if (m.Success) return int.Parse(m.Groups[1].Value);
                m = Regex.Match(dirName, @"(?:java)[-_]?(\d+)");
                if (m.Success) return int.Parse(m.Groups[1].Value);
            }
            catch { }
            return 0;
        }

        /// <summary>启动前设置 Minecraft options.txt 语言为中文（参考 PCL ChineseLaunchHelper）</summary>
        private void SetupChineseLanguage(string gameDir, string gameVersion, Action<string> logCallback)
        {
            try
            {
                string optionsPath = Path.Combine(gameDir, "options.txt");
                // 读取当前语言
                string currentLang = "none";
                if (File.Exists(optionsPath))
                {
                    foreach (string line in File.ReadAllLines(optionsPath))
                    {
                        if (line.StartsWith("lang:"))
                        {
                            currentLang = line.Substring(5).Trim();
                            break;
                        }
                    }
                }

                // 确定游戏主版本号
                int mcMajor = 0;
                Match m = Regex.Match(gameVersion, @"^1\.(\d+)");
                if (m.Success) mcMajor = int.Parse(m.Groups[1].Value);
                else
                {
                    // 可能是非标准版本名，尝试匹配
                    m = Regex.Match(gameVersion, @"(\d+)\.(\d+)");
                    if (m.Success) mcMajor = int.Parse(m.Groups[2].Value);
                }

                // PCL 版本兼容矩阵:
                // 1.0 以下: 无语言选项
                // 1.1~1.5: 需要 zh_CN (大写CN)，否则 NPE 崩溃
                // 1.6~1.10: 需要 zh_CN，zh_cn 会静默切回英文
                // 1.11~1.12: 需要 zh_cn，zh_CN 显示中文但语言菜单显示"英文"
                // 1.13+: 需要 zh_cn，zh_CN 自动切回英文
                string targetLang = "zh_cn";
                if (mcMajor >= 1 && mcMajor <= 10)
                {
                    targetLang = "zh_CN";
                }

                if (currentLang == targetLang)
                {
                    logCallback?.Invoke($"[MNL] options.txt 语言已是 {targetLang}，无需修改");
                    return;
                }

                // 写入 options.txt
                var lines = File.Exists(optionsPath)
                    ? File.ReadAllLines(optionsPath).ToList()
                    : new List<string>();

                // 先写 "-" 使缓存失效，再写真实值（参考 PCL #2385）
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith("lang:"))
                    {
                        lines[i] = $"lang:{targetLang}";
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    lines.Add($"lang:{targetLang}");
                }

                File.WriteAllLines(optionsPath, lines, Encoding.UTF8);
                logCallback?.Invoke($"[MNL] 已设置游戏语言: {currentLang} → {targetLang}");
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"[MNL] 设置中文语言失败: {ex.Message}");
            }
        }

        /// <summary>替换参数中的 ${token} 占位符（纯替换，不加引号）</summary>
        private static string ReplaceTokens(string input, Dictionary<string, string> tokens)
        {
            string result = input;
            foreach (var kv in tokens)
            {
                result = result.Replace(kv.Key, kv.Value);
            }
            return result;
        }

        /// <summary>如果参数含空格则加引号（参考 PCL）</summary>
        private static string QuoteIfNeeded(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return arg;
            if (arg.Contains(' ') && !arg.StartsWith("\"") && !arg.StartsWith("@"))
                return $"\"{arg}\"";
            return arg;
        }

        /// <summary>检查 JSON 参数中的 rules（参考 PCL McJsonRuleCheck）</summary>
        private static bool CheckJsonRules(JsonElement arg)
        {
            if (arg.ValueKind != JsonValueKind.Object) return true;
            if (!arg.TryGetProperty("rules", out JsonElement rules)) return true;

            bool allowed = false;
            foreach (JsonElement rule in rules.EnumerateArray())
            {
                bool ruleApplies = true;
                if (rule.TryGetProperty("os", out JsonElement os))
                {
                    if (os.TryGetProperty("name", out JsonElement osName))
                    {
                        string name = osName.GetString() ?? "";
                        bool isWindows = name.Equals("windows", StringComparison.OrdinalIgnoreCase);
                        bool isMac = name.Equals("osx", StringComparison.OrdinalIgnoreCase) ||
                                     name.Equals("macos", StringComparison.OrdinalIgnoreCase);
                        bool isLinux = name.Equals("linux", StringComparison.OrdinalIgnoreCase);

                        ruleApplies = (isWindows && OperatingSystem.IsWindows()) ||
                                      (isMac && OperatingSystem.IsMacOS()) ||
                                      (isLinux && OperatingSystem.IsLinux());
                    }
                    if (os.TryGetProperty("arch", out JsonElement osArch))
                    {
                        string arch = osArch.GetString() ?? "";
                        bool is64 = Environment.Is64BitOperatingSystem;
                        ruleApplies = ruleApplies && (
                            (arch == "x86" && !is64) ||
                            (arch == "x86_64" && is64));
                    }
                }
                // features 规则：MNL 不支持 demo 用户，有自定义分辨率
                if (rule.TryGetProperty("features", out JsonElement features))
                {
                    // is_demo_user: MNL 用户永远不是 demo → 规则的 features 条件不满足
                    if (features.TryGetProperty("is_demo_user", out JsonElement demo))
                        ruleApplies = ruleApplies && !demo.GetBoolean(); // demo=true 时 MNL 不匹配
                    // has_custom_resolution: 有自定义分辨率 → 匹配
                    else if (features.TryGetProperty("has_custom_resolution", out JsonElement res))
                        ruleApplies = ruleApplies && res.GetBoolean();
                    // 其他 features 不匹配（如 is_pirate、has_quick_plays_support）
                    else
                        ruleApplies = false;
                }

                if (ruleApplies)
                {
                    string action = "allow";
                    if (rule.TryGetProperty("action", out JsonElement act))
                        action = act.GetString() ?? "allow";
                    allowed = action.Equals("allow", StringComparison.OrdinalIgnoreCase);
                }
            }
            return allowed;
        }

        /// <summary>将 JSON 参数值追加到命令行（支持字符串或数组）</summary>
        private static void AppendArgValue(JsonElement val, StringBuilder sb, Dictionary<string, string> tokens)
        {
            if (val.ValueKind == JsonValueKind.String)
            {
                string v = val.GetString()?.Trim();
                if (!string.IsNullOrEmpty(v))
                {
                    if (v == "-cp" || v == "-classpath") return;
                    sb.Append(QuoteIfNeeded(ReplaceTokens(v, tokens)) + " ");
                }
            }
            else if (val.ValueKind == JsonValueKind.Array)
            {
                // 跳过 -cp / -classpath 数组（启动器自行管理 classpath）
                bool skipNext = false;
                foreach (JsonElement item in val.EnumerateArray())
                {
                    string s = item.GetString()?.Trim();
                    if (string.IsNullOrEmpty(s)) continue;
                    if (s == "-cp" || s == "-classpath") { skipNext = true; continue; }
                    if (skipNext) { skipNext = false; continue; }
                    sb.Append(QuoteIfNeeded(ReplaceTokens(s, tokens)) + " ");
                }
            }
        }

        private static void AppendArgValueDedup(JsonElement val, StringBuilder sb, HashSet<string> seen, Dictionary<string, string> tokens)
        {
            if (val.ValueKind == JsonValueKind.String)
            {
                string v = val.GetString()?.Trim();
                if (!string.IsNullOrEmpty(v))
                {
                    if (v == "-cp" || v == "-classpath") return;
                    string resolved = QuoteIfNeeded(ReplaceTokens(v, tokens));
                    if (seen.Add(resolved))
                        sb.Append(resolved + " ");
                }
            }
            else if (val.ValueKind == JsonValueKind.Array)
            {
                bool skipNext = false;
                foreach (JsonElement item in val.EnumerateArray())
                {
                    string s = item.GetString()?.Trim();
                    if (string.IsNullOrEmpty(s)) continue;
                    if (s == "-cp" || s == "-classpath") { skipNext = true; continue; }
                    if (skipNext) { skipNext = false; continue; }
                    string resolved = QuoteIfNeeded(ReplaceTokens(s, tokens));
                    if (seen.Add(resolved))
                        sb.Append(resolved + " ");
                }
            }
        }

        public int GetRecommendedMemory()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
                {
                    ulong totalMemory = 0;
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        totalMemory += Convert.ToUInt64(obj["Capacity"]);
                    }

                    ulong totalMB = totalMemory / (1024 * 1024);
                    ulong recommendedMB = totalMB / 4;

                    if (recommendedMB > 4096)
                    {
                        return 4096;
                    }
                    if (recommendedMB < 512)
                    {
                        return 512;
                    }

                    return (int)(recommendedMB / 128 * 128);
                }
            }
            catch
            {
                return 2048;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 崩溃处理（参考 PCL ModWatcher + ModCrash）
        // ═══════════════════════════════════════════════════════════

        /// <summary>崩溃关键字匹配表（参考 PCL ModCrash.AnalyzeCrit1）</summary>
        private static readonly (string Pattern, string Reason)[] CrashPatterns = new[]
        {
            // PCL 崩溃日志关键字
            ("Crash report saved to", "Minecraft 保存了崩溃报告"),
            ("This crash report has been saved to:", "Minecraft 保存了崩溃报告"),
            ("Could not save crash report to", "Minecraft 崩溃，无法保存崩溃报告"),
            ("/ERROR]: Unable to launch", "Forge 启动失败"),
            ("An exception was thrown, the game will display an error screen and halt.", "Forge 报错"),
            // Java 问题
            ("Unrecognized option:", "Java 虚拟机参数有误，请检查 JVM 参数设置"),
            ("because module java.base does not export", "Java 版本过高，不兼容当前游戏版本"),
            ("java.lang.OutOfMemoryError", "Minecraft 内存不足，请增加分配的内存"),
            ("Could not reserve enough space", "内存不足或使用了 32 位 Java"),
            ("Invalid maximum heap size", "可能使用了 32 位 Java，无法分配足够内存"),
            ("java.lang.NoSuchFieldException: ucp", "Java 版本过高，不兼容当前游戏"),
            ("java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory", "Java 版本过高"),
            // Mod 问题
            ("DuplicateModsFoundException", "Mod 重复安装，请删除重复的 Mod"),
            ("Found a duplicate mod", "Mod 重复安装"),
            ("Missing or unsupported mandatory dependencies:", "Mod 缺少前置或版本不匹配"),
            ("ModResolutionException: Duplicate", "Mod 重复安装"),
            ("Extracted mod jars found", "Mod 文件被解压了，请把 .jar 文件直接放入 mods 文件夹"),
            ("The directories below appear to be extracted jar files", "Mod 文件被解压了"),
            ("Invalid module name: '' is not a Java identifier", "有 Mod 文件名包含特殊字符"),
            ("maximum id range exceeded", "Mod 数量过多，超出 ID 限制"),
            // OptiFine / Forge
            ("OptiFine", "OptiFine 与当前 Forge 可能不兼容"),
            ("java.lang.NoSuchMethodError: 'void net.minecraftforge.client.gui.overlay.ForgeGui", "OptiFine 与 Forge 不兼容"),
            ("Cannot find launch target fmlclient", "Forge 安装不完整，请重新安装"),
            ("Invalid paths argument, contained no existing paths", "Forge 安装不完整"),
            ("Unsupported class file major version", "Java 版本不兼容当前游戏"),
            ("signer information does not match", "文件校验失败，部分库可能损坏"),
            // 显卡 / 驱动
            ("Pixel format not accelerated", "显卡驱动不支持，请更新显卡驱动"),
            ("The driver does not appear to support OpenGL", "显卡不支持 OpenGL"),
            ("EXCEPTION_ACCESS_VIOLATION", "显卡驱动问题导致崩溃，请更新显卡驱动"),
            ("1282: Invalid operation", "光影或资源包导致 OpenGL 错误"),
            ("Couldn't set pixel format", "显卡驱动不支持，无法设置像素格式"),
            // Mixin
            ("Mixin prepare failed", "Mod Mixin 注入失败"),
            ("Mixin apply failed", "Mod Mixin 注入失败"),
            ("MixinTransformerError", "Mod Mixin 注入失败"),
            // Mod 加载
            ("Caught exception from ", "特定 Mod 导致游戏崩溃"),
            ("Failed to create mod instance", "Mod 初始化失败"),
            ("Incompatible mods found!", "安装了互不兼容的 Mod"),
            // 文件损坏
            ("zip END header not found", "Mod 文件损坏（zip 格式错误），请清理 mods 文件夹"),
            ("Not enough data available", "Mod 配置文件损坏（NightConfig Bug）"),
            ("java.util.zip.ZipException", "JAR 文件损坏，请检查 mods 文件夹"),
            // Fabric
            ("A potential solution has been determined:", "Fabric 给出了解决方案"),
            ("Mod resolution failed", "Mod 解析失败"),
            // 其他
            ("Someone is closing me!", "游戏被外部程序关闭"),
        };

        /// <summary>检查单行日志是否包含崩溃关键字（参考 PCL）</summary>
        private static bool CheckCrashKeyword(string line, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(line)) return false;

            foreach (var (pattern, desc) in CrashPatterns)
            {
                if (line.Contains(pattern))
                {
                    reason = desc;
                    return true;
                }
            }
            return false;
        }

        /// <summary>崩溃日志收集与分析（参考 PCL CrashAnalyzer）</summary>
        private static List<string> AnalyzeGameCrash(string minecraftPath, string gameVersion, string crashReason, string[] recentLogs)
        {
            var result = new List<string>();

            // 1. 崩溃原因（由日志关键字检测到的）
            if (!string.IsNullOrEmpty(crashReason))
            {
                result.Add($"[MNL] 崩溃原因: {crashReason}");
                result.Add($"[MNL] 建议: {GetCrashSuggestion(crashReason)}");
            }

            // 2. 尝试读取 crash-reports 文件夹下的最新崩溃报告
            try
            {
                string versionPath = Path.Combine(minecraftPath, "versions", gameVersion);
                string crashReportsDir = Path.Combine(minecraftPath, "crash-reports");

                // PCL 兼容：也检查版本隔离目录
                if (!Directory.Exists(crashReportsDir))
                    crashReportsDir = Path.Combine(versionPath, "crash-reports");

                if (Directory.Exists(crashReportsDir))
                {
                    var crashFiles = Directory.GetFiles(crashReportsDir, "crash-*.txt")
                        .Select(f => new FileInfo(f))
                        .Where(f => (DateTime.Now - f.LastWriteTime).TotalMinutes < 5)
                        .OrderByDescending(f => f.LastWriteTime)
                        .ToList();

                    if (crashFiles.Count > 0)
                    {
                        var latest = crashFiles[0];
                        result.Add($"[MNL] 崩溃报告: {latest.Name} ({latest.LastWriteTime:HH:mm:ss})");

                        // 提取崩溃报告中的关键信息（前 50 行）
                        try
                        {
                            var lines = File.ReadAllLines(latest.FullName);
                            result.Add("[MNL] ── 崩溃报告摘要 ──");
                            int shown = 0;
                            foreach (var line in lines)
                            {
                                if (shown >= 50) break;
                                // 跳过空行和纯描述性内容
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                result.Add($"[MNL]   {line}");
                                shown++;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // 3. 尝试读取 latest.log 中的最后错误信息
            try
            {
                string logsDir = Path.Combine(minecraftPath, "logs");
                string latestLog = Path.Combine(logsDir, "latest.log");
                if (File.Exists(latestLog))
                {
                    var info = new FileInfo(latestLog);
                    if ((DateTime.Now - info.LastWriteTime).TotalMinutes < 5)
                    {
                        var lines = File.ReadAllLines(latestLog);
                        result.Add("[MNL] ── latest.log 末尾 ──");
                        int start = Math.Max(0, lines.Length - 30);
                        for (int i = start; i < lines.Length; i++)
                        {
                            result.Add($"[MNL]   {lines[i]}");
                        }
                    }
                }
            }
            catch { }

            // 4. 如果没有找到崩溃日志，输出最后几行实时日志
            if (result.Count <= 2 && recentLogs != null && recentLogs.Length > 0)
            {
                result.Add("[MNL] ── 最后实时日志 ──");
                int start = Math.Max(0, recentLogs.Length - 20);
                for (int i = start; i < recentLogs.Length; i++)
                {
                    result.Add($"[MNL]   {recentLogs[i]}");
                }
            }

            // 5. 输出从堆栈中提取的可能 Mod 关键字
            if (recentLogs != null)
            {
                var keywords = ExtractModKeywordsFromLogs(recentLogs);
                if (keywords.Count > 0)
                {
                    result.Add($"[MNL] 可能与以下 Mod 有关: {string.Join(", ", keywords)}");
                }
            }

            return result;
        }

        /// <summary>从日志堆栈中提取可能的 Mod 关键字（参考 PCL AnalyzeStackKeyword）</summary>
        private static List<string> ExtractModKeywordsFromLogs(string[] logs)
        {
            var keywords = new HashSet<string>();
            // 需要排除的 Minecraft 自带包名
            var ignorePrefixes = new HashSet<string>
            {
                "java", "sun", "javax", "jdk", "oolloo",
                "org.lwjgl", "com.sun", "net.minecraftforge", "net.minecraft", "cpw.mods",
                "com.mojang", "com.google", "org.apache", "org.spongepowered", "net.fabricmc",
                "it.unimi.dsi", "paulscode.sound", "com.mumfrey",
            };
            var ignoreWords = new HashSet<string>
            {
                "com", "org", "net", "asm", "fml", "mod", "jar", "sun", "lib", "map", "gui",
                "dev", "nio", "api", "dsi", "top", "mcp", "core", "init", "mods", "main",
                "file", "game", "load", "read", "done", "util", "tile", "item", "base",
                "fake", "oshi", "impl", "data", "pool", "task", "forge", "setup", "block",
                "model", "mixin", "event", "unimi", "netty", "world", "lwjgl", "fakes",
                "fabric", "gitlab", "common", "server", "config", "mixins", "compat",
                "loader", "launch", "script", "entity", "client", "plugin", "mojang",
                "shader", "events", "github", "recipe", "render", "packet", "handler",
                "content", "systems", "modules", "service", "scripts", "network",
                "fastutil", "optifine", "internal", "platform", "override", "fabricmc",
                "neoforge", "external", "injection", "listeners", "scheduler", "minecraft",
                "universal", "multipart", "neoforged", "microsoft", "transformer",
                "transformers", "minecraftforge", "blockentity", "spongepowered",
            };

            foreach (var line in logs)
            {
                // 匹配堆栈行: at xxx.xxx.xxx(...)
                var matches = Regex.Matches(line, @"(?<=at )[a-zA-Z_]+\w*\.[a-zA-Z_]+[\w\.]+(?=\.\w+\()");
                foreach (Match m in matches)
                {
                    var parts = m.Value.Split('.');
                    foreach (var part in parts)
                    {
                        if (part.Length <= 2) continue;
                        if (ignoreWords.Contains(part.ToLower())) continue;

                        bool ignored = false;
                        foreach (var prefix in ignorePrefixes)
                        {
                            if (m.Value.StartsWith(prefix))
                            {
                                ignored = true;
                                break;
                            }
                        }
                        if (!ignored && !keywords.Contains(part))
                            keywords.Add(part);
                    }
                }
                if (keywords.Count >= 5) break;
            }

            return keywords.ToList();
        }

        /// <summary>根据崩溃原因给出中文建议（参考 PCL GetAnalyzeResult）</summary>
        private static string GetCrashSuggestion(string reason)
        {
            if (reason.Contains("内存不足"))
                return "请在设置中增加游戏分配内存，或关闭其他占用内存的软件";
            if (reason.Contains("32 位 Java"))
                return "请更换为 64 位 Java，32 位 Java 无法分配足够内存";
            if (reason.Contains("Java 版本过高"))
                return "请在设置中更换为更低版本的 Java（如 Java 8/11/17）";
            if (reason.Contains("Java 虚拟机参数"))
                return "请检查高级设置中的 JVM 参数是否有误";
            if (reason.Contains("Mod 重复安装"))
                return "请检查 mods 文件夹，删除重复的 Mod 文件";
            if (reason.Contains("Mod 文件被解压"))
                return "请把 .jar 文件直接放入 mods 文件夹，不要解压";
            if (reason.Contains("Mod 文件名包含特殊字符"))
                return "请修改 Mod 文件名，只使用英文字母、数字、减号和下划线";
            if (reason.Contains("缺少前置"))
                return "请安装缺失的前置 Mod，查看崩溃报告获取具体 Mod 名称";
            if (reason.Contains("Mod 文件损坏") || reason.Contains("zip"))
                return "请清理 mods 文件夹中的损坏文件，或重新下载该 Mod";
            if (reason.Contains("Forge 安装不完整"))
                return "请重新安装 Forge，确保所有文件完整下载";
            if (reason.Contains("显卡驱动"))
                return "请更新显卡驱动到最新版本";
            if (reason.Contains("显卡不支持 OpenGL"))
                return "请更新显卡驱动，或检查是否使用了独立显卡";
            if (reason.Contains("Mixin 注入失败"))
                return "某个 Mod 的 Mixin 注入失败，请逐一排查最近安装的 Mod";
            if (reason.Contains("特定 Mod 导致"))
                return "请查看崩溃报告确定具体是哪个 Mod，然后禁用它";
            if (reason.Contains("互不兼容"))
                return "请查看崩溃报告确定不兼容的 Mod，移除其中一个";
            if (reason.Contains("Mod 数量过多"))
                return "请删除部分大型 Mod，或安装 JEID 等修复 Mod";
            if (reason.Contains("Java 版本不兼容"))
                return "请更换与游戏版本匹配的 Java（例如 1.12.2 用 Java 8，1.16.5+ 用 Java 17）";
            if (reason.Contains("Fabric 给出了解决方案"))
                return "请查看崩溃报告中的 Fabric 建议";
            if (reason.Contains("配置损坏"))
                return "请尝试删除该 Mod 的配置文件让其重新生成";
            if (reason.Contains("文件校验失败"))
                return "请重新下载游戏文件，或删除损坏的库文件";
            return "请查看上方崩溃报告获取详细信息";
        }

        /// <summary>前置校验：扫描 mods 文件夹，防止损坏文件导致 Forge 崩溃</summary>
        private static void ValidateModsFolder(string gameDir, Action<string> logCallback)
        {
            try
            {
                string modsPath = Path.Combine(gameDir, "mods");
                if (!Directory.Exists(modsPath))
                {
                    logCallback?.Invoke("[MNL] mods 文件夹不存在，跳过校验");
                    return;
                }

                var files = Directory.GetFiles(modsPath);
                var invalidFiles = new List<string>();

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string ext = Path.GetExtension(file).ToLower();
                    var fileInfo = new FileInfo(file);

                    // 跳过非文件
                    if ((fileInfo.Attributes & FileAttributes.Directory) != 0) continue;

                    // 检查：不是 .jar 也不是 .litemod 文件
                    if (ext != ".jar" && ext != ".litemod" && ext != ".disabled")
                    {
                        // 排除常见的非 Mod 文件
                        if (ext == ".txt" || ext == ".md" || ext == ".cfg" || ext == ".json") continue;
                        invalidFiles.Add($"{fileName} (非 Mod 文件，类型: {ext})");
                        continue;
                    }

                    // 检查：0 字节文件
                    if (fileInfo.Length == 0 && ext == ".jar")
                    {
                        invalidFiles.Add($"{fileName} (空文件，0 字节)");
                        continue;
                    }

                    // 检查：JAR 文件是否能作为 ZIP 打开（快速完整性检查）
                    if (ext == ".jar" && fileInfo.Length > 0)
                    {
                        try
                        {
                            using (var zip = ZipFile.OpenRead(file))
                            {
                                // 仅尝试读取，验证 ZIP 结构完整性
                                if (zip.Entries.Count == 0)
                                {
                                    invalidFiles.Add($"{fileName} (空的 JAR 文件)");
                                }
                            }
                        }
                        catch
                        {
                            invalidFiles.Add($"{fileName} (损坏的 JAR/ZIP 文件)");
                        }
                    }
                }

                if (invalidFiles.Count > 0)
                {
                    logCallback?.Invoke($"[MNL] ⚠ mods 文件夹中发现 {invalidFiles.Count} 个问题文件:");
                    foreach (var f in invalidFiles)
                        logCallback?.Invoke($"[MNL]   - {f}");
                    logCallback?.Invoke("[MNL] 建议删除或移走上述文件，否则可能导致游戏崩溃");
                }
                else
                {
                    logCallback?.Invoke($"[MNL] mods 文件夹校验通过 ({files.Length} 个文件)");
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"[MNL] mods 文件夹校验出错: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Java 版本兼容性检测（参考 PCL ModLaunch.vb McLaunchJava）
        // ═══════════════════════════════════════════════════════════

        /// <summary>根据游戏版本和版本 JSON 获取所需的 Java 版本范围</summary>
        public (int minMajor, int? maxMajor, string reason) GetRequiredJavaVersion(string gameVersion, JsonDocument mergedJson)
        {
            int minMajor = 8; // 默认最低 Java 8
            int? maxMajor = null; // 默认无上限

            try
            {
                var root = mergedJson?.RootElement ?? default;
                bool vaild = Version.TryParse(gameVersion, out Version mcVer);
                int vanillaMajor = vaild ? mcVer.Major : 0;
                int vanillaMinor = vaild ? mcVer.Minor : 0;

                // === 检测模组加载器 ===
                bool hasFabric = false, hasForge = false, hasNeoForge = false;
                bool hasOptiFine = false, hasLiteLoader = false;
                string forgeVersion = null;

                try
                {
                    if (root.TryGetProperty("arguments", out JsonElement args) &&
                        args.TryGetProperty("jvm", out JsonElement jvmArgs))
                    {
                        foreach (var arg in jvmArgs.EnumerateArray())
                        {
                            if (arg.ValueKind == JsonValueKind.String)
                            {
                                string s = arg.GetString() ?? "";
                                if (s.Contains("fabric-loader")) hasFabric = true;
                                if (s.Contains("forge-")) hasForge = true;
                                if (s.Contains("neoforge")) hasNeoForge = true;
                                if (s.Contains("liteloader")) hasLiteLoader = true;
                                if (s.Contains("OptiFine")) hasOptiFine = true;
                            }
                            else if (arg.ValueKind == JsonValueKind.Object &&
                                     arg.TryGetProperty("value", out JsonElement val))
                            {
                                string s = val.ValueKind == JsonValueKind.String ? val.GetString() ?? "" :
                                           val.ValueKind == JsonValueKind.Array && val.GetArrayLength() > 0
                                           ? (val[0].GetString() ?? "") : "";
                                if (s.Contains("fabric-loader")) hasFabric = true;
                                if (s.Contains("forge-")) hasForge = true;
                                if (s.Contains("neoforge")) hasNeoForge = true;
                                if (s.Contains("liteloader")) hasLiteLoader = true;
                                if (s.Contains("OptiFine")) hasOptiFine = true;
                            }
                        }
                    }

                    // 也从 libraries 检测（更可靠）
                    if (root.TryGetProperty("libraries", out JsonElement libs))
                    {
                        foreach (var lib in libs.EnumerateArray())
                        {
                            string name = "";
                            if (lib.TryGetProperty("name", out JsonElement n))
                                name = n.GetString() ?? "";
                            if (lib.TryGetProperty("Name", out n))
                                name = n.GetString() ?? "";

                            if (name.Contains("fabric-loader")) hasFabric = true;
                            if (name.Contains("forge-") || name.Contains(":forge:")) hasForge = true;
                            if (name.Contains("neoforge") || name.Contains("neo-forge")) hasNeoForge = true;
                            if (name.Contains("liteloader")) hasLiteLoader = true;
                            if (name.Contains("optifine") || name.Contains("OptiFine")) hasOptiFine = true;

                            // 提取 Forge 版本号
                            if (!hasForge && name.Contains("forge") && name.Contains(":"))
                            {
                                var parts = name.Split(':');
                                if (parts.Length >= 3)
                                {
                                    forgeVersion = parts[2]; // groupId:artifactId:version
                                    hasForge = true;
                                }
                            }
                        }
                    }
                }
                catch { }

                // === 从 JSON javaVersion 字段读取 Mojang 声明的 Java 要求（MC 1.20.5+） ===
                if (root.TryGetProperty("javaVersion", out JsonElement javaVerElement))
                {
                    if (javaVerElement.TryGetProperty("majorVersion", out JsonElement majorJ))
                    {
                        int mojVer = majorJ.GetInt32();
                        if (mojVer >= 22)
                        {
                            minMajor = Math.Max(minMajor, mojVer);
                        }
                    }
                }

                // === 原版版本映射 ===
                if (vaild && ((vanillaMajor == 1 && vanillaMinor >= 21) || vanillaMajor >= 21))
                {
                    minMajor = Math.Max(minMajor, 21);
                }
                else if (vanillaMajor == 1 && vanillaMinor >= 20 && (vaild && mcVer.Build >= 5 || !vaild))
                {
                    // 1.20.5+ 需要至少 Java 21
                    // 非标准版本(快照)也按 1.20.5 处理
                    minMajor = Math.Max(minMajor, 21);
                }
                else if (vanillaMajor == 1 && vanillaMinor >= 18 && vanillaMinor <= 20)
                {
                    // 1.18-1.20.4：需要 Java 17（Java 21+ 与 LWJGL 3.3.1 JNI 不兼容）
                    minMajor = Math.Max(minMajor, 17);
                    // 默认最高 Java 17，若有模组加载器（Forge/Fabric 特定版本）可覆盖此上限
                    if (!hasForge && !hasNeoForge && !hasFabric)
                        maxMajor = Math.Min(maxMajor ?? 999, 17);
                }
                else if (vanillaMajor == 1 && vanillaMinor >= 17)
                {
                    // 1.17+：至少 Java 16
                    minMajor = Math.Max(minMajor, 16);
                }
                else if (vanillaMajor == 1 && vanillaMinor >= 12)
                {
                    // 1.12+：至少 Java 8
                    minMajor = Math.Max(minMajor, 8);
                }
                else if (vanillaMajor == 1 && vanillaMinor <= 5)
                {
                    // 1.5.2-：最高 Java 8
                    maxMajor = Math.Min(maxMajor ?? 999, 8);
                }

                // === Forge 检测 ===
                if (hasForge)
                {
                    if (vanillaMajor == 1 && vanillaMinor >= 6 && vanillaMinor <= 7)
                    {
                        // 1.6.1-1.7.2：必须 Java 7
                        minMajor = Math.Max(minMajor, 7);
                        maxMajor = Math.Min(maxMajor ?? 999, 7);
                    }
                    else if (vaild && vanillaMajor == 1 && vanillaMinor <= 12)
                    {
                        // <=1.12：最高 Java 8
                        maxMajor = Math.Min(maxMajor ?? 999, 8);
                    }
                    else if (vanillaMajor == 1 && vanillaMinor <= 14)
                    {
                        // 1.13-1.14：Java 8-10
                        minMajor = Math.Max(minMajor, 8);
                        maxMajor = Math.Min(maxMajor ?? 999, 10);
                    }
                    else if (vanillaMajor == 1 && vanillaMinor == 15)
                    {
                        // 1.15：Java 8-15
                        minMajor = Math.Max(minMajor, 8);
                        maxMajor = Math.Min(maxMajor ?? 999, 15);
                    }
                    else if (hasOptiFine && vanillaMajor == 1 && vanillaMinor == 18)
                    {
                        // 1.18 + OptiFine：最高 Java 18
                        maxMajor = Math.Min(maxMajor ?? 999, 18);
                    }
                    else if (vaild && !string.IsNullOrEmpty(forgeVersion))
                    {
                        // 简化 Forge 版本判断
                        if (forgeVersion.Contains("45.0.") && !forgeVersion.Contains("45.0.66"))
                        {
                            // 1.19.4 Forge 45.0.21~45.0.65：最高 Java 19
                            maxMajor = Math.Min(maxMajor ?? 999, 19);
                        }
                        else
                        {
                            // 1.19.4-1.20.1 Forge 45.0.66-47.x：最高 Java 21
                            maxMajor = Math.Min(maxMajor ?? 999, 21);
                        }
                    }
                }

                // === NeoForge 检测 ===
                if (hasNeoForge)
                {
                    if ((vanillaMajor == 1 && vanillaMinor == 20 && (vaild && mcVer.Build <= 2 || !vaild)))
                    {
                        // 1.20.1-1.20.2：最高 Java 21
                        maxMajor = Math.Min(maxMajor ?? 999, 21);
                    }
                }

                // === Fabric 检测 ===
                if (hasFabric && vaild)
                {
                    if (vanillaMajor == 1 && vanillaMinor >= 15 && vanillaMinor <= 16)
                    {
                        // 1.15-1.16：Java 8+
                        minMajor = Math.Max(minMajor, 8);
                    }
                    else if (vanillaMajor == 1 && vanillaMinor >= 18)
                    {
                        // 1.18+：Java 17+
                        minMajor = Math.Max(minMajor, 17);
                    }
                }

                // === OptiFine 检测 ===
                if (hasOptiFine && vaild)
                {
                    if (vanillaMajor == 1 && vanillaMinor < 7)
                    {
                        // 1.7 以下：最高 Java 8
                        maxMajor = Math.Min(maxMajor ?? 999, 8);
                    }
                    else if (vanillaMajor == 1 && vanillaMinor >= 8 && vanillaMinor < 12)
                    {
                        // 1.8-1.11：必须恰好 Java 8
                        minMajor = Math.Max(minMajor, 8);
                        maxMajor = Math.Min(maxMajor ?? 999, 8);
                    }
                    else if (vanillaMajor == 1 && vanillaMinor == 12)
                    {
                        // 1.12：最高 Java 8
                        maxMajor = Math.Min(maxMajor ?? 999, 8);
                    }
                }

                // === LiteLoader 检测 ===
                if (hasLiteLoader && vaild)
                {
                    // LiteLoader 最高 Java 8
                    maxMajor = Math.Min(maxMajor ?? 999, 8);
                }

                // 构建原因描述
                string reason = BuildJavaReason(gameVersion, minMajor, maxMajor,
                    hasFabric, hasForge, hasNeoForge, hasOptiFine, hasLiteLoader);

                return (minMajor, maxMajor, reason);
            }
            catch
            {
                return (8, null, "无法确定版本需求，默认 Java 8+");
            }
        }

        private static string BuildJavaReason(string gameVersion, int minMajor, int? maxMajor,
            bool hasFabric, bool hasForge, bool hasNeoForge, bool hasOptiFine, bool hasLiteLoader)
        {
            var mods = new List<string>();
            if (hasForge) mods.Add("Forge");
            if (hasNeoForge) mods.Add("NeoForge");
            if (hasFabric) mods.Add("Fabric");
            if (hasOptiFine) mods.Add("OptiFine");
            if (hasLiteLoader) mods.Add("LiteLoader");

            string versionDesc = hasForge || hasNeoForge || hasFabric
                ? $"{gameVersion} ({string.Join(" + ", mods)})"
                : gameVersion;

            if (maxMajor.HasValue && maxMajor.Value < 999)
                return $"{versionDesc} 需要 Java {minMajor} ~ {maxMajor}";
            if (minMajor > 8)
                return $"{versionDesc} 至少需要 Java {minMajor}";
            return $"{versionDesc} 需要 Java 8+";
        }

        /// <summary>扫描系统所有 Java 安装，返回 (路径, 主版本号) 列表</summary>
        public List<(string path, int majorVersion)> ScanAllSystemJava()
        {
            var result = new List<(string path, int majorVersion)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. 注册表
            foreach (var java in DetectFromRegistry())
            {
                if (seen.Add(java.Path))
                {
                    int ver = GetJavaMajorVersion(java.Path);
                    if (ver > 0) result.Add((java.Path, ver));
                }
            }

            // 2. Program Files\Java
            foreach (var java in DetectFromProgramFiles())
            {
                if (seen.Add(java.Path))
                {
                    int ver = GetJavaMajorVersion(java.Path);
                    if (ver > 0) result.Add((java.Path, ver));
                }
            }

            // 3. 环境变量
            foreach (var java in DetectFromEnvironment())
            {
                if (seen.Add(java.Path))
                {
                    int ver = GetJavaMajorVersion(java.Path);
                    if (ver > 0) result.Add((java.Path, ver));
                }
            }

            // 4. MNL 自带 Java 目录
            try
            {
                string minecraftPath = _config.GetMinecraftPath();
                string mnlJavaDir = Path.Combine(minecraftPath, "java");
                if (Directory.Exists(mnlJavaDir))
                {
                    foreach (string dir in Directory.GetDirectories(mnlJavaDir))
                    {
                        string javaExe = Path.Combine(dir, "bin", "java.exe");
                        if (File.Exists(javaExe) && seen.Add(javaExe))
                        {
                            int ver = GetJavaMajorVersion(javaExe);
                            if (ver > 0) result.Add((javaExe, ver));
                        }
                    }
                }
            }
            catch { }

            // 按版本降序排序（优先选择高版本）
            result.Sort((a, b) => b.majorVersion.CompareTo(a.majorVersion));
            return result;
        }
    }
}

