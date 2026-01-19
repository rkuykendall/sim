using System.Text.Json;
using Godot;

namespace SimGame.Godot;

public sealed class UserSettings
{
    private const string SettingsPath = "user://settings.json";

    public bool Fullscreen { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static UserSettings Load()
    {
        if (!FileAccess.FileExists(SettingsPath))
            return new UserSettings();

        using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
        if (file == null)
            return new UserSettings();

        var json = file.GetAsText();

        try
        {
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, JsonOptions);

        using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"Failed to save settings to: {SettingsPath}");
            return;
        }

        file.StoreString(json);
    }
}
