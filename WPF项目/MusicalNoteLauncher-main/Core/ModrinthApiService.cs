using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class ModrinthApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxConnectionsPerServer = 10
            };

            // 强制使用 TLS 1.2 / 1.3（兼容旧 .NET 运行时）
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // 更真实的 User-Agent，避免被 API 限流
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 MusicalNoteLauncher/1.2");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate");

            return client;
        }

        public ModrinthApiService(string baseUrl = null)
        {
            _baseUrl = baseUrl ?? "https://api.modrinth.com/v2";
            _httpClient = CreateHttpClient();
        }

        public async Task<List<ModrinthMod>> SearchMods(string query, string gameVersion = "", int limit = 10, int offset = 0)
        {
            try
            {
                string url = $"{_baseUrl}/search?query={Uri.EscapeDataString(query)}&limit={limit}&offset={offset}";
                if (!string.IsNullOrEmpty(gameVersion))
                {
                    url += $"&version={Uri.EscapeDataString(gameVersion)}";
                }

                Logger.Info($"[Modrinth] SearchMods: {url}");

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Logger.Warning($"[Modrinth] SearchMods 返回 {response.StatusCode}: {errorContent}");
                    return new List<ModrinthMod>();
                }

                var content = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ModrinthSearchResult>(content);

                if (result?.Hits == null)
                {
                    Logger.Warning("[Modrinth] SearchMods 反序列化返回 null");
                    return new List<ModrinthMod>();
                }

                Logger.Info($"[Modrinth] SearchMods 找到 {result.Hits.Count} 个结果");
                return result.Hits;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Modrinth] SearchMods 异常: {ex.Message}");
                return new List<ModrinthMod>();
            }
        }

        public async Task<List<ModrinthMod>> GetPopularMods(int limit = 10)
        {
            return await SearchMods("", "", limit);
        }

        public async Task<List<ModrinthMod>> GetModpacks(int limit = 10)
        {
            try
            {
                string url = $"{_baseUrl}/search?facets=[[\"project_type:modpack\"]]&limit={limit}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ModrinthSearchResult>(content);

                return result?.Hits ?? new List<ModrinthMod>();
            }
            catch
            {
                return new List<ModrinthMod>();
            }
        }

        public async Task<List<ModrinthMod>> GetResourcePacks(int limit = 10)
        {
            try
            {
                string url = $"{_baseUrl}/search?facets=[[\"project_type:resourcepack\"]]&limit={limit}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ModrinthSearchResult>(content);

                return result?.Hits ?? new List<ModrinthMod>();
            }
            catch
            {
                return new List<ModrinthMod>();
            }
        }

        public async Task<List<ModrinthMod>> GetShaders(int limit = 10)
        {
            try
            {
                string url = $"{_baseUrl}/search?facets=[[\"project_type:shader\"]]&limit={limit}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ModrinthSearchResult>(content);

                return result?.Hits ?? new List<ModrinthMod>();
            }
            catch
            {
                return new List<ModrinthMod>();
            }
        }

        public async Task<List<ModrinthMod>> GetDatapacks(int limit = 10)
        {
            try
            {
                string url = $"{_baseUrl}/search?facets=[[\"project_type:datapack\"]]&limit={limit}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ModrinthSearchResult>(content);

                return result?.Hits ?? new List<ModrinthMod>();
            }
            catch
            {
                return new List<ModrinthMod>();
            }
        }

        public async Task<List<ModrinthMod>> GetMods(int limit = 10)
        {
            try
            {
                string url = $"{_baseUrl}/search?facets=[[\"project_type:mod\"]]&limit={limit}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ModrinthSearchResult>(content);

                return result?.Hits ?? new List<ModrinthMod>();
            }
            catch
            {
                return new List<ModrinthMod>();
            }
        }

        public async Task<string> GetDownloadUrl(string projectId)
        {
            try
            {
                string url = $"{_baseUrl}/project/{projectId}/version";
                Logger.Info($"[Modrinth] GetDownloadUrl: {url}");

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Logger.Warning($"[Modrinth] GetDownloadUrl 返回 {response.StatusCode}: {errorContent}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var versions = JsonSerializer.Deserialize<List<ModrinthVersion>>(content);

                if (versions != null && versions.Count > 0)
                {
                    var latestVersion = versions[0];
                    if (latestVersion.Files != null && latestVersion.Files.Count > 0)
                    {
                        Logger.Info($"[Modrinth] GetDownloadUrl: {latestVersion.Files[0].Url}");
                        return latestVersion.Files[0].Url;
                    }
                    Logger.Warning("[Modrinth] GetDownloadUrl: 版本无文件");
                }
                else
                {
                    Logger.Warning("[Modrinth] GetDownloadUrl: 未找到版本");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Modrinth] GetDownloadUrl 异常: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 获取指定项目的所有可用版本
        /// </summary>
        public async Task<List<ModrinthVersion>> GetProjectVersions(string projectId, string gameVersion = null)
        {
            try
            {
                string url = $"{_baseUrl}/project/{projectId}/version";
                if (!string.IsNullOrEmpty(gameVersion))
                {
                    url += $"?game_versions={Uri.EscapeDataString("[\"" + gameVersion + "\"]")}";
                }

                Logger.Info($"[Modrinth] GetProjectVersions: {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Logger.Warning($"[Modrinth] GetProjectVersions 返回 {response.StatusCode}: {errorContent}");
                    return new List<ModrinthVersion>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var versions = JsonSerializer.Deserialize<List<ModrinthVersion>>(content);

                if (versions == null || versions.Count == 0)
                {
                    Logger.Warning($"[Modrinth] GetProjectVersions: 项目 {projectId} 无可用版本");
                    return new List<ModrinthVersion>();
                }

                // 按版本类型排序（release > beta > alpha），然后按日期降序
                var sortedVersions = versions.OrderByDescending(v => v.VersionType switch
                {
                    "release" => 3,
                    "beta" => 2,
                    "alpha" => 1,
                    _ => 0
                }).ThenByDescending(v => v.DatePublished ?? DateTime.MinValue).ToList();

                Logger.Info($"[Modrinth] GetProjectVersions: 找到 {sortedVersions.Count} 个版本");
                return sortedVersions;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Modrinth] GetProjectVersions 异常: {ex.Message}");
                return new List<ModrinthVersion>();
            }
        }

        /// <summary>
        /// 获取单个项目详细信息
        /// </summary>
        public async Task<ModrinthProject> GetProject(string projectId)
        {
            try
            {
                string url = $"{_baseUrl}/project/{projectId}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ModrinthProject>(content);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Modrinth] GetProject 异常 ({projectId}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 批量获取多个项目详细信息
        /// </summary>
        public async Task<List<ModrinthProject>> GetProjects(List<string> projectIds)
        {
            try
            {
                if (projectIds == null || projectIds.Count == 0)
                    return new List<ModrinthProject>();

                string idsParam = "[" + string.Join(",", projectIds.Select(id => $"\"{id}\"")) + "]";
                string url = $"{_baseUrl}/projects?ids={Uri.EscapeDataString(idsParam)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<ModrinthProject>>(content) ?? new List<ModrinthProject>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetProjects Exception: {ex.Message}");
                return new List<ModrinthProject>();
            }
        }
    }

    public class ModrinthSearchResult
    {
        [JsonPropertyName("hits")]
        public List<ModrinthMod> Hits { get; set; }
    }

    public class ModrinthMod
    {
        [JsonPropertyName("project_id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("downloads")]
        public long Downloads { get; set; }

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }

        [JsonPropertyName("versions")]
        public List<string> Versions { get; set; }

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; }

        [JsonPropertyName("project_type")]
        public string ProjectType { get; set; }

        public string LatestVersion => Versions?.Count > 0 ? Versions[0] : "未知";

        public string DownloadCountFormatted => FormatDownloads(Downloads);

        private string FormatDownloads(long downloads)
        {
            if (downloads >= 1000000)
                return $"{(downloads / 1000000.0):0.##}M";
            if (downloads >= 1000)
                return $"{(downloads / 1000.0):0.##}K";
            return downloads.ToString();
        }
    }

    public class ModrinthVersion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version_number")]
        public string VersionNumber { get; set; }

        [JsonPropertyName("changelog")]
        public string Changelog { get; set; }

        [JsonPropertyName("game_versions")]
        public List<string> GameVersions { get; set; }

        [JsonPropertyName("version_type")]
        public string VersionType { get; set; }

        [JsonPropertyName("loaders")]
        public List<string> Loaders { get; set; }

        [JsonPropertyName("date_published")]
        public DateTime? DatePublished { get; set; }

        [JsonPropertyName("files")]
        public List<ModrinthFile> Files { get; set; }

        [JsonPropertyName("dependencies")]
        public List<ModrinthDependency> Dependencies { get; set; }

        public string DisplayVersionName
        {
            get
            {
                var display = (Name ?? "").Replace("\t", "").Trim();
                if (!string.IsNullOrEmpty(display))
                {
                    IsBadDisplay = false;
                    return display;
                }
                IsBadDisplay = true;
                return VersionNumber ?? "未知版本";
            }
        }

        public bool IsBadDisplay { get; private set; }

        /// 显示名（对齐 PCL：name 为主，version_number 降级）
        public string DisplayName => DisplayVersionName;

        public string DisplayGameVersions => GameVersions != null && GameVersions.Count > 0
            ? string.Join(", ", GameVersions.Take(3))
            : "未知";

        public string DisplayLoaders => Loaders != null && Loaders.Count > 0
            ? string.Join(", ", Loaders)
            : "未知";

        public string DownloadUrl => Files != null && Files.Count > 0 ? Files[0].Url : null;

        public long FileSize => Files != null && Files.Count > 0 ? Files[0].Size : 0;

        public string FileName => Files != null && Files.Count > 0 ? Files[0].FileName : "unknown";

        public bool IsRecommended => VersionType == "release";

        public string VersionTypeText
        {
            get
            {
                switch (VersionType)
                {
                    case "release": return "正式版";
                    case "beta": return "测试版";
                    case "alpha": return "预览版";
                    default: return "未知";
                }
            }
        }
    }

    public class ModrinthFile
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("filename")]
        public string FileName { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    /// <summary>
    /// Modrinth 版本依赖
    /// </summary>
    public class ModrinthDependency
    {
        [JsonPropertyName("version_id")]
        public string VersionId { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [JsonPropertyName("dependency_type")]
        public string DependencyType { get; set; }

        /// <summary>是否为必需依赖</summary>
        public bool IsRequired => DependencyType == "required";
    }

    /// <summary>
    /// Modrinth 项目详细信息（用于获取依赖项的图标和名称）
    /// </summary>
    public class ModrinthProject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }
    }
}