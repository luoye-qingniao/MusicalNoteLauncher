using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class JavaConfigManager
    {
        private readonly string _configPath;
        private JavaConfig _currentConfig;

        public JavaConfig CurrentConfig => _currentConfig;

        public event Action<string> StatusChanged;
        public event Action<string> LogReceived;

        public JavaConfigManager(string minecraftPath)
        {
            _configPath = Path.Combine(minecraftPath, "config", "java_config.json");
            LoadConfig();
        }

        public class JavaConfig
        {
            public string JavaPath { get; set; }
            public int MajorVersion { get; set; }
            public long MaxMemoryMb { get; set; }
            public bool IsAutoConfigured { get; set; }
            public string SelectedMinecraftVersion { get; set; }
        }

        public class DetectedJava
        {
            public string Path { get; set; }
            public int MajorVersion { get; set; }
            public string Version { get; set; }
            public long MaxMemoryMb { get; set; }
        }

        // PCL 风格关键词：用于递归搜索时的目录过滤
        private static readonly HashSet<string> _javaSearchKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "java", "jdk", "jre", "jvm", "jbr", "env", "run", "mc", "dragon", "well", "bin",
            "sdk", "candidate", "current", "software", "cache", "temp", "corretto", "roaming",
            "users", "craft", "program", "net", "oracle", "game", "file", "data", "server",
            "client", "mojang", "eclipse", "microsoft", "hotspot", "runtime", "x86", "x64",
            "arm", "forge", "optifine", "hmcl", "mod", "fabric", "download", "launch", "path",
            "version", "baka", "pcl", "zulu", "local", "packages", "4297127d64ec6", "1.", "jbr",
            // 中文关键词
            "环境", "软件", "世界", "游戏", "服务", "客户", "整合", "应用", "运行", "前置",
            "官启", "官方", "新建文件夹", "原版", "启动", "程序"
        };

        public List<DetectedJava> DetectInstalledJava()
        {
            var javaCandidates = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            StatusChanged?.Invoke("正在检测系统Java环境...");

            // 1. 从 PATH 和 JAVA_HOME 环境变量收集候选路径
            string pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
            string userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME") ?? "";
            string combinedEnv = (pathEnv + ";" + userPath + ";" + javaHome).Replace("\\", "\\").Replace("/", "\\");

            foreach (string rawPath in combinedEnv.Split(';'))
            {
                string trimmed = rawPath.Trim(' ', '"');
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (!trimmed.EndsWith("\\")) trimmed += "\\";
                string javawExe = Path.Combine(trimmed, "javaw.exe");
                if (File.Exists(javawExe))
                {
                    javaCandidates.TryAdd(Path.GetFullPath(javawExe), 0);
                }
            }

            // 2. 使用 where.exe 查找 PATH 中所有 java
            foreach (string javaPath in FindAllJavaInPath())
            {
                if (File.Exists(javaPath))
                    javaCandidates.TryAdd(Path.GetFullPath(javaPath), 0);
            }

            // 3. 遍历所有本地磁盘进行关键词递归搜索
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType == DriveType.Network || !drive.IsReady) continue;
                    JavaSearchFolder(drive.RootDirectory.FullName, javaCandidates, isFullSearch: false);
                }
                catch { }
            }

            // 4. 搜索用户目录和特殊路径
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // 用户目录（部分搜索）
            if (Directory.Exists(userProfile))
                JavaSearchFolder(userProfile, javaCandidates, isFullSearch: false);

            // .jdks 目录
            string jdksPath = Path.Combine(userProfile, ".jdks");
            if (Directory.Exists(jdksPath))
                JavaSearchFolder(jdksPath, javaCandidates, isFullSearch: true);

            // .sdkman 目录
            string sdkmanPath = Path.Combine(userProfile, ".sdkman", "candidates", "java");
            if (Directory.Exists(sdkmanPath))
                JavaSearchFolder(sdkmanPath, javaCandidates, isFullSearch: true);

            // 启动器自身目录
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(appDir))
                JavaSearchFolder(appDir, javaCandidates, isFullSearch: true);

            // Program Files 中的 Java 目录（完全搜索）
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pfJava = Path.Combine(programFiles, "Java");
            string pfx86Java = Path.Combine(programFilesX86, "Java");
            if (Directory.Exists(pfJava))
                JavaSearchFolder(pfJava, javaCandidates, isFullSearch: true);
            if (Directory.Exists(pfx86Java))
                JavaSearchFolder(pfx86Java, javaCandidates, isFullSearch: true);

            // Common third-party Java distribution paths
            var thirdPartyDirs = new[]
            {
                Path.Combine(localAppData, "Programs", "Eclipse Adoptium"),
                Path.Combine(localAppData, "Programs", "Amazon Corretto"),
                Path.Combine(localAppData, "Programs", "Microsoft"),
                Path.Combine(localAppData, "Programs", "Zulu"),
                Path.Combine(programFiles, "Eclipse Adoptium"),
                Path.Combine(programFiles, "Amazon Corretto"),
                Path.Combine(programFiles, "Zulu"),
                Path.Combine(programFiles, "Microsoft"),
                Path.Combine(programFiles, "Oracle", "Java"),
                Path.Combine(programFilesX86, "Oracle", "Java"),
            };
            foreach (string dir in thirdPartyDirs)
            {
                if (Directory.Exists(dir))
                    JavaSearchFolder(dir, javaCandidates, isFullSearch: true);
            }

            // 5. 多线程验证候选 Java
            var detectedJava = new ConcurrentBag<DetectedJava>();
            int total = javaCandidates.Count;
            int processed = 0;

            Parallel.ForEach(javaCandidates.Keys, new ParallelOptions { MaxDegreeOfParallelism = 4 }, javaPath =>
            {
                try
                {
                    // 将 javaw.exe 转换为 java.exe
                    string exePath = javaPath;
                    if (exePath.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase))
                        exePath = Path.Combine(Path.GetDirectoryName(exePath) ?? "", "java.exe");
                    if (!File.Exists(exePath)) return;

                    DetectedJava java = GetJavaInfo(exePath);
                    if (java != null)
                    {
                        detectedJava.Add(java);
                        int current = Interlocked.Increment(ref processed);
                        StatusChanged?.Invoke($"验证Java ({current}/{total}): Java {java.MajorVersion}");
                    }
                }
                catch { }
            });

            var result = detectedJava
                .GroupBy(j => j.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(j => j.MajorVersion)
                .ToList();

            // 6. 如果没找到，回退到简单检测
            if (result.Count == 0)
            {
                string defaultJava = FindFirstJavaInPath();
                if (!string.IsNullOrEmpty(defaultJava))
                {
                    DetectedJava java = GetJavaInfo(defaultJava);
                    if (java != null) result.Add(java);
                }
            }

            foreach (var j in result)
                LogReceived?.Invoke($"检测到Java: {j.Path} (版本: {j.MajorVersion}, 最大内存: {j.MaxMemoryMb}MB)");

            StatusChanged?.Invoke($"检测到 {result.Count} 个Java环境");
            return result;
        }

        /// <summary>
        /// 关键词引导的递归目录搜索（PCL风格）
        /// </summary>
        private void JavaSearchFolder(string folderPath, ConcurrentDictionary<string, byte> results, bool isFullSearch)
        {
            try
            {
                if (!Directory.Exists(folderPath)) return;

                // 检查当前目录是否包含 javaw.exe
                string javawExe = Path.Combine(folderPath, "javaw.exe");
                string javaExe = Path.Combine(folderPath, "java.exe");
                if (File.Exists(javawExe))
                {
                    results.TryAdd(Path.GetFullPath(javawExe), 0);
                }
                else if (File.Exists(javaExe))
                {
                    results.TryAdd(Path.GetFullPath(javaExe), 0);
                }

                // 枚举子目录，关键词过滤
                foreach (string subDir in Directory.GetDirectories(folderPath))
                {
                    try
                    {
                        DirectoryInfo di = new DirectoryInfo(subDir);
                        if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue; // 跳过符号链接

                        string dirName = di.Name;

                        // 父目录为 users 时总是递归
                        string parentName = di.Parent?.Name ?? "";
                        bool alwaysRecurse = parentName.Equals("users", StringComparison.OrdinalIgnoreCase);

                        // 数字目录总是递归
                        bool isNumericDir = int.TryParse(dirName, out int _);

                        // 关键词匹配
                        bool keywordMatch = _javaSearchKeywords.Any(k =>
                            dirName.StartsWith(k, StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals(k, StringComparison.OrdinalIgnoreCase));

                        bool isBinDir = dirName.Equals("bin", StringComparison.OrdinalIgnoreCase);

                        if (alwaysRecurse || isNumericDir || keywordMatch || isBinDir || isFullSearch)
                        {
                            JavaSearchFolder(subDir, results, isFullSearch);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// 使用 where.exe 查找 PATH 中所有 java 路径
        /// </summary>
        private List<string> FindAllJavaInPath()
        {
            var results = new List<string>();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "java",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return results;
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && File.Exists(trimmed))
                            results.Add(trimmed);
                    }
                }
            }
            catch { }
            return results;
        }

        /// <summary>
        /// 回退：查找 PATH 中第一个 java
        /// </summary>
        private string FindFirstJavaInPath()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "java",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return null;
                    string output = p.StandardOutput.ReadLine();
                    p.WaitForExit();
                    if (!string.IsNullOrEmpty(output) && File.Exists(output))
                        return output;
                }
            }
            catch { }
            return null;
        }

        private DetectedJava GetJavaInfo(string javaPath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-Xmx128M -version",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    string errorOutput = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    int majorVersion = 8;
                    string fullVersion = "Unknown";

                    Match versionMatch = Regex.Match(errorOutput, @"version ""([^""]+)""");
                    if (versionMatch.Success)
                    {
                        fullVersion = versionMatch.Groups[1].Value;
                        Match majorMatch = Regex.Match(fullVersion, @"(\d+)");
                        if (majorMatch.Success)
                        {
                            int parsedMajor = int.Parse(majorMatch.Groups[1].Value);
                            if (parsedMajor == 1 && fullVersion.StartsWith("1.8"))
                            {
                                majorVersion = 8;
                            }
                            else if (parsedMajor >= 9)
                            {
                                majorVersion = parsedMajor;
                            }
                            else
                            {
                                majorVersion = parsedMajor;
                            }
                        }
                    }

                    long maxMemory = GetJavaMaxMemory(javaPath);

                    return new DetectedJava
                    {
                        Path = javaPath,
                        MajorVersion = majorVersion,
                        Version = fullVersion,
                        MaxMemoryMb = maxMemory
                    };
                }
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"获取Java信息失败: {javaPath} - {ex.Message}");
                return null;
            }
        }

        private long GetJavaMaxMemory(string javaPath)
        {
            try
            {
                long installedMemory = GetInstalledMemoryMb();

                if (installedMemory >= 16000)
                    return 12288;
                else if (installedMemory >= 8000)
                    return 6144;
                else if (installedMemory >= 4000)
                    return 3072;
                else
                    return 2048;
            }
            catch
            {
                return 2048;
            }
        }

        private long GetInstalledMemoryMb()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        return Convert.ToInt64(obj["TotalVisibleMemorySize"]) / 1024;
                    }
                }
                return 8192;
            }
            catch
            {
                return 8192;
            }
        }

        public int GetRecommendedJavaVersion(string minecraftVersion)
        {
            try
            {
                string[] parts = minecraftVersion.Split('.');
                if (parts.Length >= 2)
                {
                    int major = int.Parse(parts[0]);
                    int minor = int.Parse(parts[1]);
                    int patch = 0;
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int p))
                    {
                        patch = p;
                    }

                    // 1.20.5 及以上版本需要 Java 21
                    if (major > 1 || (major == 1 && minor > 20) || (major == 1 && minor == 20 && patch >= 5))
                    {
                        return 21;
                    }
                    // 1.17 - 1.20.4 使用 Java 17
                    else if (major == 1 && minor >= 17)
                    {
                        return 17;
                    }
                    // 1.9 - 1.16 使用 Java 11
                    else if (major == 1 && minor >= 9)
                    {
                        return 11;
                    }
                    else
                    {
                        return 8;
                    }
                }
            }
            catch
            {
            }
            return 8;
        }

        public bool ValidateJavaPath(string javaPath)
        {
            if (string.IsNullOrEmpty(javaPath))
                return false;

            string actualJavaPath = javaPath;

            if (File.Exists(javaPath))
            {
                actualJavaPath = javaPath;
            }
            else if (Directory.Exists(javaPath))
            {
                string possibleJavaPath = Path.Combine(javaPath, "bin", "java.exe");
                if (File.Exists(possibleJavaPath))
                {
                    actualJavaPath = possibleJavaPath;
                }
                else
                {
                    foreach (string subDir in Directory.GetDirectories(javaPath))
                    {
                        possibleJavaPath = Path.Combine(subDir, "bin", "java.exe");
                        if (File.Exists(possibleJavaPath))
                        {
                            actualJavaPath = possibleJavaPath;
                            break;
                        }
                    }
                }
            }

            if (!File.Exists(actualJavaPath))
                return false;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = actualJavaPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(3000);
                    return p.ExitCode == 0 || p.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }

        public void SetJavaPath(string javaPath)
        {
            if (string.IsNullOrEmpty(javaPath))
            {
                throw new Exception("Java路径不能为空");
            }

            string actualJavaPath = javaPath;

            if (File.Exists(javaPath))
            {
                actualJavaPath = javaPath;
            }
            else if (Directory.Exists(javaPath))
            {
                string possibleJavaPath = Path.Combine(javaPath, "bin", "java.exe");
                if (File.Exists(possibleJavaPath))
                {
                    actualJavaPath = possibleJavaPath;
                }
                else
                {
                    foreach (string subDir in Directory.GetDirectories(javaPath))
                    {
                        possibleJavaPath = Path.Combine(subDir, "bin", "java.exe");
                        if (File.Exists(possibleJavaPath))
                        {
                            actualJavaPath = possibleJavaPath;
                            break;
                        }
                    }
                }
            }

            if (!File.Exists(actualJavaPath))
            {
                throw new Exception("无效的Java路径");
            }

            DetectedJava java = GetJavaInfo(actualJavaPath);
            if (java == null)
            {
                throw new Exception("无法获取Java信息");
            }

            _currentConfig = new JavaConfig
            {
                JavaPath = actualJavaPath,
                MajorVersion = java.MajorVersion,
                MaxMemoryMb = java.MaxMemoryMb,
                IsAutoConfigured = false
            };

            SaveConfig();
            StatusChanged?.Invoke($"已设置Java路径: {actualJavaPath}");
            LogReceived?.Invoke($"Java版本: {java.MajorVersion}, 最大内存: {java.MaxMemoryMb}MB");
        }

        public void SetAutoConfig(DetectedJava java)
        {
            _currentConfig = new JavaConfig
            {
                JavaPath = java.Path,
                MajorVersion = java.MajorVersion,
                MaxMemoryMb = java.MaxMemoryMb,
                IsAutoConfigured = true
            };

            SaveConfig();
            StatusChanged?.Invoke($"已自动配置Java: {java.Path}");
            LogReceived?.Invoke($"自动配置完成 - Java {java.MajorVersion}, 最大内存: {java.MaxMemoryMb}MB");
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    _currentConfig = JsonSerializer.Deserialize<JavaConfig>(json);
                    LogReceived?.Invoke($"已加载Java配置: {_currentConfig?.JavaPath}");
                }
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"加载Java配置失败: {ex.Message}");
            }

            if (_currentConfig == null)
            {
                _currentConfig = new JavaConfig
                {
                    JavaPath = "java",
                    MajorVersion = 8,
                    MaxMemoryMb = 4096,
                    IsAutoConfigured = false
                };
            }
        }

        public void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                string json = JsonSerializer.Serialize(_currentConfig);
                File.WriteAllText(_configPath, json);
                LogReceived?.Invoke("Java配置已保存");
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"保存Java配置失败: {ex.Message}");
            }
        }

        public string GetJavaPath()
        {
            return _currentConfig?.JavaPath ?? "java";
        }

        public long GetMaxMemoryMb()
        {
            return _currentConfig?.MaxMemoryMb ?? 4096;
        }

        public int GetJavaVersion()
        {
            return _currentConfig?.MajorVersion ?? 8;
        }

        public string GetSelectedMinecraftVersion()
        {
            return _currentConfig?.SelectedMinecraftVersion ?? "1.20.1";
        }

        public void SetSelectedMinecraftVersion(string version)
        {
            if (_currentConfig != null)
            {
                _currentConfig.SelectedMinecraftVersion = version;
                SaveConfig();
            }
        }
    }
}
