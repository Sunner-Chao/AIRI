using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Alife.Function.QChat;

/// <summary>
/// 基础的 OneBot v11 客户端，提供多态事件分发。
/// </summary>
public class OneBotClient(string url) : IAsyncDisposable
{
    /// <summary>
    /// 事件接收回调。
    /// </summary>
    public event Action<OneBotBaseEvent>? EventReceived;

    /// <summary>
    /// 连接状态改变回调。
    /// </summary>
    public event Action<bool>? ConnectionStatusChanged;

    /// <summary>
    /// 当前 Bot 的 QQ 号。
    /// </summary>
    public long BotId { get; private set; }

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected => ws.State == WebSocketState.Open;

    /// <summary>
    /// WebSocket 地址。
    /// </summary>
    public string Url
    {
        get => url;
        set => url = value;
    }


    public async Task ConnectAsync()
    {
        try
        {
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
        }
        catch (Exception)
        {
            // ignored
        }

        if (loopCancellation != null)
            await loopCancellation.CancelAsync();
        ws.Dispose();

        ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(url), CancellationToken.None);

        // 同步握手：预期第一个报文是 connect 事件
        using CancellationTokenSource cts = new(5000);
        OneBotBaseEvent? ev = await ReceiveEventAsync(cts.Token);

        if (ev is OneBotMetaEvent { MetaEventType: OneBotMetaType.Lifecycle, SubType: "connect" })
        {
            BotId = ev.SelfId;
            loopCancellation = new CancellationTokenSource();
            ReceiveLoop(loopCancellation.Token);
            ConnectionStatusChanged?.Invoke(true);
        }
        else
        {
            throw new ProtocolViolationException("[OneBotClient] 握手失败：无法识别首个报文。");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (ws.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
        ws.Dispose();
    }


    public async Task SendActionAsync(string action, object? @params = null, string? echo = null)
    {
        OneBotAction payload = new() { Action = action, Params = @params, Echo = echo };
        string json = JsonSerializer.Serialize(payload);
        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task<T?> CallActionAsync<T>(string action, object? @params = null)
    {
        string echo = Guid.NewGuid().ToString();
        TaskCompletionSource<JsonElement> tcs = new();
        pendingActions[echo] = tcs;

        await SendActionAsync(action, @params, echo);

        using CancellationTokenSource timeout = new(10000);
        await using CancellationTokenRegistration ctRegistration = timeout.Token.Register(() => tcs.TrySetCanceled());

        JsonElement result = await tcs.Task;
        OneBotResponse<T>? response = result.Deserialize<OneBotResponse<T>>();
        return response != null ? response.Data : default;
    }


    string url = url;
    readonly byte[] buffer = new byte[1024 * 64];
    readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> pendingActions = new();
    ClientWebSocket ws = new();
    CancellationTokenSource? loopCancellation;

    async void ReceiveLoop(CancellationToken ct = default)
    {
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                OneBotBaseEvent? ev = await ReceiveEventAsync(ct);
                if (ev != null) EventReceived?.Invoke(ev);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OneBotClient] 链路异常: {ex.Message}");
        }
        finally
        {
            ConnectionStatusChanged?.Invoke(false);
        }
    }

    async Task<OneBotBaseEvent?> ReceiveEventAsync(CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        string json = Encoding.UTF8.GetString(ms.ToArray());

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);

            // 如果包含 echo，说明是 API 的响应
            if (doc.RootElement.TryGetProperty("echo", out JsonElement echoElem))
            {
                string echo = echoElem.GetString() ?? "";
                if (pendingActions.TryRemove(echo, out var tcs))
                {
                    tcs.TrySetResult(doc.RootElement.Clone());
                }

                return null;
            }

            string? type = doc.RootElement.TryGetProperty("post_type", out JsonElement typeElem) ? typeElem.GetString() : "";
            string? subType = doc.RootElement.TryGetProperty("sub_type", out JsonElement subtypeElem) ? subtypeElem.GetString() : "";
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            switch (type)
            {
                case "message":
                    return doc.RootElement.Deserialize<OneBotMessageEvent>(options);
                case "message_sent":
                    return doc.RootElement.Deserialize<OneBotMessageSentEvent>(options);
                case "meta_event":
                    return doc.RootElement.Deserialize<OneBotMetaEvent>(options);
                case "notice":
                {
                    switch (subType)
                    {
                        case "poke":
                            return doc.RootElement.Deserialize<OneBotPokeEvent>(options);
                        default:
                            return doc.RootElement.Deserialize<OneBotNoticeEvent>(options);
                    }
                }
                case "request":
                    return doc.RootElement.Deserialize<OneBotRequestEvent>(options);
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OneBotClient] 报文解析异常: {ex.Message}\nRaw: {json}");
            return null;
        }
    }
}
