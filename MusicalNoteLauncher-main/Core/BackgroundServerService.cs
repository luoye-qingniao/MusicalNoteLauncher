using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>服务器返回的背景数据</summary>
    public class ServerBackground
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string type { get; set; } = "Image";
        public string file_name { get; set; } = "";
        public long file_size { get; set; }
        public string uploader { get; set; } = "";
        public int download_count { get; set; }
        public string download_url { get; set; } = "";
        public string created_at { get; set; } = "";
    }

    /// <summary>背景列表响应（带分页）</summary>
    public class BackgroundListResponse
    {
        public List<ServerBackground> backgrounds { get; set; } = new();
        public int total { get; set; }
        public int page { get; set; }
        public int page_size { get; set; }
    }

    /// <summary>背景上传响应</summary>
    public class BackgroundUploadResponse
    {
        public bool success { get; set; }
        public int id { get; set; }
        public string name { get; set; } = "";
        public string type { get; set; } = "";
        public long file_size { get; set; }
        public string download_url { get; set; } = "";
        public string error { get; set; } = "";
    }

    /// <summary>
    /// 背景素材库 HTTP 客户端，封装与服务器背景 API 的通信。
    /// 支持上传背景文件到服务器、获取服务器背景列表、删除背景。
    /// </summary>
    public class BackgroundServerService
    {
        private static BackgroundServerService _instance;
        public static BackgroundServerService Instance => _instance ??= new BackgroundServerService();

        private readonly HttpClient _http;
        private readonly HttpClient _uploadHttp; // 上传用，超时更长

        private BackgroundServerService()
        {
            _http = SafeHttpClientFactory.CreateClient();
            _uploadHttp = SafeHttpClientFactory.CreateClient(timeoutSeconds: 600);
        }

        /// <summary>
        /// 获取服务器背景列表（带分页和类型筛选）。
        /// </summary>
        public async Task<BackgroundListResponse> GetBackgroundsAsync(string type = "", int page = 1, int pageSize = 20)
        {
            try
            {
                var typeParam = string.IsNullOrEmpty(type) ? "" : $"&type={Uri.EscapeDataString(type)}";
                var url = $"{ServerConfig.BackgroundsApiUrl}?action=list&page={page}&page_size={pageSize}{typeParam}";
                var json = await _http.GetStringAsync(url);
                return JsonSerializer.Deserialize<BackgroundListResponse>(json) ?? new BackgroundListResponse();
            }
            catch
            {
                return new BackgroundListResponse();
            }
        }

        /// <summary>
        /// 上传背景文件到服务器。
        /// </summary>
        /// <param name="filePath">本地文件路径</param>
        /// <param name="displayName">背景名称</param>
        /// <param name="uploader">上传者名称</param>
        /// <returns>上传结果，失败返回 null</returns>
        public async Task<BackgroundUploadResponse> UploadAsync(string filePath, string displayName, string uploader = "")
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(filePath);
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "file", Path.GetFileName(filePath));
                form.Add(new StringContent(displayName), "name");
                form.Add(new StringContent(uploader), "uploader");

                var url = $"{ServerConfig.BackgroundsApiUrl}?action=upload";
                var response = await _uploadHttp.PostAsync(url, form);
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<BackgroundUploadResponse>(json);
            }
            catch (Exception ex)
            {
                Logger.Warning($"背景上传失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从服务器删除指定背景。
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var body = JsonSerializer.Serialize(new { id });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var url = $"{ServerConfig.BackgroundsApiUrl}?action=delete";
                var response = await _http.PostAsync(url, content);
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("success", out var success) && success.GetBoolean();
            }
            catch (Exception ex)
            {
                Logger.Warning($"背景删除失败: {ex.Message}");
                return false;
            }
        }
    }
}
