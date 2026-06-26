using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Pages
{
    public partial class ModpacksPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private readonly ConfigManager _config;
        private List<ModrinthMod> _modrinthModpacks = new List<ModrinthMod>();
        private List<CurseForgeMod> _curseForgeModpacks = new List<CurseForgeMod>();
        private int _currentPage;
        private const int PageSize = 20;
        private readonly string _modpacksPath;

        // 版本选择相关
        private List<DownloadVersionInfo> _pendingVersions;
        private List<DownloadVersionInfo> _allVersions;
        private string _pendingResourceName;
        private string _pendingTargetDir;
        private CancellationTokenSource _installCts;

        public ModpacksPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _config = new ConfigManager();
            _modpacksPath = Path.Combine(_config.GetMinecraftPath(), "modpacks");
            LoadModpacksAsync();
        }

        private async void LoadModpacksAsync()
        {
            await Task.WhenAll(LoadModrinthModpacks(), LoadCurseForgeModpacks());
            UpdateModpackList();
        }

        private async Task LoadModrinthModpacks()
        {
            try
            {
                _modrinthModpacks = await _modrinthApi.SearchMods("modpack", "", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[整合包加载] 加载Modrinth整合包失败: " + ex.Message);
                _modrinthModpacks = new List<ModrinthMod>();
            }
        }

        private async Task LoadCurseForgeModpacks()
        {
            try
            {
                _curseForgeModpacks = await _curseForgeApi.SearchMods("modpack", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[整合包加载] 加载CurseForge整合包失败: " + ex.Message);
                _curseForgeModpacks = new List<CurseForgeMod>();
            }
        }

        private void UpdateModpackList()
        {
            lbModpacks.Items.Clear();
            int count = (_currentPage + 1) * PageSize;
            if (_modrinthModpacks != null)
            {
                foreach (ModrinthMod mod in _modrinthModpacks.Take(count))
                {
                    lbModpacks.Items.Add(new ModpackItem
                    {
                        Name = mod.Name,
                        IconUrl = mod.IconUrl,
                        Description = mod.Description,
                        Version = mod.LatestVersion,
                        Author = mod.Author,
                        Downloads = mod.DownloadCountFormatted + " 下载",
                        Source = "Modrinth",
                        ProjectId = mod.Id
                    });
                }
            }
            if (_curseForgeModpacks != null)
            {
                foreach (CurseForgeMod mod in _curseForgeModpacks.Take(count))
                {
                    lbModpacks.Items.Add(new ModpackItem
                    {
                        Name = mod.Name,
                        IconUrl = mod.LogoUrl,
                        Description = mod.Summary,
                        Version = mod.LatestFile?.DisplayName ?? "",
                        Author = mod.AuthorName,
                        Downloads = mod.DownloadCountFormatted + " 下载",
                        Source = "CurseForge",
                        ProjectId = mod.Id.ToString()
                    });
                }
            }
        }

        private void BtnLoadMore_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            LoadModpacksAsync();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ModpackItem item = button.DataContext as ModpackItem;
            if (item == null) return;

            try
            {
                button.IsEnabled = false;
                _pendingResourceName = item.Name;
                _pendingTargetDir = _modpacksPath;

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

                // 单版本直接安装
                if (versions.Count == 1)
                {
                    HideVersionPanel();
                    await DownloadAndInstallModpack(versions[0], showDoneMessage: true);
                    return;
                }

                VersionTitle.Text = $"选择版本 - {_pendingResourceName}";
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

        private async void VersionItemDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DownloadVersionInfo version)
            {
                HideVersionPanel();
                await DownloadAndInstallModpack(version, showDoneMessage: true);
            }
        }

        private async void VersionListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (VersionListView.SelectedItem is DownloadVersionInfo version)
            {
                HideVersionPanel();
                await DownloadAndInstallModpack(version, showDoneMessage: true);
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

        // ═══════════════════════════════════════════════════════════════
        // 下载安装整合包（下载 → 解压 → 安装加载器 → 下载模组 → 创建版本）
        // ═══════════════════════════════════════════════════════════════

        private async Task DownloadAndInstallModpack(DownloadVersionInfo version, bool showDoneMessage = true)
        {
            if (version == null) return;

            ShowInstallProgress();
            _installCts = new CancellationTokenSource();
            var ct = _installCts.Token;

            try
            {
                // 步骤1：下载 .mrpack 文件
                UpdateInstallProgress("正在下载整合包...", 5);
                string downloadPath = Path.Combine(_modpacksPath, version.FileName ?? $"{_pendingResourceName}.mrpack");
                Directory.CreateDirectory(_modpacksPath);

                bool downloadSuccess = await DownloadFileWithProgressAsync(version.DownloadUrl, downloadPath, 5, 25, ct);
                if (!downloadSuccess)
                {
                    HideInstallProgress();
                    MessageBox.Show($"下载 {_pendingResourceName} 失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 步骤2：安装整合包
                UpdateInstallProgress("正在解压并安装整合包...", 25);
                var installer = new ModpackInstaller(_config.GetMinecraftPath());
                installer.StatusChanged += status =>
                {
                    Dispatcher.Invoke(() => UpdateInstallProgress(status, -1));
                };
                installer.ProgressChanged += progress =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        int mapped = 25 + (int)(progress * 0.70);
                        UpdateInstallProgress(null, mapped);
                    });
                };

                var result = await installer.InstallFromMrpackAsync(downloadPath, _pendingResourceName, ct);
                if (!result.Success)
                {
                    HideInstallProgress();
                    MessageBox.Show($"安装 {_pendingResourceName} 失败！\n{result.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 步骤3：清理下载的 .mrpack 文件
                try { File.Delete(downloadPath); } catch { }

                // 完成
                HideInstallProgress();

                if (showDoneMessage)
                {
                    MessageBox.Show(
                        $"整合包「{_pendingResourceName}」安装完成！\n\n" +
                        $"版本名: {result.VersionId}\n" +
                        $"Minecraft: {result.MinecraftVersion} | 加载器: {result.LoaderType}\n\n" +
                        $"可在主页版本列表中选择并启动。",
                        "安装成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                HideInstallProgress();
                MessageBox.Show($"{_pendingResourceName} 安装已取消", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HideInstallProgress();
                Logger.Error($"[安装] 整合包安装失败: {ex.Message}");
                MessageBox.Show($"操作失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _installCts?.Dispose();
                _installCts = null;
            }
        }

        private async Task<bool> DownloadFileWithProgressAsync(string url, string savePath,
            int progressStart, int progressEnd, CancellationToken ct)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("MusicalNoteLauncher/1.0");

                    var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!response.IsSuccessStatusCode) return false;

                    long totalBytes = response.Content.Headers.ContentLength ?? -1;
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        byte[] buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                int progress = progressStart + (int)((double)totalRead / totalBytes * (progressEnd - progressStart));
                                Dispatcher.Invoke(() => UpdateInstallProgress(
                                    $"下载中... ({FormatFileSize(totalRead)} / {FormatFileSize(totalBytes)})", progress));
                            }
                        }
                    }
                }
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                Logger.Error($"[安装] 下载文件失败: {ex.Message}");
                return false;
            }
        }

        private void ShowInstallProgress()
        {
            InstallProgressOverlay.Visibility = Visibility.Visible;
            InstallProgressBar.Value = 0;
            InstallPercentText.Text = "0%";
            InstallStatusText.Text = "准备中...";
            InstallCancelBtn.IsEnabled = true;
        }

        private void HideInstallProgress()
        {
            InstallProgressOverlay.Visibility = Visibility.Collapsed;
        }

        private void UpdateInstallProgress(string status, int percent)
        {
            if (!string.IsNullOrEmpty(status))
                InstallStatusText.Text = status;

            if (percent >= 0)
            {
                InstallProgressBar.Value = percent;
                InstallPercentText.Text = $"{percent}%";
            }
        }

        private void InstallCancelBtn_Click(object sender, RoutedEventArgs e)
        {
            InstallCancelBtn.IsEnabled = false;
            InstallStatusText.Text = "正在取消...";
            _installCts?.Cancel();
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
