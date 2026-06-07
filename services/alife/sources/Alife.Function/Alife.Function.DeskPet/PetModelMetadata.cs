using System.IO;
using System.Text.Json;

namespace Alife.Function.DeskPet;

public record InteractionItem
{
    public string? Text { get; set; }
    public string? Exp { get; set; }
    public MotionRef? Mtn { get; set; }
}

public record MotionRef(string Group, int Index);

/// <summary>
/// Live2D 模型元数据载体与解析器
/// </summary>
public class PetModelMetadata
{
    public List<string> Expressions { get; } = new();
    public Dictionary<string, (string Group, int Index)> Motions { get; } = new();
    public Dictionary<string, List<InteractionItem>> Interactions { get; } = new();
    public string ModelPath { get; private set; } = string.Empty;

    public static PetModelMetadata Load(string jsonPath)
    {
        PetModelMetadata metadata = new();
        if (File.Exists(jsonPath) == false) return metadata;

        // 设置模型相对路径 (用于 Web 端加载)
        // 假设路径格式为 .../wwwroot/model/...
        int wwwrootIndex = jsonPath.Replace('\\', '/').IndexOf("wwwroot/", StringComparison.Ordinal);
        if (wwwrootIndex != -1)
        {
            metadata.ModelPath = jsonPath.Substring(wwwrootIndex + "wwwroot/".Length).Replace('\\', '/');
        }

        try
        {
            using JsonDocument jsonDoc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            JsonElement root = jsonDoc.RootElement;

            // 1. 解析表达式与动作
            if (root.TryGetProperty("FileReferences", out JsonElement refs))
            {
                if (refs.TryGetProperty("Expressions", out JsonElement exps))
                {
                    foreach (JsonElement exp in exps.EnumerateArray())
                    {
                        if (exp.TryGetProperty("Name", out JsonElement nameProp))
                        {
                            string? name = nameProp.GetString();
                            if (string.IsNullOrEmpty(name) == false) metadata.Expressions.Add(name);
                        }
                    }
                }

                if (refs.TryGetProperty("Motions", out JsonElement motionsJson))
                {
                    foreach (JsonProperty groupProp in motionsJson.EnumerateObject())
                    {
                        string groupName = groupProp.Name;
                        int index = 0;
                        foreach (JsonElement motionItem in groupProp.Value.EnumerateArray())
                        {
                            if (motionItem.TryGetProperty("Name", out JsonElement nameProp))
                            {
                                string? name = nameProp.GetString();
                                if (string.IsNullOrEmpty(name) == false) metadata.Motions[name] = (groupName, index);
                            }
                            index++;
                        }
                    }
                }
            }

            // 2. 解析交互配置
            if (root.TryGetProperty("Interaction", out JsonElement interactJson))
            {
                foreach (JsonProperty poolProp in interactJson.EnumerateObject())
                {
                    metadata.Interactions[poolProp.Name] = JsonSerializer.Deserialize<List<InteractionItem>>(poolProp.Value.GetRawText(), PetProcess.JsonOptions) ?? new();
                }
            }
        }
        catch
        {
            // 解析失败时保持默认空集合，符合“别没事找事”原则，不强行抛出异常
        }

        return metadata;
    }
}
