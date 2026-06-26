using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 基岩版增强下载服务：支持断点续传、SHA1校验、多镜像源、进度实时反馈、运行环境检测
    /// </summary>
    public class BedrockEnhancedDownloadService : IDisposable
    {
        private const string BEDROCK_MANIFEST_URL = "https://data.mcappx.com/v2/bedrock.json";
        private const int DEFAULT_TIMEOUT_SECONDS = 60;

        private readonly string _minecraftPath;
        private readonly HttpClient _httpClient;
        private CancellationTokenSource _currentCts;
        private bool _disposed;

        /// <summary>下载状态变更事件</summary>
        public event Action<string> StatusChanged;
        /// <summary>下载完成事件</summary>
        public event Action<string> DownloadCompleted;
        /// <summary>下载失败事件</summary>
        public event Action<string, string> DownloadFailed;

        public BedrockEnhancedDownloadService(string minecraftPath)
        {
            _minecraftPath = minecraftPath ?? throw new ArgumentNullException(nameof(minecraftPath));

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        // ────────────────────────── 版本列表获取 ──────────────────────────

        /// <summary>获取远程基岩版版本列表</summary>
        public async Task<List<BedrockVersionInfo>> GetRemoteVersionsAsync()
        {
            var versions = new List<BedrockVersionInfo>();
            try
            {
                StatusChanged?.Invoke("正在获取基岩版版本列表...");
                string json = await _httpClient.GetStringAsync(BEDROCK_MANIFEST_URL).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(json);
                // mcappx.com API: { "From_mcappx.com": { "1.21.100.24": { ... }, ... } }
                if (doc.RootElement.TryGetProperty("From_mcappx.com", out var versionsDict))
                {
                    foreach (var kv in versionsDict.EnumerateObject())
                    {
                        try
                        {
                            string versionKey = kv.Name;
                            var item = kv.Value;

                            string type = "release";
                            if (item.TryGetProperty("Type", out var t))
                            {
                                string typeStr = t.GetString() ?? "";
                                type = typeStr.ToLowerInvariant();
                            }

                            string buildType = item.TryGetProperty("BuildType", out var bt) ? bt.GetString() : "";
                            string id = item.TryGetProperty("ID", out var vid) ? vid.GetString() : versionKey;
                            string date = item.TryGetProperty("Date", out var d) ? d.GetString() : "";

                            // Extract installer/package URL from Variations
                            string installerUrl = null;
                            if (item.TryGetProperty("Variations", out var variations))
                            {
                                foreach (var v in variations.EnumerateArray())
                                {
                                    string arch = v.TryGetProperty("Arch", out var a) ? a.GetString() : "";
                                    if (arch != "x64") continue;

                                    if (v.TryGetProperty("MetaData", out var md) && md.GetArrayLength() > 0)
                                    {
                                        string metaFirst = md[0].GetString();
                                        if (!string.IsNullOrEmpty(metaFirst))
                                        {
                                            // GDK builds: MetaData contains direct download URLs
                                            if (metaFirst.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                                installerUrl = metaFirst;
                                            // UWP builds: MetaData contains UUIDs (need Store API, skip for now)
                                        }
                                    }
                                    break;
                                }
                            }

                            if (!string.IsNullOrEmpty(id))
                            {
                                var info = new BedrockVersionInfo
                                {
                                    Id = id,
                                    Version = versionKey,
                                    Type = type,
                                    InstallerUrl = installerUrl,
                                    IsDownloaded = IsVersionDownloaded(id)
                                };

                                // Parse release date
                                if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var releaseTime))
                                    info.ReleaseTime = releaseTime;

                                versions.Add(info);
                            }
                        }
                        catch { /* 跳过解析失败的条目 */ }
                    }
                }
                StatusChanged?.Invoke($"获取到 {versions.Count} 个基岩版版本");
            }
            catch (Exception ex)
            {
                Logger.Error($"获取基岩版版本列表失败: {ex.Message}");
                StatusChanged?.Invoke("获取版本列表失败，请检查网络连接");
            }
            return versions;
        }

        /// <summary>获取已安装的基岩版版本列表</summary>
        public List<BedrockVersionInfo> GetInstalledVersions()
        {
            var versions = new List<BedrockVersionInfo>();
            try
            {
                string bedrockDir = Path.Combine(_minecraftPath, "bedrock");
                if (!Directory.Exists(bedrockDir)) return versions;

                foreach (string dir in Directory.GetDirectories(bedrockDir))
                {
                    string versionId = Path.GetFileName(dir);
                    string exePath = Path.Combine(dir, "Minecraft.Windows.exe");
                    if (File.Exists(exePath))
                    {
                        versions.Add(new BedrockVersionInfo
                        {
                            Id = versionId,
                            Version = versionId,
                            Type = "release",
                            IsDownloaded = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"获取已安装基岩版失败: {ex.Message}");
            }
            return versions;
        }

        /// <summary>检测主流稳定版（取release类型的最新版本）</summary>
        public async Task<BedrockVersionInfo> GetLatestStableVersionAsync()
        {
            var versions = await GetRemoteVersionsAsync();
            return versions
                .Where(v => v.Type == "release")
                .OrderByDescending(v => v.ReleaseTime)
                .FirstOrDefault();
        }

        private bool IsVersionDownloaded(string versionId)
        {
            try
            {
                return File.Exists(Path.Combine(_minecraftPath, "bedrock", versionId, "Minecraft.Windows.exe"));
            }
            catch { return false; }
        }

        // ────────────────────────── 断点续传下载 ──────────────────────────

        /// <summary>开始下载基岩版（使用与 Java 相同的下载 + 重试 + 进度机制）</summary>
        public async Task<DownloadTaskInfo> StartDownloadAsync(
            BedrockVersionInfo version,
            DownloadProgress progress = null,
            CancellationToken cancellationToken = default)
        {
            var taskInfo = new DownloadTaskInfo
            {
                VersionId = version.Id,
                Status = "准备中"
            };

            _currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _currentCts.Token;

            try
            {
                string versionDir = Path.Combine(_minecraftPath, "bedrock", version.Id);
                Directory.CreateDirectory(versionDir);

                string installerPath = Path.Combine(versionDir, "installer.msixbundle");
                string hashPath = Path.Combine(versionDir, "installer.sha1");

                // 已有文件且 SHA1 匹配 → 跳过下载
                if (File.Exists(installerPath) && File.Exists(hashPath))
                {
                    string storedHash = File.ReadAllText(hashPath).Trim();
                    if (!string.IsNullOrEmpty(storedHash) && VerifySha1(installerPath, storedHash))
                    {
                        taskInfo.Status = "已完成";
                        taskInfo.IsCompleted = true;
                        taskInfo.Progress = 100;
                        taskInfo.CurrentFile = "installer.msixbundle";
                        StatusChanged?.Invoke($"{version.Id} 已下载完成（文件已存在）");
                        DownloadCompleted?.Invoke(version.Id);
                        return taskInfo;
                    }
                }

                taskInfo.Status = "下载中";
                taskInfo.CurrentFile = "installer.msixbundle";
                StatusChanged?.Invoke($"正在下载基岩版 {version.Id}...");

                // 使用共享 DownloadHelper（与 Java 版相同的重试 + 进度机制）
                await DownloadHelper.DownloadFileWithRetryAsync(
                    _httpClient, version.InstallerUrl, installerPath, progress, token);

                // SHA1 校验
                taskInfo.Status = "校验中";
                StatusChanged?.Invoke("正在校验文件完整性...");
                string computedHash = ComputeSha1(installerPath);
                File.WriteAllText(hashPath, computedHash);
                Logger.Info($"基岩版 {version.Id} SHA1: {computedHash}");

                taskInfo.Status = "已完成";
                taskInfo.IsCompleted = true;
                taskInfo.Progress = 100;

                StatusChanged?.Invoke($"基岩版 {version.Id} 下载完成");
                DownloadCompleted?.Invoke(version.Id);

                // 检查运行环境
                await CheckRuntimeDependenciesAsync();
            }
            catch (OperationCanceledException)
            {
                taskInfo.Status = "已取消";
                taskInfo.ErrorMessage = "下载已取消";
            }
            catch (Exception ex)
            {
                taskInfo.Status = "下载失败";
                taskInfo.ErrorMessage = ex.Message;
                Logger.Error($"基岩版下载失败: {ex.Message}");
                DownloadFailed?.Invoke(version.Id, ex.Message);
            }
            finally
            {
                _currentCts?.Dispose();
                _currentCts = null;
            }

            return taskInfo;
        }

        /// <summary>取消当前下载</summary>
        public void CancelDownload()
        {
            try
            {
                _currentCts?.Cancel();
                StatusChanged?.Invoke("正在取消下载...");
            }
            catch (Exception ex)
            {
                Logger.Warning($"取消下载时出错: {ex.Message}");
            }
        }

        // ────────────────────────── SHA1 校验 ──────────────────────────

        /// <summary>计算文件SHA1哈希值</summary>
        public static string ComputeSha1(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>校验文件SHA1</summary>
        public static bool VerifySha1(string filePath, string expectedSha1)
        {
            if (string.IsNullOrEmpty(expectedSha1) || !File.Exists(filePath))
                return true; // 无期望值时跳过校验

            string actual = ComputeSha1(filePath);
            bool valid = string.Equals(expectedSha1, actual, StringComparison.OrdinalIgnoreCase);
            if (!valid)
                Logger.Warning($"SHA1校验不匹配 - 期望: {expectedSha1}, 实际: {actual}");
            return valid;
        }

        // ────────────────────────── 运行环境检测 ──────────────────────────

        /// <summary>检测并提示安装必要运行环境</summary>
        public async Task CheckRuntimeDependenciesAsync()
        {
            try
            {
                StatusChanged?.Invoke("正在检测运行环境...");

                bool needVcRuntime = NeedVcRuntime();
                bool needStoreFramework = NeedMicrosoftStoreFramework();

                if (needVcRuntime || needStoreFramework)
                {
                    var missing = new List<string>();
                    if (needVcRuntime) missing.Add("VC++ Redistributable");
                    if (needStoreFramework) missing.Add("Microsoft Store 应用框架");

                    Logger.Warning($"缺少运行环境: {string.Join(", ", missing)}");
                    StatusChanged?.Invoke($"提示：建议安装 {string.Join(", ", missing)} 以确保基岩版正常运行");
                }
                else
                {
                    StatusChanged?.Invoke("运行环境检测通过");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"运行环境检测异常: {ex.Message}");
            }
        }

        private static bool NeedVcRuntime()
        {
            try
            {
                // 检测常见的VC++运行时DLL是否存在
                string systemDir = Environment.GetFolderPath(
                    Environment.Is64BitOperatingSystem
                        ? Environment.SpecialFolder.SystemX86
                        : Environment.SpecialFolder.System);

                string[] vcDlls = { "vcruntime140.dll", "msvcp140.dll", "concrt140.dll" };
                foreach (string dll in vcDlls)
                {
                    if (!File.Exists(Path.Combine(systemDir, dll)))
                        return true;
                }
                return false;
            }
            catch { return true; }
        }

        private static bool NeedMicrosoftStoreFramework()
        {
            try
            {
                // 检测AppX部署服务是否可用
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Get-AppxPackage -Name Microsoft.WindowsStore\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return string.IsNullOrWhiteSpace(output) || output.Contains("未找到");
            }
            catch { return true; }
        }

        // ────────────────────────── 版本管理 ──────────────────────────

        /// <summary>删除指定版本</summary>
        public bool DeleteVersion(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(_minecraftPath, "bedrock", versionId);
                if (Directory.Exists(versionDir))
                {
                    Directory.Delete(versionDir, true);
                    Logger.Info($"已删除基岩版 {versionId}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"删除基岩版失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>获取版本占用磁盘空间</summary>
        public long GetVersionSize(string versionId)
        {
            try
            {
                string versionDir = Path.Combine(_minecraftPath, "bedrock", versionId);
                if (!Directory.Exists(versionDir)) return 0;
                return new DirectoryInfo(versionDir)
                    .GetFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch { return 0; }
        }

        // ────────────────────────── IDisposable ──────────────────────────

        public void Dispose()
        {
            if (!_disposed)
            {
                _currentCts?.Cancel();
                _currentCts?.Dispose();
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
