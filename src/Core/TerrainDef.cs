namespace SimGame.Core;

/// <summary>
/// Terrain definition that defines properties of a tile type.
/// </summary>
public sealed class TerrainDef : IContentDef
{
    public int Id { get; set; }
    public bool Walkable { get; init; } = true;
    public bool Buildable { get; init; } = true;
    public string SpriteKey { get; init; } = "";
    public bool IsPath { get; init; } = false; // Indicates terrain uses autotiling
}
