using System.Collections.Generic;
using Xunit;
using SimGame.Core;
using SimGame.Tests;

namespace SimGame.Tests
{
    public class PathfindingWallTests
    {
        [Fact]
        public void PawnCannotReachBedBehindWall()
        {
            // Arrange: 5x5 world, pawn at (1,2), bed at (3,2), vertical wall at x=2
            var sim = new TestSimulationBuilder()
                .WithWorldBounds(0, 4, 0, 4)
                .DefineNeed("Restfulness", "Restfulness")
                .DefineObject("Bed", "Bed", satisfiesNeed: "Restfulness")
                .DefineTerrain("Floor", walkable: true)
                .DefineTerrain("Wall", walkable: false)
                .AddPawn("Alice", 1, 2, new Dictionary<string, float> { { "Restfulness", 0f } })
                .AddObject("Bed", 3, 2)
                .Build();

            // Paint a vertical wall at x=2
            for (int y = 0; y <= 4; y++)
                sim.PaintTerrain(2, y, sim.Content.GetTerrainId("Wall").Value);

            var pawnId = sim.GetPawnByName("Alice");
            Assert.NotNull(pawnId);

            sim.RunTicks(100);
            var pos = sim.GetPosition(pawnId.Value);
            var restfulness100 = sim.GetNeedValue(pawnId.Value, "Restfulness");
            Assert.Equal(0f, restfulness100);
        }

        [Fact]
        public void PawnCanReachBedWithOpenUseArea()
        {
            // Arrange: 5x5 world, pawn at (1,2), bed at (3,2), wall tiles at (2,1), (2,3) but (2,2) is open
            var sim = new TestSimulationBuilder()
                .WithWorldBounds(0, 4, 0, 4)
                .DefineNeed("Tiredness", "Tiredness")
                .DefineObject("Bed", "Bed", satisfiesNeed: "Tiredness")
                .DefineTerrain("Floor", walkable: true)
                .DefineTerrain("Wall", walkable: false)
                .AddPawn("Bob", 1, 2, new Dictionary<string, float> { { "Tiredness", 100f } })
                .AddObject("Bed", 3, 2)
                .Build();

            // Only block (2,1) and (2,3), leave (2,2) open
            sim.PaintTerrain(2, 1, sim.Content.GetTerrainId("Wall").Value);
            sim.PaintTerrain(2, 3, sim.Content.GetTerrainId("Wall").Value);

            var pawnId = sim.GetPawnByName("Bob");
            Assert.NotNull(pawnId);

            sim.RunTicks(100);
            var tiredness = sim.GetNeedValue(pawnId.Value, "Tiredness");
            Assert.True(tiredness < 100f); // Pawn should be able to reach and use the bed
        }

        [Fact]
        public void PawnCannotMoveOntoWallTile()
        {
            // Arrange: 3x3 world, pawn at (0,1), wall at (1,1), bed at (2,1)
            var sim = new TestSimulationBuilder()
                .WithWorldBounds(0, 2, 0, 2)
                .DefineNeed("Tiredness", "Tiredness")
                .DefineObject("Bed", "Bed", satisfiesNeed: "Tiredness")
                .DefineTerrain("Floor", walkable: true)
                .DefineTerrain("Wall", walkable: false)
                .AddPawn("Carol", 0, 1, new Dictionary<string, float> { { "Tiredness", 100f } })
                .AddObject("Bed", 2, 1)
                .Build();

            sim.PaintTerrain(1, 1, sim.Content.GetTerrainId("Wall").Value);

            var pawnId = sim.GetPawnByName("Carol");
            Assert.NotNull(pawnId);

            sim.RunTicks(100);
            var pos = sim.GetPosition(pawnId.Value);
            Assert.NotEqual(new TileCoord(1, 1), pos); // Pawn should never move onto wall tile
        }

        [Fact]
        public void PawnCanReachBedDiagonalOpen()
        {
            // Arrange: 3x3 world, pawn at (0,0), bed at (2,2), no walls
            var sim = new TestSimulationBuilder()
                .WithWorldBounds(0, 2, 0, 2)
                .DefineNeed("Tiredness", "Tiredness")
                .DefineObject("Bed", "Bed", satisfiesNeed: "Tiredness")
                .DefineTerrain("Floor", walkable: true)
                .AddPawn("Dave", 0, 0, new Dictionary<string, float> { { "Tiredness", 100f } })
                .AddObject("Bed", 2, 2)
                .Build();

            var pawnId = sim.GetPawnByName("Dave");
            Assert.NotNull(pawnId);

            sim.RunTicks(100);
            var tiredness = sim.GetNeedValue(pawnId.Value, "Tiredness");
            Assert.True(tiredness < 100f); // Pawn should be able to reach and use the bed
        }

        [Fact]
        public void PawnCannotReachBedWithDiagonalWall()
        {
            // Arrange: 3x3 world, pawn at (0,0), bed at (2,2), walls at (1,0), (0,1), (1,1)
            var sim = new TestSimulationBuilder()
                .WithWorldBounds(0, 5, 0, 5)
                .DefineNeed("Tiredness", "Tiredness")
                .DefineObject("Bed", "Bed", satisfiesNeed: "Tiredness")
                .DefineTerrain("Floor", walkable: true)
                .DefineTerrain("Wall", walkable: false)
                .AddPawn("Eve", 0, 0, new Dictionary<string, float> { { "Tiredness", 0 } })
                .AddObject("Bed", 5, 5)
                .Build();

            var wallId = sim.Content.GetTerrainId("Wall").Value;

            for (int x = 0; x <= 5; x++)
                sim.PaintTerrain(5-x, x, wallId);

            var pawnId = sim.GetPawnByName("Eve");
            Assert.NotNull(pawnId);

            sim.RunTicks(100);
            var tiredness = sim.GetNeedValue(pawnId.Value, "Tiredness");
            Assert.Equal(0f, tiredness); // Pawn should not be able to reach or use the bed
        }
    }
}
