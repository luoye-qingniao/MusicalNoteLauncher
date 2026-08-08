using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 联机工具服务：管理 MCTier / 陶瓦(Terracotta) 的下载、安装、启动、组网。
    /// 陶瓦逻辑完整搬运自 Utils.EasyTierManager 的 HMCL 模式实现。
    /// </summary>
    public class MultiplayerService
    {
        private static MultiplayerService _instance;
        public static MultiplayerService Instance => _instance ??= new MultiplayerService();

        private readonly string _toolsDir;

        // ═══════════════ 可配置 URL ═══════════════

        /// <summary>MCTier 下载地址</summary>
        public static string MctierDownloadUrl { get; set; } =
            "https://github.com/MCTier/MCTier/releases/latest/download/MCTier-Setup.zip";

        /// <summary>陶瓦(Terracotta) 下载地址</summary>
        public static string TaowaDownloadUrl { get; set; } =
            "http://103.79.186.213/terracotta.exe";

        /// <summary>MNL ServerManager 下载地址</summary>
        public static string ServerManagerDownloadUrl { get; set; } =
            "http://85.137.246.87/mnl-server-manager.zip";

        // ═══════════════ Terracotta 运行时状态 ═══════════════

        private Process _terracottaProcess;
        private CancellationTokenSource _terracottaStateCts;
        private int _terracottaApiPort;
        private string _terracottaRoomCode;
        private string _terracottaState = "stopped";
        private readonly object _terracottaLock = new object();

        /// <summary>Terracotta 是否在运行</summary>
        public bool IsTerracottaRunning
        {
            get { lock (_terracottaLock) return _terracottaProcess != null && !_terracottaProcess.HasExited; }
        }

        private MultiplayerService()
        {
            _toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MNL", "tools");
            if (!Directory.Exists(_toolsDir))
                Directory.CreateDirectory(_toolsDir);
        }

        /// <summary>创建专用下载 HttpClient</summary>
        private static HttpClient CreateDownloadClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        }

        // ═══════════════ 路径 ═══════════════

        public string MctierDir => Path.Combine(_toolsDir, "MCTier");
        public string TaowaDir => Path.Combine(_toolsDir, "Taowa");
        public string ServerManagerDir => Path.Combine(_toolsDir, "ServerManager");

        private static string LegacyMctierDir
        {
            get { try { return Path.Combine(AppContext.MinecraftPath, "mctier"); } catch { return null; } }
        }

        private static string LegacyTaowaDir
        {
            get { try { return Path.Combine(AppContext.MinecraftPath, "terracotta"); } catch { return null; } }
        }

        /// <summary>获取 terracotta.exe 所在目录（优先 MNL tools，回退 .minecraft）</summary>
        private string FindTerracottaDir()
        {
            foreach (var dir in new[] { TaowaDir, LegacyTaowaDir })
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var exe = Path.Combine(dir, "terracotta.exe");
                if (File.Exists(exe)) return dir;
            }
            return null;
        }

        /// <summary>获取 terracotta.exe 完整路径</summary>
        private string GetTerracottaExePath()
        {
            var dir = FindTerracottaDir();
            return dir != null ? Path.Combine(dir, "terracotta.exe") : Path.Combine(TaowaDir, "terracotta.exe");
        }

        // ═══════════════ MCTier ═══════════════

        public bool IsMctierInstalled()
        {
            foreach (var dir in new[] { MctierDir, LegacyMctierDir })
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    if (Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories).Length > 0)
                        return true;
                }
                catch { }
            }
            return false;
        }

        public string GetMctierExePath()
        {
            foreach (var dir in new[] { MctierDir, LegacyMctierDir })
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    var exes = Directory.GetFiles(dir, "MCTier*.exe", SearchOption.AllDirectories);
                    if (exes.Length > 0) return exes[0];
                    exes = Directory.GetFiles(dir, "mctier*.exe", SearchOption.AllDirectories);
                    if (exes.Length > 0) return exes[0];
                    exes = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories);
                    if (exes.Length > 0) return exes[0];
                }
                catch { }
            }
            return null;
        }

        public async Task<bool> DownloadMctierAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(MctierDownloadUrl))
                {
                    Logger.Error("[联机] MCTier 下载地址未配置");
                    return false;
                }

                string zipPath = Path.Combine(_toolsDir, "MCTier-download.zip");
                Logger.Info("[联机] 开始下载 MCTier: " + MctierDownloadUrl);

                using (var http = CreateDownloadClient())
                {
                    var response = await http.GetAsync(MctierDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        await stream.CopyToAsync(fs);
                    }
                }

                Logger.Info("[联机] MCTier 下载完成，正在解压...");

                if (Directory.Exists(MctierDir))
                    Directory.Delete(MctierDir, true);
                Directory.CreateDirectory(MctierDir);

                ZipFile.ExtractToDirectory(zipPath, MctierDir);
                try { File.Delete(zipPath); } catch { }

                var extracted = Directory.GetFiles(MctierDir, "*", SearchOption.AllDirectories);
                Logger.Info($"[联机] MCTier 提取了 {extracted.Length} 个文件");
                return extracted.Length > 0;
            }
            catch (Exception ex)
            {
                Logger.Error("[联机] MCTier 下载安装失败: " + ex.Message);
                return false;
            }
        }

        public bool OpenMctierFolder()
        {
            try
            {
                string dir = MctierDir;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", dir);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("[联机] 打开 MCTier 目录失败: " + ex.Message);
                return false;
            }
        }

        public bool LaunchMctier()
        {
            try
            {
                string exe = GetMctierExePath();
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    Logger.Error("[联机] MCTier 可执行文件未找到");
                    return false;
                }
                Logger.Info("[联机] 启动 MCTier: " + exe);
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? MctierDir
                });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("[联机] 启动 MCTier 失败: " + ex.Message);
                return false;
            }
        }

        // ═══════════════ 陶瓦 (Terracotta) ═══════════════

        public bool IsTaowaInstalled()
        {
            foreach (var dir in new[] { TaowaDir, LegacyTaowaDir })
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var exePath = Path.Combine(dir, "terracotta.exe");
                if (File.Exists(exePath)) return true;
            }
            return false;
        }

        /// <summary>下载陶瓦核心 → MNL/tools/Taowa/terracotta.exe（搬运自 EasyTierManager.DownloadCoreAsync）</summary>
        public async Task<bool> DownloadTaowaAsync()
        {
            string url = TaowaDownloadUrl;
            if (string.IsNullOrEmpty(url))
            {
                Logger.Error("[陶瓦] 下载地址未配置");
                return false;
            }

            try
            {
                Logger.Info("[陶瓦] 开始下载: " + url);

                if (!Directory.Exists(TaowaDir))
                    Directory.CreateDirectory(TaowaDir);

                string exePath = Path.Combine(TaowaDir, "terracotta.exe");

                using (var client = CreateDownloadClient())
                {
                    client.DefaultRequestHeaders.UserAgent.TryParseAdd("MNL");

                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(exePath, FileMode.Create, FileAccess.Write))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }
                }

                Logger.Info("[陶瓦] 下载完成: " + exePath);
                return File.Exists(exePath);
            }
            catch (Exception ex)
            {
                Logger.Error("[陶瓦] 下载失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>启动陶瓦核心（搬运自 EasyTierManager.LaunchAsync — HMCL 模式）</summary>
        public async Task<bool> LaunchTerracottaAsync()
        {
            if (!IsTaowaInstalled())
            {
                Logger.Error("[陶瓦] 核心未安装");
                return false;
            }

            lock (_terracottaLock)
            {
                if (IsTerracottaRunning)
                {
                    Logger.Info("[陶瓦] 已在运行中，先停止再重启");
                    StopTerracotta();
                }
            }

            try
            {
                string exePath = GetTerracottaExePath();
                string workDir = FindTerracottaDir() ?? TaowaDir;

                string tempDir = Path.Combine(Path.GetTempPath(), $"mnl-terracotta-{Environment.ProcessId}");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                var args = $"--mnl \"{tempDir}\"";
                Logger.Info("[陶瓦] 正在启动 (HMCL 模式)...");

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    WorkingDirectory = workDir,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                proc.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Logger.Info("[陶瓦] " + e.Data);
                        TryParseTerracottaPort(e.Data);
                    }
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Logger.Error("[陶瓦] " + e.Data);
                        TryParseTerracottaPort(e.Data);
                    }
                };
                proc.Exited += (s, e) =>
                {
                    lock (_terracottaLock)
                    {
                        _terracottaState = "stopped";
                        _terracottaRoomCode = null;
                        _terracottaApiPort = 0;
                        _terracottaStateCts?.Cancel();
                        _terracottaProcess = null;
                    }
                    Logger.Info("[陶瓦] 进程已退出");
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                lock (_terracottaLock)
                {
                    _terracottaProcess = proc;
                    _terracottaState = "starting";
                    _terracottaApiPort = 0;
                    _terracottaRoomCode = null;
                }

                // 等待 API 端口就绪（最多 15 秒）
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);
                    lock (_terracottaLock)
                    {
                        if (_terracottaApiPort > 0) break;
                    }
                }

                lock (_terracottaLock)
                {
                    if (_terracottaApiPort <= 0)
                    {
                        Logger.Error("[陶瓦] 启动超时，未能获取 API 端口");
                        KillTerracottaInternal(proc);
                        return false;
                    }
                }

                Logger.Info($"[陶瓦] 已启动，API 端口: {_terracottaApiPort}");

                // 开始状态轮询
                _terracottaStateCts = new CancellationTokenSource();
                _ = PollTerracottaStateAsync(_terracottaStateCts.Token);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("[陶瓦] 启动失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>创建房间（房主模式）— 调用 /state/hosting</summary>
        public async Task<bool> StartTerracottaHostAsync()
        {
            int port;
            lock (_terracottaLock) { port = _terracottaApiPort; }

            if (port <= 0)
            {
                Logger.Error("[陶瓦] API 端口未就绪，无法创建房间");
                return false;
            }

            try
            {
                Logger.Info("[陶瓦] 正在创建房间...");
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var response = await client.GetAsync($"http://127.0.0.1:{port}/state/hosting");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Error("[陶瓦] 创建房间失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>加入房间（房客模式）— 调用 /state/guesting?room=CODE</summary>
        public async Task<bool> JoinTerracottaRoomAsync(string roomCode)
        {
            int port;
            lock (_terracottaLock) { port = _terracottaApiPort; }

            if (port <= 0)
            {
                Logger.Error("[陶瓦] API 端口未就绪，无法加入房间");
                return false;
            }

            try
            {
                Logger.Info($"[陶瓦] 正在加入房间: {roomCode}");
                var playerName = WindowsIdentity.GetCurrent().Name
                    .Split('\\').LastOrDefault() ?? "MNL_Player";
                var query = $"?room={Uri.EscapeDataString(roomCode)}&player={Uri.EscapeDataString(playerName)}";

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var response = await client.GetAsync($"http://127.0.0.1:{port}/state/guesting{query}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Error("[陶瓦] 加入房间失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>停止陶瓦核心</summary>
        public void StopTerracotta()
        {
            lock (_terracottaLock)
            {
                try
                {
                    _terracottaStateCts?.Cancel();
                    _terracottaStateCts?.Dispose();
                    _terracottaStateCts = null;
                }
                catch { }

                if (_terracottaProcess != null)
                {
                    KillTerracottaInternal(_terracottaProcess);
                    _terracottaProcess = null;
                }

                _terracottaState = "stopped";
                _terracottaApiPort = 0;
                _terracottaRoomCode = null;
            }
            Logger.Info("[陶瓦] 已停止");
        }

        private static void KillTerracottaInternal(Process proc)
        {
            try { if (!proc.HasExited) proc.Kill(); } catch { }
        }

        /// <summary>获取陶瓦当前运行状态（供前端轮询）</summary>
        public TerracottaStatus GetTerracottaStatus()
        {
            lock (_terracottaLock)
            {
                return new TerracottaStatus
                {
                    running = IsTerracottaRunning,
                    state = _terracottaState,
                    roomCode = _terracottaRoomCode ?? "",
                    apiPort = _terracottaApiPort
                };
            }
        }

        /// <summary>从输出中解析 API 端口（搬运自 EasyTierManager.TryParsePort）</summary>
        private void TryParseTerracottaPort(string line)
        {
            int currentPort;
            lock (_terracottaLock) { currentPort = _terracottaApiPort; }
            if (currentPort > 0) return;

            foreach (string part in line.Split(' ', ':', '/', '\t'))
            {
                if (int.TryParse(part, out int port) && port >= 1024 && port <= 65535)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                            var resp = await client.GetAsync($"http://127.0.0.1:{port}/state");
                            if (resp.IsSuccessStatusCode)
                            {
                                lock (_terracottaLock) { _terracottaApiPort = port; }
                                Logger.Info($"[陶瓦] API 端口检测成功: {port}");
                            }
                        }
                        catch { }
                    });
                    break;
                }
            }
        }

        /// <summary>轮询 Terracotta HTTP API 状态（搬运自 EasyTierManager.PollStateAsync）</summary>
        private async Task PollTerracottaStateAsync(CancellationToken token)
        {
            int lastIndex = -1;

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(500, token);

                int port;
                lock (_terracottaLock) { port = _terracottaApiPort; }
                if (port <= 0) continue;

                bool procRunning;
                lock (_terracottaLock) { procRunning = IsTerracottaRunning; }
                if (!procRunning) break;

                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                    using var response = await client.GetAsync($"http://127.0.0.1:{port}/state", token);
                    if (!response.IsSuccessStatusCode) continue;

                    string json = await response.Content.ReadAsStringAsync();
                    var node = JsonNode.Parse(json);
                    if (node == null) continue;

                    string state = node["state"]?.GetValue<string>() ?? "";
                    int index = node["index"]?.GetValue<int>() ?? -1;

                    if (index == lastIndex) continue;
                    lastIndex = index;

                    lock (_terracottaLock)
                    {
                        _terracottaState = state;

                        // HostOK → 记录房间码
                        if (state == "host-ok")
                        {
                            var room = node["room"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(room))
                                _terracottaRoomCode = room;
                        }
                    }

                    Logger.Info($"[陶瓦] 状态: {state}" +
                        (_terracottaRoomCode != null ? $" 房间码: {_terracottaRoomCode}" : ""));
                }
                catch (TaskCanceledException) { break; }
                catch { }
            }
        }

        // ═══════════════ MNL 服务端管理器 (ServerManager) ═══════════════

        public bool IsServerManagerInstalled()
        {
            string exePath = Path.Combine(ServerManagerDir, "ServerManager.exe");
            return File.Exists(exePath);
        }

        /// <summary>下载并解压 MNL 服务端管理器 → MNL/tools/ServerManager/</summary>
        public async Task<bool> DownloadServerManagerAsync()
        {
            string url = ServerManagerDownloadUrl;
            if (string.IsNullOrEmpty(url))
            {
                Logger.Error("[ServerManager] 下载地址未配置");
                return false;
            }

            try
            {
                Logger.Info("[ServerManager] 开始下载: " + url);

                if (!Directory.Exists(ServerManagerDir))
                    Directory.CreateDirectory(ServerManagerDir);

                string zipPath = Path.Combine(ServerManagerDir, "ServerManager-download.zip");

                using (var client = CreateDownloadClient())
                {
                    client.DefaultRequestHeaders.UserAgent.TryParseAdd("MNL");
                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }
                }

                Logger.Info("[ServerManager] 下载完成，正在解压...");

                // 解压到目标目录（覆盖现有文件）
                ZipFile.ExtractToDirectory(zipPath, ServerManagerDir, true);
                try { File.Delete(zipPath); } catch { }

                string exePath = Path.Combine(ServerManagerDir, "ServerManager.exe");
                bool ok = File.Exists(exePath);
                Logger.Info(ok ? "[ServerManager] 安装完成" : "[ServerManager] 解压后未找到 ServerManager.exe");
                return ok;
            }
            catch (Exception ex)
            {
                Logger.Error("[ServerManager] 下载安装失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>打开 ServerManager 安装目录</summary>
        public bool OpenServerManagerFolder()
        {
            try
            {
                if (Directory.Exists(ServerManagerDir))
                {
                    Process.Start("explorer.exe", ServerManagerDir);
                    return true;
                }
                Logger.Error("[ServerManager] 安装目录不存在: " + ServerManagerDir);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("[ServerManager] 打开目录失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>启动 ServerManager.exe</summary>
        public bool LaunchServerManager()
        {
            try
            {
                string exe = Path.Combine(ServerManagerDir, "ServerManager.exe");
                if (!File.Exists(exe))
                {
                    Logger.Error("[ServerManager] 可执行文件未找到: " + exe);
                    return false;
                }
                Logger.Info("[ServerManager] 启动: " + exe);
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                    WorkingDirectory = ServerManagerDir
                });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("[ServerManager] 启动失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>清理 — 删除安装目录</summary>
        public void CleanupTaowa()
        {
            StopTerracotta();
            try { if (Directory.Exists(TaowaDir)) Directory.Delete(TaowaDir, true); } catch { }
        }
    }

    /// <summary>陶瓦运行状态（JSON 序列化返回前端）</summary>
    public class TerracottaStatus
    {
        public bool running { get; set; }
        public string state { get; set; } = "stopped";
        public string roomCode { get; set; } = "";
        public int apiPort { get; set; }
    }
}
