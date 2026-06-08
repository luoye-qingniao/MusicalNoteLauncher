using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class VersionDownloadService
    {
        private readonly string[] _manifestUrls = new[]
        {
            "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json",
            "https://launchermeta.mojang.com/mc/game/version_manifest.json"
        };

        private readonly string _minecraftPath;
        private readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private VersionInfo _versionInfo;
        private CancellationTokenSource _cts;
        private static readonly HttpClient _httpClient = CreateHttpClient();

        public event Action<string> StatusChanged;
        public event Action<int> ProgressChanged;
        public event Action<string> DownloadCompleted;
        public event Action<string, string> DownloadFailed;

        public VersionDownloadService(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
            ConfigureSslAndProtocol();
        }

        private void ConfigureSslAndProtocol()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 200;
            ServicePointManager.MaxServicePoints = 200;
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 200,
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = false
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.ConnectionClose = false;
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate");

            return client;
        }

        public async Task<bool> ValidateUrlAsync(string url, bool enableRetry = true)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            int maxRetries = enableRetry ? 2 : 1;
            int timeoutMs = 15000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Logger.Info($"[URL校验] 尝试校验URL (尝试 {attempt}/{maxRetries}): {url}");

                    using (var client = new WebClient())
                    {
                        client.Headers.Add(HttpRequestHeader.UserAgent, _userAgent);
                        client.Encoding = Encoding.UTF8;

                        var uri = new Uri(url);
                        var request = WebRequest.Create(uri);
                        request.Timeout = timeoutMs;

                        using (var response = (HttpWebResponse)await Task.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null))
                        {
                            HttpStatusCode statusCode = response.StatusCode;
                            Logger.Info($"[URL校验] HTTP状态码: {statusCode}");

                            if (statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.Found || statusCode == HttpStatusCode.MovedPermanently)
                            {
                                return true;
                            }
                            else if (statusCode == HttpStatusCode.NotFound)
                            {
                                Logger.Warning($"[URL校验] URL {url} 返回404未找到");
                                return false;
                            }
                            else
                            {
                                Logger.Warning($"[URL校验] URL {url} 返回意外状态码: {statusCode}");
                                return false;
                            }
                        }
                    }
                }
                catch (WebException ex)
                {
                    var webResponse = ex.Response as HttpWebResponse;
                    if (webResponse != null)
                    {
                        if (webResponse.StatusCode == HttpStatusCode.NotFound)
                        {
                            Logger.Warning($"[URL校验] WebException: URL {url} 返回404错误");
                            return false;
                        }
                        Logger.Warning($"[URL校验] WebException (尝试 {attempt}/{maxRetries}): {url}, 状态: {webResponse.StatusCode}, 错误: {ex.Message}");
                    }
                    else
                    {
                        Logger.Warning($"[URL校验] WebException (尝试 {attempt}/{maxRetries}): {url}, 错误: {ex.Message}");
                    }

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000);
                    }
                }
                catch (TimeoutException ex)
                {
                    Logger.Warning($"[URL校验] 超时 (尝试 {attempt}/{maxRetries}): {url}, 错误: {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[URL校验] 异常 (尝试 {attempt}/{maxRetries}): {url}, 错误: {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000);
                    }
                }
            }

            Logger.Error($"[URL校验] URL {url} 校验失败，已重试 {maxRetries} 次");
            return false;
        }

        public string GetBackupUrl(string originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl))
                return null;

            if (originalUrl.StartsWith("https://piston-meta.mojang.com"))
            {
                return originalUrl.Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com");
            }
            if (originalUrl.StartsWith("https://launchermeta.mojang.com"))
            {
                return originalUrl.Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com");
            }
            if (originalUrl.StartsWith("https://resources.download.minecraft.net"))
            {
                return originalUrl.Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets");
            }
            if (originalUrl.StartsWith("https://piston-data.mojang.com"))
            {
                return originalUrl.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com");
            }

            return null;
        }

        public string[] GetAllAvailableUrls(string originalUrl)
        {
            var urls = new List<string>();

            if (!string.IsNullOrEmpty(originalUrl))
            {
                urls.Add(originalUrl);

                string backup = GetBackupUrl(originalUrl);
                if (!string.IsNullOrEmpty(backup) && backup != originalUrl)
                {
                    urls.Add(backup);
                }

                if (originalUrl.Contains("bmclapi2.bangbang93.com"))
                {
                    string mojangUrl = originalUrl
                        .Replace("bmclapi2.bangbang93.com", "launchermeta.mojang.com")
                        .Replace("bmclapi2.bangbang93.com", "piston-meta.mojang.com");
                    if (mojangUrl != originalUrl)
                    {
                        urls.Add(mojangUrl);
                    }
                }
            }

            return urls.Distinct().ToArray();
        }

        public async Task<string> GetValidUrlAsync(string originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl))
            {
                Logger.Warning($"[URL校验] 原始URL为空，无法校验");
                return null;
            }

            Logger.Info($"[URL校验] 开始校验URL: {originalUrl}");

            string[] urlsToTry = GetAllAvailableUrls(originalUrl);

            for (int i = 0; i < urlsToTry.Length; i++)
            {
                string urlToCheck = urlsToTry[i];
                bool isOriginal = i == 0;

                Logger.Info($"[URL校验] {'('}{i + 1}/{urlsToTry.Length}{')'} 尝试校验: {(isOriginal ? "原始" : "备用")}地址 - {urlToCheck}");

                bool isValid = await ValidateUrlAsync(urlToCheck, enableRetry: true);

                if (isValid)
                {
                    if (urlToCheck != originalUrl)
                    {
                        Logger.Info($"[URL校验] 原始URL {originalUrl} 无效/不可访问，已切换到备用地址: {urlToCheck}");
                    }
                    else
                    {
                        Logger.Info($"[URL校验] URL校验通过: {urlToCheck}");
                    }
                    return urlToCheck;
                }

                if (i < urlsToTry.Length - 1)
                {
                    Logger.Warning($"[URL校验] URL {urlToCheck} 校验失败，尝试下一个源...");
                }
            }

            Logger.Error($"[URL校验] 所有URL源均校验失败。原始地址: {originalUrl}");
            if (urlsToTry.Length > 1)
            {
                Logger.Error($"[URL校验] 已尝试的URL: {string.Join(", ", urlsToTry)}");
            }
            return null;
        }

        public async Task<List<VersionInfo>> GetRemoteVersionsAsync(CancellationToken cancellationToken = default)
        {
            List<string> errors = new List<string>();

            for (int i = 0; i < _manifestUrls.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();

                string sourceName = i == 0 ? "BMCLAPI镜像" : "Mojang官方源";
                StatusChanged?.Invoke($"正在尝试 {sourceName}...");

                try
                {
                    var versions = await FetchVersionManifestAsync(_manifestUrls[i], cancellationToken);
                    if (versions != null && versions.Count > 0)
                    {
                        StatusChanged?.Invoke($"成功从 {sourceName} 获取 {versions.Count} 个版本");
                        return versions;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add($"{sourceName}: {ex.Message}");
                    StatusChanged?.Invoke($"{sourceName} 失败，尝试下一个...");
                }
            }

            throw new Exception($"所有数据源均失败:\n{string.Join("\n", errors)}");
        }

        private async Task<List<VersionInfo>> FetchVersionManifestAsync(string url, CancellationToken cancellationToken)
        {
            return await FetchVersionManifestAsync(url, 120000, cancellationToken);
        }

        private async Task<List<VersionInfo>> FetchVersionManifestAsync(string url, int timeoutMs, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Logger.Info($"[清单获取] 第 {attempt}/{maxRetries} 次尝试从 {url} 获取版本列表，超时: {timeoutMs}ms");
                    return await FetchVersionManifestOnceAsync(url, timeoutMs, cancellationToken);
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    lastException = ex;
                    Logger.Warning($"[清单获取] 第 {attempt} 次尝试失败: {ex.Message}，{maxRetries - attempt} 次重试机会");
                    await Task.Delay(1000 * attempt, cancellationToken);
                }
            }

            Logger.Error($"[清单获取] 全部 {maxRetries} 次尝试均失败");
            throw lastException ?? new Exception("版本清单获取失败");
        }

        private async Task<List<VersionInfo>> FetchVersionManifestOnceAsync(string url, int timeoutMs, CancellationToken cancellationToken)
        {
            Logger.Info($"[清单获取] 开始从 {url} 获取版本列表");

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add(HttpRequestHeader.UserAgent, _userAgent);
                client.Headers.Add(HttpRequestHeader.Accept, "application/json");
                client.Headers.Add(HttpRequestHeader.CacheControl, "no-cache");
                client.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");

                Logger.Info($"[清单获取] UserAgent: {_userAgent}");
                Logger.Info($"[清单获取] 发送请求中...");

                var downloadTask = client.DownloadDataTaskAsync(new Uri(url));
                var timeoutTask = Task.Delay(timeoutMs, cancellationToken);

                var completedTask = await Task.WhenAny(downloadTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Logger.Error($"[清单获取] 请求超时！URL: {url}，超时时间: {timeoutMs}ms");
                    client.CancelAsync();
                    throw new TimeoutException($"请求超时: {url}");
                }

                Logger.Info($"[清单获取] 请求完成，开始读取数据...");
                byte[] data = await downloadTask;

                if (data == null || data.Length == 0)
                {
                    Logger.Error($"[清单获取] 获取到空数据！");
                    throw new Exception("获取到空数据");
                }

                Logger.Info($"[清单获取] 成功获取数据，原始数据长度: {data.Length} 字节");

                string contentEncoding = client.ResponseHeaders[HttpResponseHeader.ContentEncoding];
                string json;

                if (!string.IsNullOrEmpty(contentEncoding) && (contentEncoding.Equals("gzip", StringComparison.OrdinalIgnoreCase) ||
                    contentEncoding.Equals("deflate", StringComparison.OrdinalIgnoreCase)))
                {
                    Logger.Info($"[解压] 检测到 {contentEncoding} 压缩，开始解压...");
                    json = DecompressData(data, contentEncoding);
                    Logger.Info($"[解压] 解压完成，解压后数据长度: {json.Length} 字符");
                }
                else
                {
                    json = Encoding.UTF8.GetString(data);
                    Logger.Info($"[数据] 未压缩数据，直接转换为字符串");
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Error($"[清单获取] 获取到空数据！JSON为空或仅包含空白字符");
                    throw new Exception("获取到空数据");
                }

                Logger.Info($"[清单解析] 开始解析JSON数据...");

                var versions = ParseVersionManifestJson(json);

                if (versions != null && versions.Count > 0)
                {
                    Logger.Info($"[排序] 开始对 {versions.Count} 个版本按发布时间排序（从新到旧）...");
                    versions = versions.OrderByDescending(v => v.ReleaseTime).ToList();
                    Logger.Info($"[排序] 版本列表已按发布时间排序完成");

                    Logger.Info($"[清单统计] 共加载 {versions.Count} 个版本");
                    Logger.Info($"[清单统计] 正式版(release): {versions.Count(v => v.Type == "release")} 个");
                    Logger.Info($"[清单统计] 快照版(snapshot): {versions.Count(v => v.Type == "snapshot")} 个");
                    Logger.Info($"[清单统计] 其他类型: {versions.Count(v => v.Type != "release" && v.Type != "snapshot")} 个");

                    if (versions.Count > 0)
                    {
                        var newest = versions.First();
                        var oldest = versions.Last();
                        Logger.Info($"[清单统计] 最新版本: {newest.Id} (发布于 {newest.ReleaseTime:yyyy-MM-dd})");
                        Logger.Info($"[清单统计] 最旧版本: {oldest.Id} (发布于 {oldest.ReleaseTime:yyyy-MM-dd})");
                    }
                }
                else
                {
                    Logger.Warning($"[清单解析] 解析后版本列表为空！");
                }

                return versions;
            }
        }

        private string DecompressData(byte[] compressedData, string compressionType)
        {
            try
            {
                using (var ms = new MemoryStream(compressedData))
                {
                    Stream decompressionStream;

                    if (compressionType.Equals("gzip", StringComparison.OrdinalIgnoreCase))
                    {
                        decompressionStream = new GZipStream(ms, CompressionMode.Decompress);
                    }
                    else
                    {
                        decompressionStream = new DeflateStream(ms, CompressionMode.Decompress);
                    }

                    using (var reader = new StreamReader(decompressionStream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[解压] 解压失败: {ex.Message}");
                throw;
            }
        }

        private List<VersionInfo> ParseVersionManifestJson(string json)
        {
            List<VersionInfo> versions = new List<VersionInfo>();
            int successCount = 0, failCount = 0;

            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Warning("[JSON解析] JSON数据为空");
                    return versions;
                }

                Logger.Info($"[JSON解析] 开始使用System.Text.Json解析JSON...");

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;

                    if (!root.TryGetProperty("versions", out JsonElement versionsArray))
                    {
                        Logger.Error("[JSON解析] 无法找到versions数组属性");
                        Logger.Error($"[JSON解析] JSON前200字符: {json.Substring(0, Math.Min(200, json.Length))}...");
                        return versions;
                    }

                    if (versionsArray.ValueKind != JsonValueKind.Array)
                    {
                        Logger.Error($"[JSON解析] versions属性不是数组类型，实际类型: {versionsArray.ValueKind}");
                        return versions;
                    }

                    Logger.Info($"[JSON解析] 找到versions数组，包含 {versionsArray.GetArrayLength()} 个版本对象");

                    Logger.Info($"[JSON解析] 开始逐个解析版本对象...");
                    foreach (JsonElement element in versionsArray.EnumerateArray())
                    {
                        try
                        {
                            VersionInfo info = ParseVersionObject(element);
                            if (info != null && !string.IsNullOrEmpty(info.Id))
                            {
                                info.IsDownloaded = CheckVersionDownloaded(info.Id);
                                versions.Add(info);
                                successCount++;

                                if (successCount <= 5)
                                {
                                    Logger.Info($"[JSON解析] 第 {successCount} 个版本解析成功: {info.Id} (类型: {info.Type}, 发布时间: {info.ReleaseTime:yyyy-MM-dd})");
                                }
                                else if (successCount == 6)
                                {
                                    Logger.Info($"[JSON解析] ... (更多版本解析成功，不再详细输出)");
                                }
                            }
                            else
                            {
                                failCount++;
                                if (failCount <= 3)
                                {
                                    Logger.Warning($"[JSON解析] 版本解析失败: info为null或Id为空");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            if (failCount <= 3)
                            {
                                Logger.Warning($"[JSON解析] 版本对象解析异常: {ex.Message}");
                            }
                        }
                    }

                    Logger.Info($"[JSON解析] 版本解析完成！成功: {successCount} 个，失败: {failCount} 个");
                }
            }
            catch (JsonException ex)
            {
                Logger.Error($"[JSON解析] JSON格式解析失败: {ex.Message}");
                Logger.Error($"[JSON解析] 异常堆栈: {ex.StackTrace}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[JSON解析] JSON解析过程发生异常: {ex.Message}");
                Logger.Error($"[JSON解析] 异常堆栈: {ex.StackTrace}");
            }

            return versions;
        }

        private VersionInfo ParseVersionObject(JsonElement element)
        {
            try
            {
                VersionInfo info = new VersionInfo();

                if (element.TryGetProperty("id", out JsonElement idElement))
                {
                    info.Id = idElement.GetString();
                }

                if (element.TryGetProperty("type", out JsonElement typeElement))
                {
                    info.Type = typeElement.GetString();
                }

                if (element.TryGetProperty("url", out JsonElement urlElement))
                {
                    info.Url = urlElement.GetString();
                }

                if (element.TryGetProperty("releaseTime", out JsonElement releaseTimeElement))
                {
                    string releaseTimeStr = releaseTimeElement.GetString();
                    if (!string.IsNullOrEmpty(releaseTimeStr) && DateTime.TryParse(releaseTimeStr, out DateTime releaseTime))
                    {
                        info.ReleaseTime = releaseTime;
                    }
                }

                return !string.IsNullOrEmpty(info.Id) ? info : null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"解析版本对象失败: {ex.Message}");
                return null;
            }
        }

        public bool CheckVersionDownloaded(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
                if (!Directory.Exists(versionDir)) return false;

                string jsonFile = Path.Combine(versionDir, $"{versionId}.json");
                string jarFile = Path.Combine(versionDir, $"{versionId}.jar");

                return File.Exists(jsonFile) && File.Exists(jarFile);
            }
            catch
            {
                return false;
            }
        }

        private bool SafeCreateDirectory(string path)
        {
            int retries = 3;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    Directory.CreateDirectory(path);
                    return true;
                }
                catch (IOException ex)
                {
                    Logger.Warning($"[下载] 创建目录失败(尝试 {i + 1}/{retries}): {path}, 错误: {ex.Message}");
                    if (i == retries - 1)
                    {
                        Logger.Error($"[下载] 创建目录最终失败: {path}");
                        return false;
                    }
                    Thread.Sleep(500);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.Error($"[下载] 无权限访问目录: {path}, 错误: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        public async Task<DownloadTaskInfo> StartDownloadAsync(VersionInfo version, DownloadProgress progress, CancellationToken cancellationToken = default)
        {
            var taskInfo = new DownloadTaskInfo
            {
                VersionId = version.Id,
                Status = "等待中"
            };

            try
            {
                StatusChanged?.Invoke($"正在获取版本信息: {version.Id}...");
                taskInfo.Status = "正在获取版本信息";

                string versionDir = Path.Combine(_minecraftPath, "versions", version.Id);
                Logger.Info($"[下载] 准备创建目录: {versionDir}");

                if (!SafeCreateDirectory(versionDir))
                {
                    throw new IOException($"无法创建或访问目录: {versionDir}，请检查权限或关闭占用该目录的程序。");
                }

                string jsonFile = Path.Combine(versionDir, $"{version.Id}.json");
                string jarFile = Path.Combine(versionDir, $"{version.Id}.jar");

                StatusChanged?.Invoke($"正在下载版本JSON: {version.Id}...");
                taskInfo.Status = "正在下载版本JSON";
                taskInfo.CurrentFile = $"{version.Id}.json";

                await DownloadFileWithProgressAsync(version.Url, jsonFile, progress, cancellationToken);
                progress?.Report(new DownloadProgressInfo { Progress = 5, Status = "[1/5] 版本JSON下载完成", CurrentFile = $"{version.Id}.json" });

                string jarUrl = await GetJarDownloadUrlAsync(jsonFile);
                StatusChanged?.Invoke($"正在下载游戏本体: {version.Id}...");
                taskInfo.Status = "正在下载游戏本体";
                taskInfo.CurrentFile = $"{version.Id}.jar";

                await DownloadFileWithProgressAsync(jarUrl, jarFile, progress, cancellationToken);
                progress?.Report(new DownloadProgressInfo { Progress = 10, Status = "[2/5] 游戏本体下载完成", CurrentFile = $"{version.Id}.jar" });

                var libraries = await GetLibrariesToDownloadAsync(jsonFile);
                StatusChanged?.Invoke($"正在下载 {libraries.Count} 个依赖库...");
                taskInfo.Status = $"正在下载 {libraries.Count} 个依赖库";

                int libIndex = 0;
                int totalFiles = libraries.Count;
                foreach (var lib in libraries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.IsNullOrEmpty(lib.Url) && !string.IsNullOrEmpty(lib.Path))
                    {
                        await DownloadFileWithProgressAsync(lib.Url, lib.Path, progress, cancellationToken);
                    }
                    libIndex++;
                    int progressValue = 10 + (int)((double)libIndex / totalFiles * 40);
                    progress?.Report(new DownloadProgressInfo { Progress = Math.Min(progressValue, 50), Status = $"[库] {libIndex}/{totalFiles}: {lib.Name}", CurrentFile = lib.Name });
                }
                progress?.Report(new DownloadProgressInfo { Progress = 50, Status = $"[3/5] 依赖库下载完成 ({libraries.Count}个)", CurrentFile = "" });

                var natives = await GetNativesToDownloadAsync(jsonFile);
                if (natives.Count > 0)
                {
                    StatusChanged?.Invoke($"正在下载 {natives.Count} 个natives库...");
                    foreach (var native in natives)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!string.IsNullOrEmpty(native.Url) && !string.IsNullOrEmpty(native.Path))
                        {
                            await DownloadFileWithProgressAsync(native.Url, native.Path, progress, cancellationToken);
                        }
                    }
                    StatusChanged?.Invoke($"正在解压natives文件...");
                    await ExtractNativesAsync(version.Id, jsonFile, cancellationToken);
                }

                var assetIndex = await GetAssetIndexInfoAsync(jsonFile);
                if (!string.IsNullOrEmpty(assetIndex.Url) && !string.IsNullOrEmpty(assetIndex.Path))
                {
                    StatusChanged?.Invoke($"正在下载资源索引...");
                    await DownloadFileWithProgressAsync(assetIndex.Url, assetIndex.Path, progress, cancellationToken);

                    StatusChanged?.Invoke($"正在下载资源文件...");
                    int assetCount = await DownloadAssetsAsync(assetIndex.Path, cancellationToken);
                    progress?.Report(new DownloadProgressInfo { Progress = 90, Status = $"资源文件下载完成 ({assetCount}个)", CurrentFile = "" });
                }

                progress?.Report(new DownloadProgressInfo { Progress = 95, Status = "正在校验版本完整性...", CurrentFile = "" });
                await ValidateVersion完整性Async(version.Id, jsonFile, cancellationToken);

                StatusChanged?.Invoke($"下载完成: {version.Id} (包含 {libraries.Count} 个依赖库)");
                taskInfo.Status = "已完成";
                taskInfo.IsCompleted = true;
                taskInfo.Progress = 100;

                DownloadCompleted?.Invoke(version.Id);
            }
            catch (OperationCanceledException)
            {
                taskInfo.Status = "已取消";
                taskInfo.ErrorMessage = "下载已取消";
                Logger.Warning($"[下载] 版本 {version.Id} 下载已取消");
            }
            catch (Exception ex)
            {
                taskInfo.Status = "下载失败";
                taskInfo.ErrorMessage = ex.Message;
                Logger.Error($"[下载] 版本 {version.Id} 下载失败: {ex.Message}");
                Logger.Error($"[下载] 详细堆栈: {ex.StackTrace}");
                DownloadFailed?.Invoke(version.Id, ex.Message);
            }

            return taskInfo;
        }

        private async Task DownloadFileWithProgressAsync(string url, string savePath, int startProgress, int endProgress)
        {
            using (var client = new WebClient())
            {
                client.Headers.Add(HttpRequestHeader.UserAgent, _userAgent);

                long totalBytes = 0;
                long downloadedBytes = 0;

                client.DownloadProgressChanged += (sender, e) =>
                {
                    totalBytes = e.TotalBytesToReceive;
                    downloadedBytes = e.BytesReceived;

                    if (totalBytes > 0)
                    {
                        double fileProgress = (double)downloadedBytes / totalBytes;
                        double totalProgress = startProgress + fileProgress * (endProgress - startProgress);
                        ProgressChanged?.Invoke((int)totalProgress);
                    }
                };

                StatusChanged?.Invoke($"正在下载: {Path.GetFileName(savePath)}");
                await client.DownloadFileTaskAsync(new Uri(url), savePath);
            }
        }

        private async Task DownloadLibrariesAsync(List<LibraryDownloadInfo> libraries, int startProgress, int endProgress)
        {
            int libIndex = 0;
            int totalFiles = libraries.Count;

            foreach (var lib in libraries)
            {
                _cts.Token.ThrowIfCancellationRequested();
                
                if (!string.IsNullOrEmpty(lib.Url) && !string.IsNullOrEmpty(lib.Path))
                {
                    StatusChanged?.Invoke($"[库] {libIndex + 1}/{totalFiles}: {lib.Name}");
                    await DownloadFileAsync(lib.Url, lib.Path, _cts.Token);
                }
                
                libIndex++;
                double progressValue = startProgress + (double)libIndex / totalFiles * (endProgress - startProgress);
                ProgressChanged?.Invoke((int)progressValue);
            }
        }

        private async Task DownloadAndExtractNativesAsync(List<LibraryDownloadInfo> natives, string versionId, int startProgress, int endProgress)
        {
            if (natives.Count > 0)
            {
                int nativeIndex = 0;
                int totalFiles = natives.Count;

                foreach (var native in natives)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    
                    if (!string.IsNullOrEmpty(native.Url) && !string.IsNullOrEmpty(native.Path))
                    {
                        StatusChanged?.Invoke($"[Natives] {nativeIndex + 1}/{totalFiles}: {native.Name}");
                        await DownloadFileAsync(native.Url, native.Path, _cts.Token);
                    }
                    
                    nativeIndex++;
                    double progressValue = startProgress + (double)nativeIndex / totalFiles * (endProgress - startProgress);
                    ProgressChanged?.Invoke((int)progressValue);
                }

                StatusChanged?.Invoke("正在解压natives文件...");
                string jsonFile = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.json");
                await ExtractNativesAsync(versionId, jsonFile, _cts.Token);
            }
        }

        private async Task DownloadFileWithProgressAsync(string url, string savePath, DownloadProgress progress, CancellationToken cancellationToken)
        {
            using (var client = new WebClient())
            {
                client.Headers.Add(HttpRequestHeader.UserAgent, _userAgent);

                long totalBytes = 0;
                long downloadedBytes = 0;

                client.DownloadProgressChanged += (sender, e) =>
                {
                    totalBytes = e.TotalBytesToReceive;
                    downloadedBytes = e.BytesReceived;

                    if (totalBytes > 0)
                    {
                        double progressPercent = (double)downloadedBytes / totalBytes * 100;
                        progress?.Report(new DownloadProgressInfo
                        {
                            Progress = progressPercent,
                            DownloadedBytes = downloadedBytes,
                            TotalBytes = totalBytes,
                            CurrentFile = Path.GetFileName(savePath),
                            Status = "下载中"
                        });
                    }
                };

                await client.DownloadFileTaskAsync(new Uri(url), savePath);
            }
        }

        public async Task StartDownloadAsync(string versionId, CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            StatusChanged?.Invoke($"正在获取版本信息: {versionId}...");

            string versionDetailUrl = await GetVersionDetailUrlAsync(versionId);
            string jsonUrl = versionDetailUrl.Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com");
            
            _versionInfo = new VersionInfo { Id = versionId, Url = jsonUrl };

            await StartDownloadAsync();
        }

        public async Task StartDownloadAsync()
        {
            // 1. 创建版本目录
            string versionDir = Path.Combine(_minecraftPath, "versions", _versionInfo.Id);
            Directory.CreateDirectory(versionDir);
            
            // 2. 下载 JSON 配置文件 (0% - 5%)
            string jsonFile = Path.Combine(versionDir, $"{_versionInfo.Id}.json");
            await DownloadFileWithProgressAsync(_versionInfo.Url, jsonFile, 0, 5);
            
            // 3. 获取并下载依赖库 (5% - 40%)
            var libraries = await GetLibrariesToDownloadAsync(jsonFile);
            await DownloadLibrariesAsync(libraries, 5, 40);
            
            // 4. 获取并下载 natives (40% - 55%)
            var natives = await GetNativesToDownloadAsync(jsonFile);
            await DownloadAndExtractNativesAsync(natives, _versionInfo.Id, 40, 55);
            
            // 5. 下载资源索引和资源文件 (55% - 75%)
            var assetIndex = await GetAssetIndexInfoAsync(jsonFile);
            if (!string.IsNullOrEmpty(assetIndex.Url) && !string.IsNullOrEmpty(assetIndex.Path))
            {
                StatusChanged?.Invoke($"正在下载资源索引...");
                await DownloadFileWithProgressAsync(assetIndex.Url, assetIndex.Path, 55, 60);

                StatusChanged?.Invoke($"正在下载资源文件...");
                int assetCount = await DownloadAssetsAsync(assetIndex.Path, CancellationToken.None);
                StatusChanged?.Invoke($"资源文件下载完成 ({assetCount}个)");
            }
            
            // 6. 下载 JAR 文件 (75% - 100%)
            string jarUrl = await GetJarDownloadUrlAsync(jsonFile);
            string jarFile = Path.Combine(versionDir, $"{_versionInfo.Id}.jar");
            await DownloadFileWithProgressAsync(jarUrl, jarFile, 75, 100);
            
            DownloadCompleted?.Invoke(_versionInfo.Id);
        }

        private async Task ExtractNativesAsync(string versionId, string jsonFile, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    // 使用版本独立的natives目录
                    string versionPath = Path.Combine(_minecraftPath, "versions", versionId);
                    string nativesDir = Path.Combine(versionPath, $"{versionId}-natives");
                    nativesDir = Path.GetFullPath(nativesDir);

                    if (Directory.Exists(nativesDir))
                    {
                        foreach (string file in Directory.GetFiles(nativesDir))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                    else
                    {
                        Directory.CreateDirectory(nativesDir);
                    }

                    string json = File.ReadAllText(jsonFile);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        string nativeClassifier = GetNativeClassifier(doc.RootElement);

                        if (doc.RootElement.TryGetProperty("libraries", out JsonElement libraries))
                        {
                            foreach (JsonElement library in libraries.EnumerateArray())
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                if (!library.TryGetProperty("downloads", out JsonElement downloads) ||
                                    !downloads.TryGetProperty("classifiers", out JsonElement classifiers))
                                    continue;

                                if (!classifiers.TryGetProperty(nativeClassifier, out JsonElement nativeInfo))
                                    continue;

                                string path = nativeInfo.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
                                if (string.IsNullOrEmpty(path))
                                    continue;

                                string libPath = Path.Combine(_minecraftPath, "libraries", path);
                                if (File.Exists(libPath))
                                {
                                    ExtractZipToDirectory(libPath, nativesDir);
                                }
                            }
                        }
                    }
                    Logger.Info($"[Natives] 提取完成: {nativesDir}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Natives] 提取失败: {ex.Message}");
                }
            }, cancellationToken);
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
                    if (!file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                        !file.EndsWith(".dll.xdelta", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Extract] 解压失败 {zipPath}: {ex.Message}");
            }
        }

        private async Task<int> DownloadAssetsAsync(string assetIndexPath, CancellationToken cancellationToken)
        {
            int assetCount = 0;
            try
            {
                if (!File.Exists(assetIndexPath))
                {
                    Logger.Warning($"[Assets] 资源索引文件不存在: {assetIndexPath}");
                    return 0;
                }

                string json = await Task.Run(() => File.ReadAllText(assetIndexPath));
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("objects", out JsonElement objects))
                    {
                        var assetList = new List<(string hash, string path)>();
                        // objects 是一个对象，不是数组，需要使用 EnumerateObject()
                        int totalInIndex = 0;
                        foreach (var obj in objects.EnumerateObject())
                        {
                            totalInIndex++;
                            string hash = obj.Value.GetProperty("hash").GetString();
                            if (!string.IsNullOrEmpty(hash))
                            {
                                string subdir = hash.Substring(0, 2);
                                string assetPath = Path.Combine(_minecraftPath, "assets", "objects", subdir, hash);
                                if (!File.Exists(assetPath))
                                {
                                    string url = $"https://bmclapi2.bangbang93.com/assets/{subdir}/{hash}";
                                    assetList.Add((hash, assetPath));
                                }
                            }
                        }
                        
                        Logger.Info($"[Assets] 资源索引中共有 {totalInIndex} 个资源，需要下载 {assetList.Count} 个");

                        int idx = 0;
                        int threadCount = SettingsManager.Settings.DownloadThreads;
                        var semaphore = new SemaphoreSlim(threadCount);
                        var tasks = new List<Task>();
                        
                        foreach (var asset in assetList)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            await semaphore.WaitAsync(cancellationToken);
                            
                            var task = Task.Run(async () =>
                            {
                                try
                                {
                                    string hash = asset.hash;
                                    string url = hash.StartsWith("http") ? hash : $"https://bmclapi2.bangbang93.com/assets/{hash.Substring(0, 2)}/{hash}";
                                    await DownloadFileAsync(url, asset.path, cancellationToken);
                                    
                                    int current = Interlocked.Increment(ref idx);
                                    if (current % 100 == 0)
                                    {
                                        StatusChanged?.Invoke($"[资源] {current}/{assetList.Count}");
                                    }
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            }, cancellationToken);
                            
                            tasks.Add(task);
                        }
                        
                        await Task.WhenAll(tasks);
                        assetCount = assetList.Count;
                        Logger.Info($"[Assets] 资源下载完成，共下载 {assetCount} 个资源文件");
                    }
                    else
                    {
                        Logger.Warning($"[Assets] 资源索引中没有 'objects' 字段");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Assets] 下载失败: {ex.Message}");
            }
            return assetCount;
        }

        private async Task ValidateVersion完整性Async(string versionId, string jsonFile, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Run(() =>
                {
                    string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
                    string jarFile = Path.Combine(versionDir, $"{versionId}.jar");

                    if (!File.Exists(jarFile))
                    {
                        throw new Exception("游戏JAR文件缺失");
                    }

                    string json = File.ReadAllText(jsonFile);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("libraries", out JsonElement libraries))
                        {
                            foreach (JsonElement lib in libraries.EnumerateArray())
                            {
                                string name = lib.TryGetProperty("name", out var n) ? n.GetString() : null;
                                if (string.IsNullOrEmpty(name))
                                    continue;

                                string path = GetLibraryPathFromName(name);
                                if (string.IsNullOrEmpty(path))
                                    continue;

                                string libPath = Path.Combine(_minecraftPath, "libraries", path);
                                if (!File.Exists(libPath))
                                {
                                    Logger.Warning($"[校验] 缺失依赖库: {path}");
                                }
                            }
                        }
                    }

                    // 使用版本独立的natives目录
                    string versionPath = Path.Combine(_minecraftPath, "versions", versionId);
                    string nativesDir = Path.Combine(versionPath, $"{versionId}-natives");
                    if (!Directory.Exists(nativesDir) || Directory.GetFiles(nativesDir, "*.dll").Length == 0)
                    {
                        Logger.Warning($"[校验] natives目录缺失或为空");
                    }

                    Logger.Info($"[校验] 版本 {versionId} 完整性校验完成");
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error($"[校验] 版本校验失败: {ex.Message}");
            }
        }

        private async Task<string> GetVersionDetailUrlAsync(string versionId)
        {
            for (int i = 0; i < _manifestUrls.Length; i++)
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        client.Headers.Add(HttpRequestHeader.UserAgent, _userAgent);
                        client.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");

                        byte[] data = await client.DownloadDataTaskAsync(_manifestUrls[i]);

                        string contentEncoding = client.ResponseHeaders[HttpResponseHeader.ContentEncoding];
                        string manifestJson;

                        if (!string.IsNullOrEmpty(contentEncoding) && (contentEncoding.Equals("gzip", StringComparison.OrdinalIgnoreCase) ||
                            contentEncoding.Equals("deflate", StringComparison.OrdinalIgnoreCase)))
                        {
                            manifestJson = DecompressData(data, contentEncoding);
                        }
                        else
                        {
                            manifestJson = Encoding.UTF8.GetString(data);
                        }

                        using (JsonDocument doc = JsonDocument.Parse(manifestJson))
                        {
                            JsonElement root = doc.RootElement;

                            if (root.TryGetProperty("versions", out JsonElement versionsArray) &&
                                versionsArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement element in versionsArray.EnumerateArray())
                                {
                                    if (element.TryGetProperty("id", out JsonElement idElement))
                                    {
                                        string id = idElement.GetString();
                                        if (id == versionId)
                                        {
                                            if (element.TryGetProperty("url", out JsonElement urlElement))
                                            {
                                                return urlElement.GetString();
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

            throw new Exception($"无法找到版本 {versionId} 的详情URL");
        }

        private async Task<string> GetJarDownloadUrlAsync(string jsonFile)
        {
            try
            {
                string json = await Task.Run(() => File.ReadAllText(jsonFile));
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("downloads", out JsonElement downloads) &&
                        downloads.TryGetProperty("client", out JsonElement client) &&
                        client.TryGetProperty("url", out JsonElement urlElement))
                    {
                        string url = urlElement.GetString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            return url.Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com");
                        }
                    }
                }
            }
            catch { }

            string fileName = Path.GetFileNameWithoutExtension(jsonFile);
            return $"https://bmclapi2.bangbang93.com/minecraft/client/{fileName}.jar";
        }

        private async Task<List<LibraryDownloadInfo>> GetLibrariesToDownloadAsync(string jsonFile)
        {
            var libraries = new List<LibraryDownloadInfo>();

            try
            {
                string json = await Task.Run(() => File.ReadAllText(jsonFile));
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("libraries", out JsonElement libsElement))
                    {
                        foreach (JsonElement lib in libsElement.EnumerateArray())
                        {
                            // 检查rules过滤，只保留适用于Windows的依赖库
                            if (!IsLibraryApplicable(lib))
                                continue;

                            string name = lib.TryGetProperty("name", out var n) ? n.GetString() : null;
                            if (string.IsNullOrEmpty(name))
                                continue;

                            string path = GetLibraryPathFromName(name);
                            if (string.IsNullOrEmpty(path))
                                continue;

                            string fullPath = Path.Combine(_minecraftPath, "libraries", path);
                            
                            // 获取 SHA1 哈希值用于校验
                            string sha1 = null;
                            if (lib.TryGetProperty("downloads", out var downloads) &&
                                downloads.TryGetProperty("artifact", out var artifact) &&
                                artifact.TryGetProperty("sha1", out var sha1Element))
                            {
                                sha1 = sha1Element.GetString();
                            }

                            // 检查文件是否存在且完整
                            if (File.Exists(fullPath))
                            {
                                if (!string.IsNullOrEmpty(sha1) && !VerifyFileHash(fullPath, sha1))
                                {
                                    Logger.Warning($"[校验] 库文件 {path} 校验失败，将重新下载");
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            string url = GetLibraryDownloadUrl(path);

                            libraries.Add(new LibraryDownloadInfo
                            {
                                Name = name,
                                Path = fullPath,
                                Url = url
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[下载] 解析依赖库失败: {ex.Message}");
            }

            return libraries;
        }

        private bool IsLibraryApplicable(JsonElement library)
        {
            // 检查是否有rules属性
            if (!library.TryGetProperty("rules", out JsonElement rulesElement))
                return true; // 没有rules则适用于所有系统

            bool allow = true;

            foreach (JsonElement rule in rulesElement.EnumerateArray())
            {
                if (!rule.TryGetProperty("action", out JsonElement actionElement))
                    continue;

                string action = actionElement.GetString();

                if (rule.TryGetProperty("os", out JsonElement osElement))
                {
                    // 检查OS名称
                    if (osElement.TryGetProperty("name", out JsonElement osNameElement))
                    {
                        string osName = osNameElement.GetString();
                        // 如果指定了非Windows系统，根据action决定
                        if (osName != "windows")
                        {
                            if (action == "allow")
                                allow = false; // 只允许其他系统，Windows不允许
                            else if (action == "disallow")
                                allow = true; // 不允许其他系统，Windows允许
                        }
                    }
                    // 检查OS架构（忽略ARM等）
                    else if (osElement.TryGetProperty("arch", out JsonElement archElement))
                    {
                        string arch = archElement.GetString();
                        if (arch != "x86" && arch != "x64")
                        {
                            allow = false; // 非x86/x64架构，Windows不适用
                        }
                    }
                }
                else
                {
                    // 没有os条件，直接根据action设置
                    allow = action == "allow";
                }
            }

            return allow;
        }

        private async Task<List<LibraryDownloadInfo>> GetNativesToDownloadAsync(string jsonFile)
        {
            var natives = new List<LibraryDownloadInfo>();

            try
            {
                string json = await Task.Run(() => File.ReadAllText(jsonFile));
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    string nativeClassifier = GetNativeClassifier(doc.RootElement);

                    if (doc.RootElement.TryGetProperty("libraries", out JsonElement libsElement))
                    {
                        foreach (JsonElement lib in libsElement.EnumerateArray())
                        {
                            if (!lib.TryGetProperty("downloads", out var downloads))
                                continue;

                            if (!downloads.TryGetProperty("classifiers", out var classifiers))
                                continue;

                            if (!classifiers.TryGetProperty(nativeClassifier, out var nativeInfo))
                                continue;

                            string path = nativeInfo.TryGetProperty("path", out var p) ? p.GetString() : null;
                            if (string.IsNullOrEmpty(path))
                                continue;

                            string fullPath = Path.Combine(_minecraftPath, "libraries", path);
                            if (File.Exists(fullPath))
                                continue;

                            string url = GetLibraryDownloadUrl(path);

                            natives.Add(new LibraryDownloadInfo
                            {
                                Name = path,
                                Path = fullPath,
                                Url = url
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[下载] 解析natives失败: {ex.Message}");
            }

            return natives;
        }

        private async Task<AssetIndexInfo> GetAssetIndexInfoAsync(string jsonFile)
        {
            var info = new AssetIndexInfo();

            try
            {
                string json = await Task.Run(() => File.ReadAllText(jsonFile));
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("assetIndex", out var assetIndex))
                    {
                        string id = assetIndex.TryGetProperty("id", out var i) ? i.GetString() : "1.16";
                        string url = assetIndex.TryGetProperty("url", out var u) ? u.GetString() : null;

                        if (string.IsNullOrEmpty(url))
                        {
                            string assetId = assetIndex.TryGetProperty("id", out var a) ? a.GetString() : "1.16";
                            url = $"https://bmclapi2.bangbang93.com/minecraft/indexes/{assetId}.json";
                        }

                        string indexPath = Path.Combine(_minecraftPath, "assets", "indexes", $"{id}.json");
                        info.Path = indexPath;
                        info.Url = url;
                        info.Id = id;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[下载] 解析资源索引失败: {ex.Message}");
            }

            return info;
        }

        private string GetLibraryPathFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            string[] parts = name.Split(':');
            if (parts.Length < 2)
                return null;

            string groupId = parts[0].Replace('.', '/');
            string artifactId = parts[1];
            string version = parts.Length > 2 ? parts[2] : "1.0";

            string fileName;
            if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3]))
            {
                fileName = $"{artifactId}-{version}-{parts[3]}.jar";
            }
            else
            {
                fileName = $"{artifactId}-{version}.jar";
            }

            return $"{groupId}/{artifactId}/{version}/{fileName}";
        }

        private string GetLibraryDownloadUrl(string libPath)
        {
            if (string.IsNullOrEmpty(libPath))
                return null;

            string encodedPath = libPath.Replace('\\', '/');
            
            // icu4j 库使用官方源（BMCLAPI 的 maven 路径不可用）
            if (encodedPath.StartsWith("com/ibm/icu/"))
            {
                return $"https://libraries.minecraft.net/{encodedPath}";
            }
            
            return $"https://bmclapi2.bangbang93.com/libraries/{encodedPath}";
        }

        private string GetNativeClassifier(JsonElement versionInfo)
        {
            string nativesKey = "natives-windows";

            // 检测系统架构
            bool is64Bit = Environment.Is64BitOperatingSystem;

            if (versionInfo.TryGetProperty("libraries", out JsonElement libs))
            {
                foreach (JsonElement lib in libs.EnumerateArray())
                {
                    if (lib.TryGetProperty("downloads", out var downloads) &&
                        downloads.TryGetProperty("classifiers", out var classifiers))
                    {
                        // 优先使用 x64 版本的 natives（如果存在且是64位系统）
                        if (is64Bit)
                        {
                            if (classifiers.TryGetProperty("natives-windows-x64", out var x64Info))
                            {
                                nativesKey = "natives-windows-x64";
                                break;
                            }
                        }

                        // 回退到普通的 natives-windows
                        if (classifiers.TryGetProperty("natives-windows", out var nativeInfo))
                        {
                            nativesKey = is64Bit ? "natives-windows-x64" : "natives-windows";
                            break;
                        }
                    }
                }
            }

            return nativesKey;
        }

        private async Task DownloadFileAsync(string url, string savePath, CancellationToken cancellationToken)
        {
            int retryCount = 5;
            int delayMs = 1000;
            
            // 确保目录存在
            string dir = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 如果文件存在且为空，先删除
            if (File.Exists(savePath))
            {
                var fileInfo = new FileInfo(savePath);
                if (fileInfo.Length == 0)
                {
                    File.Delete(savePath);
                }
            }

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();
                        
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            await stream.CopyToAsync(fileStream, 81920, cancellationToken);
                        }
                    }
                    return;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    // 下载失败时删除可能的不完整文件
                    if (File.Exists(savePath))
                    {
                        try
                        {
                            File.Delete(savePath);
                        }
                        catch { }
                    }
                    
                    if (i == retryCount - 1)
                    {
                        Logger.Error($"[Assets] 下载失败: {ex.Message}");
                        throw;
                    }
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2;
                }
            }
        }

        private bool VerifyFileHash(string filePath, string expectedHash)
        {
            try
            {
                using (var stream = File.OpenRead(filePath))
                using (var sha1 = SHA1.Create())
                {
                    byte[] hash = sha1.ComputeHash(stream);
                    string actualHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    return actualHash == expectedHash?.ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[校验] 计算文件哈希失败: {ex.Message}");
                return false;
            }
        }

        public async Task<List<VersionInfo>> GetLocalVersionsAsync()
        {
            List<VersionInfo> versions = new List<VersionInfo>();

            try
            {
                string versionsDir = Path.Combine(_minecraftPath, "versions");
                if (!Directory.Exists(versionsDir)) return versions;

                string[] directories = Directory.GetDirectories(versionsDir);

                foreach (string dir in directories)
                {
                    try
                    {
                        string versionId = Path.GetFileName(dir);
                        if (string.IsNullOrEmpty(versionId)) continue;

                        string jsonFile = Path.Combine(dir, $"{versionId}.json");
                        string jarFile = Path.Combine(dir, $"{versionId}.jar");

                        if (File.Exists(jsonFile) && File.Exists(jarFile))
                        {
                            var fileInfo = new FileInfo(jsonFile);
                            versions.Add(new VersionInfo
                            {
                                Id = versionId,
                                Type = "release",
                                ReleaseTime = fileInfo.LastWriteTime,
                                IsDownloaded = true
                            });
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"获取本地版本失败: {ex.Message}");
            }

            return versions;
        }

        public async Task<List<VersionInfo>> GetRemoteVersionsWithSourceAsync(CancellationToken cancellationToken = default)
        {
            int downloadSource = SettingsManager.Settings.ToolDownloadVersion;
            List<string> errors = new List<string>();

            var sourceConfigs = GetSourceConfigs(downloadSource);

            foreach (var config in sourceConfigs)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();

                StatusChanged?.Invoke($"正在尝试 {config.Name}...");

                try
                {
                    using (var cts = new CancellationTokenSource(config.TimeoutMs))
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
                    {
                        var versions = await FetchVersionManifestAsync(config.Url, linkedCts.Token);
                        if (versions != null && versions.Count > 0)
                        {
                            StatusChanged?.Invoke($"成功从 {config.Name} 获取 {versions.Count} 个版本");
                            return versions;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add($"{config.Name}: {ex.Message}");
                    StatusChanged?.Invoke($"{config.Name} 失败，尝试下一个...");
                }
            }

            throw new Exception($"所有数据源均失败:\n{string.Join("\n", errors)}");
        }

        private List<SourceConfig> GetSourceConfigs(int downloadSource)
        {
            var configs = new List<SourceConfig>();

            switch (downloadSource)
            {
                case 0:
                    configs.Add(new SourceConfig("BMCLAPI镜像", _manifestUrls[0], 30000));
                    configs.Add(new SourceConfig("Mojang官方源", _manifestUrls[1], 60000));
                    break;
                case 1:
                    configs.Add(new SourceConfig("Mojang官方源", _manifestUrls[1], 5000));
                    configs.Add(new SourceConfig("BMCLAPI镜像", _manifestUrls[0], 30000));
                    break;
                case 2:
                    configs.Add(new SourceConfig("Mojang官方源", _manifestUrls[1], 60000));
                    break;
                default:
                    configs.Add(new SourceConfig("BMCLAPI镜像", _manifestUrls[0], 30000));
                    configs.Add(new SourceConfig("Mojang官方源", _manifestUrls[1], 60000));
                    break;
            }

            return configs;
        }

        private class SourceConfig
        {
            public string Name { get; }
            public string Url { get; }
            public int TimeoutMs { get; }

            public SourceConfig(string name, string url, int timeoutMs)
            {
                Name = name;
                Url = url;
                TimeoutMs = timeoutMs;
            }
        }
    }
}