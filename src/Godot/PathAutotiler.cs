using Godot;
using SimGame.Core;
using System.Collections.Generic;

namespace SimGame.Godot;

/// <summary>
/// Implements dual-grid autotiling for path terrains.
/// Based on dual-grid-tilemap-system-godot reference project.
///
/// The dual-grid system checks 4 neighbors (corners of a dual-grid cell)
/// to determine which of 16 tile variants to display.
/// </summary>
public static class PathAutotiler
{
	// Atlas coordinates for each tile variant in PathGrid.png (64x64, 4x4 grid of 16x16 tiles)
	// Format: (topLeft, topRight, bottomLeft, bottomRight) -> (atlasX, atlasY)
	// Coordinates match the reference dual-grid-tilemap-system-godot project
	private static readonly Dictionary<(bool, bool, bool, bool), Vector2I> NeighborPatterns = new()
	{
		// All corners path
		{(true, true, true, true), new Vector2I(2, 1)},

		// Outer corners (3 background, 1 path)
		{(false, false, false, true), new Vector2I(1, 3)},  // Outer bottom-right corner
		{(false, false, true, false), new Vector2I(0, 0)},  // Outer bottom-left corner
		{(false, true, false, false), new Vector2I(0, 2)},  // Outer top-right corner
		{(true, false, false, false), new Vector2I(3, 3)},  // Outer top-left corner

		// Edges (2 background, 2 path)
		{(false, true, false, true), new Vector2I(1, 0)},   // Right edge
		{(true, false, true, false), new Vector2I(3, 2)},   // Left edge
		{(false, false, true, true), new Vector2I(3, 0)},   // Bottom edge
		{(true, true, false, false), new Vector2I(1, 2)},   // Top edge

		// Inner corners (1 background, 3 path)
		{(false, true, true, true), new Vector2I(1, 1)},    // Inner bottom-right corner
		{(true, false, true, true), new Vector2I(2, 0)},    // Inner bottom-left corner
		{(true, true, false, true), new Vector2I(2, 2)},    // Inner top-right corner
		{(true, true, true, false), new Vector2I(3, 1)},    // Inner top-left corner

		// Diagonal corners
		{(false, true, true, false), new Vector2I(2, 3)},   // Bottom-left top-right corners
		{(true, false, false, true), new Vector2I(0, 1)},   // Top-left bottom-right corners

		// No corners (all background)
		{(false, false, false, false), new Vector2I(0, 3)},
	};

	/// <summary>
	/// Calculate which tile variant to use based on the 4 dual-grid neighbors.
	/// </summary>
	/// <param name="world">World containing tiles</param>
	/// <param name="coord">Coordinate to calculate tile for</param>
	/// <param name="pathTerrainId">Terrain ID that represents paths</param>
	/// <returns>Atlas coordinates (x, y) in the 4x4 grid</returns>
	public static Vector2I CalculateTileVariant(World world, TileCoord coord, int pathTerrainId)
	{
		// Match reference implementation: treat tile at (x,y) as bottom-right of 4-tile intersection
		// Check world tiles at (x-1,y-1), (x,y-1), (x-1,y), (x,y)
		bool topLeft = IsPathTile(world, new TileCoord(coord.X - 1, coord.Y - 1), pathTerrainId);
		bool topRight = IsPathTile(world, new TileCoord(coord.X, coord.Y - 1), pathTerrainId);
		bool bottomLeft = IsPathTile(world, new TileCoord(coord.X - 1, coord.Y), pathTerrainId);
		bool bottomRight = IsPathTile(world, new TileCoord(coord.X, coord.Y), pathTerrainId);

		var pattern = (topLeft, topRight, bottomLeft, bottomRight);

		if (NeighborPatterns.TryGetValue(pattern, out var atlasCoord))
		{
			return atlasCoord;
		}

		// Fallback to "all corners" tile
		GD.PushWarning($"PathAutotiler: Unknown pattern {pattern} at ({coord.X},{coord.Y}), using default");
		return new Vector2I(2, 1);
	}

	private static bool IsPathTile(World world, TileCoord coord, int pathTerrainId)
	{
		if (!world.IsInBounds(coord))
			return false;

		var tile = world.GetTile(coord);
		return tile.TerrainTypeId == pathTerrainId;
	}
}
