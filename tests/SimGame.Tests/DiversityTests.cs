using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

public class DiversityTests
{
    [Fact]
    public void DiversityMap_UniformTiles_HasLowDiversity()
    {
        // Diversity is based on differences from neighboring tiles
        // Uniform tiles should have low diversity scores
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(4, 4);
        var grassId = builder.DefineTerrain(key: "Grass", walkable: true);
        var sim = builder.Build();

        // Paint everything the same
        for (int x = 0; x <= 4; x++)
        for (int y = 0; y <= 4; y++)
            sim.PaintTerrain(new TileCoord(x, y), grassId, 0);

        var diversityMap = sim.GetDiversityMap();

        // Most tiles should have low diversity (0-1) since they're all the same
        Assert.True(diversityMap[2, 2] <= 1);
    }

    [Fact]
    public void DiversityMap_VariedTiles_HasHigherDiversity()
    {
        // Tiles that differ from neighbors should have higher diversity
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(4, 4);
        var grassId = builder.DefineTerrain(key: "Grass", walkable: true);
        var sim = builder.Build();

        // Create a checkerboard pattern with different colors
        for (int x = 0; x <= 4; x++)
        for (int y = 0; y <= 4; y++)
        {
            int color = (x + y) % 2;
            sim.PaintTerrain(new TileCoord(x, y), grassId, color);
        }

        var diversityMap = sim.GetDiversityMap();

        // Tiles should have higher diversity due to varying neighbors
        // At least some tiles should have diversity > 0
        bool hasHighDiversity = false;
        for (int x = 0; x <= 4; x++)
        for (int y = 0; y <= 4; y++)
        {
            if (diversityMap[x, y] > 0)
                hasHighDiversity = true;
        }

        Assert.True(hasHighDiversity);
    }

    [Fact]
    public void PawnWandering_GravitatesToDiverseArea()
    {
        // Integration test: Pawn with no needs should wander towards diverse area over time
        // This verifies the diversity-seeking behavior works in practice

        // Arrange: Create a 50x50 world
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(49, 49); // 50x50 world (0-49)
        var grassId = builder.DefineTerrain(key: "Grass", walkable: true, isAutotiling: false);
        var pathId = builder.DefineTerrain(key: "Path", walkable: true, isAutotiling: false);

        // Add a pawn in starting corner
        builder.AddPawn("TestPawn", x: 0, y: 0);

        var sim = builder.Build();

        // Create VERY diverse target area in corner (40-49, 40-49)
        // Use dense checkerboard pattern with varied colors and overlays
        for (int x = 40; x <= 49; x++)
        for (int y = 40; y <= 49; y++)
        {
            // Dense colorful base terrain (checkerboard of 4 colors)
            int color = ((x % 2) * 2 + (y % 2)) % 4;
            sim.PaintTerrain(new TileCoord(x, y), grassId, color);

            // Add path overlay to every tile in varied pattern
            sim.World.GetTile(new TileCoord(x, y)).OverlayTerrainTypeId = pathId;
            sim.World.GetTile(new TileCoord(x, y)).OverlayColorIndex = (x + y) % 3;
        }

        // Find the pawn ID and set all needs to 100 so pawn only wanders
        var pawnIdNullable = sim.GetFirstPawn();
        Assert.NotNull(pawnIdNullable);
        var pawnId = pawnIdNullable.Value;

        if (sim.Entities.Needs.TryGetValue(pawnId, out var needs))
        {
            foreach (var needId in needs.Needs.Keys.ToList())
            {
                needs.Needs[needId] = 100.0f;
            }
        }

        // Act: Run simulation for enough time for pawn to wander towards diverse area
        // At 1-3 tiles per wander with 40-80 tick idle times, this takes many cycles
        // Need ~10000+ ticks for gradual migration across 50 tiles
        for (int i = 0; i < 10000; i++)
        {
            sim.Tick();
        }

        // Assert: Pawn should have moved generally towards the diverse area
        if (!sim.Entities.Positions.TryGetValue(pawnId, out var finalPos))
        {
            Assert.Fail("Pawn position not found");
            return;
        }

        // Calculate distances from diverse corner (45, 45)
        int startDistance = 45 + 45; // Starting position (0, 0) to (45, 45)
        int finalDistanceX = Math.Abs(finalPos.Coord.X - 45);
        int finalDistanceY = Math.Abs(finalPos.Coord.Y - 45);
        int finalDistance = finalDistanceX + finalDistanceY;

        // Pawn should be closer to diverse area than starting position
        // Over 10000 ticks with diversity-seeking, should move at least 80% closer
        int requiredReduction = (int)(startDistance * 0.6); // Should be at least 80% closer
        int actualReduction = startDistance - finalDistance;

        // Verify diversity in corners and print map for visual inspection
        var diversityMap = sim.GetDiversityMap();
        Assert.True(diversityMap[0, 0] <= 1); // Start corner: uniform/low diversity
        Assert.True(diversityMap[45, 45] >= 1); // End corner: diverse

        Assert.True(
            actualReduction >= requiredReduction,
            $"Pawn at {finalPos.Coord} is {finalDistance} tiles from diverse area. "
                + $"Started at {startDistance}, moved {actualReduction} tiles closer. Expected at least {requiredReduction} tiles closer."
        );
    }
}
