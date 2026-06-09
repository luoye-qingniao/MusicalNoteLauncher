using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Pages
{
    public partial class ModsPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private List<ModrinthMod> _modrinthMods;
        private List<CurseForgeMod> _curseForgeMods;
        private string _currentCategory = "全部";
        private readonly ConfigManager _config;

        public ModsPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _config = new ConfigManager();
            LoadModsAsync();
        }

        private async Task LoadModsAsync()
        {
            await Task.WhenAll(LoadModrinthMods(), LoadCurseForgeMods());
            UpdateModList();
        }

        private async Task LoadModrinthMods()
        {
            try
            {
                _modrinthMods = await _modrinthApi.SearchMods("fabric", "", 10);
            }
            catch (Exception ex)
            {
                Logger.Error("[Mod加载] 加载Modrinth模组失败: " + ex.Message);
                _modrinthMods = new List<ModrinthMod>();
            }
        }

        private async Task LoadCurseForgeMods()
        {
            try
            {
                _curseForgeMods = await _curseForgeApi.SearchMods("fabric", 10);
            }
            catch (Exception ex)
            {
                Logger.Error("[Mod加载] 加载CurseForge模组失败: " + ex.Message);
                _curseForgeMods = new List<CurseForgeMod>();
            }
        }

        private void UpdateModList()
        {
            lbMods.Items.Clear();
            if (_modrinthMods != null)
            {
                foreach (ModrinthMod mod in _modrinthMods)
                {
                    lbMods.Items.Add(new ModItem
                    {
                        Name = mod.Name,
                        Description = mod.Description,
                        Version = mod.LatestVersion,
                        Author = mod.Author,
                        Downloads = (mod.DownloadCountFormatted ?? ""),
                        Source = "Modrinth",
                        ProjectId = (string.IsNullOrEmpty(mod.Id) ? "unknown_id" : mod.Id),
                        IconUrl = mod.IconUrl
                    });
                }
            }
            if (_curseForgeMods != null)
            {
                foreach (CurseForgeMod mod in _curseForgeMods)
                {
                    lbMods.Items.Add(new ModItem
                    {
                        Name = mod.Name,
                        Description = mod.Summary,
                        Version = mod.LatestFile?.DisplayName ?? "",
                        Author = mod.AuthorName,
                        Downloads = (mod.DownloadCountFormatted ?? ""),
                        Source = "CurseForge",
                        ProjectId = mod.Id.ToString(),
                        IconUrl = null
                    });
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        private void BtnCategory_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                _currentCategory = button.Content.ToString();
                foreach (object obj in ((StackPanel)button.Parent).Children)
                {
                    Button btn = obj as Button;
                    if (btn != null)
                    {
                        btn.Background = ((btn == button) ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D")));
                        btn.Foreground = ((btn == button) ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA")));
                    }
                }
                LoadModsByCategory(_currentCategory);
            }
        }

        private async void LoadModsByCategory(string category)
        {
            await LoadModsAsync();
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ModItem item = button.DataContext as ModItem;
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
                string modsPath = GetModsDirectory();
                string savePath = Path.Combine(modsPath, fileName);

                if (!Directory.Exists(modsPath))
                {
                    Directory.CreateDirectory(modsPath);
                }

                var task = new GenericDownloadTaskViewModel(downloadUrl, savePath, fileName);
                DownloadTaskManager.Instance.AddTask(task);
                _ = task.StartDownloadAsync();

                MessageBox.Show($"已将 {fileName} 添加到下载任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"[模组下载] 下载失败: {ex.Message}");
                MessageBox.Show($"下载失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetModsDirectory()
        {
            string minecraftPath = _config.GetMinecraftPath();
            if (SettingsManager.Settings.EnableVersionIsolation && !string.IsNullOrEmpty(_config.GameVersion))
            {
                return Path.Combine(minecraftPath, "versions", _config.GameVersion, "game", "mods");
            }
            return Path.Combine(minecraftPath, "mods");
        }
    }
}
