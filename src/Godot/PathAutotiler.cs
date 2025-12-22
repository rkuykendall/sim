using Godot;
using SimGame.Core;

namespace SimGame.Godot;

/// <summary>
/// Delegates path autotiling logic to SimGame.Core.PathAutotiler for testability.
/// </summary>
public static class PathAutotiler
{
    public static Vector2I CalculateTileVariant(World world, TileCoord coord, int pathTerrainId)
    {
        var (x, y) = SimGame.Core.PathAutotiler.CalculateTileVariant(world, coord, pathTerrainId);
        return new Vector2I(x, y);
    }
}
