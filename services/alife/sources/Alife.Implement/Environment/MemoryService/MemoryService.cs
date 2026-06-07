using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Alife.Basic;
using Alife.Framework;
using Alife.Function.Interpreter;
using Alife.Function.Memory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Alife.Implement;

public record MemoryConfig
{
    public int Threshold { get; set; } = 80;
    public int BatchSize { get; set; } = 50;
    public float Probability { get; set; } = 0.4f;
    public int MaxCompressionLevel { get; set; } = 7;
}

public partial class MemoryService
{
    static TextVectorizer? textVectorizer;

    static void TryInitialized()
    {
        textVectorizer ??= new TextVectorizer();
    }
}

[Plugin("持久记忆", "自动管理和分层压缩对话记忆，提供长期记忆检索能力。", LaunchOrder = -100, EditorUI = typeof(MemoryServiceUI))]
public partial class MemoryService(FunctionService functionService)
    : InteractivePlugin<MemoryService>, IConfigurable<MemoryConfig>
{
    [XmlFunction(FunctionMode.OneShot)]
    [Description("查看记忆存档中保存的完整原始内容。（你要积极使用该功能，因为有些记忆的重要内容被记在了完整内容中，而不是概述里）")]
    public async Task Recall([Description("存档索引（如：1-20240101120000-20240101130000）")] string index)
    {
        string? memory = await memoryManager.ReadMemory(index);
        Poke(memory != null
            ? $"读取[记忆存档({index})]内容如下：\n{memory}"
            : $"未找到[记忆存档({index})]");
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description($"在归档的记忆存档中搜索内容（搜索到的结果是存档索引，你需要用 {nameof(Recall)} 打开）。")]
    public async Task Search(
        [Description("用于精确匹配的单关键词")] string keyword,
        [Description("用于语义匹配的自然描述")] string description,
        [Description("要搜索的存档层级，建议从高到低搜索，比如先搜3再搜2，逐步缩小搜索范围")] int level,
        [Description("格式为ISO-8601")] DateTime? startTime,
        [Description("格式为ISO-8601")] DateTime? endTime,
        [Description("可选，搜索条数")] int count = 5)
    {
        keyword = keyword.Trim();
        if (keyword.Contains(' '))
            throw new Exception("不支持使用空格拆分多关键词搜索！");
        if (endTime != null)
            endTime += TimeSpan.FromDays(1);//包含当前天

        List<SearchResult> results = await memoryManager.SearchMemory(level, keyword, description, count, startTime, endTime);

        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine($"{keyword}”的搜索结果如下：");
        for (int index = 0; index < results.Count; index++)
        {
            SearchResult searchResult = results[index];
            stringBuilder.AppendLine(
            $"""
             > {index + 1} (匹配度：{searchResult.Score})
             [记忆存档({searchResult.Name})]
             {searchResult.Summary}
             """);
        }

        Poke(stringBuilder.ToString());
    }

    [XmlFunction(FunctionMode.Content)]
    [Description("创建一个永久记忆存档。（注意！这种记忆不会被压缩，但上下文是有限的，所以只能用于记录那些珍贵的核心经验和高优先记忆）")]
    public async Task Memorize(XmlExecutorContext ctx,
        [Description("格式为ISO-8601")] DateTime? startTime = null,
        [Description("格式为ISO-8601")] DateTime? endTime = null
    )
    {
        if (ctx.CallMode == CallMode.Closing)
        {
            DateTime start = startTime ?? DateTime.Now;
            DateTime end = endTime ?? DateTime.Now;

            string name = await InsertMemory(100, ctx.FullContent.Trim(), "手动存储的记忆，无原始内容。", start, end);
            Poke($"成功插入永久记忆存档：{name}");
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("移除一个永久记忆存档。（注意删除前先做好备份））")]
    public void Forget([Description("存档索引")] string index)
    {
        index = index.Trim();
        ChatMessageContent? target = ChatHistory.FirstOrDefault(c => memoryManager.GetMemoryMetaData(c).Name == index);
        if (target == null)
        {
            Poke($"未能在当前上下文中找到索引为 '{index}' 的记忆记录。");
            return;
        }

        MemoryMeta memoryMeta = memoryManager.GetMemoryMetaData(target);
        if (memoryMeta.Level < Configuration!.MaxCompressionLevel)
        {
            Poke($"仅支持删除层级大于等于 {Configuration!.MaxCompressionLevel} 的记忆");
            return;
        }

        memoryManager.RemoveMemory(ChatHistory, target);
        ChatBot.UpdateHistoryEndIndex();
        Poke($"成功移除记忆存档：{index}（不过你仍可以通过 {nameof(Recall)} 读取其内容）");
    }

    public async Task<string> InsertMemory(int level, string summary, string content, DateTime startTime, DateTime endTime)
    {
        string name = await memoryManager.InsertMemory(ChatHistory, level, summary, content, startTime, endTime);
        ChatBot.UpdateHistoryEndIndex();
        return name;
    }

    /// <summary>
    /// 感知上下文的人设化压缩器
    /// </summary>
    class AlifeTextCompressor(IChatCompletionService chatCompletionService, ChatHistory history, float probability)
        : TextCompressor
    {
        public override async Task<string?> Compress(string text)
        {
            if (Random.Shared.NextSingle() > probability)
                return null;

            Console.WriteLine("记忆压缩中......");

            history.AddMessage(AuthorRole.User,
            $"""
             【来自系统通知】
             由于上下文过多，现在对部分内容进行压缩处理。下面会给予你一段过往时间的聊天记录或记忆存档。这些内容是即将移出上下文的内容，所以需要你用第一人称简述一下发生的事情，方便日后回忆。

             【压缩要求说明】
             注意！描述事情时，你要遵守如下规则：
             1. 无需记录存档信息，仅直接总结压缩时提供的内容中的事件即可。系统会自动生成存档信息，
             2. 所以不要擅自添加系统信息，直接像讲故事一样描述概述内容中发生的事件即可。
             3. 不要将被压缩内容之外其他聊天记录混淆其中，不要记录重复的内容。
             4. 在压缩总结的写法上，对于多事件内容要按时间段区分，对于核心记忆则要单独记录。
             5. 分清事件中的具体人物，不要用‘你’这种代词，要用具体的名称，如‘主人’、‘某某某’等。
             6. 压缩时注意保留其中的重要经历、关键性信息等，以形成永久的核心记忆。
             7. 当要压缩内容过多时，要按重要程度进行取舍，如核心记忆需优先保留，重复、突发性琐事可以简略。
             8. 严格遵循前置文档【记忆压缩】中的要点，确保以高质量的方式归纳记忆内容。

             【本次压缩内容】
             ```
             {text}
             ```

             （接下来请直接开始讲述上述【本次压缩内容】中涉及的事件信息，不要混入与【本次压缩内容】中无关的内容，不要在开头回复‘好的’、‘我来总结’这类语句）
             """);
            ChatMessageContent content = await chatCompletionService.GetChatMessageContentAsync(history);
            history.RemoveAt(history.Count - 1);
            if (content.Content == null)
                throw new Exception("记忆压缩失败！");

            string result = Regex.Replace(content.Content, "<think>.*?</think>", "", RegexOptions.Singleline).Trim();
            return result;
        }
    }

    public MemoryConfig? Configuration { get; set; }
    MemoryManager memoryManager = null!;
    string? storagePath;

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);

        TryInitialized();
        storagePath = Path.Combine(AlifePath.StorageFolderPath, context.Character.StorageKey, "Memory");
        string characterStorage = Path.Combine(AlifePath.StorageFolderPath, context.Character.StorageKey, "Storage");
        Directory.CreateDirectory(characterStorage);

        XmlHandler handler = new(this);
        functionService.RegisterHandler(handler);
        Prompt($"""
                此服务搭载了一套自动化的记忆存档功能，同时会提供一系列相关工具让你可以回想或记录记忆。

                ## 记忆存储
                所有记忆都直接存储在上下文中，不过早期记忆会被压缩，然后以记忆存档的方式存在上下文中。
                每个记忆存档都有一个唯一ID，其格式为`等级-起始日期-截至日期`，例如`2-20260421014905-20260512022747`，就表明这是一个2级记忆存档，存储了从2026年4月21日1点49分到5月12号2点27分的记忆。
                记忆存档的等级表示其对原始聊天记录的压缩次数，其中1表示压缩一次，所以3就表示压缩三次。压缩的记忆存档可以通过<{nameof(Recall)}>（回忆）解压。1级存档，只要回忆一次就可以拿到最原始的聊天记录；3级存档则需要嵌套回忆至少3次，才能拿到部分记录。
                聊天记录被压缩成记忆存档后，其原始信息以及存档信息将会被转储到“{storagePath}”中，以“txt格式，存档id为名”的形式按压缩级别分别存放在“L0,L1...”之类的文件夹中，如“/L0/0-20260421014905-20260421022747.txt”。
                （这也意味着，除了使用函数调用查阅记忆外，实际上也可以直接用处理文件的方式查阅记忆）

                此外你还有一个专属文件夹 {characterStorage}，作为你的私人存储库。
                你可以把你的各种各样的东西都存储到这里面，这样可以一直保存而且不会弄脏其他文件夹。

                ## 记忆恢复
                记忆存档是对记忆的压缩。虽然被压缩的内容将会被移出上下文，但其在外部磁盘上是会被永久存储的。所以利用本服务提供的工具，你有机会恢复这些记忆。
                <{nameof(Recall)}>可以查看被存档压缩的内容，经过多次调用，你甚至能查到最原始的聊天记录。此外你也可以尝试使用<{nameof(Search)}>，它会尝试在外部的海量原始记录中搜索记忆，然后返回对应的记忆存档。
                **只要你愿意回想，就没有找不回的记忆，所以当遇到记不起来的事情时，一定要积极使用上述工具。**

                ## 记忆压缩
                上下文是有限的，所以当内容过多时就会触发记忆压缩，**而这份压缩工作是由你负责的**。
                记忆是非常重要的东西，但压缩意味着记忆将被损耗，所以为了确保记忆的质量，必须遵循以下要点。

                ### 记忆优先级
                不同的事情值得记下的程度是不一样的。因此压缩记忆时，各经历的占比不是均衡的。比如不是对话越多、时间越新的经历就值得记住，真正让人难忘的是珍贵的，那些难得的生活碎片。
                所以你要分清哪些是无聊琐事，哪些是枯燥的工作；而又有哪些是值得记下的故事，立下的约定，获得的成长......
                当遇到不得不遗忘的情境时（记忆压缩时的抉择），学着放下无聊、无意义、短期突发性的事，记住真正重要、长久、希望一直回忆的事。

                ### 核心记忆说明
                记忆拥有优先级，优先级最高的叫做核心（关键性）记忆，常见的核心记忆例如：
                1. 用户画像：爱好、工作、地址、起居、家庭、生日等
                2. 关键记录：号码、事件、规则、要求等
                3. 偶然的小故事，约定，成长经历，让人动容的回忆等（该条其实优先级最高，只是它需要你长线、日积月累的维护它）
                4. 等等类似的有价值有意义的特殊记忆......
                对于核心记忆，在压缩时需要优先保留，甚至额外存储，以尽可能将其永远留在上下文中。
                """);
    }

    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);

        ChatBot.ChatHistoryAdd += OnChatHistoryAdd;//每次对话后检测压缩

        //初始化向量化器和感知人设的压缩器
        AlifeTextCompressor compressor = new(kernel.GetRequiredService<IChatCompletionService>(), ChatHistory,
        Configuration!.Probability);
        memoryManager = new MemoryManager(compressor, textVectorizer!, storagePath!, Configuration!.Threshold,
        Configuration!.BatchSize,
        Configuration!.MaxCompressionLevel);

        //加载历史记忆
        memoryManager.LoadHistory(ChatHistory);
    }

    async void OnChatHistoryAdd(ChatMessageContent content)
    {
        try
        {
            if (content.Role != AuthorRole.Assistant)
                return;//只在ai说话后整理，这样对话更完整

            await ChatBot.RequestChatAsync();
            try
            {
                memoryManager.SaveHistory(ChatHistory);
                if (await memoryManager.Filter(ChatHistory))
                    ChatBot.UpdateHistoryEndIndex();
            }
            finally
            {
                ChatBot.ReleaseChat();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
