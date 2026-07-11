using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 整合包安装结果
    /// </summary>
    public class ModpackInstallResult
    {
        /// <summary>安装后可用于启动的版本 ID</summary>
        public string VersionId { get; set; }

        /// <summary>整合包名称</summary>
        public string ModpackName { get; set; }

        /// <summary>Minecraft 版本</summary>
        public string MinecraftVersion { get; set; }

        /// <summary>加载器类型（fabric / forge / quilt）</summary>
        public string LoaderType { get; set; }

        /// <summary>是否安装成功</summary>
        public bool Success { get; set; }

        /// <summary>错误信息</summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Modrinth .mrpack 整合包清单
    /// </summary>
    public class ModrinthManifest
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonPropertyName("game")]
        public string Game { get; set; }

        [JsonPropertyName("versionId")]
        public string VersionId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("dependencies")]
        public Dictionary<string, string> Dependencies { get; set; }

        [JsonPropertyName("files")]
        public List<ModrinthManifestFile> Files { get; set; }
    }

    public class ModrinthManifestFile
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("downloads")]
        public List<string> Downloads { get; set; }

        [JsonPropertyName("hashes")]
        public Dictionary<string, string> Hashes { get; set; }

        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("env")]
        public Dictionary<string, string> Env { get; set; }
    }

    /// <summary>
    /// 整合包安装器 —— 下载、解压 .mrpack，安装加载器与模组，创建可启动的版本
    /// </summary>
    public class ModpackInstaller
    {
        private readonly string _minecraftPath;
        private readonly HttpClient _httpClient;

        public event Action<string> StatusChanged;
        public event Action<int> ProgressChanged;

        public ModpackInstaller(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MusicalNoteLauncher/1.0");
        }

        /// <summary>
        /// 安装 Modrinth .mrpack 整合包
        /// </summary>
        /// <param name="mrpackFilePath">已下载的 .mrpack 文件路径</param>
        /// <param name="modpackDisplayName">整合包显示名称（用于版本 ID）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<ModpackInstallResult> InstallFromMrpackAsync(
            string mrpackFilePath, string modpackDisplayName,
            CancellationToken cancellationToken = default)
        {
            var result = new ModpackInstallResult
            {
                ModpackName = modpackDisplayName,
                Success = false
            };

            try
            {
                // 步骤1：解压 .mrpack 并解析清单
                ReportStatus("正在解析整合包...");
                ReportProgress(5);

                if (!File.Exists(mrpackFilePath))
                {
                    result.ErrorMessage = "整合包文件不存在";
                    return result;
                }

                ModrinthManifest manifest;
                string extractTempDir;
                using (var archive = ZipFile.OpenRead(mrpackFilePath))
                {
                    var manifestEntry = archive.GetEntry("modrinth.index.json");
                    if (manifestEntry == null)
                    {
                        result.ErrorMessage = "整合包格式无效：缺少 modrinth.index.json";
                        return result;
                    }

                    using (var stream = manifestEntry.Open())
                    {
                        string json = await new StreamReader(stream).ReadToEndAsync();
                        manifest = JsonSerializer.Deserialize<ModrinthManifest>(json);
                    }

                    if (manifest == null)
                    {
                        result.ErrorMessage = "解析整合包清单失败";
                        return result;
                    }

                    // 提取 overrides 到临时目录
                    extractTempDir = Path.Combine(Path.GetTempPath(), $"mrp-extract-{Guid.NewGuid()}");
                    Directory.CreateDirectory(extractTempDir);

                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrEmpty(entry.Name))
                        {
                            string relativePath = entry.FullName.Substring("overrides/".Length);
                            string destPath = Path.Combine(extractTempDir, relativePath);
                            string destDir = Path.GetDirectoryName(destPath);
                            if (!Directory.Exists(destDir))
                                Directory.CreateDirectory(destDir);
                            entry.ExtractToFile(destPath, overwrite: true);
                        }
                    }
                }

                ReportProgress(10);

                // 步骤2：解析版本与加载器
                string mcVer = null;
                manifest.Dependencies?.TryGetValue("minecraft", out mcVer);
                result.MinecraftVersion = mcVer ?? manifest.VersionId ?? "";
                if (string.IsNullOrEmpty(result.MinecraftVersion))
                {
                    result.ErrorMessage = "无法确定整合包所需的 Minecraft 版本";
                    return result;
                }

                result.LoaderType = DetectLoaderType(manifest.Dependencies);
                string loaderVersion = GetLoaderVersion(manifest.Dependencies, result.LoaderType);

                ReportStatus($"目标: Minecraft {result.MinecraftVersion} + {result.LoaderType}");
                ReportProgress(15);

                // 步骤3：安装加载器
                string loaderVersionId;
                bool loaderInstalled = await InstallLoaderAsync(
                    result.MinecraftVersion, result.LoaderType, loaderVersion,
                    cancellationToken);
                if (!loaderInstalled)
                {
                    result.ErrorMessage = $"{result.LoaderType} 加载器安装失败";
                    return result;
                }

                // 获取加载器创建的版本 ID
                loaderVersionId = FindLoaderVersionId(result.MinecraftVersion, result.LoaderType);
                if (string.IsNullOrEmpty(loaderVersionId))
                {
                    result.ErrorMessage = "无法找到加载器版本 ID";
                    return result;
                }

                ReportProgress(50);

                // 步骤4：创建整合包版本（继承自加载器版本）
                string modpackVersionId = SanitizeVersionId(modpackDisplayName);
                CreateModpackVersion(modpackVersionId, loaderVersionId, modpackDisplayName,
                    result.MinecraftVersion, result.LoaderType);

                // 版本隔离目录：避免整合包文件污染其他版本
                string isolatedGameDir = Path.Combine(_minecraftPath, "versions", modpackVersionId, "game");
                Directory.CreateDirectory(isolatedGameDir);

                ReportProgress(60);

                // 步骤5：复制 overrides 到版本隔离目录
                ReportStatus("正在复制配置文件...");
                CopyOverrides(extractTempDir, isolatedGameDir);
                if (Directory.Exists(extractTempDir))
                {
                    try { Directory.Delete(extractTempDir, true); } catch { }
                }

                ReportProgress(70);

                // 步骤6：下载模组到版本隔离目录
                if (manifest.Files != null && manifest.Files.Count > 0)
                {
                    ReportStatus($"正在下载 {manifest.Files.Count} 个模组...");
                    await DownloadModFilesAsync(manifest.Files, isolatedGameDir, cancellationToken);
                }

                ReportProgress(95);

                // 步骤7：刷新版本扫描
                ReportStatus("正在刷新版本列表...");
                await VersionScanService.Instance.ScanAsync("整合包安装完成");

                ReportProgress(100);
                ReportStatus($"整合包 {modpackDisplayName} 安装完成！");

                result.VersionId = modpackVersionId;
                result.Success = true;
                return result;
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "安装已取消";
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[整合包安装] 安装失败: {ex.Message}");
                result.ErrorMessage = $"安装失败: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 从依赖字典中检测加载器类型
        /// </summary>
        private static string DetectLoaderType(Dictionary<string, string> dependencies)
        {
            if (dependencies == null) return "fabric";

            foreach (var key in dependencies.Keys)
            {
                var lower = key.ToLowerInvariant();
                if (lower == "fabric-loader" || lower == "fabric") return "fabric";
                if (lower == "forge" || lower == "forge-loader") return "forge";
                if (lower == "neoforge" || lower == "neoforge-loader") return "neoforge";
                if (lower == "quilt-loader" || lower == "quilt") return "quilt";
            }
            return "fabric"; // 默认
        }

        /// <summary>
        /// 获取加载器版本号
        /// </summary>
        private static string GetLoaderVersion(Dictionary<string, string> dependencies, string loaderType)
        {
            if (dependencies == null) return null;

            foreach (var kvp in dependencies)
            {
                var lower = kvp.Key.ToLowerInvariant();
                if ((loaderType == "fabric" && lower.Contains("fabric")) ||
                    (loaderType == "forge" && lower.Contains("forge")) ||
                    (loaderType == "neoforge" && lower.Contains("neoforge")) ||
                    (loaderType == "quilt" && lower.Contains("quilt")))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// 安装加载器
        /// </summary>
        private async Task<bool> InstallLoaderAsync(string mcVersion, string loaderType,
            string loaderVersion, CancellationToken ct)
        {
            try
            {
                switch (loaderType)
                {
                    case "fabric":
                    case "quilt":
                        return await InstallFabricLoaderAsync(mcVersion, loaderVersion, ct);

                    case "forge":
                        return await InstallForgeLoaderAsync(mcVersion, loaderVersion, ct);

                    case "neoforge":
                        // NeoForge 暂时使用 Forge 类似的方式，后期可以添加专门的 NeoForge 支持
                        return await InstallForgeLoaderAsync(mcVersion, loaderVersion, ct);

                    default:
                        Logger.Warning($"[整合包安装] 不支持的加载器类型: {loaderType}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[整合包安装] 安装加载器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 安装 Fabric 加载器
        /// </summary>
        private async Task<bool> InstallFabricLoaderAsync(string mcVersion, string loaderVersion, CancellationToken ct)
        {
            try
            {
                var fabricInstaller = new FabricInstaller(_minecraftPath);
                fabricInstaller.StatusChanged += s => ReportStatus(s);

                // 获取可用的 Fabric 版本
                var fabricVersions = await fabricInstaller.GetFabricVersionsAsync(mcVersion, ct);
                if (fabricVersions == null || fabricVersions.Count == 0)
                {
                    ReportStatus("无法获取 Fabric 版本列表");
                    return false;
                }

                FabricVersionInfo selectedVersion;
                if (!string.IsNullOrEmpty(loaderVersion))
                {
                    selectedVersion = fabricVersions.FirstOrDefault(v =>
                        v.LoaderVersion == loaderVersion);
                    if (selectedVersion == null)
                    {
                        Logger.Warning($"[整合包安装] 未找到 Fabric {loaderVersion}，使用最新版本");
                        selectedVersion = fabricVersions[0];
                    }
                }
                else
                {
                    selectedVersion = fabricVersions[0];
                }

                ReportStatus($"安装 Fabric Loader {selectedVersion.LoaderVersion}...");
                return await fabricInstaller.InstallFabricAsync(mcVersion, selectedVersion, ct);
            }
            catch (Exception ex)
            {
                Logger.Error($"[整合包安装] Fabric 安装异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 安装 Forge 加载器
        /// </summary>
        private async Task<bool> InstallForgeLoaderAsync(string mcVersion, string loaderVersion, CancellationToken ct)
        {
            try
            {
                var forgeInstaller = new ForgeInstaller(_minecraftPath);
                forgeInstaller.StatusChanged += s => ReportStatus(s);

                var forgeVersions = await forgeInstaller.GetForgeVersionsAsync(mcVersion, ct);
                if (forgeVersions == null || forgeVersions.Count == 0)
                {
                    ReportStatus("无法获取 Forge 版本列表");
                    return false;
                }

                // 优先选择稳定版
                var stableVersion = forgeVersions.FirstOrDefault(v =>
                    !v.ForgeVersion.Contains("-beta", StringComparison.OrdinalIgnoreCase) &&
                    !v.ForgeVersion.Contains("-alpha", StringComparison.OrdinalIgnoreCase));
                var selectedVersion = stableVersion ?? forgeVersions[0];

                ReportStatus($"安装 Forge {selectedVersion.ForgeVersion}...");
                return await forgeInstaller.InstallForgeAsync(mcVersion, selectedVersion, ct);
            }
            catch (Exception ex)
            {
                Logger.Error($"[整合包安装] Forge 安装异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 查找加载器创建的版本 ID
        /// </summary>
        private string FindLoaderVersionId(string mcVersion, string loaderType)
        {
            string versionsDir = Path.Combine(_minecraftPath, "versions");
            if (!Directory.Exists(versionsDir)) return null;

            switch (loaderType)
            {
                case "fabric":
                case "quilt":
                    // Fabric 版本 ID 格式: fabric-loader-{mcVersion}
                    var fabricDirs = Directory.GetDirectories(versionsDir)
                        .Select(d => Path.GetFileName(d))
                        .Where(d => d.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(d => d)
                        .ToList();
                    return fabricDirs.FirstOrDefault();

                case "forge":
                    // Forge 版本 ID 格式: {mcVersion}-forge-{forgeVersion}
                    var forgeDirs = Directory.GetDirectories(versionsDir)
                        .Select(d => Path.GetFileName(d))
                        .Where(d => d.StartsWith(mcVersion + "-forge-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(d => d)
                        .ToList();
                    return forgeDirs.FirstOrDefault();

                default:
                    return null;
            }
        }

        /// <summary>
        /// 创建整合包版本（继承自加载器版本）
        /// </summary>
        private void CreateModpackVersion(string modpackVersionId, string loaderVersionId,
            string displayName, string mcVersion, string loaderType)
        {
            string versionDir = Path.Combine(_minecraftPath, "versions", modpackVersionId);
            if (!Directory.Exists(versionDir))
                Directory.CreateDirectory(versionDir);

            var versionJson = new
            {
                id = modpackVersionId,
                inheritsFrom = loaderVersionId,
                type = "release",
                releaseTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                mainClass = "net.minecraft.client.main.Main",
                arguments = new
                {
                    game = new object[] { }
                },
                libraries = new object[] { }
            };

            string jsonPath = Path.Combine(versionDir, $"{modpackVersionId}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(versionJson, options);
            File.WriteAllText(jsonPath, json);

            Logger.Info($"[整合包安装] 创建版本: {modpackVersionId} <- {loaderVersionId}");
        }

        /// <summary>
        /// 复制 overrides 到游戏目录
        /// </summary>
        private void CopyOverrides(string overridesDir, string gameDir)
        {
            if (!Directory.Exists(overridesDir)) return;

            foreach (string srcPath in Directory.GetFiles(overridesDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = srcPath.Substring(overridesDir.Length + 1);
                string destPath = Path.Combine(gameDir, relativePath);
                string destDir = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                File.Copy(srcPath, destPath, overwrite: true);
            }

            Logger.Info($"[整合包安装] 已复制 overrides 到游戏目录");
        }

        /// <summary>
        /// 下载模组文件
        /// </summary>
        private async Task DownloadModFilesAsync(List<ModrinthManifestFile> files,
            string gameDir, CancellationToken ct)
        {
            int total = files.Count;
            int downloaded = 0;
            int skipped = 0;
            int failed = 0;

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                string destPath = Path.Combine(gameDir, file.Path);
                string destDir = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                // 如果文件已存在且大小匹配，跳过
                if (File.Exists(destPath))
                {
                    var fileInfo = new FileInfo(destPath);
                    if (fileInfo.Length == file.FileSize && file.FileSize > 0)
                    {
                        skipped++;
                        downloaded++;
                        int pct = 70 + (int)(25.0 * downloaded / total);
                        ReportProgress(pct);
                        continue;
                    }
                }

                // 尝试下载
                if (file.Downloads == null || file.Downloads.Count == 0)
                {
                    Logger.Warning($"[整合包安装] 模组 {file.Path} 没有下载链接");
                    failed++;
                    continue;
                }

                bool success = false;
                foreach (string url in file.Downloads)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(url, ct);
                        if (response.IsSuccessStatusCode)
                        {
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(destPath, FileMode.Create))
                            {
                                await stream.CopyToAsync(fileStream);
                            }
                            success = true;
                            break;
                        }
                    }
                    catch
                    {
                        // 尝试下一个 URL
                    }
                }

                if (!success)
                {
                    Logger.Warning($"[整合包安装] 下载模组失败: {file.Path}");
                    failed++;
                }

                downloaded++;
                int progress = 70 + (int)(25.0 * downloaded / total);
                ReportProgress(progress);
                ReportStatus($"下载模组... ({downloaded}/{total})");
            }

            Logger.Info($"[整合包安装] 模组下载完成: {downloaded} 个文件 (跳过 {skipped}, 失败 {failed})");
        }

        /// <summary>
        /// 清理版本 ID 中的非法字符
        /// </summary>
        private static string SanitizeVersionId(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "modpack";

            // 移除非法字符，只保留字母、数字、中文、连字符、下划线
            var chars = name
                .Replace(' ', '-')
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c >= 0x4e00)
                .ToArray();
            string sanitized = new string(chars);

            if (string.IsNullOrEmpty(sanitized))
                return "modpack";

            // 截断过长名称
            if (sanitized.Length > 64)
                sanitized = sanitized.Substring(0, 64);

            return sanitized.Trim('-', '_');
        }

        private void ReportStatus(string message)
        {
            StatusChanged?.Invoke(message);
            Logger.Info($"[整合包安装] {message}");
        }

        private void ReportProgress(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            ProgressChanged?.Invoke(percent);
        }
    }
}
