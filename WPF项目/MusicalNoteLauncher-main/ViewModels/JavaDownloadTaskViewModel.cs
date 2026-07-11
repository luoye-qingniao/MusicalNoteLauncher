using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.ViewModels
{
    public class JavaDownloadTaskViewModel : INotifyPropertyChanged
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
        private bool _isDownloading;

        public string VersionId
        {
            get => _versionId;
            set => SetProperty(ref _versionId, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set => SetProperty(ref _downloadedBytes, value);
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set => SetProperty(ref _totalBytes, value);
        }

        public string CurrentFile
        {
            get => _currentFile;
            set => SetProperty(ref _currentFile, value);
        }

        public string DownloadSpeed
        {
            get => _downloadSpeed;
            set => SetProperty(ref _downloadSpeed, value);
        }

        public string RemainingTime
        {
            get => _remainingTime;
            set => SetProperty(ref _remainingTime, value);
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        public bool IsFailed
        {
            get => _isFailed;
            set => SetProperty(ref _isFailed, value);
        }

        public bool IsCancelled
        {
            get => _isCancelled;
            set => SetProperty(ref _isCancelled, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string ProgressText => $"{Progress:F1}%";

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

        public string SpeedText => $"速度: {DownloadSpeed}";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private readonly JavaDownloadService _javaDownloadService;
        private readonly int _javaVersion;
        private CancellationTokenSource _cts;
        private DateTime _startTime;
        private long _lastBytes;
        private DateTime _lastTime;
        private bool _pauseRequested = false;

        public event Action<string> DownloadCompleted;
        public event Action<string, string> DownloadFailed;
        public event Action TaskDeleted;

        public JavaDownloadTaskViewModel(int javaVersion, string minecraftPath)
        {
            _javaVersion = javaVersion;
            _javaDownloadService = new JavaDownloadService(minecraftPath);
            _versionId = $"Java {javaVersion}";
            _status = "等待中";
            _progress = 0;
            _downloadedBytes = 0;
            _totalBytes = 0;
            _currentFile = string.Empty;
            _downloadSpeed = "--";
            _remainingTime = "--";
            _isCompleted = false;
            _isFailed = false;
            _isCancelled = false;
            _isPaused = false;
            _isDownloading = false;
            _errorMessage = string.Empty;
        }

        public async Task StartDownloadAsync()
        {
            if (IsDownloading || IsCompleted) return;

            _cts = new CancellationTokenSource();
            _startTime = DateTime.Now;
            _lastBytes = 0;
            _lastTime = DateTime.Now;
            _pauseRequested = false;

            Status = "下载中";
            IsDownloading = true;
            IsCompleted = false;
            IsFailed = false;
            IsCancelled = false;
            IsPaused = false;
            Progress = 0;

            try
            {
                var progress = new DownloadProgress();
                progress.ProgressChanged += OnProgressChanged;

                _javaDownloadService.StatusChanged += OnStatusChanged;
                _javaDownloadService.LogReceived += OnLogReceived;

                string javaExePath = await _javaDownloadService.DownloadAndInstallJavaAsync(
                    _javaVersion,
                    progress,
                    _cts.Token);

                if (!_pauseRequested)
                {
                    Status = "已完成";
                    Progress = 100;
                    IsCompleted = true;
                    IsDownloading = false;
                    DownloadSpeed = "--";
                    RemainingTime = "已完成";
                    CurrentFile = "安装完成";

                    DownloadCompleted?.Invoke(javaExePath);
                }
            }
            catch (OperationCanceledException)
            {
                if (_pauseRequested)
                {
                    Status = "已暂停";
                    IsPaused = true;
                    IsDownloading = false;
                    DownloadSpeed = "--";
                    RemainingTime = "已暂停";
                    CurrentFile = "下载已暂停";
                }
                else
                {
                    Status = "已取消";
                    IsCancelled = true;
                    IsDownloading = false;
                    DownloadSpeed = "--";
                    RemainingTime = "--";
                    CurrentFile = "下载已取消";
                }
            }
            catch (Exception ex)
            {
                Status = "下载失败";
                IsFailed = true;
                IsDownloading = false;
                ErrorMessage = ex.Message;
                DownloadSpeed = "--";
                RemainingTime = "--";
                CurrentFile = "下载失败";

                DownloadFailed?.Invoke(_javaVersion.ToString(), ex.Message);
            }
            finally
            {
                _javaDownloadService.StatusChanged -= OnStatusChanged;
                _javaDownloadService.LogReceived -= OnLogReceived;
            }
        }

        public void Pause()
        {
            if (IsDownloading && !IsPaused)
            {
                _pauseRequested = true;
                _cts?.Cancel();
            }
        }

        public async void Resume()
        {
            if (IsPaused && !IsDownloading)
            {
                _pauseRequested = false;
                IsPaused = false;
                await StartDownloadAsync();
            }
        }

        public void Cancel()
        {
            _pauseRequested = false;
            _cts?.Cancel();
        }

        public void Delete()
        {
            _pauseRequested = false;
            _cts?.Cancel();
            TaskDeleted?.Invoke();
        }

        public async void Retry()
        {
            if (IsFailed || IsCancelled)
            {
                // 重置状态
                IsFailed = false;
                IsCancelled = false;
                ErrorMessage = "";
                Progress = 0;
                DownloadedBytes = 0;
                TotalBytes = 0;
                DownloadSpeed = "--";
                RemainingTime = "--";
                
                await StartDownloadAsync();
            }
        }

        private void OnStatusChanged(string status)
        {
            CurrentFile = status;
        }

        private void OnLogReceived(string log)
        {
        }

        private void OnProgressChanged(DownloadProgressInfo info)
        {
            Progress = info.Progress;
            DownloadedBytes = info.DownloadedBytes;
            TotalBytes = info.TotalBytes;
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

        private string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024):F1} MB";
        }

        public int JavaVersion => _javaVersion;
    }
}
