using System;

namespace MusicalNoteLauncher.Core
{
    public class VersionManifest
    {
        public VersionManifestItem[] versions { get; set; }
    }

    public class VersionManifestItem
    {
        public string id { get; set; }
        public string type { get; set; }
        public string url { get; set; }
        public string releaseTime { get; set; }
        public string sha1 { get; set; }
        public string minecraftArguments { get; set; }
        public int? complianceLevel { get; set; }
    }

    public class VersionDetail
    {
        public string id { get; set; }
        public Downloads downloads { get; set; }
        public string minecraftVersion { get; set; }
    }

    public class Downloads
    {
        public DownloadFile client { get; set; }
    }

    public class DownloadFile
    {
        public string url { get; set; }
        public string sha1 { get; set; }
        public long size { get; set; }
    }
}