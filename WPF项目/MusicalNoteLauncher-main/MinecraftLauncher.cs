using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyMCLauncher
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

    public class VersionManifest
    {
        public LatestVersionInfo latest { get; set; }
        public VersionItem[] versions { get; set; }
    }

    public class LatestVersionInfo
    {
        public string release { get; set; }
        public string snapshot { get; set; }
    }

    public class VersionItem
    {
        public string id { get; set; }
        public string type { get; set; }
        public string url { get; set; }
        public string time { get; set; }
        public string releaseTime { get; set; }
        public string sha1 { get; set; }
        public int? size { get; set; }
    }

    public class DownloadTask
    {
        public string VersionId { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public string SavePath { get; set; }
        public long TotalSize { get; set; }
        public long DownloadedSize { get; set; }
        public double Progress => TotalSize > 0 ? (double)DownloadedSize / TotalSize * 100 : 0;
        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
        public string ErrorMessage { get; set; }
    }

    public enum DownloadStatus
    {
        Pending,
        Waiting,
        Downloading,
        Paused,
        Completed,
        Failed,
        Cancelled
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

    public class DlClientListResult
    {
        public List<VersionInfo> Versions { get; set; } = new List<VersionInfo>();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public static class Logger
    {
        public static void Info(string message) => Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        public static void Warning(string message) => Console.WriteLine($"[WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        public static void Error(string message) => Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        public static void Error(string message, Exception ex) => Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}, Exception: {ex}");
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
            var errorMessages = new List<string>();

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
            var versions = new List<VersionInfo>();

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
                            var info = ParseVersionItem(item);
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

                var info = new VersionInfo
                {
                    Id = id,
                    Type = type,
                    Url = GetStringProperty(item, "url")
                };

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
                string versionPath = Path.Combine(_minecraftPath, "versions", versionId);
                return Directory.Exists(versionPath);
            }
            catch
            {
                return false;
            }
        }

        public async Task<DownloadTask> DownloadVersionAsync(VersionInfo version, IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            var task = new DownloadTask
            {
                VersionId = version.Id,
                Status = DownloadStatus.Waiting
            };

            try
            {
                Logger.Info($"开始下载版本: {version.Id}");

                string versionJsonUrl = version.Url;
                Logger.Info($"获取版本JSON: {versionJsonUrl}");

                string versionJson = await _httpClient.GetStringAsync(versionJsonUrl);
                var versionDoc = JsonDocument.Parse(versionJson);
                var versionRoot = versionDoc.RootElement;

                if (!versionRoot.TryGetProperty("downloads", out JsonElement downloads))
                {
                    task.Status = DownloadStatus.Failed;
                    task.ErrorMessage = "无法获取下载信息";
                    return task;
                }

                if (!versionRoot.TryGetProperty("downloads", out JsonElement downloadsSection) ||
                    !downloadsSection.TryGetProperty("client", out JsonElement clientDownload))
                {
                    task.Status = DownloadStatus.Failed;
                    task.ErrorMessage = "无法获取客户端下载信息";
                    return task;
                }

                string jarUrl = clientDownload.GetProperty("url").GetString();
                string sha1 = clientDownload.TryGetProperty("sha1", out JsonElement sha1Element) ? sha1Element.GetString() : null;

                Logger.Info($"客户端Jar地址: {jarUrl}");
                if (!string.IsNullOrEmpty(sha1))
                    Logger.Info($"SHA1: {sha1}");

                string versionsDir = Path.Combine(_minecraftPath, "versions", version.Id);
                Directory.CreateDirectory(versionsDir);

                string jarPath = Path.Combine(versionsDir, $"{version.Id}.jar");
                task.SavePath = jarPath;

                task.Status = DownloadStatus.Downloading;
                await DownloadFileAsync(jarUrl, jarPath, sha1, progress, cancellationToken);

                string versionJsonPath = Path.Combine(versionsDir, $"{version.Id}.json");
                File.WriteAllText(versionJsonPath, versionJson);

                string assetsDir = Path.Combine(_minecraftPath, "assets");
                Directory.CreateDirectory(assetsDir);

                string indexesDir = Path.Combine(assetsDir, "indexes");
                Directory.CreateDirectory(indexesDir);

                if (versionRoot.TryGetProperty("assetIndex", out JsonElement assetIndex))
                {
                    string assetIndexUrl = assetIndex.GetProperty("url").GetString();
                    string assetIndexId = assetIndex.GetProperty("id").GetString();
                    string assetIndexPath = Path.Combine(indexesDir, $"{assetIndexId}.json");

                    Logger.Info($"下载资源索引: {assetIndexUrl}");
                    await DownloadFileAsync(assetIndexUrl, assetIndexPath, null, null, cancellationToken);
                }

                task.Status = DownloadStatus.Completed;
                Logger.Info($"版本 {version.Id} 下载完成");
            }
            catch (OperationCanceledException)
            {
                task.Status = DownloadStatus.Cancelled;
                task.ErrorMessage = "下载已取消";
                Logger.Warning($"版本 {version.Id} 下载已取消");
            }
            catch (Exception ex)
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = ex.Message;
                Logger.Error($"版本 {version.Id} 下载失败: {ex.Message}");
            }

            return task;
        }

        public async Task DownloadFileAsync(string url, string savePath, string expectedSha1, IProgress<double> progress, CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                var downloadedBytes = 0L;

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

        public async Task<List<string>> GetInstalledVersionsAsync()
        {
            var installedVersions = new List<string>();

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
            }
            catch (Exception ex)
            {
                Logger.Error($"获取已安装版本失败: {ex.Message}");
            }

            return installedVersions;
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

        public string GetMinecraftPath() => _minecraftPath;
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("   MyMC Launcher - 我的世界启动器");
            Console.WriteLine("========================================");
            Console.WriteLine();

            string minecraftPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "MNL",
                ".minecraft"
            );

            Console.WriteLine($"Minecraft路径: {minecraftPath}");
            Console.WriteLine();

            var downloadManager = new DownloadManager(minecraftPath);

            try
            {
                Console.WriteLine("正在获取版本列表...");
                Console.WriteLine();

                var versions = await downloadManager.GetRemoteVersionsAsync();

                if (versions != null && versions.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"[成功] 获取到 {versions.Count} 个正式版");
                    Console.WriteLine();
                    Console.WriteLine("前10个版本:");
                    for (int i = 0; i < Math.Min(10, versions.Count); i++)
                    {
                        var v = versions[i];
                        string downloaded = v.IsDownloaded ? "[已下载]" : "[未下载]";
                        Console.WriteLine($"  {downloaded} {v.Id} ({v.DisplayType}) - {v.ReleaseTime:yyyy-MM-dd}");
                    }
                    if (versions.Count > 10)
                    {
                        Console.WriteLine($"  ... 还有 {versions.Count - 10} 个版本");
                    }
                    Console.WriteLine();
                    Console.WriteLine("离线模式: 已关闭");
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("[失败] 所有数据源均未返回有效版本数据");
                    Console.WriteLine("离线模式: 已开启");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"[错误] 启动器运行异常: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}