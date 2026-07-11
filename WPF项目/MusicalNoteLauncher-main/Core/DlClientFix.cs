using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    public class McInstance
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public string MinecraftPath { get; set; }
        public string VersionJsonPath => Path.Combine(MinecraftPath, "versions", Id, $"{Id}.json");
        public string VersionJarPath => Path.Combine(MinecraftPath, "versions", Id, $"{Id}.jar");
    }

    public class DlClientFix
    {
        private readonly HttpClient _httpClient;
        private readonly string _minecraftPath;

        public DlClientFix(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(30)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public List<LoaderBase> GetLoaders(McInstance instance, bool checkAssetsHash = true, string assetsIndexBehaviour = "auto")
        {
            var loaders = new List<LoaderBase>();

            var libraryLoader = CreateLibraryLoader(instance);
            if (libraryLoader != null)
            {
                loaders.Add(libraryLoader);
            }

            var assetsLoader = CreateAssetsLoader(instance, checkAssetsHash, assetsIndexBehaviour);
            if (assetsLoader != null)
            {
                loaders.Add(assetsLoader);
            }

            return loaders;
        }

        private LoaderBase CreateLibraryLoader(McInstance instance)
        {
            var task = new LibraryLoaderTask("下载支持库", instance, _minecraftPath, _httpClient);
            return task;
        }

        private LoaderBase CreateAssetsLoader(McInstance instance, bool checkAssetsHash, string assetsIndexBehaviour)
        {
            var task = new AssetsLoaderTask("下载资源文件", instance, _minecraftPath, _httpClient);
            return task;
        }
    }

    public class LibraryLoaderTask : LoaderBase
    {
        private readonly McInstance _instance;
        private readonly string _minecraftPath;
        private readonly HttpClient _httpClient;

        public LibraryLoaderTask(string name, McInstance instance, string minecraftPath, HttpClient httpClient)
        {
            Name = name;
            _instance = instance;
            _minecraftPath = minecraftPath;
            _httpClient = httpClient;
        }

        public override async Task RunAsync(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            OnStateChanged(LoaderState.Running);

            try
            {
                if (!File.Exists(_instance.VersionJsonPath))
                {
                    Logger.Warning($"版本JSON文件不存在: {_instance.VersionJsonPath}");
                    OnCompleted();
                    return;
                }

                string json = File.ReadAllText(_instance.VersionJsonPath);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (!root.TryGetProperty("libraries", out JsonElement librariesElement))
                {
                    Logger.Info("未找到支持库列表");
                    OnCompleted();
                    return;
                }

                var downloadTasks = new List<Task>();
                int totalLibraries = 0;
                int downloadedLibraries = 0;

                foreach (JsonElement libElement in librariesElement.EnumerateArray())
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (!libElement.TryGetProperty("downloads", out JsonElement downloadsElement))
                        continue;

                    if (!downloadsElement.TryGetProperty("artifact", out JsonElement artifactElement))
                        continue;

                    string url = GetStringProperty(artifactElement, "url");
                    string path = GetStringProperty(artifactElement, "path");
                    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(path))
                        continue;

                    string savePath = Path.Combine(_minecraftPath, "libraries", path);
                    if (File.Exists(savePath))
                        continue;

                    totalLibraries++;
                    downloadTasks.Add(DownloadFileAsync(url, savePath, cancellationToken).ContinueWith(t =>
                    {
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            Interlocked.Increment(ref downloadedLibraries);
                            Logger.Info($"已下载支持库 [{downloadedLibraries}/{totalLibraries}]: {Path.GetFileName(path)}");
                        }
                    }, cancellationToken));
                }

                if (downloadTasks.Count > 0)
                {
                    await Task.WhenAll(downloadTasks);
                    Logger.Info($"支持库下载完成，共 {downloadedLibraries}/{totalLibraries} 个");
                }

                OnCompleted();
            }
            catch (OperationCanceledException)
            {
                OnStateChanged(LoaderState.Cancelled);
            }
            catch (Exception ex)
            {
                OnFailed(ex);
            }
        }

        private async Task DownloadFileAsync(string url, string savePath, CancellationToken cancellationToken)
        {
            try
            {
                string directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await contentStream.CopyToAsync(fileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"下载文件失败: {url}, 错误: {ex.Message}");
            }
        }

        private string GetStringProperty(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out JsonElement prop))
                {
                    if (prop.ValueKind == JsonValueKind.String)
                    {
                        return prop.GetString();
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    public class AssetsLoaderTask : LoaderBase
    {
        private readonly McInstance _instance;
        private readonly string _minecraftPath;
        private readonly HttpClient _httpClient;

        public AssetsLoaderTask(string name, McInstance instance, string minecraftPath, HttpClient httpClient)
        {
            Name = name;
            _instance = instance;
            _minecraftPath = minecraftPath;
            _httpClient = httpClient;
        }

        public override async Task RunAsync(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            OnStateChanged(LoaderState.Running);

            try
            {
                if (!File.Exists(_instance.VersionJsonPath))
                {
                    Logger.Warning($"版本JSON文件不存在: {_instance.VersionJsonPath}");
                    OnCompleted();
                    return;
                }

                string json = File.ReadAllText(_instance.VersionJsonPath);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                string assetIndexName = GetStringProperty(root, "assetIndex");
                if (string.IsNullOrEmpty(assetIndexName))
                {
                    if (root.TryGetProperty("assets", out JsonElement assetsElement))
                    {
                        assetIndexName = assetsElement.GetString();
                    }
                }

                if (string.IsNullOrEmpty(assetIndexName))
                {
                    Logger.Info("未找到资源索引名称");
                    OnCompleted();
                    return;
                }

                string assetsIndexPath = Path.Combine(_minecraftPath, "assets", "indexes", $"{assetIndexName}.json");
                if (!File.Exists(assetsIndexPath))
                {
                    if (root.TryGetProperty("assetIndex", out JsonElement assetIndexElement))
                    {
                        string assetIndexUrl = GetStringProperty(assetIndexElement, "url");
                        if (!string.IsNullOrEmpty(assetIndexUrl))
                        {
                            await DownloadFileAsync(assetIndexUrl, assetsIndexPath, cancellationToken);
                            Logger.Info($"已下载资源索引: {assetIndexName}");
                        }
                    }
                }

                if (File.Exists(assetsIndexPath))
                {
                    await DownloadAssetsAsync(assetsIndexPath, cancellationToken);
                }

                OnCompleted();
            }
            catch (OperationCanceledException)
            {
                OnStateChanged(LoaderState.Cancelled);
            }
            catch (Exception ex)
            {
                OnFailed(ex);
            }
        }

        private async Task DownloadAssetsAsync(string assetsIndexPath, CancellationToken cancellationToken)
        {
            string json = File.ReadAllText(assetsIndexPath);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("objects", out JsonElement objectsElement))
            {
                Logger.Warning("资源索引文件中未找到 objects 节点");
                return;
            }

            int totalAssets = 0;
            int downloadedAssets = 0;
            var downloadTasks = new List<Task>();

            foreach (JsonProperty property in objectsElement.EnumerateObject())
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                JsonElement objElement = property.Value;
                string hash = GetStringProperty(objElement, "hash");
                if (string.IsNullOrEmpty(hash))
                    continue;

                string subPath = $"{hash.Substring(0, 2)}/{hash}";
                string savePath = Path.Combine(_minecraftPath, "assets", "objects", subPath);

                if (File.Exists(savePath))
                    continue;

                string url = $"https://bmclapi2.bangbang93.com/assets/{subPath}";
                totalAssets++;

                downloadTasks.Add(DownloadFileAsync(url, savePath, cancellationToken).ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        Interlocked.Increment(ref downloadedAssets);
                        if (downloadedAssets % 50 == 0)
                        {
                            Logger.Info($"已下载资源文件 [{downloadedAssets}/{totalAssets}]");
                        }
                    }
                }, cancellationToken));
            }

            if (downloadTasks.Count > 0)
            {
                await Task.WhenAll(downloadTasks);
                Logger.Info($"资源文件下载完成，共 {downloadedAssets}/{totalAssets} 个");
            }
        }

        private async Task DownloadFileAsync(string url, string savePath, CancellationToken cancellationToken)
        {
            try
            {
                string directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await contentStream.CopyToAsync(fileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"下载文件失败: {url}, 错误: {ex.Message}");
            }
        }

        private string GetStringProperty(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out JsonElement prop))
                {
                    if (prop.ValueKind == JsonValueKind.String)
                    {
                        return prop.GetString();
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
