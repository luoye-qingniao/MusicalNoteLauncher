using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 基岩版命令集合：封装一键下载、离线启动、版本切换、取消下载等操作逻辑
    /// 绑定到BedrockPage的UI控件，不影响原有按钮事件与导航逻辑
    /// </summary>
    public class BedrockCommand : INotifyPropertyChanged
    {
        private readonly BedrockEnhancedDownloadService _downloadService;
        private readonly BedrockOfflineLauncher _offlineLauncher;
        private readonly string _minecraftPath;

        private bool _isDownloading;
        private bool _isLaunching;
        private string _statusText = "就绪";
        private double _downloadProgress;
        private long _downloadedBytes;
        private long _totalBytes;
        private List<BedrockVersionInfo> _availableVersions = new();

        /// <summary>是否正在下载</summary>
        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDownload)); OnPropertyChanged(nameof(CanLaunch)); }
        }

        /// <summary>是否正在启动</summary>
        public bool IsLaunching
        {
            get => _isLaunching;
            set { _isLaunching = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDownload)); OnPropertyChanged(nameof(CanLaunch)); }
        }

        /// <summary>状态文本</summary>
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        /// <summary>下载进度 (0-100)</summary>
        public double DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        /// <summary>已下载字节数</summary>
        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set { _downloadedBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadSizeText)); }
        }

        /// <summary>总字节数</summary>
        public long TotalBytes
        {
            get => _totalBytes;
            set { _totalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadSizeText)); }
        }

        /// <summary>下载大小文本</summary>
        public string DownloadSizeText =>
            TotalBytes > 0
                ? $"{FileSizeFormatter.FormatFileSize(DownloadedBytes)} / {FileSizeFormatter.FormatFileSize(TotalBytes)}"
                : DownloadedBytes > 0 ? FileSizeFormatter.FormatFileSize(DownloadedBytes) : "";

        /// <summary>可用版本列表</summary>
        public List<BedrockVersionInfo> AvailableVersions
        {
            get => _availableVersions;
            set { _availableVersions = value ?? new(); OnPropertyChanged(); }
        }

        /// <summary>当前选中的版本</summary>
        public BedrockVersionInfo SelectedVersion { get; set; }

        /// <summary>离线模式开关</summary>
        private bool _offlineMode = true;
        public bool OfflineMode
        {
            get => _offlineMode;
            set { _offlineMode = value; OnPropertyChanged(); }
        }

        /// <summary>离线用户名</summary>
        private string _offlineUsername = "Player";
        public string OfflineUsername
        {
            get => _offlineUsername;
            set { _offlineUsername = value ?? "Player"; OnPropertyChanged(); }
        }

        /// <summary>是否可以执行下载</summary>
        public bool CanDownload => !IsDownloading && !IsLaunching;

        /// <summary>是否可以执行启动</summary>
        public bool CanLaunch => !IsDownloading && !IsLaunching && SelectedVersion != null;

        /// <summary>是否可以取消下载</summary>
        public bool CanCancel => IsDownloading;

        /// <summary>日志输出事件</summary>
        public event Action<string> LogOutput;
        /// <summary>下载完成事件</summary>
        public event Action<string> DownloadFinished;
        /// <summary>启动完成事件</summary>
        public event Action<bool> LaunchFinished;
        /// <summary>版本列表加载完成事件</summary>
        public event Action VersionsLoaded;

        public BedrockCommand(string minecraftPath)
        {
            _minecraftPath = minecraftPath ?? throw new ArgumentNullException(nameof(minecraftPath));
            _downloadService = new BedrockEnhancedDownloadService(_minecraftPath);
            _offlineLauncher = new BedrockOfflineLauncher(_minecraftPath);

            // 订阅下载服务事件
            _downloadService.StatusChanged += msg =>
            {
                StatusText = msg;
                LogOutput?.Invoke(msg);
            };

            _downloadService.DownloadCompleted += versionId =>
            {
                IsDownloading = false;
                StatusText = "下载完成";
                LogOutput?.Invoke($"基岩版 {versionId} 下载完成，完整性校验通过");
                DownloadFinished?.Invoke(versionId);
                // 刷新版本列表
                _ = RefreshVersionsAsync();
            };

            _downloadService.DownloadFailed += (versionId, error) =>
            {
                IsDownloading = false;
                StatusText = $"下载失败: {error}";
                LogOutput?.Invoke($"下载失败: {error}");
            };

            // 订阅离线启动器事件
            _offlineLauncher.LaunchStatusChanged += msg =>
            {
                StatusText = msg;
                LogOutput?.Invoke(msg);
            };

            _offlineLauncher.LaunchLogReceived += msg =>
            {
                LogOutput?.Invoke(msg);
            };

            _offlineLauncher.LaunchCompleted += success =>
            {
                IsLaunching = false;
                LaunchFinished?.Invoke(success);
            };
        }

        // ────────────────────────── 命令方法 ──────────────────────────

        /// <summary>刷新版本列表</summary>
        public async Task RefreshVersionsAsync()
        {
            try
            {
                StatusText = "正在获取版本列表...";
                var versions = await _downloadService.GetRemoteVersionsAsync();
                AvailableVersions = versions;

                // 自动选中最新稳定版
                if (SelectedVersion == null && versions.Count > 0)
                {
                    SelectedVersion = versions
                        .FindAll(v => v.Type == "release")
                        .Count > 0
                            ? versions.FindAll(v => v.Type == "release")[0]
                            : versions[0];
                }

                VersionsLoaded?.Invoke();
                StatusText = $"就绪 ({versions.Count} 个版本可用)";
                LogOutput?.Invoke($"获取到 {versions.Count} 个基岩版版本");
            }
            catch (Exception ex)
            {
                StatusText = "获取版本列表失败";
                LogOutput?.Invoke($"错误: {ex.Message}");
            }
        }

        /// <summary>一键下载命令</summary>
        public async Task ExecuteDownloadAsync()
        {
            if (IsDownloading || SelectedVersion == null) return;

            try
            {
                IsDownloading = true;
                DownloadProgress = 0;
                DownloadedBytes = 0;
                TotalBytes = 0;

                LogOutput?.Invoke($"开始下载基岩版 {SelectedVersion.Id}...");

                var progress = new DownloadProgress();
                progress.ProgressChanged += info =>
                {
                    DownloadProgress = info.Progress;
                    DownloadedBytes = info.DownloadedBytes;
                    TotalBytes = info.TotalBytes;
                };

                var result = await _downloadService.StartDownloadAsync(SelectedVersion, progress);

                if (result.IsCompleted)
                {
                    LogOutput?.Invoke($"下载完成! 版本: {SelectedVersion.Id}");
                }
                else if (result.Status == "已取消")
                {
                    LogOutput?.Invoke("下载已取消");
                }
                else
                {
                    LogOutput?.Invoke($"下载失败: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                LogOutput?.Invoke($"下载异常: {ex.Message}");
                IsDownloading = false;
            }
        }

        /// <summary>取消下载命令</summary>
        public void ExecuteCancelDownload()
        {
            _downloadService.CancelDownload();
            LogOutput?.Invoke("正在取消下载...");
        }

        /// <summary>离线启动命令</summary>
        public async Task ExecuteLaunchAsync()
        {
            if (IsLaunching || SelectedVersion == null) return;

            try
            {
                IsLaunching = true;

                string username = OfflineMode ? OfflineUsername : "Player";
                LogOutput?.Invoke($"准备启动基岩版 {SelectedVersion.Id} ({(OfflineMode ? "离线模式" : "在线模式")})");

                bool success;
                if (OfflineMode)
                {
                    success = await _offlineLauncher.LaunchOfflineAsync(SelectedVersion.Id, username);
                }
                else
                {
                    // 在线模式使用原有BedrockLauncher
                    var onlineLauncher = new BedrockLauncher(_minecraftPath);
                    onlineLauncher.LaunchStatusChanged += msg => { StatusText = msg; LogOutput?.Invoke(msg); };
                    onlineLauncher.LaunchLogReceived += msg => LogOutput?.Invoke(msg);
                    onlineLauncher.LaunchCompleted += result => { IsLaunching = false; LaunchFinished?.Invoke(result); };
                    success = await onlineLauncher.LaunchGameAsync(SelectedVersion.Id);
                }

                if (!success)
                {
                    LogOutput?.Invoke("启动失败，请检查版本安装和运行环境");
                }
            }
            catch (Exception ex)
            {
                LogOutput?.Invoke($"启动异常: {ex.Message}");
                IsLaunching = false;
            }
        }

        /// <summary>切换版本命令</summary>
        public void ExecuteSwitchVersion(BedrockVersionInfo version)
        {
            if (version == null) return;
            SelectedVersion = version;
            LogOutput?.Invoke($"切换版本: {version.Id}");
            OnPropertyChanged(nameof(SelectedVersion));
            OnPropertyChanged(nameof(CanLaunch));
        }

        /// <summary>获取已安装版本列表</summary>
        public List<BedrockVersionInfo> GetInstalledVersions()
        {
            return _downloadService.GetInstalledVersions();
        }

        /// <summary>删除版本</summary>
        public bool DeleteVersion(string versionId)
        {
            bool result = _downloadService.DeleteVersion(versionId);
            if (result)
            {
                LogOutput?.Invoke($"已删除版本: {versionId}");
                _ = RefreshVersionsAsync();
            }
            return result;
        }

        /// <summary>获取离线账户列表</summary>
        public IReadOnlyList<BedrockOfflineAccount> GetOfflineAccounts()
        {
            return _offlineLauncher.GetAccounts();
        }

        /// <summary>清理资源</summary>
        public void Dispose()
        {
            _downloadService?.Dispose();
        }

        // ────────────────────────── INotifyPropertyChanged ──────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
