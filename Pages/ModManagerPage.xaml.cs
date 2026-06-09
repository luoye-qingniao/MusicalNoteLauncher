using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class ModManagerPage : UserControl
    {
        private readonly ConfigManager _config;

        public ModManagerPage()
        {
            InitializeComponent();
            _config = new ConfigManager();
            CheckModLoaderAndUpdateUI();
        }

        private void CheckModLoaderAndUpdateUI()
        {
            string minecraftPath = _config.GetMinecraftPath();
            string gameVersion = _config.GameVersion;
            ModLoaderDetector.ModLoaderType modLoaderType = ModLoaderDetector.DetectModLoader(minecraftPath, gameVersion);
            if (modLoaderType == ModLoaderDetector.ModLoaderType.None)
            {
                if (gridNoLoader != null) gridNoLoader.Visibility = Visibility.Visible;
                if (gridHasLoader != null) gridHasLoader.Visibility = Visibility.Collapsed;
                if (txtLoaderInfo != null) txtLoaderInfo.Text = "（无模组加载器）";
                return;
            }
            if (gridNoLoader != null) gridNoLoader.Visibility = Visibility.Collapsed;
            if (gridHasLoader != null) gridHasLoader.Visibility = Visibility.Visible;
            if (txtLoaderInfo != null) txtLoaderInfo.Text = "（" + ModLoaderDetector.GetLoaderDisplayName(modLoaderType) + "）";
            LoadMods();
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

        private void LoadMods()
        {
            if (lstMods != null)
            {
                lstMods.Items.Clear();
                string modsDirectory = GetModsDirectory();
                if (!Directory.Exists(modsDirectory))
                {
                    Directory.CreateDirectory(modsDirectory);
                    return;
                }
                foreach (string filePath in Directory.GetFiles(modsDirectory, "*.jar"))
                {
                    string fileName = Path.GetFileName(filePath);
                    bool isEnabled = !fileName.StartsWith(".");
                    lstMods.Items.Add(new ModItem
                    {
                        ModName = (isEnabled ? fileName : fileName.Substring(1)),
                        ModVersion = GetVersionFromFileName(fileName),
                        IsEnabled = isEnabled,
                        FilePath = filePath
                    });
                }
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
                foreach (string filePath in openFileDialog.FileNames)
                {
                    string destPath = Path.Combine(modsDirectory, Path.GetFileName(filePath));
                    if (!File.Exists(destPath))
                    {
                        File.Copy(filePath, destPath);
                    }
                }
                LoadMods();
                MessageBox.Show("模组添加成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }

        private void BtnEnableMods_Click(object sender, RoutedEventArgs e)
        {
            if (lstMods == null) return;
            List<ModItem> selectedItems = lstMods.SelectedItems.Cast<ModItem>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要启用的模组", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            foreach (ModItem item in selectedItems)
            {
                if (!item.IsEnabled)
                {
                    string filePath = item.FilePath;
                    string destFileName = filePath.Replace(Path.GetFileName(filePath), Path.GetFileName(filePath).Substring(1));
                    File.Move(filePath, destFileName);
                    item.IsEnabled = true;
                }
            }
            LoadMods();
            MessageBox.Show(string.Format("已启用 {0} 个模组", selectedItems.Count), "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private void BtnDisableMods_Click(object sender, RoutedEventArgs e)
        {
            if (lstMods == null) return;
            List<ModItem> selectedItems = lstMods.SelectedItems.Cast<ModItem>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要禁用的模组", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            foreach (ModItem item in selectedItems)
            {
                if (item.IsEnabled)
                {
                    string filePath = item.FilePath;
                    string destFileName = filePath.Replace(Path.GetFileName(filePath), "." + Path.GetFileName(filePath));
                    File.Move(filePath, destFileName);
                    item.IsEnabled = false;
                }
            }
            LoadMods();
            MessageBox.Show(string.Format("已禁用 {0} 个模组", selectedItems.Count), "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private void BtnUninstallMod_Click(object sender, RoutedEventArgs e)
        {
            if (lstMods == null) return;
            List<ModItem> selectedItems = lstMods.SelectedItems.Cast<ModItem>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要卸载的模组", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            if (MessageBox.Show(string.Format("确定要卸载 {0} 个模组吗？此操作不可撤销。", selectedItems.Count), "确认卸载", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
            {
                foreach (ModItem item in selectedItems)
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                    }
                }
                LoadMods();
                MessageBox.Show(string.Format("已卸载 {0} 个模组", selectedItems.Count), "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }

        private void BtnRefreshMods_Click(object sender, RoutedEventArgs e)
        {
            CheckModLoaderAndUpdateUI();
            MessageBox.Show("模组列表已刷新", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        private void BtnModMarket_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("ComponentStore");
        }

        private void BtnInstallForge_Click(object sender, RoutedEventArgs e)
        {
            AppContext.NavigateTo("Dependencies");
        }

        public class ModItem
        {
            public string ModName { get; set; }
            public string ModVersion { get; set; }
            public bool IsEnabled { get; set; }
            public string FilePath { get; set; }
        }
    }
}
