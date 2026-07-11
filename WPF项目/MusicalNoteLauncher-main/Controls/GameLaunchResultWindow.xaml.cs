using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Pages;

namespace MusicalNoteLauncher.Controls
{
    /// <summary>游戏启动结果弹窗</summary>
    public partial class GameLaunchResultWindow : Window
    {
        private readonly GameLaunchInfo _launchInfo;

        public GameLaunchResultWindow(GameLaunchInfo launchInfo)
        {
            InitializeComponent();
            _launchInfo = launchInfo;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BuildGameDetails();
            SetupResultUI();
        }

        private void BuildGameDetails()
        {
            AddDetailRow(spGameDetails, "游戏版本", _launchInfo.VersionId);
            AddDetailRow(spGameDetails, "玩家名称", _launchInfo.Username);
            AddDetailRow(spGameDetails, "分配内存", _launchInfo.Memory);
            AddDetailRow(spGameDetails, "游戏分辨率", _launchInfo.Resolution);
            AddDetailRow(spGameDetails, "启动时间", _launchInfo.LaunchTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private void SetupResultUI()
        {
            if (_launchInfo.IsSuccess)
            {
                // 成功样式
                borderIcon.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                borderIcon.Effect = CreateGlow(46, 204, 113);
                txtTitle.Text = "游戏启动成功";
                txtSubtitle.Text = "Minecraft 已成功开始运行";
                borderError.Visibility = Visibility.Collapsed;
                btnExport.Visibility = Visibility.Collapsed;
                btnAIAnalyze.Visibility = Visibility.Collapsed;

                // 关闭按钮居中
                Grid.SetColumn(btnClose, 0);
                Grid.SetColumnSpan(btnClose, 3);
            }
            else
            {
                // 失败样式
                borderIcon.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                borderIcon.Effect = CreateGlow(231, 76, 60);
                txtIcon.Text = "✕";
                txtTitle.Text = "游戏启动失败";

                borderError.Visibility = Visibility.Visible;
                btnExport.Visibility = Visibility.Visible;
                btnAIAnalyze.Visibility = Visibility.Visible;

                // 退出码
                AddDetailRow(spErrorRows, "退出码", _launchInfo.ExitCode.ToString(), "#E74C3C");
                // 错误描述
                AddDetailRow(spErrorRows, "错误描述", _launchInfo.ErrorMessage ?? "未知错误", "#FF6B6B");

                // ── 本地分析 ──
                var logText = _launchInfo.GetErrorSummary(200);
                var analysisResult = CrashAnalyzer.Analyze(logText, _launchInfo.ExitCode, _launchInfo.ErrorMessage);

                if (analysisResult.IsKnown)
                {
                    // 本地分析命中：显示诊断结果
                    txtSubtitle.Text = $"✅ 已自动诊断：{analysisResult.Title}";

                    // 添加诊断结果区
                    var diagBorder = new Border
                    {
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(14, 12, 14, 12),
                        Background = new SolidColorBrush(Color.FromArgb(0x15, 46, 204, 113)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 46, 204, 113)),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 10, 0, 0),
                    };
                    var diagStack = new StackPanel();

                    diagStack.Children.Add(new TextBlock
                    {
                        Text = "🔍 本地诊断结果",
                        FontSize = 13,
                        FontWeight = System.Windows.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                        Margin = new Thickness(0, 0, 0, 8),
                    });

                    diagStack.Children.Add(new TextBlock
                    {
                        Text = analysisResult.Description,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 10),
                    });
                    diagStack.Children.Add(new TextBlock
                    {
                        Text = $"💡 {analysisResult.Suggestion}",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
                        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                        TextWrapping = TextWrapping.Wrap,
                    });

                    diagBorder.Child = diagStack;
                    spErrorRows.Children.Add(diagBorder);

                    // 按钮文案调整
                    btnAIAnalyze.Content = "🤖 AI 进一步分析";
                }
                else
                {
                    txtSubtitle.Text = "请查看下方错误信息";
                    btnAIAnalyze.Content = "🤖 AI 分析错误";
                }

                // 错误日志预览
                txtErrorLog.Text = Truncate(logText, 800);
            }
        }

        private static DropShadowEffect CreateGlow(byte r, byte g, byte b)
        {
            return new DropShadowEffect
            {
                Color = Color.FromRgb(r, g, b),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.3
            };
        }

        private void AddDetailRow(StackPanel parent, string label, string value, string valueColor = "#BBB")
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 6)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(lbl, 0);

            var val = new TextBlock
            {
                Text = string.IsNullOrEmpty(value) ? "—" : value,
                FontSize = 12,
                Foreground = (ColorConverter.ConvertFromString(valueColor) as Color?) is Color c
                    ? new SolidColorBrush(c)
                    : new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)),
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(val, 1);

            grid.Children.Add(lbl);
            grid.Children.Add(val);
            parent.Children.Add(grid);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|日志文件 (*.log)|*.log|所有文件 (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = $"MNL启动报告_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Title = "导出启动报告"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string report = _launchInfo.ExportReport();
                    File.WriteAllText(dialog.FileName, report, System.Text.Encoding.UTF8);
                    System.Windows.MessageBox.Show(
                        $"报告已导出到:\n{dialog.FileName}",
                        "导出成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"导出失败: {ex.Message}",
                        "导出失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void BtnAIAnalyze_Click(object sender, RoutedEventArgs e)
        {
            // 存储错误信息供 AI 助手页面使用
            AIAssistantPage.SetLaunchErrorForAnalysis(_launchInfo);
            // 关闭弹窗
            Close();
            // 导航到 AI 助手页面
            AppContext.NavigateTo("AIAssistant");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength) + "\n…(内容过长已截断)";
        }
    }
}
