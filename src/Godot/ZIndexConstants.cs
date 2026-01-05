namespace SimGame.Godot;

/// <summary>
/// Z-index constants for controlling draw order of game elements.
/// Higher values draw on top of lower values.
/// </summary>
public static class ZIndexConstants
{
    /// <summary>
    /// Base tile grid nodes.
    /// </summary>
    public const int TileNodes = -10;

    /// <summary>
    /// Non-blocking terrain layers (water, grass, etc.).
    /// </summary>
    public const int TerrainNonBlocking = 0;

    /// <summary>
    /// Blocking terrain layers (walls) and pawns (uses YSort).
    /// </summary>
    public const int TerrainBlockingAndPawns = 1;

    /// <summary>
    /// Game buildings
    /// Drawn above terrain and pawns.
    /// </summary>
    public const int Buildings = 2;

    /// <summary>
    /// UI overlays (cursor preview, debug visualizations).
    /// Always drawn on top of game elements.
    /// </summary>
    public const int UIOverlay = 3;
}
