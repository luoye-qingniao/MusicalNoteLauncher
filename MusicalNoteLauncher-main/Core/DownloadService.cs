using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class DownloadOptions
    {
        public int MaxRetries { get; set; } = 3;
        public int TimeoutMs { get; set; } = 30000;
        public bool VerifySha1 { get; set; } = true;
        public bool EnableMirrorFallback { get; set; } = true;
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    }

    public class DownloadResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
    }

    public class MirrorDownloadProgress
    {
        public double Progress { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public string CurrentFile { get; set; }
        public string Status { get; set; }
    }

    public class DownloadService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly DownloadOptions _options;
        private bool _disposed;

        private static readonly Dictionary<string, string[]> MirrorMappings = new Dictionary<string, string[]>
        {
            { "dl.dropboxusercontent.com", new[] { "bmclapi2.bangbang93.com/maven" } },
            { "libraries.minecraft.net", new[] { "bmclapi2.bangbang93.com/libraries" } },
            { "maven.minecraftforge.net", new[] { "bmclapi2.bangbang93.com/maven" } },
            { "maven.fabricmc.net", new[] { "bmclapi2.bangbang93.com/maven/fabric" } },
            { "repo.maven.apache.org/maven2", new[] { "bmclapi2.bangbang93.com/maven" } },
            { "launchermeta.mojang.com", new[] { "bmclapi2.bangbang93.com" } },
            { "piston-data.mojang.com", new[] { "bmclapi2.bangbang93.com" } }
        };

        public DownloadService() : this(new DownloadOptions()) { }

        public DownloadService(DownloadOptions options)
        {
            _options = options ?? new DownloadOptions();

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs)
            };

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            ServicePointManager.Expect100Continue = false;

            ConfigureHeaders(_httpClient);
        }

        private void ConfigureHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
            client.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");
        }

        public async Task<bool> DownloadFileAsync(
            string url,
            string savePath,
            string expectedSha1 = null,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await DownloadWithRetryAsync(url, savePath, expectedSha1, progress, cancellationToken);
        }

        public async Task<bool> DownloadFileAsync(
            string url,
            string savePath,
            string expectedSha1,
            Action<MirrorDownloadProgress> progressCallback,
            CancellationToken cancellationToken = default)
        {
            return await DownloadWithRetryAsync(url, savePath, expectedSha1, progressCallback, cancellationToken);
        }

        private async Task<bool> DownloadWithRetryAsync(
            string url,
            string savePath,
            string expectedSha1,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            return await DownloadWithRetryAsync(url, savePath, expectedSha1,
                p => progress?.Report(p.Progress), cancellationToken);
        }

        private async Task<bool> DownloadWithRetryAsync(
            string url,
            string savePath,
            string expectedSha1,
            Action<MirrorDownloadProgress> progressCallback,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Logger.Warning("下载拦截：URL为空，跳过下载");
                return false;
            }

            var mirrors = _options.EnableMirrorFallback ? GenerateMirrors(url) : new[] { url };
            Exception lastException = null;

            for (int attempt = 0; attempt < _options.MaxRetries; attempt++)
            {
                foreach (string mirrorUrl in mirrors)
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return false;

                        Logger.Info($"尝试下载: {mirrorUrl}");

                        bool success = await DownloadSingleFileAsync(
                            mirrorUrl, savePath, expectedSha1, progressCallback, cancellationToken);

                        if (success)
                        {
                            if (_options.VerifySha1 && !string.IsNullOrEmpty(expectedSha1))
                            {
                                if (!VerifySha1(savePath, expectedSha1))
                                {
                                    Logger.Warning("SHA1校验失败，删除文件重试");
                                    File.Delete(savePath);
                                    continue;
                                }
                            }
                            Logger.Info("下载完成: " + savePath);
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        Logger.Warning($"下载失败: {ex.Message}");
                        Logger.Info("自动切换备用镜像源...");
                    }
                }

                if (attempt < _options.MaxRetries - 1)
                {
                    int delay = 1000 * (attempt + 1);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            Logger.Error($"所有镜像下载失败: {url}");
            return false;
        }

        private async Task<bool> DownloadSingleFileAsync(
            string url,
            string savePath,
            string expectedSha1,
            Action<MirrorDownloadProgress> progressCallback,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            HttpResponseMessage response;

            if (url.Contains("oracle.com"))
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Cookie", "oraclelicense=accept-securebackup-cookie");
                req.Headers.Referrer = new Uri("https://www.oracle.com");
                response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            else
            {
                response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }

            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? -1;
            long downloaded = 0;
            var buffer = new byte[8192];

            using var content = await response.Content.ReadAsStreamAsync();
            using var file = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            int read;
            while ((read = await content.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await file.WriteAsync(buffer, 0, read, cancellationToken);
                downloaded += read;

                if (total > 0 && progressCallback != null)
                {
                    progressCallback(new MirrorDownloadProgress
                    {
                        Progress = (double)downloaded / total * 100,
                        DownloadedBytes = downloaded,
                        TotalBytes = total,
                        CurrentFile = Path.GetFileName(savePath),
                        Status = "下载中"
                    });
                }
            }

            return downloaded > 0;
        }

        private string[] GenerateMirrors(string url)
        {
            var mirrors = new List<string> { url };
            foreach (var map in MirrorMappings)
            {
                if (url.Contains(map.Key))
                {
                    foreach (var rep in map.Value)
                    {
                        string newUrl = url.Replace(map.Key, rep);
                        if (!mirrors.Contains(newUrl)) mirrors.Add(newUrl);
                    }
                }
            }
            return mirrors.ToArray();
        }

        public static string ComputeSha1(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(sha1.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        public static bool VerifySha1(string filePath, string expectedSha1)
        {
            if (string.IsNullOrEmpty(expectedSha1)) return true;
            if (!File.Exists(filePath)) return false;

            string actual = ComputeSha1(filePath);
            bool valid = string.Equals(expectedSha1, actual, StringComparison.OrdinalIgnoreCase);
            if (!valid) Logger.Warning($"SHA1不匹配: {filePath}");
            return valid;
        }

        public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return Array.Empty<byte>();
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(_options.TimeoutMs);
                    var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead, cts.Token);
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<byte>();
            }
        }

        public async Task<string> DownloadStringAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(_options.TimeoutMs);
                    var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead, cts.Token);
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (OperationCanceledException)
            {
                return "";
            }
        }

        public void Dispose()
        {
            if (!_disposed) _httpClient?.Dispose();
            _disposed = true;
        }
    }
}