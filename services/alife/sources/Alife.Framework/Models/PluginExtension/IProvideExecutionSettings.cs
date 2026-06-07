using Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// 允许插件为 AI 代理提供额外的执行参数（如思考模式、温度等）。
/// </summary>
public interface IProvideExecutionSettings
{
    void ProvideSettings(OpenAIPromptExecutionSettings settings);
}
