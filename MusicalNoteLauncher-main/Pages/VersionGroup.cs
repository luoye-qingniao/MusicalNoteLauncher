using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public class VersionGroup : INotifyPropertyChanged
    {
        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged(); }
        }

        public ObservableCollection<VersionItem> Versions
        {
            get { return _versions; }
            set { _versions = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    if (_isExpanded && _cachedVersions != null && (_versions == null || _versions.Count == 0))
                    {
                        LoadVersionsAsync();
                    }
                }
            }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public VersionGroup()
        {
            _versions = new ObservableCollection<VersionItem>();
        }

        public void SetCachedVersions(List<VersionItem> versions)
        {
            _cachedVersions = versions;
        }

        public bool RemoveVersion(string versionId)
        {
            bool removed = false;
            // 从缓存列表中移除
            if (_cachedVersions != null)
            {
                int removedCount = _cachedVersions.RemoveAll(v => v.VersionId == versionId);
                if (removedCount > 0) removed = true;
            }
            // 从显示列表中移除
            for (int i = Versions.Count - 1; i >= 0; i--)
            {
                if (Versions[i].VersionId == versionId)
                {
                    Versions.RemoveAt(i);
                    removed = true;
                }
            }
            return removed;
        }

        public bool HasVersions
        {
            get { return (_cachedVersions != null && _cachedVersions.Count > 0) || Versions.Count > 0; }
        }

        private async void LoadVersionsAsync()
        {
            IsLoading = true;
            try
            {
                if (_cachedVersions != null && _cachedVersions.Count > 0)
                {
                    // 整体替换集合，只触发一次 PropertyChanged 而非 N 次 CollectionChanged，
                    // 避免预览版（数量多）展开时逐项 Add 导致的 ListView 反复布局造成 UI 卡顿
                    Versions = new ObservableCollection<VersionItem>(_cachedVersions);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[版本分组] 加载版本失败: " + ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _name;
        private ObservableCollection<VersionItem> _versions;
        private bool _isExpanded;
        private bool _isLoading;
        private List<VersionItem> _cachedVersions;
    }
}


