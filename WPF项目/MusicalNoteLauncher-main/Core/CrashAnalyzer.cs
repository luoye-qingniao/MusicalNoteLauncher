using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MusicalNoteLauncher.Core
{
    /// <summary>崩溃原因分类</summary>
    public enum CrashReason
    {
        /// <summary>未识别，需要 AI 分析</summary>
        Unknown,

        // ── Java 相关 ──
        JavaVersionTooOld,
        JavaVersionTooNew,
        JavaNot64Bit,
        JavaNotFound,
        JavaJvmArgError,

        // ── 内存相关 ──
        OutOfMemory,
        NotEnoughRam,

        // ── Mod / 加载器相关 ──
        ModIncompatible,
        ModMissingDependency,
        ModDuplicate,
        ModCausingCrash,
        FabricApiMissing,
        OptifineForgeConflict,
        ForgeIncomplete,
        MixinInjectionFailed,

        // ── 文件 / 路径相关 ──
        FileNotFound,
        PathContainsSpecialChar,
        VersionJsonCorrupted,
        NativeExtractFailed,

        // ── 系统 / 驱动 ──
        DriverIncompatible,
        OpenGLNotSupported,
        PixelFormatError,

        // ── 认证 / 网络 ──
        AuthFailed,
        NetworkError,

        // ── 其他 ──
        ProcessImmediateExit,
        GameException,
    }

    /// <summary>单条错误分析结果</summary>
    public class CrashAnalysisResult
    {
        public CrashReason Reason { get; set; } = CrashReason.Unknown;
        public string Title { get; set; }
        public string Description { get; set; }
        public string Suggestion { get; set; }
        public int Priority { get; set; } = 3; // 1=高, 2=中, 3=低/未知

        /// <summary>是否被识别</summary>
        public bool IsKnown => Reason != CrashReason.Unknown;
    }

    /// <summary>本地崩溃分析器 —— 先匹配已知模式，未命中再交给 AI</summary>
    public static class CrashAnalyzer
    {
        /// <summary>分析崩溃原因</summary>
        /// <param name="logText">日志文本（可以是全部或摘要）</param>
        /// <param name="exitCode">进程退出码</param>
        /// <param name="errorMessage">GameLauncher 记录的错误描述</param>
        public static CrashAnalysisResult Analyze(string logText, int exitCode = 0, string errorMessage = null)
        {
            if (string.IsNullOrEmpty(logText))
            {
                // 仅有退出码的情况下作有限分析
                return AnalyzeByExitCode(exitCode, errorMessage);
            }

            // 第一层：高优先级精准匹配
            var result = MatchHighPriority(logText, exitCode, errorMessage);
            if (result != null) return result;

            // 第二层：中优先级匹配
            result = MatchMediumPriority(logText);
            if (result != null) return result;

            // 第三层：低优先级 / 兜底
            result = MatchLowPriority(logText, exitCode, errorMessage);
            if (result != null) return result;

            return new CrashAnalysisResult { Reason = CrashReason.Unknown, Priority = 3 };
        }

        // ═══════════════════════════════════════════════
        // 第一层：高优先级
        // ═══════════════════════════════════════════════

        private static CrashAnalysisResult MatchHighPriority(string log, int exitCode, string errorMsg)
        {
            // ── Java 相关 ──

            if (log.Contains("Unrecognized option:") || log.Contains("Unrecognized VM option"))
                return Result(CrashReason.JavaJvmArgError, 1, "JVM 启动参数错误",
                    "Java 虚拟机无法识别某些启动参数。",
                    "请检查「设置 → Java 参数」中是否有多余或错误的参数（如 -Xmn、-XX:PermSize 等在新版 Java 中已废弃）。");

            if (log.Contains("java.lang.UnsupportedClassVersionError"))
                return Result(CrashReason.JavaVersionTooOld, 1, "Java 版本过低",
                    "当前 Java 版本太旧，不支持这个 Minecraft 版本。",
                    "请在启动设置中更换为更高版本的 Java（例如 Java 17 或 Java 21）。");

            if (Match(log, @"Java\s+version\s+(\d+)[\s\S]*?(?:unsupported|incompatible|too (?:old|low))"))
                return Result(CrashReason.JavaVersionTooOld, 1, "Java 版本不兼容",
                    "当前 Java 版本与 Minecraft 版本不兼容。",
                    "在启动设置中更换为匹配的 Java 版本（1.16.5 及以下需要 Java 8，1.17 需要 Java 16，1.18+ 需要 Java 17，1.20.5+ 需要 Java 21）。");

            if (Match(log, @"java\.lang\.NoClassDefFoundError.*java/\d+"))
                return Result(CrashReason.JavaVersionTooOld, 1, "Java 版本不兼容",
                    "Minecraft 需要更高版本的 Java 才能运行。",
                    "在启动设置中更换为更高版本的 Java（推荐 Java 17 或 Java 21）。");

            // ── 内存相关 ──

            if (log.Contains("java.lang.OutOfMemoryError") || log.Contains("Out of memory"))
                return Result(CrashReason.OutOfMemory, 1, "内存不足",
                    "游戏因内存不足而崩溃。",
                    "点击「设置」加大分配内存（建议至少 4096 MB），或尝试使用更多 → 百宝箱中的内存优化功能。");

            if (Match(log, @"Could not reserve enough space for (?:object heap|\d+KB)"))
                return Result(CrashReason.NotEnoughRam, 1, "系统内存不足",
                    "操作系统无法为 Java 分配足够的内存。",
                    "请关闭其他占用内存的程序后重试，或在启动设置中降低分配的内存大小。");

            // ── Mod / 加载器 ──

            if (Match(log, @"OptiFine.*not compatible|OptiFine.*incompatible|NoSuchMethodError.*optifine", RegexOptions.IgnoreCase))
                return Result(CrashReason.OptifineForgeConflict, 1, "OptiFine 与 Forge 不兼容",
                    "当前安装的 OptiFine 版本不支持你的 Forge 版本。",
                    "前往 OptiFine 官网查看你的 Forge 版本所兼容的 OptiFine 版本，或改用支持 OptiFine 的替代方案（如 Embeddium + Oculus）。");

            if (Match(log, @"ShadersMod.*OptiFine|ShadersModCore.*already present"))
                return Result(CrashReason.ModDuplicate, 1, "光影 Mod 冲突",
                    "请不要同时安装 ShadersMod 和 OptiFine。",
                    "删除 .minecraft/mods 中的 ShadersModCore.jar，OptiFine 已内置光影加载功能。");

            if (Match(log, @"fabric-api.*not found|Fabric API.*missing|could not find.*fabric-api", RegexOptions.IgnoreCase))
                return Result(CrashReason.FabricApiMissing, 1, "Fabric API 缺失",
                    "Fabric 模组加载器需要安装 Fabric API 才能运行大多数模组。",
                    "请从 Modrinth 或 CurseForge 下载并安装 Fabric API 到 mods 文件夹。");

            if (Match(log, @"Duplicate mod|duplicate.*mod|mod with id.*already registered|already present.*Duplicate"))
                return Result(CrashReason.ModDuplicate, 1, "模组重复安装",
                    "mods 文件夹中存在重复的模组文件。",
                    "检查 .minecraft/mods 文件夹，删除不同版本的同一个模组，只保留最新版本。");

            if (Match(log, @"Missing dependencies|Requires.*to be installed|mod.*requires.*but it is missing"))
                return Result(CrashReason.ModMissingDependency, 1, "模组缺少前置依赖",
                    "某个模组所依赖的前置模组没有安装。",
                    "请检查报错信息中提示的缺失模组名称，下载并安装对应的前置模组。");

            if (Match(log, @"Extracted mod jars found|Extracted mod"))
                return Result(CrashReason.ModIncompatible, 1, "模组被解压",
                    "请不要将模组 jar 文件解压！",
                    "模组应该直接放入 mods 文件夹中，不要解压成文件夹。删除被解压的模组文件夹即可。");

            if (Match(log, @"Forge\.(?:jar|mods\.toml) not found|forge\.jar.*not found"))
                return Result(CrashReason.ForgeIncomplete, 1, "Forge 安装不完整",
                    "Forge 没有正确安装，缺少必要的文件。",
                    "请重新安装 Forge，或在启动器中换用一个新的 Forge 版本。");

            // ── 文件 / 路径 ──

            if (Match(log, @"classpath.*not found|No such file|找不到文件|错误.*路径"))
                return Result(CrashReason.FileNotFound, 1, "文件缺失",
                    "Minecraft 所需的文件丢失。",
                    "可能是版本文件下载不完整，请尝试删除该版本文件夹后重新安装。");

            if (errorMsg != null && errorMsg.Contains("启动异常") && log.Contains("System.IO.FileNotFoundException"))
                return Result(CrashReason.FileNotFound, 1, "文件缺失",
                    "启动过程中找不到关键文件。",
                    "请检查 Minecraft 版本文件是否完整，必要时重新下载版本。");

            // ── 系统 / 驱动 ──

            if (Match(log, @"EXCEPTION_ACCESS_VIOLATION.*(?:ig\d+icd64|atio6axx|nvoglv64|nvwgf2umx)"))
                return Result(CrashReason.DriverIncompatible, 1, "显卡驱动不兼容",
                    "显卡驱动程序导致游戏崩溃。",
                    "请更新显卡驱动到最新版本。Intel 核显用户可尝试安装旧版驱动；NVIDIA/AMD 用户请从官网下载最新驱动。");

            if (Match(log, @"Pixel format not accelerated|Couldn't set pixel format"))
                return Result(CrashReason.PixelFormatError, 1, "显卡不支持所需像素格式",
                    "显卡或驱动不支持 Minecraft 需要的 OpenGL 像素格式。",
                    "请更新显卡驱动。如果问题仍然存在，可能是显卡过于老旧或驱动损坏。");

            if (Match(log, @"No OpenGL context found|GLFW error.*No WGL|no opengl|OpenGL\.error"))
                return Result(CrashReason.OpenGLNotSupported, 1, "OpenGL 不可用",
                    "系统不支持 OpenGL 图形加速。",
                    "请确保已安装显卡驱动（非 Microsoft Basic Display Adapter）。Intel 用户可能需要安装显卡驱动，而不是仅用 Windows 默认驱动。");

            // ── 启动异常 ──

            if (errorMsg != null && errorMsg.Contains("启动后立即退出") && exitCode == 1)
                return Result(CrashReason.ProcessImmediateExit, 1, "游戏立即崩溃",
                    "游戏在启动后立即退出（退出码 1），通常表示 Minecraft 或模组存在问题。",
                    "查看下方错误日志获取详细原因，或尝试不使用任何模组测试纯净版本能否启动。");

            if (exitCode != 0 && log.Length < 100)
                return Result(CrashReason.ProcessImmediateExit, 1, "游戏启动失败",
                    log.Contains("【错误】") ? log : "游戏进程启动后立即退出，无足够日志信息。",
                    "请检查 Java 是否正确安装、游戏版本是否完整。可尝试用管理员身份运行启动器。");

            return null;
        }

        // ═══════════════════════════════════════════════
        // 第二层：中优先级
        // ═══════════════════════════════════════════════

        private static CrashAnalysisResult MatchMediumPriority(string log)
        {
            // Mixin 注入失败
            if (Match(log, @"Mixin prepare failed|Mixin apply failed|MixinApplyError|MixinTransformerError"))
            {
                // 尝试提取 Mod 名
                var modMatch = Regex.Match(log, @"mixin\.(\w+)\.json", RegexOptions.IgnoreCase);
                if (modMatch.Success)
                {
                    return Result(CrashReason.MixinInjectionFailed, 2, "Mixin 注入失败",
                        $"Mixin 注入过程中 {modMatch.Groups[1].Value} 的转换器出错。",
                        "这通常是模组之间的兼容性问题。请尝试更新相关模组到最新版本，或逐个禁用模组排查问题来源。");
                }
                return Result(CrashReason.MixinInjectionFailed, 2, "Mixin 注入失败",
                    "Mixin 注入过程中出现错误。",
                    "这通常是模组兼容性问题。可以尝试更新所有模组，或逐个禁用模组来定位出问题的模组。");
            }

            // 通用的 Mod 崩溃模式
            if (Match(log, @"Caused by: [\s\S]*?at \w[\w.]*\.(?:mod|Mod|client|Client)[\w.]*\.(\w+)"))
                return Result(CrashReason.ModCausingCrash, 2, "模组导致崩溃",
                    "堆栈信息显示某个模组导致了游戏崩溃。",
                    "建议先禁用近期新增的模组尝试启动，然后逐个启用以找到问题来源。");

            // Fabric loader 错误
            if (Match(log, @"Fabric Loader.*crash|fabric.*crash|loader.*crash", RegexOptions.IgnoreCase))
                return Result(CrashReason.ModCausingCrash, 2, "Fabric 加载器错误",
                    "Fabric 模组加载器在加载模组时崩溃。",
                    "检查最近安装的 Fabric 模组是否与当前 Fabric Loader 版本兼容。尝试禁用部分模组定位问题。");

            // 认证失败
            if (Match(log, @"401 Unauthorized|Invalid credentials|Yggdrasil authentication|auth\w* fail", RegexOptions.IgnoreCase))
                return Result(CrashReason.AuthFailed, 2, "认证失败",
                    "Minecraft 账号认证失败。",
                    "请在启动器设置中重新登录你的 Minecraft 账号。如果使用离线模式，请确认启动方式已切换为离线登录。");

            return null;
        }

        // ═══════════════════════════════════════════════
        // 第三层：低优先级 / 兜底
        // ═══════════════════════════════════════════════

        private static CrashAnalysisResult MatchLowPriority(string log, int exitCode, string errorMsg)
        {
            // 检测异常类名
            if (Match(log, @"java\.lang\.(\w+Error|\w+Exception)"))
                return Result(CrashReason.GameException, 3, "Java 异常",
                    "游戏运行过程中抛出了未处理的 Java 异常。",
                    "可能是模组问题或游戏本身 bug。可以尝试更新模组、切换版本或查看详细堆栈信息。");

            // Native 提取失败
            if (Match(log, @"extract.*native|native.*fail|LWJGL.*native", RegexOptions.IgnoreCase))
                return Result(CrashReason.NativeExtractFailed, 3, "运行库提取失败",
                    "Minecraft 的运行库文件（native libraries）提取失败。",
                    "请检查磁盘空间是否充足、.minecraft 目录是否有写入权限。可以尝试删除版本文件夹中的 natives 文件夹后重新启动。");

            // 网络相关
            if (Match(log, @"UnknownHostException|ConnectException|SocketTimeoutException|Failed to connect"))
                return Result(CrashReason.NetworkError, 3, "网络连接失败",
                    "无法连接到 Minecraft 服务器或资源下载服务器。",
                    "请检查网络连接，或尝试更换网络环境。如果使用 VPN，请先关闭 VPN 后重试。");

            // 路径特殊字符
            if (errorMsg != null && Match(errorMsg, @"[！!;；]") || Match(log, @"illegal character.*path|路径.*非法字符", RegexOptions.IgnoreCase))
                return Result(CrashReason.PathContainsSpecialChar, 3, "路径包含特殊字符",
                    "游戏路径中存在特殊字符（如 ！! ;）。",
                    "请将 Minecraft 文件夹移动到一个不含特殊字符的路径下。");

            return null;
        }

        // ═══════════════════════════════════════════════
        // 仅根据退出码分析
        // ═══════════════════════════════════════════════

        private static CrashAnalysisResult AnalyzeByExitCode(int exitCode, string errorMsg)
        {
            if (errorMsg != null && errorMsg.Contains("启动异常"))
                return Result(CrashReason.ProcessImmediateExit, 3, "启动异常",
                    "游戏启动过程中出现未预期的错误。",
                    "请查看启动日志获取详细信息，或使用 AI 分析功能。");

            return exitCode switch
            {
                1 => Result(CrashReason.ProcessImmediateExit, 3, "游戏异常退出 (退出码 1)",
                    "游戏启动后立即退出，可能是 Java 环境或游戏文件存在问题。",
                    "检查 Java 是否正确安装、游戏版本是否完整。"),
                _ => new CrashAnalysisResult { Reason = CrashReason.Unknown, Priority = 3 },
            };
        }

        // ═══════════════════════════════════════════════
        // 结果格式化
        // ═══════════════════════════════════════════════

        /// <summary>将分析结果格式化为可展示的中文文本</summary>
        public static string FormatResult(CrashAnalysisResult result)
        {
            if (result == null || !result.IsKnown) return null;

            var lines = new List<string>
            {
                $"【问题诊断】{result.Title}",
                $"📝 原因：{result.Description}",
                $"💡 建议：{result.Suggestion}",
            };
            return string.Join("\n\n", lines);
        }

        /// <summary>生成完整的分析报告（含用于 AI 回退的提示语）</summary>
        public static string GenerateReport(CrashAnalysisResult result)
        {
            var knownText = FormatResult(result);
            if (!string.IsNullOrEmpty(knownText))
            {
                return $"✅ 本地分析已完成检测到已知问题模式，结果如下：\n\n{knownText}";
            }

            return "⚠️ 本地分析未匹配到已知错误模式，建议使用 AI 分析获取详细诊断。";
        }

        // ═══════════════════════════════════════════════
        // 工具方法
        // ═══════════════════════════════════════════════

        private static CrashAnalysisResult Result(CrashReason reason, int priority,
            string title, string description, string suggestion)
        {
            return new CrashAnalysisResult
            {
                Reason = reason,
                Priority = priority,
                Title = title,
                Description = description,
                Suggestion = suggestion,
            };
        }

        private static bool Match(string input, string pattern, RegexOptions options = RegexOptions.IgnoreCase)
        {
            if (string.IsNullOrEmpty(input)) return false;
            return Regex.IsMatch(input, pattern, options | RegexOptions.Compiled);
        }
    }
}
