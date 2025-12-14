using System.Collections.Generic;

namespace SimGame.Core;

/// <summary>
/// Holds all loaded content definitions (buffs, needs, objects).
/// This is an instance-based replacement for the static ContentDatabase,
/// allowing multiple simulations with different content and better test isolation.
/// </summary>
public sealed class ContentRegistry
{
    public Dictionary<int, BuffDef> Buffs { get; } = new();
    public Dictionary<int, NeedDef> Needs { get; } = new();
    public Dictionary<int, ObjectDef> Objects { get; } = new();

    // Name-to-ID mappings for content lookup
    private readonly Dictionary<string, int> _buffNameToId = new();
    private readonly Dictionary<string, int> _needNameToId = new();
    private readonly Dictionary<string, int> _objectNameToId = new();

    // ID counters for auto-generation
    private int _nextBuffId = 1;
    private int _nextNeedId = 1;
    private int _nextObjectId = 1;

    /// <summary>
    /// Register a buff definition. ID is auto-assigned if buff.Id is 0.
    /// </summary>
    public void RegisterBuff(string key, BuffDef buff)
    {
        if (buff.Id == 0)
            buff.Id = _nextBuffId++;
        else if (buff.Id >= _nextBuffId)
            _nextBuffId = buff.Id + 1;

        Buffs[buff.Id] = buff;
        _buffNameToId[key] = buff.Id;
    }

    /// <summary>
    /// Register a need definition. ID is auto-assigned if need.Id is 0.
    /// </summary>
    public void RegisterNeed(string key, NeedDef need)
    {
        if (need.Id == 0)
            need.Id = _nextNeedId++;
        else if (need.Id >= _nextNeedId)
            _nextNeedId = need.Id + 1;

        Needs[need.Id] = need;
        _needNameToId[key] = need.Id;
    }

    /// <summary>
    /// Register an object definition. ID is auto-assigned if obj.Id is 0.
    /// </summary>
    public void RegisterObject(string key, ObjectDef obj)
    {
        if (obj.Id == 0)
            obj.Id = _nextObjectId++;
        else if (obj.Id >= _nextObjectId)
            _nextObjectId = obj.Id + 1;

        Objects[obj.Id] = obj;
        _objectNameToId[key] = obj.Id;
    }

    /// <summary>
    /// Get a buff ID by its key name.
    /// </summary>
    public int? GetBuffId(string name) =>
        _buffNameToId.TryGetValue(name, out var id) ? id : null;

    /// <summary>
    /// Get a need ID by its key name.
    /// </summary>
    public int? GetNeedId(string name) =>
        _needNameToId.TryGetValue(name, out var id) ? id : null;

    /// <summary>
    /// Get an object ID by its key name.
    /// </summary>
    public int? GetObjectId(string name) =>
        _objectNameToId.TryGetValue(name, out var id) ? id : null;
}
