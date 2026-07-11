using System;

using System.Collections.Generic;

using System.IO;

using System.IO.Compression;

using System.Linq;

using System.Net.Http;

using System.Text.Json;

using System.Text.RegularExpressions;

using System.Threading;

using System.Threading.Tasks;

using System.Xml;

namespace MusicalNoteLauncher.Core

{

    public class NeoForgeInstaller

    {

        private readonly string _minecraftPath;

        private readonly HttpClient _httpClient;

        private static readonly string NeoForgeMavenUrl = "https://maven.neoforged.net/releases/net/neoforged/neoforge/";

        public event Action<string> StatusChanged;

        public event Action<int> ProgressChanged;

        public NeoForgeInstaller(string minecraftPath)

        {

            _minecraftPath = minecraftPath;
            _httpClient = CreateHttpClient();

        }

        private HttpClient CreateHttpClient()

        {

            var handler = new HttpClientHandler

            {

                MaxConnectionsPerServer = 20,
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            var client = new HttpClient(handler)

            {

                Timeout = TimeSpan.FromMinutes(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            return client;

        }

        public async Task<List<NeoForgeVersionInfo>> GetNeoForgeVersionsAsync(string mcVersion, CancellationToken cancellationToken = default)

        {

            var versions = new List<NeoForgeVersionInfo>();
            try

            {

                string url = $"{NeoForgeMavenUrl}maven-metadata.xml";
                Logger.Info($"[NeoForge] 请求 URL: {url}");
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)

                {

                    Logger.Warning($"[NeoForge] HTTP 失败: {(int)response.StatusCode} {response.StatusCode}");
                    return versions;

                }

                string xmlContent = await response.Content.ReadAsStringAsync();
                var allVersions = ParseMavenMetadata(xmlContent);
                Logger.Info($"[NeoForge] 解析到 {allVersions.Count} 个 NeoForge 版本");
                int matched = 0;
                foreach (var neoVersion in allVersions)

                {

                    string mappedMcVersion = MapNeoForgeVersionToMcVersion(neoVersion);
                    if (mappedMcVersion == mcVersion)

                    {

                        versions.Add(new NeoForgeVersionInfo

                        {

                            Version = neoVersion,
                            McVersion = mcVersion,
                            NeoForgeVersion = neoVersion,
                            IsRecommended = false
                        });
                        matched++;

                    }

                }

                Logger.Info($"[NeoForge] 匹配 MC {mcVersion} 的版本数: {matched}");

            }

            catch (Exception ex)

            {

                Logger.Warning($"[NeoForge] 获取版本失败: {ex.Message}");

            }

            return versions.OrderByDescending(v => ParseVersion(v.NeoForgeVersion)).ToList();

        }

        private List<string> ParseMavenMetadata(string xmlContent)

        {

            var versions = new List<string>();
            try

            {

                var doc = new XmlDocument();
                doc.LoadXml(xmlContent);
                XmlNodeList versionNodes = doc.GetElementsByTagName("version");
                foreach (XmlNode node in versionNodes)

                {

                    string v = node.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(v))
                        versions.Add(v);

                }

            }

            catch (Exception ex)

            {

                Logger.Warning($"[NeoForge] 解析 Maven metadata 失败: {ex.Message}");

                // 兜底：尝试用正则提取

                try

                {

                    var matches = Regex.Matches(xmlContent, @"<version>([^<]+)</version>");
                    foreach (Match m in matches)

                    {

                        if (m.Groups.Count > 1)

                        {

                            string v = m.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(v))
                                versions.Add(v);

                        }

                    }

                }

                catch { }

            }

            return versions;

        }

        private string MapNeoForgeVersionToMcVersion(string neoForgeVersion)

        {

            // NeoForge 版本号 -> Minecraft 版本号 映射

            var mapping = new Dictionary<string, string>

            {

                ["47"] = "1.20.1",
                ["20.2"] = "1.20.2",
                ["20.3"] = "1.20.3",
                ["20.4"] = "1.20.4",
                ["20.5"] = "1.20.5",
                ["20.6"] = "1.20.6",
                ["21.0"] = "1.21",
                ["21.1"] = "1.21.1",
                ["21.2"] = "1.21.2",
                ["21.3"] = "1.21.3",
                ["21.4"] = "1.21.4",
                ["21.5"] = "1.21.5",
            };

            // 移除可能的前缀（如 1.20.1-47.3.10）

            string cleanVersion = neoForgeVersion;
            if (cleanVersion.Contains('-'))

            {

                // 可能是 "1.20.1-47.3.10" 或 "21.3.19-beta"

                var parts = cleanVersion.Split('-');

                // 如果有 "beta"/"alpha"，移除它们

                if (parts.Length > 1 && (parts[1] == "beta" || parts[1] == "alpha" || parts[1].StartsWith("beta") || parts[1].StartsWith("alpha")))

                {

                    cleanVersion = parts[0];

                }

                else if (parts.Length > 1 && parts[0].Length <= 6 && parts[0].StartsWith("1."))

                {

                    // 形如 "1.20.1-47.3.10"，取后一部分

                    cleanVersion = parts[1];

                }

            }

            foreach (var kv in mapping)

            {

                if (cleanVersion.StartsWith(kv.Key + "."))

                {

                    return kv.Value;

                }

            }

            return null;

        }

        private Version ParseVersion(string version)

        {

            try

            {

                string clean = version.Split('-')[0];
                return new Version(clean);

            }

            catch

            {

                return new Version(0, 0);

            }

        }

        public async Task<NeoForgeVersionInfo> GetRecommendedVersionAsync(string mcVersion, CancellationToken cancellationToken = default)

        {

            var versions = await GetNeoForgeVersionsAsync(mcVersion, cancellationToken);
            return versions.FirstOrDefault(v => !v.NeoForgeVersion.Contains("-beta") && !v.NeoForgeVersion.Contains("-alpha"))
                ?? versions.FirstOrDefault();

        }

        public async Task<bool> InstallNeoForgeAsync(string mcVersion, NeoForgeVersionInfo neoForgeVersion, CancellationToken cancellationToken = default)

        {

            try

            {

                StatusChanged?.Invoke($"开始安装 NeoForge {neoForgeVersion.Version}...");

                // 1. 下载 NeoForge 安装包

                string installerPath = await DownloadNeoForgeInstaller(neoForgeVersion, cancellationToken);
                if (string.IsNullOrEmpty(installerPath))

                {

                    StatusChanged?.Invoke("下载 NeoForge 安装包失败");
                    return false;

                }

                ProgressChanged?.Invoke(20);

                // 2. 提取安装包内容

                StatusChanged?.Invoke("提取 NeoForge 安装包...");
                string extractDir = Path.Combine(Path.GetTempPath(), $"neoforge-install-{Guid.NewGuid()}");
                await ExtractInstallerAsync(installerPath, extractDir, cancellationToken);
                ProgressChanged?.Invoke(40);

                // 3. 创建 NeoForge 版本目录

                StatusChanged?.Invoke("创建 NeoForge 版本目录...");
                string neoForgeVersionDir = Path.Combine(_minecraftPath, "versions", neoForgeVersion.VersionId);
                if (!Directory.Exists(neoForgeVersionDir))
                    Directory.CreateDirectory(neoForgeVersionDir);
                ProgressChanged?.Invoke(50);

                // 4. 生成版本 JSON

                StatusChanged?.Invoke("生成版本配置...");
                await GenerateVersionJson(mcVersion, neoForgeVersion, neoForgeVersionDir, extractDir, cancellationToken);
                ProgressChanged?.Invoke(70);

                // 5. 创建 mods 文件夹

                string modsDir = Path.Combine(_minecraftPath, "mods");
                if (!Directory.Exists(modsDir))
                    Directory.CreateDirectory(modsDir);
                ProgressChanged?.Invoke(80);

                // 6. 清理临时文件

                File.Delete(installerPath);
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);
                ProgressChanged?.Invoke(90);

                // 7. 验证安装

                if (await VerifyInstallation(neoForgeVersion.VersionId))

                {

                    StatusChanged?.Invoke($"NeoForge {neoForgeVersion.Version} 安装完成！");
                    ProgressChanged?.Invoke(100);
                    return true;

                }

                else

                {

                    StatusChanged?.Invoke("NeoForge 安装验证失败");
                    return false;

                }

            }

            catch (Exception ex)

            {

                StatusChanged?.Invoke($"安装失败: {ex.Message}");
                Logger.Error($"[NeoForge] 安装异常: {ex}");
                return false;

            }

        }

        private async Task<string> DownloadNeoForgeInstaller(NeoForgeVersionInfo neoForgeVersion, CancellationToken cancellationToken)

        {

            string fileName = $"neoforge-{neoForgeVersion.NeoForgeVersion}-installer.jar";
            string savePath = Path.Combine(Path.GetTempPath(), fileName);
            try

            {

                string url = $"{NeoForgeMavenUrl}{neoForgeVersion.NeoForgeVersion}/{fileName}";
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

            catch { }
            return null;

        }

        private async Task ExtractInstallerAsync(string installerPath, string extractDir, CancellationToken cancellationToken)

        {

            await Task.Run(() =>

            {

                using (var zip = ZipFile.OpenRead(installerPath))

                {

                    foreach (var entry in zip.Entries)

                    {

                        cancellationToken.ThrowIfCancellationRequested();
                        string entryPath = Path.Combine(extractDir, entry.FullName);
                        if (entry.FullName.EndsWith("/"))

                        {

                            Directory.CreateDirectory(entryPath);

                        }

                        else

                        {

                            Directory.CreateDirectory(Path.GetDirectoryName(entryPath));
                            entry.ExtractToFile(entryPath, true);

                        }

                    }

                }

            }, cancellationToken);

        }

        private async Task GenerateVersionJson(string mcVersion, NeoForgeVersionInfo neoForgeVersion, string versionDir, string extractDir, CancellationToken cancellationToken)

        {

            await Task.Run(() =>

            {

                var neoForgeJson = new JsonDocumentBuilder();
                neoForgeJson.AddString("id", neoForgeVersion.VersionId);
                neoForgeJson.AddString("inheritsFrom", mcVersion);
                neoForgeJson.AddString("type", "release");
                neoForgeJson.AddString("mainClass", "net.neoforged.neoforge.launch.Main");
                var librariesList = new List<JsonElement>();

                // 读取 installer 中的 libraries

                string librariesFile = Path.Combine(extractDir, "libraries.json");
                if (File.Exists(librariesFile))

                {

                    string libsContent = File.ReadAllText(librariesFile);

                    using (JsonDocument libDoc = JsonDocument.Parse(libsContent))

                    {

                        foreach (var lib in libDoc.RootElement.EnumerateArray())

                        {

                            librariesList.Add(lib.Clone());

                        }

                    }

                }

                // 读取 version.json 中的 libraries

                string versionJsonFile = Path.Combine(extractDir, "version.json");
                if (File.Exists(versionJsonFile))

                {

                    string versionContent = File.ReadAllText(versionJsonFile);

                    using (JsonDocument verDoc = JsonDocument.Parse(versionContent))

                    {

                        if (verDoc.RootElement.TryGetProperty("libraries", out var libs))

                        {

                            foreach (var lib in libs.EnumerateArray())

                            {

                                librariesList.Add(lib.Clone());

                            }

                        }

                        // 合并 arguments

                        if (verDoc.RootElement.TryGetProperty("arguments", out var args))

                        {

                            neoForgeJson.Add("arguments", args.Clone());

                        }

                    }

                }

                neoForgeJson.Add("libraries", JsonSerializer.SerializeToElement(librariesList));
                string jsonPath = Path.Combine(versionDir, $"{neoForgeVersion.VersionId}.json");
                File.WriteAllText(jsonPath, neoForgeJson.ToString());

                // 复制 neoforge jar 到版本目录

                string neoForgeJarName = $"neoforge-{neoForgeVersion.NeoForgeVersion}.jar";
                string sourceJar = Path.Combine(extractDir, "maven", "net", "neoforged", "neoforge", neoForgeVersion.NeoForgeVersion, neoForgeJarName);
                string destJar = Path.Combine(versionDir, neoForgeJarName);
                if (File.Exists(sourceJar))

                {

                    File.Copy(sourceJar, destJar, true);

                }

            }, cancellationToken);

        }

        private async Task<bool> VerifyInstallation(string versionId)

        {

            await Task.Delay(100);
            string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
            string jsonPath = Path.Combine(versionDir, $"{versionId}.json");
            return Directory.Exists(versionDir) && File.Exists(jsonPath);

        }

        public bool IsNeoForgeInstalled(string versionId)

        {

            string versionDir = Path.Combine(_minecraftPath, "versions", versionId);
            return Directory.Exists(versionDir);

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

            public JsonElement Build()

            {

                string json = JsonSerializer.Serialize(_elements);

                using (var doc = JsonDocument.Parse(json))

                {

                    return doc.RootElement.Clone();

                }

            }

            public override string ToString()

            {

                return JsonSerializer.Serialize(_elements, new JsonSerializerOptions { WriteIndented = true });

            }

        }

    }

    public class NeoForgeVersionInfo

    {

        public string Version { get; set; }

        public string McVersion { get; set; }

        public string NeoForgeVersion { get; set; }

        public bool IsRecommended { get; set; }

        public string VersionId => $"{McVersion}-neoforge-{NeoForgeVersion}";

        public string DisplayVersion => $"NeoForge {NeoForgeVersion}";

        public string LoaderVersion => NeoForgeVersion;

        public override string ToString()

        {

            return $"{McVersion} + NeoForge {NeoForgeVersion}";

        }

    }

}
