using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 本地皮肤 HTTP 服务器 —— 在游戏启动时提供皮肤纹理访问
    /// 兼容 CustomSkinLoader 和 authlib-injector 的皮肤 API
    /// </summary>
    public class SkinServer : IDisposable
    {
        private HttpListener _listener;
        private readonly string _skinsDir;
        private readonly int _port;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public string BaseUrl => $"http://127.0.0.1:{_port}";
        public int Port => _port;

        public SkinServer(string minecraftPath)
        {
            _skinsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");
            Directory.CreateDirectory(_skinsDir);
            _port = FindFreePort();
        }

        /// <summary>
        /// 启动皮肤服务器
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Prefixes.Add($"http://localhost:{_port}/");

            try
            {
                _listener.Start();
                _isRunning = true;
                Logger.Info($"[SkinServer] 皮肤服务器已启动: {BaseUrl}");

                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (HttpListenerException ex)
            {
                Logger.Warning($"[SkinServer] 启动失败 (可能需要管理员权限): {ex.Message}");
                // 尝试只监听 127.0.0.1
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                    _listener.Start();
                    _isRunning = true;
                    Logger.Info($"[SkinServer] 皮肤服务器已启动 (仅127.0.0.1): {BaseUrl}");
                    Task.Run(() => ListenLoop(_cts.Token));
                }
                catch (Exception ex2)
                {
                    Logger.Error($"[SkinServer] 启动失败: {ex2.Message}");
                    _listener?.Close();
                    _listener = null;
                }
            }
        }

        /// <summary>
        /// 停止皮肤服务器
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            Logger.Info("[SkinServer] 皮肤服务器已停止");
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener?.IsListening == true)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (ct.IsCancellationRequested) break;
                HandleRequestAsync(ctx, ct);
            }
        }

        private async void HandleRequestAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            try
            {
                string path = ctx.Request.Url.AbsolutePath.TrimStart('/');

                // 皮肤纹理 API: /MinecraftSkins/{username}.png 或 /skin/{uuid}.png
                if (path.StartsWith("MinecraftSkins/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("skin/", StringComparison.OrdinalIgnoreCase))
                {
                    string skinName = path.Substring(path.IndexOf('/') + 1);
                    skinName = skinName.Replace(".png", "");

                    byte[] skinData = LoadSkinData(skinName);
                    if (skinData != null)
                    {
                        ctx.Response.ContentType = "image/png";
                        ctx.Response.ContentLength64 = skinData.Length;
                        ctx.Response.Headers.Add("Cache-Control", "no-cache");
                        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                        await ctx.Response.OutputStream.WriteAsync(skinData, 0, skinData.Length, ct);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                    }
                }
                // 根路径：服务状态
                else if (path == "" || path == "/")
                {
                    byte[] response = Encoding.UTF8.GetBytes("SkinServer OK");
                    ctx.Response.ContentType = "text/plain";
                    ctx.Response.ContentLength64 = response.Length;
                    await ctx.Response.OutputStream.WriteAsync(response, 0, response.Length, ct);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            }
            catch { }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }

        /// <summary>
        /// 从 skins 目录加载皮肤纹理数据
        /// </summary>
        private byte[] LoadSkinData(string key)
        {
            // 先尝试以 UUID 文件名匹配
            string filePath = Path.Combine(_skinsDir, $"{key}.png");
            if (File.Exists(filePath))
                return File.ReadAllBytes(filePath);

            // 再尝试以用户名匹配
            filePath = Path.Combine(_skinsDir, $"{key}.png");
            if (File.Exists(filePath))
                return File.ReadAllBytes(filePath);

            // 生成离线 UUID 后尝试匹配
            string offlineUuid = GenerateOfflineUuid(key);
            filePath = Path.Combine(_skinsDir, $"{offlineUuid}.png");
            if (File.Exists(filePath))
                return File.ReadAllBytes(filePath);

            return null;
        }

        /// <summary>
        /// 生成离线玩家的 UUID（与 Minecraft 离线模式一致）
        /// </summary>
        public static string GenerateOfflineUuid(string username)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
                hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
                hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// 设置 CustomSkinLoader 配置文件以便在离线模式下加载本地皮肤
        /// </summary>
        public static void SetupCustomSkinLoaderConfig(string gameDir)
        {
            try
            {
                string cslDir = Path.Combine(gameDir, "CustomSkinLoader");
                Directory.CreateDirectory(cslDir);

                string skinsDir = Path.Combine(gameDir, "CustomSkinLoader", "LocalSkin", "skins");
                Directory.CreateDirectory(skinsDir);

                // 将 skins 目录下的皮肤同步到 CustomSkinLoader 目录
                string sourceSkinsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");
                if (Directory.Exists(sourceSkinsDir))
                {
                    foreach (string file in Directory.GetFiles(sourceSkinsDir, "*.png"))
                    {
                        string destFile = Path.Combine(skinsDir, Path.GetFileName(file));
                        try { File.Copy(file, destFile, true); } catch { }
                    }
                }

                // 写入 CustomSkinLoader.json 配置
                string configPath = Path.Combine(cslDir, "CustomSkinLoader.json");
                string config = @"{
  ""version"": ""14.18"",
  ""enable"": true,
  ""loadlist"": [
    {
      ""name"": ""Local"",
      ""type"": ""LocalSkin"",
      ""folder"": ""/""
    },
    {
      ""name"": ""Mojang"",
      ""type"": ""MojangAPI""
    }
  ]
}";
                File.WriteAllText(configPath, config, Encoding.UTF8);
                Logger.Info("[SkinServer] CustomSkinLoader 配置已设置");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[SkinServer] 设置 CustomSkinLoader 配置失败: {ex.Message}");
            }
        }

        private static int FindFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            _listener?.Close();
        }
    }
}
