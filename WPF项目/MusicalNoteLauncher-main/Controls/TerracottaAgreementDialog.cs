using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Controls
{
    /// <summary>
    /// 陶瓦联机 初次使用用户须知弹窗
    /// 内容参考 HMCL 启动器的 terracotta.confirm 协议，颜色跟随应用主题
    /// </summary>
    public static class TerracottaAgreementDialog
    {
        /// <summary>
        /// 直接弹出协议弹窗（不检查设置）。返回 true 表示用户接受。
        /// </summary>
        public static bool ShowDialog()
        {
            if (Application.Current.Dispatcher.CheckAccess())
                return ShowDialogCore();
            else
                return Application.Current.Dispatcher.Invoke(() => ShowDialogCore());
        }

        /// <summary>
        /// 检查是否需要显示协议弹窗，如果需要则弹出。返回 true 表示用户接受。
        /// </summary>
        public static bool ShowIfNeeded()
        {
            if (SettingsManager.Settings.TerracottaAgreementAccepted)
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
                var dialog = new TerracottaDialogWindow();
                dialog.Owner = Application.Current.MainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Topmost = true;
                dialog.ShowDialog();

                if (dialog.Accepted)
                {
                    SettingsManager.Settings.TerracottaAgreementAccepted = true;
                    SettingsManager.SaveSettings();
                }

                return dialog.Accepted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TerracottaAgreementDialog] 弹窗异常: {ex}");
                return false;
            }
        }

        /// <summary>从主题资源获取画刷，如果找不到则回退到指定颜色</summary>
        private static Brush GetThemeBrush(string key, Color fallback)
        {
            var found = Application.Current.TryFindResource(key);
            if (found is Brush brush) return brush;
            return new SolidColorBrush(fallback);
        }

        private class TerracottaDialogWindow : Window
        {
            public bool Accepted { get; private set; } = false;

            private Border _rootBorder;

            public TerracottaDialogWindow()
            {
                InitializeWindow();
                SetupContent();
            }

            private void InitializeWindow()
            {
                Title = "用户须知 - 陶瓦联机";
                Width = 580;
                Height = 560;
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
                var cardBrush = GetThemeBrush("CardBackgroundBrush", Color.FromRgb(37, 37, 37));
                var surfaceBrush = GetThemeBrush("SurfaceBrush", Color.FromRgb(56, 56, 56));
                var borderBrush = GetThemeBrush("BorderBrush", Color.FromRgb(58, 58, 58));
                var textPriBrush = GetThemeBrush("TextPrimaryBrush", Color.FromRgb(255, 255, 255));
                var textSecBrush = GetThemeBrush("TextSecondaryBrush", Color.FromRgb(170, 170, 170));
                var primaryBrush = GetThemeBrush("PrimaryBrush", Color.FromRgb(33, 150, 243));
                var greenBrush = GetThemeBrush("GreenButtonBrush", Color.FromRgb(76, 175, 80));
                var redBrush = GetThemeBrush("RedButtonBrush", Color.FromRgb(244, 67, 54));
                var bgBrush = GetThemeBrush("BackgroundBrush", Color.FromRgb(30, 30, 30));

                // 卡片阴影（始终半透明黑）
                var shadowBrush = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0));

                // 外阴影层
                var shadowWrapper = new Border
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(8),
                    Child = new Border
                    {
                        Background = shadowBrush,
                        CornerRadius = new CornerRadius(16),
                        Child = new Border
                        {
                            Background = shadowBrush,
                            CornerRadius = new CornerRadius(16),
                            Margin = new Thickness(0, 2, 0, 0)
                        }
                    }
                };

                _rootBorder = new Border
                {
                    Background = cardBrush,
                    CornerRadius = new CornerRadius(14),
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(1),
                    SnapsToDevicePixels = true,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(0.85, 0.85),
                };
                RenderOptions.SetEdgeMode(_rootBorder, EdgeMode.Aliased);

                var accentBar = new Border
                {
                    Height = 3,
                    CornerRadius = new CornerRadius(3),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = primaryBrush
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: bar
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) }); // 2: gap
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: subtitle
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) }); // 4: gap
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 5: terms
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) }); // 6: gap
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 7: hint
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) }); // 8: gap
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 9: buttons
                mainGrid.Margin = new Thickness(26, 0, 26, 22);

                Grid.SetRow(accentBar, 0);
                mainGrid.Children.Add(accentBar);

                // ── Row 1: Header ──
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var iconBorder = new Border
                {
                    Width = 48,
                    Height = 48,
                    CornerRadius = new CornerRadius(24),
                    Background = new SolidColorBrush(Color.FromArgb(20, 76, 175, 80)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(40, 76, 175, 80)),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = "\U0001F3FA",
                        FontSize = 22,
                        Foreground = greenBrush,
                        FontFamily = new FontFamily("Segoe UI Emoji"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Grid.SetColumn(iconBorder, 0);
                headerGrid.Children.Add(iconBorder);

                var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                titleStack.Children.Add(new TextBlock
                {
                    Text = "陶瓦联机 用户须知",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = textPriBrush,
                    FontFamily = new FontFamily("Microsoft YaHei")
                });
                titleStack.Children.Add(new TextBlock
                {
                    Text = "使用前请仔细阅读以下内容",
                    FontSize = 12,
                    Foreground = textSecBrush,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(titleStack, 2);
                headerGrid.Children.Add(titleStack);

                headerGrid.Margin = new Thickness(0, 24, 0, 0);
                Grid.SetRow(headerGrid, 1);
                mainGrid.Children.Add(headerGrid);

                // ── Row 3: subtitle ──
                var subtitleText = new TextBlock
                {
                    Text = "\U0001F4CB  陶瓦联机是第三方开源自由软件（基于 EasyTier），请在了解以下须知后使用：",
                    FontSize = 13,
                    Foreground = textSecBrush,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 20
                };
                Grid.SetRow(subtitleText, 3);
                mainGrid.Children.Add(subtitleText);

                // ── Row 5: Terms card ──
                var termsCard = new Border
                {
                    Background = bgBrush,
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 14, 16, 14)
                };

                // 使用 WrapPanel 来让文本正确换行
                var termsStack = new StackPanel();

                termsStack.Children.Add(CreateNoticeItem("\U0001F310", greenBrush, textSecBrush,
                    "陶瓦联机使用 P2P 技术，联机成功后房间内用户之间将直接连接，不会使用第三方服务器对您的流量进行转发。"));
                termsStack.Children.Add(CreateNoticeItem("\U0001F4F6", greenBrush, textSecBrush,
                    "最终联机体验与参与联机者的网络情况有较大关系。"));
                termsStack.Children.Add(CreateNoticeItem("\u26A0\uFE0F", redBrush, textSecBrush,
                    "在多人联机全过程中，您必须严格遵守您所在国家与地区的全部法律法规。"));
                termsStack.Children.Add(CreateNoticeItem("\U0001F41B", redBrush, textSecBrush,
                    "您在使用过程中所遇到的问题请通过相关渠道进行反馈，不得利用本软件从事任何违法违规活动。"));

                termsCard.Child = termsStack;
                Grid.SetRow(termsCard, 5);
                mainGrid.Children.Add(termsCard);

                // ── Row 7: hint ──
                var hintBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(20, 244, 67, 54)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(40, 244, 67, 54)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 10, 14, 10)
                };
                hintBorder.Child = new TextBlock
                {
                    Text = "\U0001F6C8  点击「同意并继续」即表示您已阅读并同意上述用户须知。如不同意，将无法使用陶瓦联机功能。",
                    FontSize = 12,
                    Foreground = textSecBrush,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(hintBorder, 7);
                mainGrid.Children.Add(hintBorder);

                // ── Row 9: Buttons ──
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var declineBtn = CreateThemedButton("拒绝", redBrush,
                    Color.FromRgb(211, 47, 47), Colors.White);
                declineBtn.Click += (s, e) =>
                {
                    Accepted = false;
                    AnimateAndClose();
                };
                buttonPanel.Children.Add(declineBtn);

                var acceptBtn = CreateThemedButton("同意并继续", greenBrush,
                    Color.FromRgb(56, 142, 60), Colors.White);
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

                // 把卡片放入阴影包装器
                var shadowContent = (Border)shadowWrapper.Child;
                shadowContent.Child = new Border
                {
                    Background = shadowBrush,
                    CornerRadius = new CornerRadius(14),
                    Margin = new Thickness(0, 1, 0, 0),
                    Child = _rootBorder
                };

                Content = shadowWrapper;

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

            /// <summary>创建带图标的通知条目，使用DockPanel确保文本正确换行</summary>
            private static FrameworkElement CreateNoticeItem(string icon, Brush iconColor, Brush textBrush, string text)
            {
                var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };

                var iconBorder = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(15, 76, 175, 80)),
                    VerticalAlignment = VerticalAlignment.Top
                };
                iconBorder.Child = new TextBlock
                {
                    Text = icon,
                    FontSize = 13,
                    Foreground = iconColor,
                    FontFamily = new FontFamily("Segoe UI Emoji"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(iconBorder, Dock.Left);
                dock.Children.Add(iconBorder);

                dock.Children.Add(new TextBlock
                {
                    Text = text,
                    FontSize = 13,
                    Foreground = textBrush,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                });

                return dock;
            }

            private static Button CreateThemedButton(string text, Brush normalBg, Color hoverColor, Color fg)
            {
                var btn = new Button
                {
                    Content = text,
                    MinWidth = 120,
                    Height = 38,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(fg),
                    Background = normalBg,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    Template = CreateButtonTemplate(normalBg, new SolidColorBrush(hoverColor))
                };
                return btn;
            }

            private static ControlTemplate CreateButtonTemplate(Brush normalBg, Brush hoverBg)
            {
                var template = new ControlTemplate(typeof(Button));
                var border = new FrameworkElementFactory(typeof(Border));
                border.Name = "border";
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                border.SetValue(Border.BackgroundProperty, normalBg);

                var content = new FrameworkElementFactory(typeof(ContentPresenter));
                content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                border.AppendChild(content);

                template.VisualTree = border;

                var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "border"));
                template.Triggers.Add(hoverTrigger);

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
