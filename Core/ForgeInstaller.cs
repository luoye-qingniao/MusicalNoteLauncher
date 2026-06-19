using System;

using System.Collections.Generic;

using System.IO;

using System.IO.Compression;

using System.Linq;

using System.Net.Http;

using System.Security.Cryptography;

using System.Text;

using System.Text.Json;

using System.Threading;

using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core

{

    public class ForgeInstaller

    {

        private readonly string _minecraftPath;

        private readonly HttpClient _httpClient;

        private static readonly string[] ForgeMavenUrls = new[]

        {

            "https://files.minecraftforge.net/maven/net/minecraftforge/forge/",
            "https://maven.minecraftforge.net/net/minecraftforge/forge/"
        };

        public event Action<string> StatusChanged;

        public event Action<int> ProgressChanged;

        public ForgeInstaller(string minecraftPath)

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

        public async Task<List<ForgeVersionInfo>> GetForgeVersionsAsync(string mcVersion, CancellationToken cancellationToken = default)

        {

            var versions = new List<ForgeVersionInfo>();
            foreach (var baseUrl in ForgeMavenUrls)

            {

                try

                {

                    string url = $"{baseUrl}maven-metadata.xml";
                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    if (!response.IsSuccessStatusCode) continue;
                    string xmlContent = await response.Content.ReadAsStringAsync();
                    var forgeVersions = ParseMavenMetadata(xmlContent, mcVersion);
                    foreach (var v in forgeVersions)

                    {

                        if (!versions.Any(x => x.Version == v.Version))

                        {

                            versions.Add(v);

                        }

                    }

                }

                catch { }

            }

            return versions.OrderByDescending(v => v.ForgeVersion).ToList();

        }

        private List<ForgeVersionInfo> ParseMavenMetadata(string xmlContent, string mcVersion)

        {

            var versions = new List<ForgeVersionInfo>();
            try

            {

                var lines = xmlContent.Split('\n');
                foreach (var line in lines)

                {

                    if (line.Contains("<version>") && line.Contains("</version>"))

                    {

                        string version = line.Replace("<version>", "").Replace("</version>", "").Trim();
                        if (version.StartsWith(mcVersion + "-"))

                        {

                            string forgeVersion = version.Substring(mcVersion.Length + 1);
                            versions.Add(new ForgeVersionInfo

                            {

                                Version = version,
                                McVersion = mcVersion,
                                ForgeVersion = forgeVersion,
                                IsRecommended = forgeVersion.Contains("-recommended")
                            });

                        }

                    }

                }

            }

            catch { }
            return versions;

        }

        public async Task<ForgeVersionInfo> GetRecommendedVersionAsync(string mcVersion, CancellationToken cancellationToken = default)

        {

            var versions = await GetForgeVersionsAsync(mcVersion, cancellationToken);

            // 优先推荐版本

            var recommended = versions.FirstOrDefault(v => v.IsRecommended);
            if (recommended != null) return recommended;

            // 没有推荐版本则返回最新的稳定版本

            return versions.FirstOrDefault(v => !v.ForgeVersion.Contains("-beta") && !v.ForgeVersion.Contains("-alpha")) 
                ?? versions.FirstOrDefault();

        }

        public async Task<bool> InstallForgeAsync(string mcVersion, ForgeVersionInfo forgeVersion, CancellationToken cancellationToken = default)

        {

            try

            {

                StatusChanged?.Invoke($"开始安装 Forge {forgeVersion.Version}...");

                // 1. 下载 Forge 安装包

                string installerPath = await DownloadForgeInstaller(forgeVersion, cancellationToken);
                if (string.IsNullOrEmpty(installerPath))

                {

                    StatusChanged?.Invoke("下载 Forge 安装包失败");
                    return false;

                }

                ProgressChanged?.Invoke(20);

                // 2. 提取安装包内容

                StatusChanged?.Invoke("提取 Forge 安装包...");
                string extractDir = Path.Combine(Path.GetTempPath(), $"forge-install-{Guid.NewGuid()}");
                await ExtractInstallerAsync(installerPath, extractDir, cancellationToken);
                ProgressChanged?.Invoke(40);

                // 3. 创建 Forge 版本目录

                StatusChanged?.Invoke("创建 Forge 版本目录...");
                string forgeVersionDir = Path.Combine(_minecraftPath, "versions", forgeVersion.Version);
                if (!Directory.Exists(forgeVersionDir))

                {

                    Directory.CreateDirectory(forgeVersionDir);

                }

                ProgressChanged?.Invoke(50);

                // 4. 生成版本 JSON

                StatusChanged?.Invoke("生成版本配置...");
                await GenerateVersionJson(mcVersion, forgeVersion, forgeVersionDir, extractDir, cancellationToken);
                ProgressChanged?.Invoke(70);

                // 5. 创建 mods 文件夹

                string modsDir = Path.Combine(_minecraftPath, "mods");
                if (!Directory.Exists(modsDir))

                {

                    Directory.CreateDirectory(modsDir);

                }

                ProgressChanged?.Invoke(80);

                // 6. 清理临时文件

                File.Delete(installerPath);
                if (Directory.Exists(extractDir))

                {

                    Directory.Delete(extractDir, true);

                }

                ProgressChanged?.Invoke(90);

                // 7. 验证安装

                if (await VerifyInstallation(forgeVersion.Version))

                {

                    StatusChanged?.Invoke($"Forge {forgeVersion.Version} 安装完成！");
                    ProgressChanged?.Invoke(100);
                    return true;

                }

                else

                {

                    StatusChanged?.Invoke("Forge 安装验证失败");
                    return false;

                }

            }

            catch (Exception ex)

            {

                StatusChanged?.Invoke($"安装失败: {ex.Message}");
                return false;

            }

        }

        private async Task<string> DownloadForgeInstaller(ForgeVersionInfo forgeVersion, CancellationToken cancellationToken)

        {

            string fileName = $"forge-{forgeVersion.Version}-installer.jar";
            string savePath = Path.Combine(Path.GetTempPath(), fileName);
            foreach (var baseUrl in ForgeMavenUrls)

            {

                try

                {

                    string url = $"{baseUrl}{forgeVersion.Version}/{fileName}";
                    StatusChanged?.Invoke($"下载: {url}");

                    using (var response = await _httpClient.GetAsync(url, cancellationToken))

                    {

                        if (!response.IsSuccessStatusCode) continue;

                        using (var stream = await response.Content.ReadAsStreamAsync())

                        using (var fileStream = new FileStream(savePath, FileMode.Create))

                        {

                            await stream.CopyToAsync(fileStream);

                        }

                        return savePath;

                    }

                }

                catch { }

            }

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

        private async Task GenerateVersionJson(string mcVersion, ForgeVersionInfo forgeVersion, string forgeVersionDir, string extractDir, CancellationToken cancellationToken)

        {

            await Task.Run(() =>

            {

                // 创建 Forge 版本 JSON 内容

                var forgeJson = new JsonDocumentBuilder();

                // 基本信息

                forgeJson.AddString("id", forgeVersion.Version);
                forgeJson.AddString("inheritsFrom", mcVersion);
                forgeJson.AddString("type", "release");
                forgeJson.AddString("mainClass", "net.minecraftforge.fml.loading.FMLMain");

                // 添加 libraries

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

                    }

                }

                forgeJson.Add("libraries", JsonSerializer.SerializeToElement(librariesList));

                // 添加 arguments

                var argumentsObj = new JsonDocumentBuilder();

                // JVM 参数

                var jvmArgs = new List<string>

                {

                    "-XX:+UnlockExperimentalVMOptions",
                    "-XX:+UseG1GC",
                    "-XX:G1NewSizePercent=20",
                    "-XX:G1ReservePercent=20",
                    "-XX:MaxGCPauseMillis=50",
                    "-XX:G1HeapRegionSize=32M",
                    "-Dfml.ignoreInvalidMinecraftCertificates=true",
                    "-Dfml.ignorePatchDiscrepancies=true"
                };
                argumentsObj.Add("jvm", JsonSerializer.SerializeToElement(jvmArgs));

                // Game 参数

                var gameArgs = new List<string>

                {

                    "--fml.forgeVersion", forgeVersion.ForgeVersion,
                    "--fml.mcVersion", mcVersion,
                    "--fml.forgeGroup", "net.minecraftforge",
                    "--fml.mcpVersion", "20230903.120802"
                };
                argumentsObj.Add("game", JsonSerializer.SerializeToElement(gameArgs));
                forgeJson.Add("arguments", argumentsObj.Build());

                // 写入版本 JSON

                string forgeJsonPath = Path.Combine(forgeVersionDir, $"{forgeVersion.Version}.json");
                string forgeJsonContent = forgeJson.ToString();
                File.WriteAllText(forgeJsonPath, forgeJsonContent);

                // 复制 forge jar 文件到版本目录

                string forgeJarName = $"forge-{forgeVersion.Version}.jar";
                string sourceJar = Path.Combine(extractDir, "maven", "net", "minecraftforge", "forge", forgeVersion.Version, forgeJarName);
                string destJar = Path.Combine(forgeVersionDir, forgeJarName);
                if (File.Exists(sourceJar))

                {

                    File.Copy(sourceJar, destJar, true);

                }

            }, cancellationToken);

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

        private async Task<bool> VerifyInstallation(string forgeVersion)

        {

            await Task.Delay(100);
            string versionDir = Path.Combine(_minecraftPath, "versions", forgeVersion);
            string jsonPath = Path.Combine(versionDir, $"{forgeVersion}.json");
            return Directory.Exists(versionDir) && File.Exists(jsonPath);

        }

        public bool IsForgeInstalled(string forgeVersion)

        {

            string versionDir = Path.Combine(_minecraftPath, "versions", forgeVersion);
            return Directory.Exists(versionDir);

        }

        public async Task UninstallForgeAsync(string forgeVersion, CancellationToken cancellationToken = default)

        {

            try

            {

                string versionDir = Path.Combine(_minecraftPath, "versions", forgeVersion);
                if (Directory.Exists(versionDir))

                {

                    Directory.Delete(versionDir, true);
                    StatusChanged?.Invoke($"已卸载 Forge {forgeVersion}");

                }

            }

            catch (Exception ex)

            {

                StatusChanged?.Invoke($"卸载失败: {ex.Message}");

            }

        }

        public class ForgeVersionInfo

        {

            public string Version { get; set; }

            public string McVersion { get; set; }

            public string ForgeVersion { get; set; }

            public bool IsRecommended { get; set; }

            public string DisplayVersion => $"Forge {ForgeVersion}";

            public string Type => IsRecommended ? "推荐版本" : "普通版本";

            public override string ToString()

            {

                return $"{McVersion}-Forge{ForgeVersion}";

            }

        }

    }

}
