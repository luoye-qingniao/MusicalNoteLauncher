using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class SettingsPage : UserControl
    {
        private JavaConfigManager _javaConfig;
        private JavaDownloadService _javaDownloadService;
        private List<JavaConfigManager.DetectedJava> _detectedJavaList;
        private int _recommendedJavaVersion = 8;
        private CancellationTokenSource _downloadCts;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 1) 加载配色主题下拉框
            cbThemeColor.ItemsSource = ThemeColorService.GetAllThemes();
            SelectCurrentTheme();

            // 2) 从 SettingsManager 加载其他所有设置到UI控件
            LoadAllSettingsToUI();

            // 3) 初始化 Java 配置和下载服务
            _javaConfig = new JavaConfigManager(AppContext.MinecraftPath);
            _javaDownloadService = new JavaDownloadService(AppContext.MinecraftPath);
            UpdateCurrentJavaConfigDisplay();
        }

        /// <summary>把保存的设置回填到页面所有控件</summary>
        private void LoadAllSettingsToUI()
        {
            var s = SettingsManager.Settings;

            // 常规设置
            chkAutoLogin.IsChecked = s.AutoLogin;
            chkMinimizeToTray.IsChecked = s.MinimizeToTray;
            chkCheckUpdate.IsChecked = s.CheckUpdate;
            chkShowSplash.IsChecked = s.ShowSplash;
            chkHardwareAcceleration.IsChecked = s.HardwareAcceleration;
            txtGamePath.Text = s.GamePath ?? string.Empty;

            // 游戏设置
            txtMinMemory.Text = s.MinMemory.ToString();
            txtMaxMemory.Text = s.MaxMemory.ToString();
            SelectResolutionItem(s.Resolution);
            chkFullscreen.IsChecked = s.Fullscreen;

            // Java设置
            txtJavaPath.Text = s.JavaPath ?? string.Empty;
            txtJavaArgs.Text = s.JavaArgs ?? string.Empty;

            // 下载设置
            sliderDownloadThreads.Value = s.DownloadThreads;
            txtDownloadThreadsValue.Text = s.DownloadThreads.ToString();
            txtDownloadPath.Text = s.DownloadPath ?? string.Empty;
            chkAutoInstallDependencies.IsChecked = s.AutoInstallDependencies;
            chkEnableVersionIsolation.IsChecked = s.EnableVersionIsolation;
        }

        /// <summary>选中当前已生效的配色</summary>
        private void SelectCurrentTheme()
        {
            var current = ThemeColorService.CurrentTheme;
            foreach (ThemeColorInfo item in cbThemeColor.Items)
            {
                if (item.Preset == current)
                {
                    cbThemeColor.SelectedItem = item;
                    UpdateColorPreviews(item);
                    return;
                }
            }
        }

        /// <summary>把 "1920x1080" 格式的分辨率串匹配到 ComboBoxItem 上</summary>
        private void SelectResolutionItem(string resolution)
        {
            if (string.IsNullOrWhiteSpace(resolution)) return;
            string normalized = resolution.Replace('×', 'x').Replace('X', 'x').Trim();
            foreach (var obj in cbResolution.Items)
            {
                if (obj is ComboBoxItem ci && ci.Content != null)
                {
                    string content = ci.Content.ToString().Replace('×', 'x').Replace('X', 'x').Trim();
                    if (content == normalized)
                    {
                        cbResolution.SelectedItem = ci;
                        return;
                    }
                }
            }
            // 如果没匹配到项（比如用户存了自定义值），则默认选中 "1920×1080"
            foreach (var obj in cbResolution.Items)
            {
                if (obj is ComboBoxItem ci && ci.Content?.ToString().Contains("1920") == true)
                {
                    cbResolution.SelectedItem = ci;
                    return;
                }
            }
        }

        /// <summary>用户切换配色下拉框 —— 即时生效但不写盘（点击保存按钮才写盘）</summary>
        private void CbThemeColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbThemeColor.SelectedItem is ThemeColorInfo info)
            {
                ThemeColorService.ApplyTheme(info.Preset);
                UpdateColorPreviews(info);
                RefreshActiveCategoryButton();
            }
        }

        /// <summary>更新双色块预览</summary>
        private void UpdateColorPreviews(ThemeColorInfo info)
        {
            previewPrimaryColor.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(info.PrimaryColor));
            previewSecondaryColor.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(info.PrimaryDarkColor));
        }

        /// <summary>下载线程数滑块变化 —— 同步更新旁边的数字显示</summary>
        private void SliderDownloadThreads_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtDownloadThreadsValue != null)
            {
                txtDownloadThreadsValue.Text = Math.Round(e.NewValue, 0).ToString();
            }
        }

        /// <summary>保存按钮 —— 保存整个设置页面的所有值</summary>
        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsManager.Settings;

            // --- 常规设置 ---
            s.AutoLogin = chkAutoLogin.IsChecked == true;
            s.MinimizeToTray = chkMinimizeToTray.IsChecked == true;
            s.CheckUpdate = chkCheckUpdate.IsChecked == true;
            s.ShowSplash = chkShowSplash.IsChecked == true;
            s.HardwareAcceleration = chkHardwareAcceleration.IsChecked == true;
            s.GamePath = string.IsNullOrWhiteSpace(txtGamePath.Text) ? "%appdata%\\.minecraft" : txtGamePath.Text.Trim();

            // --- 游戏设置 ---
            if (int.TryParse(txtMinMemory.Text, out int minMem) && minMem >= 128)
                s.MinMemory = minMem;
            if (int.TryParse(txtMaxMemory.Text, out int maxMem) && maxMem >= 128)
                s.MaxMemory = maxMem;
            if (cbResolution.SelectedItem is ComboBoxItem rItem && rItem.Content != null)
                s.Resolution = rItem.Content.ToString().Replace('×', 'x').Trim();
            s.Fullscreen = chkFullscreen.IsChecked == true;

            // --- Java设置 ---
            s.JavaPath = txtJavaPath.Text?.Trim() ?? string.Empty;
            s.JavaArgs = txtJavaArgs.Text?.Trim() ?? string.Empty;

            // --- 下载设置 ---
            s.DownloadThreads = (int)Math.Round(sliderDownloadThreads.Value, 0);
            s.DownloadPath = txtDownloadPath.Text?.Trim() ?? string.Empty;
            s.AutoInstallDependencies = chkAutoInstallDependencies.IsChecked == true;
            s.EnableVersionIsolation = chkEnableVersionIsolation.IsChecked == true;

            // --- 持久化 ---
            SettingsManager.SaveSettings();

            // --- 配色主题（独立的 theme_config.json）---
            if (cbThemeColor.SelectedItem is ThemeColorInfo info)
            {
                ThemeColorService.ApplyTheme(info.Preset);
                UpdateColorPreviews(info);
            }

            // --- 保存成功提示 ---
            MessageBox.Show("设置已保存", "保存成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ======== 分类导航 ========

        private void BtnCategory_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            ResetCategoryButtons();
            button.Background = (Brush)FindResource("PrimaryBrush");

            string content = button.Content.ToString();
            if (content == "常规设置") { tabSettings.SelectedIndex = 0; return; }
            if (content == "游戏设置") { tabSettings.SelectedIndex = 1; return; }
            if (content == "Java设置") { tabSettings.SelectedIndex = 2; return; }
            if (content == "下载设置") { tabSettings.SelectedIndex = 3; return; }
            if (content == "关于") { tabSettings.SelectedIndex = 4; return; }
        }

        private void ResetCategoryButtons()
        {
            var brush = (Brush)FindResource("SurfaceBrush");
            btnCategoryGeneral.Background = brush;
            btnCategoryGame.Background = brush;
            btnCategoryJava.Background = brush;
            btnCategoryDownload.Background = brush;
            btnCategoryAbout.Background = brush;
        }

        /// <summary>主题切换后刷新当前选中分类按钮的强调色</summary>
        private void RefreshActiveCategoryButton()
        {
            var brush = (Brush)FindResource("PrimaryBrush");
            switch (tabSettings.SelectedIndex)
            {
                case 0: btnCategoryGeneral.Background = brush; break;
                case 1: btnCategoryGame.Background = brush; break;
                case 2: btnCategoryJava.Background = brush; break;
                case 3: btnCategoryDownload.Background = brush; break;
                case 4: btnCategoryAbout.Background = brush; break;
            }
        }

        // ======== 浏览按钮 ========

        /// <summary>浏览选择游戏目录</summary>
        private void BtnBrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择 Minecraft 游戏目录",
                ShowNewFolderButton = true
            })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtGamePath.Text = dialog.SelectedPath;
                }
            }
        }

        /// <summary>浏览选择下载目录</summary>
        private void BtnBrowseDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择下载目录",
                ShowNewFolderButton = true
            })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtDownloadPath.Text = dialog.SelectedPath;
                }
            }
        }

        /// <summary>浏览选择 Java 可执行文件路径</summary>
        private void BtnBrowseJavaPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Java可执行文件 (java.exe)|java.exe|所有文件 (*.*)|*.*",
                Title = "选择 Java 可执行文件",
                FileName = "java.exe"
            };
            if (dialog.ShowDialog() == true)
            {
                txtJavaPath.Text = dialog.FileName;
            }
        }

        // ======== Java 检测/验证/设置 ========

        /// <summary>检测系统中的 Java 环境</summary>
        private void BtnDetectJava_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_javaConfig == null)
                    _javaConfig = new JavaConfigManager(AppContext.MinecraftPath);

                _detectedJavaList = _javaConfig.DetectInstalledJava();
                javaList.ItemsSource = _detectedJavaList;

                if (_detectedJavaList.Count == 0)
                {
                    MessageBox.Show("未检测到系统中的Java环境，请手动配置或下载Java", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("检测Java失败: " + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        /// <summary>使用检测到的 Java 版本</summary>
        private void BtnUseDetectedJava_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is JavaConfigManager.DetectedJava detected)
            {
                try
                {
                    _javaConfig.SetAutoConfig(detected);
                    txtJavaPath.Text = detected.Path;
                    UpdateCurrentJavaConfigDisplay();
                    MessageBox.Show($"已使用 Java {detected.MajorVersion}\n路径: {detected.Path}", "配置成功",
                        MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("设置Java失败: " + ex.Message, "错误",
                        MessageBoxButton.OK, MessageBoxImage.Hand);
                }
            }
        }

        /// <summary>验证 Java 配置</summary>
        private void BtnValidateJava_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string javaPath = _javaConfig?.GetJavaPath() ?? txtJavaPath.Text.Trim();
                if (string.IsNullOrEmpty(javaPath))
                {
                    MessageBox.Show("请先设置Java路径", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                bool valid = _javaConfig.ValidateJavaPath(javaPath);
                if (valid)
                {
                    MessageBox.Show("Java配置验证通过!", "验证成功",
                        MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
                else
                {
                    MessageBox.Show("Java配置验证失败，请检查路径是否正确", "验证失败",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("验证失败: " + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        /// <summary>手动设置 Java 路径</summary>
        private void BtnSetJavaPath_Click(object sender, RoutedEventArgs e)
        {
            string path = txtJavaPath.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("请输入Java路径或浏览选择", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                if (!_javaConfig.ValidateJavaPath(path))
                {
                    MessageBox.Show("无效的Java路径或Java版本无法识别", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Hand);
                    return;
                }

                _javaConfig.SetJavaPath(path);
                txtJavaPath.Text = _javaConfig.GetJavaPath();
                UpdateCurrentJavaConfigDisplay();
                MessageBox.Show("已设置Java路径: " + txtJavaPath.Text, "配置成功",
                    MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置Java失败: " + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        // ======== Java 下载 ========

        /// <summary>下载指定 Java 版本</summary>
        private void BtnDownloadJava_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is string tag && int.TryParse(tag, out int javaVersion))
            {
                StartJavaDownload(javaVersion);
            }
        }

        /// <summary>一键下载推荐 Java 版本</summary>
        private void BtnDownloadRecommended_Click(object sender, RoutedEventArgs e)
        {
            StartJavaDownload(_recommendedJavaVersion);
        }

        private async void StartJavaDownload(int javaVersion)
        {
            try
            {
                if (_javaDownloadService == null)
                    _javaDownloadService = new JavaDownloadService(AppContext.MinecraftPath);

                // 检查是否已安装
                if (_javaDownloadService.IsJavaInstalled(javaVersion))
                {
                    string installedPath = _javaDownloadService.GetInstalledJavaPath(javaVersion);
                    var result = MessageBox.Show(
                        $"Java {javaVersion} 已安装!\n路径: {installedPath}\n\n是否使用此版本?",
                        "已安装", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        txtJavaPath.Text = installedPath;
                        _javaConfig.SetJavaPath(installedPath);
                        UpdateCurrentJavaConfigDisplay();
                    }
                    return;
                }

                // 显示下载进度面板
                pnlDownloadProgress.Visibility = Visibility.Visible;
                txtDownloadStatus.Text = $"正在准备下载 Java {javaVersion}...";
                progressDownload.Value = 0;
                txtDownloadProgress.Text = "0%";

                _downloadCts = new CancellationTokenSource();

                var progress = new DownloadProgress();
                progress.ProgressChanged += (info) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        progressDownload.Value = info.Progress;
                        txtDownloadProgress.Text = $"{info.Progress:F1}%";
                        txtDownloadStatus.Text = $"正在下载 Java {javaVersion}...";
                    });
                };

                string javaExePath = await _javaDownloadService.DownloadAndInstallJavaAsync(
                    javaVersion, progress, _downloadCts.Token);

                // 下载完成
                Dispatcher.Invoke(() =>
                {
                    pnlDownloadProgress.Visibility = Visibility.Collapsed;
                    txtJavaPath.Text = javaExePath;
                    _javaConfig.SetJavaPath(javaExePath);
                    UpdateCurrentJavaConfigDisplay();
                    MessageBox.Show($"Java {javaVersion} 下载安装完成!\n路径: {javaExePath}", "下载完成",
                        MessageBoxButton.OK, MessageBoxImage.Asterisk);
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    pnlDownloadProgress.Visibility = Visibility.Collapsed;
                    MessageBox.Show("下载已取消", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    pnlDownloadProgress.Visibility = Visibility.Collapsed;
                    MessageBox.Show("下载Java失败: " + ex.Message, "错误",
                        MessageBoxButton.OK, MessageBoxImage.Hand);
                });
            }
        }

        // ======== 检查更新 ========

        /// <summary>检查更新</summary>
        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("当前版本: 1.0.0\n\n暂无可用更新。", "检查更新",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ======== 辅助方法 ========

        /// <summary>刷新「当前 Java 配置」区域</summary>
        private void UpdateCurrentJavaConfigDisplay()
        {
            if (_javaConfig == null) return;
            txtCurrentJavaPath.Text = _javaConfig.GetJavaPath();
            txtCurrentJavaVersion.Text = "Java " + _javaConfig.GetJavaVersion();
            txtRecommendedMemory.Text = _javaConfig.GetMaxMemoryMb() + " MB";
        }
    }
}
