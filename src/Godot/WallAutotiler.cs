using Godot;
using SimGame.Core;

namespace SimGame.Godot;

/// <summary>
/// Delegates wall autotiling logic to SimGame.Core.WallAutotiler for testability.
/// </summary>
public static class WallAutotiler
{
    public static Vector2I CalculateTileVariant(EntityManager entityManager, TileCoord coord, int wallObjectDefId)
    {
        var (x, y) = SimGame.Core.WallAutotiler.CalculateTileVariant(entityManager, coord, wallObjectDefId);
        return new Vector2I(x, y);
    }
}
