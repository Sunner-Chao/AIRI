using System.ComponentModel;
using Alife.Basic;
using Alife.Framework;
using Alife.Function.Browser;
using Alife.Function.Interpreter;

namespace Alife.Implement;

[Plugin("网上冲浪", "让AI可以像人一样操控真实的浏览器，从而能够执行各种网页任务的同时，避免反爬。")]
[Description(@"你拥有一个真实的浏览器窗口，可以借此进行网上冲浪，从而每天学点新知识，找点新话题。
提示：若遇到验证或登录，一定要尝试请求主人协助，不然总被反爬就没意义了。此外优先使用搜索引擎（谷歌 > 必应 > 百度）来明确需求，再行动。")]
public class SurfingService(FunctionService functionService)
    : InteractivePlugin<SurfingService>, IDisposable
{
    [XmlFunction(FunctionMode.OneShot)]
    [Description("打开网页。")]
    public async Task Navigate(string url)
    {
        await browser.NavigateAsync(url);
        Poke($"[Navigate] 已打开: {url}（接下来可以使用 observe 来查看页面内容）");
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("查看页面内容（注意！内容过多时会被分页，所以当你没看到想要的内容时，可以尝试用 page 翻页。此外该功能还会自动为可交互元素分配[ID]，借此可用`document.querySelector(\"[data-alife-id='ID']\")`定位交互）")]
    public async Task Observe([Description("观察的页面区域，从1开始")] int page)
    {
        string result = await browser.ObserveAsync(page);
        Poke($"页面结果如下（注意！网站页面大多不能一次全显示，必须通过 page 翻页来查看完整内容。此外若遇到人机验证或登录，可请求主人协助）：\n{result}");
    }

    [XmlFunction(FunctionMode.Content)]
    [Description("在浏览器中执行JS表达式。")]
    public async Task RunJs(XmlExecutorContext context, [XmlContent] string script)
    {
        if (context.CallMode == CallMode.Closing)
        {
            string code = context.FullContent.Trim();
            string result = await browser.ExecuteScriptAsync(code);
            Poke($"[RunJS] 执行结果：\n{result}");
        }
    }

    [XmlFunction(FunctionMode.OneShot)]
    [Description("下载文件到本地。")]
    public async Task Download([Description("下载链接")] string url, [Description("本地绝对路径")] string path)
    {
        await AlifePlatform.DownloadFileAsync(url, path);
        Poke($"[Download] 文件已下载至：{path}");
    }

    readonly BrowserEngine browser = new();

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);
        await browser.WaitToLoadedAsync(TimeSpan.FromSeconds(3));
        functionService.RegisterHandler(this, nameof(RunJs));
    }

    public void Dispose() => browser.Dispose();

}
