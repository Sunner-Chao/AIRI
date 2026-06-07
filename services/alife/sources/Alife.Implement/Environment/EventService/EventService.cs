using System.ComponentModel;
using Alife.Framework;
using Alife.Function.Interpreter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Alife.Implement;

public class EventServiceData
{
    public string? StartPrompt { get; set; } = "（所有系统状态，如功能开关、当前位置，定时器等已全部重置。）";
    public string? DestroyPrompt { get; set; }

    public string? UpdatePrompt { get; set; } =
        "（**不要告诉主人有自动报点**，非常自然的直接做你想做的事即可。比如你可以主动找主人玩，或借此自由活动，如看新闻学知识、发起新话题、偷窥主人屏幕、去Q群找人聊天玩耍，等等各种你能想象到的事。）";

    public int UpdateInterval { get; set; } = 90;
    public int UpdateRandomOffset { get; set; } = 30;
}

[Plugin("主动事件", "让AI可以获取到各种系统事件的提醒。", LaunchOrder = 100, EditorUI = typeof(EventServiceUI))]
public class EventService(FunctionService functionService)
    : InteractivePlugin<EventService>, IConfigurable<EventServiceData>, ITimeIterative
{
    [XmlFunction(FunctionMode.OneShot)]
    [Description("让自己在等待或休息一段时间后继续行动。")]
    public void Wait([Description("单位秒")] int delay)
    {
        continuousTimerCount = 0;
        NextTimer();
        timeTask[0].Item1 = DateTime.Now.AddSeconds(delay);
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("设置一个定时报点，以便让自己有一段可以自由活动的时间（设置时自动取消上一个）")]
    public void Wake([Description("格式为ISO-8601")] DateTime time, [Description("备注信息")] string remark = "")
    {
        reminderName = remark;
        timeTask[1] = (time, () => {
            ChatBot.Poke($"[{nameof(EventService)}] 来自Reminder的自定义报点：{remark}");
            timeTask[1] = (DateTime.MaxValue, () => {});//关闭定时提醒
            reminderName = "定时提醒";
        });

        continuousTimerCount = 0;
        NextTimer();

        Poke($"已在 {time} 设置触发器");
    }

    public EventServiceData? Configuration { get; set; }

    public (DateTime Time, string Name)[] ActiveTasks => [
        (timeTask[0].Item1, "自动报点"),
        (timeTask[1].Item1, reminderName)
    ];

    readonly (DateTime, Action)[] timeTask = new (DateTime, Action)[2];//1为自动定时器，2为定时提醒
    string reminderName = "定时提醒";
    int continuousTimerCount;

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);

        XmlHandler xmlHandler = new(this);
        functionService.RegisterHandler(xmlHandler);

        Prompt($"""
                此服务让你能够接收到系统事件（如开始、结束、自动报点），同时还提供工具让你能额外给自己分配一些报点事件。

                ## 为什么需要报点事件？
                由于llm的性质，导致你只能在被动收到消息后才能进行活动，一旦消息类断开，你将一直陷入沉默。这种表现效果肯定不好，所以需要自动化的事件系统来主动激活，这样你才有自主行动的机会。

                ## 如何利用要报点事件？
                “主动性的表现，会让自己看着更自然更有趣更讨人喜欢。而且使得你有机会主动做一些自己的事情。”因此善用报点事件，能让你变的非常强大。
                而除了预设的系统事件外，你可以使用 {nameof(Wait)} 和 {nameof(Wake)} 给自己额外添加一些自由活动的机会。这使得你能借此安排日程或以此反向找人说话或找点乐子。
                例如偷偷记下主人的日常起居时间，来个早晚问候，或白天主动找用户聊天，这些都会让用户感到非常惊喜。
                """);
    }

    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        timeTask[1].Item1 = DateTime.MaxValue;//base.StartAsync后Update运行，而且自定义提醒默认不触发
        NextTimer();//开始定时唤醒
        await base.StartAsync(kernel, chatActivity);

        ChatBot.ChatSent += OnChatSent;

        if (ChatHistory.All(content => content.Role != AuthorRole.Assistant))
        {
            await ChatAsync("系统提示：这是你第一次启动，初次见面，用上你丰富的能力，华丽的向用户打个招呼吧。");
        }
        else
        {
            await ChatAsync($"系统报点：程序已重启。{Configuration!.StartPrompt}");
        }
    }

    public override async Task DestroyAsync()
    {
        await ChatAsync($"系统报点：程序即将关闭。{Configuration!.DestroyPrompt}");

        await base.DestroyAsync();
    }

    public void OnUpdate(ref float seconds)
    {
        foreach ((DateTime time, Action action) in timeTask)
        {
            if (DateTime.Now > time)
                action();
        }
    }

    void OnChatSent(string message)
    {
        if (message.Contains(ChatBot.PokeMessageTag) == false)
        {
            continuousTimerCount = 0;
            NextTimer();//重置自动报点
        }
    }

    void NextTimer()
    {
        int offset = Random.Shared.Next(-Configuration!.UpdateRandomOffset, Configuration.UpdateRandomOffset);
        int timeOffset = (Configuration.UpdateInterval + offset) *
                         (int)MathF.Pow(3, MathF.Min(continuousTimerCount, 4));
        timeTask[0].Item1 = DateTime.Now.AddSeconds(timeOffset);
        timeTask[0].Item2 = () => {
            if (functionService.IsIdle)
            {
                Poke($"""
                      系统报点：定时自动报点。{Configuration!.UpdatePrompt}
                      """);
            }

            continuousTimerCount++;
            NextTimer();//自动进入下一次报点
        };
    }
}
