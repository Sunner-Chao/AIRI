using System.IO;
using System.Text;
using System.Windows;

namespace Alife.Function.DeskPet;

public partial class App
{
    PetActivity activity = null!;

    protected override async void OnStartup(StartupEventArgs startupEvent)
    {
        try
        {
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            File.Create("pet.log").Close();

            base.OnStartup(startupEvent);

            PetModelMetadata metadata = PetModelMetadata.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot/model/Mao/Mao.model3.json"));
            MainWindow mainWindow = await Alife.Function.DeskPet.MainWindow.Create();
            PetProcess petProcess = new(Console.Out, Console.In); //与客户端的通讯
            PetBridge bridge = new(mainWindow.WebView, metadata); //与前端的通讯

            MainWindow = mainWindow;
            activity = new(petProcess, bridge, metadata, mainWindow);
        }
        catch (Exception e)
        {
            await File.AppendAllTextAsync("pet.log", e + Environment.NewLine);
        }
    }
}
