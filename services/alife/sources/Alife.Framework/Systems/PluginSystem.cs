using System.Reflection;
using System.Runtime.Loader;
using Alife.Basic;

namespace Alife.Framework;

public class PluginSystem : IDisposable
{
    public event Action? PreReloadPlugins;
    public event Action? PostReloadPlugins;

    public StringFolder GetPluginFolder()
    {
        return pluginFolder;
    }

    public IEnumerable<Type> GetAllPlugins()
    {
        return pluginTypes.Values;
    }

    public Type? GetPlugin(string pluginID)
    {
        return pluginTypes.GetValueOrDefault(pluginID);
    }

    public string GetPluginID(Type pluginType)
    {
        return pluginType.FullName!;
    }

    public void ReloadPlugins()
    {
        PreReloadPlugins?.Invoke();

        //卸载插件
        if (pluginContext != null)
            pluginContext.Unload();
        pluginContext = new AssemblyLoadContext("AllPluginsContext", isCollectible: true);

        //获取插件
        string pluginRoot = Path.Combine(AlifePath.StorageFolderPath, "Plugins");
        if (!Directory.Exists(pluginRoot))
            Directory.CreateDirectory(pluginRoot);
        string[] pluginPaths = Directory.GetFiles(pluginRoot, "*.dll", SearchOption.AllDirectories);

        //加载插件
        HashSet<string> currentAssemblies = AssemblyLoadContext.Default.Assemblies.Select(assembly => assembly.FullName).ToHashSet()!;
        foreach (string pluginPath in pluginPaths)
        {
            try
            {
                string assemblyName = AssemblyName.GetAssemblyName(pluginPath).FullName;
                if (currentAssemblies.Contains(assemblyName))
                    continue;

                using var assemblyStream = new MemoryStream(File.ReadAllBytes(pluginPath));
                string pdbPath = Path.ChangeExtension(pluginPath, ".pdb");
                MemoryStream? pdbStream = File.Exists(pdbPath) ? new MemoryStream(File.ReadAllBytes(pdbPath)) : null;
                pluginContext.LoadFromStream(assemblyStream, pdbStream);
                pdbStream?.Dispose();
            }
            catch (Exception)
            {
                // 可能包含一些非C#的dll
                // Console.WriteLine(e);
            }
        }

        // 重新扫描所有已加载程序集中的 IPlugin
        pluginTypes.Clear();
        Assembly[] beInspectedAssemblies = pluginContext.Assemblies
            //附带官方插件
            .Append(Assembly.Load("Alife.Framework"))
            .Append(Assembly.Load("Alife.Implement"))
            .ToArray();
        foreach (Assembly assembly in beInspectedAssemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAssignableTo(typeof(Plugin)) == false)
                    continue;
                if (type.IsAbstract)
                    continue;
                if (type.IsInterface)
                    continue;
                if (type.GetCustomAttribute<PluginAttribute>() == null)
                    continue;

                pluginTypes.Add(GetPluginID(type), type);
            }
        }

        SyncFolder();
        PostReloadPlugins?.Invoke();
    }

    public void SaveData()
    {
        storageSystem.SetObject("PluginSystem/PluginFolder", pluginFolder);
    }


    readonly StorageSystem storageSystem;
    readonly Dictionary<string, Type> pluginTypes;
    readonly StringFolder pluginFolder;
    AssemblyLoadContext? pluginContext;

    public PluginSystem(StorageSystem storageSystem)
    {
        this.storageSystem = storageSystem;
        pluginTypes = new Dictionary<string, Type>();
        pluginFolder = storageSystem.GetObject("PluginSystem/PluginFolder", new StringFolder("全部插件"))!;

        //预热程序集，因为插件可能依赖Alife自身的程序集，结果Alife本身目前未用到，导致未加载
        PreloadAllAssemblies();

        ReloadPlugins();
    }

    public void Dispose()
    {
        SaveData();
    }

    void SyncFolder()
    {
        HashSet<string> currentPlugins = pluginTypes.Keys.ToHashSet();

        //移除无效插件，同时如果有效则剔除
        pluginFolder.RemoveAll(name => currentPlugins.Remove(name) == false);
        //剩下的就是还没有的插件，添加到根目录
        foreach (var typeName in currentPlugins)
            pluginFolder.Strings.Add(typeName);
    }

    void PreloadAllAssemblies()
    {
        var loadedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Assembly>();
        queue.Enqueue(Assembly.GetEntryAssembly()!);
        while (queue.Count > 0)
        {
            var assembly = queue.Dequeue();
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                // 如果这个程序集还没被加载过
                if (!loadedAssemblies.Contains(reference.FullName))
                {
                    try
                    {
                        // 强制加载它
                        var loaded = Assembly.Load(reference);
                        queue.Enqueue(loaded);
                        loadedAssemblies.Add(reference.FullName);
                    }
                    catch
                    {
                        // 忽略加载失败的程序集（有些可能是环境相关的）
                    }
                }
            }
        }
    }
}
