using System;
using System.Security.Cryptography;
using System.Text;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 基于 Windows DPAPI（Data Protection API）的本地安全存储。
    /// 使用当前用户范围加密，绑定当前 Windows 账号。
    /// </summary>
    public static class SecureStorage
    {
        private const string EncryptionPrefix = "DPAPI:";

        /// <summary>加密明文，返回 Base64 字符串（带 DPAPI: 前缀）</summary>
        public static string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return plaintext;

            byte[] bytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return EncryptionPrefix + Convert.ToBase64String(protectedBytes);
        }

        /// <summary>解密 Base64 字符串。如果以 DPAPI: 前缀开头则解密，否则当作明文返回（兼容旧数据）</summary>
        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return stored;

            // 兼容旧版明文存储
            if (!stored.StartsWith(EncryptionPrefix, StringComparison.Ordinal))
                return stored;

            string base64 = stored.Substring(EncryptionPrefix.Length);
            try
            {
                byte[] protectedBytes = Convert.FromBase64String(base64);
                byte[] bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // DPAPI 解密失败（例如跨机器迁移数据），返回空
                return "";
            }
        }
    }
}
