using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Net.Http;
using System.IO;
using MusicalNoteLauncher.ViewModels;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Pages
{
    public partial class RecommendDetailPage : UserControl
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly ModrinthApiService _modrinthApi = new ModrinthApiService();

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
            if (item != null && !string.IsNullOrEmpty(item.ProjectId))
            {
                try
                {
                    var downloadUrl = await _modrinthApi.GetDownloadUrl(item.ProjectId);
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                        string savePath = Path.Combine(AppContext.MinecraftPath, item.Type == "mod" ? "mods" : "modpacks", fileName);

                        var downloadTask = new GenericDownloadTaskViewModel(downloadUrl, savePath, fileName);
                        DownloadTaskManager.Instance.AddTask(downloadTask);
                        await downloadTask.StartDownloadAsync();

                        AppContext.NavigateTo("DownloadTask");
                    }
                    else
                    {
                        ModernMessageBox.ShowError("无法获取下载链接", "错误");
                    }
                }
                catch (Exception ex)
                {
                    ModernMessageBox.ShowError($"下载失败: {ex.Message}", "错误");
                }
            }
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