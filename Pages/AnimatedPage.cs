using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MusicalNoteLauncher.Utils;

namespace MusicalNoteLauncher.Pages
{
    /// <summary>
    /// 动画页面基类 - 为所有页面提供统一的动画支持
    /// </summary>
    public class AnimatedPage : UserControl
    {
        #region 字段

        private bool _isInitialized;
        private DispatcherTimer _entranceTimer;
        private List<FrameworkElement> _animatedElements = new List<FrameworkElement>();

        #endregion

        #region 依赖属性

        /// <summary>
        /// 启用页面入场动画
        /// </summary>
        public static readonly DependencyProperty EnableEntranceAnimationProperty =
            DependencyProperty.Register(
                nameof(EnableEntranceAnimation),
                typeof(bool),
                typeof(AnimatedPage),
                new PropertyMetadata(true, OnEnableEntranceAnimationChanged));

        public bool EnableEntranceAnimation
        {
            get => (bool)GetValue(EnableEntranceAnimationProperty);
            set => SetValue(EnableEntranceAnimationProperty, value);
        }

        /// <summary>
        /// 入场动画延迟间隔（毫秒）
        /// </summary>
        public static readonly DependencyProperty EntranceDelayProperty =
            DependencyProperty.Register(
                nameof(EntranceDelay),
                typeof(int),
                typeof(AnimatedPage),
                new PropertyMetadata(50));

        public int EntranceDelay
        {
            get => (int)GetValue(EntranceDelayProperty);
            set => SetValue(EntranceDelayProperty, value);
        }

        /// <summary>
        /// 入场动画时长（毫秒）
        /// </summary>
        public static readonly DependencyProperty EntranceDurationProperty =
            DependencyProperty.Register(
                nameof(EntranceDuration),
                typeof(int),
                typeof(AnimatedPage),
                new PropertyMetadata(200));

        public int EntranceDuration
        {
            get => (int)GetValue(EntranceDurationProperty);
            set => SetValue(EntranceDurationProperty, value);
        }

        /// <summary>
        /// 启用卡片入场动画（自动为指定Name模式的元素添加动画）
        /// </summary>
        public static readonly DependencyProperty EnableCardAnimationProperty =
            DependencyProperty.Register(
                nameof(EnableCardAnimation),
                typeof(bool),
                typeof(AnimatedPage),
                new PropertyMetadata(false, OnEnableCardAnimationChanged));

        public bool EnableCardAnimation
        {
            get => (bool)GetValue(EnableCardAnimationProperty);
            set => SetValue(EnableCardAnimationProperty, value);
        }

        #endregion

        #region 构造函数

        public AnimatedPage()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #endregion

        #region 页面加载

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (EnableEntranceAnimation && !_isInitialized)
            {
                _isInitialized = true;
                PlayEntranceAnimation();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_entranceTimer != null)
            {
                _entranceTimer.Stop();
                _entranceTimer = null;
            }
        }

        #endregion

        #region 入场动画

        private void PlayEntranceAnimation()
        {
            // 查找所有需要动画的元素
            var elements = FindAnimatableElements(this);
            if (elements.Count == 0) return;

            // 重置所有元素状态
            foreach (var element in elements)
            {
                element.RenderTransform = new TranslateTransform(0, 20);
                element.Opacity = 0;
            }

            // 逐个播放动画
            int delay = 0;
            foreach (var element in elements)
            {
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(delay)
                };

                int elementIndex = elements.IndexOf(element);
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    PlayElementEntrance(element);
                };

                timer.Start();
                delay += EntranceDelay;
            }
        }

        private void PlayElementEntrance(FrameworkElement element)
        {
            var translateTransform = element.RenderTransform as TranslateTransform;
            if (translateTransform == null)
            {
                translateTransform = new TranslateTransform(0, 20);
                element.RenderTransform = translateTransform;
            }

            // Y轴滑入动画
            var slideAnim = new DoubleAnimation
            {
                From = 20,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(EntranceDuration),
                EasingFunction = AnimationHelper.EaseOutQuart
            };

            // 淡入动画
            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(EntranceDuration),
                EasingFunction = AnimationHelper.EaseOutQuart
            };

            translateTransform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            element.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private List<FrameworkElement> FindAnimatableElements(DependencyObject parent)
        {
            var elements = new List<FrameworkElement>();

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement element)
                {
                    // 跳过过渡层和某些特定元素
                    if (element.Name != "transitionLayer" &&
                        !(element is Page))
                    {
                        // 跳过按钮模板中的内部元素
                        if (element.DataContext == null || element.Name.StartsWith("btn") || element.Name.StartsWith("Card") || element.Name.StartsWith("Border") || element.Name.StartsWith("Panel"))
                        {
                            if (element.Visibility == Visibility.Visible)
                            {
                                elements.Add(element);
                            }
                        }
                    }

                    // 递归查找子元素
                    elements.AddRange(FindAnimatableElements(child));
                }
            }

            return elements;
        }

        #endregion

        #region 静态回调

        private static void OnEnableEntranceAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AnimatedPage page && (bool)e.NewValue)
            {
                page.PlayEntranceAnimation();
            }
        }

        private static void OnEnableCardAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AnimatedPage page)
            {
                page.SetupCardAnimations();
            }
        }

        #endregion

        #region 卡片动画设置

        private void SetupCardAnimations()
        {
            // 查找所有卡片类元素
            var cards = FindCards(this);
            foreach (var card in cards)
            {
                AnimationHelper.AddHoverScaleAnimation(card);
            }
        }

        private List<Border> FindCards(DependencyObject parent)
        {
            var cards = new List<Border>();

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is Border border && border.Name.Contains("Card"))
                {
                    cards.Add(border);
                }

                cards.AddRange(FindCards(child));
            }

            return cards;
        }

        #endregion

        #region 公共动画方法

        /// <summary>
        /// 播放弹窗入场动画
        /// </summary>
        public void PlayPopupIn(FrameworkElement popup)
        {
            AnimationHelper.PopupIn(popup, AnimationHelper.PopupAnimDuration);
        }

        /// <summary>
        /// 播放弹窗退场动画
        /// </summary>
        public void PlayPopupOut(FrameworkElement popup, Action onComplete = null)
        {
            AnimationHelper.PopupOut(popup, AnimationHelper.PopupAnimDuration, onComplete);
        }

        /// <summary>
        /// 为指定元素添加悬停缩放动画
        /// </summary>
        public void AddHoverAnimation(Button button)
        {
            AnimationHelper.AddHoverScaleAnimation(button);
        }

        /// <summary>
        /// 为元素播放入场动画
        /// </summary>
        public void AnimateIn(FrameworkElement element, double slideDistance = 20)
        {
            AnimationHelper.CardSlideIn(element, slideDistance, EntranceDuration);
        }

        /// <summary>
        /// 播放元素退场动画
        /// </summary>
        public void AnimateOut(FrameworkElement element, Action onComplete = null)
        {
            AnimationHelper.FadeOut(element, 150, onComplete);
        }

        #endregion
    }

    /// <summary>
    /// 可动画化的Border扩展
    /// </summary>
    public static class AnimatedBorder
    {
        /// <summary>
        /// 启用弹窗动画
        /// </summary>
        public static readonly DependencyProperty EnablePopupAnimationProperty =
            DependencyProperty.RegisterAttached(
                "EnablePopupAnimation",
                typeof(bool),
                typeof(AnimatedBorder),
                new PropertyMetadata(false, OnEnablePopupAnimationChanged));

        public static bool GetEnablePopupAnimation(Border border)
        {
            return (bool)border.GetValue(EnablePopupAnimationProperty);
        }

        public static void SetEnablePopupAnimation(Border border, bool value)
        {
            border.SetValue(EnablePopupAnimationProperty, value);
        }

        private static void OnEnablePopupAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Border border && (bool)e.NewValue)
            {
                // 初始状态隐藏并缩小
                border.Opacity = 0;
                border.RenderTransformOrigin = new Point(0.5, 0.5);
                border.RenderTransform = new ScaleTransform(0.95, 0.95);
                border.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 显示弹窗（播放入场动画）
        /// </summary>
        public static void Show(Border popup)
        {
            if (popup == null) return;

            popup.Visibility = Visibility.Visible;
            AnimationHelper.PopupIn(popup);
        }

        /// <summary>
        /// 隐藏弹窗（播放退场动画）
        /// </summary>
        public static void Hide(Border popup, Action onComplete = null)
        {
            if (popup == null)
            {
                onComplete?.Invoke();
                return;
            }

            AnimationHelper.PopupOut(popup, 180, onComplete);
        }
    }

    /// <summary>
    /// 可动画化的按钮扩展
    /// </summary>
    public static class AnimatedButton
    {
        /// <summary>
        /// 启用悬停微缩放动画
        /// </summary>
        public static readonly DependencyProperty EnableHoverScaleProperty =
            DependencyProperty.RegisterAttached(
                "EnableHoverScale",
                typeof(bool),
                typeof(AnimatedButton),
                new PropertyMetadata(false, OnEnableHoverScaleChanged));

        public static bool GetEnableHoverScale(Button button)
        {
            return (bool)button.GetValue(EnableHoverScaleProperty);
        }

        public static void SetEnableHoverScale(Button button, bool value)
        {
            button.SetValue(EnableHoverScaleProperty, value);
        }

        private static void OnEnableHoverScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Button button && (bool)e.NewValue)
            {
                AnimationHelper.AddHoverScaleAnimation(button);
            }
        }

        /// <summary>
        /// 启用增强悬停动画（缩放+颜色变化）
        /// </summary>
        public static readonly DependencyProperty EnableEnhancedHoverProperty =
            DependencyProperty.RegisterAttached(
                "EnableEnhancedHover",
                typeof(bool),
                typeof(AnimatedButton),
                new PropertyMetadata(false, OnEnableEnhancedHoverChanged));

        public static bool GetEnableEnhancedHover(Button button)
        {
            return (bool)button.GetValue(EnableEnhancedHoverProperty);
        }

        public static void SetEnableEnhancedHover(Button button, bool value)
        {
            button.SetValue(EnableEnhancedHoverProperty, value);
        }

        private static void OnEnableEnhancedHoverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Button button && (bool)e.NewValue)
            {
                // 获取按钮的正常背景色
                var normalBrush = button.Background ?? Brushes.Transparent;
                var hoverBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // 默认蓝色

                AnimationHelper.AddEnhancedHoverAnimation(button, normalBrush, hoverBrush);
            }
        }
    }
}
