using Alife.Basic;
using Alife.Framework;
using Alife.Implement;

AlifeTerminal.Log("========================================", ConsoleColor.Magenta);
AlifeTerminal.Log("   真央 MCP 工具集成验证 Demo", ConsoleColor.Magenta);
AlifeTerminal.Log("========================================", ConsoleColor.Magenta);

// 1. 配置角色
var character = new Character {
    Name = "真央",
    Prompt = "你是一个桌宠，名叫真央。你非常活泼，喜欢用猫娘语说话（每句话带喵）。\n" +
             "你拥有 Model Context Protocol (MCP) 能力，可以通过各种外部工具来执行任务。\n" +
             "你可以通过 XML 标签来调用这些工具。系统会自动为你列出可用的工具标签。\n" +
             "请在对话中根据需要主动使用这些工具喵！",
    Plugins = new HashSet<string> {
        typeof(ChatService).FullName!,
        typeof(FunctionService).FullName!,
        typeof(McpService).FullName!,
    }
};

// 2. 初始化套件
var suite = await DemoSuite.InitializeAsync(character, config => {
    var mcpConfig = new McpPluginConfig();
    
    // 使用 npx 运行一个全能测试服务器
    mcpConfig.Servers.Add(new McpServerConfig {
        Name = "EnhancedBing",
        Description = "一个基于 MCP (Model Context Protocol) 的中文必应搜索工具。",
        Command = "npx",
        Arguments = ["-y", "bing-cn-mcp-enhanced"]
    });
    
    config.SetConfiguration(typeof(McpService), mcpConfig);
});

AlifeTerminal.LogInfo("提示：已自动配置 'EverythingServer'。");
AlifeTerminal.LogInfo("你可以试着问：'请列出你的 MCP 工具' 或者 '使用 echo 工具对我说 hello'。");
AlifeTerminal.Log("--------------------------------------------------\n", ConsoleColor.Gray);

// 3. 开启文字输入循环
await suite.RunAsync();

AlifeTerminal.Log("演示结束，再见喵！", ConsoleColor.Magenta);
