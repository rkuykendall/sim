using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonSharp.Interpreter;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SimGame.Core;

/// <summary>
/// Loads game content definitions from Lua files into a ContentRegistry.
/// </summary>
public static class ContentLoader
{
    /// <summary>
    /// Optional logging callback. Set to null to disable logging.
    /// </summary>
    public static Action<string>? Log { get; set; }

    /// <summary>
    /// Optional error logging callback. Set to null to disable error logging.
    /// </summary>
    public static Action<string>? LogError { get; set; }

    /// <summary>
    /// Load all content from YAML and Lua files and return a populated ContentRegistry.
    /// </summary>
    public static ContentRegistry LoadAll(string contentPath)
    {
        var registry = new ContentRegistry();
        var script = new Script();

        // Load color palettes first (no dependencies)
        LoadColorPalettes(registry, Path.Combine(contentPath, "palettes"));

        // Load in order: buffs first (needs reference them), then needs, then terrains, then objects
        LoadBuffs(script, registry, Path.Combine(contentPath, "core", "buffs.lua"));
        LoadNeeds(script, registry, Path.Combine(contentPath, "core", "needs.lua"));
        LoadTerrains(script, registry, Path.Combine(contentPath, "core", "terrains.lua"));
        LoadObjects(script, registry, Path.Combine(contentPath, "core", "objects.lua"));

        Log?.Invoke($"ContentLoader: Loaded {registry.ColorPalettes.Count} color palettes, {registry.Buffs.Count} buffs, {registry.Needs.Count} needs, {registry.Terrains.Count} terrains, {registry.Objects.Count} objects");

        return registry;
    }

    /// <summary>
    /// Load a Lua file and iterate over its table entries.
    /// Returns null if file doesn't exist.
    /// </summary>
    private static Table? LoadLuaTable(Script script, string path, string contentType)
    {
        if (!File.Exists(path))
        {
            LogError?.Invoke($"ContentLoader: {contentType} file not found: {path}");
            return null;
        }
        return script.DoFile(path).Table;
    }

    /// <summary>
    /// Resolve a reference from Lua data to a registered ID, with strict validation.
    /// </summary>
    private static int? ResolveReference(DynValue value, Func<string, int?> lookup, string contextKey, string fieldName)
    {
        if (value.IsNil()) return null;
        
        var name = value.String;
        return lookup(name) 
            ?? throw new InvalidOperationException($"'{contextKey}' references unknown {fieldName} '{name}'");
    }

    private static void LoadBuffs(Script script, ContentRegistry registry, string path)
    {
        var table = LoadLuaTable(script, path, "Buffs");
        if (table == null) return;

        foreach (var pair in table.Pairs)
        {
            var key = pair.Key.String;
            var data = pair.Value.Table;

            registry.RegisterBuff(key, new BuffDef
            {
                Name = data.Get("name").String,
                MoodOffset = (float)data.Get("moodOffset").Number,
                DurationTicks = (int)(data.Get("durationTicks").IsNil() ? 0 : data.Get("durationTicks").Number),
                IsFromNeed = !data.Get("isFromNeed").IsNil() && data.Get("isFromNeed").Boolean
            });
        }
    }

    private static void LoadNeeds(Script script, ContentRegistry registry, string path)
    {
        var table = LoadLuaTable(script, path, "Needs");
        if (table == null) return;

        foreach (var pair in table.Pairs)
        {
            var key = pair.Key.String;
            var data = pair.Value.Table;

            registry.RegisterNeed(key, new NeedDef
            {
                Name = data.Get("name").String,
                DecayPerTick = (float)data.Get("decayPerTick").Number,
                CriticalThreshold = (float)data.Get("criticalThreshold").Number,
                LowThreshold = (float)data.Get("lowThreshold").Number,
                CriticalDebuffId = ResolveReference(data.Get("criticalDebuff"), registry.GetBuffId, key, "criticalDebuff"),
                LowDebuffId = ResolveReference(data.Get("lowDebuff"), registry.GetBuffId, key, "lowDebuff")
            });
        }
    }

    private static void LoadTerrains(Script script, ContentRegistry registry, string path)
    {
        var table = LoadLuaTable(script, path, "Terrains");
        if (table == null) return;

        foreach (var pair in table.Pairs)
        {
            var key = pair.Key.String;
            var data = pair.Value.Table;

            // Load walkable property (defaults to true if not specified)
            var walkableData = data.Get("walkable");
            var walkable = walkableData.IsNil() || walkableData.Boolean;

            // Load buildable property (defaults to true if not specified)
            var buildableData = data.Get("buildable");
            var buildable = buildableData.IsNil() || buildableData.Boolean;

            // Load indoors property (defaults to false if not specified)
            var indoorsData = data.Get("indoors");
            var indoors = !indoorsData.IsNil() && indoorsData.Boolean;

            // Load sprite key (defaults to empty string if not specified)
            var spriteKeyData = data.Get("spriteKey");
            var spriteKey = spriteKeyData.IsNil() ? "" : spriteKeyData.String;

            // Load isPath property (defaults to false if not specified)
            var isPathData = data.Get("isPath");
            var isPath = !isPathData.IsNil() && isPathData.Boolean;

            registry.RegisterTerrain(key, new TerrainDef
            {
                Name = data.Get("name").String,
                Walkable = walkable,
                Buildable = buildable,
                Indoors = indoors,
                SpriteKey = spriteKey,
                IsPath = isPath
            });
        }
    }

    private static void LoadObjects(Script script, ContentRegistry registry, string path)
    {
        var table = LoadLuaTable(script, path, "Objects");
        if (table == null) return;

        foreach (var pair in table.Pairs)
        {
            var key = pair.Key.String;
            var data = pair.Value.Table;

            // Load use areas first
            var useAreas = new List<(int dx, int dy)>();
            var useAreasData = data.Get("useAreas");
            if (!useAreasData.IsNil() && useAreasData.Type == DataType.Table)
            {
                foreach (var areaPair in useAreasData.Table.Pairs)
                {
                    var areaTable = areaPair.Value.Table;
                    useAreas.Add(((int)areaTable.Get(1).Number, (int)areaTable.Get(2).Number));
                }
            }

            // Load walkable property (defaults to false if not specified)
            var walkableData = data.Get("walkable");
            var walkable = !walkableData.IsNil() && walkableData.Boolean;

            // Load sprite key (defaults to empty string if not specified)
            var spriteKeyData = data.Get("spriteKey");
            var spriteKey = spriteKeyData.IsNil() ? "" : spriteKeyData.String;

            var obj = new ObjectDef
            {
                Name = data.Get("name").String,
                Walkable = walkable,
                SatisfiesNeedId = ResolveReference(data.Get("satisfiesNeed"), registry.GetNeedId, key, "satisfiesNeed"),
                NeedSatisfactionAmount = (float)data.Get("satisfactionAmount").Number,
                InteractionDurationTicks = (int)data.Get("interactionDuration").Number,
                GrantsBuffId = ResolveReference(data.Get("grantsBuff"), registry.GetBuffId, key, "grantsBuff"),
                UseAreas = useAreas,
                SpriteKey = spriteKey
            };

            registry.RegisterObject(key, obj);
        }
    }

    private static void LoadColorPalettes(ContentRegistry registry, string palettesPath)
    {
        if (!Directory.Exists(palettesPath))
        {
            LogError?.Invoke($"ContentLoader: Palettes directory not found: {palettesPath}");
            return;
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var paletteFiles = Directory.GetFiles(palettesPath, "*.yaml");

        foreach (var file in paletteFiles)
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var paletteData = deserializer.Deserialize<PaletteYaml>(yaml);

                if (paletteData.Colors.Count != 12)
                {
                    LogError?.Invoke($"ColorPalette '{Path.GetFileNameWithoutExtension(file)}' must have exactly 12 colors (has {paletteData.Colors.Count})");
                    continue;
                }

                var palette = new ColorPaletteDef
                {
                    Name = paletteData.Name,
                    Description = paletteData.Description ?? "",
                    Colors = paletteData.Colors.Select(c => new ColorDef
                    {
                        Name = c.Name,
                        R = c.R,
                        G = c.G,
                        B = c.B
                    }).ToList()
                };

                var key = Path.GetFileNameWithoutExtension(file);
                registry.RegisterColorPalette(key, palette);
            }
            catch (Exception ex)
            {
                LogError?.Invoke($"ContentLoader: Failed to load palette {file}: {ex.Message}");
            }
        }

        Log?.Invoke($"ContentLoader: Loaded {registry.ColorPalettes.Count} color palettes");
    }

    // YAML deserialization classes
    private sealed class PaletteYaml
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public List<ColorYaml> Colors { get; set; } = new();
    }

    private sealed class ColorYaml
    {
        public string Name { get; set; } = "";
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }
    }
}
