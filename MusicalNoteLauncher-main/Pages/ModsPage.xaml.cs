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
    public partial class ModsPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private List<ModrinthMod> _modrinthMods;
        private List<CurseForgeMod> _curseForgeMods;
        private string _currentCategory = "全部";
        private readonly ConfigManager _config;

        // 版本选择相关
        private List<DownloadVersionInfo> _pendingVersions;
        private List<DownloadVersionInfo> _allVersions;
        private string _pendingResourceName;
        private string _pendingTargetDir;

        public ModsPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _config = new ConfigManager();
            LoadModsAsync();
        }

        private async Task LoadModsAsync()
        {
            await Task.WhenAll(LoadModrinthMods(), LoadCurseForgeMods());
            UpdateModList();
        }

        private async Task LoadModrinthMods()
        {
            try
            {
                _modrinthMods = await _modrinthApi.SearchMods("fabric", "", 10);
            }
            catch (Exception ex)
            {
                Logger.Error("[Mod加载] 加载Modrinth模组失败: " + ex.Message);
                _modrinthMods = new List<ModrinthMod>();
            }
        }

        private async Task LoadCurseForgeMods()
        {
            try
            {
                _curseForgeMods = await _curseForgeApi.SearchMods("fabric", 10);
            }
            catch (Exception ex)
            {
                Logger.Error("[Mod加载] 加载CurseForge模组失败: " + ex.Message);
                _curseForgeMods = new List<CurseForgeMod>();
            }
        }

        private void UpdateModList()
        {
            lbMods.Items.Clear();
            if (_modrinthMods != null)
            {
                foreach (ModrinthMod mod in _modrinthMods)
                {
                    lbMods.Items.Add(new ModItem
                    {
                        Name = mod.Name,
                        Description = mod.Description,
                        Version = mod.LatestVersion,
                        Author = mod.Author,
                        Downloads = (mod.DownloadCountFormatted ?? ""),
                        Source = "Modrinth",
                        ProjectId = (string.IsNullOrEmpty(mod.Id) ? "unknown_id" : mod.Id),
                        IconUrl = mod.IconUrl
                    });
                }
            }
            if (_curseForgeMods != null)
            {
                foreach (CurseForgeMod mod in _curseForgeMods)
                {
                    lbMods.Items.Add(new ModItem
                    {
                        Name = mod.Name,
                        Description = mod.Summary,
                        Version = mod.LatestFile?.DisplayName ?? "",
                        Author = mod.AuthorName,
                        Downloads = (mod.DownloadCountFormatted ?? ""),
                        Source = "CurseForge",
                        ProjectId = mod.Id.ToString(),
                        IconUrl = null
                    });
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        private void BtnCategory_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                _currentCategory = button.Content.ToString();
                foreach (object obj in ((StackPanel)button.Parent).Children)
                {
                    Button btn = obj as Button;
                    if (btn != null)
                    {
                        if (btn == button)
                        {
                            btn.Background = (Brush)FindResource("PrimaryBrush");
                            btn.BorderBrush = (Brush)FindResource("PrimaryDarkBrush");
                            btn.Foreground = Brushes.White;
                        }
                        else
                        {
                            btn.Background = (Brush)FindResource("CardHoverBrush");
                            btn.BorderBrush = (Brush)FindResource("BorderBrush");
                            btn.Foreground = (Brush)FindResource("TextSecondaryBrush");
                        }
                    }
                }
                LoadModsByCategory(_currentCategory);
            }
        }

        private async void LoadModsByCategory(string category)
        {
            await LoadModsAsync();
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ModItem item = button.DataContext as ModItem;
            if (item == null) return;

            try
            {
                button.IsEnabled = false;
                _pendingResourceName = item.Name;
                _pendingTargetDir = GetModsDirectory();

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

        private string GetModsDirectory()
        {
            string minecraftPath = _config.GetMinecraftPath();
            if (SettingsManager.Settings.EnableVersionIsolation && !string.IsNullOrEmpty(_config.GameVersion))
            {
                return Path.Combine(minecraftPath, "versions", _config.GameVersion, "game", "mods");
            }
            return Path.Combine(minecraftPath, "mods");
        }
    }
}
