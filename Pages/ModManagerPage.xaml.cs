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
            Unloaded += OnUnloaded;
            VersionScanService.Instance.ScanCompleted += OnVersionScanCompleted;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            VersionScanService.Instance.ScanCompleted -= OnVersionScanCompleted;
        }

        private void OnVersionScanCompleted(VersionScanResult result)
        {
            // 扫描完成后用 UI 线程重新填充下拉框与模组列表
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PopulateVersionComboBox();
                LoadMods();
            }));
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 先尽可能用当前缓存填充（如果有），保证进入页面能立即显示
            CheckModLoaderAndUpdateUI();

            // 同时触发一次异步扫描：确保已安装版本列表是最新的
            _ = VersionScanService.Instance.ScanAsync("模组管理页面加载");
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

            // 填充版本切换下拉框
            PopulateVersionComboBox();

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

        private void PopulateVersionComboBox()
        {
            if (cmbVersion == null) return;

            string currentVersion = _config.GameVersion ?? "";
            cmbVersion.SelectionChanged -= CmbVersion_SelectionChanged;

            cmbVersion.Items.Clear();

            // 优先使用 VersionScanService 的缓存；如果缓存为空（比如首次进入页面），
            // 直接从磁盘的 versions 目录同步读取一次，保证下拉框能立即显示版本。
            var installedVersions = VersionScanService.Instance.GetInstalledJavaVersions();
            if (installedVersions == null || installedVersions.Count == 0)
            {
                installedVersions = ScanVersionsFallback();
            }

            // 添加 "全局" 选项（使用 .minecraft/mods 目录）
            cmbVersion.Items.Add("全局");
            foreach (var version in installedVersions)
            {
                if (!cmbVersion.Items.Contains(version))
                    cmbVersion.Items.Add(version);
            }

            // 将当前版本也加入列表（即使未安装）
            if (!string.IsNullOrEmpty(currentVersion) && !cmbVersion.Items.Contains(currentVersion))
            {
                cmbVersion.Items.Add(currentVersion);
            }

            // 选中当前版本
            if (!string.IsNullOrEmpty(currentVersion) && cmbVersion.Items.Contains(currentVersion))
            {
                cmbVersion.SelectedItem = currentVersion;
            }
            else
            {
                cmbVersion.SelectedItem = "全局";
            }

            cmbVersion.SelectionChanged += CmbVersion_SelectionChanged;
        }

        /// <summary>
        /// 直接从 versions 目录读取已安装版本名，不需要依赖 VersionScanService 的缓存。
        /// 作为首次进入页面时的兜底逻辑。
        /// </summary>
        private List<string> ScanVersionsFallback()
        {
            var result = new List<string>();
            try
            {
                string minecraftPath = _config.GetMinecraftPath();
                if (string.IsNullOrEmpty(minecraftPath)) return result;

                string versionsDir = Path.Combine(minecraftPath, "versions");
                if (!Directory.Exists(versionsDir)) return result;

                foreach (var dir in Directory.GetDirectories(versionsDir))
                {
                    string dirName = Path.GetFileName(dir);
                    // 只保留看起来像版本目录的（目录内必须存在相同名称的 .json 或 .jar 文件）
                    string json = Path.Combine(dir, dirName + ".json");
                    string jar = Path.Combine(dir, dirName + ".jar");
                    if (File.Exists(json) || File.Exists(jar))
                    {
                        result.Add(dirName);
                    }
                }

                result.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                Logger.Warning("[模组管理] 回退扫描版本失败: " + ex.Message);
            }
            return result;
        }

        private bool _suppressVersionChange;
        private void CmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressVersionChange || cmbVersion.SelectedItem == null) return;

            string selectedVersion = cmbVersion.SelectedItem.ToString();

            if (selectedVersion == "全局")
            {
                _config.GameVersion = "";
            }
            else
            {
                _config.GameVersion = selectedVersion;
            }

            _suppressVersionChange = true;
            CheckModLoaderAndUpdateUI();
            _suppressVersionChange = false;
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

                    lstMods.Items.Add(new ModItem
                    {
                        ModName = displayName,
                        DisplayName = displayName,
                        ModVersion = GetVersionFromFileName(displayName),
                        IsEnabled = isEnabled,
                        FilePath = filePath,
                        StatusText = isEnabled ? "已启用" : "已禁用",
                        StatusColor = isEnabled ? "#81C784" : "#E57373"
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
                string gameVersion = _config.GameVersion;
                if (string.IsNullOrEmpty(gameVersion))
                {
                    if (MessageBox.Show(
                            "当前未设置游戏版本，无法继续安装模组加载器。\n是否前往【游戏版本】页面选择一个版本？",
                            "未选择游戏版本",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        AppContext.NavigateTo("GameVersions");
                    }
                    return;
                }

                AppContext.SelectedGameVersion = gameVersion;
                // 设置 loaderType 让 LoaderSelection 页面知道要预选哪个加载器
                AppContext.SelectedLoaderType = loaderType;
                AppContext.NavigateTo("LoaderSelection");
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
        }
    }
}
