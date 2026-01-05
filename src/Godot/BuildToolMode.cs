using Godot;

namespace SimGame.Godot;

/// <summary>
/// Available build tool modes for painting and modifying the world.
/// </summary>
public enum BuildToolMode
{
    PlaceBuilding,
    PlaceTerrain,
    FillSquare,
    OutlineSquare,
    FloodFill,
    Delete,
    Select,
}

/// <summary>
/// Utility class for converting Core color definitions to Godot.Color for rendering.
/// </summary>
public static class GameColorPalette
{
    /// <summary>
    /// Convert a Core ColorDef to Godot.Color.
    /// </summary>
    public static Color ToGodotColor(Core.ColorDef colorDef)
    {
        return new Color(colorDef.R, colorDef.G, colorDef.B);
    }

    /// <summary>
    /// Convert a list of Core ColorDefs to Godot.Color array.
    /// </summary>
    public static Color[] ToGodotColors(
        System.Collections.Generic.IReadOnlyList<Core.ColorDef> palette
    )
    {
        var colors = new Color[palette.Count];
        for (int i = 0; i < palette.Count; i++)
        {
            colors[i] = ToGodotColor(palette[i]);
        }
        return colors;
    }
}

/// <summary>
/// Global state for the currently active build tool and selected content.
/// </summary>
public static class BuildToolState
{
    public static BuildToolMode Mode { get; set; } = BuildToolMode.PlaceTerrain;
    public static int? SelectedBuildingDefId { get; set; } = null;
    public static int? SelectedTerrainDefId { get; set; } = null;
    public static int SelectedColorIndex { get; set; } = 0;
}
