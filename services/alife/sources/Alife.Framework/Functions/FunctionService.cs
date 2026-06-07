using Alife.Function.Interpreter;
using Microsoft.SemanticKernel;

namespace Alife.Framework;

[Plugin("函数调用", "为AI增加一种基于Xml的流式函数执行功能，实现快速实时的交互能力。", launchOrder: -1000)]
public class FunctionService : InteractivePlugin<FunctionService>
{
    public bool IsIdle => executor.IsInactive;

    public void RegisterHandler(XmlHandler handler, params string[] plainAreas)
    {
        handlerTable.Register(handler);
        this.plainAreas.AddRange(plainAreas);
    }

    public void UnregisterHandler(XmlHandler handler) => handlerTable.Unregister(handler);

    public void RegisterHandler(object handler, params string[] plainAreas)
    {
        handlerTable.Register(new XmlHandler(handler));
        this.plainAreas.AddRange(plainAreas);
    }

    public void UnregisterHandler(object handler) => handlerTable.Unregister(new XmlHandler(handler));

    readonly XmlHandlerTable handlerTable = new();
    readonly List<string> plainAreas = new();
    XmlStreamParser parser = null!;
    XmlStreamExecutor executor = null!;

    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);

        //创建xml解析执行器等
        parser = new XmlStreamParser(plainAreas.ToArray());
        executor = new XmlStreamExecutor(
        parser,
        handlerTable,
        ["，", "。", "！", "？", "......", "~", "…"],
        minBreakingLength: 9
        );
        parser.Error += OnError;
        executor.Error += OnError;

        chatActivity.ChatBot.ChatReceived += OnChatReceived;
        chatActivity.ChatBot.ChatSent += OnChatSent;

        Prompt($"""
                默认情况下你仅支持输出普通文本，但由于各种插件服务的存在，使得你还拥有通过输出特定的xml标签执行功能调用的能力。

                ## 可用函数
                如下函数收集于各种服务插件中，你需要配合描述以及相关服务本身的文档，按规则去使用他们。

                {handlerTable.Document()}

                ## 注意事项
                1. 由于xml的解释器的存在，【" | & | < | >】之类的xml符号都无法直接输出，你需要使用xml转义的方式【&quot; | &amp; | &lt; | &gt;】来输出尖括号。
                2. 仅支持上述提到的函数可以使用，其他任何xml标签都不支持。

                ## 使用示例
                当你的函数足够丰富后，你可以尝试用如下的方式使用他们，这是官方最佳示例。
                ```text
                这段文字主人看不到，我可以借此自言自语。 <!-- 内容可以不被标签包裹，这样相当于只给自己看，或有意不输出可见内容 -->
                <speak> <!-- 如果要对外输出，需要使用函数调用，比如这里用语音输出 -->
                主人你看我~
                <motion /> <!-- 标签可以嵌套，比如这样就能实现边说话边做动作 -->
                可以一边跳舞，一边说话噢。
                另外我还可以通过‘左尖括号python右尖括号’执行脚本呢！ <!-- 通过用代词描述‘左尖括号、右尖括号’来避免输出xml符号 -->
                </speak>
                <python> <!-- 因为python执行需要时间，在结尾调用比较合适。 -->
                print('Hello World!')
                <python>
                ```   
                """);
    }

    public override async Task DestroyAsync()
    {
        await executor.WaitToInactive();
        await executor.DisposeAsync();

        await base.DestroyAsync();
    }

    async void OnChatSent(string _)
    {
        try
        {
            await ChatBot.RequestChatAsync();
            try
            {
                executor.Flush();
                await executor.WaitToInactive(ChatBot.ChatBreakToken);
            }
            catch (OperationCanceledException)
            {
                await executor.CancelAsync();
            }
            finally
            {
                ChatBot.ReleaseChat();
            }
        }
        catch (OperationCanceledException) {}
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    void OnChatReceived(string obj)
    {
        executor.Feed(obj);
    }

    void OnError(string tag, Exception exception)
    {
        Poke($"执行{tag}标签出错：{exception.Message}");
    }
}
