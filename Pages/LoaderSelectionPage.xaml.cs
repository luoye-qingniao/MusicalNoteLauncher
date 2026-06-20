using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;
using MusicalNoteLauncher.Windows;

namespace MusicalNoteLauncher.Pages
{
    public partial class LoaderSelectionPage : UserControl
    {
        private static readonly Dictionary<string, string> _loaderIconMap = new Dictionary<string, string>
        {
            { "Forge", "Anvil.png" },
            { "Fabric", "Fabric.png" },
            { "NeoForge", "NeoForge.png" },
            { "OptiFine", "GrassPath.png" }
        };

        private static readonly Dictionary<string, string> _placeholderColors = new Dictionary<string, string>
        {
            { "Forge", "#1F4E79" },
            { "Fabric", "#1B5E20" },
            { "NeoForge", "#880E4F" },
            { "OptiFine", "#F57F17" }
        };

        private static readonly Dictionary<string, string> _selectedColors = new Dictionary<string, string>
        {
            { "Forge", "#42A5F5" },
            { "Fabric", "#66BB6A" },
            { "NeoForge", "#EC407A" },
            { "OptiFine", "#FFB74D" }
        };

        private static readonly Dictionary<string, BitmapImage> _iconCache = new Dictionary<string, BitmapImage>();
        private static readonly object _lockObj = new object();

        private readonly string _mcPath = AppContext.MinecraftPath;

        private readonly ForgeInstaller _forgeInstaller;
        private readonly FabricInstaller _fabricInstaller;
        private readonly NeoForgeInstaller _neoForgeInstaller;
        private readonly OptiFineInstaller _optiFineInstaller;

        private readonly Dictionary<string, object> _selectedLoaders = new Dictionary<string, object>();

        // 每个加载器当前是否有可用版本（false + 正在获取中 = 还没结果；false + 已获取完成 = 真的没有可用版本）
        private readonly Dictionary<string, bool> _hasAvailableVersions = new Dictionary<string, bool>
        {
            { "Forge", false },
            { "Fabric", false },
            { "NeoForge", false },
            { "OptiFine", false }
        };

        // 每个加载器是否还在“正在获取”阶段（完成后才置为 false）
        private readonly Dictionary<string, bool> _fetchInProgress = new Dictionary<string, bool>
        {
            { "Forge", false },
            { "Fabric", false },
            { "NeoForge", false },
            { "OptiFine", false }
        };

        // 记录加载器类型 -> UI 控件展示映射，方便并行更新
        // 注意：此字段在 InitializeComponent() 之后再赋值，因为控件引用在那之前都是 null
        private Dictionary<string, (Border bar, TextBlock status, Button remove)> _cardMap;

        private CancellationTokenSource _cancellation;

        // 来自 ModManagerPage 的加载器类型提示（仅设置类型，尚未选版本）
        private string _pendingAutoNavigateLoaderType;

        public LoaderSelectionPage()
        {
            _forgeInstaller = new ForgeInstaller(_mcPath);
            _fabricInstaller = new FabricInstaller(_mcPath);
            _neoForgeInstaller = new NeoForgeInstaller(_mcPath);
            _optiFineInstaller = new OptiFineInstaller(_mcPath);

            InitializeComponent();

            // 必须在 InitializeComponent() 之后才能拿到非空的控件引用
            _cardMap = new Dictionary<string, (Border, TextBlock, Button)>
            {
                { "Forge", (barForge, txtForgeStatus, btnRemoveForge) },
                { "Fabric", (barFabric, txtFabricStatus, btnRemoveFabric) },
                { "NeoForge", (barNeoForge, txtNeoForgeStatus, btnRemoveNeoForge) },
                { "OptiFine", (barOptiFine, txtOptiFineStatus, btnRemoveOptiFine) }
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try { _cancellation?.Cancel(); } catch { }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            string version = AppContext.SelectedGameVersion ?? "未知版本";
            txtVersionInfo.Text = "Minecraft " + version;

            string type = AppContext.SelectedLoaderType;
            if (!string.IsNullOrEmpty(type) && AppContext.SelectedLoaderVersion != null)
            {
                _selectedLoaders[type] = AppContext.SelectedLoaderVersion;
                AppContext.SelectedLoaderType = null;
                AppContext.SelectedLoaderVersion = null;
            }
            else if (!string.IsNullOrEmpty(type))
            {
                // 来自 ModManagerPage 的加载器安装提示：先记录，等版本列表加载完后自动跳转
                _pendingAutoNavigateLoaderType = type;
                AppContext.SelectedLoaderType = null;
            }

            SetIcon(imgForge, "Forge");
            SetIcon(imgFabric, "Fabric");
            SetIcon(imgNeoForge, "NeoForge");
            SetIcon(imgOptiFine, "OptiFine");

            // 先把所有卡片置为「正在获取」——页面刚渲染时不显示任何默认的“可以添加”
            SetAllCardsFetching();

            // 若无选中的 MC 版本，显示「未选择游戏版本」后 return
            string mcVersion = AppContext.SelectedGameVersion;
            if (string.IsNullOrEmpty(mcVersion))
            {
                if (_cardMap != null)
                {
                    foreach (var kv in _cardMap)
                    {
                        if (kv.Value.status != null)
                        {
                            kv.Value.status.Text = "未选择游戏版本";
                            kv.Value.status.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x65));
                        }
                        if (kv.Value.bar != null)
                            kv.Value.bar.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#2A2A2A");
                        if (kv.Value.remove != null)
                            kv.Value.remove.Visibility = Visibility.Collapsed;
                        _hasAvailableVersions[kv.Key] = false;
                        _fetchInProgress[kv.Key] = false;
                    }
                }
                RefreshAllCards();
                return;
            }

            // 若已有该 MC 版本的缓存：逐张卡片立即恢复状态，不闪烁
            bool hasCache = string.Equals(AppContext.CachedLoaderMcVersion, mcVersion, StringComparison.Ordinal)
                           && AppContext.ForgeVersions != null
                           && AppContext.FabricVersions != null
                           && AppContext.NeoForgeVersions != null
                           && AppContext.OptiFineVersions != null;

            if (hasCache)
            {
                UpdateCardAfterFetch("Forge", AppContext.ForgeVersions);
                UpdateCardAfterFetch("Fabric", AppContext.FabricVersions);
                UpdateCardAfterFetch("NeoForge", AppContext.NeoForgeVersions);
                UpdateCardAfterFetch("OptiFine", AppContext.OptiFineVersions);
                RefreshAllCards();
                TryAutoNavigateToLoader();
                return;
            }

            // 取消上一次可能未完成的请求
            try { _cancellation?.Cancel(); } catch { }
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;

            // 逐个加载器发起异步请求，每个完成后立即更新自己那张卡片（不等待其他加载器）
            _ = RunAndUpdateAsync("Forge", () => _forgeInstaller.GetForgeVersionsAsync(mcVersion, token));
            _ = RunAndUpdateAsync("Fabric", () => _fabricInstaller.GetFabricVersionsAsync(mcVersion, token));
            _ = RunAndUpdateAsync("NeoForge", () => _neoForgeInstaller.GetNeoForgeVersionsAsync(mcVersion, token));
            _ = RunAndUpdateAsync("OptiFine", () => _optiFineInstaller.GetOptiFineVersionsAsync(mcVersion, token));

            RefreshAllCards();
        }

        /// <summary>
        /// 把所有卡片重置为「正在获取」状态，避免页面刚渲染时显示残留的旧文字。
        /// </summary>
        private void SetAllCardsFetching()
        {
            if (_cardMap == null) return;
            foreach (var kv in _cardMap)
            {
                if (kv.Value.status != null)
                {
                    kv.Value.status.Text = "正在获取";
                    kv.Value.status.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0));
                }
                if (kv.Value.bar != null)
                    kv.Value.bar.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#2A2A2A");
                if (kv.Value.remove != null)
                    kv.Value.remove.Visibility = Visibility.Collapsed;
                _hasAvailableVersions[kv.Key] = false;
                _fetchInProgress[kv.Key] = true;
            }
        }

        /// <summary>
        /// 单独执行一个加载器的获取并在完成后立即更新该卡片的状态；
        /// 同时把结果写入 AppContext 缓存。
        /// </summary>
        private async Task RunAndUpdateAsync<T>(string loaderKey, Func<Task<List<T>>> fetch)
            where T : class
        {
            List<T> list;
            try
            {
                list = await fetch();
                if (list == null) list = new List<T>();
            }
            catch (Exception ex)
            {
                Logger.Warning("获取 " + loaderKey + " 版本失败: " + ex.Message);
                list = new List<T>();
            }

            // 写入缓存（按类型区分，用列表引用直接赋值）
            if (loaderKey == "Forge") AppContext.ForgeVersions = list as List<ForgeInstaller.ForgeVersionInfo>;
            else if (loaderKey == "Fabric") AppContext.FabricVersions = list as List<FabricVersionInfo>;
            else if (loaderKey == "NeoForge") AppContext.NeoForgeVersions = list as List<NeoForgeVersionInfo>;
            else if (loaderKey == "OptiFine") AppContext.OptiFineVersions = list as List<OptiFineVersionInfo>;

            // 所有 4 个加载器都获取完后，记录缓存对应的 MC 版本
            if (AppContext.ForgeVersions != null
                && AppContext.FabricVersions != null
                && AppContext.NeoForgeVersions != null
                && AppContext.OptiFineVersions != null)
            {
                AppContext.CachedLoaderMcVersion = AppContext.SelectedGameVersion;
            }

            // 在 UI 线程更新这张卡片的状态（已选状态由 RefreshAllCards 里的 UpdateCardState 处理）
            UpdateCardAfterFetch(loaderKey, list);
            RefreshAllCards();
            TryAutoNavigateToLoader();
        }

        /// <summary>
        /// 如果有来自 ModManagerPage 的待处理加载器类型提示，且对应加载器已有可用版本，则自动跳转到版本选择页
        /// </summary>
        private void TryAutoNavigateToLoader()
        {
            if (_pendingAutoNavigateLoaderType == null) return;

            var loaderType = _pendingAutoNavigateLoaderType;
            if (_fetchInProgress.TryGetValue(loaderType, out var inProgress) && inProgress) return;

            // 清除待处理标记（无论有无版本，只尝试一次）
            _pendingAutoNavigateLoaderType = null;

            if (_hasAvailableVersions.TryGetValue(loaderType, out var hasVer) && hasVer)
            {
                GoToLoaderVersionPage(loaderType);
            }
        }

        private async Task<List<T>> SafeGetAsync<T>(Func<Task<List<T>>> fetch)
            where T : class
        {
            try
            {
                var list = await fetch();
                return list ?? new List<T>();
            }
            catch (Exception ex)
            {
                Logger.Warning("获取加载器版本失败: " + ex.Message);
                return new List<T>();
            }
        }

        private void UpdateCardAfterFetch<T>(string loaderKey, List<T> versions)
            where T : class
        {
            if (_cardMap == null || !_cardMap.TryGetValue(loaderKey, out var card)) return;

            _hasAvailableVersions[loaderKey] = versions != null && versions.Count > 0;
            _fetchInProgress[loaderKey] = false;

            // 如果用户已选过这个加载器，保留已选提示，不覆盖
            if (_selectedLoaders.ContainsKey(loaderKey) && _selectedLoaders[loaderKey] != null)
                return;

            if (card.status == null) return;

            if (versions == null || versions.Count == 0)
            {
                card.status.Text = "暂无可用";
                card.status.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x80, 0x80));
            }
            else
            {
                card.status.Text = "可以添加";
                card.status.Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
            }
        }

        private void SetIcon(Image target, string loaderKey)
        {
            if (target == null) return;

            try
            {
                BitmapImage cached;
                lock (_lockObj)
                {
                    if (_iconCache.TryGetValue(loaderKey, out cached))
                    {
                        target.Source = cached;
                        return;
                    }
                }

                if (!_loaderIconMap.TryGetValue(loaderKey, out var fileName))
                {
                    target.Source = CreateFallback(loaderKey);
                    return;
                }

                BitmapImage bmp = null;
                try
                {
                    string packUri = "pack://application:,,,/提取的模组加载器图标/" + fileName;
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(packUri, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                }
                catch
                {
                    string filePath = Path.Combine(System.AppContext.BaseDirectory, "提取的模组加载器图标", fileName);
                    if (File.Exists(filePath))
                    {
                        bmp = new BitmapImage();
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = fs;
                            bmp.EndInit();
                        }
                        bmp.Freeze();
                    }
                }

                if (bmp == null)
                    bmp = CreateFallback(loaderKey);

                lock (_lockObj)
                {
                    _iconCache[loaderKey] = bmp;
                }

                target.Source = bmp;
            }
            catch (Exception ex)
            {
                Logger.Warning("加载 " + loaderKey + " 图标失败: " + ex.Message);
                target.Source = CreateFallback(loaderKey);
            }
        }

        private BitmapImage CreateFallback(string loaderKey)
        {
            string shortName =
                loaderKey == "Forge" ? "F" :
                loaderKey == "Fabric" ? "Fa" :
                loaderKey == "NeoForge" ? "N" : "O";

            if (!_placeholderColors.TryGetValue(loaderKey, out var bgHex))
                bgHex = "#2A2A2A";

            const int size = 96;
            var render = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            var vis = new DrawingVisual();
            using (var dc = vis.RenderOpen())
            {
                var color = (Color)ColorConverter.ConvertFromString(bgHex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                var rect = new Rect(0, 0, size, size);
                var rounded = new RectangleGeometry(rect, 18, 18);
                dc.DrawGeometry(brush, null, rounded);

                var text = new FormattedText(
                    shortName,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Microsoft YaHei"),
                        FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    shortName.Length > 1 ? 42 : 56,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                var origin = new Point((rect.Width - text.Width) / 2, (rect.Height - text.Height) / 2 - 4);
                dc.DrawText(text, origin);
            }
            render.Render(vis);
            render.Freeze();

            var bmp = new BitmapImage();
            using (var ms = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(render));
                encoder.Save(ms);
                ms.Position = 0;

                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
            }
            return bmp;
        }

        private void RefreshAllCards()
        {
            UpdateCardState("Forge", barForge, txtForgeStatus, btnRemoveForge);
            UpdateCardState("Fabric", barFabric, txtFabricStatus, btnRemoveFabric);
            UpdateCardState("NeoForge", barNeoForge, txtNeoForgeStatus, btnRemoveNeoForge);
            UpdateCardState("OptiFine", barOptiFine, txtOptiFineStatus, btnRemoveOptiFine);

            int count = _selectedLoaders.Count;
            if (count > 0)
            {
                txtDownloadSummary.Text = "将下载原版 Minecraft + " + count + " 个加载器";
                dotStatus.Fill = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
            }
            else
            {
                txtDownloadSummary.Text = "请选择需要添加的加载器";
                dotStatus.Fill = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));
            }
        }

        private void UpdateCardState(string loaderKey, Border bar, TextBlock statusText, Button removeBtn)
        {
            // 已选：显示版本号并高亮
            if (_selectedLoaders.TryGetValue(loaderKey, out var versionObj) && versionObj != null)
            {
                string status = GetDisplayVersion(loaderKey, versionObj);
                if (statusText != null)
                {
                    statusText.Text = "已选: " + status;
                    statusText.Foreground = new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD));
                }
                if (_selectedColors.TryGetValue(loaderKey, out var selHex))
                    bar.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(selHex);
                if (removeBtn != null)
                    removeBtn.Visibility = Visibility.Visible;
                return;
            }

            // 未选：重置卡片背景和移除按钮（不改动 statusText 的文字/颜色，由 SetAllCardsFetching/UpdateCardAfterFetch 处理）
            if (bar != null)
                bar.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#2A2A2A");
            if (removeBtn != null)
                removeBtn.Visibility = Visibility.Collapsed;
        }

        private string GetDisplayVersion(string loaderKey, object versionObj)
        {
            switch (loaderKey)
            {
                case "Forge":
                    return (versionObj as ForgeInstaller.ForgeVersionInfo)?.ForgeVersion ?? "已选";
                case "Fabric":
                    return (versionObj as FabricVersionInfo)?.LoaderVersion ?? "已选";
                case "NeoForge":
                    return (versionObj as NeoForgeVersionInfo)?.NeoForgeVersion ?? "已选";
                case "OptiFine":
                    if (versionObj is OptiFineVersionInfo ov)
                        return ov.Type + " " + ov.Patch;
                    return "已选";
                default:
                    return "已选";
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("GameVersions");
        }

        private void GoToLoaderVersionPage(string loaderType)
        {
            string mcVersion = AppContext.SelectedGameVersion;
            if (string.IsNullOrEmpty(mcVersion))
            {
                MessageBox.Show("未选择游戏版本", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 还在获取中：点击不反应，也不弹“没有可用版本”的误提示
            if (_fetchInProgress.TryGetValue(loaderType, out var inProgress) && inProgress)
                return;

            if (!_hasAvailableVersions.TryGetValue(loaderType, out var has) || !has)
            {
                MessageBox.Show("当前游戏版本下暂无可用的 " + loaderType + " 加载器版本", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AppContext.SelectedLoaderType = loaderType;
            AppContext.NavigateTo("LoaderVersion");
        }

        private void ForgeCard_Click(object sender, RoutedEventArgs e)
        {
            GoToLoaderVersionPage("Forge");
        }

        private void FabricCard_Click(object sender, RoutedEventArgs e)
        {
            GoToLoaderVersionPage("Fabric");
        }

        private void NeoForgeCard_Click(object sender, RoutedEventArgs e)
        {
            GoToLoaderVersionPage("NeoForge");
        }

        private void OptiFineCard_Click(object sender, RoutedEventArgs e)
        {
            GoToLoaderVersionPage("OptiFine");
        }

        private void RemoveForge_Click(object sender, RoutedEventArgs e)
        {
            _selectedLoaders.Remove("Forge");
            RefreshAllCards();
        }

        private void RemoveFabric_Click(object sender, RoutedEventArgs e)
        {
            _selectedLoaders.Remove("Fabric");
            RefreshAllCards();
        }

        private void RemoveNeoForge_Click(object sender, RoutedEventArgs e)
        {
            _selectedLoaders.Remove("NeoForge");
            RefreshAllCards();
        }

        private void RemoveOptiFine_Click(object sender, RoutedEventArgs e)
        {
            _selectedLoaders.Remove("OptiFine");
            RefreshAllCards();
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            string mcVersion = AppContext.SelectedGameVersion;
            if (string.IsNullOrEmpty(mcVersion))
            {
                MessageBox.Show("未选择游戏版本", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                btnDownload.IsEnabled = false;
                pbDownload.Visibility = Visibility.Visible;
                pbDownload.Value = 0;
                dotStatus.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom("#2196F3");

                txtDownloadSummary.Text = "正在下载 Minecraft " + mcVersion + " ...";

                var versionInfo = new Core.VersionInfo
                {
                    Id = mcVersion,
                    Url = AppContext.SelectedGameVersionUrl ?? ""
                };

                var taskViewModel = new DownloadTaskViewModel(versionInfo, _mcPath);
                DownloadTaskManager.Instance.AddTask(taskViewModel);

                // 同步底部进度条和状态文本
                PropertyChangedEventHandler handler = (s, pe) =>
                {
                    // 下载事件可能来自后台线程，必须回到 UI 线程更新控件
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        string status = taskViewModel.Status;
                        double progress = taskViewModel.Progress;

                        if (pe.PropertyName == nameof(taskViewModel.Progress))
                        {
                            pbDownload.Value = progress;
                            if (!string.IsNullOrEmpty(status))
                            {
                                txtDownloadSummary.Text = $"{status}  {progress:F1}%";
                            }
                        }
                        else if (pe.PropertyName == nameof(taskViewModel.Status))
                        {
                            if (!string.IsNullOrEmpty(status))
                            {
                                txtDownloadSummary.Text = $"{status}  {progress:F1}%";
                            }
                        }
                    }));
                };
                taskViewModel.PropertyChanged += handler;

                await taskViewModel.StartDownloadAsync();

                if (taskViewModel.IsFailed)
                {
                    throw new Exception(taskViewModel.ErrorMessage ?? "下载失败");
                }

                pbDownload.Value = 100;

                int totalLoaders = _selectedLoaders.Count;
                int loaderStep = 0;

                if (_selectedLoaders.TryGetValue("Forge", out var forgeObj)
                    && forgeObj is ForgeInstaller.ForgeVersionInfo fv)
                {
                    loaderStep++;
                    txtDownloadSummary.Text = $"正在安装 Forge ({loaderStep}/{totalLoaders}) ...";
                    bool ok = await _forgeInstaller.InstallForgeAsync(
                        mcVersion, fv, CancellationToken.None);
                    if (!ok)
                        Logger.Error("Forge 安装失败");
                }

                if (_selectedLoaders.TryGetValue("Fabric", out var fabObj)
                    && fabObj is FabricVersionInfo fabv)
                {
                    loaderStep++;
                    txtDownloadSummary.Text = $"正在安装 Fabric ({loaderStep}/{totalLoaders}) ...";
                    bool ok = await _fabricInstaller.InstallFabricAsync(
                        mcVersion, fabv, CancellationToken.None);
                    if (!ok)
                        Logger.Error("Fabric 安装失败");
                }

                if (_selectedLoaders.TryGetValue("NeoForge", out var neoObj)
                    && neoObj is NeoForgeVersionInfo nfv)
                {
                    loaderStep++;
                    txtDownloadSummary.Text = $"正在安装 NeoForge ({loaderStep}/{totalLoaders}) ...";
                    bool ok = await _neoForgeInstaller.InstallNeoForgeAsync(
                        mcVersion, nfv, CancellationToken.None);
                    if (!ok)
                        Logger.Error("NeoForge 安装失败");
                }

                if (_selectedLoaders.TryGetValue("OptiFine", out var optiObj)
                    && optiObj is OptiFineVersionInfo ov)
                {
                    loaderStep++;
                    txtDownloadSummary.Text = $"正在安装 OptiFine ({loaderStep}/{totalLoaders}) ...";
                    bool ok = await _optiFineInstaller.InstallOptiFineAsync(
                        mcVersion, ov, CancellationToken.None);
                    if (!ok)
                        Logger.Error("OptiFine 安装失败");
                }

                try
                {
                    await VersionScanService.Instance.ScanAsync("下载完成");
                }
                catch (Exception ex)
                {
                    Logger.Warning("扫描版本失败: " + ex.Message);
                }

                string summary = "Minecraft " + mcVersion + " 下载完成。";
                if (_selectedLoaders.Count > 0)
                {
                    summary += " 已安装:" + string.Join(", ", _selectedLoaders.Keys);
                }

                txtDownloadSummary.Text = summary;
                dotStatus.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom("#4CAF50");

                _selectedLoaders.Clear();
                RefreshAllCards();
            }
            catch (Exception ex)
            {
                Logger.Error("[下载] 下载失败: " + ex.Message);
                txtDownloadSummary.Text = "下载失败: " + ex.Message;
                dotStatus.Fill = (SolidColorBrush)new BrushConverter().ConvertFrom("#F44336");
                pbDownload.Visibility = Visibility.Collapsed;
                MessageBox.Show("下载失败: " + ex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnDownload.IsEnabled = true;
            }
        }
    }
}
