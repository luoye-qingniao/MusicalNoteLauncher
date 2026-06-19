using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Windows;

namespace MusicalNoteLauncher.Pages
{
    public partial class LoaderVersionPage : UserControl
    {
        private readonly string _minecraftPath;
        private ForgeInstaller _forgeInstaller;
        private FabricInstaller _fabricInstaller;
        private NeoForgeInstaller _neoForgeInstaller;
        private OptiFineInstaller _optiFineInstaller;
        private CancellationTokenSource _cts;

        private string _selectedMcVersion;
        private string _selectedLoaderType;
        private object _selectedForgeVersion;
        private object _selectedFabricVersion;
        private object _selectedNeoForgeVersion;
        private object _selectedOptiFineVersion;
        private bool _isBusy = false;

        private ObservableCollection<ForgeInstaller.ForgeVersionInfo> _forgeVersions = new();
        private ObservableCollection<FabricVersionInfo> _fabricVersions = new();
        private ObservableCollection<NeoForgeVersionInfo> _neoForgeVersions = new();
        private ObservableCollection<OptiFineVersionInfo> _optiFineVersions = new();

        /// <summary>
        /// 当前加载器对应的图标（供 XAML DataTemplate 中 <Image Source="..."> 绑定）
        /// </summary>
        public ImageSource CurrentLoaderIcon
        {
            get { return (ImageSource)GetValue(CurrentLoaderIconProperty); }
            set { SetValue(CurrentLoaderIconProperty, value); }
        }

        public static readonly DependencyProperty CurrentLoaderIconProperty =
            DependencyProperty.Register(
                nameof(CurrentLoaderIcon),
                typeof(ImageSource),
                typeof(LoaderVersionPage),
                new PropertyMetadata(null));

        /// <summary>
        /// 根据加载器类型加载对应的本地 PNG 图标。失败时返回 null。
        /// </summary>
        private static ImageSource LoadLoaderIcon(string loaderType)
        {
            if (string.IsNullOrEmpty(loaderType)) return null;

            string fileName = null;
            if (string.Equals(loaderType, "Forge", StringComparison.Ordinal)) fileName = "Anvil.png";
            else if (string.Equals(loaderType, "Fabric", StringComparison.Ordinal)) fileName = "Fabric.png";
            else if (string.Equals(loaderType, "NeoForge", StringComparison.Ordinal)) fileName = "NeoForge.png";
            else if (string.Equals(loaderType, "OptiFine", StringComparison.Ordinal)) fileName = "GrassPath.png";

            if (fileName == null) return null;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri("pack://application:,,,/提取的模组加载器图标/" + fileName, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                // pack:// 方式失败时，尝试从应用基目录读取
                try
                {
                    string filePath = Path.Combine(System.AppContext.BaseDirectory, "提取的模组加载器图标", fileName);
                    if (!File.Exists(filePath)) return null;

                    var bmp = new BitmapImage();
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = fs;
                        bmp.EndInit();
                    }
                    bmp.Freeze();
                    return bmp;
                }
                catch
                {
                    return null;
                }
            }
        }

        public LoaderVersionPage()
        {
            InitializeComponent();
            _minecraftPath = AppContext.MinecraftPath;
            _forgeInstaller = new ForgeInstaller(_minecraftPath);
            _fabricInstaller = new FabricInstaller(_minecraftPath);
            _neoForgeInstaller = new NeoForgeInstaller(_minecraftPath);
            _optiFineInstaller = new OptiFineInstaller(_minecraftPath);
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _selectedMcVersion = AppContext.SelectedGameVersion;
            _selectedLoaderType = AppContext.SelectedLoaderType;

            if (string.IsNullOrEmpty(_selectedMcVersion))
            {
                MessageBox.Show("未选择游戏版本", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                AppContext.NavigateTo("GameVersions");
                return;
            }

            if (string.IsNullOrEmpty(_selectedLoaderType))
            {
                MessageBox.Show("未选择加载器类型", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                AppContext.NavigateTo("LoaderSelection");
                return;
            }

            // 设置当前加载器的图标（供列表中每个版本项显示）
            CurrentLoaderIcon = LoadLoaderIcon(_selectedLoaderType);

            if (_selectedLoaderType == "Vanilla")
            {
                SetupVanillaView();
            }
            else
            {
                await LoadLoaderVersionsAsync();
            }
        }

        private void SetupVanillaView()
        {
            pnlLoading.Visibility = Visibility.Collapsed;
            pnlVanilla.Visibility = Visibility.Visible;
            txtTitle.Text = "原版下载";
            txtInfo.Text = $"游戏版本: {_selectedMcVersion}\n将下载纯净原版 Minecraft，不安装任何模组加载器";
            txtVanillaDesc.Text = $"Minecraft {_selectedMcVersion}";
        }

        private async Task LoadLoaderVersionsAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            pnlVanilla.Visibility = Visibility.Collapsed;
            svVersions.Visibility = Visibility.Collapsed;

            try
            {
                if (_selectedLoaderType == "Forge")
                {
                    txtTitle.Text = $"Forge 版本选择 - MC {_selectedMcVersion}";
                    txtInfo.Text = $"游戏版本: {_selectedMcVersion}\n正在获取 Forge 加载器版本列表...";

                    var versions = AppContext.ForgeVersions != null && AppContext.ForgeVersions.Count > 0
                        ? AppContext.ForgeVersions
                        : await _forgeInstaller.GetForgeVersionsAsync(_selectedMcVersion);
                    if (versions.Count == 0)
                    {
                        await ShowNotSupportedAsync($"当前游戏版本 {_selectedMcVersion} 暂无 Forge 可用版本");
                        return;
                    }

                    var recommended = versions.FirstOrDefault(v => v.IsRecommended)
                        ?? versions.FirstOrDefault(v => !v.ForgeVersion.Contains("-beta") && !v.ForgeVersion.Contains("-alpha"))
                        ?? versions.FirstOrDefault();

                    if (recommended != null)
                    {
                        recommended.IsRecommended = true;
                        _selectedForgeVersion = recommended;
                    }

                    _forgeVersions = new ObservableCollection<ForgeInstaller.ForgeVersionInfo>(versions);
                    icRecommendedVersions.ItemsSource = _forgeVersions.Where(v => v.IsRecommended).ToList();
                    icOtherVersions.ItemsSource = _forgeVersions.Where(v => !v.IsRecommended).ToList();
                }
                else if (_selectedLoaderType == "Fabric")
                {
                    txtTitle.Text = $"Fabric 版本选择 - MC {_selectedMcVersion}";
                    txtInfo.Text = $"游戏版本: {_selectedMcVersion}\n正在获取 Fabric 加载器版本列表...";

                    var versions = AppContext.FabricVersions != null && AppContext.FabricVersions.Count > 0
                        ? AppContext.FabricVersions
                        : await _fabricInstaller.GetFabricVersionsAsync(_selectedMcVersion);
                    if (versions.Count == 0)
                    {
                        await ShowNotSupportedAsync($"当前游戏版本 {_selectedMcVersion} 暂无 Fabric 可用版本");
                        return;
                    }

                    var recommended = versions.FirstOrDefault(v => !string.IsNullOrEmpty(v.LoaderVersion) && !v.LoaderVersion.Contains("-"))
                        ?? versions.FirstOrDefault();
                    if (recommended != null)
                    {
                        recommended.IsRecommended = true;
                        _selectedFabricVersion = recommended;
                    }

                    _fabricVersions = new ObservableCollection<FabricVersionInfo>(versions);
                    icRecommendedVersions.ItemsSource = _fabricVersions.Where(v => v.IsRecommended).ToList();
                    icOtherVersions.ItemsSource = _fabricVersions.Where(v => !v.IsRecommended).ToList();
                }
                else if (_selectedLoaderType == "NeoForge")
                {
                    txtTitle.Text = $"NeoForge 版本选择 - MC {_selectedMcVersion}";
                    txtInfo.Text = $"游戏版本: {_selectedMcVersion}\n正在获取 NeoForge 加载器版本列表...";

                    var versions = AppContext.NeoForgeVersions != null && AppContext.NeoForgeVersions.Count > 0
                        ? AppContext.NeoForgeVersions
                        : await _neoForgeInstaller.GetNeoForgeVersionsAsync(_selectedMcVersion);
                    if (versions.Count == 0)
                    {
                        await ShowNotSupportedAsync($"当前游戏版本 {_selectedMcVersion} 暂无 NeoForge 可用版本");
                        return;
                    }

                    var recommended = versions.FirstOrDefault(v => v.IsRecommended)
                        ?? versions.FirstOrDefault(v => !v.NeoForgeVersion.Contains("-beta") && !v.NeoForgeVersion.Contains("-alpha"))
                        ?? versions.FirstOrDefault();

                    if (recommended != null)
                    {
                        recommended.IsRecommended = true;
                        _selectedNeoForgeVersion = recommended;
                    }

                    _neoForgeVersions = new ObservableCollection<NeoForgeVersionInfo>(versions);
                    icRecommendedVersions.ItemsSource = _neoForgeVersions.Where(v => v.IsRecommended).ToList();
                    icOtherVersions.ItemsSource = _neoForgeVersions.Where(v => !v.IsRecommended).ToList();
                }
                else if (_selectedLoaderType == "OptiFine")
                {
                    txtTitle.Text = $"OptiFine 版本选择 - MC {_selectedMcVersion}";
                    txtInfo.Text = $"游戏版本: {_selectedMcVersion}\n正在获取 OptiFine 版本列表...";

                    var versions = AppContext.OptiFineVersions != null && AppContext.OptiFineVersions.Count > 0
                        ? AppContext.OptiFineVersions
                        : await _optiFineInstaller.GetOptiFineVersionsAsync(_selectedMcVersion);
                    if (versions.Count == 0)
                    {
                        await ShowNotSupportedAsync($"当前游戏版本 {_selectedMcVersion} 暂无 OptiFine 可用版本");
                        return;
                    }

                    var recommended = versions.FirstOrDefault(v => v.Type.Equals("HD_U", StringComparison.OrdinalIgnoreCase) || v.Type.Equals("pre", StringComparison.OrdinalIgnoreCase))
                        ?? versions.FirstOrDefault();
                    if (recommended != null)
                    {
                        recommended.IsRecommended = true;
                        _selectedOptiFineVersion = recommended;
                    }

                    _optiFineVersions = new ObservableCollection<OptiFineVersionInfo>(versions);
                    icRecommendedVersions.ItemsSource = _optiFineVersions.Where(v => v.IsRecommended).ToList();
                    icOtherVersions.ItemsSource = _optiFineVersions.Where(v => !v.IsRecommended).ToList();
                }
                else
                {
                    await ShowNotSupportedAsync($"加载器类型 {_selectedLoaderType} 暂不支持");
                    return;
                }

                pnlLoading.Visibility = Visibility.Collapsed;
                svVersions.Visibility = Visibility.Visible;
                int total = (icRecommendedVersions.ItemsSource as System.Collections.IList)?.Count ?? 0
                          + (icOtherVersions.ItemsSource as System.Collections.IList)?.Count ?? 0;
                txtInfo.Text = $"游戏版本: {_selectedMcVersion}\n加载器类型: {_selectedLoaderType}\n共 {total} 个可用版本";
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                txtInfo.Text = $"获取版本列表失败: {ex.Message}";
                MessageBox.Show($"获取版本列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Task ShowNotSupportedAsync(string message)
        {
            pnlLoading.Visibility = Visibility.Collapsed;
            svVersions.Visibility = Visibility.Collapsed;
            pnlVanilla.Visibility = Visibility.Visible;
            txtVanillaDesc.Text = message;
            txtTitle.Text = "暂不支持";
            txtInfo.Text = $"游戏版本: {_selectedMcVersion}\n加载器类型: {_selectedLoaderType}";
            return Task.CompletedTask;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("LoaderSelection");
        }

        private void LbVersions_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isBusy) return;

            var clicked = e.OriginalSource as DependencyObject;
            object dataItem = null;
            while (clicked != null && dataItem == null)
            {
                if (clicked is FrameworkElement fe && fe.DataContext != null)
                {
                    string typeName = fe.DataContext.GetType().Name;
                    if (typeName.Contains("Version") || typeName.Contains("ForgeVersionInfo"))
                    {
                        dataItem = fe.DataContext;
                    }
                }
                clicked = VisualTreeHelper.GetParent(clicked);
            }

            if (dataItem == null) return;

            // 保存选择，回到 LoaderSelectionPage，由该页面统一发起下载
            if (_selectedLoaderType == "Forge")
            {
                _selectedForgeVersion = dataItem;
                AppContext.SelectedLoaderType = "Forge";
                AppContext.SelectedLoaderVersion = _selectedForgeVersion;
            }
            else if (_selectedLoaderType == "Fabric")
            {
                _selectedFabricVersion = dataItem;
                AppContext.SelectedLoaderType = "Fabric";
                AppContext.SelectedLoaderVersion = _selectedFabricVersion;
            }
            else if (_selectedLoaderType == "NeoForge")
            {
                _selectedNeoForgeVersion = dataItem;
                AppContext.SelectedLoaderType = "NeoForge";
                AppContext.SelectedLoaderVersion = _selectedNeoForgeVersion;
            }
            else if (_selectedLoaderType == "OptiFine")
            {
                _selectedOptiFineVersion = dataItem;
                AppContext.SelectedLoaderType = "OptiFine";
                AppContext.SelectedLoaderVersion = _selectedOptiFineVersion;
            }

            AppContext.NavigateTo("LoaderSelection");
        }

        private void LbVersions_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (svVersions == null) return;
            e.Handled = true;
            double offset = svVersions.VerticalOffset - (e.Delta / 3.0);
            svVersions.ScrollToVerticalOffset(Math.Max(0, Math.Min(offset, svVersions.ScrollableHeight)));
        }

        private async void VanillaDownload_Click(object sender, MouseButtonEventArgs e)
        {
            if (_isBusy) return;
            await StartVanillaDownloadAsync();
        }

        private async Task StartVanillaDownloadAsync()
        {
            _isBusy = true;
            try
            {
                var downloader = new VersionDownloader(_minecraftPath);
                downloader.Closed += async (s, args) =>
                {
                    await VersionScanService.Instance.ScanAsync("原版下载完成");
                };
                downloader.ShowDialog();

                MessageBox.Show($"Minecraft {_selectedMcVersion} 原版下载完成！", "下载完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async Task StartForgeDownloadAsync()
        {
            _isBusy = true;
            _cts = new CancellationTokenSource();

            try
            {
                var selected = _selectedForgeVersion as ForgeInstaller.ForgeVersionInfo;
                var forgeVer = selected;

                if (forgeVer == null)
                    throw new Exception("未选择有效的 Forge 版本");

                var downloader = new VersionDownloader(_minecraftPath);
                downloader.ShowDialog();

                string mcVersionDir = Path.Combine(_minecraftPath, "versions", _selectedMcVersion);
                string mcJar = Path.Combine(mcVersionDir, $"{_selectedMcVersion}.jar");
                string mcJson = Path.Combine(mcVersionDir, $"{_selectedMcVersion}.json");
                if (!File.Exists(mcJar) || !File.Exists(mcJson))
                    throw new Exception("Minecraft 原版下载失败或未完成");

                bool forgeResult = await _forgeInstaller.InstallForgeAsync(_selectedMcVersion, forgeVer, _cts.Token);
                if (!forgeResult)
                    throw new Exception("Forge 安装失败");

                await VersionScanService.Instance.ScanAsync("Forge下载完成");

                string forgeVerId = $"{_selectedMcVersion}-forge{forgeVer.ForgeVersion}";
                MessageBox.Show($"Minecraft {_selectedMcVersion} + Forge {forgeVer.ForgeVersion} 下载安装完成！\n" +
                    $"安装版本: {forgeVerId}", "下载完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async Task StartFabricDownloadAsync()
        {
            _isBusy = true;
            _cts = new CancellationTokenSource();

            try
            {
                var fabricVer = _selectedFabricVersion as FabricVersionInfo;

                if (fabricVer == null)
                    throw new Exception("未选择有效的 Fabric 版本");

                var downloader = new VersionDownloader(_minecraftPath);
                downloader.ShowDialog();

                string mcVersionDir = Path.Combine(_minecraftPath, "versions", _selectedMcVersion);
                string mcJar = Path.Combine(mcVersionDir, $"{_selectedMcVersion}.jar");
                string mcJson = Path.Combine(mcVersionDir, $"{_selectedMcVersion}.json");
                if (!File.Exists(mcJar) || !File.Exists(mcJson))
                    throw new Exception("Minecraft 原版下载失败或未完成");

                bool fabricResult = await _fabricInstaller.InstallFabricAsync(_selectedMcVersion, fabricVer, _cts.Token);
                if (!fabricResult)
                    throw new Exception("Fabric 安装失败");

                await VersionScanService.Instance.ScanAsync("Fabric下载完成");

                MessageBox.Show($"Minecraft {_selectedMcVersion} + Fabric {fabricVer.LoaderVersion} 下载安装完成！\n" +
                    $"安装版本: {fabricVer.VersionId}", "下载完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async Task StartNeoForgeDownloadAsync()
        {
            _isBusy = true;
            _cts = new CancellationTokenSource();

            try
            {
                var neoForgeVer = _selectedNeoForgeVersion as NeoForgeVersionInfo;

                if (neoForgeVer == null)
                    throw new Exception("未选择有效的 NeoForge 版本");

                var downloader = new VersionDownloader(_minecraftPath);
                downloader.ShowDialog();

                string mcVersionDir = Path.Combine(_minecraftPath, "versions", _selectedMcVersion);
                string mcJar = Path.Combine(mcVersionDir, $"{_selectedMcVersion}.jar");
                string mcJson = Path.Combine(mcVersionDir, $"{_selectedMcVersion}.json");
                if (!File.Exists(mcJar) || !File.Exists(mcJson))
                    throw new Exception("Minecraft 原版下载失败或未完成");

                bool neoForgeResult = await _neoForgeInstaller.InstallNeoForgeAsync(_selectedMcVersion, neoForgeVer, _cts.Token);
                if (!neoForgeResult)
                    throw new Exception("NeoForge 安装失败");

                await VersionScanService.Instance.ScanAsync("NeoForge下载完成");

                MessageBox.Show($"Minecraft {_selectedMcVersion} + NeoForge {neoForgeVer.NeoForgeVersion} 下载安装完成！\n" +
                    $"安装版本: {neoForgeVer.VersionId}", "下载完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async Task StartOptiFineDownloadAsync()
        {
            _isBusy = true;
            _cts = new CancellationTokenSource();

            try
            {
                var optiFineVer = _selectedOptiFineVersion as OptiFineVersionInfo;

                if (optiFineVer == null)
                    throw new Exception("未选择有效的 OptiFine 版本");

                var downloader = new VersionDownloader(_minecraftPath);
                downloader.ShowDialog();

                string mcVersionDir = Path.Combine(_minecraftPath, "versions", _selectedMcVersion);
                string mcJar = Path.Combine(mcVersionDir, $"{_selectedMcVersion}.jar");
                string mcJson = Path.Combine(mcVersionDir, $"{_selectedMcVersion}.json");
                if (!File.Exists(mcJar) || !File.Exists(mcJson))
                    throw new Exception("Minecraft 原版下载失败或未完成");

                bool optiFineResult = await _optiFineInstaller.InstallOptiFineAsync(_selectedMcVersion, optiFineVer, _cts.Token);
                if (!optiFineResult)
                    throw new Exception("OptiFine 安装失败");

                await VersionScanService.Instance.ScanAsync("OptiFine下载完成");

                MessageBox.Show($"Minecraft {_selectedMcVersion} + OptiFine {optiFineVer.Type} {optiFineVer.Patch} 已准备就绪！\n" +
                    $"安装版本: {optiFineVer.VersionId}", "下载完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
