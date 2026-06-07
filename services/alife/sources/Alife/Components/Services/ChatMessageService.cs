using Alife.Framework;

namespace Alife.Components.Services;

public class ChatMessage
{
    public string? Content { get; set; }
    public string? Reasoning { get; set; }
    public bool IsUser { get; set; }
    public bool IsInputting { get; set; }
}
/// <summary>
/// UI层的聊天消息状态管理。在角色激活后立即挂接事件，确保后台对话也能被记录。
/// 采用名称索引以确保在活动重启（Character对象被Clone）时记录依然能够持久。
/// </summary>
public class ChatMessageService
{
    public event Action<string>? OnMessageChanged;
    public event Action<string>? OnUserMessageSent;

    public ChatBot? GetChatBot(string name)
    {
        return chatbotMap.GetValueOrDefault(name);
    }
    public List<ChatMessage> GetMessages(string name)
    {
        if (messagesMap.ContainsKey(name) == false)
            messagesMap.Add(name, new List<ChatMessage>());
        return messagesMap[name];
    }
    public void ClearMessages(string name)
    {
        if (messagesMap.TryGetValue(name, out List<ChatMessage>? list))
        {
            list.Clear();
        }
    }

    public string GetDraft(string name) => draftMap.GetValueOrDefault(name) ?? "";
    public void SetDraft(string name, string draft) => draftMap[name] = draft;

    readonly Dictionary<string, string> draftMap = new();
    readonly Dictionary<string, List<ChatMessage>> messagesMap = new();
    readonly Dictionary<string, ChatBot> chatbotMap = new();

    public ChatMessageService(ChatActivitySystem system)
    {
        system.Created += OnActivityCreated;
        system.Destroyed += OnActivityDestroyed;
    }
    void OnActivityDestroyed(ChatActivity activity)
    {
        string name = activity.Character.Name;
        chatbotMap.Remove(name);
    }

    /// <summary>
    /// 确保指定Activity的ChatBot事件已挂接到UI消息列表。
    /// 幂等操作，重复调用安全。
    /// </summary>
    void OnActivityCreated(ChatActivity activity)
    {
        string name = activity.Character.Name;
        List<ChatMessage> messages = GetMessages(name);
        chatbotMap.Add(name, activity.ChatBot);
        activity.ChatBot.ChatSent += message => {
            lock (messages)
            {
                messages.Add(new ChatMessage { Content = message, IsUser = true });
                messages.Add(new ChatMessage { IsUser = false, IsInputting = true });
            }

            OnMessageChanged?.Invoke(name);
            OnUserMessageSent?.Invoke(name);
        };
        activity.ChatBot.ChatReceived += (obj) => {
            ChatMessage? aiMessage = messages.LastOrDefault(m => m is { IsUser: false, IsInputting: true });
            if (aiMessage != null)
            {
                aiMessage.Content += obj;
                OnMessageChanged?.Invoke(name);
            }
        };
        activity.ChatBot.ReasoningReceived += (obj) => {
            ChatMessage? aiMessage = messages.LastOrDefault(m => m is { IsUser: false, IsInputting: true });
            if (aiMessage != null)
            {
                aiMessage.Reasoning += obj;
                OnMessageChanged?.Invoke(name);
            }
        };
        activity.ChatBot.ChatOver += () => {
            ChatMessage? aiMessage = messages.LastOrDefault(m => m is { IsUser: false, IsInputting: true });
            if (aiMessage != null)
            {
                aiMessage.IsInputting = false;
                OnMessageChanged?.Invoke(name);
            }
        };
    }
}
