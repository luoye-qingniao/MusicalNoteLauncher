using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;

namespace MusicalNoteLauncher.Windows
{
    public partial class DownloadPanel : Window
    {
        private readonly string _minecraftPath;
        private readonly List<DownloadTaskViewModel> _tasks = new List<DownloadTaskViewModel>();

        public event Action DownloadCompleted;

        public DownloadPanel(string minecraftPath)
        {
            InitializeComponent();
            _minecraftPath = minecraftPath;
        }

        public void AddDownloadTask(VersionInfo versionInfo)
        {
            if (_tasks.Exists(t => t.VersionId == versionInfo.Id && !t.IsCompleted && !t.IsFailed && !t.IsCancelled))
            {
                MessageBox.Show($"版本 {versionInfo.Id} 正在下载中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var taskViewModel = new DownloadTaskViewModel(versionInfo, _minecraftPath);
            _tasks.Add(taskViewModel);

            CreateTaskItem(taskViewModel);
            UpdateEmptyPanelVisibility();
            UpdateTaskCount();

            _ = StartTaskAsync(taskViewModel);
        }

        private void CreateTaskItem(DownloadTaskViewModel viewModel)
        {
            Border taskBorder = new Border
            {
                Background = (Brush)FindResource("SurfaceBrush"),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(16)
            };

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 版本信息行
            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(headerGrid, 0);

            TextBlock versionText = new TextBlock
            {
                Text = viewModel.VersionId,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush")
            };
            Grid.SetColumn(versionText, 0);
            headerGrid.Children.Add(versionText);

            TextBlock statusText = new TextBlock
            {
                Text = viewModel.Status,
                FontSize = 12,
                Foreground = GetStatusColor(viewModel.Status),
                Margin = new Thickness(12, 0, 0, 0)
            };
            statusText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Status") { Source = viewModel });
            statusText.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Status") { Source = viewModel, Converter = new StatusColorConverter() });
            Grid.SetColumn(statusText, 1);
            headerGrid.Children.Add(statusText);

            grid.Children.Add(headerGrid);

            // 进度条
            Border progressBorder = new Border
            {
                Height = 6,
                Background = (Brush)FindResource("CardBackgroundBrush"),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 12, 0, 8)
            };
            Grid.SetRow(progressBorder, 1);

            ProgressBar progressBar = new ProgressBar
            {
                Height = 6,
                Background = Brushes.Transparent,
                Foreground = (Brush)FindResource("PrimaryBrush"),
                BorderThickness = new Thickness(0),
                Value = viewModel.Progress
            };
            progressBar.SetBinding(ProgressBar.ValueProperty, new System.Windows.Data.Binding("Progress") { Source = viewModel });
            progressBorder.Child = progressBar;
            grid.Children.Add(progressBorder);

            // 进度信息行
            Grid infoGrid = new Grid();
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(infoGrid, 2);

            TextBlock progressPercent = new TextBlock
            {
                Text = $"{viewModel.Progress:F1}%",
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush")
            };
            progressPercent.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("ProgressText") { Source = viewModel });
            Grid.SetColumn(progressPercent, 0);
            infoGrid.Children.Add(progressPercent);

            TextBlock sizeText = new TextBlock
            {
                Text = viewModel.SizeText,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(8, 0, 0, 0)
            };
            sizeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("SizeText") { Source = viewModel });
            Grid.SetColumn(sizeText, 1);
            infoGrid.Children.Add(sizeText);

            TextBlock speedText = new TextBlock
            {
                Text = $"速度: {viewModel.DownloadSpeed}",
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(8, 0, 0, 0)
            };
            speedText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DownloadSpeed") { Source = viewModel, StringFormat = "速度: {0}" });
            Grid.SetColumn(speedText, 2);
            infoGrid.Children.Add(speedText);

            TextBlock timeText = new TextBlock
            {
                Text = viewModel.RemainingTime,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(8, 0, 0, 0)
            };
            timeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("RemainingTime") { Source = viewModel });
            Grid.SetColumn(timeText, 3);
            infoGrid.Children.Add(timeText);

            grid.Children.Add(infoGrid);

            // 当前文件和操作按钮行
            Grid fileGrid = new Grid();
            fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(fileGrid, 3);

            TextBlock fileText = new TextBlock
            {
                Text = string.IsNullOrEmpty(viewModel.CurrentFile) ? "" : $"正在下载: {viewModel.CurrentFile}",
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 8, 0, 0)
            };
            fileText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CurrentFile") { Source = viewModel, Converter = new FileNameConverter() });
            Grid.SetColumn(fileText, 0);
            fileGrid.Children.Add(fileText);

            Button cancelButton = new Button
            {
                Content = "取消",
                Style = (Style)FindResource("SecondaryButton"),
                Width = 60,
                Height = 28,
                Margin = new Thickness(8, 8, 0, 0)
            };
            cancelButton.Click += (s, e) => viewModel.Cancel();
            cancelButton.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding("IsDownloading") { Source = viewModel, Converter = new BoolToVisibilityConverter() });
            Grid.SetColumn(cancelButton, 1);
            fileGrid.Children.Add(cancelButton);

            grid.Children.Add(fileGrid);

            // 错误信息
            TextBlock errorText = new TextBlock
            {
                Text = viewModel.ErrorMessage,
                FontSize = 12,
                Foreground = Brushes.Red,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = string.IsNullOrEmpty(viewModel.ErrorMessage) ? Visibility.Collapsed : Visibility.Visible
            };
            errorText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("ErrorMessage") { Source = viewModel });
            errorText.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding("IsFailed") { Source = viewModel, Converter = new BoolToVisibilityConverter() });
            Grid.SetRow(errorText, 4);
            grid.Children.Add(errorText);

            taskBorder.Child = grid;

            // Tag用于后续查找和移除
            taskBorder.Tag = viewModel.VersionId;

            taskPanel.Children.Add(taskBorder);
        }

        private async System.Threading.Tasks.Task StartTaskAsync(DownloadTaskViewModel viewModel)
        {
            await viewModel.StartDownloadAsync();

            if (viewModel.IsCompleted)
            {
                DownloadCompleted?.Invoke();
            }
        }

        private void UpdateEmptyPanelVisibility()
        {
            emptyPanel.Visibility = _tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateTaskCount()
        {
            int activeCount = _tasks.Count(t => !t.IsCompleted && !t.IsFailed && !t.IsCancelled);
            int completedCount = _tasks.Count(t => t.IsCompleted);
            txtTaskCount.Text = $"{activeCount} 个下载中, {completedCount} 个已完成";
        }

        private Brush GetStatusColor(string status)
        {
            switch (status)
            {
                case "下载中":
                    return (Brush)FindResource("PrimaryBrush");
                case "已完成":
                    return Brushes.Green;
                case "下载失败":
                    return Brushes.Red;
                case "已取消":
                    return Brushes.Gray;
                default:
                    return (Brush)FindResource("TextSecondaryBrush");
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void btnClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            var completedTasks = _tasks.FindAll(t => t.IsCompleted || t.IsFailed || t.IsCancelled);
            foreach (var task in completedTasks)
            {
                _tasks.Remove(task);
                // 移除UI元素
                for (int i = taskPanel.Children.Count - 1; i >= 0; i--)
                {
                    if (taskPanel.Children[i] is Border border && border.Tag as string == task.VersionId)
                    {
                        taskPanel.Children.RemoveAt(i);
                        break;
                    }
                }
            }
            UpdateEmptyPanelVisibility();
            UpdateTaskCount();
        }
    }

    public class StatusColorConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string status = value as string;
            switch (status)
            {
                case "下载中":
                    return new SolidColorBrush(Color.FromRgb(33, 150, 243));
                case "已完成":
                    return Brushes.Green;
                case "下载失败":
                    return Brushes.Red;
                case "已取消":
                    return Brushes.Gray;
                default:
                    return new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FileNameConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string fileName = value as string;
            return string.IsNullOrEmpty(fileName) ? "" : $"正在下载: {fileName}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isVisible = value is bool boolValue && boolValue;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
