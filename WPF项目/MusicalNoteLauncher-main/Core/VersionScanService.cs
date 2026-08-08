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

        private string _minecraftPath => AppContext.MinecraftPath;

        private readonly ConcurrentDictionary<string, InstalledVersionInfo> _installedVersions = new ConcurrentDictionary<string, InstalledVersionInfo>();
        private readonly ConcurrentDictionary<string, InstalledVersionInfo> _installedBedrockVersions = new ConcurrentDictionary<string, InstalledVersionInfo>();

        private DateTime _lastScanTime = DateTime.MinValue;
        private bool _isScanning = false;
        private int _scanSequence = 0;
        private readonly object _scanLock = new object();

        /// <summary>上次扫描时 versions 文件夹的 LastWriteTime（递归取最大值），用于变更检测</summary>
        private DateTime _lastKnownWriteTime = DateTime.MinValue;
        /// <summary>上次扫描时文件夹名列表的校验和，用于缓存验证</summary>
        private string _lastKnownFolderChecksum = "";

        /// <summary>去抖间隔，避免短时间内重复扫描</summary>
        private static readonly TimeSpan DebounceInterval = TimeSpan.FromSeconds(1);

        public event Action<VersionScanResult> ScanCompleted;
        public event Action<string> ScanStarted;

        public bool IsScanning => _isScanning;
        public DateTime LastScanTime => _lastScanTime;

        private VersionScanService()
        {
        }

        /// <summary>智能扫描：仅当 versions 文件夹发生变更时才重新扫描</summary>
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

                // 去抖：短时间内不重复扫描
                if ((DateTime.Now - _lastScanTime) < DebounceInterval)
                {
                    Logger.Info($"[版本扫描] 去抖保护 (来源: {triggerSource}, 距上次扫描: {(DateTime.Now - _lastScanTime).TotalSeconds:F1}秒)，返回缓存");
                    return CreateResultFromCache();
                }

                _isScanning = true;
                _scanSequence++;
                currentSequence = _scanSequence;
            }

            ScanStarted?.Invoke(triggerSource);
            Logger.Info($"[版本扫描] ===============================================");
            Logger.Info($"[版本扫描] 智能扫描开始 (来源: {triggerSource}, 序号: {currentSequence})");

            try
            {
                // 检查文件夹是否变更
                string versionsDir = Path.Combine(_minecraftPath, "versions");
                if (!NeedRescan(versionsDir))
                {
                    lock (_scanLock) { _isScanning = false; }
                    Logger.Info($"[版本扫描] versions 文件夹未变更，跳过扫描");
                    Logger.Info($"[版本扫描] ===============================================");
                    return CreateResultFromCache();
                }

                Logger.Info($"[版本扫描] 检测到 versions 文件夹变更，执行全量扫描");

                await Task.Run(() => ScanVersionsInternal(currentSequence));

                _lastScanTime = DateTime.Now;
                var result = CreateResultFromCache();

                Logger.Info($"[版本扫描] 扫描完成！共发现 {_installedVersions.Count} 个Java版本, {_installedBedrockVersions.Count} 个基岩版本");

                // 持久化到磁盘缓存
                SaveCacheToDisk();

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

        /// <summary>强制扫描：跳过变更检测，始终执行全量扫描。用于已知发生变更的场景（下载完成、删除版本等）</summary>
        public async Task<VersionScanResult> ForceScanAsync(string triggerSource = "force")
        {
            int currentSequence;
            lock (_scanLock)
            {
                if (_isScanning)
                {
                    Logger.Info($"[版本扫描] 强制扫描请求但已有扫描进行中 (来源: {triggerSource})，返回当前缓存");
                    return CreateResultFromCache();
                }

                // 去抖：短时间内不重复扫描
                if ((DateTime.Now - _lastScanTime) < DebounceInterval)
                {
                    Logger.Info($"[版本扫描] 强制扫描去抖 (来源: {triggerSource})，返回缓存");
                    return CreateResultFromCache();
                }

                _isScanning = true;
                _scanSequence++;
                currentSequence = _scanSequence;
            }

            ScanStarted?.Invoke(triggerSource);
            Logger.Info($"[版本扫描] ===============================================");
            Logger.Info($"[版本扫描] 强制扫描开始 (来源: {triggerSource}, 序号: {currentSequence})");

            try
            {
                await Task.Run(() => ScanVersionsInternal(currentSequence));

                _lastScanTime = DateTime.Now;
                var result = CreateResultFromCache();

                Logger.Info($"[版本扫描] 强制扫描完成！共发现 {_installedVersions.Count} 个Java版本, {_installedBedrockVersions.Count} 个基岩版本");

                // 持久化到磁盘缓存
                SaveCacheToDisk();

                Logger.Info($"[版本扫描] ===============================================");

                ScanCompleted?.Invoke(result);

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[版本扫描] 强制扫描发生异常: {ex.Message}");
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

        /// <summary>初始化时尝试从磁盘缓存加载，若无缓存则执行全量扫描</summary>
        public async Task<VersionScanResult> InitAsync()
        {
            // 先从磁盘缓存加载
            if (LoadCacheFromDisk())
            {
                _lastScanTime = DateTime.Now;
                var result = CreateResultFromCache();
                Logger.Info($"[版本扫描] 从磁盘缓存加载完成，共 {_installedVersions.Count} 个Java版本, {_installedBedrockVersions.Count} 个基岩版本");
                ScanCompleted?.Invoke(result);
                return result;
            }

            // 无缓存，执行全量扫描
            return await ForceScanAsync("初始化-无缓存");
        }

        /// <summary>检查 versions 文件夹是否需要重新扫描</summary>
        private bool NeedRescan(string versionsDir)
        {
            try
            {
                if (!Directory.Exists(versionsDir))
                {
                    // 如果之前有缓存但目录现在不存在了，需要更新
                    if (_installedVersions.Count > 0 || _installedBedrockVersions.Count > 0)
                        return true;
                    // 之前就没有缓存且目录不存在，无需重扫
                    if (_lastKnownWriteTime == DateTime.MinValue && string.IsNullOrEmpty(_lastKnownFolderChecksum))
                        return false;
                    // 目录被删除了，需要重扫以清空缓存
                    _lastKnownWriteTime = DateTime.MinValue;
                    _lastKnownFolderChecksum = "";
                    return true;
                }

                DateTime currentWriteTime = GetFolderMaxLastWriteTime(versionsDir);
                string currentChecksum = ComputeFolderChecksum(versionsDir);

                if (currentWriteTime != _lastKnownWriteTime || currentChecksum != _lastKnownFolderChecksum)
                {
                    _lastKnownWriteTime = currentWriteTime;
                    _lastKnownFolderChecksum = currentChecksum;
                    return true;
                }

                return false;
            }
            catch
            {
                // 出错时保守处理，执行扫描
                return true;
            }
        }

        /// <summary>递归获取文件夹及其子文件/子文件夹的最大 LastWriteTime</summary>
        private DateTime GetFolderMaxLastWriteTime(string folderPath)
        {
            DateTime maxTime = Directory.GetLastWriteTime(folderPath);

            try
            {
                foreach (var file in Directory.GetFiles(folderPath))
                {
                    DateTime fileTime = File.GetLastWriteTime(file);
                    if (fileTime > maxTime) maxTime = fileTime;
                }

                foreach (var subDir in Directory.GetDirectories(folderPath))
                {
                    DateTime subTime = GetFolderMaxLastWriteTime(subDir);
                    if (subTime > maxTime) maxTime = subTime;
                }
            }
            catch { /* 权限不足时忽略 */ }

            return maxTime;
        }

        /// <summary>计算文件夹名列表的校验和（仅第一层子目录名）</summary>
        private string ComputeFolderChecksum(string versionsDir)
        {
            try
            {
                var names = new List<string>();
                foreach (var dir in Directory.GetDirectories(versionsDir))
                {
                    names.Add(Path.GetFileName(dir));
                }
                names.Sort();
                return string.Join("|", names);
            }
            catch
            {
                return "";
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

                        if (!File.Exists(jsonFile))
                        {
                            continue;
                        }

                        var versionInfo = ParseJavaVersionInfo(jsonFile, versionId)
                            ?? new InstalledVersionInfo
                            {
                                VersionId = versionId,
                                VersionType = "release",
                                ReleaseTime = DateTime.MinValue,
                                IsValid = true
                            };

                        newJavaVersions.TryAdd(versionId, versionInfo);
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
                            continue;
                        }

                        var versionInfo = new InstalledVersionInfo
                        {
                            VersionId = versionId,
                            VersionType = "bedrock",
                            IsValid = true
                        };

                        newBedrockVersions.TryAdd(versionId, versionInfo);
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
                JavaVersionInfos = _installedVersions.Values.ToList(),
                BedrockVersions = _installedBedrockVersions.Keys.ToList(),
                IsFromCache = true,
                ScanTime = _lastScanTime
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // 磁盘缓存持久化
        // ═══════════════════════════════════════════════════════════════

        private string CacheFilePath => Path.Combine(MNLEnvironment.MNLFolder, "versionCache.json");

        /// <summary>将扫描结果保存到磁盘缓存</summary>
        private void SaveCacheToDisk()
        {
            try
            {
                string cacheDir = MNLEnvironment.MNLFolder;
                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);

                var cacheData = new VersionCacheData
                {
                    FolderChecksum = _lastKnownFolderChecksum,
                    LastWriteTime = _lastKnownWriteTime.ToString("O"),
                    JavaVersions = _installedVersions.Values.Select(v => new VersionCacheEntry
                    {
                        VersionId = v.VersionId,
                        VersionType = v.VersionType,
                        ReleaseTime = v.ReleaseTime.ToString("O")
                    }).ToList(),
                    BedrockVersions = _installedBedrockVersions.Values.Select(v => new VersionCacheEntry
                    {
                        VersionId = v.VersionId,
                        VersionType = v.VersionType,
                        ReleaseTime = v.ReleaseTime.ToString("O")
                    }).ToList()
                };

                string json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(CacheFilePath, json);
                Logger.Info($"[磁盘缓存] 缓存已保存: {CacheFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[磁盘缓存] 保存缓存失败: {ex.Message}");
            }
        }

        /// <summary>从磁盘缓存加载，返回是否成功加载且缓存有效</summary>
        private bool LoadCacheFromDisk()
        {
            try
            {
                if (!File.Exists(CacheFilePath))
                {
                    Logger.Info("[磁盘缓存] 缓存文件不存在，需要全量扫描");
                    return false;
                }

                string json = File.ReadAllText(CacheFilePath);
                var cacheData = JsonSerializer.Deserialize<VersionCacheData>(json);
                if (cacheData == null)
                {
                    Logger.Info("[磁盘缓存] 缓存文件为空，需要全量扫描");
                    return false;
                }

                // 验证缓存是否仍然有效：对比 versions 文件夹的校验和
                string versionsDir = Path.Combine(_minecraftPath, "versions");
                string currentChecksum = ComputeFolderChecksum(versionsDir);

                if (currentChecksum != cacheData.FolderChecksum)
                {
                    Logger.Info($"[磁盘缓存] 文件夹校验和不匹配（缓存={cacheData.FolderChecksum}, 当前={currentChecksum}），缓存失效");
                    return false;
                }

                // 校验和匹配，恢复数据
                _installedVersions.Clear();
                _installedBedrockVersions.Clear();

                if (cacheData.JavaVersions != null)
                {
                    foreach (var entry in cacheData.JavaVersions)
                    {
                        _installedVersions.TryAdd(entry.VersionId, new InstalledVersionInfo
                        {
                            VersionId = entry.VersionId,
                            VersionType = entry.VersionType ?? "release",
                            ReleaseTime = DateTime.TryParse(entry.ReleaseTime, out var t) ? t : DateTime.MinValue,
                            IsValid = true
                        });
                    }
                }

                if (cacheData.BedrockVersions != null)
                {
                    foreach (var entry in cacheData.BedrockVersions)
                    {
                        _installedBedrockVersions.TryAdd(entry.VersionId, new InstalledVersionInfo
                        {
                            VersionId = entry.VersionId,
                            VersionType = entry.VersionType ?? "bedrock",
                            ReleaseTime = DateTime.TryParse(entry.ReleaseTime, out var t) ? t : DateTime.MinValue,
                            IsValid = true
                        });
                    }
                }

                _lastKnownFolderChecksum = currentChecksum;
                if (DateTime.TryParse(cacheData.LastWriteTime, out var writeTime))
                    _lastKnownWriteTime = writeTime;

                Logger.Info($"[磁盘缓存] 缓存加载成功，共 {_installedVersions.Count} 个Java版本, {_installedBedrockVersions.Count} 个基岩版本");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[磁盘缓存] 加载缓存失败: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 磁盘缓存数据类
        // ═══════════════════════════════════════════════════════════════

        private class VersionCacheData
        {
            public string FolderChecksum { get; set; }
            public string LastWriteTime { get; set; }
            public List<VersionCacheEntry> JavaVersions { get; set; }
            public List<VersionCacheEntry> BedrockVersions { get; set; }
        }

        private class VersionCacheEntry
        {
            public string VersionId { get; set; }
            public string VersionType { get; set; }
            public string ReleaseTime { get; set; }
        }

        // ═══════════════════════════════════════════════════════════════
        // 公共查询 API
        // ═══════════════════════════════════════════════════════════════

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
            _lastKnownWriteTime = DateTime.MinValue;
            _lastKnownFolderChecksum = "";
            Logger.Info("[版本扫描] 内存缓存已清除");
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
        /// <summary>已安装的Java版本详细信息（含type和releaseTime，避免UI层二次I/O）</summary>
        public List<InstalledVersionInfo> JavaVersionInfos { get; set; } = new List<InstalledVersionInfo>();
        public bool IsFromCache { get; set; }
        public DateTime ScanTime { get; set; }
    }
}
