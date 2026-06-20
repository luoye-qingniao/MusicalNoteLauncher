using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Pages
{
    public partial class DatapacksPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private readonly ConfigManager _config;
        private List<ModrinthMod> _modrinthDatapacks = new List<ModrinthMod>();
        private List<CurseForgeMod> _curseForgeDatapacks = new List<CurseForgeMod>();
        private int _currentPage;
        private const int PageSize = 20;
        private readonly string _datapacksPath;

        // 版本选择相关
        private List<DownloadVersionInfo> _pendingVersions;
        private List<DownloadVersionInfo> _allVersions;
        private string _pendingResourceName;
        private string _pendingTargetDir;

        public DatapacksPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _config = new ConfigManager();
            _datapacksPath = Path.Combine(_config.GetMinecraftPath(), "datapacks");
            LoadDatapacksAsync();
        }

        private async void LoadDatapacksAsync()
        {
            await Task.WhenAll(LoadModrinthDatapacks(), LoadCurseForgeDatapacks());
            UpdateDatapackList();
        }

        private async Task LoadModrinthDatapacks()
        {
            try
            {
                _modrinthDatapacks = await _modrinthApi.SearchMods("datapack", "", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[数据包加载] 加载Modrinth数据包失败: " + ex.Message);
                _modrinthDatapacks = new List<ModrinthMod>();
            }
        }

        private async Task LoadCurseForgeDatapacks()
        {
            try
            {
                _curseForgeDatapacks = await _curseForgeApi.SearchMods("datapack", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[数据包加载] 加载CurseForge数据包失败: " + ex.Message);
                _curseForgeDatapacks = new List<CurseForgeMod>();
            }
        }

        private void UpdateDatapackList()
        {
            lbDatapacks.Items.Clear();
            int count = (_currentPage + 1) * PageSize;
            if (_modrinthDatapacks != null)
            {
                foreach (ModrinthMod mod in _modrinthDatapacks.Take(count))
                {
                    lbDatapacks.Items.Add(new DatapackItem
                    {
                        Name = mod.Name,
                        IconUrl = mod.IconUrl,
                        Description = mod.Description,
                        Version = mod.LatestVersion,
                        Author = mod.Author,
                        Compatible = "1.20.x",
                        Source = "Modrinth",
                        ProjectId = mod.Id
                    });
                }
            }
            if (_curseForgeDatapacks != null)
            {
                foreach (CurseForgeMod mod in _curseForgeDatapacks.Take(count))
                {
                    lbDatapacks.Items.Add(new DatapackItem
                    {
                        Name = mod.Name,
                        IconUrl = mod.LogoUrl,
                        Description = mod.Summary,
                        Version = mod.LatestFile?.DisplayName ?? "",
                        Author = mod.AuthorName,
                        Compatible = "1.20.x",
                        Source = "CurseForge",
                        ProjectId = mod.Id.ToString()
                    });
                }
            }
        }

        private void BtnLoadMore_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            LoadDatapacksAsync();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            DatapackItem item = button.DataContext as DatapackItem;
            if (item == null) return;

            try
            {
                button.IsEnabled = false;
                _pendingResourceName = item.Name;
                _pendingTargetDir = _datapacksPath;

                // 先显示面板（加载状态）
                ShowVersionPanelLoading();

                List<DownloadVersionInfo> versions = null;

                if (item.Source == "Modrinth")
                {
                    versions = await DownloadManager.GetModrinthVersions(item.ProjectId);
                }
                else if (item.Source == "CurseForge" && long.TryParse(item.ProjectId, out long modId))
                {
                    versions = await DownloadManager.GetCurseForgeVersions(modId);
                }

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
            }
            catch (Exception ex)
            {
                button.IsEnabled = true;
                HideVersionPanel();
                Logger.Error($"[下载] 获取版本失败: {ex.Message}");
                MessageBox.Show($"获取版本列表失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D")),
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
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D"));
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
    }
}
