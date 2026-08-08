using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MusicalNoteLauncher.Backend;
using MusicalNoteLauncher.Core;
using Newtonsoft.Json.Linq;

namespace MusicalNoteLauncher.Photino;

public class Program
{
    // ── 桥接方法处理器 ──
    private static readonly Dictionary<string, Func<object[], Task<object?>>> _h = new();
    private static CancellationTokenSource _cts = new();
    private static int _httpPort;

    [STAThread]
    public static void Main(string[] args)
    {
        // WinExe 无控制台，忽略 Console 设置
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        // 初始化 MNL 环境
        try { MNLEnvironment.Initialize(); }
        catch (Exception ex) { Logger.Error($"MNL 环境初始化失败: {ex.Message}"); }

        RegisterAll();

        // 启动轻量 HTTP 服务器（音频代理 + 背景图片）
        _httpPort = FindAvailablePort();
        var httpServer = new HttpListener();
        httpServer.Prefixes.Add($"http://localhost:{_httpPort}/");
        httpServer.Prefixes.Add($"http://127.0.0.1:{_httpPort}/");
        httpServer.Start();
        _ = Task.Run(() => HttpLoop(httpServer, _cts.Token));

        Logger.Info($"[Photino] HTTP http://localhost:{_httpPort}/");

        // 创建 Photino 窗口
        string webAppDir = Path.Combine(AppContext.BaseDirectory, "WebApp");
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "图标.ico");

        var window = new global::Photino.NET.PhotinoWindow()
            .SetTitle("MusicalNoteLauncher")
            .SetUseOsDefaultSize(false)
            .SetSize(1280, 800)
            .SetMinSize(900, 600)
            .SetContextMenuEnabled(false)
            .SetDevToolsEnabled(false);

        // WebMessage 处理器（必须在 Load 之前注册！）
        window.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            _ = HandleMessageAsync(sender, message);
        });

        // 通过自带 HTTP 服务器加载 WebApp
        window.Load($"http://localhost:{_httpPort}/index.html");

        // 图标（Windows 需判断文件存在）
        if (File.Exists(iconPath))
            window.SetIconFile(iconPath);

        window.WaitForClose();

        // 清理
        Logger.Info("[Photino] 关闭中...");
        _cts.Cancel();
        try { httpServer.Stop(); } catch { }
        try { httpServer.Close(); } catch { }
        // EventBus 无需显式释放
    }

    // ── 注册所有桥接方法 ──

    private static void RegisterAll()
    {
        // === 窗口控制 ===
        _h["Minimize"] = _ => { /* Photino 窗口最小化由 OS 标题栏处理 */ return Task.FromResult<object?>(null); };
        _h["Maximize"] = _ => Task.FromResult<object?>(null);
        _h["Close"] = _ => { Environment.Exit(0); return Task.FromResult<object?>(null); };

        // === 设置 ===
        _h["GetSetting"] = args => Task.FromResult<object?>(
            SettingsManager.Get(args[0]?.ToString() ?? ""));
        _h["SetSetting"] = args => {
            SettingsManager.Set(args[0]?.ToString() ?? "", args.Length > 1 ? args[1] : null);
            return Task.FromResult<object?>(new { success = true });
        };
        _h["GetAllSettings"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new {
                termsAccepted = SettingsManager.Settings.TermsOfServiceAccepted,
                terracottaAccepted = SettingsManager.Settings.TerracottaAgreementAccepted,
                autoLogin = SettingsManager.Settings.AutoLogin,
                checkUpdate = SettingsManager.Settings.CheckUpdate,
                bgMode = "",
                bgOpacity = 100
            }));
        _h["GetAllSettingsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new {
                termsAccepted = SettingsManager.Settings.TermsOfServiceAccepted,
                terracottaAccepted = SettingsManager.Settings.TerracottaAgreementAccepted,
                autoLogin = SettingsManager.Settings.AutoLogin,
                checkUpdate = SettingsManager.Settings.CheckUpdate,
                bgMode = "",
                bgOpacity = 100
            }));
        _h["GetGameFoldersJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new[] {
            new { path = McPath, name = "默认 (.minecraft)", active = true }
        }));
        _h["SelectGameFolder"] = _ => Task.FromResult<object?>(new { success = true });

        // === 账户 ===
        _h["GetProfile"] = _ => Task.FromResult<object?>(new {
            userName = "Player", uuid = "", type = "offline", isLoggedIn = false, isOffline = true
        });
        _h["GetProfileJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new {
            userName = "Player", uuid = "", type = "offline", isLoggedIn = false, isOffline = true
        }));
        _h["Login"] = _ => Task.FromResult<object?>(new { success = false, error = "暂不可用" });
        _h["Logout"] = _ => Task.FromResult<object?>(new { success = true });
        _h["SwitchMode"] = _ => Task.FromResult<object?>(new { success = true });
        _h["CancelMicrosoftAuth"] = _ => Task.FromResult<object?>(null);
        _h["AddOfflineAccountJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["DeleteAccountJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false }));
        _h["ProfileLogout"] = _ => Task.FromResult<object?>(null);
        _h["SwitchProfileAccount"] = _ => Task.FromResult<object?>(null);
        _h["SaveSkinBase64Json"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false }));

        // === 用户协议 ===
        _h["GetAgreementStatusJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new {
            termsAccepted = SettingsManager.Settings.TermsOfServiceAccepted,
            terracottaAccepted = SettingsManager.Settings.TerracottaAgreementAccepted
        }));
        _h["AcceptTermsOfServiceJson"] = _ => {
            SettingsManager.Settings.TermsOfServiceAccepted = true;
            SettingsManager.Save();
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };
        _h["AcceptTerracottaAgreementJson"] = _ => {
            SettingsManager.Settings.TerracottaAgreementAccepted = true;
            SettingsManager.Save();
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };
        _h["DeclineTermsOfService"] = _ => Task.FromResult<object?>(null);

        // === 版本管理 ===
        _h["GetInstalledVersions"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetInstalledVersionsJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetInstalledBedrockVersions"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetInstalledBedrockVersionsJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetAvailableVersions"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetAvailableVersionsJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetAvailableBedrockVersionsJson"] = _ => Task.FromResult<object?>("[]");
        _h["InstallVersion"] = _ => Task.FromResult<object?>(new { success = false, error = "未实现" });
        _h["LaunchGame"] = _ => Task.FromResult<object?>(new { success = false, error = "未实现" });
        _h["DeleteVersion"] = _ => Task.FromResult<object?>(new { success = false });
        _h["InstallBedrockVersionJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["GetBedrockDownloadProgressJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { progress = 0, isDownloading = false }));
        _h["CancelBedrockDownloadJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));

        // === 模组 / 存档 / 下载 / 商店 / 背景 / 社区 / 联机 / 好友 / 聊天 ===
        _h["GetInstalledMods"] = _ => Task.FromResult<object?>(new List<object>());
        _h["SearchMods"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetInstalledModsJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { mods = new List<object>() }));
        _h["GetSavedWorldsJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { saves = new List<object>(), hasSaves = false }));
        _h["GetDownloadTasks"] = _ => Task.FromResult<object?>(new List<object>());
        _h["FetchBbsmcJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { items = new List<object>(), hasMore = false }));
        _h["GetStoreComponentsJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { components = new List<object>(), total = 0, page = 1 }));
        _h["GetBackgroundsJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { backgrounds = new List<object>(), total = 0 }));
        _h["GetCommunityUserInfo"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { isLoggedIn = false }));
        _h["GetCommunityMessagesJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { messages = new List<object>() }));
        _h["GetCommunityChannelsJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { channels = new List<object>() }));
        _h["GetFriendList"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetMultiplayerToolsStatusJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new {
            mctier = new { installed = false }, taowa = new { installed = false }
        }));
        _h["GetRedstoneServersJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["GetSearchJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { items = new List<object>(), hasMore = false }));

        // === 自定义下载 ===
        _h["GetCustomDownloadPathJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new {
            path = SettingsManager.Settings.CustomDownloadPath ?? ""
        }));
        _h["SetCustomDownloadPathJson"] = args => {
            SettingsManager.Settings.CustomDownloadPath = args[0]?.ToString() ?? "";
            SettingsManager.Save();
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };
        _h["CustomDlOpenFileJson"] = args => {
            PlatformHelper.OpenFile(args[0]?.ToString() ?? "");
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };
        _h["CustomDlOpenFolderJson"] = args => {
            PlatformHelper.OpenFolder(args[0]?.ToString() ?? "");
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };

        // === 更新 ===
        _h["GetAutoCheckUpdateJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new {
            autoCheckUpdate = SettingsManager.Settings.CheckUpdate
        }));
        _h["CheckForUpdateJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["GetAppVersion"] = _ => Task.FromResult<object?>("1.2.1");

        // === 工具 ===
        _h["OpenUrl"] = args => {
            PlatformHelper.OpenUrl(args[0]?.ToString() ?? "");
            return Task.FromResult<object?>(new { success = true });
        };
        _h["GetJavaList"] = _ => Task.FromResult<object?>(new List<object>());
        _h["DetectJavaJson"] = _ => Task.FromResult<object?>("[]");

        // === 音乐 ===
        _h["SearchMusicJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetSongUrlJson"] = _ => Task.FromResult<object?>(null);

        // === AI ===
        _h["AIChat"] = _ => Task.FromResult<object?>("AI 暂不可用");
        _h["GetAIModelsJson"] = _ => Task.FromResult<object?>("[]");

        // === 对话框 ===
        _h["ShowOpenFileDialog"] = _ => Task.FromResult<object?>(null);
#if WINDOWS
        _h["ShowOpenFolderDialog"] = _ =>
        {
            var result = (string?)null;
            var tcs = new TaskCompletionSource<string?>();
            var thread = new Thread(() =>
            {
                try
                {
                    using var dialog = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = "选择文件夹",
                        ShowNewFolderButton = true
                    };
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        result = dialog.SelectedPath;
                }
                catch { }
                tcs.SetResult(result);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return Task.FromResult<object?>(tcs.Task.Result);
        };
#else
        _h["ShowOpenFolderDialog"] = _ => Task.FromResult<object?>(null);
#endif

        // === 游戏目录管理 ===
        _h["AddGameFolderJson"] = args =>
        {
            var folderPath = args.ElementAtOrDefault(0)?.ToString() ?? "";
            var displayName = args.ElementAtOrDefault(1)?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(displayName))
                return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "参数无效" }));

            var s = SettingsManager.Settings;
            s.GameFolders ??= new List<GameFolderEntry>();
            string normalized = Environment.ExpandEnvironmentVariables(folderPath).TrimEnd('\\') + "\\";

            for (int i = 0; i < s.GameFolders.Count; i++)
            {
                if (string.Equals(s.GameFolders[i].NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    s.SelectedGameFolderIndex = i;
                    SettingsManager.Save();
                    return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true, alreadyExists = true }));
                }
            }
            s.GameFolders.Add(new GameFolderEntry(displayName, folderPath));
            s.SelectedGameFolderIndex = s.GameFolders.Count - 1;
            SettingsManager.Save();
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };
        _h["RemoveGameFolderJson"] = args =>
        {
            var indexPath = args.ElementAtOrDefault(0)?.ToString() ?? "";
            var s = SettingsManager.Settings;
            if (s.GameFolders == null || s.GameFolders.Count == 0)
                return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "没有可删除的文件夹" }));

            for (int i = 0; i < s.GameFolders.Count; i++)
            {
                string normalized = Environment.ExpandEnvironmentVariables(s.GameFolders[i].Path).TrimEnd('\\') + "\\";
                string target = Environment.ExpandEnvironmentVariables(indexPath).TrimEnd('\\') + "\\";
                if (string.Equals(normalized, target, StringComparison.OrdinalIgnoreCase))
                {
                    s.GameFolders.RemoveAt(i);
                    if (s.SelectedGameFolderIndex >= s.GameFolders.Count)
                        s.SelectedGameFolderIndex = Math.Max(0, s.GameFolders.Count - 1);
                    SettingsManager.Save();
                    return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
                }
            }
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "未找到该文件夹" }));
        };
        _h["RenameGameFolderJson"] = args =>
        {
            var folderPath = args.ElementAtOrDefault(0)?.ToString() ?? "";
            var newName = args.ElementAtOrDefault(1)?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(newName))
                return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "参数无效" }));

            var s = SettingsManager.Settings;
            if (s.GameFolders == null || s.GameFolders.Count == 0)
                return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "列表为空" }));

            string normalized = Environment.ExpandEnvironmentVariables(folderPath).TrimEnd('\\') + "\\";
            var entry = s.GameFolders.FirstOrDefault(f =>
                string.Equals(f.NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "未找到" }));

            entry.Name = newName.Trim();
            SettingsManager.Save();
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };
        _h["OpenGameFolderJson"] = args =>
        {
            var folderPath = args.ElementAtOrDefault(0)?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(folderPath))
                return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "路径为空" }));

            string expanded = Environment.ExpandEnvironmentVariables(folderPath);
            if (!Directory.Exists(expanded))
                return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = false, error = "目录不存在" }));

            System.Diagnostics.Process.Start("explorer.exe", expanded);
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };

        // === 杂项 ===
        _h["GetMemoryInfo"] = _ => Task.FromResult<object?>(new { total = 0L, used = 0L, free = 0L, processMb = 0L });
    }

    private static string McPath => Environment.ExpandEnvironmentVariables(
        SettingsManager.Settings.GamePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"));

    // ── Photino 消息处理 ──

    private static async Task HandleMessageAsync(object sender, string message)
    {
        var window = (global::Photino.NET.PhotinoWindow)sender;
        JObject? req = null;
        int id = 0;

        try
        {
            req = JObject.Parse(message);
            id = req["id"]?.Value<int>() ?? 0;
        }
        catch { return; }

        var method = req["method"]?.ToString() ?? "";
        var args = (req["args"] as JArray)
            ?.Select(a => (object)((JToken)a).ToObject<object>()!).ToArray()
            ?? Array.Empty<object>();

        object? result = null;
        string? error = null;

        if (_h.TryGetValue(method, out var handler))
        {
            try { result = await handler(args); }
            catch (Exception ex) { error = ex.Message; }
        }

        var response = JsonSerializer.Serialize(new { id, result, error },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        window.SendWebMessage(response);
    }

    // ── 轻量 HTTP 服务器（音频代理 + 背景图片） ──

    private static async Task HttpLoop(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleHttpAsync(ctx), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex) { Logger.Error($"[HTTP] Accept: {ex.Message}"); }
        }
    }

    private static async Task HandleHttpAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url!.AbsolutePath;
            var pathLower = path.ToLowerInvariant();

            Logger.Info($"[HTTP] REQ: {ctx.Request.HttpMethod} {path}");

            if (pathLower == "/api/audio" || pathLower.StartsWith("/api/audio?"))
            {
                await HandleAudioProxyAsync(ctx);
                return;
            }
            if (pathLower.StartsWith("/api/bg/"))
            {
                await HandleBgFileAsync(ctx);
                return;
            }
            // 静态文件（WebApp）
            await ServeStaticFileAsync(ctx, path);
        }
        catch (Exception ex)
        {
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
            Logger.Error($"[HTTP] 请求出错: {ex.Message}");
        }
    }

    private static async Task ServeStaticFileAsync(HttpListenerContext ctx, string path)
    {
        // 路由 / → /index.html
        if (path == "/" || path == "")
            path = "/index.html";

        var webAppDir = Path.Combine(AppContext.BaseDirectory, "WebApp");
        var filePath = Path.Combine(webAppDir, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        // 安全检查：防止路径遍历
        if (!filePath.StartsWith(webAppDir))
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.Close();
            return;
        }

        if (!File.Exists(filePath))
        {
            Logger.Warning($"[HTTP] 404: {path}");
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
            return;
        }

        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            ctx.Response.ContentType = ext switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            };
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            using var fs = File.OpenRead(filePath);
            ctx.Response.ContentLength64 = fs.Length;
            await fs.CopyToAsync(ctx.Response.OutputStream);
        }
        finally
        {
            try { ctx.Response.Close(); } catch { }
        }
    }

    private static async Task HandleAudioProxyAsync(HttpListenerContext ctx)
    {
        var url = ctx.Request.QueryString["url"];
        if (string.IsNullOrEmpty(url)) { ctx.Response.StatusCode = 400; ctx.Response.Close(); return; }

        try
        {
            // 302 手动跟随
            string finalUrl = url;
            for (int i = 0; i < 5; i++)
            {
                var hc = SafeHttpClientFactory.CreateClient();
                hc.Timeout = TimeSpan.FromSeconds(10);
                var checkResp = await hc.GetAsync(finalUrl, HttpCompletionOption.ResponseHeadersRead);
                if ((int)checkResp.StatusCode >= 300 && (int)checkResp.StatusCode < 400
                    && checkResp.Headers.Location != null)
                {
                    finalUrl = checkResp.Headers.Location.IsAbsoluteUri
                        ? checkResp.Headers.Location.AbsoluteUri
                        : new Uri(new Uri(finalUrl), checkResp.Headers.Location).AbsoluteUri;
                    continue;
                }
                break;
            }

            using var client = SafeHttpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var resp = await client.GetAsync(finalUrl, HttpCompletionOption.ResponseHeadersRead);
            ctx.Response.StatusCode = (int)resp.StatusCode;
            ctx.Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? "audio/mpeg";
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            await resp.Content.CopyToAsync(ctx.Response.OutputStream);
        }
        catch { ctx.Response.StatusCode = 502; }
        finally { try { ctx.Response.Close(); } catch { } }
    }

    private static async Task HandleBgFileAsync(HttpListenerContext ctx)
    {
        var fileName = ctx.Request.Url!.AbsolutePath.Split("/api/bg/").LastOrDefault() ?? "";
        fileName = Uri.UnescapeDataString(fileName);
        var bgDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MNL", "backgrounds");
        var filePath = Path.Combine(bgDir, fileName);

        if (!File.Exists(filePath)) { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }

        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            ctx.Response.ContentType = ext switch
            {
                ".mp4" => "video/mp4", ".webm" => "video/webm",
                ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png",
                ".gif" => "image/gif", ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            ctx.Response.Headers.Add("Cache-Control", "max-age=3600");
            using var fs = File.OpenRead(filePath);
            await fs.CopyToAsync(ctx.Response.OutputStream);
        }
        finally { try { ctx.Response.Close(); } catch { } }
    }

    private static int FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
