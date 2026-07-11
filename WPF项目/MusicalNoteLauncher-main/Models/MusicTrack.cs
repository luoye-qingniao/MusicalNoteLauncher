using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MusicalNoteLauncher.Models
{
    /// <summary>
    /// 音乐曲目数据模型
    /// </summary>
    public class MusicTrack : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string AlbumId { get; set; } = string.Empty;
        public string? AlbumCoverUrl { get; set; }
        public int DurationMs { get; set; }
        public string PlayUrl { get; set; } = string.Empty;
        public string MvId { get; set; } = string.Empty;

        /// <summary>本地缓存的 MP3 文件路径（若已缓存则非空）</summary>
        public string? CachedFilePath { get; set; }

        /// <summary>格式化后的时长 (mm:ss)</summary>
        [JsonIgnore]
        public string DurationText
        {
            get
            {
                int totalSec = DurationMs / 1000;
                int min = totalSec / 60;
                int sec = totalSec % 60;
                return $"{min}:{sec:D2}";
            }
        }

        /// <summary>艺术家 - 专辑 短描述</summary>
        [JsonIgnore]
        public string Subtitle => string.IsNullOrEmpty(Album)
            ? Artist
            : $"{Artist} · {Album}";

        /// <summary>是否已缓存在本地</summary>
        [JsonIgnore]
        public bool IsCached => !string.IsNullOrEmpty(CachedFilePath) && System.IO.File.Exists(CachedFilePath);

        /// <summary>按歌曲 ID 判断相等（支持播放列表去重）</summary>
        public override bool Equals(object? obj) => obj is MusicTrack other && other.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    /// <summary>
    /// 歌单信息
    /// </summary>
    public class PlaylistInfo : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public int TrackCount { get; set; }
        public string Description { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    /// <summary>
    /// 音乐平台账号信息
    /// </summary>
    public class MusicAccountInfo : INotifyPropertyChanged
    {
        public string UserId { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Cookie { get; set; } = string.Empty;
        public long LoginTime { get; set; }

        /// <summary>是否已登录</summary>
        [JsonIgnore]
        public bool IsLoggedIn => !string.IsNullOrEmpty(Token) || !string.IsNullOrEmpty(Cookie);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    /// <summary>
    /// 搜索结果
    /// </summary>
    public class MusicSearchResult
    {
        public System.Collections.Generic.List<MusicTrack> Songs { get; set; } = new();
        public int TotalCount { get; set; }
        public bool HasMore { get; set; }
    }
}
