namespace SimGame.Core;

/// <summary>
/// Terrain definition that defines properties of a tile type.
/// </summary>
public sealed class TerrainDef : IContentDef
{
    public int Id { get; set; }
    public float WalkabilityCost { get; init; } = 1.0f;
    public bool BlocksLight { get; init; } = false;
    public string SpriteKey { get; init; } = "";
    public bool IsAutotiling { get; init; } = false; // Indicates terrain uses autotiling
    public bool PaintsToBase { get; init; } = false; // If true, paints to base layer; otherwise overlay
    public int VariantCount { get; init; } = 1; // Number of texture variants (1 = no variation)
}
