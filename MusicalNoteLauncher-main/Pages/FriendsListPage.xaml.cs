using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Controls;
using MusicalNoteLauncher.Core;
using MusicalNoteLauncher.Utils;

namespace MusicalNoteLauncher.Pages
{
    public partial class FriendsListPage : UserControl
    {
        private ObservableCollection<FriendModel> _allFriends = new();
        private FriendModel _selectedFriend;
        private string _currentFilter = "全部";
        private string _searchText = "";
        private Brush _primaryBrush;
        private Brush _surfaceBrush;
        private Brush _cardHoverBrush;
        private MCTierManager _mctierManager;
        private GameLauncher _gameLauncher;

        public FriendsListPage()
        {
            InitializeComponent();
            _primaryBrush = (Brush)FindResource("PrimaryBrush");
            _surfaceBrush = (Brush)FindResource("SurfaceBrush");
            _cardHoverBrush = (Brush)FindResource("CardHoverBrush");
            _mctierManager = new MCTierManager();
            _gameLauncher = new GameLauncher(AppContext.MinecraftPath, new JavaConfigManager(AppContext.MinecraftPath));

            // 先加载本地缓存
            LoadLocalCache();

            // 从服务器加载好友列表
            Loaded += async (s, e) =>
            {
                await LoadFromServerAsync();
                StartFriendService();
            };
        }

        // ======== 好友系统启动/停止 ========

        private void StartFriendService()
        {
            var service = FriendService.Instance;
            if (!service.IsRunning)
            {
                service.Start(AppContext.QingniaoId);
                service.OnOnlineStatusChanged += OnFriendOnlineStatusChanged;
                service.OnNewMessages += OnNewServerMessages;
            }
        }

        private void OnFriendOnlineStatusChanged(List<string> onlineIds)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var f in _allFriends)
                {
                    // 通过 Name（即青鸟ID）匹配在线状态
                    f.IsOnline = onlineIds.Contains(f.Name);
                }
                ApplyFilter();
            });
        }

        private void OnNewServerMessages(List<ServerMessage> messages)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var msg in messages)
                {
                    // 查找对应好友
                    var friend = _allFriends.FirstOrDefault(f => f.Name == msg.sender_id);
                    if (friend == null) continue;

                    if (msg.msg_type == "Invite")
                    {
                        friend.ChatHistory.Add(new ChatMessage
                        {
                            SenderName = msg.sender_id,
                            Content = msg.content,
                            Timestamp = DateTime.Now,
                            IsFromMe = false,
                            MsgType = "Invite",
                            InviteNetworkName = msg.invite_network_name,
                            InviteNetworkSecret = msg.invite_network_secret,
                            InviteGameVersion = msg.invite_game_version,
                            InviteAccepted = msg.invite_accepted == 1
                        });
                    }
                    else
                    {
                        friend.ChatHistory.Add(new ChatMessage
                        {
                            SenderName = msg.sender_id,
                            Content = msg.content,
                            Timestamp = DateTime.Now,
                            IsFromMe = false
                        });
                    }
                }

                // 如果当前在聊天中，刷新消息显示
                if (_selectedFriend != null)
                    RefreshChatMessages();

                SaveLocalCache();

                // 联机邀请弹窗提示
                var invites = messages.Where(m => m.msg_type == "Invite");
                if (invites.Any())
                {
                    var first = invites.First();
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ModernMessageBox.ShowInfo($"收到来自 {first.sender_id} 的联机邀请", "新邀请");
                    }));
                }
            });
        }

        // ======== 数据持久化（本地缓存） ========

        private string CachePath => Path.Combine(AppContext.MinecraftPath, "friends_cache.json");

        private void LoadLocalCache()
        {
            try
            {
                if (File.Exists(CachePath))
                {
                    var json = File.ReadAllText(CachePath);
                    var data = JsonSerializer.Deserialize<FriendsData>(json);
                    if (data?.Friends != null)
                    {
                        foreach (var item in data.Friends)
                        {
                            _allFriends.Add(new FriendModel
                            {
                                Name = item.Name,
                                AvatarEmoji = item.AvatarEmoji,
                                AvatarColorValue = Color.FromRgb(item.AvatarR, item.AvatarG, item.AvatarB),
                                IsOnline = false, // 从服务器获取在线状态
                                ChatHistory = item.ChatHistory ?? new List<ChatMessage>()
                            });
                        }
                    }
                }
            }
            catch { /* 忽略缓存错误 */ }

            ApplyFilter();
        }

        private void SaveLocalCache()
        {
            try
            {
                var data = new FriendsData();
                foreach (var f in _allFriends)
                {
                    data.Friends.Add(new FriendStorageItem
                    {
                        Name = f.Name,
                        AvatarEmoji = f.AvatarEmoji,
                        AvatarR = f.AvatarColorValue.R,
                        AvatarG = f.AvatarColorValue.G,
                        AvatarB = f.AvatarColorValue.B,
                        IsOnline = f.IsOnline,
                        ChatHistory = f.ChatHistory
                    });
                }
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CachePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存好友缓存失败: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadFromServerAsync()
        {
            try
            {
                var serverFriends = await FriendService.Instance.GetFriendsAsync();

                // 合并服务器数据到本地
                foreach (var sf in serverFriends)
                {
                    var existing = _allFriends.FirstOrDefault(f => f.Name == sf.friend_id);
                    if (existing != null)
                    {
                        existing.IsOnline = sf.is_online == 1;
                    }
                    else
                    {
                        // 新好友（由对方添加），加入列表
                        _allFriends.Add(new FriendModel
                        {
                            Name = sf.friend_id,
                            AvatarEmoji = "👤",
                            AvatarColorValue = Color.FromRgb(0x21, 0x96, 0xF3),
                            IsOnline = sf.is_online == 1
                        });
                    }
                }

                ApplyFilter();
                SaveLocalCache();
            }
            catch { /* 服务器不可达，使用本地缓存 */ }
        }

        // ======== 筛选 ========

        private void ApplyFilter()
        {
            IEnumerable<FriendModel> filtered;
            switch (_currentFilter)
            {
                case "在线":
                    filtered = _allFriends.Where(f => f.IsOnline);
                    break;
                case "离线":
                    filtered = _allFriends.Where(f => !f.IsOnline);
                    break;
                default:
                    filtered = _allFriends;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var search = _searchText.Trim().ToLower();
                filtered = filtered.Where(f =>
                    f.Name.ToLower().Contains(search) ||
                    f.FriendName.ToLower().Contains(search));
            }

            lstFriends.ItemsSource = new ObservableCollection<FriendModel>(filtered);
            UpdateFriendCount();
        }

        private void UpdateFriendCount()
        {
            int total = _allFriends.Count;
            int online = _allFriends.Count(f => f.IsOnline);
            txtFriendCount.Text = $"共 {total} 位好友，{online} 人在线";
        }

        private void ResetFilterButtons()
        {
            btnFilterAll.Resources.Clear();
            btnFilterAll.Resources.Add("SurfaceBrush", _surfaceBrush);
        }

        private void HighlightFilterButton(Button activeBtn)
        {
            ResetFilterButtons();
            activeBtn.Resources.Clear();
            activeBtn.Resources.Add("SurfaceBrush", _primaryBrush);
        }

        // ======== 聊天面板 ========

        private void OpenChat(FriendModel friend)
        {
            _selectedFriend = friend;
            txtChatTitle.Text = $"与 {friend.FriendName} 聊天";
            btnCloseChat.Visibility = Visibility.Visible;
            bdChatPlaceholder.Visibility = Visibility.Collapsed;
            svChatMessages.Visibility = Visibility.Visible;
            bdChatInput.Visibility = Visibility.Visible;
            RefreshChatMessages();
        }

        private void RefreshChatMessages()
        {
            pnlChatMessages.Children.Clear();
            if (_selectedFriend == null) return;

            var textPri = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
            var textSec = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;

            foreach (var msg in _selectedFriend.ChatHistory)
            {
                if (msg.MsgType == "Invite")
                    RenderInviteMessage(msg, textPri, textSec);
                else
                    RenderNormalMessage(msg, textPri, textSec);
            }

            svChatMessages.ScrollToEnd();
        }

        private void RenderNormalMessage(ChatMessage msg, Brush textPri, Brush textSec)
        {
            var msgBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = msg.IsFromMe ? new Thickness(40, 2, 0, 2) : new Thickness(0, 2, 40, 2),
                HorizontalAlignment = msg.IsFromMe ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = msg.IsFromMe
                    ? new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3))
                    : (FindResource("SurfaceBrush") as Brush ?? Brushes.Gray)
            };

            var msgStack = new StackPanel();
            msgStack.Children.Add(new TextBlock
            {
                Text = $"{msg.SenderName}  {msg.Timestamp:HH:mm}",
                FontSize = 10, Foreground = msg.IsFromMe ? Brushes.White : textSec,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 2)
            });
            msgStack.Children.Add(new TextBlock
            {
                Text = msg.Content,
                FontSize = 13,
                Foreground = msg.IsFromMe ? Brushes.White : textPri,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap
            });

            msgBorder.Child = msgStack;
            pnlChatMessages.Children.Add(msgBorder);
        }

        private void RenderInviteMessage(ChatMessage msg, Brush textPri, Brush textSec)
        {
            var inviteBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = msg.IsFromMe ? new Thickness(30, 2, 0, 2) : new Thickness(0, 2, 30, 2),
                HorizontalAlignment = msg.IsFromMe ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = msg.IsFromMe
                    ? new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3))
                    : (FindResource("SurfaceBrush") as Brush ?? Brushes.Gray),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4)),
                BorderThickness = new Thickness(2),
                MinWidth = 220
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "🎮 联机邀请",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = msg.IsFromMe ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4)),
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"来自：{msg.SenderName}  {msg.Timestamp:HH:mm}",
                FontSize = 10,
                Foreground = msg.IsFromMe ? Brushes.White : textSec,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 6)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"网络名：{msg.InviteNetworkName}",
                FontSize = 12,
                Foreground = msg.IsFromMe ? Brushes.White : textPri,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 2),
                TextWrapping = TextWrapping.Wrap
            });

            if (msg.InviteAccepted)
            {
                stack.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 8, 0, 0),
                    Child = new TextBlock
                    {
                        Text = "✅ 已加入房间",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                });
            }
            else
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "请在下方选择操作：",
                    FontSize = 11,
                    Foreground = msg.IsFromMe ? Brushes.White : textSec,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                    Margin = new Thickness(0, 8, 0, 0)
                });

                var btnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var acceptBtn = new Button
                {
                    Content = "✅ 同意",
                    Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(18, 8, 18, 8),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 0, 10, 0),
                    Tag = msg
                };
                acceptBtn.Click += BtnAcceptInvite_Click;
                btnPanel.Children.Add(acceptBtn);

                var startBtn = new Button
                {
                    Content = "▶ 开始游戏",
                    Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(18, 8, 18, 8),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                    Cursor = Cursors.Hand,
                    Tag = msg
                };
                startBtn.Click += BtnStartGameFromInvite_Click;
                btnPanel.Children.Add(startBtn);

                stack.Children.Add(btnPanel);
            }

            inviteBorder.Child = stack;
            pnlChatMessages.Children.Add(inviteBorder);
        }

        // ======== 邀请处理 ========

        private void BtnAcceptInvite_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var msg = button?.Tag as ChatMessage;
            if (msg == null) return;

            if (!_mctierManager.IsCoreInstalled())
            {
                ModernMessageBox.ShowWarning("请先下载安装 MCTier 核心\n\n前往 [联机社交] → [MCTier 联机] 下载安装", "提示");
                return;
            }

            if (_mctierManager.IsRunning)
                _mctierManager.Stop();

            if (_mctierManager.Start(msg.InviteNetworkName, msg.InviteNetworkSecret))
            {
                msg.InviteAccepted = true;

                if (_selectedFriend != null)
                {
                    _selectedFriend.ChatHistory.Add(new ChatMessage
                    {
                        SenderName = "系统",
                        Content = $"✅ 已加入联机房间\n网络名：{msg.InviteNetworkName}",
                        Timestamp = DateTime.Now,
                        IsFromMe = true,
                        MsgType = "Normal"
                    });
                }

                RefreshChatMessages();
                SaveLocalCache();

                ModernMessageBox.ShowInfo(
                    $"已成功加入联机房间！\n\n网络名：{msg.InviteNetworkName}\n\n你可以点击「开始游戏」启动 Minecraft 联机。",
                    "加入成功");
            }
            else
            {
                ModernMessageBox.ShowError("加入房间失败，请检查网络连接", "加入失败");
            }
        }

        private async void BtnStartGameFromInvite_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var msg = button?.Tag as ChatMessage;
            if (msg == null) return;

            if (!msg.InviteAccepted)
            {
                if (!_mctierManager.IsCoreInstalled())
                {
                    ModernMessageBox.ShowWarning("请先下载安装 MCTier 核心\n\n前往 [联机社交] → [MCTier 联机] 下载安装", "提示");
                    return;
                }

                if (_mctierManager.IsRunning)
                    _mctierManager.Stop();

                if (_mctierManager.Start(msg.InviteNetworkName, msg.InviteNetworkSecret))
                    msg.InviteAccepted = true;
                else
                {
                    ModernMessageBox.ShowError("加入房间失败，请检查网络连接", "加入失败");
                    return;
                }
            }

            string versionId = msg.InviteGameVersion ?? AppContext.SelectedGameVersion;
            if (string.IsNullOrEmpty(versionId))
            {
                var versions = GetInstalledVersions();
                if (versions.Count > 0)
                    versionId = versions.OrderByDescending(v => v).First();
                else
                {
                    ModernMessageBox.ShowWarning("未找到已安装的游戏版本，请先在启动页安装一个版本", "提示");
                    return;
                }
            }

            string username = AppContext.Username ?? "Player";
            bool success = await _gameLauncher.LaunchGameAsync(
                versionId, username, 1024, 4096, "", true);

            if (success)
            {
                if (_selectedFriend != null)
                {
                    _selectedFriend.ChatHistory.Add(new ChatMessage
                    {
                        SenderName = "系统",
                        Content = $"🎮 已启动 Minecraft {versionId}\n联机房间：{msg.InviteNetworkName}",
                        Timestamp = DateTime.Now, IsFromMe = true, MsgType = "Normal"
                    });
                }
                RefreshChatMessages();
                SaveLocalCache();

                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                    parentWindow.WindowState = WindowState.Minimized;
            }
            else
            {
                ModernMessageBox.ShowError("游戏启动失败，请检查日志", "启动失败");
            }
        }

        private List<string> GetInstalledVersions()
        {
            var versions = new List<string>();
            string versionsPath = Path.Combine(AppContext.MinecraftPath, "versions");
            if (Directory.Exists(versionsPath))
            {
                foreach (var dir in Directory.GetDirectories(versionsPath))
                {
                    string versionName = Path.GetFileName(dir);
                    string jsonFile = Path.Combine(dir, versionName + ".json");
                    string jarFile = Path.Combine(dir, versionName + ".jar");
                    if (File.Exists(jsonFile) && File.Exists(jarFile))
                        versions.Add(versionName);
                }
            }
            return versions;
        }

        // ======== 事件处理 ========

        private async void BtnAddFriend_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddFriendDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                if (_allFriends.Any(f => f.Name.Equals(dialog.FriendName, StringComparison.OrdinalIgnoreCase)))
                {
                    ModernMessageBox.ShowWarning("已存在同名好友", "提示");
                    return;
                }

                // 通过服务器添加好友
                var (success, error) = await FriendService.Instance.AddFriendAsync(dialog.FriendName, dialog.FriendName);
                if (!success)
                {
                    ModernMessageBox.ShowWarning(error ?? "添加失败，请检查网络连接", "添加失败");
                    return;
                }

                var friend = new FriendModel
                {
                    Name = dialog.FriendName,
                    AvatarEmoji = dialog.AvatarEmoji,
                    AvatarColorValue = dialog.AvatarColor,
                    IsOnline = true
                };
                _allFriends.Add(friend);
                ApplyFilter();
                SaveLocalCache();

                ModernMessageBox.ShowSuccess($"已向 {dialog.FriendName} 发送好友请求", "添加成功");
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            HighlightFilterButton(button);
            _currentFilter = button.Content.ToString();
            ApplyFilter();
        }

        private void LstFriends_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstFriends.SelectedItem is FriendModel friend)
                OpenChat(friend);
        }

        private async void BtnInvite_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var friend = button?.DataContext as FriendModel;
            if (friend == null) return;

            if (!_mctierManager.IsCoreInstalled())
            {
                ModernMessageBox.ShowWarning("请先下载安装 MCTier 核心\n\n前往 [联机社交] → [MCTier 联机] 下载安装", "提示");
                return;
            }

            var (name, secret) = _mctierManager.GenerateNetworkCredentials();
            if (!_mctierManager.Start(name, secret))
            {
                ModernMessageBox.ShowError("创建联机房间失败，请检查网络连接", "邀请失败");
                return;
            }

            // 通过服务器发送邀请
            var gameVersion = AppContext.SelectedGameVersion ?? "";
            await FriendService.Instance.SendInviteAsync(friend.Name, name, secret, gameVersion);

            var inviteMsg = new ChatMessage
            {
                SenderName = "我",
                Content = $"🎮 联机邀请\n网络名：{name}\n密钥：{secret}",
                Timestamp = DateTime.Now,
                IsFromMe = true,
                MsgType = "Invite",
                InviteNetworkName = name,
                InviteNetworkSecret = secret,
                InviteAccepted = false,
                InviteGameVersion = gameVersion
            };
            friend.ChatHistory.Add(inviteMsg);

            var confirmMsg = new ChatMessage
            {
                SenderName = "系统",
                Content = $"✅ 已向 {friend.FriendName} 发送联机邀请\n房间网络名：{name}",
                Timestamp = DateTime.Now,
                IsFromMe = true,
                MsgType = "Normal"
            };
            friend.ChatHistory.Add(confirmMsg);

            RefreshChatMessages();
            SaveLocalCache();

            ModernMessageBox.ShowInfo(
                $"联机房间已创建！\n\n网络名：{name}\n密钥：{secret}\n\n邀请已发送给 {friend.FriendName}，\n对方可在聊天中点击「同意」加入房间。",
                "邀请发送成功");
        }

        private void BtnChat_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var friend = button?.DataContext as FriendModel;
            if (friend == null) return;
            OpenChat(friend);
        }

        private void BtnCloseChat_Click(object sender, RoutedEventArgs e)
        {
            _selectedFriend = null;
            txtChatTitle.Text = "聊天";
            btnCloseChat.Visibility = Visibility.Collapsed;
            bdChatPlaceholder.Visibility = Visibility.Visible;
            svChatMessages.Visibility = Visibility.Collapsed;
            bdChatInput.Visibility = Visibility.Collapsed;
            pnlChatMessages.Children.Clear();
            lstFriends.SelectedIndex = -1;
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendMessage();
                e.Handled = true;
            }
        }

        private async void SendMessage()
        {
            if (_selectedFriend == null)
            {
                ModernMessageBox.ShowWarning("请先选择一个好友", "提示");
                return;
            }

            var content = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(content))
            {
                ModernMessageBox.ShowWarning("请输入消息内容", "提示");
                return;
            }

            // 先通过服务器发送
            await FriendService.Instance.SendMessageAsync(_selectedFriend.Name, content);

            var msg = new ChatMessage
            {
                SenderName = "我",
                Content = content,
                Timestamp = DateTime.Now,
                IsFromMe = true
            };
            _selectedFriend.ChatHistory.Add(msg);
            txtMessage.Text = "";
            RefreshChatMessages();
            SaveLocalCache();
        }

        // ======== 搜索 ========

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = txtSearch.Text.Trim();
            btnClearSearch.Visibility = string.IsNullOrEmpty(_searchText)
                ? Visibility.Collapsed : Visibility.Visible;
            ApplyFilter();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            _searchText = "";
            btnClearSearch.Visibility = Visibility.Collapsed;
            ApplyFilter();
        }
    }
}
