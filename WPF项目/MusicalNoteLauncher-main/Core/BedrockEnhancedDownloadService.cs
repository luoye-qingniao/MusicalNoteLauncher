using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class BedrockEnhancedDownloadService : IDisposable
    {
        private readonly string[] _manifestUrls = new[]
        {
            "https://bmclapi2.bangbang93.com/bedrock/version_manifest.json",
            "https://data.mcappx.com/v2/bedrock.json"
        };

        private readonly string[] _sourceNames = new[]
        {
            "BMCLAPI镜像",
            "mcappx.com"
        };

        private readonly string _minecraftPath;
        private readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private CancellationTokenSource _currentCts;
        private bool _disposed;

        public event Action<string> StatusChanged;
        public event Action<string> DownloadCompleted;
        public event Action<string, string> DownloadFailed;

        public BedrockEnhancedDownloadService(string minecraftPath)
        {
            _minecraftPath = minecraftPath ?? throw new ArgumentNullException(nameof(minecraftPath));
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

        public async Task<List<BedrockVersionInfo>> GetRemoteVersionsAsync(CancellationToken cancellationToken = default)
        {
            List<string> errors = new List<string>();

            for (int i = 0; i < _manifestUrls.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();

                string sourceName = _sourceNames[i];
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

        private async Task<List<BedrockVersionInfo>> FetchVersionManifestAsync(string url, CancellationToken cancellationToken)
        {
            return await FetchVersionManifestAsync(url, 120000, cancellationToken);
        }

        private async Task<List<BedrockVersionInfo>> FetchVersionManifestAsync(string url, int timeoutMs, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Logger.Info($"[基岩版清单] 第 {attempt}/{maxRetries} 次尝试从 {url} 获取版本列表，超时: {timeoutMs}ms");
                    return await FetchVersionManifestOnceAsync(url, timeoutMs, cancellationToken);
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    lastException = ex;
                    Logger.Warning($"[基岩版清单] 第 {attempt} 次尝试失败: {ex.Message}，{maxRetries - attempt} 次重试机会");
                    await Task.Delay(1000 * attempt, cancellationToken);
                }
            }

            Logger.Error($"[基岩版清单] 全部 {maxRetries} 次尝试均失败");
            throw lastException ?? new Exception("基岩版版本清单获取失败");
        }

        private async Task<List<BedrockVersionInfo>> FetchVersionManifestOnceAsync(string url, int timeoutMs, CancellationToken cancellationToken)
        {
            Logger.Info($"[基岩版清单] 开始从 {url} 获取版本列表");

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add(HttpRequestHeader.UserAgent, _userAgent);
                client.Headers.Add(HttpRequestHeader.Accept, "application/json");
                client.Headers.Add(HttpRequestHeader.CacheControl, "no-cache");
                client.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");

                Logger.Info($"[基岩版清单] UserAgent: {_userAgent}");
                Logger.Info($"[基岩版清单] 发送请求中...");

                var downloadTask = client.DownloadDataTaskAsync(new Uri(url));
                var timeoutTask = Task.Delay(timeoutMs, cancellationToken);

                var completedTask = await Task.WhenAny(downloadTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Logger.Error($"[基岩版清单] 请求超时！URL: {url}，超时时间: {timeoutMs}ms");
                    client.CancelAsync();
                    throw new TimeoutException($"请求超时: {url}");
                }

                Logger.Info($"[基岩版清单] 请求完成，开始读取数据...");
                byte[] data = await downloadTask;

                if (data == null || data.Length == 0)
                {
                    Logger.Error($"[基岩版清单] 获取到空数据！");
                    throw new Exception("获取到空数据");
                }

                Logger.Info($"[基岩版清单] 成功获取数据，原始数据长度: {data.Length} 字节");

                string contentEncoding = client.ResponseHeaders[HttpResponseHeader.ContentEncoding];
                string json;

                if (!string.IsNullOrEmpty(contentEncoding) && (contentEncoding.Equals("gzip", StringComparison.OrdinalIgnoreCase) ||
                    contentEncoding.Equals("deflate", StringComparison.OrdinalIgnoreCase)))
                {
                    Logger.Info($"[基岩版解压] 检测到 {contentEncoding} 压缩，开始解压...");
                    json = DecompressData(data, contentEncoding);
                    Logger.Info($"[基岩版解压] 解压完成，解压后数据长度: {json.Length} 字符");
                }
                else
                {
                    json = Encoding.UTF8.GetString(data);
                    Logger.Info($"[基岩版数据] 未压缩数据，直接转换为字符串");
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Error($"[基岩版清单] 获取到空数据！JSON为空或仅包含空白字符");
                    throw new Exception("获取到空数据");
                }

                Logger.Info($"[基岩版解析] 开始解析JSON数据...");

                var versions = ParseVersionManifestJson(json);

                if (versions != null && versions.Count > 0)
                {
                    Logger.Info($"[基岩版排序] 开始对 {versions.Count} 个版本按发布时间排序（从新到旧）...");
                    versions = versions.OrderByDescending(v => v.ReleaseTime).ToList();
                    Logger.Info($"[基岩版排序] 版本列表已按发布时间排序完成");

                    Logger.Info($"[基岩版统计] 共加载 {versions.Count} 个版本");
                    Logger.Info($"[基岩版统计] 正式版(release): {versions.Count(v => v.Type == "release")} 个");
                    Logger.Info($"[基岩版统计] 预览版(preview): {versions.Count(v => v.Type == "preview")} 个");
                    Logger.Info($"[基岩版统计] 测试版(beta): {versions.Count(v => v.Type == "beta")} 个");
                    Logger.Info($"[基岩版统计] 其他类型: {versions.Count(v => v.Type != "release" && v.Type != "preview" && v.Type != "beta")} 个");

                    if (versions.Count > 0)
                    {
                        var newest = versions.First();
                        var oldest = versions.Last();
                        Logger.Info($"[基岩版统计] 最新版本: {newest.Id} (发布于 {newest.ReleaseTime:yyyy-MM-dd})");
                        Logger.Info($"[基岩版统计] 最旧版本: {oldest.Id} (发布于 {oldest.ReleaseTime:yyyy-MM-dd})");
                    }
                }
                else
                {
                    Logger.Warning($"[基岩版解析] 解析后版本列表为空！");
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
                Logger.Error($"[基岩版解压] 解压失败: {ex.Message}");
                throw;
            }
        }

        private List<BedrockVersionInfo> ParseVersionManifestJson(string json)
        {
            List<BedrockVersionInfo> versions = new List<BedrockVersionInfo>();
            int successCount = 0, failCount = 0;

            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Warning("[基岩版解析] JSON数据为空");
                    return versions;
                }

                Logger.Info($"[基岩版解析] 开始使用System.Text.Json解析JSON...");

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        Logger.Info($"[基岩版解析] 找到数组格式，包含 {root.GetArrayLength()} 个版本对象");

                        foreach (JsonElement element in root.EnumerateArray())
                        {
                            try
                            {
                                BedrockVersionInfo info = ParseVersionObject(element);
                                if (info != null && !string.IsNullOrEmpty(info.Id))
                                {
                                    info.IsDownloaded = IsVersionDownloaded(info.Id);
                                    versions.Add(info);
                                    successCount++;

                                    if (successCount <= 5)
                                    {
                                        Logger.Info($"[基岩版解析] 第 {successCount} 个版本解析成功: {info.Id} (类型: {info.Type}, 发布时间: {info.ReleaseTime:yyyy-MM-dd})");
                                    }
                                    else if (successCount == 6)
                                    {
                                        Logger.Info($"[基岩版解析] ... (更多版本解析成功，不再详细输出)");
                                    }
                                }
                                else
                                {
                                    failCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                failCount++;
                                if (failCount <= 3)
                                {
                                    Logger.Warning($"[基岩版解析] 版本对象解析异常: {ex.Message}");
                                }
                            }
                        }
                    }
                    else if (root.TryGetProperty("From_mcappx.com", out JsonElement versionsDict))
                    {
                        Logger.Info($"[基岩版解析] 找到mcappx格式，开始解析版本字典...");

                        foreach (var kv in versionsDict.EnumerateObject())
                        {
                            try
                            {
                                var info = ParseMcappxEntry(kv);
                                if (info != null)
                                {
                                    versions.Add(info);
                                    successCount++;

                                    if (successCount <= 5)
                                    {
                                        Logger.Info($"[基岩版解析] 第 {successCount} 个版本解析成功: {info.Id} (类型: {info.Type})");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                failCount++;
                                if (failCount <= 3)
                                {
                                    Logger.Warning($"[基岩版解析] mcappx条目解析异常: {ex.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        Logger.Error("[基岩版解析] 无法识别JSON格式");
                        Logger.Error($"[基岩版解析] JSON前200字符: {json.Substring(0, Math.Min(200, json.Length))}...");
                    }

                    Logger.Info($"[基岩版解析] 版本解析完成！成功: {successCount} 个，失败: {failCount} 个");
                }
            }
            catch (JsonException ex)
            {
                Logger.Error($"[基岩版解析] JSON格式解析失败: {ex.Message}");
                Logger.Error($"[基岩版解析] 异常堆栈: {ex.StackTrace}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[基岩版解析] JSON解析过程发生异常: {ex.Message}");
                Logger.Error($"[基岩版解析] 异常堆栈: {ex.StackTrace}");
            }

            return versions;
        }

        private BedrockVersionInfo ParseVersionObject(JsonElement element)
        {
            try
            {
                BedrockVersionInfo info = new BedrockVersionInfo();

                if (element.TryGetProperty("id", out JsonElement idElement))
                {
                    info.Id = idElement.GetString();
                }

                if (element.TryGetProperty("type", out JsonElement typeElement))
                {
                    info.Type = typeElement.GetString()?.ToLowerInvariant() ?? "release";
                }

                if (element.TryGetProperty("url", out JsonElement urlElement))
                {
                    info.InstallerUrl = urlElement.GetString();
                }

                if (element.TryGetProperty("version", out JsonElement verElement))
                {
                    info.Version = verElement.GetString() ?? info.Id;
                }

                if (element.TryGetProperty("releaseTime", out JsonElement releaseTimeElement))
                {
                    string releaseTimeStr = releaseTimeElement.GetString();
                    if (!string.IsNullOrEmpty(releaseTimeStr) && DateTime.TryParse(releaseTimeStr, out DateTime releaseTime))
                    {
                        info.ReleaseTime = releaseTime;
                    }
                }
                else if (element.TryGetProperty("date", out JsonElement dateElement))
                {
                    string dateStr = dateElement.GetString();
                    if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out DateTime date))
                    {
                        info.ReleaseTime = date;
                    }
                }

                return !string.IsNullOrEmpty(info.Id) ? info : null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[基岩版解析] 解析版本对象失败: {ex.Message}");
                return null;
            }
        }

        private BedrockVersionInfo ParseMcappxEntry(JsonProperty kv)
        {
            string versionKey = kv.Name;
            var item = kv.Value;

            string type = "release";
            if (item.TryGetProperty("Type", out var t))
                type = t.GetString()?.ToLowerInvariant() ?? "release";

            string id = item.TryGetProperty("ID", out var vid) ? vid.GetString() : versionKey;
            string date = item.TryGetProperty("Date", out var d) ? d.GetString() : "";

            string installerUrl = null;
            if (item.TryGetProperty("Variations", out var variations))
            {
                foreach (var v in variations.EnumerateArray())
                {
                    string arch = v.TryGetProperty("Arch", out var a) ? a.GetString() : "";
                    if (arch != "x64") continue;

                    if (v.TryGetProperty("MetaData", out var md) && md.GetArrayLength() > 0)
                    {
                        string metaFirst = md[0].GetString();
                        if (!string.IsNullOrEmpty(metaFirst) && metaFirst.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            installerUrl = metaFirst;
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(id)) return null;

            var info = new BedrockVersionInfo
            {
                Id = id,
                Version = versionKey,
                Type = type,
                InstallerUrl = installerUrl,
                IsDownloaded = IsVersionDownloaded(id)
            };

            if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var releaseTime))
                info.ReleaseTime = releaseTime;

            return info;
        }

        public List<BedrockVersionInfo> GetInstalledVersions()
        {
            var versions = new List<BedrockVersionInfo>();
            try
            {
                string bedrockDir = Path.Combine(_minecraftPath, "bedrock");
                if (!Directory.Exists(bedrockDir)) return versions;

                foreach (string dir in Directory.GetDirectories(bedrockDir))
                {
                    string versionId = Path.GetFileName(dir);
                    string exePath = Path.Combine(dir, "Minecraft.Windows.exe");
                    if (File.Exists(exePath))
                    {
                        versions.Add(new BedrockVersionInfo
                        {
                            Id = versionId,
                            Version = versionId,
                            Type = "release",
                            IsDownloaded = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[基岩版] 获取已安装版本失败: {ex.Message}");
            }
            return versions;
        }

        public async Task<BedrockVersionInfo> GetLatestStableVersionAsync()
        {
            var versions = await GetRemoteVersionsAsync();
            return versions
                .Where(v => v.Type == "release")
                .OrderByDescending(v => v.ReleaseTime)
                .FirstOrDefault();
        }

        private bool IsVersionDownloaded(string versionId)
        {
            try
            {
                return File.Exists(Path.Combine(_minecraftPath, "bedrock", versionId, "Minecraft.Windows.exe"));
            }
            catch { return false; }
        }

        public async Task<DownloadTaskInfo> StartDownloadAsync(
            BedrockVersionInfo version,
            DownloadProgress progress = null,
            CancellationToken cancellationToken = default)
        {
            var taskInfo = new DownloadTaskInfo
            {
                VersionId = version.Id,
                Status = "准备中"
            };

            _currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _currentCts.Token;

            try
            {
                string versionDir = Path.Combine(_minecraftPath, "bedrock", version.Id);
                Directory.CreateDirectory(versionDir);

                string installerPath = Path.Combine(versionDir, "installer.msixbundle");
                string hashPath = Path.Combine(versionDir, "installer.sha1");

                if (File.Exists(installerPath) && File.Exists(hashPath))
                {
                    string storedHash = File.ReadAllText(hashPath).Trim();
                    if (!string.IsNullOrEmpty(storedHash) && VerifySha1(installerPath, storedHash))
                    {
                        taskInfo.Status = "已完成";
                        taskInfo.IsCompleted = true;
                        taskInfo.Progress = 100;
                        taskInfo.CurrentFile = "installer.msixbundle";
                        StatusChanged?.Invoke($"{version.Id} 已下载完成（文件已存在）");
                        DownloadCompleted?.Invoke(version.Id);
                        return taskInfo;
                    }
                }

                taskInfo.Status = "下载中";
                taskInfo.CurrentFile = "installer.msixbundle";
                StatusChanged?.Invoke($"正在下载基岩版 {version.Id}...");

                await DownloadFileWithProgressAsync(version.InstallerUrl, installerPath, progress, token);

                taskInfo.Status = "校验中";
                StatusChanged?.Invoke("正在校验文件完整性...");
                string computedHash = ComputeSha1(installerPath);
                File.WriteAllText(hashPath, computedHash);
                Logger.Info($"[基岩版下载] {version.Id} SHA1: {computedHash}");

                taskInfo.Status = "已完成";
                taskInfo.IsCompleted = true;
                taskInfo.Progress = 100;

                StatusChanged?.Invoke($"基岩版 {version.Id} 下载完成");
                DownloadCompleted?.Invoke(version.Id);

                await CheckRuntimeDependenciesAsync();
            }
            catch (OperationCanceledException)
            {
                taskInfo.Status = "已取消";
                taskInfo.ErrorMessage = "下载已取消";
            }
            catch (Exception ex)
            {
                taskInfo.Status = "下载失败";
                taskInfo.ErrorMessage = ex.Message;
                Logger.Error($"[基岩版下载] 下载失败: {ex.Message}");
                DownloadFailed?.Invoke(version.Id, ex.Message);
            }
            finally
            {
                _currentCts?.Dispose();
                _currentCts = null;
            }

            return taskInfo;
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

                    if (totalBytes > 0 && progress != null)
                    {
                        double fileProgress = (double)downloadedBytes / totalBytes * 100;
                        progress.Report(new DownloadProgressInfo
                        {
                            Progress = fileProgress,
                            DownloadedBytes = downloadedBytes,
                            TotalBytes = totalBytes,
                            CurrentFile = Path.GetFileName(savePath)
                        });
                    }
                };

                await client.DownloadFileTaskAsync(new Uri(url), savePath);
            }
        }

        public void CancelDownload()
        {
            try
            {
                _currentCts?.Cancel();
                StatusChanged?.Invoke("正在取消下载...");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[基岩版下载] 取消下载时出错: {ex.Message}");
            }
        }

        public static string ComputeSha1(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static bool VerifySha1(string filePath, string expectedSha1)
        {
            if (string.IsNullOrEmpty(expectedSha1) || !File.Exists(filePath))
                return true;

            string actual = ComputeSha1(filePath);
            bool valid = string.Equals(expectedSha1, actual, StringComparison.OrdinalIgnoreCase);
            if (!valid)
                Logger.Warning($"[基岩版校验] SHA1校验不匹配 - 期望: {expectedSha1}, 实际: {actual}");
            return valid;
        }

        public async Task CheckRuntimeDependenciesAsync()
        {
            try
            {
                StatusChanged?.Invoke("正在检测运行环境...");

                bool needVcRuntime = NeedVcRuntime();
                bool needStoreFramework = NeedMicrosoftStoreFramework();

                if (needVcRuntime || needStoreFramework)
                {
                    var missing = new List<string>();
                    if (needVcRuntime) missing.Add("VC++ Redistributable");
                    if (needStoreFramework) missing.Add("Microsoft Store 应用框架");

                    Logger.Warning($"[基岩版环境] 缺少运行环境: {string.Join(", ", missing)}");
                    StatusChanged?.Invoke($"提示：建议安装 {string.Join(", ", missing)} 以确保基岩版正常运行");
                }
                else
                {
                    StatusChanged?.Invoke("运行环境检测通过");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[基岩版环境] 运行环境检测异常: {ex.Message}");
            }
        }

        private static bool NeedVcRuntime()
        {
            try
            {
                string systemDir = Environment.GetFolderPath(
                    Environment.Is64BitOperatingSystem
                        ? Environment.SpecialFolder.SystemX86
                        : Environment.SpecialFolder.System);

                string[] vcDlls = { "vcruntime140.dll", "msvcp140.dll", "concrt140.dll" };
                foreach (string dll in vcDlls)
                {
                    if (!File.Exists(Path.Combine(systemDir, dll)))
                        return true;
                }
                return false;
            }
            catch { return true; }
        }

        private static bool NeedMicrosoftStoreFramework()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Get-AppxPackage -Name Microsoft.WindowsStore\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return string.IsNullOrWhiteSpace(output) || output.Contains("未找到");
            }
            catch { return true; }
        }

        public bool DeleteVersion(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(_minecraftPath, "bedrock", versionId);
                if (Directory.Exists(versionDir))
                {
                    Directory.Delete(versionDir, true);
                    Logger.Info($"[基岩版管理] 已删除版本 {versionId}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"[基岩版管理] 删除版本失败: {ex.Message}");
                return false;
            }
        }

        public long GetVersionSize(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(_minecraftPath, "bedrock", versionId);
                if (!Directory.Exists(versionDir)) return 0;
                return new DirectoryInfo(versionDir)
                    .GetFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch { return 0; }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _currentCts?.Cancel();
                _currentCts?.Dispose();
                _disposed = true;
            }
        }
    }
}