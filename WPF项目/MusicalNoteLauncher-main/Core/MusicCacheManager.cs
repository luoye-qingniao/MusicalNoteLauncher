using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using MusicalNoteLauncher.Models;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 音乐本地缓存管理器 —— 将音乐文件下载到 MNL/music_cache/ 目录
    /// </summary>
    public class MusicCacheManager
    {
        private readonly string _cacheDir;
        private readonly HttpClient _http;
        private readonly object _downloadLock = new();

        public MusicCacheManager()
        {
            _cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MNL", "music_cache");
            if (!Directory.Exists(_cacheDir))
                Directory.CreateDirectory(_cacheDir);

            _http = SafeHttpClientFactory.CreateClient(60);
            _http.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <summary>缓存目录路径</summary>
        public string CacheDirectory => _cacheDir;

        /// <summary>获取歌曲缓存文件路径</summary>
        public string GetCachePath(string songId)
        {
            return Path.Combine(_cacheDir, $"{songId}.mp3");
        }

        /// <summary>检查歌曲是否已缓存</summary>
        public bool IsCached(string songId)
        {
            string path = GetCachePath(songId);
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        /// <summary>下载并缓存歌曲</summary>
        public async Task<string?> DownloadAndCacheAsync(MusicTrack track)
        {
            string cachePath = GetCachePath(track.Id);

            // 已缓存则直接返回
            if (IsCached(track.Id))
            {
                track.CachedFilePath = cachePath;
                return cachePath;
            }

            // 获取播放 URL
            if (string.IsNullOrEmpty(track.PlayUrl))
            {
                var api = new MusicApiService();
                track.PlayUrl = await api.GetSongUrlAsync(track.Id) ?? "";
            }

            if (string.IsNullOrEmpty(track.PlayUrl))
                return null;

            lock (_downloadLock)
            {
                if (IsCached(track.Id))
                {
                    track.CachedFilePath = cachePath;
                    return cachePath;
                }
            }

            try
            {
                byte[] data = await _http.GetByteArrayAsync(track.PlayUrl);
                string tmpPath = cachePath + ".tmp";
                await File.WriteAllBytesAsync(tmpPath, data);
                if (File.Exists(cachePath))
                    File.Delete(cachePath);
                File.Move(tmpPath, cachePath);
                track.CachedFilePath = cachePath;
                Logger.Info($"[MusicCache] 已缓存: {track.Name} ({track.Id})");
                return cachePath;
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicCache] 下载缓存失败 {track.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>删除歌曲缓存</summary>
        public void DeleteCache(string songId)
        {
            string path = GetCachePath(songId);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                Logger.Info($"[MusicCache] 已删除缓存: {songId}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicCache] 删除缓存失败: {ex.Message}");
            }
        }

        /// <summary>获取缓存占用大小（字节）</summary>
        public long GetCacheSizeBytes()
        {
            if (!Directory.Exists(_cacheDir)) return 0;
            long total = 0;
            foreach (string file in Directory.GetFiles(_cacheDir, "*.mp3"))
            {
                try { total += new FileInfo(file).Length; }
                catch { }
            }
            return total;
        }

        /// <summary>清空全部缓存</summary>
        public void ClearAllCache()
        {
            try
            {
                if (Directory.Exists(_cacheDir))
                {
                    foreach (string file in Directory.GetFiles(_cacheDir))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
                Logger.Info("[MusicCache] 已清空全部缓存");
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicCache] 清空缓存失败: {ex.Message}");
            }
        }
    }
}
