using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>服务器返回的组件数据</summary>
    public class ServerComponent
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string category { get; set; } = "";
        public string description { get; set; } = "";
        public string icon_emoji { get; set; } = "";
        public string author { get; set; } = "";
        public string download_url { get; set; } = "";
        public double rating { get; set; }
        public int download_count { get; set; }
        public string mc_version { get; set; } = "";
        public long file_size { get; set; }
    }

    /// <summary>组件列表响应（带分页）</summary>
    public class ComponentListResponse
    {
        public List<ServerComponent> components { get; set; } = new();
        public int total { get; set; }
        public int page { get; set; }
        public int page_size { get; set; }
    }

    /// <summary>
    /// 组件商店 HTTP 客户端，封装与服务器组件 API 的通信。
    /// </summary>
    public class ComponentStoreService
    {
        private static ComponentStoreService _instance;
        public static ComponentStoreService Instance => _instance ??= new ComponentStoreService();

        private readonly HttpClient _http;

        private ComponentStoreService()
        {
            _http = SafeHttpClientFactory.CreateClient();
        }

        /// <summary>
        /// 获取组件列表（带分页和分类筛选）。
        /// </summary>
        public async Task<ComponentListResponse> GetComponentsAsync(string category = "", int page = 1, int pageSize = 20)
        {
            try
            {
                var cat = string.IsNullOrEmpty(category) ? "" : Uri.EscapeDataString(category);
                var url = $"{ServerConfig.ComponentsApiUrl}?action=list&category={cat}&page={page}&page_size={pageSize}";
                var json = await _http.GetStringAsync(url);
                return JsonSerializer.Deserialize<ComponentListResponse>(json) ?? new ComponentListResponse();
            }
            catch
            {
                return new ComponentListResponse();
            }
        }

        /// <summary>
        /// 搜索组件。
        /// </summary>
        public async Task<ComponentListResponse> SearchComponentsAsync(string query)
        {
            try
            {
                var url = $"{ServerConfig.ComponentsApiUrl}?action=search&q={Uri.EscapeDataString(query)}";
                var json = await _http.GetStringAsync(url);
                return JsonSerializer.Deserialize<ComponentListResponse>(json) ?? new ComponentListResponse();
            }
            catch
            {
                return new ComponentListResponse();
            }
        }

        /// <summary>
        /// 记录下载次数。
        /// </summary>
        public async Task TrackDownloadAsync(int componentId)
        {
            try
            {
                var body = JsonSerializer.Serialize(new { component_id = componentId });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                await _http.PostAsync($"{ServerConfig.ComponentsApiUrl}?action=download", content);
            }
            catch { /* 下载计数失败不影响用户体验 */ }
        }
    }
}
