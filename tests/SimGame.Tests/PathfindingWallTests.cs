using System.Collections.Generic;
using SimGame.Core;
using SimGame.Tests;
using Xunit;

namespace SimGame.Tests
{
    public class PathfindingWallTests
    {
        [Fact]
        public void PawnCannotReachBedBehindWall()
        {
            // Arrange: 5x5 world, pawn at (1,2), bed at (3,2), vertical wall at x=2
            var builder = new TestSimulationBuilder();
            builder.DefineNeed("Restfulness");
            var bedDefId = builder.DefineObject(key: "Bed", satisfiesNeed: "Restfulness");
            builder.DefineTerrain(key: "Floor", walkable: true);
            builder.DefineTerrain(key: "Wall", walkable: false);
            builder.AddPawn("Alice", 1, 2, new Dictionary<string, float> { { "Restfulness", 0f } });
            builder.AddObject("Bed", 3, 2);
            var sim = builder.Build();

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
            var builder = new TestSimulationBuilder();
            builder.DefineNeed("Tiredness");
            var bedDefId = builder.DefineObject(key: "Bed", satisfiesNeed: "Tiredness");
            builder.DefineTerrain(key: "Floor", walkable: true);
            builder.DefineTerrain(key: "Wall", walkable: false);
            builder.AddPawn("Bob", 1, 2, new Dictionary<string, float> { { "Tiredness", 100f } });
            builder.AddObject("Bed", 3, 2);
            var sim = builder.Build();

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
            var builder = new TestSimulationBuilder();
            builder.WithWorldBounds(2, 2);
            builder.DefineNeed("Tiredness");
            var bedDefId = builder.DefineObject(key: "Bed", satisfiesNeed: "Tiredness");
            builder.DefineTerrain(key: "Floor", walkable: true);
            builder.DefineTerrain(key: "Wall", walkable: false);
            builder.AddPawn("Carol", 0, 1, new Dictionary<string, float> { { "Tiredness", 100f } });
            builder.AddObject("Bed", 2, 1);
            var sim = builder.Build();

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
            var builder = new TestSimulationBuilder();
            builder.WithWorldBounds(2, 2);
            builder.DefineNeed("Tiredness");
            var bedDefId = builder.DefineObject(key: "Bed", satisfiesNeed: "Tiredness");
            builder.DefineTerrain(key: "Floor", walkable: true);
            builder.AddPawn("Dave", 0, 0, new Dictionary<string, float> { { "Tiredness", 100f } });
            builder.AddObject("Bed", 2, 2);
            var sim = builder.Build();

            var pawnId = sim.GetPawnByName("Dave");
            Assert.NotNull(pawnId);

            sim.RunTicks(100);
            var tiredness = sim.GetNeedValue(pawnId.Value, "Tiredness");
            Assert.True(tiredness < 100f); // Pawn should be able to reach and use the bed
        }

        [Fact]
        public void PawnCannotReachBedWithDiagonalWall()
        {
            // Arrange: pawn at (0,0), bed at (2,2), walls at (1,0), (0,1), (1,1)
            var builder = new TestSimulationBuilder();
            builder.DefineNeed("Tiredness");
            var bedDefId = builder.DefineObject(key: "Bed", satisfiesNeed: "Tiredness");
            builder.DefineTerrain(key: "Floor", walkable: true);
            builder.DefineTerrain(key: "Wall", walkable: false);
            builder.AddPawn("Eve", 0, 0, new Dictionary<string, float> { { "Tiredness", 0 } });
            builder.AddObject("Bed", 4, 4);
            var sim = builder.Build();

            var wallId = sim.Content.GetTerrainId("Wall").Value;

            for (int x = 0; x <= 4; x++)
                sim.PaintTerrain(4 - x, x, wallId);

            var pawnId = sim.GetPawnByName("Eve");
            Assert.NotNull(pawnId);

            sim.RunTicks(100);
            var tiredness = sim.GetNeedValue(pawnId.Value, "Tiredness");
            Assert.Equal(0f, tiredness); // Pawn should not be able to reach or use the bed
        }
    }
}
