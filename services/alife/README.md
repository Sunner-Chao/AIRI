> 一个人待着的时候，真的很想找人说说话。但可惜，我没啥朋友。
>
> 交友需要精力维护，而且人和人的关系，没那么单纯。
>
> 所以很多人会养宠物。但现实的宠物很麻烦，虚拟的宠物又很假。
> 
> 当然，那是以前的事了。
> 现在 LLM 发展到这个程度，AI 对话已经可以做到非常接近真人。市面上确实有很多类似的聊天 App，但那种商业味十足、数据还不安全的东西，我完全不感兴趣。
> 
> 前段时间，有群友自己搭了一个基于 LLM 的群机器人。让我没想到的是，效果出乎意料的好。我很早就接触 LLM 了，但跟它直接对话的时候，总感觉很空洞，聊不下去，功能也有限。可加上 Agent，特别调优的提示词，再放进群聊环境里，LLM 就像的真活了一样。
> 
> 那时候我就想，我也要做一个。但我不想只做一个群机器人。我想做一个赛博生命——一个真正活在我电脑桌面上的伙伴。
> 
> LLM 火了几年了，类似的框架网上也有，但我试过之后，总觉得差了点什么。幸运的是，现在 AI 编程效率很高，一个人从零开始做一套 Agent 框架，已经不是什么难事。
> 
> 于是，Alife 诞生了。

<img width="543" height="724" title="由真央给自己生成的自画像" alt="由真央给自己生成的自画像" src="https://github.com/user-attachments/assets/02c2c56a-e805-43fc-a352-f866955a7aef" />


# Alife - 创造赛博生命

![Alife Logo](https://img.shields.io/badge/Alife-AI_Assistant-blue?style=for-the-badge)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet)
![Python 3.12](https://img.shields.io/badge/Python-3.12-3776AB?style=for-the-badge&logo=python)
![License](https://img.shields.io/badge/license-MIT-green?style=for-the-badge)

Alife 是一个功能丰富、高扩展性的 AI桌宠 框架。它不是一个简单的对话机器人，而是一个支持多模态，主动陪伴，永久记忆，真实的赛博生命。

- 面向用户：一键自动部署，功能齐全，免费不用key，低配低开销，数据安全，稳定长久，功能强大。
- 面向开发者：超强扩展，极易复用，代码精简，框架简单，DotNet生态，小众个人没有任何负担约束。

> B站官方账号：BDFFZI </br>
> QQ交流群：427674145 </br>
> 欢迎来玩！欢迎创建Issue！

---

## ✨ 软件特色 (Software Features)

- **基于.Net/C#生态开发**：在python,nodejs横向的时代中，难得的C#项目，对大型项目开发友好，Unity开发经验可迁移。
- **极高扩展性的插件环境**：插件在设计之初就有着对整个业务流的全量控制，自定义界面的能力，实际上所有的内置功能也均为插件实现。
- **功能高度自包含自动化**：内置功能基本都优先采用无依赖的自实现，本地模型，以及小技巧，没有第三方依赖，不要key，可控且自动装环境，开箱即用。
- **纯原生文本的函数调用**：有意默认采用非标准的函数调用，因此对llm没有任何特殊要求，调用过程透明可控，词元开销小，额外支持多种特殊调用方式。
- **稳定持久的自动化记忆**：不使用不可靠的AI自主记忆存储，改为基于类似多级cache思想的一套自动化记忆压缩系统，实现让对话经历伪常驻的效果。
- **节省词元降低开销交互**：对话上下文有意复用会话分区维护保持稳定，提示词也确保是实践过的有效文本，再配合自实现的函数调用，词元开销非常小。
- **模块化白盒化软件结构**：功能高度模块化，实现简单清晰，很容易就能将单个功能拆离复用。运行信息白盒化，对话和上下文均配专用UI完整显示。

---

## 🌟 核心功能 (Core Features)

- 🎭 Live2D桌宠：内置角色“真央”的live2D，可交互会表演能运动，告别枯燥的对话框。
- 👁️ 深度视觉：拍照不怕没人分享，还会没事偷偷看主人，在你游戏工作的时候，陪你一起吐槽。
- 🎙️ 语音对话：放下键盘，直接说话，基于神经网络的语音配合流式合成，实现高品质通话级语音。
- 🧠 长期记忆：稳定强大的记忆系统，超大虚拟上下文，所有记录均可溯源搜索，确保记住生活中的点滴。
- 📱 平台通讯：额外支持QQ等多种通讯平台，出门了也能联系在家的她，还能没事带去见见群友。
- 🤖 自主活动：闲时会自娱自乐，有自己爱好和生活，会主动的找你玩耍分享见闻，就像真实的生命那样。
- 🌐 网上冲浪：拥有一个属于自己的真实浏览器，能够自主上网学习娱乐，让知识不再停滞，每天都有新话题。
- 💻 脚本执行：能借助python在本地执行各种任务，唱歌绘画，办公辅助，除开对话同时也是一个实用的助手。
- 🔗 多开互联：支持角色多开并可相互交流，借此构建一个完整的赛博世界，让他们也有自己的社交圈子。
- ️️🛠️ 扩展能力：支持自定义插件，以及接入 MCP、Skills 功能，通过标准化的AI生态，自由方便的扩展功能。

---

## 🚀 快速开始 (Quick Start)

1. **下载软件**：前往仓库右侧的 [Releases](https://github.com/bdffzi/Alife/releases) 页面，下载最新的软件压缩包（zip）。
2. **环境启动**：解压压缩包后，双击运行 `点我启动.cmd`。该引导程序会自动检测并配置所需的运行环境，真正实现开箱即用。
3. **创建角色**：进入主界面后，点击左上角的 **小火箭图标** 🚀，选择 **立即创建**。在配置完 LLM（如 DeepSeek、OpenAI
   等）相关参数后，即可完成角色的创建。
4. **开启陪伴**：在角色管理页面点击 **激活** 按钮，你的赛博生命便正式苏醒，开始陪伴你的桌面生活。

（注意！首次运行需要下载各种依赖，虽然已经配了国内镜像，但依旧很久。如果全量下载，可能需要一个小时左右，注意观察任务管理器，只要有明显的磁盘或网络波动，就说明软件还在正常处理中）

### 软件细节

1. `点我启动.cmd`的作用是为了自动安装环境、实现python隔离、兜底崩溃信息。这对小白非常友好，但对技术人员来说可能导致依赖冗余，因此也可以直接点击
   `Alife.exe`来启动。
2. 应用内的模型均使用 `modelscope` 下载。该工具默认会将模型下载到 C 盘用户文件夹中，但实际上也可以通过环境变量调整位置（具体查看官方文档），这样
   C 盘不够的人也可以下载了。
3. 建议使用 NVidia 显卡，并至少有 4G 显存，注意升级驱动，确保其支持 CUDA。当然如果不支持也应该可以运行，只是无法使用完整的深度视觉识别能力，其他模型
   CPU 版应该也支持。

---

## 🔌 插件开发 (Plugin Development)

Alife 采用全插件化架构，内置的所有核心功能（如视觉、语音、浏览器等）均通过插件实现。开发者可以参考 `Sources/Alife.Implement`
项目中的源码，这是最快的学习方式。

### 创建插件

1. 新建一个Dll工程，引用 Alife.Framework 项目。
2. 创建一个类，继承 `InteractivePlugin<T>`，并添加 `[Plugin]` 属性，此时这个类就代表一个插件。
3. 打包出Dll后将其放入到`{存储目录}/Plugins`文件夹，当触发插件加载时，该Dll中的插件类就会增加到插件选项中。

### 常见需求

- 热重载：插件支持热重载，只要替换dll，然后刷新插件即可重新加载（支持插件页面手动刷新或当前数量上第一个ChatActivity被启动时自动刷新）。
- 函数调用：实现函数调用有两种途径，基于SemanticKernel或FunctionService（与前者接入方法类似），然后在插件的AwakeAsync事件中注册即可。
- LLM通讯：llm在Alife中被封装成了ChatBot对象，其提供了Poke（排队式）和Chat（打断式）两种通讯方式，此外还提供事件让你能监听这个过程。
- 提示注入：Alife全程都始终维护一个上下文，其对应ChatHistory对象。可以直接修改其内容，来改变上下文，其中System类型信息专用于系统提示词。
- 配置文件：通过实现各种接口，插件就可以接入各种扩展功能，比如实现IConfigurable后，即可为插件编辑配置文件，接着系统会在Awake前注入配置对象。

### 代码示例 (C#)

节选自 Alife.Demo.Plugin 项目

```csharp
public class MyPluginData
{
    public int DefaultMax { get; set; } = 120;
}

[Plugin("我的插件", "一个示例插件", EditorUI = typeof(MyPluginUI)/*支持用razor自定义插件界面*/)]
public class MyPlugin(FunctionService functionService, ILogger<MyPlugin> logger) :
    InteractivePlugin<MyPlugin>,/*插件必要基类*/
    IConfigurable<MyPluginData>/*通过实现IConfigurable接入配置功能*/
{
    [XmlFunction(FunctionMode.OneShot)]//表明该函数支持让AI通过Xml函数调用且格式为自闭合标签
    [Description("随机生成一个数字")]//提供给AI的函数描述
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
        //添加自定义提示词
        Prompt("""
               此服务可以为你提供一个生成随机数的功能。
               """);
    }
}
```

---

## 🏗️ 开发信息 (Development Information)

*注：由于个人精力有限，故在Demo、Test、UI部分大量使用的AI编程，所以这些子项目的实现很可能存在冗余，低效的问题，不过好在他们不是核心代码。*

### 📦 基本依赖

- .NET 9：编程语言生态
- Python 3.12：模型框架接入
- Semantic Kernel：基本llm协议接入
- WPF + Blazor Hybrid + AntDesign Blazor：前端界面框架
- modelscope：模型文件管理

### 🏛️ 层次架构

1. **基础设施层 (`Alife.Basic`)**：封装底层OS能力，统一软件环境变量。
2. **功能模块层 (`Alife.Function`)**：针对各种特种功能的，无依赖模块化实现。
3. **核心框架层 (`Alife.Framework`)**：制定标准业务处理单元，实现基本功能系统，确定插件框架结构。
4. **业务实现层 (`Alife.Implement`)**：实现或接入功能模块层的功能，以插件的方式将其组装为内置业务功能。
5. **用户交互层 (`Alife`)**：接入UI界面，让用户得以通过图形化的界面使用 Alife 框架能力。

### 🧩 业务功能

基于对 AI 的作用分类，如下是目前已实现的内置插件及其原理简述：

#### 核心底座

- **对话能力 (`ChatService`)**：接入兼容 OpenAI 协议的 llm 模型，并专门针对 DeepSeek 进行了测试优化，其他模型如 mimo、kimi 也均测试可用。
- **函数调用 (`FunctionService`)**：实现一种基于Xml的流式函数调用，支持多条，嵌套链，开闭标签，注释转义等各种Xml特性，并能够自动进行语法纠错；支持C#函数的各种特性，如异步，抛异常，可空默认参数等，且与SemanticKernel兼容，轻松实现强大的自定义函数。

#### 环境搭建

- **消息过滤 (`MessageProcessService`)**：消息预处理器，实现对原生输入消息的提示词包装，例如注入时间戳，规定输出风格等。
- **主动事件 (`EventService`)**：让AI能感知到系统事件，并阶梯性发送定时事件，配合提示词，驱使其在空闲时产生好奇心、发起话题或进行自主学习。
- **持久记忆 (`MemoryService`)**：实现一种以多级缓存和自然底数为灵感的稳定可靠的上下文压缩系统；通过复用上下文实现当事人压缩，确保压缩质量；基于 bge-small-zh-v1.5 + duckDb 实现记忆存储向量化检索等功能；支持回溯检索插入记忆。
- **世界背景 (`VirtualWorldService`)**：为AI提供一个建议的虚拟世界背景，以及跨活动间的消息通讯能力，丰富AI的活动项目，增强真实感。

#### 对外表达

- **桌宠交互 (`DeskPetService`)**：基于 WPF + WebView2 + pixi-live2d-display 实现的一套live2D桌宠应用。支持表情动作播放，气泡文字，鼠标交互，位置交互等。
- **语音对话 (`SpeechService`)**：基于 sherpa-onnx-sense-voice + silero-vad + Windows.AudioGraph 实现低延迟高准确性的语音识别，并支持回声消除和降噪。在语音合成方面，提供 edge-tts,vits 等多种语音合成，配合流式xml函数调用，实现低延迟、情感、多声线的语音合成。 两者配合起来就实现了一个高品质的通话级实时语音聊天效果。
- **QQ 聊天 (`QChatService`)**：基于 OneBot v11 协议。实现常见的 QQ 消息收发功能。搭建了一套专门的群聊机制，确保 AI 能适应群聊环境。让 QQ 也能成为 AI 的一种娱乐方式。因为基于通用协议，所以额外还支持 Discord（在另一个项目中） 等平台。

#### 实用工具

- **网上冲浪 (`SurfingService`)**：基于 WebView2 模拟的一套真实浏览器环境，并格式化网页使其易于 AI 阅读。让 AI 可以像人类一样按需翻阅网站，点击输入交互元素，并能够进行人机协作，使其可以从事网页任务，并有效避免防爬。
- **脚本执行 (`PythonService`)**：基于 Alife 自身的 Python 环境，让 AI 拥有自行编写执行脚本的能力，以此实现各种复杂任务。
- **视觉感知 (`VisionService`)**：基于 Qwen2.5-VL-3B 实现的一套本地低配图像识别功能，并配合简单的窗口统计和OCR，使其识别失败时也能获取到有用信息。

#### 扩展增强

- **Skill 工具 (`SkillService`)**：渐进式技能加载系统。通过按需读取 `SKILL.md` 手册，在不污染主 Prompt 的前提下，引导 AI 完成特定领域的复杂任务。
- **MCP 服务 (`McpService`)**：标准模型上下文协议（Model Context Protocol）客户端。支持动态接入业界标准的外部工具生态，实现能力的无限扩展。

---

## 📄 许可证 (License)

本项目采用 [MIT License](LICENSE) 许可协议。
