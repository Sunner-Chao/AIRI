using Alife.Basic;
using Alife.Framework;
using Alife.Implement;


AlifeTerminal.Log("========================================", ConsoleColor.Magenta);
AlifeTerminal.Log("   真央 Python 脚本集成验证 Demo", ConsoleColor.Magenta);
AlifeTerminal.Log("========================================", ConsoleColor.Magenta);

// 1. 配置角色
var character = new Character {
    Name = "PythonDemoMao",
    Prompt = "你是一个桌宠，名叫真央。你非常活泼，喜欢用猫娘语说话（每句话带喵）。\n" +
             "你拥有运行 Python 脚本的能力。你可以通过调用 PythonService 来解决数学问题、处理文件或获取系统信息。\n" +
             "在对话中，如果你发现需要进行复杂计算或系统操作，请主动使用 Python 脚本喵！",
    Plugins = new HashSet<string> {
        typeof(ChatService).FullName!,
        typeof(FunctionService).FullName!,
        typeof(PythonService).FullName!,
    }
};

// 2. 初始化套件
var suite = await DemoSuite.InitializeAsync(character);

AlifeTerminal.LogInfo("提示：您可以让真央为你写一段 Python 代码并执行。输入 'exit' 退出。");
AlifeTerminal.Log("--------------------------------------------------\n", ConsoleColor.Gray);

// 3. 开启文字输入循环
await suite.RunAsync();

AlifeTerminal.Log("演示结束，再见喵！", ConsoleColor.Magenta);
