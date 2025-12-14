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

    private static void LoadBuffs(Script script, ContentRegistry registry, string path)
    {
        if (!File.Exists(path))
        {
            LogError?.Invoke($"ContentLoader: Buffs file not found: {path}");
            return;
        }

        var result = script.DoFile(path);
        var buffsTable = result.Table;

        foreach (var pair in buffsTable.Pairs)
        {
            string key = pair.Key.String;
            var data = pair.Value.Table;

            var buff = new BuffDef
            {
                Id = 0, // Auto-assigned by registry
                Name = data.Get("name").String,
                MoodOffset = (float)data.Get("moodOffset").Number,
                DurationTicks = (int)(data.Get("durationTicks").IsNil() ? 0 : data.Get("durationTicks").Number),
                IsFromNeed = !data.Get("isFromNeed").IsNil() && data.Get("isFromNeed").Boolean
            };

            registry.RegisterBuff(key, buff);
        }
    }

    private static void LoadNeeds(Script script, ContentRegistry registry, string path)
    {
        if (!File.Exists(path))
        {
            LogError?.Invoke($"ContentLoader: Needs file not found: {path}");
            return;
        }

        var result = script.DoFile(path);
        var needsTable = result.Table;

        foreach (var pair in needsTable.Pairs)
        {
            string key = pair.Key.String;
            var data = pair.Value.Table;

            // Look up debuff IDs by name
            int? criticalDebuffId = null;
            int? lowDebuffId = null;

            var criticalDebuffName = data.Get("criticalDebuff");
            if (!criticalDebuffName.IsNil())
                criticalDebuffId = registry.GetBuffId(criticalDebuffName.String);

            var lowDebuffName = data.Get("lowDebuff");
            if (!lowDebuffName.IsNil())
                lowDebuffId = registry.GetBuffId(lowDebuffName.String);

            var need = new NeedDef
            {
                Id = 0, // Auto-assigned by registry
                Name = data.Get("name").String,
                DecayPerTick = (float)data.Get("decayPerTick").Number,
                CriticalThreshold = (float)data.Get("criticalThreshold").Number,
                LowThreshold = (float)data.Get("lowThreshold").Number,
                CriticalDebuffId = criticalDebuffId,
                LowDebuffId = lowDebuffId
            };

            registry.RegisterNeed(key, need);
        }
    }

    private static void LoadObjects(Script script, ContentRegistry registry, string path)
    {
        if (!File.Exists(path))
        {
            LogError?.Invoke($"ContentLoader: Objects file not found: {path}");
            return;
        }

        var result = script.DoFile(path);
        var objectsTable = result.Table;

        foreach (var pair in objectsTable.Pairs)
        {
            string key = pair.Key.String;
            var data = pair.Value.Table;

            // Look up need ID by name
            int? satisfiesNeedId = null;
            var satisfiesNeedName = data.Get("satisfiesNeed");
            if (!satisfiesNeedName.IsNil())
                satisfiesNeedId = registry.GetNeedId(satisfiesNeedName.String);

            // Look up buff ID by name
            int? grantsBuffId = null;
            var grantsBuffName = data.Get("grantsBuff");
            if (!grantsBuffName.IsNil())
                grantsBuffId = registry.GetBuffId(grantsBuffName.String);

            var obj = new ObjectDef
            {
                Id = 0, // Auto-assigned by registry
                Name = data.Get("name").String,
                SatisfiesNeedId = satisfiesNeedId,
                NeedSatisfactionAmount = (float)data.Get("satisfactionAmount").Number,
                InteractionDurationTicks = (int)data.Get("interactionDuration").Number,
                GrantsBuffId = grantsBuffId
            };

            // Load use areas
            var useAreasData = data.Get("useAreas");
            if (!useAreasData.IsNil() && useAreasData.Type == DataType.Table)
            {
                foreach (var areaPair in useAreasData.Table.Pairs)
                {
                    var areaTable = areaPair.Value.Table;
                    int dx = (int)areaTable.Get(1).Number;
                    int dy = (int)areaTable.Get(2).Number;
                    obj.UseAreas.Add((dx, dy));
                }
            }

            registry.RegisterObject(key, obj);
        }
    }
}
