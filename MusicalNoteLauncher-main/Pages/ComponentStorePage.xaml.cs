using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Controls;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    /// <summary>组件列表项视图模型（适配 XAML 绑定名）</summary>
    public class ComponentItemViewModel
    {
        public int Id { get; set; }
        public string ComponentName { get; set; } = "";
        public string ComponentCategory { get; set; } = "";
        public string ComponentDesc { get; set; } = "";
        public string IconEmoji { get; set; } = "";
        public string Rating { get; set; } = "";
        public string DownloadCount { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string McVersion { get; set; } = "";
        public string Author { get; set; } = "";
    }

    public partial class ComponentStorePage : UserControl
    {
        private string _currentCategory = "";
        private ObservableCollection<ComponentItemViewModel> _components = new();

        public ComponentStorePage()
        {
            InitializeComponent();
            lstBackgrounds.ItemsSource = BackgroundStoreService.Instance.Items;
            lstComponents.ItemsSource = _components;

            // 加载组件数据
            Loaded += async (s, e) => await LoadComponentsAsync();
        }

        // ======== 服务器数据加载 ========

        private async Task LoadComponentsAsync(string searchQuery = "")
        {
            try
            {
                ComponentListResponse result;
                if (!string.IsNullOrEmpty(searchQuery))
                    result = await ComponentStoreService.Instance.SearchComponentsAsync(searchQuery);
                else
                    result = await ComponentStoreService.Instance.GetComponentsAsync();

                _components.Clear();
                foreach (var c in result.components)
                {
                    string catName = c.category switch
                    {
                        "mod" => "模组",
                        "modpack" => "整合包",
                        "shader" => "光影",
                        "texture" => "材质",
                        "map" => "地图",
                        _ => c.category
                    };

                    _components.Add(new ComponentItemViewModel
                    {
                        Id = c.id,
                        ComponentName = c.name,
                        ComponentCategory = catName,
                        ComponentDesc = c.description,
                        IconEmoji = c.icon_emoji,
                        Rating = $"⭐ {c.rating:F1}",
                        DownloadCount = FormatDownloadCount(c.download_count),
                        DownloadUrl = c.download_url,
                        McVersion = c.mc_version,
                        Author = c.author
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载组件列表失败: {ex.Message}");
            }
        }

        private static string FormatDownloadCount(int count)
        {
            if (count >= 1_000_000) return $"{count / 1_000_000.0:F1}M";
            if (count >= 10_000) return $"{count / 1_000.0:F1}K";
            return count.ToString("N0");
        }

        // ======== 搜索 ========

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            string text = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                ModernMessageBox.ShowWarning("请输入搜索关键词", "提示");
                return;
            }

            // 切换到组件视图
            lstComponents.Visibility = Visibility.Visible;
            lstBackgrounds.Visibility = Visibility.Collapsed;
            bgToolbar.Visibility = Visibility.Collapsed;
            ResetCategoryButtons();

            await LoadComponentsAsync(text);
        }

        // ======== 分类切换 ========

        private void BtnCategory_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ResetCategoryButtons();
            button.Background = (Brush)FindResource("PrimaryBrush");

            string category = button.Content?.ToString() ?? "";
            _currentCategory = category;

            if (category == "背景")
            {
                lstComponents.Visibility = Visibility.Collapsed;
                lstBackgrounds.Visibility = Visibility.Visible;
                bgToolbar.Visibility = Visibility.Visible;
                BackgroundStoreService.Instance.Refresh();
                UpdateBackgroundCount();
            }
            else
            {
                lstComponents.Visibility = Visibility.Visible;
                lstBackgrounds.Visibility = Visibility.Collapsed;
                bgToolbar.Visibility = Visibility.Collapsed;

                // 重新从服务器加载组件
                _ = LoadComponentsAsync();
            }
        }

        private void UpdateBackgroundCount()
        {
            int count = BackgroundStoreService.Instance.Items.Count;
            txtBackgroundCount.Text = count > 0 ? $"共 {count} 个背景素材" : "暂无背景素材，点击上传添加";
        }

        private void ResetCategoryButtons()
        {
            btnCategoryCustom.Background = (Brush)FindResource("SurfaceBrush");
            btnCategoryBackgrounds.Background = (Brush)FindResource("SurfaceBrush");
        }

        // ======== 下载组件 ========

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ComponentItemViewModel item)
            {
                if (string.IsNullOrEmpty(item.DownloadUrl))
                {
                    ModernMessageBox.ShowInfo("此组件暂未提供下载链接，请联系作者获取", "提示");
                    return;
                }

                // 上报下载计数
                await ComponentStoreService.Instance.TrackDownloadAsync(item.Id);

                // 打开下载链接
                try
                {
                    Process.Start(new ProcessStartInfo(item.DownloadUrl) { UseShellExecute = true });
                    ModernMessageBox.ShowSuccess($"正在打开 {item.ComponentName} 的下载页面...", "开始下载");
                }
                catch (Exception ex)
                {
                    ModernMessageBox.ShowWarning($"无法打开下载链接: {ex.Message}", "下载失败");
                }
            }
        }

        // ======== 背景管理 ========

        private async void BtnUploadBackground_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择背景素材",
                Filter = "图片和视频 (*.jpg;*.jpeg;*.png;*.webp;*.mp4)|*.jpg;*.jpeg;*.png;*.webp;*.mp4|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                string fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                var btn = sender as Button;
                if (btn != null) btn.IsEnabled = false;

                try
                {
                    var item = BackgroundStoreService.Instance.Import(dialog.FileName, fileName);

                    if (item != null)
                    {
                        // 异步上传到服务器数据库
                        string uploader = AppContext.Username;
                        var serverResult = await BackgroundServerService.Instance.UploadAsync(
                            dialog.FileName, fileName, uploader);

                        if (serverResult != null && serverResult.success)
                        {
                            item.ServerId = serverResult.id;
                            item.Uploader = uploader;
                            BackgroundStoreService.Instance.SaveManifest();
                            Logger.Info($"背景 \"{item.Name}\" 已同步到服务器 (ID={serverResult.id})");
                        }

                        ResetCategoryButtons();
                        btnCategoryBackgrounds.Background = (Brush)FindResource("PrimaryBrush");
                        _currentCategory = "背景";
                        lstComponents.Visibility = Visibility.Collapsed;
                        lstBackgrounds.Visibility = Visibility.Visible;
                        bgToolbar.Visibility = Visibility.Visible;
                        UpdateBackgroundCount();

                        string msg = serverResult != null && serverResult.success
                            ? $"背景 \"{item.Name}\" 已上传到素材库并同步到服务器"
                            : $"背景 \"{item.Name}\" 已上传到素材库（服务器同步失败，可稍后重试）";
                        ModernMessageBox.ShowSuccess(msg, "上传成功");
                    }
                    else
                    {
                        ModernMessageBox.ShowWarning("不支持的文件格式，仅支持 JPG、PNG、WEBP、MP4", "上传失败");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"背景上传流程异常: {ex.Message}");
                    ModernMessageBox.ShowWarning($"上传过程中发生错误: {ex.Message}", "上传失败");
                }
                finally
                {
                    if (btn != null) btn.IsEnabled = true;
                }
            }
        }

        private void BtnUseBackground_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BackgroundStoreItem item)
            {
                if (!File.Exists(item.FilePath))
                {
                    ModernMessageBox.ShowWarning("素材文件不存在，可能已被删除", "文件丢失");
                    BackgroundStoreService.Instance.Refresh();
                    return;
                }

                var cfg = BackgroundConfigService.Instance;

                if (item.Type == "Video")
                {
                    if (cfg.SetVideoPath(item.FilePath))
                    {
                        cfg.SetMode(BackgroundMode.Video);
                        ModernMessageBox.ShowSuccess($"已应用视频背景 \"{item.Name}\"", "应用成功");
                    }
                    else
                    {
                        ModernMessageBox.ShowWarning("视频格式不支持，仅支持 MP4", "格式错误");
                    }
                }
                else
                {
                    if (cfg.SetImagePath(item.FilePath))
                    {
                        cfg.SetMode(BackgroundMode.Image);
                        ModernMessageBox.ShowSuccess($"已应用图片背景 \"{item.Name}\"", "应用成功");
                    }
                    else
                    {
                        ModernMessageBox.ShowWarning("图片格式不支持，仅支持 JPG、PNG、WEBP", "格式错误");
                    }
                }
            }
        }

        private async void BtnDeleteBackground_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BackgroundStoreItem item)
            {
                if (ModernMessageBox.ShowConfirm($"确定要删除背景 \"{item.Name}\" 吗？此操作不可撤销。", "确认删除"))
                {
                    // 先尝试从服务器删除
                    if (item.IsOnServer)
                    {
                        await BackgroundServerService.Instance.DeleteAsync(item.ServerId);
                    }

                    if (BackgroundStoreService.Instance.Remove(item.Id))
                    {
                        UpdateBackgroundCount();
                        ModernMessageBox.ShowInfo($"背景 \"{item.Name}\" 已删除", "删除成功");
                    }
                    else
                        ModernMessageBox.ShowWarning("删除失败，请检查文件权限", "删除失败");
                }
            }
        }
    }
}
