using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Utils
{
    public class EasyTierManager
    {
        private static readonly string TerracottaDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft", "terracotta");

        private static readonly string CoreExePath = Path.Combine(TerracottaDir, "terracotta.exe");

        private static readonly Random _rng = new Random();

        private Process _coreProcess;
        private string _currentNetworkName;
        private string _currentNetworkSecret;
        private bool _isRunning;

        public event Action<string> OnLogOutput;
        public event Action<bool> OnStatusChanged;

        public bool IsRunning => _isRunning;
        public string CurrentNetworkName => _currentNetworkName;
        public string CurrentNetworkSecret => _currentNetworkSecret;

        public bool IsCoreInstalled() => File.Exists(CoreExePath);

        private string GetResourceCorePath()
        {
            string exeDir = System.AppContext.BaseDirectory;
            string[] possiblePaths = new[]
            {
                Path.Combine(exeDir, "Resources", "Terracotta"),
                Path.Combine(exeDir, "..", "Resources", "Terracotta"),
                Path.Combine(exeDir, "..", "..", "Resources", "Terracotta"),
                Path.Combine(exeDir, "..", "..", "..", "Resources", "Terracotta"),
            };

            foreach (string path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    var exeFiles = Directory.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly);
                    if (exeFiles.Length > 0) return exeFiles[0];
                }
            }
            return null;
        }

        public bool HasResourceCore() => GetResourceCorePath() != null;

        public bool CopyCoreFromResource()
        {
            try
            {
                string resourceCore = GetResourceCorePath();
                if (resourceCore == null) { OnLogOutput?.Invoke("资源目录中未找到核心文件"); return false; }
                if (!Directory.Exists(TerracottaDir)) Directory.CreateDirectory(TerracottaDir);
                File.Copy(resourceCore, CoreExePath, true);
                OnLogOutput?.Invoke("陶瓦核心文件已从资源目录复制");
                return IsCoreInstalled();
            }
            catch (Exception ex) { OnLogOutput?.Invoke($"复制核心文件失败：{ex.Message}"); return false; }
        }

        public string GetCorePath() => CoreExePath;

        public async Task<bool> DownloadCoreAsync(IProgress<int> progress = null)
        {
            if (HasResourceCore())
            {
                OnLogOutput?.Invoke("正在从资源目录复制陶瓦核心文件...");
                if (CopyCoreFromResource()) { OnLogOutput?.Invoke("陶瓦核心安装完成！"); return true; }
                OnLogOutput?.Invoke("资源目录复制失败，尝试网络下载...");
            }

            string[] mirrorUrls = new[]
            {
                "https://github.com/terracotta-network/terracotta/releases/latest/download/terracotta-windows-x86_64.exe",
                "https://ghfast.top/https://github.com/terracotta-network/terracotta/releases/latest/download/terracotta-windows-x86_64.exe",
                "https://gh.con.sh/https://github.com/terracotta-network/terracotta/releases/latest/download/terracotta-windows-x86_64.exe",
                "https://gh.api.99988866.xyz/https://github.com/terracotta-network/terracotta/releases/latest/download/terracotta-windows-x86_64.exe",
            };

            Exception lastError = null;

            foreach (string url in mirrorUrls)
            {
                try
                {
                    OnLogOutput?.Invoke($"正在从 {new Uri(url).Host} 下载陶瓦核心...");
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMinutes(5);
                        client.DefaultRequestHeaders.UserAgent.TryParseAdd("MNL");

                        using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            long totalBytes = response.Content.Headers.ContentLength ?? -1;
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            {
                                if (!Directory.Exists(TerracottaDir)) Directory.CreateDirectory(TerracottaDir);
                                using (var fileStream = new FileStream(CoreExePath, FileMode.Create, FileAccess.Write))
                                {
                                    byte[] buffer = new byte[65536];
                                    long downloadedBytes = 0;
                                    int bytesRead;
                                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                    {
                                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                                        downloadedBytes += bytesRead;
                                        if (totalBytes > 0 && progress != null)
                                        {
                                            progress.Report((int)(downloadedBytes * 100 / totalBytes));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    OnLogOutput?.Invoke("陶瓦核心下载完成！");
                    return true;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    OnLogOutput?.Invoke($"从 {new Uri(url).Host} 下载失败: {ex.Message}，尝试下一个镜像...");
                }
            }

            OnLogOutput?.Invoke($"所有镜像下载均失败：{lastError?.Message}");
            return false;
        }

        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var result = new StringBuilder(length);
            lock (_rng)
            {
                for (int i = 0; i < length; i++) result.Append(chars[_rng.Next(chars.Length)]);
            }
            return result.ToString();
        }

        public bool StartHost(string networkName, string networkSecret, int gamePort = 25565)
        {
            if (!IsCoreInstalled()) { OnLogOutput?.Invoke("陶瓦核心未安装"); return false; }
            if (_isRunning) Stop();
            try
            {
                _currentNetworkName = networkName;
                _currentNetworkSecret = networkSecret;
                var args = $"server --name \"{networkName}\" --password \"{networkSecret}\" --port {gamePort}";
                OnLogOutput?.Invoke($"正在启动陶瓦服务器... 房间：{networkName}");
                var psi = new ProcessStartInfo { FileName = CoreExePath, Arguments = args, WorkingDirectory = TerracottaDir, CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                _coreProcess = new Process { StartInfo = psi };
                _coreProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) OnLogOutput?.Invoke(e.Data); };
                _coreProcess.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) OnLogOutput?.Invoke($"[ERROR] {e.Data}"); };
                _coreProcess.Start(); _coreProcess.BeginOutputReadLine(); _coreProcess.BeginErrorReadLine();
                _isRunning = true; OnStatusChanged?.Invoke(true);
                OnLogOutput?.Invoke($"陶瓦服务器启动成功！房间号：{networkName}, 密码：{networkSecret}");
                return true;
            }
            catch (Exception ex) { OnLogOutput?.Invoke($"启动失败：{ex.Message}"); return false; }
        }

        public bool StartClient(string networkName, string networkSecret, int port)
        {
            if (!IsCoreInstalled()) { OnLogOutput?.Invoke("陶瓦核心未安装"); return false; }
            if (_isRunning) Stop();
            try
            {
                _currentNetworkName = networkName; _currentNetworkSecret = networkSecret;
                var args = $"client --name \"{networkName}\" --password \"{networkSecret}\" --port {port}";
                OnLogOutput?.Invoke($"正在连接房间：{networkName} 端口：{port}");
                var psi = new ProcessStartInfo { FileName = CoreExePath, Arguments = args, WorkingDirectory = TerracottaDir, CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                _coreProcess = new Process { StartInfo = psi };
                _coreProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) OnLogOutput?.Invoke(e.Data); };
                _coreProcess.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) OnLogOutput?.Invoke($"[ERROR] {e.Data}"); };
                _coreProcess.Start(); _coreProcess.BeginOutputReadLine(); _coreProcess.BeginErrorReadLine();
                _isRunning = true; OnStatusChanged?.Invoke(true);
                OnLogOutput?.Invoke("已加入房间！"); return true;
            }
            catch (Exception ex) { OnLogOutput?.Invoke($"加入失败：{ex.Message}"); return false; }
        }

        public void Stop()
        {
            try { if (_coreProcess != null && !_coreProcess.HasExited) { _coreProcess.Kill(); _coreProcess = null; } _isRunning = false; _currentNetworkName = null; _currentNetworkSecret = null; OnStatusChanged?.Invoke(false); OnLogOutput?.Invoke("陶瓦已停止"); }
            catch (Exception ex) { OnLogOutput?.Invoke($"停止失败：{ex.Message}"); }
        }

        public void Cleanup() { Stop(); try { if (Directory.Exists(TerracottaDir)) Directory.Delete(TerracottaDir, true); } catch { } }
    }
}
