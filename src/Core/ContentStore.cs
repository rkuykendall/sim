using System.Collections.Generic;

namespace SimGame.Core;

/// <summary>
/// Interface for content definitions that have an auto-assigned ID.
/// </summary>
public interface IContentDef
{
    int Id { get; set; }
}

/// <summary>
/// Generic store for content definitions with auto-ID assignment and name lookup.
/// </summary>
public sealed class ContentStore<T> where T : IContentDef
{
    private readonly Dictionary<int, T> _byId = new();
    private readonly Dictionary<string, int> _nameToId = new();
    private int _nextId = 1;

    /// <summary>
    /// All registered definitions by ID.
    /// </summary>
    public IReadOnlyDictionary<int, T> ById => _byId;

    /// <summary>
    /// Number of registered definitions.
    /// </summary>
    public int Count => _byId.Count;

    /// <summary>
    /// Register a definition. ID is auto-assigned if def.Id is 0.
    /// </summary>
    public void Register(string key, T def)
    {
        if (def.Id == 0)
            def.Id = _nextId++;
        else if (def.Id >= _nextId)
            _nextId = def.Id + 1;

        _byId[def.Id] = def;
        _nameToId[key] = def.Id;
    }

    /// <summary>
    /// Get an ID by its key name, or null if not found.
    /// </summary>
    public int? GetId(string name) =>
        _nameToId.TryGetValue(name, out var id) ? id : null;

    /// <summary>
    /// Get a definition by ID, or default if not found.
    /// </summary>
    public T? Get(int id) =>
        _byId.TryGetValue(id, out var def) ? def : default;

    /// <summary>
    /// Try to get a definition by ID.
    /// </summary>
    public bool TryGet(int id, out T? def) =>
        _byId.TryGetValue(id, out def);
}
