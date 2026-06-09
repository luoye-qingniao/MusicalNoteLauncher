using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Pages
{
    public partial class DependenciesPage : UserControl
    {
        private readonly ModrinthApiService _modrinthApi;
        private readonly CurseForgeApiService _curseForgeApi;

        public DependenciesPage()
        {
            InitializeComponent();
            _modrinthApi = new ModrinthApiService();
            _curseForgeApi = new CurseForgeApiService();
            LoadDependencies();
            if (btnLoadMore != null)
            {
                btnLoadMore.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadDependencies()
        {
            lbDependencies.Items.Clear();
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Forge",
                IconUrl = "https://files.minecraftforge.net/images/logo.png",
                Description = "最流行的Mod加载器，支持广泛的Mod生态系统",
                Version = "1.20.1-47.2.14",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Fabric",
                IconUrl = "https://fabricmc.net/assets/logo.png",
                Description = "轻量级Mod加载器，启动更快，Mod更小",
                Version = "0.15.10",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://fabricmc.net/use/"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Quilt",
                IconUrl = "https://quiltmc.org/assets/brand/logo.svg",
                Description = "Fabric的社区驱动分支，支持更多现代特性",
                Version = "0.19.2",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://quiltmc.org/install/"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "NeoForge",
                IconUrl = "https://neoforged.net/images/logo.png",
                Description = "Forge的现代分支，支持最新游戏版本",
                Version = "1.20.1-62.0.2",
                Type = "Mod Loader",
                Compatible = "1.20.x",
                SourceUrl = "https://neoforged.net/downloads/"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "OptiFine",
                IconUrl = "https://optifine.net/logo.png",
                Description = "性能优化和光影支持，提升游戏帧率",
                Version = "HD U_G8",
                Type = "Optimization",
                Compatible = "1.20.x",
                SourceUrl = "https://optifine.net/downloads"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Sodium",
                IconUrl = "https://cdn.modrinth.com/data/AANobbMI/icon.png",
                Description = "Fabric端的性能优化Mod，大幅提升帧率",
                Version = "0.5.2",
                Type = "Optimization",
                Compatible = "1.20.x (Fabric)",
                SourceUrl = "https://modrinth.com/mod/sodium"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Lithium",
                IconUrl = "https://cdn.modrinth.com/data/gvQqBUqZ/icon.png",
                Description = "Fabric端的游戏逻辑优化，提升整体性能",
                Version = "0.11.2",
                Type = "Optimization",
                Compatible = "1.20.x (Fabric)",
                SourceUrl = "https://modrinth.com/mod/lithium"
            });
            lbDependencies.Items.Add(new DependencyItem
            {
                Name = "Iris",
                IconUrl = "https://cdn.modrinth.com/data/YL57xq9U/icon.png",
                Description = "Fabric端的光影支持Mod，兼容OptiFine光影",
                Version = "1.6.4",
                Type = "Graphics",
                Compatible = "1.20.x (Fabric)",
                SourceUrl = "https://modrinth.com/mod/iris"
            });
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Download");
        }

        private void BtnLoadMore_Click(object sender, RoutedEventArgs e)
        {
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;

            DependencyItem item = button.DataContext as DependencyItem;
            if (item == null) return;

            string modrinthId = GetModrinthId(item.Name);
            if (!string.IsNullOrEmpty(modrinthId))
            {
                try
                {
                    string downloadUrl = await _modrinthApi.GetDownloadUrl(modrinthId);
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        string fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
                        string modsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "mods");
                        string savePath = Path.Combine(modsPath, fileName);

                        if (!Directory.Exists(modsPath))
                        {
                            Directory.CreateDirectory(modsPath);
                        }

                        var task = new GenericDownloadTaskViewModel(downloadUrl, savePath, fileName);
                        DownloadTaskManager.Instance.AddTask(task);
                        _ = task.StartDownloadAsync();

                        MessageBox.Show($"已将 {fileName} 添加到下载任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[依赖库下载] 下载失败: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(item.SourceUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.SourceUrl)
                {
                    UseShellExecute = true
                });
                MessageBox.Show("已在浏览器中打开下载页面:\n" + item.Name, "下载提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            else
            {
                MessageBox.Show("未找到下载链接", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private string GetModrinthId(string name)
        {
            return name switch
            {
                "Sodium" => "AANobbMI",
                "Lithium" => "gvQqBUqZ",
                "Iris" => "YL57xq9U",
                _ => null
            };
        }
    }
}
