using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MusicalNoteLauncher.Core
{
    public enum AccountType
    {
        Offline,
        Microsoft,
        AuthlibInjector
    }

    public class GameAccount : INotifyPropertyChanged
    {
        private string _name;
        private AccountType _type;
        private string _uuid;
        private string _accessToken;
        private string _authServer;
        private ImageSource _headImage;
        private BitmapImage _avatarImage;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public AccountType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public string Uuid
        {
            get => _uuid;
            set { _uuid = value; OnPropertyChanged(); }
        }

        public string AccessToken
        {
            get => _accessToken;
            set { _accessToken = value; OnPropertyChanged(); }
        }

        public string AuthServer
        {
            get => _authServer;
            set { _authServer = value; OnPropertyChanged(); }
        }

        public ImageSource HeadImage
        {
            get => _headImage;
            set { _headImage = value; OnPropertyChanged(); }
        }

        /// <summary>用户自定义头像（个人资料头像），与 MC 皮肤的头部立雕不同</summary>
        public BitmapImage AvatarImage
        {
            get => _avatarImage;
            set { _avatarImage = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string TypeDisplay
        {
            get
            {
                switch (Type)
                {
                    case AccountType.Offline: return "离线";
                    case AccountType.Microsoft: return "微软";
                    case AccountType.AuthlibInjector: return "外置";
                    default: return "未知";
                }
            }
        }

        public string TypeIcon
        {
            get
            {
                switch (Type)
                {
                    case AccountType.Offline: return "⬡";
                    case AccountType.Microsoft: return "⊞";
                    case AccountType.AuthlibInjector: return "🌐";
                    default: return "?";
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
