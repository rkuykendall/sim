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
                sim.PaintTerrain(new TileCoord(x, y), grassId, 0);
            }
        }

        // Draw a 4x4 square of walls from (2,2) to (5,5)
        // Top and bottom edges
        for (int x = 2; x <= 5; x++)
        {
            sim.PaintTerrain(new TileCoord(x, 2), wallId, 0); // Top edge
            sim.PaintTerrain(new TileCoord(x, 5), wallId, 0); // Bottom edge
        }
        // Left and right edges
        for (int y = 2; y <= 5; y++)
        {
            sim.PaintTerrain(new TileCoord(2, y), wallId, 0); // Left edge
            sim.PaintTerrain(new TileCoord(5, y), wallId, 0); // Right edge
        }

        // Inside the square should still be grass: (3,3), (3,4), (4,3), (4,4)
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(3, 3)).BaseTerrainTypeId);
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(4, 4)).BaseTerrainTypeId);

        // Outside should also be grass
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(0, 0)).BaseTerrainTypeId);
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(7, 7)).BaseTerrainTypeId);

        // Act: Flood fill from inside the square (3,3) with floor
        // This simulates clicking inside the walled area with the fill tool
        sim.FloodFill(new TileCoord(3, 3), floorId, 0);

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
            sim.PaintTerrain(new TileCoord(x, y), grassId, 0);

        // Draw a wall square from (1,1) to (3,3) - this goes into OVERLAY layer
        for (int x = 1; x <= 3; x++)
        {
            sim.PaintTerrain(new TileCoord(x, 1), wallId, 0); // Top
            sim.PaintTerrain(new TileCoord(x, 3), wallId, 0); // Bottom
        }
        for (int y = 1; y <= 3; y++)
        {
            sim.PaintTerrain(new TileCoord(1, y), wallId, 0); // Left
            sim.PaintTerrain(new TileCoord(3, y), wallId, 0); // Right
        }

        // Verify walls are in overlay layer (not base layer)
        var wallTile = sim.World.GetTile(new TileCoord(1, 1));
        Assert.Equal(wallId, wallTile.OverlayTerrainTypeId);
        Assert.Equal(grassId, wallTile.BaseTerrainTypeId); // Base is still grass!

        // Act: Flood fill from inside the square (2,2) with floor
        sim.FloodFill(new TileCoord(2, 2), floorId, 0);

        // Assert: Walls should still exist (not be removed)
        var wallTileAfter = sim.World.GetTile(new TileCoord(1, 1));
        Assert.Equal(wallId, wallTileAfter.OverlayTerrainTypeId); // THIS WILL FAIL - wall gets cleared!

        // And outside grass should NOT be filled (walls should block flood)
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(0, 0)).BaseTerrainTypeId); // THIS WILL FAIL - gets filled!
    }

    [Fact]
    public void FloodFill_ShouldFillContiguousBlockOutlineWithNewTerrain()
    {
        // Reproduces bug: Create a block outline, switch to brick with different color,
        // flood fill the outline - all block tiles should become brick

        // Arrange: Create a 7x7 world
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(6, 6); // 7x7 world
        var grassId = builder.DefineTerrain(key: "Grass", walkable: true, isAutotiling: false);
        var blockId = builder.DefineTerrain(key: "Block", walkable: false, isAutotiling: true);
        var brickId = builder.DefineTerrain(key: "Brick", walkable: false, isAutotiling: true);
        var sim = builder.Build();

        // Paint everything grass first
        for (int x = 0; x <= 6; x++)
        for (int y = 0; y <= 6; y++)
            sim.PaintTerrain(new TileCoord(x, y), grassId, 0);

        // Create a 5x5 outline of Block tiles (color 0) from (1,1) to (5,5)
        // Top and bottom edges
        for (int x = 1; x <= 5; x++)
        {
            sim.PaintTerrain(new TileCoord(x, 1), blockId, 0); // Top edge
            sim.PaintTerrain(new TileCoord(x, 5), blockId, 0); // Bottom edge
        }
        // Left and right edges
        for (int y = 1; y <= 5; y++)
        {
            sim.PaintTerrain(new TileCoord(1, y), blockId, 0); // Left edge
            sim.PaintTerrain(new TileCoord(5, y), blockId, 0); // Right edge
        }

        // Verify the outline is Block (autotiling goes to overlay)
        var tile11 = sim.World.GetTile(new TileCoord(1, 1));
        Assert.Equal(grassId, tile11.BaseTerrainTypeId); // Base should be grass
        Assert.Equal(blockId, tile11.OverlayTerrainTypeId); // Block is autotiling, goes to overlay

        // Verify center is still grass
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(3, 3)).BaseTerrainTypeId);

        // Act: Flood fill from a corner of the Block outline (1,1) with Brick (color 3)
        // This simulates: user created Block outline, switched to Brick + color 3, clicked fill
        sim.FloodFill(new TileCoord(1, 1), brickId, 3);

        // Assert: ALL Block tiles in the outline should now be Brick (autotiling goes to overlay)
        // Top edge
        for (int x = 1; x <= 5; x++)
        {
            var t = sim.World.GetTile(new TileCoord(x, 1));
            Assert.Equal(brickId, t.OverlayTerrainTypeId); // Brick is autotiling, goes to overlay
            Assert.Equal(3, t.OverlayColorIndex); // Overlay color should be 3
        }
        // Bottom edge
        for (int x = 1; x <= 5; x++)
        {
            var t = sim.World.GetTile(new TileCoord(x, 5));
            Assert.Equal(brickId, t.OverlayTerrainTypeId);
            Assert.Equal(3, t.OverlayColorIndex);
        }
        // Left edge
        for (int y = 2; y <= 4; y++) // Skip corners (already checked)
        {
            var t = sim.World.GetTile(new TileCoord(1, y));
            Assert.Equal(brickId, t.OverlayTerrainTypeId);
            Assert.Equal(3, t.OverlayColorIndex);
        }
        // Right edge
        for (int y = 2; y <= 4; y++) // Skip corners
        {
            var t = sim.World.GetTile(new TileCoord(5, y));
            Assert.Equal(brickId, t.OverlayTerrainTypeId);
            Assert.Equal(3, t.OverlayColorIndex);
        }

        // Center should still be grass with no overlay (not filled)
        var centerTile = sim.World.GetTile(new TileCoord(3, 3));
        Assert.Equal(grassId, centerTile.BaseTerrainTypeId);
        Assert.Null(centerTile.OverlayTerrainTypeId);
    }

    [Fact]
    public void FloodFill_ShouldOnlyExpandIntoExactlyMatchingTiles()
    {
        // Fill tool should only expand into tiles that match in ALL aspects:
        // - Same base terrain
        // - Same overlay terrain (or both null)
        // - Same color
        // It should NOT spread from grass+dirt to grass-only, even though base is the same

        // Arrange: Create a 7x5 world with distinct regions
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(6, 4); // 7x5 world (0-6, 0-4)
        var grassId = builder.DefineTerrain(key: "Grass", walkable: true, isAutotiling: false);
        var dirtId = builder.DefineTerrain(key: "Dirt", walkable: true, isAutotiling: false);
        var stoneId = builder.DefineTerrain(key: "Stone", walkable: true, isAutotiling: false);
        var sim = builder.Build();

        // Create three distinct regions separated by stone walls:
        // Left region (0-2, 0-2): Grass only (color 0) - contiguous, should all fill
        // Middle region (3-4, 0-2): Grass + Dirt overlay (color 0) - should NOT fill
        // Right region (5-6, 0-2): Grass only (color 1) - different color, should NOT fill
        // Everywhere else: Stone

        // Paint everything stone first
        for (int x = 0; x <= 6; x++)
        for (int y = 0; y <= 4; y++)
            sim.PaintTerrain(new TileCoord(x, y), stoneId, 0);

        // Left region: Grass only (color 0) - 3x3 square, should all be filled
        for (int x = 0; x <= 2; x++)
        for (int y = 0; y <= 2; y++)
            sim.PaintTerrain(new TileCoord(x, y), grassId, 0);

        // Middle region: Grass + Dirt overlay (color 0) - should NOT fill
        for (int x = 3; x <= 4; x++)
        for (int y = 0; y <= 2; y++)
        {
            sim.PaintTerrain(new TileCoord(x, y), grassId, 0);
            sim.World.GetTile(new TileCoord(x, y)).OverlayTerrainTypeId = dirtId;
        }

        // Right region: Grass only (color 1) - different color, should NOT fill
        for (int x = 5; x <= 6; x++)
        for (int y = 0; y <= 2; y++)
            sim.PaintTerrain(new TileCoord(x, y), grassId, 1);

        // Verify initial state
        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(0, 0)).BaseTerrainTypeId);
        Assert.Null(sim.World.GetTile(new TileCoord(0, 0)).OverlayTerrainTypeId);
        Assert.Equal(0, sim.World.GetTile(new TileCoord(0, 0)).ColorIndex);

        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(3, 0)).BaseTerrainTypeId);
        Assert.Equal(dirtId, sim.World.GetTile(new TileCoord(3, 0)).OverlayTerrainTypeId);
        Assert.Equal(0, sim.World.GetTile(new TileCoord(3, 0)).ColorIndex);

        Assert.Equal(grassId, sim.World.GetTile(new TileCoord(5, 0)).BaseTerrainTypeId);
        Assert.Equal(1, sim.World.GetTile(new TileCoord(5, 0)).ColorIndex);

        // Act: Flood fill from (1,1) in left region (grass-only, color 0)
        // Replace with stone, color 2
        sim.FloodFill(new TileCoord(1, 1), stoneId, 2);

        // Assert: Only the left region (grass-only, color 0) should be filled with stone
        // All 9 tiles in left region should be stone now
        for (int x = 0; x <= 2; x++)
        for (int y = 0; y <= 2; y++)
        {
            Assert.Equal(stoneId, sim.World.GetTile(new TileCoord(x, y)).BaseTerrainTypeId);
            Assert.Equal(2, sim.World.GetTile(new TileCoord(x, y)).ColorIndex);
        }

        // Middle region (grass+dirt) should NOT be changed
        for (int x = 3; x <= 4; x++)
        for (int y = 0; y <= 2; y++)
        {
            Assert.Equal(grassId, sim.World.GetTile(new TileCoord(x, y)).BaseTerrainTypeId);
            Assert.Equal(dirtId, sim.World.GetTile(new TileCoord(x, y)).OverlayTerrainTypeId);
            Assert.Equal(0, sim.World.GetTile(new TileCoord(x, y)).ColorIndex);
        }

        // Right region (grass, different color) should NOT be changed
        for (int x = 5; x <= 6; x++)
        for (int y = 0; y <= 2; y++)
        {
            Assert.Equal(grassId, sim.World.GetTile(new TileCoord(x, y)).BaseTerrainTypeId);
            Assert.Equal(1, sim.World.GetTile(new TileCoord(x, y)).ColorIndex);
        }
    }
}
