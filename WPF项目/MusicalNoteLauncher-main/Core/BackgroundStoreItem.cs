using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 背景商店条目模型：本地背景库中单个背景素材的元数据。
    /// </summary>
    public class BackgroundStoreItem : INotifyPropertyChanged
    {
        private string _id = "";
        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string _name = "";
        /// <summary>背景名称（显示名）</summary>
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _type = "Image";
        /// <summary>背景类型：Image 或 Video</summary>
        public string Type { get => _type; set { _type = value; OnPropertyChanged(); } }

        private string _filePath = "";
        /// <summary>原始素材文件绝对路径</summary>
        public string FilePath { get => _filePath; set { _filePath = value; OnPropertyChanged(); } }

        private string _addedDate = "";
        /// <summary>添加日期字符串</summary>
        public string AddedDate { get => _addedDate; set { _addedDate = value; OnPropertyChanged(); } }

        private int _serverId;
        /// <summary>服务器数据库中的 ID，0 表示未上传到服务器</summary>
        public int ServerId { get => _serverId; set { _serverId = value; OnPropertyChanged(); } }

        private int _downloadCount;
        /// <summary>服务器记录的被下载次数</summary>
        public int DownloadCount { get => _downloadCount; set { _downloadCount = value; OnPropertyChanged(); } }

        private string _uploader = "";
        /// <summary>上传者名称</summary>
        public string Uploader { get => _uploader; set { _uploader = value; OnPropertyChanged(); } }

        /// <summary>是否已同步到服务器</summary>
        public bool IsOnServer => ServerId > 0;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
