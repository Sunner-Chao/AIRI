using Alife.Framework;
using Microsoft.SemanticKernel;

namespace Alife.Implement;

public class MessageFilterData
{
    public bool EnableTimestamp { get; set; } = true;
    public string MessageAppend { get; set; } = "（请简洁回复，禁用旁白、emoji）";
    public string PokeAppend { get; set; } = "";
}

[Plugin("消息过滤", "统一管理消息的提示词注入和格式化。负责添加时间戳、通用提示词以及系统消息头。", LaunchOrder = -100,
    EditorUI = typeof(MessageFilterServiceUI))]
public class MessageFilterService : InteractivePlugin<MessageFilterService>, IConfigurable<MessageFilterData>
{
    public MessageFilterData? Configuration { get; set; }

    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);
        ChatBot.ChatSend += OnChatSend;
        ChatBot.PokeSend += OnPokeSend;
    }

    string OnChatSend(string message)
    {
        string result = message;
        if (Configuration?.EnableTimestamp == true)
            result = $"[当前时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{result}";

        return $"{result}{Configuration?.MessageAppend}";
    }

    string OnPokeSend(string message)
    {
        return $"{message}{Configuration?.PokeAppend}";
    }
}