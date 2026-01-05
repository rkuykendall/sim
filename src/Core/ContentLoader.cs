using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoonSharp.Interpreter;

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
    /// Load all content from Lua files and return a populated ContentRegistry.
    /// </summary>
    public static ContentRegistry LoadAll(string contentPath)
    {
        var registry = new ContentRegistry();
        var script = new Script();

        // Load color palettes first (no dependencies)
        LoadColorPalettes(registry, contentPath);

        // Load in order: buffs first (needs reference them), then needs, then terrains, then objects
        LoadBuffs(script, registry, Path.Combine(contentPath, "core", "buffs.lua"));
        LoadNeeds(script, registry, Path.Combine(contentPath, "core", "needs.lua"));
        LoadTerrains(script, registry, Path.Combine(contentPath, "core", "terrains.lua"));
        LoadObjects(script, registry, Path.Combine(contentPath, "core", "objects.lua"));

        Log?.Invoke(
            $"ContentLoader: Loaded {registry.ColorPalettes.Count} color palettes, {registry.Buffs.Count} buffs, {registry.Needs.Count} needs, {registry.Terrains.Count} terrains, {registry.Objects.Count} objects"
        );

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
    private static int? ResolveReference(
        DynValue value,
        Func<string, int?> lookup,
        string contextKey,
        string fieldName
    )
    {
        if (value.IsNil())
            return null;

        var name = value.String;
        return lookup(name)
            ?? throw new InvalidOperationException(
                $"'{contextKey}' references unknown {fieldName} '{name}'"
            );
    }

    private static void LoadBuffs(Script script, ContentRegistry registry, string path)
    {
        var table = LoadLuaTable(script, path, "Buffs");
        if (table == null)
            return;

        foreach (var pair in table.Pairs)
        {
            var key = pair.Key.String;
            var data = pair.Value.Table;

            registry.RegisterBuff(
                key,
                new BuffDef
                {
                    Name = data.Get("name").String,
                    MoodOffset = (float)data.Get("moodOffset").Number,
                    DurationTicks = (int)(
                        data.Get("durationTicks").IsNil() ? 0 : data.Get("durationTicks").Number
                    ),
                    IsFromNeed = !data.Get("isFromNeed").IsNil() && data.Get("isFromNeed").Boolean,
                }
            );
        }
    }

    private static void LoadNeeds(Script script, ContentRegistry registry, string path)
    {
        var table = LoadLuaTable(script, path, "Needs");
        if (table == null)
            return;

        foreach (var pair in table.Pairs)
        {
            var key = pair.Key.String;
            var data = pair.Value.Table;

            registry.RegisterNeed(
                key,
                new NeedDef
                {
                    Name = data.Get("name").String,
                    DecayPerTick = (float)data.Get("decayPerTick").Number,
                    CriticalThreshold = (float)data.Get("criticalThreshold").Number,
                    LowThreshold = (float)data.Get("lowThreshold").Number,
                    CriticalDebuffId = ResolveReference(
                        data.Get("criticalDebuff"),
                        registry.GetBuffId,
                        key,
                        "criticalDebuff"
                    ),
                    LowDebuffId = ResolveReference(
                        data.Get("lowDebuff"),
                        registry.GetBuffId,
                        key,
                        "lowDebuff"
                    ),
                }
            );
        }
    }

    private static void LoadTerrains(Script script, ContentRegistry registry, string path)
    {
        var table = LoadLuaTable(script, path, "Terrains");
        if (table == null)
            return;

        foreach (var pair in table.Pairs)
        {
            var key = pair.Key.String;
            var data = pair.Value.Table;

            // Load passability (defaults to Ground if not specified)
            var passabilityData = data.Get("passability");
            var passability = TerrainPassability.Ground;
            if (!passabilityData.IsNil())
            {
                var passabilityStr = passabilityData.String;
                if (!Enum.TryParse<TerrainPassability>(passabilityStr, true, out passability))
                {
                    throw new InvalidOperationException(
                        $"Terrain '{key}' has invalid passability value '{passabilityStr}'. Must be 'Low', 'Ground', or 'High'."
                    );
                }
            }

            // Load blocksLight property (defaults to false if not specified)
            var blocksLightData = data.Get("blocksLight");
            var blocksLight = !blocksLightData.IsNil() && blocksLightData.Boolean;

            // Load sprite key (defaults to empty string if not specified)
            var spriteKeyData = data.Get("spriteKey");
            var spriteKey = spriteKeyData.IsNil() ? "" : spriteKeyData.String;

            // Load isPath property (defaults to false if not specified)
            var isAutotilingData = data.Get("isAutotiling");
            var isAutotiling = !isAutotilingData.IsNil() && isAutotilingData.Boolean;

            // Load paintsToBase property (defaults to false if not specified)
            var paintsToBaseData = data.Get("paintsToBase");
            var paintsToBase = !paintsToBaseData.IsNil() && paintsToBaseData.Boolean;

            // Load variantCount property (defaults to 1 if not specified)
            var variantCountData = data.Get("variantCount");
            var variantCount = variantCountData.IsNil() ? 1 : (int)variantCountData.Number;

            registry.RegisterTerrain(
                key,
                new TerrainDef
                {
                    Passability = passability,
                    BlocksLight = blocksLight,
                    SpriteKey = spriteKey,
                    IsAutotiling = isAutotiling,
                    PaintsToBase = paintsToBase,
                    VariantCount = variantCount,
                }
            );
        }
    }

    private static void LoadObjects(Script script, ContentRegistry registry, string path)
    {
        var table = LoadLuaTable(script, path, "Objects");
        if (table == null)
            return;

        foreach (var pair in table.Pairs)
        {
            var key = pair.Key.String;
            var data = pair.Value.Table;

            // Derive use areas from walkable property
            var useAreas = new List<(int dx, int dy)>();

            // Load walkable property (defaults to false if not specified)
            var walkableData = data.Get("walkable");
            var walkable = !walkableData.IsNil() && walkableData.Boolean;

            if (walkable)
            {
                useAreas.Add((0, 0));
            }
            else
            {
                useAreas.Add((0, 1));
                useAreas.Add((1, 0));
                useAreas.Add((0, -1));
                useAreas.Add((-1, 0));
                useAreas.Add((1, 1));
                useAreas.Add((1, -1));
                useAreas.Add((-1, 1));
                useAreas.Add((-1, -1));
            }

            // Load sprite key (defaults to empty string if not specified)
            var spriteKeyData = data.Get("spriteKey");
            var spriteKey = spriteKeyData.IsNil() ? "" : spriteKeyData.String;

            var obj = new ObjectDef
            {
                Name = key,
                Walkable = walkable,
                SatisfiesNeedId = ResolveReference(
                    data.Get("satisfiesNeed"),
                    registry.GetNeedId,
                    key,
                    "satisfiesNeed"
                ),
                NeedSatisfactionAmount = (float)data.Get("satisfactionAmount").Number,
                InteractionDurationTicks = (int)data.Get("interactionDuration").Number,
                GrantsBuffId = ResolveReference(
                    data.Get("grantsBuff"),
                    registry.GetBuffId,
                    key,
                    "grantsBuff"
                ),
                UseAreas = useAreas,
                SpriteKey = spriteKey,
            };

            registry.RegisterObject(key, obj);
        }
    }

    private static void LoadColorPalettes(ContentRegistry registry, string palettesPath)
    {
        // Load Lua palettes from core/palettes.lua
        var palettesLuaPath = Path.Combine(palettesPath, "core", "palettes.lua");
        if (!File.Exists(palettesLuaPath))
        {
            LogError?.Invoke($"ContentLoader: Palettes file not found: {palettesLuaPath}");
            return;
        }

        var script = new Script();
        Table? palettesTable = null;
        try
        {
            palettesTable = script.DoFile(palettesLuaPath).Table;
        }
        catch (Exception ex)
        {
            LogError?.Invoke($"ContentLoader: Failed to load palettes.lua: {ex.Message}");
            return;
        }
        if (palettesTable == null)
        {
            LogError?.Invoke($"ContentLoader: palettes.lua did not return a table");
            return;
        }

        foreach (var palettePair in palettesTable.Pairs)
        {
            var key = palettePair.Key.String;
            var colorArray = palettePair.Value.Table;
            if (colorArray == null)
                continue;
            var colors = new List<ColorDef>();
            foreach (var colorValue in colorArray.Values)
            {
                var hex = colorValue.CastToString();
                if (string.IsNullOrWhiteSpace(hex))
                    continue;
                try
                {
                    // Parse hex string (e.g. #RRGGBB)
                    if (hex.StartsWith("#") && hex.Length == 7)
                    {
                        var r = Convert.ToInt32(hex.Substring(1, 2), 16) / 255f;
                        var g = Convert.ToInt32(hex.Substring(3, 2), 16) / 255f;
                        var b = Convert.ToInt32(hex.Substring(5, 2), 16) / 255f;
                        colors.Add(
                            new ColorDef
                            {
                                Name = "",
                                R = r,
                                G = g,
                                B = b,
                            }
                        );
                    }
                    else
                    {
                        LogError?.Invoke($"ColorPalette '{key}' has invalid hex color: {hex}");
                    }
                }
                catch (Exception ex)
                {
                    LogError?.Invoke(
                        $"ColorPalette '{key}' failed to parse color '{hex}': {ex.Message}"
                    );
                }
            }
            if (colors.Count < 1)
            {
                LogError?.Invoke(
                    $"ColorPalette '{key}' must have at least 1 color (has {colors.Count})"
                );
                continue;
            }
            var palette = new ColorPaletteDef { Name = key, Colors = colors };
            registry.RegisterColorPalette(key, palette);
        }
        Log?.Invoke($"ContentLoader: Loaded {registry.ColorPalettes.Count} color palettes");
    }
}
