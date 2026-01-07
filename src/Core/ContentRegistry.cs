using System.Collections.Generic;

namespace SimGame.Core;

/// <summary>
/// Holds all loaded content definitions (needs, buildings, terrains).
/// This is an instance-based replacement for the static ContentDatabase,
/// allowing multiple simulations with different content and better test isolation.
/// </summary>
public sealed class ContentRegistry
{
    private readonly ContentStore<NeedDef> _needs = new();
    private readonly ContentStore<BuildingDef> _buildings = new();
    private readonly ContentStore<TerrainDef> _terrains = new();
    private readonly ContentStore<ColorPaletteDef> _colorPalettes = new();

    /// <summary>All registered need definitions by ID.</summary>
    public IReadOnlyDictionary<int, NeedDef> Needs => _needs.ById;

    /// <summary>All registered building definitions by ID.</summary>
    public IReadOnlyDictionary<int, BuildingDef> Buildings => _buildings.ById;

    /// <summary>All registered terrain definitions by ID.</summary>
    public IReadOnlyDictionary<int, TerrainDef> Terrains => _terrains.ById;

    /// <summary>All registered color palette definitions by ID.</summary>
    public IReadOnlyDictionary<int, ColorPaletteDef> ColorPalettes => _colorPalettes.ById;

    /// <summary>Register a need definition. ID is auto-assigned if need.Id is 0.</summary>
    public int RegisterNeed(string key, NeedDef need) => _needs.Register(key, need);

    /// <summary>Register a building definition. ID is auto-assigned if building.Id is 0.</summary>
    public int RegisterBuilding(string key, BuildingDef building) =>
        _buildings.Register(key, building);

    /// <summary>Register a terrain definition. ID is auto-assigned if terrain.Id is 0.</summary>
    public int RegisterTerrain(string key, TerrainDef terrain) => _terrains.Register(key, terrain);

    /// <summary>Register a color palette definition. ID is auto-assigned if palette.Id is 0.</summary>
    public int RegisterColorPalette(string key, ColorPaletteDef palette) =>
        _colorPalettes.Register(key, palette);

    /// <summary>Get a need ID by its key name.</summary>
    public int? GetNeedId(string name) => _needs.GetId(name);

    /// <summary>Get a building ID by its key name.</summary>
    public int? GetBuildingId(string name) => _buildings.GetId(name);

    /// <summary>Get a terrain ID by its key name.</summary>
    public int? GetTerrainId(string name) => _terrains.GetId(name);

    /// <summary>Get a color palette ID by its key name.</summary>
    public int? GetColorPaletteId(string name) => _colorPalettes.GetId(name);
}
