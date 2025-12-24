using Godot;
using Godot.Collections;

namespace SimGame.Godot;

/// <summary>
/// Extended TileMapLayer that supports persistent per-tile color modulation.
/// Based on: https://www.reddit.com/r/godot/comments/sdypwa/how_to_modulate_specific_tiles_in_a_tilemap/
/// </summary>
public partial class ModulatableTileMapLayer : TileMapLayer
{
    // Store colors persistently so they survive terrain updates
    private Dictionary<Vector2I, Color> _tileColors = new();

    /// <summary>
    /// Set the color modulation for a specific tile. Color persists across terrain updates.
    /// </summary>
    /// <param name="coords">Tile coordinates</param>
    /// <param name="color">Color to apply to the tile</param>
    public void SetTileColor(Vector2I coords, Color color)
    {
        _tileColors[coords] = color;
        NotifyRuntimeTileDataUpdate();
    }

    /// <summary>
    /// Clear the color for a specific tile.
    /// </summary>
    /// <param name="coords">Tile coordinates</param>
    public void ClearTileColor(Vector2I coords)
    {
        _tileColors.Remove(coords);
    }

    /// <summary>
    /// Override to indicate which tiles need runtime updates.
    /// </summary>
    public override bool _UseTileDataRuntimeUpdate(Vector2I coords)
    {
        return _tileColors.ContainsKey(coords);
    }

    /// <summary>
    /// Override to apply runtime updates to tile data.
    /// Applies the stored color to the tile data. Does NOT remove the color
    /// so it persists across terrain updates.
    /// </summary>
    public override void _TileDataRuntimeUpdate(Vector2I coords, TileData tileData)
    {
        if (!_tileColors.ContainsKey(coords))
            return;

        tileData.Modulate = _tileColors[coords];
    }
}
