using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 本地背景素材库管理服务：管理 MNL/backgrounds/ 目录下的背景素材。
    /// 提供导入（上传）、删除、列出等功能，数据通过 manifest.json 持久化。
    /// </summary>
    public class BackgroundStoreService
    {
        private static readonly Lazy<BackgroundStoreService> _instance =
            new(() => new BackgroundStoreService());

        public static BackgroundStoreService Instance => _instance.Value;

        private static readonly string StoreDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MNL", "backgrounds");

        private static readonly string ManifestPath =
            Path.Combine(StoreDir, "manifest.json");

        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        /// <summary>所有背景条目</summary>
        public ObservableCollection<BackgroundStoreItem> Items { get; } = new();

        private BackgroundStoreService()
        {
            Directory.CreateDirectory(StoreDir);
            LoadManifest();
        }

        /// <summary>
        /// 导入背景文件到本地素材库：复制文件、注册元数据。
        /// </summary>
        /// <param name="sourcePath">用户选择的原始文件路径</param>
        /// <param name="displayName">显示名称（默认取文件名）</param>
        /// <returns>成功返回新条目，失败返回 null</returns>
        public BackgroundStoreItem Import(string sourcePath, string displayName = "")
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return null;

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            string type;
            if (ext == ".mp4")
                type = "Video";
            else if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp")
                type = "Image";
            else
                return null; // 不支持的格式

            string id = Guid.NewGuid().ToString("N")[..8];
            string destFileName = $"{id}{ext}";
            string destPath = Path.Combine(StoreDir, destFileName);

            try
            {
                File.Copy(sourcePath, destPath, overwrite: true);

                string name = string.IsNullOrWhiteSpace(displayName)
                    ? Path.GetFileNameWithoutExtension(sourcePath)
                    : displayName;

                var item = new BackgroundStoreItem
                {
                    Id = id,
                    Name = name,
                    Type = type,
                    FilePath = destPath,
                    AddedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                };

                Items.Add(item);
                SaveManifest();
                return item;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从素材库中删除指定条目（同时删除文件）。
        /// </summary>
        public bool Remove(string id)
        {
            var item = Items.FirstOrDefault(i => i.Id == id);
            if (item == null) return false;

            try
            {
                if (File.Exists(item.FilePath))
                    File.Delete(item.FilePath);
                Items.Remove(item);
                SaveManifest();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 刷新列表（重新扫描目录修复不一致）。
        /// </summary>
        public void Refresh()
        {
            Items.Clear();
            LoadManifest();
        }

        private void LoadManifest()
        {
            try
            {
                if (File.Exists(ManifestPath))
                {
                    string json = File.ReadAllText(ManifestPath);
                    var list = JsonSerializer.Deserialize<List<BackgroundStoreItem>>(json);
                    if (list != null)
                    {
                        // 过滤掉文件已被手动删除的条目
                        foreach (var item in list.Where(i => File.Exists(i.FilePath)))
                            Items.Add(item);
                    }
                }
            }
            catch
            {
                // manifest 损坏则从空开始
            }
        }

        public void SaveManifest()
        {
            try
            {
                var list = Items.ToList();
                string json = JsonSerializer.Serialize(list, _jsonOptions);
                File.WriteAllText(ManifestPath, json);
            }
            catch
            {
                // 忽略保存错误
            }
        }
    }
}
