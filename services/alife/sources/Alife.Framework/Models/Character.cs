namespace Alife.Framework;

public class Character : ICloneable
{
    public DateTime Birthday { get; init; } = DateTime.Now;
    public string Name { get; init; } = "真央";
    public string Description { get; set; } = "一只喜欢说话带喵的猫娘伙伴";
    public string Prompt { get; set; } = "";
    public HashSet<string> Plugins { get; set; } = new();
    public bool AutoActivate { get; set; }
    public string StorageKey => $"Character\\{Name}";

    public object Clone()
    {
        return new Character()
        {
            Birthday = Birthday,
            Name = Name,
            Prompt = Prompt,
            Description = Description,
            Plugins = [.. Plugins],
            AutoActivate = AutoActivate,
        };
    }
}