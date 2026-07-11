using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        private const string SystemPrompt =
            "你是「音符启动器」的 AI 游戏助手，专注于帮助用户解决 Minecraft 及相关游戏的问题。" +
            "你可以回答关于：Minecraft 游戏玩法、红石电路、建筑技巧、模组(Mod)安装与使用、" +
            "光影(Shader)配置、服务器搭建、游戏命令、Boss 攻略、资源包推荐等问题。" +
            "请用中文回答，语气友好热情，回答简洁实用。如果用户问的不是游戏相关问题，" +
            "请友好地引导用户回到 Minecraft 游戏话题。你的名字叫「小音符」。";

        private readonly HttpClient _httpClient = new();
        private bool _isSending;

        /// <summary>来自启动失败弹窗的错误信息，供 AI 自动分析</summary>
        private static Core.GameLaunchInfo _pendingLaunchError;

        /// <summary>接收来自启动弹窗的错误信息</summary>
        public static void SetLaunchErrorForAnalysis(Core.GameLaunchInfo info)
        {
            _pendingLaunchError = info;
        }

        // 大模型提供商预设
        private static readonly Dictionary<string, (string Name, string Endpoint, string DefaultModel)> ProviderPresets = new()
        {
            ["openai"]     = ("OpenAI",             "https://api.openai.com/v1/chat/completions",         "gpt-4o"),
            ["deepseek"]   = ("DeepSeek",           "https://api.deepseek.com/v1/chat/completions",       "deepseek-chat"),
            ["siliconflow"]= ("硅基流动",           "https://api.siliconflow.cn/v1/chat/completions",     "Qwen/Qwen2.5-7B-Instruct"),
            ["aliyun"]     = ("阿里百炼",           "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen-plus"),
            ["zhipu"]      = ("智谱AI",             "https://open.bigmodel.cn/api/paas/v4/chat/completions", "glm-4-flash"),
            ["custom"]     = ("自定义接口",         "",                                                      ""),
        };

        public AIAssistantPage()
        {
            InitializeComponent();
            LoadSettings();

            // 延迟检查是否有待分析的启动错误
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_pendingLaunchError != null)
                {
                    var errorInfo = _pendingLaunchError;
                    _pendingLaunchError = null;
                    _ = AnalyzeLaunchErrorAsync(errorInfo);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai_config.json");

        private bool IsConfigured => !string.IsNullOrWhiteSpace(txtApiKey.Text);

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
                statusDot.ToolTip = "API 已配置";
                txtStatusLabel.Text = "已就绪";
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

        /// <summary>根据当前已填的 endpoint 匹配预设提供商</summary>
        private void AutoMatchProvider()
        {
            var currentEndpoint = txtApiEndpoint.Text?.Trim() ?? "";
            cmbProvider.SelectedIndex = cmbProvider.Items.Count - 1; // 默认"自定义"

            foreach (ComboBoxItem item in cmbProvider.Items)
            {
                if (item.Tag is string tag && ProviderPresets.TryGetValue(tag, out var p))
                {
                    if (!string.IsNullOrEmpty(p.Endpoint) &&
                        currentEndpoint.StartsWith(p.Endpoint.Split('/')[2])) // 按域名匹配
                    {
                        cmbProvider.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        /// <summary>提供商下拉框选择变更</summary>
        private void CmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbProvider.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (tag == "custom")
                {
                    // 自定义：清除字段让用户自行填写
                    return;
                }

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

        private async void SendMessage(string content)
        {
            if (_isSending || string.IsNullOrWhiteSpace(content)) return;

            _isSending = true;
            btnSend.IsEnabled = false;

            AddMessage("user", content);
            txtInput.Text = "";

            var loadingBorder = AddLoadingMessage();

            try
            {
                var endpoint = txtApiEndpoint.Text.Trim();
                var apiKey = txtApiKey.Text.Trim();
                var model = txtModelName.Text.Trim();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    RemoveLoadingMessage(loadingBorder);
                    AddMessage("assistant", "🔑 你还没有配置 API Key，请在弹出的设置面板中填写后保存。\n\n支持 OpenAI / DeepSeek / 硅基流动 等兼容接口。");
                    OpenSettings();
                    _isSending = false;
                    btnSend.IsEnabled = true;
                    return;
                }

                var messages = new List<AiChatMessage>
                {
                    new AiChatMessage { role = "system", content = SystemPrompt },
                    new AiChatMessage { role = "user", content = content }
                };

                var requestBody = new AiRequestBody
                {
                    model = model,
                    messages = messages,
                    temperature = 0.7,
                    max_tokens = 2048
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);
                var contentBytes = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.PostAsync(endpoint, contentBytes);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"API 请求失败 ({(int)response.StatusCode}): {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var aiResponse = JsonSerializer.Deserialize<AiResponse>(responseJson);

                var reply = aiResponse?.choices?[0]?.message?.content ?? "（未获取到回复）";

                RemoveLoadingMessage(loadingBorder);
                AddMessage("assistant", reply);
            }
            catch (Exception ex)
            {
                RemoveLoadingMessage(loadingBorder);
                AddMessage("assistant", $"❌ 出错了：{ex.Message}\n\n请检查 API 配置是否正确。");
            }
            finally
            {
                _isSending = false;
                btnSend.IsEnabled = true;
                txtInput.Focus();
            }
        }

        private void AddMessage(string role, string content)
        {
            var isUser = role == "user";

            // 头像颜色
            var avatarColor = isUser
                ? Color.FromRgb(33, 150, 243)
                : Color.FromRgb(46, 204, 113);

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
                bubble.Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(33, 150, 243), 0),
                        new GradientStop(Color.FromRgb(25, 118, 210), 1)
                    }
                };
            }
            else
            {
                bubble.Background = (Brush)FindResource("CardBackgroundBrush");
                bubble.BorderBrush = (Brush)FindResource("BorderBrush");
                bubble.BorderThickness = new Thickness(1);
            }

            var inner = new StackPanel();

            // 头像 + 名字
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var ava = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(avatarColor)
            };
            ava.Child = new TextBlock
            {
                Text = isUser ? "👤" : "🤖",
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
                    ? new SolidColorBrush(Color.FromRgb(179, 223, 255))
                    : new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontFamily = new FontFamily("Microsoft YaHei")
            };

            header.Children.Add(ava);
            header.Children.Add(name);

            // 内容文本
            var text = new TextBlock
            {
                Text = content,
                FontSize = 14,
                Foreground = isUser ? Brushes.White : (Brush)FindResource("TextPrimaryBrush"),
                FontFamily = new FontFamily("Microsoft YaHei"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            };

            inner.Children.Add(header);
            inner.Children.Add(text);
            bubble.Child = inner;

            spMessages.Children.Add(bubble);
            ScrollToBottom();
        }

        private Border AddLoadingMessage()
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

            // 头像 + 名字
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var ava = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113))
            };
            ava.Child = new TextBlock
            {
                Text = "🤖",
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var name = new TextBlock
            {
                Text = "小音符",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontFamily = new FontFamily("Microsoft YaHei")
            };

            header.Children.Add(ava);
            header.Children.Add(name);

            // 跳动点动画
            var dotsPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // 创建 3 个跳动点，带延迟动画
            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };

            var dot1 = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                Margin = new Thickness(0, 0, 5, 0)
            };
            var dot2 = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                Opacity = 0.4,
                Margin = new Thickness(0, 0, 5, 0)
            };
            var dot3 = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                Opacity = 0.4,
                Margin = new Thickness(0, 0, 5, 0)
            };

            dotsPanel.Children.Add(dot1);
            dotsPanel.Children.Add(dot2);
            dotsPanel.Children.Add(dot3);

            int step = 0;
            dispatcherTimer.Tick += (_, _) =>
            {
                step = (step + 1) % 3;
                dot1.Opacity = step == 0 ? 1.0 : 0.4;
                dot2.Opacity = step == 1 ? 1.0 : 0.4;
                dot3.Opacity = step == 2 ? 1.0 : 0.4;
            };
            dispatcherTimer.Start();

            // 将 timer 引用存储在 bubble 的 Tag 中以备停止
            bubble.Tag = dispatcherTimer;

            inner.Children.Add(header);
            inner.Children.Add(dotsPanel);
            bubble.Child = inner;

            spMessages.Children.Add(bubble);
            ScrollToBottom();

            return bubble;
        }

        private void RemoveLoadingMessage(Border border)
        {
            if (border.Tag is System.Windows.Threading.DispatcherTimer timer)
            {
                timer.Stop();
            }
            if (spMessages.Children.Contains(border))
            {
                spMessages.Children.Remove(border);
            }
        }

        private void ScrollToBottom()
        {
            Dispatcher.InvokeAsync(() =>
            {
                scrollMessages?.ScrollToBottom();
            });
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SendMessage(txtInput.Text);
        }

        private void TxtInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && !System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) && !System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
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

        private void CardConfigPrompt_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenSettings();
        }

        /// <summary>由启动弹窗触发：自动将错误信息填入输入框并发送分析请求</summary>
        private async Task AnalyzeLaunchErrorAsync(Core.GameLaunchInfo info)
        {
            // 先运行本地分析
            var logText = info.GetErrorSummary(200);
            var localResult = CrashAnalyzer.Analyze(logText, info.ExitCode, info.ErrorMessage);

            if (localResult.IsKnown)
            {
                // 本地诊断命中，展示结果，再补充 AI 分析
                var localReport = CrashAnalyzer.FormatResult(localResult);
                AddMessage("assistant", "🔍 本地诊断已完成，检测到已知问题模式：\n\n" + localReport);
                await Task.Delay(800);

                AddMessage("assistant", "🤖 正在使用 AI 进行深度分析，以获取更详细的解决方案…");
            }

            // 始终发送 AI 分析请求（在本地诊断基础上做深度分析）
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
            prompt.AppendLine("请分析可能的错误原因并给出解决方案。");

            // 将分析请求填入输入框（显示给用户看）
            txtInput.Text = "";
            // 自动发送分析请求
            SendMessage(prompt.ToString());
        }
    }

    public class AiConfig
    {
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
    }

    public class AiRequestBody
    {
        public string model { get; set; }
        public List<AiChatMessage> messages { get; set; }
        public double temperature { get; set; }
        public int max_tokens { get; set; }
    }

    public class AiChatMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class AiResponse
    {
        public List<AiChoice> choices { get; set; }
    }

    public class AiChoice
    {
        public AiChatMessage message { get; set; }
    }
}
