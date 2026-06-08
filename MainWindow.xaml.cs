using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MusicalNoteLauncher.Pages;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher
{
    public partial class MainWindow : Window
    {
        private readonly ConfigManager _config;
        private readonly LauncherCore _launcherCore;
        private readonly Dictionary<string, object> _pageCache = new();
        private string _currentPageKey = "";
        private bool _isNavigating;
        public event Action OnLogout;
        private const int TransitionDuration = 200;
        private const double SlideOffset = 30.0;

        public MainWindow() : this("Player", true) { }

        public MainWindow(string username, bool isOfflineMode)
        {
            InitializeComponent();
            _config = new ConfigManager();
            _launcherCore = new LauncherCore(_config);
            Loaded += async (s, e) => { await PreloadPagesAsync(); NavigateToPage("Home"); };
        }

        private async Task PreloadPagesAsync()
        {
            await Task.Run(async () =>
            {
                var pages = new List<Tuple<string, Func<object>>>
                {
                    Tuple.Create("Home", (Func<object>)(() => new HomePage())),
                    Tuple.Create("Download", (Func<object>)(() => new DownloadPage())),
                    Tuple.Create("Settings", (Func<object>)(() => new SettingsPage())),
                    Tuple.Create("Launch", (Func<object>)(() => new LaunchPage())),
                    Tuple.Create("Profile", (Func<object>)(() => new ProfilePage())),
                    Tuple.Create("Mods", (Func<object>)(() => new ModManagerPage())),
                    Tuple.Create("Modpacks", (Func<object>)(() => new ModpacksPage())),
                    Tuple.Create("JavaConfig", (Func<object>)(() => new JavaConfigPage())),
                    Tuple.Create("GameVersions", (Func<object>)(() => new GameVersionsPage())),
                    Tuple.Create("MultiplayerSocial", (Func<object>)(() => new MultiplayerSocialPage())),
                    Tuple.Create("FriendsList", (Func<object>)(() => new FriendsListPage())),
                    Tuple.Create("Mail", (Func<object>)(() => new MailPage())),
                    Tuple.Create("Community", (Func<object>)(() => new CommunityPage())),
                    Tuple.Create("ComponentStore", (Func<object>)(() => new ComponentStorePage())),
                    Tuple.Create("ModsPage", (Func<object>)(() => new ModsPage())),
                    Tuple.Create("Shaders", (Func<object>)(() => new ShadersPage())),
                    Tuple.Create("Datapacks", (Func<object>)(() => new DatapacksPage())),
                    Tuple.Create("Dependencies", (Func<object>)(() => new DependenciesPage())),
                    Tuple.Create("SaveManager", (Func<object>)(() => new SaveManagerPage())),
                    Tuple.Create("DownloadTask", (Func<object>)(() => new DownloadTaskPage())),
                    Tuple.Create("Test", (Func<object>)(() => new TestPage())),
                    Tuple.Create("Login", (Func<object>)(() => new LoginPage()))
                };
                foreach (var p in pages)
                {
                    try { Application.Current.Dispatcher.Invoke(() => _pageCache[p.Item1] = p.Item2()); }
                    catch { }
                }
            });
        }

        private void NavigateToPage(string pageKey)
        {
            if (_isNavigating || !_pageCache.ContainsKey(pageKey) || pageKey == _currentPageKey) return;
            _isNavigating = true;
            try
            {
                if (_currentPageKey != "" && _pageCache.ContainsKey(_currentPageKey))
                {
                    pageContainerOld.Content = _pageCache[_currentPageKey];
                    pageContainerOld.RenderTransform = new TranslateTransform(0, 0);
                    pageContainerOld.Opacity = 1;
                    pageContainerOld.Visibility = Visibility.Visible;
                }
                else
                {
                    pageContainerOld.Opacity = 0;
                    pageContainerOld.Visibility = Visibility.Collapsed;
                }
                pageContainerNew.Content = _pageCache[pageKey];
                pageContainerNew.RenderTransform = new TranslateTransform(SlideOffset, 0);
                pageContainerNew.Opacity = 0;
                pageContainerNew.Visibility = Visibility.Visible;
                var oldFade = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(TransitionDuration), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } };
                var newSlide = new DoubleAnimation { From = SlideOffset, To = 0, Duration = TimeSpan.FromMilliseconds(TransitionDuration), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } };
                var newFade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(TransitionDuration), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } };
                if (pageContainerOld.Opacity > 0) pageContainerOld.BeginAnimation(UIElement.OpacityProperty, oldFade);
                var transform = pageContainerNew.RenderTransform as TranslateTransform;
                transform?.BeginAnimation(TranslateTransform.XProperty, newSlide);
                pageContainerNew.BeginAnimation(UIElement.OpacityProperty, newFade);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TransitionDuration + 10) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    if (pageContainerOld.Content != null) { pageContainerOld.Content = null; pageContainerOld.Opacity = 0; pageContainerOld.Visibility = Visibility.Collapsed; }
                    pageContainerNew.ClearValue(UIElement.OpacityProperty);
                    pageContainerNew.ClearValue(UIElement.RenderTransformProperty);
                    _currentPageKey = pageKey;
                    _isNavigating = false;
                };
                timer.Start();
            }
            catch { _isNavigating = false; }
        }

        private void BtnNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pageKey) NavigateToPage(pageKey);
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            OnLogout?.Invoke();
            Close();
        }
    }
}
