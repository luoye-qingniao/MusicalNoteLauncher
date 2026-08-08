using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using MusicalNoteLauncher.Core;
using Newtonsoft.Json.Linq;

namespace MusicalNoteLauncher.Backend;

/// <summary>
/// HTTP 桥接后端。Electron 壳以此作为子进程，替代 WebView2 hostObjects。
/// POST /api/bridge → 接收 { method, args } → 调用对应处理器 → 返回 { result, error }
/// GET /api/events → SSE 事件流（C# → JS 推送）
/// </summary>
public class BackendServer
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private string WebAppDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebApp");
    private string McPath => Environment.ExpandEnvironmentVariables(
        SettingsManager.Settings.GamePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"));

    // ── 桥接方法处理器字典 ──
    private readonly Dictionary<string, Func<object[], Task<object?>>> _h = new();

    public BackendServer()
    {
        RegisterAll();
    }

    private void RegisterAll()
    {
        // === 窗口控制（Electron 中由 IPC 处理，此处空实现） ===
        _h["Minimize"] = _ => Task.FromResult<object?>(null);
        _h["Maximize"] = _ => Task.FromResult<object?>(null);
        _h["Close"] = _ => { Environment.Exit(0); return Task.FromResult<object?>(null); };

        // === 设置 ===
        _h["GetSetting"] = args => Task.FromResult<object?>(
            SettingsManager.Get(args[0]?.ToString() ?? ""));
        _h["SetSetting"] = args => {
            SettingsManager.Set(args[0]?.ToString() ?? "", args.Length > 1 ? args[1] : null);
            return Task.FromResult<object?>(new { success = true });
        };
        _h["GetAllSettings"] = _ => Task.FromResult<object?>(SettingsManager.Settings);
        _h["GetAllSettingsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(SettingsManager.Settings));
        _h["GetGameFoldersJson"] = _ => {
            var folders = new[] { new { path = McPath, name = "默认 (.minecraft)", active = true } };
            return Task.FromResult<object?>(JsonSerializer.Serialize(folders));
        };
        _h["SelectGameFolder"] = _ => Task.FromResult<object?>(new { success = true });

        // === 账户 ===
        _h["GetProfile"] = _ => Task.FromResult<object?>(new {
            userName = "Player", uuid = "", type = "offline", isLoggedIn = false, isOffline = true
        });
        _h["GetProfileJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new {
            userName = "Player", uuid = "", type = "offline", isLoggedIn = false, isOffline = true
        }));
        _h["Login"] = _ => Task.FromResult<object?>(new { success = false, error = "请使用 WPF 版本登录" });
        _h["Logout"] = _ => Task.FromResult<object?>(new { success = true });
        _h["SwitchMode"] = _ => Task.FromResult<object?>(new { success = true });
        _h["CancelMicrosoftAuth"] = _ => Task.FromResult<object?>(null);
        _h["AddOfflineAccountJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["DeleteAccountJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["ProfileLogout"] = _ => Task.FromResult<object?>(null);
        _h["SwitchProfileAccount"] = _ => Task.FromResult<object?>(null);
        _h["SetQingniaoNameJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["QingNiaoAuthJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["UploadQingniaoAvatarJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["UploadAccountSkinJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["RefreshAccountHeadJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["SaveSkinBase64Json"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));

        // === 版本管理 ===
        _h["GetInstalledVersions"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetInstalledVersionsJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetInstalledBedrockVersions"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetInstalledBedrockVersionsJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetAvailableVersions"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetAvailableVersionsJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetAvailableBedrockVersionsJson"] = _ => Task.FromResult<object?>("[]");
        _h["InstallVersion"] = async args =>
        {
            try
            {
                var versionId = args.ElementAtOrDefault(0)?.ToString() ?? "";
                var loader = args.ElementAtOrDefault(1)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(versionId))
                    return (object?)JsonSerializer.Serialize(new { success = false, error = "版本ID为空" });

                Logger.Info($"[Backend] InstallVersion: {versionId}, loader: {loader}");

                // 先从清单获取版本 JSON 的下载地址
                var downloadService = new VersionDownloadService(McPath);
                var versionUrl = await downloadService.GetVersionDownloadUrlAsync(versionId);

                if (string.IsNullOrEmpty(versionUrl))
                    return (object?)JsonSerializer.Serialize(new { success = false, error = $"无法获取版本 {versionId} 的下载地址" });

                var versionInfo = new VersionInfo
                {
                    Id = versionId,
                    Type = "release",
                    Url = versionUrl
                };

                // 异步启动下载（不等待完成，通过事件 / SSE 推送进度）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await downloadService.StartDownloadAsync(versionInfo, null);
                        EventBus.Push("version-downloaded", versionId);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[Backend] 版本下载失败: {versionId}, {ex.Message}");
                        EventBus.Push("version-download-failed",
                            JsonSerializer.Serialize(new { versionId, error = ex.Message }));
                    }
                });

                return (object?)JsonSerializer.Serialize(new { success = true, message = "下载已启动" });
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backend] InstallVersion 异常: {ex.Message}");
                return (object?)JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        };
        _h["LaunchGame"] = _ => Task.FromResult<object?>(new { success = false, error = "未实现" });
        _h["DeleteVersion"] = _ => Task.FromResult<object?>(new { success = false });
        _h["InstallBedrockVersionJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["GetBedrockDownloadProgressJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { progress = 0, isDownloading = false }));
        _h["CancelBedrockDownloadJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));

        // === 模组 ===
        _h["GetInstalledMods"] = _ => Task.FromResult<object?>(new List<object>());
        _h["SearchMods"] = async args =>
        {
            try
            {
                var query = args.ElementAtOrDefault(0)?.ToString() ?? "";
                var gameVersion = args.ElementAtOrDefault(1)?.ToString() ?? "";
                var limit = args.ElementAtOrDefault(2) is int l ? l : 20;
                var offset = args.ElementAtOrDefault(3) is int o ? o : 0;

                Logger.Info($"[Backend] SearchMods: query='{query}', gameVersion='{gameVersion}', limit={limit}");

                var modrinth = new ModrinthApiService();
                var results = await modrinth.SearchMods(query, gameVersion, limit, offset);

                return (object?)JsonSerializer.Serialize(results);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backend] SearchMods 异常: {ex.Message}");
                return (object?)JsonSerializer.Serialize(new { error = ex.Message, items = new List<object>() });
            }
        };
        _h["InstallMod"] = async args =>
        {
            try
            {
                var projectId = args.ElementAtOrDefault(0)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(projectId))
                    return (object?)JsonSerializer.Serialize(new { success = false, error = "项目ID为空" });

                Logger.Info($"[Backend] InstallMod: projectId={projectId}");

                var modrinth = new ModrinthApiService();

                // 获取项目的所有版本
                var versions = await modrinth.GetProjectVersions(projectId);
                if (versions == null || versions.Count == 0)
                    return (object?)JsonSerializer.Serialize(new { success = false, error = $"未找到项目 {projectId} 的可用版本" });

                // 取最新版本（已按 release > beta > alpha 排序）
                var targetVersion = versions[0];

                // 直接使用 ModrinthVersion 的 DownloadUrl（已包含文件下载链接）
                var downloadUrl = targetVersion.DownloadUrl;
                if (string.IsNullOrEmpty(downloadUrl))
                    return (object?)JsonSerializer.Serialize(new { success = false, error = "无法获取下载链接" });

                Logger.Info($"[Backend] InstallMod: 下载 {targetVersion.VersionNumber} → {downloadUrl}");

                // 异步下载 mod 文件
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var modsDir = Path.Combine(McPath, "mods");
                        Directory.CreateDirectory(modsDir);
                        var fileName = targetVersion.FileName;
                        if (string.IsNullOrEmpty(fileName))
                            fileName = $"{projectId}-{targetVersion.VersionNumber}.jar";
                        var savePath = Path.Combine(modsDir, fileName);

                        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("MNL/1.2");
                        var data = await client.GetByteArrayAsync(downloadUrl);
                        await File.WriteAllBytesAsync(savePath, data);

                        Logger.Info($"[Backend] Mod 下载完成: {savePath}");
                        EventBus.Push("mod-installed",
                            JsonSerializer.Serialize(new { projectId, version = targetVersion.VersionNumber, path = savePath }));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[Backend] Mod 下载失败: {ex.Message}");
                        EventBus.Push("mod-install-failed",
                            JsonSerializer.Serialize(new { projectId, error = ex.Message }));
                    }
                });

                return (object?)JsonSerializer.Serialize(new
                {
                    success = true,
                    version = targetVersion.VersionNumber,
                    fileName = targetVersion.FileName,
                    downloadUrl,
                    message = "下载已启动"
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backend] InstallMod 异常: {ex.Message}");
                return (object?)JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        };
        _h["UninstallMod"] = _ => Task.FromResult<object?>(new { success = false });
        _h["GetModDetail"] = _ => Task.FromResult<object?>(new { });

        // === 模组管理器 ===
        _h["DetectModLoadersJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { loaders = new List<object>(), hasLoader = false }));
        _h["GetInstalledModsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { mods = new List<object>() }));
        _h["ToggleModJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["DeleteModJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));

        // === 存档 ===
        _h["GetSaves"] = _ => Task.FromResult<object?>(new List<object>());
        _h["BackupSave"] = _ => Task.FromResult<object?>(new { success = false });
        _h["RestoreSave"] = _ => Task.FromResult<object?>(new { success = false });
        _h["GetSavedWorldsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { saves = new List<object>(), hasSaves = false }));
        _h["BackupSaveJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["RenameSaveJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["DeleteSaveJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["ImportSaveJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));

        // === 下载管理 ===
        _h["GetDownloadTasks"] = _ => Task.FromResult<object?>(new List<object>());
        _h["CancelDownload"] = _ => Task.FromResult<object?>(new { success = true });
        _h["PauseDownload"] = _ => Task.FromResult<object?>(new { success = true });
        _h["ResumeDownload"] = _ => Task.FromResult<object?>(new { success = true });
        _h["SearchDownloadItemsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { items = new List<object>(), hasMore = false }));
        _h["GetDependenciesJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { items = new List<object>() }));

        // === 组件商店 ===
        _h["GetStoreComponentsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { components = new List<object>(), total = 0, page = 1 }));
        _h["TrackStoreDownloadJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));

        // === 背景 ===
        _h["GetBackgroundsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { backgrounds = new List<object>(), total = 0 }));
        _h["UploadBackgroundJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["UseBackgroundJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["DeleteBackgroundJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));

        // === 协议 ===
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

        // === 自定义下载 ===
        _h["GetCustomDownloadPathJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new {
            path = SettingsManager.Settings.CustomDownloadPath ?? ""
        }));
        _h["SetCustomDownloadPathJson"] = args => {
            SettingsManager.Settings.CustomDownloadPath = args[0]?.ToString() ?? "";
            SettingsManager.Save();
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };
        _h["StartCustomDownloadJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["GetCustomDownloadProgressJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { status = "idle" }));
        _h["CancelCustomDownloadJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["CustomDlOpenFileJson"] = args => {
            PlatformHelper.OpenFile(args[0]?.ToString() ?? "");
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };
        _h["CustomDlOpenFolderJson"] = args => {
            PlatformHelper.OpenFolder(args[0]?.ToString() ?? "");
            return Task.FromResult<object?>(JsonSerializer.Serialize(new { success = true }));
        };

        // === 更新 ===
        _h["GetAutoCheckUpdateJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { autoCheckUpdate = SettingsManager.Settings.CheckUpdate }));
        _h["SetAutoCheckUpdateJson"] = args => {
            SettingsManager.Settings.CheckUpdate = Convert.ToBoolean(args[0]);
            SettingsManager.Save();
            return Task.FromResult<object?>(new { success = true });
        };
        _h["CheckForUpdateJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["DownloadUpdateJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["GetUpdateProgressJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { status = "", percent = 0 }));

        // === 资源包 ===
        _h["ManageShaders"] = _ => Task.FromResult<object?>(new { success = false });
        _h["ManageDatapacks"] = _ => Task.FromResult<object?>(new { success = false });
        _h["ManageTexturePacks"] = _ => Task.FromResult<object?>(new { success = false });

        // === 整合包 ===
        _h["SearchModpacks"] = _ => Task.FromResult<object?>(new List<object>());
        _h["ImportModpack"] = _ => Task.FromResult<object?>(new { success = false });
        _h["ImportModpackWithDialog"] = _ => Task.FromResult<object?>(new { success = false });
        _h["ProbeModpackWithDialog"] = _ => Task.FromResult<object?>(new { success = false });
        _h["StartModpackInstall"] = _ => Task.FromResult<object?>(new { success = false });
        _h["InstallModpack"] = _ => Task.FromResult<object?>(new { success = false });

        // === 社区 ===
        _h["GetCommunityUserInfo"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new {
            isLoggedIn = false
        }));
        _h["GetCommunityChannelsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { channels = new List<object>() }));
        _h["GetCommunityMessagesJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { messages = new List<object>() }));
        _h["SendCommunityMessageJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["CreateCommunityChannelJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));

        // === 联机 ===
        _h["GetMultiplayerToolsStatusJson"] = _ => Task.FromResult<object?>(JsonSerializer.Serialize(new {
            mctier = new { installed = false }, taowa = new { installed = false }
        }));
        _h["DownloadMctierJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["OpenMctierFolderJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["LaunchMctierJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["DownloadTaowaJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));

        // ── RedStone 红石联机 ──
        _h["GetRedstoneServersJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false, error = "未实现" }));
        _h["RegisterRedstoneApikeyJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["CreateRedstoneTunnelJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["QueryRedstoneTunnelJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["DeleteRedstoneTunnelJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["GetRedstonePublicRoomsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["GetRedstoneRoomModsJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["StartRedstoneRelayJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["StopRedstoneRelayJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { success = false }));
        _h["GetRedstoneRelayStatusJson"] = _ => Task.FromResult<object?>(
            JsonSerializer.Serialize(new { running = false, localPort = 0, controlStatus = "" }));

        // === 好友 ===
        _h["GetFriendList"] = _ => Task.FromResult<object?>(new List<object>());
        _h["AddFriend"] = _ => Task.FromResult<object?>(new { success = false });
        _h["RemoveFriend"] = _ => Task.FromResult<object?>(new { success = false });

        // === 聊天 ===
        _h["GetMyPublicKey"] = _ => Task.FromResult<object?>("");
        _h["SendEncryptedChatMessage"] = _ => Task.FromResult<object?>(new { success = false });
        _h["GetChatHistory"] = _ => Task.FromResult<object?>(new List<object>());
        _h["PollChatMessages"] = _ => Task.FromResult<object?>(new List<object>());

        // === 音乐 ===
        _h["SearchMusic"] = _ => Task.FromResult<object?>(new List<object>());
        _h["SearchMusicPlaylists"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetMusicPlaylistDetail"] = _ => Task.FromResult<object?>(new List<object>());
        _h["PlayMusic"] = _ => Task.FromResult<object?>(new { success = true });
        _h["PauseMusic"] = _ => Task.FromResult<object?>(new { success = true });
        _h["ResumeMusic"] = _ => Task.FromResult<object?>(new { success = true });
        _h["GetMusicPlaylist"] = _ => Task.FromResult<object?>(new List<object>());
        _h["SearchMusicJson"] = _ => Task.FromResult<object?>("[]");
        _h["SearchMusicPlaylistsJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetMusicPlaylistDetailJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetSongUrlJson"] = _ => Task.FromResult<object?>(null);

        // === AI ===
        _h["AIChat"] = _ => Task.FromResult<object?>(
            "音涟暂不可用。请使用 WPF 版本。");
        _h["GetAIModelsJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetAIConfigJson"] = _ => Task.FromResult<object?>("{}");
        _h["SaveAIConfig"] = _ => Task.FromResult<object?>(new { success = false });
        _h["ClearAIHistory"] = _ => Task.FromResult<object?>(new { success = true });

        // === 对话框 ===
        _h["ShowOpenFileDialog"] = _ => Task.FromResult<object?>(null);
        _h["ShowOpenFolderDialog"] = _ => Task.FromResult<object?>(null);

        // === 推荐 ===
        _h["GetRecommendationsJson"] = _ => Task.FromResult<object?>("[]");
        _h["GetDownloadOptionsJson"] = _ => Task.FromResult<object?>("[]");
        _h["DownloadRecommendJson"] = _ => Task.FromResult<object?>("{}");

        // === Java ===
        _h["DetectJava"] = _ => Task.FromResult<object?>(new List<object>());
        _h["GetJavaList"] = _ => Task.FromResult<object?>(new List<object>());
        _h["DownloadJava"] = _ => Task.FromResult<object?>(new { success = false });
        _h["SaveJavaPath"] = _ => Task.FromResult<object?>(new { success = true });
        _h["GetSavedJavaPath"] = _ => Task.FromResult<object?>("");
        _h["DetectJavaJson"] = _ => Task.FromResult<object?>("[]");

        // === 工具 ===
        _h["OpenUrl"] = args => {
            PlatformHelper.OpenUrl(args[0]?.ToString() ?? "");
            return Task.FromResult<object?>(new { success = true });
        };
        _h["GetAppVersion"] = _ => Task.FromResult<object?>("1.2.1");
        _h["GetMemoryInfo"] = _ => Task.FromResult<object?>(new {
            total = 0L, used = 0L, free = 0L, processMb = 0L
        });
        _h["OptimizeMemory"] = _ => Task.FromResult<object?>(new { success = false });
        _h["GetGpuInfo"] = _ => Task.FromResult<object?>(new { name = "", vramMb = 0 });
    }

    // ── HTTP 服务器 ──

    public async Task<int> StartAsync()
    {
        int port = FindAvailablePort();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();

        Console.WriteLine($"PORT:{port}");
        Console.Out.Flush();

        Logger.Info($"[Backend] HTTP http://localhost:{port}/");

        _ = Task.Run(() => AcceptLoop(_cts.Token));
        await Task.Delay(-1, _cts.Token).ContinueWith(_ => { });
        return port;
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequestAsync(ctx), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex) { Logger.Error($"[Backend] Accept: {ex.Message}"); }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url!.AbsolutePath.ToLowerInvariant();
            var method = ctx.Request.HttpMethod.ToUpperInvariant();

            // CORS 预检
            if (method == "OPTIONS")
            {
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                return;
            }

            if (path == "/api/events") { await HandleSseAsync(ctx); return; }
            if (path == "/api/bridge" && method == "POST") { await HandleBridgeAsync(ctx); return; }
            if (path == "/api/audio") { await HandleAudioProxyAsync(ctx); return; }
            if (path.StartsWith("/api/bg/")) { await HandleBgFileAsync(ctx); return; }

            await ServeStaticFileAsync(ctx);
        }
        catch (Exception ex)
        {
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); }
            catch { }
            Logger.Error($"[Backend] 请求出错: {ex.Message}");
        }
    }

    // ── POST /api/bridge ──

    private async Task HandleBridgeAsync(HttpListenerContext ctx)
    {
        JObject? req = null;
        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            req = JObject.Parse(body);
        }
        catch { }

        var methodName = req?["method"]?.ToString() ?? "";
        var argsArray = req?["args"] as JArray;
        var args = argsArray?.Select(a => (object)((JToken)a).ToObject<object>()!).ToArray()
                   ?? Array.Empty<object>();

        object? result = null;
        string? error = null;

        if (_h.TryGetValue(methodName, out var handler))
        {
            try { result = await handler(args); }
            catch (Exception ex) { error = ex.Message; }
        }

        var response = JsonSerializer.Serialize(new { result, error });
        var buffer = Encoding.UTF8.GetBytes(response);
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        await ctx.Response.OutputStream.WriteAsync(buffer);
        ctx.Response.Close();
    }

    // ── GET /api/events (SSE) ──

    private async Task HandleSseAsync(HttpListenerContext ctx)
    {
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.Add("Cache-Control", "no-cache");
        ctx.Response.Headers.Add("Connection", "keep-alive");
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        ctx.Response.StatusCode = 200;

        var output = ctx.Response.OutputStream;

        // 发送连接确认
        var init = Encoding.UTF8.GetBytes("data: {\"type\":\"connected\"}\n\n");
        await output.WriteAsync(init);
        await output.FlushAsync();

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var evt = await EventBus.WaitAsync(TimeSpan.FromSeconds(30), _cts.Token);
                if (evt == null)
                {
                    var ping = Encoding.UTF8.GetBytes(": heartbeat\n\n");
                    await output.WriteAsync(ping);
                    await output.FlushAsync();
                    continue;
                }

                var payload = JsonSerializer.Serialize(new { type = evt.Type, payload = evt.Payload });
                var data = Encoding.UTF8.GetBytes($"data: {payload}\n\n");
                await output.WriteAsync(data);
                await output.FlushAsync();
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex) { Logger.Warning($"[SSE] 关闭: {ex.Message}"); }
        finally { try { ctx.Response.Close(); } catch { } }
    }

    // ── GET /api/audio?url=... ──

    private async Task HandleAudioProxyAsync(HttpListenerContext ctx)
    {
        var url = ctx.Request.QueryString["url"];
        if (string.IsNullOrEmpty(url)) { ctx.Response.StatusCode = 400; ctx.Response.Close(); return; }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MNL/1.2");
            var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            ctx.Response.StatusCode = (int)resp.StatusCode;
            ctx.Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? "audio/mpeg";
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            if (ctx.Request.Headers["Range"] != null)
                ctx.Response.Headers.Add("Accept-Ranges", "bytes");

            await resp.Content.CopyToAsync(ctx.Response.OutputStream);
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 502;
            var err = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}");
            await ctx.Response.OutputStream.WriteAsync(err);
        }
        finally { try { ctx.Response.Close(); } catch { } }
    }

    // ── GET /api/bg/* ──

    private async Task HandleBgFileAsync(HttpListenerContext ctx)
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

    // ── 静态文件服务 ──

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8", [".css"] = "text/css; charset=utf-8",
        [".js"] = "application/javascript; charset=utf-8", [".json"] = "application/json; charset=utf-8",
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif", [".svg"] = "image/svg+xml", [".ico"] = "image/x-icon",
        [".webp"] = "image/webp", [".mp4"] = "video/mp4", [".webm"] = "video/webm",
        [".mp3"] = "audio/mpeg", [".woff"] = "font/woff", [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf", [".txt"] = "text/plain; charset=utf-8",
    };

    private async Task ServeStaticFileAsync(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(path) || path.EndsWith("/")) path = "index.html";

        path = path.Replace("\\", "/");
        while (path.Contains("..")) path = path.Replace("..", "");

        var filePath = Path.Combine(WebAppDir, path);
        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(WebAppDir, "index.html");
            if (!File.Exists(filePath)) { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }
        }

        try
        {
            var ext = Path.GetExtension(filePath);
            ctx.Response.ContentType = MimeTypes.TryGetValue(ext, out var m) ? m : "application/octet-stream";
            ctx.Response.Headers.Add("Cache-Control", "no-cache");

            if (ctx.Request.Headers["Range"] != null)
                await ServeRangeFileAsync(ctx, filePath);
            else
            {
                using var fs = File.OpenRead(filePath);
                ctx.Response.ContentLength64 = fs.Length;
                await fs.CopyToAsync(ctx.Response.OutputStream);
            }
        }
        finally { try { ctx.Response.Close(); } catch { } }
    }

    private async Task ServeRangeFileAsync(HttpListenerContext ctx, string filePath)
    {
        var fi = new FileInfo(filePath);
        var range = ctx.Request.Headers["Range"]!.Replace("bytes=", "").Split('-');
        long start = long.Parse(range[0]);
        long end = range.Length > 1 && !string.IsNullOrEmpty(range[1]) ? long.Parse(range[1]) : fi.Length - 1;

        ctx.Response.StatusCode = 206;
        ctx.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{fi.Length}");
        ctx.Response.Headers.Add("Accept-Ranges", "bytes");
        ctx.Response.ContentLength64 = end - start + 1;

        using var fs = File.OpenRead(filePath);
        fs.Seek(start, SeekOrigin.Begin);
        var remaining = end - start + 1;
        var buffer = new byte[81920];
        while (remaining > 0)
        {
            var read = await fs.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0) break;
            await ctx.Response.OutputStream.WriteAsync(buffer, 0, read);
            remaining -= read;
        }
    }

    private static int FindAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Shutdown()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
    }
}
