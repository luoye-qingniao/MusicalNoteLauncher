using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace MusicalNoteLauncher.Core
{
    /// <summary>服务器返回的好友数据</summary>
    public class ServerFriendItem
    {
        public string friend_id { get; set; } = "";
        public string friend_nickname { get; set; } = "";
        public string last_heartbeat { get; set; }
        public int is_online { get; set; }
    }

    /// <summary>服务器返回的消息数据</summary>
    public class ServerMessage
    {
        public long id { get; set; }
        public string sender_id { get; set; } = "";
        public string receiver_id { get; set; } = "";
        public string content { get; set; } = "";
        public string msg_type { get; set; } = "Normal";
        public string invite_network_name { get; set; } = "";
        public string invite_network_secret { get; set; } = "";
        public string invite_game_version { get; set; } = "";
        public int invite_accepted { get; set; }
        public string created_at { get; set; }
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
                var resp = JsonSerializer.Deserialize<JsonElement>(json);
                if (resp.TryGetProperty("friends", out var arr))
                    return JsonSerializer.Deserialize<List<ServerFriendItem>>(arr.GetRawText()) ?? new();
            }
            catch { /* 网络错误，返回空列表 */ }
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
                        var messages = JsonSerializer.Deserialize<List<ServerMessage>>(arr.GetRawText());
                        if (messages != null && messages.Count > 0)
                        {
                            _lastMessageId = messages[^1].id;
                            OnNewMessages?.Invoke(messages);
                        }
                    }
                }
                catch { /* 轮询失败静默重试 */ }

                await Task.Delay(5000); // 5秒轮询
            }
        }
    }
}
