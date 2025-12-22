using System.Collections.Generic;

namespace SimGame.Core
{
    /// <summary>
    /// Provides autotiling logic for path terrains, independent of Godot.
    /// </summary>
    public static class PathAutotiler
    {
        // Atlas coordinates for each tile variant in PathGrid.png (64x64, 4x4 grid of 16x16 tiles)
        public static readonly Dictionary<(bool, bool, bool, bool), (int x, int y)> NeighborPatterns = new()
        {
            {(true, true, true, true), (2, 1)},
            {(false, false, false, true), (1, 3)},
            {(false, false, true, false), (0, 0)},
            {(false, true, false, false), (0, 2)},
            {(true, false, false, false), (3, 3)},
            {(false, true, false, true), (1, 0)},
            {(true, false, true, false), (3, 2)},
            {(false, false, true, true), (3, 0)},
            {(true, true, false, false), (1, 2)},
            {(false, true, true, true), (1, 1)},
            {(true, false, true, true), (2, 0)},
            {(true, true, false, true), (2, 2)},
            {(true, true, true, false), (3, 1)},
            {(false, true, true, false), (2, 3)},
            {(true, false, false, true), (0, 1)},
            {(false, false, false, false), (0, 3)},
        };

        /// <summary>
        /// Calculate which tile variant to use based on the 4 dual-grid neighbors.
        /// </summary>
        /// <param name="world">World containing tiles</param>
        /// <param name="coord">Coordinate to calculate tile for</param>
        /// <param name="pathTerrainId">Terrain ID that represents paths</param>
        /// <returns>Atlas coordinates (x, y) in the 4x4 grid</returns>
        public static (int x, int y) CalculateTileVariant(World world, TileCoord coord, int pathTerrainId)
        {
            bool topLeft = IsAutotilingTile(world, new TileCoord(coord.X - 1, coord.Y - 1), pathTerrainId);
            bool topRight = IsAutotilingTile(world, new TileCoord(coord.X, coord.Y - 1), pathTerrainId);
            bool bottomLeft = IsAutotilingTile(world, new TileCoord(coord.X - 1, coord.Y), pathTerrainId);
            bool bottomRight = IsAutotilingTile(world, new TileCoord(coord.X, coord.Y), pathTerrainId);

            var pattern = (topLeft, topRight, bottomLeft, bottomRight);
            if (NeighborPatterns.TryGetValue(pattern, out var atlasCoord))
            {
                return atlasCoord;
            }
            // Fallback to "all corners" tile
            return (2, 1);
        }

        private static bool IsAutotilingTile(World world, TileCoord coord, int pathTerrainId)
        {
            if (!world.IsInBounds(coord))
                return false;
            var tile = world.GetTile(coord);
            return tile.OverlayTerrainTypeId.HasValue && tile.OverlayTerrainTypeId.Value == pathTerrainId;
        }
    }
}
