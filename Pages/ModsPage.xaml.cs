using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Core;

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

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ModItem item = button.DataContext as ModItem;
            if (item == null) return;

            MessageBox.Show($"开始下载 {item.Name}...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
