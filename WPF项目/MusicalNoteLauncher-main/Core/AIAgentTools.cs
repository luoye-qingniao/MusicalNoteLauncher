using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>AI Agent 工具调用结果</summary>
    public class AgentToolResult
    {
        public string Name { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; }
    }

    /// <summary>AI Agent 工具定义与执行器</summary>
    public class AIAgentTools
    {
        private readonly string _minecraftPath;
        private static AIAgentTools _instance;
        public static AIAgentTools Instance => _instance ?? throw new InvalidOperationException("AIAgentTools 未初始化");

        /// <summary>UI 操作回调 (JS 脚本, 超时ms) → 异步执行结果</summary>
        public static Func<string, Task> ExecuteUIAsync;

        public event Action<AgentToolResult> ToolExecuted;

        /// <summary>工具执行进度回调 (工具名, 状态描述)</summary>
        public Action<string, string>? OnToolProgress;

        public AIAgentTools(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
            _instance = this;
        }

        public List<object> GetToolDefinitions()
        {
            return new List<object>
            {
                CreateToolDef("navigate_to", "导航到启动器的指定页面。用户会看到页面切换动画。这是所有操作的第一步。",
                    new { page = Def("string", "页面名称: home(主页)/download(下载)/modmanager(模组管理)/settings(设置)/ai(AI助手)") },
                    new[] { "page" }),
                CreateToolDef("click_button", "点击启动器界面上的按钮或元素。会显示蓝色高亮边框和脉冲动画，用户能清楚看到点击位置。",
                    new { selector = Def("string", "CSS 选择器，如 .vp-latest-card-btn（下载按钮）或 .dl-card（下载分类卡片）") },
                    new[] { "selector" }),
                CreateToolDef("scroll_to", "滚动启动器界面，让目标元素可见。用于在长列表中查找内容。",
                    new { selector = Def("string", "目标元素的 CSS 选择器；空字符串滚到顶部") },
                    new string[0]),
                CreateToolDef("type_text", "在输入框中打字，模拟逐字输入效果，用户能看到每个字符出现。",
                    new { selector = Def("string", "输入框的 CSS 选择器，如 .vp-search-box input"), text = Def("string", "要输入的文字") },
                    new[] { "selector", "text" }),
                CreateToolDef("wait", "等待指定的毫秒数。用于模拟真人操作间隔，让用户看清每一步。**每步 UI 操作间都应调用。**",
                    new { ms = Def("number", "等待毫秒数，建议 400-800") },
                    new[] { "ms" }),
                CreateToolDef("launch_game", "启动已安装的 Minecraft 游戏版本。",
                    new { version_id = Def("string", "要启动的版本 ID，如 1.20.1") },
                    new[] { "version_id" }),
                CreateToolDef("get_installed_versions", "获取已安装的所有 Minecraft 版本列表。",
                    new { include_status = Def("boolean", "是否包含版本状态") },
                    new string[0]),
                CreateToolDef("check_mods", "检查指定版本已安装的模组。",
                    new { version_id = Def("string", "版本 ID，可选") },
                    new string[0]),
                CreateToolDef("get_system_info", "获取系统信息用于诊断问题。",
                    new { },
                    new string[0]),
            };
        }

        private static object Def(string type, string desc) => new { type, description = desc };

        private static object CreateToolDef(string name, string desc, object props, string[] required)
        {
            return new
            {
                type = "function",
                function = new { name, description = desc, parameters = new { type = "object", properties = props, required } }
            };
        }

        public async Task<AgentToolResult> ExecuteToolAsync(string toolName, string arguments)
        {
            AgentToolResult result;
            try
            {
                using var doc = JsonDocument.Parse(arguments);
                var args = doc.RootElement;

                result = toolName switch
                {
                    "launch_game" => await LaunchGame(args),
                    "download_version" => await DownloadVersion(args),
                    "download_mod" => await DownloadMod(args),
                    "repair_version" => await RepairVersion(args),
                    "get_installed_versions" => GetInstalledVersions(args),
                    "get_system_info" => GetSystemInfo(),
                    "install_loader" => await InstallLoader(args),
                    "delete_version" => DeleteVersion(args),
                    "check_mods" => CheckMods(args),
                    "navigate_to" => await NavigateTo(args),
                    "click_button" => await ClickButton(args),
                    "type_text" => await TypeText(args),
                    "scroll_to" => await ScrollTo(args),
                    "wait" => await Wait(args),
                    _ => new AgentToolResult { Name = toolName, Success = false, Message = $"未知的工具: {toolName}" }
                };
            }
            catch (Exception ex)
            {
                result = new AgentToolResult { Name = toolName, Success = false, Message = $"工具执行出错: {ex.Message}" };
            }

            ToolExecuted?.Invoke(result);
            return result;
        }

        // ─────────── 启动游戏 ───────────
        private async Task<AgentToolResult> LaunchGame(JsonElement args)
        {
            var versionId = args.GetProperty("version_id").GetString()!;

            var versionManager = new VersionManager(_minecraftPath);
            if (!versionManager.IsJavaVersionValid(versionId))
            {
                var installed = versionManager.GetInstalledJavaVersions();
                return new AgentToolResult
                {
                    Name = "launch_game",
                    Success = false,
                    Message = $"版本 \"{versionId}\" 不存在或已损坏。已安装: {string.Join(", ", installed)}",
                    Data = new Dictionary<string, object> { ["installed_versions"] = installed }
                };
            }

            try
            {
                var javaConfig = new JavaConfigManager(_minecraftPath);
                var launcher = new GameLauncher(_minecraftPath, javaConfig);

                // 收集日志
                var logLines = new List<string>();
                launcher.LaunchLogReceived += log =>
                {
                    logLines.Add(log);
                    if (logLines.Count > 50) logLines.RemoveAt(0);
                };

                var tcs = new TaskCompletionSource<(bool, string)>();

                launcher.LaunchResultCompleted += info =>
                {
                    var lastLogs = logLines.Count > 10
                        ? string.Join("\n", logLines.Skip(Math.Max(0, logLines.Count - 10)))
                        : string.Join("\n", logLines);

                    if (info.IsSuccess)
                        tcs.TrySetResult((true, $"游戏 \"{info.VersionId}\" 启动成功！进程正在运行中。"));
                    else
                        tcs.TrySetResult((false, $"启动失败。退出码: {info.ExitCode}，错误: {info.ErrorMessage}\n最后日志:\n{lastLogs}"));
                };

                // LauncherCore.LaunchGame 或 GameLauncher.LaunchGameAsync
                var launchMethod = typeof(GameLauncher).GetMethod("LaunchGame",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (launchMethod != null)
                {
                    launchMethod.Invoke(launcher, new object[] { versionId, "" });
                }
                else
                {
                    var launchAsync = typeof(GameLauncher).GetMethod("LaunchGameAsync",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (launchAsync != null)
                    {
                        // 使用默认内存分配 (2G-4G)
                        var task = (Task<bool>)launchAsync.Invoke(launcher,
                            new object[] { versionId, "", 2048, 4096, "", "", "" })!;
                        await task;
                        if (task.Result)
                            tcs.TrySetResult((true, $"游戏 \"{versionId}\" 启动成功！"));
                        else
                            tcs.TrySetResult((false, $"游戏 \"{versionId}\" 启动失败"));
                    }
                    else
                    {
                        return new AgentToolResult
                        {
                            Name = "launch_game",
                            Success = false,
                            Message = "无法找到 GameLauncher 的启动方法"
                        };
                    }
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(-1, cts.Token));
                if (completed == tcs.Task)
                {
                    var (success, msg) = tcs.Task.Result;
                    return new AgentToolResult
                    {
                        Name = "launch_game",
                        Success = success,
                        Message = msg,
                        Data = new Dictionary<string, object> { ["version_id"] = versionId }
                    };
                }
            }
            catch (Exception ex)
            {
                return new AgentToolResult
                {
                    Name = "launch_game",
                    Success = false,
                    Message = $"启动出错: {ex.Message}"
                };
            }

            return new AgentToolResult
            {
                Name = "launch_game",
                Success = true,
                Message = $"已发出启动 \"{versionId}\" 的命令，游戏正在启动中..."
            };
        }

        // ─────────── 下载版本 ───────────
        private async Task<AgentToolResult> DownloadVersion(JsonElement args)
        {
            var versionId = args.GetProperty("version_id").GetString()!;

            // 先导航到下载页，让用户看到操作
            if (ExecuteUIAsync != null)
            {
                await ExecuteUIAsync(
                    @"(function(){
                        var navs=document.querySelectorAll('.nav-item');
                        for(var i=0;i<navs.length;i++){
                            var spans=navs[i].querySelectorAll('span');
                            for(var j=0;j<spans.length;j++){
                                if(spans[j].textContent.trim()==='下载'){
                                    navs[i].style.boxShadow='0 0 0 3px var(--accent)';
                                    setTimeout(function(){navs[i].style.boxShadow='';},800);
                                    navs[i].click();
                                    return;
                                }
                            }
                        }
                    })()");
                await Task.Delay(600); // 等页面切换动画
            }

            var downloadService = new VersionDownloadService(_minecraftPath);

            try
            {
                // 订阅状态变化 → 实时上报进度
                downloadService.StatusChanged += msg =>
                    OnToolProgress?.Invoke("download_version", msg);

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                await downloadService.StartDownloadAsync(versionId, cts.Token);

                return new AgentToolResult
                {
                    Name = "download_version",
                    Success = true,
                    Message = $"版本 \"{versionId}\" 下载完成！可以直接启动游玩了。",
                    Data = new Dictionary<string, object> { ["version_id"] = versionId }
                };
            }
            catch (OperationCanceledException)
            {
                return new AgentToolResult
                {
                    Name = "download_version",
                    Success = false,
                    Message = "下载超时（30分钟），请检查网络后重试。"
                };
            }
            catch (Exception ex)
            {
                return new AgentToolResult
                {
                    Name = "download_version",
                    Success = false,
                    Message = $"下载出错: {ex.Message}"
                };
            }
        }

        // ─────────── 搜索模组 ───────────
        private async Task<AgentToolResult> DownloadMod(JsonElement args)
        {
            var modName = args.GetProperty("mod_name").GetString()!;
            var gameVersion = args.TryGetProperty("game_version", out var gv) ? gv.GetString() : null;

            try
            {
                var modrinthApi = new ModrinthApiService();
                var searchResult = await modrinthApi.SearchMods(modName, gameVersion ?? "", 5);

                if (searchResult == null || searchResult.Count == 0)
                {
                    var curseApi = new CurseForgeApiService();
                    var curseMods = await curseApi.SearchMods(modName, 5);

                    if (curseMods == null || curseMods.Count == 0)
                    {
                        return new AgentToolResult
                        {
                            Name = "download_mod",
                            Success = false,
                            Message = $"未找到与 \"{modName}\" 匹配的模组。请尝试更具体的关键词。"
                        };
                    }

                    var list = string.Join("\n", curseMods.Take(5).Select(m =>
                        $"  - {m.Name} (作者: {m.AuthorName})"));

                    return new AgentToolResult
                    {
                        Name = "download_mod",
                        Success = true,
                        Message = $"在 CurseForge 找到 {curseMods.Count} 个相关模组:\n{list}\n\n请到「模组市场」页面查看并下载。",
                        Data = new Dictionary<string, object>
                        {
                            ["source"] = "curseforge",
                            ["found_count"] = curseMods.Count
                        }
                    };
                }

                var topMod = searchResult.First();
                return new AgentToolResult
                {
                    Name = "download_mod",
                    Success = true,
                    Message = $"在 Modrinth 找到与 \"{modName}\" 匹配的模组:\n" +
                              $"  **{topMod.Name}** - 作者: {topMod.Author}\n" +
                              $"  下载量: {topMod.DownloadCountFormatted}\n" +
                              $"\n请到「模组市场」页面查看并下载。",
                    Data = new Dictionary<string, object>
                    {
                        ["source"] = "modrinth",
                        ["found_count"] = searchResult.Count,
                        ["top_mod"] = new { topMod.Name, topMod.Author, topMod.Id }
                    }
                };
            }
            catch (Exception ex)
            {
                return new AgentToolResult
                {
                    Name = "download_mod",
                    Success = false,
                    Message = $"搜索模组时出错: {ex.Message}"
                };
            }
        }

        // ─────────── 修复版本 ───────────
        private async Task<AgentToolResult> RepairVersion(JsonElement args)
        {
            var versionId = args.TryGetProperty("version_id", out var v) ? v.GetString() : null;

            var repairService = new VersionRepairService(_minecraftPath);
            repairService.StatusChanged += msg => OnToolProgress?.Invoke("repair_version", msg);

            try
            {
                if (!string.IsNullOrEmpty(versionId))
                {
                    // 直接使用 RepairResult，不依赖事件（事件在版本完好时不触发）
                    var repairResult = await repairService.RepairVersionAsync(versionId, CancellationToken.None);

                    return new AgentToolResult
                    {
                        Name = "repair_version",
                        Success = repairResult.IsSuccess,
                        Message = repairResult.IsSuccess
                            ? $"版本 \"{repairResult.VersionId}\" 修复完成！"
                            : $"修复失败: {repairResult.ErrorMessage ?? "未知错误"}"
                    };
                }

                // 批量修复：扫描所有损坏版本
                var versionManager = new VersionManager(_minecraftPath);
                var versions = versionManager.GetInstalledJavaVersions();
                var broken = versions.Where(v2 => !versionManager.IsJavaVersionValid(v2)).ToList();

                if (broken.Count == 0)
                {
                    return new AgentToolResult
                    {
                        Name = "repair_version",
                        Success = true,
                        Message = $"所有 {versions.Count} 个已安装版本都完好无损，无需修复。"
                    };
                }

                var repaired = new List<string>();
                var failed = new List<string>();
                foreach (var ver in broken)
                {
                    try
                    {
                        var r = await repairService.RepairVersionAsync(ver, CancellationToken.None);
                        if (r.IsSuccess)
                            repaired.Add(ver);
                        else
                            failed.Add($"{ver}({r.ErrorMessage})");
                    }
                    catch { failed.Add(ver); }
                }

                return new AgentToolResult
                {
                    Name = "repair_version",
                    Success = failed.Count == 0,
                    Message = failed.Count == 0
                        ? $"已修复 {repaired.Count} 个版本: {string.Join(", ", repaired)}"
                        : $"修复完成: 成功 {repaired.Count} 个，失败 {failed.Count} 个 ({string.Join(", ", failed)})",
                    Data = new Dictionary<string, object> { ["repaired"] = repaired, ["failed"] = failed }
                };
            }
            catch (Exception ex)
            {
                return new AgentToolResult
                {
                    Name = "repair_version",
                    Success = false,
                    Message = $"修复出错: {ex.Message}"
                };
            }
        }

        // ─────────── 版本列表 ───────────
        private AgentToolResult GetInstalledVersions(JsonElement args)
        {
            var includeStatus = args.TryGetProperty("include_status", out var s) && s.GetBoolean();

            var versionManager = new VersionManager(_minecraftPath);
            var javaVersions = versionManager.GetInstalledJavaVersions();
            var bedrockVersions = versionManager.GetInstalledBedrockVersions();

            var sb = new StringBuilder();
            sb.AppendLine("**已安装的 Minecraft 版本**");
            sb.AppendLine();

            if (javaVersions.Count > 0)
            {
                sb.AppendLine($"**Java 版** ({javaVersions.Count}个):");
                foreach (var ver in javaVersions)
                {
                    if (includeStatus)
                    {
                        var valid = versionManager.IsJavaVersionValid(ver);
                        sb.AppendLine($"  - {ver} {(valid ? "正常" : "需修复")}");
                    }
                    else sb.AppendLine($"  - {ver}");
                }
            }
            else sb.AppendLine("**Java 版**: 暂无安装");

            if (bedrockVersions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"**基岩版** ({bedrockVersions.Count}个):");
                foreach (var ver in bedrockVersions) sb.AppendLine($"  - {ver}");
            }

            return new AgentToolResult
            {
                Name = "get_installed_versions",
                Success = true,
                Message = sb.ToString(),
                Data = new Dictionary<string, object>
                {
                    ["java_versions"] = javaVersions,
                    ["bedrock_versions"] = bedrockVersions,
                    ["total_count"] = javaVersions.Count + bedrockVersions.Count
                }
            };
        }

        // ─────────── 系统信息 ───────────
        private AgentToolResult GetSystemInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("**系统信息**");
            sb.AppendLine();
            sb.AppendLine($"  操作系统: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            sb.AppendLine($"  系统架构: {(Environment.Is64BitOperatingSystem ? "64位" : "32位")}");
            sb.AppendLine($"  处理器核心: {Environment.ProcessorCount}");
            sb.AppendLine($"  工作目录: {_minecraftPath}");
            sb.AppendLine();

            var javaConfig = new JavaConfigManager(_minecraftPath);
            var javaPath = javaConfig.GetJavaPath();
            sb.AppendLine("**Java 运行时**:");
            sb.AppendLine($"  {(string.IsNullOrEmpty(javaPath) ? "未配置" : javaPath)}");

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(_minecraftPath)!);
                var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var totalGB = drive.TotalSize / (1024.0 * 1024 * 1024);
                sb.AppendLine();
                sb.AppendLine("**磁盘空间**:");
                sb.AppendLine($"  可用: {freeGB:F1} GB / 总计: {totalGB:F1} GB");
            }
            catch { }

            return new AgentToolResult
            {
                Name = "get_system_info",
                Success = true,
                Message = sb.ToString()
            };
        }

        // ─────────── 安装加载器 ───────────
        private Task<AgentToolResult> InstallLoader(JsonElement args)
        {
            var versionId = args.GetProperty("version_id").GetString()!;
            var loader = args.GetProperty("loader").GetString()!;

            var versionManager = new VersionManager(_minecraftPath);
            if (!versionManager.IsJavaVersionValid(versionId))
            {
                return Task.FromResult(new AgentToolResult
                {
                    Name = "install_loader",
                    Success = false,
                    Message = $"版本 \"{versionId}\" 不存在或已损坏，请先下载该版本。"
                });
            }

            var loaderName = loader.ToLower() switch
            {
                "forge" => "Forge",
                "fabric" => "Fabric",
                "neoforge" => "NeoForge",
                "optifine" => "OptiFine",
                _ => null
            };

            if (loaderName == null)
            {
                return Task.FromResult(new AgentToolResult
                {
                    Name = "install_loader",
                    Success = false,
                    Message = $"不支持的加载器类型: {loader}。支持: forge, fabric, neoforge, optifine"
                });
            }

            // 引导用户通过 UI 完成安装（安装器需要用户选择具体版本）
            return Task.FromResult(new AgentToolResult
            {
                Name = "install_loader",
                Success = true,
                Message = $"已为你准备好安装 {loaderName} 加载器的环境。\n" +
                          $"目标版本: {versionId}\n\n" +
                          $"请前往「启动页」→ 版本选择 → 点击「安装加载器」来完成 {loaderName} 的安装。\n" +
                          $"安装时需要选择具体的 {loaderName} 版本号，推荐使用最新稳定版。"
            });
        }

        // ─────────── 删除版本 ───────────
        private AgentToolResult DeleteVersion(JsonElement args)
        {
            var versionId = args.GetProperty("version_id").GetString()!;
            var versionDir = Path.Combine(_minecraftPath, "versions", versionId);

            if (!Directory.Exists(versionDir))
            {
                return new AgentToolResult
                {
                    Name = "delete_version",
                    Success = false,
                    Message = $"版本 \"{versionId}\" 不存在。"
                };
            }

            try
            {
                Directory.Delete(versionDir, true);
                return new AgentToolResult
                {
                    Name = "delete_version",
                    Success = true,
                    Message = $"版本 \"{versionId}\" 已成功删除。"
                };
            }
            catch (Exception ex)
            {
                return new AgentToolResult
                {
                    Name = "delete_version",
                    Success = false,
                    Message = $"删除失败: {ex.Message}。可能文件正在被占用。"
                };
            }
        }

        // ─────────── 检查模组 ───────────
        private AgentToolResult CheckMods(JsonElement args)
        {
            var versionId = args.TryGetProperty("version_id", out var v) ? v.GetString() : null;

            var versionManager = new VersionManager(_minecraftPath);
            var installedVersions = versionManager.GetInstalledJavaVersions();

            if (installedVersions.Count == 0)
            {
                return new AgentToolResult
                {
                    Name = "check_mods",
                    Success = false,
                    Message = "没有已安装的版本，无法检查模组。"
                };
            }

            var targetVersion = versionId ?? installedVersions.First();
            var modsDir = Path.Combine(_minecraftPath, "versions", targetVersion, "mods");

            if (!Directory.Exists(modsDir))
            {
                return new AgentToolResult
                {
                    Name = "check_mods",
                    Success = true,
                    Message = $"版本 \"{targetVersion}\" 的 mods 目录为空，尚未安装任何模组。"
                };
            }

            var modFiles = System.IO.Directory.GetFiles(modsDir, "*.jar");
            if (modFiles.Length == 0)
            {
                return new AgentToolResult
                {
                    Name = "check_mods",
                    Success = true,
                    Message = $"版本 \"{targetVersion}\" 的 mods 目录中没有模组文件。"
                };
            }

            var sb = new StringBuilder();
            sb.AppendLine($"**版本 \"{targetVersion}\" 的已安装模组**");
            sb.AppendLine();
            sb.AppendLine($"共 {modFiles.Length} 个模组文件:");
            sb.AppendLine();

            foreach (var file in modFiles)
            {
                var fi = new FileInfo(file);
                sb.AppendLine($"  {Path.GetFileNameWithoutExtension(file)} ({fi.Length / 1024} KB)");
            }

            sb.AppendLine();
            sb.AppendLine("要管理模组（启用/禁用/卸载），请前往「模组管理」页面。");

            return new AgentToolResult
            {
                Name = "check_mods",
                Success = true,
                Message = sb.ToString(),
                Data = new Dictionary<string, object>
                {
                    ["version_id"] = targetVersion,
                    ["mod_count"] = modFiles.Length
                }
            };
        }
        // ─────────── UI 操控工具 ───────────

        private async Task<AgentToolResult> NavigateTo(JsonElement args)
        {
            if (ExecuteUIAsync == null)
                return new AgentToolResult { Name = "navigate_to", Success = false, Message = "当前环境不支持 UI 操作" };

            string page = args.GetProperty("page").GetString()!;

            // 页面ID → 侧边栏中文标签 映射
            var pageToLabel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["home"] = "主界面", ["主页"] = "主界面",
                ["download"] = "下载", ["下载"] = "下载", ["下载管理"] = "下载",
                ["modmanager"] = "模组管理", ["模组管理"] = "模组管理", ["mod"] = "模组管理", ["模组"] = "模组管理",
                ["settings"] = "设置", ["设置"] = "设置",
                ["ai"] = "AI 助手", ["ai助手"] = "AI 助手",
                ["store"] = "组件商店",
                ["saves"] = "存档管理",
                ["multiplayer"] = "联机社交",
                ["downloads"] = "下载任务",
                ["music"] = "音乐盒",
                ["community"] = "交流社区",
                ["profile"] = "个人主页",
                ["friends"] = "好友列表",
            };

            string label = pageToLabel.TryGetValue(page, out var l) ? l : page;

            string js = $@"
(function(){{
    var navs = document.querySelectorAll('.nav-item');
    for (var i = 0; i < navs.length; i++) {{
        var spans = navs[i].querySelectorAll('span');
        for (var j = 0; j < spans.length; j++) {{
            if (spans[j].textContent.trim() === '{label}') {{
                navs[i].style.boxShadow = '0 0 0 3px var(--accent)';
                setTimeout(function(){{ navs[i].style.boxShadow = ''; }}, 800);
                navs[i].click();
                return;
            }}
        }}
    }}
}})();";

            await ExecuteUIAsync(js);
            return new AgentToolResult
            {
                Name = "navigate_to",
                Success = true,
                Message = $"已导航到「{label}」页面。"
            };
        }

        private async Task<AgentToolResult> ClickButton(JsonElement args)
        {
            if (ExecuteUIAsync == null)
                return new AgentToolResult { Name = "click_button", Success = false, Message = "当前环境不支持 UI 操作" };

            string selector = args.GetProperty("selector").GetString()!;
            // 点击版本下载按钮时，开启 AI 模式标志以跳过加载器/命名对话框
            string preJs = (selector.Contains("vp-latest-btn") || selector.Contains("vp-latest-card-btn"))
                ? "if(!window.__mnl_ai_mode)window.__mnl_ai_mode='Vanilla';"
                : "";
            // 点击后清除标志（兜底，防止残留影响用户后续手动操作）
            string postJs = (selector.Contains("vp-latest-btn") || selector.Contains("vp-latest-card-btn"))
                ? "setTimeout(function(){window.__mnl_ai_mode=false;},500);"
                : "";

            string js = preJs +
                $"var el=document.querySelector('{selector.Replace("'", "\\'")}');" +
                        "if(el){" +
                        "el.style.outline='2px solid var(--accent)';" +
                        "el.style.outlineOffset='2px';" +
                        "el.classList.add('ai-click-flash');" +
                        "setTimeout(function(){el.style.outline='';el.style.outlineOffset='';el.classList.remove('ai-click-flash');},600);" +
                        "el.click();" +
                        "el.focus&&el.focus();" +
                        "}" + postJs;

            await ExecuteUIAsync(js);
            return new AgentToolResult
            {
                Name = "click_button",
                Success = true,
                Message = $"已点击元素: {selector}"
            };
        }

        private async Task<AgentToolResult> TypeText(JsonElement args)
        {
            if (ExecuteUIAsync == null)
                return new AgentToolResult { Name = "type_text", Success = false, Message = "当前环境不支持 UI 操作" };

            string selector = args.GetProperty("selector").GetString()!;
            string text = args.GetProperty("text").GetString()!;
            string safeText = text.Replace("\\", "\\\\").Replace("'", "\\'");

            string js = $"var el=document.querySelector('{selector.Replace("'", "\\'")}');" +
                        "if(el){" +
                        "el.focus();el.value='';" +
                        // 逐字模拟输入
                        $"var i=0,t='{safeText}',iv=setInterval(function(){{" +
                        "if(i>=t.length){clearInterval(iv);el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));" +
                        // 输入完成后，点击所有分组标题展开版本列表
                        "setTimeout(function(){var hs=document.querySelectorAll('.vp-folder-header');for(var h=0;h<hs.length;h++){hs[h].click();}},200);" +
                        "return;}" +
                        "el.value+=t.charAt(i);el.dispatchEvent(new Event('input',{bubbles:true}));i++;" +
                        "},50);" +
                        "}";

            await ExecuteUIAsync(js);
            return new AgentToolResult
            {
                Name = "type_text",
                Success = true,
                Message = $"已在 {selector} 中输入: {text}"
            };
        }

        private async Task<AgentToolResult> ScrollTo(JsonElement args)
        {
            if (ExecuteUIAsync == null)
                return new AgentToolResult { Name = "scroll_to", Success = false, Message = "当前环境不支持 UI 操作" };

            string selector = "";
            if (args.TryGetProperty("selector", out var selProp))
                selector = selProp.GetString() ?? "";

            string js;
            if (!string.IsNullOrEmpty(selector))
                js = $"var el=document.querySelector('{selector.Replace("'", "\\'")}');if(el)el.scrollIntoView({{behavior:'smooth',block:'center'}});";
            else
                js = "window.scrollTo({top:0,behavior:'smooth'});";

            await ExecuteUIAsync(js);
            return new AgentToolResult
            {
                Name = "scroll_to",
                Success = true,
                Message = string.IsNullOrEmpty(selector) ? "已滚到顶部" : $"已滚动到 {selector}"
            };
        }

        private async Task<AgentToolResult> Wait(JsonElement args)
        {
            int ms = 500;
            if (args.TryGetProperty("ms", out var msProp) && msProp.TryGetInt32(out var v))
                ms = Math.Clamp(v, 100, 5000);
            await Task.Delay(ms);
            return new AgentToolResult
            {
                Name = "wait",
                Success = true,
                Message = $"已等待 {ms}ms"
            };
        }
    }
}
