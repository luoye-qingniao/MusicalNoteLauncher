using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Net.Http;
#if WINDOWS
using System.Windows;
#endif
using System.Threading;
using System.Threading.Tasks;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.ViewModels;
#if WINDOWS
using Microsoft.Web.WebView2.Core;
#endif
using Newtonsoft.Json.Linq;

namespace MusicalNoteLauncher
{
    /// <summary>
    /// WebView2 ↔ C# IPC 桥接类。
    /// JS 通过 window.chrome.webview.hostObjects.mnl 调用本类方法。
    /// </summary>
    [System.Runtime.InteropServices.ClassInterface(
        System.Runtime.InteropServices.ClassInterfaceType.AutoDual)]
    public class WebBridge
    {
#if WINDOWS
        private readonly MainWindow _window;
#else
        private readonly Photino.NET.PhotinoWindow _window;
        private readonly LauncherCore _launcherCore;
        private readonly BedrockEnhancedDownloadService _bedrockSvc;
        private readonly BedrockOfflineLauncher _bedrockLauncher;
        private readonly string _minecraftPath;
#endif
        private BedrockEnhancedDownloadService _bedrockSvcInstance;
        private BedrockOfflineLauncher _bedrockLauncherInstance;
        private CancellationTokenSource _bedrockInstallCts;
        private BedrockVersionInfo _bedrockCurrentDownload;
        private double _bedrockDlProgress;
        private long _bedrockDlBytes;
        private long _bedrockDlTotal;
        /// <summary>活跃的版本下载 CancellationTokenSource，按版本ID索引</summary>
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeInstalls = new();

#if WINDOWS
        public WebBridge(MainWindow window)
        {
            _window = window;
            // 注册 AI UI 操控回调
            Core.AIAgentTools.ExecuteUIAsync = async (script) =>
            {
                var task = await window.Dispatcher.InvokeAsync(async () =>
                {
                    if (window.WebView?.CoreWebView2 != null)
                        await window.WebView.CoreWebView2.ExecuteScriptAsync(script);
                });
                await task;
            };
        }
#endif

#if !WINDOWS
        public WebBridge(Photino.NET.PhotinoWindow window, LauncherCore launcherCore,
            BedrockEnhancedDownloadService bedrockSvc, BedrockOfflineLauncher bedrockLauncher,
            string minecraftPath)
        {
            _window = window;
            _launcherCore = launcherCore;
            _bedrockSvc = bedrockSvc;
            _bedrockLauncher = bedrockLauncher;
            _minecraftPath = minecraftPath;
        }
#endif

        private BedrockEnhancedDownloadService BedrockSvc =>
            _bedrockSvcInstance ?? (_bedrockSvcInstance = new BedrockEnhancedDownloadService(AppContext.MinecraftPath ?? "."));

        private BedrockOfflineLauncher BedrockLauncher =>
            _bedrockLauncherInstance ?? (_bedrockLauncherInstance = new BedrockOfflineLauncher(AppContext.MinecraftPath ?? "."));

        // ===== 窗口控制 =====

#if WINDOWS
        public void Minimize() => _window.Dispatcher.Invoke(() => _window.WindowState = WindowState.Minimized);

        public void Maximize()
        {
            _window.Dispatcher.Invoke(() =>
            {
                _window.WindowState = _window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            });
        }

        public void Close() => _window.Dispatcher.Invoke(() => _window.Close());
#else
        public void Minimize() => _window.SetMinimized(true);
        public void Maximize() => _window.SetMaximized(!_window.Maximized);
        public void Close() => _window.Close();
#endif

        // ===== 账户管理 =====

        public object GetProfile()
        {
            string qnId = !string.IsNullOrEmpty(AppContext.QingniaoId) ? AppContext.QingniaoId
                : (PCL.Account.Settings.Get<string>("QingniaoId")
                   ?? SettingsManager.Settings.QingniaoId
                   ?? "");
            if (string.IsNullOrEmpty(qnId) || qnId == "0000000000") qnId = "";

            string qnName = !string.IsNullOrEmpty(AppContext.QingniaoName) ? AppContext.QingniaoName
                : (PCL.Account.Settings.Get<string>("QingniaoName")
                   ?? SettingsManager.Settings.QingniaoName
                   ?? "");

            string qnAvatar = PCL.Account.Settings.Get<string>("QingniaoAvatar")
                ?? SettingsManager.Settings.QingniaoAvatar
                ?? "";

            bool hasQingNiao = !string.IsNullOrEmpty(qnId) && !string.IsNullOrEmpty(qnName);

            return new
            {
                username = hasQingNiao ? qnName : (AppContext.Username ?? "Player"),
                isOffline = hasQingNiao ? false : AppContext.IsOfflineMode,
                isQingniao = hasQingNiao,
                isQingniaoLoggedIn = hasQingNiao,
                qingniaoName = qnName,
                qingniaoId = qnId,
                qingniaoAvatar = qnAvatar,
                avatar = qnAvatar,
                loginMode = hasQingNiao ? "青鸟模式" : (AppContext.IsOfflineMode ? "离线模式" : "正版模式")
            };
        }

        public async Task<string> Login(string type)
        {
            if (type?.ToLower() == "microsoft")
            {
                try
                {
                    _window.Dispatcher.Invoke(() => _window.TriggerMsLogin());
                    return JsonSerializer.Serialize(new { success = true, message = "正在启动微软认证..." });
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { success = false, error = ex.Message });
                }
            }
            // 青鸟登录由前端表单 + QingNiaoAuthJson 完成
            return JsonSerializer.Serialize(new { success = true, message = "show_form" });
        }

        /// <summary>前端提交微软 OAuth 回调 URL（提取授权码并继续验证流程）</summary>
        public string SubmitMicrosoftAuthCodeJson(string codeUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(codeUrl))
                    return JsonSerializer.Serialize(new { success = false, error = "请输入浏览器地址栏中的完整链接" });

                bool ok = PCL.Auth.Microsoft.MinecraftMsAuth.SubmitAuthCode(codeUrl.Trim());
                if (ok)
                    return JsonSerializer.Serialize(new { success = true, message = "正在验证登录..." });
                else
                    return JsonSerializer.Serialize(new { success = false, error = "未能从链接中提取授权码，请确保复制了完整的地址栏链接" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>青鸟账号认证（邮箱+密码登录，无 WPF 弹窗）</summary>
        public async Task<string> QingNiaoAuthJson(string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return JsonSerializer.Serialize(new { success = false, error = "请输入邮箱" });
                if (string.IsNullOrWhiteSpace(password))
                    return JsonSerializer.Serialize(new { success = false, error = "请输入密码" });

                string serverUrl = PCL.Account.AccountManager.GetQingNiaoServer();
                if (string.IsNullOrEmpty(serverUrl))
                    serverUrl = PCL.Account.Settings.Get<string>("CacheQingNiaoServer");
                if (string.IsNullOrEmpty(serverUrl))
                    serverUrl = "85.137.246.87";
                serverUrl = serverUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');

                string clientToken = PCL.Account.Settings.Get<string>("CacheQingNiaoClient")
                    ?? Guid.NewGuid().ToString("N");

                string authUrl = $"http://{serverUrl}/api/auth.php?action=login";
                var payload = new JObject
                {
                    ["email"] = email.Trim(),
                    ["password"] = password,
                    ["client_id"] = clientToken
                };

                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                {
                    var content = new StringContent(
                        payload.ToString(Newtonsoft.Json.Formatting.None),
                        Encoding.UTF8, "application/json");

                    HttpResponseMessage response;
                    try { response = await client.PostAsync(authUrl, content); }
                    catch (HttpRequestException) { return JsonSerializer.Serialize(new { success = false, error = "无法连接到认证服务器" }); }
                    catch (TaskCanceledException) { return JsonSerializer.Serialize(new { success = false, error = "连接超时" }); }

                    string body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        string err;
                        try { err = JObject.Parse(body)["error"]?.ToString() ?? $"HTTP {(int)response.StatusCode}"; }
                        catch { err = $"服务器返回错误 ({(int)response.StatusCode})"; }
                        return JsonSerializer.Serialize(new { success = false, error = err });
                    }

                    var json = JObject.Parse(body);
                    string accessToken = json["token"]?.ToString();
                    var user = json["user"];

                    if (string.IsNullOrEmpty(accessToken) || user == null)
                        return JsonSerializer.Serialize(new { success = false, error = "服务器返回数据不完整" });

                    string userName = user["nickname"]?.ToString() ?? email.Trim();
                    string uuid = user["qingniao_id"]?.ToString() ?? email.Trim();

                    // 保存登录数据（与 WPF 登录逻辑完全一致）
                    PCL.Account.AccountManager.SaveServerLogin("QingNiao",
                        new PCL.Account.McLoginResult
                        {
                            Name = userName,
                            Uuid = uuid,
                            AccessToken = accessToken,
                            ClientToken = clientToken,
                            Type = "QingNiao"
                        }, email.Trim(), password);

                    PCL.Account.AccountManager.SaveServerLoginRecord("QingNiao", email.Trim(), password);
                    PCL.Account.Settings.Set("CacheQingNiaoServer", serverUrl);
                    PCL.Account.Settings.Set("CacheQingNiaoEmail", email.Trim());
                    PCL.Account.Settings.Set("CacheQingNiaoPass", Core.SecureStorage.Protect(password));
                    PCL.Account.Settings.Set("CacheQingNiaoClient", clientToken);
                    PCL.Account.Settings.Set("QingniaoId", uuid);
                    PCL.Account.Settings.Set("QingniaoName", userName);

                    // 同时也保存到 MNL 主配置 (SettingsManager)
                    SettingsManager.Settings.QingniaoId = uuid;
                    SettingsManager.Settings.QingniaoName = userName;
                    SettingsManager.Settings.QingniaoEmail = email.Trim();
                    SettingsManager.Settings.QingniaoPass = password;
                    SettingsManager.Settings.QingniaoServer = serverUrl;
                    SettingsManager.Settings.QingniaoClientToken = clientToken;
                    SettingsManager.SaveSettings();

                    // 更新 AppContext
                    AppContext.QingniaoId = uuid;
                    AppContext.QingniaoName = userName;
                    AppContext.SetQingniaoName(userName);

                    PostMessage("toast", new { message = "青鸟账号 " + userName + " 登录成功", type = "success" });

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        userName = userName,
                        uuid = uuid
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        public void Logout() => _window.TriggerLogout();

        // ===== 设置 =====

        public string GetSetting(string key)
        {
            try
            {
                if (key == "currentJava")
                {
                    var path = SettingsManager.Settings.JavaPath ?? "";
                    return JsonSerializer.Serialize(new { path, version = "" });
                }
                var prop = typeof(LauncherSettings).GetProperty(key);
                if (prop == null)
                {
                    prop = typeof(LauncherSettings).GetProperty(key,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                }
                if (prop != null)
                    return prop.GetValue(SettingsManager.Settings)?.ToString() ?? "";
                return "";
            }
            catch { return ""; }
        }

        /// <summary>保存 Java 路径到 config.ini</summary>
        public void SaveJavaPath(string path)
        {
            try
            {
                Logger.Info($"[WebBridge] SaveJavaPath: {path}");
                var config = AppContext.Config;
                config.Load();  // 先加载保留其他字段
                config.JavaPath = path;
                config.Save();
            }
            catch (Exception ex) { Logger.Error("[WebBridge] SaveJavaPath 异常: " + ex.Message); }
        }

        /// <summary>读取已保存的 Java 路径</summary>
        public string GetSavedJavaPath()
        {
            try
            {
                var config = AppContext.Config;
                config.Load();
                string p = config.JavaPath ?? "";
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
            }
            catch { }
            return "";
        }

        /// <summary>检查 Java 兼容性，返回 JSON 给前端弹窗使用（参考 PCL ModJava.vb）</summary>
        public async Task<string> CheckJavaCompatibility(string versionId)
        {
            try
            {
                var core = _window.GetLauncherCore();
                var config = AppContext.Config;
                config.Load();

                string minecraftPath = config.GetMinecraftPath();
                string versionsPath = Path.Combine(minecraftPath, "versions", versionId);

                // 获取当前设置的 Java 路径
                string currentJavaPath = config.JavaPath;
                if (string.IsNullOrEmpty(currentJavaPath) || !File.Exists(currentJavaPath))
                {
                    // 自动检测最佳 Java
                    currentJavaPath = core.AutoDetectBestJava();
                }

                int currentJavaMajor = 0;
                if (!string.IsNullOrEmpty(currentJavaPath) && File.Exists(currentJavaPath))
                    currentJavaMajor = core.GetJavaMajorVersion(currentJavaPath);

                // 加载合并后的版本 JSON 以检测模组加载器
                int requiredMin = 8;
                int? requiredMax = null;
                string reason = "无法确定版本需求";

                try
                {
                    using (var mergedDoc = core.LoadMergedVersionJson(versionId, versionsPath, null))
                    {
                        if (mergedDoc != null)
                        {
                            var (min, max, rsn) = core.GetRequiredJavaVersion(versionId, mergedDoc);
                            requiredMin = min;
                            requiredMax = max;
                            reason = rsn;
                        }
                    }
                }
                catch { /* 版本 JSON 不存在或损坏，使用默认值 */ }

                // 检查是否兼容
                bool compatible = currentJavaMajor > 0 &&
                    currentJavaMajor >= requiredMin &&
                    (!requiredMax.HasValue || currentJavaMajor <= requiredMax.Value);

                // 扫描所有系统 Java
                var allJava = core.ScanAllSystemJava();

                // 找推荐版本（在区间内的最高版本）
                string recommendedPath = null;
                int recommendedMajor = 0;
                foreach (var (path, ver) in allJava)
                {
                    if (ver >= requiredMin && (!requiredMax.HasValue || ver <= requiredMax.Value))
                    {
                        recommendedPath = path;
                        recommendedMajor = ver;
                        break; // 已按版本降序，取第一个
                    }
                }

                // 构建可选版本列表（所有系统 Java，标注兼容性）
                var alternatives = allJava.Select(j => new
                {
                    path = j.path,
                    majorVersion = j.majorVersion,
                    compatible = j.majorVersion >= requiredMin &&
                        (!requiredMax.HasValue || j.majorVersion <= requiredMax.Value),
                    isCurrent = j.path.Equals(currentJavaPath, StringComparison.OrdinalIgnoreCase)
                }).ToList();

                var result = new
                {
                    compatible,
                    currentJavaPath,
                    currentJavaMajor,
                    requiredMin,
                    requiredMax = requiredMax ?? 999,
                    reason,
                    recommendedPath = compatible ? null : recommendedPath,
                    recommendedMajor = compatible ? 0 : recommendedMajor,
                    alternatives,
                    hasAlternatives = !compatible && recommendedPath != null
                };

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                Logger.Info($"[WebBridge] CheckJavaCompatibility: {versionId} → compatible={compatible}, " +
                    $"current={currentJavaMajor}, required={requiredMin}~{requiredMax}, " +
                    $"hasAlternatives={result.hasAlternatives}");
                return json;
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBridge] CheckJavaCompatibility 异常: " + ex.ToString());
                return "{\"compatible\":true,\"error\":\"" + (ex.Message ?? "").Replace("\"", "\\\"") + "\"}";
            }
        }

        public void SetSetting(string key, string value)
        {
            try
            {
                if (key == "currentJava" && !string.IsNullOrEmpty(value))
                {
                    var obj = JObject.Parse(value);
                    string path = obj["path"]?.ToString() ?? "";
                    Logger.Info($"[WebBridge] SetSetting currentJava: {path}");
                    SettingsManager.Settings.JavaPath = path;
                    SettingsManager.SaveSettings();
                    return;
                }
                // 全景设置 —— 显式处理，避免 WebView2 JS boolean → C# string 类型转换不确定性
                if (key == "panoramaMouseFollow" || key == "PanoramaMouseFollow")
                {
                    bool b = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
                    SettingsManager.Settings.PanoramaMouseFollow = b;
                    SettingsManager.SaveSettings();
                    return;
                }
                if (key == "panoramaSpeed" || key == "PanoramaSpeed")
                {
                    if (int.TryParse(value, out int speed))
                        SettingsManager.Settings.PanoramaSpeed = Math.Clamp(speed, 0, 20);
                    SettingsManager.SaveSettings();
                    return;
                }
                if (key == "panoramaTheme" || key == "PanoramaTheme")
                {
                    SettingsManager.Settings.PanoramaTheme = value ?? "overworld";
                    SettingsManager.SaveSettings();
                    return;
                }
                var prop = typeof(LauncherSettings).GetProperty(key);
                // 回退：大小写不敏感匹配（处理前端驼峰命名与后端 PascalCase 的不一致）
                if (prop == null)
                {
                    prop = typeof(LauncherSettings).GetProperty(key,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                }
                if (prop != null && prop.CanWrite)
                    prop.SetValue(SettingsManager.Settings, Convert.ChangeType(value, prop.PropertyType));
                SettingsManager.SaveSettings();
            }
            catch (Exception ex) { Logger.Error("[WebBridge] SetSetting 异常: " + ex.Message); }
        }

        public string GetAllSettingsJson()
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };
                return System.Text.Json.JsonSerializer.Serialize(SettingsManager.Settings, options);
            }
            catch { return "{}"; }
        }

        public object GetInstalledVersions()
        {
            try
            {
                var versionsDir = System.IO.Path.Combine(
                    AppContext.MinecraftPath ?? "", "versions");
                if (!System.IO.Directory.Exists(versionsDir))
                    return Array.Empty<object>();
                return System.IO.Directory.GetDirectories(versionsDir)
                    .Select(dir =>
                    {
                        var name = System.IO.Path.GetFileName(dir);
                        var jsonFile = System.IO.Path.Combine(dir, name + ".json");
                        var date = System.IO.File.Exists(jsonFile)
                            ? System.IO.File.GetLastWriteTime(jsonFile).ToString("yyyy-MM-dd")
                            : "";
                        return new { id = name, type = "release", date, typeColor = "#2E7D32" };
                    })
                    .ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        public object GetInstalledBedrockVersions()
        {
            try
            {
                var bedrockDir = System.IO.Path.Combine(
                    AppContext.MinecraftPath ?? "", "bedrock");
                if (!System.IO.Directory.Exists(bedrockDir))
                    return Array.Empty<object>();
                return System.IO.Directory.GetDirectories(bedrockDir)
                    .Where(d => System.IO.File.Exists(System.IO.Path.Combine(d, "Minecraft.Windows.exe")))
                    .Select(dir =>
                    {
                        var name = System.IO.Path.GetFileName(dir);
                        var date = System.IO.Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd");
                        return new { id = name, type = "bedrock", date, typeColor = "#8D6E63" };
                    })
                    .ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        public async Task<string> LaunchGame(string versionId, string optionsJson)
        {
            try
            {
                var core = _window.GetLauncherCore();
                var config = AppContext.Config;

                // 1. 解析前端参数
                int memoryMB = 4096;
                string accountType = "";
                string accountName = "";
                string resolution = "1920x1080";
                bool fullscreen = false;
                string versionType = "";
                try
                {
                    var opts = JObject.Parse(optionsJson ?? "{}");
                    memoryMB = opts["ram"]?.Value<int>() ?? 4096;
                    accountType = opts["accountType"]?.ToString() ?? "";
                    accountName = opts["accountName"]?.ToString() ?? "";
                    resolution = opts["resolution"]?.ToString() ?? "1920x1080";
                    fullscreen = opts["fullscreen"]?.Value<bool>() ?? false;
                    versionType = opts["versionType"]?.ToString() ?? "";
                }
                catch { }

                // 1b. 基岩版分支：使用 BedrockOfflineLauncher
                if (versionType == "bedrock")
                {
                    string bedrockUsername = !string.IsNullOrEmpty(accountName) ? accountName : "Player";
                    PostMessage("launch-status", new
                    {
                        status = "preparing",
                        version = versionId,
                        message = $"正在启动基岩版 {versionId}（{bedrockUsername}，离线模式）..."
                    });
                    try
                    {
                        var launcher = BedrockLauncher;
                        launcher.LaunchLogReceived += (log) =>
                            PostMessage("launch-status", new { status = "log", message = "[Bedrock] " + log });
                        bool result = await Task.Run(() => launcher.LaunchOfflineAsync(versionId, bedrockUsername));
                        if (result)
                        {
                            PostMessage("launch-status", new { status = "running", version = versionId, message = "基岩版已启动" });
                            PostMessage("toast", new { message = $"基岩版 {versionId} 已启动", type = "success" });
                            return "launched";
                        }
                        else
                        {
                            PostMessage("launch-status", new { status = "error", message = "基岩版启动失败，请检查版本是否已安装" });
                            return "launch_failed";
                        }
                    }
                    catch (Exception ex)
                    {
                        string errMsg = ex.InnerException?.Message ?? ex.Message;
                        PostMessage("launch-status", new { status = "error", message = errMsg });
                        return "exception:" + errMsg;
                    }
                }

                // 2. 确定用户名和离线模式
                string username = !string.IsNullOrEmpty(accountName) ? accountName : (AppContext.Username ?? "Player");
                bool offlineMode = true;

                if (accountType == "microsoft")
                {
                    var msAccounts = PCL.Account.AccountManager.GetMsAccounts();
                    var msAccount = msAccounts.Count > 0 ? msAccounts[0] : null;
                    // 优先使用前端传来的特定账号名
                    if (!string.IsNullOrEmpty(accountName))
                        msAccount = msAccounts.FirstOrDefault(a => a.UserName == accountName) ?? msAccounts.FirstOrDefault();
                    if (msAccount != null)
                    {
                        username = msAccount.UserName;
                        offlineMode = false;
                    }
                }
                else if (accountType == "offline")
                {
                    // 离线账号：直接使用前端传来的账号名
                    offlineMode = true;
                }
                else if (accountType == "authlib")
                {
                    // 外置登录：使用前端传来的账号名
                    offlineMode = false;
                }

                // 3. 获取 Java 路径（config.ini > 自动检测）
                config.Load();
                string javaPath = config.JavaPath;
                string javaSource = "config.ini";
                if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
                {
                    var javaList = core.DetectJavaVersions();
                    if (javaList != null && javaList.Count > 0)
                    {
                        javaPath = javaList[0].Path;
                        javaSource = "自动检测";
                    }
                }
                Logger.Info($"[WebBridge] Java 路径来源: {javaSource}, 路径: {javaPath}");
                PostMessage("console-log", new { message = $"Java: {javaSource} → {javaPath}" });

                if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
                {
                    PostMessage("launch-status", new { status = "error", message = "未找到 Java，请在设置中配置 Java 路径" });
                    return "java_not_found";
                }

                // 4. 发送准备状态
                PostMessage("launch-status", new
                {
                    status = "preparing",
                    version = versionId,
                    message = $"正在启动 {versionId}（{username}，{memoryMB}M）..."
                });

                // 5. 在后台线程运行启动逻辑
                bool launchResult = false;
                await Task.Run(() =>
                {
                    launchResult = core.LaunchGame(javaPath, versionId, memoryMB, username, offlineMode,
                        (logMsg) =>
                        {
                            _window.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                // 检测崩溃标记（参考 PCL 崩溃通知机制）
                                if (logMsg != null && logMsg.StartsWith("__MNL_CRASH__"))
                                {
                                    var parts = logMsg.Substring(14).Split(new[] { "__" }, 2, StringSplitOptions.None);
                                    string exitCode = parts.Length > 0 ? parts[0] : "?";
                                    string reason = parts.Length > 1 ? parts[1] : "";
                                    PostMessage("launch-status", new
                                    {
                                        status = "crashed",
                                        exitCode = exitCode,
                                        reason = reason,
                                        message = $"游戏异常退出 (退出码: {exitCode})"
                                    });
                                    return;
                                }
                                PostMessage("launch-status", new { status = "log", message = logMsg });
                            }));
                        },
                        resolution,
                        fullscreen);
                });

                if (launchResult)
                {
                    // 持久化启动设置
                    try
                    {
                        SettingsManager.Settings.Resolution = resolution;
                        SettingsManager.Settings.Fullscreen = fullscreen;
                        SettingsManager.Settings.MaxMemory = memoryMB;
                        SettingsManager.SaveSettings();
                    }
                    catch { }

                    PostMessage("launch-status", new { status = "running", version = versionId, message = "游戏已启动" });
                    PostMessage("toast", new { message = $"游戏 {versionId} 已启动", type = "success" });
                    return "launched";
                }
                else
                {
                    PostMessage("launch-status", new { status = "error", message = "游戏进程未能启动" });
                    return "launch_failed";
                }
            }
            catch (Exception ex)
            {
                string errMsg = ex.InnerException?.Message ?? ex.Message;
                PostMessage("launch-status", new { status = "error", message = errMsg });
                PostMessage("toast", new { message = "启动失败: " + errMsg, type = "error" });
                Logger.Error("[WebBridge] LaunchGame 异常: " + ex.ToString());
                return "exception:" + errMsg;
            }
        }

        // ===== 工具 =====

        public string GetAppVersion() => "1.2.0";

        // ===== 自定义下载 =====

        /// <summary>获取自定义下载保存目录</summary>
        public string GetCustomDownloadPathJson()
        {
            try
            {
                var path = Core.SettingsManager.Settings.CustomDownloadPath;
                if (string.IsNullOrWhiteSpace(path))
                    path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                return JsonSerializer.Serialize(new { path, exists = System.IO.Directory.Exists(path) });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { path = "", error = ex.Message });
            }
        }

        /// <summary>设置自定义下载保存目录</summary>
        public string SetCustomDownloadPathJson(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && !System.IO.Directory.Exists(path))
                    System.IO.Directory.CreateDirectory(path);
                Core.SettingsManager.Settings.CustomDownloadPath = path;
                Core.SettingsManager.SaveSettings();
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>开始自定义下载（异步，立即返回）</summary>
        public string StartCustomDownloadJson(string url, string saveDir)
        {
            try
            {
                var svc = Core.CustomDownloadService.Instance;
                _ = Task.Run(async () =>
                {
                    await svc.StartDownloadAsync(url, saveDir);
                });
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>获取自定义下载进度快照（前端轮询）</summary>
        public string GetCustomDownloadProgressJson()
        {
            try
            {
                var progress = Core.CustomDownloadService.Instance.GetProgress();
                return JsonSerializer.Serialize(progress, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        /// <summary>取消自定义下载</summary>
        public string CancelCustomDownloadJson()
        {
            try
            {
                Core.CustomDownloadService.Instance.Cancel();
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>打开下载完成的文件</summary>
        public string CustomDlOpenFileJson(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                    return "{\"success\":true}";
                }
                return JsonSerializer.Serialize(new { success = false, error = "文件不存在" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>打开下载文件所在的文件夹</summary>
        public string CustomDlOpenFolderJson(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    var dir = System.IO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{filePath}\"",
                            UseShellExecute = false
                        });
                        return "{\"success\":true}";
                    }
                }
                return JsonSerializer.Serialize(new { success = false, error = "文件夹不存在" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>微软登录取消处理器（由 MainWindow 注入）</summary>
        public Action MsAuthCancelHandler { get; set; }

        /// <summary>前端调用：取消正在进行的微软登录</summary>
        public void CancelMicrosoftAuth()
        {
            try { MsAuthCancelHandler?.Invoke(); } catch { }
        }

        /// <summary>在默认浏览器中打开 URL</summary>
        public void OpenUrl(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                try { System.Diagnostics.Process.Start(url); } catch { }
            }
        }

        // ===== 内存信息 =====

        public string GetMemoryInfo()
        {
            try
            {
                return Core.MemoryOptimizer.GetMemoryInfo();
            }
            catch { return "{\"usedGB\":\"0\",\"gameGB\":\"-\",\"freeGB\":\"0\",\"totalGB\":\"0\",\"usedPercent\":0,\"gamePercent\":0,\"freePercent\":0}"; }
        }

        // ===== 设置（补充） =====

        public string GetAllSettings()
        {
            return GetAllSettingsJson();
        }

        // ===== 版本管理（补充） =====

        public async Task<object> GetAvailableVersions(string type)
        {
            try
            {
                var minecraftPath = AppContext.MinecraftPath ?? ".";
                var service = new Core.VersionDownloadService(minecraftPath);
                var versions = await service.GetRemoteVersionsAsync();
                return versions.Select(v => new
                {
                    id = v.Id,
                    type = v.Type,
                    date = v.ReleaseTime.ToString("yyyy-MM-dd"),
                    url = v.Url,
                    typeColor = v.Type switch
                    {
                        "release" => "#2E7D32",
                        "snapshot" => "#FF6F00",
                        "old_alpha" => "#5D4037",
                        "old_beta" => "#4527A0",
                        _ => "#546E7A"
                    }
                }).ToArray();
            }
            catch { return "[]"; }
        }

        // JSON 版本：直连 BMCLAPI 镜像（10s 超时），失败快速回退 Mojang（10s 超时）
        public async Task<string> GetAvailableVersionsJson(string type)
        {
            // 缓存命中且未过期，直接返回
            if (_cachedVersionsJson != null && (DateTime.Now - _cachedVersionsTime) < _versionsCacheTtl)
                return _cachedVersionsJson;

            string[] manifestUrls = new[]
            {
                "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json",
                "https://launchermeta.mojang.com/mc/game/version_manifest.json"
            };

            string json = null;
            foreach (var url in manifestUrls)
            {
                try
                {
                    using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                    {
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("MNL/1.0");
                        json = await client.GetStringAsync(url);
                        if (!string.IsNullOrEmpty(json))
                        {
                            Logger.Info($"[版本列表] 从 {url} 获取成功");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[版本列表] {url} 失败: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(json))
                return _cachedVersionsJson ?? "[]";

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var versions = doc.RootElement.GetProperty("versions");
                var list = new System.Collections.Generic.List<object>();
                foreach (var v in versions.EnumerateArray())
                {
                    string vid = v.GetProperty("id").GetString();
                    string vtype = v.TryGetProperty("type", out var tp) ? tp.GetString() : "unknown";
                    string vurl = v.TryGetProperty("url", out var u) ? u.GetString() : "";
                    string vdate = "";
                    if (v.TryGetProperty("releaseTime", out var rt))
                        vdate = rt.GetString();
                    else if (v.TryGetProperty("time", out var t))
                        vdate = t.GetString();

                    list.Add(new
                    {
                        id = vid,
                        type = vtype,
                        date = vdate?.Length >= 10 ? vdate.Substring(0, 10) : (vdate ?? ""),
                        url = vurl,
                        typeColor = vtype switch
                        {
                            "release" => "#2E7D32",
                            "snapshot" => "#FF6F00",
                            "old_alpha" => "#5D4037",
                            "old_beta" => "#4527A0",
                            _ => "#546E7A"
                        }
                    });
                }

                var result = System.Text.Json.JsonSerializer.Serialize(list);
                _cachedVersionsJson = result;
                _cachedVersionsTime = DateTime.Now;
                Logger.Info($"[版本列表] 缓存已更新，共 {list.Count} 个版本");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("[版本列表] JSON 解析失败: " + ex.Message);
                return _cachedVersionsJson ?? "[]";
            }
        }

        public string GetInstalledVersionsJson()
        {
            try
            {
                var versionsDir = System.IO.Path.Combine(
                    AppContext.MinecraftPath ?? "", "versions");
                if (!System.IO.Directory.Exists(versionsDir))
                    return "[]";
                var list = System.IO.Directory.GetDirectories(versionsDir)
                    .Select(dir =>
                    {
                        var name = System.IO.Path.GetFileName(dir);
                        var jsonFile = System.IO.Path.Combine(dir, name + ".json");
                        var date = System.IO.File.Exists(jsonFile)
                            ? System.IO.File.GetLastWriteTime(jsonFile).ToString("yyyy-MM-dd")
                            : "";
                        var (type, typeColor) = DetectGameType(jsonFile);
                        return new { id = name, type, date, typeColor };
                    })
                    .ToList();
                return JsonSerializer.Serialize(list);
            }
            catch { return "[]"; }
        }

        /// <summary>
        /// 检测游戏版本类型（release / snapshot / old_alpha / old_beta），而非加载器类型
        /// </summary>
        private static (string type, string color) DetectGameType(string jsonFile)
        {
            try
            {
                if (!System.IO.File.Exists(jsonFile))
                    return ("正式版", "#2E7D32");

                string json = System.IO.File.ReadAllText(jsonFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 读取 inheritsFrom，递归查找基础版本
                string inheritsFrom = "";
                if (root.TryGetProperty("inheritsFrom", out var inf))
                    inheritsFrom = inf.GetString() ?? "";

                // 如果有继承，去基础版本的 JSON 中读取 type
                if (!string.IsNullOrEmpty(inheritsFrom))
                {
                    var baseDir = System.IO.Path.GetDirectoryName(jsonFile);
                    var baseJsonFile = System.IO.Path.Combine(baseDir ?? "", inheritsFrom, inheritsFrom + ".json");
                    if (!System.IO.File.Exists(baseJsonFile))
                    {
                        // 可能在上级目录
                        var versionsDir = System.IO.Path.Combine(
                            AppContext.MinecraftPath ?? "", "versions");
                        baseJsonFile = System.IO.Path.Combine(versionsDir, inheritsFrom, inheritsFrom + ".json");
                    }
                    if (System.IO.File.Exists(baseJsonFile))
                    {
                        return DetectGameType(baseJsonFile);
                    }
                    // 找不到基础版本，用 id 猜测
                    return GuessGameType(inheritsFrom);
                }

                // 直接读取 type 字段（由 Mojang 写入）
                if (root.TryGetProperty("type", out var t))
                {
                    var rawType = t.GetString() ?? "";
                    return rawType switch
                    {
                        "release" => ("正式版", "#2E7D32"),
                        "snapshot" => ("预览版", "#F39C12"),
                        "old_alpha" => ("远古版", "#7F8C8D"),
                        "old_beta" => ("远古版(beta)", "#4527A0"),
                        _ => (rawType, "#546E7A")
                    };
                }

                // 没有 type 字段，根据版本 id 猜测
                return GuessGameType(System.IO.Path.GetFileNameWithoutExtension(jsonFile) ?? "");
            }
            catch
            {
                return ("正式版", "#2E7D32");
            }
        }

        /// <summary>
        /// 根据版本 id 猜测游戏类型
        /// </summary>
        private static (string type, string color) GuessGameType(string versionId)
        {
            if (string.IsNullOrEmpty(versionId))
                return ("正式版", "#2E7D32");

            var lower = versionId.ToLowerInvariant();

            // 快照版本：数字+w+周数 如 24w14a
            if (System.Text.RegularExpressions.Regex.IsMatch(lower, @"^\d{2}w\d{2}[a-z]$"))
                return ("预览版", "#F39C12");

            // 远古版本
            if (lower.StartsWith("a") || lower.StartsWith("c") || lower.StartsWith("inf"))
                return ("远古版", "#7F8C8D");
            if (lower.StartsWith("b") || lower.StartsWith("beta"))
                return ("远古版(beta)", "#4527A0");
            if (lower.StartsWith("rd-"))
                return ("远古版", "#7F8C8D");

            // 默认正式版
            return ("正式版", "#2E7D32");
        }

        public string GetInstalledBedrockVersionsJson()
        {
            try
            {
                var bedrockDir = System.IO.Path.Combine(
                    AppContext.MinecraftPath ?? "", "bedrock");
                if (!System.IO.Directory.Exists(bedrockDir))
                    return "[]";
                var list = System.IO.Directory.GetDirectories(bedrockDir)
                    .Where(d => System.IO.File.Exists(System.IO.Path.Combine(d, "Minecraft.Windows.exe")))
                    .Select(dir =>
                    {
                        var name = System.IO.Path.GetFileName(dir);
                        var date = System.IO.Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd");
                        return new { id = name, type = "bedrock", date, typeColor = "#8D6E63" };
                    })
                    .ToList();
                return JsonSerializer.Serialize(list);
            }
            catch { return "[]"; }
        }

        // ===== 基岩版远程版本 & 安装 =====

        /// <summary>获取基岩版可下载版本列表（从缓存或远程拉取）</summary>
        public async Task<string> GetAvailableBedrockVersionsJson()
        {
            try
            {
                var cache = BedrockVersionCacheService.Instance;
                await cache.LoadAsync();
                var versions = cache.CachedVersions.ToList();
                var list = versions.Select(v => new
                {
                    id = v.Id,
                    version = v.Version,
                    type = v.Type,
                    typeDisplay = v.TypeDisplay,
                    releaseTime = v.ReleaseTime.ToString("yyyy-MM-dd"),
                    desc = v.Description,
                    installerUrl = v.InstallerUrl,
                    isDownloaded = v.IsDownloaded,
                    canDownload = v.CanDownload,
                    statusText = v.StatusText,
                    typeColor = v.Type == "release" ? "#2E7D32" : v.Type == "preview" ? "#FF6F00" : "#6A1B9A",
                    source = v.Source ?? "mcappx",
                    sourceDisplay = v.SourceDisplay
                }).ToList();
                return JsonSerializer.Serialize(list);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message, versions = new object[0] });
            }
        }

        /// <summary>安装基岩版版本（异步下载 msixbundle）</summary>
        public async Task<string> InstallBedrockVersionJson(string versionId)
        {
            try
            {
                var cache = BedrockVersionCacheService.Instance;
                await cache.LoadAsync();
                var version = cache.CachedVersions.FirstOrDefault(v => v.Id == versionId);
                if (version == null)
                    return JsonSerializer.Serialize(new { success = false, error = "版本不存在" });

                if (!version.CanDownload)
                    return JsonSerializer.Serialize(new { success = false, error = "该版本无可下载链接" });

                _bedrockInstallCts?.Cancel();
                _bedrockInstallCts = new CancellationTokenSource();
                _bedrockCurrentDownload = version;
                _bedrockDlProgress = 0;
                _bedrockDlBytes = 0;
                _bedrockDlTotal = 0;

                var progress = new DownloadProgress();
                progress.ProgressChanged += (p) =>
                {
                    _bedrockDlProgress = p.Progress;
                    _bedrockDlBytes = p.DownloadedBytes;
                    _bedrockDlTotal = p.TotalBytes;
                };

                var svc = BedrockSvc;
                svc.StatusChanged += (msg) => PostMessage("bedrock-dl-status", new { message = msg });
                svc.DownloadCompleted += (id) =>
                {
                    PostMessage("bedrock-dl-done", new { versionId = id });
                    PostMessage("toast", new { message = $"基岩版 {id} 下载完成", type = "success" });
                    PostMessage("mnl:versions-changed", new { });
                };
                svc.DownloadFailed += (id, err) =>
                {
                    PostMessage("bedrock-dl-error", new { versionId = id, error = err });
                    PostMessage("toast", new { message = $"基岩版 {id} 下载失败: {err}", type = "error" });
                };

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await svc.StartDownloadAsync(version, progress, _bedrockInstallCts.Token);
                    }
                    catch { }
                });

                return JsonSerializer.Serialize(new { success = true, versionId = version.Id });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>获取基岩版下载进度</summary>
        public string GetBedrockDownloadProgressJson()
        {
            try
            {
                return JsonSerializer.Serialize(new
                {
                    versionId = _bedrockCurrentDownload?.Id ?? "",
                    progress = _bedrockDlProgress,
                    downloadedBytes = _bedrockDlBytes,
                    totalBytes = _bedrockDlTotal,
                    isDownloading = _bedrockInstallCts != null && !_bedrockInstallCts.IsCancellationRequested
                });
            }
            catch { return "{}"; }
        }

        /// <summary>取消基岩版下载</summary>
        public string CancelBedrockDownloadJson()
        {
            try
            {
                _bedrockInstallCts?.Cancel();
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        public async Task<string> InstallVersion(string versionId, string loader, string directoryName = null, string versionUrl = null)
        {
            var minecraftPath = AppContext.MinecraftPath ?? ".";
            // 使用自定义目录名（如果有），否则使用原始版本ID
            var finalDirName = !string.IsNullOrEmpty(directoryName) ? directoryName : versionId;

            // 创建下载任务并注册到下载任务中心
            var task = new GenericDownloadTaskViewModel(
                "version://" + finalDirName,
                System.IO.Path.Combine(minecraftPath, "versions"),
                finalDirName);
            task.Status = "准备中";
            Core.DownloadTaskManager.Instance.AddTask(task);

            // 立即通知前端任务已创建，让下载任务页立刻显示
            PostMessage("version-download-progress", new { versionId = finalDirName, progress = 0, type = "vanilla", status = "准备中" });

            var cts = new CancellationTokenSource();
            // 注册为活跃安装任务（允许取消）
            _activeInstalls[finalDirName] = cts;

            try
            {
                var versionsDir = System.IO.Path.Combine(minecraftPath, "versions");
                System.IO.Directory.CreateDirectory(versionsDir);

                // 1. 下载游戏版本（原版）
                var downloadService = new VersionDownloadService(minecraftPath);

                // 速度计算
                var lastProgress = 0;
                var lastProgressTime = DateTime.UtcNow;

                // 订阅状态和进度事件 → 更新任务
                var currentStatus = "准备中";
                downloadService.StatusChanged += (s) =>
                {
                    task.Status = s;
                    task.CurrentFile = s;
                    currentStatus = s;
                    // 立即推送状态变化到前端（不等待 ProgressChanged）
                    PostMessage("version-download-progress", new
                    {
                        versionId = finalDirName,
                        progress = task.Progress,
                        type = "vanilla",
                        status = s,
                        speed = task.DownloadSpeed,
                        size = task.FileSize
                    });
                };
                downloadService.ProgressChanged += (p) =>
                {
                    task.Progress = p;
                    // 根据进度变化率估算速度
                    var now = DateTime.UtcNow;
                    var delta = (now - lastProgressTime).TotalSeconds;
                    if (delta > 0.3 && p > lastProgress)
                    {
                        var percentPerSec = (p - lastProgress) / delta;
                        if (percentPerSec > 0.001)
                        {
                            task.DownloadSpeed = $"{percentPerSec * 60:F1}%/min";
                        }
                        lastProgress = p;
                        lastProgressTime = now;
                    }
                    PostMessage("version-download-progress", new
                    {
                        versionId = finalDirName,
                        progress = p,
                        type = "vanilla",
                        status = currentStatus,
                        speed = task.DownloadSpeed,
                        size = task.FileSize
                    });
                };

                await downloadService.StartDownloadAsync(versionId, versionUrl, cts.Token);

                task.IsCompleted = true;
                task.Status = "已完成";
                task.Progress = 100;
                task.DownloadSpeed = "-";
                PostMessage("toast", new { message = $"版本 {finalDirName} 下载完成", type = "info" });

                // 2. 安装加载器（如果指定，支持逗号分隔多个）
                if (!string.IsNullOrEmpty(loader))
                {
                    var loaderList = loader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var l in loaderList)
                    {
                        PostMessage("toast", new { message = $"正在安装 {l}...", type = "info" });
                        task.Status = $"安装 {l}...";
                        bool loaderOk = await InstallLoaderAsync(minecraftPath, versionId, l, cts.Token);
                        if (!loaderOk)
                        {
                            task.IsFailed = true;
                            task.Status = $"{l} 安装失败";
                            task.ErrorMessage = $"{l} 安装失败";
                            PostMessage("toast", new { message = $"{l} 安装失败", type = "error" });
                            return JsonSerializer.Serialize(new { success = false, error = $"{l} 安装失败" });
                        }
                        PostMessage("toast", new { message = $"{l} 安装完成", type = "info" });
                    }
                }

                // 3. 如果指定了自定义目录名（且不等于原始版本ID），重命名版本目录
                if (!string.IsNullOrEmpty(directoryName) && directoryName != versionId)
                {
                    var srcDir = System.IO.Path.Combine(minecraftPath, "versions", versionId);
                    var dstDir = System.IO.Path.Combine(minecraftPath, "versions", directoryName);
                    if (System.IO.Directory.Exists(srcDir) && !System.IO.Directory.Exists(dstDir))
                    {
                        System.IO.Directory.Move(srcDir, dstDir);
                        // 更新版本 JSON 的 id 字段和文件名
                        var oldJson = System.IO.Path.Combine(dstDir, versionId + ".json");
                        var newJson = System.IO.Path.Combine(dstDir, directoryName + ".json");
                        if (System.IO.File.Exists(oldJson))
                        {
                            var jsonContent = System.IO.File.ReadAllText(oldJson);
                            var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);
                            jsonObj["id"] = directoryName;
                            System.IO.File.WriteAllText(newJson, jsonObj.ToString(Newtonsoft.Json.Formatting.Indented));
                            System.IO.File.Delete(oldJson);
                        }

                        // 4. 更新所有加载器版本 JSON 中的 inheritsFrom 和 jar 引用
                        //    重命名原版目录后，Fabric/Forge/NeoForge 的 inheritsFrom 仍指向旧 versionId，
                        //    必须更新为 directoryName 否则继承链断裂导致 ClassNotFoundException。
                        UpdateLoaderInheritsFrom(minecraftPath, versionId, directoryName);
                    }
                }

                PostMessage("version-download-complete", new { versionId = finalDirName });
                return "{\"success\":true}";
            }
            catch (OperationCanceledException)
            {
                task.IsCancelled = true;
                task.Status = "已取消";
                task.DownloadSpeed = "-";
                PostMessage("toast", new { message = $"下载已取消: {finalDirName}", type = "warning" });
                return JsonSerializer.Serialize(new { success = false, error = "下载已取消" });
            }
            catch (Exception ex)
            {
                Logger.Error($"[InstallVersion] 安装失败: {ex.Message}");
                task.IsFailed = true;
                task.Status = "失败";
                task.ErrorMessage = ex.Message;
                task.DownloadSpeed = "-";
                PostMessage("toast", new { message = $"安装失败: {ex.Message}", type = "error" });
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
            finally
            {
                _activeInstalls.TryRemove(finalDirName, out _);
            }
        }

        /// <summary>
        /// 重命名原版版本目录后，扫描所有加载器版本 JSON，
        /// 将 inheritsFrom 和 jar 字段中的旧 versionId 替换为 directoryName。
        /// </summary>
        private static void UpdateLoaderInheritsFrom(string minecraftPath, string oldVersionId, string newVersionId)
        {
            try
            {
                var versionsDir = System.IO.Path.Combine(minecraftPath, "versions");
                if (!System.IO.Directory.Exists(versionsDir)) return;

                foreach (var dir in System.IO.Directory.GetDirectories(versionsDir))
                {
                    var dirName = System.IO.Path.GetFileName(dir);
                    // 跳过刚重命名的目录本身
                    if (dirName == newVersionId) continue;
                    // 跳过原版目录（已被重命名，但防御性检查）
                    if (dirName == oldVersionId) continue;

                    var jsonPath = System.IO.Path.Combine(dir, dirName + ".json");
                    if (!System.IO.File.Exists(jsonPath)) continue;

                    try
                    {
                        var jsonContent = System.IO.File.ReadAllText(jsonPath);
                        var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);
                        bool modified = false;

                        // 更新 inheritsFrom
                        if (jsonObj["inheritsFrom"]?.ToString() == oldVersionId)
                        {
                            jsonObj["inheritsFrom"] = newVersionId;
                            modified = true;
                            Logger.Info($"[InstallVersion] 更新 {dirName} 的 inheritsFrom: {oldVersionId} → {newVersionId}");
                        }

                        // 更新 jar 字段（Forge 版本可能通过 jar 字段指定客户端 JAR 位置）
                        if (jsonObj["jar"]?.ToString() == oldVersionId)
                        {
                            jsonObj["jar"] = newVersionId;
                            modified = true;
                            Logger.Info($"[InstallVersion] 更新 {dirName} 的 jar: {oldVersionId} → {newVersionId}");
                        }

                        // 更新 arguments.jvm 中引用旧版本 ID 的路径
                        if (jsonObj["arguments"] is Newtonsoft.Json.Linq.JObject args &&
                            args["jvm"] is Newtonsoft.Json.Linq.JArray jvmArgs)
                        {
                            foreach (var arg in jvmArgs)
                            {
                                if (arg is Newtonsoft.Json.Linq.JObject argObj &&
                                    argObj["value"] is Newtonsoft.Json.Linq.JToken val)
                                {
                                    var valArray = val as Newtonsoft.Json.Linq.JArray;
                                    var valString = val as Newtonsoft.Json.Linq.JValue;
                                    if (valArray != null)
                                    {
                                        for (int i = 0; i < valArray.Count; i++)
                                        {
                                            if (valArray[i] is Newtonsoft.Json.Linq.JValue v &&
                                                v.Type == Newtonsoft.Json.Linq.JTokenType.String &&
                                                v.ToString().Contains(oldVersionId))
                                            {
                                                valArray[i] = v.ToString().Replace(oldVersionId, newVersionId);
                                                modified = true;
                                            }
                                        }
                                    }
                                    else if (valString != null && valString.Type == Newtonsoft.Json.Linq.JTokenType.String &&
                                             valString.ToString().Contains(oldVersionId))
                                    {
                                        argObj["value"] = valString.ToString().Replace(oldVersionId, newVersionId);
                                        modified = true;
                                    }
                                }
                                else if (arg is Newtonsoft.Json.Linq.JValue strArg &&
                                         strArg.Type == Newtonsoft.Json.Linq.JTokenType.String &&
                                         strArg.ToString().Contains(oldVersionId))
                                {
                                    // 直接字符串参数中的路径替换
                                    var newVal = new Newtonsoft.Json.Linq.JValue(
                                        strArg.ToString().Replace(oldVersionId, newVersionId));
                                    arg.Replace(newVal);
                                    modified = true;
                                }
                            }
                        }

                        if (modified)
                        {
                            System.IO.File.WriteAllText(jsonPath,
                                jsonObj.ToString(Newtonsoft.Json.Formatting.Indented));
                            Logger.Info($"[InstallVersion] 已更新 {dirName}/{dirName}.json 中的版本引用");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"[InstallVersion] 更新 {dirName} JSON 失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[InstallVersion] 扫描加载器继承引用失败: {ex.Message}");
            }
        }

        private async Task<bool> InstallLoaderAsync(string minecraftPath, string versionId, string loader, CancellationToken ct)
        {
            try
            {
                switch (loader)
                {
                    case "Forge":
                        var forgeInstaller = new ForgeInstaller(minecraftPath);
                        var forgeVersions = await forgeInstaller.GetForgeVersionsAsync(versionId, ct);
                        var latestForge = forgeVersions?.FirstOrDefault();
                        if (latestForge == null) throw new Exception($"未找到适用于 MC {versionId} 的 Forge 版本");
                        return await forgeInstaller.InstallForgeAsync(versionId, latestForge, ct);

                    case "Fabric":
                        var fabricInstaller = new FabricInstaller(minecraftPath);
                        var fabricVersion = await fabricInstaller.GetRecommendedVersionAsync(versionId, ct);
                        if (fabricVersion == null) throw new Exception($"未找到适用于 MC {versionId} 的 Fabric 版本");
                        return await fabricInstaller.InstallFabricAsync(versionId, fabricVersion, ct);

                    case "NeoForge":
                        var nfInstaller = new NeoForgeInstaller(minecraftPath);
                        var nfVersions = await nfInstaller.GetNeoForgeVersionsAsync(versionId, ct);
                        var latestNf = nfVersions?.FirstOrDefault();
                        if (latestNf == null) throw new Exception($"未找到适用于 MC {versionId} 的 NeoForge 版本");
                        return await nfInstaller.InstallNeoForgeAsync(versionId, latestNf, ct);

                    case "OptiFine":
                        var optiFineInstaller = new OptiFineInstaller(minecraftPath);
                        var optiFineVersions = await optiFineInstaller.GetOptiFineVersionsAsync(versionId, ct);
                        var latestOptiFine = optiFineVersions?.FirstOrDefault();
                        if (latestOptiFine == null) throw new Exception($"未找到适用于 MC {versionId} 的 OptiFine 版本");
                        return await optiFineInstaller.InstallOptiFineAsync(versionId, latestOptiFine, ct);

                    default:
                        Logger.Warning($"[InstallLoader] 未知加载器: {loader}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[InstallLoader] {loader} 安装异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>取消正在进行的版本安装</summary>
        public bool CancelInstall(string versionId)
        {
            if (_activeInstalls.TryRemove(versionId, out var cts))
            {
                try { cts.Cancel(); } catch { }
                PostMessage("toast", new { message = $"已取消 {versionId} 的下载", type = "info" });
                return true;
            }
            return false;
        }

        public bool DeleteVersion(string versionId)
        {
            // 先取消正在进行的下载
            CancelInstall(versionId);
            try
            {
                var versionsDir = System.IO.Path.Combine(
                    AppContext.MinecraftPath ?? "", "versions", versionId);
                if (System.IO.Directory.Exists(versionsDir))
                {
                    System.IO.Directory.Delete(versionsDir, true);
                    PostMessage("toast", new { message = $"版本 {versionId} 已删除", type = "success" });
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                PostMessage("toast", new { message = "删除失败: " + ex.Message, type = "error" });
                return false;
            }
        }

        /// <summary>
        /// 为已安装版本安装模组加载器（Forge/Fabric/NeoForge/OptiFine）
        /// </summary>
        public async Task InstallModLoader(string versionId, string loader)
        {
            try
            {
                PostMessage("toast", new { message = $"正在安装 {loader} 加载器...", type = "info" });
                var minecraftPath = AppContext.MinecraftPath ?? ".";
                using var ct = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                bool result = await InstallLoaderAsync(minecraftPath, versionId, loader, ct.Token);
                if (result)
                {
                    PostMessage("toast", new { message = $"{loader} 安装完成！", type = "success" });
                    PostMessage("mnl:modloader-installed", new { versionId, loader });
                }
                else
                {
                    PostMessage("toast", new { message = $"{loader} 安装失败", type = "error" });
                }
            }
            catch (Exception ex)
            {
                PostMessage("toast", new { message = $"{loader} 安装失败: {ex.Message}", type = "error" });
            }
        }

        /// <summary>修复版本文件（检查并下载缺失的库、natives、jar、assets）</summary>
        public async Task RepairVersion(string versionId)
        {
            // 创建下载任务并注册到下载任务中心
            var task = new GenericDownloadTaskViewModel(
                "repair://" + versionId,
                System.IO.Path.Combine(AppContext.MinecraftPath ?? ".", "versions", versionId),
                "修复 " + versionId);
            task.Status = "准备中";
            task.Progress = 0;
            Core.DownloadTaskManager.Instance.AddTask(task);

            try
            {
                var minecraftPath = AppContext.MinecraftPath ?? ".";
                var repairService = new Core.VersionRepairService(minecraftPath);

                repairService.StatusChanged += (msg) =>
                {
                    task.Status = msg;
                };
                repairService.ProgressChanged += (p) =>
                {
                    task.Progress = p;
                };
                repairService.RepairCompleted += (vid, msg) =>
                {
                    task.Status = "已完成";
                    task.Progress = 100;
                    task.IsCompleted = true;
                    PostMessage("toast", new { message = msg, type = "success" });
                    PostMessage("mnl:versions-changed", new { });
                };
                repairService.RepairFailed += (vid, msg) =>
                {
                    task.Status = "修复失败";
                    task.IsFailed = true;
                    task.ErrorMessage = msg;
                    PostMessage("toast", new { message = "修复失败: " + msg, type = "error" });
                };

                var result = await repairService.RepairVersionAsync(versionId);

                if (result.IsSuccess)
                {
                    // 如果没有缺失文件，RepairCompleted 不会触发，需手动标记完成
                    if (!task.IsCompleted)
                    {
                        task.Status = "已完成";
                        task.Progress = 100;
                        task.IsCompleted = true;
                        PostMessage("toast", new { message = "版本 " + versionId + " 完整，无需修复", type = "success" });
                    }
                }
                else if (!task.IsFailed)
                {
                    task.Status = "修复失败";
                    task.IsFailed = true;
                    task.ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                task.Status = "修复失败";
                task.IsFailed = true;
                task.ErrorMessage = ex.Message;
                PostMessage("toast", new { message = "修复异常: " + ex.Message, type = "error" });
            }
        }

        // ===== Java 管理 =====

        public object DetectJava()
        {
            try
            {
                var core = _window.GetLauncherCore();
                var javaList = core.DetectJavaVersions();
                return javaList.Select(j => new
                {
                    path = j.Path,
                    version = j.Version ?? "",
                    displayName = j.DisplayName ?? ""
                }).ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        public object GetJavaList()
        {
            return DetectJava();
        }

        /// <summary>检测 Java 列表，使用完整 JavaConfigManager 检测逻辑，返回 JSON</summary>
        public async Task<string> DetectJavaJson()
        {
            try
            {
                var minecraftPath = AppContext.MinecraftPath ?? ".";
                var javaConfig = new JavaConfigManager(minecraftPath);
                var javaList = await Task.Run(() => javaConfig.DetectInstalledJava());
                var list = javaList.Select(j => new
                {
                    path = j.Path ?? "",
                    version = j.Version ?? "",
                    displayName = $"Java {j.MajorVersion} ({j.Version})",
                    majorVersion = j.MajorVersion,
                    maxMemoryMb = j.MaxMemoryMb,
                    arch = DetectArchFromPath(j.Path) ?? DetectArchFromVersion(j.Version) ?? ""
                }).ToList();
                return JsonSerializer.Serialize(list);
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBridge] DetectJavaJson 异常: " + ex.Message);
                return "[]";
            }
        }

        private static string DetectArchFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var lower = path.ToLowerInvariant();
            if (lower.Contains("x86") || lower.Contains("(x86)")) return "32-bit";
            if (lower.Contains("x64") || lower.Contains("64-bit") || lower.Contains("64bit")) return "64-bit";
            return null;
        }

        private static string DetectArchFromVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return null;
            if (version.Contains("64-Bit") || version.Contains("64-bit")) return "64-bit";
            if (version.Contains("32-Bit") || version.Contains("32-bit")) return "32-bit";
            return null;
        }

        public async Task<string> DownloadJava(int version)
        {
            try
            {
                var minecraftPath = AppContext.MinecraftPath ?? ".";
                var service = new Core.JavaDownloadService(minecraftPath);
                service.StatusChanged += (msg) => PostMessage("toast", new { message = msg, type = "info" });
                service.ProgressChanged += (pct) => PostMessage("download-progress", new { taskId = $"java_{version}", progress = pct });
                var javaPath = await service.DownloadAndInstallJavaAsync(version);
                if (!string.IsNullOrEmpty(javaPath))
                {
                    PostMessage("toast", new { message = $"Java {version} 下载完成", type = "success" });
                    return javaPath;
                }
                PostMessage("toast", new { message = $"Java {version} 下载失败", type = "error" });
                return "";
            }
            catch (Exception ex)
            {
                PostMessage("toast", new { message = "下载失败: " + ex.Message, type = "error" });
                return "";
            }
        }

        // ===== 下载管理 =====

        public object GetDownloadTasks()
        {
            try
            {
                var tasks = Core.DownloadTaskManager.Instance.Tasks;
                return tasks.Select(t => new
                {
                    id = t.VersionId,
                    name = t.FileName,
                    size = t.FileSize,
                    progress = t.Progress,
                    speed = t.Speed,
                    status = t.Status
                }).ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        public void CancelDownload(string taskId)
        {
            try
            {
                var task = Core.DownloadTaskManager.Instance.Tasks
                    .FirstOrDefault(t => t.VersionId == taskId || t.FileName == taskId);
                if (task != null)
                {
                    task.Cancel();
                    PostMessage("toast", new { message = "已取消下载: " + task.FileName, type = "info" });
                }
            }
            catch (Exception ex)
            {
                PostMessage("toast", new { message = "取消失败: " + ex.Message, type = "error" });
            }
        }

        public void PauseDownload(string taskId)
        {
            try
            {
                var task = Core.DownloadTaskManager.Instance.Tasks
                    .FirstOrDefault(t => t.VersionId == taskId || t.FileName == taskId);
                task?.Pause();
            }
            catch { }
        }

        public void ResumeDownload(string taskId)
        {
            try
            {
                var task = Core.DownloadTaskManager.Instance.Tasks
                    .FirstOrDefault(t => t.VersionId == taskId || t.FileName == taskId);
                if (task != null)
                    Core.DownloadTaskManager.Instance.ResumeTask(task as DownloadTaskViewModel);
            }
            catch { }
        }

        // ===== 存档管理 =====

        public object GetSaves()
        {
            try
            {
                var savesDir = System.IO.Path.Combine(
                    AppContext.MinecraftPath ?? "", "saves");
                if (!System.IO.Directory.Exists(savesDir))
                    return Array.Empty<object>();
                return System.IO.Directory.GetDirectories(savesDir)
                    .Select(dir =>
                    {
                        var info = new System.IO.DirectoryInfo(dir);
                        return new
                        {
                            name = System.IO.Path.GetFileName(dir),
                            path = dir,
                            lastPlayed = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            version = ReadLevelDatVersion(System.IO.Path.Combine(dir, "level.dat"))
                        };
                    }).ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        private string ReadLevelDatVersion(string levelDatPath)
        {
            try { return "---"; }
            catch { return "---"; }
        }

        public async Task<string> BackupSave(string name)
        {
            try
            {
                var savesDir = System.IO.Path.Combine(AppContext.MinecraftPath ?? "", "saves");
                var backupDir = System.IO.Path.Combine(AppContext.MinecraftPath ?? "", "backups");
                System.IO.Directory.CreateDirectory(backupDir);
                var src = System.IO.Path.Combine(savesDir, name);
                var dst = System.IO.Path.Combine(backupDir, name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip");
                if (System.IO.Directory.Exists(src))
                {
                    System.IO.Compression.ZipFile.CreateFromDirectory(src, dst);
                    PostMessage("toast", new { message = $"存档 {name} 备份完成", type = "success" });
                    return "ok";
                }
                return "not_found";
            }
            catch (Exception ex)
            {
                PostMessage("toast", new { message = "备份失败: " + ex.Message, type = "error" });
                return "error";
            }
        }

        public string RestoreSave(string name) { return "not_implemented"; }

        // ===== 推荐/热门 =====

        /// <summary>获取主页推荐：6 模组 + 6 整合包（对齐 WPF HomePage.LoadRecommendationsAsync）</summary>
        public async Task<object> GetRecommendations()
        {
            try
            {
                var api = new Core.ModrinthApiService();

                // 并行拉取 6 个热门模组 + 6 个整合包
                var modsTask = api.GetPopularMods(6);
                var packsTask = api.GetModpacks(6);
                await Task.WhenAll(modsTask, packsTask);

                var mods = modsTask.Result ?? new List<Core.ModrinthMod>();
                var packs = packsTask.Result ?? new List<Core.ModrinthMod>();

                object Map(Core.ModrinthMod m, string type)
                {
                    return new
                    {
                        id = m.Id,
                        name = m.Name,
                        description = m.Description,
                        author = m.Author,
                        downloads = m.DownloadCountFormatted,
                        iconUrl = m.IconUrl,
                        tags = m.Categories != null ? string.Join(", ", m.Categories) : "",
                        type = type,
                        source = "Modrinth"
                    };
                }

                return new
                {
                    mods = mods.Select(m => Map(m, "mod")).ToArray(),
                    packs = packs.Select(p => Map(p, "modpack")).ToArray()
                };
            }
            catch { return new { mods = Array.Empty<object>(), packs = Array.Empty<object>() }; }
        }

        /// <summary>获取主页推荐 JSON（用于前端 JS 桥接）</summary>
        public async Task<string> GetRecommendationsJson()
        {
            try
            {
                var result = await GetRecommendations();
                return JsonSerializer.Serialize(result);
            }
            catch { return "{\"mods\":[],\"packs\":[]}"; }
        }

        /// <summary>获取下载选项（对齐 WPF DownloadConfirmDialog）：已安装版本、路径、所有可选版本等</summary>
        public async Task<string> GetDownloadOptionsJson(string projectId, string name, string type, string source = null)
        {
            try
            {
                // BBSMC 源使用 BBSMC API，Modrinth API 格式完全兼容
                string apiBase = (source == "bbsmc") ? "https://api.bbsmc.net/v2" : null;
                var versions = await Core.DownloadManager.GetModrinthVersions(projectId, apiBase);
                if (versions == null || versions.Count == 0)
                    return "{\"success\":false,\"error\":\"未找到可用的下载版本\"}";

                var recommended = Core.DownloadManager.GetRecommendedVersion(versions);
                if (recommended == null)
                    return "{\"success\":false,\"error\":\"未找到推荐版本\"}";

                string minecraftPath = AppContext.MinecraftPath ?? ".";
                var config = AppContext.Config;
                string currentVersion = config.GameVersion ?? "";

                // 获取已安装的版本列表
                var versionsDir = System.IO.Path.Combine(minecraftPath, "versions");
                var installedVersions = new List<string>();
                if (System.IO.Directory.Exists(versionsDir))
                {
                    foreach (string dir in System.IO.Directory.GetDirectories(versionsDir))
                    {
                        string vName = System.IO.Path.GetFileName(dir);
                        if (System.IO.File.Exists(System.IO.Path.Combine(dir, vName + ".json")))
                            installedVersions.Add(vName);
                    }
                }

                // 默认文件名（对齐 PCL：模组文件 ~ → - 替换避免 Mixin 兼容问题）
                string defaultFileName = recommended.FileName;
                if (string.IsNullOrEmpty(defaultFileName))
                    defaultFileName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}.jar";
                if (type == "mod") defaultFileName = defaultFileName.Replace("~", "-");
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                    defaultFileName = defaultFileName.Replace(c, '_');

                // 计算默认目标目录（对齐 WPF RecommendDetailPage.GetTargetDirectory）
                string subDir = type == "modpack" ? "modpacks" : "mods";
                string defaultTargetDir;
                if (!string.IsNullOrEmpty(currentVersion)
                    && SettingsManager.Settings.ShouldIsolateVersionForVersion(minecraftPath, currentVersion))
                    defaultTargetDir = System.IO.Path.Combine(minecraftPath, "versions", currentVersion, "game", subDir);
                else
                    defaultTargetDir = System.IO.Path.Combine(minecraftPath, subDir);

                // 所有可选版本列表（对齐 PCL：保留 name 作为版本标题，~ → - 替换，附带 IsBadDisplay）
                var isMod = type == "mod";
                var availableVersions = versions.Select((v, i) => new
                {
                    id = i,
                    versionName = v.VersionName ?? "",
                    displayInfo = v.DisplayInfo ?? "",
                    fileName = isMod ? (v.FileName ?? "").Replace("~", "-") : (v.FileName ?? ""),
                    fileSize = v.FileSize,
                    versionType = v.VersionType ?? "",
                    isRecommended = v.IsRecommended,
                    isBadDisplay = v.IsBadDisplay,
                }).ToArray();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    installedVersions = installedVersions.ToArray(),
                    currentVersion,
                    minecraftPath,
                    subDir,
                    defaultFileName,
                    defaultTargetDir,
                    versionName = recommended.VersionName ?? "",
                    availableVersions,
                    recommendedIndex = versions.IndexOf(recommended),
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>下载推荐项目 → mods/modpacks 目录（对齐 RecommendDetailPage.BtnDownload_Click）</summary>
        public async Task<string> DownloadRecommendJson(string projectId, string name, string type, string savePath = null, string fileName = null, string versionIndex = null, string source = null)
        {
            try
            {
                string apiBase = (source == "bbsmc") ? "https://api.bbsmc.net/v2" : null;
                var versions = await Core.DownloadManager.GetModrinthVersions(projectId, apiBase);
                if (versions == null || versions.Count == 0)
                    return "{\"success\":false,\"error\":\"未找到可用的下载版本\"}";

                // 按 versionIndex 选择版本，默认用推荐版本
                Core.DownloadVersionInfo targetVersion;
                if (!string.IsNullOrEmpty(versionIndex) && int.TryParse(versionIndex, out int idx)
                    && idx >= 0 && idx < versions.Count)
                {
                    targetVersion = versions[idx];
                }
                else
                {
                    targetVersion = Core.DownloadManager.GetRecommendedVersion(versions);
                    if (targetVersion == null)
                        return "{\"success\":false,\"error\":\"未找到推荐版本\"}";
                }

                // 使用自定义路径或自动生成
                string finalPath;
                string finalFileName;
                if (!string.IsNullOrEmpty(savePath) && !string.IsNullOrEmpty(fileName))
                {
                    finalPath = savePath;
                    finalFileName = fileName;
                    if (type == "mod") finalFileName = finalFileName.Replace("~", "-");
                }
                else
                {
                    string minecraftPath = AppContext.MinecraftPath ?? ".";
                    string subDir = type == "modpack" ? "modpacks" : "mods";
                    string targetDir = System.IO.Path.Combine(minecraftPath, subDir);
                    if (!System.IO.Directory.Exists(targetDir))
                        System.IO.Directory.CreateDirectory(targetDir);

                    finalFileName = targetVersion.FileName;
                    if (string.IsNullOrEmpty(finalFileName))
                        finalFileName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}.jar";
                    if (type == "mod") finalFileName = finalFileName.Replace("~", "-");
                    foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                        finalFileName = finalFileName.Replace(c, '_');

                    finalPath = System.IO.Path.Combine(targetDir, finalFileName);
                }

                // 确保目标目录存在
                string dir = System.IO.Path.GetDirectoryName(finalPath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                bool success = Core.DownloadManager.AddDownloadTask(name, finalPath, targetVersion, finalFileName);
                return JsonSerializer.Serialize(new { success, error = success ? null : "添加下载任务失败" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        // ===== 模组管理 =====

        public object GetInstalledMods(string versionId)
        {
            try
            {
                var modsDir = System.IO.Path.Combine(
                    AppContext.MinecraftPath ?? "", "versions", versionId, "mods");
                if (!System.IO.Directory.Exists(modsDir))
                    return Array.Empty<object>();
                return System.IO.Directory.GetFiles(modsDir, "*.jar")
                    .Select(f =>
                    {
                        var fi = new System.IO.FileInfo(f);
                        return new
                        {
                            id = System.IO.Path.GetFileNameWithoutExtension(f),
                            name = System.IO.Path.GetFileNameWithoutExtension(f),
                            path = f,
                            size = fi.Length,
                            enabled = !f.EndsWith(".disabled")
                        };
                    }).ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        public async Task<object> SearchMods(string query, int offset, int limit)
        {
            try
            {
                var api = new Core.ModrinthApiService();
                var mods = await api.SearchMods(query, "", Math.Min(limit, 50), offset);
                return mods.Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    author = m.Author,
                    description = m.Description,
                    downloads = m.DownloadCountFormatted,
                    source = "Modrinth"
                }).ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        public string InstallMod(string modId, string versionId) { return "not_implemented"; }
        public string UninstallMod(string modId, string versionId) { return "not_implemented"; }
        public string GetModDetail(string modId) { return "{}"; }

        // ===== 账户（补充） =====

        public string SwitchMode(bool isOffline, string username)
        {
            try
            {
                AppContext.IsOfflineMode = isOffline;
                if (!string.IsNullOrEmpty(username))
                    AppContext.Username = username;
                PostMessage("toast", new { message = isOffline ? "已切换至离线模式" : "已切换至在线模式", type = "info" });
                return "ok";
            }
            catch { return "error"; }
        }

        // ===== 好友 =====

        /// <summary>获取好友列表（JSON数组）</summary>
        public async Task<string> GetFriendList()
        {
            try
            {
                // 确保好友服务已启动
                var service = Core.FriendService.Instance;
                if (!service.IsRunning && !string.IsNullOrEmpty(AppContext.QingniaoId))
                    service.Start(AppContext.QingniaoId);

                var serverFriends = await service.GetFriendsAsync();
                Debug.WriteLine($"[WebBridge] GetFriendList: 服务器返回 {serverFriends.Count} 个好友");
                foreach (var f in serverFriends)
                    Debug.WriteLine($"[WebBridge]   好友: id={f.friend_id}, nickname={f.friend_nickname}, online={f.is_online}");

                var list = serverFriends.Select(f => new
                {
                    id = f.friend_id,
                    name = !string.IsNullOrEmpty(f.friend_nickname) ? f.friend_nickname : f.friend_id,
                    status = f.is_online == 1 ? "online" : "offline",
                    color = DeriveFriendColor(f.friend_id)
                }).ToList();

                var result = JsonSerializer.Serialize(list);
                Debug.WriteLine($"[WebBridge] GetFriendList 返回 JSON: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] GetFriendList 异常: {ex.Message}");
                return "[]";
            }
        }

        /// <summary>添加好友</summary>
        public async Task<string> AddFriend(string friendId)
        {
            try
            {
                Debug.WriteLine($"[WebBridge] AddFriend: friendId={friendId}");
                if (string.IsNullOrWhiteSpace(friendId))
                    return JsonSerializer.Serialize(new { success = false, error = "请输入好友的青鸟ID" });

                // 格式校验：只允许数字
                friendId = friendId.Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(friendId, @"^\d{5,12}$"))
                    return JsonSerializer.Serialize(new { success = false, error = "青鸟ID格式无效（5~12位数字）" });

                // 不允许添加自己
                string myId = AppContext.QingniaoId ?? "";
                if (!string.IsNullOrEmpty(myId) && friendId == myId)
                    return JsonSerializer.Serialize(new { success = false, error = "不能添加自己为好友" });

                // 检查目标用户是否真实存在
                bool userExists = await CheckUserExistsAsync(friendId);
                if (!userExists)
                    return JsonSerializer.Serialize(new { success = false, error = "该青鸟ID不存在，请确认后重试" });

                // 确保好友服务已启动
                var service = Core.FriendService.Instance;
                if (!service.IsRunning && !string.IsNullOrEmpty(myId))
                    service.Start(myId);

                var (success, error) = await service.AddFriendAsync(friendId);
                Debug.WriteLine($"[WebBridge] AddFriend 结果: success={success}, error={error}");
                return JsonSerializer.Serialize(new { success, error });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] AddFriend 异常: {ex.Message}");
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>检查用户是否在服务器上真实存在</summary>
        private async Task<bool> CheckUserExistsAsync(string userId)
        {
            try
            {
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var url = $"{Core.ServerConfig.FriendsApiUrl}?action=list&user_id={Uri.EscapeDataString(userId)}";
                var json = await http.GetStringAsync(url);
                // 如果服务器返回了合法的 friends 列表（即使是空数组），说明用户存在
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("friends", out _);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] CheckUserExists 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>删除好友</summary>
        public async Task<string> RemoveFriend(string friendId)
        {
            try
            {
                var service = Core.FriendService.Instance;
                var success = await service.RemoveFriendAsync(friendId);
                return JsonSerializer.Serialize(new { success });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>发送聊天消息给好友</summary>
        public async Task<string> SendFriendMessage(string friendId, string message)
        {
            try
            {
                var service = Core.FriendService.Instance;
                var success = await service.SendMessageAsync(friendId, message);
                return JsonSerializer.Serialize(new { success });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>根据好友ID确定性派生头像颜色</summary>
        private static string DeriveFriendColor(string id)
        {
            var colors = new[] { "#2196F3", "#F44336", "#4CAF50", "#FF9800", "#9C27B0", "#00BCD4", "#E91E63", "#607D8B" };
            int hash = 0;
            foreach (char c in id)
                hash = (hash * 31 + c) & 0x7FFFFFFF;
            return colors[Math.Abs(hash) % colors.Length];
        }

        // ===== 加密聊天 =====

        /// <summary>获取或生成自己的 RSA 密钥对（返回公钥给前端）</summary>
        public string GetMyPublicKey()
        {
            try
            {
                string privateKey = PCL.Account.Settings.Get<string>("CryptoPrivateKey");
                if (string.IsNullOrEmpty(privateKey))
                {
                    var (pub, priv) = Core.ChatCryptoService.GenerateRsaKeyPair();
                    PCL.Account.Settings.Set("CryptoPrivateKey", priv);
                    PCL.Account.Settings.Set("CryptoPublicKey", pub);
                    Debug.WriteLine("[WebBridge] 已生成新的 RSA 密钥对");
                    return pub;
                }
                return PCL.Account.Settings.Get<string>("CryptoPublicKey") ?? "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] GetMyPublicKey 异常: {ex.Message}");
                return "";
            }
        }

        /// <summary>发送加密聊天消息（接收方ID, 明文）</summary>
        public async Task<string> SendEncryptedChatMessage(string friendId, string plaintext)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(friendId) || string.IsNullOrWhiteSpace(plaintext))
                    return JsonSerializer.Serialize(new { success = false, error = "参数不能为空" });

                // 获取对方的公钥
                string recipientPubKey = await GetFriendCryptoKeyAsync(friendId);
                if (string.IsNullOrEmpty(recipientPubKey))
                    return JsonSerializer.Serialize(new { success = false, error = "无法获取对方的加密公钥，请确认对方已登录" });

                // 加密
                string encrypted = Core.ChatCryptoService.EncryptMessage(plaintext, recipientPubKey);
                Debug.WriteLine($"[WebBridge] 加密消息长度: {encrypted.Length}");

                // 发送到服务器
                var service = Core.FriendService.Instance;
                if (!service.IsRunning && !string.IsNullOrEmpty(AppContext.QingniaoId))
                    service.Start(AppContext.QingniaoId);

                bool ok = await service.SendChatMessageAsync(friendId, encrypted);
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "发送失败" });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] SendEncryptedChatMessage 异常: {ex.Message}");
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>获取与某好友的聊天历史（自动解密）</summary>
        public async Task<string> GetChatHistory(string friendId, long sinceId)
        {
            try
            {
                string myPrivateKey = PCL.Account.Settings.Get<string>("CryptoPrivateKey") ?? "";
                if (string.IsNullOrEmpty(myPrivateKey))
                    return "[]";

                var service = Core.FriendService.Instance;
                if (!service.IsRunning && !string.IsNullOrEmpty(AppContext.QingniaoId))
                    service.Start(AppContext.QingniaoId);

                var messages = await service.GetChatHistoryAsync(friendId, sinceId);
                var result = new List<object>();
                foreach (var msg in messages)
                {
                    try
                    {
                        string text = Core.ChatCryptoService.DecryptMessage(msg.encrypted_content, myPrivateKey);
                        result.Add(new { id = msg.id, from = msg.sender_id, text, time = msg.created_at });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebBridge] 解密消息 {msg.id} 失败: {ex.Message}");
                        result.Add(new { id = msg.id, from = msg.sender_id, text = "[解密失败]", time = msg.created_at });
                    }
                }
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] GetChatHistory 异常: {ex.Message}");
                return "[]";
            }
        }

        /// <summary>轮询新聊天消息（自动解密）</summary>
        public async Task<string> PollChatMessages(long sinceId)
        {
            try
            {
                string myPrivateKey = PCL.Account.Settings.Get<string>("CryptoPrivateKey") ?? "";
                if (string.IsNullOrEmpty(myPrivateKey))
                    return "[]";

                var service = Core.FriendService.Instance;
                if (!service.IsRunning && !string.IsNullOrEmpty(AppContext.QingniaoId))
                    service.Start(AppContext.QingniaoId);

                var messages = await service.PollChatMessagesAsync(sinceId);
                var result = new List<object>();
                foreach (var msg in messages)
                {
                    try
                    {
                        string text = Core.ChatCryptoService.DecryptMessage(msg.encrypted_content, myPrivateKey);
                        result.Add(new { id = msg.id, sender = msg.sender_id, receiver = msg.receiver_id, text, time = msg.created_at });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebBridge] 轮询解密消息 {msg.id} 失败: {ex.Message}");
                    }
                }
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] PollChatMessages 异常: {ex.Message}");
                return "[]";
            }
        }

        /// <summary>从服务器获取好友的加密公钥</summary>
        private async Task<string> GetFriendCryptoKeyAsync(string friendId)
        {
            try
            {
                var service = Core.FriendService.Instance;
                var http = new HttpClient();
                var url = $"{Core.ServerConfig.ChatApiUrl}?action=get_key&user_id={Uri.EscapeDataString(friendId)}";
                var json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("public_key", out var keyProp))
                    return keyProp.GetString() ?? "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] 获取好友公钥失败: {ex.Message}");
            }
            return "";
        }

        // ===== 社区 =====

        /// <summary>获取当前登录用户的社区身份信息</summary>
        public async Task<string> GetCommunityUserInfo()
        {
            try
            {
                // 多源回退 — 与 GetProfile() 保持一致
                string qnId = !string.IsNullOrEmpty(AppContext.QingniaoId) ? AppContext.QingniaoId
                    : (PCL.Account.Settings.Get<string>("QingniaoId")
                       ?? SettingsManager.Settings.QingniaoId
                       ?? "");
                if (string.IsNullOrEmpty(qnId) || qnId == "0000000000") qnId = "";

                string qnName = !string.IsNullOrEmpty(AppContext.QingniaoName) ? AppContext.QingniaoName
                    : (PCL.Account.Settings.Get<string>("QingniaoName")
                       ?? SettingsManager.Settings.QingniaoName
                       ?? "");
                if (string.IsNullOrEmpty(qnName)) qnName = "";

                bool isLoggedIn = !string.IsNullOrEmpty(qnId) && !string.IsNullOrEmpty(qnName);

                // 青鸟头像
                string qnAvatar = PCL.Account.Settings.Get<string>("QingniaoAvatar")
                    ?? SettingsManager.Settings.QingniaoAvatar
                    ?? "";

                // 派生头像颜色（无头像时使用）
                string color = !string.IsNullOrEmpty(qnName)
                    ? DeriveFriendColor(qnId.Length > 0 ? qnId : qnName)
                    : "#6C5CE7";
                return JsonSerializer.Serialize(new
                {
                    isLoggedIn,
                    qingniaoId = qnId,
                    nickname = qnName,
                    avatarUrl = qnAvatar,
                    avatarColor = color,
                    avatarChar = !string.IsNullOrEmpty(qnName) ? qnName[0].ToString() : "?"
                });
            }
            catch { return "{\"isLoggedIn\":false}"; }
        }

        public async Task<string> GetCommunityChannelsJson()
        {
            try
            {
                var channels = await Core.CommunityService.Instance.GetChannelsAsync();
                var list = channels.Select(c => new
                {
                    id = c.id,
                    name = c.name,
                    description = c.description,
                    icon_emoji = c.icon_emoji,
                    message_count = c.message_count,
                    last_message = c.last_message != null ? new
                    {
                        content = c.last_message.content,
                        sender_id = c.last_message.sender_id,
                        created_at = c.last_message.created_at
                    } : null
                }).ToList();
                return JsonSerializer.Serialize(new { channels = list });
            }
            catch { return "{\"channels\":[]}"; }
        }

        public async Task<string> GetCommunityMessagesJson(int channelId)
        {
            try
            {
                var messages = await Core.CommunityService.Instance.GetMessagesAsync(channelId);

                // 收集所有不重复的 sender_id，批量读取本地头像
                var senderIds = messages.Select(m => m.sender_id).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                var avatarCache = new Dictionary<string, string>();
                foreach (var sid in senderIds)
                {
                    avatarCache[sid] = GetSenderAvatarBase64(sid);
                }

                var list = messages.Select(m => new
                {
                    id = m.id,
                    channel_id = m.channel_id,
                    sender_id = m.sender_id,
                    /// 服务器返回的昵称（community.php LEFT JOIN users 查询）
                    sender_nickname = !string.IsNullOrEmpty(m.sender_nickname) ? m.sender_nickname : m.sender_id,
                    /// 头像：优先本地 avatars/{id}.png，其次服务端 avatar_url
                    sender_avatar = !string.IsNullOrEmpty(m.sender_avatar) ? m.sender_avatar
                        : (avatarCache.TryGetValue(m.sender_id ?? "", out var localAvatar) ? localAvatar : ""),
                    title = m.title,
                    content = m.content,
                    msg_type = m.msg_type,
                    created_at = m.created_at
                }).ToList();
                return JsonSerializer.Serialize(new { messages = list });
            }
            catch { return "{\"messages\":[]}"; }
        }

        /// <summary>读取本地头像文件返回 base64 data URI，无文件返回空字符串</summary>
        private static string GetSenderAvatarBase64(string qingniaoId)
        {
            if (string.IsNullOrEmpty(qingniaoId)) return "";
            try
            {
                string avatarFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "avatars", qingniaoId + ".png");
                if (System.IO.File.Exists(avatarFile))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(avatarFile);
                    return "data:image/png;base64," + Convert.ToBase64String(bytes);
                }
            }
            catch { /* 读取失败返回空 */ }
            return "";
        }

        /// <summary>发帖（强制使用当前登录用户身份）</summary>
        public async Task<string> SendCommunityMessageJson(int channelId, string senderId, string title, string content)
        {
            try
            {
                // 强制使用当前登录用户的 Qingniao ID
                string realSenderId = AppContext.QingniaoId ?? "";
                if (string.IsNullOrEmpty(realSenderId))
                    return "{\"success\":false,\"error\":\"请先登录青鸟账号后再发帖\"}";

                // 使用真实 sender_nickname
                string senderNickname = AppContext.QingniaoName ?? realSenderId;

                var result = await Core.CommunityService.Instance.SendMessageAsync(channelId, realSenderId, title, content);
                return JsonSerializer.Serialize(new
                {
                    success = result.success,
                    id = result.id,
                    created_at = result.created_at,
                    sender_nickname = senderNickname,
                    sender_avatar = GetSenderAvatarBase64(realSenderId),
                    error = result.success ? null : "发帖失败"
                });
            }
            catch (Exception ex) { return JsonSerializer.Serialize(new { success = false, error = ex.Message }); }
        }

        public async Task<string> CreateCommunityChannelJson(string name, string description, string iconEmoji)
        {
            try
            {
                // 检查登录状态
                string creatorId = AppContext.QingniaoId ?? "";
                if (string.IsNullOrEmpty(creatorId))
                    return "{\"success\":false,\"error\":\"请先登录青鸟账号后再创建频道\"}";

                // 将 creator_id 传递给社区服务（服务器端也会验证）
                var ok = await Core.CommunityService.Instance.CreateChannelAsync(name, description, iconEmoji, creatorId);
                return JsonSerializer.Serialize(new { success = ok });
            }
            catch (Exception ex) { return JsonSerializer.Serialize(new { success = false, error = ex.Message }); }
        }

        // 保留旧接口兼容
        public object GetCommunityMessages(string channel) { return Array.Empty<object>(); }
        public string SendCommunityMessage(string channel, string content) { return "not_implemented"; }

        // ===== 联机工具 (MCTier / 陶瓦) =====

        /// <summary>获取 MCTier 和 陶瓦 的安装状态</summary>
        public string GetMultiplayerToolsStatusJson()
        {
            try
            {
                var mp = Core.MultiplayerService.Instance;
                return JsonSerializer.Serialize(new
                {
                    mctier = new
                    {
                        installed = mp.IsMctierInstalled(),
                        exePath = mp.GetMctierExePath() ?? "",
                        installDir = mp.MctierDir
                    },
                    taowa = new
                    {
                        installed = mp.IsTaowaInstalled(),
                        installDir = mp.TaowaDir,
                        running = mp.IsTerracottaRunning
                    },
                    serverManager = new
                    {
                        installed = mp.IsServerManagerInstalled(),
                        installDir = mp.ServerManagerDir
                    }
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        /// <summary>下载并安装 MCTier</summary>
        public async Task<string> DownloadMctierJson()
        {
            try
            {
                var mp = Core.MultiplayerService.Instance;
                bool ok = await mp.DownloadMctierAsync();
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "下载安装失败，请查看日志" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>打开 MCTier 安装目录</summary>
        public string OpenMctierFolderJson()
        {
            try
            {
                bool ok = Core.MultiplayerService.Instance.OpenMctierFolder();
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "打开目录失败" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>启动 MCTier 程序</summary>
        public string LaunchMctierJson()
        {
            try
            {
                var mp = Core.MultiplayerService.Instance;
                if (!mp.IsMctierInstalled())
                    return "{\"success\":false,\"error\":\"MCTier 尚未安装，请先下载\"}";
                bool ok = mp.LaunchMctier();
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "启动失败" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>下载并安装陶瓦核心</summary>
        public async Task<string> DownloadTaowaJson()
        {
            try
            {
                var mp = Core.MultiplayerService.Instance;
                bool ok = await mp.DownloadTaowaAsync();
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "下载安装失败，请查看日志" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>启动陶瓦核心 (HMCL 模式)</summary>
        public async Task<string> LaunchTaowaJson()
        {
            try
            {
                var mp = Core.MultiplayerService.Instance;
                if (!mp.IsTaowaInstalled())
                    return "{\"success\":false,\"error\":\"陶瓦核心尚未安装，请先下载\"}";
                bool ok = await mp.LaunchTerracottaAsync();
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "启动失败，请查看日志" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        // ═══════════════ MNL 服务端管理器 (ServerManager) ═══════════════

        /// <summary>下载并安装 MNL 服务端管理器</summary>
        public async Task<string> DownloadServerManagerJson()
        {
            try
            {
                var mp = Core.MultiplayerService.Instance;
                bool ok = await mp.DownloadServerManagerAsync();
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "下载安装失败，请查看日志" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>打开 ServerManager 安装目录</summary>
        public string OpenServerManagerFolderJson()
        {
            try
            {
                bool ok = Core.MultiplayerService.Instance.OpenServerManagerFolder();
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "打开目录失败" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>启动 ServerManager.exe</summary>
        public string LaunchServerManagerJson()
        {
            try
            {
                var mp = Core.MultiplayerService.Instance;
                if (!mp.IsServerManagerInstalled())
                    return "{\"success\":false,\"error\":\"MNL 服务端管理器尚未安装，请先下载\"}";
                bool ok = mp.LaunchServerManager();
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "启动失败" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>陶瓦房主模式 — 创建房间</summary>
        public async Task<string> StartTaowaHostJson()
        {
            try
            {
                var mp = Core.MultiplayerService.Instance;
                if (!mp.IsTerracottaRunning)
                    return "{\"success\":false,\"error\":\"陶瓦核心未启动，请先启动\"}";
                bool ok = await mp.StartTerracottaHostAsync();
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "创建房间失败" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>陶瓦房客模式 — 加入房间</summary>
        public async Task<string> JoinTaowaRoomJson(string roomCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode))
                    return "{\"success\":false,\"error\":\"请输入邀请码\"}";
                var mp = Core.MultiplayerService.Instance;
                if (!mp.IsTerracottaRunning)
                    return "{\"success\":false,\"error\":\"陶瓦核心未启动，请先启动\"}";
                bool ok = await mp.JoinTerracottaRoomAsync(roomCode.Trim());
                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "加入房间失败" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>停止陶瓦核心</summary>
        public string StopTaowaJson()
        {
            try
            {
                Core.MultiplayerService.Instance.StopTerracotta();
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>获取陶瓦当前运行状态</summary>
        public string GetTaowaStatusJson()
        {
            try
            {
                var s = Core.MultiplayerService.Instance.GetTerracottaStatus();
                return JsonSerializer.Serialize(s);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        // ===== 用户协议 =====

        /// <summary>获取用户协议接受状态（主启动器 + 陶瓦联机）</summary>
        public string GetAgreementStatusJson()
        {
            try
            {
                var s = Core.SettingsManager.Settings;
                return JsonSerializer.Serialize(new
                {
                    termsAccepted = s.TermsOfServiceAccepted,
                    terracottaAccepted = s.TerracottaAgreementAccepted
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        /// <summary>接受主启动器用户协议并保存</summary>
        public string AcceptTermsOfServiceJson()
        {
            try
            {
                Core.SettingsManager.Settings.TermsOfServiceAccepted = true;
                Core.SettingsManager.SaveSettings();
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>接受陶瓦联机用户协议并保存</summary>
        public string AcceptTerracottaAgreementJson()
        {
            try
            {
                Core.SettingsManager.Settings.TerracottaAgreementAccepted = true;
                Core.SettingsManager.SaveSettings();
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        // ===== RedStone 红石联机 =====

        /// <summary>获取 RedStone 服务器节点列表（{名称: IP}）</summary>
        public async Task<string> GetRedstoneServersJson()
        {
            try
            {
                var servers = await Core.RedstoneService.Instance.GetServersAsync();
                return JsonSerializer.Serialize(new { success = true, servers });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>注册 RedStone API Key</summary>
        public async Task<string> RegisterRedstoneApikeyJson(string serverIp, string apikey)
        {
            try
            {
                var (success, message) = await Core.RedstoneService.Instance.RegisterApikeyAsync(serverIp, apikey);
                return JsonSerializer.Serialize(new { success, message });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>创建 RedStone 隧道（可选公开房间）</summary>
        public async Task<string> CreateRedstoneTunnelJson(string serverIp, string apikey,
            bool isPublic, string title, string description, string mods, string version, bool online)
        {
            try
            {
                var (success, message, tunnel) = await Core.RedstoneService.Instance.CreateTunnelAsync(
                    serverIp, apikey, isPublic, title, description, mods, version, online);
                return JsonSerializer.Serialize(new
                {
                    success,
                    message,
                    tunnelId = tunnel?.tunnelId,
                    listenPort = tunnel?.listenPort
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>查询 RedStone 隧道状态</summary>
        public async Task<string> QueryRedstoneTunnelJson(string serverIp, string apikey)
        {
            try
            {
                var (success, message, tunnels) = await Core.RedstoneService.Instance.QueryTunnelsAsync(serverIp, apikey);
                return JsonSerializer.Serialize(new { success, message, tunnels });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>删除/关闭 RedStone 隧道</summary>
        public async Task<string> DeleteRedstoneTunnelJson(string serverIp, string apikey)
        {
            try
            {
                var (success, message) = await Core.RedstoneService.Instance.DeleteTunnelAsync(serverIp, apikey);
                return JsonSerializer.Serialize(new { success, message });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>浏览 RedStone 公开房间列表（遍历所有服务器）</summary>
        public async Task<string> GetRedstonePublicRoomsJson(int offset)
        {
            try
            {
                var (success, message, rooms) = await Core.RedstoneService.Instance.GetPublicRoomsAsync(offset);
                return JsonSerializer.Serialize(new { success, message, rooms });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>获取单个公开房间的模组列表</summary>
        public async Task<string> GetRedstoneRoomModsJson(int roomId)
        {
            try
            {
                var (success, message, mods) = await Core.RedstoneService.Instance.GetRoomModsAsync(roomId);
                return JsonSerializer.Serialize(new { success, message, mods });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>启动本地 TCP 中继（连接服务器:7000 + 本地监听 MC 连接）</summary>
        public async Task<string> StartRedstoneRelayJson(string serverIp, string apikey)
        {
            try
            {
                var (success, message, tunnelId) = await Core.RedstoneService.Instance.StartTunnelRelayAsync(serverIp, apikey);
                return JsonSerializer.Serialize(new
                {
                    success,
                    message,
                    tunnelId = tunnelId ?? "",
                    localPort = Core.RedstoneService.Instance.RelayLocalPort
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>停止本地 TCP 中继</summary>
        public string StopRedstoneRelayJson()
        {
            try
            {
                Core.RedstoneService.Instance.StopTunnelRelay();
                return JsonSerializer.Serialize(new { success = true, message = "中继已停止" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>获取本地 TCP 中继状态</summary>
        public string GetRedstoneRelayStatusJson()
        {
            try
            {
                var status = Core.RedstoneService.Instance.GetRelayStatus();
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    status.running,
                    status.localPort,
                    status.controlStatus
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>拒绝主启动器用户协议 — 关闭应用程序</summary>
        public void DeclineTermsOfService()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }

        // ===== 启动器更新 =====

        /// <summary>当前更新进度（供前端轮询）</summary>
        private static Core.UpdateService.UpdateProgress _latestUpdateProgress;

        // ===== 版本列表缓存（避免每次前端请求都调远程 API） =====
        private static string _cachedVersionsJson;
        private static DateTime _cachedVersionsTime = DateTime.MinValue;
        private static readonly TimeSpan _versionsCacheTtl = TimeSpan.FromMinutes(10);

        /// <summary>获取自动更新开关状态</summary>
        public string GetAutoCheckUpdateJson()
        {
            try
            {
                return JsonSerializer.Serialize(new {
                    autoCheckUpdate = Core.SettingsManager.Settings.CheckUpdate
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        /// <summary>设置自动更新开关</summary>
        public string SetAutoCheckUpdateJson(bool enabled)
        {
            try
            {
                Core.SettingsManager.Settings.CheckUpdate = enabled;
                Core.SettingsManager.SaveSettings();
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>检查更新 — 返回版本信息</summary>
        public async Task<string> CheckForUpdateJson()
        {
            try
            {
                var info = await Core.UpdateService.FetchLatestVersionAsync();
                if (info == null)
                {
                    return JsonSerializer.Serialize(new {
                        success = false,
                        error = "无法连接到更新服务器"
                    });
                }
                return JsonSerializer.Serialize(new {
                    success = true,
                    needUpdate = info.NeedUpdate,
                    currentVersion = Core.UpdateService.CurrentVersion,
                    version = info.Version,
                    versionCode = info.VersionCode,
                    fileSize = info.FileSize,
                    releaseNotes = info.ReleaseNotes,
                    isForced = info.IsForced,
                    downloadUrl = info.DownloadUrl,
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>下载并安装更新（带进度回调）</summary>
        public async Task<string> DownloadUpdateJson()
        {
            try
            {
                var info = await Core.UpdateService.FetchLatestVersionAsync();
                if (info == null || !info.NeedUpdate)
                {
                    return JsonSerializer.Serialize(new {
                        success = false,
                        error = "没有可用的更新"
                    });
                }

                _latestUpdateProgress = null;

                bool ok = await Core.UpdateService.DownloadAndInstallAsync(info, progress =>
                {
                    _latestUpdateProgress = progress;
                });

                return JsonSerializer.Serialize(new { success = ok, error = ok ? null : "更新失败" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>获取下载进度（前端轮询）</summary>
        public string GetUpdateProgressJson()
        {
            try
            {
                if (_latestUpdateProgress == null)
                {
                    return JsonSerializer.Serialize(new { status = "准备下载...", percent = 0 });
                }
                return JsonSerializer.Serialize(new {
                    status = _latestUpdateProgress.Status,
                    percent = _latestUpdateProgress.ProgressPercent
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        // ===== 下载分类搜索 =====

        public async Task<string> SearchDownloadItemsJson(string category, string query, string source, string version, string type, int page)
        {
            try
            {
                int pageSize = 20;
                int offset = (page - 1) * pageSize;
                int apiLimit = 100;
                var modrinthApi = new Core.ModrinthApiService();
                var curseForgeApi = new Core.CurseForgeApiService();
                var result = new List<object>();

                if (source == "CurseForge" || source == "All")
                {
                    List<Core.CurseForgeMod> cfItems = null;
                    if (!string.IsNullOrEmpty(query))
                        cfItems = await curseForgeApi.SearchMods(query, apiLimit, offset);
                    else
                        cfItems = category switch
                        {
                            "modpacks" or "packs" => await curseForgeApi.GetModpacks(apiLimit),
                            "shaders" => await curseForgeApi.GetShaders(apiLimit),
                            "datapacks" => await curseForgeApi.GetDatapacks(apiLimit),
                            _ => await curseForgeApi.GetPopularMods(apiLimit)
                        };

                    if (cfItems != null)
                    {
                        foreach (var m in cfItems.Skip(offset).Take(pageSize))
                        {
                            result.Add(new { name = m.Name ?? "", iconUrl = m.LogoUrl ?? "", description = m.Summary ?? "",
                                version = m.LatestFile?.DisplayName ?? "", author = m.AuthorName ?? "", downloads = m.DownloadCountFormatted ?? "0",
                                source = "CurseForge", projectId = m.Id.ToString(), type = "" });
                        }
                    }
                }

                if (source == "Modrinth" || source == "All" || string.IsNullOrEmpty(source))
                {
                    List<Core.ModrinthMod> mrItems = null;
                    if (!string.IsNullOrEmpty(query))
                        mrItems = await modrinthApi.SearchMods(query, version, apiLimit, offset);
                    else
                        mrItems = category switch
                        {
                            "modpacks" or "packs" => await modrinthApi.GetModpacks(apiLimit),
                            "shaders" => await modrinthApi.GetShaders(apiLimit),
                            "datapacks" => await modrinthApi.GetDatapacks(apiLimit),
                            "mods" => await modrinthApi.GetMods(apiLimit),
                            _ => await modrinthApi.GetPopularMods(apiLimit)
                        };

                    if (mrItems != null)
                    {
                        int take = pageSize - result.Count;
                        foreach (var m in mrItems.Skip(offset).Take(take > 0 ? take : pageSize))
                        {
                            result.Add(new { name = m.Name ?? "", iconUrl = m.IconUrl ?? "", description = m.Description ?? "",
                                version = m.LatestVersion ?? "", author = m.Author ?? "", downloads = m.Downloads.ToString(),
                                source = "Modrinth", projectId = m.Id ?? "", type = "" });
                        }
                    }
                }

                bool hasMore = result.Count >= pageSize;
                return JsonSerializer.Serialize(new { items = result, hasMore });
            }
            catch (Exception ex)
            {
                Logger.Error("[下载搜索] 失败: " + ex.Message);
                return "{\"items\":[],\"hasMore\":false}";
            }
        }

        /// <summary>
        /// 从 bbsmc.net API v2 获取资源列表（兼容 Modrinth API）。
        /// type: mods|plugins|modpacks|shaders|resourcepacks|languages|datapacks|maps|softwares
        /// </summary>
        public async Task<string> FetchBbsmcJson(string type, string query, int page)
        {
            try
            {
                string projectType = (type ?? "mods") switch
                {
                    "plugins" => "plugin",
                    "modpacks" or "packs" or "domestic-packs" => "modpack",
                    "shaders" => "shader",
                    "resourcepacks" => "resourcepack",
                    "datapacks" => "datapack",
                    "languages" => "language",
                    "maps" => "map",
                    "softwares" => "software",
                    "mods" => "mod",
                    _ => "mod"
                };

                int limit = 20;
                int offset = (page - 1) * limit;

                // 构建 facets 过滤（统一用 project_type）
                string facets = System.Net.WebUtility.UrlEncode(
                    $"[[\"project_type:{projectType}\"]]");

                string url = $"https://api.bbsmc.net/v2/search?query={System.Net.WebUtility.UrlEncode(query ?? "")}&offset={offset}&limit={limit}&facets={facets}";

                // 使用独立 HttpClient，避免 SafeHttpClientFactory 的 Referrer: minecraft.net 被 API 拒绝
                using var handler = new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 };
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MNL/1.0");

                string json = await http.GetStringAsync(url);
                if (string.IsNullOrEmpty(json)) return "{\"items\":[],\"hasMore\":false}";

                var response = Newtonsoft.Json.Linq.JObject.Parse(json);
                var hits = response["hits"] as Newtonsoft.Json.Linq.JArray;
                int totalHits = (int)(response["total_hits"] ?? 0);
                int totalPages = limit > 0 ? (int)Math.Ceiling((double)totalHits / limit) : 1;

                var items = new List<object>();
                if (hits != null)
                {
                    foreach (var hit in hits)
                    {
                        var versions = hit["versions"] as Newtonsoft.Json.Linq.JArray;
                        string latestVersion = "";
                        if (versions != null && versions.Count > 0)
                        {
                            var lastVer = versions[versions.Count - 1];
                            if (lastVer.Type == Newtonsoft.Json.Linq.JTokenType.String)
                            {
                                // 纯字符串版本号
                                latestVersion = lastVer.ToString();
                            }
                            else if (lastVer is Newtonsoft.Json.Linq.JObject vObj)
                            {
                                // 版本对象：优先取 version_number > name > id
                                latestVersion = (string)(vObj["version_number"] ?? vObj["name"] ?? vObj["id"] ?? "");
                            }
                        }

                        items.Add(new
                        {
                            name = (string)(hit["title"] ?? ""),
                            iconUrl = (string)(hit["icon_url"] ?? ""),
                            description = (string)(hit["description"] ?? ""),
                            version = latestVersion,
                            author = (string)(hit["author"] ?? ""),
                            downloads = (hit["downloads"] ?? 0).ToString(),
                            source = "BBSMC",
                            projectId = (string)(hit["slug"] ?? hit["project_id"] ?? ""),
                            projectType = (string)(hit["project_type"] ?? type ?? ""),
                            type = type ?? ""
                        });
                        if (items.Count >= limit) break;
                    }
                }

                bool hasMore = offset + items.Count < totalHits;
                return JsonSerializer.Serialize(new { items, hasMore, total = totalHits, totalPages });
            }
            catch (Exception ex)
            {
                Logger.Error("[BBSMC] API 请求失败: " + ex.Message);
                return "{\"items\":[],\"hasMore\":false}";
            }
        }

        public string GetDependenciesJson()
        {
            var items = new[]
            {
                new { name = "Forge", iconUrl = "https://files.minecraftforge.net/images/logo.png", description = "最流行的Mod加载器，支持广泛的Mod生态系统", version = "1.20.1-47.2.14", type = "Mod 加载器", compatible = "1.20.x", sourceUrl = "https://files.minecraftforge.net/" },
                new { name = "Fabric", iconUrl = "https://fabricmc.net/assets/logo.png", description = "轻量级Mod加载器，启动更快，Mod更小", version = "0.15.10", type = "Mod 加载器", compatible = "1.20.x", sourceUrl = "https://fabricmc.net/" },
                new { name = "Quilt", iconUrl = "https://quiltmc.org/assets/brand/logo.svg", description = "Fabric的社区驱动分支，支持更多现代特性", version = "0.19.2", type = "Mod 加载器", compatible = "1.20.x", sourceUrl = "https://quiltmc.org/" },
                new { name = "NeoForge", iconUrl = "https://neoforged.net/images/logo.png", description = "Forge的现代分支，支持最新游戏版本", version = "1.20.1-62.0.2", type = "Mod 加载器", compatible = "1.20.x", sourceUrl = "https://neoforged.net/" },
                new { name = "OptiFine", iconUrl = "https://optifine.net/logo.png", description = "性能优化和光影支持，提升游戏帧率", version = "HD U_G8", type = "优化", compatible = "1.20.x", sourceUrl = "https://optifine.net/" },
                new { name = "Sodium", iconUrl = "https://cdn.modrinth.com/data/AANobbMI/icon.png", description = "Fabric端的性能优化Mod，大幅提升帧率", version = "0.5.2", type = "优化", compatible = "1.20.x (Fabric)", sourceUrl = "https://modrinth.com/mod/sodium" },
                new { name = "Lithium", iconUrl = "https://cdn.modrinth.com/data/gvQqBUqZ/icon.png", description = "Fabric端的游戏逻辑优化，提升整体性能", version = "0.11.2", type = "优化", compatible = "1.20.x (Fabric)", sourceUrl = "https://modrinth.com/mod/lithium" },
                new { name = "Iris", iconUrl = "https://cdn.modrinth.com/data/YL57xq9U/icon.png", description = "现代光影加载器，兼容Sodium", version = "1.6.7", type = "渲染/图形", compatible = "1.20.x (Fabric)", sourceUrl = "https://irisshaders.dev/" }
            };
            return JsonSerializer.Serialize(new { items });
        }

        // ===== 模组管理 =====

        public string DetectModLoadersJson(string versionId)
        {
            try
            {
                var minecraftPath = AppContext.MinecraftPath ?? ".";
                var loaders = Core.ModLoaderDetector.DetectAllLoaders(minecraftPath, versionId);
                var list = loaders.Select(l => new
                {
                    versionId = l.VersionId ?? "",
                    loaderName = l.LoaderVersion ?? "",
                    displayName = l.DisplayName ?? "",
                    minecraftVersion = l.MinecraftVersion ?? ""
                }).ToList();
                return JsonSerializer.Serialize(new { loaders = list, hasLoader = loaders.Any() });
            }
            catch { return "{\"loaders\":[],\"hasLoader\":false}"; }
        }

        public string GetInstalledModsJson(string versionId, string contentType)
        {
            try
            {
                var minecraftPath = AppContext.MinecraftPath ?? ".";
                string dirName = contentType switch
                {
                    "shader" => "shaderpacks",
                    "resource" => "resourcepacks",
                    _ => "mods"
                };
                string ext = (contentType == "shader" || contentType == "resource") ? ".zip" : ".jar";

                string modsPath;
                if (!string.IsNullOrEmpty(versionId))
                {
                    modsPath = System.IO.Path.Combine(minecraftPath, "versions", versionId, dirName);
                    if (!System.IO.Directory.Exists(modsPath))
                        modsPath = System.IO.Path.Combine(minecraftPath, dirName);
                }
                else
                {
                    modsPath = System.IO.Path.Combine(minecraftPath, dirName);
                }

                if (!System.IO.Directory.Exists(modsPath))
                    return "{\"mods\":[]}";

                var files = System.IO.Directory.GetFiles(modsPath, "*" + ext);
                var list = files.Select(f =>
                {
                    var fileName = System.IO.Path.GetFileName(f);
                    bool enabled = !fileName.StartsWith(".");
                    string displayName = enabled ? fileName : fileName.Substring(1);
                    string iconBase64 = ExtractModIconBase64(f);
                    string version = GetVersionFromFileName(displayName);
                    return new
                    {
                        id = fileName.GetHashCode().ToString("X8"),
                        name = displayName.Replace(ext, ""),
                        fileName = fileName,
                        fullPath = f,
                        enabled = enabled,
                        version = version,
                        size = new System.IO.FileInfo(f).Length,
                        icon = iconBase64 != null ? "data:image/png;base64," + iconBase64 : null
                    };
                }).ToList();
                return JsonSerializer.Serialize(new { mods = list });
            }
            catch (Exception ex)
            {
                Logger.Error("[模组管理] 扫描失败: " + ex.Message);
                return "{\"mods\":[]}";
            }
        }

        private static string ExtractModIconBase64(string jarPath)
        {
            try
            {
                string iconDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(jarPath) ?? "", ".mod_icons");
                string iconName = System.IO.Path.GetFileNameWithoutExtension(jarPath) + ".png";
                string iconPath = System.IO.Path.Combine(iconDir, iconName);

                // 缓存命中
                if (System.IO.File.Exists(iconPath))
                    return System.Convert.ToBase64String(System.IO.File.ReadAllBytes(iconPath));

                using (var archive = System.IO.Compression.ZipFile.OpenRead(jarPath))
                {
                    string[] iconNames = {
                        "pack.png", "logo.png", "icon.png", "mod_icon.png",
                        "icon_32.png", "icon_64.png", "mod_logo.png",
                        "thumbnail.png", "texture.png"
                    };

                    // 根目录精确匹配
                    foreach (string name in iconNames)
                    {
                        var entry = archive.GetEntry(name);
                        if (entry != null && entry.Length > 0)
                            return SaveIcon(entry, iconDir, iconPath);
                    }

                    // 遍历所有条目模糊匹配（子目录等）
                    foreach (var entry in archive.Entries)
                    {
                        string entryName = System.IO.Path.GetFileName(entry.FullName);
                        foreach (string candidate in iconNames)
                        {
                            if (string.Equals(entryName, candidate, StringComparison.OrdinalIgnoreCase))
                                return SaveIcon(entry, iconDir, iconPath);
                        }
                    }

                    // 回退：找第一个较小 png（排除大纹理文件）
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Length < 256 * 1024 &&
                            entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            return SaveIcon(entry, iconDir, iconPath);
                        }
                    }
                }
            }
            catch { /* 静默失败 */ }
            return null;
        }

        private static string SaveIcon(System.IO.Compression.ZipArchiveEntry entry,
            string iconDir, string iconPath)
        {
            System.IO.Directory.CreateDirectory(iconDir);
            using (var src = entry.Open())
            using (var dst = System.IO.File.Create(iconPath))
                src.CopyTo(dst);
            return System.Convert.ToBase64String(System.IO.File.ReadAllBytes(iconPath));
        }

        private static string GetVersionFromFileName(string fileName)
        {
            try
            {
                // 常见版本号模式：x.x.x 或 x.x
                var match = System.Text.RegularExpressions.Regex.Match(
                    System.IO.Path.GetFileNameWithoutExtension(fileName),
                    @"\b(\d+\.\d+(?:\.\d+)?(?:[-+][\w.]+)?)\b");
                return match.Success ? match.Groups[1].Value : "";
            }
            catch { return ""; }
        }

        public string ToggleModJson(string modPath, bool enabled)
        {
            try
            {
                if (!System.IO.File.Exists(modPath))
                    return "{\"success\":false,\"error\":\"文件不存在\"}";

                var dir = System.IO.Path.GetDirectoryName(modPath);
                var fileName = System.IO.Path.GetFileName(modPath);
                string newPath;

                if (enabled)
                {
                    // Enable: remove leading dot
                    if (fileName.StartsWith("."))
                        newPath = System.IO.Path.Combine(dir, fileName.Substring(1));
                    else
                        return "{\"success\":true}";
                }
                else
                {
                    // Disable: add leading dot
                    if (!fileName.StartsWith("."))
                        newPath = System.IO.Path.Combine(dir, "." + fileName);
                    else
                        return "{\"success\":true}";
                }

                System.IO.File.Move(modPath, newPath);
                return JsonSerializer.Serialize(new { success = true, newPath = newPath });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        public string DeleteModJson(string modPath)
        {
            try
            {
                if (System.IO.File.Exists(modPath))
                {
                    System.IO.File.Delete(modPath);
                    return "{\"success\":true}";
                }
                return "{\"success\":false,\"error\":\"文件不存在\"}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        // ===== 存档管理 =====

        /// <summary>启动存档对应的游戏版本</summary>
        public async Task<string> LaunchSaveJson(string savePath)
        {
            try
            {
                var minecraftPath = ExpandEnv(AppContext.MinecraftPath ?? ".");
                string versionsDir = System.IO.Path.Combine(minecraftPath, "versions");

                // 查找存档所属的版本目录
                string saveDir = System.IO.Path.GetDirectoryName(savePath);
                string grandParent = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(saveDir));

                if (grandParent == versionsDir)
                {
                    // 版本隔离存档: versions/{ver}/game/saves/{saveName}
                    string versionId = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(saveDir)));
                    if (!string.IsNullOrEmpty(versionId) && System.IO.Directory.Exists(System.IO.Path.Combine(versionsDir, versionId)))
                    {
                        var javaConfig = new JavaConfigManager(minecraftPath);
                        var launcher = new GameLauncher(minecraftPath, javaConfig);
                        var username = AppContext.Username ?? "Player";
                        await launcher.LaunchGameAsync(versionId, username, 2048, 4096, offlineMode: AppContext.IsOfflineMode);
                        return JsonSerializer.Serialize(new { success = true, versionId, message = "已启动游戏版本: " + versionId });
                    }
                }

                // 全局存档: 尝试找到第一个已安装的 Java 版本启动
                if (System.IO.Directory.Exists(versionsDir))
                {
                    foreach (string verDir in System.IO.Directory.GetDirectories(versionsDir))
                    {
                        string vid = System.IO.Path.GetFileName(verDir);
                        if (new VersionManager(minecraftPath).IsJavaVersionValid(vid))
                        {
                            var javaConfig = new JavaConfigManager(minecraftPath);
                            var launcher = new GameLauncher(minecraftPath, javaConfig);
                            var username = AppContext.Username ?? "Player";
                            await launcher.LaunchGameAsync(vid, username, 2048, 4096, offlineMode: AppContext.IsOfflineMode);
                            return JsonSerializer.Serialize(new { success = true, versionId = vid, message = "已启动最近版本: " + vid });
                        }
                    }
                }

                return JsonSerializer.Serialize(new { success = false, error = "未找到可启动的游戏版本" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        public async Task<string> GetSavedWorldsJson()
        {
            return await Task.Run(() =>
            {
            try
            {
                var minecraftPath = ExpandEnv(AppContext.MinecraftPath ?? ".");
                var list = new List<object>();

                // 1. 版本隔离存档 (versions/{ver}/game/saves/)
                string versionsDir = System.IO.Path.Combine(minecraftPath, "versions");
                if (System.IO.Directory.Exists(versionsDir))
                {
                    foreach (string verDir in System.IO.Directory.GetDirectories(versionsDir))
                    {
                        string versionId = System.IO.Path.GetFileName(verDir);
                        string versionSavesDir = System.IO.Path.Combine(verDir, "game", "saves");
                        if (System.IO.Directory.Exists(versionSavesDir))
                        {
                            foreach (string saveDir in System.IO.Directory.GetDirectories(versionSavesDir))
                            {
                                string saveName = System.IO.Path.GetFileName(saveDir);
                                list.Add(new
                                {
                                    name = saveName + " (" + versionId + ")",
                                    path = saveDir.Replace("\\", "/"),
                                    size = GetDirectorySize(saveDir),
                                    sizeText = FormatSizeText(GetDirectorySize(saveDir)),
                                    playTime = GetPlayTime(saveDir),
                                    source = "version"
                                });
                            }
                        }
                    }
                }

                // 2. 全局存档 (saves/)
                string globalSavesDir = System.IO.Path.Combine(minecraftPath, "saves");
                if (System.IO.Directory.Exists(globalSavesDir))
                {
                    foreach (string saveDir in System.IO.Directory.GetDirectories(globalSavesDir))
                    {
                        string saveName = System.IO.Path.GetFileName(saveDir);
                        list.Add(new
                        {
                            name = saveName,
                            path = saveDir.Replace("\\", "/"),
                            size = GetDirectorySize(saveDir),
                            sizeText = FormatSizeText(GetDirectorySize(saveDir)),
                            playTime = GetPlayTime(saveDir),
                            source = "global"
                        });
                    }
                }

                return JsonSerializer.Serialize(new { saves = list, hasSaves = list.Any() });
            }
            catch (Exception ex)
            {
                Logger.Error("[存档] 扫描失败: " + ex.Message);
                return "{\"saves\":[],\"hasSaves\":false,\"error\":\"" + ex.Message.Replace("\"", "'") + "\"}";
            }
            });
        }

        public async Task<string> BackupSaveJson(string folderPath)
        {
            return await Task.Run(() =>
            {
            try
            {
                if (!System.IO.Directory.Exists(folderPath))
                    return "{\"success\":false,\"error\":\"存档不存在\"}";

                var minecraftPath = AppContext.MinecraftPath ?? ".";
                string backupsDir = System.IO.Path.Combine(minecraftPath, "backups");
                System.IO.Directory.CreateDirectory(backupsDir);

                string saveName = System.IO.Path.GetFileName(folderPath);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string zipPath = System.IO.Path.Combine(backupsDir, saveName + "_" + timestamp + ".zip");

                if (System.IO.File.Exists(zipPath))
                    System.IO.File.Delete(zipPath);

                System.IO.Compression.ZipFile.CreateFromDirectory(folderPath, zipPath);

                long zipSize = new System.IO.FileInfo(zipPath).Length;
                return JsonSerializer.Serialize(new { success = true, path = zipPath, name = System.IO.Path.GetFileName(zipPath), size = FormatSizeText(zipSize) });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
            });
        }

        public async Task<string> RenameSaveJson(string folderPath, string newName)
        {
            return await Task.Run(() =>
            {
            try
            {
                if (!System.IO.Directory.Exists(folderPath))
                    return "{\"success\":false,\"error\":\"存档不存在\"}";

                string parentDir = System.IO.Path.GetDirectoryName(folderPath);
                string newPath = System.IO.Path.Combine(parentDir, newName);

                if (System.IO.Directory.Exists(newPath))
                    return "{\"success\":false,\"error\":\"目标名称已存在\"}";

                System.IO.Directory.Move(folderPath, newPath);
                return JsonSerializer.Serialize(new { success = true, newPath = newPath });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
            });
        }

        public async Task<string> DeleteSaveJson(string folderPath)
        {
            return await Task.Run(() =>
            {
            try
            {
                if (!System.IO.Directory.Exists(folderPath))
                    return "{\"success\":false,\"error\":\"存档不存在\"}";

                System.IO.Directory.Delete(folderPath, true);
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
            });
        }

        private static long GetDirectorySize(string path)
        {
            try
            {
                return new System.IO.DirectoryInfo(path)
                    .EnumerateFiles("*.*", System.IO.SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch { return 0L; }
        }

        private static string FormatSizeText(long bytes)
        {
            if (bytes < 1024L) return bytes + " B";
            if (bytes < 1048576L) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1073741824L) return (bytes / 1048576.0).ToString("F1") + " MB";
            return (bytes / 1073741824.0).ToString("F2") + " GB";
        }

        public async Task<string> ImportSaveJson()
        {
            try
            {
                var minecraftPath = ExpandEnv(AppContext.MinecraftPath ?? ".");
                string savesPath = System.IO.Path.Combine(minecraftPath, "saves");

                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "存档压缩文件 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                    Title = "选择要导入的存档压缩文件"
                };

                bool? result = null;
                _window.Dispatcher.Invoke(() => result = dlg.ShowDialog());

                if (result != true || string.IsNullOrEmpty(dlg.FileName))
                    return "{\"success\":false,\"cancelled\":true}";

                string zipPath = dlg.FileName;
                string saveName = System.IO.Path.GetFileNameWithoutExtension(zipPath);
                string destPath = System.IO.Path.Combine(savesPath, saveName);

                if (System.IO.Directory.Exists(destPath))
                    return JsonSerializer.Serialize(new { success = false, error = "存档名称已存在: " + saveName });

                // 解压放后台线程，避免阻塞 UI
                await Task.Run(() =>
                {
                    System.IO.Directory.CreateDirectory(savesPath);
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, destPath);
                });

                return JsonSerializer.Serialize(new { success = true, name = saveName });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        private static string ExpandEnv(string path)
        {
            try { return Environment.ExpandEnvironmentVariables(path); }
            catch { return path; }
        }

        private static string GetPlayTime(string dirPath)
        {
            try
            {
                return System.IO.Directory.GetLastWriteTime(dirPath).ToString("yyyy-MM-dd HH:mm");
            }
            catch { return ""; }
        }

        // ===== 组件商店 =====

        public async Task<string> GetStoreComponentsJson(string category, string search, int page)
        {
            try
            {
                var service = Core.ComponentStoreService.Instance;
                Core.ComponentListResponse result;
                if (!string.IsNullOrEmpty(search))
                    result = await service.SearchComponentsAsync(search);
                else
                    result = await service.GetComponentsAsync(category ?? "", page > 0 ? page : 1);

                var list = result.components.Select(c =>
                {
                    string catName = c.category switch
                    {
                        "mod" => "模组",
                        "modpack" => "整合包",
                        "shader" => "光影",
                        "texture" => "材质",
                        "map" => "地图",
                        "resourcepack" => "资源包",
                        "datapack" => "数据包",
                        _ => c.category
                    };
                    string dl = c.download_count >= 1000000 ? (c.download_count / 1000000.0).ToString("F1") + "M"
                              : c.download_count >= 10000 ? (c.download_count / 1000.0).ToString("F1") + "K"
                              : c.download_count.ToString("N0");
                    return new
                    {
                        id = c.id,
                        name = c.name,
                        category = c.category,
                        catName = catName,
                        description = c.description,
                        icon = c.icon_emoji ?? "",
                        author = c.author ?? "",
                        downloadUrl = c.download_url ?? "",
                        rating = c.rating,
                        downloads = dl,
                        downloadCount = c.download_count,
                        mcVersion = c.mc_version ?? "",
                        fileSize = c.file_size
                    };
                }).ToList();

                return JsonSerializer.Serialize(new { components = list, total = result.total, page = result.page });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { components = new List<object>(), total = 0, page = 1, error = ex.Message });
            }
        }

        public string TrackStoreDownloadJson(int componentId)
        {
            try { _ = Core.ComponentStoreService.Instance.TrackDownloadAsync(componentId); return "{\"success\":true}"; }
            catch { return "{\"success\":false}"; }
        }

        /// <summary>获取背景列表（合并本地 + 服务器）</summary>
        public async Task<string> GetBackgroundsJson()
        {
            try
            {
                var allBackgrounds = new List<object>();

                // 本地背景
                var localItems = Core.BackgroundStoreService.Instance.Items;
                foreach (var b in localItems)
                {
                    allBackgrounds.Add(new
                    {
                        id = b.Id ?? b.Name ?? "",
                        name = b.Name ?? "",
                        filePath = b.FilePath ?? "",
                        serverId = b.ServerId,
                        date = b.AddedDate ?? "",
                        type = b.Type ?? "",
                        source = "local"
                    });
                }

                // 服务器背景（合并时去重：本地已有的 serverId 不再重复添加）
                var localServerIds = new HashSet<int>(localItems.Where(b => b.ServerId > 0).Select(b => b.ServerId));
                try
                {
                    var serverList = await Core.BackgroundServerService.Instance.GetBackgroundsAsync("", 1, 100);
                    if (serverList?.backgrounds != null)
                    {
                        foreach (var sb in serverList.backgrounds.Where(sb => !localServerIds.Contains(sb.id)))
                        {
                            allBackgrounds.Add(new
                            {
                                id = sb.id.ToString(),
                                name = sb.name ?? sb.file_name ?? "",
                                filePath = sb.download_url ?? "",
                                serverId = sb.id,
                                date = sb.created_at ?? "",
                                type = sb.type ?? "Image",
                                downloadCount = sb.download_count,
                                uploader = sb.uploader ?? "",
                                source = "server"
                            });
                        }
                    }
                }
                catch { /* 服务器不可达忽略 */ }

                return JsonSerializer.Serialize(new { backgrounds = allBackgrounds, total = allBackgrounds.Count });
            }
            catch { return "{\"backgrounds\":[],\"total\":0}"; }
        }

        /// <summary>上传背景（本地导入 + 服务器上传）</summary>
        public async Task<string> UploadBackgroundJson()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择背景素材",
                    Filter = "图片和视频 (*.jpg;*.jpeg;*.png;*.webp;*.mp4)|*.jpg;*.jpeg;*.png;*.webp;*.mp4|所有文件 (*.*)|*.*",
                    CheckFileExists = true
                };
                bool? result = null;
                _window.Dispatcher.Invoke(() => result = dlg.ShowDialog());
                if (result != true || string.IsNullOrEmpty(dlg.FileName))
                    return "{\"success\":false,\"cancelled\":true}";

                string displayName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);

                // 1. 本地导入
                var item = Core.BackgroundStoreService.Instance.Import(dlg.FileName, displayName);
                if (item == null)
                    return "{\"success\":false,\"error\":\"本地导入失败\"}";

                // 2. 上传到服务器
                string uploaderName = AppContext.QingniaoId ?? AppContext.QingniaoName ?? "";
                try
                {
                    var uploadResp = await Core.BackgroundServerService.Instance.UploadAsync(
                        dlg.FileName, displayName, uploaderName);
                    if (uploadResp != null && uploadResp.success)
                    {
                        item.ServerId = uploadResp.id;
                        item.Uploader = uploaderName;
                        Core.BackgroundStoreService.Instance.SaveManifest();
                        Debug.WriteLine($"[WebBridge] 背景上传服务器成功: id={uploadResp.id}, name={uploadResp.name}");
                    }
                    else
                    {
                        Debug.WriteLine($"[WebBridge] 背景上传服务器失败: {uploadResp?.error ?? "无响应"}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebBridge] 背景上传服务器异常: {ex.Message}");
                    // 本地导入已成功，服务器失败不阻塞
                }

                var fileName = System.IO.Path.GetFileName(item.FilePath);
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    name = item.Name ?? displayName,
                    fileName = fileName,
                    type = item.Type ?? "",
                    serverId = item.ServerId,
                    uploadedToServer = item.ServerId > 0
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] UploadBackgroundJson 异常: {ex.Message}");
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>使用背景</summary>
        public string UseBackgroundJson(string id)
        {
            try
            {
                var items = Core.BackgroundStoreService.Instance.Items;
                var found = items.FirstOrDefault(b => (b.Id ?? b.Name ?? "") == id);
                if (found != null)
                {
                    var fileName = Path.GetFileName(found.FilePath);
                    return JsonSerializer.Serialize(new { success = true, id = found.Id ?? found.Name, name = found.Name, fileName = fileName });
                }
                return "{\"success\":false,\"error\":\"未找到背景\"}";
            }
            catch { return "{\"success\":false}"; }
        }

        /// <summary>删除背景（本地 + 服务器）</summary>
        public async Task<string> DeleteBackgroundJson(string id)
        {
            try
            {
                var items = Core.BackgroundStoreService.Instance.Items;
                var found = items.FirstOrDefault(b => (b.Id ?? b.Name ?? "") == id);
                if (found == null)
                    return "{\"success\":false,\"error\":\"未找到背景\"}";

                // 1. 如果已上传服务器，从服务器删除
                if (found.ServerId > 0)
                {
                    try
                    {
                        await Core.BackgroundServerService.Instance.DeleteAsync(found.ServerId);
                        Debug.WriteLine($"[WebBridge] 背景从服务器删除: serverId={found.ServerId}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebBridge] 背景服务器删除失败: {ex.Message}");
                    }
                }

                // 2. 删除本地文件
                try { System.IO.File.Delete(found.FilePath); } catch { }

                // 3. 从集合移除
                items.Remove(found);
                Core.BackgroundStoreService.Instance.SaveManifest();
                return "{\"success\":true}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebBridge] DeleteBackgroundJson 异常: {ex.Message}");
                return "{\"success\":false,\"error\":\"" + ex.Message + "\"}";
            }
        }

        // ===== 个人主页 =====

        public string GetProfileJson()
        {
            try
            {
                // 优先从 AppContext 读取，然后 PCL.Settings，最后 SettingsManager（MNL主配置）兜底
                string qnId = !string.IsNullOrEmpty(AppContext.QingniaoId) ? AppContext.QingniaoId
                    : (PCL.Account.Settings.Get<string>("QingniaoId")
                       ?? SettingsManager.Settings.QingniaoId
                       ?? "");
                if (string.IsNullOrEmpty(qnId) || qnId == "0000000000") qnId = "--";

                string qnName = !string.IsNullOrEmpty(AppContext.QingniaoName) ? AppContext.QingniaoName
                    : (PCL.Account.Settings.Get<string>("QingniaoName")
                       ?? SettingsManager.Settings.QingniaoName
                       ?? "");
                if (string.IsNullOrEmpty(qnName)) qnName = AppContext.Username ?? "Player";

                bool hasQingNiao = qnId != "--" && qnId != "0000000000";

                // 验证登录缓存是否仍然有效（与 WPF RefreshQingNiaoInfo 一致）
                if (hasQingNiao)
                {
                    string cachedEmail = PCL.Account.Settings.Get<string>("CacheQingNiaoEmail")
                        ?? SettingsManager.Settings.QingniaoEmail
                        ?? "";
                    string cachedPass = Core.SecureStorage.Unprotect(PCL.Account.Settings.Get<string>("CacheQingNiaoPass")
                        ?? SettingsManager.Settings.QingniaoPass
                        ?? "");
                    if (!PCL.Account.AccountManager.IsServerCacheValid("QingNiao", cachedEmail, cachedPass))
                    {
                        // 缓存验证失败时，如果 SettingsManager 有持久化数据则仍视为有效
                        if (string.IsNullOrEmpty(SettingsManager.Settings.QingniaoId))
                        {
                            hasQingNiao = false;
                            qnId = "--";
                        }
                    }
                }

                string qnServer = "";
                try { qnServer = PCL.Account.AccountManager.GetQingNiaoServer()
                    ?? SettingsManager.Settings.QingniaoServer
                    ?? ""; } catch { }

                // 加载青鸟头像（优先从 avatars/{qingniaoId}.png 读取）
                string qingniaoAvatar = "";
                if (hasQingNiao)
                {
                    try
                    {
                        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                        string avatarFile = System.IO.Path.Combine(exeDir, "avatars", qnId + ".png");
                        if (System.IO.File.Exists(avatarFile))
                        {
                            byte[] bytes = System.IO.File.ReadAllBytes(avatarFile);
                            qingniaoAvatar = "data:image/png;base64," + Convert.ToBase64String(bytes);
                        }
                    }
                    catch { /* 加载头像失败时使用空值，前端显示默认图标 */ }
                }

                var obj = new
                {
                    username = hasQingNiao ? qnName : (AppContext.Username ?? "Player"),
                    qingniaoName = qnName,
                    qingniaoId = qnId,
                    qingniaoAvatar = qingniaoAvatar,
                    qingniaoServer = qnServer,
                    qingniaoEmail = PCL.Account.Settings.Get<string>("CacheQingNiaoEmail")
                        ?? SettingsManager.Settings.QingniaoEmail
                        ?? "",
                    isQingniaoLoggedIn = hasQingNiao,
                    isOffline = AppContext.IsOfflineMode,
                    loginMode = hasQingNiao ? "青鸟模式" : (AppContext.IsOfflineMode ? "离线模式" : "正版模式"),
                    playTime = 0,
                    launchCount = 0,
                    modCount = 0,
                    accounts = GetAccountList()
                };
                return JsonSerializer.Serialize(obj);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[WebBridge] GetProfileJson error: " + ex.Message);
                return "{}";
            }
        }

        public string GetAccountListJson()
        {
            try { return JsonSerializer.Serialize(GetAccountList()); }
            catch { return "[]"; }
        }

        private List<object> GetAccountList()
        {
            var list = new List<object>();
            var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddToList(string name, string type, string uuid, bool selected)
            {
                // 使用 名称+类型 作为去重 key，允许不同登录方式的同名账号共存
                string key = $"{name}|{type}";
                if (addedNames.Contains(key)) return;
                addedNames.Add(key);
                string head = GenerateAccountHeadBase64(uuid, name);
                list.Add(new { name = name, type = type, uuid = uuid, selected = selected, headImage = head });
            }

            try
            {
                // 优先从本地文件加载离线账号
                foreach (var item in LoadLocalAccounts())
                {
                    try
                    {
                        var obj = (System.Text.Json.JsonElement)item;
                        string name = obj.TryGetProperty("name", out var pn) ? pn.GetString() : "";
                        string type = obj.TryGetProperty("type", out var pt) ? pt.GetString() : "offline";
                        if (!string.IsNullOrEmpty(name))
                            AddToList(name, type, GenerateOfflineUuid(name), false);
                    }
                    catch (Exception ex) { Logger.Error("[GetAccountList] 本地账号加载失败: " + ex.Message); }
                }

                // 同时从 PCL 加载（兼容旧数据，去重）
                try
                {
                    foreach (string name in PCL.Account.AccountManager.GetLegacyAccounts())
                    {
                        try { AddToList(name, "offline", GenerateOfflineUuid(name), false); }
                        catch (Exception ex) { Logger.Error("[GetAccountList] PCL 离线账号 " + name + " 加载失败: " + ex.Message); }
                    }
                }
                catch { }

                foreach (var ms in PCL.Account.AccountManager.GetMsAccounts())
                {
                    try { AddToList(ms.UserName, "microsoft", ms.Uuid ?? "", false); }
                    catch (Exception ex) { Logger.Error("[GetAccountList] 微软账号 " + ms.UserName + " 加载失败: " + ex.Message); }
                }

                foreach (var record in PCL.Account.AccountManager.GetServerLoginRecords("Auth"))
                {
                    try { AddToList(record.Item1, "authlib", record.Item2 ?? "", false); }
                    catch (Exception ex) { Logger.Error("[GetAccountList] 外置账号 " + record.Item1 + " 加载失败: " + ex.Message); }
                }

            }
            catch (Exception ex) { Logger.Error("[GetAccountList] 账号列表加载失败: " + ex.Message); }
            return list;
        }

        /// <summary>生成离线账号 UUID（与 SkinServer.GenerateOfflineUuid 一致）</summary>
        private static string GenerateOfflineUuid(string name)
        {
            try { return MusicalNoteLauncher.Core.SkinServer.GenerateOfflineUuid(name ?? ""); }
            catch { return Guid.NewGuid().ToString("N"); }
        }

        /// <summary>为游戏账号生成 Steve 风格头雕（base64 PNG data URL）</summary>
        private static string GenerateAccountHeadBase64(string uuid, string name)
        {
            try
            {
                // 1. 优先从 skins/{uuid}.png 或 skins/{name}.png 加载自定义皮肤
                string skinsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");
                string skinPath = null;
                if (!string.IsNullOrEmpty(uuid))
                {
                    string p = System.IO.Path.Combine(skinsDir, uuid + ".png");
                    if (System.IO.File.Exists(p)) skinPath = p;
                }
                if (skinPath == null && !string.IsNullOrEmpty(name))
                {
                    string p = System.IO.Path.Combine(skinsDir, name + ".png");
                    if (System.IO.File.Exists(p)) skinPath = p;
                }

                if (skinPath != null)
                {
                    using (var fs = new System.IO.FileStream(skinPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                            fs, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                        var frame = decoder.Frames[0];
                        int w = frame.PixelWidth;
                        int h = frame.PixelHeight;
                        bool looksLikeSkin = (w >= 48) && (h >= 16) && (w == h || w == 2 * h);
                        if (looksLikeSkin)
                        {
                            var formatted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                                frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                            formatted.Freeze();
                            return BitmapSourceToBase64Png(ComposeHeadFront(formatted));
                        }
                        // 非皮肤格式 → 直接返回原图（裁剪为方形）
                        return BitmapSourceToBase64Png(frame);
                    }
                }

                // 2. 没有皮肤文件 → 生成默认 Steve 头雕
                var steve = Core.DefaultSkinFactory.GetDefaultSkinBitmap(false); // Steve 皮肤
                return BitmapSourceToBase64Png(ComposeHeadFront(steve));
            }
            catch
            {
                return ""; // 失败时返回空，前端显示 👤 emoji
            }
        }

        /// <summary>BitmapSource → base64 PNG data URL</summary>
        private static string BitmapSourceToBase64Png(System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                enc.Save(ms);
                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>
        /// 从 Minecraft 皮肤提取头部正面 8x8 → 32x32 像素化放大（与 WPF 逻辑一致）
        /// </summary>
        private static System.Windows.Media.Imaging.BitmapSource ComposeHeadFront(System.Windows.Media.Imaging.BitmapSource skin)
        {
            const int headSize = 8;
            const int outSize = 32;
            int w = skin.PixelWidth;
            int h = skin.PixelHeight;
            double scale = w / 64.0;

            byte[] src = new byte[w * h * 4];
            skin.CopyPixels(src, w * 4, 0);

            var head = new byte[headSize * headSize * 4];

            // 1. 内层脸部正面（坐标 x=8..15, y=8..15）
            for (int y = 0; y < headSize; y++)
                for (int x = 0; x < headSize; x++)
                {
                    int sx = (int)Math.Floor(8.0 * scale + (x + 0.5) * scale);
                    int sy = (int)Math.Floor(8.0 * scale + (y + 0.5) * scale);
                    sx = Math.Max(0, Math.Min(sx, w - 1));
                    sy = Math.Max(0, Math.Min(sy, h - 1));
                    int si = (sy * w + sx) * 4;
                    int di = (y * headSize + x) * 4;
                    head[di + 0] = src[si + 0];
                    head[di + 1] = src[si + 1];
                    head[di + 2] = src[si + 2];
                    head[di + 3] = src[si + 3];
                }

            // 2. 外层帽子（坐标 x=40..47, y=8..15）—— 非透明像素覆盖
            for (int y = 0; y < headSize; y++)
                for (int x = 0; x < headSize; x++)
                {
                    int sx = (int)Math.Floor(40.0 * scale + (x + 0.5) * scale);
                    int sy = (int)Math.Floor(8.0 * scale + (y + 0.5) * scale);
                    sx = Math.Max(0, Math.Min(sx, w - 1));
                    sy = Math.Max(0, Math.Min(sy, h - 1));
                    int si = (sy * w + sx) * 4;
                    if (src[si + 3] == 0) continue;
                    int di = (y * headSize + x) * 4;
                    head[di + 0] = src[si + 0];
                    head[di + 1] = src[si + 1];
                    head[di + 2] = src[si + 2];
                    head[di + 3] = src[si + 3];
                }

            // 3. 8×8 → 32×32 最近邻放大
            var outPixels = new byte[outSize * outSize * 4];
            int ratio = outSize / headSize;
            for (int y = 0; y < outSize; y++)
                for (int x = 0; x < outSize; x++)
                {
                    int srcIdx = ((y / ratio) * headSize + (x / ratio)) * 4;
                    int dstIdx = (y * outSize + x) * 4;
                    outPixels[dstIdx + 0] = head[srcIdx + 0];
                    outPixels[dstIdx + 1] = head[srcIdx + 1];
                    outPixels[dstIdx + 2] = head[srcIdx + 2];
                    outPixels[dstIdx + 3] = head[srcIdx + 3];
                }

            return System.Windows.Media.Imaging.BitmapSource.Create(
                outSize, outSize, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, outPixels, outSize * 4);
        }

        /// <summary>上传皮肤（打开文件对话框 → 保存到 skins/{uuid}.png → 返回新头雕）</summary>
        public string UploadAccountSkinJson(string uuid)
        {
            try
            {
                if (string.IsNullOrEmpty(uuid))
                    return "{\"success\":false,\"error\":\"UUID 不能为空\"}";

                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择 Minecraft 皮肤图片",
                    Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*"
                };
                bool? result = null;
                _window.Dispatcher.Invoke(() => result = dlg.ShowDialog());
                if (result != true || string.IsNullOrEmpty(dlg.FileName))
                    return "{\"success\":false,\"cancelled\":true}";

                // 保存到 skins 目录
                string skinsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");
                if (!System.IO.Directory.Exists(skinsDir))
                    System.IO.Directory.CreateDirectory(skinsDir);

                string destFile = System.IO.Path.Combine(skinsDir, uuid + ".png");
                string ext = System.IO.Path.GetExtension(dlg.FileName)?.ToLower() ?? "";

                if (ext == ".png")
                {
                    System.IO.File.Copy(dlg.FileName, destFile, true);
                }
                else
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    using (var src = new System.IO.FileStream(dlg.FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.StreamSource = src;
                        bmp.EndInit();
                        bmp.Freeze();
                    }
                    using (var fs = new System.IO.FileStream(destFile, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                    {
                        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                        enc.Save(fs);
                    }
                }

                // 重新生成头雕
                string headBase64 = GenerateAccountHeadBase64(uuid, null);
                PostMessage("toast", new { message = "皮肤已更新", type = "success" });
                return JsonSerializer.Serialize(new { success = true, headImage = headBase64 });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>刷新单个账号头雕</summary>
        public string RefreshAccountHeadJson(string uuid, string name)
        {
            try
            {
                string head = GenerateAccountHeadBase64(uuid ?? "", name ?? "");
                return JsonSerializer.Serialize(new { success = true, headImage = head });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>保存皮肤文件（Web 端上传，base64 格式）</summary>
        public string SaveSkinBase64Json(string uuid, string accountName, string base64Data, bool isSlim)
        {
            try
            {
                if (string.IsNullOrEmpty(uuid))
                    return "{\"success\":false,\"error\":\"账号 UUID 不能为空\"}";

                string skinsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skins");
                if (!System.IO.Directory.Exists(skinsDir))
                    System.IO.Directory.CreateDirectory(skinsDir);
                string skinFile = System.IO.Path.Combine(skinsDir, uuid + ".png");

                // 恢复默认皮肤：删除文件
                if (string.IsNullOrEmpty(base64Data) || base64Data == "default")
                {
                    if (System.IO.File.Exists(skinFile))
                        System.IO.File.Delete(skinFile);
                    PostMessage("toast", new { message = "已恢复默认皮肤", type = "success" });
                }
                else
                {
                    // 处理 data URL 前缀
                    string data = base64Data;
                    if (data.StartsWith("data:image/"))
                    {
                        int commaIdx = data.IndexOf(',');
                        if (commaIdx > 0) data = data.Substring(commaIdx + 1);
                    }
                    byte[] bytes = Convert.FromBase64String(data);
                    System.IO.File.WriteAllBytes(skinFile, bytes);
                    PostMessage("toast", new { message = "皮肤已更新（" + (isSlim ? "Alex" : "Steve") + " 模型）", type = "success" });
                }

                // 保存模型类型（与 WPF 逻辑一致）
                try { PCL.Account.Settings.Set("SkinSlim_" + uuid, isSlim); } catch { }

                // 重新生成头雕
                string headImage = GenerateAccountHeadBase64(uuid, accountName);
                return JsonSerializer.Serialize(new { success = true, headImage = headImage });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        // ===== 本地账号文件持久化 =====

        private static string GetAccountsFilePath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game_accounts.json");
        }

        private static List<object> LoadLocalAccounts()
        {
            try
            {
                string path = GetAccountsFilePath();
                if (!System.IO.File.Exists(path)) return new List<object>();
                string json = System.IO.File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<object>>(json) ?? new List<object>();
            }
            catch { return new List<object>(); }
        }

        private static void SaveLocalAccounts(List<object> accounts)
        {
            try
            {
                string path = GetAccountsFilePath();
                string json = JsonSerializer.Serialize(accounts);
                System.IO.File.WriteAllText(path, json);
            }
            catch (Exception ex) { Logger.Error("[SaveLocalAccounts] " + ex.Message); }
        }

        private static void AddLocalAccount(string name, string type)
        {
            var list = LoadLocalAccounts();
            var existing = list.FirstOrDefault(a =>
            {
                var obj = (System.Text.Json.JsonElement)a;
                string n = obj.TryGetProperty("name", out var pn) ? pn.GetString() : "";
                return n.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                       (obj.TryGetProperty("type", out var pt) ? pt.GetString() : "") == type;
            });
            if (existing == null)
            {
                list.Add(new { name = name, type = type });
                SaveLocalAccounts(list);
            }
        }

        /// <summary>将微软账号持久化到 game_accounts.json（双重保险）</summary>
        public static void SaveMicrosoftAccount(string name)
        {
            AddLocalAccount(name, "microsoft");
        }

        private static void RemoveLocalAccount(string name)
        {
            var list = LoadLocalAccounts();
            list.RemoveAll(a =>
            {
                var obj = (System.Text.Json.JsonElement)a;
                string n = obj.TryGetProperty("name", out var pn) ? pn.GetString() : "";
                return n.Equals(name, StringComparison.OrdinalIgnoreCase);
            });
            SaveLocalAccounts(list);
        }

        public string AddOfflineAccountJson(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return "{\"success\":false,\"error\":\"名称不能为空\"}";
                string trimmed = name.Trim();
                if (trimmed.Length < 3 || trimmed.Length > 16)
                    return "{\"success\":false,\"error\":\"用户名长度需要在3-16个字符之间\"}";

                // 检查重复（本地文件 + 微软账号）
                var localAccounts = LoadLocalAccounts();
                bool exists = localAccounts.Any(a =>
                {
                    var obj = (System.Text.Json.JsonElement)a;
                    string n = obj.TryGetProperty("name", out var pn) ? pn.GetString() : "";
                    return n.Equals(trimmed, StringComparison.OrdinalIgnoreCase);
                });
                if (exists)
                    return "{\"success\":false,\"error\":\"已存在同名离线账号\"}";

                // 也检查微软账号是否已有同名
                try
                {
                    var msAccounts = PCL.Account.AccountManager.GetMsAccounts();
                    if (msAccounts.Any(m => m.UserName.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
                        return "{\"success\":false,\"error\":\"该名称已被微软账号使用，请更换名称\"}";
                }
                catch { }

                // 保存到本地文件 + PCL（双写）
                AddLocalAccount(trimmed, "offline");
                try { PCL.Account.AccountManager.SaveLegacyLogin(trimmed); } catch { }

                PostMessage("toast", new { message = "离线账号 " + trimmed + " 已添加", type = "success" });
                return "{\"success\":true}";
            }
            catch (Exception ex) { return JsonSerializer.Serialize(new { success = false, error = ex.Message }); }
        }

        public string DeleteAccountJson(string type, string name)
        {
            try
            {
                // 本地文件删除
                RemoveLocalAccount(name);

                // PCL 删除（兼容）
                try
                {
                    if (type == "offline")
                        PCL.Account.AccountManager.RemoveLegacyAccount(name);
                    else if (type == "microsoft")
                        PCL.Account.AccountManager.RemoveMsAccount(name);
                    else if (type == "authlib")
                        PCL.Account.AccountManager.RemoveServerAccount("Auth", name);
                }
                catch { }

                PostMessage("toast", new { message = "账号 " + name + " 已删除", type = "success" });
                return "{\"success\":true}";
            }
            catch (Exception ex) { return JsonSerializer.Serialize(new { success = false, error = ex.Message }); }
        }

        public void ProfileLogout()
        {
            _window.Dispatcher.Invoke(() =>
            {
                // 先保存 ID 用于删除头像（后面的清除操作会清空它）
                string uidToDelete = AppContext.QingniaoId ?? "";
                AppContext.QingniaoId = "";
                AppContext.QingniaoName = "";
                // 删除头像文件
                try
                {
                    if (!string.IsNullOrEmpty(uidToDelete))
                    {
                        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                        string avatarFile = System.IO.Path.Combine(exeDir, "avatars", uidToDelete + ".png");
                        if (System.IO.File.Exists(avatarFile)) System.IO.File.Delete(avatarFile);
                    }
                }
                catch { }
                PostMessage("toast", new { message = "已登出青鸟账号", type = "success" });
            });
        }

        public string SetQingniaoNameJson(string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return "{\"success\":false,\"error\":\"名称不能为空\"}";
                var trimmed = newName.Trim();
                if (trimmed.Length < 2 || trimmed.Length > 24)
                    return "{\"success\":false,\"error\":\"名字长度应在2-24个字符之间\"}";
                AppContext.SetQingniaoName(trimmed);
                PostMessage("toast", new { message = "青鸟名字已更新为: " + trimmed, type = "success" });
                return "{\"success\":true,\"name\":\"" + trimmed.Replace("\"", "\\\"") + "\"}";
            }
            catch (Exception ex) { return JsonSerializer.Serialize(new { success = false, error = ex.Message }); }
        }

        public string UploadQingniaoAvatarJson()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择头像图片",
                    Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*"
                };
                bool? result = null;
                _window.Dispatcher.Invoke(() => result = dlg.ShowDialog());
                if (result != true || string.IsNullOrEmpty(dlg.FileName))
                    return "{\"success\":false,\"cancelled\":true}";

                string uid = string.IsNullOrEmpty(AppContext.QingniaoId) ? "player" : AppContext.QingniaoId;
                string avatarsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "avatars");
                if (!System.IO.Directory.Exists(avatarsDir))
                    System.IO.Directory.CreateDirectory(avatarsDir);

                string targetFile = System.IO.Path.Combine(avatarsDir, uid + ".png");

                // 加载图片并统一缩放至 256x256，减小体积
                var srcImage = new System.Windows.Media.Imaging.BitmapImage();
                using (var src = new System.IO.FileStream(dlg.FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    srcImage.BeginInit();
                    srcImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    srcImage.StreamSource = src;
                    srcImage.EndInit();
                    srcImage.Freeze();
                }

                const int maxSize = 256;
                int w = srcImage.PixelWidth;
                int h = srcImage.PixelHeight;
                double scale = Math.Min((double)maxSize / w, (double)maxSize / h);
                if (scale < 1.0)
                {
                    w = (int)(w * scale);
                    h = (int)(h * scale);
                }

                var resized = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                var visual = new System.Windows.Media.DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawImage(srcImage, new System.Windows.Rect(0, 0, w, h));
                }
                resized.Render(visual);
                resized.Freeze();

                using (var fs = new System.IO.FileStream(targetFile, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                {
                    var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(resized));
                    enc.Save(fs);
                }

                byte[] bytes = System.IO.File.ReadAllBytes(targetFile);
                string base64 = "data:image/png;base64," + Convert.ToBase64String(bytes);

                // 异步上传到服务器，完成后通过 PostMessage 通知前端
                string capturedUid = uid;
                string capturedBase64 = base64;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var (uploaded, errorMsg) = await Core.CommunityService.Instance.UploadAvatarAsync(capturedUid, capturedBase64);
                        if (uploaded)
                        {
                            Logger.Info("[社区] 头像已同步到服务器");
                            PostMessage("toast", new { message = "头像已同步到服务器", type = "success" });
                        }
                        else
                        {
                            Logger.Error($"[社区] 头像同步失败: {errorMsg}");
                            PostMessage("toast", new { message = "头像同步失败：" + errorMsg, type = "error" });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("[社区] 头像同步异常: " + ex.Message);
                        PostMessage("toast", new { message = "头像同步异常：" + ex.Message, type = "error" });
                    }
                });

                return JsonSerializer.Serialize(new { success = true, avatar = base64 });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        public void SwitchProfileAccount(string type)
        {
            try
            {
                if (type == "offline")
                    AppContext.IsOfflineMode = true;
                else
                    AppContext.IsOfflineMode = false;
                PostMessage("toast", new { message = "账号已切换，请重启启动器生效", type = "info" });
            }
            catch { }
        }

        // ===== 音乐 =====

        public async Task<object> SearchMusic(string query)
        {
            try
            {
                var service = new Core.MusicApiService();
                var result = await service.SearchSongsAsync(query, 30, 0);
                if (result?.Songs == null) return Array.Empty<object>();
                return result.Songs.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    title = t.Name,
                    artist = t.Artist,
                    duration = t.DurationText
                }).ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        /// <summary>搜索歌曲，返回 JSON 字符串</summary>
        public async Task<string> SearchMusicJson(string query)
        {
            try
            {
                var service = new Core.MusicApiService();
                var result = await service.SearchSongsAsync(query, 30, 0);
                if (result?.Songs == null) return "[]";
                var arr = result.Songs.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    title = t.Name,
                    artist = t.Artist,
                    duration = t.DurationText,
                    coverUrl = t.AlbumCoverUrl
                }).ToArray();
                return JsonSerializer.Serialize(arr);
            }
            catch { return "[]"; }
        }

        public async Task<object> SearchMusicPlaylists(string query)
        {
            try
            {
                var service = new Core.MusicApiService();
                var playlists = string.IsNullOrEmpty(query)
                    ? await service.GetRecommendPlaylistsAsync(12)
                    : await service.SearchPlaylistsAsync(query, 20);
                if (playlists == null) return Array.Empty<object>();
                return playlists.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    desc = p.Description,
                    coverUrl = p.CoverUrl,
                    trackCount = p.TrackCount
                }).ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        /// <summary>搜索歌单，返回 JSON 字符串（避免 COM 对象传递到 Vue 响应式系统）</summary>
        public async Task<string> SearchMusicPlaylistsJson(string query)
        {
            try
            {
                var service = new Core.MusicApiService();
                var playlists = string.IsNullOrEmpty(query)
                    ? await service.GetRecommendPlaylistsAsync(12)
                    : await service.SearchPlaylistsAsync(query, 20);
                if (playlists == null) return "[]";
                var result = playlists.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    desc = p.Description,
                    coverUrl = p.CoverUrl,
                    trackCount = p.TrackCount
                }).ToArray();
                return JsonSerializer.Serialize(result);
            }
            catch { return "[]"; }
        }

        public async Task<object> GetMusicPlaylistDetail(string id)
        {
            try
            {
                var service = new Core.MusicApiService();
                var tracks = await service.GetPlaylistTracksAsync(id, 50);
                if (tracks == null) return new { tracks = Array.Empty<object>() };
                return new
                {
                    tracks = tracks.Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        title = t.Name,
                        artist = t.Artist,
                        duration = t.DurationText
                    }).ToArray()
                };
            }
            catch { return new { tracks = Array.Empty<object>() }; }
        }

        /// <summary>获取歌单详情，返回 JSON 字符串</summary>
        public async Task<string> GetMusicPlaylistDetailJson(string id)
        {
            try
            {
                var service = new Core.MusicApiService();
                var tracks = await service.GetPlaylistTracksAsync(id, 50);
                if (tracks == null) return "[]";
                var arr = tracks.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    title = t.Name,
                    artist = t.Artist,
                    duration = t.DurationText,
                    coverUrl = t.AlbumCoverUrl
                }).ToArray();
                return JsonSerializer.Serialize(arr);
            }
            catch { return "[]"; }
        }

        public string PlayMusic(string trackId) { return "not_implemented"; }
        public string PauseMusic() { return "not_implemented"; }
        public string ResumeMusic() { return "not_implemented"; }

        /// <summary>获取歌曲播放 URL</summary>
        public async Task<string?> GetSongUrlJson(string trackId)
        {
            try
            {
                var service = new Core.MusicApiService();
                return await service.GetSongUrlAsync(trackId);
            }
            catch { return null; }
        }

        public async Task<object> GetMusicPlaylist()
        {
            try
            {
                var service = new Core.MusicApiService();
                var playlists = await service.GetRecommendPlaylistsAsync(10);
                if (playlists == null) return Array.Empty<object>();
                return playlists.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    desc = p.Description,
                    coverUrl = p.CoverUrl,
                    trackCount = p.TrackCount
                }).ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        // ===== AI =====

        private Core.AIAgentService _aiAgent;
        private Core.AIAgentService AiAgent
        {
            get
            {
                if (_aiAgent == null)
                {
                    string mcPath = AppContext.MinecraftPath ?? ".";
                    _aiAgent = new Core.AIAgentService(mcPath);
                }
                return _aiAgent;
            }
        }

        private Core.AiAgentConfig GetAiConfig(string model = null)
        {
            return new Core.AiAgentConfig
            {
                Endpoint = NormalizeAiEndpoint(SettingsManager.Settings.AiEndpoint),
                ApiKey = SettingsManager.Settings.AiApiKey ?? "",
                Model = string.IsNullOrEmpty(model) ? (SettingsManager.Settings.AiModel ?? "gpt-3.5-turbo") : model
            };
        }

        private string NormalizeAiEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                return "";
            endpoint = endpoint.TrimEnd('/');
            if (endpoint.EndsWith("/chat/completions"))
                return endpoint;
            if (!endpoint.EndsWith("/v1"))
                endpoint += "/v1";
            return endpoint + "/chat/completions";
        }

        public async Task<string> AIChat(string message, string model)
        {
            try
            {
                Logger.Info($"[AI] AIChat 被调用 — message={message?.Substring(0, Math.Min(message?.Length ?? 0, 50))}, model={model}");

                var config = GetAiConfig(model);
                if (string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.ApiKey))
                    return "⚠️ 请先配置 API 接口地址和 API Key。点击右上角 ⚙️ 按钮进行配置。";
                if (string.IsNullOrEmpty(config.Model))
                    return "⚠️ 请先配置模型名称。";

                // 订阅工具进度事件，实时推送给前端
                void OnToolStarted(string name, string args) =>
                    PostMessage("ai-tool", new { action = "started", tool = ToolLabel(name), args });

                void OnToolCompleted(AgentToolResult result) =>
                    PostMessage("ai-tool", new { action = "completed", tool = ToolLabel(result.Name), success = result.Success, message = result.Message });

                void OnToolProgress(string name, string msg) =>
                    PostMessage("ai-tool", new { action = "progress", tool = ToolLabel(name), status = msg });

                AiAgent.ToolCallStarted += OnToolStarted;
                AiAgent.ToolCallCompleted += OnToolCompleted;
                AiAgent.ToolProgressChanged += OnToolProgress;

                try
                {
                    Logger.Info($"[AI] 调用 SendMessageAsync — endpoint={config.Endpoint}, model={config.Model}");
                    var result = await AiAgent.SendMessageAsync(config, message);
                    Logger.Info($"[AI] SendMessageAsync 返回 — result={result?.Substring(0, Math.Min(result?.Length ?? 0, 50))}");
                    PostMessage("ai-tool", new { action = "done" });
                    return result;
                }
                finally
                {
                    AiAgent.ToolCallStarted -= OnToolStarted;
                    AiAgent.ToolCallCompleted -= OnToolCompleted;
                    AiAgent.ToolProgressChanged -= OnToolProgress;
                }
            }
            catch (OperationCanceledException)
            {
                PostMessage("ai-tool", new { action = "cancelled" });
                return "⚠️ 操作已取消。";
            }
            catch (Exception ex)
            {
                PostMessage("ai-tool", new { action = "error", message = ex.Message });
                Logger.Error($"[AI] 聊天异常: {ex.Message}");
                return $"❌ 发生错误: {ex.Message}";
            }
        }

        /// <summary>
        /// 取消当前 AI 操作（取消 CancellationTokenSource）
        /// </summary>
        public void CancelAI()
        {
            try
            {
                AiAgent.Cancel();
                PostMessage("ai-tool", new { action = "cancelled" });
                Logger.Info("[AI] 用户取消了操作");
            }
            catch (Exception ex)
            {
                Logger.Error($"[AI] 取消失败: {ex.Message}");
            }
        }

        /// <summary>将工具内部名转为中文显示名</summary>
        private static string ToolLabel(string name) => name switch
        {
            "download_version" => "📥 下载游戏版本",
            "launch_game" => "🚀 启动游戏",
            "list_versions" => "📋 查看版本",
            "install_loader" => "🔧 安装加载器",
            "download_mod" => "📦 下载模组",
            "search_versions" => "🔍 搜索版本",
            "delete_version" => "🗑️ 删除版本",
            "system_info" => "💻 系统诊断",
            "list_mods" => "📋 模组列表",
            "navigate_to" => "🧭 界面导航",
            "click_button" => "👆 模拟点击",
            "type_text" => "⌨️ 输入文字",
            "scroll_to" => "📜 滚动页面",
            _ => $"🔨 {name}"
        };

        public object GetAIModels()
        {
            try
            {
                string endpoint = SettingsManager.Settings.AiEndpoint;
                string apiKey = SettingsManager.Settings.AiApiKey;

                if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
                    return Array.Empty<object>();

                // 从 endpoint 推导 models 地址
                string modelsUrl = endpoint.TrimEnd('/');
                if (modelsUrl.EndsWith("/chat/completions"))
                    modelsUrl = modelsUrl.Substring(0, modelsUrl.Length - "/chat/completions".Length);
                if (!modelsUrl.EndsWith("/models"))
                {
                    if (!modelsUrl.EndsWith("/v1"))
                        modelsUrl += "/v1";
                    modelsUrl += "/models";
                }

                var models = new List<object>();
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                    var response = client.GetAsync(modelsUrl).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        string body = response.Content.ReadAsStringAsync().Result;
                        using (var doc = System.Text.Json.JsonDocument.Parse(body))
                        {
                            if (doc.RootElement.TryGetProperty("data", out var data))
                            {
                                foreach (var item in data.EnumerateArray())
                                {
                                    if (item.TryGetProperty("id", out var idProp))
                                    {
                                        models.Add(new { id = idProp.GetString() });
                                    }
                                }
                            }
                        }
                    }
                }
                return models.ToArray();
            }
            catch
            {
                return Array.Empty<object>();
            }
        }

        public string GetAIModelsJson()
        {
            return JsonSerializer.Serialize(GetAIModels());
        }

        /// <summary>
        /// 保存 AI 配置
        /// </summary>
        public string SaveAIConfig(string endpoint, string apiKey, string model, string preset)
        {
            try
            {
                SettingsManager.Settings.AiEndpoint = endpoint ?? "";
                SettingsManager.Settings.AiApiKey = apiKey ?? "";
                SettingsManager.Settings.AiModel = model ?? "";
                SettingsManager.Settings.AiPreset = preset ?? "";
                SettingsManager.SaveSettings();
                // 清空历史记录，新配置新对话
                AiAgent.ClearHistory();
                return "ok";
            }
            catch (Exception ex)
            {
                Logger.Error($"[AI] 保存配置失败: {ex.Message}");
                return ex.Message;
            }
        }

        /// <summary>
        /// 获取 AI 配置
        /// </summary>
        public object GetAIConfig()
        {
            return new
            {
                endpoint = SettingsManager.Settings.AiEndpoint ?? "",
                apiKey = SettingsManager.Settings.AiApiKey ?? "",
                model = SettingsManager.Settings.AiModel ?? "",
                preset = SettingsManager.Settings.AiPreset ?? "",
                configured = !string.IsNullOrEmpty(SettingsManager.Settings.AiApiKey)
            };
        }

        public string GetAIConfigJson()
        {
            return JsonSerializer.Serialize(GetAIConfig());
        }

        /// <summary>
        /// 清空 AI 对话历史
        /// </summary>
        public string ClearAIHistory()
        {
            AiAgent.ClearHistory();
            return "ok";
        }

        // ===== 文件对话框 =====

        public string ShowOpenFileDialog(string filters)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = filters ?? "所有文件|*.*"
                };
                var result = _window.Dispatcher.Invoke(() => dialog.ShowDialog());
                return result == true ? dialog.FileName : "";
            }
            catch { return ""; }
        }

        public string ShowOpenFolderDialog()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "选择文件夹",
                    Multiselect = false
                };
                var result = _window.Dispatcher.Invoke(() => dialog.ShowDialog());
                return result == true ? dialog.FolderName : "";
            }
            catch { return ""; }
        }

        /// <summary>获取游戏目录列表 JSON（供前端设置页面渲染）</summary>
        public string GetGameFoldersJson()
        {
            try
            {
                var s = SettingsManager.Settings;
                if (s.GameFolders == null || s.GameFolders.Count == 0)
                    s.InitializeDefaultFolders();

                var list = s.GameFolders.Select((f, i) => new
                {
                    name = f.Name,
                    path = f.Path,
                    isSelected = (i == s.SelectedGameFolderIndex)
                }).ToArray();

                return JsonSerializer.Serialize(list);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new object[0]);
            }
        }

        /// <summary>保存选中的游戏目录索引，并切换活动路径、通知前端刷新</summary>
        public void SelectGameFolder(int index)
        {
            try
            {
                var s = SettingsManager.Settings;
                if (s.GameFolders != null && index >= 0 && index < s.GameFolders.Count)
                {
                    s.SelectedGameFolderIndex = index;
                    SettingsManager.SaveSettings();

                    // 切换到对应的游戏目录，触发 GameFolderChanged → VersionScanService 重新扫描
                    string selectedPath = s.GameFolders[index].Path;
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        string expanded = Environment.ExpandEnvironmentVariables(selectedPath);
                        AppContext.MinecraftPath = expanded;
                    }

                    // 通知前端刷新版本列表
                    PostMessage("mnl:versions-changed", new { });
                }
            }
            catch { }
        }

        /// <summary>添加游戏目录</summary>
        public string AddGameFolderJson(string folderPath, string displayName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(displayName))
                    return JsonSerializer.Serialize(new { success = false, error = "参数无效" });

                var s = SettingsManager.Settings;
                s.GameFolders ??= new System.Collections.Generic.List<GameFolderEntry>();
                string normalized = Environment.ExpandEnvironmentVariables(folderPath).TrimEnd('\\') + "\\";

                for (int i = 0; i < s.GameFolders.Count; i++)
                {
                    if (string.Equals(s.GameFolders[i].NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        s.SelectedGameFolderIndex = i;
                        SettingsManager.SaveSettings();
                        return JsonSerializer.Serialize(new { success = true, alreadyExists = true });
                    }
                }
                s.GameFolders.Add(new GameFolderEntry(displayName, folderPath));
                s.SelectedGameFolderIndex = s.GameFolders.Count - 1;
                SettingsManager.SaveSettings();
                return JsonSerializer.Serialize(new { success = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>删除游戏目录</summary>
        public string RemoveGameFolderJson(string folderPath)
        {
            try
            {
                var s = SettingsManager.Settings;
                if (s.GameFolders == null || s.GameFolders.Count == 0)
                    return JsonSerializer.Serialize(new { success = false, error = "列表为空" });

                string normalized = Environment.ExpandEnvironmentVariables(folderPath).TrimEnd('\\') + "\\";
                int removed = s.GameFolders.RemoveAll(f => string.Equals(f.NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    if (s.SelectedGameFolderIndex >= s.GameFolders.Count)
                        s.SelectedGameFolderIndex = Math.Max(0, s.GameFolders.Count - 1);
                    SettingsManager.SaveSettings();
                    return JsonSerializer.Serialize(new { success = true });
                }
                return JsonSerializer.Serialize(new { success = false, error = "未找到" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>重命名游戏目录显示名称</summary>
        public string RenameGameFolderJson(string folderPath, string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(newName))
                    return JsonSerializer.Serialize(new { success = false, error = "参数无效" });

                var s = SettingsManager.Settings;
                if (s.GameFolders == null || s.GameFolders.Count == 0)
                    return JsonSerializer.Serialize(new { success = false, error = "列表为空" });

                string normalized = Environment.ExpandEnvironmentVariables(folderPath).TrimEnd('\\') + "\\";
                var entry = s.GameFolders.FirstOrDefault(f =>
                    string.Equals(f.NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    return JsonSerializer.Serialize(new { success = false, error = "未找到" });

                entry.Name = newName.Trim();
                SettingsManager.SaveSettings();
                return JsonSerializer.Serialize(new { success = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>在资源管理器中打开游戏目录</summary>
        public string OpenGameFolderJson(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                    return JsonSerializer.Serialize(new { success = false, error = "路径为空" });

                string expanded = Environment.ExpandEnvironmentVariables(folderPath);
                if (!Directory.Exists(expanded))
                    return JsonSerializer.Serialize(new { success = false, error = "目录不存在" });

                System.Diagnostics.Process.Start("explorer.exe", expanded);
                return JsonSerializer.Serialize(new { success = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        // ===== AI 浏览器操控 =====

        /// <summary>导航到指定 URL</summary>
        public async Task<string> AiBrowseNavigate(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return JsonSerializer.Serialize(new { success = false, error = "URL 为空" });

                var safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                await _window.Dispatcher.InvokeAsync(async () =>
                {
                    if (_window.WebView?.CoreWebView2 != null)
                    {
                        await _window.WebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.__aiBrowseNavigate('{safeUrl}');");
                    }
                });
                return JsonSerializer.Serialize(new { success = true, url });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>在浏览面板中点击元素</summary>
        public async Task<string> AiBrowseClick(string selector)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(selector))
                    return JsonSerializer.Serialize(new { success = false, error = "选择器为空" });

                var safeSelector = selector.Replace("\\", "\\\\").Replace("'", "\\'");
                await _window.Dispatcher.InvokeAsync(async () =>
                {
                    if (_window.WebView?.CoreWebView2 != null)
                    {
                        var result = await _window.WebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.__aiBrowseClick('{safeSelector}');");
                    }
                });
                return JsonSerializer.Serialize(new { success = true, selector });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>在浏览面板的输入框中输入文字</summary>
        public async Task<string> AiBrowseType(string selector, string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(selector))
                    return JsonSerializer.Serialize(new { success = false, error = "选择器为空" });

                var safeSelector = selector.Replace("\\", "\\\\").Replace("'", "\\'");
                var safeText = (text ?? "").Replace("\\", "\\\\").Replace("'", "\\'");
                await _window.Dispatcher.InvokeAsync(async () =>
                {
                    if (_window.WebView?.CoreWebView2 != null)
                    {
                        await _window.WebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.__aiBrowseType('{safeSelector}', '{safeText}');");
                    }
                });
                return JsonSerializer.Serialize(new { success = true, selector, text });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>滚动浏览面板</summary>
        public async Task<string> AiBrowseScroll(int x, int y)
        {
            try
            {
                await _window.Dispatcher.InvokeAsync(async () =>
                {
                    if (_window.WebView?.CoreWebView2 != null)
                    {
                        await _window.WebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.__aiBrowseScroll({x}, {y});");
                    }
                });
                return JsonSerializer.Serialize(new { success = true, x, y });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>获取浏览面板中的文本内容（供 AI 分析）</summary>
        public async Task<string> AiBrowseGetContent()
        {
            try
            {
                string text = "";
                await _window.Dispatcher.InvokeAsync(async () =>
                {
                    if (_window.WebView?.CoreWebView2 != null)
                    {
                        var result = await _window.WebView.CoreWebView2.ExecuteScriptAsync(
                            "window.__aiBrowseGetContent();");
                        text = result?.Trim('"') ?? "";
                    }
                });
                return JsonSerializer.Serialize(new { success = true, content = text });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>在浏览面板中执行任意 JS</summary>
        public async Task<string> AiBrowseExecuteScript(string script)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(script))
                    return JsonSerializer.Serialize(new { success = false, error = "脚本为空" });

                var safeScript = script.Replace("\\", "\\\\").Replace("'", "\\'");
                await _window.Dispatcher.InvokeAsync(async () =>
                {
                    if (_window.WebView?.CoreWebView2 != null)
                    {
                        await _window.WebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.__aiBrowseExecuteScript('{safeScript}');");
                    }
                });
                return JsonSerializer.Serialize(new { success = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>关闭浏览面板</summary>
        public async Task<string> AiBrowseClose()
        {
            try
            {
                await _window.Dispatcher.InvokeAsync(async () =>
                {
                    if (_window.WebView?.CoreWebView2 != null)
                    {
                        await _window.WebView.CoreWebView2.ExecuteScriptAsync(
                            "window.__aiBrowseClose();");
                    }
                });
                return JsonSerializer.Serialize(new { success = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        // ===== 资源包 =====

        public string ManageShaders(string action, string paramsJson) { return "not_implemented"; }
        public string ManageDatapacks(string action, string paramsJson) { return "not_implemented"; }
        public string ManageTexturePacks(string action, string paramsJson) { return "not_implemented"; }

        // ===== 整合包 =====

        public async Task<object> SearchModpacks(string query, string platform)
        {
            try
            {
                var api = new Core.ModrinthApiService();
                var packs = await api.GetModpacks(20);
                if (!string.IsNullOrEmpty(query))
                    packs = packs.Where(p => p.Name.ToLower().Contains(query.ToLower())).ToList();
                return packs.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    author = p.Author,
                    description = p.Description,
                    downloads = p.DownloadCountFormatted,
                    source = platform ?? "Modrinth"
                }).ToArray();
            }
            catch { return Array.Empty<object>(); }
        }

        /// <summary>弹出文件对话框并导入整合包，返回 JSON 结果</summary>
        public string ImportModpackWithDialog()
        {
            return ProbeModpackWithDialog();
        }

        /// <summary>
        /// 打开文件选择器并探测整合包元数据（不开始安装）
        /// </summary>
        public string ProbeModpackWithDialog()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择整合包文件",
                    Filter = "整合包文件|*.mrpack;*.zip;*.curseforge;*.json|Minecraft整合包|*.mrpack|压缩包|*.zip|所有文件|*.*"
                };

                if (dlg.ShowDialog(_window) != true)
                    return JsonSerializer.Serialize(new { success = false, error = "已取消" });

                string filePath = dlg.FileName;
                if (!File.Exists(filePath))
                    return JsonSerializer.Serialize(new { success = false, error = "文件不存在" });

                var info = Core.ModpackInstaller.ProbeModpack(filePath);
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    filePath = filePath,
                    packType = info.PackType,
                    name = info.Name,
                    version = info.Version,
                    minecraftVersion = info.MinecraftVersion,
                    loaderType = info.LoaderType,
                    loaderVersion = info.LoaderVersion,
                    summary = info.Summary,
                    author = info.Author,
                    modCount = info.ModCount
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// 开始安装整合包（带自定义名称）
        /// </summary>
        public string StartModpackInstall(string filePath, string customName)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return JsonSerializer.Serialize(new { success = false, error = "文件不存在" });

            Task.Run(async () =>
            {
                try
                {
                    var result = await InstallModpackAsync(filePath, customName);
                    PostMessage("modpack-install-complete", new
                    {
                        success = result.Success,
                        error = result.ErrorMessage,
                        path = result.InstalledPath,
                        name = result.PackName,
                        versionId = result.VersionId
                    });
                }
                catch (Exception ex)
                {
                    PostMessage("modpack-install-complete", new { success = false, error = ex.Message, versionId = (string)null });
                }
            });

            return JsonSerializer.Serialize(new { success = true, started = true });
        }

        private async Task<(bool Success, string ErrorMessage, string InstalledPath, string PackName, string VersionId)> InstallModpackAsync(string filePath, string customName = null)
        {
            var probeInfo = Core.ModpackInstaller.ProbeModpack(filePath);
            string packName = probeInfo.Name ?? Path.GetFileNameWithoutExtension(filePath);
            string displayName = !string.IsNullOrEmpty(customName) ? customName : packName;
            displayName = SanitizeFolderName(displayName);
            string detectedType = probeInfo.PackType;

            PostMessage("modpack-install-progress", new
            {
                status = $"正在检测整合包类型：{detectedType}",
                progress = 5,
                packName = displayName
            });

            var settings = SettingsManager.Settings;
            string baseDir = Environment.ExpandEnvironmentVariables(settings.GamePath);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

            string versionId = displayName;
            string targetFolder = Path.Combine(baseDir, "versions", versionId);
            int suffix = 1;
            while (Directory.Exists(targetFolder))
            {
                versionId = $"{displayName}_{suffix}";
                targetFolder = Path.Combine(baseDir, "versions", versionId);
                suffix++;
            }

            try
            {
                bool success = false;
                string error = null;

                if (detectedType == "modrinth")
                {
                    try
                    {
                        var installer = new Core.ModpackInstaller(baseDir);
                        installer.StatusChanged += (status) =>
                        {
                            PostMessage("modpack-install-progress", new { status = status, progress = -1, packName = packName });
                        };
                        installer.ProgressChanged += (progress) =>
                        {
                            PostMessage("modpack-install-progress", new { status = (string)null, progress = progress, packName = packName });
                        };
                        var installResult = await installer.InstallFromMrpackAsync(filePath, versionId);
                        if (installResult.Success)
                        {
                            packName = installResult.ModpackName ?? packName;
                            versionId = installResult.VersionId ?? versionId;
                            targetFolder = Path.Combine(baseDir, "versions", versionId);
                        }
                        success = installResult.Success;
                        error = installResult.ErrorMessage;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }
                else if (detectedType == "curseforge")
                {
                    try
                    {
                        var installer = new Core.ModpackInstaller(baseDir);
                        installer.StatusChanged += (status) =>
                        {
                            PostMessage("modpack-install-progress", new { status = status, progress = -1, packName = packName });
                        };
                        installer.ProgressChanged += (progress) =>
                        {
                            PostMessage("modpack-install-progress", new { status = (string)null, progress = progress, packName = packName });
                        };
                        var installResult = await installer.InstallFromZipManifestAsync(filePath, versionId);
                        if (installResult.Success)
                        {
                            packName = installResult.ModpackName ?? packName;
                            versionId = installResult.VersionId ?? versionId;
                            targetFolder = Path.Combine(baseDir, "versions", versionId);
                        }
                        success = installResult.Success;
                        error = installResult.ErrorMessage;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }
                else if (detectedType == "mcbbs")
                {
                    try
                    {
                        var installer = new Core.ModpackInstaller(baseDir);
                        installer.StatusChanged += (status) =>
                        {
                            PostMessage("modpack-install-progress", new { status = status, progress = -1, packName = packName });
                        };
                        installer.ProgressChanged += (progress) =>
                        {
                            PostMessage("modpack-install-progress", new { status = (string)null, progress = progress, packName = packName });
                        };
                        var installResult = await installer.InstallFromMCBBSAsync(filePath, versionId);
                        if (installResult.Success)
                        {
                            packName = installResult.ModpackName ?? packName;
                            versionId = installResult.VersionId ?? versionId;
                            targetFolder = Path.Combine(baseDir, "versions", versionId);
                        }
                        success = installResult.Success;
                        error = installResult.ErrorMessage;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }
                else if (detectedType == "hmcl")
                {
                    try
                    {
                        var installer = new Core.ModpackInstaller(baseDir);
                        installer.StatusChanged += (status) =>
                        {
                            PostMessage("modpack-install-progress", new { status = status, progress = -1, packName = packName });
                        };
                        installer.ProgressChanged += (progress) =>
                        {
                            PostMessage("modpack-install-progress", new { status = (string)null, progress = progress, packName = packName });
                        };
                        var installResult = await installer.InstallFromHMCLAsync(filePath, versionId);
                        if (installResult.Success)
                        {
                            packName = installResult.ModpackName ?? packName;
                            versionId = installResult.VersionId ?? versionId;
                            targetFolder = Path.Combine(baseDir, "versions", versionId);
                        }
                        success = installResult.Success;
                        error = installResult.ErrorMessage;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }
                else if (detectedType == "mmc")
                {
                    try
                    {
                        var installer = new Core.ModpackInstaller(baseDir);
                        installer.StatusChanged += (status) =>
                        {
                            PostMessage("modpack-install-progress", new { status = status, progress = -1, packName = packName });
                        };
                        installer.ProgressChanged += (progress) =>
                        {
                            PostMessage("modpack-install-progress", new { status = (string)null, progress = progress, packName = packName });
                        };
                        var installResult = await installer.InstallFromMMCAsync(filePath, versionId);
                        if (installResult.Success)
                        {
                            packName = installResult.ModpackName ?? packName;
                            versionId = installResult.VersionId ?? versionId;
                            targetFolder = Path.Combine(baseDir, "versions", versionId);
                        }
                        success = installResult.Success;
                        error = installResult.ErrorMessage;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }
                else
                {
                    Directory.CreateDirectory(targetFolder);
                    Action<string> reportStatus = (status) =>
                    {
                        PostMessage("modpack-install-progress", new { status = status, progress = -1, packName = packName });
                    };
                    Action<int> reportProgress = (progress) =>
                    {
                        PostMessage("modpack-install-progress", new { status = (string)null, progress = progress, packName = packName });
                    };
                    var result = await InstallGenericZipPackAsync(filePath, targetFolder, versionId, packName, baseDir,
                        reportStatus, reportProgress, CancellationToken.None);
                    success = result.Success;
                    error = result.ErrorMessage;
                    if (success)
                    {
                        versionId = result.VersionId ?? versionId;
                        targetFolder = Path.Combine(baseDir, "versions", versionId);
                    }
                }

                if (success)
                {
                    PostMessage("modpack-install-progress", new { status = "安装完成", progress = 100, packName = packName });
                    return (true, null, targetFolder, packName, versionId);
                }
                else
                {
                    try { if (Directory.Exists(targetFolder)) Directory.Delete(targetFolder, true); } catch { }
                    return (false, error, null, packName, null);
                }
            }
            catch (Exception ex)
            {
                try { if (Directory.Exists(targetFolder)) Directory.Delete(targetFolder, true); } catch { }
                return (false, ex.Message, null, packName, null);
            }
        }

        private async Task<(bool Success, string ErrorMessage, string VersionId)> InstallGenericZipPackAsync(
            string zipPath, string targetFolder, string versionId, string packName, string baseDir,
            Action<string> reportStatus, Action<int> reportProgress, CancellationToken ct)
        {
            try
            {
                reportStatus("正在解压压缩包...");
                reportProgress(10);

                string tempDir = Path.Combine(Path.GetTempPath(), $"mnl-zip-{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);
                    reportProgress(30);
                    reportStatus("正在检测游戏目录结构...");

                    string sourceDir = await FindMinecraftRootAsync(tempDir);
                    if (sourceDir == null)
                        sourceDir = tempDir;

                    reportProgress(50);
                    reportStatus("正在检测版本信息...");

                    // 尝试从解压的文件中检测 Minecraft 版本
                    string detectedMcVersion = null;
                    string detectedLoaderType = null;
                    string detectedVersionId = null;

                    // 检查是否已有 versions 目录
                    string versionsDir = Path.Combine(sourceDir, "versions");
                    if (Directory.Exists(versionsDir))
                    {
                        var versionDirs = Directory.GetDirectories(versionsDir);
                        if (versionDirs.Length > 0)
                        {
                            // 取第一个版本目录
                            string firstVerDir = versionDirs[0];
                            detectedVersionId = Path.GetFileName(firstVerDir);
                            string verJsonFile = Path.Combine(firstVerDir, detectedVersionId + ".json");
                            if (File.Exists(verJsonFile))
                            {
                                try
                                {
                                    string json = File.ReadAllText(verJsonFile);
                                    using var doc = JsonDocument.Parse(json);
                                    var root = doc.RootElement;
                                    if (root.TryGetProperty("id", out var idProp))
                                        detectedVersionId = idProp.GetString() ?? detectedVersionId;
                                    // 如果有 inheritsFrom，基础版本就是 Minecraft 版本
                                    if (root.TryGetProperty("inheritsFrom", out var infProp))
                                    {
                                        detectedMcVersion = infProp.GetString();
                                        detectedLoaderType = "unknown";
                                    }
                                    else
                                    {
                                        detectedMcVersion = detectedVersionId;
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    reportProgress(65);
                    reportStatus("正在复制文件...");

                    // 如果 ZIP 里有完整的 .minecraft 结构（包含 versions 等），把内容复制到 baseDir
                    // 否则，把文件放到版本隔离目录中
                    bool hasFullMinecraftStructure = Directory.Exists(Path.Combine(sourceDir, "versions"));

                    string finalVersionId = versionId;
                    string finalVersionDir = Path.Combine(baseDir, "versions", finalVersionId);

                    if (hasFullMinecraftStructure && !string.IsNullOrEmpty(detectedVersionId))
                    {
                        // ZIP 中有完整版本结构，使用其中的版本 ID
                        finalVersionId = detectedVersionId;
                        finalVersionDir = Path.Combine(baseDir, "versions", finalVersionId);

                        // 如果目标已存在，加后缀
                        int suffix = 1;
                        string baseVid = finalVersionId;
                        while (Directory.Exists(finalVersionDir))
                        {
                            finalVersionId = $"{baseVid}_{suffix}";
                            finalVersionDir = Path.Combine(baseDir, "versions", finalVersionId);
                            suffix++;
                        }

                        // 把整个 .minecraft 结构的内容复制到 baseDir
                        foreach (string dir in Directory.GetDirectories(sourceDir))
                        {
                            string dirName = Path.GetFileName(dir);
                            string destDir = Path.Combine(baseDir, dirName);
                            if (dirName.Equals("versions", StringComparison.OrdinalIgnoreCase))
                            {
                                // versions 目录特殊处理，重命名版本目录
                                foreach (string verDir in Directory.GetDirectories(dir))
                                {
                                    string srcVerName = Path.GetFileName(verDir);
                                    if (srcVerName == detectedVersionId)
                                    {
                                        // 重命名为 finalVersionId
                                        if (!Directory.Exists(finalVersionDir))
                                            Directory.CreateDirectory(finalVersionDir);
                                        CopyDirectoryRecursive(verDir, finalVersionDir);
                                        // 重命名 JSON 文件
                                        string oldJson = Path.Combine(finalVersionDir, srcVerName + ".json");
                                        string newJson = Path.Combine(finalVersionDir, finalVersionId + ".json");
                                        if (File.Exists(oldJson) && !File.Exists(newJson))
                                        {
                                            // 修改 JSON 中的 id 字段
                                            try
                                            {
                                                string jsonContent = File.ReadAllText(oldJson);
                                                var jsonDoc = JsonDocument.Parse(jsonContent);
                                                var root = jsonDoc.RootElement;
                                                // 重建 JSON，替换 id
                                                using var ms = new MemoryStream();
                                                using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
                                                writer.WriteStartObject();
                                                foreach (var prop in root.EnumerateObject())
                                                {
                                                    if (prop.Name == "id")
                                                    {
                                                        writer.WriteString("id", finalVersionId);
                                                    }
                                                    else
                                                    {
                                                        prop.WriteTo(writer);
                                                    }
                                                }
                                                writer.WriteEndObject();
                                                writer.Flush();
                                                ms.Position = 0;
                                                using var sr = new StreamReader(ms);
                                                File.WriteAllText(newJson, sr.ReadToEnd());
                                                File.Delete(oldJson);
                                            }
                                            catch
                                            {
                                                File.Move(oldJson, newJson);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // 其他版本目录直接复制
                                        string destVerDir = Path.Combine(destDir, srcVerName);
                                        CopyDirectoryRecursive(verDir, destVerDir);
                                    }
                                }
                            }
                            else
                            {
                                // 其他目录直接复制
                                CopyDirectoryRecursive(dir, destDir);
                            }
                        }
                    }
                    else
                    {
                        // ZIP 中没有完整版本结构，创建一个版本并把文件放进去
                        if (!Directory.Exists(finalVersionDir))
                            Directory.CreateDirectory(finalVersionDir);

                        // 创建版本隔离的游戏目录
                        string gameDir = Path.Combine(finalVersionDir, "game");
                        Directory.CreateDirectory(gameDir);

                        // 复制所有文件到 game 目录
                        CopyDirectoryRecursive(sourceDir, gameDir);

                        // 创建版本 JSON（继承自一个已有的版本，或创建基础版本）
                        detectedMcVersion = detectedMcVersion ?? "1.20.1"; // 默认版本

                        // 查找一个已安装的基础版本
                        string baseVersionId = FindLatestInstalledVersion(baseDir, detectedMcVersion);
                        if (string.IsNullOrEmpty(baseVersionId))
                        {
                            // 没有找到基础版本，创建一个简单的 release 版本
                            CreateSimpleVersion(finalVersionDir, finalVersionId, detectedMcVersion);
                        }
                        else
                        {
                            // 继承基础版本
                            CreateInheritedVersion(finalVersionDir, finalVersionId, baseVersionId, packName);
                        }
                    }

                    reportProgress(90);
                    reportStatus("正在刷新版本列表...");

                    try { await VersionScanService.Instance.ScanAsync("整合包安装完成"); } catch { }

                    reportProgress(100);

                    return (true, null, finalVersionId);
                }
                finally
                {
                    try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        /// <summary>查找最新安装的指定 Minecraft 版本</summary>
        private static string FindLatestInstalledVersion(string baseDir, string preferredVersion)
        {
            try
            {
                string versionsDir = Path.Combine(baseDir, "versions");
                if (!Directory.Exists(versionsDir)) return null;

                var dirs = Directory.GetDirectories(versionsDir);
                string bestMatch = null;
                DateTime bestTime = DateTime.MinValue;

                foreach (var dir in dirs)
                {
                    string name = Path.GetFileName(dir);
                    string jsonFile = Path.Combine(dir, name + ".json");
                    if (!File.Exists(jsonFile)) continue;

                    try
                    {
                        string json = File.ReadAllText(jsonFile);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        // 如果有 inheritsFrom，跳过（这是衍生版本）
                        if (root.TryGetProperty("inheritsFrom", out _))
                            continue;

                        // 检查 id 是否匹配 preferredVersion
                        if (root.TryGetProperty("id", out var idProp))
                        {
                            string id = idProp.GetString() ?? "";
                            DateTime writeTime = File.GetLastWriteTime(jsonFile);
                            if (id == preferredVersion)
                            {
                                if (writeTime > bestTime)
                                {
                                    bestTime = writeTime;
                                    bestMatch = id;
                                }
                            }
                            else if (bestMatch == null)
                            {
                                bestTime = writeTime;
                                bestMatch = id;
                            }
                        }
                    }
                    catch { }
                }

                return bestMatch;
            }
            catch { return null; }
        }

        /// <summary>创建简单的独立版本 JSON</summary>
        private static void CreateSimpleVersion(string versionDir, string versionId, string mcVersion)
        {
            var versionJson = new
            {
                id = versionId,
                type = "release",
                releaseTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                mainClass = "net.minecraft.client.main.Main",
                arguments = new { game = new object[] { } },
                libraries = new object[] { }
            };

            string jsonPath = Path.Combine(versionDir, $"{versionId}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(versionJson, options);
            File.WriteAllText(jsonPath, json);

            Logger.Info($"[整合包安装] 创建简单版本: {versionId} (MC {mcVersion})");
        }

        /// <summary>创建继承版本 JSON</summary>
        private static void CreateInheritedVersion(string versionDir, string versionId,
            string baseVersionId, string displayName)
        {
            if (!Directory.Exists(versionDir))
                Directory.CreateDirectory(versionDir);

            var versionJson = new
            {
                id = versionId,
                inheritsFrom = baseVersionId,
                type = "release",
                releaseTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                mainClass = "net.minecraft.client.main.Main",
                arguments = new { game = new object[] { } },
                libraries = new object[] { }
            };

            string jsonPath = Path.Combine(versionDir, $"{versionId}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(versionJson, options);
            File.WriteAllText(jsonPath, json);

            Logger.Info($"[整合包安装] 创建继承版本: {versionId} <- {baseVersionId}");
        }

        private async Task<string> FindMinecraftRootAsync(string baseDir)
        {
            if (Directory.Exists(Path.Combine(baseDir, "versions")))
                return baseDir;

            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                if (Directory.Exists(Path.Combine(dir, "versions")))
                    return dir;
                if (Directory.Exists(Path.Combine(dir, ".minecraft")))
                    return Path.Combine(dir, ".minecraft");
            }

            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                var result = await FindMinecraftRootAsync(dir);
                if (result != null) return result;
            }

            return null;
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Minecraft";
            var invalid = Path.GetInvalidFileNameChars();
            var filtered = name.Where(c => !invalid.Contains(c)).ToArray();
            string result = new string(filtered).Trim();
            if (string.IsNullOrEmpty(result)) return "Minecraft";
            if (result.Length > 64) result = result.Substring(0, 64);
            return result;
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, dest);
            }
        }

        public string ImportModpack(string filePath) { return ImportModpackWithDialog(); }
        /// <summary>
        /// 从 API 下载并安装整合包（packId 为 JSON 字符串：{ projectId, source }）
        /// 参考 PCL ModLaunch 整合包安装流程
        /// </summary>
        public string InstallModpack(string argsJson)
        {
            try
            {
                var args = JsonSerializer.Deserialize<Dictionary<string, string>>(argsJson ?? "{}");
                if (args == null || !args.TryGetValue("projectId", out string projectId) ||
                    !args.TryGetValue("source", out string source))
                    return JsonSerializer.Serialize(new { success = false, error = "参数缺失：projectId / source" });

                // 异步执行下载+安装
                Task.Run(async () =>
                {
                    try
                    {
                        string downloadUrl = null;
                        string fileName = null;
                        string versionName = null;

                        PostMessage("toast", new { message = "正在获取整合包信息...", type = "info" });

                        if (source == "Modrinth")
                        {
                            var api = new Core.ModrinthApiService();
                            var versions = await api.GetProjectVersions(projectId, null);
                            if (versions == null || versions.Count == 0)
                            {
                                PostMessage("modpack-install-complete", new { success = false, error = "未找到可用版本" });
                                return;
                            }

                            // 选最新 release 版本
                            var releaseVersion = versions.OrderByDescending(v =>
                                v.VersionType == "release" ? 2 : v.VersionType == "beta" ? 1 : 0)
                                .ThenByDescending(v => v.DatePublished)
                                .FirstOrDefault() ?? versions[0];

                            var file = releaseVersion.Files?.FirstOrDefault();
                            if (file == null)
                            {
                                PostMessage("modpack-install-complete", new { success = false, error = "版本无下载文件" });
                                return;
                            }

                            downloadUrl = file.Url;
                            fileName = file.FileName ?? $"{projectId}.mrpack";
                            versionName = releaseVersion.Name;
                        }
                        else if (source == "CurseForge")
                        {
                            if (!int.TryParse(projectId, out int modId))
                            {
                                PostMessage("modpack-install-complete", new { success = false, error = "无效的 CurseForge 项目 ID" });
                                return;
                            }

                            var api = new Core.CurseForgeApiService();
                            var files = await api.GetModFiles(modId);
                            if (files == null || files.Count == 0)
                            {
                                PostMessage("modpack-install-complete", new { success = false, error = "未找到可用文件" });
                                return;
                            }

                            // 选最新 release 版本
                            var releaseFile = files.OrderByDescending(f =>
                                f.ReleaseType == 1 ? 2 : f.ReleaseType == 2 ? 1 : 0)
                                .ThenByDescending(f => f.FileDate)
                                .FirstOrDefault() ?? files[0];

                            downloadUrl = releaseFile.DownloadUrl;
                            fileName = releaseFile.FileName ?? $"{projectId}.zip";
                            versionName = releaseFile.DisplayName;
                        }
                        else
                        {
                            PostMessage("modpack-install-complete", new { success = false, error = $"未知来源: {source}" });
                            return;
                        }

                        if (string.IsNullOrEmpty(downloadUrl))
                        {
                            PostMessage("modpack-install-complete", new { success = false, error = "无法获取下载地址" });
                            return;
                        }

                        // 下载到临时目录
                        string tempDir = Path.Combine(Path.GetTempPath(), "MNL", "modpack_download");
                        Directory.CreateDirectory(tempDir);
                        string downloadPath = Path.Combine(tempDir, fileName ?? "pack.mrpack");

                        PostMessage("toast", new { message = "正在下载整合包: " + (versionName ?? fileName), type = "info" });
                        PostMessage("modpack-install-progress", new { status = "正在下载整合包...", progress = 10, packName = fileName });

                        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                        {
                            http.DefaultRequestHeaders.UserAgent.ParseAdd("MNL/1.0");
                            byte[] data = await http.GetByteArrayAsync(downloadUrl);
                            await File.WriteAllBytesAsync(downloadPath, data);
                        }

                        // 安装整合包
                        var installResult = await InstallModpackAsync(downloadPath, null);

                        // 清理临时文件
                        try { File.Delete(downloadPath); } catch { }

                        PostMessage("modpack-install-complete", new
                        {
                            success = installResult.Success,
                            error = installResult.ErrorMessage,
                            versionId = installResult.VersionId,
                            packName = installResult.PackName
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("[整合包安装] 失败: " + ex.Message);
                        PostMessage("modpack-install-complete", new { success = false, error = ex.Message, versionId = (string)null });
                    }
                });

                return JsonSerializer.Serialize(new { success = true, started = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }

        // ===== 工具（补充） =====

        public string OptimizeMemory()
        {
            try
            {
                Core.MemoryOptimizer.OptimizeAsync();
                PostMessage("toast", new { message = "内存优化完成", type = "success" });
                return "ok";
            }
            catch { return "error"; }
        }

        // ===== 导航（C# 端触发页面跳转） =====

        public void NavigateTo(string pageKey)
        {
            PostMessage("navigation", new { page = pageKey });
        }

        /// <summary>页面加载完成后调用，通知 C# 端发送用户信息</summary>
        public void OnWebAppReady()
        {
            _window.Dispatcher.Invoke(() => _window.InitializeBackground());
            PostMessage("toast", new { message = "MNL 音符启动器已就绪", type = "info" });
        }

        // ===== 内部方法 =====

        public void PostMessage(string type, object payload)
        {
            _window.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    if (_window.WebView?.CoreWebView2 != null)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(
                            new { type, payload });
                        await _window.WebView.CoreWebView2.ExecuteScriptAsync(
                            $"window.postMessage({json});");
                    }
                }
                catch { /* WebView 尚未就绪 */ }
            });
        }
    }
}
