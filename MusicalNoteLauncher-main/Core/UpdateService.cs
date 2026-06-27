using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 启动器自动更新服务 —— 版本拉取、升级包下载、文件替换、进程重启。
    /// </summary>
    public static class UpdateService
    {
        /// <summary>当前启动器版本号</summary>
        public const string CurrentVersion = "1.0.0";

        /// <summary>当前启动器版本序号</summary>
        public const int CurrentVersionCode = 1;

        private static readonly HttpClient _http = SafeHttpClientFactory.CreateClient();
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        /// <summary>
        /// 版本信息返回模型
        /// </summary>
        public class VersionInfo
        {
            public string Version { get; set; } = "";
            public int VersionCode { get; set; }
            public string DownloadUrl { get; set; } = "";
            public string FileHash { get; set; } = "";
            public long FileSize { get; set; }
            public string ReleaseNotes { get; set; } = "";
            public bool IsForced { get; set; }
            public bool NeedUpdate { get; set; }
        }

        /// <summary>
        /// 检查更新进度回调
        /// </summary>
        public class UpdateProgress
        {
            public string Status { get; set; } = "";
            public int ProgressPercent { get; set; }
        }

        /// <summary>
        /// 从服务器拉取最新版本信息
        /// </summary>
        /// <returns>版本信息；网络错误或服务不可用时返回 null</returns>
        public static async Task<VersionInfo?> FetchLatestVersionAsync()
        {
            try
            {
                string url = $"{ServerConfig.VersionApiUrl}?current_version={CurrentVersion}";
                string json = await _http.GetStringAsync(url);

                var info = JsonSerializer.Deserialize<VersionInfo>(json, _jsonOptions);
                return info;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[UpdateService] 版本检查失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 下载并安装更新
        /// </summary>
        /// <param name="info">版本信息</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>安装成功返回 true</returns>
        public static async Task<bool> DownloadAndInstallAsync(
            VersionInfo info,
            Action<UpdateProgress>? progressCallback = null)
        {
            if (string.IsNullOrWhiteSpace(info.DownloadUrl))
            {
                Logger.Error("[UpdateService] 升级包下载地址为空");
                return false;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "MNL_Update");
            string zipPath = Path.Combine(tempDir, "update.zip");
            string extractDir = Path.Combine(tempDir, "extracted");

            try
            {
                // 准备临时目录
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                // 下载升级包
                progressCallback?.Invoke(new UpdateProgress
                {
                    Status = "正在下载升级包...",
                    ProgressPercent = 0,
                });

                using var response = await _http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? info.FileSize;

                using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var contentStream = await response.Content.ReadAsStreamAsync();

                byte[] buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;
                int lastReportedPercent = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        int percent = (int)(totalRead * 100 / totalBytes);
                        if (percent != lastReportedPercent && percent % 5 == 0)
                        {
                            lastReportedPercent = percent;
                            progressCallback?.Invoke(new UpdateProgress
                            {
                                Status = $"正在下载升级包... {percent}%",
                                ProgressPercent = percent,
                            });
                        }
                    }
                }

                // 校验 SHA256
                if (!string.IsNullOrWhiteSpace(info.FileHash))
                {
                    progressCallback?.Invoke(new UpdateProgress
                    {
                        Status = "正在校验文件完整性...",
                        ProgressPercent = 95,
                    });

                    string actualHash = ComputeSha256(zipPath);
                    if (!string.Equals(actualHash, info.FileHash, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Error($"[UpdateService] 文件校验失败: 期望={info.FileHash}, 实际={actualHash}");
                        progressCallback?.Invoke(new UpdateProgress
                        {
                            Status = "升级包校验失败，请重试",
                            ProgressPercent = 0,
                        });
                        return false;
                    }
                }

                // 解压升级包
                progressCallback?.Invoke(new UpdateProgress
                {
                    Status = "正在安装更新...",
                    ProgressPercent = 98,
                });

                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // 获取当前程序目录
                string appDir = AppDomain.CurrentDomain.BaseDirectory;

                // 先启动一个外部脚本替换文件，然后重启
                string batPath = Path.Combine(tempDir, "update.bat");
                string exePath = Path.Combine(appDir, "MusicalNoteLauncher.exe");

                WriteUpdateScript(batPath, extractDir, appDir, exePath, Process.GetCurrentProcess().Id);

                progressCallback?.Invoke(new UpdateProgress
                {
                    Status = "即将重启应用更新...",
                    ProgressPercent = 100,
                });

                // 启动更新脚本并退出当前进程
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });

                Logger.Info($"[UpdateService] 更新脚本已启动: {batPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[UpdateService] 下载/安装失败: {ex.Message}", ex);
                progressCallback?.Invoke(new UpdateProgress
                {
                    Status = $"更新失败: {ex.Message}",
                    ProgressPercent = 0,
                });
                return false;
            }
        }

        /// <summary>
        /// 计算文件 SHA256
        /// </summary>
        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// 生成用于替换文件并重启的 bat 脚本
        /// </summary>
        private static void WriteUpdateScript(
            string batPath, string sourceDir, string targetDir, string exePath, int launcherPid)
        {
            string script = $@"@echo off
chcp 65001 > nul
echo 等待启动器退出...
:waitloop
tasklist /fi ""PID eq {launcherPid}"" 2>nul | find ""{launcherPid}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak > nul
    goto waitloop
)

echo 正在更新文件...
xcopy ""{sourceDir}\*"" ""{targetDir}"" /E /Y /H /R /Q

echo 清理临时文件...
rmdir /S /Q ""{Path.Combine(Path.GetTempPath(), "MNL_Update")}""

echo 更新完成，正在重启启动器...
start """" ""{exePath}""
exit
";
            File.WriteAllText(batPath, script, System.Text.Encoding.UTF8);
        }
    }
}
