using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MusicalNoteLauncher.Controls;

namespace MusicalNoteLauncher.Pages
{
    public partial class CreateChannelDialog : Window
    {
        public string ChannelName { get; private set; }
        public string ChannelDescription { get; private set; }
        public string ChannelIcon { get; private set; } = "📌";
        public string ChannelColor { get; private set; } = "#6C5CE7";

        public CreateChannelDialog()
        {
            MinWidth = 420;
            MinHeight = 320;
            SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;
            Title = "创建新频道";

            var textPri = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
            var textSec = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
            var surfaceBg = TryFindResource("SurfaceBrush") as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
            var cardBg = TryFindResource("CardBackgroundBrush") as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));

            var root = new Border
            {
                Background = cardBg,
                CornerRadius = new CornerRadius(16),
                BorderBrush = TryFindResource("BorderBrush") as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4)
            };

            var stack = new StackPanel { Margin = new Thickness(24, 20, 24, 24) };

            // 标题
            stack.Children.Add(new TextBlock
            {
                Text = "创建新频道",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = textPri,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 16)
            });

            // 图标显示
            var iconLabel = new TextBlock
            {
                Text = "📌",
                FontSize = 14,
                Foreground = textSec,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(iconLabel);

            // 图标选择按钮行
            var iconPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 4), MaxWidth = 400 };
            string[] icons = { "💬", "🧩", "🏰", "🔧", "❓", "🌐", "🎨", "📌", "🎮", "⭐" };
            foreach (var icon in icons)
            {
                var btn = new Button
                {
                    Content = icon,
                    FontSize = 18,
                    Width = 36,
                    Height = 36,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = icon,
                    Margin = new Thickness(0, 0, 4, 4)
                };
                btn.Click += (s, e) =>
                {
                    ChannelIcon = (s as Button)?.Tag?.ToString() ?? "📌";
                    iconLabel.Text = icon;
                };
                iconPanel.Children.Add(btn);
            }
            stack.Children.Add(iconPanel);

            // 频道名称
            stack.Children.Add(new TextBlock
            {
                Text = "频道名称",
                FontSize = 13,
                Foreground = textPri,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var txtName = new TextBox
            {
                Style = CreateTextBoxStyle(surfaceBg, textPri),
                Height = 36,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(txtName);

            // 频道描述
            stack.Children.Add(new TextBlock
            {
                Text = "频道描述",
                FontSize = 13,
                Foreground = textPri,
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var txtDesc = new TextBox
            {
                Style = CreateTextBoxStyle(surfaceBg, textPri),
                Height = 56,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(txtDesc);

            // 按钮行
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Style = CreateButtonStyle(Brushes.Transparent, textPri),
                Width = 80,
                Height = 34,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            btnRow.Children.Add(btnCancel);

            var btnOk = new Button
            {
                Content = "创建",
                Style = CreatePurpleButtonStyle(),
                Width = 80,
                Height = 34
            };
            btnOk.Click += (s, e) =>
            {
                ChannelName = txtName.Text?.Trim() ?? "";
                ChannelDescription = txtDesc.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(ChannelName))
                {
                    ModernMessageBox.ShowWarning("请输入频道名称");
                    return;
                }
                DialogResult = true;
                Close();
            };
            btnRow.Children.Add(btnOk);

            stack.Children.Add(btnRow);

            root.Child = stack;
            Content = root;
        }

        private static Style CreateTextBoxStyle(Brush bg, Brush fg)
        {
            var style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(TextBox.BackgroundProperty, bg));
            style.Setters.Add(new Setter(TextBox.ForegroundProperty, fg));
            style.Setters.Add(new Setter(TextBox.CaretBrushProperty, fg));
            style.Setters.Add(new Setter(TextBox.BorderBrushProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A"))));
            style.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(10, 8, 10, 8)));

            var template = new ControlTemplate(typeof(TextBox));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "bd";
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

            var sv = new FrameworkElementFactory(typeof(ScrollViewer));
            sv.Name = "PART_ContentHost";
            sv.SetValue(ScrollViewer.MarginProperty, new Thickness(2));
            border.AppendChild(sv);
            template.VisualTree = border;

            var trigger = new Trigger { Property = TextBox.IsFocusedProperty, Value = true };
            trigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C5CE7")), "bd"));
            template.Triggers.Add(trigger);

            style.Setters.Add(new Setter(TextBox.TemplateProperty, template));
            return style;
        }

        private static Style CreateButtonStyle(Brush bg, Brush fg)
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Button.BackgroundProperty, bg));
            style.Setters.Add(new Setter(Button.ForegroundProperty, fg));
            style.Setters.Add(new Setter(Button.BorderBrushProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A"))));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));
            style.Setters.Add(new Setter(Button.FontFamilyProperty, new System.Windows.Media.FontFamily("Microsoft YaHei")));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "bd";
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.PaddingProperty, new Thickness(12, 6, 12, 6));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            return style;
        }

        private static Style CreatePurpleButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));
            style.Setters.Add(new Setter(Button.FontFamilyProperty, new System.Windows.Media.FontFamily("Microsoft YaHei")));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "bd";
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C5CE7")));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.PaddingProperty, new Thickness(12, 6, 12, 6));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;

            var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5B4CDB")), "bd"));
            template.Triggers.Add(trigger);

            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            return style;
        }
    }
}
