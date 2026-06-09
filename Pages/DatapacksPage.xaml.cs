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
    public partial class DatapacksPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;
        private List<ModrinthMod> _modrinthDatapacks = new List<ModrinthMod>();
        private List<CurseForgeMod> _curseForgeDatapacks = new List<CurseForgeMod>();
        private int _currentPage;
        private const int PageSize = 20;
        private readonly string _datapacksPath;

        public DatapacksPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            _datapacksPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "datapacks");
            LoadDatapacksAsync();
        }

        private async void LoadDatapacksAsync()
        {
            await Task.WhenAll(LoadModrinthDatapacks(), LoadCurseForgeDatapacks());
            UpdateDatapackList();
        }

        private async Task LoadModrinthDatapacks()
        {
            try
            {
                _modrinthDatapacks = await _modrinthApi.SearchMods("datapack", "", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[数据包加载] 加载Modrinth数据包失败: " + ex.Message);
                _modrinthDatapacks = new List<ModrinthMod>();
            }
        }

        private async Task LoadCurseForgeDatapacks()
        {
            try
            {
                _curseForgeDatapacks = await _curseForgeApi.SearchMods("datapack", 20);
            }
            catch (Exception ex)
            {
                Logger.Error("[数据包加载] 加载CurseForge数据包失败: " + ex.Message);
                _curseForgeDatapacks = new List<CurseForgeMod>();
            }
        }

        private void UpdateDatapackList()
        {
            lbDatapacks.Items.Clear();
            int count = (_currentPage + 1) * PageSize;
            if (_modrinthDatapacks != null)
            {
                foreach (ModrinthMod mod in _modrinthDatapacks.Take(count))
                {
                    lbDatapacks.Items.Add(new DatapackItem
                    {
                        Name = mod.Name,
                        IconUrl = mod.IconUrl,
                        Description = mod.Description,
                        Version = mod.LatestVersion,
                        Author = mod.Author,
                        Compatible = "1.20.x",
                        Source = "Modrinth",
                        ProjectId = mod.Id
                    });
                }
            }
            if (_curseForgeDatapacks != null)
            {
                foreach (CurseForgeMod mod in _curseForgeDatapacks.Take(count))
                {
                    lbDatapacks.Items.Add(new DatapackItem
                    {
                        Name = mod.Name,
                        IconUrl = mod.LogoUrl,
                        Description = mod.Summary,
                        Version = mod.LatestFile?.DisplayName ?? "",
                        Author = mod.AuthorName,
                        Compatible = "1.20.x",
                        Source = "CurseForge",
                        ProjectId = mod.Id.ToString()
                    });
                }
            }
        }

        private void BtnLoadMore_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            LoadDatapacksAsync();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            DatapackItem item = button.DataContext as DatapackItem;
            if (item == null) return;

            MessageBox.Show($"开始下载数据包 {item.Name}...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
