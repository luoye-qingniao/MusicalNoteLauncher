using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class MainPage : UserControl
    {
        // Java 版服务
        private GameLauncher _gameLauncher;
        private JavaConfigManager _javaConfig;
        private VersionManager _versionManager;

        // 基岩版服务
        private BedrockEnhancedDownloadService _bedrockDownloadService;
        private BedrockOfflineLauncher _bedrockOfflineLauncher;
        private List<BedrockVersionInfo> _bedrockVersionList = new List<BedrockVersionInfo>();
        private bool _isBedrockMode;
        private bool _isSwitching;
        private bool _isInitialized;

        public MainPage()
        {
            string mcPath = AppContext.MinecraftPath;

            _javaConfig = new JavaConfigManager(mcPath);
            _gameLauncher = new GameLauncher(mcPath, _javaConfig);
            _versionManager = new VersionManager(mcPath);
            _bedrockDownloadService = new BedrockEnhancedDownloadService(mcPath);
            _bedrockOfflineLauncher = new BedrockOfflineLauncher(mcPath);

            _gameLauncher.LaunchStatusChanged += OnJavaLaunchStatus;
            _gameLauncher.LaunchCompleted += OnJavaLaunchCompleted;

            InitializeComponent();
            LoadSavedSettings();

            // 使用 Dispatcher.Loaded 优先级确保在控件加载后执行初始化
            Dispatcher.BeginInvoke(new Action(InitializePage),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>页面初始化 — 加载版本列表并选中默认 Java 版</summary>
        private void InitializePage()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            VersionScanService.Instance.ScanCompleted += OnVersionScanCompleted;
            rbJava.IsChecked = true;
            _ = SwitchToJavaAsync();
        }

        /// <summary>从 ConfigManager 加载已保存的设置到 UI</summary>
        private void LoadSavedSettings()
        {
            var config = AppContext.Config;
            txtJavaPath.Text = _javaConfig.GetJavaPath();
            txtGameDir.Text = AppContext.MinecraftPath;
            txtAccountUser.Text = AppContext.Username;
            sliderMemory.Value = config.MemorySize;
            txtMemoryValue.Text = $"{config.MemorySize} MB";
            UpdateLaunchPreview();
        }

        // ═══════════════════════════════════════════════════════════════
        // 版本扫描回调
        // ═══════════════════════════════════════════════════════════════

        private void OnVersionScanCompleted(VersionScanResult result)
        {
            Dispatcher.Invoke(() => LoadVersionsFromScanResult(result));
        }

        private void LoadVersionsFromScanResult(VersionScanResult result)
        {
            if (_isBedrockMode) return; // 基岩版使用独立的远程版本列表

            cmbGameVersion.ItemsSource = null;
            cmbGameVersion.DisplayMemberPath = null;
            cmbGameVersion.Items.Clear();

            var versions = result.JavaVersions;

            if (versions.Count == 0)
            {
                cmbGameVersion.Items.Add("无已安装版本");
                cmbGameVersion.SelectedIndex = 0;
                txtVersionStatus.Text = "未找到已安装的 Java 版本";
            }
            else
            {
                foreach (string v in versions)
                    cmbGameVersion.Items.Add(v);

                // 优先选中上次选择的版本
                string lastVersion = _javaConfig.GetSelectedMinecraftVersion();
                if (!string.IsNullOrEmpty(lastVersion) && versions.Contains(lastVersion))
                    cmbGameVersion.SelectedItem = lastVersion;
                else
                    cmbGameVersion.SelectedIndex = 0;

                txtVersionStatus.Text = $"{versions.Count} 个 Java 版本已安装";
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Java 版启动状态回调
        // ═══════════════════════════════════════════════════════════════

        private void OnJavaLaunchStatus(string status)
        {
            Dispatcher.Invoke(() => txtVersionStatus.Text = status);
        }

        private void OnJavaLaunchCompleted(bool success)
        {
            Dispatcher.Invoke(() =>
            {
                btnLaunch.IsEnabled = true;
                btnLaunch.Content = "♪ 启动游戏";

                if (success)
                {
                    txtVersionStatus.Text = "游戏运行中";
                    Window parentWindow = Window.GetWindow(this);
                    if (parentWindow != null)
                        parentWindow.WindowState = WindowState.Minimized;
                }
                else
                {
                    txtVersionStatus.Text = "启动失败，请检查日志";
                }
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // 下载版本按钮
        // ═══════════════════════════════════════════════════════════════

        private void BtnDownloadVersion_Click(object sender, RoutedEventArgs e)
        {
            if (_isBedrockMode)
                AppContext.NavigateTo("GameVersions");
            else
                AppContext.NavigateTo("GameVersions");
        }

        // ═══════════════════════════════════════════════════════════════
        // 启动按钮
        // ═══════════════════════════════════════════════════════════════

        private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (_isBedrockMode)
                await LaunchBedrockAsync();
            else
                await LaunchJavaAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // Java 版启动
        // ═══════════════════════════════════════════════════════════════

        private async Task LaunchJavaAsync()
        {
            string versionId = cmbGameVersion.SelectedItem as string;
            if (string.IsNullOrEmpty(versionId) || versionId == "无已安装版本")
            {
                MessageBox.Show("请先选择或下载一个 Java 版游戏版本", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_versionManager.IsJavaVersionValid(versionId))
            {
                MessageBox.Show($"版本 {versionId} 无效或已损坏，请重新下载", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                VersionScanService.Instance.ClearCache();
                _ = VersionScanService.Instance.ScanAsync($"校验失败后刷新:{versionId}");
                return;
            }

            string username = txtAccountUser.Text?.Trim();
            if (string.IsNullOrWhiteSpace(username)) username = "Player";

            int minMemory = (int)sliderMemory.Value;
            int maxMemory = (int)sliderMemory.Value;

            btnLaunch.IsEnabled = false;
            btnLaunch.Content = "启动中...";

            try
            {
                bool success = await _gameLauncher.LaunchGameAsync(
                    versionId, username, minMemory, maxMemory,
                    additionalArgs: "", offlineMode: AppContext.IsOfflineMode);

                if (!success)
                {
                    btnLaunch.IsEnabled = true;
                    btnLaunch.Content = "♪ 启动游戏";
                }
            }
            catch (Exception ex)
            {
                btnLaunch.IsEnabled = true;
                btnLaunch.Content = "♪ 启动游戏";
                txtVersionStatus.Text = $"启动失败: {ex.Message}";
                Logger.Error("Java版启动异常: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 游戏类型切换 (Java / 基岩) - 使用 Click 事件，比 Checked 更可靠
        // ═══════════════════════════════════════════════════════════════

        private async void RbJava_Click(object sender, RoutedEventArgs e)
        {
            if (_isSwitching || !_isBedrockMode) return;
            await SwitchToJavaAsync();
        }

        private async void RbBedrock_Click(object sender, RoutedEventArgs e)
        {
            if (_isSwitching || _isBedrockMode) return;
            await SwitchToBedrockAsync();
        }

        private async Task SwitchToJavaAsync()
        {
            _isSwitching = true;
            _isBedrockMode = false;

            panelJavaVersion.Visibility = Visibility.Visible;
            panelBedrockUser.Visibility = Visibility.Collapsed;
            lblVersionSelect.Text = "选择版本";
            btnDownloadVersion.Content = "下载版本";
            UpdateLaunchPreview();

            var result = await VersionScanService.Instance.ScanAsync("切换到Java版");
            LoadVersionsFromScanResult(result);

            _isSwitching = false;
        }

        private async Task SwitchToBedrockAsync()
        {
            _isSwitching = true;
            _isBedrockMode = true;

            panelJavaVersion.Visibility = Visibility.Collapsed;
            panelBedrockUser.Visibility = Visibility.Visible;
            lblVersionSelect.Text = "选择基岩版版本";
            btnDownloadVersion.Content = "下载基岩版";
            txtLaunchPreview.Text = "基岩版离线启动（不修改游戏文件）";

            await LoadBedrockVersions();
            _isSwitching = false;
        }

        // ═══════════════════════════════════════════════════════════════
        // 版本刷新按钮
        // ═══════════════════════════════════════════════════════════════

        private void BtnRefreshVersions_Click(object sender, RoutedEventArgs e)
        {
            if (_isBedrockMode)
                _ = LoadBedrockVersions();
            else
                _ = VersionScanService.Instance.ScanAsync("用户刷新版本");
        }

        // ═══════════════════════════════════════════════════════════════
        // 版本选择变更
        // ═══════════════════════════════════════════════════════════════

        private void CmbGameVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isBedrockMode) return;

            string versionId = cmbGameVersion.SelectedItem as string;
            if (!string.IsNullOrEmpty(versionId) && versionId != "无已安装版本")
            {
                txtVersionStatus.Text = $"已选择: {versionId}";
                _javaConfig.SetSelectedMinecraftVersion(versionId);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 内存滑块
        // ═══════════════════════════════════════════════════════════════

        private void SliderMemory_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int memory = (int)sliderMemory.Value;
            txtMemoryValue.Text = $"{memory} MB";
            UpdateLaunchPreview();
        }

        private void UpdateLaunchPreview()
        {
            int memory = (int)sliderMemory.Value;
            if (_isBedrockMode)
                txtLaunchPreview.Text = "基岩版离线启动（不修改游戏文件）";
            else
                txtLaunchPreview.Text = $"-Xmx{memory}M -Xms{memory}M ...";
        }

        // ═══════════════════════════════════════════════════════════════
        // 自动检测 Java
        // ═══════════════════════════════════════════════════════════════

        private void BtnAutoDetectJava_Click(object sender, RoutedEventArgs e)
        {
            txtJavaStatus.Text = "正在检测 Java 环境...";

            var detectedList = _javaConfig.DetectInstalledJava();
            if (detectedList.Count == 0)
            {
                txtJavaStatus.Text = "未检测到 Java，请手动设置";
                return;
            }

            // 优先选择版本最高的 Java
            var best = detectedList.OrderByDescending(j => j.MajorVersion).First();
            _javaConfig.SetAutoConfig(best);
            txtJavaPath.Text = best.Path;
            txtJavaStatus.Text = $"已设置: Java {best.MajorVersion} (最高内存 {best.MaxMemoryMb}MB)";
        }

        // ═══════════════════════════════════════════════════════════════
        // 浏览选择 Java
        // ═══════════════════════════════════════════════════════════════

        private void BtnBrowseJava_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择 Java 可执行文件",
                Filter = "Java 可执行文件|java.exe;javaw.exe|所有文件|*.*",
                DefaultExt = "java.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _javaConfig.SetJavaPath(dialog.FileName);
                    txtJavaPath.Text = _javaConfig.GetJavaPath();
                    txtJavaStatus.Text = "Java 路径已更新";
                }
                catch (Exception ex)
                {
                    txtJavaStatus.Text = $"设置失败: {ex.Message}";
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 选择游戏目录
        // ═══════════════════════════════════════════════════════════════

        private void BtnSelectGameDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择 Minecraft 游戏目录",
                SelectedPath = AppContext.MinecraftPath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string newPath = dialog.SelectedPath;
                AppContext.MinecraftPath = newPath;
                txtGameDir.Text = newPath;
                txtVersionStatus.Text = "游戏目录已更改，请刷新版本列表";

                // 重新初始化服务
                _javaConfig = new JavaConfigManager(newPath);
                _gameLauncher = new GameLauncher(newPath, _javaConfig);
                _versionManager = new VersionManager(newPath);
                _bedrockDownloadService = new BedrockEnhancedDownloadService(newPath);
                _bedrockOfflineLauncher = new BedrockOfflineLauncher(newPath);

                _gameLauncher.LaunchStatusChanged += OnJavaLaunchStatus;
                _gameLauncher.LaunchCompleted += OnJavaLaunchCompleted;

                VersionScanService.Instance.ClearCache();
                _ = VersionScanService.Instance.ScanAsync("游戏目录变更");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 保存设置
        // ═══════════════════════════════════════════════════════════════

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var config = AppContext.Config;

            string username = txtAccountUser.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(username))
            {
                config.Username = username;
                AppContext.Username = username;
                txtCurrentUser.Text = username;
            }

            config.MemorySize = (int)sliderMemory.Value;
            config.GameDirectory = txtGameDir.Text;

            _javaConfig.SaveConfig();
            config.Save();

            txtVersionStatus.Text = "设置已保存";
        }

        // ═══════════════════════════════════════════════════════════════
        // 退出登录
        // ═══════════════════════════════════════════════════════════════

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要退出登录吗？\n将返回登录界面。", "退出确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                AppContext.NavigateTo("Login");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 基岩版版本加载
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadBedrockVersions()
        {
            try
            {
                cmbGameVersion.ItemsSource = null;
                cmbGameVersion.DisplayMemberPath = null;
                cmbGameVersion.Items.Clear();
                cmbGameVersion.DisplayMemberPath = "Id";

                txtVersionStatus.Text = "正在获取基岩版版本列表...";

                _bedrockVersionList = await _bedrockDownloadService.GetRemoteVersionsAsync();
                cmbGameVersion.ItemsSource = _bedrockVersionList;

                var installed = _bedrockVersionList.FirstOrDefault(v => v.IsDownloaded);
                if (installed != null)
                    cmbGameVersion.SelectedItem = installed;
                else if (_bedrockVersionList.Count > 0)
                    cmbGameVersion.SelectedIndex = 0;

                txtVersionStatus.Text = $"{_bedrockVersionList.Count} 个基岩版版本可用";
            }
            catch (Exception ex)
            {
                txtVersionStatus.Text = "获取基岩版版本失败";
                Logger.Error("MainPage加载基岩版版本失败: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 基岩版启动
        // ═══════════════════════════════════════════════════════════════

        private async Task LaunchBedrockAsync()
        {
            var selected = cmbGameVersion.SelectedItem as BedrockVersionInfo;
            if (selected == null)
            {
                MessageBox.Show("请先选择基岩版版本", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!selected.IsDownloaded)
            {
                MessageBox.Show("该基岩版版本尚未下载，请先前往 游戏版本 → 基岩版 下载", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string username = txtBedrockUsername.Text?.Trim();
                if (string.IsNullOrWhiteSpace(username)) username = "Player";

                btnLaunch.IsEnabled = false;
                btnLaunch.Content = "启动中...";

                _bedrockOfflineLauncher.LaunchStatusChanged += OnBedrockLaunchStatus;
                _bedrockOfflineLauncher.LaunchCompleted += OnBedrockLaunchCompleted;

                await _bedrockOfflineLauncher.LaunchOfflineAsync(selected.Id, username);
            }
            catch (Exception ex)
            {
                btnLaunch.IsEnabled = true;
                btnLaunch.Content = "♪ 启动游戏";
                txtVersionStatus.Text = $"启动失败: {ex.Message}";
                Logger.Error("基岩版启动异常: " + ex.Message);
            }
        }

        private void OnBedrockLaunchStatus(string status)
        {
            Dispatcher.Invoke(() => txtVersionStatus.Text = status);
        }

        private void OnBedrockLaunchCompleted(bool success)
        {
            Dispatcher.Invoke(() =>
            {
                btnLaunch.IsEnabled = true;
                btnLaunch.Content = "♪ 启动游戏";
                _bedrockOfflineLauncher.LaunchStatusChanged -= OnBedrockLaunchStatus;
                _bedrockOfflineLauncher.LaunchCompleted -= OnBedrockLaunchCompleted;

                if (success)
                    txtVersionStatus.Text = "游戏已启动（离线模式）";
                else
                    txtVersionStatus.Text = "启动失败，请检查日志";
            });
        }
    }
}
