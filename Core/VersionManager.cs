using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MusicalNoteLauncher.Core
{
    public class VersionManager
    {
        private readonly string _minecraftPath;
        private readonly VersionScanService _scanService;

        public VersionManager(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
            _scanService = VersionScanService.Instance;
        }

        public List<string> GetInstalledJavaVersions()
        {
            return _scanService.GetInstalledJavaVersions();
        }

        public List<string> GetInstalledBedrockVersions()
        {
            return _scanService.GetInstalledBedrockVersions();
        }

        public bool IsJavaVersionValid(string versionId)
        {
            string versionDir = Path.Combine(_minecraftPath, "versions", versionId);

            if (!Directory.Exists(versionDir))
                return false;

            string jarFile = Path.Combine(versionDir, $"{versionId}.jar");
            string jsonFile = Path.Combine(versionDir, $"{versionId}.json");

            if (!File.Exists(jarFile) || !File.Exists(jsonFile))
                return false;

            return IsJsonValid(jsonFile);
        }

        private bool IsJsonValid(string jsonPath)
        {
            try
            {
                string content = File.ReadAllText(jsonPath);
                using (var doc = JsonDocument.Parse(content))
                {
                    return doc.RootElement.TryGetProperty("id", out _);
                }
            }
            catch
            {
                return false;
            }
        }

        public bool IsVersionInstalled(string versionId, VersionType versionType)
        {
            return versionType switch
            {
                VersionType.Java => _scanService.IsJavaVersionInstalled(versionId),
                VersionType.Bedrock => _scanService.IsBedrockVersionInstalled(versionId),
                _ => false
            };
        }

        public List<string> GetInstalledVersions(VersionType versionType)
        {
            return versionType switch
            {
                VersionType.Java => GetInstalledJavaVersions(),
                VersionType.Bedrock => GetInstalledBedrockVersions(),
                _ => new List<string>()
            };
        }
    }
}