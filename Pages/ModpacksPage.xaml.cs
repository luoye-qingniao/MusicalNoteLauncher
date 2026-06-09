using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class ModpacksPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private List<ModrinthMod> _modrinthModpacks = new List<ModrinthMod>();
        private List<CurseForgeMod> _curseForgeModpacks = new List<CurseForgeMod>();
        private int _currentPage;
        private const int PageSize = 20;
        private readonly string _modpacksPath;

        public ModpacksPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _modpacksPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "modpacks");
            LoadModpacksAsync();
        }

        private async void LoadModpacksAsync()
        {
            await Task.WhenAll(LoadModrinthModpacks(), LoadCurseForgeModpacks());
            UpdateModpackList();
        }

        private async Task LoadModrinthModpacks()
        {
            try
            {
                _modrinthModpacks = await _modrinthApi.SearchMods("modpack", "", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[整合包加载] 加载Modrinth整合包失败: " + ex.Message);
                _modrinthModpacks = new List<ModrinthMod>();
            }
        }

        private async Task LoadCurseForgeModpacks()
        {
            try
            {
                _curseForgeModpacks = await _curseForgeApi.SearchMods("modpack", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[整合包加载] 加载CurseForge整合包失败: " + ex.Message);
                _curseForgeModpacks = new List<CurseForgeMod>();
            }
        }

        private void UpdateModpackList()
        {
            lbModpacks.Items.Clear();
            int count = (_currentPage + 1) * PageSize;
            if (_modrinthModpacks != null)
            {
                foreach (ModrinthMod mod in _modrinthModpacks.Take(count))
                {
                    lbModpacks.Items.Add(new ModpackItem
                    {
                        Name = mod.Name,
                        IconUrl = mod.IconUrl,
                        Description = mod.Description,
                        Version = mod.LatestVersion,
                        Author = mod.Author,
                        Downloads = mod.DownloadCountFormatted + " 下载",
                        Source = "Modrinth",
                        ProjectId = mod.Id
                    });
                }
            }
            if (_curseForgeModpacks != null)
            {
                foreach (CurseForgeMod mod in _curseForgeModpacks.Take(count))
                {
                    lbModpacks.Items.Add(new ModpackItem
                    {
                        Name = mod.Name,
                        IconUrl = mod.LogoUrl,
                        Description = mod.Summary,
                        Version = mod.LatestFile?.DisplayName ?? "",
                        Author = mod.AuthorName,
                        Downloads = mod.DownloadCountFormatted + " 下载",
                        Source = "CurseForge",
                        ProjectId = mod.Id.ToString()
                    });
                }
            }
        }

        private void BtnLoadMore_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            LoadModpacksAsync();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ModpackItem item = button.DataContext as ModpackItem;
            if (item == null) return;

            MessageBox.Show($"开始下载整合包 {item.Name}...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
