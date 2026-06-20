using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Windows
{
    public partial class VersionDownloader : Window
    {
        private readonly VersionDownloadService _downloadService;
        private readonly List<VersionInfo> _allVersions = new List<VersionInfo>();
        private readonly ObservableCollection<VersionInfo> _displayVersions = new ObservableCollection<VersionInfo>();
        private CancellationTokenSource _loadingCts;
        private string _selectedVersionId;
        private bool _isLoading = false;
        private bool _isOfflineMode = false;
        private ForgeInstaller _forgeInstaller;
        private List<ForgeInstaller.ForgeVersionInfo> _forgeVersions;

        public VersionDownloader(string minecraftPath)
        {
            InitializeComponent();
            _downloadService = new VersionDownloadService(minecraftPath);
            _downloadService.StatusChanged += DownloadService_StatusChanged;
            _downloadService.ProgressChanged += DownloadService_ProgressChanged;
            _downloadService.DownloadCompleted += DownloadService_DownloadCompleted;
            _downloadService.DownloadFailed += DownloadService_DownloadFailed;

            _forgeInstaller = new ForgeInstaller(minecraftPath);
            _forgeInstaller.StatusChanged += ForgeInstaller_StatusChanged;
            _forgeInstaller.ProgressChanged += ForgeInstaller_ProgressChanged;

            lstVersions.ItemsSource = _displayVersions;
            Loaded += VersionDownloader_Loaded;
        }

        private void ForgeInstaller_StatusChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                txtVersionInfo.Text = status;
            });
        }

        private void ForgeInstaller_ProgressChanged(int progress)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = progress;
                txtProgress.Text = $"{progress}%";
            });
        }

        private void DownloadService_StatusChanged(string status)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DownloadService_StatusChanged(status));
                return;
            }
            txtVersionCount.Text = status;
        }

        private void DownloadService_ProgressChanged(int progress)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DownloadService_ProgressChanged(progress));
                return;
            }
            progressBar.Value = progress;
        }

        private void DownloadService_DownloadCompleted(string versionId)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DownloadService_DownloadCompleted(versionId));
                return;
            }
            MessageBox.Show($"版本 {versionId} 下载完成！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            _ = LoadVersionsAsync();
        }

        private void DownloadService_DownloadFailed(string versionId, string error)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DownloadService_DownloadFailed(versionId, error));
                return;
            }
            MessageBox.Show($"版本 {versionId} 下载失败:\n{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void VersionDownloader_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadVersionsAsync();
        }

        private async Task LoadVersionsAsync()
        {
            if (_isLoading) return;

            _isLoading = true;
            _loadingCts?.Cancel();
            _loadingCts = new CancellationTokenSource();

            loadingPanel.Visibility = Visibility.Visible;
            lstVersions.IsEnabled = false;
            btnDownload.IsEnabled = false;

            _allVersions.Clear();
            _displayVersions.Clear();

            try
            {
                txtVersionCount.Text = "正在获取版本列表...";

                var versions = await _downloadService.GetRemoteVersionsAsync(_loadingCts.Token);

                if (versions != null && versions.Count > 0)
                {
                    _isOfflineMode = false;

                    _allVersions.AddRange(versions);
                    _allVersions.Sort((a, b) => b.ReleaseTime.CompareTo(a.ReleaseTime));

                    ApplyFilter();

                    txtVersionCount.Text = $"共 {_displayVersions.Count} 个版本（正式版）";
                    btnDownload.IsEnabled = !_isOfflineMode && _displayVersions.Count > 0;
                }
                else
                {
                    await LoadLocalVersionsAsync();
                }
            }
            catch (OperationCanceledException)
            {
                txtVersionCount.Text = "已取消加载";
            }
            catch (Exception ex)
            {
                Logger.Error($"加载版本列表失败: {ex.Message}");

                var result = MessageBox.Show(
                    $"获取版本列表失败:\n{ex.Message}\n\n是否使用离线模式（仅显示本地版本）？",
                    "网络错误",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await LoadLocalVersionsAsync();
                }
                else
                {
                    txtVersionCount.Text = "加载已取消";
                }
            }
            finally
            {
                _isLoading = false;
                loadingPanel.Visibility = Visibility.Collapsed;
                lstVersions.IsEnabled = true;
            }
        }

        private async Task LoadLocalVersionsAsync()
        {
            _isOfflineMode = true;
            txtVersionCount.Text = "离线模式 - 正在加载本地版本...";

            try
            {
                var localVersions = await _downloadService.GetLocalVersionsAsync();

                _allVersions.Clear();
                _allVersions.AddRange(localVersions);
                _allVersions.Sort((a, b) => b.ReleaseTime.CompareTo(a.ReleaseTime));

                ApplyFilter();

                if (_displayVersions.Count > 0)
                {
                    txtVersionCount.Text = $"离线模式 - 共 {_displayVersions.Count} 个本地版本";
                }
                else
                {
                    txtVersionCount.Text = "离线模式 - 本地无版本";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"加载本地版本失败: {ex.Message}");
                txtVersionCount.Text = "离线模式 - 加载失败";
            }
        }

        private void ApplyFilter()
        {
            _displayVersions.Clear();

            IEnumerable<VersionInfo> filtered = _allVersions;

            bool showAll = chkShowAllVersions?.IsChecked ?? false;
            if (!showAll)
            {
                filtered = filtered.Where(v => 
                    !string.IsNullOrEmpty(v.Type) && 
                    string.Equals(v.Type, "release", StringComparison.OrdinalIgnoreCase));
            }

            foreach (var version in filtered)
            {
                _displayVersions.Add(version);
            }
        }

        private void chkShowAllVersions_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
            txtVersionCount.Text = $"共 {_displayVersions.Count} 个版本";
        }

        private async void lstVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstVersions.SelectedItem is VersionInfo version)
            {
                _selectedVersionId = version.Id;

                string typeText = version.Type == "release" ? "正式版" :
                                  version.Type == "snapshot" ? "快照版" : version.Type;
                txtVersionInfo.Text = $"{version.Id} | {typeText} | 发布于 {version.ReleaseTime:yyyy-MM-dd}";

                // 检测是否支持 Forge
                await LoadForgeVersions(version.Id);

                if (version.IsDownloaded)
                {
                    txtVersionInfo.Text += " (已下载)";
                    btnDownload.IsEnabled = false;
                }
                else
                {
                    btnDownload.IsEnabled = !_isOfflineMode;
                }
            }
            else
            {
                _selectedVersionId = null;
                txtVersionInfo.Text = "请选择一个版本";
                btnDownload.IsEnabled = false;
                chkInstallForge.IsChecked = false;
                pnlForgeVersion.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadForgeVersions(string mcVersion)
        {
            try
            {
                _forgeVersions = await _forgeInstaller.GetForgeVersionsAsync(mcVersion);
                
                if (_forgeVersions.Count > 0)
                {
                    chkInstallForge.IsEnabled = true;
                    
                    cmbForgeVersion.Items.Clear();
                    foreach (var forgeVer in _forgeVersions)
                    {
                        string displayText = forgeVer.IsRecommended 
                            ? $"{forgeVer.Version} (推荐)" 
                            : forgeVer.Version;
                        cmbForgeVersion.Items.Add(new ComboBoxItem { Content = displayText, Tag = forgeVer });
                    }
                    
                    // 默认选择推荐版本
                    var recommended = _forgeVersions.FirstOrDefault(v => v.IsRecommended) ?? _forgeVersions.FirstOrDefault();
                    if (recommended != null)
                    {
                        cmbForgeVersion.SelectedIndex = _forgeVersions.IndexOf(recommended);
                    }
                }
                else
                {
                    chkInstallForge.IsEnabled = false;
                    chkInstallForge.IsChecked = false;
                }
            }
            catch
            {
                chkInstallForge.IsEnabled = false;
                chkInstallForge.IsChecked = false;
            }
        }

        private void chkInstallForge_Checked(object sender, RoutedEventArgs e)
        {
            pnlForgeVersion.Visibility = Visibility.Visible;
        }

        private void chkInstallForge_Unchecked(object sender, RoutedEventArgs e)
        {
            pnlForgeVersion.Visibility = Visibility.Collapsed;
        }

        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedVersionId))
            {
                MessageBox.Show("请先选择一个版本！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_isOfflineMode)
            {
                MessageBox.Show("当前处于离线模式，无法下载新版本", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedVersion = _allVersions.FirstOrDefault(v => v.Id == _selectedVersionId);
            if (selectedVersion == null)
            {
                MessageBox.Show("未找到选中的版本信息", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            btnDownload.IsEnabled = false;
            btnPause.IsEnabled = true;
            btnCancel.IsEnabled = true;
            progressBar.Value = 0;
            txtProgress.Text = "0% (0 MB / 0 MB)";
            txtSpeed.Text = "速度: --";

            using (var cts = new CancellationTokenSource())
            {
                _loadingCts = cts;

                try
                {
                    var downloadProgress = new DownloadProgress();
                    downloadProgress.ProgressChanged += (info) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            progressBar.Value = info.Progress;
                            string downloadedMB = (info.DownloadedBytes / 1024.0 / 1024.0).ToString("0.00");
                            string totalMB = (info.TotalBytes / 1024.0 / 1024.0).ToString("0.00");
                            txtProgress.Text = $"{info.Progress:F1}% ({downloadedMB} MB / {totalMB} MB)";
                            txtVersionInfo.Text = $"正在下载: {info.CurrentFile}";
                        });
                    };

                    var result = await _downloadService.StartDownloadAsync(selectedVersion, downloadProgress, cts.Token);

                    if (result.IsCompleted)
                    {
                        // 如果勾选了安装 Forge
                        if (chkInstallForge.IsChecked == true && cmbForgeVersion.SelectedItem is ComboBoxItem selectedForgeItem)
                        {
                            var forgeVersion = selectedForgeItem.Tag as ForgeInstaller.ForgeVersionInfo;
                            if (forgeVersion != null)
                            {
                                txtVersionInfo.Text = $"正在安装 Forge {forgeVersion.Version}...";
                                progressBar.Value = 0;

                                bool forgeSuccess = await _forgeInstaller.InstallForgeAsync(_selectedVersionId, forgeVersion, cts.Token);
                                
                                if (forgeSuccess)
                                {
                                    MessageBox.Show($"版本 {_selectedVersionId} 下载完成！\nForge {forgeVersion.Version} 安装成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                else
                                {
                                    MessageBox.Show($"版本 {_selectedVersionId} 下载完成！\nForge 安装失败，请手动安装", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show($"版本 {_selectedVersionId} 下载完成！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        
                        await LoadVersionsAsync();
                    }
                    else
                    {
                        MessageBox.Show($"版本 {_selectedVersionId} 下载失败:\n{result.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("下载已取消", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error($"下载失败: {ex.Message}");
                    MessageBox.Show($"下载失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btnDownload.IsEnabled = !string.IsNullOrEmpty(_selectedVersionId) && !_isOfflineMode;
                    btnPause.IsEnabled = false;
                    btnCancel.IsEnabled = false;
                    _loadingCts = null;
                }
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadVersionsAsync();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            _loadingCts?.Cancel();
            Close();
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_loadingCts != null)
            {
                _loadingCts.Cancel();
                MessageBox.Show("已暂停下载", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            _loadingCts?.Cancel();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _loadingCts?.Cancel();
            base.OnClosing(e);
        }
    }
}