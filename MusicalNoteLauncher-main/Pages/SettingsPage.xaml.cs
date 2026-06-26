using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public class GameFolderItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; }
        public string Path { get; set; }

        public string DisplayPath
        {
            get
            {
                try
                {
                    return Environment.ExpandEnvironmentVariables(Path).TrimEnd('\\') + "\\";
                }
                catch { return Path; }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class SettingsPage : UserControl
    {
        private JavaConfigManager _javaConfig;
        private JavaDownloadService _javaDownloadService;
        private List<JavaConfigManager.DetectedJava> _detectedJavaList;
        private int _recommendedJavaVersion = 8;
        private CancellationTokenSource _downloadCts;
        private ObservableCollection<GameFolderItemViewModel> _folderItems;
        private GameFolderItemViewModel _contextMenuFolder;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            _folderItems = new ObservableCollection<GameFolderItemViewModel>();
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            cbThemeColor.ItemsSource = ThemeColorService.GetAllThemes();
            SelectCurrentTheme();
            LoadAllSettingsToUI();
            _javaConfig = new JavaConfigManager(AppContext.MinecraftPath);
            _javaDownloadService = new JavaDownloadService(AppContext.MinecraftPath);
            UpdateCurrentJavaConfigDisplay();
            StartRamRefreshTimer();

            Unloaded += (s2, e2) => StopRamRefreshTimer();
        }

        private void LoadAllSettingsToUI()
        {
            var s = SettingsManager.Settings;

            chkAutoLogin.IsChecked = s.AutoLogin;
            chkMinimizeToTray.IsChecked = s.MinimizeToTray;
            chkCheckUpdate.IsChecked = s.CheckUpdate;
            chkShowSplash.IsChecked = s.ShowSplash;
            chkHardwareAcceleration.IsChecked = s.HardwareAcceleration;

            RefreshFolderList();

            // 内存设置 - PCL风格
            radioRamAuto.IsChecked = s.MemoryAutoMode;
            radioRamCustom.IsChecked = !s.MemoryAutoMode;
            sliderRamCustom.Value = s.MemoryCustomGB;
            UpdateRamDisplay();
            UpdateRamAutoInfo();
            SelectResolutionItem(s.Resolution);
            chkFullscreen.IsChecked = s.Fullscreen;

            txtJavaPath.Text = s.JavaPath ?? string.Empty;
            txtJavaArgs.Text = s.JavaArgs ?? string.Empty;

            sliderDownloadThreads.Value = s.DownloadThreads;
            txtDownloadThreadsValue.Text = s.DownloadThreads.ToString();
            txtDownloadPath.Text = s.DownloadPath ?? string.Empty;
            chkAutoInstallDependencies.IsChecked = s.AutoInstallDependencies;
            cmbVersionIsolation.SelectedIndex = Math.Clamp(s.VersionIsolationLevel, 0, 4);
            chkLaunchArgumentRam.IsChecked = s.LaunchArgumentRam;
        }

        private void RefreshFolderList()
        {
            var s = SettingsManager.Settings;
            _folderItems.Clear();

            if (s.GameFolders == null || s.GameFolders.Count == 0)
            {
                s.InitializeDefaultFolders();
            }

            for (int i = 0; i < s.GameFolders.Count; i++)
            {
                var folder = s.GameFolders[i];
                var item = new GameFolderItemViewModel
                {
                    Name = folder.Name,
                    Path = folder.Path,
                    IsSelected = (i == s.SelectedGameFolderIndex)
                };
                _folderItems.Add(item);
            }

            listGameFolders.ItemsSource = _folderItems;
        }

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
            foreach (var obj in cbResolution.Items)
            {
                if (obj is ComboBoxItem ci && ci.Content?.ToString().Contains("1920") == true)
                {
                    cbResolution.SelectedItem = ci;
                    return;
                }
            }
        }

        private void CbThemeColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbThemeColor.SelectedItem is ThemeColorInfo info)
            {
                ThemeColorService.ApplyTheme(info.Preset);
                UpdateColorPreviews(info);
                RefreshActiveCategoryButton();
            }
        }

        private void UpdateColorPreviews(ThemeColorInfo info)
        {
            previewPrimaryColor.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(info.PrimaryColor));
            previewSecondaryColor.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(info.PrimaryDarkColor));
        }

        private void SliderDownloadThreads_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtDownloadThreadsValue != null)
            {
                txtDownloadThreadsValue.Text = Math.Round(e.NewValue, 0).ToString();
            }
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsManager.Settings;

            s.AutoLogin = chkAutoLogin.IsChecked == true;
            s.MinimizeToTray = chkMinimizeToTray.IsChecked == true;
            s.CheckUpdate = chkCheckUpdate.IsChecked == true;
            s.ShowSplash = chkShowSplash.IsChecked == true;
            s.HardwareAcceleration = chkHardwareAcceleration.IsChecked == true;

            // 内存设置 - PCL风格
            s.MemoryAutoMode = radioRamAuto.IsChecked == true;
            s.MemoryCustomGB = Math.Round(sliderRamCustom.Value, 1);
            int memoryMB = s.GetMemoryMB();
            s.MinMemory = memoryMB;
            s.MaxMemory = memoryMB;
            if (cbResolution.SelectedItem is ComboBoxItem rItem && rItem.Content != null)
                s.Resolution = rItem.Content.ToString().Replace('×', 'x').Trim();
            s.Fullscreen = chkFullscreen.IsChecked == true;

            s.JavaPath = txtJavaPath.Text?.Trim() ?? string.Empty;
            s.JavaArgs = txtJavaArgs.Text?.Trim() ?? string.Empty;

            s.DownloadThreads = (int)Math.Round(sliderDownloadThreads.Value, 0);
            s.DownloadPath = txtDownloadPath.Text?.Trim() ?? string.Empty;
            s.AutoInstallDependencies = chkAutoInstallDependencies.IsChecked == true;
            s.VersionIsolationLevel = cmbVersionIsolation.SelectedIndex;
            s.LaunchArgumentRam = chkLaunchArgumentRam.IsChecked == true;

            SettingsManager.SaveSettings();

            string selectedPath = s.GamePath;
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                string expanded = Environment.ExpandEnvironmentVariables(selectedPath);
                AppContext.MinecraftPath = expanded;
            }

            if (cbThemeColor.SelectedItem is ThemeColorInfo info)
            {
                ThemeColorService.ApplyTheme(info.Preset);
                UpdateColorPreviews(info);
            }

            MessageBox.Show("设置已保存", "保存成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #region 内存设置 (PCL风格)

        private System.Windows.Threading.DispatcherTimer _ramRefreshTimer;

        private void StartRamRefreshTimer()
        {
            if (_ramRefreshTimer == null)
            {
                _ramRefreshTimer = new System.Windows.Threading.DispatcherTimer();
                _ramRefreshTimer.Interval = TimeSpan.FromSeconds(2);
                _ramRefreshTimer.Tick += RamRefreshTimer_Tick;
            }
            _ramRefreshTimer.Start();
        }

        private void StopRamRefreshTimer()
        {
            _ramRefreshTimer?.Stop();
        }

        private void RamRefreshTimer_Tick(object sender, EventArgs e)
        {
            UpdateRamDisplay();
            if (radioRamAuto.IsChecked == true)
            {
                UpdateRamAutoInfo();
            }
        }

        private void RadioRamMode_Changed(object sender, RoutedEventArgs e)
        {
            if (panRamCustom == null || panRamAuto == null) return;

            bool isAuto = radioRamAuto.IsChecked == true;
            panRamCustom.Visibility = isAuto ? Visibility.Collapsed : Visibility.Visible;
            panRamAuto.Visibility = isAuto ? Visibility.Visible : Visibility.Collapsed;

            if (isAuto)
            {
                UpdateRamAutoInfo();
            }
            UpdateRamDisplay();
        }

        private void SliderRamCustom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtRamCustomValue != null)
            {
                double gb = Math.Round(e.NewValue, 1);
                txtRamCustomValue.Text = $"{gb:F1} GB";
                UpdateRamDisplay();
            }
        }

        private void UpdateRamAutoInfo()
        {
            if (txtRamAutoValue == null) return;

            try
            {
                int autoMB = LauncherSettings.CalculateAutoMemoryMB();
                int modCount = 0;
                try
                {
                    string gamePath = Environment.ExpandEnvironmentVariables(
                        SettingsManager.Settings?.GamePath ?? "%appdata%\\.minecraft");
                    string modsPath = System.IO.Path.Combine(gamePath, "mods");
                    if (System.IO.Directory.Exists(modsPath))
                        modCount = System.IO.Directory.GetFiles(modsPath, "*.jar", System.IO.SearchOption.TopDirectoryOnly).Length;
                }
                catch { }

                string modInfo = modCount > 0 ? $" (检测到 {modCount} 个模组)" : "";
                txtRamAutoValue.Text = $"{(autoMB / 1024.0):F1} GB{modInfo}";
            }
            catch
            {
                txtRamAutoValue.Text = "计算失败";
            }
        }

        private void UpdateRamDisplay()
        {
            if (bdrRamBar == null) return;

            try
            {
                double totalGB = LauncherSettings.GetTotalSystemMemoryGB();
                double availableGB = LauncherSettings.GetAvailableSystemMemoryGB();
                double usedGB = totalGB - availableGB;

                double gameGB;
                if (radioRamAuto.IsChecked == true)
                {
                    gameGB = LauncherSettings.CalculateAutoMemoryMB() / 1024.0;
                }
                else
                {
                    gameGB = sliderRamCustom.Value;
                }

                // 游戏分配内存不应超过可用内存
                gameGB = Math.Min(gameGB, availableGB);

                double freeGB = availableGB - gameGB;
                if (freeGB < 0) freeGB = 0;

                // 更新内存条列宽 (使用Star比例)
                double totalForBar = usedGB + gameGB + freeGB;
                if (totalForBar <= 0) totalForBar = 1;

                colRamUsed.Width = new GridLength(usedGB / totalForBar, GridUnitType.Star);
                colRamGame.Width = new GridLength(gameGB / totalForBar, GridUnitType.Star);
                colRamFree.Width = new GridLength(freeGB / totalForBar, GridUnitType.Star);

                // 更新标签
                if (txtRamUsedLabel != null)
                    txtRamUsedLabel.Text = $"已使用 {usedGB:F1} GB / {totalGB:F1} GB";
                if (txtRamGameLabel != null)
                    txtRamGameLabel.Text = $"游戏分配 {gameGB:F1} GB";
                if (txtRamFreeLabel != null)
                    txtRamFreeLabel.Text = $"空闲 {freeGB:F1} GB";

                // 检查32位Java警告 (通过Java路径判断)
                CheckJava32BitWarning();
            }
            catch { }
        }

        private void CheckJava32BitWarning()
        {
            if (bdrJava32Warning == null) return;

            try
            {
                string javaPath = SettingsManager.Settings?.JavaPath;
                if (string.IsNullOrWhiteSpace(javaPath))
                {
                    bdrJava32Warning.Visibility = Visibility.Collapsed;
                    return;
                }

                javaPath = Environment.ExpandEnvironmentVariables(javaPath);
                if (!System.IO.File.Exists(javaPath))
                {
                    bdrJava32Warning.Visibility = Visibility.Collapsed;
                    return;
                }

                var fileInfo = new System.IO.FileInfo(javaPath);
                // 简单判断：检查文件是否在Program Files (x86)下或文件名含32
                bool is32Bit = javaPath.Contains("(x86)") ||
                               javaPath.Contains("x86") ||
                               javaPath.ToLower().Contains("32");

                bdrJava32Warning.Visibility = is32Bit ? Visibility.Visible : Visibility.Collapsed;

                // 如果是32位Java且自定义模式，限制最大值为1GB
                if (is32Bit && radioRamCustom.IsChecked == true && sliderRamCustom != null)
                {
                    sliderRamCustom.Maximum = 1.0;
                    if (sliderRamCustom.Value > 1.0)
                        sliderRamCustom.Value = 1.0;
                }
                else if (sliderRamCustom != null)
                {
                    sliderRamCustom.Maximum = 32.0;
                }
            }
            catch
            {
                bdrJava32Warning.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

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

        // ======== 游戏文件夹管理 ========

        private void FolderRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is GameFolderItemViewModel item)
            {
                var s = SettingsManager.Settings;
                for (int i = 0; i < _folderItems.Count; i++)
                {
                    if (_folderItems[i] == item)
                    {
                        s.SelectedGameFolderIndex = i;
                        _folderItems[i].IsSelected = true;
                    }
                    else
                    {
                        _folderItems[i].IsSelected = false;
                    }
                }

                string selectedPath = s.GamePath;
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    string expanded = Environment.ExpandEnvironmentVariables(selectedPath);
                    AppContext.MinecraftPath = expanded;
                }
            }
        }

        private void FolderSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameFolderItemViewModel item)
            {
                _contextMenuFolder = item;

                var contextMenu = new ContextMenu();

                var renameItem = new MenuItem { Header = "重命名" };
                renameItem.Click += MenuRename_Click;
                contextMenu.Items.Add(renameItem);

                var openItem = new MenuItem { Header = "打开文件夹" };
                openItem.Click += MenuOpen_Click;
                contextMenu.Items.Add(openItem);

                contextMenu.Items.Add(new Separator());

                var removeItem = new MenuItem { Header = "从列表中移除" };
                removeItem.Click += MenuRemove_Click;
                contextMenu.Items.Add(removeItem);

                var deleteItem = new MenuItem
                {
                    Header = "删除文件夹",
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69))
                };
                deleteItem.Click += MenuDelete_Click;
                contextMenu.Items.Add(deleteItem);

                btn.ContextMenu = contextMenu;
                contextMenu.PlacementTarget = btn;
                contextMenu.Placement = PlacementMode.MousePoint;
                contextMenu.IsOpen = true;
            }
        }

        private void BtnCreateFolder_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string newFolder = Path.Combine(exeDir, ".minecraft");

                if (!Directory.Exists(newFolder))
                {
                    Directory.CreateDirectory(newFolder);
                    string versionsDir = Path.Combine(newFolder, "versions");
                    Directory.CreateDirectory(versionsDir);
                }

                string folderName = Path.GetFileName(exeDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(folderName)) folderName = "启动器目录";

                AddFolderToList(newFolder, folderName);
                MessageBox.Show("新建 .minecraft 文件夹成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("创建文件夹失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddFolder_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择 Minecraft 游戏文件夹（需包含versions子文件夹或为其所在目录）",
                ShowNewFolderButton = true
            })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    string versionsPath = Path.Combine(selectedPath, "versions");
                    string targetPath = selectedPath;
                    if (!Directory.Exists(versionsPath))
                    {
                        bool foundSub = false;
                        foreach (var subDir in Directory.GetDirectories(selectedPath))
                        {
                            if (Directory.Exists(Path.Combine(subDir, "versions")))
                            {
                                targetPath = subDir;
                                foundSub = true;
                                break;
                            }
                        }
                        if (!foundSub && !Directory.Exists(versionsPath))
                        {
                            var result = MessageBox.Show(
                                "该文件夹似乎不是有效的 Minecraft 文件夹（未找到versions子文件夹）。\n是否仍要添加？",
                                "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (result != MessageBoxResult.Yes) return;
                        }
                    }

                    var parts = targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string defaultName = parts.LastOrDefault() ?? "游戏文件夹";
                    if (defaultName == ".minecraft" && parts.Length >= 2)
                        defaultName = parts[parts.Length - 2];

                    string inputName = ShowInputDialog("输入显示名称", "输入该文件夹在列表中显示的名称：", defaultName);
                    if (string.IsNullOrWhiteSpace(inputName)) return;

                    AddFolderToList(targetPath, inputName);
                    MessageBox.Show($"文件夹 {inputName} 已添加！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async void BtnImportModpack_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择整合包文件",
                Filter = "整合包文件 (*.mrpack;*.zip)|*.mrpack;*.zip|Modrinth 整合包 (*.mrpack)|*.mrpack|CurseForge 整合包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true) return;

            string selectedFile = dialog.FileName;
            string fileExt = Path.GetExtension(selectedFile).ToLowerInvariant();

            try
            {
                string packName = null;
                string packFormat = null;

                if (fileExt == ".mrpack")
                {
                    packFormat = "modrinth";
                    packName = await ReadMrpackNameAsync(selectedFile);
                }
                else if (fileExt == ".zip")
                {
                    var (format, name) = await DetectZipFormatAsync(selectedFile);
                    packFormat = format;
                    packName = name;
                }

                if (string.IsNullOrEmpty(packName))
                    packName = Path.GetFileNameWithoutExtension(selectedFile);

                string defaultFolderName = SanitizeFolderName(packName);
                string folderName = ShowInputDialog("输入文件夹名称", "请输入整合包文件夹的显示名称：", defaultFolderName);
                if (string.IsNullOrWhiteSpace(folderName)) return;

                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string newFolderPath = Path.Combine(exeDir, SanitizeFolderName(folderName));

                int suffix = 1;
                string originalPath = newFolderPath;
                while (Directory.Exists(newFolderPath))
                {
                    newFolderPath = originalPath + "_" + suffix;
                    suffix++;
                }

                var progressWindow = new Window
                {
                    Title = "正在导入整合包",
                    Width = 420,
                    Height = 200,
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this)
                };

                var progressGrid = new Grid { Margin = new Thickness(20) };
                progressGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                progressGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                progressGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var statusText = new TextBlock
                {
                    Text = "正在准备...",
                    FontSize = 14,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                Grid.SetRow(statusText, 0);
                progressGrid.Children.Add(statusText);

                var progressBar = new ProgressBar
                {
                    Height = 20,
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                Grid.SetRow(progressBar, 1);
                progressGrid.Children.Add(progressBar);

                var detailText = new TextBlock
                {
                    Text = "",
                    FontSize = 12,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(detailText, 2);
                progressGrid.Children.Add(detailText);

                progressWindow.Content = progressGrid;

                Directory.CreateDirectory(newFolderPath);

                bool success = false;
                string errorMsg = null;
                string installedVersionId = null;

                var cts = new CancellationTokenSource();

                progressWindow.Loaded += async (s, args) =>
                {
                    try
                    {
                        if (packFormat == "modrinth" || fileExt == ".mrpack")
                        {
                            var installer = new ModpackInstaller(newFolderPath);
                            installer.StatusChanged += status =>
                            {
                                progressWindow.Dispatcher.Invoke(() =>
                                {
                                    statusText.Text = status;
                                    detailText.Text = $"文件夹: {newFolderPath}";
                                });
                            };
                            installer.ProgressChanged += pct =>
                            {
                                progressWindow.Dispatcher.Invoke(() =>
                                {
                                    progressBar.Value = pct;
                                });
                            };

                            var result = await installer.InstallFromMrpackAsync(selectedFile, folderName, cts.Token);
                            success = result.Success;
                            errorMsg = result.ErrorMessage;
                            installedVersionId = result.VersionId;
                        }
                        else if (packFormat == "curseforge")
                        {
                            var result = await InstallCurseForgePackAsync(selectedFile, newFolderPath, folderName,
                                status => progressWindow.Dispatcher.Invoke(() =>
                                {
                                    statusText.Text = status;
                                    detailText.Text = $"文件夹: {newFolderPath}";
                                }),
                                pct => progressWindow.Dispatcher.Invoke(() =>
                                {
                                    progressBar.Value = pct;
                                }), cts.Token);
                            success = result.Success;
                            errorMsg = result.ErrorMessage;
                        }
                        else
                        {
                            var result = await InstallGenericZipPackAsync(selectedFile, newFolderPath, folderName,
                                status => progressWindow.Dispatcher.Invoke(() =>
                                {
                                    statusText.Text = status;
                                    detailText.Text = $"文件夹: {newFolderPath}";
                                }),
                                pct => progressWindow.Dispatcher.Invoke(() =>
                                {
                                    progressBar.Value = pct;
                                }), cts.Token);
                            success = result.Success;
                            errorMsg = result.ErrorMessage;
                        }
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        errorMsg = ex.Message;
                    }

                    progressWindow.Dispatcher.Invoke(() => progressWindow.Close());
                };

                progressWindow.ShowDialog();

                if (success)
                {
                    AddFolderToList(newFolderPath, folderName);
                    SettingsManager.SaveSettings();

                    string targetPath = Environment.ExpandEnvironmentVariables(SettingsManager.Settings.GamePath);
                    AppContext.MinecraftPath = targetPath;

                    MessageBox.Show(
                        $"整合包「{folderName}」导入成功！\n\n安装位置: {newFolderPath}\n\n已自动切换到该文件夹，可在主页下载对应游戏版本后启动。",
                        "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    try { if (Directory.Exists(newFolderPath)) Directory.Delete(newFolderPath, true); } catch { }

                    if (!string.IsNullOrEmpty(errorMsg))
                    {
                        MessageBox.Show($"整合包导入失败！\n{errorMsg}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[整合包导入] 异常: {ex.Message}");
                MessageBox.Show($"导入整合包时发生错误:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> ReadMrpackNameAsync(string mrpackPath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(mrpackPath))
                {
                    var entry = archive.GetEntry("modrinth.index.json");
                    if (entry != null)
                    {
                        using (var stream = entry.Open())
                        using (var reader = new StreamReader(stream))
                        {
                            string json = await reader.ReadToEndAsync();
                            using (var doc = JsonDocument.Parse(json))
                            {
                                if (doc.RootElement.TryGetProperty("name", out var nameEl))
                                    return nameEl.GetString();
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private async Task<(string format, string name)> DetectZipFormatAsync(string zipPath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string name = entry.FullName;
                        if (name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var stream = entry.Open())
                            using (var reader = new StreamReader(stream))
                            {
                                string json = await reader.ReadToEndAsync();
                                using (var doc = JsonDocument.Parse(json))
                                {
                                    bool hasAddons = doc.RootElement.TryGetProperty("addons", out _);
                                    string packName = null;
                                    if (doc.RootElement.TryGetProperty("name", out var nameEl))
                                        packName = nameEl.GetString();

                                    if (hasAddons)
                                        return ("mcbbs", packName);
                                    else
                                        return ("curseforge", packName);
                                }
                            }
                        }
                        if (name.Equals("modrinth.index.json", StringComparison.OrdinalIgnoreCase))
                        {
                            return ("modrinth", null);
                        }
                        if (name.Equals("mmc-pack.json", StringComparison.OrdinalIgnoreCase))
                        {
                            return ("mmc", null);
                        }
                        if (name.Equals("modpack.json", StringComparison.OrdinalIgnoreCase))
                        {
                            return ("hmcl", null);
                        }
                    }

                    foreach (var entry in archive.Entries)
                    {
                        if (Regex.IsMatch(entry.FullName, @"^[^/]+/versions/[^/]+/[^/]+\.json$") ||
                            Regex.IsMatch(entry.FullName, @"^versions/[^/]+/[^/]+\.json$"))
                        {
                            return ("generic", null);
                        }
                    }
                }
            }
            catch { }
            return ("unknown", null);
        }

        private async Task<(bool Success, string ErrorMessage)> InstallCurseForgePackAsync(
            string zipPath, string targetFolder, string packName,
            Action<string> reportStatus, Action<int> reportProgress, CancellationToken ct)
        {
            try
            {
                reportStatus("正在解析 CurseForge 整合包...");
                reportProgress(5);

                string tempDir = Path.Combine(Path.GetTempPath(), $"cf-extract-{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    reportStatus("正在解压文件...");
                    reportProgress(15);
                    ZipFile.ExtractToDirectory(zipPath, tempDir);

                    string manifestPath = Path.Combine(tempDir, "manifest.json");
                    if (!File.Exists(manifestPath))
                    {
                        foreach (var dir in Directory.GetDirectories(tempDir))
                        {
                            if (File.Exists(Path.Combine(dir, "manifest.json")))
                            {
                                manifestPath = Path.Combine(dir, "manifest.json");
                                break;
                            }
                        }
                    }

                    if (!File.Exists(manifestPath))
                        return (false, "无法找到 manifest.json，可能不是有效的 CurseForge 整合包");

                    string mcVersion = null;
                    string loaderType = null;
                    string overridesDir = null;

                    using (var stream = File.OpenRead(manifestPath))
                    using (var reader = new StreamReader(stream))
                    {
                        string json = await reader.ReadToEndAsync();
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;

                            if (root.TryGetProperty("minecraft", out var mcEl) &&
                                mcEl.TryGetProperty("version", out var verEl))
                            {
                                mcVersion = verEl.GetString();
                            }

                            if (root.TryGetProperty("minecraft", out mcEl) &&
                                mcEl.TryGetProperty("modLoaders", out var loaders))
                            {
                                foreach (var loader in loaders.EnumerateArray())
                                {
                                    if (loader.TryGetProperty("id", out var idEl))
                                    {
                                        string loaderId = idEl.GetString() ?? "";
                                        if (loaderId.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
                                        {
                                            loaderType = "forge";
                                        }
                                        else if (loaderId.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
                                        {
                                            loaderType = "fabric";
                                        }
                                    }
                                }
                            }

                            if (root.TryGetProperty("overrides", out var ovEl))
                            {
                                string overridesRel = ovEl.GetString() ?? "overrides";
                                overridesDir = Path.Combine(Path.GetDirectoryName(manifestPath), overridesRel);
                            }
                        }
                    }

                    reportProgress(30);

                    if (overridesDir != null && Directory.Exists(overridesDir))
                    {
                        reportStatus("正在复制配置文件...");
                        reportProgress(45);
                        CopyDirectoryRecursive(overridesDir, targetFolder);
                    }

                    reportProgress(60);

                    string versionsDir = Path.Combine(targetFolder, "versions");
                    Directory.CreateDirectory(versionsDir);

                    reportStatus($"需要安装 Minecraft {mcVersion ?? "(未知版本)"} + {loaderType ?? " Forge/Fabric"}");
                    reportProgress(80);

                    string notice = $"整合包「{packName}」的基础文件已解压。\n\n";
                    if (!string.IsNullOrEmpty(mcVersion))
                        notice += $"所需 Minecraft 版本: {mcVersion}\n";
                    if (!string.IsNullOrEmpty(loaderType))
                        notice += $"所需加载器: {loaderType}\n";
                    notice += $"\n请在主页下载安装对应版本后，将 mods 文件夹中的模组放入。";

                    reportStatus("正在完成...");
                    reportProgress(100);

                    return (true, null);
                }
                finally
                {
                    try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool Success, string ErrorMessage)> InstallGenericZipPackAsync(
            string zipPath, string targetFolder, string packName,
            Action<string> reportStatus, Action<int> reportProgress, CancellationToken ct)
        {
            try
            {
                reportStatus("正在解压压缩包...");
                reportProgress(10);

                string tempDir = Path.Combine(Path.GetTempPath(), $"zip-extract-{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    ZipFile.ExtractToDirectory(zipPath, tempDir);

                    reportProgress(40);
                    reportStatus("正在检测游戏目录结构...");

                    string sourceDir = await FindMinecraftRootAsync(tempDir);
                    if (sourceDir == null)
                        sourceDir = tempDir;

                    reportProgress(60);
                    reportStatus("正在复制文件...");

                    CopyDirectoryRecursive(sourceDir, targetFolder);

                    reportProgress(90);
                    reportStatus("正在完成...");

                    Directory.CreateDirectory(Path.Combine(targetFolder, "versions"));

                    reportProgress(100);
                    return (true, null);
                }
                finally
                {
                    try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<string> FindMinecraftRootAsync(string baseDir)
        {
            if (Directory.Exists(Path.Combine(baseDir, "versions")))
                return baseDir;

            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                if (Directory.Exists(Path.Combine(dir, "versions")))
                    return dir;

                if (Directory.Exists(Path.Combine(dir, ".minecraft")))
                    return Path.Combine(dir, ".minecraft");
            }

            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                var result = await FindMinecraftRootAsync(dir);
                if (result != null) return result;
            }

            return null;
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Minecraft";
            var invalid = Path.GetInvalidFileNameChars();
            var filtered = name.Where(c => !invalid.Contains(c)).ToArray();
            string result = new string(filtered).Trim();
            if (string.IsNullOrEmpty(result)) return "Minecraft";
            if (result.Length > 64) result = result.Substring(0, 64);
            return result;
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, dest);
            }
        }

        private void AddFolderToList(string folderPath, string displayName)
        {
            var s = SettingsManager.Settings;
            if (s.GameFolders == null) s.GameFolders = new List<GameFolderEntry>();

            string normalizedPath = Environment.ExpandEnvironmentVariables(folderPath).TrimEnd('\\') + "\\";

            for (int i = 0; i < s.GameFolders.Count; i++)
            {
                if (string.Equals(s.GameFolders[i].NormalizedPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    s.SelectedGameFolderIndex = i;
                    RefreshFolderList();
                    return;
                }
            }

            s.GameFolders.Add(new GameFolderEntry(displayName, folderPath));
            s.SelectedGameFolderIndex = s.GameFolders.Count - 1;
            RefreshFolderList();
        }

        private void MenuRename_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuFolder == null) return;

            string newName = ShowInputDialog("重命名", "输入新的文件夹名称：", _contextMenuFolder.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;

            var s = SettingsManager.Settings;
            for (int i = 0; i < s.GameFolders.Count; i++)
            {
                if (s.GameFolders[i].Name == _contextMenuFolder.Name && s.GameFolders[i].Path == _contextMenuFolder.Path)
                {
                    s.GameFolders[i].Name = newName;
                    break;
                }
            }
            RefreshFolderList();
        }

        private void MenuOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuFolder == null) return;
            try
            {
                string path = Environment.ExpandEnvironmentVariables(_contextMenuFolder.Path);
                if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("文件夹不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开文件夹失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuFolder == null) return;

            var s = SettingsManager.Settings;
            if (s.GameFolders.Count <= 1)
            {
                MessageBox.Show("至少需要保留一个游戏文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"确定要从列表中移除 \"{_contextMenuFolder.Name}\" 吗？\n这不会删除实际的文件夹，只会从启动器列表中移除。",
                "确认移除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            int removedIndex = -1;
            for (int i = 0; i < s.GameFolders.Count; i++)
            {
                if (s.GameFolders[i].Name == _contextMenuFolder.Name && s.GameFolders[i].Path == _contextMenuFolder.Path)
                {
                    removedIndex = i;
                    s.GameFolders.RemoveAt(i);
                    break;
                }
            }

            if (removedIndex >= 0)
            {
                if (s.SelectedGameFolderIndex >= s.GameFolders.Count)
                    s.SelectedGameFolderIndex = s.GameFolders.Count - 1;
                else if (s.SelectedGameFolderIndex > removedIndex)
                    s.SelectedGameFolderIndex--;
                RefreshFolderList();
            }
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuFolder == null) return;

            var s = SettingsManager.Settings;
            if (s.GameFolders.Count <= 1)
            {
                MessageBox.Show("至少需要保留一个游戏文件夹！无法删除最后一个文件夹。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string folderPath = Environment.ExpandEnvironmentVariables(_contextMenuFolder.Path);

            var confirm1 = MessageBox.Show(
                $"确定要删除文件夹 \"{_contextMenuFolder.Name}\" 吗？\n\n路径: {folderPath}\n\n⚠️ 此操作将永久删除该文件夹及其所有内容（包括游戏版本、模组、存档、配置等），且无法恢复！",
                "⚠️ 确认删除文件夹", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm1 != MessageBoxResult.Yes) return;

            var confirm2 = MessageBox.Show(
                $"最后确认！\n\n你真的要永久删除这个文件夹吗？\n所有数据将被彻底删除，无法撤销！",
                "⚠️ 再次确认", MessageBoxButton.YesNo, MessageBoxImage.Stop);
            if (confirm2 != MessageBoxResult.Yes) return;

            int removedIndex = -1;
            for (int i = 0; i < s.GameFolders.Count; i++)
            {
                if (s.GameFolders[i].Name == _contextMenuFolder.Name && s.GameFolders[i].Path == _contextMenuFolder.Path)
                {
                    removedIndex = i;
                    break;
                }
            }

            bool wasSelected = (removedIndex == s.SelectedGameFolderIndex);

            bool deleted = false;
            string errorMsg = null;
            try
            {
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, recursive: true);
                }
                deleted = true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
            }

            if (!deleted)
            {
                MessageBox.Show($"删除文件夹失败！\n{errorMsg}\n\n文件夹已从列表中移除，但磁盘上的文件可能被占用。", "删除失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (removedIndex >= 0)
            {
                s.GameFolders.RemoveAt(removedIndex);

                if (wasSelected || s.SelectedGameFolderIndex >= s.GameFolders.Count)
                    s.SelectedGameFolderIndex = s.GameFolders.Count - 1;
                else if (s.SelectedGameFolderIndex > removedIndex)
                    s.SelectedGameFolderIndex--;

                SettingsManager.SaveSettings();

                string newSelectedPath = Environment.ExpandEnvironmentVariables(s.GamePath);
                AppContext.MinecraftPath = newSelectedPath;

                RefreshFolderList();

                if (deleted)
                {
                    MessageBox.Show($"文件夹 \"{_contextMenuFolder.Name}\" 已成功删除！", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private string ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            var inputWindow = new InputBox(title, prompt, defaultValue);
            if (inputWindow.ShowDialog() == true)
            {
                return inputWindow.ResponseText?.Trim();
            }
            return null;
        }

        // ======== 浏览按钮 ========

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

        // ======== 内存优化 ========

        private async void BtnMemoryOptimize_Click(object sender, RoutedEventArgs e)
        {
            btnMemoryOptimize.IsEnabled = false;
            btnMemoryOptimize.Content = "优化中...";
            try
            {
                long freed = await MemoryOptimizer.OptimizeAsync(null);
                MessageBox.Show($"内存优化完成！\n释放内存: {freed} MB", "内存优化",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("内存优化失败: " + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnMemoryOptimize.IsEnabled = true;
                btnMemoryOptimize.Content = "立即优化内存";
            }
        }

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

        private void BtnDownloadJava_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is string tag && int.TryParse(tag, out int javaVersion))
            {
                StartJavaDownload(javaVersion);
            }
        }

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

        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("当前版本: 1.0.0\n\n暂无可用更新。", "检查更新",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateCurrentJavaConfigDisplay()
        {
            if (_javaConfig == null) return;

            string javaPath = _javaConfig.GetJavaPath();
            int javaVersion = _javaConfig.GetJavaVersion();
            long maxMem = _javaConfig.GetMaxMemoryMb();

            // 状态卡片
            bool hasJava = !string.IsNullOrWhiteSpace(javaPath) && javaPath != "java";
            if (txtCurrentJavaVersionBold != null)
                txtCurrentJavaVersionBold.Text = hasJava ? $"Java {javaVersion}" : "未配置";
            if (txtCurrentJavaPathSmall != null)
                txtCurrentJavaPathSmall.Text = hasJava ? javaPath : "请在下方检测或设置 Java 路径";
            if (txtCurrentJavaMem != null)
                txtCurrentJavaMem.Text = hasJava ? $"最大可用内存: {maxMem} MB" : "";
            if (bdrJavaStatus != null)
                bdrJavaStatus.Background = hasJava
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
        }
    }
}
