using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class VersionInfo
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public DateTime ReleaseTime { get; set; }
        public bool IsDownloaded { get; set; }
        public string DisplayType => Type == "release" ? "正式版" : (Type == "snapshot" ? "快照版" : Type);
    }

    public class LibraryDownloadInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Url { get; set; }
    }

    public class AssetIndexInfo
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string Url { get; set; }
    }

    public class DownloadTaskInfo
    {
        public string VersionId { get; set; }
        public string Status { get; set; } = "等待中";
        public double Progress { get; set; } = 0;
        public string ErrorMessage { get; set; }
        public bool IsCompleted { get; set; } = false;
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public string CurrentFile { get; set; }
    }

    public class DownloadProgressInfo
    {
        public string Status { get; set; }
        public double Progress { get; set; }
        public string CurrentFile { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public string VersionId { get; set; }
        // 新增：速度相关字段
        public string DownloadedSize { get; set; }
        public string TotalSize { get; set; }
        public string Speed { get; set; }
        public double SpeedBytesPerSecond { get; set; }
    }

    public static class FileSizeFormatter
    {
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }

        public static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond:F1} B/s";
            if (bytesPerSecond < 1024 * 1024)
                return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024.0 * 1024):F1} MB/s";
        }
    }

    public class DownloadProgress : IProgress<DownloadProgressInfo>
    {
        public event Action<DownloadProgressInfo> ProgressChanged;

        public void Report(DownloadProgressInfo value)
        {
            ProgressChanged?.Invoke(value);
        }
    }

    public class DlClientListResult
    {
        public List<VersionInfo> Versions { get; set; } = new List<VersionInfo>();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class LoaderTask<TInput, TResult>
    {
        public string Name { get; set; }
        public TInput Input { get; set; }
        public TResult Result { get; set; }
        public Exception Error { get; set; }
        public bool IsCancelled { get; set; }
        public CancellationToken CancellationToken { get; set; }

        private readonly Action<LoaderTask<TInput, TResult>> _action;

        public LoaderTask(string name, Action<LoaderTask<TInput, TResult>> action)
        {
            Name = name;
            _action = action;
        }

        public void Run(TInput input, CancellationToken cancellationToken = default)
        {
            try
            {
                Input = input;
                CancellationToken = cancellationToken;
                _action?.Invoke(this);
            }
            catch (OperationCanceledException)
            {
                IsCancelled = true;
            }
            catch (Exception ex)
            {
                Error = ex;
            }
        }
    }

    public class DownloadManager
    {
        private const string BMCLAPI_URL = "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json";
        private const string MOJANG_URL = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
        private const int REQUEST_TIMEOUT_MS = 30000;

        private readonly string _minecraftPath;
        private readonly HttpClient _httpClient;
        public LoaderTask<string, DlClientListResult> DlClientListLoader;

        public DownloadManager(string minecraftPath)
        {
            _minecraftPath = minecraftPath ?? throw new ArgumentNullException(nameof(minecraftPath));

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(REQUEST_TIMEOUT_MS)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

            DlClientListLoader = new LoaderTask<string, DlClientListResult>("DlClientList Main", DlClientListMain);
        }

        private void DlClientListMain(LoaderTask<string, DlClientListResult> loader)
        {
            try
            {
                DlSourceLoader(loader, new[] { BmclapiLoader(30), MojangLoader(90) }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                loader.Error = ex;
                Logger.Error($"DlClientListMain error: {ex.Message}");
            }
        }

        private async Task DlSourceLoader(LoaderTask<string, DlClientListResult> loader, Func<Task<List<VersionInfo>>>[] sources)
        {
            List<string> errorMessages = new List<string>();

            for (int i = 0; i < sources.Length; i++)
            {
                if (loader.CancellationToken.IsCancellationRequested)
                {
                    loader.IsCancelled = true;
                    Logger.Info("DlSourceLoader 已取消");
                    return;
                }

                var source = sources[i];
                string sourceName = i == 0 ? "BMCLAPI" : "Mojang";

                try
                {
                    Logger.Info($"正在尝试第 {i + 1} 个下载源: {sourceName}");

                    var versions = await source();
                    if (versions != null && versions.Count > 0)
                    {
                        loader.Result = new DlClientListResult { Versions = versions, Success = true };
                        Logger.Info($"下载源 {sourceName} 加载成功，获取到 {versions.Count} 个版本");
                        return;
                    }
                    else
                    {
                        string msg = $"下载源 {sourceName} 返回空版本列表";
                        errorMessages.Add(msg);
                        Logger.Warning(msg);
                    }
                }
                catch (OperationCanceledException)
                {
                    loader.IsCancelled = true;
                    Logger.Info($"下载源 {sourceName} 请求被取消");
                    return;
                }
                catch (Exception ex)
                {
                    string msg = $"下载源 {sourceName} 加载失败: {ex.Message}";
                    errorMessages.Add(msg);
                    Logger.Warning(msg);
                }
            }

            loader.Result = new DlClientListResult { Success = false, ErrorMessage = string.Join("\n", errorMessages) };
            Logger.Warning($"所有下载源均加载失败: {string.Join("; ", errorMessages)}");
        }

        private Func<Task<List<VersionInfo>>> BmclapiLoader(int timeoutSeconds)
        {
            return async () =>
            {
                Logger.Info($"正在尝试 BMCLAPI 镜像: {BMCLAPI_URL}");

                using (var cts = new CancellationTokenSource(timeoutSeconds * 1000))
                {
                    return await FetchVersionManifest(BMCLAPI_URL, cts.Token);
                }
            };
        }

        private Func<Task<List<VersionInfo>>> MojangLoader(int timeoutSeconds)
        {
            return async () =>
            {
                Logger.Info($"正在尝试 Mojang 官方源: {MOJANG_URL}");

                using (var cts = new CancellationTokenSource(timeoutSeconds * 1000))
                {
                    return await FetchVersionManifest(MOJANG_URL, cts.Token);
                }
            };
        }

        private async Task<List<VersionInfo>> FetchVersionManifest(string url, CancellationToken cancellationToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                ServicePointManager.Expect100Continue = false;

                Logger.Info($"正在发送请求到: {url}");

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(REQUEST_TIMEOUT_MS);

                    HttpResponseMessage response = await _httpClient.GetAsync(url, cts.Token);
                    response.EnsureSuccessStatusCode();

                    string json = await response.Content.ReadAsStringAsync();
                    Logger.Info($"成功获取JSON数据，长度: {json.Length} 字符");

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var versions = ParseVersionManifest(json);
                        if (versions.Count > 0)
                        {
                            Logger.Info($"成功解析到 {versions.Count} 个版本");
                            return versions;
                        }
                        else
                        {
                            Logger.Warning("解析到0个版本");
                        }
                    }
                    else
                    {
                        Logger.Warning("获取到空的JSON数据");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warning($"请求被取消: {url}");
            }
            catch (TimeoutException ex)
            {
                Logger.Warning($"请求超时: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                Logger.Warning($"网络错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"请求失败: {ex.Message}");
            }

            return new List<VersionInfo>();
        }

        public async Task<List<VersionInfo>> GetRemoteVersionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string localManifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version_manifest.json");
                if (File.Exists(localManifestPath))
                {
                    Logger.Info($"发现本地版本清单文件，优先读取: {localManifestPath}");
                    try
                    {
                        string json = File.ReadAllText(localManifestPath);
                        var versions = ParseVersionManifest(json);
                        if (versions.Count > 0)
                        {
                            Logger.Info($"成功从本地文件解析到 {versions.Count} 个版本");
                            return versions;
                        }
                        Logger.Warning("本地文件解析到0个版本，尝试网络获取");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"读取本地版本清单失败: {ex.Message}，尝试网络获取");
                    }
                }

                await Task.Run(() => DlClientListLoader.Run("", cancellationToken), cancellationToken);

                if (DlClientListLoader.Result != null && DlClientListLoader.Result.Versions != null && DlClientListLoader.Result.Versions.Count > 0)
                {
                    Logger.Info($"成功获取到 {DlClientListLoader.Result.Versions.Count} 个版本");
                    return DlClientListLoader.Result.Versions;
                }

                if (DlClientListLoader.Error != null)
                {
                    Logger.Warning($"加载器执行出错: {DlClientListLoader.Error.Message}");
                }

                if (DlClientListLoader.Result != null && !string.IsNullOrEmpty(DlClientListLoader.Result.ErrorMessage))
                {
                    Logger.Warning($"加载结果失败: {DlClientListLoader.Result.ErrorMessage}");
                }

                Logger.Warning("所有数据源均未返回有效版本数据");
                return new List<VersionInfo>();
            }
            catch (OperationCanceledException)
            {
                Logger.Info("GetRemoteVersionsAsync 已取消");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"GetRemoteVersionsAsync error: {ex.Message}");
                return new List<VersionInfo>();
            }
        }

        private List<VersionInfo> ParseVersionManifest(string json)
        {
            List<VersionInfo> versions = new List<VersionInfo>();

            if (string.IsNullOrWhiteSpace(json))
            {
                Logger.Warning("ParseVersionManifest received empty JSON");
                return versions;
            }

            try
            {
                Logger.Info("开始解析版本清单JSON...");

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;

                    if (!root.TryGetProperty("versions", out JsonElement versionsElement))
                    {
                        Logger.Warning("无法找到 versions 数组");
                        return versions;
                    }

                    if (versionsElement.ValueKind != JsonValueKind.Array)
                    {
                        Logger.Warning("versions 不是数组格式");
                        return versions;
                    }

                    Logger.Info($"JSON解析成功，找到 {versionsElement.GetArrayLength()} 个版本对象");

                    foreach (JsonElement item in versionsElement.EnumerateArray())
                    {
                        try
                        {
                            VersionInfo info = ParseVersionItem(item);
                            if (info != null && !string.IsNullOrWhiteSpace(info.Id))
                            {
                                versions.Add(info);
                                Logger.Info($"已添加版本: {info.Id} ({info.Type})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"解析单个版本对象失败: {ex.Message}");
                        }
                    }
                }

                Logger.Info($"版本解析完成，共 {versions.Count} 个正式版");
            }
            catch (JsonException ex)
            {
                Logger.Error($"JSON解析异常: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"解析版本清单失败: {ex.Message}");
            }

            return versions;
        }

        private VersionInfo ParseVersionItem(JsonElement item)
        {
            try
            {
                string id = GetStringProperty(item, "id");
                if (string.IsNullOrWhiteSpace(id))
                    return null;

                string type = GetStringProperty(item, "type");
                if (!string.Equals(type, "release", StringComparison.OrdinalIgnoreCase))
                    return null;

                VersionInfo info = new VersionInfo();
                info.Id = id;
                info.Type = type;
                info.Url = GetStringProperty(item, "url");

                string releaseTimeStr = GetStringProperty(item, "releaseTime");
                if (!string.IsNullOrEmpty(releaseTimeStr))
                {
                    DateTime.TryParse(releaseTimeStr, out DateTime releaseTime);
                    info.ReleaseTime = releaseTime;
                }

                info.IsDownloaded = IsVersionDownloaded(info.Id);

                return info;
            }
            catch (Exception ex)
            {
                Logger.Warning($"解析版本项失败: {ex.Message}");
                return null;
            }
        }

        private string GetStringProperty(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out JsonElement prop))
                {
                    if (prop.ValueKind == JsonValueKind.String)
                    {
                        return prop.GetString();
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            return property.Value.GetString();
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private bool IsVersionDownloaded(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
                string jarPath = Path.Combine(versionDir, $"{versionId}.jar");
                string jsonPath = Path.Combine(versionDir, $"{versionId}.json");
                return File.Exists(jarPath) && File.Exists(jsonPath);
            }
            catch
            {
                return false;
            }
        }

        public string GetMinecraftPath()
        {
            return _minecraftPath;
        }

        public async Task<List<string>> GetInstalledVersionIdsAsync()
        {
            List<string> installedVersions = new List<string>();

            try
            {
                string versionsDir = Path.Combine(_minecraftPath, "versions");
                if (Directory.Exists(versionsDir))
                {
                    foreach (string dir in Directory.GetDirectories(versionsDir))
                    {
                        string versionId = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(versionId))
                        {
                            installedVersions.Add(versionId);
                        }
                    }
                }

                Logger.Info($"扫描到 {installedVersions.Count} 个已安装版本");
            }
            catch (Exception ex)
            {
                Logger.Error($"获取已安装版本失败: {ex.Message}");
            }

            return installedVersions;
        }

        public async Task<List<string>> GetInstalledVersionsAsync()
        {
            List<string> installedVersions = new List<string>();

            try
            {
                string versionsDir = Path.Combine(_minecraftPath, "versions");
                if (Directory.Exists(versionsDir))
                {
                    foreach (string dir in Directory.GetDirectories(versionsDir))
                    {
                        string versionId = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(versionId))
                        {
                            installedVersions.Add(versionId);
                        }
                    }
                }

                Logger.Info($"扫描到 {installedVersions.Count} 个已安装版本");
            }
            catch (Exception ex)
            {
                Logger.Error($"获取已安装版本失败: {ex.Message}");
            }

            return installedVersions;
        }

        public async Task<List<VersionInfo>> GetInstalledVersionInfosAsync()
        {
            List<VersionInfo> installedVersions = new List<VersionInfo>();

            try
            {
                string versionsDir = Path.Combine(_minecraftPath, "versions");
                if (Directory.Exists(versionsDir))
                {
                    foreach (string dir in Directory.GetDirectories(versionsDir))
                    {
                        string versionId = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(versionId))
                        {
                            string jsonPath = Path.Combine(dir, $"{versionId}.json");
                            if (File.Exists(jsonPath))
                            {
                                try
                                {
                                    string jsonContent = File.ReadAllText(jsonPath);
                                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                                    {
                                        JsonElement root = doc.RootElement;
                                        string type = GetStringProperty(root, "type");
                                        string releaseTimeStr = GetStringProperty(root, "releaseTime");

                                        DateTime.TryParse(releaseTimeStr, out DateTime releaseTime);

                                        installedVersions.Add(new VersionInfo
                                        {
                                            Id = versionId,
                                            Type = string.IsNullOrEmpty(type) ? "release" : type,
                                            ReleaseTime = releaseTime,
                                            IsDownloaded = true
                                        });
                                    }
                                }
                                catch
                                {
                                    installedVersions.Add(new VersionInfo
                                    {
                                        Id = versionId,
                                        Type = "release",
                                        IsDownloaded = true
                                    });
                                }
                            }
                            else
                            {
                                installedVersions.Add(new VersionInfo
                                {
                                    Id = versionId,
                                    Type = "release",
                                    IsDownloaded = true
                                });
                            }
                        }
                    }
                }

                Logger.Info($"扫描到 {installedVersions.Count} 个已安装版本");
            }
            catch (Exception ex)
            {
                Logger.Error($"获取已安装版本失败: {ex.Message}");
            }

            return installedVersions;
        }

        public async Task<DownloadTaskInfo> StartDownloadAsync(VersionInfo version, IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            return await StartDownloadInternalAsync(version.Id, version.Url, progress, null, cancellationToken);
        }

        public async Task<DownloadTaskInfo> StartDownloadAsync(VersionInfo version, DownloadProgress progress, CancellationToken cancellationToken = default)
        {
            return await StartDownloadInternalAsync(version.Id, version.Url, null, progress, cancellationToken);
        }

        public async Task StartDownloadAsync(string versionId)
        {
            await StartDownloadAsync(new VersionInfo { Id = versionId, Url = "" });
        }

        private async Task<DownloadTaskInfo> StartDownloadInternalAsync(string versionId, string versionUrl, IProgress<double> simpleProgress = null, DownloadProgress detailedProgress = null, CancellationToken cancellationToken = default)
        {
            var taskInfo = new DownloadTaskInfo
            {
                VersionId = versionId,
                Status = "等待中"
            };

            try
            {
                Logger.Info($"========================================");
                Logger.Info($"开始下载版本: {versionId}");
                Logger.Info($"========================================");

                if (IsVersionDownloaded(versionId))
                {
                    Logger.Info($"版本 {versionId} 已安装，跳过下载");
                    taskInfo.Status = "已完成";
                    taskInfo.IsCompleted = true;
                    taskInfo.Progress = 100;
                    return taskInfo;
                }

                if (string.IsNullOrEmpty(versionUrl))
                {
                    taskInfo.Status = "下载失败";
                    taskInfo.ErrorMessage = "该版本暂不可下载";
                    Logger.Error($"版本 {versionId} 下载失败: 版本URL为空");
                    return taskInfo;
                }

                taskInfo.Status = "正在获取版本信息";
                Logger.Info($"获取版本JSON: {versionUrl}");

                string versionJson = await _httpClient.GetStringAsync(versionUrl);
                Logger.Info($"成功获取版本JSON，长度: {versionJson.Length} 字符");

                using (JsonDocument versionDoc = JsonDocument.Parse(versionJson))
                {
                    JsonElement versionRoot = versionDoc.RootElement;

                    if (!versionRoot.TryGetProperty("downloads", out JsonElement downloads))
                    {
                        taskInfo.Status = "下载失败";
                        taskInfo.ErrorMessage = "无法获取下载信息";
                        Logger.Error($"版本 {versionId} 下载失败: 无法获取下载信息");
                        return taskInfo;
                    }

                    if (!downloads.TryGetProperty("client", out JsonElement clientDownload))
                    {
                        taskInfo.Status = "下载失败";
                        taskInfo.ErrorMessage = "无法获取客户端下载信息";
                        Logger.Error($"版本 {versionId} 下载失败: 无法获取客户端下载信息");
                        return taskInfo;
                    }

                    string jarUrl = clientDownload.GetProperty("url").GetString();
                    string sha1 = clientDownload.TryGetProperty("sha1", out JsonElement sha1Element) ? sha1Element.GetString() : null;

                    Logger.Info($"客户端Jar地址: {jarUrl}");
                    if (!string.IsNullOrEmpty(sha1))
                        Logger.Info($"SHA1: {sha1}");

                    string versionsDir = Path.Combine(_minecraftPath, "versions", versionId);
                    Directory.CreateDirectory(versionsDir);
                    Logger.Info($"创建版本目录: {versionsDir}");

                    string jarPath = Path.Combine(versionsDir, $"{versionId}.jar");
                    string jsonPath = Path.Combine(versionsDir, $"{versionId}.json");

                    taskInfo.Status = "正在下载客户端Jar";
                    Logger.Info($"开始下载客户端Jar: {jarPath}");

                    bool downloadSuccess = await DownloadFileWithRetryAsync(jarUrl, jarPath, sha1, simpleProgress, detailedProgress, cancellationToken);

                    if (!downloadSuccess)
                    {
                        taskInfo.Status = "下载失败";
                        taskInfo.ErrorMessage = "Jar文件下载失败";
                        Logger.Error($"版本 {versionId} 客户端Jar下载失败");
                        return taskInfo;
                    }

                    Logger.Info($"客户端Jar下载完成: {jarPath}");

                    taskInfo.Status = "正在保存版本JSON";
                    Logger.Info($"保存版本JSON: {jsonPath}");
                    await Task.Run(() => File.WriteAllText(jsonPath, versionJson), cancellationToken);
                    Logger.Info($"版本JSON保存完成");

                    taskInfo.Status = "正在下载资源索引";
                    Logger.Info($"开始下载资源索引...");

                    if (versionRoot.TryGetProperty("assetIndex", out JsonElement assetIndex))
                    {
                        string assetIndexUrl = assetIndex.GetProperty("url").GetString();
                        string assetIndexId = assetIndex.GetProperty("id").GetString();

                        string assetsDir = Path.Combine(_minecraftPath, "assets", "indexes");
                        Directory.CreateDirectory(assetsDir);

                        string assetIndexPath = Path.Combine(assetsDir, $"{assetIndexId}.json");
                        Logger.Info($"下载资源索引: {assetIndexUrl}");

                        await DownloadFileWithRetryAsync(assetIndexUrl, assetIndexPath, null, null, null, cancellationToken);
                        Logger.Info($"资源索引下载完成");
                    }

                    if (versionRoot.TryGetProperty("libraries", out JsonElement libraries))
                        {
                            Logger.Info($"开始下载依赖库文件...");
                            taskInfo.Status = "正在下载依赖库";

                            string librariesDir = Path.Combine(_minecraftPath, "libraries");
                            Directory.CreateDirectory(librariesDir);

                            int downloadedCount = 0;
                            int skippedCount = 0;
                            int filteredCount = 0;

                            foreach (JsonElement library in libraries.EnumerateArray())
                            {
                                if (!IsLibraryApplicable(library))
                                {
                                    filteredCount++;
                                    continue;
                                }

                                if (library.TryGetProperty("downloads", out JsonElement libDownloads) &&
                                    libDownloads.TryGetProperty("jar", out JsonElement libJar))
                                {
                                    string libUrl = libJar.GetProperty("url").GetString();
                                    string libPath = libJar.TryGetProperty("path", out JsonElement libPathElement)
                                        ? Path.Combine(librariesDir, libPathElement.GetString())
                                        : null;

                                    if (!string.IsNullOrEmpty(libPath))
                                    {
                                        if (File.Exists(libPath))
                                        {
                                            skippedCount++;
                                            continue;
                                        }

                                        Directory.CreateDirectory(Path.GetDirectoryName(libPath));
                                        await DownloadFileWithRetryAsync(libUrl, libPath, null, null, null, cancellationToken);
                                        downloadedCount++;
                                    }
                                }
                            }
                            Logger.Info($"依赖库文件下载完成: 下载 {downloadedCount} 个，跳过 {skippedCount} 个，过滤 {filteredCount} 个");
                        }

                    if (versionRoot.TryGetProperty("logging", out JsonElement logging) &&
                        logging.TryGetProperty("client", out JsonElement clientLogging) &&
                        clientLogging.TryGetProperty("url", out JsonElement loggingUrl))
                    {
                        string loggingConfigUrl = loggingUrl.GetString();
                        string loggingConfigPath = Path.Combine(_minecraftPath, "assets", "log_configs", $"{versionId}-client.xml");
                        Directory.CreateDirectory(Path.GetDirectoryName(loggingConfigPath));
                        await DownloadFileWithRetryAsync(loggingConfigUrl, loggingConfigPath, null, null, null, cancellationToken);
                    }

                    taskInfo.Status = "已完成";
                    taskInfo.IsCompleted = true;
                    taskInfo.Progress = 100;

                    Logger.Info($"========================================");
                    Logger.Info($"版本 {versionId} 下载完成!");
                    Logger.Info($"文件位置: {jarPath}");
                    Logger.Info($"========================================");
                }
            }
            catch (OperationCanceledException)
            {
                taskInfo.Status = "已取消";
                taskInfo.ErrorMessage = "下载已取消";
                Logger.Warning($"版本 {versionId} 下载已取消");
            }
            catch (Exception ex)
            {
                taskInfo.Status = "下载失败";
                taskInfo.ErrorMessage = ex.Message;
                Logger.Error($"版本 {versionId} 下载失败: {ex.Message}");
            }

            return taskInfo;
        }

        private async Task<bool> DownloadFileWithRetryAsync(string url, string savePath, string expectedSha1, IProgress<double> simpleProgress = null, DownloadProgress detailedProgress = null, CancellationToken cancellationToken = default, int maxRetries = 3)
        {
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    if (retry > 0)
                    {
                        Logger.Info($"第 {retry + 1} 次重试下载: {url}");
                        await Task.Delay(1000 * retry, cancellationToken);
                    }

                    using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();

                        long totalBytes = response.Content.Headers.ContentLength ?? -1;
                        long downloadedBytes = 0;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                downloadedBytes += bytesRead;

                                if (totalBytes > 0)
                                {
                                    double progressValue = (double)downloadedBytes / totalBytes * 100;
                                    simpleProgress?.Report(progressValue);
                                    
                                    detailedProgress?.Report(new DownloadProgressInfo
                                    {
                                        Progress = progressValue,
                                        DownloadedBytes = downloadedBytes,
                                        TotalBytes = totalBytes,
                                        CurrentFile = Path.GetFileName(savePath),
                                        Status = "下载中"
                                    });
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(expectedSha1))
                    {
                        Logger.Info($"正在校验SHA1: {savePath}");
                        detailedProgress?.Report(new DownloadProgressInfo
                        {
                            Progress = 100,
                            CurrentFile = Path.GetFileName(savePath),
                            Status = "校验中"
                        });
                        
                        string actualSha1 = ComputeSha1(savePath);
                        if (!string.Equals(expectedSha1, actualSha1, StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Warning($"SHA1校验失败，期望: {expectedSha1}, 实际: {actualSha1}");
                            if (File.Exists(savePath))
                                File.Delete(savePath);
                            continue;
                        }
                        Logger.Info($"SHA1校验通过: {savePath}");
                    }

                    Logger.Info($"文件下载成功: {savePath}");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"下载失败 (尝试 {retry + 1}/{maxRetries}): {ex.Message}");
                    if (File.Exists(savePath))
                        File.Delete(savePath);
                }
            }

            Logger.Error($"文件下载失败，已重试 {maxRetries} 次: {url}");
            return false;
        }

        public async Task DownloadFileAsync(string url, string savePath, string expectedSha1, IProgress<double> progress, CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                long downloadedBytes = 0;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                        {
                            double progressValue = (double)downloadedBytes / totalBytes * 100;
                            progress?.Report(progressValue);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(expectedSha1))
            {
                string actualSha1 = ComputeSha1(savePath);
                if (!string.Equals(expectedSha1, actualSha1, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"SHA1校验失败，期望: {expectedSha1}, 实际: {actualSha1}");
                }
                Logger.Info($"SHA1校验通过: {savePath}");
            }
        }

        public string ComputeSha1(string filePath)
        {
            using (var sha1 = SHA1.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha1.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public bool DeleteVersion(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
                if (Directory.Exists(versionDir))
                {
                    Directory.Delete(versionDir, true);
                    Logger.Info($"已删除版本: {versionId}");
                    return true;
                }
                Logger.Warning($"版本目录不存在: {versionId}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"删除版本失败: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> FileExistsAndValidAsync(string filePath, string expectedSha1)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                if (!string.IsNullOrEmpty(expectedSha1))
                {
                    string actualSha1 = ComputeSha1(filePath);
                    return string.Equals(expectedSha1, actualSha1, StringComparison.OrdinalIgnoreCase);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsLibraryApplicable(JsonElement library)
        {
            try
            {
                if (!library.TryGetProperty("rules", out JsonElement rules))
                    return true;

                bool applicable = false;
                foreach (JsonElement rule in rules.EnumerateArray())
                {
                    string action = rule.TryGetProperty("action", out var actionElement) ? actionElement.GetString() : "allow";
                    bool applies = true;

                    if (rule.TryGetProperty("os", out JsonElement os))
                    {
                        string osName = os.TryGetProperty("name", out var osNameElement) ? osNameElement.GetString() : null;
                        applies = osName == "windows";
                    }

                    if (applies)
                    {
                        applicable = action == "allow";
                    }
                }

                return applicable;
            }
            catch
            {
                return true;
            }
        }
    }
}