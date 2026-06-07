using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Alife.Framework;

public sealed class AnthropicChatCompletionService : IChatCompletionService
{
    readonly Uri endpoint;
    readonly string modelId;
    readonly string apiKey;
    readonly HttpClient httpClient;

    public AnthropicChatCompletionService(string endpoint, string modelId, string apiKey, HttpClient httpClient)
    {
        this.endpoint = BuildMessagesEndpoint(endpoint);
        this.modelId = modelId;
        this.apiKey = apiKey;
        this.httpClient = httpClient;

        Attributes = new ConcurrentDictionary<string, object?> {
            ["ModelId"] = modelId,
            ["Endpoint"] = this.endpoint.ToString()
        };
    }

    public IReadOnlyDictionary<string, object?> Attributes { get; }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        string text = await GetNonStreamingTextAsync(chatHistory, cancellationToken);
        return new[] { new ChatMessageContent(AuthorRole.Assistant, text, modelId) };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string text = await GetNonStreamingTextAsync(chatHistory, cancellationToken);
        if (!string.IsNullOrEmpty(text))
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, text, modelId: modelId);
    }

    async Task<string> GetNonStreamingTextAsync(ChatHistory chatHistory, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(chatHistory, stream: false);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic messages request failed ({response.StatusCode}): {json}");

        return ExtractNonStreamingText(json) ?? string.Empty;
    }

    HttpRequestMessage CreateRequest(ChatHistory chatHistory, bool stream)
    {
        HttpRequestMessage request = new(HttpMethod.Post, endpoint);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = BuildPayload(chatHistory, stream);
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return request;
    }

    object BuildPayload(ChatHistory chatHistory, bool stream)
    {
        List<string> systemMessages = new();
        List<AnthropicMessage> messages = new();

        foreach (var message in chatHistory)
        {
            string content = message.Content ?? message.ToString();
            if (string.IsNullOrWhiteSpace(content))
                continue;

            if (message.Role == AuthorRole.System)
            {
                systemMessages.Add(content);
                continue;
            }

            string role = message.Role == AuthorRole.Assistant ? "assistant" : "user";
            AddMessage(messages, role, content);
        }

        if (messages.Count == 0)
            AddMessage(messages, "user", "Hello");

        var payload = new Dictionary<string, object?> {
            ["model"] = modelId,
            ["max_tokens"] = 8192,
            ["stream"] = stream,
            ["messages"] = messages
        };

        if (systemMessages.Count > 0)
            payload["system"] = string.Join("\n\n", systemMessages);

        return payload;
    }

    static void AddMessage(List<AnthropicMessage> messages, string role, string content)
    {
        if (messages.Count > 0 && messages[^1].Role == role)
        {
            messages[^1] = messages[^1] with { Content = messages[^1].Content + "\n\n" + content };
            return;
        }

        messages.Add(new AnthropicMessage(role, content));
    }

    static string? ExtractStreamingText(string data)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(data);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("delta", out JsonElement delta)
                && delta.TryGetProperty("text", out JsonElement deltaText))
                return deltaText.GetString();

            if (root.TryGetProperty("content_block", out JsonElement block)
                && block.TryGetProperty("text", out JsonElement blockText))
                return blockText.GetString();

            if (root.TryGetProperty("message", out JsonElement message)
                && message.TryGetProperty("content", out JsonElement content))
                return ExtractContentText(content);

            if (root.TryGetProperty("content", out JsonElement rootContent))
                return ExtractContentText(rootContent);
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    static string? ExtractNonStreamingText(string data)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(data);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("content", out JsonElement content))
                return ExtractContentText(content);
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    static string? ExtractContentText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString();

        if (content.ValueKind != JsonValueKind.Array)
            return null;

        StringBuilder builder = new();
        StringBuilder thinkingFallback = new();
        foreach (JsonElement item in content.EnumerateArray())
        {
            if (item.TryGetProperty("text", out JsonElement text))
                builder.Append(text.GetString());
            else if (item.TryGetProperty("thinking", out JsonElement thinking))
                thinkingFallback.Append(thinking.GetString());
        }

        if (builder.Length > 0)
            return builder.ToString();

        return thinkingFallback.Length == 0 ? null : thinkingFallback.ToString();
    }

    static Uri BuildMessagesEndpoint(string endpoint)
    {
        string url = endpoint.TrimEnd('/');

        if (url.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
            return new Uri(url);

        if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return new Uri($"{url}/messages");

        return new Uri($"{url}/v1/messages");
    }

    readonly record struct AnthropicMessage(string Role, string Content);
}
