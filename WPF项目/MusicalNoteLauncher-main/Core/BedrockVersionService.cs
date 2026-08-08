using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class BedrockVersionInfo
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public string InstallerUrl { get; set; }
        public DateTime ReleaseTime { get; set; }
        public bool IsDownloaded { get; set; }

        /// <summary>下载来源：mcappx / mcapks</summary>
        public string Source { get; set; } = "mcappx";
        /// <summary>下载来源友好显示名称</summary>
        public string SourceDisplay => Source switch
        {
            "mcapks" => "苦力怕论坛",
            "mcappx" => "mcappx",
            _ => Source
        };

        /// <summary>按钮文字：已下载 / 一键下载</summary>
        public string StatusText => IsDownloaded ? "已下载" : "一键下载";
        /// <summary>是否可点击下载</summary>
        public bool CanDownload => !IsDownloaded && !string.IsNullOrEmpty(InstallerUrl);
        /// <summary>版本类型显示友好名称</summary>
        public string TypeDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Type)) return "未知";
                return Type.ToLowerInvariant() switch
                {
                    "release" => "正式版",
                    "preview" => "预览版",
                    "beta" => "测试版",
                    _ => Type
                };
            }
        }
        /// <summary>分组名（正式版 / 预览版 / 测试版 / 其他）</summary>
        public string GroupType
        {
            get
            {
                if (string.IsNullOrEmpty(Type)) return "其他";
                return Type.ToLowerInvariant() switch
                {
                    "release" => "正式版",
                    "preview" => "预览版",
                    "beta" => "测试版",
                    _ => "其他"
                };
            }
        }
        /// <summary>版本描述（版本号 + 日期）</summary>
        public string Description => ReleaseTime != default
            ? $"版本 {Version} · {ReleaseTime:yyyy-MM-dd}"
            : $"版本 {Version}";
    }

    public class BedrockDownloadService
    {
        private const string BEDROCK_MANIFEST_URL = "https://data.mcappx.com/v2/bedrock.json";
        private const string MCAPKS_LIST_URL = "https://mcapks.com/api/get-vslist.php";
        private const string MCAPKS_DOWNLOAD_URL = "https://mcapks.com/api/get-download.php";
        private readonly string _minecraftPath;
        private readonly HttpClient _httpClient;

        public event Action<string> StatusChanged;
        public event Action<string, string> DownloadFailed;
        public event Action<string> DownloadCompleted;

        public BedrockDownloadService(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<List<BedrockVersionInfo>> GetRemoteVersionsAsync(CancellationToken cancellationToken = default)
        {
            var allVersions = new Dictionary<string, BedrockVersionInfo>(StringComparer.OrdinalIgnoreCase);

            // 源1: mcappx.com（Windows）
            var mcappxVersions = await FetchMcappxVersionsAsync(cancellationToken);
            foreach (var v in mcappxVersions)
            {
                var key = v.Version ?? v.Id;
                if (!allVersions.ContainsKey(key))
                    allVersions[key] = v;
            }

            // 源2: mcapks.com（苦力怕论坛 / Android APK）
            var mcapksVersions = await FetchMcapksVersionsAsync(cancellationToken);
            foreach (var v in mcapksVersions)
            {
                var key = v.Version ?? v.Id;
                if (!allVersions.ContainsKey(key))
                    allVersions[key] = v;
            }

            return allVersions.Values.OrderByDescending(v => v.ReleaseTime).ToList();
        }

        /// <summary>从 mcappx.com 获取版本（Windows）</summary>
        private async Task<List<BedrockVersionInfo>> FetchMcappxVersionsAsync(CancellationToken cancellationToken)
        {
            var versions = new List<BedrockVersionInfo>();
            try
            {
                StatusChanged?.Invoke("正在获取基岩版版本列表 (mcappx)...");
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(15000);
                    string json = await _httpClient.GetStringAsync(BEDROCK_MANIFEST_URL);

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("From_mcappx.com", out JsonElement versionsDict))
                        {
                            foreach (var kv in versionsDict.EnumerateObject())
                            {
                                try
                                {
                                    string versionKey = kv.Name;
                                    var item = kv.Value;

                                    string type = "release";
                                    if (item.TryGetProperty("Type", out var t))
                                        type = (t.GetString() ?? "").ToLowerInvariant();

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

                                    var info = new BedrockVersionInfo
                                    {
                                        Id = id,
                                        Version = versionKey,
                                        Type = type,
                                        InstallerUrl = installerUrl,
                                        Source = "mcappx"
                                    };

                                    if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var rt))
                                        info.ReleaseTime = rt;

                                    if (!string.IsNullOrEmpty(info.Id))
                                    {
                                        info.IsDownloaded = IsVersionDownloaded(info.Id);
                                        versions.Add(info);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[基岩版 mcappx] 获取失败: {ex.Message}");
            }
            return versions;
        }

        /// <summary>从 mcapks.com（苦力怕论坛）获取版本</summary>
        private async Task<List<BedrockVersionInfo>> FetchMcapksVersionsAsync(CancellationToken cancellationToken)
        {
            var versions = new List<BedrockVersionInfo>();
            try
            {
                StatusChanged?.Invoke("正在获取基岩版版本列表 (苦力怕论坛)...");
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(15000);
                    string json = await _httpClient.GetStringAsync(MCAPKS_LIST_URL);

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                        {
                            if (root.TryGetProperty("data", out var data))
                            {
                                if (data.TryGetProperty("versions", out var vsList))
                                {
                                    foreach (var item in vsList.EnumerateArray())
                                    {
                                        try
                                        {
                                            string versionStr = item.TryGetProperty("version", out var v) ? v.GetString() : "";
                                            if (string.IsNullOrEmpty(versionStr)) continue;

                                            bool beta = item.TryGetProperty("beta", out var b) && b.GetBoolean();
                                            string date = item.TryGetProperty("date", out var d) ? d.GetString() : "";

                                            var info = new BedrockVersionInfo
                                            {
                                                Id = versionStr,
                                                Version = versionStr,
                                                Type = beta ? "preview" : "release",
                                                InstallerUrl = versionStr, // 暂存版本号，下载时再查真实地址
                                                Source = "mcapks"
                                            };

                                            if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var rt))
                                                info.ReleaseTime = rt;

                                            info.IsDownloaded = IsVersionDownloaded(info.Id);
                                            versions.Add(info);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[基岩版 苦力怕论坛] 获取失败: {ex.Message}");
            }
            return versions;
        }

        /// <summary>解析 mcapks 版本的真实下载地址</summary>
        private async Task<string> ResolveMcapksDownloadUrlAsync(string versionId, CancellationToken cancellationToken)
        {
            try
            {
                string url = $"{MCAPKS_DOWNLOAD_URL}?version={Uri.EscapeDataString(versionId)}";
                string json = await _httpClient.GetStringAsync(url);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                    {
                        if (root.TryGetProperty("data", out var data))
                        {
                            if (data.TryGetProperty("downloads", out var downloads) && downloads.GetArrayLength() > 0)
                            {
                                foreach (var d in downloads.EnumerateArray())
                                {
                                    string dlUrl = d.TryGetProperty("url", out var u) ? u.GetString() : "";
                                    if (!string.IsNullOrEmpty(dlUrl) && dlUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                        return dlUrl;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[基岩版 苦力怕论坛] 解析下载地址失败 ({versionId}): {ex.Message}");
            }
            return null;
        }

        private bool IsVersionDownloaded(string versionId)
        {
            try
            {
                string bedrockDir = Path.Combine(_minecraftPath, "bedrock", versionId);
                string exePath = Path.Combine(bedrockDir, "Minecraft.Windows.exe");
                return File.Exists(exePath);
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<BedrockVersionInfo>> GetInstalledVersionsAsync()
        {
            List<BedrockVersionInfo> versions = new List<BedrockVersionInfo>();

            try
            {
                string bedrockDir = Path.Combine(_minecraftPath, "bedrock");
                if (Directory.Exists(bedrockDir))
                {
                    foreach (string dir in Directory.GetDirectories(bedrockDir))
                    {
                        string versionId = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(versionId))
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
            }
            catch (Exception ex)
            {
                Logger.Error($"获取已安装基岩版失败: {ex.Message}");
            }

            return versions;
        }

        public async Task<DownloadTaskInfo> StartDownloadAsync(BedrockVersionInfo version, DownloadProgress progress, CancellationToken cancellationToken = default)
        {
            var taskInfo = new DownloadTaskInfo
            {
                VersionId = version.Id,
                Status = "等待中"
            };

            try
            {
                string bedrockDir = Path.Combine(_minecraftPath, "bedrock", version.Id);
                Directory.CreateDirectory(bedrockDir);

                // 解析真实下载地址
                string downloadUrl = version.InstallerUrl;
                string installerExt = ".msixbundle";

                if (version.Source == "mcapks")
                {
                    taskInfo.Status = "正在解析下载地址...";
                    StatusChanged?.Invoke($"正在获取基岩版 {version.Id} 下载地址 (苦力怕论坛)...");
                    downloadUrl = await ResolveMcapksDownloadUrlAsync(version.InstallerUrl, cancellationToken);
                    if (string.IsNullOrEmpty(downloadUrl))
                        throw new Exception("无法获取下载地址");
                    installerExt = ".xapk";
                }

                string installerPath = Path.Combine(bedrockDir, $"installer{installerExt}");

                taskInfo.Status = "正在下载安装包";
                taskInfo.CurrentFile = Path.GetFileName(installerPath);
                StatusChanged?.Invoke($"正在下载基岩版 {version.Id} ({version.SourceDisplay})...");

                await DownloadFileWithProgressAsync(downloadUrl, installerPath, progress, cancellationToken);

                taskInfo.Status = "已完成";
                taskInfo.IsCompleted = true;
                taskInfo.Progress = 100;

                StatusChanged?.Invoke($"基岩版 {version.Id} 下载完成");
                DownloadCompleted?.Invoke(version.Id);
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
                DownloadFailed?.Invoke(version.Id, ex.Message);
            }

            return taskInfo;
        }

        private async Task DownloadFileWithProgressAsync(string url, string savePath, DownloadProgress progress, CancellationToken cancellationToken)
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
                            progress?.Report(new DownloadProgressInfo
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
        }

        public bool DeleteVersion(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(_minecraftPath, "bedrock", versionId);
                if (Directory.Exists(versionDir))
                {
                    Directory.Delete(versionDir, true);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
