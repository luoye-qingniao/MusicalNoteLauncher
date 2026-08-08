using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// RedStone 红石联机服务 — 基于 RedStone 隧道 API 实现跨网络 Minecraft 联机。
    ///
    /// 完整流程（API.md）：
    ///   1. 选择中转服务器节点
    ///   2. POST :3000/apikey 注册 API Key（一次）
    ///   3. TCP 连接 :7000 发送 apikey 进入连接池
    ///   4. POST :3000/tunnels 创建隧道 → 返回 listenPort（远程玩家连接用）
    ///   5. 本地 TCP 中继：MC(:25565) ←→ 本程序 ←→ 服务器:7000
    ///   6. 退出时 DELETE :3000/tunnels 销毁隧道
    /// </summary>
    public class RedstoneService
    {
        private static RedstoneService _instance;
        public static RedstoneService Instance => _instance ??= new RedstoneService();

        private readonly HttpClient _http;
        private const string ServerListUrl = "https://shithub.site/server.json";
        private const int WebPort = 3000;
        private const int ControlPort = 7000;
        private const int McDefaultPort = 25565;

        /// <summary>已缓存的服务器列表 {名称 → IP}</summary>
        private Dictionary<string, string> _cachedServers;
        private DateTime _serversFetchedAt = DateTime.MinValue;
        private static readonly TimeSpan ServerCacheDuration = TimeSpan.FromMinutes(5);

        // 本地 TCP 中继状态
        private CancellationTokenSource _relayCts;
        private Task _relayTask;
        private TcpClient _controlTcp;        // → 服务器 :7000 控制/数据连接
        private TcpListener _localListener;    // ← MC 客户端连接
        private readonly object _relayLock = new();

        /// <summary>中继是否运行中</summary>
        public bool IsRelayRunning
        {
            get { lock (_relayLock) return _relayCts != null && !_relayCts.IsCancellationRequested; }
        }

        /// <summary>中继本地监听端口</summary>
        public int RelayLocalPort { get; private set; }

        /// <summary>控制连接状态（OK TUNNEL / OK WAITING / 错误）</summary>
        public string ControlStatus { get; private set; } = "";

        /// <summary>中继日志回调</summary>
        public event Action<string> OnRelayLog;

        private RedstoneService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        // ═══════════════════ 服务器列表 ═══════════════════

        /// <summary>获取可用服务器节点（带缓存）</summary>
        public async Task<Dictionary<string, string>> GetServersAsync()
        {
            if (_cachedServers != null && DateTime.Now - _serversFetchedAt < ServerCacheDuration)
                return _cachedServers;

            try
            {
                var json = await _http.GetStringAsync(ServerListUrl);
                var servers = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                _cachedServers = servers ?? new Dictionary<string, string>();
                _serversFetchedAt = DateTime.Now;
                return _cachedServers;
            }
            catch
            {
                // 返回缓存数据兜底
                return _cachedServers ?? new Dictionary<string, string>();
            }
        }

        // ═══════════════════ API Key 管理 ═══════════════════

        /// <summary>注册 API Key（只需一次）</summary>
        public async Task<(bool success, string message)> RegisterApikeyAsync(string serverIp, string apikey)
        {
            try
            {
                var url = $"http://{serverIp}:{WebPort}/apikey";
                var content = new StringContent(
                    JsonSerializer.Serialize(new { apikey }),
                    Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return (true, "API Key 注册成功");
                if ((int)response.StatusCode == 409)
                    return (true, "API Key 已存在");

                return (false, $"注册失败 ({response.StatusCode}): {body}");
            }
            catch (Exception ex)
            {
                return (false, $"连接失败: {ex.Message}");
            }
        }

        // ═══════════════════ 隧道管理 ═══════════════════

        /// <summary>创建隧道（支持公开房间）</summary>
        public async Task<(bool success, string message, RedstoneTunnelInfo tunnel)> CreateTunnelAsync(
            string serverIp, string apikey, bool isPublic = false,
            string title = "", string description = "", string mods = "",
            string version = "", bool online = true)
        {
            try
            {
                var query = isPublic ? "?publicAccess=1" : "";
                var url = $"http://{serverIp}:{WebPort}/tunnels{query}";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", apikey);

                if (isPublic)
                {
                    var body = new
                    {
                        title = SanitizeTitle(title),
                        description = SanitizeDesc(description),
                        mods = string.IsNullOrWhiteSpace(mods)
                            ? Array.Empty<string>()
                            : mods.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                        version,
                        online
                    };
                    var json = JsonSerializer.Serialize(body);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                var response = await _http.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tunnel = JsonSerializer.Deserialize<RedstoneTunnelInfo>(responseBody);
                    return (true, "隧道创建成功", tunnel);
                }

                var msg = (int)response.StatusCode switch
                {
                    401 => "API Key 无效",
                    429 => "该 API Key 已有隧道，需先关闭",
                    _ => $"创建失败 ({response.StatusCode})"
                };
                return (false, msg, null);
            }
            catch (Exception ex)
            {
                return (false, $"连接失败: {ex.Message}", null);
            }
        }

        /// <summary>查询隧道状态</summary>
        public async Task<(bool success, string message, List<RedstoneTunnelStatus> tunnels)> QueryTunnelsAsync(
            string serverIp, string apikey)
        {
            try
            {
                var url = $"http://{serverIp}:{WebPort}/tunnels";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", apikey);

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<RedstoneTunnelsResponse>(body);
                    return (true, "查询成功", result?.tunnels ?? new List<RedstoneTunnelStatus>());
                }

                return (false, "查询失败", null);
            }
            catch (Exception ex)
            {
                return (false, $"连接失败: {ex.Message}", null);
            }
        }

        /// <summary>删除/关闭隧道</summary>
        public async Task<(bool success, string message)> DeleteTunnelAsync(string serverIp, string apikey)
        {
            try
            {
                var url = $"http://{serverIp}:{WebPort}/tunnels";
                using var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Add("Authorization", apikey);

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return (true, "隧道已关闭");

                var msg = (int)response.StatusCode switch
                {
                    401 => "API Key 无效",
                    404 => "没有活跃隧道",
                    _ => $"关闭失败 ({response.StatusCode})"
                };
                return (false, msg);
            }
            catch (Exception ex)
            {
                return (false, $"连接失败: {ex.Message}");
            }
        }

        // ═══════════════════ 公开房间浏览 ═══════════════════

        /// <summary>获取公开房间列表（遍历所有服务器）</summary>
        public async Task<(bool success, string message, List<RedstonePublicRoom> rooms)> GetPublicRoomsAsync(int offset = 0)
        {
            try
            {
                var servers = await GetServersAsync();
                var allRooms = new List<RedstonePublicRoom>();

                foreach (var kv in servers)
                {
                    try
                    {
                        var url = $"http://{kv.Value}:{WebPort}/tunnels/list?offset={offset}";
                        var json = await _http.GetStringAsync(url);
                        var result = JsonSerializer.Deserialize<RedstonePublicRoomsResponse>(json);
                        if (result?.tunnels != null)
                        {
                            foreach (var room in result.tunnels)
                            {
                                room.serverName = kv.Key;
                                room.serverIp = kv.Value;
                            }
                            allRooms.AddRange(result.tunnels);
                        }
                    }
                    catch { /* 单个服务器失败不阻塞 */ }
                }

                return (true, "获取成功", allRooms);
            }
            catch (Exception ex)
            {
                return (false, $"获取失败: {ex.Message}", new List<RedstonePublicRoom>());
            }
        }

        /// <summary>获取单个房间的模组列表</summary>
        public async Task<(bool success, string message, List<string> mods)> GetRoomModsAsync(int roomId)
        {
            try
            {
                var servers = await GetServersAsync();

                foreach (var kv in servers)
                {
                    try
                    {
                        var url = $"http://{kv.Value}:{WebPort}/tunnels/mods?id={roomId}";
                        var json = await _http.GetStringAsync(url);
                        var result = JsonSerializer.Deserialize<RedstoneRoomModsResponse>(json);
                        if (result?.mods != null)
                            return (true, "获取成功", result.mods);
                    }
                    catch { /* 尝试下一个服务器 */ }
                }

                return (false, "未找到该房间", new List<string>());
            }
            catch (Exception ex)
            {
                return (false, $"获取失败: {ex.Message}", new List<string>());
            }
        }

        // ═══════════════════ 工具方法 ═══════════════════

        /// <summary>过滤标题非法字符，限制 8 字符</summary>
        private static string SanitizeTitle(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var illegal = new[] { '<', '>', '"', '\'', '&', '\\', '/', '`', ';', '(', ')', '{', '}', '[', ']' };
            foreach (var c in illegal) s = s.Replace(c.ToString(), "");
            return s.Length > 8 ? s.Substring(0, 8) : s;
        }

        /// <summary>过滤简介非法字符，限制 100 字符</summary>
        private static string SanitizeDesc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var illegal = new[] { '<', '>', '"', '\'', '&', '\\', '/', '`', ';', '(', ')', '{', '}', '[', ']' };
            foreach (var c in illegal) s = s.Replace(c.ToString(), "");
            return s.Length > 100 ? s.Substring(0, 100) : s;
        }

        // ═══════════════════ 本地 TCP 中继 ═══════════════════
        //  步骤3: TCP → :7000 发送 apikey → OK TUNNEL / OK WAITING
        //  步骤5: MC(:25565) ←→ 本程序 ←→ 服务器:7000（双向转发）

        /// <summary>启动本地 TCP 中继（阻塞直到控制连接就绪）</summary>
        /// <param name="serverIp">RedStone 服务器 IP</param>
        /// <param name="apikey">API Key</param>
        /// <param name="localPort">本地监听端口（0=自动分配，默认 0）</param>
        /// <returns>(success, message, tunnelId)</returns>
        public async Task<(bool success, string message, string tunnelId)> StartTunnelRelayAsync(
            string serverIp, string apikey, int localPort = 0)
        {
            lock (_relayLock)
            {
                if (IsRelayRunning) return (false, "中继已在运行", null);
            }

            try
            {
                // 1) 连接控制端口 :7000
                LogRelay($"连接控制端口 {serverIp}:{ControlPort}...");
                _controlTcp = new TcpClient();
                await _controlTcp.ConnectAsync(serverIp, ControlPort);
                LogRelay("控制端口已连接");

                var stream = _controlTcp.GetStream();

                // 2) 发送 apikey
                var apikeyBytes = Encoding.UTF8.GetBytes(apikey + "\n");
                await stream.WriteAsync(apikeyBytes, 0, apikeyBytes.Length);
                await stream.FlushAsync();
                LogRelay("已发送 API Key，等待隧道...");

                // 3) 读取响应行
                var reader = new StreamReader(stream, Encoding.UTF8);
                var line = await reader.ReadLineAsync();

                if (string.IsNullOrEmpty(line))
                {
                    throw new IOException("控制连接对方未响应");
                }

                LogRelay($"服务器: {line}");

                string tunnelId = null;
                if (line.StartsWith("OK TUNNEL"))
                {
                    // 隧道已就绪：OK TUNNEL <tunnelId>
                    var parts = line.Split(' ');
                    tunnelId = parts.Length >= 3 ? parts[2] : "";
                    ControlStatus = "TUNNEL";
                }
                else if (line.StartsWith("OK WAITING"))
                {
                    // 隧道尚未创建，等待中
                    ControlStatus = "WAITING";
                    LogRelay("等待隧道创建...（请先创建隧道）");

                    // 阻塞等待隧道创建（服务器会在隧道创建后发送 OK TUNNEL）
                    line = await reader.ReadLineAsync();
                    LogRelay($"服务器: {line}");
                    if (line != null && line.StartsWith("OK TUNNEL"))
                    {
                        var parts = line.Split(' ');
                        tunnelId = parts.Length >= 3 ? parts[2] : "";
                        ControlStatus = "TUNNEL";
                    }
                    else
                    {
                        throw new IOException($"意外的服务器响应: {line}");
                    }
                }
                else if (line.StartsWith("ERR"))
                {
                    throw new IOException($"服务器错误: {line}");
                }
                else
                {
                    throw new IOException($"未知响应: {line}");
                }

                // 4) 启动本地监听（MC 客户端连接端）
                var mcListener = new TcpListener(System.Net.IPAddress.Loopback, localPort);
                mcListener.Start();
                _localListener = mcListener;
                RelayLocalPort = ((System.Net.IPEndPoint)mcListener.LocalEndpoint).Port;
                LogRelay($"本地 TCP 中继已启动: 127.0.0.1:{RelayLocalPort}");

                // 5) 启动中继任务（后台双向转发）
                _relayCts = new CancellationTokenSource();
                var ct = _relayCts.Token;
                var controlStream = stream;
                var controlClient = _controlTcp;

                _relayTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            // 接受一个 MC 客户端连接
                            var mcClient = await mcListener.AcceptTcpClientAsync();
                            LogRelay($"MC 客户端已连接: {mcClient.Client.RemoteEndPoint}");

                            // 创建两个转发任务（双向）
                            var mcStream = mcClient.GetStream();
                            var toServer = ForwardAsync(mcStream, controlStream, ct, "MC→服务器");
                            var toMc = ForwardAsync(controlStream, mcStream, ct, "服务器→MC");

                            await Task.WhenAny(toServer, toMc);
                            // 一端断开就清理
                            try { mcClient.Close(); } catch { }
                            LogRelay("MC 客户端已断开");
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        LogRelay($"中继任务异常: {ex.Message}");
                    }
                    finally
                    {
                        StopTunnelRelay();
                    }
                }, ct);

                return (true, "中继已启动", tunnelId);
            }
            catch (Exception ex)
            {
                StopTunnelRelay();
                return (false, ex.Message, null);
            }
        }

        /// <summary>停止本地 TCP 中继</summary>
        public void StopTunnelRelay()
        {
            lock (_relayLock)
            {
                try { _relayCts?.Cancel(); } catch { }
                try { _localListener?.Stop(); } catch { }
                try { _controlTcp?.Close(); } catch { }

                _localListener = null;
                _controlTcp = null;
                _relayCts = null;
                ControlStatus = "";
                RelayLocalPort = 0;
            }
            LogRelay("TCP 中继已停止");
        }

        /// <summary>获取当前中继状态 DTO</summary>
        public RedstoneRelayStatus GetRelayStatus()
        {
            return new RedstoneRelayStatus
            {
                running = IsRelayRunning,
                localPort = RelayLocalPort,
                controlStatus = ControlStatus
            };
        }

        /// <summary>双向转发（单方向）</summary>
        private static async Task ForwardAsync(NetworkStream from, NetworkStream to, CancellationToken ct, string tag)
        {
            var buf = new byte[8192];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var read = await from.ReadAsync(buf, 0, buf.Length, ct);
                    if (read == 0) break; // EOF
                    await to.WriteAsync(buf, 0, read, ct);
                    await to.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }

        private void LogRelay(string msg) => OnRelayLog?.Invoke(msg);
    }

    // ═══════════════════ DTO ═══════════════════

    /// <summary>隧道创建响应</summary>
    public class RedstoneTunnelInfo
    {
        public string tunnelId { get; set; }
        public int listenPort { get; set; }
        public int maxPlayers { get; set; }
    }

    /// <summary>隧道查询响应包装</summary>
    public class RedstoneTunnelsResponse
    {
        public List<RedstoneTunnelStatus> tunnels { get; set; } = new();
    }

    /// <summary>隧道状态</summary>
    public class RedstoneTunnelStatus
    {
        public string id { get; set; }
        public int listenPort { get; set; }
        public string createdAt { get; set; }
    }

    /// <summary>公开房间列表响应</summary>
    public class RedstonePublicRoomsResponse
    {
        public List<RedstonePublicRoom> tunnels { get; set; } = new();
    }

    /// <summary>公开房间</summary>
    public class RedstonePublicRoom
    {
        public int id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string version { get; set; }
        public int port { get; set; }
        public bool online { get; set; }

        // 附加信息（由 GetPublicRoomsAsync 填入）
        public string serverName { get; set; }
        public string serverIp { get; set; }
    }

    /// <summary>房间模组列表响应</summary>
    public class RedstoneRoomModsResponse
    {
        public List<string> mods { get; set; } = new();
    }

    /// <summary>本地 TCP 中继状态</summary>
    public class RedstoneRelayStatus
    {
        public bool running { get; set; }
        public int localPort { get; set; }
        public string controlStatus { get; set; } = "";
    }
}
