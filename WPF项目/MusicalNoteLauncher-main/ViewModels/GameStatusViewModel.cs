using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Models;

namespace MusicalNoteLauncher.ViewModels
{
    public class GameStatusViewModel : INotifyPropertyChanged
    {
        private string _statusText = "等待启动...";
        private Brush _statusColor = new SolidColorBrush(Color.FromRgb(170, 170, 170));
        private bool _isGameRunning = false;
        private ObservableCollection<LogEntry> _logs = new ObservableCollection<LogEntry>();
        private GameLauncher _gameLauncher;
        private TextBlock _statusTextBlock;
        private TextBlock _statusIndicator;
        private ItemsControl _consoleControl;

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (SetProperty(ref _statusText, value))
                {
                    UpdateStatusTextBlock();
                }
            }
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set
            {
                if (SetProperty(ref _statusColor, value))
                {
                    UpdateStatusIndicator();
                }
            }
        }

        public bool IsGameRunning
        {
            get => _isGameRunning;
            set => SetProperty(ref _isGameRunning, value);
        }

        public ObservableCollection<LogEntry> Logs
        {
            get => _logs;
            set => SetProperty(ref _logs, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public GameStatusViewModel(GameLauncher gameLauncher = null)
        {
            _gameLauncher = gameLauncher;
            InitializeLauncherEvents();
        }

        public void SetUIElements(TextBlock statusText, TextBlock statusIndicator, ItemsControl consoleControl)
        {
            _statusTextBlock = statusText;
            _statusIndicator = statusIndicator;
            _consoleControl = consoleControl;
            
            // Initialize UI with current values
            UpdateStatusTextBlock();
            UpdateStatusIndicator();
        }

        private void UpdateStatusTextBlock()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_statusTextBlock != null)
                {
                    _statusTextBlock.Text = _statusText;
                }
            });
        }

        private void UpdateStatusIndicator()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_statusIndicator != null)
                {
                    _statusIndicator.Foreground = _statusColor;
                }
            });
        }

        private void InitializeLauncherEvents()
        {
            if (_gameLauncher == null) return;

            _gameLauncher.LaunchStatusChanged += OnLaunchStatusChanged;
            _gameLauncher.LaunchLogReceived += OnLaunchLogReceived;
            _gameLauncher.LaunchCompleted += OnLaunchCompleted;
        }

        private void OnLaunchStatusChanged(string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = status;
                
                if (status.Contains("已启动") || status.Contains("启动成功"))
                {
                    StatusColor = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                    IsGameRunning = true;
                }
                else if (status.Contains("启动中") || status.Contains("初始化") || status.Contains("加载"))
                {
                    StatusColor = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                }
                else if (status.Contains("已退出") || status.Contains("失败") || status.Contains("异常"))
                {
                    StatusColor = new SolidColorBrush(Color.FromRgb(170, 170, 170));
                    IsGameRunning = false;
                }
                else
                {
                    StatusColor = new SolidColorBrush(Color.FromRgb(170, 170, 170));
                }
            });
        }

        private void OnLaunchLogReceived(string log)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                bool isError = log.StartsWith("[ERROR]") || log.Contains("Exception") || log.Contains("Error");
                Logs.Add(new LogEntry(log, isError));
            });
        }

        private void OnLaunchCompleted(bool success)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsGameRunning = false;
                if (success)
                {
                    StatusText = "游戏已退出";
                    StatusColor = new SolidColorBrush(Color.FromRgb(170, 170, 170));
                }
                else
                {
                    StatusText = "游戏启动失败";
                    StatusColor = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                }
            });
        }

        public void ClearLogs()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Clear();
            });
        }

        public void SetLauncher(GameLauncher launcher)
        {
            _gameLauncher = launcher;
            InitializeLauncherEvents();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
