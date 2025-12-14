using System.Collections.Generic;

namespace SimGame.Core;

/// <summary>
/// Holds all loaded content definitions (buffs, needs, objects).
/// This is an instance-based replacement for the static ContentDatabase,
/// allowing multiple simulations with different content and better test isolation.
/// </summary>
public sealed class ContentRegistry
{
    private readonly ContentStore<BuffDef> _buffs = new();
    private readonly ContentStore<NeedDef> _needs = new();
    private readonly ContentStore<ObjectDef> _objects = new();

    /// <summary>All registered buff definitions by ID.</summary>
    public IReadOnlyDictionary<int, BuffDef> Buffs => _buffs.ById;

    /// <summary>All registered need definitions by ID.</summary>
    public IReadOnlyDictionary<int, NeedDef> Needs => _needs.ById;

    /// <summary>All registered object definitions by ID.</summary>
    public IReadOnlyDictionary<int, ObjectDef> Objects => _objects.ById;

    /// <summary>Register a buff definition. ID is auto-assigned if buff.Id is 0.</summary>
    public void RegisterBuff(string key, BuffDef buff) => _buffs.Register(key, buff);

    /// <summary>Register a need definition. ID is auto-assigned if need.Id is 0.</summary>
    public void RegisterNeed(string key, NeedDef need) => _needs.Register(key, need);

    /// <summary>Register an object definition. ID is auto-assigned if obj.Id is 0.</summary>
    public void RegisterObject(string key, ObjectDef obj) => _objects.Register(key, obj);

    /// <summary>Get a buff ID by its key name.</summary>
    public int? GetBuffId(string name) => _buffs.GetId(name);

    /// <summary>Get a need ID by its key name.</summary>
    public int? GetNeedId(string name) => _needs.GetId(name);

    /// <summary>Get an object ID by its key name.</summary>
    public int? GetObjectId(string name) => _objects.GetId(name);
}
