using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Controls;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class VersionSelectPage : UserControl
    {
        private List<DownloadVersionInfo> _allVersions;
        private bool _initialized;

        public VersionSelectPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await InitializeAsync();
        }

        /// <summary>页面加载时异步初始化数据</summary>
        private async Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;

            var resourceName = VersionSelectData.ResourceName;
            VersionTitle.Text = $"选择下载版本 - {resourceName}";

            // 获取版本列表：优先用预加载数据，否则自行从网络获取
            var versions = VersionSelectData.Versions;
            if (versions == null)
            {
                if (string.IsNullOrEmpty(VersionSelectData.Source) || string.IsNullOrEmpty(VersionSelectData.ProjectId))
                {
                    NavigateBack();
                    return;
                }

                try
                {
                    LoadingText.Text = "正在获取版本列表...";
                    if (VersionSelectData.Source == "Modrinth")
                        versions = await DownloadManager.GetModrinthVersions(VersionSelectData.ProjectId);
                    else if (VersionSelectData.Source == "CurseForge" && long.TryParse(VersionSelectData.ProjectId, out long modId))
                        versions = await DownloadManager.GetCurseForgeVersions(modId);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[版本选择] 获取版本失败: {ex.Message}");
                    NavigateBack();
                    ModernMessageBox.ShowError($"获取版本列表失败:\n{ex.Message}", "错误");
                    return;
                }
            }

            if (versions == null || versions.Count == 0)
            {
                EmptyHint.Visibility = Visibility.Visible;
                LoadingOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            // 单版本也弹出确认对话框
            if (versions.Count == 1)
            {
                var callback = VersionSelectData.OnVersionSelected;
                if (callback != null)
                    callback(versions[0]);
                else
                    ShowDownloadDialog(versions[0]);
                NavigateBack();
                return;
            }

            // 显示版本列表
            LoadingOverlay.Visibility = Visibility.Collapsed;
            VersionListView.Visibility = Visibility.Visible;
            _allVersions = versions;
            VersionCountText.Text = $"共 {versions.Count} 个版本";
            VersionListView.ItemsSource = versions;
            SetupVersionFilters(versions);

            // 自动选中推荐版本
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

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigateBack();
        }

        private void VersionListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (VersionListView.SelectedItem is DownloadVersionInfo version)
                ConfirmDownload(version);
        }

        private void VersionItemDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DownloadVersionInfo version)
                ConfirmDownload(version);
        }

        private void ConfirmDownload(DownloadVersionInfo version)
        {
            if (version == null) return;

            var callback = VersionSelectData.OnVersionSelected;
            if (callback != null)
            {
                callback(version);
                NavigateBack();
                return;
            }

            // 弹出下载确认对话框
            ShowDownloadDialog(version);
            NavigateBack();
        }

        /// <summary>弹出下载确认对话框让用户选择安装位置和文件名</summary>
        private void ShowDownloadDialog(DownloadVersionInfo version)
        {
            var resourceName = VersionSelectData.ResourceName;
            var targetDir = VersionSelectData.TargetDir;
            var defaultPath = DownloadManager.BuildSavePath(resourceName, targetDir, version);
            var defaultFileName = Path.GetFileName(defaultPath);
            var defaultDir = Path.GetDirectoryName(defaultPath);

            var dialog = new DownloadConfirmDialog(
                resourceName,
                defaultDir,
                defaultFileName,
                VersionSelectData.MinecraftPath ?? MNLEnvironment.MinecraftPath,
                VersionSelectData.InstalledVersions ?? new List<string>(),
                VersionSelectData.CurrentGameVersion ?? "")
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SavePath))
            {
                bool success = DownloadManager.AddDownloadTask(
                    resourceName,
                    dialog.SavePath,
                    version,
                    dialog.FileName);
                if (success)
                    ModernMessageBox.ShowInfo(
                        $"[{resourceName}] 已添加到下载任务！（版本: {version.VersionName}）", "提示");
            }
        }

        private void NavigateBack()
        {
            string backPage = VersionSelectData.BackPage ?? "ModsPage";
            VersionSelectData.Clear();
            AppContext.NavigateTo(backPage);
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
                Background = isActive ? FindResource("PrimaryBrush") as Brush
                    : FindResource("CardHoverBrush") as Brush,
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
                    b.Background = active ? FindResource("PrimaryBrush") as Brush
                        : FindResource("CardHoverBrush") as Brush;
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
            VersionCountText.Text = $"共 {filtered.Count} 个版本";
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
    }
}
