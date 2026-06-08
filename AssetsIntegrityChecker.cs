using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PCLCS
{
    public static class AssetsIntegrityChecker
    {
        public static AssetsCheckResult CheckAssetsIntegrity(
            string mcFolder,
            string versionJsonPath,
            bool checkHash = true,
            bool ignoreFileCheck = false,
            Action<string> onLog = null,
            Action<double> onProgress = null)
        {
            var result = new AssetsCheckResult();

            try
            {
                onLog?.Invoke("开始校验资源文件完整性");

                if (ignoreFileCheck)
                {
                    onLog?.Invoke("已设置忽略文件检查，跳过校验");
                    result.Success = true;
                    result.Skipped = true;
                    return result;
                }

                var versionJson = ParseVersionJson(versionJsonPath);
                string indexName = GetAssetsIndexName(versionJson);
                onLog?.Invoke($"资源索引名称: {indexName}");

                string indexPath = Path.Combine(mcFolder, "assets", "indexes", $"{indexName}.json");
                if (!File.Exists(indexPath))
                {
                    onLog?.Invoke($"资源索引文件不存在: {indexPath}");
                    result.Success = false;
                    result.ErrorMessage = $"资源索引文件不存在: {indexPath}";
                    return result;
                }

                var assetsList = ParseAssetsIndex(indexPath, mcFolder, versionJsonPath);
                onLog?.Invoke($"资源文件总数: {assetsList.Count}");

                int totalFiles = assetsList.Count;
                int checkedFiles = 0;
                int validFiles = 0;
                int missingFiles = 0;
                int sizeMismatchFiles = 0;
                int hashMismatchFiles = 0;

                foreach (var asset in assetsList)
                {
                    checkedFiles++;
                    onProgress?.Invoke((double)checkedFiles / totalFiles);

                    var checkResult = CheckSingleAsset(asset, checkHash);

                    if (checkResult.IsValid)
                    {
                        validFiles++;
                        continue;
                    }

                    if (checkResult.IsMissing)
                    {
                        missingFiles++;

                        result.MissingAssets.Add(asset);
                        // 只记录前10个缺失文件，避免大量日志输出
                        if (missingFiles <= 10)
                        {
                            onLog?.Invoke($"缺失文件: {asset.LocalPath}");
                        }
                    }
                    else if (checkResult.SizeMismatch)
                    {
                        sizeMismatchFiles++;
                        result.SizeMismatchAssets.Add(asset);
                        if (sizeMismatchFiles <= 10)
                        {
                            onLog?.Invoke($"大小不匹配: {asset.LocalPath} (期望: {asset.Size}, 实际: {checkResult.ActualSize})");
                        }
                    }
                    else if (checkResult.HashMismatch)
                    {
                        hashMismatchFiles++;
                        result.HashMismatchAssets.Add(asset);
                        if (hashMismatchFiles <= 10)
                        {
                            onLog?.Invoke($"Hash不匹配: {asset.LocalPath} (期望: {asset.Hash}, 实际: {checkResult.ActualHash})");
                        }
                    }
                }

                result.TotalAssets = totalFiles;
                result.ValidAssets = validFiles;
                result.MissingAssetsCount = missingFiles;
                result.SizeMismatchCount = sizeMismatchFiles;
                result.HashMismatchCount = hashMismatchFiles;
                result.Success = true;

                onLog?.Invoke($"校验完成: 总计 {totalFiles}, 有效 {validFiles}, 缺失 {missingFiles}, 大小不匹配 {sizeMismatchFiles}, Hash不匹配 {hashMismatchFiles}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                onLog?.Invoke($"校验失败: {ex.Message}");
            }

            return result;
        }

        public static SingleAssetCheckResult CheckSingleAsset(AssetEntry asset, bool checkHash)
        {
            var result = new SingleAssetCheckResult();

            if (!File.Exists(asset.LocalPath))
            {
                result.IsMissing = true;
                return result;
            }

            var fileInfo = new FileInfo(asset.LocalPath);
            result.ActualSize = fileInfo.Length;

            if (asset.Size > 0 && asset.Size != fileInfo.Length)
            {
                result.SizeMismatch = true;
                return result;
            }

            if (checkHash && !string.IsNullOrEmpty(asset.Hash))
            {
                string actualHash = ComputeSHA1(asset.LocalPath);
                result.ActualHash = actualHash;

                if (!asset.Hash.Equals(actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    result.HashMismatch = true;
                    return result;
                }
            }

            result.IsValid = true;
            return result;
        }

        public static List<AssetEntry> ParseAssetsIndex(string indexPath, string mcFolder, string versionJsonPath)
        {
            var result = new List<AssetEntry>();

            string jsonContent = File.ReadAllText(indexPath);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            bool mapToResources = false;
            bool isVirtual = false;

            if (root.TryGetProperty("map_to_resources", out var mapProp))
            {
                mapToResources = mapProp.GetBoolean();
            }

            if (root.TryGetProperty("virtual", out var virtualProp))
            {
                isVirtual = virtualProp.GetBoolean();
            }

            if (!root.TryGetProperty("objects", out var objects))
            {
                return result;
            }

            string versionFolder = Path.GetDirectoryName(versionJsonPath) ?? mcFolder;

            foreach (var obj in objects.EnumerateObject())
            {
                var asset = new AssetEntry
                {
                    Name = obj.Name,
                    Hash = obj.Value.GetProperty("hash").GetString(),
                    Size = obj.Value.GetProperty("size").GetInt64()
                };

                string hashPrefix = asset.Hash.Substring(0, 2);

                if (mapToResources)
                {
                    asset.LocalPath = Path.Combine(versionFolder, "resources", obj.Name.Replace("/", "\\"));
                }
                else if (isVirtual)
                {
                    asset.LocalPath = Path.Combine(mcFolder, "assets", "virtual", "legacy", obj.Name.Replace("/", "\\"));
                }
                else
                {
                    asset.LocalPath = Path.Combine(mcFolder, "assets", "objects", hashPrefix, asset.Hash);
                }

                result.Add(asset);
            }

            return result;
        }

        public static string GetAssetsIndexName(VersionJsonInfo versionJson)
        {
            if (versionJson?.AssetIndex?.Id != null)
            {
                return versionJson.AssetIndex.Id;
            }

            if (!string.IsNullOrEmpty(versionJson?.Assets))
            {
                return versionJson.Assets;
            }

            return "legacy";
        }

        public static VersionJsonInfo ParseVersionJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                return new VersionJsonInfo();
            }

            string jsonContent = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            var result = new VersionJsonInfo();

            if (root.TryGetProperty("assetIndex", out var assetIndex))
            {
                result.AssetIndex = new AssetIndexInfo();
                if (assetIndex.TryGetProperty("id", out var id))
                {
                    result.AssetIndex.Id = id.GetString();
                }
                if (assetIndex.TryGetProperty("url", out var url))
                {
                    result.AssetIndex.Url = url.GetString();
                }
                if (assetIndex.TryGetProperty("sha1", out var sha1))
                {
                    result.AssetIndex.Sha1 = sha1.GetString();
                }
                if (assetIndex.TryGetProperty("size", out var size))
                {
                    result.AssetIndex.Size = size.GetInt64();
                }
            }

            if (root.TryGetProperty("assets", out var assets))
            {
                result.Assets = assets.GetString();
            }

            if (root.TryGetProperty("inheritsFrom", out var inheritsFrom))
            {
                result.InheritsFrom = inheritsFrom.GetString();
            }

            return result;
        }

        public static List<NetFile> GetMissingAssetsDownloadList(
            string mcFolder,
            string versionJsonPath,
            bool checkHash,
            Action<string> onLog = null)
        {
            var result = new List<NetFile>();

            var versionJson = ParseVersionJson(versionJsonPath);
            string indexName = GetAssetsIndexName(versionJson);

            string indexPath = Path.Combine(mcFolder, "assets", "indexes", $"{indexName}.json");
            if (!File.Exists(indexPath))
            {
                onLog?.Invoke($"资源索引文件不存在: {indexPath}");
                return result;
            }

            var assetsList = ParseAssetsIndex(indexPath, mcFolder, versionJsonPath);

            int downloadCount = 0;
            foreach (var asset in assetsList)
            {
                var checkResult = CheckSingleAsset(asset, checkHash);

                if (checkResult.IsValid)
                    continue;

                string url = $"https://resources.download.minecraft.net/{asset.Hash.Substring(0, 2)}/{asset.Hash}";
                string mirrorUrl = $"https://bmclapi2.bangbang93.com/assets/{asset.Hash.Substring(0, 2)}/{asset.Hash}";

                var netFile = new NetFile(
                    new[] { url, mirrorUrl },
                    asset.LocalPath,
                    new FileChecker(actualSize: asset.Size > 0 ? asset.Size : -1, hash: asset.Hash)
                );

                result.Add(netFile);
                downloadCount++;
                // 只记录前10个需要下载的文件，避免大量日志输出
                if (downloadCount <= 10)
                {
                    onLog?.Invoke($"需要下载: {asset.Name}");
                }
            }

            if (downloadCount > 10)
            {
                onLog?.Invoke($"... 还有 {downloadCount - 10} 个文件需要下载");
            }

            return result;
        }

        public static string ComputeSHA1(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        public static string ComputeSHA256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        public static string ComputeMD5(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    public class AssetsCheckResult
    {
        public bool Success { get; set; }
        public bool Skipped { get; set; }
        public string ErrorMessage { get; set; }

        public int TotalAssets { get; set; }
        public int ValidAssets { get; set; }
        public int MissingAssetsCount { get; set; }
        public int SizeMismatchCount { get; set; }
        public int HashMismatchCount { get; set; }

        public List<AssetEntry> MissingAssets { get; set; } = new List<AssetEntry>();
        public List<AssetEntry> SizeMismatchAssets { get; set; } = new List<AssetEntry>();
        public List<AssetEntry> HashMismatchAssets { get; set; } = new List<AssetEntry>();

        public int TotalIssues => MissingAssetsCount + SizeMismatchCount + HashMismatchCount;
        public bool HasIssues => TotalIssues > 0;
    }

    public class SingleAssetCheckResult
    {
        public bool IsValid { get; set; }
        public bool IsMissing { get; set; }
        public bool SizeMismatch { get; set; }
        public bool HashMismatch { get; set; }
        public long ActualSize { get; set; }
        public string ActualHash { get; set; }
    }

    public class AssetEntry
    {
        public string Name { get; set; }
        public string Hash { get; set; }
        public long Size { get; set; }
        public string LocalPath { get; set; }
    }

    public class VersionJsonInfo
    {
        public AssetIndexInfo AssetIndex { get; set; }
        public string Assets { get; set; }
        public string InheritsFrom { get; set; }
    }

    public class AssetIndexInfo
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Sha1 { get; set; }
        public long Size { get; set; }
    }

    public class NetFile
    {
        public string[] Urls { get; set; }
        public string LocalPath { get; set; }
        public FileChecker Checker { get; set; }

        public NetFile(string[] urls, string localPath, FileChecker checker)
        {
            Urls = urls;
            LocalPath = localPath;
            Checker = checker;
        }
    }

    public class FileChecker
    {
        public long ActualSize { get; set; }
        public string Hash { get; set; }

        public FileChecker(long actualSize = -1, string hash = null)
        {
            ActualSize = actualSize;
            Hash = hash;
        }
    }
}