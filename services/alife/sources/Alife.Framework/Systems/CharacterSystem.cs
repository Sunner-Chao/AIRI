using Newtonsoft.Json.Linq;

namespace Alife.Framework;

public class CharacterSystem : IDisposable
{
    public event Action? OnChanged;

    public List<Character> GetAllCharacters()
    {
        return characters;
    }

    public Character CreateCharacter(string name)
    {
        name = SanitizeName(name);

        // 如果重名，补充后缀
        string uniqueName = name;
        int index = 1;
        while (characters.Any(c => c.Name == uniqueName))
            uniqueName = $"{name}_{index++}";

        Character character = new Character { Name = uniqueName };
        characters.Add(character);
        OnChanged?.Invoke();
        return character;

        static string SanitizeName(string name)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
                name = name.Replace(c, '_');
            return name;
        }
    }
    public void DeleteCharacter(Character character)
    {
        storageSystem.DeleteObject($"{character.StorageKey}/index");
        characters.Remove(character);
        OnChanged?.Invoke();
    }

    public void SaveCharacters()
    {
        foreach (Character character in characters)
            SaveCharacter(character);
    }
    public void SaveCharacter(Character character)
    {
        JObject jObject = JObject.FromObject(character);
        storageSystem.SetObject($"Character/{character.Name}/index", jObject);
    }

    readonly StorageSystem storageSystem;
    readonly List<Character> characters;

    public CharacterSystem(StorageSystem storageSystem)
    {
        this.storageSystem = storageSystem;
        characters = new List<Character>();

        LoadCharacters();
    }
    public void Dispose()
    {
        SaveCharacters();
    }

    void LoadCharacters()
    {
        characters.Clear();

        string[] folder = storageSystem.GetFolders("Character");
        foreach (string name in folder)
        {
            Character? character = LoadCharacter(name);
            if (character != null)
                characters.Add(character);
        }
    }
    Character? LoadCharacter(string name)
    {
        JObject? jObject = storageSystem.GetObject<JObject>(Path.Combine("Character", name, "index"));
        if (jObject == null)
            return null;
        return jObject.ToObject<Character>();
    }
}
