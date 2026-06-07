using Newtonsoft.Json.Linq;

namespace Alife.Framework;

public class ConfigurationSystem
{
    public Type? GetConfigurationType(Type target)
    {
        if (configurationTypes.TryGetValue(target, out Type? configurationType))
            return configurationType;

        Type[] interfaces = target.GetInterfaces();
        Type? targetInterface = interfaces.FirstOrDefault(value => value.IsGenericType && value.GetGenericTypeDefinition() == typeof(IConfigurable<>));
        if (targetInterface == null)
            return null;

        configurationType = targetInterface.GetGenericArguments()[0];
        configurationTypes[target] = configurationType;
        return configurationType;
    }
    public bool CanConfiguration(Type type)
    {
        return GetConfigurationType(type) != null;
    }
    public object? GetConfiguration(Type target, string root = "")
    {
        Type? configurationType = GetConfigurationType(target);
        if (configurationType == null)
            return null;

        JObject? configuration = storageSystem.GetObject<JObject>(Path.Combine(root, "Configuration", target.FullName!)) ??
                                 storageSystem.GetObject<JObject>(Path.Combine("Configuration", target.FullName!));
        if (configuration != null) return configuration.ToObject(configurationType);
        return Activator.CreateInstance(configurationType, null);
    }
    public JObject? GetConfigurationJson(Type target, string root = "")
    {
        object? configuration = GetConfiguration(target, root);
        if (configuration != null)
            return JObject.FromObject(configuration);
        return null;
    }
    public void SetConfiguration(Type target, object configuration, string root = "")
    {
        if (CanConfiguration(target) == false)
            throw new Exception("目标类型不支持配置功能！");

        storageSystem.SetObject(Path.Combine(root, "Configuration", target.FullName!), configuration);
    }
    public void DeleteConfiguration(Type target, string root = "")
    {
        storageSystem.DeleteObject(Path.Combine(root, "Configuration", target.FullName!));
    }
    public bool HasConfiguration(Type target, string root = "")
    {
        string path = Path.Combine(root, "Configuration", target.FullName!);
        return storageSystem.GetObject<JObject>(path) != null;
    }

    readonly StorageSystem storageSystem;
    readonly Dictionary<Type, Type> configurationTypes;

    public ConfigurationSystem(StorageSystem storageSystem)
    {
        this.storageSystem = storageSystem;
        configurationTypes = new Dictionary<Type, Type>();
    }
}
