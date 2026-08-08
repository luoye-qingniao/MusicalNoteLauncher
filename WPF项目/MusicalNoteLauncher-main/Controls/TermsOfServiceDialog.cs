using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Controls
{
    /// <summary>
    /// 用户协议弹窗 —— 首次启动时弹出，要求用户接受用户协议
    /// </summary>
    public static class TermsOfServiceDialog
    {
        private const string TermsUrl = "https://www.mnl.pub/terms";

        /// <summary>
        /// 检查是否需要显示协议弹窗，如果需要则弹出。返回 true 表示用户接受。
        /// </summary>
        public static bool ShowIfNeeded()
        {
            if (SettingsManager.Settings.TermsOfServiceAccepted)
                return true;

            if (Application.Current.Dispatcher.CheckAccess())
                return ShowDialogCore();
            else
                return Application.Current.Dispatcher.Invoke(() => ShowDialogCore());
        }

        private static bool ShowDialogCore()
        {
            try
            {
                var dialog = new TermsDialogWindow();
                dialog.Owner = GetActiveWindow();
                dialog.WindowStartupLocation = dialog.Owner != null
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen;
                dialog.ShowDialog();

                if (dialog.Accepted)
                {
                    SettingsManager.Settings.TermsOfServiceAccepted = true;
                    SettingsManager.SaveSettings();
                }
                else
                {
                    Application.Current.Shutdown();
                }

                return dialog.Accepted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TermsOfServiceDialog] 弹窗失败: {ex.Message}");
                return true; // 出错时默认允许继续
            }
        }

        private static void OpenTermsUrl()
        {
            try
            {
                Process.Start(new ProcessStartInfo(TermsUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TermsOfServiceDialog] 打开链接失败: {ex.Message}");
            }
        }

        private static Window GetActiveWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.IsActive) return window;
            }
            return Application.Current.MainWindow;
        }

        /// <summary>
        /// 用户协议弹窗窗口实现
        /// </summary>
        private class TermsDialogWindow : Window
        {
            public bool Accepted { get; private set; } = false;

            private Color _cardBg;
            private Color _borderClr;
            private Color _textPri;
            private Color _textSec;
            private Color _accent;
            private Color _accentDim;
            private Color _accentBg;
            private Color _redClr;
            private Color _redHover;
            private Color _redText;
            private Color _termsBg;
            private Color _shadowClr;

            private Border _rootBorder;

            public TermsDialogWindow()
            {
                LoadThemeColors();
                InitializeWindow();
                SetupContent();
            }

            private void LoadThemeColors()
            {
                // 从应用主题资源读取配色，与 ModernTheme.xaml 保持一致
                _cardBg    = GetResourceColor("CardBackgroundColor", Color.FromRgb(45, 45, 45));
                _borderClr = GetResourceColor("BorderColor",          Color.FromRgb(58, 58, 58));
                _textPri   = GetResourceColor("TextPrimaryColor",     Color.FromRgb(255, 255, 255));
                _textSec   = GetResourceColor("TextSecondaryColor",   Color.FromRgb(170, 170, 170));
                _accent    = GetResourceColor("BlueButtonColor",      Color.FromRgb(52, 152, 219));
                _accentDim = GetResourceColor("PrimaryDarkColor",     Color.FromRgb(25, 118, 210));
                _redClr    = GetResourceColor("RedButtonColor",       Color.FromRgb(231, 76, 60));
                _redHover  = GetResourceColor("RedButtonHoverColor",  Color.FromRgb(192, 57, 43));
                _termsBg   = GetResourceColor("BackgroundColor",      Color.FromRgb(30, 30, 30));

                // 推导颜色
                _accentBg = Color.FromArgb(20, _accent.R, _accent.G, _accent.B);
                _redText  = Color.FromRgb(255, 255, 255);
                _shadowClr = Color.FromArgb(60, 0, 0, 0);
            }

            private static Color GetResourceColor(string key, Color fallback)
            {
                try
                {
                    object resource = Application.Current.Resources[key];
                    if (resource is Color c) return c;
                    if (resource is SolidColorBrush brush) return brush.Color;
                }
                catch { }
                return fallback;
            }

            private void InitializeWindow()
            {
                Title = "用户协议";
                Width = 540;
                Height = 540;
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Topmost = false;
                UseLayoutRounding = true;
                SnapsToDevicePixels = true;

                MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ButtonState == MouseButtonState.Pressed) DragMove();
                };

                KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Escape)
                    {
                        Accepted = false;
                        AnimateAndClose();
                    }
                };
            }

            private void SetupContent()
            {
                // 外层透明容器（承载阴影 + 卡片）
                var shadowWrapper = new Border
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(6),
                    Child = new Border
                    {
                        Background = new SolidColorBrush(_shadowClr),
                        CornerRadius = new CornerRadius(16),
                        Child = new Border
                        {
                            Background = new SolidColorBrush(_shadowClr),
                            CornerRadius = new CornerRadius(16),
                            Margin = new Thickness(0, 2, 0, 0)
                        }
                    }
                };

                // 主体卡片
                _rootBorder = new Border
                {
                    Background = new SolidColorBrush(_cardBg),
                    CornerRadius = new CornerRadius(14),
                    BorderBrush = new SolidColorBrush(_borderClr),
                    BorderThickness = new Thickness(1),
                    SnapsToDevicePixels = true,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(0.85, 0.85),
                };
                RenderOptions.SetEdgeMode(_rootBorder, EdgeMode.Aliased);

                // 顶部彩色装饰条
                var accentBar = new Border
                {
                    Height = 3,
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                var accentGradient = new LinearGradientBrush(
                    _accentDim, _accent, new Point(0, 0), new Point(1, 0));
                accentBar.Background = accentGradient;

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: accent bar
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) }); // 2: gap
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: description
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) }); // 4: gap
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5: terms card
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) }); // 6: gap
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 7: hint
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) }); // 8: gap
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 9: buttons
                mainGrid.Margin = new Thickness(26, 0, 26, 22);

                Grid.SetRow(accentBar, 0);
                mainGrid.Children.Add(accentBar);

                // ===== Row 1: Header =====
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Icon circle with gradient
                var iconBorder = new Border
                {
                    Width = 48,
                    Height = 48,
                    CornerRadius = new CornerRadius(24),
                    Background = new SolidColorBrush(_accentBg),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(40, _accent.R, _accent.G, _accent.B)),
                    BorderThickness = new Thickness(1)
                };
                iconBorder.Child = new TextBlock
                {
                    Text = "\U0001F4DC",
                    FontSize = 22,
                    Foreground = new SolidColorBrush(_accent),
                    FontFamily = new FontFamily("Segoe UI Emoji"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(iconBorder, 0);
                headerGrid.Children.Add(iconBorder);

                // Title + subtitle
                var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var titleText = new TextBlock
                {
                    Text = "用户协议与隐私政策",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(_textPri),
                    FontFamily = new FontFamily("Microsoft YaHei")
                };
                var subTitle = new TextBlock
                {
                    Text = "请仔细阅读以下内容后再继续",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(_textSec),
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                titleStack.Children.Add(titleText);
                titleStack.Children.Add(subTitle);
                Grid.SetColumn(titleStack, 2);
                headerGrid.Children.Add(titleStack);

                headerGrid.Margin = new Thickness(0, 24, 0, 0);
                Grid.SetRow(headerGrid, 1);
                mainGrid.Children.Add(headerGrid);

                // ===== Row 3: Description =====
                var descText = new TextBlock
                {
                    Text = "欢迎使用 MNL 音符启动器！在继续使用之前，请仔细阅读我们的用户协议。",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(_textPri),
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 22
                };
                Grid.SetRow(descText, 3);
                mainGrid.Children.Add(descText);

                // ===== Row 5: Terms card =====
                var termsCard = new Border
                {
                    Background = new SolidColorBrush(_termsBg),
                    BorderBrush = new SolidColorBrush(_borderClr),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(18, 16, 18, 16)
                };

                var termsStack = new StackPanel();

                // Each term line with colored icon
                AddTermLine(termsStack, "\U0001F4CB", _accent, "本启动器仅供个人学习交流使用，请勿用于商业用途");
                AddTermLine(termsStack, "\u2696\uFE0F", _accent, "使用本启动器即表示您同意遵守相关法律法规");
                AddTermLine(termsStack, "\U0001F512", _accent, "我们重视您的隐私保护，详情请查看完整协议");
                AddTermLine(termsStack, "\u26A0\uFE0F", _redClr, "如不同意本协议，将无法继续使用本启动器");

                // View full terms button - outline style
                var viewTermsBtn = new Button
                {
                    Content = "\U0001F517  查看完整用户协议",
                    Height = 34,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(_accent),
                    Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, _accent.R, _accent.G, _accent.B)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    Margin = new Thickness(0, 14, 0, 0),
                    Template = CreateOutlineButtonTemplate(_accent, Color.FromArgb(15, _accent.R, _accent.G, _accent.B))
                };
                viewTermsBtn.Click += (s, e) => OpenTermsUrl();
                termsStack.Children.Add(viewTermsBtn);

                termsCard.Child = termsStack;
                Grid.SetRow(termsCard, 5);
                mainGrid.Children.Add(termsCard);

                // ===== Row 7: Hint =====
                var hintBorder = new Border
                {
                    Background = new SolidColorBrush(_accentBg),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(40, _accent.R, _accent.G, _accent.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 10, 14, 10)
                };
                var hintText = new TextBlock
                {
                    Text = "\U0001F6C8  点击「同意并继续」即表示您已阅读并同意上述用户协议。",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(_textSec),
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    TextWrapping = TextWrapping.Wrap
                };
                hintBorder.Child = hintText;
                Grid.SetRow(hintBorder, 7);
                mainGrid.Children.Add(hintBorder);

                // ===== Row 9: Buttons =====
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var declineBtn = CreateSolidButton("拒绝并退出", _redClr, _redHover, _redText);
                declineBtn.Click += (s, e) =>
                {
                    Accepted = false;
                    AnimateAndClose();
                };
                buttonPanel.Children.Add(declineBtn);

                var acceptBtn = CreateSolidButton("同意并继续", _accent,
                    Color.FromRgb(
                        (byte)Math.Min(255, _accent.R + 25),
                        (byte)Math.Min(255, _accent.G + 25),
                        (byte)Math.Min(255, _accent.B + 25)),
                    _textPri);
                acceptBtn.Margin = new Thickness(8, 0, 0, 0);
                acceptBtn.Click += (s, e) =>
                {
                    Accepted = true;
                    AnimateAndClose();
                };
                buttonPanel.Children.Add(acceptBtn);

                Grid.SetRow(buttonPanel, 9);
                mainGrid.Children.Add(buttonPanel);

                _rootBorder.Child = mainGrid;
                Content = shadowWrapper;

                // 把卡片放入阴影包装器的内容层
                var shadowContent = (Border)shadowWrapper.Child;
                shadowContent.Child = new Border
                {
                    Background = new SolidColorBrush(_shadowClr),
                    CornerRadius = new CornerRadius(14),
                    Margin = new Thickness(0, 1, 0, 0),
                    Child = _rootBorder
                };

                // 入场动画
                Loaded += (s, e) =>
                {
                    var scaleAnim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(250))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    var opacityAnim = new DoubleAnimation(0, 1.0, TimeSpan.FromMilliseconds(200));
                    _rootBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    _rootBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                    _rootBorder.BeginAnimation(OpacityProperty, opacityAnim);
                };
            }

            private void AddTermLine(Panel parent, string icon, Color iconColor, string text)
            {
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var iconBorder = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(15, iconColor.R, iconColor.G, iconColor.B)),
                    VerticalAlignment = VerticalAlignment.Top
                };
                iconBorder.Child = new TextBlock
                {
                    Text = icon,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(iconColor),
                    FontFamily = new FontFamily("Segoe UI Emoji"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(iconBorder);

                var contentText = new TextBlock
                {
                    Text = text,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(_textSec),
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                panel.Children.Add(contentText);

                parent.Children.Add(panel);
            }

            private Button CreateSolidButton(string text, Color normalBg, Color hoverBg, Color fg)
            {
                var btn = new Button
                {
                    Content = text,
                    MinWidth = 110,
                    Height = 38,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(fg),
                    Background = new SolidColorBrush(normalBg),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    Template = CreateSolidButtonTemplate(normalBg, hoverBg)
                };
                return btn;
            }

            private ControlTemplate CreateSolidButtonTemplate(Color normalBg, Color hoverBg)
            {
                var template = new ControlTemplate(typeof(Button));
                var border = new FrameworkElementFactory(typeof(Border));
                border.Name = "border";
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                border.SetValue(Border.BackgroundProperty, new SolidColorBrush(normalBg));

                var content = new FrameworkElementFactory(typeof(ContentPresenter));
                content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                border.AppendChild(content);

                template.VisualTree = border;

                var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBg), "border"));
                template.Triggers.Add(hoverTrigger);

                var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
                var pressedBg = Color.FromRgb(
                    (byte)Math.Max(0, hoverBg.R - 20),
                    (byte)Math.Max(0, hoverBg.G - 20),
                    (byte)Math.Max(0, hoverBg.B - 20));
                pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(pressedBg), "border"));
                template.Triggers.Add(pressedTrigger);

                return template;
            }

            private ControlTemplate CreateOutlineButtonTemplate(Color accentColor, Color hoverBgColor)
            {
                var template = new ControlTemplate(typeof(Button));
                var border = new FrameworkElementFactory(typeof(Border));
                border.Name = "border";
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B)));
                border.SetValue(Border.BorderThicknessProperty, new Thickness(1));

                var content = new FrameworkElementFactory(typeof(ContentPresenter));
                content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                border.AppendChild(content);

                template.VisualTree = border;

                var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBgColor), "border"));
                hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(accentColor), "border"));
                template.Triggers.Add(hoverTrigger);

                var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
                pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(25, accentColor.R, accentColor.G, accentColor.B)), "border"));
                template.Triggers.Add(pressedTrigger);

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
}
