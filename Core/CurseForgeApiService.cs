using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class CurseForgeApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.curseforge.com/v1";

        public CurseForgeApiService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MusicalNoteLauncher/1.0");
        }

        public async Task<List<CurseForgeMod>> SearchMods(string query, int limit = 10)
        {
            try
            {
                string url = $"{BaseUrl}/mods/search?gameId=432&searchFilter={Uri.EscapeDataString(query)}&pageSize={limit}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    return await GetPopularMods(limit);
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CurseForgeSearchResult>(content);

                return result?.Data ?? new List<CurseForgeMod>();
            }
            catch
            {
                return await GetPopularMods(limit);
            }
        }

        public async Task<List<CurseForgeMod>> GetPopularMods(int limit = 10)
        {
            try
            {
                string url = $"{BaseUrl}/mods/search?gameId=432&sortField=2&sortOrder=desc&pageSize={limit}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<CurseForgeMod>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CurseForgeSearchResult>(content);

                return result?.Data ?? new List<CurseForgeMod>();
            }
            catch
            {
                return new List<CurseForgeMod>();
            }
        }

        public async Task<List<CurseForgeMod>> GetModpacks(int limit = 10)
        {
            try
            {
                string url = $"{BaseUrl}/mods/search?gameId=432&classId=4471&sortField=2&sortOrder=desc&pageSize={limit}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<CurseForgeMod>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CurseForgeSearchResult>(content);

                return result?.Data ?? new List<CurseForgeMod>();
            }
            catch
            {
                return new List<CurseForgeMod>();
            }
        }

        public async Task<List<CurseForgeMod>> GetResourcePacks(int limit = 10)
        {
            try
            {
                string url = $"{BaseUrl}/mods/search?gameId=432&classId=12&sortField=2&sortOrder=desc&pageSize={limit}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<CurseForgeMod>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CurseForgeSearchResult>(content);

                return result?.Data ?? new List<CurseForgeMod>();
            }
            catch
            {
                return new List<CurseForgeMod>();
            }
        }

        public async Task<List<CurseForgeMod>> GetShaders(int limit = 10)
        {
            try
            {
                string url = $"{BaseUrl}/mods/search?gameId=432&classId=16&sortField=2&sortOrder=desc&pageSize={limit}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<CurseForgeMod>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CurseForgeSearchResult>(content);

                return result?.Data ?? new List<CurseForgeMod>();
            }
            catch
            {
                return new List<CurseForgeMod>();
            }
        }

        public async Task<List<CurseForgeMod>> GetDatapacks(int limit = 10)
        {
            try
            {
                string url = $"{BaseUrl}/mods/search?gameId=432&classId=15&sortField=2&sortOrder=desc&pageSize={limit}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<CurseForgeMod>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CurseForgeSearchResult>(content);

                return result?.Data ?? new List<CurseForgeMod>();
            }
            catch
            {
                return new List<CurseForgeMod>();
            }
        }

        public async Task<string> GetDownloadUrl(long modId)
        {
            try
            {
                string url = $"{BaseUrl}/mods/{modId}/files";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CurseForgeFilesResult>(content);

                if (result?.Data != null && result.Data.Count > 0)
                {
                    return result.Data[0].DownloadUrl;
                }
            }
            catch { }

            return null;
        }
    }

    public class CurseForgeSearchResult
    {
        [JsonPropertyName("data")]
        public List<CurseForgeMod> Data { get; set; }
    }

    public class CurseForgeFilesResult
    {
        [JsonPropertyName("data")]
        public List<CurseForgeFile> Data { get; set; }
    }

    public class CurseForgeMod
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("authors")]
        public List<CurseForgeAuthor> Authors { get; set; }

        [JsonPropertyName("downloadCount")]
        public long DownloadCount { get; set; }

        [JsonPropertyName("latestFiles")]
        public List<CurseForgeFile> LatestFiles { get; set; }

        [JsonPropertyName("logoUrl")]
        public string LogoUrl { get; set; }

        public string AuthorName { get; set; } = "未知作者";

        public CurseForgeFile LatestFile { get; set; } = new CurseForgeFile();

        public string DownloadCountFormatted => FormatDownloads(DownloadCount);

        private string FormatDownloads(long downloads)
        {
            if (downloads >= 1000000)
                return $"{(downloads / 1000000.0):0.##}M";
            if (downloads >= 1000)
                return $"{(downloads / 1000.0):0.##}K";
            return downloads.ToString();
        }
    }

    public class CurseForgeAuthor
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class CurseForgeFile
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; }
    }
}