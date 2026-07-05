using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace OpenSpace.Core;

public sealed class AppConfig
{
    public HotkeySetting ToggleOverlay { get; set; } = new(ModifierKeys.Windows, Key.OemTilde);
    public HotkeySetting NextWindow { get; set; } = new(ModifierKeys.None, Key.Tab);
    public HotkeySetting PreviousWindow { get; set; } = new(ModifierKeys.Shift, Key.Tab);
    public HotkeySetting ActivateWindow { get; set; } = new(ModifierKeys.None, Key.Enter);
    public HotkeySetting CloseOverlay { get; set; } = new(ModifierKeys.None, Key.Escape);
    public HotkeySetting PanLeft { get; set; } = new(ModifierKeys.None, Key.Left);
    public HotkeySetting PanRight { get; set; } = new(ModifierKeys.None, Key.Right);
    public HotkeySetting PanUp { get; set; } = new(ModifierKeys.None, Key.Up);
    public HotkeySetting PanDown { get; set; } = new(ModifierKeys.None, Key.Down);

    public bool StartMinimizedToTray { get; set; } = false;
    public bool CloseToTray { get; set; } = true;
}

public sealed class HotkeySetting
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModifierKeys Modifiers { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Key Key { get; set; }

    public HotkeySetting()
    {
    }

    public HotkeySetting(ModifierKeys modifiers, Key key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    public bool Matches(Key key, ModifierKeys modifiers)
    {
        return Key == key && Modifiers == (modifiers & (ModifierKeys.Windows | ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift));
    }
}

internal static class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OpenSpace",
        "config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
        }

        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var json = JsonSerializer.Serialize(config, Options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
}
