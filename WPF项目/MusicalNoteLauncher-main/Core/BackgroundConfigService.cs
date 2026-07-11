using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace MusicalNoteLauncher.Core
{
    /// <summary>
    /// 背景配置管理服务：单例，负责背景模式切换、参数持久化、预览生效。
    /// 复用 theme_config.json 中新增的 Background* 字段，不新建配置文件。
    /// </summary>
    public class BackgroundConfigService : INotifyPropertyChanged
    {
        private static readonly Lazy<BackgroundConfigService> _instance =
            new(() => new BackgroundConfigService());

        public static BackgroundConfigService Instance => _instance.Value;

        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme_config.json");

        private ThemeConfig _config;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        // ── 笔刷透明度管理 ──
        /// <summary>需要做半透明处理的背景型笔刷 Key 列表</summary>
        private static readonly string[] PanelBrushKeys =
        {
            "SurfaceBrush", "CardBackgroundBrush", "BackgroundBrush", "CardHoverBrush"
        };

        /// <summary>各笔刷的透明度强度系数（值越大越透）</summary>
        private static readonly Dictionary<string, double> BrushTransparencyFactor = new()
        {
            ["BackgroundBrush"] = 0.70,       // 窗口底色 → 最透
            ["SurfaceBrush"] = 0.35,           // 输入框/卡片 → 中等
            ["CardBackgroundBrush"] = 0.35,    // 卡片背景 → 中等
            ["CardHoverBrush"] = 0.28,         // 侧边栏 → 适度透明
        };
        // ── 防抖保存 ──
        private System.Threading.Timer _debounceSaveTimer;

        private void DebouncedSaveConfig()
        {
            _debounceSaveTimer?.Dispose();
            _debounceSaveTimer = new System.Threading.Timer(_ => SaveConfig(), null, 500, System.Threading.Timeout.Infinite);
        }

        /// <summary>首次捕获的原始笔刷颜色快照</summary>
        private Dictionary<string, Color> _originalBrushColors;

        // ── 可绑定属性 ──

        private BackgroundMode _mode = BackgroundMode.Mica;
        public BackgroundMode Mode
        {
            get => _mode;
            set { if (_mode != value) { _mode = value; OnPropertyChanged(); } }
        }

        private string _imagePath = "";
        public string ImagePath
        {
            get => _imagePath;
            set { if (_imagePath != value) { _imagePath = value; OnPropertyChanged(); } }
        }

        private string _videoPath = "";
        public string VideoPath
        {
            get => _videoPath;
            set { if (_videoPath != value) { _videoPath = value; OnPropertyChanged(); } }
        }

        private double _opacity = 0.6;
        public double Opacity
        {
            get => _opacity;
            set
            {
                double clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_opacity - clamped) < 0.001) return;
                _opacity = clamped;
                OnPropertyChanged();
                OnBackgroundChanged();
                RefreshPanelTransparency();
                DebouncedSaveConfig();
            }
        }

        private double _blurRadius = 0;
        public double BlurRadius
        {
            get => _blurRadius;
            set
            {
                double clamped = Math.Clamp(value, 0.0, 20.0);
                if (Math.Abs(_blurRadius - clamped) > 0.001)
                { _blurRadius = clamped; OnPropertyChanged(); DebouncedSaveConfig(); }
            }
        }

        // ── 事件 ──

        /// <summary>背景配置发生变更时触发，MainWindow 订阅以刷新背景显示</summary>
        public event Action BackgroundChanged;
        /// <summary>背景模式切换时触发</summary>
        public event Action<BackgroundMode> ModeChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        private BackgroundConfigService()
        {
            LoadConfig();
        }

        // ── 加载/保存 ──

        /// <summary>
        /// 从 theme_config.json 加载背景配置，兼容旧版无背景字段的配置文件。
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    _config = JsonSerializer.Deserialize<ThemeConfig>(json) ?? new ThemeConfig();
                }
                else
                {
                    _config = new ThemeConfig();
                }

                // 解析背景模式
                if (!Enum.TryParse<BackgroundMode>(_config.BackgroundMode, out var parsedMode))
                    parsedMode = BackgroundMode.Mica;
                _mode = parsedMode;

                _imagePath = _config.BackgroundImagePath ?? "";
                _videoPath = _config.BackgroundVideoPath ?? "";
                _opacity = Math.Clamp(_config.BackgroundOpacity, 0.0, 1.0);
                _blurRadius = Math.Clamp(_config.BackgroundBlurRadius, 0.0, 20.0);

                Logger.Info($"背景配置加载成功: Mode={Mode}, Opacity={Opacity:F2}, Blur={BlurRadius:F0}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"背景配置加载失败，使用默认值: {ex.Message}");
                _config = new ThemeConfig();
                _mode = BackgroundMode.Mica;
                _imagePath = "";
                _videoPath = "";
                _opacity = 0.6;
                _blurRadius = 0;
            }
        }

        /// <summary>
        /// 保存背景配置到 theme_config.json，复用现有的 JSON 读写逻辑。
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                if (_config == null) _config = new ThemeConfig();

                _config.BackgroundMode = _mode.ToString();
                _config.BackgroundImagePath = _imagePath;
                _config.BackgroundVideoPath = _videoPath;
                _config.BackgroundOpacity = _opacity;
                _config.BackgroundBlurRadius = _blurRadius;

                // 读取现有文件保留主题字段
                ThemeConfig existing = null;
                if (File.Exists(ConfigPath))
                {
                    try
                    {
                        string existingJson = File.ReadAllText(ConfigPath);
                        existing = JsonSerializer.Deserialize<ThemeConfig>(existingJson);
                    }
                    catch { }
                }

                if (existing != null)
                {
                    // 仅覆盖背景字段，保留 ThemePreset
                    existing.BackgroundMode = _config.BackgroundMode;
                    existing.BackgroundImagePath = _config.BackgroundImagePath;
                    existing.BackgroundVideoPath = _config.BackgroundVideoPath;
                    existing.BackgroundOpacity = _config.BackgroundOpacity;
                    existing.BackgroundBlurRadius = _config.BackgroundBlurRadius;

                    string json = JsonSerializer.Serialize(existing, _jsonOptions);
                    File.WriteAllText(ConfigPath, json);
                }
                else
                {
                    // 无现有文件，新建（ThemePreset 使用默认）
                    _config.ThemePreset = ThemeColorPreset.ClassicDark.ToString();
                    string json = JsonSerializer.Serialize(_config, _jsonOptions);
                    File.WriteAllText(ConfigPath, json);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"背景配置保存失败: {ex.Message}");
            }
        }

        // ── 模式切换 ──

        /// <summary>
        /// 切换背景模式。自动清空其他模式的路径，保存配置并发出通知。
        /// </summary>
        /// <returns>成功返回 true；视频/图片路径无效且无法回退时返回 false。</returns>
        public bool SetMode(BackgroundMode newMode)
        {
            if (newMode == BackgroundMode.Image && string.IsNullOrWhiteSpace(_imagePath))
            {
                // 未设置图片路径，不允许切换到图片模式
                return false;
            }
            if (newMode == BackgroundMode.Video && string.IsNullOrWhiteSpace(_videoPath))
            {
                return false;
            }

            var oldMode = _mode;
            _mode = newMode;

            // 切换时清空其他模式路径
            if (newMode == BackgroundMode.Mica)
            {
                _imagePath = "";
                _videoPath = "";
            }
            else if (newMode == BackgroundMode.Image)
            {
                _videoPath = "";
            }
            else if (newMode == BackgroundMode.Video)
            {
                _imagePath = "";
            }

            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(ImagePath));
            OnPropertyChanged(nameof(VideoPath));
            SaveConfig();
            ModeChanged?.Invoke(newMode);
            RefreshPanelTransparency();
            OnBackgroundChanged();

            Logger.Info($"背景模式切换: {oldMode} -> {newMode}");
            return true;
        }

        /// <summary>
        /// 设置图片背景路径，自动校验文件格式。
        /// </summary>
        public bool SetImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _imagePath = "";
                OnPropertyChanged(nameof(ImagePath));
                SaveConfig();
                return true;
            }

            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
                {
                    Logger.Warning($"不支持的图片格式: {ext}");
                    return false;
                }

                if (!File.Exists(path))
                {
                    Logger.Warning($"图片文件不存在: {path}");
                    return false;
                }

                _imagePath = path;
                _videoPath = "";
                OnPropertyChanged(nameof(ImagePath));
                OnPropertyChanged(nameof(VideoPath));
                SaveConfig();
                OnBackgroundChanged();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"设置图片路径失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 设置视频背景路径，自动校验文件格式。
        /// </summary>
        public bool SetVideoPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _videoPath = "";
                OnPropertyChanged(nameof(VideoPath));
                SaveConfig();
                return true;
            }

            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".mp4")
                {
                    Logger.Warning($"不支持的视频格式: {ext}");
                    return false;
                }

                if (!File.Exists(path))
                {
                    Logger.Warning($"视频文件不存在: {path}");
                    return false;
                }

                _videoPath = path;
                _imagePath = "";
                OnPropertyChanged(nameof(VideoPath));
                OnPropertyChanged(nameof(ImagePath));
                SaveConfig();
                OnBackgroundChanged();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"设置视频路径失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重置为默认 Mica 云母背景，清空所有自定义素材配置。
        /// </summary>
        public void ResetToDefault()
        {
            _mode = BackgroundMode.Mica;
            _imagePath = "";
            _videoPath = "";
            _opacity = 0.6;
            _blurRadius = 0;

            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(ImagePath));
            OnPropertyChanged(nameof(VideoPath));
            OnPropertyChanged(nameof(Opacity));
            OnPropertyChanged(nameof(BlurRadius));

            SaveConfig();
            ModeChanged?.Invoke(BackgroundMode.Mica);
            RestorePanelTransparency();
            OnBackgroundChanged();

            Logger.Info("背景配置已重置为默认 Mica 模式");
        }

        /// <summary>
        /// 校验当前背景文件是否有效。无效时自动回退到 Mica 模式。
        /// </summary>
        public bool ValidateCurrentBackground()
        {
            if (_mode == BackgroundMode.Mica)
                return true;

            if (_mode == BackgroundMode.Image)
            {
                if (string.IsNullOrWhiteSpace(_imagePath) || !File.Exists(_imagePath))
                {
                    Logger.Warning($"图片背景文件失效，回退到 Mica 模式: {_imagePath}");
                    FallbackToMica();
                    return false;
                }
                string ext = Path.GetExtension(_imagePath).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
                {
                    Logger.Warning($"图片背景格式不支持，回退到 Mica 模式: {ext}");
                    FallbackToMica();
                    return false;
                }
                return true;
            }

            if (_mode == BackgroundMode.Video)
            {
                if (string.IsNullOrWhiteSpace(_videoPath) || !File.Exists(_videoPath))
                {
                    Logger.Warning($"视频背景文件失效，回退到 Mica 模式: {_videoPath}");
                    FallbackToMica();
                    return false;
                }
                if (Path.GetExtension(_videoPath).ToLowerInvariant() != ".mp4")
                {
                    Logger.Warning($"视频背景格式不支持，回退到 Mica 模式");
                    FallbackToMica();
                    return false;
                }
                return true;
            }

            return true;
        }

        private void FallbackToMica()
        {
            _mode = BackgroundMode.Mica;
            _imagePath = "";
            _videoPath = "";
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(ImagePath));
            OnPropertyChanged(nameof(VideoPath));
            SaveConfig();
            ModeChanged?.Invoke(BackgroundMode.Mica);
            RestorePanelTransparency();
            OnBackgroundChanged();
        }

        // ── 面板笔刷透明度调节 ──

        /// <summary>上次捕获笔刷时的主题预设，用于检测主题切换</summary>
        private string _lastKnownThemePreset;

        /// <summary>
        /// 捕获 Application 资源中背景型笔刷的当前颜色快照。
        /// 仅在首次调用或主题切换后重新捕获，防止已透明化的笔刷被误存为"原始值"。
        /// </summary>
        private void CaptureOriginalBrushColors()
        {
            if (_originalBrushColors != null)
                return; // 本会话内已捕获，透明度调节期间不需要重复读文件

            string currentPreset = "";
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var temp = JsonSerializer.Deserialize<ThemeConfig>(File.ReadAllText(ConfigPath));
                    currentPreset = temp?.ThemePreset ?? "";
                }
            }
            catch { currentPreset = _config?.ThemePreset ?? ""; }

            var resources = Application.Current?.Resources;
            if (resources == null) { Logger.Warning("[BG] CaptureOriginalBrushColors: Application.Current.Resources 为 null"); return; }

            var captured = new Dictionary<string, Color>();
            foreach (string key in PanelBrushKeys)
            {
                if (resources[key] is SolidColorBrush brush)
                {
                    captured[key] = brush.Color;
                }
                else
                {
                    Logger.Warning($"[BG] CaptureOriginalBrushColors: 未找到笔刷 {key}");
                }
            }

            if (captured.Count == 0) { Logger.Warning("[BG] CaptureOriginalBrushColors: 未捕获任何笔刷"); return; }

            _originalBrushColors = captured;
            _lastKnownThemePreset = currentPreset;
            Logger.Info($"[BG] CaptureOriginalBrushColors: 已捕获 {captured.Count} 个笔刷, 主题={currentPreset}");
        }

        /// <summary>
        /// 根据当前背景透明度，对各面板笔刷施加差异化半透明效果。
        /// 不同笔刷使用不同强度系数：窗口底色最透，侧边栏微透以保证文字可读。
        /// </summary>
        private void ApplyPanelTransparency()
        {
            CaptureOriginalBrushColors();
            if (_originalBrushColors == null) return;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            foreach (string key in PanelBrushKeys)
            {
                if (!_originalBrushColors.TryGetValue(key, out var orig) ||
                    !(resources[key] is SolidColorBrush))
                    continue;

                double strength = BrushTransparencyFactor.TryGetValue(key, out var f) ? f : 0.18;
                double factor = 1.0 - (_opacity * strength);
                byte a = (byte)Math.Clamp((int)(orig.A * factor), 0, 255);
                resources[key] = new SolidColorBrush(Color.FromArgb(a, orig.R, orig.G, orig.B));
            }
        }

        /// <summary>
        /// 恢复所有面板笔刷到当前主题的原始完全不透明状态。
        /// </summary>
        private void RestorePanelTransparency()
        {
            CaptureOriginalBrushColors();
            if (_originalBrushColors == null) return;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            foreach (string key in PanelBrushKeys)
            {
                if (_originalBrushColors.TryGetValue(key, out var orig))
                {
                    resources[key] = new SolidColorBrush(orig);
                }
            }
        }

        /// <summary>
        /// 根据当前模式刷新面板笔刷透明度：Mica 恢复原样，Image/Video 施加半透明。
        /// </summary>
        public void RefreshPanelTransparency()
        {
            if (_mode == BackgroundMode.Mica)
                RestorePanelTransparency();
            else
                ApplyPanelTransparency();
        }

        /// <summary>主题切换后调用，强制下次 Capture 重新抓取笔刷颜色</summary>
        public void InvalidateBrushCache()
        {
            _originalBrushColors = null;
        }

        private void OnBackgroundChanged()
        {
            BackgroundChanged?.Invoke();
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
