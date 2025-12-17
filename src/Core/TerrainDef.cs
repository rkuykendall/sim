namespace SimGame.Core;

/// <summary>
/// Terrain definition that defines properties of a tile type.
/// </summary>
public sealed class TerrainDef : IContentDef
{
    public int Id { get; set; }
    public string Name { get; init; } = "";
    public bool Walkable { get; init; } = true;
    public bool Buildable { get; init; } = true;
    public bool Indoors { get; init; } = false;
    public string SpriteKey { get; init; } = "";
}
