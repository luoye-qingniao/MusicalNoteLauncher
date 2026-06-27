using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace MusicalNoteLauncher.Utils
{
    /// <summary>
    /// MCTier 联机管理器，基于 EasyTier 核心实现 P2P 虚拟组网。
    /// 房主和房客使用相同命令，EasyTier 自动完成节点发现和 NAT 穿透。
    /// </summary>
    public class MCTierManager
    {
        private static readonly string MCTierDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft", "mctier");

        private Process _coreProcess;
        private string _currentNetworkName;
        private string _currentNetworkSecret;
        private bool _isRunning;

        /// <summary>
        /// 日志输出事件。
        /// </summary>
        public event Action<string> OnLogOutput;

        /// <summary>
        /// 运行状态变更事件。
        /// </summary>
        public event Action<bool> OnStatusChanged;

        public bool IsRunning => _isRunning;
        public string CurrentNetworkName => _currentNetworkName;
        public string CurrentNetworkSecret => _currentNetworkSecret;

        /// <summary>
        /// 查找 EasyTier 核心可执行文件。
        /// 优先级：MCTier 目录中的 easytier-core → mctier.exe → 资源目录。
        /// </summary>
        private string FindCoreExe()
        {
            // 1. 在 MCTier 目录中查找 easytier-core
            if (Directory.Exists(MCTierDir))
            {
                var coreExe = Directory.GetFiles(MCTierDir, "easytier-core.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (coreExe != null) return coreExe;

                // 备选：查找 mctier.exe
                var mctExe = Directory.GetFiles(MCTierDir, "mctier.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (mctExe != null) return mctExe;
            }

            // 2. 在资源目录中查找
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string[] resourcePaths = new[]
            {
                Path.Combine(exeDir, "Resources", "MCTier"),
                Path.Combine(exeDir, "..", "Resources", "MCTier"),
                Path.Combine(exeDir, "..", "..", "Resources", "MCTier"),
            };

            foreach (string path in resourcePaths)
            {
                if (Directory.Exists(path))
                {
                    var exeFiles = Directory.GetFiles(path, "*.exe", SearchOption.AllDirectories);
                    if (exeFiles.Length > 0) return exeFiles[0];
                }
            }

            return null;
        }

        /// <summary>
        /// 检查 MCTier 核心是否已安装。
        /// </summary>
        public bool IsCoreInstalled()
        {
            return FindCoreExe() != null;
        }

        /// <summary>
        /// 获取核心文件路径。
        /// </summary>
        public string GetCorePath()
        {
            return FindCoreExe();
        }

        /// <summary>
        /// 生成邀请码（5位随机字符）。
        /// </summary>
        public string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            var result = new StringBuilder();
            for (int i = 0; i < 5; i++)
                result.Append(chars[random.Next(chars.Length)]);
            return result.ToString();
        }

        /// <summary>
        /// 生成网络凭据（网络名和密钥）。
        /// </summary>
        public (string name, string secret) GenerateNetworkCredentials()
        {
            var random = new Random();
            char roomLetter = (char)random.Next('A', 'Z' + 1);
            string name = $"MCT-{roomLetter}{GenerateInviteCode()}";
            string secret = GenerateInviteCode();
            return (name, secret);
        }

        /// <summary>
        /// 启动 MCTier 组网（房主和房客统一入口）。
        /// EasyTier 使用公共中继节点互联，自动完成 NAT 穿透和 P2P 直连。
        /// </summary>
        public bool Start(string networkName, string networkSecret)
        {
            string coreExe = FindCoreExe();
            if (coreExe == null)
            {
                OnLogOutput?.Invoke("MCTier 核心未安装，请先下载");
                return false;
            }

            if (_isRunning) Stop();

            try
            {
                _currentNetworkName = networkName;
                _currentNetworkSecret = networkSecret;

                // EasyTier CLI: 所有节点使用相同命令，公共中继节点协助发现
                string args = $"-d --network-name \"{networkName}\" --network-secret \"{networkSecret}\" " +
                    $"-p tcp://public.easytier.cn:11010 " +
                    $"--no-listener " +
                    $"--dev-name mctier --ipv4 10.144.0.0/16";

                OnLogOutput?.Invoke($"正在启动 MCTier 网络... 网络名：{networkName}");

                var psi = new ProcessStartInfo
                {
                    FileName = coreExe,
                    Arguments = args,
                    WorkingDirectory = Path.GetDirectoryName(coreExe) ?? MCTierDir,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _coreProcess = new Process { StartInfo = psi };
                _coreProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnLogOutput?.Invoke(e.Data);
                };
                _coreProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnLogOutput?.Invoke($"[MCTier] {e.Data}");
                };

                _coreProcess.Start();
                _coreProcess.BeginOutputReadLine();
                _coreProcess.BeginErrorReadLine();

                _isRunning = true;
                OnStatusChanged?.Invoke(true);
                OnLogOutput?.Invoke($"MCTier 网络已启动！网络名：{networkName}");
                return true;
            }
            catch (Exception ex)
            {
                OnLogOutput?.Invoke($"启动失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检测 Minecraft 局域网端口。
        /// </summary>
        public int DetectMinecraftLanPort()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("javaw");
                if (processes.Length == 0)
                    processes = Process.GetProcessesByName("java");
                if (processes.Length == 0)
                    return 0;

                foreach (var process in processes)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(process.MainWindowTitle) &&
                            process.MainWindowTitle.Contains("Minecraft", StringComparison.OrdinalIgnoreCase))
                        {
                            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                            int[] commonPorts = { 25565, 25566, 25575, 25585, 25595, 25577, 25580, 25590, 25600, 25500 };

                            foreach (int port in commonPorts)
                            {
                                if (listeners.Any(l => l.Port == port))
                                    return port;
                            }

                            foreach (var ep in listeners)
                            {
                                if (ep.Port >= 10000 && ep.Port <= 65535)
                                    return ep.Port;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 停止 MCTier 网络。
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_coreProcess != null && !_coreProcess.HasExited)
                {
                    _coreProcess.Kill();
                    _coreProcess = null;
                }
                _isRunning = false;
                _currentNetworkName = null;
                _currentNetworkSecret = null;
                OnStatusChanged?.Invoke(false);
                OnLogOutput?.Invoke("MCTier 已停止");
            }
            catch (Exception ex)
            {
                OnLogOutput?.Invoke($"停止失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 清理 MCTier 目录。
        /// </summary>
        public void Cleanup()
        {
            Stop();
        }
    }
}
