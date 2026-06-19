using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Pages
{
    public partial class ShadersPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private List<ModrinthMod> _modrinthShaders = new List<ModrinthMod>();
        private List<CurseForgeMod> _curseForgeShaders = new List<CurseForgeMod>();
        private List<ModrinthMod> _modrinthResourcePacks = new List<ModrinthMod>();
        private List<CurseForgeMod> _curseForgeResourcePacks = new List<CurseForgeMod>();
        private bool _isShaderMode = true;
        private int _currentPage;
        private const int PageSize = 20;
        private readonly string _shadersPath;
        private readonly string _resourcePacksPath;

        public ShadersPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _shadersPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "shaderpacks");
            _resourcePacksPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "resourcepacks");
            LoadContentAsync();
        }

        private async void LoadContentAsync()
        {
            if (_isShaderMode)
            {
                await Task.WhenAll(LoadModrinthShaders(), LoadCurseForgeShaders());
            }
            else
            {
                await Task.WhenAll(LoadModrinthResourcePacks(), LoadCurseForgeResourcePacks());
            }
            UpdateShaderList();
        }

        private async Task LoadModrinthShaders()
        {
            try
            {
                _modrinthShaders = await _modrinthApi.SearchMods("shader", "", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[光影加载] 加载Modrinth光影失败: " + ex.Message);
                _modrinthShaders = new List<ModrinthMod>();
            }
        }

        private async Task LoadCurseForgeShaders()
        {
            try
            {
                _curseForgeShaders = await _curseForgeApi.SearchMods("shader", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[光影加载] 加载CurseForge光影失败: " + ex.Message);
                _curseForgeShaders = new List<CurseForgeMod>();
            }
        }

        private async Task LoadModrinthResourcePacks()
        {
            try
            {
                _modrinthResourcePacks = await _modrinthApi.SearchMods("resource pack", "", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[资源包加载] 加载Modrinth资源包失败: " + ex.Message);
                _modrinthResourcePacks = new List<ModrinthMod>();
            }
        }

        private async Task LoadCurseForgeResourcePacks()
        {
            try
            {
                _curseForgeResourcePacks = await _curseForgeApi.SearchMods("resource pack", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[资源包加载] 加载CurseForge资源包失败: " + ex.Message);
                _curseForgeResourcePacks = new List<CurseForgeMod>();
            }
        }

        private void UpdateShaderList()
        {
            lbShaders.Items.Clear();
            int count = (_currentPage + 1) * PageSize;
            if (_isShaderMode)
            {
                if (_modrinthShaders != null)
                {
                    foreach (ModrinthMod mod in _modrinthShaders.Take(count))
                    {
                        lbShaders.Items.Add(new ShaderItem
                        {
                            Name = mod.Name,
                            Thumbnail = mod.IconUrl,
                            Description = mod.Description,
                            Version = mod.LatestVersion,
                            Author = mod.Author,
                            Resolution = "1080p",
                            Source = "Modrinth",
                            ProjectId = mod.Id
                        });
                    }
                }
                if (_curseForgeShaders != null)
                {
                    foreach (CurseForgeMod mod in _curseForgeShaders.Take(count))
                    {
                        lbShaders.Items.Add(new ShaderItem
                        {
                            Name = mod.Name,
                            Thumbnail = mod.LogoUrl,
                            Description = mod.Summary,
                            Version = mod.LatestFile?.DisplayName ?? "",
                            Author = mod.AuthorName,
                            Resolution = "1080p",
                            Source = "CurseForge",
                            ProjectId = mod.Id.ToString()
                        });
                    }
                }
            }
            else
            {
                if (_modrinthResourcePacks != null)
                {
                    foreach (ModrinthMod mod in _modrinthResourcePacks.Take(count))
                    {
                        lbShaders.Items.Add(new ShaderItem
                        {
                            Name = mod.Name,
                            Thumbnail = mod.IconUrl,
                            Description = mod.Description,
                            Version = mod.LatestVersion,
                            Author = mod.Author,
                            Resolution = "32x",
                            Source = "Modrinth",
                            ProjectId = mod.Id
                        });
                    }
                }
                if (_curseForgeResourcePacks != null)
                {
                    foreach (CurseForgeMod mod in _curseForgeResourcePacks.Take(count))
                    {
                        lbShaders.Items.Add(new ShaderItem
                        {
                            Name = mod.Name,
                            Thumbnail = mod.LogoUrl,
                            Description = mod.Summary,
                            Version = mod.LatestFile?.DisplayName ?? "",
                            Author = mod.AuthorName,
                            Resolution = "32x",
                            Source = "CurseForge",
                            ProjectId = mod.Id.ToString()
                        });
                    }
                }
            }
        }

        private async void BtnLoadMore_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            await Task.WhenAll(LoadModrinthShaders(), LoadCurseForgeShaders(), LoadModrinthResourcePacks(), LoadCurseForgeResourcePacks());
            UpdateShaderList();
        }

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
                            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
                            btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2"));
                            btn.Foreground = Brushes.White;
                        }
                        else
                        {
                            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D"));
                            btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A"));
                            btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));
                        }
                    }
                }
                _isShaderMode = (mode == "光影");
                LoadContentAsync();
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ShaderItem item = button.DataContext as ShaderItem;
            if (item == null) return;

            try
            {
                string downloadUrl = null;
                
                if (item.Source == "Modrinth")
                {
                    downloadUrl = await _modrinthApi.GetDownloadUrl(item.ProjectId);
                }
                else if (item.Source == "CurseForge")
                {
                    if (long.TryParse(item.ProjectId, out long modId))
                    {
                        downloadUrl = await _curseForgeApi.GetDownloadUrl(modId);
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    MessageBox.Show("无法获取下载链接", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
                string savePath = Path.Combine(_isShaderMode ? _shadersPath : _resourcePacksPath, fileName);

                if (!Directory.Exists(Path.GetDirectoryName(savePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                }

                var task = new GenericDownloadTaskViewModel(downloadUrl, savePath, fileName);
                DownloadTaskManager.Instance.AddTask(task);
                _ = task.StartDownloadAsync();

                MessageBox.Show($"已将 {fileName} 添加到下载任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"[{(_isShaderMode ? "光影" : "资源包")}下载] 下载失败: {ex.Message}");
                MessageBox.Show($"下载失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
