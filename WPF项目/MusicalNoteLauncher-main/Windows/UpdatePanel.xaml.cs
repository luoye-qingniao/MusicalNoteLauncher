using System;
using System.Windows;
using System.Windows.Input;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Windows
{
    /// <summary>
    /// 启动器更新面板 —— 展示新版本信息、更新日志、下载进度，
    /// 支持用户选择立即更新或跳过。
    /// </summary>
    public partial class UpdatePanel : Window
    {
        private readonly UpdateService.VersionInfo _versionInfo;
        private bool _isUpdating;

        /// <summary>用户选择了立即更新</summary>
        public bool UpdateAccepted { get; private set; }

        /// <summary>
        /// 创建更新面板
        /// </summary>
        /// <param name="versionInfo">从服务端拉取的版本信息</param>
        public UpdatePanel(UpdateService.VersionInfo versionInfo)
        {
            InitializeComponent();
            _versionInfo = versionInfo;
            LoadVersionInfo();

            // 整个窗口可拖拽
            MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };
        }

        /// <summary>将版本信息填入界面控件</summary>
        private void LoadVersionInfo()
        {
            txtCurrentVersion.Text = UpdateService.CurrentVersion;
            txtNewVersion.Text = _versionInfo.Version;
            txtReleaseNotes.Text = string.IsNullOrWhiteSpace(_versionInfo.ReleaseNotes)
                ? "暂无更新说明"
                : _versionInfo.ReleaseNotes;

            // 文件大小格式化
            if (_versionInfo.FileSize > 0)
                txtFileSize.Text = FormatFileSize(_versionInfo.FileSize);

            // 强制更新：隐藏关闭和跳过按钮
            if (_versionInfo.IsForced)
            {
                forcedTag.Visibility = Visibility.Visible;
                btnSkip.Visibility = Visibility.Collapsed;
                btnClose.Visibility = Visibility.Collapsed;
                txtSubtitle.Text = "此版本为强制更新，请立即更新以继续使用";
            }
        }

        /// <summary>关闭按钮</summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
                Close();
        }

        /// <summary>跳过此版本</summary>
        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
                Close();
        }

        /// <summary>立即更新 —— 开始下载并安装</summary>
        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;
            _isUpdating = true;

            // 切换到下载模式
            btnUpdate.IsEnabled = false;
            btnUpdate.Content = "下载中...";
            btnSkip.IsEnabled = false;
            btnClose.IsEnabled = false;
            progressPanel.Visibility = Visibility.Visible;

            try
            {
                bool success = await UpdateService.DownloadAndInstallAsync(
                    _versionInfo,
                    progress => Dispatcher.Invoke(() => UpdateProgress(progress)));

                if (success)
                {
                    UpdateAccepted = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown(0);
                    });
                }
                else
                {
                    txtProgressStatus.Text = "更新失败，请稍后重试";
                    txtProgressPercent.Text = "";
                    btnUpdate.IsEnabled = true;
                    btnUpdate.Content = "重试更新";
                    btnClose.IsEnabled = true;
                    _isUpdating = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[UpdatePanel] 更新过程异常: {ex.Message}", ex);
                txtProgressStatus.Text = $"更新失败: {ex.Message}";
                txtProgressPercent.Text = "";
                btnUpdate.IsEnabled = true;
                btnUpdate.Content = "重试更新";
                btnClose.IsEnabled = true;
                _isUpdating = false;
            }
        }

        /// <summary>更新下载进度显示</summary>
        private void UpdateProgress(UpdateService.UpdateProgress progress)
        {
            txtProgressStatus.Text = progress.Status;
            txtProgressPercent.Text = $"{progress.ProgressPercent}%";
            downloadProgressBar.Value = progress.ProgressPercent;
        }

        /// <summary>格式化文件大小（字节 → 可读字符串）</summary>
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.#} {suffixes[order]}";
        }
    }
}
