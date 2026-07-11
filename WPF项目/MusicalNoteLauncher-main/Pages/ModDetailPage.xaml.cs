using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Controls;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Pages
{
    public partial class ModDetailPage : UserControl
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly ModrinthApiService _modrinthApi = new ModrinthApiService();
        private readonly CurseForgeApiService _curseForgeApi = new CurseForgeApiService();
        private readonly ConfigManager _config = new ConfigManager();

        private List<DownloadVersionInfo> _allVersions = new List<DownloadVersionInfo>();
        private List<DependencyMod> _dependencies = new List<DependencyMod>();

        public ModDetailPage()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                LoadModDetail();
            }
        }

        private async void LoadModDetail()
        {
            if (string.IsNullOrEmpty(ModDetailData.ProjectId) || string.IsNullOrEmpty(ModDetailData.Source))
                return;

            // 清空旧数据
            PanelVersions.Children.Clear();
            PanelDeps.Children.Clear();
            SectionDeps.Visibility = Visibility.Collapsed;
            _allVersions.Clear();
            _dependencies.Clear();

            // 显示加载状态
            LoadingOverlay.Visibility = Visibility.Visible;
            StartLoadingSpinner();

            try
            {
                // 填充基本信息
                TxtName.Text = ModDetailData.Name ?? "未知模组";
                TxtAuthor.Text = ModDetailData.Author ?? "未知作者";
                TxtSource.Text = ModDetailData.Source ?? "未知来源";
                TxtDownloads.Text = $"下载量: {ModDetailData.Downloads ?? "0"}";
                TxtPageTitle.Text = (ModDetailData.Name ?? "模组详情");
                TxtDescription.Text = string.IsNullOrEmpty(ModDetailData.Description)
                    ? "暂无描述"
                    : ModDetailData.Description;

                // 加载图标
                if (!string.IsNullOrEmpty(ModDetailData.IconUrl))
                {
                    IconBorder.Visibility = Visibility.Visible;
                    IconPlaceholder.Visibility = Visibility.Collapsed;
                    LoadIcon(ModDetailData.IconUrl);
                }
                else
                {
                    IconBorder.Visibility = Visibility.Collapsed;
                    IconPlaceholder.Visibility = Visibility.Visible;
                }

                // 加载版本列表
                LoadingText.Text = "正在加载版本列表...";
                if (ModDetailData.Source == "Modrinth")
                {
                    _allVersions = await DownloadManager.GetModrinthVersions(ModDetailData.ProjectId);
                    await ResolveModrinthDependencies();
                }
                else if (ModDetailData.Source == "CurseForge")
                {
                    if (long.TryParse(ModDetailData.ProjectId, out long cfId))
                    {
                        _allVersions = await DownloadManager.GetCurseForgeVersions(cfId);
                        await ResolveCurseForgeDependencies();
                    }
                }

                // 渲染前置模组
                if (_dependencies.Count > 0)
                {
                    SectionDeps.Visibility = Visibility.Visible;
                    RenderDependencies();
                }

                // 渲染版本列表
                RenderVersionList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModDetailPage Load Error: {ex.Message}");
                PanelVersions.Children.Add(new TextBlock
                {
                    Text = $"加载失败: {ex.Message}",
                    FontSize = 13,
                    Foreground = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                    FontFamily = new FontFamily("Microsoft YaHei")
                });
            }
            finally
            {
                StopLoadingSpinner();
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadIcon(string iconUrl)
        {
            _ = LoadIconAsync(iconUrl);
        }

        private async Task LoadIconAsync(string iconUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(iconUrl);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = new MemoryStream(bytes);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            ImgIcon.Source = bitmap;
                        }
                        catch { }
                    });
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        // 依赖解析
        // ═══════════════════════════════════════════════════════════════

        private async Task ResolveModrinthDependencies()
        {
            var depIds = new HashSet<string>();
            foreach (var v in _allVersions)
            {
                if (v.SourceData is ModrinthVersion mv && mv.Dependencies != null)
                {
                    foreach (var dep in mv.Dependencies)
                    {
                        if (dep.IsRequired && !string.IsNullOrEmpty(dep.ProjectId))
                            depIds.Add(dep.ProjectId);
                    }
                }
            }

            if (depIds.Count == 0) return;

            DepsLoadingHint.Visibility = Visibility.Visible;
            var projects = await _modrinthApi.GetProjects(depIds.ToList());

            foreach (var proj in projects)
            {
                if (proj == null) continue;
                _dependencies.Add(new DependencyMod
                {
                    ProjectId = proj.Id,
                    Name = proj.Title ?? proj.Id,
                    IconUrl = proj.IconUrl,
                    Source = "Modrinth",
                    IsRequired = true
                });
            }
            DepsLoadingHint.Visibility = Visibility.Collapsed;
        }

        private async Task ResolveCurseForgeDependencies()
        {
            var depIds = new HashSet<long>();
            foreach (var v in _allVersions)
            {
                if (v.SourceData is CurseForgeVersion cv && cv.Dependencies != null)
                {
                    foreach (var dep in cv.Dependencies)
                    {
                        if (dep.IsRequired && dep.ModId > 0)
                            depIds.Add(dep.ModId);
                    }
                }
            }

            if (depIds.Count == 0) return;

            DepsLoadingHint.Visibility = Visibility.Visible;
            var mods = await _curseForgeApi.GetMods(depIds.ToList());

            foreach (var mod in mods)
            {
                if (mod?.Data == null) continue;
                _dependencies.Add(new DependencyMod
                {
                    ProjectId = mod.Data.Id.ToString(),
                    Name = mod.Data.Name ?? mod.Data.Id.ToString(),
                    IconUrl = mod.LogoUrl,
                    Source = "CurseForge",
                    IsRequired = true
                });
            }
            DepsLoadingHint.Visibility = Visibility.Collapsed;
        }

        // ═══════════════════════════════════════════════════════════════
        // 渲染前置模组列表
        // ═══════════════════════════════════════════════════════════════

        private void RenderDependencies()
        {
            PanelDeps.Children.Clear();

            foreach (var dep in _dependencies)
            {
                var card = CreateDependencyCard(dep);
                PanelDeps.Children.Add(card);
            }
        }

        private Border CreateDependencyCard(DependencyMod dep)
        {
            var surfaceBrush = FindResource("SurfaceBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
            var textPri = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
            var textSec = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
            var borderBrush = FindResource("BorderBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
            var accentBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = surfaceBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 10, 12, 10),
                Cursor = Cursors.Hand,
                Tag = dep
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 图标
            var iconBorder = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = surfaceBrush,
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(dep.IconUrl))
            {
                var img = new Image { Width = 36, Height = 36, Stretch = Stretch.UniformToFill };
                _ = LoadDepIconAsync(img, dep.IconUrl);
                iconBorder.Child = img;
            }
            else
            {
                iconBorder.Child = new TextBlock
                {
                    Text = "?",
                    FontSize = 16,
                    Foreground = textSec,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            // 名称
            var nameBlock = new TextBlock
            {
                Text = dep.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPri,
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameBlock, 1);
            grid.Children.Add(nameBlock);

            // 来源标签
            var sourceTag = new TextBlock
            {
                Text = dep.Source,
                FontSize = 11,
                Foreground = textSec,
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };
            Grid.SetColumn(sourceTag, 2);
            grid.Children.Add(sourceTag);

            border.Child = grid;

            // 让卡片本身处理点击，子元素不拦截
            grid.IsHitTestVisible = false;

            // 点击前置模组跳转到其详情页（用 MouseLeftButtonDown 保证可靠触发）
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (dep == null) return;
                string currentBackPage = ModDetailData.BackPage ?? "ModsPage";

                ModDetailData.ProjectId = dep.ProjectId;
                ModDetailData.Source = dep.Source;
                ModDetailData.Name = dep.Name;
                ModDetailData.IconUrl = dep.IconUrl;
                ModDetailData.Author = null;
                ModDetailData.Description = null;
                ModDetailData.Downloads = null;
                ModDetailData.BackPage = currentBackPage;
                ModDetailData.MinecraftPath = ModDetailData.MinecraftPath;
                ModDetailData.CurrentGameVersion = ModDetailData.CurrentGameVersion;
                ModDetailData.InstalledVersions = ModDetailData.InstalledVersions;
                ModDetailData.TargetSubDir = ModDetailData.TargetSubDir;
                AppContext.NavigateTo("ModDetail");
            };

            // 悬停效果
            var cardBg = FindResource("CardBackgroundBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
            border.MouseEnter += (s, e) =>
            {
                border.Background = cardBg;
                border.BorderBrush = accentBrush;
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = surfaceBrush;
                border.BorderBrush = borderBrush;
            };

            return border;
        }

        private async Task LoadDepIconAsync(Image img, string iconUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(iconUrl);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = new MemoryStream(bytes);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            img.Source = bitmap;
                        }
                        catch { }
                    });
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        // 渲染版本列表
        // ═══════════════════════════════════════════════════════════════

        private void RenderVersionList()
        {
            PanelVersions.Children.Clear();

            if (_allVersions.Count == 0)
            {
                PanelVersions.Children.Add(new TextBlock
                {
                    Text = "未找到可用的版本",
                    FontSize = 13,
                    Foreground = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    Margin = new Thickness(0, 8, 0, 0)
                });
                return;
            }

            foreach (var version in _allVersions)
            {
                var card = CreateVersionCard(version);
                PanelVersions.Children.Add(card);
            }
        }

        private Border CreateVersionCard(DownloadVersionInfo version)
        {
            var surfaceBrush = FindResource("SurfaceBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
            var textPri = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
            var textSec = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
            var borderBrush = FindResource("BorderBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
            var greenBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0xBB, 0x8E));
            var orangeBrush = new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12));

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = surfaceBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(14, 10, 14, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 左侧：版本信息
            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // 版本名 + 类型标签
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            var nameBlock = new TextBlock
            {
                Text = version.VersionName,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPri,
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(nameBlock);

            if (version.IsRecommended)
            {
                var recTag = new Border
                {
                    Background = greenBrush,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                recTag.Child = new TextBlock
                {
                    Text = "推荐",
                    FontSize = 10,
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Microsoft YaHei")
                };
                titleRow.Children.Add(recTag);
            }

            if (!string.IsNullOrEmpty(version.VersionType) && version.VersionType != "正式版")
            {
                var typeTag = new Border
                {
                    Background = orangeBrush,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                typeTag.Child = new TextBlock
                {
                    Text = version.VersionType,
                    FontSize = 10,
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Microsoft YaHei")
                };
                titleRow.Children.Add(typeTag);
            }
            infoPanel.Children.Add(titleRow);

            // 详细信息
            infoPanel.Children.Add(new TextBlock
            {
                Text = version.DisplayInfo,
                FontSize = 12,
                Foreground = textSec,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // 文件大小
            if (version.FileSize > 0)
            {
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"大小: {FormatFileSize(version.FileSize)}",
                    FontSize = 11,
                    Foreground = textSec,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            Grid.SetColumn(infoPanel, 0);
            grid.Children.Add(infoPanel);

            // 右侧：下载按钮
            var primaryBrush = FindResource("PrimaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
            var primaryDarkBrush = FindResource("PrimaryDarkBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2));
            var downloadBtn = new Button
            {
                Content = "下载",
                FontSize = 13,
                Background = primaryBrush,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = version
            };
            // 圆角模板
            var btnBg = primaryBrush;
            var btnHoverBg = primaryDarkBrush;
            var btnTemplate = new ControlTemplate(typeof(Button));
            var btnBorder = new FrameworkElementFactory(typeof(Border));
            btnBorder.Name = "border";
            btnBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            btnBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            btnBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            btnBorder.SetValue(Border.PaddingProperty, new Thickness(20, 8, 20, 8));
            btnBorder.SetValue(Border.SnapsToDevicePixelsProperty, true);
            btnBorder.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            btnTemplate.VisualTree = btnBorder;
            // 悬停和按下触发器
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, btnHoverBg, "border"));
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, btnHoverBg, "border"));
            btnTemplate.Triggers.Add(hoverTrigger);
            btnTemplate.Triggers.Add(pressedTrigger);
            downloadBtn.Template = btnTemplate;
            downloadBtn.Click += BtnVersionDownload_Click;
            Grid.SetColumn(downloadBtn, 1);
            grid.Children.Add(downloadBtn);

            border.Child = grid;
            return border;
        }

        private void BtnVersionDownload_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is not DownloadVersionInfo version) return;

            string resourceName = ModDetailData.Name ?? "未知模组";
            string minecraftPath = ModDetailData.MinecraftPath ?? _config.GetMinecraftPath();
            string targetDir = GetTargetDirectory();

            string defaultFileName = version.FileName;
            if (string.IsNullOrEmpty(defaultFileName))
                defaultFileName = $"{resourceName}_{DateTime.Now:yyyyMMddHHmmss}.jar";
            foreach (char c in Path.GetInvalidFileNameChars())
                defaultFileName = defaultFileName.Replace(c, '_');

            var dialog = new DownloadConfirmDialog(
                resourceName,
                targetDir,
                defaultFileName,
                minecraftPath,
                ModDetailData.InstalledVersions ?? GetInstalledVersions(minecraftPath),
                ModDetailData.CurrentGameVersion ?? _config.GameVersion
            );

            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                string savePath = dialog.SavePath;
                if (!string.IsNullOrEmpty(savePath))
                {
                    bool success = DownloadManager.AddDownloadTask(resourceName, savePath, version, dialog.FileName);
                    if (success)
                    {
                        string versionInfo = !string.IsNullOrEmpty(version.VersionName)
                            ? $"（版本: {version.VersionName}）"
                            : "";
                        ModernMessageBox.ShowInfo(
                            $"已将 {resourceName}{versionInfo} 添加到下载任务", "提示");
                    }
                }
            }
        }

        private string GetTargetDirectory()
        {
            string minecraftPath = ModDetailData.MinecraftPath ?? _config.GetMinecraftPath();
            string currentVersion = ModDetailData.CurrentGameVersion ?? _config.GameVersion;

            if (!string.IsNullOrEmpty(currentVersion)
                && SettingsManager.Settings.ShouldIsolateVersionForVersion(minecraftPath, currentVersion))
                return Path.Combine(minecraftPath, "versions", currentVersion, "game", ModDetailData.TargetSubDir);

            return Path.Combine(minecraftPath, ModDetailData.TargetSubDir);
        }

        private List<string> GetInstalledVersions(string minecraftPath)
        {
            var versions = new List<string>();
            var versionsPath = Path.Combine(minecraftPath, "versions");
            if (Directory.Exists(versionsPath))
            {
                foreach (string dir in Directory.GetDirectories(versionsPath))
                {
                    string name = Path.GetFileName(dir);
                    if (File.Exists(Path.Combine(dir, $"{name}.json")))
                        versions.Add(name);
                }
            }
            return versions;
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes >= 1048576)
                return $"{(bytes / 1048576.0):0.##} MB";
            if (bytes >= 1024)
                return $"{(bytes / 1024.0):0.##} KB";
            return $"{bytes} B";
        }

        // ═══════════════════════════════════════════════════════════════
        // 加载动画
        // ═══════════════════════════════════════════════════════════════

        private System.Windows.Media.Animation.Storyboard _spinnerStoryboard;

        private void StartLoadingSpinner()
        {
            _spinnerStoryboard = new System.Windows.Media.Animation.Storyboard();
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(1),
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            System.Windows.Media.Animation.Storyboard.SetTarget(animation, SpinnerRotate);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(animation,
                new PropertyPath(System.Windows.Media.RotateTransform.AngleProperty));
            _spinnerStoryboard.Children.Add(animation);
            _spinnerStoryboard.Begin();
        }

        private void StopLoadingSpinner()
        {
            _spinnerStoryboard?.Stop();
            _spinnerStoryboard = null;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            string backPage = ModDetailData.BackPage ?? "ModsPage";
            AppContext.NavigateTo(backPage);
        }
    }
}
