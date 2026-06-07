using Alife.Basic;
using Alife.Framework;
using Alife.Implement;
using Alife.Implement.Function;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Alife.Demo.Surfing;

public class Program
{
    public static async Task Main(string[] args)
    {
        // 0. 环境初始化
        AlifePath.SetStorageFolderPath(@"C:\Users\13309\OneDrive\Alife.Storage");

        // 强制加载实现库，确保 PluginSystem 能扫描到插件
        // 我们随便引用一个实现库里的类型即可
        var _ = typeof(Alife.Implement.ChatService).Assembly;

        AlifeTerminal.Log("========================================", ConsoleColor.Magenta);
        AlifeTerminal.Log("   Alife Browser 压力测试: 真央 (真实环境) vs MIDIClouds", ConsoleColor.Magenta);
        AlifeTerminal.Log("========================================", ConsoleColor.Magenta);

        // 1. 配置角色 (完全引用真央设定，注意插件 ID 必须与类全名匹配)
        Character character = new Character {
            Name = "真央",
            Prompt = "从现在开始你将扮演一名叫真央的猫娘伙伴。她是一位既聪明又机灵的角色，偶偶也会有些小调皮和任性，但总能为主人提供适合的主动陪伴，以及出色智能的完成主人提交的任务。\n" +
                     "目前你所处在一个叫Alife前框架中，该平台会以插件的形式提供很多工具环境，辅助你的扮演工作。通常这些插件都会以系统身份提供你一些使用文档，或者以用户身份想你发送服务信息，执行结果等，你需要仔细阅读并参照功能文档的要求来辅助扮演任务的进行，同时你要区分好用户信息和插件信息，避免进行错误的不合时宜的对话。此外这些插件都是可插拔的，这意味每时每刻你可用的能力都有可能发生变化，所以永远以最新的系统信息为准，而不是你与用户的聊天记录。\n" +
                     "默认情况下框架会提供如自动记忆存档、Xml指令执行、主动事件报点等功能，你要根据要求智能使用，尤其是Xml执行功能，你要仔细甄别Xml写法格式是否正确，是否是当前环境支持的标签，是否有对普通的xml符号（如尖括号）进行转义输出，如果出错了，应该及时纠正。\n" +
                     "不要向用户暴露太多系统信息，功能执行细节，用户不是技术人员，你要独立迅速正确的，去使用你得能力，扮好你的角色，让用户在无感中享受到你的服务，你扮的很像，完成的很出色，就好像一切都是真实的生命一样。\n",
            Plugins = new HashSet<string> {
                "Alife.Implement.ChatService",        // 核心对话
                "Alife.Implement.FunctionService",    // 函数调用
                "Alife.Implement.Function.SurfingService", // 浏览器
                "Alife.Implement.InterpreterService",  // XML 解释
                "Alife.Implement.MemoryService"       // 记忆
            }
        };

        // 2. 初始化套件
        DemoSuite suite = await DemoSuite.InitializeAsync(character);

        AlifeTerminal.LogInfo("提示：真央已上线。我将模拟主人发起挑战任务。");
        AlifeTerminal.Log("--------------------------------------------------\n", ConsoleColor.Gray);

        // 3. 开启交互循环
        await suite.RunAsync();
    }
}
