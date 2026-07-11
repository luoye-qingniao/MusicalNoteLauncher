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
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Pages
{
    public partial class DependenciesPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private readonly ConfigManager _config;

        // 搜索相关
        private List<DependencyItem> _allDependencyItems = new List<DependencyItem>();

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
            _allDependencyItems.Clear();
            _allDependencyItems.Add(new DependencyItem
            {
                Name = "Forge",
                IconUrl = "https://files.minecraftforge.net/images/logo.png",
                Description = "最流行的Mod加载器，支持广泛的Mod生态系统",
                Version = "1.20.1-47.2.14",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/"
            });
            _allDependencyItems.Add(new DependencyItem
            {
                Name = "Fabric",
                IconUrl = "https://fabricmc.net/assets/logo.png",
                Description = "轻量级Mod加载器，启动更快，Mod更小",
                Version = "0.15.10",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://fabricmc.net/use/"
            });
            _allDependencyItems.Add(new DependencyItem
            {
                Name = "Quilt",
                IconUrl = "https://quiltmc.org/assets/brand/logo.svg",
                Description = "Fabric的社区驱动分支，支持更多现代特性",
                Version = "0.19.2",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://quiltmc.org/install/"
            });
            _allDependencyItems.Add(new DependencyItem
            {
                Name = "NeoForge",
                IconUrl = "https://neoforged.net/images/logo.png",
                Description = "Forge的现代分支，支持最新游戏版本",
                Version = "1.20.1-62.0.2",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://neoforged.net/downloads/"
            });
            _allDependencyItems.Add(new DependencyItem
            {
                Name = "OptiFine",
                IconUrl = "https://optifine.net/logo.png",
                Description = "性能优化和光影支持，提升游戏帧率",
                Version = "HD U_G8",
                Type = "Optimization",
                Compatible = "1.20.x",
                SourceUrl = "https://optifine.net/downloads"
            });
            _allDependencyItems.Add(new DependencyItem
            {
                Name = "Sodium",
                IconUrl = "https://cdn.modrinth.com/data/AANobbMI/icon.png",
                Description = "Fabric端的性能优化Mod，大幅提升帧率",
                Version = "0.5.2",
                Type = "Optimization",
                Compatible = "1.20.x (Fabric)",
                SourceUrl = "https://modrinth.com/mod/sodium"
            });
            _allDependencyItems.Add(new DependencyItem
            {
                Name = "Lithium",
                IconUrl = "https://cdn.modrinth.com/data/gvQqBUqZ/icon.png",
                Description = "Fabric端的游戏逻辑优化，提升整体性能",
                Version = "0.11.2",
                Type = "Optimization",
                Compatible = "1.20.x (Fabric)",
                SourceUrl = "https://modrinth.com/mod/lithium"
            });
            _allDependencyItems.Add(new DependencyItem
            {
                Name = "Iris",
                IconUrl = "https://cdn.modrinth.com/data/YL57xq9U/icon.png",
                Description = "Fabric端的光影支持Mod，兼容OptiFine光影",
                Version = "1.6.4",
                Type = "Graphics",
                Compatible = "1.20.x (Fabric)",
                SourceUrl = "https://modrinth.com/mod/iris"
            });
            ApplySearch();
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
                    string resourceName = item.Name;
                    string targetDir = Path.Combine(_config.GetMinecraftPath(), "mods");

                    List<DownloadVersionInfo> versions = await DownloadManager.GetModrinthVersions(modrinthId);

                    button.IsEnabled = true;

                    if (versions == null || versions.Count == 0)
                    {
                        ModernMessageBox.ShowInfo($"[{resourceName}] 未找到可用的下载版本！", "提示");
                        return;
                    }

                    if (versions.Count == 1)
                    {
                        bool success = DownloadManager.AddDownloadTask(resourceName,
                            DownloadManager.BuildSavePath(resourceName, targetDir, versions[0]), versions[0]);
                        if (success)
                            ModernMessageBox.ShowInfo($"[{resourceName}] 已添加到下载任务！", "提示");
                        return;
                    }

                    // 多个版本 → 导航到专用版本选择页面
                    VersionSelectData.Versions = versions;
                    VersionSelectData.ResourceName = resourceName;
                    VersionSelectData.TargetDir = targetDir;
                    VersionSelectData.OnVersionSelected = null;
                    VersionSelectData.BackPage = "Dependencies";
                    VersionSelectData.MinecraftPath = _config.GetMinecraftPath();
                    VersionSelectData.CurrentGameVersion = _config.GameVersion;
                    VersionSelectData.InstalledVersions = GetInstalledVersions();
                    AppContext.NavigateTo("VersionSelect");
                    return;
                }
                catch (Exception ex)
                {
                    button.IsEnabled = true;
                    Logger.Error($"[下载] 获取版本失败: {ex.Message}");
                    ModernMessageBox.ShowError($"获取版本列表失败:\n{ex.Message}", "错误");
                    return;
                }
            }

            if (!string.IsNullOrEmpty(item.SourceUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.SourceUrl)
                {
                    UseShellExecute = true
                });
                ModernMessageBox.ShowInfo("已在浏览器中打开下载页面:\n" + item.Name, "下载提示");
            }
            else
            {
                ModernMessageBox.ShowWarning("未找到下载链接", "提示");
            }
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

        private List<string> GetInstalledVersions()
        {
            var versions = new List<string>();
            string minecraftPath = _config.GetMinecraftPath();
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

        // ═══════════════════════════════════════════════════════════════
        // 搜索功能
        // ═══════════════════════════════════════════════════════════════

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSearchHint();
            ApplySearch();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplySearch();
        }

        private void BtnSearchClear_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            txtSearch.Focus();
        }

        private void UpdateSearchHint()
        {
            bool isEmpty = string.IsNullOrEmpty(txtSearch.Text);
            txtSearchHint.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            btnSearchClear.Opacity = isEmpty ? 0 : 1;
            btnSearchClear.IsHitTestVisible = !isEmpty;
        }

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySearch();
        }

        private void BtnSearchRun_Click(object sender, RoutedEventArgs e)
        {
            ApplySearch();
        }

        private void BtnSearchReset_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            ((ComboBoxItem)cmbType.Items[0]).IsSelected = true;
            ApplySearch();
        }

        private string GetSelectedTag(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag != null)
                return item.Tag.ToString();
            return "";
        }

        private void ApplySearch()
        {
            if (lbDependencies == null) return;
            lbDependencies.Items.Clear();

            string query = txtSearch.Text?.Trim() ?? "";
            string typeFilter = GetSelectedTag(cmbType);

            IEnumerable<DependencyItem> filtered = _allDependencyItems;

            if (!string.IsNullOrEmpty(typeFilter))
            {
                filtered = filtered.Where(m =>
                    string.Equals(m.Type, typeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(m =>
                    SearchHelper.IsMatch(query, m.Name, m.Description, m.Type, m.Version));
            }

            foreach (var item in filtered)
                lbDependencies.Items.Add(item);
        }
    }
}
