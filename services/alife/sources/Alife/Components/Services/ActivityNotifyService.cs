using Alife.Framework;

namespace Alife.Components.Services;

/// <summary>
/// UI层专属的活动状态变更通知中心。
/// 由于不直接修改Framework，该服务用于显式通知UI组件（如侧边栏）刷新状态。
/// </summary>
public class ActivityNotifyService
{
    public event Action? OnChanged;

    public ActivityNotifyService(ChatActivitySystem system)
    {
        system.Created += _ => OnChanged?.Invoke();
        system.Destroyed += _ => OnChanged?.Invoke();
    }
}
