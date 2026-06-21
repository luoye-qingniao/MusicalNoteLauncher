using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MusicalNoteLauncher.Pages;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher
{
    public partial class MainWindow : Window
    {
        private readonly ConfigManager _config;
        private readonly LauncherCore _launcherCore;
        private readonly Dictionary<string, UserControl> _pageCache = new();
        private string _currentPageKey = "";
        private bool _activeIsA = true;
        private bool _isAnimating;
        public event Action OnLogout;

        public MainWindow() : this("Player", true) { }

        public MainWindow(string username, bool isOfflineMode)
        {
            InitializeComponent();
            _config = new ConfigManager();
            string effectiveMcPath = _config.GetMinecraftPath();
            AppContext.Initialize(username, isOfflineMode, minecraftPath: effectiveMcPath, config: _config);
            _launcherCore = new LauncherCore(_config);
            App.InitializeBedrockServices(effectiveMcPath);
            AppContext.NavigateRequested += pageKey => Dispatcher.Invoke(() => ShowPage(pageKey));
            Loaded += (s, e) => ShowPage("Home");
        }

        private UserControl CreatePage(string key)
        {
            switch (key)
            {
                case "Home": return new HomePage();
                case "Download": return new DownloadPage();
                case "Settings": return new SettingsPage();
                case "Profile": return new ProfilePage();
                case "Mods": return new ModManagerPage();
                case "Modpacks": return new ModpacksPage();
                case "JavaConfig": return new JavaConfigPage();
                case "GameVersions": return new GameVersionsPage();
                case "MultiplayerSocial": return new MultiplayerSocialPage();
                case "FriendsList": return new FriendsListPage();
                case "Mail": return new MailPage();
                case "Community": return new CommunityPage();
                case "ComponentStore": return new ComponentStorePage();
                case "ModsPage": return new ModsPage();
                case "Shaders": return new ShadersPage();
                case "Datapacks": return new DatapacksPage();
                case "Dependencies": return new DependenciesPage();
                case "SaveManager": return new SaveManagerPage();
                case "DownloadTask": return new DownloadTaskPage();
                case "Login": return new LoginPage();
                case "RecommendDetail": return new RecommendDetailPage();
                case "LoaderSelection": return new LoaderSelectionPage();
                case "LoaderVersion": return new LoaderVersionPage();
                default: return null;
            }
        }

        private ContentControl Active => _activeIsA ? pageContainerA : pageContainerB;
        private ContentControl Inactive => _activeIsA ? pageContainerB : pageContainerA;

        private void ShowPage(string pageKey)
        {
            if (_isAnimating || pageKey == _currentPageKey) return;

            try
            {
                if (!_pageCache.ContainsKey(pageKey))
                {
                    var page = CreatePage(pageKey);
                    if (page == null)
                    {
                        MessageBox.Show($"鏈煡椤甸潰: {pageKey}", "閿欒", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    _pageCache[pageKey] = page;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"椤甸潰 {pageKey} 鍔犺浇澶辫触:\n{ex}", "閿欒", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _isAnimating = true;

            // 鏂伴〉闈㈡斁鍏ラ潪娲昏穬瀹瑰櫒
            var newContainer = Inactive;
            newContainer.Content = _pageCache[pageKey];
            newContainer.RenderTransform = new TranslateTransform(30, 0);
            newContainer.Opacity = 0;
            newContainer.Visibility = Visibility.Visible;

            var oldContainer = Active;

            var sb = new Storyboard();
            const double duration = 0.2;

            // 鏃ч〉闈㈡贰鍑?            if (oldContainer.Content != null)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(duration))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                Storyboard.SetTarget(fadeOut, oldContainer);
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(fadeOut);
            }

// 新页面滑入
            var slideIn = new DoubleAnimation(30, 0, TimeSpan.FromSeconds(duration))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(slideIn, newContainer);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            sb.Children.Add(slideIn);

// 新页面淡入
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(duration))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(fadeIn, newContainer);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fadeIn);

            sb.Completed += (_, _) =>
            {
                // 娓呯悊鏃у鍣?                if (oldContainer.Content != null)
                {
                    oldContainer.Content = null;
                    oldContainer.Opacity = 0;
                    oldContainer.Visibility = Visibility.Collapsed;
                    oldContainer.RenderTransform = null;
                }
                // 娓呯悊鏂板鍣ㄥ姩鐢绘畫鐣?                newContainer.ClearValue(UIElement.OpacityProperty);
                newContainer.RenderTransform = null;

                _activeIsA = !_activeIsA;
                _currentPageKey = pageKey;
                _isAnimating = false;
            };

            sb.Begin();
        }

        private void BtnNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pageKey)
            {
                ShowPage(pageKey);
                UpdateNavButtonStyle(button);
            }
        }

        private void UpdateNavButtonStyle(Button activeButton)
        {
            var navButtons = new[] { btnHome, btnDownload, btnMods, btnSaves, btnMultiplayer, btnDownloadTask, btnCommunity, btnComponentStore, btnProfile, btnFriends, btnSettings };
            
            foreach (var btn in navButtons)
            {
                if (btn == activeButton)
                {
                    btn.Foreground = new SolidColorBrush(Colors.White);
                    btn.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                }
                else
                {
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
                    btn.Background = Brushes.Transparent;
                }
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            OnLogout?.Invoke();
            Close();
        }
    }
}






