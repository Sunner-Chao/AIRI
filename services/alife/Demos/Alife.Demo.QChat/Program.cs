using Alife.Basic;
using Alife.Framework;
using Alife.Implement;
using Alife.Function.QChat;
using System;
using System.Collections.Generic;

namespace Alife.Demo.QChat;

public class Program
{
    public static async Task Main(string[] args)
    {
        AlifeTerminal.Log("========================================", ConsoleColor.Magenta);
        AlifeTerminal.Log("   Alife OneBot AI Plugin 集成验证 Demo", ConsoleColor.Magenta);
        AlifeTerminal.Log("========================================", ConsoleColor.Magenta);

        // 1. 配置角色 (真央)
        Character character = new Character {
            Name = "真央",
            Prompt = "你是一个集成在 QQ 中的 AI 助手，名叫真央。你非常活泼，喜欢用猫娘语说话（每句话带喵）。\n" +
                     "你正在通过 OneBot 协议与用户交流。如果你想发送消息，请使用 <QChat /> 标签；如果你想发送图片或文件，请使用 <QSendFile file=\"url或路径\" /> 标签。\n" +
                     "在群聊中，如果你想回复特定的人，在消息开头使用 OneBot 的 [CQ:at,qq=发送者ID] 即可。\n" +
                     "示例：\n" +
                     "1. 发送文字：<QChat target=\"123456\" type=\"group\">[CQ:at,qq=789] 你好喵！我也在看这个喵~</QChat>\n" +
                     "2. 发送图片：<QSendFile file=\"url或路径\" />",
            Plugins = new HashSet<string> {
                typeof(ChatService).FullName!,
                typeof(FunctionService).FullName!,
                typeof(QChatService).FullName!,
            }
        };

        // 2. 初始化套件
        DemoSuite suite = await DemoSuite.InitializeAsync(character, system => {
            system.SetConfiguration(typeof(QChatService), new QChatConfig {
                Url = "ws://127.0.0.1:3001",
                OwnerId = 1330958515L,
            });
        });

        AlifeTerminal.LogInfo("提示：OneBot 插件已加载。您可以直接在此输入消息模拟 QQ 互动。");
        AlifeTerminal.Log("--------------------------------------------------\n", ConsoleColor.Gray);

        // 4. 开启交互循环
        await suite.RunAsync();

        AlifeTerminal.Log("演示结束，再见喵！", ConsoleColor.Magenta);
    }
}
