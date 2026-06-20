using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class JavaDownloadService
    {
        private readonly string _minecraftPath;
        private readonly HttpClient _httpClient;
        
        // 主镜像源：BMCLAPI
        private readonly List<string> _bmclApiUrls = new List<string>
        {
            "https://bmclapi2.bangbang93.com/java",
            "https://bmclapi.bangbang93.com/java",
            "https://bmclapi.mc163.com/java",
            "https://bmclapi.talecraft.top/java"
        };

        // 备用镜像源：Adoptium/Temurin
        private readonly Dictionary<int, string> _adoptiumUrls = new Dictionary<int, string>
        {
            { 8, "https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u412-b08/OpenJDK8U-jdk_x64_windows_hotspot_8u412b08.zip" },
            { 11, "https://github.com/adoptium/temurin11-binaries/releases/download/jdk-11.0.23%2B9/OpenJDK11U-jdk_x64_windows_hotspot_11.0.23_9.zip" },
            { 16, "https://github.com/adoptium/temurin16-binaries/releases/download/jdk-16.0.2%2B7/OpenJDK16U-jdk_x64_windows_hotspot_16.0.2_7.zip" },
            { 17, "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.11%2B9/OpenJDK17U-jdk_x64_windows_hotspot_17.0.11_9.zip" },
            { 21, "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.3%2B9/OpenJDK21U-jdk_x64_windows_hotspot_21.0.3_9.zip" }
        };

        // 额外备用源：Microsoft Build of OpenJDK
        private readonly Dictionary<int, string> _microsoftUrls = new Dictionary<int, string>
        {
            { 11, "https://aka.ms/download-jdk/microsoft-jdk-11.0.23-windows-x64.zip" },
            { 17, "https://aka.ms/download-jdk/microsoft-jdk-17.0.11-windows-x64.zip" },
            { 21, "https://aka.ms/download-jdk/microsoft-jdk-21.0.3-windows-x64.zip" }
        };

        // Oracle官网下载源（需要特殊处理）
        private readonly Dictionary<int, string> _oracleUrls = new Dictionary<int, string>
        {
            { 8, "https://download.oracle.com/java/8/archive/jdk-8u421-windows-x64.zip" },
            { 11, "https://download.oracle.com/otn/java/jdk/11.0.24%2B8/fd6b5b6c/graalvm-jdk-11.0.24_windows-x64_bin.zip" },
            { 17, "https://download.oracle.com/java/17/archive/jdk-17.0.12_windows-x64_bin.zip" },
            { 21, "https://download.oracle.com/java/21/archive/jdk-21.0.4_windows-x64_bin.zip" }
        };

        public event Action<string> StatusChanged;
        public event Action<string> LogReceived;
        public event Action<double> ProgressChanged;
        public event Action<string> DownloadCompleted;
        public event Action<string, string> DownloadFailed;

        public JavaDownloadService(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
            _httpClient = CreateHttpClient();
        }

        private HttpClient CreateHttpClient()
        {
            // 设置安全协议
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            ServicePointManager.Expect100Continue = false;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 15,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(30)
            };

            // 添加完整的请求头，模拟浏览器请求，修复 403 问题
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
            client.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");
            client.DefaultRequestHeaders.Referrer = new Uri("https://www.minecraft.net/");
            client.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"128\", \"Not;A=Brand\";v=\"24\", \"Microsoft Edge\";v=\"128\"");
            client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            return client;
        }

        public string GetRecommendedJavaVersion(string minecraftVersion)
        {
            try
            {
                string[] parts = minecraftVersion.Split('.');
                if (parts.Length >= 2)
                {
                    int major = int.Parse(parts[0]);
                    int minor = int.Parse(parts[1]);

                    if (major > 1 || (major == 1 && minor >= 21))
                    {
                        return "21";
                    }
                    else if (major == 1 && minor >= 17)
                    {
                        return "17";
                    }
                    else if (major == 1 && minor >= 16)
                    {
                        return "16";
                    }
                    else if (major == 1 && minor >= 13)
                    {
                        return "11";
                    }
                    else
                    {
                        return "8";
                    }
                }
            }
            catch
            {
            }
            return "8";
        }

        private List<string> GetDownloadUrls(int javaVersion)
        {
            List<string> urls = new List<string>();
            string os = Environment.Is64BitOperatingSystem ? "windows-x64" : "windows-x86";

            // 添加 BMCLAPI 源（主源）
            foreach (string baseUrl in _bmclApiUrls)
            {
                urls.Add($"{baseUrl}/java-{javaVersion}-{os}.zip");
            }

            // 添加 Adoptium 备用源
            if (_adoptiumUrls.ContainsKey(javaVersion))
            {
                urls.Add(_adoptiumUrls[javaVersion]);
            }

            // 添加 Microsoft 备用源
            if (_microsoftUrls.ContainsKey(javaVersion))
            {
                urls.Add(_microsoftUrls[javaVersion]);
            }

            // 添加 Oracle 官网源（最后尝试）
            if (_oracleUrls.ContainsKey(javaVersion))
            {
                urls.Add(_oracleUrls[javaVersion]);
            }

            return urls;
        }

        public string GetJavaInstallDir(int javaVersion)
        {
            return Path.Combine(_minecraftPath, "java", $"java-{javaVersion}");
        }

        public async Task<string> DownloadAndInstallJavaAsync(int javaVersion, DownloadProgress progress = null, CancellationToken cancellationToken = default)
        {
            string javaDir = GetJavaInstallDir(javaVersion);
            string zipPath = Path.Combine(_minecraftPath, "java", $"java-{javaVersion}.zip");
            string javaExePath = Path.Combine(javaDir, "bin", "java.exe");

            // 如果已安装，直接返回路径
            if (File.Exists(javaExePath) && VerifyJavaInstallation(javaDir))
            {
                StatusChanged?.Invoke($"Java {javaVersion} 已安装");
                LogReceived?.Invoke($"Java路径: {javaExePath}");
                return javaExePath;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(zipPath));

                List<string> downloadUrls = GetDownloadUrls(javaVersion);
                string lastError = null;
                bool downloadSuccess = false;

                // 尝试所有镜像源
                for (int i = 0; i < downloadUrls.Count; i++)
                {
                    string url = downloadUrls[i];
                    string sourceName = GetSourceName(i, downloadUrls.Count);

                    try
                    {
                        StatusChanged?.Invoke($"正在从 {sourceName} 下载 Java {javaVersion}...");
                        LogReceived?.Invoke($"尝试第 {i + 1}/{downloadUrls.Count} 个源: {url}");

                        await DownloadFileWithRetryAsync(url, zipPath, progress, cancellationToken);

                        // 下载成功，验证文件完整性
                        if (await VerifyDownloadedFile(zipPath))
                        {
                            downloadSuccess = true;
                            break;
                        }
                        else
                        {
                            LogReceived?.Invoke($"文件校验失败，尝试下一个源");
                            File.Delete(zipPath);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        lastError = GetHttpErrorMessage(ex);
                        LogReceived?.Invoke($"从 {sourceName} 下载失败: {lastError}");

                        if (i < downloadUrls.Count - 1)
                        {
                            StatusChanged?.Invoke($"切换到备用源... ({i + 2}/{downloadUrls.Count})");
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        LogReceived?.Invoke($"从 {sourceName} 下载失败: {lastError}");

                        if (i < downloadUrls.Count - 1)
                        {
                            StatusChanged?.Invoke($"切换到备用源... ({i + 2}/{downloadUrls.Count})");
                        }
                    }
                }

                if (!downloadSuccess || !File.Exists(zipPath))
                {
                    string errorMsg = $"所有镜像源均无法下载 Java {javaVersion}";
                    if (!string.IsNullOrEmpty(lastError))
                    {
                        errorMsg += $"\n最后错误: {lastError}";
                    }
                    throw new Exception(errorMsg);
                }

                // 解压 Java 环境
                StatusChanged?.Invoke("正在解压 Java 环境...");
                LogReceived?.Invoke($"解压到: {javaDir}");

                // 确保目录为空
                if (Directory.Exists(javaDir))
                {
                    foreach (string file in Directory.GetFiles(javaDir, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                    }
                    Directory.Delete(javaDir, true);
                }
                Directory.CreateDirectory(javaDir);

                // 使用标准解压方法
                try
                {
                    ZipFile.ExtractToDirectory(zipPath, javaDir);
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke($"解压警告: {ex.Message}");
                    throw new Exception($"解压失败: {ex.Message}");
                }

                // 删除压缩包
                File.Delete(zipPath);

                // 处理嵌套目录结构
                string extractedDir = FindJavaHomeInDir(javaDir);
                if (!string.IsNullOrEmpty(extractedDir) && extractedDir != javaDir)
                {
                    foreach (string file in Directory.GetFiles(extractedDir, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.Substring(extractedDir.Length).TrimStart('\\', '/');
                        string destPath = Path.Combine(javaDir, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                        if (File.Exists(destPath))
                            File.Delete(destPath);
                        File.Move(file, destPath);
                    }
                    try { Directory.Delete(extractedDir, true); } catch { }
                }

                // 查找 java.exe
                javaExePath = Path.Combine(javaDir, "bin", "java.exe");
                if (!File.Exists(javaExePath))
                {
                    javaExePath = FindJavaExe(javaDir);
                }

                if (!File.Exists(javaExePath))
                {
                    throw new Exception("无法找到 java.exe 文件");
                }

                // 验证 Java 版本
                string javaVersionOutput = GetJavaVersionOutput(javaExePath);
                LogReceived?.Invoke($"Java 版本验证: {javaVersionOutput}");

                // 验证安装完整性
                if (!VerifyJavaInstallation(javaDir))
                {
                    throw new Exception("Java 安装验证失败，文件可能损坏");
                }

                StatusChanged?.Invoke($"Java {javaVersion} 安装完成");
                LogReceived?.Invoke($"Java 路径: {javaExePath}");
                DownloadCompleted?.Invoke(javaExePath);

                return javaExePath;
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke("下载已取消");
                throw;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Java {javaVersion} 安装失败");
                LogReceived?.Invoke($"错误: {ex.Message}");
                DownloadFailed?.Invoke(javaVersion.ToString(), ex.Message);

                // 清理残留文件
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                if (Directory.Exists(javaDir))
                {
                    try { Directory.Delete(javaDir, true); } catch { }
                }

                throw;
            }
        }

        private string GetSourceName(int index, int total)
        {
            int bmclCount = _bmclApiUrls.Count;
            int adoptiumCount = _adoptiumUrls.Count;
            int microsoftCount = _microsoftUrls.Count;

            if (index < bmclCount)
            {
                return $"BMCLAPI ({index + 1})";
            }
            else if (index < bmclCount + adoptiumCount)
            {
                return "Adoptium";
            }
            else if (index < bmclCount + adoptiumCount + microsoftCount)
            {
                return "Microsoft";
            }
            else
            {
                return "Oracle官网";
            }
        }

        private string GetHttpErrorMessage(HttpRequestException ex)
        {
            string message = ex.Message;
            
            if (message.Contains("403"))
            {
                return "403 Forbidden - 访问被拒绝，请检查网络或稍后重试";
            }
            else if (message.Contains("404"))
            {
                return "404 Not Found - 资源不存在";
            }
            else if (message.Contains("504"))
            {
                return "504 Gateway Timeout - 服务器超时";
            }
            else if (message.Contains("503"))
            {
                return "503 Service Unavailable - 服务暂时不可用";
            }
            else if (message.Contains("timeout"))
            {
                return "请求超时，请检查网络连接";
            }
            
            return message;
        }

        private async Task<bool> VerifyDownloadedFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                long fileSize = new FileInfo(filePath).Length;
                
                // 检查文件大小是否合理（至少 30MB）
                if (fileSize < 30 * 1024 * 1024)
                {
                    LogReceived?.Invoke($"文件大小过小: {fileSize} bytes，可能下载不完整");
                    return false;
                }

                // 尝试读取 ZIP 文件头验证
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] header = new byte[4];
                    await stream.ReadAsync(header, 0, 4);
                    // ZIP 文件头应该是 "PK\x03\x04"
                    if (header[0] != 0x50 || header[1] != 0x4B || header[2] != 0x03 || header[3] != 0x04)
                    {
                        LogReceived?.Invoke("文件不是有效的 ZIP 格式");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"文件验证失败: {ex.Message}");
                return false;
            }
        }

        private bool VerifyJavaInstallation(string javaDir)
        {
            try
            {
                string javaExe = Path.Combine(javaDir, "bin", "java.exe");
                string javacExe = Path.Combine(javaDir, "bin", "javac.exe");
                
                if (!File.Exists(javaExe))
                    return false;
                
                // 对于 JRE，可能没有 javac.exe
                // 检查关键文件是否存在
                string[] requiredFiles = {
                    Path.Combine(javaDir, "bin", "java.exe"),
                    Path.Combine(javaDir, "lib", "rt.jar"),
                    Path.Combine(javaDir, "lib", "jvm.cfg")
                };

                foreach (string file in requiredFiles)
                {
                    if (!File.Exists(file))
                    {
                        // 某些版本可能文件结构不同，放宽检查
                        if (!file.EndsWith("rt.jar") && !file.EndsWith("jvm.cfg"))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task DownloadFileWithRetryAsync(string url, string savePath, DownloadProgress progress, CancellationToken cancellationToken)
        {
            int retryCount = 3;
            int delayMs = 2000;

            for (int attempt = 1; attempt <= retryCount; attempt++)
            {
                try
                {
                    await DownloadFileWithProgressAsync(url, savePath, progress, cancellationToken);
                    return;
                }
                catch (HttpRequestException ex) when (IsRetryableError(ex))
                {
                    if (attempt < retryCount)
                    {
                        LogReceived?.Invoke($"下载失败 ({ex.Message})，第 {attempt}/{retryCount} 次重试...");
                        StatusChanged?.Invoke($"下载失败，正在重试 ({attempt}/{retryCount})...");
                        await Task.Delay(delayMs, cancellationToken);
                        delayMs *= 2;
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception) when (attempt < retryCount)
                {
                    LogReceived?.Invoke($"下载异常，第 {attempt}/{retryCount} 次重试...");
                    StatusChanged?.Invoke($"下载异常，正在重试 ({attempt}/{retryCount})...");
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2;
                }
            }
        }

        private bool IsRetryableError(HttpRequestException ex)
        {
            string message = ex.Message;
            return message.Contains("403") || message.Contains("503") || 
                   message.Contains("504") || message.Contains("timeout") ||
                   message.Contains("reset") || message.Contains("aborted");
        }

        private async Task DownloadFileWithProgressAsync(string url, string savePath, DownloadProgress progress, CancellationToken cancellationToken)
        {
            // 为Oracle下载创建特殊请求
            bool isOracleDownload = url.Contains("oracle.com");
            
            if (isOracleDownload)
            {
                // Oracle需要特殊的请求头
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    // 添加Oracle特有的请求头
                    request.Headers.Add("Cookie", "oraclelicense=accept-securebackup-cookie");
                    request.Headers.Referrer = new Uri("https://www.oracle.com/java/technologies/downloads/");
                    
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string errorMessage = GetHttpStatusCodeMessage(response.StatusCode);
                            throw new HttpRequestException(errorMessage);
                        }

                        await ProcessDownloadResponse(response, savePath, progress, cancellationToken);
                    }
                }
            }
            else
            {
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorMessage = GetHttpStatusCodeMessage(response.StatusCode);
                        throw new HttpRequestException(errorMessage);
                    }

                    await ProcessDownloadResponse(response, savePath, progress, cancellationToken);
                }
            }
        }

        private async Task ProcessDownloadResponse(HttpResponseMessage response, string savePath, DownloadProgress progress, CancellationToken cancellationToken)
        {
            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long downloadedBytes = 0;

            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        double progressValue = (double)downloadedBytes / totalBytes * 100;
                        ProgressChanged?.Invoke(progressValue);
                        progress?.Report(new DownloadProgressInfo
                        {
                            Progress = progressValue,
                            DownloadedBytes = downloadedBytes,
                            TotalBytes = totalBytes,
                            CurrentFile = Path.GetFileName(savePath),
                            Status = "下载中"
                        });
                    }
                }
            }
        }

        private string GetHttpStatusCodeMessage(HttpStatusCode statusCode)
        {
            switch (statusCode)
            {
                case HttpStatusCode.Forbidden:
                    return "403 Forbidden - 访问被拒绝，服务器拒绝了请求。请稍后重试或切换网络。";
                case HttpStatusCode.NotFound:
                    return "404 Not Found - 资源不存在，该 Java 版本可能已被移除。";
                case HttpStatusCode.GatewayTimeout:
                    return "504 Gateway Timeout - 服务器超时，请检查网络连接。";
                case HttpStatusCode.ServiceUnavailable:
                    return "503 Service Unavailable - 服务暂时不可用，请稍后重试。";
                case HttpStatusCode.RequestTimeout:
                    return "408 Request Timeout - 请求超时，请检查网络。";
                case HttpStatusCode.InternalServerError:
                    return "500 Internal Server Error - 服务器内部错误。";
                default:
                    return $"HTTP {(int)statusCode} - {statusCode}";
            }
        }

        private string FindJavaHomeInDir(string baseDir)
        {
            foreach (string dir in Directory.GetDirectories(baseDir))
            {
                string javaExe = Path.Combine(dir, "bin", "java.exe");
                if (File.Exists(javaExe))
                {
                    return dir;
                }
                string subResult = FindJavaHomeInDir(dir);
                if (!string.IsNullOrEmpty(subResult))
                {
                    return subResult;
                }
            }
            return null;
        }

        private string FindJavaExe(string baseDir)
        {
            foreach (string file in Directory.GetFiles(baseDir, "java.exe", SearchOption.AllDirectories))
            {
                return file;
            }
            return null;
        }

        private string GetJavaVersionOutput(string javaExePath)
        {
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = javaExePath;
                    process.StartInfo.Arguments = "-version";
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    string output = process.StandardError.ReadToEnd();
                    process.WaitForExit(3000);

                    return output.Trim();
                }
            }
            catch
            {
                return "无法验证版本";
            }
        }

        public bool IsJavaInstalled(int javaVersion)
        {
            string javaExePath = Path.Combine(GetJavaInstallDir(javaVersion), "bin", "java.exe");
            return File.Exists(javaExePath);
        }

        public string GetInstalledJavaPath(int javaVersion)
        {
            string javaExePath = Path.Combine(GetJavaInstallDir(javaVersion), "bin", "java.exe");
            if (File.Exists(javaExePath))
            {
                return javaExePath;
            }
            return null;
        }
    }
}