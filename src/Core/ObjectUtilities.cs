using System.Collections.Generic;

namespace SimGame.Core;

/// <summary>
/// Utility methods for multi-tile object calculations.
/// </summary>
public static class ObjectUtilities
{
    /// <summary>
    /// Calculate all tiles occupied by an object given its anchor position and size.
    /// For a 2x2 object at (5,5), returns [(5,5), (6,5), (5,6), (6,6)].
    /// </summary>
    public static List<TileCoord> GetOccupiedTiles(TileCoord anchorCoord, int tileSize)
    {
        var tiles = new List<TileCoord>();
        for (int dx = 0; dx < tileSize; dx++)
        {
            for (int dy = 0; dy < tileSize; dy++)
            {
                tiles.Add(new TileCoord(anchorCoord.X + dx, anchorCoord.Y + dy));
            }
        }
        return tiles;
    }

    /// <summary>
    /// Calculate all tiles occupied by an object given its anchor position and definition.
    /// </summary>
    public static List<TileCoord> GetOccupiedTiles(TileCoord anchorCoord, ObjectDef objDef)
    {
        return GetOccupiedTiles(anchorCoord, objDef.TileSize);
    }

    /// <summary>
    /// Generate all tiles adjacent to a multi-tile object for use areas.
    /// Returns all tiles that touch any edge of the object's footprint.
    /// </summary>
    public static List<(int dx, int dy)> GenerateUseAreasForSize(int tileSize)
    {
        var useAreas = new List<(int dx, int dy)>();

        // Top edge (y = -1, x from -1 to tileSize)
        for (int x = -1; x <= tileSize; x++)
        {
            useAreas.Add((x, -1));
        }

        // Bottom edge (y = tileSize, x from -1 to tileSize)
        for (int x = -1; x <= tileSize; x++)
        {
            useAreas.Add((x, tileSize));
        }

        // Left edge (x = -1, y from 0 to tileSize-1)
        for (int y = 0; y < tileSize; y++)
        {
            useAreas.Add((-1, y));
        }

        // Right edge (x = tileSize, y from 0 to tileSize-1)
        for (int y = 0; y < tileSize; y++)
        {
            useAreas.Add((tileSize, y));
        }

        return useAreas;
    }
}
