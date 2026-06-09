using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

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

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            DatapackItem item = button.DataContext as DatapackItem;
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
                string savePath = Path.Combine(_datapacksPath, fileName);

                if (!Directory.Exists(_datapacksPath))
                {
                    Directory.CreateDirectory(_datapacksPath);
                }

                var task = new GenericDownloadTaskViewModel(downloadUrl, savePath, fileName);
                DownloadTaskManager.Instance.AddTask(task);
                _ = task.StartDownloadAsync();

                MessageBox.Show($"已将 {fileName} 添加到下载任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"[数据包下载] 下载失败: {ex.Message}");
                MessageBox.Show($"下载失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
