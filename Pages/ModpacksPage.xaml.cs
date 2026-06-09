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

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            ModpackItem item = button.DataContext as ModpackItem;
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
                string savePath = Path.Combine(_modpacksPath, fileName);

                if (!Directory.Exists(_modpacksPath))
                {
                    Directory.CreateDirectory(_modpacksPath);
                }

                var task = new GenericDownloadTaskViewModel(downloadUrl, savePath, fileName);
                DownloadTaskManager.Instance.AddTask(task);
                _ = task.StartDownloadAsync();

                MessageBox.Show($"已将 {fileName} 添加到下载任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"[整合包下载] 下载失败: {ex.Message}");
                MessageBox.Show($"下载失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
