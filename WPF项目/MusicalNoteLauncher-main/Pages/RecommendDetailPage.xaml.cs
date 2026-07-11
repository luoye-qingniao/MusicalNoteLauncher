using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MusicalNoteLauncher.ViewModels;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Pages
{
    public partial class RecommendDetailPage : UserControl
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly ModrinthApiService _modrinthApi = new ModrinthApiService();
        private readonly ConfigManager _config = new ConfigManager();

        public RecommendDetailPage()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                LoadDetailData();
            }
        }

        private async void LoadDetailData()
        {
            var item = AppContext.CurrentRecommendItem;
            if (item != null)
            {
                txtName.Text = item.Name;
                txtAuthor.Text = $"作者: {item.Author}";
                txtSource.Text = $"来源: {item.Source}";
                txtDescription.Text = string.IsNullOrEmpty(item.Description) ? "暂无描述" : item.Description;
                txtDownloads.Text = item.DownloadCount ?? "0";
                txtRating.Text = item.Rating ?? "-";

                if (!string.IsNullOrEmpty(item.IconUrl))
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(item.IconUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.StreamSource = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();
                                imgIcon.Source = bitmap;
                            }
                        }
                    }
                    catch
                    {
                        imgIcon.Source = null;
                    }
                }

                pnlTags.Children.Clear();
                if (!string.IsNullOrEmpty(item.Tags))
                {
                    foreach (var tag in item.Tags.Split(','))
                    {
                        var trimmedTag = tag.Trim();
                        if (!string.IsNullOrEmpty(trimmedTag))
                        {
                            var tagBtn = new Button
                            {
                                Content = trimmedTag,
                                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                                Foreground = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                                BorderThickness = new Thickness(0),
                                Padding = new Thickness(8, 4, 8, 4),
                                FontSize = 11,
                                FontFamily = new FontFamily("Microsoft YaHei"),
                                Margin = new Thickness(0, 0, 8, 8),
                                Cursor = Cursors.Hand
                            };
                            pnlTags.Children.Add(tagBtn);
                        }
                    }
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Home");
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            var item = AppContext.CurrentRecommendItem;
            if (item == null || string.IsNullOrEmpty(item.ProjectId))
                return;

            btnDownload.IsEnabled = false;
            btnDownload.Content = "正在获取版本...";

            try
            {
                // 根据来源获取版本列表（目前推荐栏仅 Modrinth）
                var versions = await DownloadManager.GetModrinthVersions(item.ProjectId);

                btnDownload.IsEnabled = true;
                btnDownload.Content = "立即下载";

                if (versions == null || versions.Count == 0)
                {
                    ModernMessageBox.ShowInfo($"未找到可用的下载版本！", "提示");
                    return;
                }

                // 自动选推荐版本
                var recommended = DownloadManager.GetRecommendedVersion(versions);
                if (recommended == null)
                {
                    ModernMessageBox.ShowInfo($"未找到可用的下载版本！", "提示");
                    return;
                }

                string resourceName = item.Name ?? "未知资源";
                string minecraftPath = AppContext.MinecraftPath;
                string subDir = item.Type == "modpack" ? "modpacks" : "mods";
                string targetDir = GetTargetDirectory(minecraftPath, subDir);

                string defaultFileName = recommended.FileName;
                if (string.IsNullOrEmpty(defaultFileName))
                    defaultFileName = $"{resourceName}_{DateTime.Now:yyyyMMddHHmmss}.jar";
                foreach (char c in Path.GetInvalidFileNameChars())
                    defaultFileName = defaultFileName.Replace(c, '_');

                var dialog = new DownloadConfirmDialog(
                    resourceName,
                    targetDir,
                    defaultFileName,
                    minecraftPath,
                    GetInstalledVersions(minecraftPath),
                    _config.GameVersion
                );

                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    string savePath = dialog.SavePath;
                    if (!string.IsNullOrEmpty(savePath))
                    {
                        bool success = DownloadManager.AddDownloadTask(resourceName, savePath, recommended, dialog.FileName);
                        if (success)
                        {
                            string versionInfo = !string.IsNullOrEmpty(recommended.VersionName)
                                ? $"（版本: {recommended.VersionName}）"
                                : "";
                            ModernMessageBox.ShowInfo(
                                $"已将 {resourceName}{versionInfo} 添加到下载任务", "提示");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                btnDownload.IsEnabled = true;
                btnDownload.Content = "立即下载";
                ModernMessageBox.ShowError($"下载失败: {ex.Message}", "错误");
            }
        }

        private string GetTargetDirectory(string minecraftPath, string subDir)
        {
            string currentVersion = _config.GameVersion;

            if (!string.IsNullOrEmpty(currentVersion)
                && SettingsManager.Settings.ShouldIsolateVersionForVersion(minecraftPath, currentVersion))
                return Path.Combine(minecraftPath, "versions", currentVersion, "game", subDir);

            return Path.Combine(minecraftPath, subDir);
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

        private void BtnViewMore_Click(object sender, RoutedEventArgs e)
        {
            var item = AppContext.CurrentRecommendItem;
            if (item != null)
            {
                if (item.Type == "mod")
                {
                    AppContext.NavigateTo("ModsPage");
                }
                else if (item.Type == "modpack")
                {
                    AppContext.NavigateTo("Modpacks");
                }
            }
        }
    }
}