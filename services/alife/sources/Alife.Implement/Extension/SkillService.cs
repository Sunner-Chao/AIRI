using System.ComponentModel;
using Alife.Basic;
using Alife.Framework;
using Alife.Function.Interpreter;
using Microsoft.Extensions.DependencyInjection;

namespace Alife.Implement;

[Plugin("Skill工具", "Skill 是一种渐进式（按需加载省token）的工具包，通过预编写的手册引导和规范AI完成各种各样的复杂任务。\n你可以使用“modelscope skills add”来手动添加新的技能。")]
public class SkillService(FunctionService functionService) : InteractivePlugin<SkillService>
{
    [XmlFunction(FunctionMode.OneShot)]
    [Description("快速查看 skill 内容")]
    public void ReadSkill(string skillName)
    {
        string skillDocPath = Path.Combine(skillsPath, skillName, "SKILL.md");
        if (File.Exists(skillDocPath) == false)
        {
            ChatBot.Poke($"[{nameof(ReadSkill)}] skill文件不存在");
            return;
        }

        string skillDoc = File.ReadAllText(skillDocPath);
        string[] appendFiles = Directory.GetFiles(Path.Combine(skillsPath, skillName), "*", SearchOption.AllDirectories);

        Poke(
        $"""
         [{nameof(ReadSkill)}] 已读取 {skillName} skill

         > 包含文件：
         - {string.Join("\n- ", appendFiles)}

         > 手册内容：
         ```
         {skillDoc}
         ```
         """);
    }


    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);

        //注册函数
        XmlHandler xmlHandler = new(this);
        functionService.RegisterHandler(xmlHandler);

        //附加提示词
        string[] skills = Directory.Exists(skillsPath)
            ? Directory.GetDirectories(skillsPath).Select(Path.GetFileName).Cast<string>().ToArray()
            : [];
        Prompt($"""
                此服务让你拥有使用和管理 skill 的功能，skill 是一种可按需装载的文档功能，可以在需要的时候扩展你的能力。

                每个 skill 都是一个文件夹，它由一个 SKILL.md（使用手册），和可能附带的脚本、资源文件构成。
                如果要使用 skill，只需要先阅读一下 <skillFolder>/SKILL.md ，然后按其指导的方式去使用即可（注意手册中提到的文件一般是相对 skillFolder 的路径，你可以用python执行）。
                你的所有 skill 都存在 {skillsPath} 这个根目录中，根据 skill 的格式你也可以创建自己的 skill。

                当前 skill根目录 中已经存在的 skill 有：
                - {string.Join("\n- ", skills)}
                """);
    }

    readonly string skillsPath = Path.Combine(AlifePath.StorageFolderPath, "Skills");
}
