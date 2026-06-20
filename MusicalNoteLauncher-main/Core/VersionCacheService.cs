using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class VersionCacheService : INotifyPropertyChanged
    {
        private static VersionCacheService _instance;
        public static VersionCacheService Instance => _instance ?? (_instance = new VersionCacheService());

        private readonly VersionDownloadService _downloadService;
        private readonly ObservableCollection<VersionItem> _cachedVersions = new ObservableCollection<VersionItem>();
        private bool _isLoaded = false;
        private bool _isLoading = false;
        private DateTime _lastLoadTime = DateTime.MinValue;

        public ObservableCollection<VersionItem> CachedVersions => _cachedVersions;
        public bool IsLoaded => _isLoaded;
        public bool IsLoading => _isLoading;
        public DateTime LastLoadTime => _lastLoadTime;

        private VersionCacheService()
        {
            string gamePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft";
            _downloadService = new VersionDownloadService(gamePath);
        }

        public async Task LoadAsync(bool forceRefresh = false)
        {
            if (forceRefresh)
            {
                Logger.Info("[版本缓存] 强制刷新模式，清除缓�?..");
                ClearCache();
            }

            if (_isLoading)
            {
                Logger.Info("[版本缓存] 正在加载中，等待完成...");
                int waitCount = 0;
                while (_isLoading)
                {
                    await Task.Delay(200);
                    waitCount++;
                    if (waitCount > 50)
                    {
                        Logger.Warning("[版本缓存] 等待加载超时");
                        break;
                    }
                }
                return;
            }

            if (_isLoaded && !forceRefresh)
            {
                TimeSpan elapsed = DateTime.Now - _lastLoadTime;
                Logger.Info($"[版本缓存] 缓存已加载(距上次加载 {elapsed.TotalMinutes:F1}分钟)，跳过重复加载");
                return;
            }

            _isLoading = true;
            OnPropertyChanged(nameof(IsLoading));

            try
            {
                Logger.Info("[版本缓存] 开始后台预加载版本列表...");

                var versions = await _downloadService.GetRemoteVersionsWithSourceAsync();

                if (versions != null && versions.Count > 0)
                {
                    _cachedVersions.Clear();

                    int successCount = 0;
                    foreach (var version in versions)
                    {
                        try
                        {
                            bool isDownloaded = _downloadService.CheckVersionDownloaded(version.Id);
                            var item = new VersionItem
                            {
                                VersionId = version.Id,
                                VersionType = version.Type,
                                ReleaseTime = version.ReleaseTime,
                                DownloadUrl = version.Url,
                                Status = isDownloaded ? "已下载" : "可下载",
                                DownloadProgress = isDownloaded ? 100 : 0,
                                IsUrlValid = true
                            };

                            _cachedVersions.Add(item);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"[版本缓存] 处理版本 {version.Id} 失败: {ex.Message}");
                        }
                    }

                    Logger.Info($"[版本缓存] 成功预加载了 {_cachedVersions.Count} 个版本");
                    _isLoaded = true;
                    _lastLoadTime = DateTime.Now;
                    OnPropertyChanged(nameof(IsLoaded));
                    OnPropertyChanged(nameof(LastLoadTime));
                }
                else
                {
                    Logger.Warning("[版本缓存] 获取到空的版本列表");
                    _isLoaded = true;
                    _lastLoadTime = DateTime.Now;
                    OnPropertyChanged(nameof(IsLoaded));
                    OnPropertyChanged(nameof(LastLoadTime));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[版本缓存] 预加载失败: {ex.Message}", ex);
            }
            finally
            {
                _isLoading = false;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public async Task ForceRefreshAsync()
        {
            await LoadAsync(forceRefresh: true);
        }

        public void ClearCache()
        {
            _cachedVersions.Clear();
            _isLoaded = false;
            _isLoading = false;
            _lastLoadTime = DateTime.MinValue;
            OnPropertyChanged(nameof(IsLoaded));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(LastLoadTime));
            Logger.Info("[版本缓存] 缓存已清空");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class VersionItem : INotifyPropertyChanged
    {
        private string _versionId;
        private string _versionType;
        private DateTime _releaseTime;
        private string _status;
        private double _downloadProgress;
        private string _description;
        private string _downloadUrl;
        private bool _isUrlValid = true;
        private bool _isDetailLoaded = true;

        public string DownloadUrl
        {
            get => _downloadUrl;
            set { _downloadUrl = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDownload)); OnPropertyChanged(nameof(StatusText)); }
        }

        public bool IsUrlValid
        {
            get => _isUrlValid;
            set { _isUrlValid = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDownload)); OnPropertyChanged(nameof(StatusText)); }
        }

        public bool IsDetailLoaded
        {
            get => _isDetailLoaded;
            set { _isDetailLoaded = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDownload)); OnPropertyChanged(nameof(StatusText)); }
        }

        public bool CanDownload => !string.IsNullOrEmpty(DownloadUrl) && (Status == "可下载" || Status == "已下载");

        public string VersionId
        {
            get => _versionId;
            set { _versionId = value; OnPropertyChanged(); }
        }

        public string VersionType
        {
            get => _versionType;
            set { _versionType = value; OnPropertyChanged(); OnPropertyChanged(nameof(GroupType)); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string GroupType
        {
            get
            {
                switch (VersionType)
                {
                    case "release":
                        return "正式版";
                    case "snapshot":
                        return "预览版";
                    case "old_alpha":
                        return "远古版";
                    case "old_beta":
                        return "远古版";
                    case "alpha":
                        return "远古版";
                    case "beta":
                        return "远古版";
                    case "demo":
                        return "演示版";
                    default:
                        if (!string.IsNullOrEmpty(VersionId) && VersionId.Contains("april", StringComparison.OrdinalIgnoreCase))
                        {
                            return "愚人节版";
                        }
                        return "远古版";
                }
            }
        }

        public DateTime ReleaseTime
        {
            get => _releaseTime;
            set { _releaseTime = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDownloading)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(CanDownload)); }
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        public bool IsDownloaded => Status == "已下载";
        public bool IsDownloading => Status == "下载中";
        public string ProgressText => $"{DownloadProgress:F0}%";

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case "已下载":
                        return "重新安装";
                    case "下载中":
                        return "下载中...";
                    default:
                        return "下载";
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
