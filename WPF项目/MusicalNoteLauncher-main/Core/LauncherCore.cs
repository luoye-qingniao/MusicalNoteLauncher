using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

        public bool LaunchGame(string javaPath, string gameVersion, int memoryMB, string username, bool offlineMode, Action<string> logCallback = null)
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
                string jarPath = Path.Combine(versionsPath, gameVersion + ".jar");
                // 浣跨敤鐗堟湰鐙珛鐨刵atives鐩綍
                string nativesPath = Path.Combine(versionsPath, $"{gameVersion}-natives");
                string librariesPath = Path.Combine(minecraftPath, "libraries");
                string assetsPath = Path.Combine(minecraftPath, "assets");

                if (!Directory.Exists(nativesPath))
                {
                    Directory.CreateDirectory(nativesPath);
                    ExtractNatives(librariesPath, nativesPath, gameVersion, versionsPath);
                }

                string assetIndex = GetAssetIndex(gameVersion, versionsPath);
                string librariesClassPath = GetLibrariesClassPath(librariesPath, gameVersion, versionsPath);

                string uuid = offlineMode ? Guid.NewGuid().ToString("N") : GenerateUuid();
                string accessToken = offlineMode ? Guid.NewGuid().ToString("N") : "invalid";

                string mainClass = GetMainClass(gameVersion, versionsPath);

                StringBuilder arguments = new StringBuilder();
                arguments.Append($"-Xmx{memoryMB}M -Xms{memoryMB}M ");
                arguments.Append("-Dfile.encoding=UTF-8 ");
                arguments.Append("-Dstdout.encoding=UTF-8 ");
                arguments.Append("-Dstderr.encoding=UTF-8 ");
                arguments.Append("-XX:+UnlockExperimentalVMOptions ");
                arguments.Append("-XX:+UseG1GC ");
                arguments.Append("-XX:G1NewSizePercent=20 ");
                arguments.Append("-XX:G1ReservePercent=20 ");
                arguments.Append("-XX:MaxGCPauseMillis=50 ");
                arguments.Append("-XX:G1HeapRegionSize=32M ");
                arguments.Append("-XX:+DisableExplicitGC ");
                arguments.Append($"-Dminecraft.client.jar=\"{jarPath}\" ");
                arguments.Append("-Dminecraft.launcher.brand=MusicalNoteLauncher ");
                arguments.Append("-Dminecraft.launcher.version=1.0 ");
                arguments.Append($"-Djava.library.path=\"{nativesPath}\" ");
                arguments.Append($"-cp \"{jarPath};{librariesClassPath}\" ");
                arguments.Append($"{mainClass} ");
                arguments.Append($"--username {username} ");
                arguments.Append($"--version {gameVersion} ");
                arguments.Append($"--gameDir \"{minecraftPath}\" ");
                arguments.Append($"--assetsDir \"{assetsPath}\" ");
                arguments.Append($"--assetIndex {assetIndex} ");
                arguments.Append($"--uuid {uuid} ");
                arguments.Append($"--accessToken {accessToken} ");
                arguments.Append("--userType mojang ");
                arguments.Append("--versionType release");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = arguments.ToString(),
                    WorkingDirectory = minecraftPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                Process process = Process.Start(startInfo);
                
                if (process != null)
                {
                    process.WaitForExit(3000);
                    if (process.HasExited && process.ExitCode != 0)
                    {
                        throw new Exception($"娓告垙鍚姩澶辫触锛岃繘绋嬬珛鍗抽€€鍑?(閫€鍑虹爜: {process.ExitCode})");
                    }
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception("启动游戏失败：" + ex.Message, ex);
            }
        }

        private void ExtractNatives(string librariesPath, string nativesPath, string gameVersion, string versionsPath)
        {
            try
            {
                string jsonPath = Path.Combine(versionsPath, gameVersion + ".json");
                if (!File.Exists(jsonPath)) return;

                string jsonContent = File.ReadAllText(jsonPath);
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    if (doc.RootElement.TryGetProperty("libraries", out JsonElement libraries))
                    {
                        foreach (JsonElement library in libraries.EnumerateArray())
                        {
                            if (library.TryGetProperty("downloads", out JsonElement downloads) &&
                                downloads.TryGetProperty("classifiers", out JsonElement classifiers))
                            {
                                if (classifiers.TryGetProperty("natives-windows", out JsonElement native))
                                {
                                    if (native.TryGetProperty("path", out JsonElement pathElement))
                                    {
                                        string path = pathElement.GetString();
                                        if (!string.IsNullOrEmpty(path))
                                        {
                                            string libPath = Path.Combine(librariesPath, path);
                                            if (File.Exists(libPath))
                                            {
                                                try
                                                {
                                                    System.IO.Compression.ZipFile.ExtractToDirectory(libPath, nativesPath);
                                                }
                                                catch { }
                                            }
                                        }
                                    }
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
                string jsonPath = Path.Combine(versionsPath, gameVersion + ".json");
                if (File.Exists(jsonPath))
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    Match match = Regex.Match(jsonContent, "\"assetIndex\"\\s*:\\s*\\{[^}]*\"id\"\\s*:\\s*\"([^\"]+)\"");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
            catch
            {
            }
            return gameVersion;
        }

        private string GetLibrariesClassPath(string librariesPath, string gameVersion, string versionsPath)
        {
            HashSet<string> classPathSet = new HashSet<string>();

            try
            {
                string jsonPath = Path.Combine(versionsPath, gameVersion + ".json");
                if (File.Exists(jsonPath))
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                    {
                        if (doc.RootElement.TryGetProperty("libraries", out JsonElement libraries))
                        {
                            foreach (JsonElement library in libraries.EnumerateArray())
                            {
                                string libPath = GetLibraryPathFromJson(library);
                                if (!string.IsNullOrEmpty(libPath))
                                {
                                    string fullPath = Path.Combine(librariesPath, libPath);
                                    if (File.Exists(fullPath))
                                    {
                                        classPathSet.Add(fullPath);
                                    }
                                }
                            }
                        }
                    }
                }

                if (Directory.Exists(librariesPath))
                {
                    foreach (string jarFile in Directory.GetFiles(librariesPath, "*.jar", SearchOption.AllDirectories))
                    {
                        string fileName = Path.GetFileName(jarFile).ToLower();
                        bool isNativeJar = fileName.Contains("-natives-") && (fileName.Contains("-windows-") || fileName.Contains("-linux-") || fileName.Contains("-macos-"));
                        
                        if (isNativeJar)
                        {
                            continue;
                        }
                        
                        classPathSet.Add(jarFile);
                    }
                }
            }
            catch { }

            return string.Join(";", classPathSet);
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
    }
}

