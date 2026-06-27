using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public class BedrockVersionGroup : INotifyPropertyChanged
    {
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public ObservableCollection<BedrockVersionInfo> Versions
        {
            get => _versions;
            set { _versions = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    if (_isExpanded && _cachedVersions != null && (_versions == null || _versions.Count == 0))
                    {
                        LoadVersions();
                    }
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public BedrockVersionGroup()
        {
            _versions = new ObservableCollection<BedrockVersionInfo>();
        }

        public void SetCachedVersions(List<BedrockVersionInfo> versions)
        {
            _cachedVersions = versions;
        }

        private void LoadVersions()
        {
            if (_cachedVersions == null || _cachedVersions.Count == 0) return;
            // 整体替换集合，只触发一次 PropertyChanged，避免逐项 Add 导致的 UI 卡顿
            Versions = new ObservableCollection<BedrockVersionInfo>(_cachedVersions);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _name;
        private ObservableCollection<BedrockVersionInfo> _versions;
        private bool _isExpanded;
        private bool _isLoading;
        private List<BedrockVersionInfo> _cachedVersions;
    }
}
