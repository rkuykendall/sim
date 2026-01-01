namespace SimGame.Godot;

/// <summary>
/// Shared constants for rendering sprites and tiles.
/// </summary>
public static class RenderingConstants
{
    /// <summary>
    /// Source sprite/texture tile size in pixels (before scaling).
    /// </summary>
    public const int SourceTileSize = 16;

    /// <summary>
    /// Rendered tile size in pixels (after scaling).
    /// </summary>
    public const int RenderedTileSize = 32;

    /// <summary>
    /// Scale factor applied to sprites (SourceTileSize * SpriteScale = RenderedTileSize).
    /// </summary>
    public const float SpriteScale = 2.0f;

    /// <summary>
    /// Number of variant tiles per row in variant terrain atlases.
    /// </summary>
    public const int VariantsPerRow = 2;
}
