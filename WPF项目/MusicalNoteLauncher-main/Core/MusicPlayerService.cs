using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MusicalNoteLauncher.Models;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 音乐播放服务 —— 管理播放状态、播放列表、当前曲目
    /// </summary>
    public class MusicPlayerService : INotifyPropertyChanged
    {
        private static MusicPlayerService? _instance;
        public static MusicPlayerService Instance => _instance ??= new MusicPlayerService();

        private MediaElement? _mediaElement;
        private readonly DispatcherTimer _updateTimer;

        // ── 播放列表 ──
        public ObservableCollection<MusicTrack> Playlist { get; } = new();
        private int _currentIndex = -1;

        // ── 播放状态 ──
        private MusicTrack? _currentTrack;
        public MusicTrack? CurrentTrack
        {
            get => _currentTrack;
            private set { _currentTrack = value; OnPropertyChanged(); }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            private set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayPauseIcon)); }
        }

        private double _volume = 0.7;
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0, 1);
                if (_mediaElement != null) _mediaElement.Volume = _volume;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumePercent));
            }
        }

        public int VolumePercent => (int)(_volume * 100);

        private double _currentPosition;
        public double CurrentPosition
        {
            get => _currentPosition;
            set
            {
                _currentPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PositionText));
                OnPropertyChanged(nameof(ProgressPercent));
            }
        }

        private double _totalDuration;
        public double TotalDuration
        {
            get => _totalDuration;
            set
            {
                _totalDuration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalDurationText));
                OnPropertyChanged(nameof(ProgressPercent));
            }
        }

        public string PositionText => FormatTime(CurrentPosition);
        public string TotalDurationText => FormatTime(TotalDuration);
        public double ProgressPercent => TotalDuration > 0 ? (CurrentPosition / TotalDuration) * 100 : 0;
        public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";

        /// <summary>播放模式: 0=顺序, 1=单曲循环, 2=列表循环, 3=随机</summary>
        private int _playMode;
        public int PlayMode
        {
            get => _playMode;
            set { _playMode = value % 4; OnPropertyChanged(); OnPropertyChanged(nameof(PlayModeIcon)); }
        }

        public string PlayModeIcon => _playMode switch
        {
            1 => "🔂",
            2 => "🔁",
            3 => "🔀",
            _ => "➡"
        };

        // ── 事件 ──
        public event Action<MusicTrack?>? TrackChanged;
        public event Action<bool>? PlayStateChanged;

        private MusicPlayerService()
        {
            Volume = 0.7;

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _updateTimer.Tick += OnUpdateTimerTick;
        }

        /// <summary>绑定 MediaElement 控件</summary>
        public void BindPlayer(MediaElement mediaElement)
        {
            if (_mediaElement != null)
            {
                _mediaElement.MediaOpened -= OnMediaOpened;
                _mediaElement.MediaEnded -= OnMediaEnded;
                _mediaElement.MediaFailed -= OnMediaFailed;
            }

            _mediaElement = mediaElement;
            _mediaElement.Volume = _volume;
            _mediaElement.MediaOpened += OnMediaOpened;
            _mediaElement.MediaEnded += OnMediaEnded;
            _mediaElement.MediaFailed += OnMediaFailed;

            if (IsPlaying && CurrentTrack != null)
                PlayCurrent();
        }

        private void OnUpdateTimerTick(object? sender, EventArgs e)
        {
            if (_mediaElement != null && _mediaElement.NaturalDuration.HasTimeSpan)
            {
                TotalDuration = _mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                CurrentPosition = _mediaElement.Position.TotalSeconds;
            }
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_mediaElement == null) return;
                _mediaElement.Volume = _volume;
                _mediaElement.Play();
                IsPlaying = true;
                TotalDuration = _mediaElement.NaturalDuration.HasTimeSpan
                    ? _mediaElement.NaturalDuration.TimeSpan.TotalSeconds
                    : 0;
                _updateTimer.Start();
                PlayStateChanged?.Invoke(true);
            });
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _updateTimer.Stop();
                PlayStateChanged?.Invoke(false);
                Next();
            });
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logger.Error($"[MusicPlayer] 播放失败: {e.ErrorException?.Message}");
                _updateTimer.Stop();
                IsPlaying = false;
                PlayStateChanged?.Invoke(false);
                // TODO: 可尝试自动播放下一首
            });
        }

        // ── 播放控制 ──

        /// <summary>设置播放列表并播放指定索引</summary>
        public void SetPlaylist(ObservableCollection<MusicTrack> tracks, int startIndex = 0)
        {
            Playlist.Clear();
            foreach (var t in tracks)
                Playlist.Add(t);

            _currentIndex = Math.Clamp(startIndex, 0, Playlist.Count - 1);
            PlayCurrent();
        }

        /// <summary>添加到播放列表末尾</summary>
        public void AddToPlaylist(MusicTrack track)
        {
            Playlist.Add(track);
            if (_currentIndex < 0)
            {
                _currentIndex = 0;
                PlayCurrent();
            }
        }

        /// <summary>添加到播放列表并立即播放（自动去重）</summary>
        public void PlayNow(MusicTrack track)
        {
            int idx = Playlist.IndexOf(track);
            if (idx >= 0)
            {
                _currentIndex = idx;
            }
            else
            {
                idx = Playlist.Count;
                Playlist.Add(track);
                _currentIndex = idx;
            }
            PlayCurrent();
        }

        /// <summary>播放当前曲目</summary>
        public void PlayCurrent()
        {
            if (_mediaElement == null || _currentIndex < 0 || _currentIndex >= Playlist.Count)
                return;

            var track = Playlist[_currentIndex];
            CurrentTrack = track;

            string source = track.CachedFilePath ?? track.PlayUrl;
            if (string.IsNullOrEmpty(source)) return;

            _mediaElement.Stop();
            _mediaElement.Source = new Uri(source);
            _mediaElement.Play();
            IsPlaying = true;
            TrackChanged?.Invoke(track);
        }

        /// <summary>播放/暂停切换</summary>
        public void TogglePlayPause()
        {
            if (_mediaElement == null) return;

            if (IsPlaying)
            {
                _mediaElement.Pause();
                IsPlaying = false;
                _updateTimer.Stop();
            }
            else
            {
                if (_mediaElement.Source == null && CurrentTrack != null)
                {
                    PlayCurrent();
                }
                else
                {
                    _mediaElement.Play();
                    IsPlaying = true;
                    _updateTimer.Start();
                }
            }
            PlayStateChanged?.Invoke(IsPlaying);
        }

        /// <summary>暂停</summary>
        public void Pause()
        {
            if (_mediaElement != null && IsPlaying)
            {
                _mediaElement.Pause();
                IsPlaying = false;
                _updateTimer.Stop();
            }
        }

        /// <summary>继续</summary>
        public void Resume()
        {
            if (_mediaElement != null && !IsPlaying && CurrentTrack != null)
            {
                _mediaElement.Play();
                IsPlaying = true;
                _updateTimer.Start();
            }
        }

        /// <summary>下一首</summary>
        public void Next()
        {
            if (Playlist.Count == 0) return;

            int nextIndex = _playMode switch
            {
                1 => _currentIndex,                          // 单曲循环
                3 => new Random().Next(Playlist.Count),      // 随机
                _ => (_currentIndex + 1) % Playlist.Count     // 顺序 / 列表循环
            };

            // 顺序模式下到达末尾停止
            if (_playMode == 0 && nextIndex == 0 && _currentIndex == Playlist.Count - 1)
            {
                Stop();
                return;
            }

            _currentIndex = nextIndex;
            PlayCurrent();
        }

        /// <summary>上一首</summary>
        public void Previous()
        {
            if (Playlist.Count == 0) return;

            if (CurrentPosition > 3)
            {
                // 进度超过3秒则重播当前
                if (_mediaElement != null)
                {
                    _mediaElement.Position = TimeSpan.Zero;
                }
                return;
            }

            int prevIndex;
            if (_playMode == 3)
                prevIndex = new Random().Next(Playlist.Count);
            else
                prevIndex = _currentIndex <= 0 ? Playlist.Count - 1 : _currentIndex - 1;

            _currentIndex = prevIndex;
            PlayCurrent();
        }

        /// <summary>跳转到指定位置（秒）</summary>
        public void Seek(double seconds)
        {
            if (_mediaElement != null && _mediaElement.NaturalDuration.HasTimeSpan)
            {
                seconds = Math.Clamp(seconds, 0, TotalDuration);
                _mediaElement.Position = TimeSpan.FromSeconds(seconds);
                CurrentPosition = seconds;
            }
        }

        /// <summary>停止播放</summary>
        public void Stop()
        {
            if (_mediaElement != null)
            {
                _mediaElement.Stop();
                _mediaElement.Source = null;
            }
            IsPlaying = false;
            _updateTimer.Stop();
            CurrentPosition = 0;
            TotalDuration = 0;
        }

        /// <summary>播放列表中指定索引的曲目</summary>
        public void PlayIndex(int index)
        {
            if (index >= 0 && index < Playlist.Count)
            {
                _currentIndex = index;
                PlayCurrent();
            }
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) return "0:00";
            int min = (int)seconds / 60;
            int sec = (int)seconds % 60;
            return $"{min}:{sec:D2}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
