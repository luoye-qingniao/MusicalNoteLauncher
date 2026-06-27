using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Windows
{
    public partial class GenericDownloader : Window
    {
        private readonly string _downloadUrl;
        private readonly string _savePath;
        private readonly string _fileName;
        private CancellationTokenSource _cts;
        private long _downloadedBytes;
        private long _totalBytes;
        private DateTime _lastUpdateTime;
        private long _lastDownloadedBytes;

        public GenericDownloader(string downloadUrl, string savePath, string fileName)
        {
            InitializeComponent();
            _downloadUrl = downloadUrl;
            _savePath = savePath;
            _fileName = fileName;
            txtFileName.Text = $"正在下载: {fileName}";
            Loaded += GenericDownloader_Loaded;
        }

        private async void GenericDownloader_Loaded(object sender, RoutedEventArgs e)
        {
            await StartDownloadAsync();
        }

        private async Task StartDownloadAsync()
        {
            btnPause.IsEnabled = true;
            btnCancel.IsEnabled = true;

            _cts = new CancellationTokenSource();
            _downloadedBytes = 0;
            _totalBytes = 0;
            _lastUpdateTime = DateTime.Now;
            _lastDownloadedBytes = 0;

            try
            {
                using (var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
                {
                    using (var response = await httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead, _cts.Token))
                    {
                        response.EnsureSuccessStatusCode();
                        _totalBytes = response.Content.Headers.ContentLength ?? -1;

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(_savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[8192];
                            int bytesRead;

                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                _downloadedBytes += bytesRead;

                                UpdateProgress();
                            }
                        }
                    }
                }

                ModernMessageBox.ShowInfo($"下载完成！\n文件已保存到: {_savePath}", "成功");
                Close();
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(_savePath))
                {
                    File.Delete(_savePath);
                }
                ModernMessageBox.ShowInfo("下载已取消", "提示");
                Close();
            }
            catch (Exception ex)
            {
                if (File.Exists(_savePath))
                {
                    File.Delete(_savePath);
                }
                ModernMessageBox.ShowError($"下载失败:\n{ex.Message}", "错误");
                Close();
            }
            finally
            {
                btnPause.IsEnabled = false;
                btnCancel.IsEnabled = false;
            }
        }

        private void UpdateProgress()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateProgress);
                return;
            }

            double progress = _totalBytes > 0 ? (_downloadedBytes * 100.0 / _totalBytes) : 0;
            progressBar.Value = progress;

            string downloadedMB = (_downloadedBytes / 1024.0 / 1024.0).ToString("0.00");
            string totalMB = _totalBytes > 0 ? (_totalBytes / 1024.0 / 1024.0).ToString("0.00") : "未知";
            txtProgress.Text = $"{progress:F1}% ({downloadedMB} MB / {totalMB} MB)";

            // 计算下载速度
            var now = DateTime.Now;
            var elapsed = (now - _lastUpdateTime).TotalSeconds;
            if (elapsed >= 1)
            {
                var bytesPerSecond = _downloadedBytes - _lastDownloadedBytes;
                var speedKBps = bytesPerSecond / 1024.0;
                var speedMBps = speedKBps / 1024.0;

                string speedText;
                if (speedMBps >= 1)
                    speedText = $"{speedMBps:F2} MB/s";
                else
                    speedText = $"{speedKBps:F2} KB/s";

                txtSpeed.Text = $"速度: {speedText}";

                _lastUpdateTime = now;
                _lastDownloadedBytes = _downloadedBytes;
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _cts?.Cancel();
            base.OnClosing(e);
        }
    }
}