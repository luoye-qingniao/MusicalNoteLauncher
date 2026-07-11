using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 远程日志上报服务 —— 将启动日志和崩溃日志发送到测试服务器。
    /// 所有网络操作均为"尽最大努力"，失败不影响本地功能。
    /// </summary>
    public static class LogReporter
    {
        private static readonly HttpClient _http = SafeHttpClientFactory.CreateClient();
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        /// <summary>客户端唯一标识（机器名+用户名混合MD5，不包含敏感信息）</summary>
        private static string _clientId = null!;

        /// <summary>
        /// 获取客户端唯一 ID（机器名 + 用户名字符串的 MD5，非隐私数据）
        /// </summary>
        public static string GetClientId()
        {
            if (_clientId != null!) return _clientId;

            string raw = $"{Environment.MachineName}_{Environment.UserName}";
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(raw));
            _clientId = Convert.ToHexString(hash).ToLowerInvariant();
            return _clientId;
        }

        /// <summary>
        /// 上报启动日志（非阻塞，异步发送）
        /// </summary>
        /// <param name="isSuccess">启动是否成功</param>
        /// <param name="errorMessage">失败时的错误信息（可选）</param>
        public static async Task ReportStartupAsync(bool isSuccess, string? errorMessage = null)
        {
            try
            {
                var payload = new
                {
                    type = "startup",
                    client_id = GetClientId(),
                    launcher_version = UpdateService.CurrentVersion,
                    os_version = Environment.OSVersion.ToString(),
                    clr_version = Environment.Version.ToString(),
                    startup_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    is_success = isSuccess ? 1 : 0,
                    error_message = errorMessage,
                };

                string json = JsonSerializer.Serialize(payload, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(ServerConfig.LogApiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    Logger.Info("[LogReporter] 启动日志上报成功");
                }
                else
                {
                    Logger.Warning($"[LogReporter] 启动日志上报失败: HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                // 静默处理：日志上报失败不影响本地运行
                Logger.Warning($"[LogReporter] 启动日志上报异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 上报崩溃日志（非阻塞，异步发送）
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <param name="threadName">崩溃线程类型（如 "UI线程"、"后台线程"）</param>
        /// <param name="isTerminating">是否导致进程终止</param>
        public static async Task ReportCrashAsync(Exception exception, string threadName = "", bool isTerminating = false)
        {
            try
            {
                var payload = new
                {
                    type = "crash",
                    client_id = GetClientId(),
                    launcher_version = UpdateService.CurrentVersion,
                    crash_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    exception_type = exception.GetType().FullName ?? exception.GetType().Name,
                    exception_message = exception.Message,
                    stack_trace = exception.StackTrace ?? "",
                    thread_name = threadName,
                    is_terminating = isTerminating ? 1 : 0,
                };

                string json = JsonSerializer.Serialize(payload, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(ServerConfig.LogApiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    Logger.Info("[LogReporter] 崩溃日志上报成功");
                }
                else
                {
                    Logger.Warning($"[LogReporter] 崩溃日志上报失败: HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[LogReporter] 崩溃日志上报异常: {ex.Message}");
            }
        }
    }
}
