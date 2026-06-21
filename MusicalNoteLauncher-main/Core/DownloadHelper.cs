using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 共享下载工具，提供带重试和进度反馈的下载能力
    /// </summary>
    public static class DownloadHelper
    {
        private const int BufferSize = 8192;
        private const int DefaultRetryCount = 3;
        private const int InitialRetryDelayMs = 2000;

        /// <summary>
        /// 下载文件（带进度回调），失败自动重试 3 次（指数退避 2s/4s/8s）
        /// </summary>
        /// <param name="httpClient">共享的 HttpClient 实例</param>
        /// <param name="url">下载地址</param>
        /// <param name="savePath">保存路径（会自动创建父目录）</param>
        /// <param name="progress">进度报告器（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async Task DownloadFileWithRetryAsync(
            HttpClient httpClient,
            string url,
            string savePath,
            DownloadProgress progress,
            CancellationToken cancellationToken = default)
        {
            int delayMs = InitialRetryDelayMs;
            for (int attempt = 1; attempt <= DefaultRetryCount; attempt++)
            {
                try
                {
                    await DownloadFileAsync(httpClient, url, savePath, progress, cancellationToken);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception) when (attempt < DefaultRetryCount)
                {
                    progress?.Report(new DownloadProgressInfo
                    {
                        Status = $"下载失败，正在重试 ({attempt}/{DefaultRetryCount})...",
                        CurrentFile = Path.GetFileName(savePath)
                    });
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2;
                }
            }
        }

        /// <summary>
        /// 流式下载文件并报告进度
        /// </summary>
        private static async Task DownloadFileAsync(
            HttpClient httpClient,
            string url,
            string savePath,
            DownloadProgress progress,
            CancellationToken cancellationToken)
        {
            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long downloadedBytes = 0;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write,
                FileShare.None, BufferSize, useAsync: true);

            var buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    double pct = (double)downloadedBytes / totalBytes * 100;
                    progress?.Report(new DownloadProgressInfo
                    {
                        Progress = Math.Min(100, pct),
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
