using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 聊天端到端加密服务，使用 RSA+AES-GCM 混合加密。
    /// 参考: https://github.com/minhphan46/chat_app_signalr (Crypto branch)
    ///       https://github.com/TGrigor/securemessaging-diploma
    /// </summary>
    public static class ChatCryptoService
    {
        private const int AesKeySize = 256;
        private const int AesNonceSize = 12; // GCM nonce = 12 bytes
        private const int AesTagSize = 16;   // GCM tag = 16 bytes

        // ======== RSA 密钥管理 ========

        /// <summary>生成 RSA 4096位 密钥对，返回 (公钥XML, 私钥XML)</summary>
        public static (string publicKey, string privateKey) GenerateRsaKeyPair()
        {
            using var rsa = RSA.Create(4096);
            return (rsa.ToXmlString(false), rsa.ToXmlString(true));
        }

        /// <summary>用 RSA 公钥加密数据</summary>
        public static byte[] RsaEncrypt(byte[] data, string publicKeyXml)
        {
            using var rsa = RSA.Create();
            rsa.FromXmlString(publicKeyXml);
            return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        /// <summary>用 RSA 私钥解密数据</summary>
        public static byte[] RsaDecrypt(byte[] data, string privateKeyXml)
        {
            using var rsa = RSA.Create();
            rsa.FromXmlString(privateKeyXml);
            return rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        // ======== AES-256-GCM 加密/解密 ========

        /// <summary>用 AES-256-GCM 加密明文，返回 (nonce + tag + ciphertext) 拼接</summary>
        public static byte[] AesGcmEncrypt(string plaintext, byte[] key)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] nonce = new byte[AesNonceSize];
            byte[] tag = new byte[AesTagSize];
            byte[] ciphertext = new byte[plainBytes.Length];

            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(key, AesTagSize);
            aes.Encrypt(nonce, plainBytes, ciphertext, tag);

            // 返回: nonce(12) + tag(16) + ciphertext
            byte[] result = new byte[AesNonceSize + AesTagSize + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, AesNonceSize);
            Buffer.BlockCopy(tag, 0, result, AesNonceSize, AesTagSize);
            Buffer.BlockCopy(ciphertext, 0, result, AesNonceSize + AesTagSize, ciphertext.Length);
            return result;
        }

        /// <summary>用 AES-256-GCM 解密 (nonce + tag + ciphertext)，返回明文</summary>
        public static string AesGcmDecrypt(byte[] encryptedData, byte[] key)
        {
            int nonceSize = AesNonceSize;
            int tagSize = AesTagSize;
            int cipherSize = encryptedData.Length - nonceSize - tagSize;

            byte[] nonce = new byte[nonceSize];
            byte[] tag = new byte[tagSize];
            byte[] ciphertext = new byte[cipherSize];
            byte[] plaintext = new byte[cipherSize];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonceSize);
            Buffer.BlockCopy(encryptedData, nonceSize, tag, 0, tagSize);
            Buffer.BlockCopy(encryptedData, nonceSize + tagSize, ciphertext, 0, cipherSize);

            using var aes = new AesGcm(key, tagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }

        // ======== 混合加密（对外接口） ========

        /// <summary>
        /// 端到端加密一条消息。
        /// 流程: 生成随机 AES-256 key → AES-GCM 加密明文 → RSA(对方公钥) 加密 AES key → 打包
        /// </summary>
        /// <returns>Base64 编码的加密包 JSON</returns>
        public static string EncryptMessage(string plaintext, string recipientPublicKeyXml)
        {
            // 1. 生成随机 AES-256 密钥
            byte[] aesKey = new byte[AesKeySize / 8];
            RandomNumberGenerator.Fill(aesKey);

            // 2. AES-GCM 加密明文
            byte[] encryptedContent = AesGcmEncrypt(plaintext, aesKey);

            // 3. RSA 加密 AES 密钥
            byte[] encryptedKey = RsaEncrypt(aesKey, recipientPublicKeyXml);

            // 4. 打包
            var package = new
            {
                key = Convert.ToBase64String(encryptedKey),
                content = Convert.ToBase64String(encryptedContent),
                version = 1
            };
            return JsonSerializer.Serialize(package);
        }

        /// <summary>
        /// 端到端解密一条消息。
        /// 流程: 解包 → RSA(自己私钥) 解密 AES key → AES-GCM 解密密文
        /// </summary>
        /// <param name="encryptedPackageJson">EncryptMessage 返回的 JSON</param>
        /// <param name="myPrivateKeyXml">自己的 RSA 私钥</param>
        /// <returns>明文</returns>
        public static string DecryptMessage(string encryptedPackageJson, string myPrivateKeyXml)
        {
            using var doc = JsonDocument.Parse(encryptedPackageJson);
            var root = doc.RootElement;

            byte[] encryptedKey = Convert.FromBase64String(root.GetProperty("key").GetString()!);
            byte[] encryptedContent = Convert.FromBase64String(root.GetProperty("content").GetString()!);

            // 1. RSA 解密 AES 密钥
            byte[] aesKey = RsaDecrypt(encryptedKey, myPrivateKeyXml);

            // 2. AES-GCM 解密密文
            return AesGcmDecrypt(encryptedContent, aesKey);
        }
    }
}
