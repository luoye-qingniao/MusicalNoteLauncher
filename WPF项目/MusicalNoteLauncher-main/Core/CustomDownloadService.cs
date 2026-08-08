using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 自定义下载服务 — 基于 HttpClient 的多段并行下载引擎。
    /// 参照 GitHub bezzad/Downloader 设计：分段 Range 请求 + 并行写入 + 实时进度。
    /// </summary>
    public class CustomDownloadService : IDisposable
    {
        private static CustomDownloadService _instance;
        public static CustomDownloadService Instance => _instance ??= new CustomDownloadService();

        private readonly HttpClient _http;
        private CancellationTokenSource _cts;
        private readonly object _lock = new();

        /// <summary>当前下载状态</summary>
        public string Status { get; private set; } = "idle";

        /// <summary>文件名（从 URL 或 Content-Disposition 提取）</summary>
        public string FileName { get; private set; } = "";

        /// <summary>保存路径</summary>
        public string SavePath { get; private set; } = "";

        /// <summary>下载 URL</summary>
        public string DownloadUrl { get; private set; } = "";

        /// <summary>已下载字节数</summary>
        public long DownloadedBytes { get; private set; }

        /// <summary>文件总大小（-1 = 未知）</summary>
        public long TotalBytes { get; private set; } = -1;

        /// <summary>下载速度（字节/秒）</summary>
        public double SpeedBytesPerSec { get; private set; }

        /// <summary>错误信息</summary>
        public string ErrorMessage { get; private set; } = "";

        /// <summary>下载开始时间（用于计算速度）</summary>
        private DateTime _startTime;
        private long _lastBytes;
        private DateTime _lastTime;

        private const int DefaultSegments = 8; // 默认 8 段并行

        private CustomDownloadService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.Add("User-Agent", "MNL/1.2.0");
        }

        /// <summary>获取当前进度快照（供前端轮询）</summary>
        public CustomDownloadProgress GetProgress()
        {
            lock (_lock)
            {
                return new CustomDownloadProgress
                {
                    Status = Status,
                    FileName = FileName,
                    SavePath = SavePath,
                    DownloadedBytes = DownloadedBytes,
                    TotalBytes = TotalBytes,
                    SpeedBytesPerSec = SpeedBytesPerSec,
                    ErrorMessage = ErrorMessage
                };
            }
        }

        /// <summary>开始多段并行下载</summary>
        public async Task<(bool success, string message)> StartDownloadAsync(string url, string saveDir, string fileName = "", int segments = DefaultSegments)
        {
            lock (_lock)
            {
                if (Status == "downloading")
                    return (false, "已有下载任务进行中");
            }

            try
            {
                // 重置状态
                lock (_lock)
                {
                    Status = "connecting";
                    DownloadedBytes = 0;
                    TotalBytes = -1;
                    SpeedBytesPerSec = 0;
                    ErrorMessage = "";
                    DownloadUrl = url;
                    _startTime = DateTime.Now;
                    _lastBytes = 0;
                    _lastTime = _startTime;
                }

                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;

                // 1) HEAD 请求获取文件大小和文件名
                long? contentLength;
                string resolvedFileName;

                try
                {
                    var headReq = new HttpRequestMessage(HttpMethod.Head, url);
                    var headResp = await _http.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, ct);
                    headResp.EnsureSuccessStatusCode();

                    contentLength = headResp.Content.Headers.ContentLength;
                    resolvedFileName = ExtractFileName(headResp, url, fileName);
                }
                catch (HttpRequestException)
                {
                    // HEAD 失败，尝试 GET 不吃 body
                    var getReq = new HttpRequestMessage(HttpMethod.Get, url);
                    getReq.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                    var getResp = await _http.SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead, ct);
                    getResp.EnsureSuccessStatusCode();

                    contentLength = getResp.Content.Headers.ContentRange?.Length;
                    resolvedFileName = ExtractFileName(getResp, url, fileName);
                }

                // 确定文件大小
                long totalSize = contentLength ?? -1;
                if (totalSize <= 0)
                {
                    // 服务器不提供 Content-Length，降级为单段下载
                    segments = 1;
                    totalSize = -1;
                }

                // 确保保存目录存在
                if (!Directory.Exists(saveDir))
                    Directory.CreateDirectory(saveDir);

                var finalPath = Path.Combine(saveDir, resolvedFileName);
                // 处理重名
                if (File.Exists(finalPath))
                {
                    var nameNoExt = Path.GetFileNameWithoutExtension(resolvedFileName);
                    var ext = Path.GetExtension(resolvedFileName);
                    var counter = 1;
                    while (File.Exists(finalPath))
                        finalPath = Path.Combine(saveDir, $"{nameNoExt} ({counter++}){ext}");
                }

                lock (_lock)
                {
                    FileName = resolvedFileName;
                    SavePath = finalPath;
                    TotalBytes = totalSize;
                    Status = "downloading";
                }

                ct.ThrowIfCancellationRequested();

                if (totalSize > 0 && segments > 1)
                {
                    // 多段并行下载
                    await DownloadMultiSegmentAsync(url, finalPath, totalSize, segments, ct);
                }
                else
                {
                    // 单段流式下载（服务器不提供大小）
                    await DownloadSingleSegmentAsync(url, finalPath, ct);
                }

                ct.ThrowIfCancellationRequested();

                lock (_lock)
                {
                    Status = "completed";
                    DownloadedBytes = TotalBytes > 0 ? TotalBytes : DownloadedBytes;
                    SpeedBytesPerSec = 0;
                }

                return (true, "下载完成");
            }
            catch (OperationCanceledException)
            {
                lock (_lock)
                {
                    Status = "cancelled";
                    ErrorMessage = "下载已取消";
                }
                return (false, "下载已取消");
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    Status = "error";
                    ErrorMessage = ex.Message;
                }
                return (false, ex.Message);
            }
        }

        /// <summary>取消当前下载</summary>
        public void Cancel()
        {
            _cts?.Cancel();
        }

        /// <summary>多段并行下载</summary>
        private async Task DownloadMultiSegmentAsync(string url, string filePath, long totalSize, int segments, CancellationToken ct)
        {
            segments = Math.Min(segments, 16); // 最多 16 段
            var segmentSize = totalSize / segments;

            // 预分配文件
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Write, 4096, FileOptions.Asynchronous))
            {
                fs.SetLength(totalSize);
            }

            // 并行下载各段
            var tasks = new Task[segments];
            for (int i = 0; i < segments; i++)
            {
                var segIdx = i;
                var start = segIdx * segmentSize;
                var end = (segIdx == segments - 1) ? totalSize - 1 : (segIdx + 1) * segmentSize - 1;
                tasks[i] = DownloadSegmentAsync(url, filePath, start, end, segIdx, ct);
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>下载单个分段</summary>
        private async Task DownloadSegmentAsync(string url, string filePath, long start, long end, int segIdx, CancellationToken ct)
        {
            const int bufferSize = 8192;

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

                using (var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();

                    using (var stream = await resp.Content.ReadAsStreamAsync())
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.Write, bufferSize, FileOptions.Asynchronous))
                    {
                        fs.Seek(start, SeekOrigin.Begin);

                        var buffer = new byte[bufferSize];
                        long written = 0;

                        while (written < (end - start + 1))
                        {
                            ct.ThrowIfCancellationRequested();

                            var toRead = (int)Math.Min(bufferSize, (end - start + 1) - written);
                            var read = await stream.ReadAsync(buffer, 0, toRead, ct);
                            if (read == 0) break;

                            await fs.WriteAsync(buffer, 0, read, ct);
                            written += read;

                            lock (_lock)
                            {
                                DownloadedBytes += read;
                                UpdateSpeed();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>单段流式下载（未知大小场景）</summary>
        private async Task DownloadSingleSegmentAsync(string url, string filePath, CancellationToken ct)
        {
            const int bufferSize = 8192;

            using (var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();

                // 尝试从响应中获取大小
                var cl = resp.Content.Headers.ContentLength;
                lock (_lock) { if (cl.HasValue) TotalBytes = cl.Value; }

                using (var stream = await resp.Content.ReadAsStreamAsync())
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous))
                {
                    var buffer = new byte[bufferSize];
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, bufferSize, ct)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        await fs.WriteAsync(buffer, 0, read, ct);

                        lock (_lock)
                        {
                            DownloadedBytes += read;
                            UpdateSpeed();
                        }
                    }
                }
            }
        }

        /// <summary>更新下载速度</summary>
        private void UpdateSpeed()
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastTime).TotalSeconds;
            if (elapsed >= 0.5) // 每 0.5 秒更新一次速度
            {
                SpeedBytesPerSec = (DownloadedBytes - _lastBytes) / elapsed;
                _lastBytes = DownloadedBytes;
                _lastTime = now;
            }
        }

        /// <summary>从 HTTP 响应提取文件名</summary>
        private static string ExtractFileName(HttpResponseMessage resp, string url, string preferredName)
        {
            // 优先从 Content-Disposition 提取
            var cd = resp.Content.Headers.ContentDisposition;
            if (cd != null && !string.IsNullOrWhiteSpace(cd.FileNameStar))
                return Uri.UnescapeDataString(cd.FileNameStar);
            if (cd != null && !string.IsNullOrWhiteSpace(cd.FileName))
                return Uri.UnescapeDataString(cd.FileName);

            // 其次使用指定文件名
            if (!string.IsNullOrWhiteSpace(preferredName))
                return preferredName;

            // 最后从 URL 提取
            try
            {
                var uri = new Uri(url);
                var name = Path.GetFileName(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(name))
                    return Uri.UnescapeDataString(name);
            }
            catch { }

            return "download_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _http?.Dispose();
        }
    }

    /// <summary>下载进度快照（JSON 序列化给前端）</summary>
    public class CustomDownloadProgress
    {
        public string Status { get; set; } = "idle";
        public string FileName { get; set; } = "";
        public string SavePath { get; set; } = "";
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedBytesPerSec { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
}
