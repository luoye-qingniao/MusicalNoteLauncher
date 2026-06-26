using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Pages
{
    public partial class DependenciesPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private readonly ConfigManager _config;

        // 版本选择相关
        private List<DownloadVersionInfo> _pendingVersions;
        private List<DownloadVersionInfo> _allVersions;
        private string _pendingResourceName;
        private string _pendingTargetDir;

        public DependenciesPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _config = new ConfigManager();
            LoadDependencies();
        }

        private void LoadDependencies()
        {
            lbDependencies.Items.Clear();
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Forge",
                IconUrl = "https://files.minecraftforge.net/images/logo.png",
                Description = "最流行的Mod加载器，支持广泛的Mod生态系统",
                Version = "1.20.1-47.2.14",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Fabric",
                IconUrl = "https://fabricmc.net/assets/logo.png",
                Description = "轻量级Mod加载器，启动更快，Mod更小",
                Version = "0.15.10",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://fabricmc.net/use/"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Quilt",
                IconUrl = "https://quiltmc.org/assets/brand/logo.svg",
                Description = "Fabric的社区驱动分支，支持更多现代特性",
                Version = "0.19.2",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://quiltmc.org/install/"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "NeoForge",
                IconUrl = "https://neoforged.net/images/logo.png",
                Description = "Forge的现代分支，支持最新游戏版本",
                Version = "1.20.1-62.0.2",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://neoforged.net/downloads/"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "OptiFine",
                IconUrl = "https://optifine.net/logo.png",
                Description = "性能优化和光影支持，提升游戏帧率",
                Version = "HD U_G8",
                Type = "Optimization",
                Compatible = "1.20.x",
                SourceUrl = "https://optifine.net/downloads"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Sodium",
                IconUrl = "https://cdn.modrinth.com/data/AANobbMI/icon.png",
                Description = "Fabric端的性能优化Mod，大幅提升帧率",
                Version = "0.5.2",
                Type = "Optimization",
                Compatible = "1.20.x (Fabric)",
                SourceUrl = "https://modrinth.com/mod/sodium"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Lithium",
                IconUrl = "https://cdn.modrinth.com/data/gvQqBUqZ/icon.png",
                Description = "Fabric端的游戏逻辑优化，提升整体性能",
                Version = "0.11.2",
                Type = "Optimization",
                Compatible = "1.20.x (Fabric)",
                SourceUrl = "https://modrinth.com/mod/lithium"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Iris",
                IconUrl = "https://cdn.modrinth.com/data/YL57xq9U/icon.png",
                Description = "Fabric端的光影支持Mod，兼容OptiFine光影",
                Version = "1.6.4",
                Type = "Graphics",
                Compatible = "1.20.x (Fabric)",
                SourceUrl = "https://modrinth.com/mod/iris"
            });
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        private void BtnLoadMore_Click(object sender, RoutedEventArgs e)
        {
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            DependencyItem item = button.DataContext as DependencyItem;
            if (item == null) return;

            string modrinthId = GetModrinthId(item.Name);
            if (!string.IsNullOrEmpty(modrinthId))
            {
                try
                {
                    button.IsEnabled = false;
                    _pendingResourceName = item.Name;
                    _pendingTargetDir = Path.Combine(_config.GetMinecraftPath(), "mods");

                    // 先显示面板（加载状态）
                    ShowVersionPanelLoading();

                    List<DownloadVersionInfo> versions = await DownloadManager.GetModrinthVersions(modrinthId);

                    button.IsEnabled = true;

                    if (versions == null || versions.Count == 0)
                    {
                        HideVersionPanel();
                        MessageBox.Show($"[{_pendingResourceName}] 未找到可用的下载版本！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    if (versions.Count == 1)
                    {
                        HideVersionPanel();
                        bool success = DownloadManager.AddDownloadTask(_pendingResourceName, _pendingTargetDir, versions[0]);
                        if (success)
                            MessageBox.Show($"[{_pendingResourceName}] 已添加到下载任务！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    PopulateVersionPanel(versions);
                    return;
                }
                catch (Exception ex)
                {
                    button.IsEnabled = true;
                    HideVersionPanel();
                    Logger.Error($"[下载] 获取版本失败: {ex.Message}");
                    MessageBox.Show($"获取版本列表失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(item.SourceUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.SourceUrl)
                {
                    UseShellExecute = true
                });
                MessageBox.Show("已在浏览器中打开下载页面:\n" + item.Name, "下载提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            else
            {
                MessageBox.Show("未找到下载链接", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void ShowVersionPanelLoading()
        {
            _pendingVersions = null;
            _allVersions = null;
            VersionTitle.Text = $"选择下载版本 - {_pendingResourceName}";
            VersionCountText.Text = "正在加载版本列表...";
            VersionListView.ItemsSource = null;
            VersionFilterPanel.Children.Clear();
            VersionPanel.Visibility = Visibility.Visible;
        }

        private void PopulateVersionPanel(List<DownloadVersionInfo> versions)
        {
            _pendingVersions = versions;
            _allVersions = versions;
            VersionTitle.Text = $"选择下载版本 - {_pendingResourceName}";
            VersionCountText.Text = $"共 {versions.Count} 个版本";
            VersionListView.ItemsSource = versions;
            SetupVersionFilters(versions);

            VersionListView.SelectedIndex = 0;
            for (int i = 0; i < versions.Count; i++)
            {
                if (versions[i].IsRecommended)
                {
                    VersionListView.SelectedIndex = i;
                    break;
                }
            }
            VersionListView.ScrollIntoView(VersionListView.SelectedItem);
        }

        private void HideVersionPanel()
        {
            VersionPanel.Visibility = Visibility.Collapsed;
            _pendingVersions = null;
        }

        private void VersionBackBtn_Click(object sender, RoutedEventArgs e)
        {
            HideVersionPanel();
        }

        private void VersionListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (VersionListView.SelectedItem is DownloadVersionInfo version)
            {
                ConfirmDownload(version);
            }
        }

        private void VersionItemDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DownloadVersionInfo version)
            {
                ConfirmDownload(version);
            }
        }

        private void ConfirmDownload(DownloadVersionInfo version)
        {
            if (version == null) return;

            bool success = DownloadManager.AddDownloadTask(_pendingResourceName, _pendingTargetDir, version);
            if (success)
            {
                HideVersionPanel();
                MessageBox.Show($"[{_pendingResourceName}] 已添加到下载任务！（版本: {version.VersionName}）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SetupVersionFilters(List<DownloadVersionInfo> versions)
        {
            VersionFilterPanel.Children.Clear();
            var gameVersions = new HashSet<string>();
            foreach (var v in versions)
            {
                var gvs = ExtractGameVersions(v.DisplayInfo);
                foreach (var gv in gvs)
                    gameVersions.Add(gv);
            }
            var sorted = gameVersions.OrderByDescending(v =>
            {
                var parts = v.Split('.');
                long major = parts.Length > 0 && long.TryParse(parts[0], out long m) ? m : 0;
                long minor = parts.Length > 1 && long.TryParse(parts[1], out long n) ? n : 0;
                long patch = parts.Length > 2 && long.TryParse(parts[2], out long p) ? p : 0;
                return major * 1000000 + minor * 1000 + patch;
            }).ToList();

            AddFilterButton("全部", true);
            AddFilterButton("⭐", false);
            foreach (var gv in sorted.Take(10))
                AddFilterButton(gv, false);
        }

        private void AddFilterButton(string text, bool isActive)
        {
            var btn = new Button
            {
                Content = text,
                Padding = new Thickness(14, 5, 14, 5),
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand,
                Background = isActive
                    ? (Brush)FindResource("PrimaryBrush")
                    : (Brush)FindResource("CardHoverBrush"),
                Foreground = isActive ? Brushes.White
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBBBBB")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444")),
                BorderThickness = new Thickness(1)
            };
            btn.Click += VersionFilter_Click;
            VersionFilterPanel.Children.Add(btn);
        }

        private void VersionFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            string filter = btn.Content.ToString();

            foreach (UIElement child in VersionFilterPanel.Children)
            {
                if (child is Button b)
                {
                    bool active = (b == btn);
                    b.Background = active
                        ? (Brush)FindResource("PrimaryBrush")
                        : (Brush)FindResource("CardHoverBrush");
                    b.Foreground = active ? Brushes.White
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBBBBB"));
                }
            }

            List<DownloadVersionInfo> filtered;
            if (filter == "全部")
                filtered = _allVersions;
            else if (filter == "⭐")
                filtered = _allVersions.Where(v => v.IsRecommended).ToList();
            else
                filtered = _allVersions.Where(v => ExtractGameVersions(v.DisplayInfo).Contains(filter)).ToList();

            VersionListView.ItemsSource = filtered;
            UpdateVersionCount(filtered.Count);
        }

        private List<string> ExtractGameVersions(string displayInfo)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(displayInfo)) return result;
            var prefix = "支持版本: ";
            var idx = displayInfo.IndexOf(prefix);
            if (idx < 0) return result;
            var start = idx + prefix.Length;
            var end = displayInfo.IndexOf(" |", start);
            var gvPart = end > 0 ? displayInfo.Substring(start, end - start) : displayInfo.Substring(start);
            foreach (var gv in gvPart.Split(','))
            {
                var trimmed = gv.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }
            return result;
        }

        private void UpdateVersionCount(int count)
        {
            VersionCountText.Text = $"共 {count} 个版本";
        }

        private string GetModrinthId(string name)
        {
            return name switch
            {
                "Sodium" => "AANobbMI",
                "Lithium" => "gvQqBUqZ",
                "Iris" => "YL57xq9U",
                _ => null
            };
        }
    }
}
