using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Controls
{
    /// <summary>
    /// 下载确认弹窗：选择安装到哪个游戏版本、自定义目录和文件名
    /// </summary>
    public class DownloadConfirmDialog : Window
    {
        /// <summary>用户确认的完整保存路径</summary>
        public string SavePath { get; private set; }

        /// <summary>用户自定义的文件名（不含路径）</summary>
        public string FileName { get; private set; }

        private readonly string _minecraftPath;
        private readonly string _defaultVersion;
        private readonly List<string> _versions;
        private TextBox _txtFileName;
        private TextBlock _lblDir;
        private TextBlock _previewLabel;
        private ComboBox _versionCombo;
        private string _targetDir;
        private string _defaultFileName;
        private string _resourceName;

        /// <summary>
        /// 创建下载确认弹窗
        /// </summary>
        /// <param name="resourceName">资源名称（模组/整合包名等）</param>
        /// <param name="defaultDir">默认安装目录</param>
        /// <param name="defaultFileName">默认文件名</param>
        /// <param name="minecraftPath">Minecraft 根目录</param>
        /// <param name="versions">已安装的游戏版本列表</param>
        /// <param name="currentVersion">当前启动器选中的版本</param>
        public DownloadConfirmDialog(string resourceName, string defaultDir, string defaultFileName,
            string minecraftPath, List<string> versions, string currentVersion)
        {
            _resourceName = resourceName;
            _targetDir = defaultDir;
            _defaultFileName = defaultFileName;
            _minecraftPath = minecraftPath;
            _defaultVersion = currentVersion;
            _versions = versions ?? new List<string>();

            MinWidth = 520;
            MinHeight = 420;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;
            Title = $"下载确认 - {resourceName}";

            var textPri = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
            var textSec = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
            var surfaceBg = TryFindResource("SurfaceBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            var cardBg = TryFindResource("CardBackgroundBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            var borderBr = TryFindResource("BorderBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
            var primaryBrush = TryFindResource("PrimaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x1E, 0xBB, 0x8E));

            var root = new Border
            {
                Background = cardBg,
                CornerRadius = new CornerRadius(16),
                BorderBrush = borderBr,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4)
            };

            var panel = new StackPanel { Margin = new Thickness(24) };

            // 标题栏
            var titleBar = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition());
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.Children.Add(new TextBlock
            {
                Text = $"📥 下载确认 - {resourceName}",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPri,
                FontFamily = new FontFamily("Microsoft YaHei")
            });
            var closeBtn = new Button
            {
                Content = "✕", FontSize = 14, Cursor = Cursors.Hand,
                Foreground = textSec, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            closeBtn.Click += (s, ev) => { DialogResult = false; Close(); };
            Grid.SetColumn(closeBtn, 1);
            titleBar.Children.Add(closeBtn);
            panel.Children.Add(titleBar);

            // ── 游戏版本选择 ──
            panel.Children.Add(new TextBlock
            {
                Text = "安装到哪个游戏版本",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPri,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var versionRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            versionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            versionRow.ColumnDefinitions.Add(new ColumnDefinition());

            _versionCombo = new ComboBox
            {
                Width = 180,
                FontSize = 13,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Background = surfaceBg,
                Foreground = textPri,
                BorderBrush = borderBr,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            // 填充版本列表
            var versionItems = new List<string>();
            if (!string.IsNullOrEmpty(_defaultVersion))
                versionItems.Add(_defaultVersion);
            foreach (var v in _versions)
            {
                if (v != _defaultVersion && !versionItems.Contains(v))
                    versionItems.Add(v);
            }
            foreach (var v in versionItems)
                _versionCombo.Items.Add(v);

            _versionCombo.SelectedItem = _defaultVersion ?? versionItems.FirstOrDefault();
            _versionCombo.SelectionChanged += VersionCombo_SelectionChanged;
            Grid.SetColumn(_versionCombo, 0);
            versionRow.Children.Add(_versionCombo);

            var globalBtn = new Button
            {
                Content = "📂 全局 mods",
                FontSize = 12,
                Padding = new Thickness(12, 6, 12, 6),
                Foreground = textPri,
                Background = surfaceBg,
                BorderBrush = borderBr,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            globalBtn.Click += (s, ev) =>
            {
                _targetDir = Path.Combine(_minecraftPath, "mods");
                _lblDir.Text = _targetDir;
                UpdatePreview();
            };
            Grid.SetColumn(globalBtn, 1);
            versionRow.Children.Add(globalBtn);
            panel.Children.Add(versionRow);

            panel.Children.Add(new TextBlock
            {
                Text = "💡 选择版本后自动定位到该版本的隔离 mods 目录",
                FontSize = 11,
                Foreground = textSec,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 14)
            });

            // ── 安装目录 ──
            panel.Children.Add(new TextBlock
            {
                Text = "安装目录",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPri,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var dirRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            dirRow.ColumnDefinitions.Add(new ColumnDefinition());
            dirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _lblDir = new TextBlock
            {
                Text = _targetDir,
                FontSize = 12,
                Foreground = textSec,
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_lblDir, 0);
            dirRow.Children.Add(_lblDir);

            var browseBtn = new Button
            {
                Content = "浏览...",
                FontSize = 12,
                Padding = new Thickness(12, 6, 12, 6),
                Foreground = textPri,
                Background = surfaceBg,
                BorderBrush = borderBr,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            browseBtn.Click += BrowseBtn_Click;
            Grid.SetColumn(browseBtn, 1);
            dirRow.Children.Add(browseBtn);
            panel.Children.Add(dirRow);

            panel.Children.Add(new TextBlock
            {
                Text = "💡 可点击「浏览...」自定义安装目录",
                FontSize = 11,
                Foreground = textSec,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 14)
            });

            // ── 文件名 ──
            panel.Children.Add(new TextBlock
            {
                Text = "文件名",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPri,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            });

            _txtFileName = new TextBox
            {
                Text = _defaultFileName,
                FontSize = 14,
                Padding = new Thickness(12, 10, 12, 10),
                Background = surfaceBg,
                Foreground = textPri,
                BorderBrush = borderBr,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(_txtFileName);

            panel.Children.Add(new TextBlock
            {
                Text = $"💡 最终保存路径: {Path.Combine(_targetDir, _defaultFileName)}",
                FontSize = 11,
                Foreground = textSec,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 18),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Name = "PreviewPath"
            });
            _previewLabel = (TextBlock)panel.Children[panel.Children.Count - 1];

            _txtFileName.TextChanged += (s, ev) => UpdatePreview();

            // ── 按钮行 ──
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelBtn = new Button
            {
                Content = "取消",
                FontSize = 13,
                Padding = new Thickness(16, 8, 16, 8),
                Foreground = textPri,
                Background = Brushes.Transparent,
                BorderBrush = borderBr,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            cancelBtn.Click += (s, ev) => { DialogResult = false; Close(); };
            btnRow.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content = "确认下载",
                FontSize = 13,
                Padding = new Thickness(16, 8, 16, 8),
                Foreground = Brushes.White,
                Background = primaryBrush,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Microsoft YaHei")
            };
            okBtn.Click += OkBtn_Click;
            btnRow.Children.Add(okBtn);
            panel.Children.Add(btnRow);

            root.Child = panel;
            Content = root;

            UpdatePreview();
        }

        private void VersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_versionCombo.SelectedItem is string selectedVersion)
            {
                // 计算该版本的 mods 目录
                if (SettingsManager.Settings.ShouldIsolateVersionForVersion(_minecraftPath, selectedVersion))
                    _targetDir = Path.Combine(_minecraftPath, "versions", selectedVersion, "game", "mods");
                else
                    _targetDir = Path.Combine(_minecraftPath, "mods");

                _lblDir.Text = _targetDir;
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            var fn = _txtFileName?.Text?.Trim() ?? _defaultFileName;
            _previewLabel.Text = $"💡 最终保存路径: {Path.Combine(_targetDir, fn)}";
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择安装目录",
                ShowNewFolderButton = true,
                AutoUpgradeEnabled = true
            };

            if (Directory.Exists(_targetDir))
                dialog.SelectedPath = _targetDir;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _targetDir = dialog.SelectedPath;
                _lblDir.Text = _targetDir;
                UpdatePreview();
            }
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            var fn = _txtFileName?.Text?.Trim();
            if (string.IsNullOrEmpty(fn))
            {
                ModernMessageBox.ShowWarning("请输入文件名", "提示");
                return;
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                if (fn.Contains(c))
                {
                    ModernMessageBox.ShowWarning($"文件名包含无效字符: {c}", "提示");
                    return;
                }
            }

            FileName = fn;
            SavePath = Path.Combine(_targetDir, fn);
            DialogResult = true;
            Close();
        }
    }
}
