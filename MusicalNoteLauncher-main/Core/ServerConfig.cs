using System;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// MNL 测试服务器配置，集中管理所有后端接口地址。
    /// </summary>
    public static class ServerConfig
    {
        /// <summary>测试服务器根地址（部署时改为实际地址）</summary>
        public const string BaseUrl = "http://192.168.100.106:8080";

        /// <summary>版本查询接口</summary>
        public static string VersionApiUrl => $"{BaseUrl}/api/version.php";

        /// <summary>日志上报接口</summary>
        public static string LogApiUrl => $"{BaseUrl}/api/log.php";

        /// <summary>白名单验证接口</summary>
        public static string WhitelistApiUrl => $"{BaseUrl}/api/whitelist.php";

        /// <summary>好友系统接口</summary>
        public static string FriendsApiUrl => $"{BaseUrl}/api/friends.php";

        /// <summary>组件商店接口</summary>
        public static string ComponentsApiUrl => $"{BaseUrl}/api/components.php";

        /// <summary>背景素材库接口</summary>
        public static string BackgroundsApiUrl => $"{BaseUrl}/api/backgrounds.php";
    }
}
