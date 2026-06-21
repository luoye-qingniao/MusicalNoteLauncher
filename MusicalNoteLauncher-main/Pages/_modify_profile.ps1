# SkinEditDialog 修复脚本：
# 1. ComboBox 自定义样式
# 2. 按钮圆角
# 3. 默认皮肤位图生成

$file = "f:\mc音符启动器\MusicalNoteLauncher-main\MusicalNoteLauncher-main\Pages\ProfilePage.xaml.cs"
$content = [System.IO.File]::ReadAllText($file)

# ============ 修改 1：为 _modelCombo 添加 Style ============
$old1 = @"
            _modelCombo = new ComboBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontSize = 12,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _modelCombo.Items.Add("Steve (WIDE)");
"@

$new1 = @"
            _modelCombo = new ComboBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontSize = 12,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _modelCombo.Style = BuildDarkComboBoxStyle();
            _modelCombo.Items.Add("Steve (WIDE)");
"@

if ($content.Contains($old1)) {
    $content = $content.Replace($old1, $new1)
    Write-Host "[OK] 1. 已为 _modelCombo 添加 Style"
} else {
    Write-Host "[FAIL] 1. 未找到 _modelCombo 原始代码"
}

# ============ 修改 2：btnPickSkin 添加 CornerRadius ============
$old2 = @"
            var btnPickSkin = new Button
            {
                Content = "浏览",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnPickSkin.Click += (s, e) => PickSkinFile();
"@

$new2 = @"
            var btnPickSkin = new Button
            {
                Content = "浏览",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnPickSkin.Click += (s, e) => PickSkinFile();
"@

if ($content.Contains($old2)) {
    $content = $content.Replace($old2, $new2)
    Write-Host "[OK] 2. btnPickSkin 已添加 CornerRadius"
} else {
    Write-Host "[FAIL] 2. 未找到 btnPickSkin 原始代码"
}

# ============ 修改 3：btnPickCape 添加 CornerRadius ============
$old3 = @"
            var btnPickCape = new Button
            {
                Content = "浏览",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnPickCape.Click += (s, e) => PickCapeFile();
"@

$new3 = @"
            var btnPickCape = new Button
            {
                Content = "浏览",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnPickCape.Click += (s, e) => PickCapeFile();
"@

if ($content.Contains($old3)) {
    $content = $content.Replace($old3, $new3)
    Write-Host "[OK] 3. btnPickCape 已添加 CornerRadius"
} else {
    Write-Host "[FAIL] 3. 未找到 btnPickCape 原始代码"
}

# ============ 修改 4：btnLittleLink 添加 CornerRadius ============
$old4 = @"
            var btnLittleLink = new Button
            {
                Content = "LittleSkin",
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(14, 0, 14, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")),
                BorderThickness = new Thickness(0),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
"@

$new4 = @"
            var btnLittleLink = new Button
            {
                Content = "LittleSkin",
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(14, 0, 14, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                FontSize = 12,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
"@

if ($content.Contains($old4)) {
    $content = $content.Replace($old4, $new4)
    Write-Host "[OK] 4. btnLittleLink 已添加 CornerRadius"
} else {
    Write-Host "[FAIL] 4. 未找到 btnLittleLink 原始代码"
}

# ============ 修改 5：btnCancel 添加 CornerRadius ============
$old5 = @"
            var btnCancel = new Button
            {
                Content = "取消",
                Width = 90,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
"@

$new5 = @"
            var btnCancel = new Button
            {
                Content = "取消",
                Width = 90,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                FontSize = 13,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
"@

if ($content.Contains($old5)) {
    $content = $content.Replace($old5, $new5)
    Write-Host "[OK] 5. btnCancel 已添加 CornerRadius"
} else {
    Write-Host "[FAIL] 5. 未找到 btnCancel 原始代码"
}

# ============ 修改 6：btnOk 添加 CornerRadius ============
$old6 = @"
            var btnOk = new Button
            {
                Content = "确定",
                Width = 90,
                Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            btnOk.Click += (s, e) => ConfirmAndSave();
"@

$new6 = @"
            var btnOk = new Button
            {
                Content = "确定",
                Width = 90,
                Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Cursor = Cursors.Hand
            };
            btnOk.Click += (s, e) => ConfirmAndSave();
"@

if ($content.Contains($old6)) {
    $content = $content.Replace($old6, $new6)
    Write-Host "[OK] 6. btnOk 已添加 CornerRadius"
} else {
    Write-Host "[FAIL] 6. 未找到 btnOk 原始代码"
}

# ============ 修改 7：SelectSource 中 Default/Steve/Alex 分支使用默认位图 ============
$old7 = @"
                case SkinSource.Default:
                case SkinSource.Steve:
                case SkinSource.Alex:
                    IsDefault = true;
                    if (source == SkinSource.Steve) IsSlim = false;
                    else if (source == SkinSource.Alex) IsSlim = true;
                    _skin3dViewer.UpdateSkin(null, IsSlim);
                    _lblStatus.Text = "将使用默认皮肤";
                    break;
"@

$new7 = @"
                case SkinSource.Default:
                case SkinSource.Steve:
                case SkinSource.Alex:
                    IsDefault = true;
                    if (source == SkinSource.Steve) IsSlim = false;
                    else if (source == SkinSource.Alex) IsSlim = true;
                    _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
                    _lblStatus.Text = "将使用默认皮肤";
                    break;
"@

if ($content.Contains($old7)) {
    $content = $content.Replace($old7, $new7)
    Write-Host "[OK] 7. SelectSource 默认分支已使用 GenerateDefaultSkinBitmap"
} else {
    Write-Host "[FAIL] 7. 未找到 SelectSource 默认分支代码"
}

# ============ 修改 8：UpdatePreviewFromCurrent 中无文件时使用默认皮肤 ============
$old8 = @"
        private void UpdatePreviewFromCurrent()
        {
            if (string.IsNullOrEmpty(_selectedSkinFile) || !File.Exists(_selectedSkinFile))
            {
                _skin3dViewer.UpdateSkin(null, IsSlim);
                _lblStatus.Text = "未选择皮肤文件";
                return;
            }
"@

$new8 = @"
        private void UpdatePreviewFromCurrent()
        {
            if (string.IsNullOrEmpty(_selectedSkinFile) || !File.Exists(_selectedSkinFile))
            {
                if (_currentSource == SkinSource.Default ||
                    _currentSource == SkinSource.Steve ||
                    _currentSource == SkinSource.Alex)
                {
                    _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
                    _lblStatus.Text = "将使用默认皮肤";
                }
                else
                {
                    _skin3dViewer.UpdateSkin(null, IsSlim);
                    _lblStatus.Text = "未选择皮肤文件";
                }
                return;
            }
"@

if ($content.Contains($old8)) {
    $content = $content.Replace($old8, $new8)
    Write-Host "[OK] 8. UpdatePreviewFromCurrent 无文件分支已处理"
} else {
    Write-Host "[FAIL] 8. 未找到 UpdatePreviewFromCurrent 无文件分支代码"
}

# ============ 修改 9：LoadCurrentPreview 无文件时使用默认皮肤 ============
$old9 = @"
        private void LoadCurrentPreview()
        {
            if (!string.IsNullOrEmpty(_currentSkinFile) && File.Exists(_currentSkinFile))
            {
                UpdatePreviewFromCurrent();
            }
            else
            {
                _skin3dViewer.UpdateSkin(null, IsSlim);
            }
        }
"@

$new9 = @"
        private void LoadCurrentPreview()
        {
            if (!string.IsNullOrEmpty(_currentSkinFile) && File.Exists(_currentSkinFile))
            {
                UpdatePreviewFromCurrent();
            }
            else
            {
                _skin3dViewer.UpdateSkin(GenerateDefaultSkinBitmap(IsSlim), IsSlim);
            }
        }
"@

if ($content.Contains($old9)) {
    $content = $content.Replace($old9, $new9)
    Write-Host "[OK] 9. LoadCurrentPreview 已处理"
} else {
    Write-Host "[FAIL] 9. 未找到 LoadCurrentPreview 代码"
}

# ============ 修改 10：添加 BuildDarkComboBoxStyle 和 GenerateDefaultSkinBitmap 辅助方法 ============
# 在 SelectSource 方法之前插入辅助方法
$old10 = @"
        private void SelectSource(SkinSource source)
"@

$new10 = @"
        private static Style BuildDarkComboBoxStyle()
        {
            var style = new Style(typeof(ComboBox));

            style.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E"))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.BorderBrushProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A"))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            style.Setters.Add(new Setter(ComboBox.HorizontalContentAlignmentProperty,
                HorizontalAlignment.Left));
            style.Setters.Add(new Setter(ComboBox.VerticalContentAlignmentProperty,
                VerticalAlignment.Center));

            // ComboBoxItem 深色样式
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525"))));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            var itemHoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            itemHoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A"))));
            itemStyle.Triggers.Add(itemHoverTrigger);
            var itemSelectedTrigger = new Trigger
            {
                Property = ListBoxItem.IsSelectedProperty,
                Value = true
            };
            itemSelectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A"))));
            itemStyle.Triggers.Add(itemSelectedTrigger);
            style.Setters.Add(new Setter(ComboBox.ItemContainerStyleProperty, itemStyle));

            // ControlTemplate：Border 包裹 Grid，Grid 左列 ContentPresenter 右列下拉箭头按钮
            var template = new ControlTemplate(typeof(ComboBox));

            // 最外层圆角 Border
            var rootBorderFactory = new FrameworkElementFactory(typeof(Border), "templateRoot");
            rootBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            rootBorderFactory.SetValue(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")));
            rootBorderFactory.SetValue(Border.BorderBrushProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")));
            rootBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            rootBorderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);
            template.VisualTree = rootBorderFactory;

            // Grid：两列
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            rootBorderFactory.AppendChild(gridFactory);

            var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            gridFactory.AppendChild(col0);

            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(0, GridUnitType.Auto));
            gridFactory.AppendChild(col1);

            // 左列：ContentPresenter 显示选中项
            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter), "ContentPresenter");
            contentPresenterFactory.SetValue(Grid.ColumnProperty, 0);
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty,
                HorizontalAlignment.Left);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty,
                VerticalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.MarginProperty, new Thickness(6, 0, 0, 0));
            contentPresenterFactory.SetValue(ContentPresenter.SnapsToDevicePixelsProperty, true);
            gridFactory.AppendChild(contentPresenterFactory);

            // 右列：ToggleButton（下拉箭头）—— 自定义其模板
            var toggleButtonFactory = new FrameworkElementFactory(typeof(ToggleButton), "DropDownToggle");
            toggleButtonFactory.SetValue(Grid.ColumnProperty, 1);
            toggleButtonFactory.SetValue(ToggleButton.FocusableProperty, false);
            toggleButtonFactory.SetValue(ToggleButton.IsCheckedProperty,
                new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            toggleButtonFactory.SetValue(Control.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")));
            toggleButtonFactory.SetValue(Control.ForegroundProperty, Brushes.White);
            toggleButtonFactory.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            toggleButtonFactory.SetValue(Control.PaddingProperty, new Thickness(4, 0, 4, 0));
            toggleButtonFactory.SetValue(FrameworkElement.MinWidthProperty, 18.0);
            toggleButtonFactory.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

            // ToggleButton 自定义模板：Border + Path（V 形箭头）
            var toggleTemplate = new ControlTemplate(typeof(ToggleButton));
            var toggleBorder = new FrameworkElementFactory(typeof(Border));
            toggleBorder.SetValue(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E")));
            toggleBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(0, 4, 4, 0));
            toggleBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            toggleTemplate.VisualTree = toggleBorder;

            var pathFactory = new FrameworkElementFactory(typeof(Path));
            pathFactory.SetValue(Path.DataProperty,
                Geometry.Parse("M 0,0 L 4,4 L 8,0 Z"));
            pathFactory.SetValue(Shape.FillProperty, Brushes.White);
            pathFactory.SetValue(Shape.StretchProperty, Stretch.None);
            pathFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty,
                HorizontalAlignment.Center);
            pathFactory.SetValue(FrameworkElement.VerticalAlignmentProperty,
                VerticalAlignment.Center);
            pathFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 0));
            toggleBorder.AppendChild(pathFactory);

            toggleButtonFactory.SetValue(Control.TemplateProperty, toggleTemplate);

            gridFactory.AppendChild(toggleButtonFactory);

            // Popup 部分
            var popupFactory = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popupFactory.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popupFactory.SetValue(Popup.StaysOpenProperty, false);
            popupFactory.SetValue(Popup.AllowsTransparencyProperty, true);
            popupFactory.SetValue(Popup.IsOpenProperty,
                new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            gridFactory.AppendChild(popupFactory);

            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525")));
            popupBorder.SetValue(Border.BorderBrushProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A4A")));
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            popupBorder.SetValue(Border.PaddingProperty, new Thickness(0));
            popupBorder.SetValue(FrameworkElement.MinWidthProperty,
                new Binding("ActualWidth") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            popupFactory.AppendChild(popupBorder);

            var scrollViewerFactory = new FrameworkElementFactory(typeof(ScrollViewer));
            scrollViewerFactory.SetValue(ScrollViewer.CanContentScrollProperty, true);
            popupBorder.AppendChild(scrollViewerFactory);

            var itemsPresenterFactory = new FrameworkElementFactory(typeof(ItemsPresenter), "ItemsPresenter");
            itemsPresenterFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
            itemsPresenterFactory.SetValue(FrameworkElement.SnapsToDevicePixelsProperty, true);
            scrollViewerFactory.AppendChild(itemsPresenterFactory);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private static WriteableBitmap GenerateDefaultSkinBitmap(bool isSlim)
        {
            const int width = 64;
            const int height = 64;
            var bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];

            // Steve：肤色偏蓝灰 + 青色衣服
            // Alex：  肤色偏红 + 深绿色衣服
            byte skinB, skinG, skinR;
            byte bodyB, bodyG, bodyR;
            if (isSlim)
            {
                // Alex：偏红色调皮肤 + 深绿色衣服
                skinB = 120; skinG = 145; skinR = 195;
                bodyB = 45;  bodyG = 110; bodyR = 65;
            }
            else
            {
                // Steve：偏蓝色调皮肤 + 青色衣服
                skinB = 155; skinG = 135; skinR = 105;
                bodyB = 150; bodyG = 95;  bodyR = 70;
            }

            // 工具方法：填充指定矩形区域
            void FillRect(int x, int y, int w, int h, byte b, byte g, byte r)
            {
                for (int row = y; row < y + h; row++)
                {
                    if (row < 0 || row >= height) continue;
                    for (int col = x; col < x + w; col++)
                    {
                        if (col < 0 || col >= width) continue;
                        int idx = row * stride + col * 4;
                        pixels[idx + 0] = b;
                        pixels[idx + 1] = g;
                        pixels[idx + 2] = r;
                        pixels[idx + 3] = 255;
                    }
                }
            }

            // ======== 头部区域 (前 16 行) ========
            // 头正面：x=8..16, y=8..16 (8x8)
            FillRect(8, 8, 8, 8, skinB, skinG, skinR);
            // 头顶部：x=8..16, y=0..8
            FillRect(8, 0, 8, 8, (byte)(skinB * 0.85), (byte)(skinG * 0.85), (byte)(skinR * 0.85));
            // 头底部：x=16..24, y=0..8
            FillRect(16, 0, 8, 8, (byte)(skinB * 0.75), (byte)(skinG * 0.75), (byte)(skinR * 0.75));
            // 头右侧：x=0..8, y=8..16
            FillRect(0, 8, 8, 8, (byte)(skinB * 0.9), (byte)(skinG * 0.9), (byte)(skinR * 0.9));
            // 头左侧：x=16..24, y=8..16
            FillRect(16, 8, 8, 8, (byte)(skinB * 0.95), (byte)(skinG * 0.95), (byte)(skinR * 0.95));
            // 头后面：x=24..32, y=8..16
            FillRect(24, 8, 8, 8, (byte)(skinB * 0.88), (byte)(skinG * 0.88), (byte)(skinR * 0.88));
            // 头部二层（帽子）颜色稍深一点
            FillRect(40, 8, 8, 8, (byte)(skinB * 0.7), (byte)(skinG * 0.7), (byte)(skinR * 0.7));
            FillRect(40, 0, 8, 8, (byte)(skinB * 0.6), (byte)(skinG * 0.6), (byte)(skinR * 0.6));
            FillRect(48, 0, 8, 8, (byte)(skinB * 0.55), (byte)(skinG * 0.55), (byte)(skinR * 0.55));
            FillRect(32, 8, 8, 8, (byte)(skinB * 0.65), (byte)(skinG * 0.65), (byte)(skinR * 0.65));
            FillRect(48, 8, 8, 8, (byte)(skinB * 0.72), (byte)(skinG * 0.72), (byte)(skinR * 0.72));
            FillRect(56, 8, 8, 8, (byte)(skinB * 0.6), (byte)(skinG * 0.6), (byte)(skinR * 0.6));

            // ======== 身体区域 (16..32 行) ========
            // 身体正面：x=16..40, y=20..32 (中间 12 高)
            FillRect(16, 20, 24, 12, bodyB, bodyG, bodyR);
            // 身体顶部（背面也占用同样顶部行）
            FillRect(16, 16, 24, 4, (byte)(bodyB * 0.8), (byte)(bodyG * 0.8), (byte)(bodyR * 0.8));
            // 身体背面
            FillRect(32, 20, 24, 12, (byte)(bodyB * 0.9), (byte)(bodyG * 0.9), (byte)(bodyR * 0.9));

            // 左臂（粗臂 4 宽 / 细臂 3 宽）：x=40..44(或43), y=16..28
            int armW = isSlim ? 3 : 4;
            FillRect(40, 20, armW, 12, (byte)(skinB * 0.92), (byte)(skinG * 0.92), (byte)(skinR * 0.92));
            FillRect(40, 16, armW, 4, (byte)(skinB * 0.78), (byte)(skinG * 0.78), (byte)(skinR * 0.78));
            // 左臂外侧、内侧
            FillRect(44, 16, 4, 12, (byte)(skinB * 0.85), (byte)(skinG * 0.85), (byte)(skinR * 0.85));
            FillRect(48, 16, 4, 12, (byte)(skinB * 0.9), (byte)(skinG * 0.9), (byte)(skinR * 0.9));

            // 右臂：x=0..4(或3), y=16..28
            FillRect(0, 20, armW, 12, (byte)(skinB * 0.9), (byte)(skinG * 0.9), (byte)(skinR * 0.9));
            FillRect(0, 16, armW, 4, (byte)(skinB * 0.76), (byte)(skinG * 0.76), (byte)(skinR * 0.76));
            // 右臂外侧、内侧
            FillRect(8, 16, 4, 12, (byte)(skinB * 0.85), (byte)(skinG * 0.85), (byte)(skinR * 0.85));
            FillRect(12, 16, 4, 12, (byte)(skinB * 0.92), (byte)(skinG * 0.92), (byte)(skinR * 0.92));

            // 左腿：x=16..24, y=32..48 (8 宽 x 12 高)
            FillRect(16, 32, 8, 16, (byte)(bodyB * 1.1), (byte)(bodyG * 1.1), (byte)(bodyR * 1.1));
            // 左腿外侧/内侧
            FillRect(8, 32, 8, 16, (byte)(bodyB), (byte)(bodyG), (byte)(bodyR));
            FillRect(24, 32, 8, 16, (byte)(bodyB * 1.05), (byte)(bodyG * 1.05), (byte)(bodyR * 1.05));
            // 左腿底部
            FillRect(16, 48, 8, 4, (byte)(bodyB * 0.8), (byte)(bodyG * 0.8), (byte)(bodyR * 0.8));
            // 左腿顶部
            FillRect(16, 32, 8, 4, (byte)(bodyB * 0.8), (byte)(bodyG * 0.8), (byte)(bodyR * 0.8));

            // 右腿：x=32..40, y=32..48 (8 宽 x 12 高)
            FillRect(32, 32, 8, 16, (byte)(bodyB * 1.1), (byte)(bodyG * 1.1), (byte)(bodyR * 1.1));
            // 右腿外侧/内侧
            FillRect(40, 32, 8, 16, (byte)(bodyB), (byte)(bodyG), (byte)(bodyR));
            FillRect(48, 32, 8, 16, (byte)(bodyB * 1.05), (byte)(bodyG * 1.05), (byte)(bodyR * 1.05));
            // 右腿底部
            FillRect(32, 48, 8, 4, (byte)(bodyB * 0.8), (byte)(bodyG * 0.8), (byte)(bodyR * 0.8));
            // 右腿顶部
            FillRect(32, 32, 8, 4, (byte)(bodyB * 0.8), (byte)(bodyG * 0.8), (byte)(bodyR * 0.8));

            // 腿二层（外裤）颜色
            FillRect(0, 48, 16, 16, (byte)(bodyB * 0.7), (byte)(bodyG * 0.7), (byte)(bodyR * 0.7));
            FillRect(16, 48, 16, 16, (byte)(bodyB * 0.65), (byte)(bodyG * 0.65), (byte)(bodyR * 0.65));
            FillRect(32, 48, 16, 16, (byte)(bodyB * 0.72), (byte)(bodyG * 0.72), (byte)(bodyR * 0.72));
            FillRect(48, 48, 16, 16, (byte)(bodyB * 0.68), (byte)(bodyG * 0.68), (byte)(bodyR * 0.68));

            // 手臂二层（外套）颜色
            FillRect(40, 32, 16, 16, (byte)(bodyB * 0.6), (byte)(bodyG * 0.6), (byte)(bodyR * 0.6));
            FillRect(56, 32, 8, 16, (byte)(bodyB * 0.55), (byte)(bodyG * 0.55), (byte)(bodyR * 0.55));
            FillRect(0, 32, 8, 16, (byte)(bodyB * 0.62), (byte)(bodyG * 0.62), (byte)(bodyR * 0.62));

            bmp.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            bmp.Freeze();
            return bmp;
        }

        private void SelectSource(SkinSource source)
"@

if ($content.Contains($old10)) {
    $content = $content.Replace($old10, $new10)
    Write-Host "[OK] 10. 已添加辅助方法 BuildDarkComboBoxStyle / GenerateDefaultSkinBitmap"
} else {
    Write-Host "[FAIL] 10. 未找到 SelectSource 方法签名（用于插入点）"
}

# ============ 写出文件 ============
[System.IO.File]::WriteAllText($file, $content)
Write-Host ""
Write-Host "完成：文件已保存 $file"
