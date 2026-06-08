using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class ModrinthApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.modrinth.com/v2";

        public ModrinthApiService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MusicalNoteLauncher/1.0");
        }

        public async Task<List<ModrinthMod>> SearchMods(string query, string gameVersion = "", int limit = 10)
        {
            try
            {
                string url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query)}&limit={limit}";
                if (!string.IsNullOrEmpty(gameVersion))
                {
                    url += $"&version={Uri.EscapeDataString(gameVersion)}";
                }
                
                System.Diagnostics.Debug.WriteLine($"Searching mods: {url}");

                var response = await _httpClient.GetAsync(url);
                System.Diagnostics.Debug.WriteLine($"Response status: {response.StatusCode}");
                
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Response length: {content.Length}");
                
                // 打印响应预览
                if (content.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Response preview: {content.Substring(0, Math.Min(500, content.Length))}...");
                }

                var result = JsonSerializer.Deserialize<ModrinthSearchResult>(content);
                
                if (result == null)
                {
                    System.Diagnostics.Debug.WriteLine("Deserialization returned null");
                    return new List<ModrinthMod>();
                }
                
                if (result.Hits == null)
                {
                    System.Diagnostics.Debug.WriteLine("Hits is null");
                    return new List<ModrinthMod>();
                }
                
                System.Diagnostics.Debug.WriteLine($"Found {result.Hits.Count} items");
                
                // 打印前几个mod的信息
                for (int i = 0; i < Math.Min(3, result.Hits.Count); i++)
                {
                    var mod = result.Hits[i];
                    System.Diagnostics.Debug.WriteLine($"Mod {i}: Name={mod.Name ?? "null"}, Id={mod.Id ?? "null"}");
                }

                return result.Hits;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchMods Exception: {ex.Message}");
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
                string url = $"{BaseUrl}/search?facets=[[\"project_type:modpack\"]]&limit={limit}";
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
                string url = $"{BaseUrl}/search?facets=[[\"project_type:resourcepack\"]]&limit={limit}";
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
                string url = $"{BaseUrl}/search?facets=[[\"project_type:shader\"]]&limit={limit}";
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
                string url = $"{BaseUrl}/search?facets=[[\"project_type:datapack\"]]&limit={limit}";
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
                string url = $"{BaseUrl}/project/{projectId}/version";
                System.Diagnostics.Debug.WriteLine($"Requesting URL: {url}");
                
                var response = await _httpClient.GetAsync(url);
                System.Diagnostics.Debug.WriteLine($"Response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Modrinth API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Response content length: {content.Length} characters");
                
                // 打印前500个字符用于调试
                if (content.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Response preview: {content.Substring(0, Math.Min(500, content.Length))}...");
                }

                var versions = JsonSerializer.Deserialize<List<ModrinthVersion>>(content);

                if (versions != null && versions.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Found {versions.Count} versions");
                    var latestVersion = versions[0];
                    if (latestVersion.Files != null && latestVersion.Files.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found {latestVersion.Files.Count} files, URL: {latestVersion.Files[0].Url}");
                        return latestVersion.Files[0].Url;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No files found in version");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No versions found or deserialization failed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDownloadUrl Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception stack: {ex.StackTrace}");
            }

            return null;
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
        [JsonPropertyName("files")]
        public List<ModrinthFile> Files { get; set; }
    }

    public class ModrinthFile
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}