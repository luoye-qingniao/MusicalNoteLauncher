using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PCL.Auth.Microsoft
{
    /// <summary>
    /// 微软登录完整流程实现
    /// 基于 Microsoft OAuth2 Authorization Code Flow + Xbox Live 认证链
    /// 
    /// 流程概览（6 步）：
    ///   步骤 1 → 打开浏览器授权 → 用户粘贴回调 URL → 换取 AccessToken / RefreshToken
    ///   步骤 2 → OAuth AccessToken → XBL Token
    ///   步骤 3 → XBL Token → XSTS Token + User Hash (UHS)
    ///   步骤 4 → XSTS + UHS → Minecraft AccessToken
    ///   步骤 5 → 验证 Minecraft 正版授权（是否购买）
    ///   步骤 6 → 获取 Minecraft 玩家档案（UUID + 用户名）
    /// </summary>
    public class MinecraftMsAuth
    {
        static MinecraftMsAuth()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        }

        private static string OAuthClientId =>
            Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID") ?? "00000000402b5328";

        private static TaskCompletionSource<string> _authCodeTcs;

        /// <summary>
        /// 触发当需要用户打开浏览器登录时，参数为授权 URL
        /// </summary>
        public static event Action<string> OnBrowserAuthRequired;

        /// <summary>
        /// 用户完成浏览器授权后，提交完整的回调 URL
        /// </summary>
        public static bool SubmitAuthCode(string fullRedirectUrl)
        {
            try
            {
                string code = null;

                // 方法1：标准 URL 解析
                int codeIdx = fullRedirectUrl.IndexOf("code=");
                if (codeIdx >= 0)
                {
                    int start = codeIdx + 5;
                    int end = fullRedirectUrl.IndexOf('&', start);
                    code = end > 0
                        ? fullRedirectUrl.Substring(start, end - start)
                        : fullRedirectUrl.Substring(start);
                    code = Uri.UnescapeDataString(code);
                }

                if (!string.IsNullOrEmpty(code))
                {
                    _authCodeTcs?.TrySetResult(code);
                    return true;
                }

                _authCodeTcs?.TrySetException(new MsAuthException("未从 URL 中提取到授权码"));
                return false;
            }
            catch (Exception ex)
            {
                _authCodeTcs?.TrySetException(new MsAuthException("解析授权码失败", ex));
                return false;
            }
        }

        /// <summary>
        /// 完整的微软登录入口。
        /// </summary>
        public static async Task<MsLoginResult> LoginAsync(
            string oAuthRefreshToken,
            Action<double> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                onProgress?.Invoke(0.05);

                string oAuthAccessToken;
                string newRefreshToken;

                if (string.IsNullOrEmpty(oAuthRefreshToken))
                {
                    (oAuthAccessToken, newRefreshToken) = await Step1_NewAuthCodeAsync(cancellationToken);
                }
                else
                {
                    (oAuthAccessToken, newRefreshToken) = await Step1_RefreshAsync(oAuthRefreshToken);
                    if (oAuthAccessToken == "Relogin")
                        throw new MsAuthReloginRequiredException("需要重新登录");
                }

                cancellationToken.ThrowIfCancellationRequested();
                onProgress?.Invoke(0.25);

                string xblToken = await Step2_GetXblTokenAsync(oAuthAccessToken);

                cancellationToken.ThrowIfCancellationRequested();
                onProgress?.Invoke(0.40);

                (string xstsToken, string uhs) = await Step3_GetXstsTokenAsync(xblToken);

                cancellationToken.ThrowIfCancellationRequested();
                onProgress?.Invoke(0.55);

                (string mcAccessToken, long expiresAt) = await Step4_GetMcAccessTokenAsync(xstsToken, uhs);

                cancellationToken.ThrowIfCancellationRequested();
                onProgress?.Invoke(0.70);

                await Step5_CheckOwnershipAsync(mcAccessToken);

                cancellationToken.ThrowIfCancellationRequested();
                onProgress?.Invoke(0.85);

                var profile = await Step6_GetProfileAsync(mcAccessToken);

                onProgress?.Invoke(0.98);

                return new MsLoginResult
                {
                    OAuthRefreshToken = newRefreshToken,
                    OAuthAccessToken = oAuthAccessToken,
                    McAccessToken = mcAccessToken,
                    McExpiresAt = expiresAt,
                    Uuid = profile.Uuid,
                    UserName = profile.UserName,
                    ProfileJson = profile.ProfileJson
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (MsAuthException) { throw; }
            catch (Exception ex)
            {
                throw new MsAuthException("微软登录失败", ex);
            }
        }

        public static bool IsTokenValid(long expiresAt, string cachedUserName, string expectedUserName)
        {
            if (string.IsNullOrEmpty(cachedUserName) || expiresAt <= 0)
                return false;
            if (expiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                return false;
            if (cachedUserName != expectedUserName)
                return false;
            return true;
        }

        #region 步骤 1：Authorization Code Flow

        private static async Task<(string accessToken, string refreshToken)> Step1_NewAuthCodeAsync(
            CancellationToken cancellationToken)
        {
            // 构建授权 URL
            string authUrl =
                "https://login.live.com/oauth20_authorize.srf?" +
                $"client_id={OAuthClientId}&" +
                "response_type=code&" +
                "scope=XboxLive.signin%20offline_access&" +
                "redirect_uri=https://login.live.com/oauth20_desktop.srf";

            _authCodeTcs = new TaskCompletionSource<string>();

            // 触发事件：通知 UI 打开浏览器
            OnBrowserAuthRequired?.Invoke(authUrl);

            // 等待用户提交授权码（最多 10 分钟）
            using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            {
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token))
                {
                    linkedCts.Token.Register(() =>
                        _authCodeTcs.TrySetCanceled(linkedCts.Token));

                    string authCode;
                    try
                    {
                        authCode = await _authCodeTcs.Task;
                    }
                    catch (OperationCanceledException)
                    {
                        if (timeoutCts.IsCancellationRequested)
                            throw new MsAuthException("登录超时（10 分钟未操作）");
                        throw new MsAuthUserCancelledException("用户取消了登录");
                    }
                    catch (Exception)
                    {
                        throw;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    // 用授权码换取 token
                    string tokenUrl = "https://login.live.com/oauth20_token.srf";
                    string tokenBody =
                        $"client_id={OAuthClientId}&" +
                        $"code={Uri.EscapeDataString(authCode)}&" +
                        "grant_type=authorization_code&" +
                        "redirect_uri=https://login.live.com/oauth20_desktop.srf&" +
                        "scope=XboxLive.signin%20offline_access";

                    try
                    {
                        string result = await PostFormUrlEncodedAsync(tokenUrl, tokenBody);
                        JObject resultJson = JObject.Parse(result);

                        if (resultJson["error"] != null)
                        {
                            string error = resultJson["error"].ToString();
                            string errorDesc = resultJson["error_description"]?.ToString() ?? error;
                            throw new MsAuthException($"换取 Token 失败：{error} - {errorDesc}");
                        }

                        return (
                            resultJson["access_token"].ToString(),
                            resultJson["refresh_token"].ToString()
                        );
                    }
                    catch (MsAuthException) { throw; }
                    catch (HttpRequestException ex)
                    {
                        throw new MsAuthException("换取 Token 失败：" + ex.Message, ex);
                    }
                }
            }
        }

        private static async Task<(string accessToken, string refreshToken)> Step1_RefreshAsync(
            string refreshToken)
        {
            string url = "https://login.live.com/oauth20_token.srf";
            string body =
                $"client_id={OAuthClientId}&" +
                $"refresh_token={Uri.EscapeDataString(refreshToken)}&" +
                "grant_type=refresh_token&" +
                "scope=XboxLive.signin%20offline_access";

            try
            {
                string result = await PostFormUrlEncodedWithHeadersAsync(url, body,
                    new Dictionary<string, string>
                    {
                        {"Accept-Language", "en-US,en;q=0.5"},
                        {"X-Requested-With", "XMLHttpRequest"}
                    });

                JObject resultJson = JObject.Parse(result);
                return (
                    resultJson["access_token"].ToString(),
                    resultJson["refresh_token"].ToString()
                );
            }
            catch (HttpRequestException ex)
            {
                string response = ex.Message;
                if (response.Contains("must sign in again", StringComparison.OrdinalIgnoreCase) ||
                    response.Contains("password expired", StringComparison.OrdinalIgnoreCase) ||
                    (response.Contains("refresh_token") && response.Contains("is not valid", StringComparison.OrdinalIgnoreCase)) ||
                    response.Contains("expired", StringComparison.OrdinalIgnoreCase))
                {
                    return ("Relogin", "");
                }
                if (response.Contains("Account security interrupt"))
                    throw new MsAuthException("该账号由于安全问题无法登陆");
                if (response.Contains("service abuse"))
                    throw new MsAuthException("该账号已被微软封禁，无法登录");
                throw;
            }
        }

        #endregion

        #region 步骤 2：OAuth → XBL Token

        private static async Task<string> Step2_GetXblTokenAsync(string accessToken)
        {
            string url = "https://user.auth.xboxlive.com/user/authenticate";
            string prefix = accessToken.StartsWith("d=") ? "" : "d=";

            string requestBody = new JObject
            {
                ["Properties"] = new JObject
                {
                    ["AuthMethod"] = "RPS",
                    ["SiteName"] = "user.auth.xboxlive.com",
                    ["RpsTicket"] = prefix + accessToken
                },
                ["RelyingParty"] = "http://auth.xboxlive.com",
                ["TokenType"] = "JWT"
            }.ToString(Formatting.None);

            string result = await PostJsonAsync(url, requestBody);
            return JObject.Parse(result)["Token"].ToString();
        }

        #endregion

        #region 步骤 3：XBL Token → XSTS Token + UHS

        private static async Task<(string xstsToken, string uhs)> Step3_GetXstsTokenAsync(string xblToken)
        {
            string url = "https://xsts.auth.xboxlive.com/xsts/authorize";

            string requestBody = new JObject
            {
                ["Properties"] = new JObject
                {
                    ["SandboxId"] = "RETAIL",
                    ["UserTokens"] = new JArray { xblToken }
                },
                ["RelyingParty"] = "rp://api.minecraftservices.com/",
                ["TokenType"] = "JWT"
            }.ToString(Formatting.None);

            try
            {
                string result = await PostJsonAsync(url, requestBody);
                JObject json = JObject.Parse(result);

                return (
                    json["Token"].ToString(),
                    json["DisplayClaims"]["xui"][0]["uhs"].ToString()
                );
            }
            catch (HttpRequestException ex)
            {
                string response = ex.Message;
                if (response.Contains("2148916227"))
                    throw new MsAuthException("该账号已被微软封禁，无法登录");
                if (response.Contains("2148916233"))
                    throw new MsAuthXboxNotRegisteredException("尚未注册 Xbox 账户");
                if (response.Contains("2148916235"))
                    throw new MsAuthRegionBlockedException("你所在国家或地区无法登录微软账号，请使用 VPN 或加速器");
                if (response.Contains("2148916238"))
                    throw new MsAuthUnderageException("账号年龄不足，需要修改出生日期");
                throw;
            }
        }

        #endregion

        #region 步骤 4：XSTS + UHS → Minecraft AccessToken

        private static async Task<(string mcAccessToken, long expiresAt)> Step4_GetMcAccessTokenAsync(
            string xstsToken, string uhs)
        {
            string url = "https://api.minecraftservices.com/authentication/login_with_xbox";

            string requestBody = new JObject
            {
                ["identityToken"] = $"XBL3.0 x={uhs};{xstsToken}"
            }.ToString(Formatting.None);

            try
            {
                string result = await PostJsonAsync(url, requestBody);
                JObject json = JObject.Parse(result);

                string mcAccessToken = json["access_token"].ToString();
                int expiresIn = json["expires_in"].ToObject<int>();
                long expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn - 1200;

                return (mcAccessToken, expiresAt);
            }
            catch (HttpRequestException ex)
            {
                string response = ex.Message;
                if (response.Contains("ACCOUNT_SUSPENDED"))
                    throw new MsAuthException("该账号已被封禁，无法登录");
                if (response.Contains("429"))
                    throw new MsAuthRateLimitException("登录尝试太过频繁，请等待几分钟后再试");
                if (response.Contains("403") || response.Contains("Forbidden"))
                    throw new MsAuthException("当前 IP 登录异常。如使用 VPN 或加速器，请关闭或更换节点后再试");
                throw;
            }
        }

        #endregion

        #region 步骤 5：检查正版授权

        private static async Task Step5_CheckOwnershipAsync(string mcAccessToken)
        {
            string url = "https://api.minecraftservices.com/entitlements/mcstore";

            try
            {
                string result = await GetJsonAsync(url,
                    new Dictionary<string, string> { { "Authorization", "Bearer " + mcAccessToken } });

                JObject json = JObject.Parse(result);
                if (!json.ContainsKey("items") || json["items"] == null || !json["items"].HasValues)
                {
                    throw new MsAuthNotOwnedException("你尚未购买正版 Minecraft，或 Xbox Game Pass 已到期");
                }
            }
            catch (MsAuthException) { throw; }
            catch (Exception ex)
            {
                throw new MsAuthException("检查正版授权时出错", ex);
            }
        }

        #endregion

        #region 步骤 6：获取玩家档案

        private static async Task<MsProfile> Step6_GetProfileAsync(string mcAccessToken)
        {
            string url = "https://api.minecraftservices.com/minecraft/profile";

            try
            {
                string result = await GetJsonAsync(url,
                    new Dictionary<string, string> { { "Authorization", "Bearer " + mcAccessToken } });

                JObject json = JObject.Parse(result);
                return new MsProfile
                {
                    Uuid = json["id"].ToString(),
                    UserName = json["name"].ToString(),
                    ProfileJson = result
                };
            }
            catch (HttpRequestException ex)
            {
                string response = ex.Message;
                if (response.Contains("429"))
                    throw new MsAuthRateLimitException("登录尝试太过频繁，请等待几分钟后再试");
                if (response.Contains("404") || response.Contains("NotFound"))
                    throw new MsAuthNoProfileException("尚未创建 Minecraft 玩家档案");
                throw;
            }
        }

        #endregion

        #region 网络请求辅助方法

        private static async Task<string> PostFormUrlEncodedAsync(string url, string body)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
                var response = await client.PostAsync(url, content);
                return await EnsureSuccessAsync(response);
            }
        }

        private static async Task<string> PostFormUrlEncodedWithHeadersAsync(
            string url, string body, Dictionary<string, string> headers)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                foreach (var h in headers)
                    client.DefaultRequestHeaders.Add(h.Key, h.Value);
                var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
                var response = await client.PostAsync(url, content);
                return await EnsureSuccessAsync(response);
            }
        }

        private static async Task<string> PostJsonAsync(string url, string jsonBody)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                return await EnsureSuccessAsync(response);
            }
        }

        private static async Task<string> GetJsonAsync(string url, Dictionary<string, string> headers)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                foreach (var h in headers)
                    client.DefaultRequestHeaders.Add(h.Key, h.Value);
                var response = await client.GetAsync(url);
                return await EnsureSuccessAsync(response);
            }
        }

        /// <summary>
        /// 检查响应状态码，非 2xx 时读取错误正文并抛出带详细信息的异常
        /// </summary>
        private static async Task<string> EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"[{(int)response.StatusCode}] {response.ReasonPhrase} — {errorBody}");
            }
            return await response.Content.ReadAsStringAsync();
        }

        #endregion
    }

    #region 数据模型

    public class MsProfile
    {
        public string Uuid { get; set; }
        public string UserName { get; set; }
        public string ProfileJson { get; set; }
    }

    public class MsLoginResult
    {
        public string OAuthRefreshToken { get; set; }
        public string OAuthAccessToken { get; set; }
        public string McAccessToken { get; set; }
        public long McExpiresAt { get; set; }
        public string Uuid { get; set; }
        public string UserName { get; set; }
        public string ProfileJson { get; set; }
    }

    #endregion

    #region 异常类型

    public class MsAuthException : Exception
    {
        public MsAuthException(string message) : base(message) { }
        public MsAuthException(string message, Exception inner) : base(message, inner) { }
    }

    public class MsAuthReloginRequiredException : MsAuthException
    {
        public MsAuthReloginRequiredException(string message) : base(message) { }
    }

    public class MsAuthUserCancelledException : MsAuthException
    {
        public MsAuthUserCancelledException(string message) : base(message) { }
    }

    public class MsAuthXboxNotRegisteredException : MsAuthException
    {
        public MsAuthXboxNotRegisteredException(string message) : base(message) { }
    }

    public class MsAuthRegionBlockedException : MsAuthException
    {
        public MsAuthRegionBlockedException(string message) : base(message) { }
    }

    public class MsAuthUnderageException : MsAuthException
    {
        public MsAuthUnderageException(string message) : base(message) { }
    }

    public class MsAuthRateLimitException : MsAuthException
    {
        public MsAuthRateLimitException(string message) : base(message) { }
    }

    public class MsAuthNotOwnedException : MsAuthException
    {
        public MsAuthNotOwnedException(string message) : base(message) { }
    }

    public class MsAuthNoProfileException : MsAuthException
    {
        public MsAuthNoProfileException(string message) : base(message) { }
    }

    #endregion
}
