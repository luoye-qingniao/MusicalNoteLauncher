using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicalNoteLauncher.Pages
{
    public partial class CommunityPage : UserControl
    {
        private ObservableCollection<PostItem> Posts { get; set; }

        public CommunityPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            LoadMockData();
        }

        private void LoadMockData()
        {
            Posts = new ObservableCollection<PostItem>
            {
                new PostItem
                {
                    Id = "1",
                    Author = "MC玩家小王",
                    AvatarEmoji = "🎮",
                    AvatarColor = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    Time = "2小时前",
                    Content = "今天终于通关了末影龙！分享一下我的经验：首先准备好足够的药水和装备，建议用钻石套加附魔，然后带一些末影珍珠方便传送。最重要的是要先摧毁所有的末影水晶，不然龙会一直回血！",
                    LikeCount = 128,
                    CommentCount = 23,
                    ViewCount = 856
                },
                new PostItem
                {
                    Id = "2",
                    Author = "建筑大师",
                    AvatarEmoji = "🏰",
                    AvatarColor = new SolidColorBrush(Color.FromRgb(236, 72, 153)),
                    Time = "5小时前",
                    Content = "花了一周时间建造的中世纪城堡，用了大量的石砖和橡木。特别喜欢塔楼的设计，每一层都有不同的功能。准备接下来建一个护城河和吊桥，让城堡更有气势！",
                    LikeCount = 256,
                    CommentCount = 45,
                    ViewCount = 1234
                },
                new PostItem
                {
                    Id = "3",
                    Author = "红石科技",
                    AvatarEmoji = "🔧",
                    AvatarColor = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    Time = "昨天",
                    Content = "分享一个实用的红石自动农场设计！用观察者检测作物成熟，然后活塞自动收割，水流收集到箱子里。效率很高，不用手动收割了。需要的话可以发详细教程！",
                    LikeCount = 312,
                    CommentCount = 67,
                    ViewCount = 2156
                },
                new PostItem
                {
                    Id = "4",
                    Author = "模组爱好者",
                    AvatarEmoji = "🧩",
                    AvatarColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                    Time = "昨天",
                    Content = "推荐几个必装的模组：Optifine提升画质、JEI物品管理器、Tweakeroo优化游戏体验、Sodium大幅提升帧率。这几个模组兼容性很好，强烈推荐！",
                    LikeCount = 445,
                    CommentCount = 89,
                    ViewCount = 3567
                },
                new PostItem
                {
                    Id = "5",
                    Author = "生存专家",
                    AvatarEmoji = "🌲",
                    AvatarColor = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                    Time = "2天前",
                    Content = "极限生存第100天纪念！从一无所有到拥有全套附魔装备和大型农场。最大的收获是找到了远古城市，拿到了下界合金装备。继续挑战！",
                    LikeCount = 567,
                    CommentCount = 112,
                    ViewCount = 4890
                }
            };

            lstPosts.ItemsSource = Posts;
        }

        private void BtnPost_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtPostContent.Text.Trim()))
            {
                MessageBox.Show("请输入帖子内容", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var newPost = new PostItem
            {
                Id = Guid.NewGuid().ToString(),
                Author = "我",
                AvatarEmoji = "👤",
                AvatarColor = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                Time = "刚刚",
                Content = txtPostContent.Text,
                LikeCount = 0,
                CommentCount = 0,
                ViewCount = 0
            };

            Posts.Insert(0, newPost);
            txtPostContent.Clear();
            MessageBox.Show("帖子发布成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnReply_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag != null)
            {
                var postId = button.Tag.ToString();
                MessageBox.Show($"正在回复帖子 {postId}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnLike_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag != null)
            {
                var postId = button.Tag.ToString();
                var post = Posts.FirstOrDefault(p => p.Id == postId);
                if (post != null)
                {
                    post.LikeCount++;
                }
            }
        }
    }

    public class PostItem : INotifyPropertyChanged
    {
        private string _id;
        private string _author;
        private string _avatarEmoji;
        private Brush _avatarColor;
        private string _time;
        private string _content;
        private int _likeCount;
        private int _commentCount;
        private int _viewCount;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Author
        {
            get => _author;
            set { _author = value; OnPropertyChanged(); }
        }

        public string AvatarEmoji
        {
            get => _avatarEmoji;
            set { _avatarEmoji = value; OnPropertyChanged(); }
        }

        public Brush AvatarColor
        {
            get => _avatarColor;
            set { _avatarColor = value; OnPropertyChanged(); }
        }

        public string Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged(); }
        }

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        public int LikeCount
        {
            get => _likeCount;
            set { _likeCount = value; OnPropertyChanged(); }
        }

        public int CommentCount
        {
            get => _commentCount;
            set { _commentCount = value; OnPropertyChanged(); }
        }

        public int ViewCount
        {
            get => _viewCount;
            set { _viewCount = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}




