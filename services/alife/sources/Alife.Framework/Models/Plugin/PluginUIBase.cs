using Microsoft.AspNetCore.Components;

namespace Alife.Framework;

/// <summary>
/// 插件 UI 基类（带配置）。
/// TPlugin: 插件类型，TConfig: 配置类型。
/// PluginType 由泛型自动推导，ConfigSaveUI 由框架层常驻渲染，子类无需关心。
/// </summary>
public abstract class PluginUIBase<TPlugin, TConfig> : ComponentBase
    where TPlugin : Plugin
    where TConfig : class, new()
{
    public Type PluginType => typeof(TPlugin);

    [Parameter] public Character? Character { get; set; }
    [Parameter] public ChatActivity? ChatActivity { get; set; }
    [Parameter] public TPlugin? Plugin { get; set; }
    [Parameter] public TConfig Configuration { get; set; } = new();
    [Parameter] public RenderFragment DefaultUI { get; set; } = _ => { };
}

/// <summary>
/// 插件 UI 基类（无配置）。
/// </summary>
public abstract class PluginUIBase<TPlugin> : PluginUIBase<TPlugin, object>
    where TPlugin : Plugin
{
}
