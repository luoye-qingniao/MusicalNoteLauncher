using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PCL.Account;

namespace MusicalNoteLauncher.Core
{
    /// <summary>服务器返回的频道数据</summary>
    public class ServerChannel
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string description { get; set; } = "";
        public string icon_emoji { get; set; } = "";
        public int message_count { get; set; }
        public ServerMessageInfo last_message { get; set; }
    }

    /// <summary>服务器返回的频道最后消息</summary>
    public class ServerMessageInfo
    {
        public string content { get; set; } = "";
        public string sender_id { get; set; } = "";
        public string created_at { get; set; }
    }

    /// <summary>服务器返回的社区消息</summary>
    public class ServerCommunityMessage
    {
        public long id { get; set; }
        public int channel_id { get; set; }
        public string sender_id { get; set; } = "";
        /// <summary>发帖人昵称（community.php LEFT JOIN users 查询）</summary>
        public string sender_nickname { get; set; } = "";
        /// <summary>发帖人头像 URL（community.php LEFT JOIN users 查询）</summary>
        public string sender_avatar { get; set; } = "";
        public string title { get; set; } = "";
        public string content { get; set; } = "";
        public string msg_type { get; set; } = "Normal";
        public string created_at { get; set; }
    }

    /// <summary>发送消息的请求体</summary>
    public class SendMessageRequest
    {
        public int channel_id { get; set; }
        public string sender_id { get; set; } = "";
        public string title { get; set; } = "";
        public string content { get; set; } = "";
    }

    /// <summary>发送消息的响应</summary>
    public class SendMessageResponse
    {
        public bool success { get; set; }
        public long id { get; set; }
        public string created_at { get; set; }
    }

    /// <summary>
    /// 聊天社区 HTTP 服务，封装与服务器社区 API 的通信。
    /// </summary>
    public class CommunityService
    {
        private static CommunityService _instance;
        public static CommunityService Instance => _instance ??= new CommunityService();

        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        private CommunityService()
        {
            _http = SafeHttpClientFactory.CreateClient();
        }

        /// <summary>获取频道列表</summary>
        public async Task<List<ServerChannel>> GetChannelsAsync()
        {
            try
            {
                string url = $"{ServerConfig.CommunityApiUrl}?action=channels";
                string json = await _http.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);
                var channels = doc.RootElement.GetProperty("channels")
                    .Deserialize<List<ServerChannel>>(_jsonOpts);
                return channels ?? new List<ServerChannel>();
            }
            catch (Exception ex)
            {
                Logger.Error("[社区] 获取频道列表失败: " + ex.Message);
                return new List<ServerChannel>();
            }
        }

        /// <summary>获取频道消息</summary>
        public async Task<List<ServerCommunityMessage>> GetMessagesAsync(int channelId, long since = 0, int limit = 50)
        {
            try
            {
                string url = $"{ServerConfig.CommunityApiUrl}?action=messages&channel_id={channelId}&since={since}&limit={limit}";
                string json = await _http.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);
                var messages = doc.RootElement.GetProperty("messages")
                    .Deserialize<List<ServerCommunityMessage>>(_jsonOpts);
                return messages ?? new List<ServerCommunityMessage>();
            }
            catch (Exception ex)
            {
                Logger.Error($"[社区] 获取频道 {channelId} 消息失败: " + ex.Message);
                return new List<ServerCommunityMessage>();
            }
        }

        /// <summary>发送消息</summary>
        public async Task<SendMessageResponse> SendMessageAsync(int channelId, string senderId, string title, string content)
        {
            try
            {
                string url = $"{ServerConfig.CommunityApiUrl}?action=send";
                var body = new SendMessageRequest
                {
                    channel_id = channelId,
                    sender_id = senderId,
                    title = title,
                    content = content
                };
                string jsonBody = JsonSerializer.Serialize(body, _jsonOpts);
                var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync(url, httpContent);
                string respJson = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SendMessageResponse>(respJson, _jsonOpts);
                return result ?? new SendMessageResponse();
            }
            catch (Exception ex)
            {
                Logger.Error("[社区] 发送消息失败: " + ex.Message);
                return new SendMessageResponse { success = false };
            }
        }

        /// <summary>创建频道（预留，当前仅在服务器端支持）</summary>
        public async Task<bool> CreateChannelAsync(string name, string description, string iconEmoji, string creatorId = "")
        {
            try
            {
                string url = $"{ServerConfig.CommunityApiUrl}?action=create_channel";
                var body = new { name, description, icon_emoji = iconEmoji, creator_id = creatorId };
                string jsonBody = JsonSerializer.Serialize(body, _jsonOpts);
                var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync(url, httpContent);
                string respJson = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(respJson);
                return doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
            }
            catch (Exception ex)
            {
                Logger.Error("[社区] 创建频道失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>上传用户头像到服务器</summary>
        /// <returns>返回 (success, errorMessage)。success=false 时 errorMessage 为失败原因</returns>
        public async Task<(bool success, string errorMessage)> UploadAvatarAsync(string qingniaoId, string avatarBase64)
        {
            try
            {
                string serverUrl = AccountManager.GetQingNiaoServer();
                string accessToken = PCL.Account.Settings.Get<string>("CacheQingNiaoAccess");

                if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(accessToken))
                {
                    Logger.Warning("[社区] 头像上传跳过：未登录青鸟账号");
                    return (false, "未登录青鸟账号，请先登录");
                }

                string url = $"http://{serverUrl}/api/auth.php?action=update_profile";
                var requestBody = new { avatar_url = avatarBase64 };
                string json = JsonSerializer.Serialize(requestBody, _jsonOpts);

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(request);
                string respJson = await resp.Content.ReadAsStringAsync();
                Logger.Info($"[社区] 头像上传 URL={url} 响应: {resp.StatusCode} body={respJson}");

                if (resp.IsSuccessStatusCode)
                {
                    return (true, "");
                }

                // 解析服务器错误信息
                string serverError = respJson;
                try
                {
                    var doc = JsonDocument.Parse(respJson);
                    if (doc.RootElement.TryGetProperty("error", out var err))
                        serverError = err.GetString() ?? "";
                }
                catch { }

                if (string.IsNullOrWhiteSpace(serverError))
                    serverError = $"服务器返回 {resp.StatusCode}";

                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return (false, "登录已过期，请重新登录");

                return (false, serverError);
            }
            catch (Exception ex)
            {
                Logger.Error("[社区] 头像上传失败: " + ex.Message);
                return (false, "网络连接失败，请检查网络后重试");
            }
        }
    }
}
