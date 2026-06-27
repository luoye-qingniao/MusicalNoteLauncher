using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Controls;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class CommunityPage : UserControl
    {
        // ===== 数据模型 =====

        public class ForumChannel : INotifyPropertyChanged
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
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

            // 用于 UI 绑定的转换属性
            public SolidColorBrush ChannelColor =>
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex));

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string prop = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public class ForumPost : INotifyPropertyChanged
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Title { get; set; }
            public string Content { get; set; }
            public string Author { get; set; }
            public string AvatarEmoji { get; set; }
            public string AvatarColorHex { get; set; }
            public string Time { get; set; } = "刚刚";
            public string ChannelId { get; set; }

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
        private string _dataFilePath;
        private static readonly string[] AvatarColors = {
            "#6C5CE7", "#E91E63", "#4CAF50", "#FF9800",
            "#2196F3", "#9C27B0", "#00BCD4", "#FF5722",
            "#607D8B", "#795548"
        };

        private static readonly (string emoji, string name)[] MockAuthors = {
            ("🎮", "MC玩家小王"), ("🏰", "建筑大师"), ("🔧", "红石科技"),
            ("🧩", "模组爱好者"), ("🌲", "生存专家"), ("⚔️", "PVP高手"),
            ("📦", "整合包作者"), ("🎨", "材质画师"), ("💻", "编程达人"),
            ("🌟", "服务器主")
        };

        // ===== 构造函数 =====

        public CommunityPage()
        {
            InitializeComponent();
            _dataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "forum_data.json");
            LoadData();
            Loaded += (s, e) => RefreshChannelList();
        }

        // ===== 数据持久化 =====

        private void LoadData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    string json = File.ReadAllText(_dataFilePath);
                    var channels = JsonSerializer.Deserialize<List<ForumChannel>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (channels != null && channels.Count > 0)
                    {
                        // 兜底: 如果从旧 JSON 加载的数据缺失 Description / Icon, 用默认值补全
                        var defaults = GetDefaultChannels();
                        foreach (var ch in channels)
                        {
                            if (string.IsNullOrEmpty(ch.Description))
                            {
                                var def = defaults.FirstOrDefault(d => d.Id == ch.Id);
                                if (def != null) ch.Description = def.Description;
                            }
                            if (string.IsNullOrEmpty(ch.Icon) && ch.Icon != "📌")
                            {
                                var def = defaults.FirstOrDefault(d => d.Id == ch.Id);
                                if (def != null) ch.Icon = def.Icon;
                            }
                        }
                        _channels = new ObservableCollection<ForumChannel>(channels);
                        return;
                    }
                }
            }
            catch { }

            // 无保存数据 → 初始化默认频道
            InitializeDefaultChannels();
        }

        private void SaveData()
        {
            try
            {
                var json = JsonSerializer.Serialize(_channels.ToList(),
                    new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Error("[社区] 保存数据失败: " + ex.Message);
            }
        }

        private void InitializeDefaultChannels()
        {
            var channels = GetDefaultChannels();
            foreach (var ch in channels)
            {
                ch.Posts = new ObservableCollection<ForumPost>(GenerateMockPosts(ch.Id, ch.Name));
                ch.PostCount = ch.Posts.Count;
            }

            _channels = new ObservableCollection<ForumChannel>(channels);
            SaveData();
        }

        /// <summary>
        /// 返回预设频道的纯元数据 (不含帖子)
        /// </summary>
        private static List<ForumChannel> GetDefaultChannels()
        {
            return new List<ForumChannel>
            {
                new() { Id = "general",  Name = "综合讨论", Description = "Minecraft 相关话题自由交流", Icon = "💬", ColorHex = "#6C5CE7" },
                new() { Id = "mods",     Name = "模组交流", Description = "模组推荐、问题讨论与使用心得", Icon = "🧩", ColorHex = "#4CAF50" },
                new() { Id = "builds",   Name = "建筑展示", Description = "分享你的建筑作品和设计灵感", Icon = "🏰", ColorHex = "#FF9800" },
                new() { Id = "redstone", Name = "红石科技", Description = "红石电路、机械、自动化的技术交流", Icon = "🔧", ColorHex = "#E91E63" },
                new() { Id = "help",     Name = "求助问答", Description = "遇到问题？来这里寻求帮助", Icon = "❓", ColorHex = "#2196F3" },
                new() { Id = "servers",  Name = "服务器宣传", Description = "宣传和寻找心仪的 Minecraft 服务器", Icon = "🌐", ColorHex = "#9C27B0" },
                new() { Id = "creations",Name = "原创作品", Description = "数据包、资源包、地图等原创内容分享", Icon = "🎨", ColorHex = "#00BCD4" },
            };
        }

        private List<ForumPost> GenerateMockPosts(string channelId, string channelName)
        {
            var posts = new List<ForumPost>();
            var random = new Random(channelId.GetHashCode());

            for (int i = 0; i < 3 + random.Next(3); i++)
            {
                var author = MockAuthors[random.Next(MockAuthors.Length)];
                var postId = $"{channelId}_post_{i}";
                var post = new ForumPost
                {
                    Id = postId,
                    Title = GetMockTitle(channelId, i),
                    Content = GetMockContent(channelId),
                    Author = author.name,
                    AvatarEmoji = author.emoji,
                    AvatarColorHex = AvatarColors[random.Next(AvatarColors.Length)],
                    Time = $"{random.Next(1, 48)}小时前",
                    ChannelId = channelId,
                    LikeCount = random.Next(0, 500),
                    CommentCount = random.Next(0, 50),
                    ViewCount = random.Next(100, 5000),
                };
                post.Comments = new ObservableCollection<ForumComment>(
                    Enumerable.Range(0, random.Next(0, 4)).Select(j =>
                    {
                        var ca = MockAuthors[random.Next(MockAuthors.Length)];
                        return new ForumComment
                        {
                            Id = $"{postId}_comment_{j}",
                            Content = GetMockComment(channelId),
                            Author = ca.name,
                            AvatarEmoji = ca.emoji,
                            AvatarColorHex = AvatarColors[random.Next(AvatarColors.Length)],
                            Time = $"{random.Next(0, 12)}小时前",
                            LikeCount = random.Next(0, 20)
                        };
                    }));
                posts.Add(post);
            }
            return posts;
        }

        private static string GetMockTitle(string chId, int i) => chId switch
        {
            "general" => new[] { "新版本生存体验分享", "MC 最有趣的瞬间", "大家最喜欢哪个版本？", "游戏技巧交流帖", "关于新生物投票的讨论" }[i % 5],
            "mods" => new[] { "必装的优化模组推荐", "Forge vs Fabric 选哪个？", "这个模组的兼容性问题", "光影模组横向对比", "模组整合包推荐" }[i % 5],
            "builds" => new[] { "中世纪城堡建筑展示", "现代别墅设计分享", "像素艺术创作", "花园庭院布置", "水下基地一周年" }[i % 5],
            "redstone" => new[] { "全自动农场教程", "高效刷怪塔设计", "红石计算器制作", "物品分类系统", "红石电梯原理" }[i % 5],
            "help" => new[] { "游戏崩溃怎么解决？", "联机问题求助", "模组冲突排查", "帧数优化方法", "存档损坏修复" }[i % 5],
            "servers" => new[] { "纯净生存服招募玩家", "模组服招新公告", "建筑服务器宣传", "小游戏服务器", "RP 角色扮演服" }[i % 5],
            "creations" => new[] { "自制数据包分享", "原创材质包发布", "自制地图冒险模式", "音效包分享", "原创皮肤系列" }[i % 5],
            _ => "新帖子"
        };

        private static string GetMockContent(string chId) => chId switch
        {
            "general" => "分享一下最近的游戏体验。不管是原版生存还是加了模组，Minecraft 总是能带来惊喜。大家都玩的什么模式？",
            "mods" => "推荐几个特别好用的模组。Tweakeroo 的快速放置功能太实用了，Sodium 提升了很多帧数，还有 JEI 方便查看合成配方。你们用过哪些必装的模组？",
            "builds" => "花了不少时间建造这个，用了大量的石砖和橡木。特别喜欢内部的空间设计，每个房间都有不同的主题。欢迎提出改进建议！",
            "redstone" => "这个红石装置的核心是用观察者检测变化，然后通过比较器输出信号。活塞组用来收割，漏斗矿车在水流下收集掉落物。效率很高！",
            "help" => "游戏启动后就闪退，日志显示有个模组冲突。试过了重装 Java 和更新显卡驱动，还是不行。有没有遇到类似情况的朋友？",
            "servers" => "服务器版本 1.21，安装了少量的辅助模组。主打原版生存体验，有商店系统、领地保护和玩家交易。欢迎各位加入！",
            "creations" => "这个数据包新增了 20 多个合成配方，增加了几个新的生物掉落物，还有一些隐藏进度。适合喜欢探索和收集的玩家。",
            _ => "内容加载中..."
        };

        private static string GetMockComment(string chId) => chId switch
        {
            "general" => "说得太对了！我也是这么觉得的。",
            "mods" => "这几个模组我都用过，确实很好。",
            "builds" => "建筑太漂亮了！能分享一下存档吗？",
            "redstone" => "教程很详细，学习了。",
            "help" => "试试更新一下显卡驱动。",
            "servers" => "服务器很好玩，已经加入啦！",
            "creations" => "太棒了，已下载试试看。",
            _ => "支持一下！"
        };

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

        private void SelectChannel(ForumChannel channel)
        {
            _selectedChannel = channel;
            channel.UnreadCount = 0;

            // 更新频道列表选中状态
            foreach (var item in lstChannels.Items)
            {
                if (item is FrameworkElement fe && fe.Tag == channel)
                {
                    // 高亮逻辑由 XAML DataTrigger 处理
                }
            }

            // 刷新帖子列表
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

        private void ShowChannelList()
        {
            panelChannelList.Visibility = Visibility.Visible;
            panelPostList.Visibility = Visibility.Collapsed;
            panelPostDetail.Visibility = Visibility.Collapsed;
            RefreshChannelList();
        }

        // ===== 创建频道 =====

        private void BtnCreateChannel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateChannelDialog();
            dialog.Owner = Window.GetWindow(this);

            // 从父窗口获取主题资源
            var window = Window.GetWindow(this);
            if (window != null)
            {
                dialog.Resources.MergedDictionaries.Clear();
                foreach (var dict in window.Resources.MergedDictionaries)
                    dialog.Resources.MergedDictionaries.Add(dict);
            }

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ChannelName))
            {
                var newChannel = new ForumChannel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = dialog.ChannelName.Trim(),
                    Description = dialog.ChannelDescription?.Trim() ?? "新创建的频道",
                    Icon = dialog.ChannelIcon ?? "📌",
                    ColorHex = dialog.ChannelColor ?? "#6C5CE7",
                    PostCount = 0,
                    Posts = new ObservableCollection<ForumPost>()
                };

                _channels.Add(newChannel);
                RefreshChannelList();
                SaveData();
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

        private void BtnSubmitPost_Click(object sender, RoutedEventArgs e)
        {
            string title = txtPostTitle.Text?.Trim() ?? "";
            string content = txtPostContent.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(title))
            {
                ModernMessageBox.ShowWarning("请输入帖子标题");
                return;
            }
            if (string.IsNullOrEmpty(content))
            {
                ModernMessageBox.ShowWarning("请输入帖子内容");
                return;
            }

            var random = new Random();
            var author = MockAuthors[random.Next(MockAuthors.Length)];
            var post = new ForumPost
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                Content = content,
                Author = "我",
                AvatarEmoji = "👤",
                AvatarColorHex = "#6C5CE7",
                Time = "刚刚",
                ChannelId = _selectedChannel?.Id,
                LikeCount = 0,
                CommentCount = 0,
                ViewCount = 0,
                Comments = new ObservableCollection<ForumComment>()
            };

            _selectedChannel.Posts.Insert(0, post);
            _selectedChannel.PostCount = _selectedChannel.Posts.Count;

            RefreshPostList();
            panelCreatePost.Visibility = Visibility.Collapsed;
            SaveData();
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

            // 绑定详情数据
            txtDetailTitle.Text = post.Title;
            txtDetailAuthor.Text = post.Author;
            txtDetailTime.Text = post.Time;
            txtDetailContent.Text = post.Content;

            // 头像
            if (detailAvatar.Child is TextBlock tb2)
                tb2.Text = post.AvatarEmoji;
            detailAvatar.Background = post.AvatarColor;

            // 点赞按钮
            btnDetailLike.Content = post.IsLiked ? "👍 已赞" : "👍 点赞";

            // 评论列表
            lstComments.ItemsSource = null;
            lstComments.ItemsSource = post.Comments;

            // 统计
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
            SaveData();
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
                SaveData();
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
            SaveData();
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

            string shareText = $"【MNL 社区】{post.Title}\n\n{post.Content.Substring(0, Math.Min(post.Content.Length, 100))}...\n\n—— {post.Author} 发布于 MNL 音符启动器";

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

        /// <summary>
        /// 解决 ListBox 内部 ScrollViewer 截获鼠标滚轮导致外层 ScrollViewer 无法滚动的问题
        /// </summary>
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
