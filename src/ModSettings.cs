using System.IO;
using System.Text.Json;

namespace ToastersRinkCompanion;

public class ModSettings
{
    public string spawnPuckKeybind { get; set; } = "<keyboard>/g";
    public string voteYesKeybind { get; set; } = "<keyboard>/f1";
    public string voteNoKeybind { get; set; } = "<keyboard>/f2";
    public string panelKeybind { get; set; } = "<keyboard>/f3";
    public bool showModifiersHUD { get; set; } = true;
    public bool showMinimapObjects { get; set; } = true;
    public int hudPositionX { get; set; } = 0;   // 0-100%, 0=left edge, 100=right edge
    public int hudPositionY { get; set; } = 95;   // 0-100%, 0=top edge, 100=bottom edge

    static string ConfigurationFileName = $"{Plugin.MOD_NAME}.json";

    public static ModSettings Load()
    {
        Plugin.Log($"Loading {ConfigurationFileName}...");
        var path = GetConfigPath();
        var dir = Path.GetDirectoryName(path);

        // 1) make sure "config/" exists
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Plugin.Log($"Created missing /config directory");
        }
        
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<ModSettings>(json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                settings ??= new ModSettings();
                settings.MigrateKeybinds();
                return settings;
            }
            catch (JsonException je)
            {
                Plugin.Log($"Corrupt config JSON, using defaults: {je.Message}");
                return new ModSettings();
            }
        }
        
        var defaults = new ModSettings();
        File.WriteAllText(path,
            JsonSerializer.Serialize(defaults, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
                
        Plugin.Log($"Config file `{path}` did not exist, created with defaults.");
        return defaults;
    }

    public void Save()
    {
        Plugin.Log($"Saving {ConfigurationFileName}...");
        var path = GetConfigPath();
        var dir  = Path.GetDirectoryName(path);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path,
            JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
    }

    public void MigrateKeybinds()
    {
        voteYesKeybind = MigrateKeyValue(voteYesKeybind);
        voteNoKeybind = MigrateKeyValue(voteNoKeybind);
        panelKeybind = MigrateKeyValue(panelKeybind);
    }

    private static string MigrateKeyValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "<keyboard>/f1";
        if (value.StartsWith("<")) return value;
        return $"<keyboard>/{value}";
    }

    public static string GetConfigPath()
    {
        string rootPath = Path.GetFullPath(".");
        string configPath = Path.Combine(rootPath, "config", ConfigurationFileName);
        return configPath;
    }
}