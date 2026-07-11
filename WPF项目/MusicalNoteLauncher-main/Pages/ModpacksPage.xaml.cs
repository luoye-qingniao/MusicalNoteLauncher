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
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Pages
{
    public partial class ModpacksPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private readonly ConfigManager _config;
        private int _currentPage;
        private const int PageSize = 20;
        private readonly string _modpacksPath;

        // 搜索相关
        private List<ModpackItem> _allModpackItems = new List<ModpackItem>();

        // 服务端分页
        private int _modrinthLoadedCount;
        private int _curseForgeLoadedCount;
        private const int ApiPageSize = 100;
        private bool _isLoadingPage;
        private bool _hasMoreData = true;

        private CancellationTokenSource _installCts;

        public ModpacksPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _config = new ConfigManager();
            _modpacksPath = Path.Combine(_config.GetMinecraftPath(), "modpacks");
            VersionScanService.Instance.ScanCompleted += OnVersionScanCompleted;
            PopulateVersionDropdown();
            LoadFirstPageAsync();
        }

        private void OnVersionScanCompleted(VersionScanResult result)
        {
            this.Dispatcher.Invoke(() => PopulateVersionDropdown());
        }

        private void PopulateVersionDropdown()
        {
            if (cmbVersion == null) return;
            while (cmbVersion.Items.Count > 1)
                cmbVersion.Items.RemoveAt(cmbVersion.Items.Count - 1);

            var installed = VersionScanService.Instance.GetInstalledJavaVersions();
            if (installed.Count == 0)
            {
                string[] fallback = { "1.21.1", "1.20.1", "1.19.2", "1.18.2", "1.16.5", "1.12.2", "1.7.10" };
                foreach (var v in fallback)
                    cmbVersion.Items.Add(new ComboBoxItem { Content = v, Tag = v });
                return;
            }
            foreach (var v in installed)
                cmbVersion.Items.Add(new ComboBoxItem { Content = v, Tag = v });
        }

        // ═══════════════════════════════════════════════════════════════
        // 服务端分页加载
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 首次加载：拉取第一页数据
        /// </summary>
        private async void LoadFirstPageAsync()
        {
            _modrinthLoadedCount = 0;
            _curseForgeLoadedCount = 0;
            _allModpackItems.Clear();
            _currentPage = 0;
            _hasMoreData = true;
            await LoadMoreFromApiAsync();
            ApplySearch();
        }

        /// <summary>
        /// 从 API 加载更多数据（追加到已有数据），哪个源先返回就先显示
        /// </summary>
        private async Task LoadMoreFromApiAsync()
        {
            if (_isLoadingPage || !_hasMoreData) return;
            _isLoadingPage = true;

            try
            {
                string query = txtSearch.Text?.Trim() ?? "";

                var modrinthTask = LoadModrinthBatch(query);
                var curseForgeTask = LoadCurseForgeBatch(query);

                var tasks = new List<Task> { modrinthTask, curseForgeTask };
                int newTotal = 0;

                while (tasks.Count > 0)
                {
                    var completed = await Task.WhenAny(tasks);
                    tasks.Remove(completed);

                    if (completed == modrinthTask)
                    {
                        var list = modrinthTask.Result;
                        if (list != null && list.Count > 0)
                        {
                            foreach (var mod in list)
                                _allModpackItems.Add(new ModpackItem
                                {
                                    Name = mod.Name, IconUrl = mod.IconUrl,
                                    Description = mod.Description,
                                    Version = mod.LatestVersion, Author = mod.Author,
                                    Downloads = mod.DownloadCountFormatted + " 下载",
                                    Source = "Modrinth", ProjectId = mod.Id
                                });
                            _modrinthLoadedCount += list.Count;
                            newTotal += list.Count;
                        }
                    }
                    else
                    {
                        var list = curseForgeTask.Result;
                        if (list != null && list.Count > 0)
                        {
                            foreach (var mod in list)
                                _allModpackItems.Add(new ModpackItem
                                {
                                    Name = mod.Name, IconUrl = mod.LogoUrl,
                                    Description = mod.Summary,
                                    Version = mod.LatestFile?.DisplayName ?? "",
                                    Author = mod.AuthorName,
                                    Downloads = mod.DownloadCountFormatted + " 下载",
                                    Source = "CurseForge", ProjectId = mod.Id.ToString()
                                });
                            _curseForgeLoadedCount += list.Count;
                            newTotal += list.Count;
                        }
                    }

                    // 立即刷新界面，不等另一个源
                    ApplySearch();
                }

                if (newTotal == 0)
                    _hasMoreData = false;
            }
            catch (Exception ex)
            {
                Logger.Error("[整合包分页] 加载更多数据失败: " + ex.Message);
            }
            finally
            {
                _isLoadingPage = false;
            }
        }

        private async Task<List<ModrinthMod>> LoadModrinthBatch(string query)
        {
            try
            {
                string searchTerm = GetEffectiveSearchQuery(query, "modpack");
                return await _modrinthApi.SearchMods(searchTerm, "", ApiPageSize, _modrinthLoadedCount);
            }
            catch (Exception ex)
            {
                Logger.Error("[整合包分页] Modrinth批量加载失败: " + ex.Message);
                return new List<ModrinthMod>();
            }
        }

        private async Task<List<CurseForgeMod>> LoadCurseForgeBatch(string query)
        {
            try
            {
                string searchTerm = GetEffectiveSearchQuery(query, "modpack");
                return await _curseForgeApi.SearchMods(searchTerm, ApiPageSize, _curseForgeLoadedCount);
            }
            catch (Exception ex)
            {
                Logger.Error("[整合包分页] CurseForge批量加载失败: " + ex.Message);
                return new List<CurseForgeMod>();
            }
        }

        private static string GetEffectiveSearchQuery(string rawQuery, string defaultKeyword)
        {
            if (string.IsNullOrEmpty(rawQuery))
                return defaultKeyword;
            if (ModNameDatabase.ContainsChinese(rawQuery))
            {
                string english = ModNameDatabase.GetBestEnglishKeyword(rawQuery);
                if (!string.IsNullOrEmpty(english))
                    return english;
                return defaultKeyword;
            }
            return rawQuery;
        }

        /// <summary>
        /// 重置数据并重新从API加载第一页
        /// </summary>
        private async void ResetAndReload()
        {
            _modrinthLoadedCount = 0;
            _curseForgeLoadedCount = 0;
            _allModpackItems.Clear();
            _hasMoreData = true;
            await LoadMoreFromApiAsync();
            ApplySearch();
        }

        private async Task LoadMoreAndRefreshAsync()
        {
            await LoadMoreFromApiAsync();
            ApplySearch();
        }

        // ═══════════════════════════════════════════════════════════════
        // 导航与返回
        // ═══════════════════════════════════════════════════════════════

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        // ═══════════════════════════════════════════════════════════════
        // 下载安装整合包（下载 → 解压 → 安装加载器 → 下载模组 → 创建版本）
        // ═══════════════════════════════════════════════════════════════

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ModpackItem item = button.DataContext as ModpackItem;
            if (item == null) return;

            string resourceName = item.Name;
            string targetDir = _modpacksPath;

            VersionSelectData.Versions = null;
            VersionSelectData.Source = item.Source;
            VersionSelectData.ProjectId = item.ProjectId;
            VersionSelectData.ResourceName = resourceName;
            VersionSelectData.TargetDir = targetDir;
            VersionSelectData.OnVersionSelected = (v) =>
            {
                _ = DownloadAndInstallModpack(v, resourceName, showDoneMessage: true);
            };
            VersionSelectData.BackPage = "Modpacks";
            string minecraftPath = _config.GetMinecraftPath();
            VersionSelectData.MinecraftPath = minecraftPath;
            VersionSelectData.CurrentGameVersion = _config.GameVersion;
            VersionSelectData.InstalledVersions = GetInstalledVersions(minecraftPath);
            AppContext.NavigateTo("VersionSelect");
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


        // ═══════════════════════════════════════════════════════════════
        // 下载安装整合包（下载 → 解压 → 安装加载器 → 下载模组 → 创建版本）
        // ═══════════════════════════════════════════════════════════════

        private async Task DownloadAndInstallModpack(DownloadVersionInfo version, string resourceName, bool showDoneMessage = true)
        {
            if (version == null) return;

            ShowInstallProgress();
            _installCts = new CancellationTokenSource();
            var ct = _installCts.Token;

            try
            {
                // 步骤1：下载 .mrpack 文件
                UpdateInstallProgress("正在下载整合包...", 5);
                string downloadPath = Path.Combine(_modpacksPath, version.FileName ?? $"{resourceName}.mrpack");
                Directory.CreateDirectory(_modpacksPath);

                bool downloadSuccess = await DownloadFileWithProgressAsync(version.DownloadUrl, downloadPath, 5, 25, ct);
                if (!downloadSuccess)
                {
                    HideInstallProgress();
                    ModernMessageBox.ShowError($"下载 {resourceName} 失败！", "错误");
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

                var result = await installer.InstallFromMrpackAsync(downloadPath, resourceName, ct);
                if (!result.Success)
                {
                    HideInstallProgress();
                    ModernMessageBox.ShowError($"安装 {resourceName} 失败！\n{result.ErrorMessage}", "错误");
                    return;
                }

                // 步骤3：清理下载的 .mrpack 文件
                try { File.Delete(downloadPath); } catch { }
                // 完成
                HideInstallProgress();

                if (showDoneMessage)
                {
                    ModernMessageBox.ShowInfo(
                        $"整合包「{resourceName}」安装完成！\n\n" +
                        $"版本名: {result.VersionId}\n" +
                        $"Minecraft: {result.MinecraftVersion} | 加载器: {result.LoaderType}\n\n" +
                        $"可在主页版本列表中选择并启动。", "安装成功");
                }
            }
            catch (OperationCanceledException)
            {
                HideInstallProgress();
                ModernMessageBox.ShowInfo($"{resourceName} 安装已取消", "提示");
            }
            catch (Exception ex)
            {
                HideInstallProgress();
                Logger.Error($"[安装] 整合包安装失败: {ex.Message}");
                ModernMessageBox.ShowError($"操作失败:\n{ex.Message}", "错误");
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
            _currentPage = 0;
            ResetAndReload();
        }

        private void BtnSearchRun_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 0;
            ResetAndReload();
        }

        private void BtnSearchReset_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            ((ComboBoxItem)cmbSource.Items[0]).IsSelected = true;
            cmbVersion.Text = "全部 (也可自行输入)";
            ((ComboBoxItem)cmbVersion.Items[0]).IsSelected = true;
            ((ComboBoxItem)cmbType.Items[0]).IsSelected = true;
            _currentPage = 0;
            ResetAndReload();
        }

        private string GetSelectedTag(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag != null)
                return item.Tag.ToString();
            return "";
        }

        private async void ApplySearch()
        {
            if (lbModpacks == null) return;
            lbModpacks.Items.Clear();

            string query = txtSearch.Text?.Trim() ?? "";
            string sourceFilter = GetSelectedTag(cmbSource);
            string versionFilter = cmbVersion.Text?.Trim() ?? "";
            string typeFilter = GetSelectedTag(cmbType);

            if (cmbVersion.SelectedItem is ComboBoxItem verItem && string.IsNullOrEmpty(verItem.Tag?.ToString()))
            {
                if (versionFilter == "全部 (也可自行输入)" || string.IsNullOrEmpty(versionFilter))
                    versionFilter = "";
            }

            IEnumerable<ModpackItem> filtered = _allModpackItems;

            if (!string.IsNullOrEmpty(sourceFilter) && sourceFilter != "All")
            {
                filtered = filtered.Where(m =>
                    string.Equals(m.Source, sourceFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(versionFilter) && versionFilter != "全部 (也可自行输入)")
            {
                filtered = filtered.Where(m =>
                    (m.Version ?? "").IndexOf(versionFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!string.IsNullOrEmpty(typeFilter))
            {
                filtered = filtered.Where(m =>
                    string.Equals(m.Type ?? "", typeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(query))
                {
                    // 中文搜索: API 已用英文关键词, 跳过客户端中文过滤
                    if (!ModNameDatabase.ContainsChinese(query))
                    {
                        filtered = filtered.Where(m =>
                            SearchHelper.IsMatch(query, m.Name, m.Description, m.Author, m.Version));
                    }
                }

            var filteredList = filtered.ToList();
            int totalItems = filteredList.Count;
            int startIndex = _currentPage * PageSize;
            var pageItems = filteredList.Skip(startIndex).Take(PageSize).ToList();

            // 后台预加载更多
            if (!_isLoadingPage && _hasMoreData &&
                (pageItems.Count < PageSize || startIndex + PageSize * 2 > _allModpackItems.Count))
            {
                _ = LoadMoreAndRefreshAsync();
            }

            foreach (var item in pageItems)
                lbModpacks.Items.Add(item);

            UpdatePageControls(totalItems);
        }

        private void UpdatePageControls(int totalItems)
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            int displayPage = _currentPage + 1;

            LabPage.Text = $"{displayPage} / {totalPages}";
            BtnPageFirst.IsEnabled = _currentPage > 0;
            BtnPageFirst.Opacity = _currentPage > 0 ? 1 : 0.4;
            BtnPagePrev.IsEnabled = _currentPage > 0;
            BtnPagePrev.Opacity = _currentPage > 0 ? 1 : 0.4;
            BtnPageNext.IsEnabled = _currentPage < totalPages - 1;
            BtnPageNext.Opacity = _currentPage < totalPages - 1 ? 1 : 0.4;
        }

        private void BtnPageFirst_Click(object sender, RoutedEventArgs e) { NavigateToPage(0); }
        private void BtnPagePrev_Click(object sender, RoutedEventArgs e) { NavigateToPage(_currentPage - 1); }
        private void BtnPageNext_Click(object sender, RoutedEventArgs e) { NavigateToPage(_currentPage + 1); }

        private async void NavigateToPage(int page)
        {
            _currentPage = page;
            if (lbModpacks.Items.Count > 0)
                lbModpacks.ScrollIntoView(lbModpacks.Items[0]);

            int neededCount = (page + 1) * PageSize;
            if (neededCount >= _allModpackItems.Count && !_isLoadingPage && _hasMoreData)
            {
                await LoadMoreFromApiAsync();
            }

            ApplySearch();
        }
    }
}
