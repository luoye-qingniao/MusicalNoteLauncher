using System;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// MNL 测试服务器配置，集中管理所有后端接口地址。
    /// </summary>
    public static class ServerConfig
    {
        /// <summary>MNL 官方服务器地址</summary>
        public const string BaseUrl = "http://85.137.246.87";

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

        /// <summary>青鸟账号认证接口</summary>
        public static string AuthApiUrl => $"{BaseUrl}/api/auth.php";

        /// <summary>聊天社区接口</summary>
        public static string CommunityApiUrl => $"{BaseUrl}/api/community.php";
    }
}
