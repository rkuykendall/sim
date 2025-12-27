using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

public class FillToolTests
{
    [Fact]
    public void FloodFill_ShouldRespectWallBoundaries()
    {
        // Arrange: Create a 10x10 world
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 9); // 10x10 world (0-9)

        var grassId = builder.DefineTerrain(key: "Grass", walkable: true);
        var wallId = builder.DefineTerrain(key: "Wall", walkable: false);
        var floorId = builder.DefineTerrain(key: "Floor", walkable: true);

        var sim = builder.Build();

        // Paint entire world with grass first
        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                sim.PaintTerrain(x, y, grassId, 0);
            }
        }

        // Draw a 4x4 square of walls from (2,2) to (5,5)
        // Top and bottom edges
        for (int x = 2; x <= 5; x++)
        {
            sim.PaintTerrain(x, 2, wallId, 0); // Top edge
            sim.PaintTerrain(x, 5, wallId, 0); // Bottom edge
        }
        // Left and right edges
        for (int y = 2; y <= 5; y++)
        {
            sim.PaintTerrain(2, y, wallId, 0); // Left edge
            sim.PaintTerrain(5, y, wallId, 0); // Right edge
        }

        // Inside the square should still be grass: (3,3), (3,4), (4,3), (4,4)
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(3, 3)).BaseTerrainTypeId);
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(4, 4)).BaseTerrainTypeId);

        // Outside should also be grass
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(0, 0)).BaseTerrainTypeId);
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(7, 7)).BaseTerrainTypeId);

        // Act: Flood fill from inside the square (3,3) with floor
        // This simulates clicking inside the walled area with the fill tool
        FloodFillHelper(sim, new TileCoord(3, 3), floorId, 0);

        // Assert: Only tiles inside the wall square should be floor
        // Inside tiles should be floor
        Assert.Equal(floorId, sim.World.GetTile(new TileCoord(3, 3)).BaseTerrainTypeId);
        Assert.Equal(floorId, sim.World.GetTile(new TileCoord(3, 4)).BaseTerrainTypeId);
        Assert.Equal(floorId, sim.World.GetTile(new TileCoord(4, 3)).BaseTerrainTypeId);
        Assert.Equal(floorId, sim.World.GetTile(new TileCoord(4, 4)).BaseTerrainTypeId);

        // Walls should still be walls
        Assert.Equal(wallId, sim.World.GetTile(new TileCoord(2, 2)).BaseTerrainTypeId);
        Assert.Equal(wallId, sim.World.GetTile(new TileCoord(5, 5)).BaseTerrainTypeId);

        // Outside tiles should still be grass (not filled)
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(0, 0)).BaseTerrainTypeId);
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(7, 7)).BaseTerrainTypeId);
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(6, 3)).BaseTerrainTypeId); // Just outside the wall
    }

    [Fact]
    public void FloodFill_ShouldRespectAutoTilingWallsAsBarriers()
    {
        // BUG: Autotiling terrains (walls) go into overlay layer, but flood fill only checks base layer
        // This causes walls to not act as barriers, and flood fill clears them

        // Arrange: Simple 5x5 world
        var builder = new TestSimulationBuilder();
        var grassId = builder.DefineTerrain(key: "Grass", walkable: true, isAutotiling: false);
        var wallId = builder.DefineTerrain(key: "Wall", walkable: false, isAutotiling: true); // Overlay
        var floorId = builder.DefineTerrain(key: "Floor", walkable: true, isAutotiling: false);
        var sim = builder.Build();

        // Paint everything grass (base layer)
        for (int x = 0; x <= 4; x++)
        for (int y = 0; y <= 4; y++)
            sim.PaintTerrain(x, y, grassId, 0);

        // Draw a wall square from (1,1) to (3,3) - this goes into OVERLAY layer
        for (int x = 1; x <= 3; x++)
        {
            sim.PaintTerrain(x, 1, wallId, 0); // Top
            sim.PaintTerrain(x, 3, wallId, 0); // Bottom
        }
        for (int y = 1; y <= 3; y++)
        {
            sim.PaintTerrain(1, y, wallId, 0); // Left
            sim.PaintTerrain(3, y, wallId, 0); // Right
        }

        // Verify walls are in overlay layer (not base layer)
        var wallTile = sim.World.GetTile(new TileCoord(1, 1));
        Assert.Equal(wallId, wallTile.OverlayTerrainTypeId);
        Assert.Equal(grassId, wallTile.BaseTerrainTypeId); // Base is still grass!

        // Act: Flood fill from inside the square (2,2) with floor
        FloodFillHelper(sim, new TileCoord(2, 2), floorId, 0);

        // Assert: Walls should still exist (not be removed)
        var wallTileAfter = sim.World.GetTile(new TileCoord(1, 1));
        Assert.Equal(wallId, wallTileAfter.OverlayTerrainTypeId); // THIS WILL FAIL - wall gets cleared!

        // And outside grass should NOT be filled (walls should block flood)
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(0, 0)).BaseTerrainTypeId); // THIS WILL FAIL - gets filled!
    }

    // Helper method that implements flood fill logic (copied from GameRoot.cs)
    // This will be replaced with a proper API on Simulation later
    private void FloodFillHelper(
        Simulation sim,
        TileCoord start,
        int newTerrainId,
        int newColorIndex
    )
    {
        if (!sim.World.IsInBounds(start))
            return;

        var world = sim.World;
        var tile = world.GetTile(start);
        int oldTerrainId = tile.BaseTerrainTypeId;
        int oldColorIndex = tile.ColorIndex;

        if (oldTerrainId == newTerrainId && oldColorIndex == newColorIndex)
            return;

        var width = world.Width;
        var height = world.Height;
        var visited = new HashSet<TileCoord>();
        var queue = new Queue<TileCoord>();
        queue.Enqueue(start);
        visited.Add(start);

        int[] dx = { 0, 1, 0, -1 };
        int[] dy = { -1, 0, 1, 0 };

        while (queue.Count > 0)
        {
            var coord = queue.Dequeue();
            var t = world.GetTile(coord);
            if (t.BaseTerrainTypeId == oldTerrainId && t.ColorIndex == oldColorIndex)
            {
                sim.PaintTerrain(coord.X, coord.Y, newTerrainId, newColorIndex);

                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = coord.X + dx[dir];
                    int ny = coord.Y + dy[dir];
                    var ncoord = new TileCoord(nx, ny);

                    if (
                        nx >= 0
                        && nx < width
                        && ny >= 0
                        && ny < height
                        && !visited.Contains(ncoord)
                    )
                    {
                        var ntile = world.GetTile(ncoord);

                        // Don't flood through tiles that have blocking overlay terrain (like walls)
                        bool hasBlockingOverlay = false;
                        if (
                            ntile.OverlayTerrainTypeId.HasValue
                            && sim.Content.Terrains.TryGetValue(
                                ntile.OverlayTerrainTypeId.Value,
                                out var overlayDef
                            )
                        )
                        {
                            hasBlockingOverlay =
                                overlayDef.BlocksLight
                                || overlayDef.Passability == TerrainPassability.High;
                        }

                        if (
                            !hasBlockingOverlay
                            && ntile.BaseTerrainTypeId == oldTerrainId
                            && ntile.ColorIndex == oldColorIndex
                        )
                        {
                            queue.Enqueue(ncoord);
                            visited.Add(ncoord);
                        }
                    }
                }
            }
        }
    }
}
