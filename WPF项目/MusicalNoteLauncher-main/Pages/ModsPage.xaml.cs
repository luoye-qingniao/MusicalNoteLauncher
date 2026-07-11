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
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Pages
{
    public partial class ModsPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private readonly ConfigManager _config;

        // 筛选相关
        private List<ModItem> _allModItems = new List<ModItem>();

        // 分页相关
        private int _currentPage;
        private const int PageSize = 20;

        // 服务端分页 —— 已加载的每页原始数据缓存（按来源分开）
        private int _modrinthLoadedCount;   // Modrinth 已加载总数
        private int _curseForgeLoadedCount; // CurseForge 已加载总数
        private const int ApiPageSize = 100; // 每次从API拉取的数量
        private bool _isLoadingPage;
        private bool _hasMoreData = true;

        public ModsPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _config = new ConfigManager();
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
            // 移除旧的版本项（保留"全部"）
            while (cmbVersion.Items.Count > 1)
                cmbVersion.Items.RemoveAt(cmbVersion.Items.Count - 1);

            var installed = VersionScanService.Instance.GetInstalledJavaVersions();
            if (installed.Count == 0)
            {
                // 没有已安装版本时显示常用版本
                string[] fallback = { "1.21.1", "1.20.1", "1.19.2", "1.18.2", "1.16.5", "1.12.2", "1.7.10" };
                foreach (var v in fallback)
                    cmbVersion.Items.Add(new ComboBoxItem { Content = v, Tag = v });
                return;
            }
            foreach (var v in installed)
                cmbVersion.Items.Add(new ComboBoxItem { Content = v, Tag = v });
        }

        /// <summary>
        /// 首次加载：拉取第一页数据
        /// </summary>
        private async void LoadFirstPageAsync()
        {
            _modrinthLoadedCount = 0;
            _curseForgeLoadedCount = 0;
            _allModItems.Clear();
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
                string versionFilter = GetVersionFilterText();

                var modrinthTask = LoadModrinthBatch(query, versionFilter);
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
                                _allModItems.Add(new ModItem
                                {
                                    Name = mod.Name, Description = mod.Description,
                                    Version = mod.LatestVersion, Author = mod.Author,
                                    Downloads = mod.DownloadCountFormatted ?? "",
                                    Source = "Modrinth",
                                    ProjectId = string.IsNullOrEmpty(mod.Id) ? "unknown_id" : mod.Id,
                                    IconUrl = mod.IconUrl
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
                                _allModItems.Add(new ModItem
                                {
                                    Name = mod.Name, Description = mod.Summary,
                                    Version = mod.LatestFile?.DisplayName ?? "",
                                    Author = mod.AuthorName,
                                    Downloads = mod.DownloadCountFormatted ?? "",
                                    Source = "CurseForge", ProjectId = mod.Id.ToString(),
                                    IconUrl = null
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
                Logger.Error("[Mod分页] 加载更多数据失败: " + ex.Message);
            }
            finally
            {
                _isLoadingPage = false;
            }
        }

        private async Task<List<ModrinthMod>> LoadModrinthBatch(string query, string gameVersion)
        {
            try
            {
                string searchTerm = GetEffectiveSearchQuery(query, "fabric");
                return await _modrinthApi.SearchMods(searchTerm, gameVersion, ApiPageSize, _modrinthLoadedCount);
            }
            catch (Exception ex)
            {
                Logger.Error("[Mod分页] Modrinth批量加载失败: " + ex.Message);
                return new List<ModrinthMod>();
            }
        }

        private async Task<List<CurseForgeMod>> LoadCurseForgeBatch(string query)
        {
            try
            {
                string searchTerm = GetEffectiveSearchQuery(query, "fabric");
                return await _curseForgeApi.SearchMods(searchTerm, ApiPageSize, _curseForgeLoadedCount);
            }
            catch (Exception ex)
            {
                Logger.Error("[Mod分页] CurseForge批量加载失败: " + ex.Message);
                return new List<CurseForgeMod>();
            }
        }

        /// <summary>
        /// 获取有效的 API 搜索词: 中文输入 → 查映射表转英文, 否则直接用原文
        /// </summary>
        private static string GetEffectiveSearchQuery(string rawQuery, string defaultKeyword)
        {
            if (string.IsNullOrEmpty(rawQuery))
                return defaultKeyword;

            if (ModNameDatabase.ContainsChinese(rawQuery))
            {
                string english = ModNameDatabase.GetBestEnglishKeyword(rawQuery);
                if (!string.IsNullOrEmpty(english))
                    return english;
                // 中文未匹配 → 用默认词加载数据, 靠客户端过滤
                return defaultKeyword;
            }

            return rawQuery;
        }

        private string GetVersionFilterText()
        {
            if (cmbVersion.SelectedItem is ComboBoxItem verItem && string.IsNullOrEmpty(verItem.Tag?.ToString()))
            {
                var text = cmbVersion.Text?.Trim() ?? "";
                if (text == "全部 (也可自行输入)" || string.IsNullOrEmpty(text))
                    return "";
                return text;
            }
            return "";
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        // ═══════════════════════════════════════════════════════════════
        // 筛选功能 —— PCL 风格（名称 / 来源 / 版本 / 类型）
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

        /// <summary>
        /// 重置数据并重新从API加载第一页
        /// </summary>
        private async void ResetAndReload()
        {
            _modrinthLoadedCount = 0;
            _curseForgeLoadedCount = 0;
            _allModItems.Clear();
            _hasMoreData = true;
            await LoadMoreFromApiAsync();
            ApplySearch();
        }

        private void UpdateSearchHint()
        {
            bool isEmpty = string.IsNullOrEmpty(txtSearch.Text);
            txtSearchHint.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            btnSearchClear.Opacity = isEmpty ? 0 : 1;
            btnSearchClear.IsHitTestVisible = !isEmpty;
        }

        private string GetSelectedTag(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag != null)
                return item.Tag.ToString();
            return "";
        }

        private async void ApplySearch()
        {
            if (lbMods == null) return; // XAML 初始化期间控件可能未就绪
            lbMods.Items.Clear();

            string query = txtSearch.Text?.Trim() ?? "";
            string sourceFilter = GetSelectedTag(cmbSource);
            string versionFilter = cmbVersion.Text?.Trim() ?? "";
            string typeFilter = GetSelectedTag(cmbType);

            // 处理版本 ComboBox 的"全部"
            if (cmbVersion.SelectedItem is ComboBoxItem verItem && string.IsNullOrEmpty(verItem.Tag?.ToString()))
            {
                if (versionFilter == "全部 (也可自行输入)" || string.IsNullOrEmpty(versionFilter))
                    versionFilter = "";
            }

            IEnumerable<ModItem> filtered = _allModItems;

            // 来源筛选
            if (!string.IsNullOrEmpty(sourceFilter) && sourceFilter != "All")
            {
                filtered = filtered.Where(m =>
                    string.Equals(m.Source, sourceFilter, StringComparison.OrdinalIgnoreCase));
            }

            // 版本筛选
            if (!string.IsNullOrEmpty(versionFilter) && versionFilter != "全部 (也可自行输入)")
            {
                filtered = filtered.Where(m =>
                    (m.Version ?? "").IndexOf(versionFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // 名称搜索（模糊匹配）
            if (!string.IsNullOrEmpty(query))
            {
                // 中文搜索: API 已用英文关键词, 跳过客户端中文过滤
                if (!ModNameDatabase.ContainsChinese(query))
                {
                    filtered = filtered.Where(m =>
                        SearchHelper.IsMatch(query, m.Name, m.Description, m.Author, m.Version));
                }
            }

            // 类型筛选（关键词匹配名称和描述）
            if (!string.IsNullOrEmpty(typeFilter))
            {
                filtered = filtered.Where(m =>
                    (m.Name ?? "").IndexOf(typeFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (m.Description ?? "").IndexOf(typeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var filteredList = filtered.ToList();
            int totalItems = filteredList.Count;
            int startIndex = _currentPage * PageSize;
            var pageItems = filteredList.Skip(startIndex).Take(PageSize).ToList();

            // 如果当前页数据不足或接近已加载数据末尾，后台预加载更多
            if (!_isLoadingPage && _hasMoreData &&
                (pageItems.Count < PageSize || startIndex + PageSize * 2 > _allModItems.Count))
            {
                // 后台预加载下一批，不阻塞当前显示
                _ = LoadMoreAndRefreshAsync();
            }

            foreach (var item in pageItems)
                lbMods.Items.Add(item);

            UpdatePageControls(totalItems);
        }

        /// <summary>
        /// 后台加载更多数据后刷新列表
        /// </summary>
        private async Task LoadMoreAndRefreshAsync()
        {
            await LoadMoreFromApiAsync();
            ApplySearch();
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
            if (lbMods.Items.Count > 0)
                lbMods.ScrollIntoView(lbMods.Items[0]);

            // 检查是否需要加载更多数据（目标页可能超出已加载数据范围）
            int neededCount = (page + 1) * PageSize;
            if (neededCount >= _allModItems.Count && !_isLoadingPage && _hasMoreData)
            {
                await LoadMoreFromApiAsync();
            }

            ApplySearch();
        }

        // ═══════════════════════════════════════════════════════════════
        // 下载功能
        // ═══════════════════════════════════════════════════════════════

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ModItem item = button.DataContext as ModItem;
            if (item == null) return;

            string minecraftPath = _config.GetMinecraftPath();

            // 填充 ModDetailData 并跳转到详情页
            ModDetailData.Name = item.Name;
            ModDetailData.Description = item.Description;
            ModDetailData.Author = item.Author;
            ModDetailData.Downloads = item.Downloads;
            ModDetailData.IconUrl = item.IconUrl;
            ModDetailData.Source = item.Source;
            ModDetailData.ProjectId = item.ProjectId;
            ModDetailData.MinecraftPath = minecraftPath;
            ModDetailData.CurrentGameVersion = _config.GameVersion;
            ModDetailData.InstalledVersions = GetInstalledVersions(minecraftPath);
            ModDetailData.TargetSubDir = "mods";
            ModDetailData.BackPage = "ModsPage";

            AppContext.NavigateTo("ModDetail");
        }

        private string GetModsDirectory()
        {
            string minecraftPath = _config.GetMinecraftPath();
            if (SettingsManager.Settings.ShouldIsolateVersionForVersion(minecraftPath, _config.GameVersion))
                return Path.Combine(minecraftPath, "versions", _config.GameVersion, "game", "mods");
            return Path.Combine(minecraftPath, "mods");
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
    }
}
