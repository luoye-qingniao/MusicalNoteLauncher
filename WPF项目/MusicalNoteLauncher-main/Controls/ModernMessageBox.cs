using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicalNoteLauncher.Controls
{
    /// <summary>
    /// 现代化自定义弹窗 —— 替代原生 MessageBox，与深色主题风格统一
    /// 使用方法与 MessageBox.Show() 几乎一致：
    ///   ModernMessageBox.Show("内容", "标题");
    ///   ModernMessageBox.ShowInfo("操作成功");
    ///   ModernMessageBox.ShowWarning("请先选择版本");
    ///   ModernMessageBox.ShowError("启动失败");
    ///   var result = ModernMessageBox.ShowConfirm("确定要删除吗？");
    /// </summary>
    public static class ModernMessageBox
    {
        #region Public Show Methods (同步，匹配 MessageBox 签名)

        /// <summary>显示信息弹窗</summary>
        public static MessageBoxResult Show(string message, string title = "提示",
            MessageBoxButton button = MessageBoxButton.OK, ModernMessageIcon icon = ModernMessageIcon.Info)
        {
            return ShowInternal(message, title, button, icon);
        }

        /// <summary>信息提示（纯通知，无返回值）</summary>
        public static void ShowInfo(string message, string title = "提示")
        {
            ShowInternal(message, title, MessageBoxButton.OK, ModernMessageIcon.Info);
        }

        /// <summary>成功提示</summary>
        public static void ShowSuccess(string message, string title = "成功")
        {
            ShowInternal(message, title, MessageBoxButton.OK, ModernMessageIcon.Success);
        }

        /// <summary>警告提示</summary>
        public static void ShowWarning(string message, string title = "警告")
        {
            ShowInternal(message, title, MessageBoxButton.OK, ModernMessageIcon.Warning);
        }

        /// <summary>错误提示</summary>
        public static MessageBoxResult ShowError(string message, string title = "错误")
        {
            return ShowInternal(message, title, MessageBoxButton.OK, ModernMessageIcon.Error);
        }

        /// <summary>确认对话框（确定/取消），返回 true 表示用户点击了确定</summary>
        public static bool ShowConfirm(string message, string title = "确认",
            ModernMessageIcon icon = ModernMessageIcon.Question)
        {
            return ShowInternal(message, title, MessageBoxButton.OKCancel, icon) == MessageBoxResult.OK;
        }

        /// <summary>是/否对话框，返回 true 表示用户点击了是</summary>
        public static bool ShowYesNo(string message, string title = "确认",
            ModernMessageIcon icon = ModernMessageIcon.Question)
        {
            return ShowInternal(message, title, MessageBoxButton.YesNo, icon) == MessageBoxResult.Yes;
        }

        /// <summary>是/否/取消对话框</summary>
        public static MessageBoxResult ShowYesNoCancel(string message, string title = "确认",
            ModernMessageIcon icon = ModernMessageIcon.Question)
        {
            return ShowInternal(message, title, MessageBoxButton.YesNoCancel, icon);
        }

        #endregion

        #region Async Methods (异步版本，不阻塞调用线程)

        public static async Task<MessageBoxResult> ShowAsync(string message, string title = "提示",
            MessageBoxButton button = MessageBoxButton.OK, ModernMessageIcon icon = ModernMessageIcon.Info)
        {
            return await Task.Run(() => Application.Current.Dispatcher.Invoke(() =>
                ShowInternal(message, title, button, icon)));
        }

        #endregion

        #region Internal Implementation

        private static MessageBoxResult ShowInternal(string message, string title,
            MessageBoxButton button, ModernMessageIcon icon)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                // 已在 UI 线程，直接创建
                return ShowDialogCore(message, title, button, icon);
            }
            else
            {
                // 非 UI 线程，调度到 UI 线程
                return Application.Current.Dispatcher.Invoke(() =>
                    ShowDialogCore(message, title, button, icon));
            }
        }

        private static MessageBoxResult ShowDialogCore(string message, string title,
            MessageBoxButton button, ModernMessageIcon icon)
        {
            try
            {
                var dialog = new ModernDialogWindow(message, title, button, icon);
                dialog.Owner = GetActiveWindow();
                dialog.WindowStartupLocation = dialog.Owner != null
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen;
                dialog.ShowDialog();
                return dialog.Result;
            }
            catch (Exception ex)
            {
                // ModernDialogWindow 创建失败时回退到系统 MessageBox
                Debug.WriteLine($"[ModernMessageBox] 自定义弹窗失败，回退到系统MessageBox: {ex.Message}");
                return MessageBox.Show(message, title, button,
                    icon == ModernMessageIcon.Error ? MessageBoxImage.Error :
                    icon == ModernMessageIcon.Warning ? MessageBoxImage.Warning :
                    icon == ModernMessageIcon.Question ? MessageBoxImage.Question :
                    MessageBoxImage.Information);
            }
        }

        private static Window GetActiveWindow()
        {
            // 优先获取当前活动的 Window
            foreach (Window window in Application.Current.Windows)
            {
                if (window.IsActive) return window;
            }
            return Application.Current.MainWindow;
        }

        #endregion
    }

    /// <summary>弹窗图标类型</summary>
    public enum ModernMessageIcon
    {
        /// <summary>信息 (ℹ)</summary>
        Info,
        /// <summary>成功 (✓)</summary>
        Success,
        /// <summary>警告 (⚠)</summary>
        Warning,
        /// <summary>错误 (✕)</summary>
        Error,
        /// <summary>询问 (?)</summary>
        Question
    }

    /// <summary>自定义弹窗窗口实现</summary>
    internal class ModernDialogWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private readonly string _message;
        private readonly string _title;
        private readonly MessageBoxButton _buttonType;
        private readonly ModernMessageIcon _icon;

        // 主题颜色
        private Color _bgColor;
        private Color _cardBgColor;
        private Color _borderColor;
        private Color _textPrimary;
        private Color _textSecondary;
        private Color _primaryColor;
        private Color _buttonBgColor;
        private Color _buttonHoverColor;
        private Color _redColor;
        private Color _redHoverColor;

        private Border _rootBorder;
        private const double CornerRadiusValue = 14;

        public ModernDialogWindow(string message, string title,
            MessageBoxButton button, ModernMessageIcon icon)
        {
            _message = message;
            _title = title;
            _buttonType = button;
            _icon = icon;

            LoadThemeColors();
            InitializeWindow();
            SetupContent();
        }

        private void LoadThemeColors()
        {
            try
            {
                _bgColor = GetThemeColor("BackgroundColor", Color.FromRgb(30, 30, 30));
                _cardBgColor = GetThemeColor("CardHoverColor", Color.FromRgb(44, 44, 48));
                _borderColor = GetThemeColor("BorderColor", Color.FromRgb(64, 64, 68));
                _textPrimary = GetThemeColor("TextPrimaryColor", Color.FromRgb(230, 230, 230));
                _textSecondary = GetThemeColor("TextSecondaryColor", Color.FromRgb(170, 170, 170));
                _primaryColor = GetThemeColor("PrimaryColor", Color.FromRgb(33, 150, 243));
                _buttonBgColor = GetThemeColor("SurfaceColor", Color.FromRgb(55, 55, 60));
                _buttonHoverColor = GetThemeColor("ComboBoxHoverColor", Color.FromRgb(70, 70, 76));
                _redColor = Color.FromRgb(198, 40, 40);
                _redHoverColor = Color.FromRgb(229, 57, 53);
            }
            catch
            {
                // 加载失败用默认深色
                _bgColor = Color.FromRgb(30, 30, 30);
                _cardBgColor = Color.FromRgb(44, 44, 48);
                _borderColor = Color.FromRgb(64, 64, 68);
                _textPrimary = Color.FromRgb(230, 230, 230);
                _textSecondary = Color.FromRgb(170, 170, 170);
                _primaryColor = Color.FromRgb(33, 150, 243);
                _buttonBgColor = Color.FromRgb(55, 55, 60);
                _buttonHoverColor = Color.FromRgb(70, 70, 76);
                _redColor = Color.FromRgb(198, 40, 40);
                _redHoverColor = Color.FromRgb(229, 57, 53);
            }
        }

        private static Color GetThemeColor(string key, Color fallback)
        {
            Color? resource = Application.Current.Resources[key] as Color?;
            return resource ?? fallback;
        }

        private void InitializeWindow()
        {
            Title = _title;
            MinWidth = 380;
            MinHeight = 180;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = false;

            // 消除 AllowsTransparency 导致的边角黑色锯齿
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // 动态尺寸：根据消息长度和换行数计算
			int newlineCount = _message.Split('\n').Length - 1;
			int estimatedLines = Math.Max(1, (int)Math.Ceiling(_message.Length / 27.0)) + newlineCount;
			Width = Math.Max(400, Math.Min(600, _message.Length * 12 + 200));
			Height = Math.Max(200, Math.Min(550, 180 + estimatedLines * 24));

            // 允许拖拽
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            // ESC 关闭
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Result = MessageBoxResult.Cancel;
                    AnimateAndClose();
                }
            };
        }

        private void SetupContent()
        {
            _rootBorder = new Border
            {
                Background = new SolidColorBrush(_cardBgColor),
                CornerRadius = new CornerRadius(CornerRadiusValue),
                BorderBrush = new SolidColorBrush(_borderColor),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(28, 24, 28, 20),
                SnapsToDevicePixels = true,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.85, 0.85),
            };
            RenderOptions.SetEdgeMode(_rootBorder, EdgeMode.Aliased);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // 0: 标题+关闭
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) }); // 1: 间距
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // 2: 图标+消息
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // 3: 间距
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // 4: 按钮

            BuildTitleBar(mainGrid);
            BuildContentArea(mainGrid);
            BuildButtonBar(mainGrid);

            _rootBorder.Child = mainGrid;
            Content = _rootBorder;

            // 入场动画
            Loaded += (s, e) =>
            {
                var scaleAnim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                var opacityAnim = new DoubleAnimation(0, 1.0, TimeSpan.FromMilliseconds(180));
                _rootBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                _rootBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                _rootBorder.BeginAnimation(OpacityProperty, opacityAnim);
            };
        }

        private void BuildTitleBar(Grid grid)
        {
            var titleBar = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = _title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(_textPrimary),
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 0);
            titleBar.Children.Add(titleText);

            // 关闭按钮
            var closeBtn = new Button
            {
                Content = "✕",
                Width = 30,
                Height = 30,
                FontSize = 14,
                Foreground = new SolidColorBrush(_textSecondary),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Template = CreateTransparentButtonTemplate()
            };
            closeBtn.Click += (s, e) =>
            {
                Result = MessageBoxResult.Cancel;
                AnimateAndClose();
            };
            closeBtn.MouseEnter += (s, e) => closeBtn.Foreground = new SolidColorBrush(_textPrimary);
            closeBtn.MouseLeave += (s, e) => closeBtn.Foreground = new SolidColorBrush(_textSecondary);
            Grid.SetColumn(closeBtn, 1);
            titleBar.Children.Add(closeBtn);

            Grid.SetRow(titleBar, 0);
            grid.Children.Add(titleBar);
        }

        private void BuildContentArea(Grid grid)
        {
            var contentPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // 图标
            var iconBorder = CreateIcon();
            contentPanel.Children.Add(iconBorder);

            // 消息文本
            var messageText = new TextBlock
            {
                Text = _message,
                FontSize = 14,
                Foreground = new SolidColorBrush(_textPrimary),
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0),
                MaxWidth = 380
            };
            contentPanel.Children.Add(messageText);

            Grid.SetRow(contentPanel, 2);
            grid.Children.Add(contentPanel);
        }

        private Border CreateIcon()
        {
            string iconText;
            Color iconBgColor;
            Color iconFgColor;

            switch (_icon)
            {
                case ModernMessageIcon.Success:
                    iconText = "✓";
                    iconBgColor = Color.FromArgb(30, 76, 175, 80);
                    iconFgColor = Color.FromRgb(76, 175, 80);
                    break;
                case ModernMessageIcon.Warning:
                    iconText = "⚠";
                    iconBgColor = Color.FromArgb(30, 255, 152, 0);
                    iconFgColor = Color.FromRgb(255, 183, 77);
                    break;
                case ModernMessageIcon.Error:
                    iconText = "✕";
                    iconBgColor = Color.FromArgb(30, 244, 67, 54);
                    iconFgColor = Color.FromRgb(239, 83, 80);
                    break;
                case ModernMessageIcon.Question:
                    iconText = "?";
                    iconBgColor = Color.FromArgb(30, 33, 150, 243);
                    iconFgColor = _primaryColor;
                    break;
                default: // Info
                    iconText = "ℹ";
                    iconBgColor = Color.FromArgb(30, 33, 150, 243);
                    iconFgColor = _primaryColor;
                    break;
            }

            return new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(21),
                Background = new SolidColorBrush(iconBgColor),
                Child = new TextBlock
                {
                    Text = iconText,
                    FontSize = 20,
                    Foreground = new SolidColorBrush(iconFgColor),
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Bold
                }
            };
        }

        private void BuildButtonBar(Grid grid)
        {
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            switch (_buttonType)
            {
                case MessageBoxButton.OK:
                    AddButton(buttonPanel, "确定", MessageBoxResult.OK, isPrimary: true);
                    break;

                case MessageBoxButton.OKCancel:
                    AddButton(buttonPanel, "确定", MessageBoxResult.OK, isPrimary: true);
                    AddButton(buttonPanel, "取消", MessageBoxResult.Cancel, isPrimary: false);
                    break;

                case MessageBoxButton.YesNo:
                    AddButton(buttonPanel, "是", MessageBoxResult.Yes, isPrimary: true);
                    AddButton(buttonPanel, "否", MessageBoxResult.No, isPrimary: false);
                    break;

                case MessageBoxButton.YesNoCancel:
                    AddButton(buttonPanel, "是", MessageBoxResult.Yes, isPrimary: true);
                    AddButton(buttonPanel, "否", MessageBoxResult.No, isPrimary: false);
                    AddButton(buttonPanel, "取消", MessageBoxResult.Cancel, isPrimary: false);
                    break;
            }

            Grid.SetRow(buttonPanel, 4);
            grid.Children.Add(buttonPanel);
        }

        private void AddButton(Panel parent, string text, MessageBoxResult result, bool isPrimary)
        {
            bool isDanger = (_icon == ModernMessageIcon.Error && result == MessageBoxResult.OK) ||
                           (result == MessageBoxResult.No && _buttonType == MessageBoxButton.YesNo);

            Color normalBg, hoverBg, normalFg;

            if (isDanger)
            {
                normalBg = _redColor;
                hoverBg = _redHoverColor;
                normalFg = Colors.White;
            }
            else if (isPrimary)
            {
                normalBg = _primaryColor;
                hoverBg = Color.FromRgb(
                    (byte)Math.Min(255, _primaryColor.R + 20),
                    (byte)Math.Min(255, _primaryColor.G + 20),
                    (byte)Math.Min(255, _primaryColor.B + 20));
                normalFg = Colors.White;
            }
            else
            {
                normalBg = _buttonBgColor;
                hoverBg = _buttonHoverColor;
                normalFg = _textPrimary;
            }

            var btn = new Button
            {
                Content = text,
                Width = 80,
                Height = 34,
                FontSize = 13,
                Foreground = new SolidColorBrush(normalFg),
                Background = new SolidColorBrush(normalBg),
                BorderThickness = isPrimary ? new Thickness(0) : new Thickness(1),
                BorderBrush = new SolidColorBrush(_borderColor),
                Cursor = Cursors.Hand,
                Margin = new Thickness(parent.Children.Count > 0 ? 8 : 0, 0, 0, 0),
                FontFamily = new FontFamily("Microsoft YaHei"),
                Template = CreateButtonTemplate(normalBg, hoverBg, normalFg, isPrimary)
            };
            btn.Click += (s, e) =>
            {
                Result = result;
                AnimateAndClose();
            };
            parent.Children.Add(btn);
        }

        private ControlTemplate CreateButtonTemplate(Color normalBg, Color hoverBg, Color fg, bool isPrimary)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(normalBg));
            if (!isPrimary)
                border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(_borderColor));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(isPrimary ? 0 : 1));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            template.VisualTree = border;

            // Hover 触发器
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBg), "border"));
            template.Triggers.Add(hoverTrigger);

            // Pressed 触发器
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            var pressedBg = Color.FromRgb(
                (byte)Math.Max(0, hoverBg.R - 15),
                (byte)Math.Max(0, hoverBg.G - 15),
                (byte)Math.Max(0, hoverBg.B - 15));
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(pressedBg), "border"));
            template.Triggers.Add(pressedTrigger);

            return template;
        }

        private ControlTemplate CreateTransparentButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            return template;
        }

        private void AnimateAndClose()
        {
            var scaleAnim = new DoubleAnimation(1.0, 0.9, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var opacityAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(100));

            opacityAnim.Completed += (s, e) => Close();

            _rootBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _rootBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            _rootBorder.BeginAnimation(OpacityProperty, opacityAnim);
        }
    }
}
