using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;

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

        public List<DetectedJava> DetectInstalledJava()
        {
            List<DetectedJava> detectedJava = new List<DetectedJava>();
            StatusChanged?.Invoke("正在检测系统Java环境...");

            List<string> searchPaths = new List<string>();

            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                searchPaths.Add(javaHome);
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            searchPaths.Add(Path.Combine(programFiles, "Java"));
            searchPaths.Add(Path.Combine(programFilesX86, "Java"));

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            searchPaths.Add(Path.Combine(appData, "..", "Local", "Programs", "Eclipse Adoptium"));
            searchPaths.Add(Path.Combine(appData, "..", "Local", "Programs", "Amazon Corretto"));
            searchPaths.Add(Path.Combine(appData, "..", "Local", "Programs", "Microsoft"));

            foreach (string basePath in searchPaths)
            {
                if (Directory.Exists(basePath))
                {
                    try
                    {
                        foreach (string dir in Directory.GetDirectories(basePath))
                        {
                            string javaExe = Path.Combine(dir, "bin", "java.exe");
                            if (File.Exists(javaExe))
                            {
                                DetectedJava java = GetJavaInfo(javaExe);
                                if (java != null && !detectedJava.Exists(j => j.Path == java.Path))
                                {
                                    detectedJava.Add(java);
                                    LogReceived?.Invoke($"检测到Java: {java.Path} (版本: {java.MajorVersion}, 最大内存: {java.MaxMemoryMb}MB)");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogReceived?.Invoke($"搜索路径失败: {basePath} - {ex.Message}");
                    }
                }
            }

            string pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (string path in pathEnv.Split(';'))
                {
                    if (path.Contains("java", StringComparison.OrdinalIgnoreCase) && !path.Contains("jre", StringComparison.OrdinalIgnoreCase))
                    {
                        string javaExe = Path.Combine(path, "java.exe");
                        if (File.Exists(javaExe) && !detectedJava.Exists(j => j.Path.Equals(javaExe, StringComparison.OrdinalIgnoreCase)))
                        {
                            DetectedJava java = GetJavaInfo(javaExe);
                            if (java != null)
                            {
                                detectedJava.Add(java);
                                LogReceived?.Invoke($"检测到Java: {java.Path} (版本: {java.MajorVersion}, 最大内存: {java.MaxMemoryMb}MB)");
                            }
                        }
                    }
                }
            }

            if (detectedJava.Count == 0)
            {
                string defaultJava = FindJavaInPATH();
                if (!string.IsNullOrEmpty(defaultJava))
                {
                    DetectedJava java = GetJavaInfo(defaultJava);
                    if (java != null)
                    {
                        detectedJava.Add(java);
                        LogReceived?.Invoke($"在PATH中找到Java: {java.Path}");
                    }
                }
            }

            StatusChanged?.Invoke($"检测到 {detectedJava.Count} 个Java环境");
            return detectedJava;
        }

        private string FindJavaInPATH()
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
                    string output = p.StandardOutput.ReadLine();
                    p.WaitForExit();
                    if (!string.IsNullOrEmpty(output) && File.Exists(output))
                    {
                        return output;
                    }
                }
            }
            catch
            {
            }
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

                    if (major > 1 || (major == 1 && minor >= 17))
                    {
                        return 17;
                    }
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
