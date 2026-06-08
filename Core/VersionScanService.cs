using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class VersionScanService
    {
        private static VersionScanService _instance;
        public static VersionScanService Instance => _instance ??= new VersionScanService();

        private readonly string _minecraftPath;
        private readonly ConcurrentDictionary<string, InstalledVersionInfo> _installedVersions = new ConcurrentDictionary<string, InstalledVersionInfo>();
        private readonly ConcurrentDictionary<string, InstalledVersionInfo> _installedBedrockVersions = new ConcurrentDictionary<string, InstalledVersionInfo>();

        private DateTime _lastScanTime = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(10);
        private bool _isScanning = false;
        private int _scanSequence = 0;
        private readonly object _scanLock = new object();

        public event Action<VersionScanResult> ScanCompleted;
        public event Action<string> ScanStarted;

        public bool IsScanning => _isScanning;
        public DateTime LastScanTime => _lastScanTime;

        private VersionScanService()
        {
            _minecraftPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        }

        public async Task<VersionScanResult> ScanAsync(string triggerSource = "manual")
        {
            int currentSequence;
            lock (_scanLock)
            {
                if (_isScanning)
                {
                    Logger.Info($"[版本扫描] 扫描正在进行中 (来源: {triggerSource})，跳过重复请求");
                    return CreateResultFromCache();
                }

                if ((DateTime.Now - _lastScanTime) < _cacheDuration && _installedVersions.Count > 0)
                {
                    Logger.Info($"[版本扫描] 缓存仍在有效期内 (来源: {triggerSource}, 距上次扫描: {(DateTime.Now - _lastScanTime).TotalSeconds:F1}秒)，跳过重复请求");
                    return CreateResultFromCache();
                }

                _isScanning = true;
                _scanSequence++;
                currentSequence = _scanSequence;
            }

            ScanStarted?.Invoke(triggerSource);
            Logger.Info($"[版本扫描] ===============================================");
            Logger.Info($"[版本扫描] 开始扫描已安装版本 (来源: {triggerSource}, 序号: {currentSequence})");

            try
            {
                await Task.Run(() => ScanVersionsInternal(currentSequence));

                _lastScanTime = DateTime.Now;
                var result = CreateResultFromCache();

                Logger.Info($"[版本扫描] 扫描完成！共发现 {_installedVersions.Count} 个Java版本, {_installedBedrockVersions.Count} 个基岩版本");
                Logger.Info($"[版本扫描] ===============================================");

                ScanCompleted?.Invoke(result);

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[版本扫描] 扫描过程发生异常: {ex.Message}");
                return CreateResultFromCache();
            }
            finally
            {
                lock (_scanLock)
                {
                    _isScanning = false;
                }
            }
        }

        private void ScanVersionsInternal(int sequence)
        {
            try
            {
                ScanJavaVersions(sequence);
                ScanBedrockVersions(sequence);
            }
            catch (Exception ex)
            {
                Logger.Error($"[版本扫描] 版本扫描内部异常: {ex.Message}");
            }
        }

        private void ScanJavaVersions(int sequence)
        {
            string versionsDir = Path.Combine(_minecraftPath, "versions");
            var newJavaVersions = new ConcurrentDictionary<string, InstalledVersionInfo>();

            if (!Directory.Exists(versionsDir))
            {
                Logger.Info($"[版本扫描] Java版本目录不存在: {versionsDir}");
                _installedVersions.Clear();
                return;
            }

            try
            {
                var versionDirs = Directory.GetDirectories(versionsDir);
                Logger.Info($"[版本扫描] 发现 {versionDirs.Length} 个Java版本目录");

                foreach (var dir in versionDirs)
                {
                    try
                    {
                        string versionId = Path.GetFileName(dir);
                        string jsonFile = Path.Combine(dir, $"{versionId}.json");
                        string jarFile = Path.Combine(dir, $"{versionId}.jar");

                        if (!File.Exists(jsonFile) || !File.Exists(jarFile))
                        {
                            Logger.Info($"[版本扫描] [序号:{sequence}] 跳过无效Java版本: {versionId} (缺少jar或json文件)");
                            continue;
                        }

                        var versionInfo = ParseJavaVersionInfo(jsonFile, versionId);
                        if (versionInfo != null)
                        {
                            newJavaVersions.TryAdd(versionId, versionInfo);
                            Logger.Info($"[版本扫描] [序号:{sequence}] 发现有效Java版本: {versionId}");
                        }
                        else
                        {
                            Logger.Info($"[版本扫描] [序号:{sequence}] 跳过无效Java版本: {versionId} (json解析失败)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"[版本扫描] [序号:{sequence}] 解析Java版本目录失败: {dir}, 错误: {ex.Message}");
                    }
                }

                _installedVersions.Clear();
                foreach (var kvp in newJavaVersions)
                {
                    _installedVersions.TryAdd(kvp.Key, kvp.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[版本扫描] [序号:{sequence}] 扫描Java版本目录失败: {ex.Message}");
            }
        }

        private void ScanBedrockVersions(int sequence)
        {
            string bedrockDir = Path.Combine(_minecraftPath, "bedrock");
            var newBedrockVersions = new ConcurrentDictionary<string, InstalledVersionInfo>();

            if (!Directory.Exists(bedrockDir))
            {
                Logger.Info($"[版本扫描] 基岩版目录不存在: {bedrockDir}");
                _installedBedrockVersions.Clear();
                return;
            }

            try
            {
                var versionDirs = Directory.GetDirectories(bedrockDir);
                Logger.Info($"[版本扫描] 发现 {versionDirs.Length} 个基岩版目录");

                foreach (var dir in versionDirs)
                {
                    try
                    {
                        string versionId = Path.GetFileName(dir);
                        string exePath = Path.Combine(dir, "Minecraft.Windows.exe");

                        if (!File.Exists(exePath))
                        {
                            Logger.Info($"[版本扫描] [序号:{sequence}] 跳过无效基岩版: {versionId} (缺少Minecraft.Windows.exe)");
                            continue;
                        }

                        var versionInfo = new InstalledVersionInfo
                        {
                            VersionId = versionId,
                            VersionType = "bedrock",
                            IsValid = true
                        };

                        newBedrockVersions.TryAdd(versionId, versionInfo);
                        Logger.Info($"[版本扫描] [序号:{sequence}] 发现有效基岩版: {versionId}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"[版本扫描] [序号:{sequence}] 解析基岩版目录失败: {dir}, 错误: {ex.Message}");
                    }
                }

                _installedBedrockVersions.Clear();
                foreach (var kvp in newBedrockVersions)
                {
                    _installedBedrockVersions.TryAdd(kvp.Key, kvp.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[版本扫描] [序号:{sequence}] 扫描基岩版目录失败: {ex.Message}");
            }
        }

        private InstalledVersionInfo ParseJavaVersionInfo(string jsonPath, string versionId)
        {
            try
            {
                string jsonContent = File.ReadAllText(jsonPath);
                using (var doc = JsonDocument.Parse(jsonContent))
                {
                    var root = doc.RootElement;

                    string versionType = "release";
                    DateTime releaseTime = DateTime.MinValue;

                    if (root.TryGetProperty("id", out var idElement) && idElement.GetString() != versionId)
                    {
                        Logger.Warning($"[版本扫描] 版本ID不匹配: 目录名={versionId}, json中的id={idElement.GetString()}");
                    }

                    if (root.TryGetProperty("type", out var typeElement))
                    {
                        versionType = typeElement.GetString() ?? "release";
                    }

                    if (root.TryGetProperty("releaseTime", out var timeElement))
                    {
                        string timeStr = timeElement.GetString();
                        if (!string.IsNullOrEmpty(timeStr) && DateTime.TryParse(timeStr, out DateTime parsedTime))
                        {
                            releaseTime = parsedTime;
                        }
                    }

                    return new InstalledVersionInfo
                    {
                        VersionId = versionId,
                        VersionType = versionType,
                        ReleaseTime = releaseTime,
                        IsValid = true
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[版本扫描] 解析版本JSON失败: {jsonPath}, 错误: {ex.Message}");
                return null;
            }
        }

        private VersionScanResult CreateResultFromCache()
        {
            return new VersionScanResult
            {
                JavaVersions = _installedVersions.Keys.ToList(),
                BedrockVersions = _installedBedrockVersions.Keys.ToList(),
                IsFromCache = true,
                ScanTime = _lastScanTime
            };
        }

        public bool IsJavaVersionInstalled(string versionId)
        {
            return _installedVersions.ContainsKey(versionId);
        }

        public bool IsBedrockVersionInstalled(string versionId)
        {
            return _installedBedrockVersions.ContainsKey(versionId);
        }

        public bool IsVersionInstalled(string versionId, VersionType versionType)
        {
            return versionType switch
            {
                VersionType.Java => IsJavaVersionInstalled(versionId),
                VersionType.Bedrock => IsBedrockVersionInstalled(versionId),
                _ => false
            };
        }

        public List<string> GetInstalledJavaVersions()
        {
            return _installedVersions.Keys.OrderByDescending(v => v, new VersionStringComparer()).ToList();
        }

        public List<string> GetInstalledBedrockVersions()
        {
            return _installedBedrockVersions.Keys.OrderByDescending(v => v, new VersionStringComparer()).ToList();
        }

        public List<string> GetInstalledVersions(VersionType versionType)
        {
            return versionType switch
            {
                VersionType.Java => GetInstalledJavaVersions(),
                VersionType.Bedrock => GetInstalledBedrockVersions(),
                _ => new List<string>()
            };
        }

        public void ClearCache()
        {
            _installedVersions.Clear();
            _installedBedrockVersions.Clear();
            _lastScanTime = DateTime.MinValue;
            Logger.Info("[版本扫描] 缓存已清除");
        }

        private class VersionStringComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                try
                {
                    var v1 = ParseVersion(x);
                    var v2 = ParseVersion(y);
                    return v2.CompareTo(v1);
                }
                catch
                {
                    return string.Compare(y, x, StringComparison.Ordinal);
                }
            }

            private Version ParseVersion(string versionString)
            {
                if (string.IsNullOrEmpty(versionString))
                    return new Version(0, 0);

                string cleanVersion = versionString.Split('-')[0].Split('+')[0];
                if (Version.TryParse(cleanVersion, out Version result))
                    return result;

                return new Version(0, 0);
            }
        }
    }

    public class InstalledVersionInfo
    {
        public string VersionId { get; set; }
        public string VersionType { get; set; }
        public DateTime ReleaseTime { get; set; }
        public bool IsValid { get; set; }
    }

    public class VersionScanResult
    {
        public List<string> JavaVersions { get; set; } = new List<string>();
        public List<string> BedrockVersions { get; set; } = new List<string>();
        public bool IsFromCache { get; set; }
        public DateTime ScanTime { get; set; }
    }
}