using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.ViewModels
{
    public class DownloadTaskViewModel : IDownloadTask
    {
        private string _versionId;
        private string _status;
        private double _progress;
        private long _downloadedBytes;
        private long _totalBytes;
        private string _currentFile;
        private string _downloadSpeed;
        private string _remainingTime;
        private bool _isCompleted;
        private bool _isFailed;
        private bool _isCancelled;
        private bool _isPaused;
        private string _errorMessage;

        public string VersionId
        {
            get => _versionId;
            set => SetProperty(ref _versionId, value);
        }

        public string Status
        {
            get => _status;
            set { SetProperty(ref _status, value, nameof(Status)); OnPropertyChanged(nameof(IsDownloading)); OnPropertyChanged(nameof(StatusIcon)); }
        }

        public double Progress
        {
            get => _progress;
            set { SetProperty(ref _progress, value, nameof(Progress)); OnPropertyChanged(nameof(ProgressText)); }
        }

        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set { SetProperty(ref _downloadedBytes, value, nameof(DownloadedBytes)); OnPropertyChanged(nameof(FileSize)); OnPropertyChanged(nameof(SizeText)); }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set { SetProperty(ref _totalBytes, value, nameof(TotalBytes)); OnPropertyChanged(nameof(FileSize)); OnPropertyChanged(nameof(SizeText)); }
        }

        public string CurrentFile
        {
            get => _currentFile;
            set { SetProperty(ref _currentFile, value, nameof(CurrentFile)); OnPropertyChanged(nameof(FileName)); }
        }

        public string DownloadSpeed
        {
            get => _downloadSpeed;
            set { SetProperty(ref _downloadSpeed, value, nameof(DownloadSpeed)); OnPropertyChanged(nameof(Speed)); }
        }

        public string RemainingTime
        {
            get => _remainingTime;
            set { SetProperty(ref _remainingTime, value, nameof(RemainingTime)); }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set { SetProperty(ref _isCompleted, value, nameof(IsCompleted)); OnPropertyChanged(nameof(IsDownloading)); }
        }

        public bool IsFailed
        {
            get => _isFailed;
            set { SetProperty(ref _isFailed, value, nameof(IsFailed)); OnPropertyChanged(nameof(IsDownloading)); }
        }

        public bool IsCancelled
        {
            get => _isCancelled;
            set { SetProperty(ref _isCancelled, value, nameof(IsCancelled)); OnPropertyChanged(nameof(IsDownloading)); }
        }

        public bool IsPaused
        {
            get => _isPaused;
            set { SetProperty(ref _isPaused, value, nameof(IsPaused)); OnPropertyChanged(nameof(IsDownloading)); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { SetProperty(ref _errorMessage, value, nameof(ErrorMessage)); }
        }

        public bool IsDownloading => !IsCompleted && !IsFailed && !IsCancelled && Status == "下载中";

        private readonly VersionDownloadService _downloadService;
        private readonly VersionInfo _versionInfo;
        private CancellationTokenSource _cts;
        private DateTime _startTime;
        private long _lastBytes;
        private DateTime _lastTime;
        private long _lastFileTotal;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler == null) return;

            // PropertyChanged 由 WPF 绑定目标监听，必须在 UI 线程触发
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
                return;
            }
            if (dispatcher.CheckAccess())
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                try
                {
                    dispatcher.BeginInvoke(DispatcherPriority.DataBind,
                        new Action(() => handler(this, new PropertyChangedEventArgs(propertyName))));
                }
                catch
                {
                    handler(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public DownloadTaskViewModel(VersionInfo versionInfo, string minecraftPath)
        {
            _versionInfo = versionInfo;
            _downloadService = new VersionDownloadService(minecraftPath);
            _versionId = versionInfo.Id;
            _status = "等待中";
            _progress = 0;
            _downloadedBytes = 0;
            _totalBytes = 0;
            _currentFile = string.Empty;
            _downloadSpeed = "--";
            _remainingTime = "--";
            _isPaused = false;
        }

        public async Task StartDownloadAsync()
        {
            if (IsDownloading || IsCompleted) return;

            _cts = new CancellationTokenSource();
            _startTime = DateTime.Now;
            _lastBytes = 0;
            _lastTime = DateTime.Now;
            _lastFileTotal = 0;

            Status = "下载中";
            IsCompleted = false;
            IsFailed = false;
            IsCancelled = false;
            IsPaused = false;

            try
            {
                _downloadService.StatusChanged += OnStatusChanged;
                
                var progress = new DownloadProgress();
                progress.ProgressChanged += OnProgressChanged;

                await _downloadService.StartDownloadAsync(_versionInfo, progress, _cts.Token);

                Status = "已完成";
                Progress = 100;
                IsCompleted = true;
                DownloadSpeed = "--";
                RemainingTime = "已完成";
            }
            catch (OperationCanceledException)
            {
                if (IsPaused)
                {
                    Status = "已暂停";
                    DownloadSpeed = "--";
                    RemainingTime = "--";
                }
                else
                {
                    Status = "已取消";
                    IsCancelled = true;
                    DownloadSpeed = "--";
                    RemainingTime = "--";
                }
            }
            catch (Exception ex)
            {
                Status = "下载失败";
                IsFailed = true;
                ErrorMessage = ex.Message;
                DownloadSpeed = "--";
                RemainingTime = "--";
            }
            finally
            {
                _downloadService.StatusChanged -= OnStatusChanged;
            }
        }

        private void OnStatusChanged(string status)
        {
            Status = status;
            OnPropertyChanged(nameof(IsDownloading));
        }

        private void OnProgressChanged(DownloadProgressInfo info)
        {
            // 当文件切换（TotalBytes 发生变化或文件变小）时重置速度基准。
            // 避免 "下载一个新文件时 bytesDelta 为负 -> 速度显示负数" 的问题。
            long total = info.TotalBytes;
            if (_lastFileTotal <= 0 || total != _lastFileTotal || info.DownloadedBytes < _lastBytes)
            {
                _lastFileTotal = total;
                _lastBytes = info.DownloadedBytes;
                _lastTime = DateTime.Now;
            }

            Progress = info.Progress;
            DownloadedBytes = info.DownloadedBytes;
            TotalBytes = total;
            CurrentFile = info.CurrentFile;

            CalculateSpeedAndTime();
        }

        private void CalculateSpeedAndTime()
        {
            DateTime now = DateTime.Now;
            TimeSpan elapsed = now - _lastTime;

            if (elapsed.TotalSeconds >= 1 && TotalBytes > 0)
            {
                long bytesDelta = DownloadedBytes - _lastBytes;
                if (bytesDelta < 0)
                {
                    // 文件切换导致 bytesDelta 异常，重置基准后下次再计算
                    _lastBytes = DownloadedBytes;
                    _lastTime = now;
                    return;
                }
                double bytesPerSecond = bytesDelta / elapsed.TotalSeconds;

                DownloadSpeed = FormatSpeed(bytesPerSecond);

                long remainingBytes = TotalBytes - DownloadedBytes;
                if (bytesPerSecond > 0)
                {
                    double remainingSeconds = remainingBytes / bytesPerSecond;
                    RemainingTime = FormatTime(remainingSeconds);
                }

                _lastBytes = DownloadedBytes;
                _lastTime = now;
            }
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 0) bytesPerSecond = 0;
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond:F1} B/s";
            if (bytesPerSecond < 1024 * 1024)
                return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }

        private string FormatTime(double seconds)
        {
            if (seconds < 60)
                return $"{(int)seconds}秒";
            if (seconds < 3600)
                return $"{(int)(seconds / 60)}分{(int)(seconds % 60)}秒";
            int hours = (int)(seconds / 3600);
            int minutes = (int)((seconds % 3600) / 60);
            int secs = (int)(seconds % 60);
            return $"{hours}时{minutes}分{secs}秒";
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }

        public void Pause()
        {
            if (!IsDownloading) return;

            IsPaused = true;
            _cts?.Cancel();
            Logger.Info($"[下载任务] 已暂停任务 {VersionId}");
        }

        public string ProgressText => $"{Progress:F1}%";

        public string FileName => string.IsNullOrEmpty(CurrentFile) ? VersionId : CurrentFile;

        public string FileSize => SizeText;

        public string Speed => DownloadSpeed;

        public string StatusIcon
        {
            get
            {
                switch (Status)
                {
                    case "下载中":
                        return "⬇️";
                    case "已完成":
                        return "✅";
                    case "下载失败":
                        return "❌";
                    case "已取消":
                        return "⏹️";
                    case "已暂停":
                        return "⏸️";
                    case "等待中":
                    default:
                        return "⏳";
                }
            }
        }

        public string SizeText
        {
            get
            {
                if (TotalBytes <= 0)
                    return "-- / --";
                string downloaded = FormatSize(DownloadedBytes);
                string total = FormatSize(TotalBytes);
                return $"{downloaded} / {total}";
            }
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024):F1} MB";
        }
    }
}
