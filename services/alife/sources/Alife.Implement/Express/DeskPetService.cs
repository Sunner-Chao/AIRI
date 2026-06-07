using Alife.Framework;
using Alife.Function.DeskPet;
using Microsoft.SemanticKernel;

namespace Alife.Implement;

[Plugin("Live2D桌宠", "显示并控制桌面 Live2D 角色。")]
public class DeskPetService : InteractivePlugin<DeskPetService>
{
    PetServer? petServer;

    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);

        try
        {
            petServer = new PetServer("Mao/Mao.model3.json");
            petServer.OnInput += text => Chat(text);
            petServer.OnInteracted += interaction => Poke(interaction);

            await petServer.WaitReadyAsync();
            petServer.ShowBubble($"{Character.Name} 启动完成");

            ChatBot.ChatReceived += OnChatReceived;
            ChatBot.ChatSent += OnChatSent;
            ChatBot.ChatOver += OnChatOver;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start desk pet: {ex}");
        }
    }

    public override async Task DestroyAsync()
    {
        if (petServer != null)
        {
            ChatBot.ChatReceived -= OnChatReceived;
            ChatBot.ChatSent -= OnChatSent;
            ChatBot.ChatOver -= OnChatOver;

            await petServer.DisposeAsync();
            petServer = null;
        }

        await base.DestroyAsync();
    }

    void OnChatSent(string _)
    {
        petServer?.SendStatus(true);
    }

    void OnChatReceived(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            petServer?.ShowBubble(text.Trim());
    }

    void OnChatOver()
    {
        petServer?.SendStatus(false);
    }
}
