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
    /// 整合包探测结果（元数据）
    /// </summary>
    public class ModpackInfo
    {
        /// <summary>整合包类型（modrinth / curseforge / generic）</summary>
        public string PackType { get; set; }

        /// <summary>整合包名称</summary>
        public string Name { get; set; }

        /// <summary>整合包版本</summary>
        public string Version { get; set; }

        /// <summary>Minecraft 版本</summary>
        public string MinecraftVersion { get; set; }

        /// <summary>加载器类型（fabric / forge / neoforge / quilt）</summary>
        public string LoaderType { get; set; }

        /// <summary>加载器版本</summary>
        public string LoaderVersion { get; set; }

        /// <summary>整合包简介</summary>
        public string Summary { get; set; }

        /// <summary>作者</summary>
        public string Author { get; set; }

        /// <summary>模组数量</summary>
        public int ModCount { get; set; }
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

    // ═══════════════════════════════════════════════
    // CurseForge / 通用 Manifest 整合包模型
    // ═══════════════════════════════════════════════

    /// <summary>CurseForge manifest.json 根对象</summary>
    public class CurseForgeManifest
    {
        [JsonPropertyName("minecraft")]
        public CurseForgeManifestMinecraft Minecraft { get; set; }

        [JsonPropertyName("manifestType")]
        public string ManifestType { get; set; }

        [JsonPropertyName("manifestVersion")]
        public int ManifestVersion { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("overrides")]
        public string Overrides { get; set; } = "overrides";

        [JsonPropertyName("files")]
        public List<CurseForgeManifestFile> Files { get; set; }
    }

    public class CurseForgeManifestMinecraft
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("modLoaders")]
        public List<CurseForgeManifestLoader> ModLoaders { get; set; }
    }

    public class CurseForgeManifestLoader
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }
    }

    public class CurseForgeManifestFile
    {
        [JsonPropertyName("projectID")]
        public long ProjectId { get; set; }

        [JsonPropertyName("fileID")]
        public long FileId { get; set; }

        [JsonPropertyName("required")]
        public bool Required { get; set; }
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
        /// 探测整合包元数据（不执行安装）
        /// </summary>
        /// <param name="filePath">整合包文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static ModpackInfo ProbeModpack(string filePath)
        {
            var info = new ModpackInfo();
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            info.Name = fileName;
            info.PackType = "generic";

            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                using (var archive = ZipFile.OpenRead(filePath))
                {
                    // Modrinth 格式
                    var mrpackEntry = archive.GetEntry("modrinth.index.json");
                    if (mrpackEntry != null || ext == ".mrpack")
                    {
                        info.PackType = "modrinth";
                        if (mrpackEntry == null)
                            mrpackEntry = archive.GetEntry("modrinth.index.json");
                        if (mrpackEntry != null)
                        {
                            using var stream = mrpackEntry.Open();
                            using var doc = JsonDocument.Parse(stream);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("name", out var nameProp))
                                info.Name = nameProp.GetString() ?? info.Name;
                            if (root.TryGetProperty("versionId", out var verProp))
                                info.Version = verProp.GetString();
                            if (root.TryGetProperty("summary", out var sumProp))
                                info.Summary = sumProp.GetString();
                            if (root.TryGetProperty("dependencies", out var depsProp))
                            {
                                if (depsProp.TryGetProperty("minecraft", out var mcProp))
                                    info.MinecraftVersion = mcProp.GetString();
                                if (depsProp.TryGetProperty("forge", out var forgeProp))
                                {
                                    info.LoaderType = "forge";
                                    info.LoaderVersion = forgeProp.GetString();
                                }
                                else if (depsProp.TryGetProperty("fabric-loader", out var fabricProp))
                                {
                                    info.LoaderType = "fabric";
                                    info.LoaderVersion = fabricProp.GetString();
                                }
                                else if (depsProp.TryGetProperty("quilt-loader", out var quiltProp))
                                {
                                    info.LoaderType = "quilt";
                                    info.LoaderVersion = quiltProp.GetString();
                                }
                                else if (depsProp.TryGetProperty("neoforge", out var neoProp))
                                {
                                    info.LoaderType = "neoforge";
                                    info.LoaderVersion = neoProp.GetString();
                                }
                            }
                            if (root.TryGetProperty("files", out var filesProp))
                                info.ModCount = filesProp.GetArrayLength();
                        }
                        return info;
                    }

                    // MCBBS 整合包（优先于 manifest.json 判断）
                    var mcbbsEntry = archive.GetEntry("mcbbs.packmeta");
                    if (mcbbsEntry != null)
                    {
                        info.PackType = "mcbbs";
                        try
                        {
                            using var stream = mcbbsEntry.Open();
                            using var doc = JsonDocument.Parse(stream);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("name", out var nameProp))
                                info.Name = nameProp.GetString() ?? info.Name;
                            if (root.TryGetProperty("version", out var verProp))
                                info.Version = verProp.GetString();
                            if (root.TryGetProperty("author", out var authorProp))
                                info.Author = authorProp.GetString();
                        }
                        catch { }
                        // 继续检测 manifest.json 获取更多信息
                    }

                    // HMCL 整合包
                    var hmclEntry = archive.GetEntry("modpack.json");
                    if (hmclEntry != null)
                    {
                        info.PackType = "hmcl";
                        try
                        {
                            using var stream = hmclEntry.Open();
                            using var doc = JsonDocument.Parse(stream);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("name", out var nameProp))
                                info.Name = nameProp.GetString() ?? info.Name;
                            if (root.TryGetProperty("version", out var verProp))
                                info.Version = verProp.GetString();
                            if (root.TryGetProperty("mcVersion", out var mcProp))
                                info.MinecraftVersion = mcProp.GetString();
                            if (root.TryGetProperty("author", out var authorProp))
                                info.Author = authorProp.GetString();
                        }
                        catch { }
                    }

                    // MMC 整合包
                    var mmcEntry = archive.GetEntry("mmc-pack.json");
                    if (mmcEntry != null)
                    {
                        info.PackType = "mmc";
                        // MMC 格式的名称通常在实例目录名中，后面再处理
                    }

                    // CurseForge / MCBBS 格式（都有 manifest.json）
                    var manifestEntry = archive.GetEntry("manifest.json");
                    if (manifestEntry != null)
                    {
                        using var stream = manifestEntry.Open();
                        using var doc = JsonDocument.Parse(stream);
                        var root = doc.RootElement;

                        // 如果有 addons 字段，是 MCBBS 格式
                        if (root.TryGetProperty("addons", out _))
                        {
                            info.PackType = "mcbbs";
                        }
                        else if (info.PackType == "generic")
                        {
                            info.PackType = "curseforge";
                        }

                        if (root.TryGetProperty("name", out var nameProp))
                            info.Name = nameProp.GetString() ?? info.Name;
                        if (root.TryGetProperty("version", out var verProp))
                            info.Version = verProp.GetString();
                        if (root.TryGetProperty("author", out var authorProp))
                            info.Author = authorProp.GetString();
                        if (root.TryGetProperty("minecraft", out var mcProp))
                        {
                            if (mcProp.TryGetProperty("version", out var mcVerProp))
                                info.MinecraftVersion = mcVerProp.GetString();
                            if (mcProp.TryGetProperty("modLoaders", out var loadersProp))
                            {
                                foreach (var loader in loadersProp.EnumerateArray())
                                {
                                    if (loader.TryGetProperty("id", out var idProp))
                                    {
                                        string id = idProp.GetString() ?? "";
                                        if (id.StartsWith("forge-"))
                                        {
                                            info.LoaderType = "forge";
                                            info.LoaderVersion = id.Substring("forge-".Length);
                                            break;
                                        }
                                        else if (id.StartsWith("fabric-"))
                                        {
                                            info.LoaderType = "fabric";
                                            info.LoaderVersion = id.Substring("fabric-".Length);
                                            break;
                                        }
                                        else if (id.StartsWith("neoforge-"))
                                        {
                                            info.LoaderType = "neoforge";
                                            info.LoaderVersion = id.Substring("neoforge-".Length);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (root.TryGetProperty("files", out var filesProp))
                            info.ModCount = filesProp.GetArrayLength();

                        if (info.PackType == "curseforge" || info.PackType == "mcbbs")
                            return info;
                    }

                    // 通用 ZIP：检测是否包含完整的 .minecraft 结构（versions/XXX/XXX.json）
                    var versionRegex = new System.Text.RegularExpressions.Regex(
                        @"^.*\/(?=versions\/(?<ver>[^\/]+)\/(\k<ver>)\.json$)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    string detectedVersionId = null;
                    string archiveBaseFolder = null;
                    int modCount = 0;

                    foreach (var entry in archive.Entries)
                    {
                        string entryPath = "/" + entry.FullName.Replace('\\', '/');
                        var match = versionRegex.Match(entryPath);
                        if (match.Success && match.Groups["ver"].Success)
                        {
                            detectedVersionId = match.Groups["ver"].Value;
                            archiveBaseFolder = match.Value.Replace("/", "\\").TrimStart('\\');
                            break;
                        }
                        if (entry.FullName.Contains("/mods/") && entry.FullName.EndsWith(".jar"))
                            modCount++;
                    }

                    if (!string.IsNullOrEmpty(detectedVersionId))
                    {
                        info.Name = detectedVersionId;
                        info.PackType = "generic";
                        info.ModCount = modCount;

                        // 尝试读取版本 JSON 获取更多信息
                        try
                        {
                            string versionJsonPath = string.IsNullOrEmpty(archiveBaseFolder)
                                ? $"versions/{detectedVersionId}/{detectedVersionId}.json"
                                : $"{archiveBaseFolder.TrimEnd('\\')}/versions/{detectedVersionId}/{detectedVersionId}.json";
                            versionJsonPath = versionJsonPath.Replace('\\', '/');

                            var versionEntry = archive.GetEntry(versionJsonPath);
                            if (versionEntry != null)
                            {
                                using var stream = versionEntry.Open();
                                using var doc = JsonDocument.Parse(stream);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("inheritsFrom", out var inheritProp))
                                {
                                    string inherited = inheritProp.GetString();
                                    if (!string.IsNullOrEmpty(inherited))
                                    {
                                        // 从继承版本推断 MC 版本
                                        if (inherited.Contains("forge"))
                                            info.LoaderType = "forge";
                                        else if (inherited.Contains("fabric"))
                                            info.LoaderType = "fabric";
                                        else if (inherited.Contains("neoforge"))
                                            info.LoaderType = "neoforge";
                                        else if (inherited.Contains("quilt"))
                                            info.LoaderType = "quilt";
                                    }
                                }
                            }
                        }
                        catch { }
                        return info;
                    }

                    // 通用 ZIP：检测 overrides 目录
                    int overrideModCount = 0;
                    foreach (var entry in archive.Entries)
                    {
                        if ((entry.FullName.StartsWith("overrides/mods/") || entry.FullName.StartsWith("mods/"))
                            && entry.FullName.EndsWith(".jar"))
                            overrideModCount++;
                    }
                    if (overrideModCount > 0)
                    {
                        info.ModCount = overrideModCount;
                        if (info.PackType == "generic" && string.IsNullOrEmpty(info.MinecraftVersion))
                        {
                            // 尝试从文件名中提取 MC 版本
                            var mcVerMatch = System.Text.RegularExpressions.Regex.Match(
                                fileName, @"(1\.\d+(\.\d+)?)");
                            if (mcVerMatch.Success)
                                info.MinecraftVersion = mcVerMatch.Value;
                        }
                    }
                }
            }
            catch
            {
                // 探测失败，返回基本信息
            }

            return info;
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
        /// 从含有 manifest.json 的 zip 安装整合包（CurseForge 格式）
        /// </summary>
        public async Task<ModpackInstallResult> InstallFromZipManifestAsync(
            string zipFilePath, string modpackDisplayName,
            CancellationToken cancellationToken = default)
        {
            var result = new ModpackInstallResult
            {
                ModpackName = modpackDisplayName,
                Success = false
            };

            try
            {
                ReportStatus("正在解析整合包清单...");
                ReportProgress(5);

                if (!File.Exists(zipFilePath))
                {
                    result.ErrorMessage = "整合包文件不存在";
                    return result;
                }

                CurseForgeManifest manifest;
                string extractTempDir;

                using (var archive = System.IO.Compression.ZipFile.OpenRead(zipFilePath))
                {
                    var manifestEntry = archive.GetEntry("manifest.json");
                    if (manifestEntry == null)
                    {
                        result.ErrorMessage = "整合包格式无效：缺少 manifest.json";
                        return result;
                    }

                    using (var stream = manifestEntry.Open())
                    {
                        string json = await new StreamReader(stream).ReadToEndAsync();
                        manifest = JsonSerializer.Deserialize<CurseForgeManifest>(json);
                    }

                    if (manifest?.Minecraft == null)
                    {
                        result.ErrorMessage = "解析整合包清单失败";
                        return result;
                    }

                    // 提取 overrides 到临时目录
                    extractTempDir = Path.Combine(Path.GetTempPath(), $"cf-extract-{Guid.NewGuid()}");
                    Directory.CreateDirectory(extractTempDir);

                    string overridesDir = manifest.Overrides ?? "overrides";
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.StartsWith(overridesDir + "/", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrEmpty(entry.Name))
                        {
                            string relativePath = entry.FullName.Substring(overridesDir.Length + 1);
                            string destPath = Path.Combine(extractTempDir, relativePath);
                            string destDir = Path.GetDirectoryName(destPath);
                            if (!Directory.Exists(destDir))
                                Directory.CreateDirectory(destDir);
                            entry.ExtractToFile(destPath, overwrite: true);
                        }
                    }
                }

                ReportProgress(10);

                // 解析版本与加载器
                string mcVer = manifest.Minecraft.Version;
                result.MinecraftVersion = mcVer;
                if (string.IsNullOrEmpty(mcVer))
                {
                    result.ErrorMessage = "无法确定整合包所需的 Minecraft 版本";
                    return result;
                }

                string loaderType = null;
                string loaderVersion = null;
                if (manifest.Minecraft.ModLoaders != null && manifest.Minecraft.ModLoaders.Count > 0)
                {
                    var primary = manifest.Minecraft.ModLoaders.FirstOrDefault(l => l.Primary)
                                  ?? manifest.Minecraft.ModLoaders[0];
                    string loaderId = primary.Id?.ToLowerInvariant() ?? "";

                    if (loaderId.StartsWith("forge-"))
                    {
                        loaderType = "forge";
                        loaderVersion = loaderId.Substring("forge-".Length);
                    }
                    else if (loaderId.StartsWith("neoforge-"))
                    {
                        loaderType = "neoforge";
                        loaderVersion = loaderId.Substring("neoforge-".Length);
                    }
                    else if (loaderId.StartsWith("fabric-"))
                    {
                        loaderType = "fabric";
                        loaderVersion = loaderId.Substring("fabric-".Length);
                    }
                    else if (loaderId.StartsWith("quilt-"))
                    {
                        loaderType = "quilt";
                        loaderVersion = loaderId.Substring("quilt-".Length);
                    }
                }

                string loaderLabel = loaderType ?? "无加载器";
                ReportStatus($"目标: Minecraft {mcVer} + {loaderLabel}");
                ReportProgress(15);

                // 创建版本目录
                string modpackVersionId = SanitizeVersionId(modpackDisplayName);
                string isolatedGameDir = Path.Combine(_minecraftPath, "versions", modpackVersionId, "game");
                Directory.CreateDirectory(isolatedGameDir);

                // 复制 overrides
                ReportStatus("正在复制配置文件...");
                CopyOverrides(extractTempDir, isolatedGameDir);
                if (Directory.Exists(extractTempDir))
                {
                    try { Directory.Delete(extractTempDir, true); } catch { }
                }

                ReportProgress(25);

                // 通过 CurseForge API 批量获取文件下载信息并下载
                if (manifest.Files != null && manifest.Files.Count > 0)
                {
                    ReportStatus($"正在查询 {manifest.Files.Count} 个模组的下载信息...");
                    ReportProgress(30);

                    var fileIds = manifest.Files.Where(f => f.Required).Select(f => f.FileId).ToList();
                    var optionalFiles = manifest.Files.Where(f => !f.Required).ToList();

                    if (optionalFiles.Count > 0)
                    {
                        Logger.Info($"[整合包安装] 发现 {optionalFiles.Count} 个可选模组，将一并下载");
                        fileIds.AddRange(optionalFiles.Select(f => f.FileId));
                    }

                    if (fileIds.Count > 0)
                    {
                        var api = new CurseForgeApiService();
                        var fileInfos = await api.GetFilesBatchAsync(fileIds);
                        ReportProgress(35);

                        if (fileInfos.Count > 0)
                        {
                            ReportStatus($"正在下载 {fileInfos.Count} 个文件到整合包目录...");
                            await DownloadCurseForgeFilesAsync(fileInfos, isolatedGameDir,
                                manifest.Files, cancellationToken);
                        }
                        else
                        {
                            Logger.Warning("[整合包安装] CurseForge API 返回为空，跳过模组下载");
                        }
                    }
                }

                ReportProgress(70);

                // 安装加载器
                if (!string.IsNullOrEmpty(loaderType))
                {
                    ReportStatus($"正在安装 {loaderLabel}...");
                    bool loaderInstalled = await InstallLoaderAsync(
                        mcVer, loaderType, loaderVersion, cancellationToken);
                    if (!loaderInstalled)
                    {
                        result.ErrorMessage = $"{loaderType} 加载器安装失败";
                        return result;
                    }

                    string loaderVersionId = FindLoaderVersionId(mcVer, loaderType);
                    if (string.IsNullOrEmpty(loaderVersionId))
                    {
                        result.ErrorMessage = "无法找到加载器版本 ID";
                        return result;
                    }

                    CreateModpackVersion(modpackVersionId, loaderVersionId, modpackDisplayName,
                        mcVer, loaderType);
                }
                else
                {
                    CreateModpackVersion(modpackVersionId, null, modpackDisplayName, mcVer, null);
                }

                ReportProgress(90);

                // 刷新版本扫描
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
                Logger.Error($"[整合包安装-依赖] 安装失败: {ex.Message}");
                result.ErrorMessage = $"安装失败: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 检测 zip 文件中是否包含有效的 manifest.json
        /// </summary>
        public static bool HasManifestJson(string zipFilePath)
        {
            try
            {
                using (var archive = System.IO.Compression.ZipFile.OpenRead(zipFilePath))
                {
                    return archive.GetEntry("manifest.json") != null;
                }
            }
            catch { return false; }
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
                if (lower == "optifine" || lower == "optifine-loader") return "optifine";
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
                    (loaderType == "forge" && lower.Contains("forge") && !lower.Contains("neoforge")) ||
                    (loaderType == "neoforge" && lower.Contains("neoforge")) ||
                    (loaderType == "optifine" && lower.Contains("optifine")) ||
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

                    case "optifine":
                        return await InstallOptiFineLoaderAsync(mcVersion, loaderVersion, ct);

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

                case "optifine":
                    // OptiFine 版本 ID 格式: {mcVersion}-OptiFine-{type}-{patch}
                    var optiFineDirs = Directory.GetDirectories(versionsDir)
                        .Select(d => Path.GetFileName(d))
                        .Where(d => d.StartsWith(mcVersion + "-OptiFine-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(d => d)
                        .ToList();
                    return optiFineDirs.FirstOrDefault();

                default:
                    return null;
            }
        }

        /// <summary>
        /// 安装 OptiFine 加载器（参考 PCL McDownloadOptiFineLoader）
        /// </summary>
        private async Task<bool> InstallOptiFineLoaderAsync(string mcVersion, string optiFineVersionStr, CancellationToken ct)
        {
            try
            {
                var optiFineInstaller = new OptiFineInstaller(_minecraftPath);
                optiFineInstaller.StatusChanged += s => ReportStatus(s);
                optiFineInstaller.ProgressChanged += p => ReportProgress(20 + (int)(p * 0.5));

                // 获取可用的 OptiFine 版本
                var optiFineVersions = await optiFineInstaller.GetOptiFineVersionsAsync(mcVersion, ct);
                if (optiFineVersions == null || optiFineVersions.Count == 0)
                {
                    ReportStatus("无法获取 OptiFine 版本列表（该 MC 版本可能不支持 OptiFine）");
                    return false;
                }

                OptiFineVersionInfo selectedVersion;
                if (!string.IsNullOrEmpty(optiFineVersionStr))
                {
                    // OptiFine 版本字符串格式可能是 "HD_U_I1" 或 "HD U I1"
                    string normalized = optiFineVersionStr.Replace(' ', '_');
                    selectedVersion = optiFineVersions.FirstOrDefault(v =>
                        v.Patch == normalized ||
                        v.LoaderVersion?.Replace(' ', '_') == normalized ||
                        $"{v.Type}_{v.Patch}" == normalized);

                    if (selectedVersion == null)
                    {
                        Logger.Warning($"[整合包安装] 未找到 OptiFine {optiFineVersionStr}，使用最新版本");
                        selectedVersion = optiFineVersions[0];
                    }
                }
                else
                {
                    selectedVersion = optiFineVersions[0];
                }

                ReportStatus($"安装 OptiFine {selectedVersion.DisplayVersion}...");
                return await optiFineInstaller.InstallOptiFineAsync(mcVersion, selectedVersion, ct);
            }
            catch (Exception ex)
            {
                Logger.Error($"[整合包安装] OptiFine 安装异常: {ex.Message}");
                return false;
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

            // 保存整合包元数据（名称、MC版本、加载器等）
            var modpackInfo = new
            {
                name = displayName,
                minecraftVersion = mcVersion,
                loaderType = loaderType,
                modpackVersionId = modpackVersionId,
                installTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz")
            };
            string infoPath = Path.Combine(versionDir, "modpack-info.json");
            File.WriteAllText(infoPath, JsonSerializer.Serialize(modpackInfo, options));

            Logger.Info($"[整合包安装] 创建版本: {modpackVersionId} <- {loaderVersionId} (显示名: {displayName})");
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
        /// 安装 HMCL 格式整合包（modpack.json）
        /// </summary>
        public async Task<ModpackInstallResult> InstallFromHMCLAsync(
            string zipFilePath, string modpackDisplayName,
            CancellationToken cancellationToken = default)
        {
            var result = new ModpackInstallResult
            {
                ModpackName = modpackDisplayName,
                Success = false
            };

            try
            {
                ReportStatus("正在解析 HMCL 整合包...");
                ReportProgress(5);

                if (!File.Exists(zipFilePath))
                {
                    result.ErrorMessage = "整合包文件不存在";
                    return result;
                }

                // 解析 modpack.json
                string mcVersion = null;
                string loaderType = null;
                string loaderVersion = null;
                string packName = modpackDisplayName;
                string archiveBaseFolder = null;

                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    var entry = archive.GetEntry("modpack.json");
                    if (entry == null)
                    {
                        // 尝试在一级目录下查找
                        foreach (var e in archive.Entries)
                        {
                            var parts = e.FullName.Replace('\\', '/').Split('/');
                            if (parts.Length == 2 && parts[1] == "modpack.json")
                            {
                                entry = e;
                                archiveBaseFolder = parts[0] + "/";
                                break;
                            }
                        }
                    }
                    if (entry == null)
                    {
                        result.ErrorMessage = "未找到 modpack.json，不是有效的 HMCL 整合包";
                        return result;
                    }

                    using var stream = entry.Open();
                    using var doc = JsonDocument.Parse(stream);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("name", out var nameProp))
                        packName = nameProp.GetString() ?? packName;
                    if (root.TryGetProperty("gameVersion", out var mcProp))
                        mcVersion = mcProp.GetString();
                    if (string.IsNullOrEmpty(mcVersion) && root.TryGetProperty("version", out var verProp))
                        mcVersion = verProp.GetString();

                    // 检测加载器
                    if (root.TryGetProperty("launchInfo", out var launchProp))
                    {
                        // 有些版本在 launchInfo 里
                    }
                }

                if (string.IsNullOrEmpty(mcVersion))
                {
                    result.ErrorMessage = "无法获取游戏版本信息";
                    return result;
                }

                result.MinecraftVersion = mcVersion;
                if (!string.IsNullOrEmpty(loaderType))
                    result.LoaderType = loaderType;

                string modpackVersionId = SanitizeVersionId(packName);
                string versionDir = Path.Combine(_minecraftPath, "versions", modpackVersionId);
                string isolatedGameDir = Path.Combine(versionDir, "game");
                string extractTempDir = Path.Combine(Path.GetTempPath(), $"hmcl-{Guid.NewGuid()}");

                try
                {
                    ReportStatus("正在解压整合包...");
                    ReportProgress(15);

                    Directory.CreateDirectory(extractTempDir);
                    ZipFile.ExtractToDirectory(zipFilePath, extractTempDir, overwriteFiles: true);

                    // 找到 minecraft 目录
                    string minecraftDir = Path.Combine(extractTempDir, "minecraft");
                    if (!string.IsNullOrEmpty(archiveBaseFolder))
                        minecraftDir = Path.Combine(extractTempDir, archiveBaseFolder.TrimEnd('/'), "minecraft");
                    if (!Directory.Exists(minecraftDir))
                    {
                        // 尝试在其他位置查找
                        var dirs = Directory.GetDirectories(extractTempDir, "minecraft", SearchOption.AllDirectories);
                        if (dirs.Length > 0)
                            minecraftDir = dirs[0];
                    }

                    ReportStatus("正在安装游戏版本...");
                    ReportProgress(25);

                    // 安装加载器（包含 Minecraft 基础版本）
                    string baseVersionId = mcVersion;
                    if (!string.IsNullOrEmpty(loaderType) && !string.IsNullOrEmpty(loaderVersion))
                    {
                        ReportStatus($"正在安装 {loaderType} 加载器...");
                        ReportProgress(40);

                        bool loaderOk = await InstallLoaderAsync(mcVersion, loaderType, loaderVersion, cancellationToken);
                        if (loaderOk)
                        {
                            baseVersionId = FindLoaderVersionId(mcVersion, loaderType);
                        }
                    }
                    else
                    {
                        // 只有 Minecraft，没有加载器，确保基础版本存在
                        bool mcExists = Directory.Exists(Path.Combine(_minecraftPath, "versions", mcVersion));
                        if (!mcExists)
                        {
                            result.ErrorMessage = $"未找到 Minecraft {mcVersion} 基础版本，请先安装";
                            return result;
                        }
                    }

                    ReportProgress(55);

                    // 创建整合包版本（继承）
                    Directory.CreateDirectory(versionDir);
                    Directory.CreateDirectory(isolatedGameDir);
                    CreateModpackVersion(modpackVersionId, baseVersionId, packName, mcVersion, loaderType ?? "release");

                    ReportProgress(65);

                    // 复制 minecraft 目录内容到版本隔离目录
                    if (Directory.Exists(minecraftDir))
                    {
                        ReportStatus("正在复制整合包文件...");
                        CopyOverrides(minecraftDir, isolatedGameDir);
                    }

                    ReportProgress(95);
                    ReportStatus("正在刷新版本列表...");
                    await VersionScanService.Instance.ScanAsync("HMCL整合包安装完成");

                    ReportProgress(100);
                    ReportStatus($"整合包 {packName} 安装完成！");

                    result.VersionId = modpackVersionId;
                    result.ModpackName = packName;
                    result.Success = true;
                    return result;
                }
                finally
                {
                    if (Directory.Exists(extractTempDir))
                    {
                        try { Directory.Delete(extractTempDir, true); } catch { }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "安装已取消";
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[HMCL整合包安装] 安装失败: {ex.Message}");
                result.ErrorMessage = $"安装失败: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 安装 MMC (MultiMC/Prism) 格式整合包（mmc-pack.json）
        /// </summary>
        public async Task<ModpackInstallResult> InstallFromMMCAsync(
            string zipFilePath, string modpackDisplayName,
            CancellationToken cancellationToken = default)
        {
            var result = new ModpackInstallResult
            {
                ModpackName = modpackDisplayName,
                Success = false
            };

            try
            {
                ReportStatus("正在解析 MMC 整合包...");
                ReportProgress(5);

                if (!File.Exists(zipFilePath))
                {
                    result.ErrorMessage = "整合包文件不存在";
                    return result;
                }

                string mcVersion = null;
                string loaderType = null;
                string loaderVersion = null;
                string packName = modpackDisplayName;
                string archiveBaseFolder = null;

                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    var packEntry = archive.GetEntry("mmc-pack.json");
                    var cfgEntry = archive.GetEntry("instance.cfg");
                    if (packEntry == null)
                    {
                        // 尝试在一级目录下查找
                        foreach (var e in archive.Entries)
                        {
                            var parts = e.FullName.Replace('\\', '/').Split('/');
                            if (parts.Length == 2 && parts[1] == "mmc-pack.json")
                            {
                                packEntry = e;
                                archiveBaseFolder = parts[0] + "/";
                                cfgEntry = archive.GetEntry(parts[0] + "/instance.cfg");
                                break;
                            }
                        }
                    }
                    if (packEntry == null)
                    {
                        result.ErrorMessage = "未找到 mmc-pack.json，不是有效的 MMC 整合包";
                        return result;
                    }

                    // 解析 mmc-pack.json 获取版本信息
                    using var stream = packEntry.Open();
                    using var doc = JsonDocument.Parse(stream);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("components", out var compsProp))
                    {
                        foreach (var comp in compsProp.EnumerateArray())
                        {
                            if (!comp.TryGetProperty("uid", out var uidProp)) continue;
                            string uid = uidProp.GetString() ?? "";
                            string version = comp.TryGetProperty("version", out var verProp) ? verProp.GetString() : null;

                            switch (uid)
                            {
                                case "net.minecraft":
                                    mcVersion = version;
                                    break;
                                case "net.minecraftforge":
                                    loaderType = "forge";
                                    loaderVersion = version;
                                    break;
                                case "net.neoforged":
                                    loaderType = "neoforge";
                                    loaderVersion = version;
                                    break;
                                case "net.fabricmc.fabric-loader":
                                    loaderType = "fabric";
                                    loaderVersion = version;
                                    break;
                                case "org.quiltmc.quilt-loader":
                                    loaderType = "quilt";
                                    loaderVersion = version;
                                    break;
                            }
                        }
                    }

                    // 从 instance.cfg 获取实例名称
                    if (cfgEntry != null)
                    {
                        using var cfgStream = cfgEntry.Open();
                        using var reader = new StreamReader(cfgStream);
                        string cfgContent = reader.ReadToEnd();
                        var nameMatch = System.Text.RegularExpressions.Regex.Match(cfgContent, @"name\s*=\s*(.+)");
                        if (nameMatch.Success)
                            packName = nameMatch.Groups[1].Value.Trim();
                    }
                }

                if (string.IsNullOrEmpty(mcVersion))
                {
                    result.ErrorMessage = "无法获取游戏版本信息";
                    return result;
                }

                result.MinecraftVersion = mcVersion;
                result.LoaderType = loaderType;

                string modpackVersionId = SanitizeVersionId(packName);
                string versionDir = Path.Combine(_minecraftPath, "versions", modpackVersionId);
                string isolatedGameDir = Path.Combine(versionDir, "game");
                string extractTempDir = Path.Combine(Path.GetTempPath(), $"mmc-{Guid.NewGuid()}");

                try
                {
                    ReportStatus("正在解压整合包...");
                    ReportProgress(15);

                    Directory.CreateDirectory(extractTempDir);
                    ZipFile.ExtractToDirectory(zipFilePath, extractTempDir, overwriteFiles: true);

                    // 找到 .minecraft 目录
                    string minecraftDir = Path.Combine(extractTempDir, ".minecraft");
                    if (!string.IsNullOrEmpty(archiveBaseFolder))
                        minecraftDir = Path.Combine(extractTempDir, archiveBaseFolder.TrimEnd('/'), ".minecraft");
                    if (!Directory.Exists(minecraftDir))
                    {
                        var dirs = Directory.GetDirectories(extractTempDir, ".minecraft", SearchOption.AllDirectories);
                        if (dirs.Length > 0)
                            minecraftDir = dirs[0];
                    }

                    ReportStatus("正在安装游戏版本...");
                    ReportProgress(25);

                    // 安装加载器（包含 Minecraft 基础版本）
                    string baseVersionId = mcVersion;
                    if (!string.IsNullOrEmpty(loaderType) && !string.IsNullOrEmpty(loaderVersion))
                    {
                        ReportStatus($"正在安装 {loaderType} 加载器...");
                        ReportProgress(40);

                        bool loaderOk = await InstallLoaderAsync(mcVersion, loaderType, loaderVersion, cancellationToken);
                        if (loaderOk)
                        {
                            baseVersionId = FindLoaderVersionId(mcVersion, loaderType);
                        }
                    }
                    else
                    {
                        // 只有 Minecraft，没有加载器，确保基础版本存在
                        bool mcExists = Directory.Exists(Path.Combine(_minecraftPath, "versions", mcVersion));
                        if (!mcExists)
                        {
                            result.ErrorMessage = $"未找到 Minecraft {mcVersion} 基础版本，请先安装";
                            return result;
                        }
                    }

                    ReportProgress(55);

                    Directory.CreateDirectory(versionDir);
                    Directory.CreateDirectory(isolatedGameDir);
                    CreateModpackVersion(modpackVersionId, baseVersionId, packName, mcVersion, loaderType ?? "release");

                    ReportProgress(65);

                    if (Directory.Exists(minecraftDir))
                    {
                        ReportStatus("正在复制整合包文件...");
                        CopyOverrides(minecraftDir, isolatedGameDir);
                    }

                    ReportProgress(95);
                    ReportStatus("正在刷新版本列表...");
                    await VersionScanService.Instance.ScanAsync("MMC整合包安装完成");

                    ReportProgress(100);
                    ReportStatus($"整合包 {packName} 安装完成！");

                    result.VersionId = modpackVersionId;
                    result.ModpackName = packName;
                    result.Success = true;
                    return result;
                }
                finally
                {
                    if (Directory.Exists(extractTempDir))
                    {
                        try { Directory.Delete(extractTempDir, true); } catch { }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "安装已取消";
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[MMC整合包安装] 安装失败: {ex.Message}");
                result.ErrorMessage = $"安装失败: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 安装 MCBBS 格式整合包（mcbbs.packmeta 或 manifest.json + addons）
        /// </summary>
        public async Task<ModpackInstallResult> InstallFromMCBBSAsync(
            string zipFilePath, string modpackDisplayName,
            CancellationToken cancellationToken = default)
        {
            var result = new ModpackInstallResult
            {
                ModpackName = modpackDisplayName,
                Success = false
            };

            try
            {
                ReportStatus("正在解析 MCBBS 整合包...");
                ReportProgress(5);

                if (!File.Exists(zipFilePath))
                {
                    result.ErrorMessage = "整合包文件不存在";
                    return result;
                }

                string mcVersion = null;
                string loaderType = null;
                string loaderVersion = null;
                string packName = modpackDisplayName;
                string overridesFolder = "overrides";
                Dictionary<string, string> addons = null;

                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    // 优先读取 mcbbs.packmeta，其次 manifest.json
                    var entry = archive.GetEntry("mcbbs.packmeta") ?? archive.GetEntry("manifest.json");
                    if (entry == null)
                    {
                        // 尝试在一级目录下查找
                        foreach (var e in archive.Entries)
                        {
                            var parts = e.FullName.Replace('\\', '/').Split('/');
                            if (parts.Length == 2 && (parts[1] == "mcbbs.packmeta" || parts[1] == "manifest.json"))
                            {
                                entry = e;
                                break;
                            }
                        }
                    }
                    if (entry == null)
                    {
                        result.ErrorMessage = "未找到整合包元数据文件";
                        return result;
                    }

                    using var stream = entry.Open();
                    using var doc = JsonDocument.Parse(stream);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("name", out var nameProp))
                        packName = nameProp.GetString() ?? packName;

                    // 解析 addons
                    if (root.TryGetProperty("addons", out var addonsProp))
                    {
                        addons = new Dictionary<string, string>();
                        foreach (var addon in addonsProp.EnumerateArray())
                        {
                            string id = addon.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                            string ver = addon.TryGetProperty("version", out var verProp) ? verProp.GetString() : null;
                            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ver))
                                addons[id] = ver;
                        }
                    }

                    if (addons != null && addons.Count > 0)
                    {
                        if (addons.TryGetValue("game", out var gameVer))
                            mcVersion = gameVer;
                        if (addons.TryGetValue("forge", out var forgeVer))
                        {
                            loaderType = "forge";
                            loaderVersion = forgeVer;
                        }
                        else if (addons.TryGetValue("neoforge", out var neoVer))
                        {
                            loaderType = "neoforge";
                            loaderVersion = neoVer;
                        }
                        else if (addons.TryGetValue("fabric", out var fabricVer))
                        {
                            loaderType = "fabric";
                            loaderVersion = fabricVer;
                        }
                        else if (addons.TryGetValue("quilt", out var quiltVer))
                        {
                            loaderType = "quilt";
                            loaderVersion = quiltVer;
                        }
                    }

                    // 如果没有 addons，尝试从 manifest.json 的 minecraft 字段读取（CurseForge 格式变种）
                    if (string.IsNullOrEmpty(mcVersion) && root.TryGetProperty("minecraft", out var mcProp))
                    {
                        if (mcProp.TryGetProperty("version", out var mcVerProp))
                            mcVersion = mcVerProp.GetString();
                    }
                }

                if (string.IsNullOrEmpty(mcVersion))
                {
                    result.ErrorMessage = "无法获取游戏版本信息";
                    return result;
                }

                result.MinecraftVersion = mcVersion;
                result.LoaderType = loaderType;

                string modpackVersionId = SanitizeVersionId(packName);
                string versionDir = Path.Combine(_minecraftPath, "versions", modpackVersionId);
                string isolatedGameDir = Path.Combine(versionDir, "game");
                string extractTempDir = Path.Combine(Path.GetTempPath(), $"mcbbs-{Guid.NewGuid()}");

                try
                {
                    ReportStatus("正在解压整合包...");
                    ReportProgress(15);

                    Directory.CreateDirectory(extractTempDir);
                    ZipFile.ExtractToDirectory(zipFilePath, extractTempDir, overwriteFiles: true);

                    // 找到 overrides 目录
                    string overridesDir = Path.Combine(extractTempDir, overridesFolder);
                    if (!Directory.Exists(overridesDir))
                    {
                        var dirs = Directory.GetDirectories(extractTempDir, overridesFolder, SearchOption.AllDirectories);
                        if (dirs.Length > 0)
                            overridesDir = dirs[0];
                    }

                    ReportStatus("正在安装游戏版本...");
                    ReportProgress(25);

                    // 安装加载器（包含 Minecraft 基础版本）
                    string baseVersionId = mcVersion;
                    if (!string.IsNullOrEmpty(loaderType) && !string.IsNullOrEmpty(loaderVersion))
                    {
                        ReportStatus($"正在安装 {loaderType} 加载器...");
                        ReportProgress(40);

                        bool loaderOk = await InstallLoaderAsync(mcVersion, loaderType, loaderVersion, cancellationToken);
                        if (loaderOk)
                        {
                            baseVersionId = FindLoaderVersionId(mcVersion, loaderType);
                        }
                    }
                    else
                    {
                        // 只有 Minecraft，没有加载器，确保基础版本存在
                        bool mcExists = Directory.Exists(Path.Combine(_minecraftPath, "versions", mcVersion));
                        if (!mcExists)
                        {
                            result.ErrorMessage = $"未找到 Minecraft {mcVersion} 基础版本，请先安装";
                            return result;
                        }
                    }

                    ReportProgress(55);

                    Directory.CreateDirectory(versionDir);
                    Directory.CreateDirectory(isolatedGameDir);
                    CreateModpackVersion(modpackVersionId, baseVersionId, packName, mcVersion, loaderType ?? "release");

                    ReportProgress(65);

                    if (Directory.Exists(overridesDir))
                    {
                        ReportStatus("正在复制整合包文件...");
                        CopyOverrides(overridesDir, isolatedGameDir);
                    }

                    ReportProgress(95);
                    ReportStatus("正在刷新版本列表...");
                    await VersionScanService.Instance.ScanAsync("MCBBS整合包安装完成");

                    ReportProgress(100);
                    ReportStatus($"整合包 {packName} 安装完成！");

                    result.VersionId = modpackVersionId;
                    result.ModpackName = packName;
                    result.Success = true;
                    return result;
                }
                finally
                {
                    if (Directory.Exists(extractTempDir))
                    {
                        try { Directory.Delete(extractTempDir, true); } catch { }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "安装已取消";
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[MCBBS整合包安装] 安装失败: {ex.Message}");
                result.ErrorMessage = $"安装失败: {ex.Message}";
                return result;
            }
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
        /// 下载 CurseForge 整合包中的文件（按类型分发到 mods/resourcepacks/shaderpacks）
        /// </summary>
        private async Task DownloadCurseForgeFilesAsync(
            List<CurseForgeFileInfo> fileInfos, string gameDir,
            List<CurseForgeManifestFile> manifestFiles, CancellationToken ct)
        {
            int total = fileInfos.Count;
            int downloaded = 0;
            int skipped = 0;
            int failed = 0;

            // 建立 fileId → manifestFile 映射，用于检查 required 字段
            var requiredMap = manifestFiles
                .Where(f => f.Required)
                .Select(f => f.FileId)
                .ToHashSet();

            foreach (var fileInfo in fileInfos)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(fileInfo.DownloadUrl))
                {
                    failed++;
                    continue;
                }

                // 根据文件名判断目标子目录
                string fileName = fileInfo.FileName ?? fileInfo.DisplayName ?? "";
                string subDir;
                if (fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                    subDir = "mods";
                else if (fileName.IndexOf("pack.mcmeta", StringComparison.OrdinalIgnoreCase) >= 0
                         || fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    subDir = "resourcepacks";
                else if (fileName.EndsWith(".fsh", StringComparison.OrdinalIgnoreCase)
                         || fileName.EndsWith(".vsh", StringComparison.OrdinalIgnoreCase))
                    subDir = "shaderpacks";
                else
                    subDir = "mods"; // 默认放 mods

                string destDir = Path.Combine(gameDir, subDir);
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                string destPath = Path.Combine(destDir, fileName);

                // 文件已存在且大小匹配则跳过
                if (File.Exists(destPath))
                {
                    var fi = new FileInfo(destPath);
                    if (fi.Length == fileInfo.FileSize && fileInfo.FileSize > 0)
                    {
                        skipped++;
                        downloaded++;
                        int pct = 35 + (int)(35.0 * downloaded / total);
                        ReportProgress(pct);
                        continue;
                    }
                }

                // 下载文件
                bool success = false;
                foreach (string url in new[] { fileInfo.DownloadUrl })
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
                    catch { }
                }

                if (success)
                    downloaded++;
                else
                    failed++;

                int progress = 35 + (int)(35.0 * downloaded / total);
                ReportProgress(progress);
                ReportStatus($"下载模组... ({downloaded}/{total})");
            }

            Logger.Info($"[整合包安装] CurseForge 文件下载完成: {downloaded} 个 (跳过 {skipped}, 失败 {failed})");
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
