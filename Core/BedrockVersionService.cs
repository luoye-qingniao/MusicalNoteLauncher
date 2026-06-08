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
    public class BedrockVersionInfo
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public string InstallerUrl { get; set; }
        public DateTime ReleaseTime { get; set; }
        public bool IsDownloaded { get; set; }
    }

    public class BedrockDownloadService
    {
        private const string BEDROCK_MANIFEST_URL = "https://bmclapi2.bangbang93.com/bedrock/version_manifest_v2.json";
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
            List<BedrockVersionInfo> versions = new List<BedrockVersionInfo>();

            try
            {
                StatusChanged?.Invoke("正在获取基岩版版本列表...");
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(30000);
                    string json = await _httpClient.GetStringAsync(BEDROCK_MANIFEST_URL);

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("versions", out JsonElement versionsElement))
                    {
                        foreach (JsonElement item in versionsElement.EnumerateArray())
                        {
                            try
                            {
                                BedrockVersionInfo info = new BedrockVersionInfo
                                {
                                    Id = item.GetProperty("id").GetString(),
                                    Version = item.GetProperty("version").GetString(),
                                    Type = item.GetProperty("type").GetString(),
                                    Url = item.TryGetProperty("url", out var url) ? url.GetString() : null,
                                    InstallerUrl = item.TryGetProperty("installerUrl", out var installerUrl) ? installerUrl.GetString() : null
                                };

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
                Logger.Error($"获取基岩版版本列表失败: {ex.Message}");
            }

            return versions;
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

                string installerPath = Path.Combine(bedrockDir, "installer.msixbundle");

                taskInfo.Status = "正在下载安装包";
                taskInfo.CurrentFile = "installer.msixbundle";
                StatusChanged?.Invoke($"正在下载基岩版 {version.Id}...");

                await DownloadFileWithProgressAsync(version.InstallerUrl, installerPath, progress, cancellationToken);

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
