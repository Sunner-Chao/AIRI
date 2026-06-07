using System.Text.RegularExpressions;
using Alife.Basic;
using Alife.Framework;
using Alife.Implement;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Alife.Demo.Memory;

public class Program
{
    public static async Task Main(string[] args)
    {
        AlifeTerminal.Log("========================================", ConsoleColor.Cyan);
        AlifeTerminal.Log("   分层记忆系统 (Memory System) 验证 Demo", ConsoleColor.Cyan);
        AlifeTerminal.Log("   配置：阈值 8 条", ConsoleColor.Cyan);
        AlifeTerminal.Log("========================================", ConsoleColor.Cyan);

        // 1. 配置角色 (记忆助手)
        Character character = new Character {
            Name = "记忆助手",
            Prompt = "你是一个拥有长期记忆能力的助手。请尽量简洁地回答用户。",
            Plugins = new HashSet<string> {
                typeof(ChatService).FullName!,
                typeof(FunctionService).FullName!,
                typeof(MemoryService).FullName!,
            }
        };

        // 2. 初始化标准套件
        DemoSuite suite = await DemoSuite.InitializeAsync(character, system => {
            system.SetConfiguration(typeof(MemoryService), new MemoryConfig {
                Threshold = 8,
                BatchSize = 6,
                MaxCompressionLevel = 3,
                Probability = 1,
            },character.StorageKey);
        });

        // 4. 注入历史轴实时探测器 (探测分层记忆的变化)
        suite.ChatBot.ChatOver += () => {
            PrintHistoryStructure(suite.ChatBot.ChatHistory);
        };

        AlifeTerminal.LogInfo("提示：你可以连续输入多条短消息，观察上下文变长后触发的自动归档。");
        AlifeTerminal.LogInfo("输入 'exit' 退出。");
        AlifeTerminal.Log("--------------------------------------------------\n", ConsoleColor.Gray);

        // 5. 运行交互循环
        await suite.RunAsync();

        AlifeTerminal.Log("演示结束。", ConsoleColor.Cyan);
    }

    static void PrintHistoryStructure(ChatHistory history)
    {
        lock (AlifeTerminal.ConsoleLock)
        {
            Console.WriteLine("\n[探测器] 当前上下文物理结构监控:");
            Console.WriteLine("----------------------------------------------------------------------");
            for (int i = 0; i < history.Count; i++)
            {
                var msg = history[i];
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"{i:00} | ");

                if (msg.Role == AuthorRole.System)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write("[SYSTEM     ] ");
                }
                else if (msg.Content != null && msg.Content.StartsWith("[记忆存档"))
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write("[ARCHIVED   ] ");
                    Console.WriteLine(Regex.Match(msg.Content, @"\[记忆存档(.*)\]").Value);
                    continue;
                }
                else
                {
                    Console.ForegroundColor = msg.Role == AuthorRole.User ? ConsoleColor.White : ConsoleColor.Green;
                    Console.Write($"[{msg.Role.ToString().ToUpper(),-12}] ");
                }

                string preview = msg.Content?.Length > 60 ? msg.Content.Substring(0, 57).Replace("\n", " ") + "..." : msg.Content?.Replace("\n", " ") ?? "";
                Console.WriteLine(preview);
            }
            Console.WriteLine("----------------------------------------------------------------------\n");
            Console.ResetColor();
        }
    }
}
