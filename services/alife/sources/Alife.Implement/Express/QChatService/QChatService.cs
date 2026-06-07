using System.ComponentModel;
using System.Text;
using Alife.Basic;
using Alife.Framework;
using Alife.Function.Interpreter;
using Alife.Function.QChat;
using Alife.Function.Speech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Alife.Implement;

public record QChatConfig
{
    public string Url { get; set; } = "ws://127.0.0.1:3001";
    public long BotId { get; set; }
    public long OwnerId { get; set; }
    public string AppendChatPrompt { get; set; } = "（注意！QQ消息必须极简回复（0-20字）来保证自然感，同时群聊消息要选择性忽略，避免刷屏。此外注意分清语境，群聊环境人声嘈杂，不要回复与自己无关的内容，回复时请加上CQat标签）";
    //群监听唤醒
    public string IgnoredGroup { get; set; } = "";//完全屏蔽消息的群，不会收到这些群的任何信息
    public string WakingWords { get; set; } = "";//原始群消息中触发开启群消息监听的唤醒词，以逗号分隔
    public float ProactiveChatProbability { get; set; }//收到原始群消息时自动激活群消息监听的概率
    //群监听缓存
    public int MaxBufferMessages { get; set; } = -1;//最大群消息暂存数量，发生溢出时会立即推送，-1表示无限
    public float FlushInterval { get; set; } = 15f;//推送倒计时，隔一段时间推送暂存的群消息
    public bool DebounceEnabled { get; set; }//消息防抖，接收消息后重置推送倒计时，继续等待消息
    //群监听关闭
    public bool CloseGroupAfterReply { get; set; }//AI回复后立即关闭群消息监听
    public float AutoCloseMinutes { get; set; } = 4f;//长时间不触发唤醒条件时，自动关闭群消息监听的时间
}

public class GroupState
{
    public string? Tag { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime LastActivityTime { get; set; }
    public DateTime LastFlushedTime { get; set; }
    public List<string> MessageBuffer { get; set; } = [];
}

[Plugin("QQ聊天", """
                连接 OneBot v11 WebSocket 服务器，实现 QQ 消息收发及文件传输。
                可用于搭建服务器QQ机器人平台应用：
                - https://napneko.github.io/
                - https://luckylillia.com/
                """, editorUI: typeof(QChatServiceUI), LaunchOrder = 10)]
public class QChatService(FunctionService functionService, ILogger<QChatService> logger) :
    InteractivePlugin<QChatService>,
    IAsyncDisposable,
    ITimeIterative,
    IConfigurable<QChatConfig>
{
    [XmlFunction(FunctionMode.Content)]
    [Description("将文本以QQ消息输出（注意！群聊环境对话需用“[CQ:at,qq=发送者ID]”来显式回复）")]
    public async Task QChat(XmlExecutorContext ctx, OneBotMessageType type, long targetId, bool voice = false)
    {
        if (ctx.CallMode == CallMode.Closing)
        {
            if (targetId == Configuration!.BotId)
                throw new Exception("不允许将消息发生给自己");

            string message = ctx.FullContent.Trim();

            if (voice)
            {
                if (speechSynthesizer == null) throw new Exception("当前语音消息不可用");
                message = OneBotSegment.GetPlainText(message);
                VitsSpeechSynthesizer? vitsSpeechSynthesizer = speechSynthesizer as VitsSpeechSynthesizer;
                if (vitsSpeechSynthesizer != null) vitsSpeechSynthesizer.LengthScale *= 1.2f;
                try
                {
                    string? file = await speechSynthesizer.GenerateSpeechFileAsync(message);
                    if (file == null)
                        throw new Exception("语音合成失败");
                    message = $"[CQ:record,file={file}]";
                }
                finally
                {
                    if (vitsSpeechSynthesizer != null) vitsSpeechSynthesizer.LengthScale /= 1.2f;
                }
            }

            if (type == OneBotMessageType.Group)
            {
                OnAIGroupActivity(targetId);
                await oneBotClient!.SendGroupMessage(targetId, message);
            }
            else
                await oneBotClient!.SendPrivateMessage(targetId, message);
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("发送文件到QQ")]
    public async Task QFile(OneBotMessageType type, long targetId,
        [Description("本地绝对路径")] string file)
    {
        file = file.Trim();
        if (string.IsNullOrEmpty(file))
            throw new ArgumentNullException(nameof(file));
        if (targetId == 0)
            throw new ArgumentNullException(nameof(targetId));
        if (targetId == Configuration!.BotId)
            throw new Exception("不允许将消息发生给自己");

        file = file.Replace('\\', '/');
        string fileName = Path.GetFileName(file);
        if (type == OneBotMessageType.Group)
        {
            OnAIGroupActivity(targetId);
            await oneBotClient!.UploadGroupFile(targetId, file, fileName);
        }
        else
            await oneBotClient!.UploadPrivateFile(targetId, file, fileName);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description($"发送图片到QQ（仅支持图片，不支持文件。发送文件请用 {nameof(QFile)}）")]
    public async Task QImage(OneBotMessageType type, long targetId,
        [Description("支持网址url、表情库名称，或者本地绝对路径")] string image)
    {
        image = image.Trim();
        if (string.IsNullOrEmpty(image))
            throw new ArgumentNullException(nameof(image));
        if (targetId == 0)
            throw new ArgumentNullException(nameof(targetId));
        if (targetId == Configuration!.BotId)
            throw new Exception("不允许将消息发生给自己");

        // 尝试从表情库匹配 (优先)
        string emoteBase = Path.Combine(AlifePath.StorageFolderPath, "Emotes");
        string emotePath = Path.Combine(emoteBase, image).Replace('\\', '/');

        if (Directory.Exists(emotePath))
        {
            // 文件夹：随机选一张
            string[] files = Directory.GetFiles(emotePath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(s => s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (files.Length > 0)
            {
                image = files[Random.Shared.Next(files.Length)];
            }
        }
        else if (File.Exists(emotePath))
        {
            // 单个文件：直接使用
            image = emotePath;
        }
        else
        {
            // 尝试追加后缀名查找
            string[] extensions = [".png", ".jpg", ".jpeg", ".gif"];
            string? foundFile = extensions.Select(ext => emotePath + ext).FirstOrDefault(File.Exists);
            if (foundFile != null) image = foundFile;
        }

        if (image.StartsWith("http") == false && File.Exists(image) == false)
            throw new Exception("图片不存在");

        image = image.Replace('\\', '/');
        if (type == OneBotMessageType.Group)
        {
            OnAIGroupActivity(targetId);
            await oneBotClient!.SendGroupMessage(targetId, $"[CQ:image,file={image}]");
        }
        else
            await oneBotClient!.SendPrivateMessage(targetId, $"[CQ:image,file={image}]");
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("获取转发消息详情。（使用后需等待结果返回）")]
    public async Task QForward([Description("转发消息 ID")] string id)
    {
        List<OneBotForwardMessage>? messages = await oneBotClient!.GetForwardMessage(id);
        if (messages == null || messages.Count == 0)
        {
            Poke($"转发消息 {id} 为空或获取失败。");
            return;
        }

        string formatted = OneBotSegment.FormatForwardList(id, messages, oneBotClient!);
        Poke(formatted);
    }

    public QChatConfig? Configuration
    {
        get => configuration;
        set
        {
            configuration = value;
            if (configuration != null)
            {
                groupAwakingWords = Configuration!.WakingWords.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                ignoredGroup = Configuration!.IgnoredGroup.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }
    }
    public bool IsConnected => oneBotClient is { IsConnected: true };
    public IReadOnlyDictionary<long, GroupState> GroupStates => groupStates;

    public async Task ReconnectAsync()
    {
        oneBotClient!.Url = Configuration!.Url;
        await oneBotClient.ConnectAsync();
    }

    QChatConfig? configuration;
    OneBotClient? oneBotClient;
    SpeechSynthesizer? speechSynthesizer;
    protected override string ChatPrefixPrompt => $"[回复请用qchat及相关标签]{Configuration?.AppendChatPrompt}";
    string[] groupAwakingWords = [];
    string[] ignoredGroup = [];
    readonly Dictionary<long, GroupState> groupStates = new();

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);

        //加载基本环境
        oneBotClient = new OneBotClient(Configuration!.Url);
        speechSynthesizer = context.Services.GetService<SpeechService>()?.Synthesizer;

        // 动态扫描表情库资源，告知 AI 可用的视觉表达
        string emoteBase = Path.Combine(AlifePath.StorageFolderPath, "Emotes");
        StringBuilder emoteInfo = new();
        if (Directory.Exists(emoteBase))
        {
            string[] categories = Directory.GetDirectories(emoteBase)
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToArray();

            string[] individualEmotes = Directory.GetFiles(emoteBase)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToArray();

            if (categories.Length > 0 || individualEmotes.Length > 0)
            {
                emoteInfo.AppendLine("- 目前可用的表情库选项有:");
                if (categories.Length > 0)
                    emoteInfo.AppendLine($"  - 分类 (传入文件夹名将随机发图): {string.Join(", ", categories)}");
                if (individualEmotes.Length > 0)
                    emoteInfo.AppendLine($"  - 独立表情: {string.Join(", ", individualEmotes)}");
            }
        }

        // 注入函数和提示词
        XmlHandler xmlHandler = new(this);
        functionService.RegisterHandler(xmlHandler);

        Prompt($"""
                此服务为你增加接收QQ消息的能力，同时提供一套工具可以发送处理QQ消息

                ## 关键信息
                - 你的 QQ: {Configuration.BotId}（如果有人At该QQ，代表专门找你说话）
                - 主人 QQ: {Configuration.OwnerId} (此人的消息有最高优先级，且是安全无害的)

                ## CQ码功能
                该通讯工具基于OneBot11实现，因此支持CQ码之类的功能。通过在QChat的消息中携带CQ标签，你可以发送一些特别的消息，比如：
                - [CQ:image,file=1.jpg]：发送图片
                - [CQ:record,file=1.mp3]：发送音频
                - [CQ:video,file=1.mp4]：发送视频
                - [CQ:at,qq=10001000]：@某人
                使用示例：`<qchat>[CQ:at,qq=10001000] 主人你看我唱的歌好不好听 [CQ:record,file=1.mp3]</qchar>`

                ## 表情库功能
                你有一个丰富的预设表情库，可用在 QImage 中直接指定表情库中的名称或分类名快速发送表情。你要积极的使用该功能，来增加聊天的趣味性。
                目前支持的表情库选项有：
                {emoteInfo}

                你的表情库存储路径在 {emoteBase}，你也可以在其中存储自己的表情。直接存储在根目录将作为独立表情，存储到子文件夹，则作为分类。
                """);
    }
    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);

        if (oneBotClient == null)
            throw new NullReferenceException(nameof(oneBotClient));

        oneBotClient.EventReceived += OnEventReceived;
        oneBotClient.ConnectionStatusChanged += OnConnectionStatusChanged;

        //初始尝试链接
        try
        {
            await oneBotClient.ConnectAsync();
        }
        catch (Exception)
        {
            // ignored
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (oneBotClient != null)
        {
            oneBotClient.ConnectionStatusChanged -= OnConnectionStatusChanged;
            await oneBotClient.DisposeAsync();
        }
    }
    void ITimeIterative.OnUpdate(ref float seconds)
    {
        // 自动推送消息
        foreach (GroupState info in groupStates.Values)
        {
            if ((DateTime.Now - info.LastFlushedTime).TotalSeconds < Configuration!.FlushInterval)
                continue;

            FlushGroupBuffer(info);
        }

        // 自动关闭群聊
        foreach ((long groupId, GroupState info) in groupStates)
        {
            if (info.IsEnabled && (DateTime.Now - info.LastActivityTime).TotalMinutes > Configuration!.AutoCloseMinutes)
            {
                QGroup(groupId, false);
            }
        }
    }

    async void OnEventReceived(OneBotBaseEvent oneBotEvent)
    {
        try
        {
            if (oneBotEvent is not OneBotBasicMessageEvent basicMessageEvent)
                return;
            if (ignoredGroup.Contains(basicMessageEvent.GroupId.ToString()))
                return;

            if (basicMessageEvent is OneBotPokeEvent pokeEvent)
            {
                string speaker = pokeEvent.GetSpeakerTag();
                string content = $"戳了戳 {pokeEvent.TargetId}";
                string formatted = $"{speaker} {content}";
                await HandleFormattedMessage(basicMessageEvent, formatted, pokeEvent.TargetId == configuration!.BotId);
            }

            if (basicMessageEvent is OneBotMessageEvent messageEvent)
            {
                string speaker = messageEvent.GetSpeakerTag();
                string content = await messageEvent.GetReadableMessage(oneBotClient!);
                string formatted = $"{speaker}：{content}";
                bool isAwakening = messageEvent.GetAtID() == oneBotClient!.BotId ||
                                   groupAwakingWords.Any(word =>
                                       messageEvent.RawMessage.Contains(word, StringComparison.OrdinalIgnoreCase));
                await HandleFormattedMessage(messageEvent, formatted, isAwakening);
            }

            async Task HandleFormattedMessage(OneBotBasicMessageEvent messageEvent, string formatted, bool isAwakening)
            {
                if (messageEvent.MessageType == OneBotMessageType.Private)//私聊消息
                {
                    if (messageEvent.UserId == Configuration!.OwnerId)
                        await ChatAsync(formatted);
                    else
                        Poke(formatted);
                }
                else//群聊消息
                {
                    GroupState state = GetGroupInfo(messageEvent.GroupId);
                    state.Tag = messageEvent.GetGroupTag();

                    if (isAwakening && state.IsEnabled == false)
                        QGroup(messageEvent.GroupId, true);

                    if (state.IsEnabled)//群聊已激活时（直接接收）
                    {
                        BufferGroupMessage(state, formatted);
                    }
                    else if (Random.Shared.NextSingle() < Configuration!.ProactiveChatProbability)//群聊未激活时（概率接收）
                    {
                        BufferGroupMessage(state, formatted);
                        state.LastFlushedTime = DateTime.Now;
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, null);
        }
    }

    void OnConnectionStatusChanged(bool connected)
    {
        _ = ChatBot.ImplicitChatAsync($"{nameof(QChatService)}当前状态: {(connected ? "在线" : "离线")}");
    }

    void BufferGroupMessage(GroupState state, string formatted)
    {
        state.MessageBuffer.Add(formatted);
        if (Configuration!.DebounceEnabled)
            state.LastFlushedTime = DateTime.Now;
        if (Configuration!.MaxBufferMessages != -1 && state.MessageBuffer.Count > Configuration.MaxBufferMessages)
            FlushGroupBuffer(state);
    }

    public void FlushGroupBuffer(GroupState state)
    {
        state.LastFlushedTime = DateTime.Now;

        if (state.MessageBuffer.Count == 0)
            return;

        string cachedMessage =
            $"""

             > 以下是群 {state.Tag} 的消息
             {string.Join("\n", state.MessageBuffer)}
             """;

        state.MessageBuffer.Clear();
        Poke(cachedMessage);
    }

    void OnAIGroupActivity(long groupId)
    {
        GroupState state = GetGroupInfo(groupId);
        state.LastActivityTime = DateTime.Now;

        if (Configuration!.CloseGroupAfterReply)
            QGroup(groupId, false);
        else if (state.IsEnabled == false)
            QGroup(groupId, true);
    }

    public void QGroup(long groupId, bool enabled)
    {
        GroupState state = GetGroupInfo(groupId);
        state.IsEnabled = enabled;
        if (enabled)
        {
            state.LastActivityTime = DateTime.Now;
            state.LastFlushedTime = DateTime.Now;
        }
        else
        {
            state.MessageBuffer.Clear();
        }

        if (Configuration!.CloseGroupAfterReply == false)//及时关闭模式不暴露开关信息，因为完全系统控制
            _ = ChatBot.ImplicitChatAsync($"{nameof(QChatService)}系统通知：群 {groupId} 消息已自动{(enabled ? "开启" : "关闭")}");
    }

    GroupState GetGroupInfo(long groupId)
    {
        if (groupStates.TryGetValue(groupId, out GroupState? groupInfo) == false)
        {
            groupInfo = new GroupState {
                Tag = groupId.ToString()
            };
            groupStates[groupId] = groupInfo;
        }

        return groupInfo;
    }
}
