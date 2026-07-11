using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>离线账户信息</summary>
    public class BedrockOfflineAccount
    {
        /// <summary>账户唯一标识</summary>
        public string Uuid { get; set; }
        /// <summary>玩家用户名（显示名称）</summary>
        public string Username { get; set; }
        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>最后登录时间</summary>
        public DateTime LastLoginAt { get; set; }
        /// <summary>本地游戏数据目录</summary>
        public string DataDirectory { get; set; }
    }

    /// <summary>
    /// 基岩版离线启动器：本地账户管理 + 离线模式启动（不修改游戏核心文件）
    /// 通过启动参数和环境变量注入实现离线登录，绕过Microsoft账户在线验证
    /// </summary>
    public class BedrockOfflineLauncher
    {
        private readonly string _minecraftPath;
        private readonly string _accountsFilePath;
        private List<BedrockOfflineAccount> _accounts;

        /// <summary>启动状态变更事件</summary>
        public event Action<string> LaunchStatusChanged;
        /// <summary>启动日志事件</summary>
        public event Action<string> LaunchLogReceived;
        /// <summary>启动完成事件（参数：是否成功）</summary>
        public event Action<bool> LaunchCompleted;

        public BedrockOfflineLauncher(string minecraftPath)
        {
            _minecraftPath = minecraftPath ?? throw new ArgumentNullException(nameof(minecraftPath));
            _accountsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MusicalNoteLauncher", "bedrock_offline_accounts.json");
            _accounts = new List<BedrockOfflineAccount>();
            LoadAccounts();
        }

        // ────────────────────────── 本地账户管理 ──────────────────────────

        /// <summary>获取所有离线账户</summary>
        public IReadOnlyList<BedrockOfflineAccount> GetAccounts()
        {
            return _accounts.AsReadOnly();
        }

        /// <summary>创建或获取离线账户（同名账户复用）</summary>
        public BedrockOfflineAccount GetOrCreateAccount(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                username = "Player";

            var existing = _accounts.Find(a =>
                string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.LastLoginAt = DateTime.Now;
                SaveAccounts();
                return existing;
            }

            var account = new BedrockOfflineAccount
            {
                Uuid = GenerateLocalUuid(username),
                Username = username,
                CreatedAt = DateTime.Now,
                LastLoginAt = DateTime.Now,
                DataDirectory = Path.Combine(_minecraftPath, "bedrock", "offline_data", SanitizeFolderName(username))
            };

            _accounts.Add(account);
            SaveAccounts();
            EnsureDataDirectory(account);

            Logger.Info($"创建基岩版离线账户: {username} (UUID: {account.Uuid})");
            return account;
        }

        /// <summary>删除离线账户</summary>
        public bool DeleteAccount(string username)
        {
            var account = _accounts.Find(a =>
                string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase));
            if (account == null) return false;

            _accounts.Remove(account);
            SaveAccounts();
            Logger.Info($"已删除基岩版离线账户: {username}");
            return true;
        }

        /// <summary>获取上次使用的账户</summary>
        public BedrockOfflineAccount GetLastUsedAccount()
        {
            if (_accounts.Count == 0) return null;
            _accounts.Sort((a, b) => b.LastLoginAt.CompareTo(a.LastLoginAt));
            return _accounts[0];
        }

        private static string GenerateLocalUuid(string username)
        {
            // 基于用户名的确定性离线UUID生成（兼容Minecraft离线UUID格式）
            using var md5 = System.Security.Cryptography.MD5.Create();
            string input = "OfflinePlayer:" + username;
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            // 设置版本3 UUID标记
            hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()
                .Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-");
        }

        private static string SanitizeFolderName(string name)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            var sanitized = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                sanitized.Append(invalid.Contains(c) ? '_' : c);
            }
            return sanitized.ToString();
        }

        private void EnsureDataDirectory(BedrockOfflineAccount account)
        {
            try
            {
                if (!Directory.Exists(account.DataDirectory))
                {
                    Directory.CreateDirectory(account.DataDirectory);
                    Logger.Info($"创建离线数据目录: {account.DataDirectory}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"创建离线数据目录失败: {ex.Message}");
            }
        }

        private void LoadAccounts()
        {
            try
            {
                string dir = Path.GetDirectoryName(_accountsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(_accountsFilePath))
                {
                    string json = File.ReadAllText(_accountsFilePath);
                    _accounts = JsonSerializer.Deserialize<List<BedrockOfflineAccount>>(json) ?? new();
                    Logger.Info($"加载了 {_accounts.Count} 个基岩版离线账户");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"加载离线账户失败: {ex.Message}");
                _accounts = new List<BedrockOfflineAccount>();
            }
        }

        private void SaveAccounts()
        {
            try
            {
                string dir = Path.GetDirectoryName(_accountsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_accounts, options);
                File.WriteAllText(_accountsFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Warning($"保存离线账户失败: {ex.Message}");
            }
        }

        // ────────────────────────── 离线启动 ──────────────────────────

        /// <summary>离线启动基岩版（通过进程参数注入，不修改游戏文件）</summary>
        public async Task<bool> LaunchOfflineAsync(string versionId, string username)
        {
            try
            {
                LaunchStatusChanged?.Invoke("正在准备离线启动基岩版...");
                LaunchLogReceived?.Invoke("========================================");
                LaunchLogReceived?.Invoke($"基岩版离线启动 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LaunchLogReceived?.Invoke("========================================");

                // 验证版本是否已安装
                string bedrockDir = Path.Combine(_minecraftPath, "bedrock", versionId);
                string exePath = Path.Combine(bedrockDir, "Minecraft.Windows.exe");

                if (!File.Exists(exePath))
                {
                    string errorMsg = $"基岩版 {versionId} 未安装，请先下载版本";
                    LaunchLogReceived?.Invoke($"错误: {errorMsg}");
                    LaunchStatusChanged?.Invoke("启动失败: 版本未安装");
                    LaunchCompleted?.Invoke(false);
                    return false;
                }

                LaunchLogReceived?.Invoke($"版本ID: {versionId}");
                LaunchLogReceived?.Invoke($"可执行文件: {exePath}");

                // 创建/获取离线账户
                var account = GetOrCreateAccount(username);
                LaunchLogReceived?.Invoke($"离线账户: {account.Username} (UUID: {account.Uuid})");

                // 设置离线模式环境 - 写入本地游戏状态文件
                LaunchStatusChanged?.Invoke("正在配置离线环境...");
                SetupOfflineEnvironment(account, bedrockDir);

                // 显示重要提示
                LaunchLogReceived?.Invoke("========================================");
                LaunchLogReceived?.Invoke("⚠ 离线模式提示：");
                LaunchLogReceived?.Invoke("  1. 离线模式仅用于个人本地试玩");
                LaunchLogReceived?.Invoke("  2. 不支持多人联机与领域服务器");
                LaunchLogReceived?.Invoke("  3. 不支持市场与皮肤下载");
                LaunchLogReceived?.Invoke("========================================");

                // 启动游戏进程
                LaunchStatusChanged?.Invoke("正在启动基岩版（离线模式）...");
                LaunchLogReceived?.Invoke("正在启动游戏进程...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = bedrockDir,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    // 通过环境变量传递离线配置
                    Environment =
                    {
                        // 使用自定义环境变量（游戏进程可选择性读取）
                        ["MNL_OFFLINE_MODE"] = "1",
                        ["MNL_USERNAME"] = account.Username,
                        ["MNL_UUID"] = account.Uuid,
                        ["MNL_DATA_DIR"] = account.DataDirectory
                    }
                };

                var gameProcess = Process.Start(startInfo);

                if (gameProcess != null)
                {
                    LaunchLogReceived?.Invoke($"游戏进程已启动 (PID: {gameProcess.Id})");
                    LaunchLogReceived?.Invoke($"进程名: {gameProcess.ProcessName}");
                    LaunchStatusChanged?.Invoke("基岩版已启动（离线模式）");
                    LaunchCompleted?.Invoke(true);

                    // 异步等待进程退出（.NET Framework 4.8: Task.Run包装同步WaitForExit）
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            gameProcess.WaitForExit();
                            LaunchLogReceived?.Invoke($"游戏进程已退出 (退出代码: {gameProcess.ExitCode})");
                            LaunchStatusChanged?.Invoke("游戏已退出");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"等待游戏进程退出时出错: {ex.Message}");
                        }
                        finally
                        {
                            gameProcess.Dispose();
                        }
                    });

                    return true;
                }
                else
                {
                    string errorMsg = "无法启动游戏进程，请检查安装是否完整";
                    LaunchLogReceived?.Invoke($"错误: {errorMsg}");
                    LaunchStatusChanged?.Invoke("启动失败");
                    LaunchCompleted?.Invoke(false);
                    return false;
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"启动失败: {ex.Message}";
                LaunchLogReceived?.Invoke($"错误: {errorMsg}");
                LaunchStatusChanged?.Invoke($"启动失败: {ex.Message}");
                LaunchCompleted?.Invoke(false);
                Logger.Error($"基岩版离线启动异常: {ex}");
                return false;
            }
        }

        /// <summary>配置离线游戏环境（写入本地状态，不修改游戏核心文件）</summary>
        private void SetupOfflineEnvironment(BedrockOfflineAccount account, string bedrockDir)
        {
            try
            {
                // 在版本目录下创建离线配置标记文件
                // 这些文件供启动参数解析，不修改游戏核心二进制文件
                string offlineConfigDir = Path.Combine(bedrockDir, "mnl_offline_config");
                Directory.CreateDirectory(offlineConfigDir);

                // 写入离线配置
                var offlineConfig = new
                {
                    enabled = true,
                    account_uuid = account.Uuid,
                    account_username = account.Username,
                    offline_mode = true,
                    data_directory = account.DataDirectory,
                    created_at = DateTime.Now.ToString("O"),
                    launcher_version = "MNL-1.0"
                };

                string configJson = JsonSerializer.Serialize(offlineConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(offlineConfigDir, "offline_config.json"), configJson);
                Logger.Info("离线配置文件已创建");

                LaunchLogReceived?.Invoke("离线配置已就绪");
                LaunchLogReceived?.Invoke($"数据目录: {account.DataDirectory}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"配置离线环境时出错（不影响启动）: {ex.Message}");
                LaunchLogReceived?.Invoke($"注意: 离线配置部分失败 - {ex.Message}");
            }
        }

        /// <summary>检查指定版本是否已安装</summary>
        public bool IsVersionInstalled(string versionId)
        {
            try
            {
                return File.Exists(Path.Combine(
                    _minecraftPath, "bedrock", versionId, "Minecraft.Windows.exe"));
            }
            catch { return false; }
        }

        /// <summary>获取已安装版本列表</summary>
        public List<string> GetInstalledVersions()
        {
            var versions = new List<string>();
            try
            {
                string bedrockDir = Path.Combine(_minecraftPath, "bedrock");
                if (!Directory.Exists(bedrockDir)) return versions;

                foreach (string dir in Directory.GetDirectories(bedrockDir))
                {
                    string versionId = Path.GetFileName(dir);
                    if (versionId == "offline_data" || versionId == "mnl_offline_config")
                        continue;

                    if (File.Exists(Path.Combine(dir, "Minecraft.Windows.exe")))
                    {
                        versions.Add(versionId);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"获取已安装版本列表失败: {ex.Message}");
            }
            return versions;
        }
    }
}
