using System.Text.Json;

namespace CapsLockRemap;

internal sealed record Configuration
{
    public int Version { get; init; } = 1;
    public bool Enabled { get; init; } = true;
    public bool Control { get; init; } = true;
    public bool Alt { get; init; } = true;
    public bool Shift { get; init; } = true;
    public bool Windows { get; init; }
    public int TapKey { get; init; }
    public int TapMilliseconds { get; init; } = 200;
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CapsLockRemap");
    public static string FilePath => Path.Combine(Folder, "settings.json");
    public void Validate()
    {
        if (Version != 1 || TapMilliseconds is < 50 or > 1000 || !TapChoices.ContainsKey(TapKey))
            throw new InvalidDataException("Unsupported settings version, tap key, or timing.");
    }
    public static readonly Dictionary<int, string> TapChoices = new() { [0] = "Do nothing", [27] = "Escape", [9] = "Tab", [13] = "Enter", [8] = "Backspace", [32] = "Space" };
    public static Configuration Load()
    {
        if (!File.Exists(FilePath)) return new();
        var result = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("Empty settings.");
        result.Validate();
        return result;
    }
    public void Save()
    {
        Validate();
        Directory.CreateDirectory(Folder);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
