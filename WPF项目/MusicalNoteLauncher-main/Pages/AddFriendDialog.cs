using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Pages
{
    public partial class AddFriendDialog : Window
    {
        public string FriendName { get; private set; }
        public string AvatarEmoji { get; private set; } = "👤";
        public Color AvatarColor { get; private set; } = Color.FromRgb(0x21, 0x96, 0xF3);

        private static readonly List<string> _defaultEmojis = new()
        {
            "👤", "😊", "🎮", "🦸", "🧙", "🐱", "🐶", "🦊", "🐼", "🐨", "🦄", "🐉",
        };

        private static readonly Color[] _defaultColors = new[]
        {
            Color.FromRgb(0x21, 0x96, 0xF3), Color.FromRgb(0xF4, 0x43, 0x36),
            Color.FromRgb(0x4C, 0xAF, 0x50), Color.FromRgb(0xFF, 0x98, 0x00),
            Color.FromRgb(0x9C, 0x27, 0xB0), Color.FromRgb(0x00, 0xBC, 0xD4),
            Color.FromRgb(0xE9, 0x1E, 0x63), Color.FromRgb(0x60, 0x7D, 0x8B),
        };

        private Border _previewAvatar;
        private TextBlock _previewEmoji;
        private TextBox _txtName;

        public AddFriendDialog()
        {
            MinWidth = 400;
            MinHeight = 280;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;
            Title = "添加好友";

            var textPri = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
            var textSec = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
            var surfaceBg = TryFindResource("SurfaceBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            var cardBg = TryFindResource("CardBackgroundBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            var borderBr = TryFindResource("BorderBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
            var primaryBrush = TryFindResource("PrimaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));

            var root = new Border
            {
                Background = cardBg,
                CornerRadius = new CornerRadius(16),
                BorderBrush = borderBr,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4)
            };

            var panel = new StackPanel { Margin = new Thickness(24) };

            // 标题栏
            var titleBar = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition());
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.Children.Add(new TextBlock
            {
                Text = "添加好友",
                FontSize = 18, FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = textPri, FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei")
            });
            var closeBtn = new Button
            {
                Content = "✕", FontSize = 14, Cursor = System.Windows.Input.Cursors.Hand,
                Foreground = textSec, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 2, 8, 2)
            };
            closeBtn.Click += (s, ev) => { DialogResult = false; Close(); };
            Grid.SetColumn(closeBtn, 1);
            titleBar.Children.Add(closeBtn);
            panel.Children.Add(titleBar);

            // ID 输入区域
            panel.Children.Add(new TextBlock
            {
                Text = "青鸟账号唯一ID名",
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = textPri,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 4)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "请输入好友的青鸟账号唯一ID名，系统将自动生成头像标识",
                FontSize = 12,
                Foreground = textSec,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });

            // 输入框 + 预览
            var inputRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputRow.ColumnDefinitions.Add(new ColumnDefinition());

            // 实时预览头像
            _previewAvatar = new Border
            {
                Width = 48, Height = 48, CornerRadius = new CornerRadius(24),
                Background = new SolidColorBrush(AvatarColor),
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _previewEmoji = new TextBlock
            {
                Text = AvatarEmoji, FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            _previewAvatar.Child = _previewEmoji;
            Grid.SetColumn(_previewAvatar, 0);
            inputRow.Children.Add(_previewAvatar);

            _txtName = new TextBox
            {
                FontSize = 15,
                Padding = new Thickness(12, 10, 12, 10),
                Background = surfaceBg,
                Foreground = textPri,
                BorderBrush = borderBr,
                BorderThickness = new Thickness(1),
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Center
            };
            _txtName.TextChanged += (s, ev) =>
            {
                var id = _txtName.Text.Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    var (emoji, color) = DeriveAvatarFromId(id);
                    _previewEmoji.Text = emoji;
                    _previewAvatar.Background = new SolidColorBrush(color);
                }
                else
                {
                    _previewEmoji.Text = "👤";
                    _previewAvatar.Background = new SolidColorBrush(AvatarColor);
                }
            };
            Grid.SetColumn(_txtName, 1);
            inputRow.Children.Add(_txtName);
            panel.Children.Add(inputRow);

            // 提示信息
            var hintBorder = new Border
            {
                Background = surfaceBg,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var hintText = new TextBlock
            {
                Text = "💡 青鸟账号ID是好友在启动器「个人资料 → 青鸟账号」中设置的唯一标识名。\n添加后可通过好友列表直接发送联机邀请。",
                FontSize = 12,
                Foreground = textSec,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap
            };
            hintBorder.Child = hintText;
            panel.Children.Add(hintBorder);

            // 按钮行
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancelBtn = new Button
            {
                Content = "取消", FontSize = 13, Padding = new Thickness(16, 8, 16, 8),
                Foreground = textPri, Background = Brushes.Transparent,
                BorderBrush = borderBr, BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 8, 0),
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei")
            };
            cancelBtn.Click += (s, ev) => { DialogResult = false; Close(); };
            btnRow.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content = "确认添加",
                FontSize = 13,
                Padding = new Thickness(16, 8, 16, 8),
                Foreground = Brushes.White,
                Background = primaryBrush,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei")
            };
            okBtn.Click += (s, ev) =>
            {
                var id = _txtName.Text.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    ModernMessageBox.ShowWarning("请输入好友的青鸟账号唯一ID名", "提示");
                    return;
                }
                if (id.Length < 3 || id.Length > 32)
                {
                    ModernMessageBox.ShowWarning("青鸟账号ID长度应在3-32个字符之间", "提示");
                    return;
                }
                // 检查是否为字母、数字、下划线、中文
                bool hasInvalid = false;
                foreach (char c in id)
                {
                    if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && !IsCjkChar(c))
                    {
                        hasInvalid = true;
                        break;
                    }
                }
                if (hasInvalid)
                {
                    ModernMessageBox.ShowWarning("青鸟账号ID只能包含字母、数字、下划线、连字符和中文", "提示");
                    return;
                }

                // 不允许添加自己
                string myId = AppContext.QingniaoId ?? "";
                if (!string.IsNullOrEmpty(myId) && id == myId)
                {
                    ModernMessageBox.ShowWarning("不能添加自己为好友", "提示");
                    return;
                }

                FriendName = id;
                var (emoji, color) = DeriveAvatarFromId(id);
                AvatarEmoji = emoji;
                AvatarColor = color;
                DialogResult = true;
                Close();
            };
            btnRow.Children.Add(okBtn);
            panel.Children.Add(btnRow);

            root.Child = panel;
            Content = root;
        }

        /// <summary>
        /// 根据青鸟账号 ID 确定性派生出头像 emoji 和颜色，
        /// 相同 ID 始终得到相同的视觉标识。
        /// </summary>
        private static (string emoji, Color color) DeriveAvatarFromId(string id)
        {
            int hash = 0;
            foreach (char c in id)
                hash = (hash * 31 + c) & 0x7FFFFFFF;

            int emojiIdx = Math.Abs(hash) % _defaultEmojis.Count;
            int colorIdx = Math.Abs(hash >> 8) % _defaultColors.Length;

            return (_defaultEmojis[emojiIdx], _defaultColors[colorIdx]);
        }

        /// <summary>判断字符是否为中日韩统一表意文字</summary>
        private static bool IsCjkChar(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) ||
                   (c >= 0x3400 && c <= 0x4DBF) ||
                   (c >= 0x20000 && c <= 0x2A6DF) ||
                   (c >= 0xF900 && c <= 0xFAFF);
        }
    }
}
