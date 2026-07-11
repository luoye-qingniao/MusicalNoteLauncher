using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MusicalNoteLauncher.Models;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 网易云音乐 API —— 全 HTTPS，旧版 API 优先。
    /// weapi 仅作后备（已知可能被服务端返回空 body 拒绝）。
    /// </summary>
    public class MusicApiService
    {
        private const string ApiBase = "https://music.163.com";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        private const string FirstKey = "0CoJUm6Qyw8W8jud";
        private const string IvParam = "0102030405060708";

        private const string RsaPublicKey =
            "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDgtQn2JZ34ZC28NWYpAUd98iZ37BUrX/aKzmF" +
            "bt7clFSs6sXqHauqKVqdt4kGdEp+pH3cvfXGXQJ+J0gC5kGS2StxS0Fp6Wqzj8SD0dILxTDR4l" +
            "4wBjRwEBjKs2R4myACDk1IpYFiOiJmXxT1+2HFEBwcLMwZ6dhbJjFvK3jjUwQIDAQAB";

        private readonly HttpClient _http;
        private readonly CookieContainer _cookieContainer;
        private bool _initialized;
        private bool _reachable;

        public MusicApiService(string? cookie = null)
        {
            _cookieContainer = new CookieContainer();

            string nuid = GenerateRandomHex(16);
            var uri = new Uri(ApiBase);
            _cookieContainer.Add(uri, new Cookie("_ntes_nuid", nuid));
            _cookieContainer.Add(uri, new Cookie("NMTID", $"00O{GenerateRandomHex(20)}"));

            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = _cookieContainer,
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            };

            _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            _http.DefaultRequestHeaders.Referrer = uri;
            _http.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");

            if (!string.IsNullOrEmpty(cookie))
                _cookieContainer.SetCookies(uri, cookie);
        }

        public bool IsReachable => _reachable;

        private async Task EnsureInitializedAsync()
        {
            if (_initialized) return;
            try
            {
                string enc = Uri.EscapeDataString("test");
                var url = $"{ApiBase}/api/search/get/web?csrf_token=hlpretag=&hlposttag=&s={enc}&type=1&limit=1";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Referrer = new Uri(ApiBase);
                var resp = await _http.SendAsync(req);
                string body = await resp.Content.ReadAsStringAsync();

                if (resp.IsSuccessStatusCode && body.Length > 50)
                {
                    _reachable = true;
                    Logger.Info($"[MusicApi] 连通OK (HTTP {(int)resp.StatusCode}, len={body.Length})");
                }
                else
                {
                    Logger.Error($"[MusicApi] 连通异常 HTTP {(int)resp.StatusCode} body={Truncate(body, 200)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicApi] 网络异常: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Logger.Error($"[MusicApi] Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            _initialized = true;
        }

        // ════════════ 旧版 API（HTTPS GET，无需加密，最可靠） ════════════

        private async Task<JsonDocument?> LegacyGetAsync(string pathAndQuery)
        {
            await EnsureInitializedAsync();
            try
            {
                var url = $"{ApiBase}{pathAndQuery}";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Referrer = new Uri(ApiBase);

                var resp = await _http.SendAsync(req);
                string respBody = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode || string.IsNullOrWhiteSpace(respBody))
                {
                    Logger.Error($"[MusicApi] Legacy HTTP {(int)resp.StatusCode} len={respBody.Length}");
                    return null;
                }
                return JsonDocument.Parse(respBody);
            }
            catch (JsonException ex)
            {
                Logger.Error($"[MusicApi] JSON解析失败: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicApi] Legacy 异常: {ex.Message}");
                return null;
            }
        }

        // ════════════ weapi POST（已被服务端拒绝，仅保留代码备用） ════════════

        private async Task<JsonDocument?> WeapiPostAsync(string path, Dictionary<string, object> data)
        {
            await EnsureInitializedAsync();
            try
            {
                var (encParams, encSecKey) = WeapiEncrypt(data);
                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("params", encParams),
                    new KeyValuePair<string, string>("encSecKey", encSecKey)
                });

                var url = $"{ApiBase}{path}";
                var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
                req.Headers.Referrer = new Uri(ApiBase);
                var resp = await _http.SendAsync(req);
                string respBody = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode || string.IsNullOrWhiteSpace(respBody))
                    return null;

                return JsonDocument.Parse(respBody);
            }
            catch
            {
                return null;
            }
        }

        // ════════════ 搜索 ════════════

        /// <summary>搜索 API 不返回 picUrl，通过 /api/album 批量获取封面</summary>
        private async Task FillCoverUrlsAsync(List<MusicTrack> tracks)
        {
            // 按 albumId 去重，避免重复请求同一个专辑
            var albumIds = tracks
                .Where(t => string.IsNullOrEmpty(t.AlbumCoverUrl) && !string.IsNullOrEmpty(t.AlbumId))
                .Select(t => t.AlbumId).Distinct().Take(15).ToList();

            if (albumIds.Count == 0) return;

            Logger.Info($"[MusicApi] 封面补全: {albumIds.Count} 个专辑...");
            var coverMap = new Dictionary<string, string>();

            foreach (var albumId in albumIds)
            {
                try
                {
                    var doc = await LegacyGetAsync($"/api/album/{albumId}");
                    if (doc == null) continue;

                    var root = doc.RootElement;
                    if (!root.TryGetProperty("album", out var album)) continue;
                    var picUrl = album.TryGetProperty("picUrl", out var pu) ? pu.GetString() : null;
                    if (!string.IsNullOrEmpty(picUrl))
                        coverMap[albumId] = picUrl;
                }
                catch { }
            }

            int matched = 0;
            foreach (var t in tracks)
                if (coverMap.TryGetValue(t.AlbumId, out var cover))
                { t.AlbumCoverUrl = cover; matched++; }

            Logger.Info($"[MusicApi] 封面补全: {matched}/{tracks.Count} 首");
        }

        public async Task<MusicSearchResult> SearchSongsAsync(string keyword, int limit = 30, int offset = 0)
        {
            try
            {
                string enc = Uri.EscapeDataString(keyword);
                string path = $"/api/search/get/web?csrf_token=hlpretag=&hlposttag=&s={enc}&type=1&offset={offset}&total=true&limit={limit}";

                var doc = await LegacyGetAsync(path);
                if (doc == null) return new MusicSearchResult();

                var root = doc.RootElement;
                if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                {
                    Logger.Error($"[MusicApi] 搜索 code={code.GetInt32()}");
                    return new MusicSearchResult();
                }

                var result = root.GetProperty("result");
                var songs = result.TryGetProperty("songs", out var s) ? s : default;
                int total = result.TryGetProperty("songCount", out var sc) ? sc.GetInt32() : 0;

                var list = new List<MusicTrack>();
                if (songs.ValueKind == JsonValueKind.Array)
                    foreach (var song in songs.EnumerateArray())
                        list.Add(ParseTrack(song));

                // 搜索 API 不返回 picUrl，用 song/detail 批量补全封面
                if (list.Count > 0)
                    await FillCoverUrlsAsync(list);

                Logger.Info($"[MusicApi] 搜索: '{keyword}' -> {list.Count}/{total} 首");
                return new MusicSearchResult { Songs = list, TotalCount = total, HasMore = list.Count >= limit };
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicApi] 搜索异常: {ex.Message}");
                return new MusicSearchResult();
            }
        }

        // ════════════ 播放地址（weapi — 可能不可用） ════════════

        public async Task<string?> GetSongUrlAsync(string songId, string level = "standard")
        {
            // weapi 已知不可用，直接使用免费直连（无需等待超时）
            return $"https://music.163.com/song/media/outer/url?id={songId}.mp3";
        }

        // ════════════ 歌单 ════════════

        /// <summary>从网易云实时获取热门歌单。并行搜索多个关键词，去重后返回。</summary>
        public async Task<List<PlaylistInfo>> GetRecommendPlaylistsAsync(int limit = 20)
        {
            var list = new List<PlaylistInfo>();
            var seenIds = new HashSet<string>();

            // 并行搜索多个热门关键词（type=1000 = 歌单）
            var tasks = new[]
            {
                SearchPlaylistsAsync("热歌榜", 8),
                SearchPlaylistsAsync("抖音", 8),
                SearchPlaylistsAsync("欧美金曲", 6),
                SearchPlaylistsAsync("华语经典", 6),
                SearchPlaylistsAsync("经典老歌", 6),
                SearchPlaylistsAsync("日语流行", 6),
            };

            var results = await Task.WhenAll(tasks);

            foreach (var sub in results)
            {
                foreach (var pl in sub)
                {
                    if (list.Count >= limit) break;
                    if (seenIds.Contains(pl.Id)) continue;
                    seenIds.Add(pl.Id);
                    list.Add(pl);
                }
                if (list.Count >= limit) break;
            }

            Logger.Info($"[MusicApi] 推荐歌单（在线获取）: {list.Count} 个");
            return list;
        }

        private async Task<List<PlaylistInfo>> SearchPlaylistsAsync(string keyword, int limit)
        {
            var list = new List<PlaylistInfo>();
            try
            {
                string enc = Uri.EscapeDataString(keyword);
                var doc = await LegacyGetAsync($"/api/search/get/web?csrf_token=hlpretag=&hlposttag=&s={enc}&type=1000&limit={limit}");
                if (doc == null) return list;

                var root = doc.RootElement;
                if (!root.TryGetProperty("result", out var result)) return list;
                if (!result.TryGetProperty("playlists", out var playlists)) return list;
                if (playlists.ValueKind != JsonValueKind.Array) return list;

                foreach (var pl in playlists.EnumerateArray())
                {
                    list.Add(new PlaylistInfo
                    {
                        Id = pl.TryGetProperty("id", out var idEl) ? idEl.GetInt64().ToString() : "",
                        Name = pl.TryGetProperty("name", out var n) ? n.GetString() ?? keyword : keyword,
                        CoverUrl = pl.TryGetProperty("coverImgUrl", out var c) ? c.GetString() : null,
                        TrackCount = pl.TryGetProperty("trackCount", out var tc) ? tc.GetInt32() : 0,
                        Description = pl.TryGetProperty("description", out var d) ? d.GetString() ?? "" : ""
                    });
                }
            }
            catch { }
            return list;
        }

        public async Task<List<MusicTrack>> GetPlaylistTracksAsync(string playlistId, int limit = 50)
        {
            var list = new List<MusicTrack>();
            try
            {
                var doc = await LegacyGetAsync($"/api/playlist/detail?id={playlistId}");
                if (doc == null) return list;

                var root = doc.RootElement;

                // 旧版 API 返回 result，新版 API 返回 playlist
                JsonElement tracks = default;
                if (root.TryGetProperty("result", out var result))
                    result.TryGetProperty("tracks", out tracks);
                else if (root.TryGetProperty("playlist", out var playlist))
                    playlist.TryGetProperty("tracks", out tracks);

                if (tracks.ValueKind != JsonValueKind.Array) return list;

                foreach (var song in tracks.EnumerateArray())
                    list.Add(ParseTrack(song));

                Logger.Info($"[MusicApi] 歌单歌曲: {list.Count} 首");
            }
            catch (Exception ex) { Logger.Error($"[MusicApi] 歌单异常: {ex.Message}"); }
            return list;
        }

        // ════════════ 账号（weapi，可能不可用） ════════════

        public async Task<MusicAccountInfo?> GetLoginStatusAsync(string? cookie = null)
        {
            try
            {
                var api = string.IsNullOrEmpty(cookie) ? this : new MusicApiService(cookie);
                var doc = await api.WeapiPostAsync("/weapi/w/nuser/account/get", new());
                if (doc == null) return null;
                var root = doc.RootElement;
                if (!root.TryGetProperty("profile", out var profile)) return null;
                return new MusicAccountInfo
                {
                    UserId = profile.TryGetProperty("userId", out var uid) ? uid.GetInt64().ToString() : "",
                    Nickname = profile.TryGetProperty("nickname", out var nick) ? nick.GetString() ?? "" : "",
                    AvatarUrl = profile.TryGetProperty("avatarUrl", out var av) ? av.GetString() ?? "" : "",
                    Cookie = cookie ?? ""
                };
            }
            catch { return null; }
        }

        public async Task<string?> GetQrKeyAsync()
        {
            var doc = await WeapiPostAsync("/weapi/login/qrcode/unikey", new() { ["type"] = 1 });
            return doc?.RootElement.TryGetProperty("unikey", out var uk) == true ? uk.GetString() : null;
        }

        public async Task<string?> CreateQrImageAsync(string key)
        {
            var doc = await WeapiPostAsync("/weapi/login/qrcode/create", new() { ["key"] = key, ["qrimg"] = true });
            if (doc == null) return null;
            var root = doc.RootElement;
            return root.TryGetProperty("data", out var d) && d.TryGetProperty("qrimg", out var qr) ? qr.GetString() : null;
        }

        public async Task<(int Code, string? Cookie)> CheckQrStatusAsync(string key)
        {
            var doc = await WeapiPostAsync("/weapi/login/qrcode/client/login", new() { ["key"] = key, ["type"] = 1 });
            if (doc == null) return (-1, null);
            var root = doc.RootElement;
            int code = root.TryGetProperty("code", out var c) ? c.GetInt32() : -1;
            if (code == 803) return (803, null);
            string? cookie = root.TryGetProperty("cookie", out var ck) ? ck.GetString() : null;
            return (code, cookie);
        }

        public async Task<List<MusicTrack>> GetDailyRecommendAsync(string cookie)
        {
            var list = new List<MusicTrack>();
            try
            {
                var api = new MusicApiService(cookie);
                var doc = await api.WeapiPostAsync("/weapi/v1/discovery/recommend/songs", new());
                if (doc == null) return list;
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("dailySongs", out var songs))
                    foreach (var s in songs.EnumerateArray())
                        list.Add(ParseTrack(s));
            }
            catch { }
            return list;
        }

        // ════════════ 加密 ════════════

        private static (string, string) WeapiEncrypt(Dictionary<string, object> data)
        {
            string json = JsonSerializer.Serialize(data);
            string secKey = GenerateRandomHex(16);
            return (AesEncrypt(AesEncrypt(json, FirstKey), secKey), RsaEncrypt(secKey));
        }

        private static string GenerateRandomHex(int len)
        {
            byte[] b = RandomNumberGenerator.GetBytes(len);
            var sb = new StringBuilder(len * 2);
            foreach (byte x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        private static string AesEncrypt(string text, string key)
        {
            using var aes = Aes.Create();
            aes.KeySize = aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = Encoding.UTF8.GetBytes(IvParam);
            using var enc = aes.CreateEncryptor();
            byte[] r = enc.TransformFinalBlock(Encoding.UTF8.GetBytes(text), 0, Encoding.UTF8.GetByteCount(text));
            return Convert.ToBase64String(r);
        }

        private static string RsaEncrypt(string text)
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(RsaPublicKey), out _);
            byte[] r = rsa.Encrypt(Encoding.UTF8.GetBytes(text), RSAEncryptionPadding.Pkcs1);
            var sb = new StringBuilder(r.Length * 2);
            foreach (byte b in r) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static MusicTrack ParseTrack(JsonElement song)
        {
            string id = song.TryGetProperty("id", out var sid) ? sid.GetInt64().ToString() : "";
            string name = song.TryGetProperty("name", out var sn) ? sn.GetString() ?? "" : "";

            string artist = "";
            JsonElement ar = default;
            if (!song.TryGetProperty("ar", out ar))
                song.TryGetProperty("artists", out ar);
            if (ar.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                foreach (var a in ar.EnumerateArray())
                {
                    if (sb.Length > 0) sb.Append(" / ");
                    sb.Append(a.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "");
                }
                artist = sb.ToString();
            }

            string album = "";
            string albumId = "";
            string? cover = null;
            JsonElement al = default;
            if (!song.TryGetProperty("al", out al))
                song.TryGetProperty("album", out al);
            if (al.ValueKind == JsonValueKind.Object)
            {
                album = al.TryGetProperty("name", out var aln) ? aln.GetString() ?? "" : "";
                albumId = al.TryGetProperty("id", out var alid) ? alid.GetInt64().ToString() : "";
                cover = al.TryGetProperty("picUrl", out var pic) ? pic.GetString() : null;
            }

            int duration = 0;
            if (song.TryGetProperty("dt", out var dt))
                duration = dt.GetInt32();
            else if (song.TryGetProperty("duration", out var dur))
            {
                duration = dur.GetInt32();
                if (duration > 0 && duration < 10000) duration *= 1000;
            }

            string mvId = "";
            if (song.TryGetProperty("mv", out var mv) && mv.GetInt64() > 0)
                mvId = mv.GetInt64().ToString();

            return new MusicTrack
            {
                Id = id, Name = name, Artist = artist, Album = album, AlbumId = albumId,
                AlbumCoverUrl = cover, DurationMs = duration, MvId = mvId
            };
        }

        private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "...";
    }
}
