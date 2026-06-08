using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class UrlValidationCache
    {
        private static UrlValidationCache _instance;
        public static UrlValidationCache Instance => _instance ??= new UrlValidationCache();

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(5, 5);
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

        private class CacheEntry
        {
            public bool IsValid { get; set; }
            public string ValidUrl { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        private UrlValidationCache()
        {
        }

        public async Task<(bool IsValid, string ValidUrl)> ValidateUrlAsync(string originalUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(originalUrl))
                return (false, null);

            string cacheKey = originalUrl.GetHashCode().ToString();

            if (_cache.TryGetValue(cacheKey, out var cachedEntry) && cachedEntry.ExpiresAt > DateTime.Now)
            {
                Logger.Info($"[URL缓存] 命中缓存: {originalUrl} -> {(cachedEntry.IsValid ? "有效" : "无效")}");
                return (cachedEntry.IsValid, cachedEntry.ValidUrl);
            }

            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedEntry) && cachedEntry.ExpiresAt > DateTime.Now)
                {
                    Logger.Info($"[URL缓存] 命中缓存(并发后): {originalUrl} -> {(cachedEntry.IsValid ? "有效" : "无效")}");
                    return (cachedEntry.IsValid, cachedEntry.ValidUrl);
                }

                var result = await ValidateUrlDirectlyAsync(originalUrl, cancellationToken);

                _cache.AddOrUpdate(cacheKey,
                    new CacheEntry
                    {
                        IsValid = result.IsValid,
                        ValidUrl = result.ValidUrl,
                        ExpiresAt = DateTime.Now.Add(_cacheDuration)
                    },
                    (key, existing) => new CacheEntry
                    {
                        IsValid = result.IsValid,
                        ValidUrl = result.ValidUrl,
                        ExpiresAt = DateTime.Now.Add(_cacheDuration)
                    });

                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<(bool IsValid, string ValidUrl)> ValidateUrlDirectlyAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                string[] urlsToTry = GetAllAvailableUrls(url);

                foreach (string urlToCheck in urlsToTry)
                {
                    try
                    {
                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            cts.CancelAfter(TimeSpan.FromSeconds(10));

                            using var client = new WebClient();
                            client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                            client.Encoding = System.Text.Encoding.UTF8;

                            var uri = new Uri(urlToCheck);
                            var request = WebRequest.Create(uri);
                            request.Timeout = 10000;

                            using var response = (HttpWebResponse)await Task.Factory.FromAsync(
                                request.BeginGetResponse,
                                request.EndGetResponse,
                                null);

                            if (response.StatusCode == HttpStatusCode.OK ||
                                response.StatusCode == HttpStatusCode.Found ||
                                response.StatusCode == HttpStatusCode.MovedPermanently)
                            {
                                return (true, urlToCheck);
                            }

                            if (response.StatusCode == HttpStatusCode.NotFound)
                            {
                                continue;
                            }
                        }
                    }
                    catch (WebException ex) when (ex.Status == WebExceptionStatus.Timeout ||
                                                   ex.Status == WebExceptionStatus.ConnectFailure ||
                                                   ex.Status == WebExceptionStatus.RequestCanceled)
                    {
                        continue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                Logger.Error($"[URL校验] 校验失败: {url}, 错误: {ex.Message}");
                return (false, null);
            }
        }

        private string[] GetAllAvailableUrls(string originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl))
                return Array.Empty<string>();

            var urls = new System.Collections.Generic.List<string> { originalUrl };

            if (originalUrl.StartsWith("https://piston-meta.mojang.com"))
            {
                urls.Add(originalUrl.Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com"));
                urls.Add(originalUrl.Replace("https://piston-meta.mojang.com", "https://mcpdetector.net/minecraft"));
            }
            else if (originalUrl.StartsWith("https://launchermeta.mojang.com"))
            {
                urls.Add(originalUrl.Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com"));
                urls.Add(originalUrl.Replace("https://launchermeta.mojang.com", "https://mcpdetector.net/minecraft"));
            }
            else if (originalUrl.StartsWith("https://resources.download.minecraft.net"))
            {
                urls.Add(originalUrl.Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/resources"));
                urls.Add(originalUrl.Replace("https://resources.download.minecraft.net", "https://mcpdetector.net/minecraft/resources"));
            }

            return urls.ToArray();
        }

        public void ClearCache()
        {
            _cache.Clear();
            Logger.Info("[URL缓存] 缓存已清除");
        }
    }
}