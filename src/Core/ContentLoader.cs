using System;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;

namespace SimGame.Core;

/// <summary>
/// Loads game content definitions from Lua files.
/// </summary>
public static class ContentLoader
{
    private static Script? _script;
    private static Dictionary<string, int> _buffNameToId = new();
    private static Dictionary<string, int> _needNameToId = new();

    /// <summary>
    /// Optional logging callback. Set to null to disable logging.
    /// Defaults to Godot.GD.Print when running in Godot, null otherwise.
    /// </summary>
    public static Action<string>? Log { get; set; }

    /// <summary>
    /// Optional error logging callback. Set to null to disable error logging.
    /// </summary>
    public static Action<string>? LogError { get; set; }

    public static void LoadAll(string contentPath)
    {
        _script = new Script();
        _buffNameToId.Clear();
        _needNameToId.Clear();

        // Load in order: buffs first (needs reference them), then needs, then objects
        LoadBuffs(Path.Combine(contentPath, "core", "buffs.lua"));
        LoadNeeds(Path.Combine(contentPath, "core", "needs.lua"));
        LoadObjects(Path.Combine(contentPath, "core", "objects.lua"));

        Log?.Invoke($"ContentLoader: Loaded {ContentDatabase.Buffs.Count} buffs, {ContentDatabase.Needs.Count} needs, {ContentDatabase.Objects.Count} objects");
    }

    /// <summary>
    /// Clear all loaded content. Useful for test isolation.
    /// </summary>
    public static void ClearAll()
    {
        ContentDatabase.Buffs.Clear();
        ContentDatabase.Needs.Clear();
        ContentDatabase.Objects.Clear();
        _buffNameToId.Clear();
        _needNameToId.Clear();
    }

    /// <summary>
    /// Register a buff definition directly (for testing without Lua files).
    /// </summary>
    public static void RegisterBuff(string key, BuffDef buff)
    {
        ContentDatabase.Buffs[buff.Id] = buff;
        _buffNameToId[key] = buff.Id;
    }

    /// <summary>
    /// Register a need definition directly (for testing without Lua files).
    /// </summary>
    public static void RegisterNeed(string key, NeedDef need)
    {
        ContentDatabase.Needs[need.Id] = need;
        _needNameToId[key] = need.Id;
    }

    /// <summary>
    /// Register an object definition directly (for testing without Lua files).
    /// </summary>
    public static void RegisterObject(string key, ObjectDef obj)
    {
        ContentDatabase.Objects[obj.Id] = obj;
    }

    private static void LoadBuffs(string path)
    {
        if (!File.Exists(path))
        {
            LogError?.Invoke($"ContentLoader: Buffs file not found: {path}");
            return;
        }

        ContentDatabase.Buffs.Clear();

        var result = _script!.DoFile(path);
        var buffsTable = result.Table;

        foreach (var pair in buffsTable.Pairs)
        {
            string key = pair.Key.String;
            var data = pair.Value.Table;

            int id = (int)data.Get("id").Number;
            var buff = new BuffDef
            {
                Id = id,
                Name = data.Get("name").String,
                MoodOffset = (float)data.Get("moodOffset").Number,
                DurationTicks = (int)(data.Get("durationTicks").IsNil() ? 0 : data.Get("durationTicks").Number),
                IsFromNeed = !data.Get("isFromNeed").IsNil() && data.Get("isFromNeed").Boolean
            };

            ContentDatabase.Buffs[id] = buff;
            _buffNameToId[key] = id;
        }
    }

    private static void LoadNeeds(string path)
    {
        if (!File.Exists(path))
        {
            LogError?.Invoke($"ContentLoader: Needs file not found: {path}");
            return;
        }

        ContentDatabase.Needs.Clear();

        var result = _script!.DoFile(path);
        var needsTable = result.Table;

        foreach (var pair in needsTable.Pairs)
        {
            string key = pair.Key.String;
            var data = pair.Value.Table;

            int id = (int)data.Get("id").Number;
            
            // Look up debuff IDs by name
            int? criticalDebuffId = null;
            int? lowDebuffId = null;
            
            var criticalDebuffName = data.Get("criticalDebuff");
            if (!criticalDebuffName.IsNil() && _buffNameToId.TryGetValue(criticalDebuffName.String, out var critId))
                criticalDebuffId = critId;
            
            var lowDebuffName = data.Get("lowDebuff");
            if (!lowDebuffName.IsNil() && _buffNameToId.TryGetValue(lowDebuffName.String, out var lowId))
                lowDebuffId = lowId;

            var need = new NeedDef
            {
                Id = id,
                Name = data.Get("name").String,
                DecayPerTick = (float)data.Get("decayPerTick").Number,
                CriticalThreshold = (float)data.Get("criticalThreshold").Number,
                LowThreshold = (float)data.Get("lowThreshold").Number,
                CriticalDebuffId = criticalDebuffId,
                LowDebuffId = lowDebuffId
            };

            ContentDatabase.Needs[id] = need;
            _needNameToId[key] = id;
        }
    }

    private static void LoadObjects(string path)
    {
        if (!File.Exists(path))
        {
            LogError?.Invoke($"ContentLoader: Objects file not found: {path}");
            return;
        }

        ContentDatabase.Objects.Clear();

        var result = _script!.DoFile(path);
        var objectsTable = result.Table;

        foreach (var pair in objectsTable.Pairs)
        {
            string key = pair.Key.String;
            var data = pair.Value.Table;

            int id = (int)data.Get("id").Number;

            // Look up need ID by name
            int? satisfiesNeedId = null;
            var satisfiesNeedName = data.Get("satisfiesNeed");
            if (!satisfiesNeedName.IsNil() && _needNameToId.TryGetValue(satisfiesNeedName.String, out var needId))
                satisfiesNeedId = needId;

            // Look up buff ID by name
            int? grantsBuffId = null;
            var grantsBuffName = data.Get("grantsBuff");
            if (!grantsBuffName.IsNil() && _buffNameToId.TryGetValue(grantsBuffName.String, out var buffId))
                grantsBuffId = buffId;

            var obj = new ObjectDef
            {
                Id = id,
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

            ContentDatabase.Objects[id] = obj;
        }
    }

    /// <summary>
    /// Get a buff ID by its Lua key name.
    /// </summary>
    public static int? GetBuffId(string name) =>
        _buffNameToId.TryGetValue(name, out var id) ? id : null;

    /// <summary>
    /// Get a need ID by its Lua key name.
    /// </summary>
    public static int? GetNeedId(string name) =>
        _needNameToId.TryGetValue(name, out var id) ? id : null;
}
