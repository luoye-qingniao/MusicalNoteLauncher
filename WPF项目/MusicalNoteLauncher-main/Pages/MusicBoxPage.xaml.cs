using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Models;

namespace MusicalNoteLauncher.Pages
{
    public partial class MusicBoxPage : UserControl
    {
        private readonly MusicApiService _api;
        private readonly MusicCacheManager _cache;
        private readonly MusicPlayerService _player;
        private readonly MusicAccountService _account;

        private string _lastSearchKeyword = "";
        private bool _isSearching;
        private bool _isProgressDragging;

        // 推荐歌单缓存
        private List<PlaylistInfo> _recommendPlaylists = new();

        public MusicBoxPage()
        {
            // 必须在 InitializeComponent 之前初始化，因为 XAML 中 Slider.Value 会触发 ValueChanged
            _api = new MusicApiService();
            _cache = new MusicCacheManager();
            _player = MusicPlayerService.Instance;
            _account = MusicAccountService.Instance;

            InitializeComponent();

            _player.TrackChanged += OnPlayerTrackChanged;
            _player.PlayStateChanged += OnPlayerPlayStateChanged;
            _account.AccountChanged += OnAccountChanged;

            // 绑定 MediaElement
            _player.BindPlayer(mediaPlayer);

            // 占位符提示联动
            txtSearch.TextChanged += (s, e) =>
            {
                lblPlaceholder.Visibility = string.IsNullOrEmpty(txtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            };
            lblPlaceholder.Visibility = Visibility.Visible;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateAccountUI();
            _ = LoadRecommendPlaylistsAsync();
            SyncPlayerUI();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 不停止播放，只解绑事件避免内存泄漏
        }

        // ──────────── 推荐歌单 ────────────

        /// <summary>显示浮动提示弹窗</summary>
        private void ShowPopup(string icon, string text, string sub = "")
        {
            popupIcon.Text = icon;
            popupText.Text = text;
            popupSub.Text = sub;
            popupHint.Visibility = Visibility.Visible;
        }

        /// <summary>隐藏浮动提示弹窗</summary>
        private void HidePopup()
        {
            popupHint.Visibility = Visibility.Collapsed;
        }

        private async Task LoadRecommendPlaylistsAsync()
        {
            try
            {
                ShowPopup("⏳", "加载中...");

                _recommendPlaylists = await _api.GetRecommendPlaylistsAsync(20);

                if (_recommendPlaylists.Count > 0)
                {
                    lblSearchResultTitle.Text = "🎵 推荐歌单";
                    lblResultCount.Text = $"{_recommendPlaylists.Count} 个歌单";
                    listMain.ItemsSource = _recommendPlaylists;
                    HidePopup();
                }
                else
                {
                    ShowPopup("⚠️", "无法连接到网易云音乐服务器", "请检查网络连接，海外用户可能需要中国大陆网络环境");
                }
            }
            catch (HttpRequestException)
            {
                ShowPopup("❌", "网络连接失败", "无法访问 music.163.com，请检查网络和防火墙设置");
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicBox] 加载推荐失败: {ex.Message}");
                ShowPopup("❌", "加载失败", ex.Message);
            }
        }

        // ──────────── 搜索 ────────────

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            await DoSearchAsync();
        }

        private async void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await DoSearchAsync();
        }

        private async Task DoSearchAsync()
        {
            if (_isSearching) return;
            string keyword = txtSearch.Text?.Trim();
            if (string.IsNullOrEmpty(keyword)) return;

            _isSearching = true;
            _lastSearchKeyword = keyword;

            ShowPopup("🔍", "搜索中...");

            try
            {
                var result = await _api.SearchSongsAsync(keyword, 30, 0);

                if (result.Songs.Count > 0)
                {
                    // 检查缓存状态
                    foreach (var track in result.Songs)
                    {
                        if (_cache.IsCached(track.Id))
                            track.CachedFilePath = _cache.GetCachePath(track.Id);
                    }

                    HidePopup();
                    lblSearchResultTitle.Text = "🔍 搜索结果";
                    lblResultCount.Text = $"共 {result.TotalCount} 首";
                    listMain.ItemsSource = result.Songs;
                }
                else
                {
                    ShowPopup("🔍", "未找到相关歌曲", "试试其他关键词");
                }
            }
            finally
            {
                _isSearching = false;
            }
        }

        // ──────────── 歌单 / 歌曲点击 ────────────

        private async void TrackCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (element.Tag is MusicTrack track)
                    await PlayTrackInternal(track);
                else if (element.Tag is PlaylistInfo playlist)
                    await LoadPlaylistTracksAsync(playlist);
            }
        }

        private async Task LoadPlaylistTracksAsync(PlaylistInfo playlist)
        {
            ShowPopup("📋", "加载歌单中...", playlist.Name);

            var tracks = await _api.GetPlaylistTracksAsync(playlist.Id, 50);

            if (tracks.Count > 0)
            {
                foreach (var t in tracks)
                {
                    if (_cache.IsCached(t.Id))
                        t.CachedFilePath = _cache.GetCachePath(t.Id);
                }

                HidePopup();
                lblSearchResultTitle.Text = $"📋 {playlist.Name}";
                lblResultCount.Text = $"{tracks.Count} 首";
                listMain.ItemsSource = tracks;
                _lastSearchKeyword = ""; // 重置搜索状态
            }
            else
            {
                ShowPopup("📋", "该歌单暂无歌曲");
            }
        }

        /// <summary>播放单曲（获取 URL，加入播放列表，开始播放）</summary>
        private async Task PlayTrackInternal(MusicTrack track)
        {
            // 如果已缓存则直接播放
            if (!track.IsCached)
            {
                // 获取播放 URL
                if (string.IsNullOrEmpty(track.PlayUrl))
                {
                    string? url = await _api.GetSongUrlAsync(track.Id);
                    if (string.IsNullOrEmpty(url))
                    {
                        MessageBox.Show("无法获取播放地址，该歌曲可能受版权保护", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    track.PlayUrl = url;
                }
            }

            _player.PlayNow(track);
            RefreshPlaylistUI();
        }

        // ──────────── 当前播放列表操作 ────────────

        private void PlaylistTrack_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is MusicTrack track)
            {
                int index = _player.Playlist.IndexOf(track);
                if (index >= 0)
                    _player.PlayIndex(index);
            }
        }

        private void BtnClearPlaylist_Click(object sender, RoutedEventArgs e)
        {
            _player.Stop();
            _player.Playlist.Clear();
            RefreshPlaylistUI();
            SyncPlayerUI();
        }

        /// <summary>添加搜索结果全部到播放列表</summary>
        private async void BtnAddAll_Click(object sender, RoutedEventArgs e)
        {
            if (listMain.ItemsSource is IEnumerable<MusicTrack> tracks)
            {
                foreach (var track in tracks)
                {
                    if (!_player.Playlist.Contains(track))
                    {
                        if (!track.IsCached && string.IsNullOrEmpty(track.PlayUrl))
                        {
                            track.PlayUrl = await _api.GetSongUrlAsync(track.Id) ?? "";
                        }
                        _player.AddToPlaylist(track);
                    }
                }
                RefreshPlaylistUI();
            }
        }

        private void RefreshPlaylistUI()
        {
            listPlaylist.ItemsSource = null;
            listPlaylist.ItemsSource = _player.Playlist;
            lblPlaylistCount.Text = _player.Playlist.Count > 0 ? $"{_player.Playlist.Count} 首" : "";
        }

        // ──────────── 播放器控制 ────────────

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            _player.TogglePlayPause();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            _player.Previous();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            _player.Next();
        }

        private void BtnPlayMode_Click(object sender, RoutedEventArgs e)
        {
            _player.PlayMode = (_player.PlayMode + 1) % 4;
            btnPlayMode.Content = _player.PlayModeIcon;
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _player.Volume = sliderVolume.Value / 100.0;
        }

        private void SliderProgress_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isProgressDragging = true;
        }

        private void SliderProgress_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isProgressDragging = false;
            _player.Seek(sliderProgress.Value / 100.0 * _player.TotalDuration);
        }

        private void SliderProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isProgressDragging) return;
            // 拖拽过程中不 seek，松开鼠标后才 seek
        }

        private void BtnCacheCurrent_Click(object sender, RoutedEventArgs e)
        {
            _ = CacheCurrentTrackAsync();
        }

        private async Task CacheCurrentTrackAsync()
        {
            var track = _player.CurrentTrack;
            if (track == null) return;

            btnCacheCurrent.IsEnabled = false;
            btnCacheCurrent.Content = "⏳";

            try
            {
                string? path = await _cache.DownloadAndCacheAsync(track);
                if (path != null)
                {
                    btnCacheCurrent.Content = "✅";
                    MessageBox.Show($"已缓存: {track.Name}", "缓存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    btnCacheCurrent.Content = "❌";
                    MessageBox.Show("缓存失败，请检查网络", "缓存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                await Task.Delay(2000);
                btnCacheCurrent.Content = "📥";
                btnCacheCurrent.IsEnabled = true;
            }
        }

        // ──────────── 播放器事件回调 ────────────

        private void OnPlayerTrackChanged(MusicTrack? track)
        {
            Dispatcher.Invoke(() =>
            {
                if (track == null)
                {
                    txtCurrentTrack.Text = "未在播放";
                    txtCurrentArtist.Text = "";
                    imgCurrentCover.Source = null;
                    return;
                }

                txtCurrentTrack.Text = track.Name;
                txtCurrentArtist.Text = track.Artist;

                // 加载封面
                if (!string.IsNullOrEmpty(track.AlbumCoverUrl))
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(track.AlbumCoverUrl);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        imgCurrentCover.Source = bmp;
                    }
                    catch { imgCurrentCover.Source = null; }
                }
                else
                {
                    imgCurrentCover.Source = null;
                }

                RefreshPlaylistUI();
            });
        }

        private void OnPlayerPlayStateChanged(bool isPlaying)
        {
            Dispatcher.Invoke(() =>
            {
                btnPlayPause.Content = isPlaying ? "⏸" : "▶";
            });
        }

        // ──────────── UI 定时更新 ────────────

        private void SyncPlayerUI()
        {
            btnPlayPause.Content = _player.IsPlaying ? "⏸" : "▶";
            btnPlayMode.Content = _player.PlayModeIcon;
            sliderVolume.Value = _player.Volume * 100;
            RefreshPlaylistUI();

            if (_player.CurrentTrack != null)
            {
                txtCurrentTrack.Text = _player.CurrentTrack.Name;
                txtCurrentArtist.Text = _player.CurrentTrack.Artist;
            }

            // 启动定时更新进度条
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (s, e) =>
            {
                if (!_isProgressDragging && _player.TotalDuration > 0)
                {
                    sliderProgress.Value = _player.ProgressPercent;
                    txtPosition.Text = _player.PositionText;
                    txtDuration.Text = _player.TotalDurationText;
                }
            };
            timer.Start();
        }

        // ──────────── MediaElement 事件 ────────────

        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Play();
            btnPlayPause.Content = "⏸";
            RefreshPlaylistUI();
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            _player.Next();
        }

        private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Logger.Error($"[MusicBox] 播放失败: {e.ErrorException?.Message}");
            btnPlayPause.Content = "▶";
            MessageBox.Show("播放失败，请稍后重试", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ──────────── 账号登录 ────────────

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            _ = StartQrLoginAsync();
        }

        private void PanelLoggedIn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 点击已登录面板可退出
            var result = MessageBox.Show($"当前登录: {_account.CurrentAccount?.Nickname}\n\n是否退出登录？",
                "音乐账号", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _account.Logout();
                UpdateAccountUI();
            }
        }

        private async Task StartQrLoginAsync()
        {
            btnLogin.IsEnabled = false;
            btnLogin.Content = "⏳ 获取二维码...";

            try
            {
                string? key = await _account.GetQrKeyAsync();
                if (string.IsNullOrEmpty(key))
                {
                    MessageBox.Show("无法获取登录二维码，请检查 API 服务", "登录失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string? qrBase64 = await _account.CreateQrImageAsync(key);
                if (string.IsNullOrEmpty(qrBase64))
                {
                    MessageBox.Show("无法生成二维码", "登录失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 显示二维码窗口
                ShowQrCodeWindow(qrBase64, key);
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicBox] 登录异常: {ex.Message}");
                MessageBox.Show("登录异常，请检查网络", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnLogin.Content = "🎵 登录网易云";
                btnLogin.IsEnabled = true;
            }
        }

        private void ShowQrCodeWindow(string qrBase64, string key)
        {
            var window = new Window
            {
                Title = "扫码登录网易云音乐",
                Width = 340,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E))
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 二维码图片
            var img = new System.Windows.Controls.Image { Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(20) };
            try
            {
                byte[] bytes = Convert.FromBase64String(qrBase64.Replace("data:image/png;base64,", ""));
                using var ms = new System.IO.MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                img.Source = bmp;
            }
            catch { }

            grid.Children.Add(img);
            Grid.SetRow(img, 0);

            // 提示文字
            var txtStatus = new TextBlock
            {
                Text = "请使用网易云音乐 APP 扫码登录",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray),
                FontSize = 14,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            grid.Children.Add(txtStatus);
            Grid.SetRow(txtStatus, 1);

            // 关闭按钮
            var btnClose = new Button
            {
                Content = "取消",
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x38, 0x38, 0x38)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24, 8, 24, 8),
                FontSize = 13,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            btnClose.Click += (_, _) => window.Close();
            grid.Children.Add(btnClose);
            Grid.SetRow(btnClose, 2);

            window.Content = grid;

            // 轮询扫码状态
            var pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            bool isClosed = false;
            window.Closed += (_, _) => { isClosed = true; pollTimer.Stop(); };

            pollTimer.Tick += async (_, _) =>
            {
                if (isClosed) return;

                var (code, cookie) = await _account.CheckQrStatusAsync(key);

                window.Dispatcher.Invoke(() =>
                {
                    switch (code)
                    {
                        case 800:
                            txtStatus.Text = "等待扫码...";
                            break;
                        case 801:
                            txtStatus.Text = "请在手机上确认登录";
                            break;
                        case 802:
                            txtStatus.Text = "登录成功！";
                            pollTimer.Stop();
                            window.Close();
                            _ = _account.LoginWithCookieAsync(cookie ?? "");
                            Dispatcher.Invoke(() => UpdateAccountUI());
                            break;
                        case 803:
                            txtStatus.Text = "二维码已过期，请重新获取";
                            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0xE7, 0x4C, 0x3C));
                            pollTimer.Stop();
                            break;
                    }
                });
            };

            pollTimer.Start();
            window.ShowDialog();
            pollTimer.Stop();
        }

        // ──────────── 账号 UI 更新 ────────────

        private void UpdateAccountUI()
        {
            var account = _account.CurrentAccount;
            if (account != null && account.IsLoggedIn)
            {
                btnLogin.Visibility = Visibility.Collapsed;
                panelLoggedIn.Visibility = Visibility.Visible;
                txtNickname.Text = account.Nickname;

                if (!string.IsNullOrEmpty(account.AvatarUrl))
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(account.AvatarUrl);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        imgAvatar.Source = bmp;
                    }
                    catch { }
                }
            }
            else
            {
                btnLogin.Visibility = Visibility.Visible;
                panelLoggedIn.Visibility = Visibility.Collapsed;
            }
        }

        private void OnAccountChanged(MusicAccountInfo? account)
        {
            Dispatcher.Invoke(() => UpdateAccountUI());
        }
    }
}
