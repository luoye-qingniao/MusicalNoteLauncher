using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Controls;
using MusicalNoteLauncher.Core;
using PCL.Account;

namespace MusicalNoteLauncher.Pages
{
    public partial class CommunityPage : UserControl
    {
        // ===== 数据模型 =====

        public class ForumChannel : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Icon { get; set; }
            public string ColorHex { get; set; } = "#6C5CE7";
            public int PostCount { get; set; }

            private int _unreadCount;
            public int UnreadCount
            {
                get => _unreadCount;
                set { _unreadCount = value; OnPropertyChanged(); }
            }

            public ObservableCollection<ForumPost> Posts { get; set; } = new();

            public SolidColorBrush ChannelColor =>
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex));

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string prop = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public class ForumPost : INotifyPropertyChanged
        {
            public long Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public string Author { get; set; }
            public string AvatarEmoji { get; set; } = "👤";
            public string AvatarColorHex { get; set; } = "#6C5CE7";
            public string Time { get; set; } = "刚刚";
            public int ChannelId { get; set; }
            public bool IsFromServer { get; set; }

            private int _likeCount;
            public int LikeCount
            {
                get => _likeCount;
                set { _likeCount = value; OnPropertyChanged(); }
            }

            private int _commentCount;
            public int CommentCount
            {
                get => _commentCount;
                set { _commentCount = value; OnPropertyChanged(); }
            }

            public int ViewCount { get; set; }

            private bool _isLiked;
            public bool IsLiked
            {
                get => _isLiked;
                set { _isLiked = value; OnPropertyChanged(); OnPropertyChanged(nameof(LikeButtonText)); }
            }

            public string LikeButtonText => IsLiked ? "👍 已赞" : "👍 点赞";
            public string LikeCountDisplay => LikeCount > 0 ? LikeCount.ToString() : "点赞";

            public ObservableCollection<ForumComment> Comments { get; set; } = new();

            public SolidColorBrush AvatarColor =>
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(AvatarColorHex ?? "#6C5CE7"));

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string prop = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public class ForumComment : INotifyPropertyChanged
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Content { get; set; }
            public string Author { get; set; }
            public string AvatarEmoji { get; set; }
            public string AvatarColorHex { get; set; }
            public string Time { get; set; } = "刚刚";

            private int _likeCount;
            public int LikeCount
            {
                get => _likeCount;
                set { _likeCount = value; OnPropertyChanged(); }
            }

            private bool _isLiked;
            public bool IsLiked
            {
                get => _isLiked;
                set { _isLiked = value; OnPropertyChanged(); }
            }

            public string LikeCountDisplay => _likeCount > 0 ? _likeCount.ToString() : "";

            public SolidColorBrush AvatarColor =>
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(AvatarColorHex ?? "#6C5CE7"));

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string prop = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        // ===== 私有字段 =====

        private ObservableCollection<ForumChannel> _channels = new();
        private ForumChannel _selectedChannel;
        private ForumPost _selectedPost;
        private bool _isLoading;

        private static readonly string[] ChannelColors = {
            "#6C5CE7", "#4CAF50", "#FF9800", "#E91E63",
            "#2196F3", "#9C27B0", "#00BCD4", "#FF5722",
            "#607D8B", "#795548"
        };

        // ===== 构造函数 =====

        public CommunityPage()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadChannelsAsync();
        }

        // ===== 服务器数据加载 =====

        private async System.Threading.Tasks.Task LoadChannelsAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                var serverChannels = await CommunityService.Instance.GetChannelsAsync();
                _channels.Clear();

                for (int i = 0; i < serverChannels.Count; i++)
                {
                    var sc = serverChannels[i];
                    _channels.Add(new ForumChannel
                    {
                        Id = sc.id,
                        Name = sc.name,
                        Description = sc.description,
                        Icon = string.IsNullOrEmpty(sc.icon_emoji) ? "💬" : sc.icon_emoji,
                        ColorHex = ChannelColors[i % ChannelColors.Length],
                        PostCount = sc.message_count,
                        Posts = new ObservableCollection<ForumPost>()
                    });
                }

                RefreshChannelList();
            }
            catch (Exception ex)
            {
                Logger.Error("[社区] 加载频道失败: " + ex.Message);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async System.Threading.Tasks.Task LoadMessagesAsync(ForumChannel channel)
        {
            try
            {
                var messages = await CommunityService.Instance.GetMessagesAsync(channel.Id);
                channel.Posts.Clear();

                foreach (var msg in messages)
                {
                    // 取消息前30字作为标题
                    string title = msg.content.Length > 30
                        ? msg.content[..30] + "..."
                        : msg.content;

                    channel.Posts.Add(new ForumPost
                    {
                        Id = msg.id,
                        Title = title,
                        Content = msg.content,
                        Author = msg.sender_id,
                        AvatarEmoji = "💬",
                        AvatarColorHex = ChannelColors[Math.Abs(msg.sender_id.GetHashCode()) % ChannelColors.Length],
                        Time = FormatTime(msg.created_at),
                        ChannelId = channel.Id,
                        IsFromServer = true,
                        Comments = new ObservableCollection<ForumComment>()
                    });
                }

                channel.PostCount = channel.Posts.Count;
            }
            catch (Exception ex)
            {
                Logger.Error($"[社区] 加载频道 {channel.Id} 消息失败: " + ex.Message);
            }
        }

        private static string FormatTime(string serverTime)
        {
            if (string.IsNullOrEmpty(serverTime)) return "";
            try
            {
                var dt = DateTime.Parse(serverTime);
                var span = DateTime.Now - dt;
                if (span.TotalMinutes < 1) return "刚刚";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}分钟前";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}小时前";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}天前";
                return dt.ToString("MM-dd HH:mm");
            }
            catch { return serverTime; }
        }

        // ===== 频道列表 =====

        private void RefreshChannelList()
        {
            lstChannels.ItemsSource = null;
            lstChannels.ItemsSource = _channels;
        }

        private void Channel_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is ForumChannel channel)
            {
                SelectChannel(channel);
            }
        }

        private async void SelectChannel(ForumChannel channel)
        {
            _selectedChannel = channel;
            channel.UnreadCount = 0;

            // 从服务器加载消息
            await LoadMessagesAsync(channel);
            RefreshPostList();
            ShowPostList();
        }

        private void RefreshPostList()
        {
            if (_selectedChannel == null) return;
            txtChannelTitle.Text = _selectedChannel.Icon + " " + _selectedChannel.Name;
            txtChannelDesc.Text = _selectedChannel.Description;

            lstPosts.ItemsSource = null;
            lstPosts.ItemsSource = _selectedChannel.Posts;
        }

        private void ShowPostList()
        {
            panelChannelList.Visibility = Visibility.Collapsed;
            panelPostList.Visibility = Visibility.Visible;
            panelPostDetail.Visibility = Visibility.Collapsed;
        }

        private void BtnBackToChannels_Click(object sender, RoutedEventArgs e)
        {
            ShowChannelList();
        }

        private async void ShowChannelList()
        {
            panelChannelList.Visibility = Visibility.Visible;
            panelPostList.Visibility = Visibility.Collapsed;
            panelPostDetail.Visibility = Visibility.Collapsed;
            await LoadChannelsAsync();
        }

        // ===== 刷新频道按钮 =====

        private async void BtnRefreshChannels_Click(object sender, RoutedEventArgs e)
        {
            await LoadChannelsAsync();
        }

        // ===== 创建频道 =====

        private async void BtnCreateChannel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateChannelDialog();
            dialog.Owner = Window.GetWindow(this);

            var window = Window.GetWindow(this);
            if (window != null)
            {
                dialog.Resources.MergedDictionaries.Clear();
                foreach (var dict in window.Resources.MergedDictionaries)
                    dialog.Resources.MergedDictionaries.Add(dict);
            }

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ChannelName))
            {
                bool ok = await CommunityService.Instance.CreateChannelAsync(
                    dialog.ChannelName.Trim(),
                    dialog.ChannelDescription?.Trim() ?? "",
                    dialog.ChannelIcon ?? "💬"
                );

                if (ok)
                {
                    ModernMessageBox.ShowSuccess("频道创建成功！", "创建成功");
                    await LoadChannelsAsync();
                }
                else
                {
                    ModernMessageBox.ShowWarning("频道创建失败，请稍后重试。", "创建失败");
                }
            }
        }

        // ===== 发帖 =====

        private void BtnCreatePost_Click(object sender, RoutedEventArgs e)
        {
            txtPostTitle.Text = "";
            txtPostContent.Text = "";
            panelCreatePost.Visibility = panelCreatePost.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void BtnSubmitPost_Click(object sender, RoutedEventArgs e)
        {
            string content = txtPostContent.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(content))
            {
                ModernMessageBox.ShowWarning("请输入消息内容");
                return;
            }

            if (_selectedChannel == null) return;

            string senderId = AccountManager.GetLatestMsLogin()?.UserName ?? "匿名用户";
            var result = await CommunityService.Instance.SendMessageAsync(_selectedChannel.Id, senderId, content);

            if (result.success)
            {
                // 插入本地显示
                _selectedChannel.Posts.Insert(0, new ForumPost
                {
                    Id = result.id,
                    Title = content.Length > 30 ? content[..30] + "..." : content,
                    Content = content,
                    Author = senderId,
                    AvatarEmoji = "👤",
                    AvatarColorHex = "#6C5CE7",
                    Time = "刚刚",
                    ChannelId = _selectedChannel.Id,
                    IsFromServer = false,
                    Comments = new ObservableCollection<ForumComment>()
                });
                _selectedChannel.PostCount = _selectedChannel.Posts.Count;

                RefreshPostList();
                panelCreatePost.Visibility = Visibility.Collapsed;
                txtPostTitle.Text = "";
                txtPostContent.Text = "";
            }
            else
            {
                ModernMessageBox.ShowWarning("发送失败，请稍后重试。");
            }
        }

        // ===== 查看帖子详情 =====

        private void PostItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is ForumPost post)
            {
                OpenPostDetail(post);
            }
        }

        private void OpenPostDetail(ForumPost post)
        {
            _selectedPost = post;
            post.ViewCount++;

            txtDetailTitle.Text = post.Title;
            txtDetailAuthor.Text = post.Author;
            txtDetailTime.Text = post.Time;
            txtDetailContent.Text = post.Content;

            if (detailAvatar.Child is TextBlock tb2)
                tb2.Text = post.AvatarEmoji;
            detailAvatar.Background = post.AvatarColor;

            btnDetailLike.Content = post.IsLiked ? "👍 已赞" : "👍 点赞";

            lstComments.ItemsSource = null;
            lstComments.ItemsSource = post.Comments;

            txtDetailStats.Text = $"👍 {post.LikeCount}  ·  💬 {post.CommentCount}  ·  👁 {post.ViewCount}";

            panelChannelList.Visibility = Visibility.Collapsed;
            panelPostList.Visibility = Visibility.Collapsed;
            panelPostDetail.Visibility = Visibility.Visible;
        }

        private void BtnBackToPosts_Click(object sender, RoutedEventArgs e)
        {
            panelPostDetail.Visibility = Visibility.Collapsed;
            panelPostList.Visibility = Visibility.Visible;
            panelChannelList.Visibility = Visibility.Collapsed;
            RefreshPostList();
        }

        // ===== 点赞 =====

        private void BtnLikePost_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var post = btn?.Tag as ForumPost ?? _selectedPost;
            if (post == null) return;

            if (post.IsLiked)
            {
                post.LikeCount = Math.Max(0, post.LikeCount - 1);
                post.IsLiked = false;
            }
            else
            {
                post.LikeCount++;
                post.IsLiked = true;
            }

            if (_selectedPost == post)
            {
                btnDetailLike.Content = post.IsLiked ? "👍 已赞" : "👍 点赞";
                txtDetailStats.Text = $"👍 {post.LikeCount}  ·  💬 {post.CommentCount}  ·  👁 {post.ViewCount}";
            }
        }

        private void BtnLikeComment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ForumComment comment)
            {
                if (comment.IsLiked)
                {
                    comment.LikeCount = Math.Max(0, comment.LikeCount - 1);
                    comment.IsLiked = false;
                }
                else
                {
                    comment.LikeCount++;
                    comment.IsLiked = true;
                }
                lstComments.ItemsSource = null;
                lstComments.ItemsSource = _selectedPost?.Comments;
            }
        }

        // ===== 评论 =====

        private void BtnSubmitComment_Click(object sender, RoutedEventArgs e)
        {
            string commentText = txtCommentInput.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(commentText)) return;
            if (_selectedPost == null) return;

            var comment = new ForumComment
            {
                Id = Guid.NewGuid().ToString(),
                Content = commentText,
                Author = "我",
                AvatarEmoji = "👤",
                AvatarColorHex = "#6C5CE7",
                Time = "刚刚",
                LikeCount = 0
            };

            _selectedPost.Comments.Add(comment);
            _selectedPost.CommentCount = _selectedPost.Comments.Count;

            lstComments.ItemsSource = null;
            lstComments.ItemsSource = _selectedPost.Comments;
            txtCommentInput.Text = "";
            txtDetailStats.Text = $"👍 {_selectedPost.LikeCount}  ·  💬 {_selectedPost.CommentCount}  ·  👁 {_selectedPost.ViewCount}";
        }

        private void TxtCommentInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                BtnSubmitComment_Click(sender, e);
                e.Handled = true;
            }
        }

        // ===== 分享 =====

        private void BtnShare_Click(object sender, RoutedEventArgs e)
        {
            var post = (sender as FrameworkElement)?.Tag as ForumPost ?? _selectedPost;
            if (post == null) return;

            string shareText = $"【MNL 社区】{post.Title}\n\n{post.Content[..Math.Min(post.Content.Length, 100)]}...\n\n—— {post.Author} 发布于 MNL 音符启动器";

            try
            {
                Clipboard.SetText(shareText);
                ModernMessageBox.ShowSuccess("帖子内容已复制到剪贴板，可以粘贴分享给朋友！", "分享成功");
            }
            catch
            {
                ModernMessageBox.ShowWarning("复制失败，请手动复制内容。", "分享失败");
            }
        }

        // ===== 鼠标滚轮滚动修复 =====

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }
    }
}
