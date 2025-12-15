using System;
using System.IO;
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

        // Load in order: buffs first (needs reference them), then needs, then objects
        LoadBuffs(script, registry, Path.Combine(contentPath, "core", "buffs.lua"));
        LoadNeeds(script, registry, Path.Combine(contentPath, "core", "needs.lua"));
        LoadObjects(script, registry, Path.Combine(contentPath, "core", "objects.lua"));

        Log?.Invoke($"ContentLoader: Loaded {registry.Buffs.Count} buffs, {registry.Needs.Count} needs, {registry.Objects.Count} objects");

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

            var obj = new ObjectDef
            {
                Name = data.Get("name").String,
                SatisfiesNeedId = ResolveReference(data.Get("satisfiesNeed"), registry.GetNeedId, key, "satisfiesNeed"),
                NeedSatisfactionAmount = (float)data.Get("satisfactionAmount").Number,
                InteractionDurationTicks = (int)data.Get("interactionDuration").Number,
                GrantsBuffId = ResolveReference(data.Get("grantsBuff"), registry.GetBuffId, key, "grantsBuff"),
                UseAreas = useAreas
            };

            registry.RegisterObject(key, obj);
        }
    }
}
