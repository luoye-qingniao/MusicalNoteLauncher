using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;
using MusicalNoteLauncher.Windows;

namespace MusicalNoteLauncher.Pages
{
    public partial class GameVersionsPage : UserControl
    {
        private readonly VersionDownloadService _downloadService;
        private bool _isLoading;
        private bool _isInstalledLoading;
        private ObservableCollection<VersionItem> _latestVersions = new ObservableCollection<VersionItem>();
        private ObservableCollection<VersionGroup> _versionGroups = new ObservableCollection<VersionGroup>();
        private ObservableCollection<VersionGroup> _installedVersionGroups = new ObservableCollection<VersionGroup>();

        public GameVersionsPage()
        {
            InitializeComponent();
            string minecraftPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
            _downloadService = new VersionDownloadService(minecraftPath);
            lvLatestVersions.ItemsSource = _latestVersions;
            icVersions.ItemsSource = _versionGroups;
            icInstalledVersions.ItemsSource = _installedVersionGroups;
            VersionTabControl.SelectionChanged += VersionTabControl_SelectionChanged;
            Loaded += GameVersionsPage_Loaded;
            VersionScanService.Instance.ScanCompleted += OnVersionScanCompleted;
            DownloadTaskManager.Instance.TaskCompleted += OnDownloadTaskCompleted;
            DownloadTaskManager.Instance.TaskFailed += OnDownloadTaskFailed;
        }

        private void OnVersionScanCompleted(VersionScanResult result)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateDownloadButtonStates(result);
                if (VersionTabControl.SelectedIndex == 1)
                {
                    LoadInstalledVersionsAsync();
                }
            });
        }

        private void VersionsContainer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            VersionScanService.Instance.ScanAsync("点击下载页面版本列表刷新");
        }

        private void InstalledVersionsContainer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            VersionScanService.Instance.ScanAsync("点击已安装版本列表刷新");
        }

        private void OnDownloadTaskCompleted(IDownloadTask task)
        {
            Logger.Info("[下载任务回调] 任务完成: " + task.VersionId + "，触发版本扫描");
            VersionScanService.Instance.ScanAsync("下载完成:" + task.VersionId);
        }

        private void OnDownloadTaskFailed(IDownloadTask task)
        {
            Logger.Info("[下载任务回调] 任务失败: " + task.VersionId + "，触发版本扫描");
            VersionScanService.Instance.ScanAsync("下载失败:" + task.VersionId);
        }

        private void UpdateDownloadButtonStates(VersionScanResult result)
        {
            foreach (VersionItem versionItem in _latestVersions)
            {
                bool isInstalled = result.JavaVersions.Contains(versionItem.VersionId);
                if (isInstalled && versionItem.Status != "下载中")
                {
                    versionItem.Status = "已下载";
                    versionItem.DownloadProgress = 100.0;
                }
                else if (!isInstalled && versionItem.Status == "已下载")
                {
                    versionItem.Status = "可下载";
                    versionItem.DownloadProgress = 0.0;
                }
            }
            foreach (VersionGroup versionGroup in _versionGroups)
            {
                foreach (VersionItem versionItem in versionGroup.Versions)
                {
                    bool isInstalled = result.JavaVersions.Contains(versionItem.VersionId);
                    if (isInstalled && versionItem.Status != "下载中")
                    {
                        versionItem.Status = "已下载";
                        versionItem.DownloadProgress = 100.0;
                    }
                    else if (!isInstalled && versionItem.Status == "已下载")
                    {
                        versionItem.Status = "可下载";
                        versionItem.DownloadProgress = 0.0;
                    }
                }
            }
        }

        private async void GameVersionsPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= GameVersionsPage_Loaded;
            await LoadVersionListAsync();
            VersionScanService.Instance.ClearCache();
            VersionScanService.Instance.ScanAsync("GameVersionsPage 初始化");
        }

        private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer visualChild = GetVisualChild<ScrollViewer>(this);
            if (visualChild != null)
            {
                visualChild.ScrollToVerticalOffset(visualChild.VerticalOffset - (double)(e.Delta / 3));
                e.Handled = true;
            }
        }

        private static T GetVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                T t = child as T;
                if (t != null)
                {
                    return t;
                }
                T visualChild = GetVisualChild<T>(child);
                if (visualChild != null)
                {
                    return visualChild;
                }
            }
            return default(T);
        }

        private async Task LoadVersionListAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                await LoadFromRemote();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task LoadFromRemote()
        {
            try
            {
                var versions = await _downloadService.GetRemoteVersionsAsync();
                List<VersionItem> items = new List<VersionItem>();
                foreach (var info in versions)
                {
                    bool isDownloaded = CheckVersionDownloaded(info.Id);
                    items.Add(new VersionItem
                    {
                        VersionId = info.Id,
                        VersionType = info.Type,
                        ReleaseTime = info.ReleaseTime,
                        Status = isDownloaded ? "已下载" : "可下载",
                        DownloadProgress = isDownloaded ? 100.0 : 0.0
                    });
                }
                BindVersionData(items);
            }
            catch (Exception ex)
            {
                Logger.Error("[UI加载] 加载远程版本列表失败: " + ex.Message);
            }
        }

        private void BindVersionData(IEnumerable<VersionItem> items)
        {
            int releaseCount = items.Count(v => v.VersionType == "release");
            int snapshotCount = items.Count(v => v.VersionType == "snapshot");
            int oldCount = items.Count(v => v.VersionType != "release" && v.VersionType != "snapshot");
            int downloadedCount = items.Count(v => v.IsDownloaded);
            
            Logger.Info($"[UI加载] 数据绑定: 正式版 {releaseCount} 个, 快照版 {snapshotCount} 个, 老旧版本 {oldCount} 个");
            
            List<VersionItem> sortedItems = items.OrderByDescending(v => v.ReleaseTime).ToList();
            VersionItem latestRelease = sortedItems.FirstOrDefault(v => v.VersionType == "release");
            VersionItem latestSnapshot = sortedItems.FirstOrDefault(v => v.VersionType == "snapshot");

            _latestVersions.Clear();
            if (latestRelease != null)
            {
                latestRelease.Description = $"最新正式版，发布于 {latestRelease.ReleaseTime:yyyy/MM/dd HH:mm:ss}";
                _latestVersions.Add(latestRelease);
            }
            if (latestSnapshot != null)
            {
                string snapshotId = latestSnapshot.VersionId;
                string releaseId = latestRelease?.VersionId;
                if (snapshotId != releaseId)
                {
                    latestSnapshot.Description = $"最新预览版，发布于 {latestSnapshot.ReleaseTime:yyyy/MM/dd HH:mm:ss}";
                    _latestVersions.Add(latestSnapshot);
                }
            }

            IEnumerable<VersionItem> remainingItems = items.Where(v => v != latestRelease && v != latestSnapshot);
            _versionGroups.Clear();
            
            foreach (var grouping in remainingItems.GroupBy(v => v.GroupType).OrderBy(g => GetGroupOrder(g.Key)))
            {
                VersionGroup versionGroup = new VersionGroup();
                versionGroup.Name = grouping.Key;
                versionGroup.SetCachedVersions(grouping.OrderByDescending(v => v.ReleaseTime).ToList());
                _versionGroups.Add(versionGroup);
            }

            Logger.Info("[UI加载] 版本添加完成！");
            Logger.Info($"[UI加载] 统计: 共 {releaseCount + snapshotCount + oldCount} 个版本");
            Logger.Info($"[UI加载] 统计: 正式版 {releaseCount} 个，快照版 {snapshotCount} 个，老旧版本 {oldCount} 个");
            Logger.Info($"[UI加载] 统计: 已下载 {downloadedCount} 个，可下载 {releaseCount + snapshotCount + oldCount - downloadedCount} 个");
        }

        private int GetGroupOrder(string groupName)
        {
            switch (groupName)
            {
                case "正式版":
                    return 0;
                case "预览版":
                    return 1;
                case "远古版":
                    return 2;
                case "愚人节版":
                    return 3;
                default:
                    return 10;
            }
        }

        private bool CheckVersionDownloaded(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"), "versions", versionId);
                if (!Directory.Exists(versionDir))
                    return false;
                string jsonPath = Path.Combine(versionDir, versionId + ".json");
                string jarPath = Path.Combine(versionDir, versionId + ".jar");
                return File.Exists(jsonPath) && File.Exists(jarPath);
            }
            catch
            {
                return false;
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            VersionItem item = btn.DataContext as VersionItem;
            if (item == null) return;

            if (item.Status == "下载中") return;

            try
            {
                item.Status = "下载中";
                item.DownloadProgress = 0.0;

                var downloader = new VersionDownloader(item.VersionId);
                downloader.ShowDialog();

                VersionScanService.Instance.ScanAsync("下载窗口关闭");
            }
            catch (Exception ex)
            {
                Logger.Error("[下载] 启动下载失败: " + ex.Message);
                item.Status = "可下载";
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        private void VersionTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionTabControl.SelectedIndex == 0)
            {
                VersionScanService.Instance.ScanAsync("切换到下载页");
                LoadVersionListAsync();
            }
            else if (VersionTabControl.SelectedIndex == 1)
            {
                VersionScanService.Instance.ScanAsync("切换到已安装页");
                LoadInstalledVersionsAsync();
            }
        }

        private async void LoadInstalledVersionsAsync()
        {
            if (_isInstalledLoading) return;
            _isInstalledLoading = true;

            try
            {
                var result = await VersionScanService.Instance.ScanAsync("加载已安装版本");
                var items = await CreateVersionItemsFromScanResult(result);
                BindInstalledVersionData(items);
            }
            finally
            {
                _isInstalledLoading = false;
            }
        }

        private async Task<List<VersionItem>> CreateVersionItemsFromScanResult(VersionScanResult result)
        {
            List<VersionItem> items = new List<VersionItem>();
            string versionsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "versions");

            foreach (string versionId in result.JavaVersions)
            {
                string jsonPath = Path.Combine(versionsDir, versionId, versionId + ".json");
                VersionItem item = ParseVersionJson(jsonPath);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private VersionItem ParseVersionJson(string jsonPath)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonPath)))
                {
                    JsonElement root = doc.RootElement;
                    string versionId = string.Empty;
                    string versionType = "release";
                    DateTime releaseTime = DateTime.MinValue;

                    if (root.TryGetProperty("id", out JsonElement idElement))
                    {
                        versionId = idElement.GetString();
                    }
                    if (root.TryGetProperty("type", out JsonElement typeElement))
                    {
                        versionType = typeElement.GetString() ?? "release";
                    }
                    if (root.TryGetProperty("releaseTime", out JsonElement timeElement))
                    {
                        string timeStr = timeElement.GetString();
                        if (!string.IsNullOrEmpty(timeStr) && DateTime.TryParse(timeStr, out DateTime parsedTime))
                        {
                            releaseTime = parsedTime;
                        }
                    }

                    if (string.IsNullOrEmpty(versionId))
                        return null;

                    return new VersionItem
                    {
                        VersionId = versionId,
                        VersionType = versionType,
                        ReleaseTime = releaseTime,
                        Status = "已下载",
                        DownloadProgress = 100.0
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("[UI加载] 解析版本JSON失败: " + jsonPath + ", 错误: " + ex.Message);
                return null;
            }
        }

        private void BindInstalledVersionData(List<VersionItem> items)
        {
            _installedVersionGroups.Clear();
            if (items.Count == 0)
            {
                Logger.Info("[UI加载] 未发现已安装的版本");
                return;
            }

            foreach (var grouping in items.GroupBy(v => v.GroupType).OrderBy(g => GetGroupOrder(g.Key)))
            {
                VersionGroup versionGroup = new VersionGroup();
                versionGroup.Name = grouping.Key;
                versionGroup.SetCachedVersions(grouping.OrderByDescending(v => v.ReleaseTime).ToList());
                versionGroup.IsExpanded = true;
                _installedVersionGroups.Add(versionGroup);
            }

            Logger.Info($"[UI加载] 已安装版本分组完成: {_installedVersionGroups.Count} 个分组");
        }

        private void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            VersionItem item = button.DataContext as VersionItem;
            if (item == null) return;

            try
            {
                Logger.Info("[游戏启动] 尝试启动版本: " + item.VersionId);
                string minecraftPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
                var javaConfig = new JavaConfigManager(minecraftPath);
                new GameLauncher(minecraftPath, javaConfig).LaunchGameAsync(item.VersionId, "Player", 2048, 4096, "", true, SettingsManager.Settings.Resolution, false).ContinueWith(task =>
                {
                    if (task.Result)
                    {
                        Logger.Info("[游戏启动] 版本 " + item.VersionId + " 启动成功");
                    }
                    else
                    {
                        Logger.Error("[游戏启动] 版本 " + item.VersionId + " 启动失败");
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("启动失败，请检查日志", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error("[游戏启动] 启动版本 " + item.VersionId + " 异常: " + ex.Message);
                MessageBox.Show("启动失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        private void BtnDeleteVersion_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            VersionItem versionItem = button.DataContext as VersionItem;
            if (versionItem == null) return;

            if (MessageBox.Show($"确定要删除版本 {versionItem.VersionId} 吗？\n此操作将删除该版本的所有文件，且无法撤销。", "确认删除", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
            {
                DeleteVersion(versionItem.VersionId);
            }
        }

        private async void DeleteVersion(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"), "versions", versionId);
                if (Directory.Exists(versionDir))
                {
                    Directory.Delete(versionDir, true);
                    Logger.Info("[版本删除] 成功删除版本: " + versionId);
                    RefreshDownloadPageStatus(versionId);
                    VersionScanService.Instance.ScanAsync("删除版本后刷新");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[版本删除] 删除版本失败: " + versionId + ", 错误: " + ex.Message);
                MessageBox.Show("删除失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        private void RefreshDownloadPageStatus(string versionId)
        {
            foreach (VersionItem item in _latestVersions)
            {
                if (item.VersionId == versionId)
                {
                    item.Status = "可下载";
                    item.DownloadProgress = 0.0;
                }
            }
            foreach (VersionGroup group in _versionGroups)
            {
                foreach (VersionItem item in group.Versions)
                {
                    if (item.VersionId == versionId)
                    {
                        item.Status = "可下载";
                        item.DownloadProgress = 0.0;
                    }
                }
            }
        }
    }
}
