using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class VersionRepairService
    {
        private readonly string _minecraftPath;
        private readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        public event Action<string> StatusChanged;
        public event Action<int> ProgressChanged;
        public event Action<string, string> RepairCompleted;
        public event Action<string, string> RepairFailed;
        public event Action<int, string> DownloadProgressChanged;
        public event Action<DownloadProgressInfo> DownloadProgressInfoChanged;

        private static readonly string[] _libraryMirrors = new[]
        {
            "https://bmclapi2.bangbang93.com/libraries/",
            "https://libraries.minecraft.net/"
        };

        private static readonly string[] _versionMirrors = new[]
        {
            "https://bmclapi2.bangbang93.com/version/",
            "https://launchermeta.mojang.com/"
        };

        private const int MaxRetryAttempts = 3;

        private long _lastBytesReceived = 0;
        private DateTime _lastProgressTime = DateTime.Now;

        public VersionRepairService(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
            ConfigureSslAndProtocol();
        }

        private void ConfigureSslAndProtocol()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            ServicePointManager.Expect100Continue = false;
        }

        public async Task<RepairResult> RepairVersionAsync(string versionId, CancellationToken cancellationToken = default)
        {
            var result = new RepairResult { VersionId = versionId };

            try
            {
                StatusChanged?.Invoke($"开始校验版本 {versionId} 的完整性...");

                var versionInfo = LoadVersionInfo(versionId);
                if (versionInfo.ValueKind == JsonValueKind.Undefined)
                {
                    result.ErrorMessage = "无法读取版本信息";
                    return result;
                }

                var missingLibraries = await CheckLibrariesAsync(versionId, versionInfo, cancellationToken);
                var missingNatives = await CheckNativesAsync(versionId, versionInfo, cancellationToken);
                var missingJar = await CheckMainJarAsync(versionId, versionInfo, cancellationToken);

                result.MissingLibraries = missingLibraries;
                result.MissingNatives = missingNatives;
                result.MissingJar = missingJar;

                int totalMissing = missingLibraries.Count + missingNatives.Count + (missingJar ? 1 : 0);
                if (totalMissing > 0)
                {
                    StatusChanged?.Invoke($"发现 {totalMissing} 个缺失文件，开始修复...");

                    int repaired = 0;
                    int total = totalMissing;

                    if (missingLibraries.Count > 0)
                    {
                        StatusChanged?.Invoke($"[修复] 正在下载 {missingLibraries.Count} 个缺失的依赖库...");
                        await DownloadLibrariesAsync(missingLibraries, (progress) =>
                        {
                            int overallProgress = (int)((repaired + progress * 0.3) / total * 100);
                            ProgressChanged?.Invoke(overallProgress);
                            DownloadProgressChanged?.Invoke(overallProgress, $"下载依赖库: {progress:F1}%");
                        }, cancellationToken);
                        repaired += missingLibraries.Count;
                    }

                    if (missingNatives.Count > 0)
                    {
                        StatusChanged?.Invoke($"[修复] 正在下载 {missingNatives.Count} 个缺失的natives库...");
                        await DownloadLibrariesAsync(missingNatives, (progress) =>
                        {
                            int overallProgress = (int)((repaired + progress * 0.2) / total * 100);
                            ProgressChanged?.Invoke(overallProgress);
                            DownloadProgressChanged?.Invoke(overallProgress, $"下载natives: {progress:F1}%");
                        }, cancellationToken);
                        repaired += missingNatives.Count;
                    }

                    if (missingJar)
                    {
                        StatusChanged?.Invoke("[修复] 正在下载主JAR文件...");
                        await DownloadMainJarAsync(versionId, versionInfo, (progress) =>
                        {
                            int overallProgress = (int)((repaired + progress * 0.5) / total * 100);
                            ProgressChanged?.Invoke(overallProgress);
                            DownloadProgressChanged?.Invoke(overallProgress, $"下载游戏JAR: {progress:F1}%");
                        }, cancellationToken);
                    }

                    StatusChanged?.Invoke("[修复] 正在重新提取natives文件...");
                    await ReExtractNativesAsync(versionId, versionInfo, cancellationToken);

                    StatusChanged?.Invoke("[修复] 正在校验完整性...");
                    bool integrityOk = await ValidateIntegrityAsync(versionId, versionInfo, cancellationToken);
                    if (!integrityOk)
                    {
                        result.ErrorMessage = "完整性校验失败";
                        StatusChanged?.Invoke("[错误] 完整性校验失败");
                        return result;
                    }

                    ProgressChanged?.Invoke(100);
                    StatusChanged?.Invoke($"版本 {versionId} 修复完成！");
                    result.IsSuccess = true;
                    RepairCompleted?.Invoke(versionId, $"修复完成，已下载 {totalMissing} 个文件");
                }
                else
                {
                    StatusChanged?.Invoke($"版本 {versionId} 完整性校验通过，无需修复");
                    result.IsSuccess = true;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                StatusChanged?.Invoke($"[错误] 修复失败: {ex.Message}");
                RepairFailed?.Invoke(versionId, ex.Message);
            }

            return result;
        }

        public async Task<VersionDownloadResult> DownloadVersionAsync(string versionId, CancellationToken cancellationToken = default)
        {
            var result = new VersionDownloadResult { VersionId = versionId };

            try
            {
                StatusChanged?.Invoke($"开始下载版本 {versionId}...");

                string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
                if (!Directory.Exists(versionDir))
                {
                    Directory.CreateDirectory(versionDir);
                }

                string jsonFile = Path.Combine(versionDir, $"{versionId}.json");
                string jarFile = Path.Combine(versionDir, $"{versionId}.jar");

                string versionUrl = await GetVersionDownloadUrlAsync(versionId, cancellationToken);

                ProgressChanged?.Invoke(0);
                DownloadProgressChanged?.Invoke(0, "下载JSON配置: 0%");
                StatusChanged?.Invoke("[1/7] 下载JSON配置文件...");
                await DownloadFileWithRetryAsync(versionUrl, jsonFile, 0, 5, cancellationToken);
                ProgressChanged?.Invoke(5);
                DownloadProgressChanged?.Invoke(5, "JSON下载完成");

                var versionInfo = LoadVersionInfo(versionId);
                if (versionInfo.ValueKind == JsonValueKind.Undefined)
                {
                    result.ErrorMessage = "无法解析版本JSON";
                    return result;
                }

                var libraries = await GetAllLibrariesToDownloadAsync(versionId, versionInfo, cancellationToken);
                StatusChanged?.Invoke($"[2/7] 解析到 {libraries.Count} 个依赖库");

                int libCompleted = 0;
                int libTotal = libraries.Count;
                foreach (var lib in libraries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await DownloadFileWithRetryAsync(lib.DownloadUrl, lib.FullPath, 5, 40, cancellationToken);
                    libCompleted++;
                    int progress = 5 + (int)((double)libCompleted / libTotal * 35);
                    ProgressChanged?.Invoke(progress);
                    DownloadProgressChanged?.Invoke(progress, $"[3/7] 下载依赖库: {libCompleted}/{libTotal}");
                }
                ProgressChanged?.Invoke(40);
                DownloadProgressChanged?.Invoke(40, "依赖库下载完成");

                var natives = await GetNativesToDownloadAsync(versionId, versionInfo, cancellationToken);
                StatusChanged?.Invoke($"[4/7] 下载并解压 {natives.Count} 个natives库...");
                int nativeCompleted = 0;
                int nativeTotal = natives.Count;
                foreach (var native in natives)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await DownloadFileWithRetryAsync(native.DownloadUrl, native.FullPath, 40, 55, cancellationToken);
                    nativeCompleted++;
                    int progress = 40 + (int)((double)nativeCompleted / nativeTotal * 15);
                    ProgressChanged?.Invoke(progress);
                    DownloadProgressChanged?.Invoke(progress, $"[5/7] 下载natives: {nativeCompleted}/{nativeTotal}");
                }
                await ReExtractNativesAsync(versionId, versionInfo, cancellationToken);
                ProgressChanged?.Invoke(55);
                DownloadProgressChanged?.Invoke(55, "Natives解压完成");

                string jarUrl = await GetJarDownloadUrlAsync(versionInfo, cancellationToken);
                StatusChanged?.Invoke("[6/7] 下载主JAR文件...");
                await DownloadFileWithRetryAsync(jarUrl, jarFile, 55, 100, cancellationToken);
                ProgressChanged?.Invoke(100);
                DownloadProgressChanged?.Invoke(100, "[7/7] 下载完成!");

                StatusChanged?.Invoke($"版本 {versionId} 下载完成！");
                result.IsSuccess = true;
                result.TotalLibraries = libraries.Count;
                result.TotalNatives = natives.Count;
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "下载已取消";
                StatusChanged?.Invoke("[警告] 下载已取消");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                StatusChanged?.Invoke($"[错误] 下载失败: {ex.Message}");
                Logger.Error($"[下载] 版本 {versionId} 下载失败: {ex.Message}");
            }

            return result;
        }

        private async Task<string> GetVersionDownloadUrlAsync(string versionId, CancellationToken cancellationToken)
        {
            foreach (string mirror in _versionMirrors)
            {
                try
                {
                    string url = $"{mirror}{versionId}/json";
                    using (var client = new WebClient())
                    {
                        client.Headers.Add(HttpRequestHeader.UserAgent, _userAgent);
                        await client.DownloadStringTaskAsync(new Uri(url));
                        return url;
                    }
                }
                catch { }
            }
            throw new Exception($"无法获取版本 {versionId} 的下载地址");
        }

        private async Task<string> GetJarDownloadUrlAsync(JsonElement versionInfo, CancellationToken cancellationToken)
        {
            JsonElement effectiveInfo = versionInfo;
            string parentId = GetInheritsFrom(versionInfo);
            if (!string.IsNullOrEmpty(parentId))
            {
                JsonElement parentInfo = LoadVersionInfo(parentId);
                if (parentInfo.ValueKind != JsonValueKind.Undefined)
                {
                    effectiveInfo = parentInfo;
                }
            }

            if (effectiveInfo.TryGetProperty("downloads", out JsonElement downloads) &&
                downloads.TryGetProperty("client", out JsonElement client) &&
                client.TryGetProperty("url", out JsonElement urlElement))
            {
                return urlElement.GetString();
            }
            throw new Exception("无法从JSON中获取JAR下载链接");
        }

        private async Task<List<LibraryInfo>> GetAllLibrariesToDownloadAsync(string versionId, JsonElement versionInfo, CancellationToken cancellationToken)
        {
            var libraries = new List<LibraryInfo>();

            if (!versionInfo.TryGetProperty("libraries", out JsonElement libs))
                return libraries;

            await Task.Run(() =>
            {
                foreach (JsonElement lib in libs.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!IsLibraryApplicable(lib))
                        continue;

                    string libPath = GetLibraryPath(lib);
                    if (string.IsNullOrEmpty(libPath))
                        continue;

                    string fullPath = Path.Combine(_minecraftPath, "libraries", libPath);
                    string url = GetLibraryDownloadUrl(libPath);

                    libraries.Add(new LibraryInfo
                    {
                        Path = libPath,
                        FullPath = fullPath,
                        DownloadUrl = url
                    });
                }
            }, cancellationToken);

            return libraries;
        }

        private async Task<List<LibraryInfo>> GetNativesToDownloadAsync(string versionId, JsonElement versionInfo, CancellationToken cancellationToken)
        {
            var natives = new List<LibraryInfo>();

            if (!versionInfo.TryGetProperty("libraries", out JsonElement libs))
                return natives;

            string nativeClassifier = GetNativeClassifier(versionInfo);

            await Task.Run(() =>
            {
                foreach (JsonElement lib in libs.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!lib.TryGetProperty("downloads", out JsonElement downloads) ||
                        !downloads.TryGetProperty("classifiers", out JsonElement classifiers))
                        continue;

                    if (!classifiers.TryGetProperty(nativeClassifier, out JsonElement native))
                        continue;

                    string path = native.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
                    if (string.IsNullOrEmpty(path))
                        continue;

                    string fullPath = Path.Combine(_minecraftPath, "libraries", path);
                    string url = GetLibraryDownloadUrl(path);

                    natives.Add(new LibraryInfo
                    {
                        Path = path,
                        FullPath = fullPath,
                        DownloadUrl = url
                    });
                }
            }, cancellationToken);

            return natives;
        }

        private async Task<bool> DownloadFileWithRetryAsync(string url, string savePath, int progressStart, int progressEnd, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(url))
            {
                Logger.Warning($"[下载] URL为空，跳过: {savePath}");
                return false;
            }

            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Exception lastException = null;

            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    foreach (string mirror in _libraryMirrors)
                    {
                        try
                        {
                            string fullUrl = url.StartsWith("http") ? url : mirror + url;

                            using (var client = new WebClient())
                            {
                                client.Headers.Add(HttpRequestHeader.UserAgent, _userAgent);
                                
                                client.DownloadProgressChanged += (sender, e) =>
                                {
                                    if (e.TotalBytesToReceive > 0)
                                    {
                                        double fileProgress = (double)e.BytesReceived / e.TotalBytesToReceive;
                                        int overallProgress = progressStart + (int)(fileProgress * (progressEnd - progressStart));
                                        
                                        DateTime now = DateTime.Now;
                                        TimeSpan timeDiff = now - _lastProgressTime;
                                        long bytesDiff = e.BytesReceived - _lastBytesReceived;
                                        
                                        double speedBps = 0;
                                        if (timeDiff.TotalSeconds > 0 && bytesDiff > 0)
                                        {
                                            speedBps = bytesDiff / timeDiff.TotalSeconds;
                                        }
                                        
                                        _lastProgressTime = now;
                                        _lastBytesReceived = e.BytesReceived;
                                        
                                        string downloadedSize = FileSizeFormatter.FormatFileSize(e.BytesReceived);
                                        string totalSize = FileSizeFormatter.FormatFileSize(e.TotalBytesToReceive);
                                        string speed = FileSizeFormatter.FormatSpeed(speedBps);
                                        string fileName = Path.GetFileName(savePath);
                                        
                                        DownloadProgressInfoChanged?.Invoke(new DownloadProgressInfo
                                        {
                                            Progress = (double)overallProgress,
                                            DownloadedBytes = e.BytesReceived,
                                            TotalBytes = e.TotalBytesToReceive,
                                            DownloadedSize = downloadedSize,
                                            TotalSize = totalSize,
                                            Speed = speed,
                                            SpeedBytesPerSecond = speedBps,
                                            CurrentFile = fileName
                                        });
                                        
                                        ProgressChanged?.Invoke(overallProgress);
                                        DownloadProgressChanged?.Invoke(overallProgress, $"正在下载: {fileName} ({downloadedSize}/{totalSize} @ {speed})");
                                    }
                                };

                                var downloadTask = client.DownloadFileTaskAsync(new Uri(fullUrl), savePath);
                                var completedTask = await Task.WhenAny(downloadTask, Task.Delay(-1, cancellationToken));
                                if (completedTask != downloadTask)
                                {
                                    throw new OperationCanceledException(cancellationToken);
                                }
                                await downloadTask;
                                
                                Logger.Info($"[下载] 成功下载: {Path.GetFileName(savePath)} (尝试 {attempt})");
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"[下载] 从 {mirror} 下载失败 (尝试 {attempt}): {ex.Message}");
                            lastException = ex;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[下载] 下载异常 (尝试 {attempt}): {ex.Message}");
                    lastException = ex;
                }

                if (attempt < MaxRetryAttempts)
                {
                    await Task.Delay(1000 * attempt, cancellationToken);
                }
            }

            Logger.Error($"[下载] 下载失败 {savePath}: {lastException?.Message}");
            return false;
        }

        private string GetInheritsFrom(JsonElement versionInfo)
        {
            if (versionInfo.TryGetProperty("inheritsFrom", out JsonElement inheritsElement))
            {
                string value = inheritsElement.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            return null;
        }

        private async Task<bool> CheckMainJarAsync(string versionId, JsonElement versionInfo, CancellationToken cancellationToken)
        {
            string jarFile = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.jar");
            if (File.Exists(jarFile))
            {
                return await Task.FromResult(false);
            }

            string parentId = GetInheritsFrom(versionInfo);
            if (!string.IsNullOrEmpty(parentId))
            {
                string parentJar = Path.Combine(_minecraftPath, "versions", parentId, $"{parentId}.jar");
                if (File.Exists(parentJar))
                {
                    return await Task.FromResult(false);
                }

                JsonElement parentInfo = LoadVersionInfo(parentId);
                if (parentInfo.ValueKind != JsonValueKind.Undefined)
                {
                    return await CheckMainJarAsync(parentId, parentInfo, cancellationToken);
                }
            }

            return await Task.FromResult(true);
        }

        private async Task DownloadMainJarAsync(string versionId, JsonElement versionInfo, Action<double> progressCallback, CancellationToken cancellationToken)
        {
            JsonElement effectiveInfo = versionInfo;
            string targetVersionId = versionId;
            string parentId = GetInheritsFrom(versionInfo);
            if (!string.IsNullOrEmpty(parentId))
            {
                JsonElement parentInfo = LoadVersionInfo(parentId);
                if (parentInfo.ValueKind != JsonValueKind.Undefined)
                {
                    effectiveInfo = parentInfo;
                    targetVersionId = parentId;
                }
            }

            string jarFile = Path.Combine(_minecraftPath, "versions", targetVersionId, $"{targetVersionId}.jar");

            if (effectiveInfo.TryGetProperty("downloads", out JsonElement downloads) &&
                downloads.TryGetProperty("client", out JsonElement client) &&
                client.TryGetProperty("url", out JsonElement urlElement))
            {
                string url = urlElement.GetString();
                await DownloadFileWithRetryAsync(url, jarFile, 55, 100, cancellationToken);
            }
        }

        private async Task<List<LibraryInfo>> CheckLibrariesAsync(string versionId, JsonElement versionInfo, CancellationToken cancellationToken)
        {
            var missingLibraries = new List<LibraryInfo>();

            if (!versionInfo.TryGetProperty("libraries", out JsonElement libraries))
                return missingLibraries;

            await Task.Run(() =>
            {
                foreach (JsonElement library in libraries.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!IsLibraryApplicable(library))
                        continue;

                    string libPath = GetLibraryPath(library);
                    if (string.IsNullOrEmpty(libPath))
                        continue;

                    string fullPath = Path.Combine(_minecraftPath, "libraries", libPath);
                    string url = GetLibraryDownloadUrl(libPath);

                    if (!File.Exists(fullPath))
                    {
                        missingLibraries.Add(new LibraryInfo
                        {
                            Path = libPath,
                            FullPath = fullPath,
                            DownloadUrl = url
                        });
                        Logger.Warning($"[校验] 缺失库文件: {libPath}");
                    }
                }
            }, cancellationToken);

            return missingLibraries;
        }

        private bool IsLibraryApplicable(JsonElement library)
        {
            if (!library.TryGetProperty("rules", out JsonElement rulesElement))
                return true;

            bool allow = true;

            foreach (JsonElement rule in rulesElement.EnumerateArray())
            {
                if (!rule.TryGetProperty("action", out JsonElement actionElement))
                    continue;

                string action = actionElement.GetString();

                if (rule.TryGetProperty("os", out JsonElement osElement))
                {
                    if (osElement.TryGetProperty("name", out JsonElement osNameElement))
                    {
                        string osName = osNameElement.GetString();
                        if (osName != "windows")
                        {
                            if (action == "allow")
                                allow = false;
                            else if (action == "disallow")
                                allow = true;
                        }
                    }
                    else if (osElement.TryGetProperty("arch", out JsonElement archElement))
                    {
                        string arch = archElement.GetString();
                        if (arch != "x86" && arch != "x64")
                        {
                            allow = false;
                        }
                    }
                }
                else
                {
                    allow = action == "allow";
                }
            }

            return allow;
        }

        private async Task<List<LibraryInfo>> CheckNativesAsync(string versionId, JsonElement versionInfo, CancellationToken cancellationToken)
        {
            var missingNatives = new List<LibraryInfo>();

            if (!versionInfo.TryGetProperty("libraries", out JsonElement libraries))
                return missingNatives;

            string nativeClassifier = GetNativeClassifier(versionInfo);

            await Task.Run(() =>
            {
                foreach (JsonElement library in libraries.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!library.TryGetProperty("downloads", out JsonElement downloads) ||
                        !downloads.TryGetProperty("classifiers", out JsonElement classifiers))
                        continue;

                    if (!classifiers.TryGetProperty(nativeClassifier, out JsonElement native))
                        continue;

                    string path = native.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
                    if (string.IsNullOrEmpty(path))
                        continue;

                    string fullPath = Path.Combine(_minecraftPath, "libraries", path);
                    string url = GetLibraryDownloadUrl(path);

                    if (!File.Exists(fullPath))
                    {
                        missingNatives.Add(new LibraryInfo
                        {
                            Path = path,
                            FullPath = fullPath,
                            DownloadUrl = url
                        });
                        Logger.Warning($"[校验] 缺失natives库: {path}");
                    }
                }
            }, cancellationToken);

            return missingNatives;
        }

        private async Task<bool> ValidateIntegrityAsync(string versionId, JsonElement versionInfo, CancellationToken cancellationToken)
        {
            string effectiveVersionId = versionId;
            JsonElement effectiveInfo = versionInfo;

            string parentId = GetInheritsFrom(versionInfo);
            if (!string.IsNullOrEmpty(parentId))
            {
                JsonElement parentInfo = LoadVersionInfo(parentId);
                if (parentInfo.ValueKind != JsonValueKind.Undefined)
                {
                    effectiveVersionId = parentId;
                    effectiveInfo = parentInfo;
                }
            }

            string versionPath = Path.Combine(_minecraftPath, "versions", effectiveVersionId);
            string nativesDir = Path.Combine(versionPath, $"{effectiveVersionId}-natives");

            if (!Directory.Exists(nativesDir))
            {
                string fallbackDir = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}-natives");
                if (Directory.Exists(fallbackDir))
                {
                    nativesDir = fallbackDir;
                }
                else
                {
                    Logger.Warning($"[校验] natives目录不存在: {nativesDir}");
                    return false;
                }
            }

            string[] dllFiles = Directory.GetFiles(nativesDir, "*.dll");
            if (dllFiles.Length == 0)
            {
                Logger.Warning($"[校验] natives目录没有.dll文件: {nativesDir}");
                return false;
            }

            foreach (string dll in dllFiles)
            {
                var fi = new FileInfo(dll);
                if (fi.Length == 0)
                {
                    Logger.Warning($"[校验] 无效的dll文件: {dll}");
                    return false;
                }
            }

            Logger.Info($"[校验] natives完整性校验通过 ({dllFiles.Length} 个dll文件)");
            return await Task.FromResult(true);
        }

        private async Task DownloadLibrariesAsync(List<LibraryInfo> libraries, Action<double> progressCallback, CancellationToken cancellationToken)
        {
            int total = libraries.Count;
            int completed = 0;

            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(3);

            foreach (var library in libraries)
            {
                await semaphore.WaitAsync(cancellationToken);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        bool success = await DownloadFileWithRetryAsync(library.DownloadUrl, library.FullPath, 0, 100, cancellationToken);
                        if (success)
                        {
                            Interlocked.Increment(ref completed);
                            progressCallback?.Invoke((double)completed / total * 100);
                            Logger.Info($"[下载] 已下载库文件: {library.Path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[下载] 下载库文件失败 {library.Path}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        private async Task ReExtractNativesAsync(string versionId, JsonElement versionInfo, CancellationToken cancellationToken)
        {
            string effectiveVersionId = versionId;
            JsonElement effectiveInfo = versionInfo;

            string parentId = GetInheritsFrom(versionInfo);
            if (!string.IsNullOrEmpty(parentId))
            {
                JsonElement parentInfo = LoadVersionInfo(parentId);
                if (parentInfo.ValueKind != JsonValueKind.Undefined)
                {
                    effectiveVersionId = parentId;
                    effectiveInfo = parentInfo;
                }
            }

            string versionPath = Path.Combine(_minecraftPath, "versions", effectiveVersionId);
            string nativesDir = Path.Combine(versionPath, $"{effectiveVersionId}-natives");
            nativesDir = Path.GetFullPath(nativesDir);

            await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(nativesDir))
                    {
                        foreach (string file in Directory.GetFiles(nativesDir))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                    else
                    {
                        Directory.CreateDirectory(nativesDir);
                    }

                    string nativeClassifier = GetNativeClassifier(effectiveInfo);

                    if (effectiveInfo.TryGetProperty("libraries", out JsonElement libraries))
                    {
                        foreach (JsonElement library in libraries.EnumerateArray())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (!library.TryGetProperty("downloads", out JsonElement downloads) ||
                                !downloads.TryGetProperty("classifiers", out JsonElement classifiers))
                                continue;

                            if (!classifiers.TryGetProperty(nativeClassifier, out JsonElement nativeInfo))
                                continue;

                            string path = nativeInfo.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
                            if (string.IsNullOrEmpty(path))
                                continue;

                            string libPath = Path.Combine(_minecraftPath, "libraries", path);
                            if (File.Exists(libPath))
                            {
                                ExtractZipToDirectory(libPath, nativesDir);
                            }
                            else
                            {
                                Logger.Warning($"[Natives] 找不到natives压缩包: {libPath}");
                            }
                        }
                    }

                    int dllCount = Directory.GetFiles(nativesDir, "*.dll").Length;
                    Logger.Info($"[Natives] 提取完成: {nativesDir} ({dllCount} 个dll文件)");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Natives] 重新提取失败: {ex.Message}");
                }
            }, cancellationToken);
        }

        private void ExtractZipToDirectory(string zipPath, string destDir)
        {
            try
            {
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                using (var zipArchive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in zipArchive.Entries)
                    {
                        if (!entry.FullName.Contains("/") && entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            string destFilePath = Path.Combine(destDir, entry.Name);
                            entry.ExtractToFile(destFilePath, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[解压] 解压失败 {zipPath}: {ex.Message}");
            }
        }

        private string GetLibraryPath(JsonElement library)
        {
            if (library.TryGetProperty("downloads", out JsonElement downloads) &&
                downloads.TryGetProperty("artifact", out JsonElement artifact))
            {
                return artifact.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
            }

            if (library.TryGetProperty("name", out JsonElement nameElement))
            {
                string name = nameElement.GetString();
                return GetLibraryPathFromName(name);
            }

            return null;
        }

        private string GetLibraryPathFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            string[] parts = name.Split(':');
            if (parts.Length < 2)
                return null;

            string groupId = parts[0].Replace('.', '/');
            string artifactId = parts[1];
            string version = parts.Length > 2 ? parts[2] : "1.0";
            string classifier = parts.Length > 3 ? parts[3] : "";

            string fileName = classifier.Length > 0
                ? $"{artifactId}-{version}-{classifier}.jar"
                : $"{artifactId}-{version}.jar";

            return $"{groupId}/{artifactId}/{version}/{fileName}";
        }

        private string GetLibraryDownloadUrl(string libPath)
        {
            if (string.IsNullOrEmpty(libPath))
                return null;
            return libPath.Replace('\\', '/');
        }

        private string GetNativeClassifier(JsonElement versionInfo)
        {
            string nativesKey = "natives-windows";

            bool is64Bit = Environment.Is64BitOperatingSystem;

            if (versionInfo.TryGetProperty("libraries", out JsonElement libs))
            {
                foreach (JsonElement lib in libs.EnumerateArray())
                {
                    if (lib.TryGetProperty("downloads", out var downloads) &&
                        downloads.TryGetProperty("classifiers", out var classifiers))
                    {
                        if (is64Bit)
                        {
                            if (classifiers.TryGetProperty("natives-windows-x64", out var x64Info))
                            {
                                nativesKey = "natives-windows-x64";
                                break;
                            }
                        }

                        if (classifiers.TryGetProperty("natives-windows", out var nativeInfo))
                        {
                            nativesKey = is64Bit ? "natives-windows-x64" : "natives-windows";
                            break;
                        }
                    }
                }
            }

            return nativesKey;
        }

        private JsonElement LoadVersionInfo(string versionId)
        {
            try
            {
                string jsonPath = Path.Combine(_minecraftPath, "versions", versionId, $"{versionId}.json");
                string jsonContent = File.ReadAllText(jsonPath);

                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = doc.RootElement;
                    string parentId = GetInheritsFrom(root);

                    if (string.IsNullOrEmpty(parentId))
                    {
                        return root.Clone();
                    }

                    JsonElement parentInfo = LoadVersionInfo(parentId);
                    if (parentInfo.ValueKind == JsonValueKind.Undefined)
                    {
                        return root.Clone();
                    }

                    return MergeVersionInfo(root, parentInfo, versionId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[版本信息] 读取失败: {ex.Message}");
                return default;
            }
        }

        private JsonElement MergeVersionInfo(JsonElement current, JsonElement parent, string currentVersionId)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(ms))
                {
                    writer.WriteStartObject();

                    writer.WriteString("id", currentVersionId);

                    if (current.TryGetProperty("inheritsFrom", out JsonElement inheritsElement))
                    {
                        writer.WritePropertyName("inheritsFrom");
                        inheritsElement.WriteTo(writer);
                    }

                    string[] stringProps = { "mainClass", "minecraftArguments", "type", "releaseTime", "time", "assetIndex", "assets" };
                    foreach (var prop in stringProps)
                    {
                        if (current.TryGetProperty(prop, out JsonElement curVal))
                        {
                            writer.WritePropertyName(prop);
                            curVal.WriteTo(writer);
                        }
                        else if (parent.TryGetProperty(prop, out JsonElement parVal))
                        {
                            writer.WritePropertyName(prop);
                            parVal.WriteTo(writer);
                        }
                    }

                    if (current.TryGetProperty("arguments", out JsonElement argsCur))
                    {
                        writer.WritePropertyName("arguments");
                        argsCur.WriteTo(writer);
                    }
                    else if (parent.TryGetProperty("arguments", out JsonElement argsPar))
                    {
                        writer.WritePropertyName("arguments");
                        argsPar.WriteTo(writer);
                    }

                    if (current.TryGetProperty("downloads", out JsonElement dlCur) &&
                        dlCur.TryGetProperty("client", out _))
                    {
                        writer.WritePropertyName("downloads");
                        dlCur.WriteTo(writer);
                    }
                    else if (parent.TryGetProperty("downloads", out JsonElement dlPar))
                    {
                        writer.WritePropertyName("downloads");
                        dlPar.WriteTo(writer);
                    }
                    else if (current.TryGetProperty("downloads", out JsonElement dlCurFallback))
                    {
                        writer.WritePropertyName("downloads");
                        dlCurFallback.WriteTo(writer);
                    }

                    writer.WritePropertyName("libraries");
                    writer.WriteStartArray();

                    if (parent.TryGetProperty("libraries", out JsonElement parentLibs))
                    {
                        foreach (JsonElement lib in parentLibs.EnumerateArray())
                        {
                            lib.WriteTo(writer);
                        }
                    }

                    if (current.TryGetProperty("libraries", out JsonElement currentLibs))
                    {
                        foreach (JsonElement lib in currentLibs.EnumerateArray())
                        {
                            lib.WriteTo(writer);
                        }
                    }

                    writer.WriteEndArray();

                    writer.WriteEndObject();
                }

                ms.Position = 0;
                using (JsonDocument mergedDoc = JsonDocument.Parse(ms.ToArray()))
                {
                    return mergedDoc.RootElement.Clone();
                }
            }
        }

        public bool ValidateAllLibraries(string versionId)
        {
            try
            {
                var versionInfo = LoadVersionInfo(versionId);
                if (versionInfo.ValueKind == JsonValueKind.Undefined)
                    return false;

                if (!versionInfo.TryGetProperty("libraries", out JsonElement libraries))
                    return true;

                foreach (JsonElement library in libraries.EnumerateArray())
                {
                    if (!IsLibraryApplicable(library))
                        continue;

                    string libPath = GetLibraryPath(library);
                    if (string.IsNullOrEmpty(libPath))
                        continue;

                    string fullPath = Path.Combine(_minecraftPath, "libraries", libPath);
                    if (!File.Exists(fullPath))
                    {
                        Logger.Warning($"[校验] 库文件缺失: {libPath}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[校验] 校验失败: {ex.Message}");
                return false;
            }
        }
    }

    public class LibraryInfo
    {
        public string Path { get; set; }
        public string FullPath { get; set; }
        public string DownloadUrl { get; set; }
    }

    public class RepairResult
    {
        public string VersionId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public List<LibraryInfo> MissingLibraries { get; set; } = new List<LibraryInfo>();
        public List<LibraryInfo> MissingNatives { get; set; } = new List<LibraryInfo>();
        public List<string> MissingAssets { get; set; } = new List<string>();
        public bool MissingJar { get; set; }
    }

    public class VersionDownloadResult
    {
        public string VersionId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public int TotalLibraries { get; set; }
        public int TotalNatives { get; set; }
    }
}
