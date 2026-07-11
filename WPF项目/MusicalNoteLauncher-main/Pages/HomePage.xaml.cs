using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;
using MusicalNoteLauncher.Controls;
using PCL.Account;

namespace MusicalNoteLauncher.Pages
{
    public partial class HomePage : UserControl
    {
        private GameLauncher _gameLauncher;
        private string _username;
        private bool _isOfflineMode;
        private SkinServer _skinServer;

        private string _minecraftPath => AppContext.MinecraftPath;
        private string _launcherMinecraftPath; // 记录 _gameLauncher 创建时使用的路径

        // 基岩版服务
        private BedrockEnhancedDownloadService _bedrockDownloadService;
        private BedrockOfflineLauncher _bedrockOfflineLauncher;
        private bool _isBedrockMode;
        private bool _isSwitchingType;

        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        
        private List<RecommendItemViewModel> _modRecommendations = new List<RecommendItemViewModel>();
        private List<RecommendItemViewModel> _packRecommendations = new List<RecommendItemViewModel>();
        private int _currentModIndex = 0;
        private int _currentPackIndex = 0;
        private Timer _carouselTimer;

        public HomePage()
        {
            InitializeComponent();
            
            _username = AppContext.Username ?? "Player";
            _isOfflineMode = AppContext.IsOfflineMode;

            _launcherMinecraftPath = AppContext.MinecraftPath;
            var javaConfig = new JavaConfigManager(_launcherMinecraftPath);
            _gameLauncher = new GameLauncher(_launcherMinecraftPath, javaConfig);
            _bedrockDownloadService = new BedrockEnhancedDownloadService(_minecraftPath);
            _bedrockOfflineLauncher = new BedrockOfflineLauncher(_minecraftPath);
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();

            // 监听账号变更，同步昵称下拉框
            AppContext.AccountChanged += OnAccountChanged;

            WireGameLauncherEvents();

            Loaded += OnLoaded;
            IsVisibleChanged += OnVisibleChanged;
        }

        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue && _launcherMinecraftPath != AppContext.MinecraftPath)
            {
                _launcherMinecraftPath = AppContext.MinecraftPath;
                var javaConfig = new JavaConfigManager(_launcherMinecraftPath);
                _gameLauncher = new GameLauncher(_launcherMinecraftPath, javaConfig);
                _bedrockDownloadService = new BedrockEnhancedDownloadService(_launcherMinecraftPath);
                _bedrockOfflineLauncher = new BedrockOfflineLauncher(_launcherMinecraftPath);
                WireGameLauncherEvents();
                LoadInstalledVersions();
                Logger.Info($"[HomePage] 游戏目录已变更，已刷新启动器: {_launcherMinecraftPath}");
            }
        }

        private void WireGameLauncherEvents()
        {
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
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            try { RefreshAccountList(); }
            catch (Exception ex) { Logger.Error($"[HomePage] 加载账号列表失败: {ex.Message}"); }
            
            LoadRecommendationsAsync();

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
            string actualGameDir = _minecraftPath;
            try
            {
                if (_isBedrockMode)
                {
                    await LaunchBedrockGame();
                    return;
                }

                string gameVersion = (cboGameVersion?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                var selectedItem = cboPlayerName?.SelectedItem as ComboBoxItem;
                var account = selectedItem?.Tag as GameAccount;
                string username = selectedItem?.Content?.ToString() ?? _username;
                bool offlineMode = account?.Type == AccountType.Offline;
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
                    ModernMessageBox.ShowWarning("请先选择游戏版本", "提示");
                    return;
                }

                // 计算版本隔离后的实际游戏目录（与 GameLauncher 一致）
                if (SettingsManager.Settings.ShouldIsolateVersionForVersion(_minecraftPath, gameVersion))
                {
                    actualGameDir = Path.Combine(_minecraftPath, "versions", gameVersion, "game");
                    Directory.CreateDirectory(actualGameDir);
                    
                    // 如果隔离目录没有 options.txt，从主目录复制
                    string isolatedOptionsPath = Path.Combine(actualGameDir, "options.txt");
                    string mainOptionsPath = Path.Combine(_minecraftPath, "options.txt");
                    if (!File.Exists(isolatedOptionsPath) && File.Exists(mainOptionsPath))
                    {
                        File.Copy(mainOptionsPath, isolatedOptionsPath);
                    }
                }

                // 准备皮肤并等待完成（确保游戏启动前资源包已就绪）
                await Task.Run(() =>
                {
                    StartSkinServer();
                    SkinServer.SetupCustomSkinLoaderConfig(actualGameDir);

                    // PCL 风格: 构建资源包并注入 options.txt (无需 CustomSkinLoader Mod)
                    // 仅在设置中启用时生效
                    if (SettingsManager.Settings.EnableSkinResourcePack)
                    {
                        // 优先使用自定义皮肤；没有则回退到默认 Steve/Alex 皮肤
                        string skinFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins", $"{account.Uuid}.png");
                        bool isSlim = false;
                        if (File.Exists(skinFile))
                        {
                            isSlim = PCL.Account.Settings.Get<bool>($"SkinSlim_{account.Uuid}");
                        }
                        else
                        {
                            // 无自定义皮肤：读取用户在个人资料页保存的 Steve/Alex 偏好
                            isSlim = PCL.Account.Settings.Get<bool>($"SkinSlim_{account.Uuid}");
                            skinFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Skins",
                                isSlim ? "alex.png" : "steve.png");
                        }

                        SkinResourcePackBuilder.Build(skinFile, isSlim, actualGameDir, gameVersion);
                        SkinResourcePackBuilder.InjectToOptions(actualGameDir);
                    }
                });

                await _gameLauncher.LaunchGameAsync(
                    gameVersion, username, 2048, maxMemory,
                    "", offlineMode, resolution, false);
            }
            catch (Exception ex)
            {
                Logger.Error($"[HomePage] 启动失败：{ex.Message}");
                ModernMessageBox.ShowError($"启动失败：{ex.Message}", "错误");
            }
            finally
            {
                // 无论成功失败都显示启动结果弹窗
                ShowLaunchResultPopup();
                // 游戏退出后停止皮肤服务器
                StopSkinServer();
                // 清理资源包 (避免下次不同账号看到错误皮肤)
                SkinResourcePackBuilder.CleanFromOptions(actualGameDir);
            }
        }

        private async Task LaunchBedrockGame()
        {
            string versionId = (cboGameVersion?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string username = _username;

            if (string.IsNullOrEmpty(versionId) || versionId == "无已安装基岩版版本" || versionId == "正在扫描本地基岩版...")
            {
                ModernMessageBox.ShowWarning("请先选择基岩版版本", "提示");
                return;
            }

            btnLaunchGame.IsEnabled = false;
            btnLaunchGame.Content = "启动中...";

            try
            {
                await _bedrockOfflineLauncher.LaunchOfflineAsync(versionId, username);
            }
            finally
            {
                btnLaunchGame.IsEnabled = true;
                btnLaunchGame.Content = "启动游戏";
            }
        }

        private void BtnClearConsole_Click(object sender, RoutedEventArgs e)
        {
            if (txtConsole != null) txtConsole.Text = "";
        }

        private void ShowLaunchResultPopup()
        {
            var info = _gameLauncher?.LastLaunchInfo ?? new Core.GameLaunchInfo
            {
                IsSuccess = false,
                ErrorMessage = "启动失败（启动器未能捕获到具体错误信息）",
                VersionId = "",
                Username = "",
                Memory = "",
                Resolution = "",
            };

            Dispatcher.InvokeAsync(() =>
            {
                var popup = new GameLaunchResultWindow(info);
                popup.ShowDialog();
            });
        }

        private void BtnViewModDetail_Click(object sender, RoutedEventArgs e)
        {
            if (_modRecommendations.Count > 0 && _currentModIndex < _modRecommendations.Count)
            {
                AppContext.CurrentRecommendItem = _modRecommendations[_currentModIndex];
                AppContext.NavigateTo("RecommendDetail");
            }
        }

        private void BtnViewPackDetail_Click(object sender, RoutedEventArgs e)
        {
            if (_packRecommendations.Count > 0 && _currentPackIndex < _packRecommendations.Count)
            {
                AppContext.CurrentRecommendItem = _packRecommendations[_currentPackIndex];
                AppContext.NavigateTo("RecommendDetail");
            }
        }

        private void BtnViewMods_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("Mods"); }
        private void BtnViewPacks_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("Modpacks"); }
        private void BtnViewMore_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("ComponentStore"); }
        private void BtnGoToStore_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("ComponentStore"); }
        private void BtnJavaConfig_Click(object sender, RoutedEventArgs e) { AppContext.NavigateTo("JavaConfig"); }

        private async void BtnVersionType_Click(object sender, RoutedEventArgs e)
        {
            if (_isSwitchingType) return;

            if (sender is Button button)
            {
                string tag = button.Tag as string;
                if (tag == "Java")
                {
                    if (_isBedrockMode)
                    {
                        _isBedrockMode = false;
                        UpdateVersionTypeButtons(isJava: true);
                        lblVersionLabel.Text = "游戏版本";
                        LoadInstalledVersions();
                    }
                }
                else if (tag == "Bedrock")
                {
                    if (!_isBedrockMode)
                    {
                        _isBedrockMode = true;
                        UpdateVersionTypeButtons(isJava: false);
                        lblVersionLabel.Text = "选择基岩版版本";
                        await LoadBedrockVersionsAsync();
                    }
                }
            }
        }

        private void UpdateVersionTypeButtons(bool isJava)
        {
            if (isJava)
            {
                btnJavaVersion.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                btnJavaVersion.Foreground = Brushes.White;
                btnBedrockVersion.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                btnBedrockVersion.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }
            else
            {
                btnBedrockVersion.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                btnBedrockVersion.Foreground = Brushes.White;
                btnJavaVersion.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                btnJavaVersion.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }
        }

        private async Task LoadBedrockVersionsAsync()
        {
            _isSwitchingType = true;
            try
            {
                cboGameVersion.Items.Clear();
                cboGameVersion.Items.Add(new ComboBoxItem
                {
                    Content = "正在扫描本地基岩版...",
                    Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                    Foreground = Brushes.White,
                    Padding = new Thickness(12, 10, 12, 10)
                });
                cboGameVersion.SelectedIndex = 0;

                // 扫描本地已安装的基岩版版本，而非从网络获取
                var result = await VersionScanService.Instance.ScanAsync("切换到基岩版");

                cboGameVersion.Items.Clear();
                if (result.BedrockVersions.Count == 0)
                {
                    cboGameVersion.Items.Add(new ComboBoxItem
                    {
                        Content = "无已安装基岩版版本",
                        Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                        Foreground = Brushes.White,
                        Padding = new Thickness(12, 10, 12, 10)
                    });
                    cboGameVersion.SelectedIndex = 0;
                }
                else
                {
                    foreach (var v in result.BedrockVersions)
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
                cboGameVersion.Items.Clear();
                cboGameVersion.Items.Add(new ComboBoxItem
                {
                    Content = "扫描基岩版版本失败",
                    Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                    Foreground = Brushes.White,
                    Padding = new Thickness(12, 10, 12, 10)
                });
                cboGameVersion.SelectedIndex = 0;
                Logger.Error("[HomePage] 加载基岩版版本失败: " + ex.Message);
            }
            finally
            {
                _isSwitchingType = false;
            }
        }

        private void BtnResolutionConfig_Click(object sender, RoutedEventArgs e)
        {
            ModernMessageBox.ShowInfo("分辨率设置功能开发中...", "提示");
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

        private async void LoadRecommendationsAsync()
        {
            await Task.WhenAll(
                LoadModRecommendationsAsync(),
                LoadPackRecommendationsAsync()
            );
            
            StartCarousel();
        }

        private async Task LoadModRecommendationsAsync()
        {
            try
            {
                var modrinthMods = await _modrinthApi.SearchMods("", "", 6);
                foreach (var mod in modrinthMods)
                {
                    _modRecommendations.Add(new RecommendItemViewModel
                    {
                        Name = mod.Name,
                        Author = mod.Author,
                        DownloadCount = mod.DownloadCountFormatted,
                        Source = "Modrinth",
                        ProjectId = mod.Id,
                        Type = "mod",
                        Description = mod.Description,
                        IconUrl = mod.IconUrl,
                        Tags = mod.Categories != null ? string.Join(",", mod.Categories) : ""
                    });
                }
                
                if (_modRecommendations.Count > 0)
                {
                    UpdateModDisplay(_modRecommendations[0]);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[HomePage] 加载模组推荐失败: {ex.Message}");
                _modRecommendations.Add(new RecommendItemViewModel
                {
                    Name = "加载失败",
                    Source = "-",
                    Type = "mod"
                });
            }
        }

        private async Task LoadPackRecommendationsAsync()
        {
            try
            {
                var modrinthPacks = await _modrinthApi.GetModpacks(6);
                foreach (var pack in modrinthPacks)
                {
                    _packRecommendations.Add(new RecommendItemViewModel
                    {
                        Name = pack.Name,
                        Author = pack.Author,
                        DownloadCount = pack.DownloadCountFormatted,
                        Source = "Modrinth",
                        ProjectId = pack.Id,
                        Type = "modpack",
                        Description = pack.Description,
                        IconUrl = pack.IconUrl,
                        Tags = pack.Categories != null ? string.Join(",", pack.Categories) : ""
                    });
                }
                
                if (_packRecommendations.Count > 0)
                {
                    UpdatePackDisplay(_packRecommendations[0]);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[HomePage] 加载整合包推荐失败: {ex.Message}");
                _packRecommendations.Add(new RecommendItemViewModel
                {
                    Name = "加载失败",
                    Source = "-",
                    Type = "modpack"
                });
            }
        }

        private void StartCarousel()
        {
            _carouselTimer = new Timer(5000);
            _carouselTimer.Elapsed += OnCarouselTimerElapsed;
            _carouselTimer.Start();
        }

        private void OnCarouselTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_modRecommendations.Count > 1)
                {
                    _currentModIndex = (_currentModIndex + 1) % _modRecommendations.Count;
                    UpdateModDisplay(_modRecommendations[_currentModIndex]);
                }
                
                if (_packRecommendations.Count > 1)
                {
                    _currentPackIndex = (_currentPackIndex + 1) % _packRecommendations.Count;
                    UpdatePackDisplay(_packRecommendations[_currentPackIndex]);
                }
            });
        }

        private async void UpdateModDisplay(RecommendItemViewModel item)
        {
            if (spModContent != null)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                spModContent.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                
                await Task.Delay(300);
                
                if (txtModName != null)
                    txtModName.Text = item.Name;
                if (txtModSource != null)
                    txtModSource.Text = $"({item.Source})";
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                spModContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private async void UpdatePackDisplay(RecommendItemViewModel item)
        {
            if (spPackContent != null)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                spPackContent.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                
                await Task.Delay(300);
                
                if (txtPackName != null)
                    txtPackName.Text = item.Name;
                if (txtPackSource != null)
                    txtPackSource.Text = $"({item.Source})";
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                spPackContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private void RefreshAccountList()
        {
            if (cboPlayerName == null) return;

            // 记住当前选中项
            string selectedName = null;
            if (cboPlayerName.SelectedItem is ComboBoxItem prevItem && prevItem.Tag is GameAccount prevAcc)
                selectedName = prevAcc.Name;

            cboPlayerName.Items.Clear();

            var accountNames = new List<(string name, AccountType type, string uuid)>();

            // 离线账号
            foreach (var name in AccountManager.GetLegacyAccounts())
                accountNames.Add((name, AccountType.Offline, SkinServer.GenerateOfflineUuid(name)));

            // 微软账号
            foreach (var ms in AccountManager.GetMsAccounts())
            {
                var cached = AccountManager.LoadCachedMsLogin(ms.UserName);
                accountNames.Add((ms.UserName, AccountType.Microsoft, cached?.Uuid ?? ""));
            }

            // Nide 账号
            foreach (var record in AccountManager.GetServerLoginRecords("Nide"))
                accountNames.Add((record.Item1, AccountType.AuthlibInjector, ""));

            // Auth 账号
            foreach (var record in AccountManager.GetServerLoginRecords("Auth"))
                accountNames.Add((record.Item1, AccountType.AuthlibInjector, ""));

            if (accountNames.Count == 0)
            {
                AddDefaultAccountItems();
                return;
            }

            int selectedIndex = 0;
            for (int i = 0; i < accountNames.Count; i++)
            {
                var a = accountNames[i];
                var account = new GameAccount { Name = a.name, Type = a.type, Uuid = a.uuid };
                var item = CreateAccountItem(a.name, account);
                cboPlayerName.Items.Add(item);

                if (a.name == selectedName)
                    selectedIndex = i;
            }

            if (selectedIndex >= 0 && selectedIndex < cboPlayerName.Items.Count)
                cboPlayerName.SelectedIndex = selectedIndex;
            else
                cboPlayerName.SelectedIndex = 0;

            // 同步到 AppContext
            SyncAppContextFromSelection();
        }

        private void AddDefaultAccountItems()
        {
            cboPlayerName.Items.Add(CreateAccountItem("Player",
                new GameAccount { Name = "Player", Type = AccountType.Offline, Uuid = SkinServer.GenerateOfflineUuid("Player") }));
            cboPlayerName.SelectedIndex = 0;
        }

        private static ComboBoxItem CreateAccountItem(string displayText, GameAccount account)
        {
            return new ComboBoxItem
            {
                Content = displayText,
                Tag = account,
                Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                Foreground = Brushes.White,
                Padding = new Thickness(12, 10, 12, 10)
            };
        }

        private void OnAccountChanged(string name, string uuid, bool isOnline)
        {
            Dispatcher.Invoke(() =>
            {
                _username = name;
                _isOfflineMode = !isOnline;

                // 更新下拉框选中项
                for (int i = 0; i < cboPlayerName.Items.Count; i++)
                {
                    if (cboPlayerName.Items[i] is ComboBoxItem item && item.Tag is GameAccount acc && acc.Name == name)
                    {
                        cboPlayerName.SelectedIndex = i;
                        return;
                    }
                }

                // 如果列表中没找到，刷新整个列表
                RefreshAccountList();
            });
        }

        private void SyncAppContextFromSelection()
        {
            if (cboPlayerName?.SelectedItem is ComboBoxItem item && item.Tag is GameAccount account)
            {
                AppContext.Username = account.Name;
                AppContext.IsOfflineMode = account.Type == AccountType.Offline;
                AppContext.CurrentAccountUuid = account.Uuid;
            }
        }

        private void StartSkinServer()
        {
            try
            {
                _skinServer = new SkinServer(_minecraftPath);
                _skinServer.Start();
            }
            catch (Exception ex)
            {
                Logger.Warning($"[HomePage] 皮肤服务器启动失败: {ex.Message}");
            }
        }

        private void StopSkinServer()
        {
            try
            {
                _skinServer?.Stop();
                _skinServer?.Dispose();
                _skinServer = null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[HomePage] 皮肤服务器停止异常: {ex.Message}");
            }
        }
    }
}