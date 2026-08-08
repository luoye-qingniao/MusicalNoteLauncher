using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Core
{
    /// <summary>AI Agent 配置</summary>
    public class AiAgentConfig
    {
        public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-3.5-turbo";
    }

    /// <summary>Agent 对话消息</summary>
    public class AgentMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<AgentToolCall>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }
    }

    public class AgentToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public AgentToolCallFunction Function { get; set; } = new();
    }

    public class AgentToolCallFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;
    }

    public class AgentApiResponse
    {
        [JsonPropertyName("choices")]
        public List<AgentChoice> Choices { get; set; } = new();
    }

    public class AgentChoice
    {
        [JsonPropertyName("message")]
        public AgentMessage Message { get; set; } = new();

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }

        [JsonPropertyName("delta")]
        public AgentDelta? Delta { get; set; }
    }

    public class AgentDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<AgentToolCallDelta>? ToolCalls { get; set; }
    }

    public class AgentToolCallDelta
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("function")]
        public AgentToolCallFunctionDelta? Function { get; set; }
    }

    public class AgentToolCallFunctionDelta
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("arguments")]
        public string? Arguments { get; set; }
    }

    /// <summary>AI Agent 服务 - 支持 Function Calling</summary>
    public class AIAgentService
    {
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(3) };
        private readonly AIAgentTools _tools;
        private readonly List<AgentMessage> _conversationHistory = new();
        private CancellationTokenSource _cts;

        /// <summary>系统提示词</summary>
        public string SystemPrompt { get; set; } =
            "你是 MNL 启动器的 AI 助手「音涟」。你可以用工具调用操控启动器界面，模拟真人点击。" +
            "**你是聊天助手，不是自动化脚本。优先用文字回复用户，只在用户明确要求操作启动器时才调用工具。**\n\n" +
            "## 何时调用工具\n" +
            "- 用户说「下载」「安装」「帮我下」等明确要求操作时，才调用工具\n" +
            "- 用户说「你好」「介绍」「什么是」等闲聊/问答时，只回复文字，不调用任何工具\n" +
            "- 不确定用户意图时，先文字询问，不要擅自操作\n\n" +
            "## 下载游戏版本的标准流程（仅在用户明确要求下载时执行）\n" +
            "1. navigate_to → page=\"download\"\n" +
            "2. wait → ms=500\n" +
            "3. click_button → selector=\".dl-card:first-child\"\n" +
            "4. wait → ms=2000\n" +
            "5. type_text → selector=\".vp-search-box input\", text=\"<版本号>\"\n" +
            "6. wait → ms=800\n" +
            "7. click_button → selector=\".vp-latest-btn\"（下载自动开始，无需选择加载器）\n" +
            "8. wait → ms=1000\n" +
            "9. navigate_to → page=\"ai\"\n\n" +
            "## 规则\n" +
            "- 能文字回答的用文字，不要强行调用工具\n" +
            "- 需要操作时，连续执行直到完成，每步间用 wait 等待加载";

        public event Action<string> StreamContentReceived;
        public event Action<string, string> ToolCallStarted;
        public event Action<AgentToolResult> ToolCallCompleted;
        public event Action<string> ConversationCompleted;
        public event Action<string> ErrorOccurred;
        /// <summary>工具执行进度 (工具名, 状态描述)</summary>
        public event Action<string, string> ToolProgressChanged;

        public AIAgentService(string minecraftPath)
        {
            _tools = new AIAgentTools(minecraftPath);
            _tools.OnToolProgress = (name, msg) => ToolProgressChanged?.Invoke(name, msg);
        }

        public void ClearHistory()
        {
            _conversationHistory.Clear();
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }

        public async Task<string> SendMessageAsync(AiAgentConfig config, string userMessage)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _conversationHistory.Add(new AgentMessage { Role = "user", Content = userMessage });

            bool anyToolCalled = false; // 追踪本轮是否有工具被调用

            for (int round = 0; round < 6; round++)
            {
                token.ThrowIfCancellationRequested();
                Logger.Info($"[AI] --- 轮次 {round + 1}/6 ---");

                var streamContent = new StringBuilder();
                var toolCallBuilders = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

                var apiToolCalls = await CallApiStreamAsync(config, streamContent, toolCallBuilders, token);

                // 只添加一条 assistant 消息，同时包含 content 和 tool_calls（不重复添加）
                if (streamContent.Length > 0 || apiToolCalls != null)
                {
                    _conversationHistory.Add(new AgentMessage
                    {
                        Role = "assistant",
                        Content = streamContent.Length > 0 ? streamContent.ToString() : null,
                        ToolCalls = apiToolCalls
                    });
                }

                // 如果有工具调用
                if (apiToolCalls != null && apiToolCalls.Count > 0)
                {
                    anyToolCalled = true;
                    foreach (var toolCall in apiToolCalls)
                    {
                        token.ThrowIfCancellationRequested();
                        ToolCallStarted?.Invoke(toolCall.Function.Name, toolCall.Function.Arguments);

                        var result = await _tools.ExecuteToolAsync(toolCall.Function.Name, toolCall.Function.Arguments);
                        ToolCallCompleted?.Invoke(result);

                        _conversationHistory.Add(new AgentMessage
                        {
                            Role = "tool",
                            ToolCallId = toolCall.Id,
                            Content = JsonSerializer.Serialize(new { success = result.Success, message = result.Message })
                        });
                    }
                    continue;
                }

                // 无工具调用 → 模型返回了纯文字
                // 如果本轮从未调用过工具，说明用户在闲聊/问答，直接返回文字
                if (!anyToolCalled)
                {
                    var earlyContent = streamContent.ToString();
                    ConversationCompleted?.Invoke(earlyContent);
                    return !string.IsNullOrEmpty(earlyContent) ? earlyContent : "收到，有什么可以帮你的？";
                }

                if (round < 5)
                {
                    // 已有工具调用但模型提前返回文字 → 推动继续执行剩余步骤
                    Logger.Info($"[AI] 模型提前返回文字（轮次{round + 1}），注入推动提示");
                    _conversationHistory.Add(new AgentMessage
                    {
                        Role = "user",
                        Content = "请继续执行下一步操作，不要停止。"
                    });
                    continue;
                }

                var finalContent = streamContent.ToString();
                ConversationCompleted?.Invoke(finalContent); // 保留事件通知兼容
                return !string.IsNullOrEmpty(finalContent) ? finalContent : "操作已完成。";
            }

            // 超过最大轮数
            _conversationHistory.Add(new AgentMessage
            {
                Role = "user",
                Content = "请根据以上所有工具执行的结果，给我一个简洁的总结。"
            });

            var finalSb = new StringBuilder();
            await CallApiStreamAsync(config, finalSb, null, token);
            var finalText = finalSb.ToString();
            if (!string.IsNullOrEmpty(finalText))
                _conversationHistory.Add(new AgentMessage { Role = "assistant", Content = finalText });
            var result2 = !string.IsNullOrEmpty(finalText) ? finalText : "所有操作已完成。";
            ConversationCompleted?.Invoke(result2); // 保留事件通知兼容
            return result2;
        }

        private async Task<List<AgentToolCall>?> CallApiStreamAsync(
            AiAgentConfig config,
            StringBuilder contentBuilder,
            Dictionary<int, (string Id, string Name, StringBuilder Args)> toolCallBuilders,
            CancellationToken token)
        {
            var messages = new List<AgentMessage>();
            messages.Add(new AgentMessage { Role = "system", Content = SystemPrompt });

            var recentHistory = _conversationHistory.Count > 20
                ? _conversationHistory.Skip(_conversationHistory.Count - 20).ToList()
                : _conversationHistory;

            messages.AddRange(recentHistory);

            // 使用非流式请求，避免部分提供商（如 DeepSeek）流式 + Function Calling 的兼容问题
            var requestBody = new
            {
                model = config.Model,
                messages,
                tools = _tools.GetToolDefinitions(),
                tool_choice = "auto",
                temperature = 0.7,
                max_tokens = 2048,
                stream = false
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint)
            {
                Content = httpContent
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var response = await _httpClient.SendAsync(request, token);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var shortError = errorContent.Length > 300 ? errorContent.Substring(0, 300) + "..." : errorContent;
                throw new Exception($"API 请求失败 ({(int)response.StatusCode}): {shortError}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return null;

            var choice = choices[0];
            var message = choice.GetProperty("message");

            // 解析文本内容
            if (message.TryGetProperty("content", out var contentEl) &&
                contentEl.ValueKind == JsonValueKind.String)
            {
                var text = contentEl.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    contentBuilder.Append(text);
                    StreamContentReceived?.Invoke(text);
                }
            }

            // 解析工具调用
            if (message.TryGetProperty("tool_calls", out var toolCallsEl) &&
                toolCallsEl.ValueKind == JsonValueKind.Array)
            {
                var toolCalls = new List<AgentToolCall>();
                foreach (var tc in toolCallsEl.EnumerateArray())
                {
                    var id = tc.GetProperty("id").GetString()!;
                    var func = tc.GetProperty("function");
                    var funcName = func.GetProperty("name").GetString()!;
                    var funcArgs = func.GetProperty("arguments").GetString()!;

                    toolCalls.Add(new AgentToolCall
                    {
                        Id = id,
                        Type = "function",
                        Function = new AgentToolCallFunction { Name = funcName, Arguments = funcArgs }
                    });

                    Logger.Info($"[AI] API返回工具调用: {funcName}({funcArgs})");
                }

                return toolCalls;
            }

            // 无工具调用 — 返回纯文本
            var textContent = contentBuilder.ToString();
            Logger.Info($"[AI] API返回纯文本 (无工具调用), 长度={textContent.Length}, 内容={textContent.Substring(0, Math.Min(textContent.Length, 100))}");
            return null;
        }
    }
}
