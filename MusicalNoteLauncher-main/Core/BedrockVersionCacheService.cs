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
    public class BedrockVersionCacheService : INotifyPropertyChanged
    {
        private static BedrockVersionCacheService _instance;
        public static BedrockVersionCacheService Instance => _instance ?? (_instance = new BedrockVersionCacheService());

        private readonly BedrockEnhancedDownloadService _downloadService;
        private readonly ObservableCollection<BedrockVersionInfo> _cachedVersions = new ObservableCollection<BedrockVersionInfo>();
        private bool _isLoaded = false;
        private bool _isLoading = false;
        private DateTime _lastLoadTime = DateTime.MinValue;

        public ObservableCollection<BedrockVersionInfo> CachedVersions => _cachedVersions;
        public bool IsLoaded => _isLoaded;
        public bool IsLoading => _isLoading;
        public DateTime LastLoadTime => _lastLoadTime;

        private BedrockVersionCacheService()
        {
            _downloadService = new BedrockEnhancedDownloadService(AppContext.MinecraftPath);
        }

        public async Task LoadAsync(bool forceRefresh = false)
        {
            if (forceRefresh)
            {
                Logger.Info("[基岩版缓存] 强制刷新模式，清除缓存...");
                ClearCache();
            }

            if (_isLoading)
            {
                Logger.Info("[基岩版缓存] 正在加载中，等待完成...");
                int waitCount = 0;
                while (_isLoading)
                {
                    await Task.Delay(200);
                    waitCount++;
                    if (waitCount > 50)
                    {
                        Logger.Warning("[基岩版缓存] 等待加载超时");
                        break;
                    }
                }
                return;
            }

            if (_isLoaded && !forceRefresh)
            {
                TimeSpan elapsed = DateTime.Now - _lastLoadTime;
                Logger.Info($"[基岩版缓存] 缓存已加载(距上次加载 {elapsed.TotalMinutes:F1}分钟)，跳过重复加载");
                return;
            }

            _isLoading = true;
            OnPropertyChanged(nameof(IsLoading));

            try
            {
                Logger.Info("[基岩版缓存] 开始后台预加载版本列表...");

                var versions = await _downloadService.GetRemoteVersionsAsync();

                if (versions != null && versions.Count > 0)
                {
                    _cachedVersions.Clear();

                    foreach (var version in versions)
                    {
                        try
                        {
                            _cachedVersions.Add(version);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"[基岩版缓存] 处理版本 {version.Id} 失败: {ex.Message}");
                        }
                    }

                    Logger.Info($"[基岩版缓存] 成功预加载了 {_cachedVersions.Count} 个版本");
                    _isLoaded = true;
                    _lastLoadTime = DateTime.Now;
                    OnPropertyChanged(nameof(IsLoaded));
                    OnPropertyChanged(nameof(LastLoadTime));
                }
                else
                {
                    Logger.Warning("[基岩版缓存] 获取到空的版本列表");
                    _isLoaded = true;
                    _lastLoadTime = DateTime.Now;
                    OnPropertyChanged(nameof(IsLoaded));
                    OnPropertyChanged(nameof(LastLoadTime));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[基岩版缓存] 预加载失败: {ex.Message}", ex);
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
            Logger.Info("[基岩版缓存] 缓存已清空");
        }

        public void SetCachedVersions(List<BedrockVersionInfo> versions)
        {
            _cachedVersions.Clear();
            foreach (var version in versions)
            {
                _cachedVersions.Add(version);
            }
            _isLoaded = true;
            _isLoading = false;
            _lastLoadTime = DateTime.Now;
            OnPropertyChanged(nameof(IsLoaded));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(LastLoadTime));
            Logger.Info($"[基岩版缓存] 通过外部数据更新缓存，共 {versions.Count} 个版本");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}