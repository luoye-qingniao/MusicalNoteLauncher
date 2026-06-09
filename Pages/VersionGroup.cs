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

        private async void LoadVersionsAsync()
        {
            IsLoading = true;
            try
            {
                if (_cachedVersions != null)
                {
                    await Task.Run(() =>
                    {
                        foreach (var v in _cachedVersions)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => Versions.Add(v));
                        }
                    });
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


