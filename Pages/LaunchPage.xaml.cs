using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class LaunchPage : Page
    {
        private readonly GameLauncher _gameLauncher;
        private readonly BedrockLauncher _bedrockLauncher;
        private readonly string _minecraftPath;
        private readonly JavaConfigManager _javaConfig;
        private readonly VersionManager _versionManager;
        private VersionType _currentVersionType = VersionType.Java;

        public LaunchPage(string minecraftPath)
        {
            InitializeComponent();
            _minecraftPath = minecraftPath;
            _javaConfig = new JavaConfigManager(minecraftPath);
            _gameLauncher = new GameLauncher(minecraftPath, _javaConfig);
            _bedrockLauncher = new BedrockLauncher(minecraftPath);
            _versionManager = new VersionManager(minecraftPath);

            _gameLauncher.LaunchStatusChanged += OnLaunchStatusChanged;
            _gameLauncher.LaunchLogReceived += OnLaunchLogReceived;
            _gameLauncher.LaunchCompleted += OnLaunchCompleted;

            _bedrockLauncher.LaunchStatusChanged += OnLaunchStatusChanged;
            _bedrockLauncher.LaunchLogReceived += OnLaunchLogReceived;
            _bedrockLauncher.LaunchCompleted += OnLaunchCompleted;

            VersionScanService.Instance.ScanCompleted += OnVersionScanCompleted;

            SetJavaVersionActive();
            Logger.Info("[启动页面] 初始化版本扫描服务");
            VersionScanService.Instance.ClearCache();
            Logger.Info("[启动页面] 缓存已清除");
            _ = VersionScanService.Instance.ScanAsync("启动器初始化");
        }

        private void SetJavaVersionActive()
        {
            btnJavaVersion.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
            btnJavaVersion.Foreground = Brushes.White;
            btnBedrockVersion.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            btnBedrockVersion.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            _currentVersionType = VersionType.Java;
        }

        private void SetBedrockVersionActive()
        {
            btnBedrockVersion.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
            btnBedrockVersion.Foreground = Brushes.White;
            btnJavaVersion.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            btnJavaVersion.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            _currentVersionType = VersionType.Bedrock;
        }

        private void BtnVersionType_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string tag = button.Tag?.ToString();

                if (tag == "Java")
                {
                    SetJavaVersionActive();
                }
                else if (tag == "Bedrock")
                {
                    SetBedrockVersionActive();
                }

                _ = VersionScanService.Instance.ScanAsync($"切换{tag}版本");
            }
        }

        private void OnVersionScanCompleted(VersionScanResult result)
        {
            Dispatcher.Invoke(() =>
            {
                LoadVersionsFromScanResult(result);
            });
        }

        private void LoadVersionsFromScanResult(VersionScanResult result)
        {
            Logger.Info($"[版本加载] 开始加载版本列表，当前类型: {_currentVersionType}");
            
            cboVersions.Items.Clear();

            List<string> versions = _currentVersionType == VersionType.Java
                ? result.JavaVersions
                : result.BedrockVersions;

            Logger.Info($"[版本加载] 扫描结果包含 {versions.Count} 个版本: {string.Join(", ", versions)}");

            if (versions.Count == 0)
            {
                cboVersions.Items.Add("无已安装版本");
                btnLaunch.IsEnabled = false;
                Logger.Info("[版本加载] 未发现已安装版本");
            }
            else
            {
                foreach (string version in versions)
                {
                    cboVersions.Items.Add(version);
                    Logger.Info($"[版本加载] 添加版本: {version}");
                }
                btnLaunch.IsEnabled = true;
            }

            if (cboVersions.Items.Count > 0)
            {
                cboVersions.SelectedIndex = 0;
            }
            
            Logger.Info($"[版本加载] 版本列表加载完成，共 {cboVersions.Items.Count} 个项目");
        }
        private void CboVersions_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _ = VersionScanService.Instance.ScanAsync("点击下拉框刷新");
        }

        private async void btnLaunch_Click(object sender, RoutedEventArgs e)
        {
            string versionId = cboVersions.SelectedItem as string;
            if (string.IsNullOrEmpty(versionId) || versionId == "无已安装版本")
            {
                return;
            }

            await VersionScanService.Instance.ScanAsync($"启动前校验:{versionId}");

            if (!ValidateVersionBeforeLaunch(versionId))
            {
                return;
            }

            if (_currentVersionType == VersionType.Java)
            {
                await LaunchJavaGame(versionId);
            }
            else
            {
                await LaunchBedrockGame(versionId);
            }
        }

        private bool ValidateVersionBeforeLaunch(string versionId)
        {
            bool isValid = _versionManager.IsVersionInstalled(versionId, _currentVersionType);

            if (!isValid)
            {
                Logger.Warning($"[启动校验] 版本 {versionId} 不存在或已损坏，自动从下拉框移除");
                Dispatcher.Invoke(() =>
                {
                    string itemToRemove = null;
                    foreach (object obj in cboVersions.Items)
                    {
                        if (obj is string s && s == versionId)
                        {
                            itemToRemove = s;
                            break;
                        }
                    }
                    if (itemToRemove != null)
                    {
                        cboVersions.Items.Remove(itemToRemove);
                    }
                    if (cboVersions.Items.Count == 0)
                    {
                        cboVersions.Items.Add("无已安装版本");
                        btnLaunch.IsEnabled = false;
                    }
                    else
                    {
                        cboVersions.SelectedIndex = 0;
                    }
                });
                return false;
            }

            Logger.Info($"[启动校验] 版本 {versionId} 校验通过");
            return true;
        }

        private async System.Threading.Tasks.Task LaunchJavaGame(string versionId)
        {
            string username = txtUsername.Text.Trim();
            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtMinMemory.Text, out int minMemory))
            {
                MessageBox.Show("请输入有效的最小内存值", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtMaxMemory.Text, out int maxMemory))
            {
                MessageBox.Show("请输入有效的最大内存值", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (minMemory > maxMemory)
            {
                MessageBox.Show("最小内存不能大于最大内存", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnLaunch.IsEnabled = false;
            txtLog.Clear();
            txtStatus.Text = "准备启动...";

            bool success = await _gameLauncher.LaunchGameAsync(
                versionId,
                username,
                minMemory,
                maxMemory,
                txtArgs.Text.Trim(),
                chkOfflineMode.IsChecked ?? true
            );

            if (success)
            {
                Window parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.WindowState = WindowState.Minimized;
                }
            }
        }

        private async System.Threading.Tasks.Task LaunchBedrockGame(string versionId)
        {
            btnLaunch.IsEnabled = false;
            txtLog.Clear();
            txtStatus.Text = "准备启动基岩版...";

            bool success = await _bedrockLauncher.LaunchGameAsync(versionId);

            if (success)
            {
                Window parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    parentWindow.WindowState = WindowState.Minimized;
                }
            }
        }

        private void OnLaunchStatusChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = status;
            });
        }

        private readonly Queue<string> _logBuffer = new Queue<string>();
        private bool _isLogProcessing = false;

        private void OnLaunchLogReceived(string log)
        {
            lock (_logBuffer)
            {
                _logBuffer.Enqueue(log);
            }

            ProcessLogBuffer();
        }

        private void ProcessLogBuffer()
        {
            if (_isLogProcessing) return;

            _isLogProcessing = true;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    lock (_logBuffer)
                    {
                        while (_logBuffer.Count > 0)
                        {
                            string log = _logBuffer.Dequeue();

                            if (!string.IsNullOrEmpty(txtLog.Text))
                            {
                                txtLog.Text += Environment.NewLine;
                            }
                            txtLog.Text += $"[{DateTime.Now:HH:mm:ss}] {log}";

                            if (txtLog.Text.Length > 100000)
                            {
                                int overflowIndex = txtLog.Text.IndexOf(Environment.NewLine, 50000);
                                if (overflowIndex > 0)
                                {
                                    txtLog.Text = txtLog.Text.Substring(overflowIndex + Environment.NewLine.Length);
                                }
                                else
                                {
                                    txtLog.Text = txtLog.Text.Substring(50000);
                                }
                            }
                        }

                        txtLog.ScrollToEnd();
                    }
                }
                finally
                {
                    _isLogProcessing = false;
                }
            });
        }

        private void OnLaunchCompleted(bool success)
        {
            Dispatcher.Invoke(() =>
            {
                btnLaunch.IsEnabled = true;

                if (success)
                {
                    txtStatus.Text = "游戏运行中";
                }
                else
                {
                    txtStatus.Text = "启动失败";
                }
            });
        }

        private void btnRefreshVersions_Click(object sender, RoutedEventArgs e)
        {
            _ = VersionScanService.Instance.ScanAsync("用户刷新版本");
        }
    }
}
