using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Core;

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
                        btn.Background = ((btn == button) ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D")));
                        btn.Foreground = ((btn == button) ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")));
                    }
                }
                _isShaderMode = (mode == "光影");
                LoadContentAsync();
            }
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ShaderItem item = button.DataContext as ShaderItem;
            if (item == null) return;

            MessageBox.Show($"开始下载 {(item.Resolution == "1080p" ? "光影" : "资源包")} {item.Name}...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
