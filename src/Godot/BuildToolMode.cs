using Godot;

namespace SimGame.Godot;

/// <summary>
/// Available build tool modes for painting and modifying the world.
/// </summary>
public enum BuildToolMode
{
    Select,      // Default: click to select pawns/objects (existing behavior)
    PlaceObject, // Place objects (fridge, bed, walls, decorations, etc.)
    PlaceTerrain,// Paint terrain tiles
    Delete       // Remove objects
}

/// <summary>
/// Kidpix-style limited color palette.
/// </summary>
public static class GameColorPalette
{
    public static readonly Color[] Colors = new Color[]
    {
        new Color(0.2f, 0.6f, 0.2f),   // 0: Green (grass)
        new Color(0.5f, 0.3f, 0.1f),   // 1: Brown (dirt)
        new Color(0.7f, 0.7f, 0.7f),   // 2: Light Gray (concrete)
        new Color(0.8f, 0.6f, 0.3f),   // 3: Tan (wood)
        new Color(0.4f, 0.4f, 0.4f),   // 4: Dark Gray (stone)
        new Color(0.2f, 0.4f, 0.8f),   // 5: Blue (water)
        new Color(0.9f, 0.2f, 0.2f),   // 6: Red
        new Color(1.0f, 0.8f, 0.2f),   // 7: Yellow
        new Color(0.6f, 0.3f, 0.6f),   // 8: Purple
        new Color(1.0f, 0.5f, 0.3f),   // 9: Orange
        new Color(0.2f, 0.8f, 0.8f),   // 10: Cyan
        new Color(0.95f, 0.95f, 0.95f) // 11: White
    };
}

/// <summary>
/// Global state for the currently active build tool and selected content.
/// </summary>
public static class BuildToolState
{
    public static BuildToolMode Mode { get; set; } = BuildToolMode.Select;
    public static int? SelectedObjectDefId { get; set; } = null;
    public static int? SelectedTerrainDefId { get; set; } = null;
    public static int SelectedColorIndex { get; set; } = 0; // Default to green
}
