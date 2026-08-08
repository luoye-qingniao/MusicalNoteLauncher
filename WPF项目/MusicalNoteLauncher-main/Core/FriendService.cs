using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace MusicalNoteLauncher.Core
{
    /// <summary>服务器返回的好友数据</summary>
    public class ServerFriendItem
    {
        [JsonPropertyName("friend_id")] public string friend_id { get; set; } = "";
        [JsonPropertyName("friend_nickname")] public string friend_nickname { get; set; } = "";
        [JsonPropertyName("last_heartbeat")] public string last_heartbeat { get; set; }
        [JsonPropertyName("is_online")] public int is_online { get; set; }
    }

    /// <summary>服务器返回的消息数据</summary>
    public class ServerMessage
    {
        [JsonPropertyName("id")] public long id { get; set; }
        [JsonPropertyName("sender_id")] public string sender_id { get; set; } = "";
        [JsonPropertyName("receiver_id")] public string receiver_id { get; set; } = "";
        [JsonPropertyName("content")] public string content { get; set; } = "";
        [JsonPropertyName("msg_type")] public string msg_type { get; set; } = "Normal";
        [JsonPropertyName("invite_network_name")] public string invite_network_name { get; set; } = "";
        [JsonPropertyName("invite_network_secret")] public string invite_network_secret { get; set; } = "";
        [JsonPropertyName("invite_game_version")] public string invite_game_version { get; set; } = "";
        [JsonPropertyName("invite_accepted")] public int invite_accepted { get; set; }
        [JsonPropertyName("created_at")] public string created_at { get; set; }
    }

    /// <summary>
    /// 好友系统 HTTP 客户端，封装与服务器好友 API 的通信。
    /// </summary>
    public class FriendService
    {
        private static FriendService _instance;
        public static FriendService Instance => _instance ??= new FriendService();

        private readonly HttpClient _http;
        private Timer _heartbeatTimer;
        private long _lastMessageId;
        private string _userId;

        /// <summary>大小写不敏感的 JSON 反序列化选项，兼容 PHP 端不同命名风格</summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public event Action<List<ServerMessage>> OnNewMessages;
        public event Action<List<string>> OnOnlineStatusChanged;

        public bool IsRunning { get; private set; }

        private FriendService()
        {
            _http = SafeHttpClientFactory.CreateClient();
            _lastMessageId = 0;
        }

        /// <summary>
        /// 启动好友系统（登录后调用），开始心跳和消息轮询。
        /// </summary>
        public void Start(string userId)
        {
            _userId = userId;
            IsRunning = true;

            _heartbeatTimer = new Timer(30000); // 30秒心跳
            _heartbeatTimer.Elapsed += async (s, e) => await DoHeartbeatAsync();
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Start();

            _ = DoHeartbeatAsync();
            _ = PollLoopAsync();
        }

        /// <summary>
        /// 停止好友系统（退出登录时调用）。
        /// </summary>
        public void Stop()
        {
            IsRunning = false;
            _heartbeatTimer?.Stop();
            _heartbeatTimer?.Dispose();
        }

        /// <summary>
        /// 获取好友列表。
        /// </summary>
        public async Task<List<ServerFriendItem>> GetFriendsAsync()
        {
            try
            {
                var url = $"{ServerConfig.FriendsApiUrl}?action=list&user_id={Uri.EscapeDataString(_userId)}";
                var json = await _http.GetStringAsync(url);
                Debug.WriteLine($"[FriendService] 好友列表原始响应: {json}");
                var resp = JsonSerializer.Deserialize<JsonElement>(json);
                if (resp.TryGetProperty("friends", out var arr))
                {
                    var list = JsonSerializer.Deserialize<List<ServerFriendItem>>(arr.GetRawText(), _jsonOptions);
                    Debug.WriteLine($"[FriendService] 解析到 {list?.Count ?? 0} 个好友");
                    return list ?? new();
                }
                Debug.WriteLine($"[FriendService] 响应中未找到 'friends' 字段");
            }
            catch (Exception ex) { Debug.WriteLine($"[FriendService] 获取好友列表失败: {ex.Message}"); }
            return new List<ServerFriendItem>();
        }

        /// <summary>
        /// 添加好友。
        /// </summary>
        public async Task<(bool success, string error)> AddFriendAsync(string friendId, string nickname = "")
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    user_id = _userId,
                    friend_id = friendId,
                    friend_nickname = string.IsNullOrEmpty(nickname) ? friendId : nickname
                });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync($"{ServerConfig.FriendsApiUrl}?action=add", content);
                var json = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(json);

                if (resp.IsSuccessStatusCode && result.TryGetProperty("success", out _))
                    return (true, null);

                string error = "添加失败";
                if (result.TryGetProperty("error", out var errProp))
                    error = errProp.GetString();
                return (false, error);
            }
            catch (Exception ex)
            {
                return (false, $"网络错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除好友。
        /// </summary>
        public async Task<bool> RemoveFriendAsync(string friendId)
        {
            try
            {
                var body = JsonSerializer.Serialize(new { user_id = _userId, friend_id = friendId });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync($"{ServerConfig.FriendsApiUrl}?action=remove", content);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// 发送聊天消息。
        /// </summary>
        public async Task<bool> SendMessageAsync(string receiverId, string messageContent)
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    sender_id = _userId,
                    receiver_id = receiverId,
                    content = messageContent
                });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync($"{ServerConfig.FriendsApiUrl}?action=send", content);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// 发送联机邀请。
        /// </summary>
        public async Task<bool> SendInviteAsync(string receiverId, string networkName, string networkSecret, string gameVersion)
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    sender_id = _userId,
                    receiver_id = receiverId,
                    network_name = networkName,
                    network_secret = networkSecret,
                    game_version = gameVersion
                });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync($"{ServerConfig.FriendsApiUrl}?action=invite", content);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// 接受联机邀请。
        /// </summary>
        public async Task<bool> AcceptInviteAsync(long messageId)
        {
            try
            {
                var body = JsonSerializer.Serialize(new { message_id = messageId });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync($"{ServerConfig.FriendsApiUrl}?action=accept_invite", content);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// 心跳：上报在线状态并获取好友在线信息。
        /// </summary>
        private async Task DoHeartbeatAsync()
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    user_id = _userId,
                    launcher_version = UpdateService.CurrentVersion
                });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync($"{ServerConfig.FriendsApiUrl}?action=heartbeat", content);
                var json = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(json);

                if (result.TryGetProperty("online_friends", out var arr))
                {
                    var onlineList = new List<string>();
                    foreach (var item in arr.EnumerateArray())
                        onlineList.Add(item.GetString());
                    OnOnlineStatusChanged?.Invoke(onlineList);
                }
            }
            catch { /* 心跳失败静默忽略 */ }
        }

        /// <summary>
        /// 轮询新消息。
        /// </summary>
        private async Task PollLoopAsync()
        {
            while (IsRunning)
            {
                try
                {
                    var url = $"{ServerConfig.FriendsApiUrl}?action=poll&user_id={Uri.EscapeDataString(_userId)}&since={_lastMessageId}";
                    var json = await _http.GetStringAsync(url);
                    var resp = JsonSerializer.Deserialize<JsonElement>(json);

                    if (resp.TryGetProperty("messages", out var arr))
                    {
                        var messages = JsonSerializer.Deserialize<List<ServerMessage>>(arr.GetRawText(), _jsonOptions);
                        if (messages != null && messages.Count > 0)
                        {
                            _lastMessageId = messages[^1].id;
                            OnNewMessages?.Invoke(messages);
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[FriendService] 轮询失败: {ex.Message}"); }

                await Task.Delay(5000); // 5秒轮询
            }
        }

        // ======== 加密聊天消息 ========

        /// <summary>聊天消息数据模型</summary>
        public class ChatMessageItem
        {
            public long id { get; set; }
            public string sender_id { get; set; } = "";
            public string receiver_id { get; set; } = "";
            public string encrypted_content { get; set; } = "";
            public string created_at { get; set; } = "";
        }

        /// <summary>发送加密聊天消息到服务器</summary>
        public async Task<bool> SendChatMessageAsync(string receiverId, string encryptedContent)
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    sender_id = _userId,
                    receiver_id = receiverId,
                    encrypted_content = encryptedContent
                });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync($"{ServerConfig.ChatApiUrl}?action=send", content);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FriendService] 发送聊天消息失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>获取与某好友的聊天历史</summary>
        public async Task<List<ChatMessageItem>> GetChatHistoryAsync(string friendId, long sinceId = 0, int limit = 50)
        {
            try
            {
                var url = $"{ServerConfig.ChatApiUrl}?action=history&user_id={Uri.EscapeDataString(_userId)}&friend_id={Uri.EscapeDataString(friendId)}&since={sinceId}&limit={limit}";
                var json = await _http.GetStringAsync(url);
                var resp = JsonSerializer.Deserialize<JsonElement>(json);
                if (resp.TryGetProperty("messages", out var arr))
                    return JsonSerializer.Deserialize<List<ChatMessageItem>>(arr.GetRawText(), _jsonOptions) ?? new();
            }
            catch (Exception ex) { Debug.WriteLine($"[FriendService] 获取聊天历史失败: {ex.Message}"); }
            return new List<ChatMessageItem>();
        }

        /// <summary>轮询所有好友的新聊天消息</summary>
        public async Task<List<ChatMessageItem>> PollChatMessagesAsync(long sinceId = 0)
        {
            try
            {
                var url = $"{ServerConfig.ChatApiUrl}?action=poll&user_id={Uri.EscapeDataString(_userId)}&since={sinceId}";
                var json = await _http.GetStringAsync(url);
                var resp = JsonSerializer.Deserialize<JsonElement>(json);
                if (resp.TryGetProperty("messages", out var arr))
                    return JsonSerializer.Deserialize<List<ChatMessageItem>>(arr.GetRawText(), _jsonOptions) ?? new();
            }
            catch (Exception ex) { Debug.WriteLine($"[FriendService] 轮询聊天消息失败: {ex.Message}"); }
            return new List<ChatMessageItem>();
        }
    }
}
