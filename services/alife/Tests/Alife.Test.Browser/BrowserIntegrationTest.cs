using Alife.Function.Browser;
using Xunit;
using System.Text.RegularExpressions;

namespace Alife.Test.Browser;

public class BrowserIntegrationTest
{
    [Fact]
    public async Task TestFullSearchWorkflow()
    {
        using var engine = new BrowserEngine();
        await engine.WaitToLoadedAsync(TimeSpan.FromSeconds(5));

        // 1. 初始导航
        var navResult = await engine.NavigateAsync("https://www.midiclouds.com/");
        Assert.True(navResult.Success, "首页导航失败");

        // 2. 第一次观察：获取输入框 ID
        string observe = await engine.ObserveAsync(1);
        string inputID = Regex.Match(observe, @"输入框\[(.*)\]").Groups[1].Value;
        string searchID = Regex.Match(observe, @"检索本站\[(.*)\]").Groups[1].Value;
        Assert.True(!string.IsNullOrEmpty(inputID) && !string.IsNullOrEmpty(searchID), "未在首页找到输入框和搜索按钮");

        // 3. 执行 JS 输入和回车搜索
        string keyword = "极乐净土";
        string searchScript = $@"
            (function() {{
                const inp = document.querySelector('[data-alife-id=""{inputID}""]');
                inp.focus();
                inp.value = '{keyword}';
                inp.dispatchEvent(new Event('input', {{ bubbles: true }}));
                
                const search = document.querySelector('[data-alife-id=""{searchID}""]');
                search.click();

                return '搜索已发起';
            }})()";

        string jsResult = await engine.ExecuteScriptAsync(searchScript);
        Assert.Contains("搜索已发起", jsResult);

        // 4. 第二次观察：验证搜索结果
        string searchResult = await engine.ObserveAsync(1);
        Assert.Contains("极乐净土钢琴版", searchResult);
    }
}
