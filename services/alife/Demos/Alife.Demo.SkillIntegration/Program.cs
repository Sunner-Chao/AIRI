using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alife.Basic;
using Alife.Framework;
using Alife.Implement;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        AlifeTerminal.Log("========================================", ConsoleColor.Cyan);
        AlifeTerminal.Log("   Alife Skill 集成测试 Demo", ConsoleColor.Cyan);
        AlifeTerminal.Log("========================================", ConsoleColor.Cyan);

        // 0. 强制加载程序集以确保插件被扫描
        _ = typeof(Alife.Implement.ChatService).Assembly;
        _ = typeof(Alife.Implement.SkillService).Assembly;

        // 1. 初始化系统环境
        AlifeTerminal.LogInfo("正在初始化系统环境...");
        AlifePath.SetStorageFolderPath(@"C:\Users\13309\OneDrive\Alife.Storage");
        StorageSystem storage = new();
        ConfigurationSystem config = new(storage);
        PluginSystem plugins = new(storage);

        // 2. 准备角色
        Character character = new()
        {
            Name = "小助手",
            Prompt = "你是一个全能的电脑操作助手。你拥有许多 'skill'（技能），这些技能存放在指定的文件夹中。\n" +
                     "当你遇到无法直接完成的任务时，你应该先检查是否有相关的 skill 可以使用。\n" +
                     "你可以使用 <SkillService ReadSkill=\"skillName\" /> 来阅读技能手册。\n" +
                     "阅读手册后，请严格按照手册中的 Python 示例代码或指导来完成任务。",
            Plugins = 
            { 
                "Alife.Implement.ChatService", 
                "Alife.Implement.SkillService", 
                "Alife.Implement.PythonService", 
                "Alife.Implement.FunctionService" 
            }
        };

        // 3. 创建 ChatActivity 并注入插件
        AlifeTerminal.LogInfo("正在创建 ChatActivity 并注入插件...");
        ChatActivity activity = await ChatActivity.Create(character, config, plugins, null, [config, storage]);
        await activity.Start(); // 必须调用 Start 才能激活插件

        AlifeTerminal.LogInfo($"[插件加载完毕]: {string.Join(", ", activity.Plugins.Select(p => p.GetType().Name))}");

        // 订阅聊天事件以查看输出
        activity.ChatBot.ChatSent += (msg) => AlifeTerminal.Log($"> USER: {msg}", ConsoleColor.Green);
        activity.ChatBot.ChatReceived += (msg) => Console.Write(msg);
        activity.ChatBot.ChatOver += () => Console.WriteLine();
        activity.ChatBot.ChatHistoryAdd += (msg) => 
        {
            if (msg.Role == AuthorRole.Tool)
                AlifeTerminal.Log($"[TOOL] {msg.Content}", ConsoleColor.DarkGray);
        };

        // 检查是否有对话模型
        if (!activity.Plugins.Any(p => p is ChatService))
        {
            AlifeTerminal.LogError("警告: ChatService 未加载！请检查插件配置。");
        }

        // 4. 运行交互
        if (args.Length > 0)
        {
            string input = string.Join(" ", args);
            AlifeTerminal.LogInfo($"执行单次聊天: {input}");
            await activity.ChatBot.ChatAsync(input);
            
            // 等待工具执行和后续回复 (Poke 循环)
            AlifeTerminal.LogInfo("正在等待后续工具执行与回复...");
            int waitTime = 0;
            while (waitTime < 30) // 最多等30秒
            {
                await Task.Delay(2000); // Poke 有 2s 延迟
                if (!activity.ChatBot.IsChatting)
                {
                    // 再次检查确认没有新的 Poke 产生
                    await Task.Delay(1000);
                    if (!activity.ChatBot.IsChatting) break;
                }
                waitTime += 3;
            }
            AlifeTerminal.LogInfo("测试执行完毕。");
        }
        else
        {
            AlifeTerminal.LogHint("测试环境构建完成！你可以尝试以下指令：");
            AlifeTerminal.Log("- '帮我看看电脑现在的运行状态'");
            AlifeTerminal.Log("- '帮我整理一下桌面上的文件'");
            AlifeTerminal.Log("- '帮我把这张图片变成黑白的: C:\\Users\\13309\\Desktop\\test.jpg'");

            while (true)
            {
                Console.Write("\nUSER > ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                await activity.ChatBot.ChatAsync(input);
            }
        }

        await activity.DisposeAsync();
    }
}
