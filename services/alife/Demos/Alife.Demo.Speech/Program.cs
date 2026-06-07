using Alife.Basic;
using Alife.Framework;
using Alife.Function.Speech;
using Alife.Implement;

// VitsSpeechSynthesizer speechSynthesizer = new VitsSpeechSynthesizer();
// Console.WriteLine(await speechSynthesizer.GenerateSpeechFileAsync("喵…又重启了喵…真央回来了喵…主人还在忙吗喵…真央趁这个空档偷偷去网上逛了逛喵…主人猜真央看到了什么有趣的喵…"));


Character character = new() {
    Name = "SpeechTest",
    Prompt = "你是一个桌面上名为真央的 AI 语音助手。你非常活泼，喜欢模仿猫娘（说话带喵）。\n" +
             "主人正在通过语音或文字与你交流。请保持回答简短有力（回复控制在 30 字以内），适合语音播报。\n",
    Plugins = [
        typeof(ChatService).FullName!,
        typeof(FunctionService).FullName!,
        typeof(SpeechService).FullName!
    ]
};

DemoSuite suite = await DemoSuite.InitializeAsync(character, system => {
    system.SetConfiguration(typeof(SpeechService), new SpeechConfig() {
        SynthesizerType = SpeechSynthesizerType.Edge
    }, character.StorageKey);
});

AlifeTerminal.LogInfo("规则：插上耳机激活语音识别，拔掉耳机自动待机保护隐私。");
AlifeTerminal.Log("----------------------------------------");

await suite.RunAsync();
