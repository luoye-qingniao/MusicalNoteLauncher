using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 白名单验证服务 —— 在启动前向服务器验证当前用户是否在白名单中。
    /// </summary>
    public static class WhitelistService
    {
        private static readonly HttpClient _http = SafeHttpClientFactory.CreateClient();
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        /// <summary>
        /// 白名单验证结果
        /// </summary>
        public class WhitelistResult
        {
            public bool Allowed { get; set; }
            public string Message { get; set; } = "";
            public string Username { get; set; } = "";
        }

        /// <summary>
        /// 验证当前用户是否在白名单中
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>验证结果。网络错误时默认放行（避免网络问题阻塞离线用户）</returns>
        public static async Task<WhitelistResult> CheckAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return new WhitelistResult { Allowed = false, Message = "用户名为空" };

            try
            {
                string url = $"{ServerConfig.WhitelistApiUrl}?username={Uri.EscapeDataString(username)}&client_id={LogReporter.GetClientId()}";
                string json = await _http.GetStringAsync(url);

                var result = JsonSerializer.Deserialize<WhitelistResult>(json, _jsonOptions);
                return result ?? new WhitelistResult
                {
                    Allowed = false,
                    Message = "服务器返回数据异常",
                };
            }
            catch (HttpRequestException ex)
            {
                Logger.Warning($"[WhitelistService] 白名单验证请求失败 (HttpRequestException): {ex.Message}");
                // 网络不可达时默认放行（避免断网用户无法使用启动器）
                return new WhitelistResult
                {
                    Allowed = true,
                    Message = "白名单服务不可达，已默认放行",
                };
            }
            catch (TaskCanceledException)
            {
                Logger.Warning("[WhitelistService] 白名单验证超时");
                return new WhitelistResult
                {
                    Allowed = true,
                    Message = "白名单验证超时，已默认放行",
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"[WhitelistService] 白名单验证异常: {ex.Message}", ex);
                return new WhitelistResult
                {
                    Allowed = true,
                    Message = "白名单验证异常，已默认放行",
                };
            }
        }
    }
}
