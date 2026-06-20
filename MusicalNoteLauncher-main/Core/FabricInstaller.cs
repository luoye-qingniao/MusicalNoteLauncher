using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using System.Net.Http;

using System.Text.Json;

using System.Threading;

using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core

{

    public class FabricInstaller

    {

        private readonly string _minecraftPath;

        private readonly HttpClient _httpClient;

        private static readonly string FabricLoaderUrl = "https://meta.fabricmc.net/v2/versions/loader/{0}";

        private static readonly string FabricInstallerJsonUrl = "https://meta.fabricmc.net/v2/versions/loader/{0}/{1}/profile/json";

        public event Action<string> StatusChanged;

        public event Action<int> ProgressChanged;

        public FabricInstaller(string minecraftPath)

        {

            _minecraftPath = minecraftPath;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromMinutes(10);

        }

        public async Task<List<FabricVersionInfo>> GetFabricVersionsAsync(string mcVersion, CancellationToken cancellationToken = default)

        {

            var versions = new List<FabricVersionInfo>();
            try

            {

                string url = string.Format(FabricLoaderUrl, mcVersion);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(json))

                {

                    foreach (var item in doc.RootElement.EnumerateArray())

                    {

                        var version = new FabricVersionInfo();
                        if (item.TryGetProperty("loader", out var loader))

                        {

                            if (loader.TryGetProperty("version", out var versionProp))
                                version.LoaderVersion = versionProp.GetString();

                        }

                        if (item.TryGetProperty("intermediary", out var intermediary))

                        {

                            if (intermediary.TryGetProperty("version", out var versionProp))
                                version.IntermediaryVersion = versionProp.GetString();

                        }

                        version.McVersion = mcVersion;
                        versions.Add(version);

                    }

                }

            }

            catch (Exception ex)

            {

                Logger.Warning($"[Fabric] 获取加载器版本失败: {ex.Message}");

            }

            return versions.OrderByDescending(v => VersionHelper.ParseVersion(v.LoaderVersion)).ToList();

        }

        public async Task<FabricVersionInfo> GetRecommendedVersionAsync(string mcVersion, CancellationToken cancellationToken = default)

        {

            var versions = await GetFabricVersionsAsync(mcVersion, cancellationToken);

            // 优先稳定版本

            var stable = versions.FirstOrDefault(v => !string.IsNullOrEmpty(v.LoaderVersion) && !v.LoaderVersion.Contains("-"));
            return stable ?? versions.FirstOrDefault();

        }

        public async Task<bool> InstallFabricAsync(string mcVersion, FabricVersionInfo fabricVersion, CancellationToken cancellationToken = default)

        {

            try

            {

                StatusChanged?.Invoke($"开始安装 Fabric Loader {fabricVersion.LoaderVersion} 适用于 MC {mcVersion}...");

                // 0. 先检查/下载父版本（原版 mcVersion），没有原版就自动下载
                if (!await EnsureParentVersionAsync(mcVersion, cancellationToken))
                {
                    StatusChanged?.Invoke($"错误：无法准备原版 {mcVersion}，Fabric 安装中止");
                    return false;
                }
                Logger.Info($"[Fabric] 父版本 {mcVersion} 已准备，可以继续安装 Fabric");

                // 1. 获取 Fabric 安装配置 JSON

                StatusChanged?.Invoke("获取 Fabric 安装配置...");
                string profileJson = await GetFabricProfileJson(mcVersion, fabricVersion.LoaderVersion, cancellationToken);
                if (string.IsNullOrEmpty(profileJson))

                {

                    StatusChanged?.Invoke("获取 Fabric 配置失败");
                    return false;

                }

                ProgressChanged?.Invoke(20);

                // 2. 解析配置

                StatusChanged?.Invoke("解析 Fabric 配置...");

                using (JsonDocument doc = JsonDocument.Parse(profileJson))

                {

                    string versionId = doc.RootElement.GetProperty("id").GetString();
                    string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
                    if (!Directory.Exists(versionDir))
                        Directory.CreateDirectory(versionDir);
                    ProgressChanged?.Invoke(30);

                    // 3. 下载 Fabric Loader 库文件

                    StatusChanged?.Invoke("下载 Fabric Loader 库文件...");
                    if (doc.RootElement.TryGetProperty("libraries", out var libraries))

                    {

                        int totalLibs = libraries.GetArrayLength();
                        int downloaded = 0;
                        foreach (var lib in libraries.EnumerateArray())

                        {

                            cancellationToken.ThrowIfCancellationRequested();
                            string libName = lib.GetProperty("name").GetString();
                            string libPath = ResolveLibraryPath(libName);
                            string destPath = Path.Combine(_minecraftPath, "libraries", libPath);
                            if (!File.Exists(destPath))

                            {

                                string libUrl = GetLibraryDownloadUrl(lib);
                                if (!string.IsNullOrEmpty(libUrl))

                                {

                                    await DownloadFileAsync(libUrl, destPath, cancellationToken);

                                }

                            }

                            downloaded++;
                            ProgressChanged?.Invoke(30 + (int)(40.0 * downloaded / totalLibs));

                        }

                    }

                    ProgressChanged?.Invoke(70);

                    // 4. 写入版本 JSON

                    StatusChanged?.Invoke("写入版本配置...");
                    string jsonPath = Path.Combine(versionDir, $"{versionId}.json");
                    File.WriteAllText(jsonPath, profileJson);
                    ProgressChanged?.Invoke(85);

                    // 5. 创建 mods 文件夹

                    string modsDir = Path.Combine(_minecraftPath, "mods");
                    if (!Directory.Exists(modsDir))
                        Directory.CreateDirectory(modsDir);
                    ProgressChanged?.Invoke(90);

                    // 6. 验证安装

                    if (File.Exists(jsonPath))

                    {

                        StatusChanged?.Invoke($"Fabric Loader {fabricVersion.LoaderVersion} 安装完成！");
                        ProgressChanged?.Invoke(100);
                        return true;

                    }

                }

            }

            catch (OperationCanceledException)

            {

                StatusChanged?.Invoke("安装已取消");
                return false;

            }

            catch (Exception ex)

            {

                StatusChanged?.Invoke($"安装失败: {ex.Message}");
                Logger.Error($"[Fabric] 安装异常: {ex}");
                return false;

            }

            return false;

        }

        /// <summary>
        /// 检查并准备原版 Minecraft（Fabric 的父版本）：
        /// 1. 若原版版本目录已有 jar + json，直接返回 true
        /// 2. 否则自动通过 BMCLAPI/Mojang 的 version_manifest 拉取原版 json、下载 client jar、资源索引、依赖库
        /// </summary>
        private async Task<bool> EnsureParentVersionAsync(string mcVersion, CancellationToken cancellationToken)
        {
            string parentVersionDir = Path.Combine(_minecraftPath, "versions", mcVersion);
            string parentJar = Path.Combine(parentVersionDir, $"{mcVersion}.jar");
            string parentJsonFile = Path.Combine(parentVersionDir, $"{mcVersion}.json");

            // 已安装就跳过
            if (File.Exists(parentJar) && File.Exists(parentJsonFile))
            {
                return true;
            }

            StatusChanged?.Invoke($"[Fabric] 检测到未安装原版 {mcVersion}，将自动下载...");
            Logger.Info($"[Fabric] 开始自动下载原版 {mcVersion}（Fabric 需要依赖原版 jar）");

            try
            {
                // Step A: 下载版本清单，查找 mcVersion 的 json URL
                StatusChanged?.Invoke($"[Fabric] 获取版本清单...");
                string versionJsonUrl = await FetchVersionJsonUrlAsync(mcVersion, cancellationToken);
                if (string.IsNullOrEmpty(versionJsonUrl))
                {
                    Logger.Error($"[Fabric] 找不到版本 {mcVersion} 的下载地址");
                    return false;
                }

                // Step B: 下载版本 JSON
                StatusChanged?.Invoke($"[Fabric] 下载原版 {mcVersion} 配置...");
                Logger.Info($"[Fabric] 原版版本 JSON URL: {versionJsonUrl}");
                string versionJson = await _httpClient.GetStringAsync(versionJsonUrl);
                if (string.IsNullOrEmpty(versionJson))
                {
                    Logger.Error($"[Fabric] 原版版本 JSON 为空");
                    return false;
                }

                Directory.CreateDirectory(parentVersionDir);
                File.WriteAllText(parentJsonFile, versionJson);
                Logger.Info($"[Fabric] 已保存原版版本 JSON: {parentJsonFile}");

                // Step C: 下载 client jar + assetIndex + libraries（基于版本 JSON 的 downloads 字段）
                using (JsonDocument doc = JsonDocument.Parse(versionJson))
                {
                    JsonElement root = doc.RootElement;

                    // 3.1 client jar
                    if (root.TryGetProperty("downloads", out JsonElement downloads) &&
                        downloads.TryGetProperty("client", out JsonElement client))
                    {
                        string jarUrl = client.GetProperty("url").GetString();
                        StatusChanged?.Invoke($"[Fabric] 下载原版客户端 Jar...");
                        Logger.Info($"[Fabric] 原版 Jar: {jarUrl} -> {parentJar}");
                        await DownloadFileAsync(jarUrl, parentJar, cancellationToken);
                    }
                    else
                    {
                        Logger.Error($"[Fabric] 版本 JSON 中没有 client.jar 下载信息");
                        return false;
                    }

                    // 3.2 asset index
                    if (root.TryGetProperty("assetIndex", out JsonElement assetIndex))
                    {
                        string assetIndexUrl = assetIndex.GetProperty("url").GetString();
                        string assetIndexId = assetIndex.GetProperty("id").GetString();
                        string assetsDir = Path.Combine(_minecraftPath, "assets", "indexes");
                        Directory.CreateDirectory(assetsDir);
                        string assetIndexPath = Path.Combine(assetsDir, $"{assetIndexId}.json");
                        if (!File.Exists(assetIndexPath))
                        {
                            StatusChanged?.Invoke($"[Fabric] 下载资源索引 {assetIndexId}...");
                            await DownloadFileAsync(assetIndexUrl, assetIndexPath, cancellationToken);
                        }
                    }

                    // 3.3 libraries（只处理 Windows 需要的、且通过 downloads.jar 下载字段的库）
                    if (root.TryGetProperty("libraries", out JsonElement libraries))
                    {
                        StatusChanged?.Invoke($"[Fabric] 下载原版依赖库...");
                        int total = libraries.GetArrayLength();
                        int done = 0;
                        int skipped = 0;

                        foreach (JsonElement lib in libraries.EnumerateArray())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // 判断此库是否在 Windows 平台需要（rules / classifiers 简单判断）
                            if (!IsLibraryApplicable(lib))
                            {
                                done++;
                                continue;
                            }

                            if (lib.TryGetProperty("downloads", out JsonElement libDownloads) &&
                                libDownloads.TryGetProperty("artifact", out JsonElement artifact))
                            {
                                string libUrl = artifact.GetProperty("url").GetString();
                                string libPath = artifact.TryGetProperty("path", out JsonElement pathEl)
                                    ? pathEl.GetString()
                                    : null;

                                if (!string.IsNullOrEmpty(libPath) && !string.IsNullOrEmpty(libUrl))
                                {
                                    string fullPath = Path.Combine(_minecraftPath, "libraries", libPath);
                                    if (!File.Exists(fullPath))
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                                        try
                                        {
                                            await DownloadFileAsync(libUrl, fullPath, cancellationToken);
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Warning($"[Fabric] 依赖库下载失败（可忽略，若启动时报错请手动修复）：{libPath} — {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        skipped++;
                                    }
                                }
                            }

                            // 额外处理 classifiers/natives（LWJGL 的 Windows 原生库放在这里）
                            if (lib.TryGetProperty("downloads", out JsonElement libDls2) &&
                                libDls2.TryGetProperty("classifiers", out JsonElement classifiers))
                            {
                                string[] nativeKeys = Environment.Is64BitOperatingSystem
                                    ? new[] { "natives-windows-x64", "natives-windows" }
                                    : new[] { "natives-windows-x86", "natives-windows" };

                                foreach (string key in nativeKeys)
                                {
                                    if (classifiers.TryGetProperty(key, out JsonElement classifier))
                                    {
                                        string cUrl = classifier.TryGetProperty("url", out var cUrlEl) ? cUrlEl.GetString() : null;
                                        string cPath = classifier.TryGetProperty("path", out var cPathEl) ? cPathEl.GetString() : null;

                                        if (!string.IsNullOrEmpty(cPath) && !string.IsNullOrEmpty(cUrl))
                                        {
                                            string cFull = Path.Combine(_minecraftPath, "libraries", cPath);
                                            if (!File.Exists(cFull))
                                            {
                                                try
                                                {
                                                    Directory.CreateDirectory(Path.GetDirectoryName(cFull));
                                                    await DownloadFileAsync(cUrl, cFull, cancellationToken);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Logger.Warning($"[Fabric] 原生库下载失败（可忽略）：{cPath} — {ex.Message}");
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            done++;
                        }

                        Logger.Info($"[Fabric] 原版依赖库处理完成：共 {total} 个，已跳过 {skipped} 个已存在");
                    }

                    // 3.4 log config（可选）
                    if (root.TryGetProperty("logging", out JsonElement logging) &&
                        logging.TryGetProperty("client", out JsonElement clientLog) &&
                        clientLog.TryGetProperty("file", out JsonElement logFile))
                    {
                        string logUrl = logFile.GetProperty("url").GetString();
                        string logId = logFile.GetProperty("id").GetString();
                        string logDir = Path.Combine(_minecraftPath, "assets", "log_configs");
                        Directory.CreateDirectory(logDir);
                        string logPath = Path.Combine(logDir, logId);
                        if (!File.Exists(logPath))
                        {
                            try { await DownloadFileAsync(logUrl, logPath, cancellationToken); }
                            catch { }
                        }
                    }
                }

                Logger.Info($"[Fabric] 原版 {mcVersion} 自动下载完成");
                return File.Exists(parentJar);
            }
            catch (OperationCanceledException)
            {
                Logger.Warning($"[Fabric] 原版下载被取消");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Fabric] 自动下载原版 {mcVersion} 失败: {ex.Message}");
                StatusChanged?.Invoke($"原版 {mcVersion} 下载失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从 BMCLAPI 或 Mojang 的 version_manifest.json 查找指定 mcVersion 对应 version.json 的 URL
        /// </summary>
        private async Task<string> FetchVersionJsonUrlAsync(string mcVersion, CancellationToken cancellationToken)
        {
            // 优先 BMCLAPI，其次 Mojang
            string[] manifestSources = new[]
            {
                "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json",
                "https://launchermeta.mojang.com/mc/game/version_manifest.json"
            };

            foreach (string manifestUrl in manifestSources)
            {
                try
                {
                    Logger.Info($"[Fabric] 请求版本清单: {manifestUrl}");
                    string manifest = await _httpClient.GetStringAsync(manifestUrl);
                    using (JsonDocument doc = JsonDocument.Parse(manifest))
                    {
                        if (doc.RootElement.TryGetProperty("versions", out JsonElement versions))
                        {
                            foreach (JsonElement v in versions.EnumerateArray())
                            {
                                if (v.TryGetProperty("id", out JsonElement idEl) && idEl.GetString() == mcVersion)
                                {
                                    if (v.TryGetProperty("url", out JsonElement urlEl))
                                    {
                                        string url = urlEl.GetString();
                                        // 如果是官方地址，也能替换为 BMCLAPI 镜像
                                        Logger.Info($"[Fabric] 找到版本 {mcVersion} 的 JSON: {url}");
                                        return url;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[Fabric] 从 {manifestUrl} 获取清单失败: {ex.Message}");
                }
            }

            // 兜底：直接用 BMCLAPI 的固定 URL 格式
            string fallback = $"https://bmclapi2.bangbang93.com/versions/{mcVersion}/{mcVersion}.json";
            Logger.Info($"[Fabric] 使用兜底 URL: {fallback}");
            return fallback;
        }

        /// <summary>
        /// 判断该库文件对当前 Windows 平台是否需要（用于过滤原版库列表下载）
        /// </summary>
        private bool IsLibraryApplicable(JsonElement library)
        {
            if (library.TryGetProperty("rules", out JsonElement rules))
            {
                bool allow = true;
                foreach (JsonElement rule in rules.EnumerateArray())
                {
                    string action = rule.TryGetProperty("action", out var act) ? act.GetString() : "allow";
                    bool osMatch = true;
                    if (rule.TryGetProperty("os", out JsonElement os))
                    {
                        osMatch = false;
                        if (os.TryGetProperty("name", out JsonElement osName))
                        {
                            if (osName.GetString() == "windows")
                                osMatch = true;
                        }
                        else if (os.TryGetProperty("arch", out JsonElement archEl))
                        {
                            // 仅当明确写了 arch=x86 时，64 位系统才不下载
                            if (archEl.GetString() == "x86" && Environment.Is64BitOperatingSystem)
                                osMatch = false;
                        }
                    }
                    if (action == "allow" && osMatch) allow = true;
                    if (action == "disallow" && osMatch) allow = false;
                }
                if (!allow) return false;
            }
            return true;
        }

        private async Task<string> GetFabricProfileJson(string mcVersion, string loaderVersion, CancellationToken cancellationToken)

        {

            try

            {

                string url = string.Format(FabricInstallerJsonUrl, mcVersion, loaderVersion);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();

            }

            catch (Exception ex)

            {

                Logger.Error($"[Fabric] 获取安装配置失败: {ex.Message}");
                return null;

            }

        }

        private string ResolveLibraryPath(string name)

        {

            var parts = name.Split(':');
            if (parts.Length < 3) return name;
            string group = parts[0].Replace('.', '/');
            string artifact = parts[1];
            string version = parts[2];
            if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(artifact) || string.IsNullOrEmpty(version))
                return name;
            return $"{group}/{artifact}/{version}/{artifact}-{version}.jar";

        }

        private string GetLibraryDownloadUrl(JsonElement lib)

        {

            if (lib.TryGetProperty("url", out var urlProp))

            {

                string baseUrl = urlProp.GetString();
                string libName = lib.GetProperty("name").GetString();
                string libPath = ResolveLibraryPath(libName);
                return $"{baseUrl}{libPath}";

            }

            // 默认从 maven central 下载

            string defaultName = lib.GetProperty("name").GetString();
            string defaultPath = ResolveLibraryPath(defaultName);
            return $"https://maven.fabricmc.net/{defaultPath}";

        }

        private async Task DownloadFileAsync(string url, string savePath, CancellationToken cancellationToken)

        {

            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))

            {

                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())

                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))

                {

                    await stream.CopyToAsync(fileStream);

                }

            }

        }

        public bool IsFabricInstalled(string versionId)

        {

            string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
            return Directory.Exists(versionDir) && File.Exists(Path.Combine(versionDir, $"{versionId}.json"));

        }

    }

    public class FabricVersionInfo

    {

        public string McVersion { get; set; }

        public string LoaderVersion { get; set; }

        public string IntermediaryVersion { get; set; }

        public bool IsRecommended { get; set; }

        public string VersionId => $"fabric-loader-{LoaderVersion}-{McVersion}";

        public string DisplayVersion => $"Fabric {LoaderVersion}";

        public string Type => IsRecommended ? "推荐版本" : "稳定版本";

        public override string ToString()

        {

            return $"{McVersion} + Fabric {LoaderVersion}";

        }

    }

    internal static class VersionHelper

    {

        public static Version ParseVersion(string version)

        {

            try

            {

                string clean = version.Split('-')[0];
                return new Version(clean);

            }

            catch

            {

                return new Version(0, 0);

            }

        }

    }

}
