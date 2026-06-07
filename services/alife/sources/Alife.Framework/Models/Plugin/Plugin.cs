using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace Alife.Framework;

public abstract class Plugin
{
    public struct AwakeContext
    {
        public Character Character { get; init; }
        public ServiceProvider Services { get; init; }
        public IKernelBuilder KernelBuilder { get; init; }
        public ChatHistoryAgentThread ContextBuilder { get; init; }
    }

    /// <summary>
    /// 活动初始化时调用，此时AI还未激活，你有机会去调整它的插件环境和上下文内容
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual Task AwakeAsync(AwakeContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// AI被激活后触发
    /// </summary>
    /// <param name="kernel"></param>
    /// <param name="chatActivity"></param>
    /// <returns></returns>
    public virtual Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// AI即将关闭时触发
    /// </summary>
    /// <returns></returns>
    public virtual Task DestroyAsync()
    {
        return Task.CompletedTask;
    }
}
