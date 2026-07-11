using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MusicalNoteLauncher.Models;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 音乐平台账号服务 —— 管理网易云音乐账号的登录与持久化
    /// </summary>
    public class MusicAccountService
    {
        private static MusicAccountService? _instance;
        public static MusicAccountService Instance => _instance ??= new MusicAccountService();

        private readonly MusicApiService _api;
        private MusicAccountInfo? _account;
        private readonly string _stateFilePath;

        public MusicAccountInfo? CurrentAccount
        {
            get => _account;
            private set
            {
                _account = value;
                AccountChanged?.Invoke(value);
            }
        }

        public event Action<MusicAccountInfo?>? AccountChanged;

        private MusicAccountService()
        {
            _api = new MusicApiService();
            _stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MNL", "music_account.json");
            LoadAccount();
        }

        /// <summary>从文件加载已保存的账号</summary>
        private void LoadAccount()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    string json = File.ReadAllText(_stateFilePath);
                    CurrentAccount = JsonSerializer.Deserialize<MusicAccountInfo>(json);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicAccount] 加载账号失败: {ex.Message}");
            }
        }

        /// <summary>保存账号到文件</summary>
        private void SaveAccount()
        {
            try
            {
                if (CurrentAccount == null)
                {
                    if (File.Exists(_stateFilePath))
                        File.Delete(_stateFilePath);
                    return;
                }
                string json = JsonSerializer.Serialize(CurrentAccount, new JsonSerializerOptions { WriteIndented = true });
                string dir = Path.GetDirectoryName(_stateFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Error($"[MusicAccount] 保存账号失败: {ex.Message}");
            }
        }

        /// <summary>尝试用 Cookie 恢复登录状态</summary>
        public async Task<bool> TryRestoreSessionAsync()
        {
            if (CurrentAccount == null || string.IsNullOrEmpty(CurrentAccount.Cookie))
                return false;

            var account = await _api.GetLoginStatusAsync(CurrentAccount.Cookie);
            if (account != null && !string.IsNullOrEmpty(account.UserId))
            {
                account.Cookie = CurrentAccount.Cookie;
                CurrentAccount = account;
                SaveAccount();
                Logger.Info("[MusicAccount] Cookie 登录恢复成功");
                return true;
            }

            Logger.Info("[MusicAccount] Cookie 已过期");
            return false;
        }

        /// <summary>获取二维码登录 Key</summary>
        public async Task<string?> GetQrKeyAsync()
        {
            return await _api.GetQrKeyAsync();
        }

        /// <summary>生成二维码图片 Base64</summary>
        public async Task<string?> CreateQrImageAsync(string key)
        {
            return await _api.CreateQrImageAsync(key);
        }

        /// <summary>检查二维码扫码状态</summary>
        public async Task<(int Code, string? Cookie)> CheckQrStatusAsync(string key)
        {
            return await _api.CheckQrStatusAsync(key);
        }

        /// <summary>使用 Cookie 完成登录</summary>
        public async Task<bool> LoginWithCookieAsync(string cookie)
        {
            var account = await _api.GetLoginStatusAsync(cookie);
            if (account == null || string.IsNullOrEmpty(account.UserId))
                return false;

            account.Cookie = cookie;
            account.LoginTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            CurrentAccount = account;
            SaveAccount();
            Logger.Info($"[MusicAccount] 登录成功: {account.Nickname}");
            return true;
        }

        /// <summary>退出登录</summary>
        public void Logout()
        {
            CurrentAccount = null;
            SaveAccount();
            Logger.Info("[MusicAccount] 已退出登录");
        }
    }
}
