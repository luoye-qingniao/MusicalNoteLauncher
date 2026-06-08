using System;
using System.IO;
using System.Text;

namespace MusicalNoteLauncher.Core
{
    public class ConfigManager
    {
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicalNoteLauncher");

        private static readonly string ConfigFile = Path.Combine(ConfigFolder, "config.ini");

        public string Username { get; set; }
        public bool RememberAccount { get; set; }
        public bool OfflineMode { get; set; }
        public string JavaPath { get; set; }
        public string GameVersion { get; set; }
        public string GameDirectory { get; set; }
        public int MemorySize { get; set; }
        public int ToolDownloadVersion { get; set; }

        public ConfigManager()
        {
            Username = "Player";
            RememberAccount = true;
            OfflineMode = true;
            JavaPath = string.Empty;
            GameVersion = "1.20.1";
            GameDirectory = GetDefaultMinecraftPath();
            MemorySize = 2048;
            ToolDownloadVersion = 0;

            EnsureGameDirectoryExists();

            Logger.Info("ConfigManager initialized with default values");
        }

        private string GetDefaultMinecraftPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, ".minecraft");
        }

        public void EnsureGameDirectoryExists()
        {
            try
            {
                string gameDir = GetMinecraftPath();
                
                if (!Directory.Exists(gameDir))
                {
                    Directory.CreateDirectory(gameDir);
                    Logger.Info($"Created game directory: {gameDir}");
                }

                string[] subFolders = { "versions", "assets", "saves", "mods", "resourcepacks", "shaderpacks" };
                foreach (string subFolder in subFolders)
                {
                    string subPath = Path.Combine(gameDir, subFolder);
                    if (!Directory.Exists(subPath))
                    {
                        Directory.CreateDirectory(subPath);
                        Logger.Info($"Created subdirectory: {subPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating game directories: {ex.Message}", ex);
            }
        }

        public void Load()
        {
            try
            {
                Logger.Info($"Loading config from: {ConfigFile}");

                if (!File.Exists(ConfigFile))
                {
                    Logger.Info("Config file does not exist, using default values");
                    return;
                }

                string[] lines = File.ReadAllLines(ConfigFile, Encoding.UTF8);
                int loadedCount = 0;

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, separatorIndex).Trim();
                    string value = line.Substring(separatorIndex + 1).Trim();

                    try
                    {
                        switch (key)
                        {
                            case "Username":
                                Username = value;
                                loadedCount++;
                                break;
                            case "RememberAccount":
                                RememberAccount = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                                loadedCount++;
                                break;
                            case "OfflineMode":
                                OfflineMode = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                                loadedCount++;
                                break;
                            case "JavaPath":
                                JavaPath = value ?? string.Empty;
                                loadedCount++;
                                break;
                            case "GameVersion":
                                GameVersion = value ?? "1.20.1";
                                loadedCount++;
                                break;
                            case "GameDirectory":
                                GameDirectory = value ?? string.Empty;
                                loadedCount++;
                                break;
                            case "MemorySize":
                                if (int.TryParse(value, out int memory))
                                {
                                    MemorySize = Math.Max(512, Math.Min(8192, memory));
                                }
                                loadedCount++;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Error parsing config line '{key}={value}': {ex.Message}");
                    }
                }

                Logger.Info($"Config loaded successfully, {loadedCount} items loaded");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading config: {ex.Message}", ex);
            }
        }

        public void Save()
        {
            try
            {
                Logger.Info($"Saving config to: {ConfigFile}");

                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                    Logger.Info($"Created config directory: {ConfigFolder}");
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# MusicalNote Launcher Config");
                sb.AppendLine($"# Last updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Username={Username ?? string.Empty}");
                sb.AppendLine($"RememberAccount={RememberAccount}");
                sb.AppendLine($"OfflineMode={OfflineMode}");
                sb.AppendLine($"JavaPath={JavaPath ?? string.Empty}");
                sb.AppendLine($"GameVersion={GameVersion ?? "1.20.1"}");
                sb.AppendLine($"GameDirectory={GameDirectory ?? string.Empty}");
                sb.AppendLine($"MemorySize={MemorySize}");

                File.WriteAllText(ConfigFile, sb.ToString(), Encoding.UTF8);
                Logger.Info("Config saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving config: {ex.Message}", ex);
            }
        }

        public string GetMinecraftPath()
        {
            try
            {
                if (!string.IsNullOrEmpty(GameDirectory) && Directory.Exists(GameDirectory))
                {
                    return GameDirectory;
                }

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string defaultPath = Path.Combine(appData, ".minecraft");

                Logger.Info($"Using default Minecraft path: {defaultPath}");
                return defaultPath;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting Minecraft path: {ex.Message}", ex);
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
            }
        }
    }
}