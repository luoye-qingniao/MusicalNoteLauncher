using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class ModManagerPage : UserControl
    {
        private readonly ConfigManager _config;
        private List<ModLoaderDetector.LoaderInfo> _detectedLoaders = new List<ModLoaderDetector.LoaderInfo>();

        public ModManagerPage()
        {
            InitializeComponent();
            _config = new ConfigManager();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CheckModLoaderAndUpdateUI();
        }

        private void CheckModLoaderAndUpdateUI()
        {
            string minecraftPath = _config.GetMinecraftPath();
            string gameVersion = _config.GameVersion;

            if (!string.IsNullOrEmpty(gameVersion))
            {
                AppContext.SelectedGameVersion = gameVersion;
            }

            // 扫描所有已安装的加载器（如果设置了 MC 版本，优先显示该版本的加载器）
            _detectedLoaders = ModLoaderDetector.DetectAllLoaders(minecraftPath, gameVersion);

            if (txtHintVersion != null)
            {
                if (string.IsNullOrEmpty(gameVersion))
                {
                    txtHintVersion.Text = "当前未选择游戏版本。以下是 .minecraft 中找到的所有加载器。";
                }
                else
                {
                    txtHintVersion.Text = "当前游戏版本：Minecraft " + gameVersion
                                          + "（位置：" + minecraftPath + "）"
                                          + "，已找到 " + _detectedLoaders.Count + " 个加载器。";
                }
            }

            if (_detectedLoaders.Count == 0)
            {
                if (gridNoLoader != null) gridNoLoader.Visibility = Visibility.Visible;
                if (gridHasLoader != null) gridHasLoader.Visibility = Visibility.Collapsed;
                if (txtLoaderInfo != null) txtLoaderInfo.Text = "（无模组加载器）";
                return;
            }

            if (gridNoLoader != null) gridNoLoader.Visibility = Visibility.Collapsed;
            if (gridHasLoader != null) gridHasLoader.Visibility = Visibility.Visible;
            if (txtLoaderInfo != null)
            {
                List<string> names = _detectedLoaders.ConvertAll(l => l.DisplayName);
                txtLoaderInfo.Text = "（已检测到：" + string.Join(", ", names) + "）";
            }
            LoadMods();
        }

        private string GetModsDirectory()
        {
            string minecraftPath = _config.GetMinecraftPath();
            if (SettingsManager.Settings != null
                && SettingsManager.Settings.EnableVersionIsolation
                && !string.IsNullOrEmpty(_config.GameVersion))
            {
                return Path.Combine(minecraftPath, "versions", _config.GameVersion, "mods");
            }
            return Path.Combine(minecraftPath, "mods");
        }

        private void LoadMods()
        {
            if (lstMods == null) return;

            lstMods.Items.Clear();

            string modsDirectory = GetModsDirectory();
            if (!Directory.Exists(modsDirectory))
            {
                try
                {
                    Directory.CreateDirectory(modsDirectory);
                }
                catch
                {
                    // ignore
                }
            }

            if (Directory.Exists(modsDirectory))
            {
                foreach (string filePath in Directory.GetFiles(modsDirectory, "*.jar"))
                {
                    string fileName = Path.GetFileName(filePath);
                    bool isEnabled = !fileName.StartsWith(".");
                    string displayName = isEnabled ? fileName : fileName.Substring(1);

                    string iconPath = ExtractModIcon(filePath);

                    lstMods.Items.Add(new ModItem
                    {
                        ModName = displayName,
                        DisplayName = displayName,
                        ModVersion = GetVersionFromFileName(displayName),
                        IsEnabled = isEnabled,
                        FilePath = filePath,
                        StatusText = isEnabled ? "已启用" : "已禁用",
                        StatusColor = isEnabled ? "#81C784" : "#E57373",
                        IconPath = iconPath,
                        IconImage = iconPath != null
                            ? new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath, UriKind.Absolute))
                            : null
                    });
                }
            }

            bool hasItems = lstMods.Items.Count > 0;
            if (lstMods != null) lstMods.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            if (panelEmptyMods != null)
            {
                panelEmptyMods.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
                if (txtEmptyModsHint != null)
                {
                    txtEmptyModsHint.Text =
                        "当前 mods 目录：" + (modsDirectory ?? "(未知)") + "\n"
                        + "点击下方 “添加模组” 按钮，选择 .jar 文件即可添加。";
                }
            }

            if (txtModListTitle != null)
            {
                txtModListTitle.Text = "已安装模组";
            }
            if (txtModCountBadge != null)
            {
                txtModCountBadge.Text = "共 " + lstMods.Items.Count + " 个";
            }
        }

        private string GetVersionFromFileName(string fileName)
        {
            string[] parts = Path.GetFileNameWithoutExtension(fileName).Split('-');
            if (parts.Length >= 2)
            {
                string version = parts.Last();
                if (version.All(c => char.IsDigit(c) || c == '.'))
                {
                    return version;
                }
            }
            return "未知版本";
        }

        private void BtnAddMod_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "模组文件 (*.jar)|*.jar|所有文件 (*.*)|*.*",
                Multiselect = true,
                Title = "选择模组文件"
            };

            if (openFileDialog.ShowDialog().GetValueOrDefault())
            {
                string modsDirectory = GetModsDirectory();
                if (!Directory.Exists(modsDirectory))
                {
                    try { Directory.CreateDirectory(modsDirectory); }
                    catch (Exception ex)
                    {
                        MessageBox.Show("无法创建 mods 目录：" + ex.Message, "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                int added = 0;
                int skipped = 0;

                foreach (string filePath in openFileDialog.FileNames)
                {
                    try
                    {
                        string destPath = Path.Combine(modsDirectory, Path.GetFileName(filePath));
                        if (!File.Exists(destPath))
                        {
                            File.Copy(filePath, destPath);
                            added++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning("添加模组失败: " + ex.Message);
                    }
                }

                LoadMods();

                string msg = "已添加 " + added + " 个模组。";
                if (skipped > 0) msg += " 跳过 " + skipped + " 个已存在的模组。";
                MessageBox.Show(msg, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private List<ModItem> GetSelectedItems()
        {
            if (lstMods == null) return new List<ModItem>();
            return lstMods.SelectedItems.Cast<ModItem>().ToList();
        }

        private void BtnEnableMods_Click(object sender, RoutedEventArgs e)
        {
            List<ModItem> selectedItems = GetSelectedItems();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先在列表中选择要启用的模组（可使用 Ctrl / Shift 多选）。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int done = 0;
            foreach (ModItem item in selectedItems)
            {
                if (!item.IsEnabled)
                {
                    try
                    {
                        string directory = Path.GetDirectoryName(item.FilePath);
                        string newFileName = Path.GetFileName(item.FilePath).Substring(1);
                        string newPath = Path.Combine(directory, newFileName);
                        File.Move(item.FilePath, newPath);
                        item.FilePath = newPath;
                        item.IsEnabled = true;
                        done++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning("启用模组失败: " + ex.Message);
                    }
                }
            }

            LoadMods();
            MessageBox.Show("已启用 " + done + " 个模组。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDisableMods_Click(object sender, RoutedEventArgs e)
        {
            List<ModItem> selectedItems = GetSelectedItems();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先在列表中选择要禁用的模组（可使用 Ctrl / Shift 多选）。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int done = 0;
            foreach (ModItem item in selectedItems)
            {
                if (item.IsEnabled)
                {
                    try
                    {
                        string directory = Path.GetDirectoryName(item.FilePath);
                        string newFileName = "." + Path.GetFileName(item.FilePath);
                        string newPath = Path.Combine(directory, newFileName);
                        File.Move(item.FilePath, newPath);
                        item.FilePath = newPath;
                        item.IsEnabled = false;
                        done++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning("禁用模组失败: " + ex.Message);
                    }
                }
            }

            LoadMods();
            MessageBox.Show("已禁用 " + done + " 个模组。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnUninstallMod_Click(object sender, RoutedEventArgs e)
        {
            List<ModItem> selectedItems = GetSelectedItems();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先在列表中选择要卸载的模组。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(
                    "确定要卸载 " + selectedItems.Count + " 个模组吗？此操作不可撤销。",
                    "确认卸载",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            int done = 0;
            foreach (ModItem item in selectedItems)
            {
                try
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                        done++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning("卸载模组失败: " + ex.Message);
                }
            }

            LoadMods();
            MessageBox.Show("已卸载 " + done + " 个模组。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            CheckModLoaderAndUpdateUI();
        }

        private void BtnRefreshMods_Click(object sender, RoutedEventArgs e)
        {
            CheckModLoaderAndUpdateUI();
        }

        private void BtnModMarket_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("ComponentStore");
        }

        private void BtnInstallForge_Click(object sender, RoutedEventArgs e)
        {
            NavigateToLoaderSelection("Forge");
        }

        private void BtnOpenLoaderSelection_Click(object sender, RoutedEventArgs e)
        {
            NavigateToLoaderSelection(null);
        }

        private void BtnInstallSpecificLoader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string loaderType)
            {
                NavigateToLoaderSelection(loaderType);
            }
        }

        private void NavigateToLoaderSelection(string targetLoaderType)
        {
            string gameVersion = _config.GameVersion;
            if (string.IsNullOrEmpty(gameVersion))
            {
                if (MessageBox.Show(
                        "当前未设置游戏版本，无法继续安装模组加载器。\n是否前往 “游戏版本” 页面选择一个版本？",
                        "未选择游戏版本",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    AppContext.NavigateTo("GameVersions");
                }
                return;
            }

            AppContext.SelectedGameVersion = gameVersion;

            if (!string.IsNullOrEmpty(targetLoaderType))
            {
                // 直接跳转到对应加载器的版本选择页面，更快捷
                AppContext.SelectedLoaderType = targetLoaderType;
                AppContext.NavigateTo("LoaderVersion");
            }
            else
            {
                AppContext.NavigateTo("LoaderSelection");
            }
        }

        public class ModItem
        {
            public string ModName { get; set; }
            public string DisplayName { get; set; }
            public string ModVersion { get; set; }
            public bool IsEnabled { get; set; }
            public string FilePath { get; set; }
            public string StatusText { get; set; }
            public string StatusColor { get; set; }
            public string IconPath { get; set; }
            public System.Windows.Media.ImageSource IconImage { get; set; }
        }

        private string ExtractModIcon(string jarPath)
        {
            try
            {
                string iconDir = Path.Combine(
                    Path.GetDirectoryName(jarPath) ?? "",
                    ".mod_icons"
                );
                string iconName = Path.GetFileNameWithoutExtension(jarPath) + ".png";
                string iconPath = Path.Combine(iconDir, iconName);

                if (File.Exists(iconPath))
                    return iconPath;

                using (var archive = System.IO.Compression.ZipFile.OpenRead(jarPath))
                {
                    // 常见图标文件名列表
                    string[] iconNames = {
                        "pack.png", "logo.png", "icon.png", "mod_icon.png",
                        "icon_32.png", "icon_64.png", "mod_logo.png",
                        "thumbnail.png", "texture.png"
                    };

                    // 优先查找根目录
                    foreach (string name in iconNames)
                    {
                        var entry = archive.GetEntry(name);
                        if (entry != null)
                        {
                            Directory.CreateDirectory(iconDir);
                            using (var entryStream = entry.Open())
                            using (var fileStream = File.Create(iconPath))
                                entryStream.CopyTo(fileStream);
                            return iconPath;
                        }
                    }

                    // 遍历所有条目，按文件名匹配
                    foreach (var entry in archive.Entries)
                    {
                        string entryName = Path.GetFileName(entry.FullName);
                        foreach (string candidate in iconNames)
                        {
                            if (string.Equals(entryName, candidate, StringComparison.OrdinalIgnoreCase))
                            {
                                Directory.CreateDirectory(iconDir);
                                using (var entryStream = entry.Open())
                                using (var fileStream = File.Create(iconPath))
                                    entryStream.CopyTo(fileStream);
                                return iconPath;
                            }
                        }
                    }

                    // 回退：找第一个较小的 png（排除大纹理文件）
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                            && entry.Length < 256 * 1024) // 小于256KB
                        {
                            Directory.CreateDirectory(iconDir);
                            using (var entryStream = entry.Open())
                            using (var fileStream = File.Create(iconPath))
                                entryStream.CopyTo(fileStream);
                            return iconPath;
                        }
                    }
                }
            }
            catch
            {
                // 提取失败时返回 null，使用默认图标
            }
            return null;
        }
    }
}
