using System;
using System.IO;
using System.Text.Json;

namespace MusicalNoteLauncher.Core
{
    public static class ModLoaderDetector
    {
        public enum ModLoaderType
        {
            None,
            Forge,
            Fabric,
            Quilt
        }

        public static ModLoaderType DetectModLoader(string minecraftPath, string versionId)
        {
            if (string.IsNullOrEmpty(versionId))
                return ModLoaderType.None;

            string versionJsonPath = Path.Combine(minecraftPath, "versions", versionId, $"{versionId}.json");
            
            if (!File.Exists(versionJsonPath))
                return ModLoaderType.None;

            try
            {
                string jsonContent = File.ReadAllText(versionJsonPath);
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = doc.RootElement;
                    
                    // 检查 mainClass 是否包含加载器标识
                    if (root.TryGetProperty("mainClass", out var mainClassElement))
                    {
                        string mainClass = mainClassElement.GetString() ?? string.Empty;
                        
                        if (mainClass.Contains("forge", StringComparison.OrdinalIgnoreCase))
                            return ModLoaderType.Forge;
                        
                        if (mainClass.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                            return ModLoaderType.Fabric;
                        
                        if (mainClass.Contains("quilt", StringComparison.OrdinalIgnoreCase))
                            return ModLoaderType.Quilt;
                    }
                    
                    // 检查继承关系
                    if (root.TryGetProperty("inheritsFrom", out var inheritsElement))
                    {
                        string inheritsFrom = inheritsElement.GetString();
                        if (!string.IsNullOrEmpty(inheritsFrom))
                        {
                            // 递归检测继承的版本
                            return DetectModLoader(minecraftPath, inheritsFrom);
                        }
                    }
                    
                    // 检查 libraries 是否包含加载器库
                    if (root.TryGetProperty("libraries", out var librariesElement) && librariesElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var libElement in librariesElement.EnumerateArray())
                        {
                            if (libElement.TryGetProperty("name", out var nameElement))
                            {
                                string libName = nameElement.GetString() ?? string.Empty;
                                
                                if (libName.Contains("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase))
                                    return ModLoaderType.Forge;
                                
                                if (libName.Contains("net.fabricmc:fabric-loader", StringComparison.OrdinalIgnoreCase))
                                    return ModLoaderType.Fabric;
                                
                                if (libName.Contains("org.quiltmc:quilt-loader", StringComparison.OrdinalIgnoreCase))
                                    return ModLoaderType.Quilt;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略解析错误
            }

            return ModLoaderType.None;
        }

        public static string GetLoaderDisplayName(ModLoaderType loaderType)
        {
            switch (loaderType)
            {
                case ModLoaderType.Forge:
                    return "Forge";
                case ModLoaderType.Fabric:
                    return "Fabric";
                case ModLoaderType.Quilt:
                    return "Quilt";
                default:
                    return "无";
            }
        }
    }
}