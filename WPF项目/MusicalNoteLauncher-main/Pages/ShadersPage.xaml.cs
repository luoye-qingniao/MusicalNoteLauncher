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
    public partial class ShadersPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private readonly ConfigManager _config;
        private bool _isShaderMode = true;
        private int _currentPage;
        private const int PageSize = 20;
        private readonly string _shadersPath;
        private readonly string _resourcePacksPath;

        // 搜索相关
        private List<ShaderItem> _allShaderItems = new List<ShaderItem>();

        // 服务端分页
        private int _modrinthLoadedCount;
        private int _curseForgeLoadedCount;
        private const int ApiPageSize = 100;
        private bool _isLoadingPage;
        private bool _hasMoreData = true;

        public ShadersPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _config = new ConfigManager();
            string mcPath = _config.GetMinecraftPath();
            _shadersPath = Path.Combine(mcPath, "shaderpacks");
            _resourcePacksPath = Path.Combine(mcPath, "resourcepacks");
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
            _allShaderItems.Clear();
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
                string searchKeyword = _isShaderMode ? "shader" : "resource pack";
                string resolution = _isShaderMode ? "1080p" : "32x";

                var modrinthTask = LoadModrinthBatch(query, searchKeyword);
                var curseForgeTask = LoadCurseForgeBatch(query, searchKeyword);

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
                                _allShaderItems.Add(new ShaderItem
                                {
                                    Name = mod.Name, Thumbnail = mod.IconUrl,
                                    Description = mod.Description, Version = mod.LatestVersion,
                                    Author = mod.Author, Resolution = resolution,
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
                                _allShaderItems.Add(new ShaderItem
                                {
                                    Name = mod.Name, Thumbnail = mod.LogoUrl,
                                    Description = mod.Summary,
                                    Version = mod.LatestFile?.DisplayName ?? "",
                                    Author = mod.AuthorName, Resolution = resolution,
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
                Logger.Error("[光影/材质分页] 加载更多数据失败: " + ex.Message);
            }
            finally
            {
                _isLoadingPage = false;
            }
        }

        private async Task<List<ModrinthMod>> LoadModrinthBatch(string query, string keyword)
        {
            try
            {
                string searchTerm = GetEffectiveSearchQuery(query, keyword);
                return await _modrinthApi.SearchMods(searchTerm, "", ApiPageSize, _modrinthLoadedCount);
            }
            catch (Exception ex)
            {
                Logger.Error("[光影/材质分页] Modrinth批量加载失败: " + ex.Message);
                return new List<ModrinthMod>();
            }
        }

        private async Task<List<CurseForgeMod>> LoadCurseForgeBatch(string query, string keyword)
        {
            try
            {
                string searchTerm = GetEffectiveSearchQuery(query, keyword);
                return await _curseForgeApi.SearchMods(searchTerm, ApiPageSize, _curseForgeLoadedCount);
            }
            catch (Exception ex)
            {
                Logger.Error("[光影/材质分页] CurseForge批量加载失败: " + ex.Message);
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
            _allShaderItems.Clear();
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

        private void BtnMode_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                string mode = button.Content.ToString();
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
                _isShaderMode = (mode == "光影");
                LoadFirstPageAsync();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // 下载功能
        // ═══════════════════════════════════════════════════════════════

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ShaderItem item = button.DataContext as ShaderItem;
            if (item == null) return;

            string resourceName = item.Name;
            string targetDir = _isShaderMode ? _shadersPath : _resourcePacksPath;

            VersionSelectData.Versions = null;
            VersionSelectData.Source = item.Source;
            VersionSelectData.ProjectId = item.ProjectId;
            VersionSelectData.ResourceName = resourceName;
            VersionSelectData.TargetDir = targetDir;
            VersionSelectData.OnVersionSelected = null;
            VersionSelectData.BackPage = "Shaders";
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
            if (lbShaders == null) return;
            lbShaders.Items.Clear();

            string query = txtSearch.Text?.Trim() ?? "";
            string sourceFilter = GetSelectedTag(cmbSource);
            string versionFilter = cmbVersion.Text?.Trim() ?? "";
            string typeFilter = GetSelectedTag(cmbType);

            if (cmbVersion.SelectedItem is ComboBoxItem verItem && string.IsNullOrEmpty(verItem.Tag?.ToString()))
            {
                if (versionFilter == "全部 (也可自行输入)" || string.IsNullOrEmpty(versionFilter))
                    versionFilter = "";
            }

            IEnumerable<ShaderItem> filtered = _allShaderItems;

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
                (pageItems.Count < PageSize || startIndex + PageSize * 2 > _allShaderItems.Count))
            {
                _ = LoadMoreAndRefreshAsync();
            }

            foreach (var item in pageItems)
                lbShaders.Items.Add(item);

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
            if (lbShaders.Items.Count > 0)
                lbShaders.ScrollIntoView(lbShaders.Items[0]);

            int neededCount = (page + 1) * PageSize;
            if (neededCount >= _allShaderItems.Count && !_isLoadingPage && _hasMoreData)
            {
                await LoadMoreFromApiAsync();
            }

            ApplySearch();
        }
    }
}
