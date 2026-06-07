# Alife AI Memory

## 0. 强制开发准则 (Mandatory Rules)

- **[IMPORTANT]** 每次对话开始前，必须阅读 **`.gemini/开发规范`** 文件夹下的所有文档，确保代码设计符合项目标准。
- 遵循单一职责、开闭原则及依赖倒转原则，特别是处理跨平台抽象时。

## 1. 项目架构概述 (Architecture Overview)

### 目录结构 (Directory Structure)
- **.gemini/**: AI 规则、规范及长期记忆。
- **Sources/**: 源代码 (五层分层架构)。
- **Outputs/**: 统一编译输出，包含 DLL 及必要资源。
- **Tests/**: 单元测试与集成测试。
- **Demos/**: 演示工程。
- **Launch.cmd**: 引导程序，处理环境初始化。

### 技术栈 (Tech Stack)
- **Runtime**: .NET 9.0 (Stable) - *注：由于 .NET 10 Preview 存在 Razor SDK 编译 Bug，已执行全局降级。*
- **UI**: WPF + Blazor Hybrid (Ant Design Blazor).
- **Architecture**: 插件化分层架构。
- **Alife.Basic (基础设施层)**:
    - 职责：封装底层 OS 能力。
    - 核心：`AlifePlatform` (调度器)、`WindowsPlatform` (Windows 实现)、`WindowsNative` (所有 Win32 P/Invoke 集中地)。
    - 功能：截屏、窗口枚举、进程追踪 (`ProcessTracker`)、路径管理 (`AlifePath`)。
- **Alife.Framework (核心框架层)**:
    - 职责：定义插件协议与交互契约。
    - 核心：`PluginAttribute`、`InteractivePlugin`、`PluginUIBase`。
- **Alife.Function (功能模块层)**:
    - 职责：特定领域的重型能力（AI 模型推理、桌宠行为逻辑）。
    - 模块：`Vision` (InternVL)、`DeskPet` (Live2D 交互、`IpcCommand`)。
- **Alife.Implement (业务实现层)**:
    - 职责：组装能力为 AI 可用的工具服务。
    - 核心：`VisionService`、`ChatService` 等。
- **Alife (展现层)**:
    - 职责：主程序外壳，使用 WPF + WebView2 (Blazor Hybrid) 渲染。

## 2. 最近重构进展 (Recent Refactorings)

- **环境初始化外迁**: 将 Python 环境检测与初始化逻辑从 C# 代码移除，迁移至根目录的 `Launch.cmd`。
- **平台代码隔离**: 
    - 实现了 `AlifePlatform` 抽象层。
    - 成功将所有 Windows 原生调用 (P/Invoke) 集中到 `WindowsNative.cs`。
    - 业务逻辑不再直接耦合 Windows API。
- **依赖优化**: 清理了 `Alife.Implement` 中冗余的 `System.Drawing.Common` 引用，统一由 `Basic` 层处理。
- **本地记忆检索极简重构**:
    - 彻底干掉了 DuckDB FTS 全文检索，解决了原作者为使新记忆可搜而盲目在每次检索前执行 `PRAGMA drop_fts_index` 的极重磁盘 I/O 开销。
    - 将检索 SQL 改造为极其高效的 `ILIKE` 判断（精准命中加 1 分），结合大模型余弦相似度做同梯队内细粒度排序，中文匹配度达 100%。
    - 实现 C# 与 DuckDB 端到端的 `float` 类型对齐，改用 `reader.GetFloat(6)` 零冗余原生存取，大幅简化了代码并降低了系统耦合。
- **TTS 语音合成三引擎支持 (Edge-TTS + 本地离线 VITS + Genie-TTS)**:
  - 实现了 `VitsSpeechSynthesizer`，通过底层 `sherpa-onnx` 的 `OfflineTts` 加载本地 `G_953000.onnx` 模型进行纯离线语音合成推理。
  - 重构了 `SpeechSynthesizerBase` 抽象基类，将 NAudio 音频播放、`SilenceTrimmer` 静音裁剪、异步任务排队、打断与临时文件生命周期管理完全收拢在基类，实现最大化复用。
  - 改造了 `SpeechService`，引入了双引擎及 Genie-TTS 支持，采用“Awake 时单次创建，配置更改时仅修改属性”的设计，避免在运行时反复重建引擎（从而节约了加载 ONNX 模型的开销）。
  - 成功使用 Python 向导出的 `G_953000.onnx` 模型注入了 `sample_rate="22050"`、`model_type="vits"`、`n_speakers="804"` 等核心元数据，消除了 `sherpa-onnx` 初始化时的元数据缺失报错。
  - **Genie-TTS (GPT-SoVITS) 引擎集成**：
    - 引入了基于 `genie_tts` 的 `GenieSpeechSynthesizer` 克隆引擎，通过后台 Python 桥接子进程 `genie_bridge.py` 协同运行。
    - 设计了极具鲁棒性的加载器，可自动读取 `Runtime/Genie-TTS` 文件夹内的模型元数据（支持 `prompt_wav.json`、`refer.wav`/`refer.txt` 或自动提取 `.wav` + `.txt`）。若文件夹为空，自动 fallback 为官方 `feibi` 模型并静默下载，实现真正的零摩擦体验。
    - 解决了便携式 Python 运行环境中因缺少 C 编译器导致 native `jieba_fast` 无法编译安装的痛点，通过引入 pure-python `jieba` + `g2pM` 并为库文件注入 try-except fallback 导入，确保了中文字音转换（G2P）100% 离线自治运行。
  - **TTS 语音质量提升与控制台日志净化**：
    - 解决了中日双语模型在 C# 下由于 `zh_ja` 标签导致分词器识别错误（将 `都`/`了`/`要`等汉字映射到日语音读 `to`/`ryō`/`yō`，听着像东北话/外语）的问题，通过修改 ONNX 元数据为 `language = Chinese` 和 `add_blank = 1` 彻底恢复了完美中文读音。
    - 通过 Python 脚本对包含 1300+ 重复项的双语 `lexicon.txt` 进行主动去重，去除了 C++ `InitLexicon` 加载时的 `Duplicated word` 警告刷屏。
    - 在 `VitsSpeechSynthesizer` 和 `GenieSpeechSynthesizer` 内部加入了文本清洗器 `SanitizeText`，将波浪号、省略号等语气标点转换为逗号以带来自然停顿，并滤除所有单/双引号及无关字符防范 `OOV Ignore it!` 报错。
    - **TTS UI 优化**：将“VITS 语速 (Length Scale)”重命名为“VITS 声音长度 (Length Scale)”，并添加了支持实时模糊过滤的“说话人角色 ID 映射表”Modal 弹窗，用户点击后可以直接一键选择 804 位角色。另外，针对 Genie-TTS 提供了一键直观说明的 UI，自动说明自定义加载模式。

## 3. 未来计划 (Future Plans)

- **[ ] 跨平台准备 (TFM 切换)**:
    - 将 `Alife.Basic` 的 TargetFramework 从 `net10.0-windows` 切换为 `net10.0`，并移除 `UseWPF`。
- **[ ] 跨平台 UI 探索 (Avalonia)**:
    - 计划引入 **Avalonia UI** 作为跨平台外壳。
    - 利用 **Avalonia + Blazor Hybrid** 模式保留现有 Razor 组件。
- **[ ] 移动端适配 (Android)**:
    - 探索将 `DeskPet` 以悬浮窗形式移植到安卓。
    - 需要在 `Alife.Basic` 中实现 `AndroidPlatform`。
- **[ ] 架构持续解耦**:
    - 逐步用 `ImageSharp` 替换 `System.Drawing.Common` 以消除 GDI+ 依赖。
    - 在 `AlifePlatform.Command` 中增加对 Linux/macOS Shell 的支持。

## 4. 项目里程碑 (Project Milestones)

- [2026-04-28] 解决 Razor SDK 编译冲突: 修复了 `Accessibility.dll` 缺少 `FusionName` 导致的编译崩溃。通过降级至 .NET 9 稳定版工具链并在 `Directory.Build.props` 中使用 `ItemDefinitionGroup` 全局补全元数据解决。
- [2026-04-28] 存储路径无感迁移: 实现了 `AlifePath` 在路径变更时自动搬迁数据的功能，增强了用户体验。
- [2026-04-28] 打包与环境自动化升级: 引入了 `Build_Release.cmd` 解决 WPF Blazor 静态资产绝对路径依赖问题，并在 `Launch.cmd` 中加入了 `.NET 9 Desktop Runtime` 的全自动检测与静默安装逻辑，实现了真正的开箱即用。
- [2026-04-27] 平台能力架构重构: 完成了 `Alife.Basic` 层的解耦，所有 Win32 原生调用已集中管理，实现了业务与平台的初步分离。
- [2026-04-27] 环境管理外迁: 引入 `Launch.cmd` 统一管理 Python 环境初始化。
- [2026-05-06] XML 解析器修复与强化: 修复了 `XmlStreamParser` 中 `\"` 在属性内被截断导致标签无法闭合的严重 bug。优化了 `SurfingService.ExecuteScript` 以支持将 JS 代码置于标签内容（Content）中，极大提高了大段 JS 和包含引号/正则的代码执行成功率。
- [2026-05-11] 扩展有趣且实用的 Skill 体系: 
    - 新增「游戏中心」：支持 TRPG 跑团（包含掷骰子逻辑）、成语接龙等互动游戏。
    - 新增「创意画师」：支持通过 Python (PIL) 生成抽象几何艺术画或展示 ASCII 字符画。
    - 新增「办公管家」：支持待办事项 (TODO) 管理、会议纪要整理等办公辅助。
    - 新增「赛博算命」：集成周易起卦 (I Ching) 逻辑，提供玄学情感慰藉。
    - 成功验证：AI 能够自发地组合使用这些 Skill，表现出极高的交互趣味性和实用价值。
- [2026-05-19] TTS 双引擎重构与本地离线 VITS 适配:
  - 成功将本地 VITS (.pth) 导出并转换为带 `sherpa-onnx` 元数据的 `.onnx` 模型。
  - 重构 `SpeechSynthesizerBase` 基类，收拢音频队列控制与播放逻辑。
  - 完成 `SpeechService` 配置化改造，采用“Awake 时决定合成引擎类型，属性更改仅同步更新参数”的设计，避免热切换时频繁重载大型模型。
  - **TTS 语音质量与日志优化**：修复了双语分词映射错误（音译像东北话）问题，主动对双语 `lexicon.txt` 进行了去重（消除 1300+ 行加载警告），并在 C# 端设计了 `SanitizeText` 过滤器防范 OOV 报错。完成前端 TTS UI 优化，重命名 VITS 长度参数，引入了模糊搜索的角色 ID 映射选择 Modal。
- [2026-05-19] Genie-TTS (GPT-SoVITS) 三引擎融合: 引入了基于 python 桥接的 Genie-TTS 高拟真克隆合成引擎，设计了模型自动提取和 predefined feibi 自动 fallback 功能。在 `genie_bridge.py` 中通过 `jieba_fast` 动态重定向至 pure-python `jieba` 的垫片彻底扫清了非便携式 Python 环境下的编译障碍；并且通过将 `split_sentence` 设为 `False` 解决了 GPT-SoVITS 分句切片带来的长静音叠加（消除断断续续感），实现了极高拟真的平滑语音合成。
- [2026-05-20] 修复 Windows 锁屏检测: 重新实现 `WindowsPlatform.IsLocked()`，通过 `GetUserObjectInformation` 查询当前活动输入桌面名称是否不为 `"Default"`，完美解决了 Windows 在锁屏（Win+L）时无法正确判定锁屏状态的问题。
- [2026-05-20] 解决 Genie-TTS 文本重复与断断续续冲突: 重新开启了 `split_sentence=True` 选项以避免长文本自回归模型注意力漂移造成的发音鬼畜重复。同时在 `genie_bridge.py` 中对 `TTSPlayer` 的 `_save_session_audio` 方法进行了猴子补丁，在合并前自动裁剪每个子句音频片段 of 起止静音，并注入 150ms 间隔，实现了不拖沓且无鬼畜重复的高品质克隆声音生成。
- [2026-05-20] VITS 与 Genie-TTS 的 Python 原生化及 GPU 优先加速:
  - 将 VITS 的音频合成完全重构为 Python 进程桥接方式（`vits_bridge.py`），利用 `numpy` 与内置 `wave` 执行句级/句内自适应切分（40字以内）、静音裁剪与 150ms 间隔拼接。
  - 在 `vits_bridge.py` 与 `genie_bridge.py` 中增加了首选 GPU 执行的逻辑，通过动态检测 `onnxruntime.get_available_providers()` 绑定 `CUDAExecutionProvider` 或 `DmlExecutionProvider`，有 GPU 时自动使用 GPU 加速，彻底消除了由于单次直接调用 C# Wrapper 传超长文本导致的异常，也排除了 C# dll 潜在的底层限制。

## 5. 经验教训与技巧 (Lessons Learned & Tips)

- **XML 标签生成最佳实践**: AI 过去在生成 `<execute_script script="...">` 时常因属性值内转义双引号 `\"` 导致 XML 流解析器卡死崩溃。尽管解析器已修复，但最佳实践仍是**将复杂 JavaScript 代码放在 `<execute_script>` 的内容块中**，而不是作为 `script` 属性。
- **MIDI 获取与 MusicMaster 进阶策略**:
    - **先验证再搜索**: 对于不确定的曲名，必须先通过 `websearch` 确认标准名、艺术家及别名，避免因译名差异搜不到。
    - **阶梯搜索**: 优先尝试 GitHub 和 MIDIClouds 的精确定位，若失败应**果断切换到 Google (SurfingService.websearch)** 进行全局搜索，不要死扣在专门的 MIDI 站。
    - **及时止损**: 如果尝试多种关键词仍无果，主动告知用户尝试过的关键词并请求协助（提供链接或文件），严禁在无效结果中循环搜索。
