using Alife.Basic;
using System.ComponentModel;
using Alife.Framework;
using Alife.Function.Interpreter;
using Alife.Function.Vision;

namespace Alife.Implement;

public record VisionConfig
{
    [Description("是否启用深度视觉（开启后将加载深度模型并进行复杂场景分析）")]
    public bool EnableDeepVision { get; set; } = true;
    // public string AppendPrompt { get; set; } = "（提示：你收到的图片通常来自于年轻人，他们喜欢搞怪和反差，因此基本不会发送仅提供观赏价值的普通图片，每一张其实都代表了强烈的个人情绪或者某种搞笑炫耀性的场景。你要努力理解他们图片中的梗、搞笑、情感、意图等。注意！他们可能比你想象的更加狂野自由有表达欲甚至低俗）";
    public string AppendPrompt { get; set; } = "（请精简的描述一下图片大体内容，避免输出过多的文本，提高分析速度）";
}

public partial class VisionService
{
    static VisionAnalyzer? analyzer;

    /// <summary>
    /// 确保视觉分析器已初始化（供其他服务调用）
    /// </summary>
    static void TryInitAnalyzer()
    {
        analyzer ??= new VisionAnalyzer();
    }
}

[Plugin("视觉感知", "让 AI 能够看到屏幕内容，理解图片，观察世界。")]
[Description("此服务让你拥有视觉感知能力：你可以截取屏幕画面并理解其内容，或者分析用户提供的图片。" +
             "在分析图片时，你需要提供prompt参数。在该参数中，你要清晰描述你的问题，并尽可能提供背景信息，以帮助视觉模型分析，但也注意不要随意揣测其中内容，防止影响识别结果！")]
public partial class VisionService(FunctionService functionService)
    : InteractivePlugin<VisionService>, IConfigurable<VisionConfig>
{

    /// <summary>
    /// 截取屏幕并进行视觉理解，将结果反馈给 AI。
    /// </summary>
    [XmlFunction(FunctionMode.OneShot)]
    [Description("查看当前屏幕内容。（使用后需等待结果返回）")]
    public async Task LookScreen([Description("请尽可能提供完整的上下文背景以辅助视觉分析")] string prompt)
    {
        if (AlifePlatform.IsLocking())
        {
            Poke("【屏幕分析结果】当前电脑处于锁屏状态，无法获取屏幕内容，用户应该不在电脑前。");
            return;
        }

        prompt += $"（这是一张屏幕截图，当前焦点窗口为{WindowsPlatform.GetActiveWindowTitle()}）" + configuration!.AppendPrompt;

        string screenshotPath = AlifePlatform.Screenshot();

        string deepVisionResult = "未开启";
        if (Configuration?.EnableDeepVision == true)
        {
            CancellationTokenSource cancellationTokenSource = new(30000);
            deepVisionResult = $"{await analyzer!.QueryAsync(screenshotPath, prompt, cancellationToken: cancellationTokenSource.Token)}";
        }

        Poke($"""
              【屏幕分析结果】
              - 窗口列表：{AlifePlatform.GetRunningWindowTitles()}
              - 焦点窗口：{WindowsPlatform.GetActiveWindowTitle()}
              - 深度视觉：{deepVisionResult}（内容不一定准确仅供参考）
              """);
    }

    /// <summary>
    /// 分析指定路径的图片。
    /// </summary>
    [XmlFunction(FunctionMode.OneShot)]
    [Description("对指定的图片进行视觉分析。（使用后需等待结果返回）")]
    public async Task LookImage([Description("图片地址或网址")] string path, [Description("请尽可能提供完整的上下文背景以辅助视觉分析")] string prompt)
    {
        prompt += configuration!.AppendPrompt;

        try
        {
            // 处理网络图片
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                string downloaded = $"{AlifePath.TempFolderPath}/vision_download.png";
                await AlifePlatform.DownloadFileAsync(path, downloaded);
                path = downloaded;
            }


            string result = "未开启";
            if (Configuration?.EnableDeepVision == true)
            {
                CancellationTokenSource cancellationTokenSource = new(30000);
                result = $"{await analyzer!.QueryAsync(path, prompt, cancellationToken: cancellationTokenSource.Token)}";
            }

            Poke($"""
                  【图片分析结果】
                  - 文字识别：{await AlifePlatform.OcrAsync(path)}
                  - 深度视觉：{result}（内容不一定准确仅供参考）
                  """);
        }
        catch (Exception ex)
        {
            Poke($"图片分析失败：{ex.Message}");
        }
    }

    public VisionConfig? Configuration
    {
        get => configuration;
        set
        {
            configuration = value;
            if (value is { EnableDeepVision: true })
            {
                TryInitAnalyzer();
            }
        }
    }

    VisionConfig? configuration;

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);

        functionService.RegisterHandler(this);
    }
}
