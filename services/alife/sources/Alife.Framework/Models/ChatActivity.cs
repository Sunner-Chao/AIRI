using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json.Linq;
using System.Text;


namespace Alife.Framework;

public partial class ChatActivity
{
    public static async Task<ChatActivity> Create(
        Character character,
        ConfigurationSystem configurationSystem,
        PluginSystem pluginSystem,
        IProgress<(string, float)>? progress = null,
        object[]? appendServices = null)
    {
        //创建服务容器
        ServiceCollection serviceBuilder = new();
        {
            //添加系统服务
            serviceBuilder.AddLogging(builder => {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            if (appendServices != null)
            {
                foreach (var appendService in appendServices)
                    serviceBuilder.AddSingleton(appendService.GetType(), appendService);
            }

            //添加插件服务
            Type[] pluginTypes = character.Plugins
                .Select(pluginSystem.GetPlugin)
                .Where(t => t != null).Cast<Type>().ToArray();
            foreach (Type pluginType in pluginTypes)
                serviceBuilder.AddSingleton(pluginType);
        }
        ServiceProvider services = serviceBuilder.BuildServiceProvider();

        try
        {
            //实例化所有插件
            Type[] allPluginTypes = character.Plugins
                .Select(pluginSystem.GetPlugin)
                .Where(pluginType => pluginType != null)
                .Cast<Type>()
                .OrderBy(type => type.GetCustomAttribute<PluginAttribute>()?.LaunchOrder)
                .ToArray();
            Plugin[] allPlugins = new Plugin[allPluginTypes.Length];
            for (int index = 0; index < allPluginTypes.Length; index++)
            {
                Type pluginType = allPluginTypes[index];
                progress?.Report(($"创建服务 {pluginType.Name}", (float)index / serviceBuilder.Count));
                allPlugins[index] = (Plugin)services.GetRequiredService(pluginType);
            }

            //赋值插件配置数据
            for (int index = 0; index < allPlugins.Length; index++)
            {
                Plugin pluginInstance = allPlugins[index];
                if (pluginInstance is IConfigurable configurable)
                {
                    Type pluginType = pluginInstance.GetType();
                    progress?.Report(($"配置服务 {pluginType.Name}", (float)index / serviceBuilder.Count));
                    object? configData = configurationSystem.GetConfiguration(pluginType, character.StorageKey);
                    configurable.Configuration = configData;
                }
            }

            //创建人工智能构建器
            IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
            //创建上下文构建器
            ChatHistoryAgentThread contentBuilder = new();
            //构建环境
            Plugin.AwakeContext awakeContext = new() {
                Character = character,
                Services = services,
                KernelBuilder = kernelBuilder,
                ContextBuilder = contentBuilder
            };
            for (int index = 0; index < allPlugins.Length; index++)
            {
                Plugin pluginInstance = allPlugins[index];
                progress?.Report(($"初始化服务 {pluginInstance.GetType().Name}", (float)index / allPlugins.Length));

                await pluginInstance.AwakeAsync(awakeContext);
            }

            //正式开始 AI 代理
            Kernel kernelService = kernelBuilder.Build();
            ChatActivity chatActivity = new(character, contentBuilder, kernelService, services, allPlugins);

            return chatActivity;
        }
        catch
        {
            await services.DisposeAsync();
            throw;
        }
    }
}

public partial class ChatActivity : IAsyncDisposable
{
    public ServiceProvider PluginService => pluginService;
    public Kernel KernelService => kernelService;
    public Character Character => character;
    public ChatBot ChatBot => chatBot;
    public IReadOnlyList<Plugin> Plugins => plugins;

    ChatActivity(Character character, ChatHistoryAgentThread context,
        Kernel kernelService, ServiceProvider pluginService, Plugin[] plugins)
    {
        this.pluginService = pluginService;
        this.kernelService = kernelService;
        this.plugins = plugins;

        //保存原始设定
        this.character = (Character)character.Clone();

        //创建最核心的大语言服务功能
        if (kernelService.Services.GetService<IChatCompletionService>() == null)
            throw new NotSupportedException("必须至少提供一个支持对话能力的模型！");

        // 收集所有插件提供的执行参数（如思考模式）
        OpenAIPromptExecutionSettings executionSettings = new();
        foreach (IProvideExecutionSettings plugin in plugins.OfType<IProvideExecutionSettings>())
            plugin.ProvideSettings(executionSettings);

        ChatCompletionAgent llmAgent = new() {
            Name = character.Name,
            Instructions =
                $"名称：{character.Name}\n生日：{character.Birthday}\n简介：{character.Description}\n设定：\n{character.Prompt}",
            InstructionsRole = AuthorRole.System,
            Kernel = kernelService,
            Arguments = new KernelArguments(executionSettings),
        };

        chatBot = new(llmAgent, context);
    }

    public async Task Start(IProgress<(string, float)>? progress = null)
    {
        for (int index = 0; index < plugins.Length; index++)
        {
            Plugin pluginInstance = plugins[index];
            progress?.Report(($"开始服务 {pluginInstance.GetType().Name}", (float)index / plugins.Length));

            await pluginInstance.StartAsync(kernelService, this);
        }
    }

    public IEnumerable<string> GetImplicitFunctionContext()
    {
        return kernelService.Plugins.GetFunctionsMetadata()
            .Select(metadata => metadata.ToOpenAIFunction().ToFunctionDefinition(true))
            .Select(chatTool => new JObject() {
                ["kind"] = chatTool.Kind.GetHashCode(),
                ["FunctionName"] = chatTool.FunctionName,
                ["FunctionDescription"] = chatTool.FunctionDescription,
                ["FunctionParameters"] = JToken.Parse(Encoding.UTF8.GetString(chatTool.FunctionParameters)),
                ["FunctionSchemaIsStrict"] = chatTool.FunctionSchemaIsStrict
            }).Select(jObject => jObject.ToString());
    }

    readonly Character character;
    readonly ChatBot chatBot;
    readonly Kernel kernelService;
    readonly ServiceProvider pluginService;
    readonly Plugin[] plugins;

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (Plugin plugin in Plugins.Reverse())
                await plugin.DestroyAsync();
            await chatBot.DisposeAsync();
            await pluginService.DisposeAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
