using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 6套Minecraft风格主题配色预设枚举
    /// </summary>
    public enum ThemeColorPreset
    {
        /// <summary>经典深色（默认） - 主色#2C2C2C 强调色#007ACC</summary>
        ClassicDark,
        /// <summary>清新草绿 - 主色#2E7D32 强调色#81C784</summary>
        FreshGrassGreen,
        /// <summary>深海矿石蓝 - 主色#1976D2 强调色#64B5F6</summary>
        DeepOceanBlue,
        /// <summary>暖沙石橙黄 - 主色#F57C00 强调色#FFB74D</summary>
        WarmSandOrange,
        /// <summary>丛林绯红 - 主色#C62828 强调色#EF5350</summary>
        JungleCrimson,
        /// <summary>暖白温馨 - 主色#FDFBF7 强调色#E67E22</summary>
        PureWhite
    }

    /// <summary>
    /// 单套完整配色信息（14色）
    /// </summary>
    public class ThemeColorInfo
    {
        public ThemeColorPreset Preset { get; set; }
        public string Name { get; set; }

        // ── 基础背景 ──
        public string BackgroundColor { get; set; }        // 页面底色
        public string CardBackgroundColor { get; set; }     // 卡片/面板底色
        public string SurfaceColor { get; set; }            // 输入框/下拉框底色
        public string BorderColor { get; set; }             // 边框色
        public string TextPrimaryColor { get; set; }        // 主文字
        public string TextSecondaryColor { get; set; }      // 次文字

        // ── 强调色 ──
        public string PrimaryColor { get; set; }            // 主强调（按钮/滑块高亮, 原主色）
        public string PrimaryDarkColor { get; set; }        // 深强调（hover）
        public string AccentColor { get; set; }             // 辅强调
        public string AccentLightColor { get; set; }        // 浅辅强调

        // ── ComboBox 专用 ──
        public string ComboBoxBgColor { get; set; }
        public string ComboBoxHoverColor { get; set; }
        public string ComboBoxSelectedColor { get; set; }

        // ── 悬停状态 ──
        public string CardHoverColor { get; set; }
    }

    /// <summary>
    /// theme_config.json 持久化结构
    /// </summary>
    internal class ThemeConfig
    {
        public string ThemePreset { get; set; } = ThemeColorPreset.ClassicDark.ToString();
    }

    /// <summary>
    /// 配色管理服务：5套预设枚举+色值映射表+JSON持久化+运行时动态全局换色
    /// </summary>
    public static class ThemeColorService
    {
        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme_config.json");

        /// <summary>当前生效的配色预设</summary>
        public static ThemeColorPreset CurrentTheme { get; private set; } = ThemeColorPreset.ClassicDark;

        /// <summary>5套配色完整映射表（14色）</summary>
        public static readonly IReadOnlyDictionary<ThemeColorPreset, ThemeColorInfo> ThemeMap =
            new Dictionary<ThemeColorPreset, ThemeColorInfo>
            {
                // ── 经典深色 ──
                [ThemeColorPreset.ClassicDark] = new ThemeColorInfo
                {
                    Preset = ThemeColorPreset.ClassicDark,
                    Name = "经典深色",
                    BackgroundColor = "#1E1E1E",
                    CardBackgroundColor = "#252525",
                    SurfaceColor = "#383838",
                    BorderColor = "#3A3A3A",
                    TextPrimaryColor = "#FFFFFF",
                    TextSecondaryColor = "#AAAAAA",
                    PrimaryColor = "#2196F3",
                    PrimaryDarkColor = "#1976D2",
                    AccentColor = "#2196F3",
                    AccentLightColor = "#1976D2",
                    ComboBoxBgColor = "#333333",
                    ComboBoxHoverColor = "#3A3A3A",
                    ComboBoxSelectedColor = "#1A73E8",
                    CardHoverColor = "#353535"
                },
                // ── 清新草绿 ──
                [ThemeColorPreset.FreshGrassGreen] = new ThemeColorInfo
                {
                    Preset = ThemeColorPreset.FreshGrassGreen,
                    Name = "清新草绿",
                    BackgroundColor = "#1A2315",
                    CardBackgroundColor = "#1F2A1A",
                    SurfaceColor = "#2A3822",
                    BorderColor = "#33442B",
                    TextPrimaryColor = "#FFFFFF",
                    TextSecondaryColor = "#AAAAAA",
                    PrimaryColor = "#2E7D32",
                    PrimaryDarkColor = "#1B5E20",
                    AccentColor = "#81C784",
                    AccentLightColor = "#A5D6A7",
                    ComboBoxBgColor = "#283520",
                    ComboBoxHoverColor = "#33442B",
                    ComboBoxSelectedColor = "#1B5E20",
                    CardHoverColor = "#2A3822"
                },
                // ── 深海矿石蓝 ──
                [ThemeColorPreset.DeepOceanBlue] = new ThemeColorInfo
                {
                    Preset = ThemeColorPreset.DeepOceanBlue,
                    Name = "深海矿石蓝",
                    BackgroundColor = "#1A1E2A",
                    CardBackgroundColor = "#1F2535",
                    SurfaceColor = "#2A3247",
                    BorderColor = "#35405A",
                    TextPrimaryColor = "#FFFFFF",
                    TextSecondaryColor = "#AAAAAA",
                    PrimaryColor = "#1976D2",
                    PrimaryDarkColor = "#1565C0",
                    AccentColor = "#64B5F6",
                    AccentLightColor = "#90CAF9",
                    ComboBoxBgColor = "#283046",
                    ComboBoxHoverColor = "#35405A",
                    ComboBoxSelectedColor = "#0D47A1",
                    CardHoverColor = "#2A3247"
                },
                // ── 暖沙石橙黄 ──
                [ThemeColorPreset.WarmSandOrange] = new ThemeColorInfo
                {
                    Preset = ThemeColorPreset.WarmSandOrange,
                    Name = "暖沙石橙黄",
                    BackgroundColor = "#251E15",
                    CardBackgroundColor = "#2C251A",
                    SurfaceColor = "#3D3425",
                    BorderColor = "#4D4230",
                    TextPrimaryColor = "#FFFFFF",
                    TextSecondaryColor = "#AAAAAA",
                    PrimaryColor = "#F57C00",
                    PrimaryDarkColor = "#E65100",
                    AccentColor = "#FFB74D",
                    AccentLightColor = "#FFCC80",
                    ComboBoxBgColor = "#3A3225",
                    ComboBoxHoverColor = "#4D4230",
                    ComboBoxSelectedColor = "#BF360C",
                    CardHoverColor = "#3D3425"
                },
                // ── 丛林绯红 ──
                [ThemeColorPreset.JungleCrimson] = new ThemeColorInfo
                {
                    Preset = ThemeColorPreset.JungleCrimson,
                    Name = "丛林绯红",
                    BackgroundColor = "#1F1515",
                    CardBackgroundColor = "#251A1A",
                    SurfaceColor = "#352222",
                    BorderColor = "#452A2A",
                    TextPrimaryColor = "#FFFFFF",
                    TextSecondaryColor = "#AAAAAA",
                    PrimaryColor = "#C62828",
                    PrimaryDarkColor = "#B71C1C",
                    AccentColor = "#EF5350",
                    AccentLightColor = "#E57373",
                    ComboBoxBgColor = "#322020",
                    ComboBoxHoverColor = "#452A2A",
                    ComboBoxSelectedColor = "#880E4F",
                    CardHoverColor = "#352222"
                },
                // ── 暖白温馨 ──
                [ThemeColorPreset.PureWhite] = new ThemeColorInfo
                {
                    Preset = ThemeColorPreset.PureWhite,
                    Name = "暖白温馨",
                    BackgroundColor = "#FDFBF7",
                    CardBackgroundColor = "#FAF7F2",
                    SurfaceColor = "#F0ECE5",
                    BorderColor = "#D0C4B5",
                    TextPrimaryColor = "#000000",
                    TextSecondaryColor = "#333333",
                    PrimaryColor = "#E67E22",
                    PrimaryDarkColor = "#D35400",
                    AccentColor = "#F0932B",
                    AccentLightColor = "#F9CA24",
                    ComboBoxBgColor = "#FAF7F2",
                    ComboBoxHoverColor = "#F0ECE5",
                    ComboBoxSelectedColor = "#FDE9D9",
                    CardHoverColor = "#F5F0E8"
                }
            };

        /// <summary>获取所有配色列表（用于UI绑定）</summary>
        public static List<ThemeColorInfo> GetAllThemes() => ThemeMap.Values.ToList();

        /// <summary>
        /// 切换配色：更新所有14色全局资源（单色画刷+渐变+颜色值）。
        /// </summary>
        public static void ApplyTheme(ThemeColorPreset preset)
        {
            if (!ThemeMap.TryGetValue(preset, out var info))
                return;

            var appResources = Application.Current.Resources;

            // ── 解析 Hex → Color ──
            var bg = ParseColor(info.BackgroundColor);
            var cardBg = ParseColor(info.CardBackgroundColor);
            var surface = ParseColor(info.SurfaceColor);
            var border = ParseColor(info.BorderColor);
            var textPri = ParseColor(info.TextPrimaryColor);
            var textSec = ParseColor(info.TextSecondaryColor);
            var primary = ParseColor(info.PrimaryColor);
            var primaryDark = ParseColor(info.PrimaryDarkColor);
            var accent = ParseColor(info.AccentColor);
            var accentLight = ParseColor(info.AccentLightColor);
            var comboBg = ParseColor(info.ComboBoxBgColor);
            var comboHover = ParseColor(info.ComboBoxHoverColor);
            var comboSelected = ParseColor(info.ComboBoxSelectedColor);
            var cardHover = ParseColor(info.CardHoverColor);

            // ── 单色画刷（14个） ──
            SetBrushResource(appResources, "BackgroundBrush", bg);
            SetBrushResource(appResources, "CardBackgroundBrush", cardBg);
            SetBrushResource(appResources, "SurfaceBrush", surface);
            SetBrushResource(appResources, "BorderBrush", border);
            SetBrushResource(appResources, "TextPrimaryBrush", textPri);
            SetBrushResource(appResources, "TextSecondaryBrush", textSec);
            SetBrushResource(appResources, "PrimaryBrush", primary);
            SetBrushResource(appResources, "PrimaryDarkBrush", primaryDark);
            SetBrushResource(appResources, "AccentBrush", accent);
            SetBrushResource(appResources, "AccentLightBrush", accentLight);
            SetBrushResource(appResources, "ComboBoxBackgroundBrush", comboBg);
            SetBrushResource(appResources, "ComboBoxHoverBrush", comboHover);
            SetBrushResource(appResources, "ComboBoxSelectedBrush", comboSelected);
            SetBrushResource(appResources, "CardHoverBrush", cardHover);

            // ── 渐变画刷 ──
            SetGradientBrushResource(appResources, "PrimaryGradientBrush", primary, primaryDark);
            SetGradientBrushResource(appResources, "AccentGradientBrush", accent, accentLight);

            // ── Color 原始值 ──
            appResources["BackgroundColor"] = bg;
            appResources["CardBackgroundColor"] = cardBg;
            appResources["SurfaceColor"] = surface;
            appResources["BorderColor"] = border;
            appResources["TextPrimaryColor"] = textPri;
            appResources["TextSecondaryColor"] = textSec;
            appResources["PrimaryColor"] = primary;
            appResources["PrimaryDarkColor"] = primaryDark;
            appResources["AccentColor"] = accent;
            appResources["AccentLightColor"] = accentLight;
            appResources["ComboBoxBackgroundColor"] = comboBg;
            appResources["ComboBoxHoverColor"] = comboHover;
            appResources["ComboBoxSelectedColor"] = comboSelected;
            appResources["CardHoverColor"] = cardHover;

            CurrentTheme = preset;
            SaveConfig();

            LogThemeChange(info);
        }

        private static Color ParseColor(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        /// <summary>启动时加载上次保存的配色</summary>
        public static void LoadSavedTheme()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<ThemeConfig>(json);
                    if (config != null && Enum.TryParse<ThemeColorPreset>(config.ThemePreset, out var saved))
                    {
                        ApplyTheme(saved);
                        return;
                    }
                }
            }
            catch
            {
                // 配置文件损坏则使用默认配色（已在ApplyTheme中设为默认）
            }

            // 首次启动：应用默认「经典深色」
            ApplyTheme(ThemeColorPreset.ClassicDark);
        }

        private static void SaveConfig()
        {
            try
            {
                var config = new ThemeConfig { ThemePreset = CurrentTheme.ToString() };
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // 忽略保存错误，不影响主流程
            }
        }

        /// <summary>
        /// 设置单色画刷资源：
        /// ① 资源存在且可写 → 修改.Color
        /// ② 资源不存在或已冻结 → 写入新实例（新实例默认未冻结，下次可直接修改）
        /// </summary>
        private static void SetBrushResource(ResourceDictionary resources, string key, Color color)
        {
            var existing = resources[key] as SolidColorBrush;
            if (existing != null && !existing.IsFrozen)
            {
                existing.Color = color;
                return;
            }
            resources[key] = new SolidColorBrush(color);
        }

        /// <summary>
        /// 设置渐变画刷资源：始终重建新实例，避免GradientStop被冻结的问题。
        /// </summary>
        private static void SetGradientBrushResource(ResourceDictionary resources, string key,
            Color primaryColor, Color secondaryColor)
        {
            var gradient = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(primaryColor, 0),
                    new GradientStop(secondaryColor, 1)
                }
            };
            resources[key] = gradient;
        }

        /// <summary>将颜色变暗指定比例（0~1），值越大越暗</summary>
        private static Color DarkenColor(Color color, float factor)
        {
            return Color.FromRgb(
                (byte)(color.R * (1 - factor)),
                (byte)(color.G * (1 - factor)),
                (byte)(color.B * (1 - factor))
            );
        }

        private static void LogThemeChange(ThemeColorInfo info)
        {
            Logger.Info($"配色切换: {info.Name} | 背景={info.BackgroundColor} 主强调={info.PrimaryColor}");
        }
    }
}
