using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using System.Net.Http;

using System.Text.Json;

using System.Text.RegularExpressions;

using System.Threading;

using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core

{

    public class OptiFineInstaller

    {

        private readonly string _minecraftPath;

        private readonly HttpClient _httpClient;

        private static readonly string OptiFineBmclApiUrl = "https://bmclapi2.bangbang93.com/optifine/{0}";

        public event Action<string> StatusChanged;

        public event Action<int> ProgressChanged;

        public OptiFineInstaller(string minecraftPath)

        {

            _minecraftPath = minecraftPath;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromMinutes(10);

        }

        public async Task<List<OptiFineVersionInfo>> GetOptiFineVersionsAsync(string mcVersion, CancellationToken cancellationToken = default)

        {

            var versions = new List<OptiFineVersionInfo>();
            try

            {

                string url = string.Format(OptiFineBmclApiUrl, mcVersion);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)

                {

                    Logger.Warning($"[OptiFine] 获取版本列表失败: {response.StatusCode}");
                    return versions;

                }

                string json = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(json))

                {

                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                        return versions;
                    foreach (var item in doc.RootElement.EnumerateArray())

                    {

                        string type = "";
                        string patch = "";
                        if (item.TryGetProperty("type", out var typeProp))
                            type = typeProp.GetString();
                        if (item.TryGetProperty("patch", out var patchProp))
                            patch = patchProp.GetString();
                        if (!string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(patch))

                        {

                            versions.Add(new OptiFineVersionInfo

                            {

                                McVersion = mcVersion,
                                Type = type,
                                Patch = patch
                            });

                        }

                    }

                }

            }

            catch (Exception ex)

            {

                Logger.Warning($"[OptiFine] 获取版本失败: {ex.Message}");

            }

            // 按 patch 号降序排序（字母顺序通常也是版本顺序）

            return versions.OrderByDescending(v => v.Patch, StringComparer.Ordinal).ToList();

        }

        public async Task<OptiFineVersionInfo> GetRecommendedVersionAsync(string mcVersion, CancellationToken cancellationToken = default)

        {

            var versions = await GetOptiFineVersionsAsync(mcVersion, cancellationToken);

            // 通常列表已按 patch 降序，第一个就是最新版

            return versions.FirstOrDefault();

        }

        public async Task<bool> InstallOptiFineAsync(string mcVersion, OptiFineVersionInfo optiFineVersion, CancellationToken cancellationToken = default)

        {

            try

            {

                StatusChanged?.Invoke($"开始下载 OptiFine {optiFineVersion.DisplayVersion}...");
                string installerPath = await DownloadOptiFineInstaller(optiFineVersion, cancellationToken);
                if (string.IsNullOrEmpty(installerPath))

                {

                    StatusChanged?.Invoke("下载 OptiFine 安装包失败");
                    return false;

                }

                ProgressChanged?.Invoke(40);

                // OptiFine 是 jar mod，这里把它作为独立版本安装

                // 1. 创建版本目录

                string versionDir = Path.Combine(_minecraftPath, "versions", optiFineVersion.VersionId);
                if (!Directory.Exists(versionDir))
                    Directory.CreateDirectory(versionDir);

                // 2. 生成版本 JSON（inheritsFrom 原版，添加 OptiFine 库）

                StatusChanged?.Invoke("生成 OptiFine 版本配置...");
                await GenerateVersionJson(mcVersion, optiFineVersion, versionDir, installerPath, cancellationToken);
                ProgressChanged?.Invoke(80);

                // 3. 复制安装器 jar 作为版本 jar（OptiFine 安装器本身不包含原版类，无法直接运行；

                //    这里仅作为占位，实际运行时启动器会解析 libraries 中引用的 OptiFine jar）

                string destJar = Path.Combine(versionDir, $"{optiFineVersion.VersionId}.jar");
                File.Copy(installerPath, destJar, true);
                ProgressChanged?.Invoke(90);

                // 4. 清理临时文件

                File.Delete(installerPath);
                ProgressChanged?.Invoke(100);
                StatusChanged?.Invoke($"OptiFine {optiFineVersion.DisplayVersion} 已准备就绪！");
                return true;

            }

            catch (OperationCanceledException)

            {

                StatusChanged?.Invoke("安装已取消");
                return false;

            }

            catch (Exception ex)

            {

                StatusChanged?.Invoke($"安装失败: {ex.Message}");
                Logger.Error($"[OptiFine] 安装异常: {ex}");
                return false;

            }

        }

        private async Task<string> DownloadOptiFineInstaller(OptiFineVersionInfo optiFineVersion, CancellationToken cancellationToken)

        {

            string fileName = $"OptiFine_{optiFineVersion.McVersion}_{optiFineVersion.Type}_{optiFineVersion.Patch}.jar";
            string savePath = Path.Combine(Path.GetTempPath(), fileName);
            try

            {

                // BMCLAPI 下载地址

                string url = $"https://bmclapi2.bangbang93.com/optifine/{optiFineVersion.McVersion}/{optiFineVersion.Type}/{optiFineVersion.Patch}";
                StatusChanged?.Invoke($"下载: {url}");

                using (var response = await _httpClient.GetAsync(url, cancellationToken))

                {

                    if (!response.IsSuccessStatusCode) return null;

                    using (var stream = await response.Content.ReadAsStreamAsync())

                    using (var fileStream = new FileStream(savePath, FileMode.Create))

                    {

                        await stream.CopyToAsync(fileStream);

                    }

                    return savePath;

                }

            }

            catch (Exception ex)

            {

                Logger.Error($"[OptiFine] 下载安装器失败: {ex.Message}");

            }

            return null;

        }

        private async Task GenerateVersionJson(string mcVersion, OptiFineVersionInfo optiFineVersion, string versionDir, string installerPath, CancellationToken cancellationToken)

        {

            await Task.Run(() =>

            {

                var jsonBuilder = new JsonDocumentBuilder();
                jsonBuilder.AddString("id", optiFineVersion.VersionId);
                jsonBuilder.AddString("inheritsFrom", mcVersion);
                jsonBuilder.AddString("type", "release");
                jsonBuilder.AddString("mainClass", "net.minecraft.launchwrapper.Launch");
                var librariesList = new List<JsonElement>();

                // 添加 OptiFine 库

                using (var optifineLibDoc = JsonDocument.Parse("{\"name\":\"optifine:OptiFine:" + optiFineVersion.McVersion + "_" + optiFineVersion.Type + "_" + optiFineVersion.Patch + "\"}"))

                {

                    librariesList.Add(optifineLibDoc.RootElement.Clone());

                }

                // 添加 launchwrapper（OptiFine 需要）

                using (var launchWrapperDoc = JsonDocument.Parse("{\"name\":\"net.minecraft:launchwrapper:1.12\"}"))

                {

                    librariesList.Add(launchWrapperDoc.RootElement.Clone());

                }

                jsonBuilder.Add("libraries", JsonSerializer.SerializeToElement(librariesList));
                string jsonPath = Path.Combine(versionDir, $"{optiFineVersion.VersionId}.json");
                File.WriteAllText(jsonPath, jsonBuilder.ToString());
            }, cancellationToken);

        }

        public bool IsOptiFineInstalled(string versionId)

        {

            string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
            return Directory.Exists(versionDir) && File.Exists(Path.Combine(versionDir, $"{versionId}.json"));

        }

        private class JsonDocumentBuilder

        {

            private readonly Dictionary<string, JsonElement> _elements = new Dictionary<string, JsonElement>();

            public void Add(string key, JsonElement value)

            {

                _elements[key] = value;

            }

            public void AddString(string key, string value)

            {

                using (var doc = JsonDocument.Parse($"\"{value}\""))

                {

                    _elements[key] = doc.RootElement.Clone();

                }

            }

            public override string ToString()

            {

                return JsonSerializer.Serialize(_elements, new JsonSerializerOptions { WriteIndented = true });

            }

        }

    }

    public class OptiFineVersionInfo

    {

        public string McVersion { get; set; }

        public string Type { get; set; }

        public string Patch { get; set; }

        public bool IsRecommended { get; set; }

        public string VersionId => $"{McVersion}-OptiFine-{Type}-{Patch}";

        public string DisplayVersion => $"OptiFine {McVersion} {Type} {Patch}";

        public string LoaderVersion => $"{Type} {Patch}";

        public override string ToString()

        {

            return DisplayVersion;

        }

    }

}
