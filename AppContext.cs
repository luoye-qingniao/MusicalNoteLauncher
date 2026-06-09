using System;
using System.IO;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher
{
    public static class AppContext
    {
        public static string Username { get; set; } = "Player";
        public static bool IsOfflineMode { get; set; } = true;
        public static string MinecraftPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        public static ConfigManager Config { get; set; } = new ConfigManager();

        public static event Action<string> NavigateRequested;

        public static void NavigateTo(string pageKey)
        {
            NavigateRequested?.Invoke(pageKey);
        }

        public static void Initialize(string username, bool isOfflineMode, string minecraftPath = null, ConfigManager config = null)
        {
            Username = username;
            IsOfflineMode = isOfflineMode;
            if (minecraftPath != null) MinecraftPath = minecraftPath;
            if (config != null) Config = config;
        }
    }
}
