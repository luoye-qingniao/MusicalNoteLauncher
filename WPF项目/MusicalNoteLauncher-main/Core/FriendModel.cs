using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace MusicalNoteLauncher.Core
{
    /// <summary>消息类型</summary>
    public enum MessageType
    {
        /// <summary>普通聊天消息</summary>
        Normal,
        /// <summary>联机邀请消息</summary>
        Invite
    }

    /// <summary>聊天消息记录</summary>
    public class ChatMessage
    {
        public string SenderName { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        /// <summary>true = 我方发送, false = 好友发送</summary>
        public bool IsFromMe { get; set; }
        /// <summary>消息类型</summary>
        public string MsgType { get; set; } = "Normal";
        /// <summary>邀请的网络名（仅 Invite 类型有效）</summary>
        public string InviteNetworkName { get; set; }
        /// <summary>邀请的网络密钥（仅 Invite 类型有效）</summary>
        public string InviteNetworkSecret { get; set; }
        /// <summary>邀请是否已被接收</summary>
        public bool InviteAccepted { get; set; }
        /// <summary>邀请的游戏版本（用于启动）</summary>
        public string InviteGameVersion { get; set; }
    }

    /// <summary>好友数据模型，支持属性变更通知</summary>
    public class FriendModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _name = "";
        /// <summary>好友昵称</summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(FriendName)); }
        }

        /// <summary>显示名称（与 Name 相同，用于 XAML 绑定）</summary>
        public string FriendName => Name;

        private string _avatarEmoji = "👤";
        /// <summary>头像 Emoji</summary>
        public string AvatarEmoji
        {
            get => _avatarEmoji;
            set { _avatarEmoji = value; OnPropertyChanged(); }
        }

        private Color _avatarColorValue = Color.FromRgb(0x21, 0x96, 0xF3);
        /// <summary>头像背景色（内部存储）</summary>
        public Color AvatarColorValue
        {
            get => _avatarColorValue;
            set { _avatarColorValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(AvatarColor)); }
        }

        /// <summary>头像背景色 Brush（用于 XAML 绑定）</summary>
        public SolidColorBrush AvatarColor => new SolidColorBrush(AvatarColorValue);

        private bool _isOnline = false;
        /// <summary>是否在线</summary>
        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                _isOnline = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        /// <summary>状态文本</summary>
        public string StatusText => IsOnline ? "🟢 在线" : "⚫ 离线";

        /// <summary>状态颜色</summary>
        public SolidColorBrush StatusColor =>
            IsOnline
                ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

        /// <summary>聊天记录</summary>
        public List<ChatMessage> ChatHistory { get; set; } = new List<ChatMessage>();
    }

    /// <summary>好友列表存储容器（用于 JSON 序列化）</summary>
    public class FriendsData
    {
        public List<FriendStorageItem> Friends { get; set; } = new List<FriendStorageItem>();
    }

    /// <summary>单个好友的 JSON 存储项（Brush 无法直接序列化，使用原始类型）</summary>
    public class FriendStorageItem
    {
        public string Name { get; set; } = "";
        public string AvatarEmoji { get; set; } = "👤";
        public byte AvatarR { get; set; } = 0x21;
        public byte AvatarG { get; set; } = 0x96;
        public byte AvatarB { get; set; } = 0xF3;
        public bool IsOnline { get; set; }
        public List<ChatMessage> ChatHistory { get; set; } = new List<ChatMessage>();
    }
}
