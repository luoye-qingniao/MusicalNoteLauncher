using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MusicalNoteLauncher.Core;
using Photino.NET;

namespace MusicalNoteLauncher
{
    class Program
    {
        private static WebBridge? _bridge;
        private static HttpListener? _httpServer;
        private static int _httpPort;
        private static LauncherCore? _launcherCore;
        private static string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mnl_webview.log");
        private static object _logLock = new();

        static void Log(string msg)
        {
            try { lock (_logLock) File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}\n"); }
            catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Log("========================================");
            Log($"MNL 跨平台版启动: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log($"操作系统: {Environment.OSVersion}");
            Log($"Runtime: {Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            Log($"工作目录: {AppDomain.CurrentDomain.BaseDirectory}");

            try
            {
                // 初始化核心服务
                var config = new ConfigManager();
                var mcPath = GetEffectiveMcPath(config);
                _launcherCore = new LauncherCore(config);

                // 初始化基岩版服务
                var bedrockService = new BedrockEnhancedDownloadService(mcPath);
                var bedrockLauncher = new BedrockOfflineLauncher(mcPath);

                // 创建 Photino 窗口
                var window = new PhotinoWindow()
                    .SetTitle("MNL 音符启动器")
                    .SetUseOsDefaultLocation(true)
                    .SetWidth(1280)
                    .SetHeight(800)
                    .SetMinWidth(900)
                    .SetMinHeight(600)
                    .SetResizable(true);

                // 启动本地 HTTP 服务器，提供 WebApp 文件
                string webAppDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebApp");
                _httpPort = FindAvailablePort();
                StartHttpServer(webAppDir);
                Log($"[HTTP] 服务器启动于 http://localhost:{_httpPort}/");

                // WebBridge 初始化
                _bridge = new WebBridge(window, _launcherCore, bedrockService, bedrockLauncher, mcPath);

                // 注册 C# ↔ JS 通信
                window.RegisterWebMessageReceivedHandler((sender, message) =>
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message);
                        if (parsed != null && parsed.TryGetValue("method", out var methodEl))
                        {
                            string method = methodEl.GetString() ?? "";
                            object? result = _bridge.HandleInvoke(method, parsed);
                            if (result != null)
                            {
                                var json = JsonSerializer.Serialize(result);
                                window.SendWebMessage(json);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[Bridge] 消息处理异常: {ex.Message}");
                    }
                });

                // 启动完成后加载首页
                window.Load($"http://localhost:{_httpPort}/index.html");

                Log("[Photino] 窗口已创建，等待用户操作...");
                window.WaitForClose();

                // 清理
                _httpServer?.Stop();
                _httpServer?.Close();
                _bridge?.Dispose();
                Log("[Photino] 应用已退出");
            }
            catch (Exception ex)
            {
                Log($"[FATAL] 启动失败: {ex}");
                Console.Error.WriteLine($"启动失败: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static string GetEffectiveMcPath(ConfigManager config)
        {
            string settingsPath = SettingsManager.Settings.GamePath;
            if (!string.IsNullOrWhiteSpace(settingsPath))
                return Environment.ExpandEnvironmentVariables(settingsPath);
            return config.GetMinecraftPath();
        }

        static int FindAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        static void StartHttpServer(string rootFolder)
        {
            rootFolder = Path.GetFullPath(rootFolder);
            _httpServer = new HttpListener();
            _httpServer.Prefixes.Add($"http://localhost:{_httpPort}/");
            _httpServer.Start();
            _ = Task.Run(() => ServeHttpRequests(rootFolder));
        }

        static async Task ServeHttpRequests(string rootFolder)
        {
            while (_httpServer?.IsListening == true)
            {
                try
                {
                    var ctx = await _httpServer.GetContextAsync();
                    var rawPath = ctx.Request.Url!.AbsolutePath;
                    var filePath = rawPath.TrimStart('/').Split('?')[0];
                    if (string.IsNullOrEmpty(filePath) || filePath.EndsWith("/"))
                        filePath += "index.html";

                    // API: 皮肤文件
                    if (filePath.StartsWith("api/skin/"))
                    {
                        await ServeStaticFile(ctx, rootFolder, filePath, "skins", "image/png");
                        continue;
                    }

                    // API: 背景图片/视频
                    if (filePath.StartsWith("api/bg/"))
                    {
                        var bgDir = Path.Combine(PlatformHelper.AppDataDir, "MNL", "backgrounds");
                        await ServeBackgroundFile(ctx, bgDir, filePath);
                        continue;
                    }

                    // API: 音频代理（MusicCacheManager 需要）
                    if (filePath.StartsWith("api/audio"))
                    {
                        HandleAudioProxy(ctx);
                        continue;
                    }

                    // 静态文件
                    var fullPath = Path.Combine(rootFolder, filePath);
                    // 安全检查：防止路径穿越
                    if (!Path.GetFullPath(fullPath).StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = 403;
                        ctx.Response.Close();
                        continue;
                    }

                    if (File.Exists(fullPath))
                    {
                        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                        var contentType = ext switch
                        {
                            ".html" => "text/html; charset=utf-8",
                            ".css" => "text/css; charset=utf-8",
                            ".js" => "application/javascript; charset=utf-8",
                            ".json" => "application/json; charset=utf-8",
                            ".png" => "image/png",
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".svg" => "image/svg+xml",
                            ".ico" => "image/x-icon",
                            ".woff2" => "font/woff2",
                            ".woff" => "font/woff",
                            _ => "application/octet-stream"
                        };
                        ctx.Response.ContentType = contentType;
                        ctx.Response.Headers.Add("Cache-Control", ext switch
                        {
                            ".html" => "no-cache",
                            ".js" or ".css" => "public, max-age=3600",
                            _ => "public, max-age=86400"
                        });
                        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

                        using var fs = File.OpenRead(fullPath);
                        ctx.Response.ContentLength64 = fs.Length;
                        await fs.CopyToAsync(ctx.Response.OutputStream);
                    }
                    else
                    {
                        // 404 → 返回 index.html（SPA fallback）
                        var indexPath = Path.Combine(rootFolder, "index.html");
                        if (File.Exists(indexPath) && !filePath.Contains("."))
                        {
                            ctx.Response.ContentType = "text/html; charset=utf-8";
                            ctx.Response.Headers.Add("Cache-Control", "no-cache");
                            using var fs = File.OpenRead(indexPath);
                            ctx.Response.ContentLength64 = fs.Length;
                            await fs.CopyToAsync(ctx.Response.OutputStream);
                        }
                        else
                        {
                            ctx.Response.StatusCode = 404;
                        }
                    }
                    ctx.Response.Close();
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    try { Log($"[HTTP] 错误: {ex.Message}"); } catch { }
                }
            }
        }

        static async Task ServeStaticFile(HttpListenerContext ctx, string rootFolder, string requestPath, string subDir, string contentType)
        {
            try
            {
                var fileName = requestPath.Substring(("api/" + subDir + "/").Length).Split('/')[0];
                if (string.IsNullOrEmpty(fileName) || fileName.Contains(".."))
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.Close();
                    return;
                }
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, subDir);
                var file = Path.Combine(dir, fileName + ".png");
                if (File.Exists(file))
                {
                    ctx.Response.ContentType = contentType;
                    ctx.Response.AddHeader("Cache-Control", "no-cache");
                    ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
                    using var fs = File.OpenRead(file);
                    ctx.Response.ContentLength64 = fs.Length;
                    await fs.CopyToAsync(ctx.Response.OutputStream);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            }
            catch { ctx.Response.StatusCode = 500; }
            ctx.Response.Close();
        }

        static async Task ServeBackgroundFile(HttpListenerContext ctx, string bgDir, string requestPath)
        {
            try
            {
                var bgFileName = requestPath.Substring("api/bg/".Length).Split('/')[0];
                if (string.IsNullOrEmpty(bgFileName) || bgFileName.Contains(".."))
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.Close();
                    return;
                }
                var bgFile = Path.Combine(bgDir, bgFileName);
                if (File.Exists(bgFile))
                {
                    var ext = Path.GetExtension(bgFile).ToLowerInvariant();
                    ctx.Response.ContentType = ext switch
                    {
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".png" => "image/png",
                        ".webp" => "image/webp",
                        ".mp4" => "video/mp4",
                        _ => "application/octet-stream"
                    };
                    ctx.Response.AddHeader("Cache-Control", "no-cache");
                    ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
                    ctx.Response.AddHeader("Accept-Ranges", "bytes");

                    // Range 请求支持
                    var rangeHeader = ctx.Request.Headers["Range"];
                    if (!string.IsNullOrEmpty(rangeHeader) && ext == ".mp4")
                    {
                        using var fs = File.OpenRead(bgFile);
                        var totalSize = fs.Length;
                        var rangeParts = rangeHeader.Replace("bytes=", "").Split('-');
                        long start = long.TryParse(rangeParts[0], out var s) ? s : 0;
                        long end = (rangeParts.Length > 1 && long.TryParse(rangeParts[1], out var e)) ? e : totalSize - 1;
                        end = Math.Min(end, totalSize - 1);
                        var contentLength = end - start + 1;

                        ctx.Response.StatusCode = 206;
                        ctx.Response.AddHeader("Content-Range", $"bytes {start}-{end}/{totalSize}");
                        ctx.Response.ContentLength64 = contentLength;
                        await ctx.Response.OutputStream.FlushAsync();

                        fs.Seek(start, SeekOrigin.Begin);
                        var buffer = new byte[81920];
                        long remaining = contentLength;
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(buffer.Length, remaining);
                            int read = await fs.ReadAsync(buffer, 0, toRead);
                            if (read == 0) break;
                            await ctx.Response.OutputStream.WriteAsync(buffer, 0, read);
                            remaining -= read;
                        }
                    }
                    else
                    {
                        ctx.Response.ContentLength64 = new FileInfo(bgFile).Length;
                        await ctx.Response.OutputStream.FlushAsync();
                        using var fs = File.OpenRead(bgFile);
                        await fs.CopyToAsync(ctx.Response.OutputStream);
                    }
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            }
            catch { ctx.Response.StatusCode = 500; }
            ctx.Response.Close();
        }

        static void HandleAudioProxy(HttpListenerContext ctx)
        {
            // Audio代理: 由 MusicCacheManager 或后端处理
            // Photino 项目暂不完整实现，返回空
            // 后续可集成完整的音频代理逻辑
            ctx.Response.StatusCode = 501;
            ctx.Response.Close();
        }
    }
}
