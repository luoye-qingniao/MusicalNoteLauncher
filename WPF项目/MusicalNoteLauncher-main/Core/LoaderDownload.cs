using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class LoaderDownload : LoaderBase
    {
        private readonly string _url;
        private readonly string _savePath;
        private readonly HttpClient _httpClient;

        public string Url => _url;
        public string SavePath => _savePath;
        public long TotalBytes { get; private set; }
        public long DownloadedBytes { get; private set; }

        public LoaderDownload(string name, string url, string savePath, HttpClient httpClient = null)
        {
            Name = name;
            _url = url;
            _savePath = savePath;
            _httpClient = httpClient ?? CreateDefaultHttpClient();
        }

        private HttpClient CreateDefaultHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            return client;
        }

        public override async Task RunAsync(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            OnStateChanged(LoaderState.Running);

            try
            {
                string directory = Path.GetDirectoryName(_savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var response = await _httpClient.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    TotalBytes = response.Content.Headers.ContentLength ?? 0;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(_savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        int bytesRead;
                        DownloadedBytes = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                OnStateChanged(LoaderState.Cancelled);
                                return;
                            }

                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            DownloadedBytes += bytesRead;

                            if (TotalBytes > 0)
                            {
                                double progress = (double)DownloadedBytes / TotalBytes * 100;
                                OnProgressChanged(progress);
                                ProgressText = $"{DownloadedBytes / 1024.0 / 1024.0:F2} MB / {TotalBytes / 1024.0 / 1024.0:F2} MB";
                            }
                        }
                    }
                }

                OnCompleted();
            }
            catch (OperationCanceledException)
            {
                OnStateChanged(LoaderState.Cancelled);
                if (File.Exists(_savePath))
                {
                    try { File.Delete(_savePath); } catch { }
                }
            }
            catch (Exception ex)
            {
                OnFailed(ex);
                if (File.Exists(_savePath))
                {
                    try { File.Delete(_savePath); } catch { }
                }
            }
        }
    }
}
