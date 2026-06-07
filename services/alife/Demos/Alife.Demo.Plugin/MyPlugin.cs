using System.ComponentModel;
using Alife.Demo.Plugin;
using Alife.Framework;
using Alife.Function.Interpreter;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

public class MyPluginData
{
    public int DefaultMax { get; set; } = 120;
}

[Plugin("我的插件", "一个示例插件", EditorUI = typeof(MyPluginUI)/*支持用razor自定义插件界面*/)]
public class MyPlugin(FunctionService functionService, ILogger<MyPlugin> logger) :
    InteractivePlugin<MyPlugin>,/*插件必要基类*/
    IConfigurable<MyPluginData>,/*通过实现IConfigurable接入配置功能*/
    IProvideExecutionSettings
{
    // [KernelFunction]//与SemanticKernel函数实现方法兼容
    [XmlFunction(FunctionMode.OneShot)]// 表明该函数支持让AI通过Xml函数调用且格式为自闭合标签
    [Description("随机生成一个数字")]// 提供给AI的函数描述
    public Task Rand([Description("随机的最大范围")] int? max = null/*支持任何可被字符串转换的参数，包括默认值可选这些特性*/)
    {
        if (max == null)
            max = Configuration!.DefaultMax;//配置在插件构造后立即注入，故系统事件期间都是不为空的
        if (max < 0)
            throw new Exception("最大值必须大于 0");//可以正常抛出异常

        int value = Random.Shared.Next(max.Value);
        Poke("随机数结果：" + value);//向AI反馈结果
        logger.LogInformation($"调用 {nameof(Rand)} 结果 {value}");//支持依赖注入的Logger

        return Task.CompletedTask;//如果有需要你可以使用异步代码
    }

    public MyPluginData? Configuration { get; set; }

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);

        //注册函数调用
        functionService.RegisterHandler(this);
        // context.KernelBuilder.Plugins.AddFromObject(this);//可选的，基于SemanticKernel实现标准函数调用
        //添加自定义提示词
        Prompt("""
               此服务可以为你提供一个生成随机数的功能。
               """);
    }

    public void ProvideSettings(OpenAIPromptExecutionSettings settings)
    {
        //基于SemanticKernel的函数调用需要（这对模型会产生限制和兼容性问题，而且没有回显，不建议使用）
        // settings.ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions;
    }
}
