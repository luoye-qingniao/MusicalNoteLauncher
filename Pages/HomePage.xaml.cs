using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class HomePage : UserControl
    {
        private readonly GameLauncher _gameLauncher;
        private readonly string _minecraftPath;
        private readonly string _username;
        private readonly bool _isOfflineMode;

        public HomePage()
        {
            InitializeComponent();
            
            _username = AppContext.Username ?? "Player";
            _isOfflineMode = AppContext.IsOfflineMode;
            _minecraftPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

            var javaConfig = new JavaConfigManager(_minecraftPath);
            _gameLauncher = new GameLauncher(_minecraftPath, javaConfig);

            _gameLauncher.LaunchStatusChanged += (status) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateGameStatus(status);
                    if (txtConsole != null)
                    {
                        txtConsole.Text += "[状态] " + status + Environment.NewLine;
                        txtConsole.ScrollToEnd();
                    }
                });
            };

            _gameLauncher.LaunchCompleted += (success) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!success && txtStatusDot != null)
                    {
                        txtStatusDot.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    }
                });
            };

            _gameLauncher.LaunchLogReceived += (log) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (txtConsole != null)
                    {
                        txtConsole.Text += log + Environment.NewLine;
                        txtConsole.ScrollToEnd();
                    }
                });
            };

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            if (cboPlayerName != null && cboPlayerName.Items.Count > 0)
            {
                cboPlayerName.SelectedIndex = 0;
            }

            LoadInstalledVersions();
            CheckGpuCompatibilityAsync();

            VersionScanService.Instance.ScanCompleted += OnVersionScanCompleted;
            VersionScanService.Instance.ClearCache();
            VersionScanService.Instance.ScanAsync("HomePage 初始化");
        }

        private void OnVersionScanCompleted(VersionScanResult result)
        {
            Dispatcher.Invoke(() => LoadInstalledVersionsFromScanResult(result));
        }

        private void LoadInstalledVersionsFromScanResult(VersionScanResult result)
        {
            if (cboGameVersion == null) return;

            cboGameVersion.Items.Clear();
            var versions = result.JavaVersions;

            if (versions.Count == 0)
            {
                cboGameVersion.Items.Add(new ComboBoxItem
                {
                    Content = "无已安装版本",
                    Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                    Foreground = Brushes.White,
                    Padding = new Thickness(12, 10, 12, 10)
                });
                cboGameVersion.SelectedIndex = 0;
                return;
            }

            foreach (var v in versions)
            {
                cboGameVersion.Items.Add(new ComboBoxItem
                {
                    Content = v,
                    Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                    Foreground = Brushes.White,
                    Padding = new Thickness(12, 10, 12, 10)
                });
            }
            cboGameVersion.SelectedIndex = 0;
        }

        private void LoadInstalledVersions()
        {
            try
            {
                if (cboGameVersion == null) return;

                var versions = VersionScanService.Instance.GetInstalledJavaVersions();
                cboGameVersion.Items.Clear();

                if (versions.Count == 0)
                {
                    cboGameVersion.Items.Add(new ComboBoxItem
                    {
                        Content = "无已安装版本",
                        Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                        Foreground = Brushes.White,
                        Padding = new Thickness(12, 10, 12, 10)
                    });
                    cboGameVersion.SelectedIndex = 0;
                }
                else
                {
                    foreach (var v in versions)
                    {
                        cboGameVersion.Items.Add(new ComboBoxItem
                        {
                            Content = v,
                            Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                            Foreground = Brushes.White,
                            Padding = new Thickness(12, 10, 12, 10)
                        });
                    }
                    cboGameVersion.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[HomePage] 加载已安装版本失败: " + ex.Message);
            }
        }

        private void UpdateGameStatus(string status)
        {
            if (txtGameStatus == null || txtStatusDot == null) return;

            txtGameStatus.Text = status;

            if (status.Contains("启动成功") || status.Contains("已启动") || status.Contains("正在运行"))
            {
                txtStatusDot.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }
            else if (status.Contains("失败") || status.Contains("异常") || status.Contains("校验失败"))
            {
                txtStatusDot.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            }
            else if (status.Contains("关闭") || status.Contains("退出"))
            {
                txtStatusDot.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
            }
            else
            {
                txtStatusDot.Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219));
            }
        }

        private async void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string gameVersion = (cboGameVersion?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                string username = (cboPlayerName?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _username;
                int maxMemory = 4096;

                if (cboMemory?.SelectedItem is ComboBoxItem memItem)
                {
                    string memStr = memItem.Content?.ToString() ?? "";
                    memStr = memStr.Replace("MB", "").Replace("GB", "").Trim();
                    if (memStr.Contains("半") || memStr.Contains("1/2"))
                    {
                        maxMemory = 2048;
                    }
                    else if (int.TryParse(memStr, out int memMb))
                    {
                        maxMemory = memMb;
                    }
                }

                string resolution = "1920x1080";
                if (cboResolution?.SelectedItem is ComboBoxItem resItem)
                {
                    resolution = resItem.Content?.ToString() ?? "1920x1080";
                }

                if (string.IsNullOrEmpty(gameVersion) || gameVersion == "无已安装版本")
                {
                    MessageBox.Show("请先选择游戏版本", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await _gameLauncher.LaunchGameAsync(
                    gameVersion, username, 2048, maxMemory,
                    "", _isOfflineMode, resolution, false);
            }
            catch (Exception ex)
            {
                Logger.Error($"[HomePage] 启动失败：{ex.Message}");
                MessageBox.Show($"启动失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearConsole_Click(object sender, RoutedEventArgs e)
        {
            if (txtConsole != null) txtConsole.Text = "";
        }

        private void BtnViewMods_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("Mods"); }
        private void BtnViewPacks_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("Modpacks"); }
        private void BtnViewMore_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("ComponentStore"); }
        private void BtnGoToStore_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("ComponentStore"); }
        private void BtnJavaConfig_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("JavaConfig"); }

        private void BtnVersionType_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string tag = button.Tag as string;
                if (tag == "Java")
                {
                    if (btnJavaVersion != null)
                    {
                        btnJavaVersion.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                        btnJavaVersion.Foreground = Brushes.White;
                    }
                    if (btnBedrockVersion != null)
                    {
                        btnBedrockVersion.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                        btnBedrockVersion.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
                    }
                }
                else if (tag == "Bedrock")
                {
                    if (btnBedrockVersion != null)
                    {
                        btnBedrockVersion.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                        btnBedrockVersion.Foreground = Brushes.White;
                    }
                    if (btnJavaVersion != null)
                    {
                        btnJavaVersion.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                        btnJavaVersion.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
                    }
                }
            }
        }

        private void BtnResolutionConfig_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("分辨率设置功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private async Task CheckGpuCompatibilityAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    string gpuName = GetGraphicsCardName();
                    Dispatcher.Invoke(() =>
                    {
                        if (txtGpuStatusIcon != null && txtGpuStatusText != null)
                        {
                            txtGpuStatusIcon.Text = "GPU 已检测";
                            txtGpuStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                            txtGpuStatusText.Text = gpuName ?? "未知显卡";
                            txtGpuStatusText.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
                        }
                    });
                }
                catch
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (txtGpuStatusIcon != null && txtGpuStatusText != null)
                        {
                            txtGpuStatusIcon.Text = "GPU 未检测到";
                            txtGpuStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                            txtGpuStatusText.Text = "无法获取显卡信息";
                        }
                    });
                }
            });
        }

        private string GetGraphicsCardName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                {
                    string fallback = "";
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            string lower = name.ToLower();
                            if (lower.Contains("amd") || lower.Contains("nvidia") || lower.Contains("radeon") || lower.Contains("geforce"))
                                return name;
                            if (string.IsNullOrEmpty(fallback))
                                fallback = name;
                        }
                    }
                    return !string.IsNullOrEmpty(fallback) ? fallback : "未知显卡";
                }
            }
            catch
            {
                return "未知显卡";
            }
        }
    }
}
