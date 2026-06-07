using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System.Net;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Alife.Framework;

public class ChatServiceConfig
{
    public string protocol = "OpenAI";
    public string endpoint = "https://api.deepseek.com/v1";
    public string modelId = "deepseek-v4-flash";
    public string apiKey = "";
    public bool thinkingEnabled = true;
    public string reasoningEffort = "high";
}

[Plugin(
"对话能力", "基于OpenAI协议的对话模型功能接入。",
url: "https://www.deepseek.com/",
editorUI: typeof(ChatServiceUI)
)]
public class ChatService : Plugin, IConfigurable<ChatServiceConfig>, IProvideExecutionSettings
{
    public ChatServiceConfig? Configuration { get; set; }

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);

        // 强制使用 HTTP 1.1 以解决某些提供者（如 DeepSeek）在流式传输时可能出现的 HttpIOException
        SocketsHttpHandler handler = new SocketsHttpHandler {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions {
                RemoteCertificateValidationCallback = delegate { return true; }
            },
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        // 使用通用处理器拦截并破解所有 OpenAI 兼容协议的思考过程字段
        OpenAICompatibleHandler reasoningHandler = new(handler);

        HttpClient httpClient = new(reasoningHandler) {
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        if (UseAnthropicProtocol(Configuration!))
        {
            context.KernelBuilder.Services.AddSingleton<IChatCompletionService>(
                new AnthropicChatCompletionService(Configuration!.endpoint, Configuration!.modelId, Configuration!.apiKey, httpClient));
            return;
        }

        context.KernelBuilder.AddOpenAIChatCompletion(
        endpoint: new Uri(Configuration!.endpoint),
        modelId: Configuration!.modelId,
        apiKey: Configuration!.apiKey,
        httpClient: httpClient
        );
    }

    static bool UseAnthropicProtocol(ChatServiceConfig configuration)
    {
        if (string.Equals(configuration.protocol, "Anthropic", StringComparison.OrdinalIgnoreCase))
            return true;

        return configuration.endpoint.Contains("/anthropic", StringComparison.OrdinalIgnoreCase);
    }

    [Experimental("SKEXP0010")]
    public void ProvideSettings(OpenAIPromptExecutionSettings settings)
    {
        if (Configuration!.thinkingEnabled)
            settings.ReasoningEffort = Configuration!.reasoningEffort;

        // 思考模式支持
        settings.ExtraBody = new Dictionary<string, object?>();
        settings.ExtraBody["thinking"] = new {
            type = Configuration!.thinkingEnabled ? "enabled" : "disabled"
        };
    }
}
