using System.Collections.Generic;
using SimGame.Core;

namespace SimGame.Core
{
    /// <summary>
    /// Provides autotiling logic for wall objects, independent of Godot.
    /// </summary>
    public static class WallAutotiler
    {
        // Atlas coordinates for wall variants
        public static readonly Dictionary<
            (bool, bool, bool, bool),
            (int x, int y)
        > NeighborPatterns = new()
        {
            // TODO: Copy patterns from PathAutotiler once wall sprites are ready
            { (true, true, true, true), (2, 1) },
            // ... other patterns
        };

        /// <summary>
        /// Calculate which wall tile variant to use based on 4 dual-grid neighbors.
        /// </summary>
        /// <param name="entityManager">EntityManager containing wall objects</param>
        /// <param name="coord">Coordinate to calculate tile for</param>
        /// <param name="wallObjectDefId">Object definition ID for walls</param>
        /// <returns>Atlas coordinates (x, y) in the 4x4 grid</returns>
        public static (int x, int y) CalculateTileVariant(
            EntityManager entityManager,
            TileCoord coord,
            int wallObjectDefId
        )
        {
            // TODO: Implement neighbor checks using entityManager
            // Temporary: return default tile
            return (2, 1);
        }
    }
}
