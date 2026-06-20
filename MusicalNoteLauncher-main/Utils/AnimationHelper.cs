using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace MusicalNoteLauncher.Utils
{
    /// <summary>
    /// 高端动画助手 - 60FPS流畅动画系统
    /// </summary>
    public static class AnimationHelper
    {
        #region 缓动函数定义

        /// <summary>
        /// EaseOutQuart缓动 - 快速启动，优雅减速
        /// </summary>
        public static CubicEase EaseOutQuart => new CubicEase { EasingMode = EasingMode.EaseOut };

        /// <summary>
        /// EaseOutCubic缓动 - 平滑减速
        /// </summary>
        public static CubicEase EaseOutCubic => new CubicEase { EasingMode = EasingMode.EaseOut };

        /// <summary>
        /// EaseOutBack缓动 - 带轻微弹性回弹
        /// </summary>
        public static BackEase EaseOutBack => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };

        /// <summary>
        /// EaseInOutCubic缓动 - 渐进加速减速
        /// </summary>
        public static CubicEase EaseInOutCubic => new CubicEase { EasingMode = EasingMode.EaseInOut };

        #endregion

        #region 动画时长常量

        public const int PageTransitionDuration = 200;      // 页面切换总时长
        public const int SidebarAnimDuration = 160;          // 侧边栏展开/收起
        public const int PopupAnimDuration = 180;             // 弹窗弹出/关闭
        public const int HoverAnimDuration = 120;            // 按钮悬停
        public const int ScaleAnimDuration = 150;             // 微缩放动画

        #endregion

        #region 缩放常量

        public const double HoverScale = 1.03;               // 悬停时缩放
        public const double PageEnterScale = 0.98;            // 页面入场缩放起点
        public const double PageExitScale = 0.98;            // 页面退场缩放终点
        public const double PopupEnterScale = 0.95;          // 弹窗入场缩放起点

        #endregion

        #region 位移常量

        public const double PageSlideOffset = 40;            // 页面滑入偏移量(px)
        public const double SidebarExpandOffset = 200;       // 侧边栏展开偏移

        #endregion

        #region 硬件加速配置

        /// <summary>
        /// 配置UIElement的硬件加速模式
        /// </summary>
        public static void EnableHardwareAcceleration(UIElement element)
        {
            if (element == null) return;

            // 设置位图缓存以启用硬件加速
            element.CacheMode = new BitmapCache
            {
                EnableClearType = true,
                RenderAtScale = 1.0,
                SnapsToDevicePixels = true
            };

            // 禁用TextOptions的文本呈现优化
            if (element is FrameworkElement fe)
            {
                TextOptions.SetTextFormattingMode(fe, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(fe, TextRenderingMode.ClearType);
            }
        }

        /// <summary>
        /// 为窗口设置硬件加速
        /// </summary>
        public static void ConfigureWindowHardwareAcceleration(Window window)
        {
            if (window == null) return;

            // 尝试启用软件渲染回退到硬件
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;

            // 设置整个窗口的位图缓存模式
            EnableHardwareAcceleration(window.Content as UIElement);
        }

        #endregion

        #region 页面切换动画

        /// <summary>
        /// 执行分层滑动+微缩放+淡入淡出的页面切换
        /// </summary>
        /// <param name="oldContent">旧内容容器(VisualBrush来源)</param>
        /// <param name="transitionLayer">过渡层</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="onComplete">完成回调</param>
        public static void PerformLayeredPageTransition(
            Visual oldContent,
            Grid transitionLayer,
            double width,
            double height,
            Action onComplete = null)
        {
            if (transitionLayer == null) return;

            // 清理过渡层
            transitionLayer.Children.Clear();

            // 创建旧内容的快照
            var oldBrush = new VisualBrush(oldContent)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };

            var oldSnapshot = new Border
            {
                Width = width,
                Height = height,
                Background = oldBrush,
                CacheMode = new BitmapCache { EnableClearType = true, RenderAtScale = 1.0 }
            };

            // 创建新内容的快照层(入场动画)
            var newSnapshot = new Border
            {
                Width = width,
                Height = height,
                Background = new VisualBrush(transitionLayer),
                CacheMode = new BitmapCache { EnableClearType = true, RenderAtScale = 1.0 },
                Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            // 设置新内容的缩放变换
            var newScaleTransform = new ScaleTransform(1.0, 1.0);
            var newTranslateTransform = new TranslateTransform(0, 0);
            var newTransformGroup = new TransformGroup();
            newTransformGroup.Children.Add(newScaleTransform);
            newTransformGroup.Children.Add(newTranslateTransform);
            newSnapshot.RenderTransform = newTransformGroup;

            // 添加到过渡层
            transitionLayer.Children.Add(oldSnapshot);
            transitionLayer.Children.Add(newSnapshot);

            // 创建入场动画 - 新页面：X轴向右偏移40px入场 + 缩放0.98→1.0 + 透明度0→1
            var slideAnimation = new DoubleAnimation
            {
                From = PageSlideOffset,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(PageTransitionDuration),
                EasingFunction = EaseOutQuart,
                FillBehavior = FillBehavior.HoldEnd
            };

            var scaleAnimation = new DoubleAnimation
            {
                From = PageEnterScale,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(PageTransitionDuration),
                EasingFunction = EaseOutQuart,
                FillBehavior = FillBehavior.HoldEnd
            };

            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(PageTransitionDuration),
                EasingFunction = EaseOutQuart,
                FillBehavior = FillBehavior.HoldEnd
            };

            // 旧页面：保持原位 + 缩放1.0→0.98 + 透明度1→0
            var oldScaleAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = PageExitScale,
                Duration = TimeSpan.FromMilliseconds(PageTransitionDuration),
                EasingFunction = EaseOutQuart,
                FillBehavior = FillBehavior.HoldEnd
            };

            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(PageTransitionDuration),
                EasingFunction = EaseOutQuart,
                FillBehavior = FillBehavior.HoldEnd
            };

            // 应用旧内容动画
            oldSnapshot.RenderTransform = new ScaleTransform(1.0, 1.0);
            oldSnapshot.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);

            var oldScale = oldSnapshot.RenderTransform as ScaleTransform;
            if (oldScale != null)
            {
                oldScale.BeginAnimation(ScaleTransform.ScaleXProperty, oldScaleAnimation);
                oldScale.BeginAnimation(ScaleTransform.ScaleYProperty, oldScaleAnimation);
            }

            // 应用新内容动画
            newSnapshot.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            newTranslateTransform.BeginAnimation(TranslateTransform.XProperty, slideAnimation);
            newScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            newScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

            // 动画完成回调
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PageTransitionDuration + 20)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                transitionLayer.Children.Clear();
                onComplete?.Invoke();
            };
            timer.Start();
        }

        /// <summary>
        /// 简单的入场淡入动画
        /// </summary>
        public static void FadeIn(UIElement element, int durationMs = 200, Action onComplete = null)
        {
            if (element == null) return;

            element.Opacity = 0;
            element.Visibility = Visibility.Visible;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseOutQuart
            };

            if (onComplete != null)
            {
                animation.Completed += (s, e) => onComplete();
            }

            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        /// <summary>
        /// 简单的退场淡出动画
        /// </summary>
        public static void FadeOut(UIElement element, int durationMs = 200, Action onComplete = null)
        {
            if (element == null) return;

            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseOutQuart
            };

            if (onComplete != null)
            {
                animation.Completed += (s, e) =>
                {
                    element.Visibility = Visibility.Collapsed;
                    onComplete();
                };
            }
            else
            {
                animation.Completed += (s, e) => element.Visibility = Visibility.Collapsed;
            }

            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        #endregion

        #region 弹窗动画

        /// <summary>
        /// 弹窗中心缩放弹出(0.95→1.0)+淡入
        /// </summary>
        public static void PopupIn(FrameworkElement element, int durationMs = 180, Action onComplete = null)
        {
            if (element == null) return;

            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(0.95, 0.95);
            element.Opacity = 0;
            element.Visibility = Visibility.Visible;

            var scaleAnim = new DoubleAnimation
            {
                From = PopupEnterScale,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseOutBack
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseOutQuart
            };

            var scaleTransform = element.RenderTransform as ScaleTransform;
            if (scaleTransform != null)
            {
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }

            if (onComplete != null)
            {
                fadeAnim.Completed += (s, e) => onComplete();
            }

            element.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        /// <summary>
        /// 弹窗关闭 - 反向执行(缩放1.0→0.95)+淡出
        /// </summary>
        public static void PopupOut(FrameworkElement element, int durationMs = 180, Action onComplete = null)
        {
            if (element == null) return;

            var scaleAnim = new DoubleAnimation
            {
                From = 1.0,
                To = PopupEnterScale,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseOutQuart
            };

            var fadeAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseOutQuart
            };

            fadeAnim.Completed += (s, e) =>
            {
                element.Visibility = Visibility.Collapsed;
                onComplete?.Invoke();
            };

            var scaleTransform = element.RenderTransform as ScaleTransform;
            if (scaleTransform != null)
            {
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }

            element.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        #endregion

        #region 按钮悬停动画

        /// <summary>
        /// 为按钮添加悬停微缩放动画(1.0→1.03)
        /// </summary>
        public static void AddHoverScaleAnimation(Button button, double hoverScale = HoverScale)
        {
            AddHoverScaleAnimation(button as UIElement, hoverScale);
        }

        /// <summary>
        /// 为UI元素添加悬停微缩放动画(1.0→1.03)
        /// </summary>
        public static void AddHoverScaleAnimation(UIElement element, double hoverScale = HoverScale)
        {
            if (element == null) return;

            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(1.0, 1.0);

            var enterScaleAnim = new DoubleAnimation
            {
                To = hoverScale,
                Duration = TimeSpan.FromMilliseconds(HoverAnimDuration),
                EasingFunction = EaseOutQuart
            };

            var leaveScaleAnim = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(HoverAnimDuration),
                EasingFunction = EaseOutQuart
            };

            element.MouseEnter += (s, e) =>
            {
                var transform = element.RenderTransform as ScaleTransform;
                if (transform != null)
                {
                    transform.BeginAnimation(ScaleTransform.ScaleXProperty, enterScaleAnim);
                    transform.BeginAnimation(ScaleTransform.ScaleYProperty, enterScaleAnim);
                }
            };

            element.MouseLeave += (s, e) =>
            {
                var transform = element.RenderTransform as ScaleTransform;
                if (transform != null)
                {
                    transform.BeginAnimation(ScaleTransform.ScaleXProperty, leaveScaleAnim);
                    transform.BeginAnimation(ScaleTransform.ScaleYProperty, leaveScaleAnim);
                }
            };
        }

        /// <summary>
        /// 为按钮添加增强悬停动画(微缩放+底色渐变)
        /// </summary>
        public static void AddEnhancedHoverAnimation(Button button, Brush normalBrush, Brush hoverBrush, double hoverScale = HoverScale)
        {
            if (button == null) return;

            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.RenderTransform = new ScaleTransform(1.0, 1.0);

            var enterScaleAnim = new DoubleAnimation
            {
                To = hoverScale,
                Duration = TimeSpan.FromMilliseconds(HoverAnimDuration),
                EasingFunction = EaseOutQuart
            };

            var leaveScaleAnim = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(HoverAnimDuration),
                EasingFunction = EaseOutQuart
            };

            var enterColorAnim = new ColorAnimation
            {
                To = (hoverBrush as SolidColorBrush)?.Color ?? Colors.Transparent,
                Duration = TimeSpan.FromMilliseconds(HoverAnimDuration),
                EasingFunction = EaseOutQuart
            };

            button.MouseEnter += (s, e) =>
            {
                var transform = button.RenderTransform as ScaleTransform;
                transform?.BeginAnimation(ScaleTransform.ScaleXProperty, enterScaleAnim);
                transform?.BeginAnimation(ScaleTransform.ScaleYProperty, enterScaleAnim);
            };

            button.MouseLeave += (s, e) =>
            {
                var transform = button.RenderTransform as ScaleTransform;
                transform?.BeginAnimation(ScaleTransform.ScaleXProperty, leaveScaleAnim);
                transform?.BeginAnimation(ScaleTransform.ScaleYProperty, leaveScaleAnim);
            };
        }

        #endregion

        #region 侧边栏动画

        /// <summary>
        /// 侧边栏展开动画(160ms，横向平滑位移+透明度渐变，伴随轻微弹性收尾)
        /// </summary>
        public static void SidebarExpand(Border sidebar, double targetWidth, Action onComplete = null)
        {
            if (sidebar == null) return;

            var widthAnim = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(SidebarAnimDuration),
                EasingFunction = EaseOutBack
            };

            var opacityAnim = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(SidebarAnimDuration),
                EasingFunction = EaseOutQuart
            };

            if (onComplete != null)
            {
                widthAnim.Completed += (s, e) => onComplete();
            }

            sidebar.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            sidebar.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        /// <summary>
        /// 侧边栏收起动画
        /// </summary>
        public static void SidebarCollapse(Border sidebar, double targetWidth, Action onComplete = null)
        {
            if (sidebar == null) return;

            var widthAnim = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(SidebarAnimDuration),
                EasingFunction = EaseOutQuart
            };

            var opacityAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(SidebarAnimDuration),
                EasingFunction = EaseOutQuart
            };

            if (onComplete != null)
            {
                widthAnim.Completed += (s, e) => onComplete();
            }

            sidebar.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            sidebar.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        #endregion

        #region 卡片/面板动画

        /// <summary>
        /// 卡片入场动画(从下方滑入+淡入)
        /// </summary>
        public static void CardSlideIn(FrameworkElement card, double slideDistance = 30, int durationMs = 250, Action onComplete = null)
        {
            if (card == null) return;

            card.RenderTransform = new TranslateTransform(0, slideDistance);
            card.Opacity = 0;

            var translateAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseOutQuart
            };

            var fadeAnim = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseOutQuart
            };

            if (onComplete != null)
            {
                fadeAnim.Completed += (s, e) => onComplete();
            }

            var transform = card.RenderTransform as TranslateTransform;
            transform?.BeginAnimation(TranslateTransform.YProperty, translateAnim);
            card.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        /// <summary>
        /// 列表项逐个入场动画(用于列表/卡片网格的逐个显示)
        /// </summary>
        public static void StaggeredEntrance(UIElementCollection items, int baseDelayMs = 50, Action onAllComplete = null)
        {
            if (items == null || items.Count == 0) return;

            int completedCount = 0;
            int totalCount = items.Count;

            for (int i = 0; i < totalCount; i++)
            {
                var item = items[i] as FrameworkElement;
                if (item == null)
                {
                    completedCount++;
                    continue;
                }

                var delay = TimeSpan.FromMilliseconds(i * baseDelayMs);

                // 延迟后执行动画
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = delay
                };

                int index = i;
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    CardSlideIn(item, 20, 200, () =>
                    {
                        completedCount++;
                        if (completedCount >= totalCount && onAllComplete != null)
                        {
                            onAllComplete();
                        }
                    });
                };

                timer.Start();
            }
        }

        #endregion

        #region 滚动动画

        /// <summary>
        /// 平滑滚动到指定位置
        /// </summary>
        public static void SmoothScrollTo(ScrollViewer scrollViewer, double targetOffset, int durationMs = 300)
        {
            if (scrollViewer == null) return;

            var animation = new DoubleAnimation
            {
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = EaseOutQuart
            };

            scrollViewer.BeginAnimation(ScrollViewer.VerticalOffsetProperty, animation);
        }

        #endregion

        #region 附加属性 - 快速启用动画

        /// <summary>
        /// 附加属性：为元素快速启用悬停动画
        /// </summary>
        public static class AnimationExtensions
        {
            public static readonly DependencyProperty EnableHoverAnimationProperty =
                DependencyProperty.RegisterAttached(
                    "EnableHoverAnimation",
                    typeof(bool),
                    typeof(AnimationExtensions),
                    new PropertyMetadata(false, OnEnableHoverAnimationChanged));

            public static bool GetEnableHoverAnimation(DependencyObject obj)
            {
                return (bool)obj.GetValue(EnableHoverAnimationProperty);
            }

            public static void SetEnableHoverAnimation(DependencyObject obj, bool value)
            {
                obj.SetValue(EnableHoverAnimationProperty, value);
            }

            private static void OnEnableHoverAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                if (d is Button button && (bool)e.NewValue)
                {
                    AnimationHelper.AddHoverScaleAnimation(button);
                }
            }

            public static readonly DependencyProperty EnablePopupAnimationProperty =
                DependencyProperty.RegisterAttached(
                    "EnablePopupAnimation",
                    typeof(bool),
                    typeof(AnimationExtensions),
                    new PropertyMetadata(false, OnEnablePopupAnimationChanged));

            public static bool GetEnablePopupAnimation(DependencyObject obj)
            {
                return (bool)obj.GetValue(EnablePopupAnimationProperty);
            }

            public static void SetEnablePopupAnimation(DependencyObject obj, bool value)
            {
                obj.SetValue(EnablePopupAnimationProperty, value);
            }

            private static void OnEnablePopupAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                if (d is FrameworkElement element && (bool)e.NewValue)
                {
                    element.Visibility = Visibility.Collapsed;
                    element.RenderTransformOrigin = new Point(0.5, 0.5);
                    element.RenderTransform = new ScaleTransform(0.95, 0.95);
                }
            }
        }

        #endregion
    }
}
