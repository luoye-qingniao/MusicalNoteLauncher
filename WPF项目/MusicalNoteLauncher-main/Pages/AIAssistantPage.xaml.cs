using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicalNoteLauncher.Core;

namespace MusicalNoteLauncher.Pages
{
    public partial class AIAssistantPage : UserControl
    {
        private AIAgentService _agentService;
        private bool _isSending;
        private string _streamingMessageId = string.Empty;
        private Border _streamingBubble;
        private TextBlock _streamingText;

        private static Core.GameLaunchInfo _pendingLaunchError;
        private static event Action<Core.GameLaunchInfo> LaunchErrorReceived;

        public static void SetLaunchErrorForAnalysis(Core.GameLaunchInfo info)
        {
            _pendingLaunchError = info;
            LaunchErrorReceived?.Invoke(info);
        }

        private static readonly Dictionary<string, (string Name, string Endpoint, string DefaultModel)> ProviderPresets = new()
        {
            ["openai"]     = ("OpenAI",     "https://api.openai.com/v1/chat/completions",         "gpt-4o"),
            ["deepseek"]   = ("DeepSeek",   "https://api.deepseek.com/v1/chat/completions",       "deepseek-chat"),
            ["siliconflow"]= ("硅基流动",   "https://api.siliconflow.cn/v1/chat/completions",     "Qwen/Qwen2.5-7B-Instruct"),
            ["aliyun"]     = ("阿里百炼",   "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen-plus"),
            ["zhipu"]      = ("智谱AI",     "https://open.bigmodel.cn/api/paas/v4/chat/completions", "glm-4-flash"),
            ["custom"]     = ("自定义接口", "",                                                      ""),
        };

        public AIAssistantPage()
        {
            InitializeComponent();
            LoadSettings();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_pendingLaunchError != null)
                {
                    var errorInfo = _pendingLaunchError;
                    _pendingLaunchError = null;
                    _ = AnalyzeLaunchErrorAsync(errorInfo);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            LaunchErrorReceived += OnLaunchErrorReceived;
        }

        private async void OnLaunchErrorReceived(Core.GameLaunchInfo info)
        {
            _pendingLaunchError = null;
            await AnalyzeLaunchErrorAsync(info);
        }

        private string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai_config.json");

        private bool IsConfigured => !string.IsNullOrWhiteSpace(txtApiKey.Text);

        private AiAgentConfig GetConfig()
        {
            return new AiAgentConfig
            {
                Endpoint = txtApiEndpoint.Text?.Trim() ?? "",
                ApiKey = txtApiKey.Text?.Trim() ?? "",
                Model = txtModelName.Text?.Trim() ?? "gpt-3.5-turbo"
            };
        }

        private AIAgentService GetAgentService()
        {
            if (_agentService == null)
            {
                var minecraftPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ".minecraft");
                _agentService = new AIAgentService(minecraftPath);

                _agentService.StreamContentReceived += OnStreamContent;
                _agentService.ToolCallStarted += OnToolCallStarted;
                _agentService.ToolCallCompleted += OnToolCallCompleted;
                _agentService.ConversationCompleted += OnConversationCompleted;
                _agentService.ErrorOccurred += OnError;
                _agentService.ToolProgressChanged += OnToolProgress;
            }
            return _agentService;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<AiConfig>(json);
                    if (config != null)
                    {
                        txtApiEndpoint.Text = config.Endpoint;
                        txtApiKey.Text = config.ApiKey;
                        txtModelName.Text = config.Model;
                    }
                }
                else
                {
                    txtApiEndpoint.Text = "https://api.openai.com/v1/chat/completions";
                    txtModelName.Text = "gpt-3.5-turbo";
                }
            }
            catch
            {
                txtApiEndpoint.Text = "https://api.openai.com/v1/chat/completions";
                txtModelName.Text = "gpt-3.5-turbo";
            }

            AutoMatchProvider();
            UpdateApiStatus();
        }

        private void UpdateApiStatus()
        {
            if (IsConfigured)
            {
                statusDot.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                statusDot.ToolTip = "API 已配置 · Agent 模式就绪";
                txtStatusLabel.Text = "Agent 就绪";
                txtStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                cardConfigPrompt.Visibility = Visibility.Collapsed;
            }
            else
            {
                statusDot.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                statusDot.ToolTip = "API 未配置，点击 ⚙ 配置";
                txtStatusLabel.Text = "未配置";
                txtStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                cardConfigPrompt.Visibility = Visibility.Visible;
            }
        }

        private void AutoMatchProvider()
        {
            var currentEndpoint = txtApiEndpoint.Text?.Trim() ?? "";
            cmbProvider.SelectedIndex = cmbProvider.Items.Count - 1;

            foreach (ComboBoxItem item in cmbProvider.Items)
            {
                if (item.Tag is string tag && ProviderPresets.TryGetValue(tag, out var p))
                {
                    if (!string.IsNullOrEmpty(p.Endpoint) &&
                        currentEndpoint.StartsWith(p.Endpoint.Split('/')[2]))
                    {
                        cmbProvider.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void CmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbProvider.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (tag == "custom") return;
                if (ProviderPresets.TryGetValue(tag, out var preset))
                {
                    if (!string.IsNullOrEmpty(preset.Endpoint))
                        txtApiEndpoint.Text = preset.Endpoint;
                    if (!string.IsNullOrEmpty(preset.DefaultModel))
                        txtModelName.Text = preset.DefaultModel;
                }
            }
        }

        private void SaveSettings()
        {
            try
            {
                var config = new AiConfig
                {
                    Endpoint = txtApiEndpoint.Text,
                    ApiKey = txtApiKey.Text,
                    Model = txtModelName.Text
                };
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(ConfigPath, json);
                popupSettings.IsOpen = false;
                UpdateApiStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenSettings()
        {
            AutoMatchProvider();
            popupSettings.PlacementTarget = btnSettings;
            popupSettings.IsOpen = true;
        }

        // ─────────────── Agent 发送消息 ───────────────

        private async void SendMessage(string content)
        {
            if (_isSending || string.IsNullOrWhiteSpace(content)) return;

            var apiKey = txtApiKey.Text?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                AddMessage("assistant", "你需要先配置 API Key，请在弹出的设置面板中填写后保存。\n\n支持 OpenAI / DeepSeek / 硅基流动 等兼容接口。Agent 模式需要支持 Function Calling 的模型。");
                OpenSettings();
                return;
            }

            _isSending = true;
            btnSend.IsEnabled = false;

            AddMessage("user", content);
            txtInput.Text = "";

            _streamingMessageId = Guid.NewGuid().ToString();
            _streamingBubble = AddStreamingBubble();
            _streamingText = null;

            ShowStatus("正在思考...", "#2ECC71");

            try
            {
                var agent = GetAgentService();
                await agent.SendMessageAsync(GetConfig(), content);
            }
            catch (Exception ex)
            {
                FinishStreaming();
                AddMessage("assistant", $"出错了：{ex.Message}\n\n请检查 API 配置和网络是否正常。");
            }
            finally
            {
                _isSending = false;
                btnSend.IsEnabled = true;
                HideStatus();
                txtInput.Focus();
            }
        }

        private void ShowStatus(string text, string color)
        {
            txtStatus.Text = text;
            txtStatus.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(color);
            txtStatus.Visibility = Visibility.Visible;
        }

        private void HideStatus()
        {
            txtStatus.Visibility = Visibility.Collapsed;
        }

        private void OnToolProgress(string toolName, string status)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var displayName = GetToolDisplayName(toolName);
                ShowStatus($"{displayName}: {status}", "#F39C12");
            });
        }

        // ─────────────── Agent 事件处理 ───────────────

        private void OnStreamContent(string text)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_streamingBubble == null) return;
                if (_streamingText == null)
                    _streamingText = FindStreamingTextBlock(_streamingBubble);

                if (_streamingText != null)
                {
                    _streamingText.Text += text;
                    scrollMessages?.ScrollToBottom();
                }
            });
        }

        private void OnToolCallStarted(string toolName, string args)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ShowStatus($"正在执行: {GetToolDisplayName(toolName)}...", "#F39C12");
                AddToolCallCard(toolName, false);
            });
        }

        private void OnToolCallCompleted(AgentToolResult result)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var icon = result.Success ? "" : "";
                ShowStatus($"{icon} {GetToolDisplayName(result.Name)} {(result.Success ? "完成" : "失败")}",
                    result.Success ? "#2ECC71" : "#E74C3C");
                AddToolResultCard(result);
            });
        }

        private void OnConversationCompleted(string finalContent)
        {
            Dispatcher.InvokeAsync(() =>
            {
                FinishStreaming();
                HideStatus();
            });
        }

        private void OnError(string error)
        {
            Dispatcher.InvokeAsync(() =>
            {
                FinishStreaming();
                AddMessage("assistant", $"{error}");
                HideStatus();
            });
        }

        private void FinishStreaming()
        {
            _streamingMessageId = string.Empty;
            _streamingBubble = null;
            _streamingText = null;
        }

        private TextBlock FindStreamingTextBlock(Border bubble)
        {
            if (bubble.Child is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb && tb.Name == "streamingContent")
                        return tb;
                }
            }
            return null;
        }

        // ─────────────── UI 消息气泡 ───────────────

        private void AddMessage(string role, string content)
        {
            var isUser = role == "user";
            var avatarColor = isUser
                ? Color.FromRgb(155, 89, 182)   // 优雅紫色
                : Color.FromRgb(46, 204, 113);  // 绿色（AI）

            var bubble = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = isUser
                    ? new Thickness(60, 0, 0, 10)
                    : new Thickness(0, 0, 60, 10),
                MaxWidth = 680,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            if (isUser)
            {
                // 用户消息：深色卡片 + 左侧彩色边框（简洁优雅）
                bubble.Background = new SolidColorBrush(Color.FromRgb(40, 40, 45));
                bubble.BorderBrush = new SolidColorBrush(Color.FromRgb(155, 89, 182));
                bubble.BorderThickness = new Thickness(3, 0, 0, 0);
            }
            else
            {
                bubble.Background = (Brush)FindResource("CardBackgroundBrush");
                bubble.BorderBrush = (Brush)FindResource("BorderBrush");
                bubble.BorderThickness = new Thickness(1);
            }

            var inner = new StackPanel();
            inner.Children.Add(CreateMessageHeader(isUser, avatarColor));

            var text = new TextBlock
            {
                Text = content,
                FontSize = 14,
                Foreground = isUser ? Brushes.White : (Brush)FindResource("TextPrimaryBrush"),
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            };
            inner.Children.Add(text);
            bubble.Child = inner;

            spMessages.Children.Add(bubble);
            scrollMessages?.ScrollToBottom();
        }

        private Border AddStreamingBubble()
        {
            var bubble = new Border
            {
                Background = (Brush)FindResource("CardBackgroundBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 60, 10),
                MaxWidth = 680,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var inner = new StackPanel();
            inner.Children.Add(CreateMessageHeader(false, Color.FromRgb(46, 204, 113)));

            var text = new TextBlock
            {
                Name = "streamingContent",
                Text = "",
                FontSize = 14,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            };
            inner.Children.Add(text);
            bubble.Child = inner;

            spMessages.Children.Add(bubble);
            scrollMessages?.ScrollToBottom();

            return bubble;
        }

        private StackPanel CreateMessageHeader(bool isUser, Color avatarColor)
        {
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var ava = new Border
            {
                Width = 30, Height = 30,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(avatarColor)
            };
            ava.Child = new TextBlock
            {
                Text = isUser ? "\U0001F464" : "\U0001F916",
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var name = new TextBlock
            {
                Text = isUser ? "我" : "小音符",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = isUser
                    ? new SolidColorBrush(Color.FromRgb(187, 134, 252))
                    : new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontFamily = new FontFamily("Microsoft YaHei")
            };

            header.Children.Add(ava);
            header.Children.Add(name);
            return header;
        }

        private void AddToolCallCard(string toolName, bool isResult)
        {
            var icon = GetToolIcon(toolName);
            var displayName = GetToolDisplayName(toolName);

            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 60, 6),
                MaxWidth = 680,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                BorderThickness = new Thickness(1),
                Opacity = 0.9
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"正在执行: {displayName}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                FontFamily = new FontFamily("Microsoft YaHei"),
                VerticalAlignment = VerticalAlignment.Center
            });

            card.Child = stack;
            spMessages.Children.Add(card);
            scrollMessages?.ScrollToBottom();
        }

        private void AddToolResultCard(AgentToolResult result)
        {
            var ok = result.Success;
            var bgColor = ok ? Color.FromRgb(20, 55, 35) : Color.FromRgb(58, 26, 26);
            var borderColor = ok ? Color.FromRgb(46, 204, 113) : Color.FromRgb(231, 76, 60);
            var icon = ok ? "✅" : "❌";

            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 60, 6),
                MaxWidth = 680,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1),
                Opacity = 0.85
            };

            var stack = new StackPanel();
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            headerRow.Children.Add(new TextBlock
            {
                Text = $"{icon} {GetToolDisplayName(result.Name)}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(borderColor),
                FontFamily = new FontFamily("Microsoft YaHei")
            });

            stack.Children.Add(headerRow);

            var msg = result.Message;
            if (msg.Length > 300) msg = msg.Substring(0, 300) + "...";
            stack.Children.Add(new TextBlock
            {
                Text = msg,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            });

            card.Child = stack;
            spMessages.Children.Add(card);
            scrollMessages?.ScrollToBottom();
        }

        private static string GetToolIcon(string toolName) => toolName switch
        {
            "launch_game" => "🚀",
            "download_version" => "📥",
            "download_mod" => "🔍",
            "repair_version" => "🔧",
            "get_installed_versions" => "📋",
            "get_system_info" => "🖥",
            "install_loader" => "⚙",
            "delete_version" => "🗑",
            "check_mods" => "📦",
            _ => "🔨"
        };

        private static string GetToolDisplayName(string toolName) => toolName switch
        {
            "launch_game" => "启动游戏",
            "download_version" => "下载版本",
            "download_mod" => "搜索模组",
            "repair_version" => "修复版本",
            "get_installed_versions" => "版本列表",
            "get_system_info" => "系统诊断",
            "install_loader" => "安装加载器",
            "delete_version" => "删除版本",
            "check_mods" => "检查模组",
            _ => toolName
        };

        // ─────────────── 按钮事件 ───────────────

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(txtInput.Text);
        }

        private void TxtInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter &&
                !System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) &&
                !System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
            {
                e.Handled = true;
                SendMessage(txtInput.Text);
            }
        }

        private void BtnQuickQuestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                txtInput.Text = btn.Content.ToString();
                SendMessage(btn.Content.ToString());
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            popupSettings.IsOpen = false;
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _agentService?.ClearHistory();

            var welcomeCard = spMessages.Children[0];
            spMessages.Children.Clear();
            spMessages.Children.Add(welcomeCard);

            HideStatus();
            FinishStreaming();
        }

        private void CardConfigPrompt_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenSettings();
        }

        // ─────────────── 启动错误分析 ───────────────

        private async Task AnalyzeLaunchErrorAsync(Core.GameLaunchInfo info)
        {
            var logText = info.GetErrorSummary(200);
            var localResult = CrashAnalyzer.Analyze(logText, info.ExitCode, info.ErrorMessage);

            if (localResult.IsKnown)
            {
                var localReport = CrashAnalyzer.FormatResult(localResult);
                AddMessage("assistant", "本地诊断已完成，检测到已知问题模式：\n\n" + localReport);
                await Task.Delay(800);
                AddMessage("assistant", "正在使用 AI Agent 进行深度分析，以获取更详细的解决方案…");
            }

            var prompt = new StringBuilder();
            prompt.AppendLine("我的 Minecraft 游戏启动失败了，请帮我分析一下错误原因：");
            prompt.AppendLine();
            prompt.AppendLine($"游戏版本: {info.VersionId}");
            prompt.AppendLine($"Java 路径: {info.JavaPath}");
            prompt.AppendLine($"退出码: {info.ExitCode}");
            prompt.AppendLine($"错误信息: {info.ErrorMessage}");
            prompt.AppendLine();

            if (localResult.IsKnown)
            {
                prompt.AppendLine($"本地诊断已初步判定为：{localResult.Title}");
                prompt.AppendLine($"本地建议：{localResult.Suggestion}");
                prompt.AppendLine("请在此基础上进行深度分析，提供更具体的操作步骤。");
            }

            prompt.AppendLine();
            prompt.AppendLine("以下是游戏启动日志中的错误部分：");
            prompt.AppendLine("```");
            prompt.AppendLine(logText);
            prompt.AppendLine("```");
            prompt.AppendLine();
            prompt.AppendLine("如果判断是版本文件损坏，请直接使用 repair_version 工具帮我修复。");

            txtInput.Text = "";
            SendMessage(prompt.ToString());
        }
    }

    public class AiConfig
    {
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
    }
}
