using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PCL.Account
{
    public enum McLoginType
    {
        Legacy,
        Ms,
        Nide,
        Auth
    }

    public class McLoginMs
    {
        public string OAuthRefreshToken { get; set; }
        public string UserName { get; set; }
        public string AccessToken { get; set; }
        public string Uuid { get; set; }
        public string ProfileJson { get; set; }
    }

    public class McLoginServer
    {
        public McLoginType Type { get; set; }
        public string Token { get; set; }
        public string BaseUrl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Description { get; set; }
        public bool ForceReselectProfile { get; set; }
    }

    public class McLoginLegacy
    {
        public string UserName { get; set; }
        public int SkinType { get; set; }
        public string SkinName { get; set; }
    }

    public class McLoginResult
    {
        public string Name { get; set; }
        public string Uuid { get; set; }
        public string AccessToken { get; set; }
        public string ClientToken { get; set; }
        public string Type { get; set; }
        public string ProfileJson { get; set; }
        public long ExpiresAt { get; set; }
    }

    public static class AccountManager
    {
        #region 微软账户管理

        public static void SaveMsLogin(string oAuthRefreshToken, McLoginResult result, long expiresAt, string oldUserName = null)
        {
            Settings.Set("CacheMsV2OAuthRefresh", oAuthRefreshToken);
            Settings.Set("CacheMsV2Access", result.AccessToken);
            Settings.Set("CacheMsV2Uuid", result.Uuid);
            Settings.Set("CacheMsV2Name", result.Name);
            Settings.Set("CacheMsV2ProfileJson", result.ProfileJson);
            Settings.Set("CacheMsV2Expires", expiresAt);

            JObject msJson;
            try
            {
                msJson = JObject.Parse(Settings.Get<string>("LoginMsJson") ?? "{}");
            }
            catch
            {
                msJson = new JObject();
            }

            msJson.Remove(oldUserName ?? result.Name);
            msJson[result.Name] = oAuthRefreshToken;
            Settings.Set("LoginMsJson", msJson.ToString(Formatting.None));
        }

        public static McLoginResult LoadCachedMsLogin(string userName)
        {
            string cacheName = Settings.Get<string>("CacheMsV2Name");
            long expiresAt = Settings.Get<long>("CacheMsV2Expires");

            if (!string.IsNullOrEmpty(cacheName) &&
                expiresAt > 0 &&
                expiresAt > GetUnixTimestampUtc() &&
                userName == cacheName)
            {
                return new McLoginResult
                {
                    Name = userName,
                    Type = "Microsoft",
                    AccessToken = Settings.Get<string>("CacheMsV2Access"),
                    Uuid = Settings.Get<string>("CacheMsV2Uuid"),
                    ClientToken = Settings.Get<string>("CacheMsV2Uuid"),
                    ProfileJson = Settings.Get<string>("CacheMsV2ProfileJson")
                };
            }
            return null;
        }

        public static List<McLoginMs> GetMsAccounts()
        {
            var accounts = new List<McLoginMs>();
            try
            {
                JObject msJson = JObject.Parse(Settings.Get<string>("LoginMsJson") ?? "{}");
                foreach (var account in msJson)
                {
                    accounts.Add(new McLoginMs
                    {
                        UserName = account.Key,
                        OAuthRefreshToken = account.Value.ToString()
                    });
                }
            }
            catch
            {
                Settings.Set("LoginMsJson", "{}");
            }
            return accounts;
        }

        public static McLoginMs GetLatestMsLogin()
        {
            return new McLoginMs
            {
                OAuthRefreshToken = Settings.Get<string>("CacheMsV2OAuthRefresh"),
                UserName = Settings.Get<string>("CacheMsV2Name")
            };
        }

        public static void RemoveMsAccount(string userName)
        {
            try
            {
                JObject msJson = JObject.Parse(Settings.Get<string>("LoginMsJson") ?? "{}");
                msJson.Remove(userName);
                Settings.Set("LoginMsJson", msJson.ToString(Formatting.None));
            }
            catch
            {
                Settings.Set("LoginMsJson", "{}");
            }
        }

        public static void ExitMsLogin()
        {
            Settings.Set("CacheMsV2OAuthRefresh", "");
            Settings.Set("CacheMsV2Access", "");
            Settings.Set("CacheMsV2ProfileJson", "");
            Settings.Set("CacheMsV2Uuid", "");
            Settings.Set("CacheMsV2Name", "");
            Settings.Set("CacheMsV2Expires", 0L);
        }

        public static void MigrateMsV2IfNeeded()
        {
            if (!Settings.Get<bool>("CacheMsV2Migrated"))
            {
                Settings.Set("CacheMsV2Migrated", true);
                Settings.Set("CacheMsV2OAuthRefresh", Settings.Get<string>("CacheMsOAuthRefresh"));
                Settings.Set("CacheMsV2Access", Settings.Get<string>("CacheMsAccess"));
                Settings.Set("CacheMsV2ProfileJson", Settings.Get<string>("CacheMsProfileJson"));
                Settings.Set("CacheMsV2Uuid", Settings.Get<string>("CacheMsUuid"));
                Settings.Set("CacheMsV2Name", Settings.Get<string>("CacheMsName"));
            }
        }

        #endregion

        #region 离线账户管理

        public static void SaveLegacyLogin(string userName)
        {
            List<string> names = new List<string>();
            string raw = Settings.Get<string>("LoginLegacyName");
            if (!string.IsNullOrEmpty(raw))
                names.AddRange(raw.Split('¨'));

            names.Remove(userName);
            names.Insert(0, userName);
            Settings.Set("LoginLegacyName", string.Join("¨", names));
        }

        public static List<string> GetLegacyAccounts()
        {
            string raw = Settings.Get<string>("LoginLegacyName");
            if (string.IsNullOrEmpty(raw))
                return new List<string>();
            return raw.Split('¨').ToList();
        }

        public static void RemoveLegacyAccount(string userName)
        {
            List<string> names = new List<string>();
            string raw = Settings.Get<string>("LoginLegacyName");
            if (!string.IsNullOrEmpty(raw))
                names.AddRange(raw.Split('¨'));

            names.Remove(userName);
            Settings.Set("LoginLegacyName", string.Join("¨", names));
        }

        #endregion

        #region 服务端账户管理

        public static void SaveServerLogin(string token, McLoginResult result, string userName, string password)
        {
            Settings.Set("Cache" + token + "Access", result.AccessToken);
            Settings.Set("Cache" + token + "Client", result.ClientToken);
            Settings.Set("Cache" + token + "Uuid", result.Uuid);
            Settings.Set("Cache" + token + "Name", result.Name);
            Settings.Set("Cache" + token + "Username", userName);
            Settings.Set("Cache" + token + "Pass", password);
        }

        public static bool IsServerCacheValid(string token, string userName, string password)
        {
            return Settings.Get<string>("Cache" + token + "Username") == userName &&
                   Settings.Get<string>("Cache" + token + "Pass") == password &&
                   !string.IsNullOrEmpty(Settings.Get<string>("Cache" + token + "Access")) &&
                   !string.IsNullOrEmpty(Settings.Get<string>("Cache" + token + "Client")) &&
                   !string.IsNullOrEmpty(Settings.Get<string>("Cache" + token + "Uuid")) &&
                   !string.IsNullOrEmpty(Settings.Get<string>("Cache" + token + "Name"));
        }

        public static McLoginResult LoadCachedServerLogin(string token)
        {
            return new McLoginResult
            {
                AccessToken = Settings.Get<string>("Cache" + token + "Access"),
                ClientToken = Settings.Get<string>("Cache" + token + "Client"),
                Uuid = Settings.Get<string>("Cache" + token + "Uuid"),
                Name = Settings.Get<string>("Cache" + token + "Name"),
                Type = token
            };
        }

        public static void SaveServerLoginRecord(string token, string userName, string password)
        {
            try
            {
                Dictionary<string, string> dict = new Dictionary<string, string>();

                string emailsRaw = Settings.Get<string>("Login" + token + "Email");
                string passesRaw = Settings.Get<string>("Login" + token + "Pass");

                if (!string.IsNullOrEmpty(emailsRaw) && !string.IsNullOrEmpty(passesRaw))
                {
                    string[] emails = emailsRaw.Split('¨');
                    string[] passes = passesRaw.Split('¨');
                    for (int i = 0; i < Math.Min(emails.Length, passes.Length); i++)
                    {
                        if (!dict.ContainsKey(emails[i]))
                            dict[emails[i]] = passes[i];
                    }
                }

                dict.Remove(userName);
                dict = new Dictionary<string, string> { { userName, password } }
                    .Concat(dict.Where(kv => kv.Key != userName))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                Settings.Set("Login" + token + "Email", string.Join("¨", dict.Keys));
                Settings.Set("Login" + token + "Pass", string.Join("¨", dict.Values));
            }
            catch
            {
                Settings.Set("Login" + token + "Email", "");
                Settings.Set("Login" + token + "Pass", "");
            }
        }

        public static List<Tuple<string, string>> GetServerLoginRecords(string token)
        {
            var records = new List<Tuple<string, string>>();
            string emailsRaw = Settings.Get<string>("Login" + token + "Email");
            string passesRaw = Settings.Get<string>("Login" + token + "Pass");

            if (!string.IsNullOrEmpty(emailsRaw) && !string.IsNullOrEmpty(passesRaw))
            {
                string[] emails = emailsRaw.Split('¨');
                string[] passes = passesRaw.Split('¨');
                for (int i = 0; i < Math.Min(emails.Length, passes.Length); i++)
                {
                    records.Add(Tuple.Create(emails[i], passes[i]));
                }
            }
            return records;
        }

        public static void RemoveServerAccount(string token, string userName)
        {
            try
            {
                Dictionary<string, string> dict = new Dictionary<string, string>();
                string emailsRaw = Settings.Get<string>("Login" + token + "Email");
                string passesRaw = Settings.Get<string>("Login" + token + "Pass");

                if (!string.IsNullOrEmpty(emailsRaw) && !string.IsNullOrEmpty(passesRaw))
                {
                    string[] emails = emailsRaw.Split('¨');
                    string[] passes = passesRaw.Split('¨');
                    for (int i = 0; i < Math.Min(emails.Length, passes.Length); i++)
                    {
                        if (!dict.ContainsKey(emails[i]))
                            dict[emails[i]] = passes[i];
                    }
                }

                dict.Remove(userName);
                Settings.Set("Login" + token + "Email", string.Join("¨", dict.Keys));
                Settings.Set("Login" + token + "Pass", string.Join("¨", dict.Values));
            }
            catch
            {
                Settings.Set("Login" + token + "Email", "");
                Settings.Set("Login" + token + "Pass", "");
            }
        }

        #endregion

        #region Nide 专用账户管理

        public static object McInstanceSelected { get; set; }

        public static string GetNideServer()
        {
            return McInstanceSelected != null
                ? Settings.Get<string>("VersionServerNide", McInstanceSelected)
                : Settings.Get<string>("CacheNideServer");
        }

        public static McLoginServer GetNideLoginData(string userName = null, string password = null)
        {
            string server = GetNideServer();
            return new McLoginServer
            {
                Token = "Nide",
                UserName = userName ?? Settings.Get<string>("CacheNideUsername"),
                Password = password ?? Settings.Get<string>("CacheNidePass"),
                Description = "统一通行证",
                Type = McLoginType.Nide,
                BaseUrl = $"https://auth.mc-user.com:233/{server}/authserver"
            };
        }

        public static void ClearNideAccessOnInputChange()
        {
            Settings.Set("CacheNideAccess", "");
        }

        public static void ExitNideLogin()
        {
            Settings.Set("CacheNideAccess", "");
        }

        #endregion

        #region Authlib-Injector 专用账户管理

        public static string GetAuthServer()
        {
            return McInstanceSelected != null
                ? Settings.Get<string>("VersionServerAuthServer", McInstanceSelected)
                : Settings.Get<string>("CacheAuthServerServer");
        }

        public static McLoginServer GetAuthLoginData(string userName = null, string password = null)
        {
            string server = GetAuthServer();
            return new McLoginServer
            {
                Token = "Auth",
                BaseUrl = $"{server}/authserver",
                UserName = userName ?? Settings.Get<string>("CacheAuthUsername"),
                Password = password ?? Settings.Get<string>("CacheAuthPass"),
                Description = "Authlib-Injector",
                Type = McLoginType.Auth
            };
        }

        public static void ClearAuthAccessOnInputChange()
        {
            Settings.Set("CacheAuthAccess", "");
        }

        public static void ClearAuthProfileCache()
        {
            Settings.Set("CacheAuthUuid", "");
            Settings.Set("CacheAuthName", "");
        }

        public static void ExitAuthLogin()
        {
            Settings.Set("CacheAuthAccess", "");
            Settings.Set("CacheAuthUuid", "");
            Settings.Set("CacheAuthName", "");
        }

        #endregion

        #region 通用账户管理

        public static bool IsRememberPasswordEnabled()
        {
            return Settings.Get<bool>("LoginRemember");
        }

        public static void SetRememberPassword(bool enabled)
        {
            Settings.Set("LoginRemember", enabled);
        }

        public static McLoginType GetLoginType()
        {
            string raw = Settings.Get<string>("LoginType");
            if (Enum.TryParse(raw, out McLoginType type))
                return type;
            return McLoginType.Legacy;
        }

        public static void SetLoginType(McLoginType type)
        {
            Settings.Set("LoginType", type.ToString());
        }

        public static void RemoveAccount(string loginType, string userName)
        {
            switch (loginType)
            {
                case "Ms":
                    RemoveMsAccount(userName);
                    break;
                case "Legacy":
                    RemoveLegacyAccount(userName);
                    break;
                case "Nide":
                case "Auth":
                    RemoveServerAccount(loginType, userName);
                    break;
            }
        }

        public static string ValidateLoginData(object loginData)
        {
            if (loginData is McLoginMs ms)
            {
                if (string.IsNullOrEmpty(ms.OAuthRefreshToken))
                    return "请在登录账号后再启动游戏！";
            }
            else if (loginData is McLoginServer server)
            {
                if (string.IsNullOrEmpty(server.UserName))
                    return "账号不能为空！";
                if (string.IsNullOrEmpty(server.Password))
                    return "密码不能为空！";
            }
            return "";
        }

        #endregion

        #region 辅助方法

        private static long GetUnixTimestampUtc()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        #endregion
    }
}
