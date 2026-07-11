using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicalNoteLauncher.ViewModels
{
    public class RecommendItemViewModel : INotifyPropertyChanged
    {
        private string _name;
        private string _description;
        private string _author;
        private string _downloadCount;
        private string _iconUrl;
        private string _source;
        private string _projectId;
        private string _type;
        private string _rating;
        private string _tags;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string Author
        {
            get => _author;
            set => SetProperty(ref _author, value);
        }

        public string DownloadCount
        {
            get => _downloadCount;
            set => SetProperty(ref _downloadCount, value);
        }

        public string IconUrl
        {
            get => _iconUrl;
            set => SetProperty(ref _iconUrl, value);
        }

        public string Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }

        public string ProjectId
        {
            get => _projectId;
            set => SetProperty(ref _projectId, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string Rating
        {
            get => _rating;
            set => SetProperty(ref _rating, value);
        }

        public string Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}