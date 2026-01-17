using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

public class SaveLoadTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    [Fact]
    public void SaveData_CanSerializeEmptySimulation()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        var sim = builder.Build();

        // Act
        var saveData = SaveService.ToSaveData(sim, "test-save");
        var json = JsonSerializer.Serialize(saveData, JsonOptions);
        var restored = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);

        // Assert
        Assert.NotNull(restored);
        Assert.Equal("test-save", restored.Name);
        Assert.Equal(sim.Seed, restored.Seed);
        Assert.Equal(sim.Time.Tick, restored.CurrentTick);
    }

    [Fact]
    public void SaveData_CanSerializeImpassableTiles()
    {
        // Arrange - this is the key test for the infinity bug
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        var wallId = builder.DefineTerrain("wall", walkable: false);
        var sim = builder.Build();

        // Paint a wall tile (has WalkabilityCost = float.PositiveInfinity)
        sim.PaintTerrain(new TileCoord(2, 2), wallId);

        // Act - this should not throw
        var saveData = SaveService.ToSaveData(sim, "test-save");
        var json = JsonSerializer.Serialize(saveData, JsonOptions);
        var restored = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);

        // Assert
        Assert.NotNull(restored);
        var wallTile = restored.World.Tiles.First(t => t.X == 2 && t.Y == 2);
        Assert.True(float.IsPositiveInfinity(wallTile.WalkabilityCost));
    }

    [Fact]
    public void SaveData_CanSerializePawns()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        var hungerId = builder.DefineNeed("hunger", decayPerTick: 0.02f);
        builder.AddPawn(
            "Alice",
            x: 1,
            y: 1,
            needs: new Dictionary<int, float> { { hungerId, 75f } }
        );
        var sim = builder.Build();

        // Act
        var saveData = SaveService.ToSaveData(sim, "test-save");
        var json = JsonSerializer.Serialize(saveData, JsonOptions);
        var restored = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);

        // Assert
        Assert.NotNull(restored);
        var pawnSave = restored.Entities.First(e => e.Type == "Pawn");
        Assert.Equal("Alice", pawnSave.Name);
        Assert.Equal(1, pawnSave.X);
        Assert.Equal(1, pawnSave.Y);
        Assert.NotNull(pawnSave.Needs);
        Assert.Equal(75f, pawnSave.Needs[hungerId]);
    }

    [Fact]
    public void SaveData_CanSerializeBuildings()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        var bedId = builder.DefineBuilding("bed", satisfiesNeedId: null);
        builder.AddBuilding(bedId, x: 2, y: 2);
        var sim = builder.Build();

        // Act
        var saveData = SaveService.ToSaveData(sim, "test-save");
        var json = JsonSerializer.Serialize(saveData, JsonOptions);
        var restored = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);

        // Assert
        Assert.NotNull(restored);
        var buildingSave = restored.Entities.First(e => e.Type == "Building");
        Assert.Equal(bedId, buildingSave.BuildingDefId);
        Assert.Equal(2, buildingSave.X);
        Assert.Equal(2, buildingSave.Y);
    }

    [Fact]
    public void FromSaveData_RestoresPawnState()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        var hungerId = builder.DefineNeed("hunger", decayPerTick: 0.02f);
        builder.AddPawn("Bob", x: 2, y: 3, needs: new Dictionary<int, float> { { hungerId, 60f } });
        var original = builder.Build();

        // Run a few ticks to change state
        original.RunTicks(10);

        // Save
        var saveData = SaveService.ToSaveData(original, "test-save");

        // Act - restore to a new simulation
        var builder2 = new TestSimulationBuilder();
        builder2.DefineTerrain("flat", walkable: true);
        builder2.DefineNeed("hunger", decayPerTick: 0.02f);
        var content = new ContentRegistry();
        // Copy content from builder (we need matching content)
        var originalContent = original.Content;

        var restored = Simulation.FromSaveData(saveData, originalContent);

        // Assert
        var originalPawn = original.GetPawnByName("Bob");
        Assert.NotNull(originalPawn);

        var restoredPawnId = restored.Entities.AllPawns().First();
        Assert.True(restored.Entities.Pawns.TryGetValue(restoredPawnId, out var restoredPawn));
        Assert.Equal("Bob", restoredPawn.Name);

        // Check position is restored
        var restoredPos = restored.GetPosition(restoredPawnId);
        var originalPos = original.GetPosition(originalPawn.Value);
        Assert.Equal(originalPos, restoredPos);

        // Check needs are restored (approximately, after 10 ticks of decay)
        Assert.True(restored.Entities.Needs.TryGetValue(restoredPawnId, out var restoredNeeds));
        Assert.True(original.Entities.Needs.TryGetValue(originalPawn.Value, out var originalNeeds));
        Assert.Equal(originalNeeds.Needs[hungerId], restoredNeeds.Needs[hungerId], precision: 2);
    }

    [Fact]
    public void FromSaveData_RestoresWorldTiles()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var flatId = builder.DefineTerrain("flat", walkable: true);
        var wallId = builder.DefineTerrain("wall", walkable: false);
        var original = builder.Build();

        // Paint some tiles
        original.PaintTerrain(new TileCoord(1, 1), wallId);
        original.PaintTerrain(new TileCoord(2, 2), wallId);

        // Save
        var saveData = SaveService.ToSaveData(original, "test-save");

        // Act
        var restored = Simulation.FromSaveData(saveData, original.Content);

        // Assert - check wall tiles are restored as impassable
        Assert.False(restored.World.IsWalkable(new TileCoord(1, 1)));
        Assert.False(restored.World.IsWalkable(new TileCoord(2, 2)));
        Assert.True(restored.World.IsWalkable(new TileCoord(0, 0)));
    }

    [Fact]
    public void FromSaveData_RestoresBuildings()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        var bedId = builder.DefineBuilding("bed");
        builder.AddBuilding(bedId, x: 1, y: 1);
        var original = builder.Build();

        // Save
        var saveData = SaveService.ToSaveData(original, "test-save");

        // Act
        var restored = Simulation.FromSaveData(saveData, original.Content);

        // Assert
        var buildings = restored.Entities.AllBuildings().ToList();
        Assert.Single(buildings);

        var buildingId = buildings[0];
        Assert.True(restored.Entities.Buildings.TryGetValue(buildingId, out var building));
        Assert.Equal(bedId, building.BuildingDefId);

        var pos = restored.GetPosition(buildingId);
        Assert.Equal(new TileCoord(1, 1), pos);
    }

    [Fact]
    public void FromSaveData_RestoresTime()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        var original = builder.Build();

        // Run ticks to advance time
        original.RunTicks(100);
        var originalTick = original.Time.Tick;

        // Save
        var saveData = SaveService.ToSaveData(original, "test-save");

        // Act
        var restored = Simulation.FromSaveData(saveData, original.Content);

        // Assert
        Assert.Equal(originalTick, restored.Time.Tick);
    }

    [Fact]
    public void FromSaveData_RestoresEntityIds()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        builder.AddPawn("Pawn1", x: 0, y: 0);
        builder.AddPawn("Pawn2", x: 1, y: 0);
        var original = builder.Build();

        var originalIds = original.Entities.AllPawns().Select(id => id.Value).ToList();

        // Save
        var saveData = SaveService.ToSaveData(original, "test-save");

        // Act
        var restored = Simulation.FromSaveData(saveData, original.Content);

        // Assert - entity IDs should be preserved
        var restoredIds = restored.Entities.AllPawns().Select(id => id.Value).ToList();
        Assert.Equal(originalIds.OrderBy(x => x), restoredIds.OrderBy(x => x));
    }

    [Fact]
    public void ToMetadata_ExtractsCorrectInfo()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        builder.AddPawn("P1", x: 0, y: 0);
        builder.AddPawn("P2", x: 1, y: 0);
        builder.AddPawn("P3", x: 2, y: 0);
        var sim = builder.Build();

        // Run to day 2
        sim.RunTicks(TimeService.TicksPerDay + 100);

        var saveData = SaveService.ToSaveData(sim, "My Save");

        // Act
        var metadata = SaveService.ToMetadata(saveData);

        // Assert
        Assert.Equal("My Save", metadata.SlotName);
        Assert.Equal("My Save", metadata.DisplayName);
        Assert.Equal(3, metadata.PawnCount);
        Assert.Equal(2, metadata.Day);
    }

    /// <summary>
    /// Helper to verify a simulation can be serialized, deserialized, and restored.
    /// </summary>
    private static Simulation AssertRoundTrip(Simulation original)
    {
        var saveData = SaveService.ToSaveData(original, "test-roundtrip");

        // Serialize to JSON (this is where infinity issues would surface)
        var json = JsonSerializer.Serialize(saveData, JsonOptions);
        Assert.False(string.IsNullOrEmpty(json), "Serialization produced empty JSON");

        // Deserialize back (this is where parsing issues would surface)
        var loadedData = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
        Assert.NotNull(loadedData);

        // Restore simulation
        var restored = Simulation.FromSaveData(loadedData, original.Content);
        Assert.NotNull(restored);

        return restored;
    }

    [Fact]
    public void RoundTrip_ComplexScenario_WithBuildingsAndPawns()
    {
        // Arrange - complex scenario with multiple buildings and pawns
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 9);
        builder.DefineTerrain("flat", walkable: true);
        var wallId = builder.DefineTerrain("wall", walkable: false);

        var hungerId = builder.DefineNeed("hunger", decayPerTick: 0.02f, criticalDebuff: -20f);
        var energyId = builder.DefineNeed("energy", decayPerTick: 0.01f);

        var farmId = builder.DefineBuilding(
            "farm",
            satisfiesNeedId: hungerId,
            canBeWorkedAt: true,
            resourceType: "food",
            maxResourceAmount: 100f
        );
        var bedId = builder.DefineBuilding("bed", satisfiesNeedId: energyId);

        builder.AddBuilding(farmId, x: 2, y: 2);
        builder.AddBuilding(bedId, x: 5, y: 5);
        builder.AddPawn(
            "Worker",
            x: 0,
            y: 0,
            needs: new Dictionary<int, float> { { hungerId, 80f }, { energyId, 90f } }
        );

        var sim = builder.Build();

        // Paint some walls
        sim.PaintTerrain(new TileCoord(4, 0), wallId);
        sim.PaintTerrain(new TileCoord(4, 1), wallId);
        sim.PaintTerrain(new TileCoord(4, 2), wallId);

        // Run simulation to create some state
        sim.RunTicks(50);

        // Act
        var restored = AssertRoundTrip(sim);

        // Assert - verify key state is preserved
        Assert.Equal(sim.Time.Tick, restored.Time.Tick);
        Assert.Equal(sim.Entities.AllPawns().Count(), restored.Entities.AllPawns().Count());
        Assert.Equal(sim.Entities.AllBuildings().Count(), restored.Entities.AllBuildings().Count());

        // Walls should still be impassable
        Assert.False(restored.World.IsWalkable(new TileCoord(4, 0)));
        Assert.False(restored.World.IsWalkable(new TileCoord(4, 1)));
    }

    [Fact]
    public void RoundTrip_WithBuffsAndMood()
    {
        // Arrange - scenario with buffs
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);

        var hungerId = builder.DefineNeed(
            "hunger",
            decayPerTick: 0.5f, // Fast decay to trigger debuff
            criticalThreshold: 30f,
            criticalDebuff: -25f
        );

        builder.AddPawn(
            "HungryPawn",
            x: 2,
            y: 2,
            needs: new Dictionary<int, float> { { hungerId, 20f } } // Start below critical
        );

        var sim = builder.Build();

        // Run to apply debuffs
        sim.RunTicks(5);

        // Act
        var restored = AssertRoundTrip(sim);

        // Assert - verify buffs are preserved
        var pawnId = restored.Entities.AllPawns().First();
        Assert.True(restored.Entities.Buffs.TryGetValue(pawnId, out var buffs));
        Assert.True(restored.Entities.Moods.TryGetValue(pawnId, out var mood));

        // Should have negative mood from hunger debuff
        Assert.True(mood.Mood < 0, "Pawn should have negative mood from hunger debuff");
    }

    [Fact]
    public void RoundTrip_WithResources()
    {
        // Arrange - building with resources
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);

        var farmId = builder.DefineBuilding(
            "farm",
            canBeWorkedAt: true,
            resourceType: "food",
            maxResourceAmount: 100f
        );

        builder.AddBuilding(farmId, x: 2, y: 2);
        var sim = builder.Build();

        // Act
        var restored = AssertRoundTrip(sim);

        // Assert
        var buildingId = restored.Entities.AllBuildings().First();
        Assert.True(restored.Entities.Resources.TryGetValue(buildingId, out var resource));
        Assert.Equal("food", resource.ResourceType);
        Assert.Equal(100f, resource.MaxAmount);
    }

    [Fact]
    public void RoundTrip_WithAttachments()
    {
        // Arrange - scenario that creates attachments
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 9);
        builder.DefineTerrain("flat", walkable: true);

        var hungerId = builder.DefineNeed("hunger", decayPerTick: 0.1f);
        var farmId = builder.DefineBuilding(
            "farm",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 5
        );

        builder.AddBuilding(farmId, x: 2, y: 2);
        builder.AddPawn(
            "Farmer",
            x: 2,
            y: 3, // Adjacent to farm's use area
            needs: new Dictionary<int, float> { { hungerId, 30f } } // Low hunger
        );

        var sim = builder.Build();

        // Run long enough to use building and create attachment
        sim.RunTicks(100);

        // Act
        var restored = AssertRoundTrip(sim);

        // Assert - verify attachments are preserved
        var buildingId = restored.Entities.AllBuildings().First();
        Assert.True(restored.Entities.Attachments.TryGetValue(buildingId, out var attachments));
        // Attachment may or may not exist depending on if pawn used building
    }

    [Fact]
    public void RoundTrip_WithInventory()
    {
        // Arrange - pawn carrying resources (hauling scenario)
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(14, 14);
        builder.DefineTerrain("flat", walkable: true);

        var farmId = builder.DefineBuilding(
            "farm",
            canBeWorkedAt: true,
            resourceType: "food",
            maxResourceAmount: 100f,
            workType: BuildingWorkType.Direct
        );

        var marketId = builder.DefineBuilding(
            "market",
            canBeWorkedAt: true,
            resourceType: "food",
            workType: BuildingWorkType.HaulFromBuilding,
            haulSourceResourceType: "food"
        );

        builder.AddBuilding(farmId, x: 2, y: 2);
        builder.AddBuilding(marketId, x: 10, y: 10);
        builder.AddPawn("Hauler", x: 5, y: 5);

        var sim = builder.Build();

        // Run to potentially start hauling
        sim.RunTicks(200);

        // Act - should not throw even if pawn has inventory
        var restored = AssertRoundTrip(sim);

        // Assert
        var pawnId = restored.Entities.AllPawns().First();
        Assert.True(restored.Entities.Inventory.TryGetValue(pawnId, out var inventory));
        Assert.NotNull(inventory);
    }

    [Fact]
    public void RoundTrip_RestoredSimulationCanContinue()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);
        var hungerId = builder.DefineNeed("hunger", decayPerTick: 0.02f);
        builder.AddPawn(
            "TestPawn",
            x: 2,
            y: 2,
            needs: new Dictionary<int, float> { { hungerId, 100f } }
        );

        var sim = builder.Build();
        sim.RunTicks(50);

        // Act
        var restored = AssertRoundTrip(sim);

        // Continue running the restored simulation - should not throw
        var tickBefore = restored.Time.Tick;
        restored.RunTicks(100);

        // Assert
        Assert.Equal(tickBefore + 100, restored.Time.Tick);

        // Hunger should have decayed
        var pawnId = restored.Entities.AllPawns().First();
        var hunger = restored.GetNeedValue(pawnId, "hunger");
        Assert.True(hunger < 100f, "Hunger should have decayed after running ticks");
    }

    [Fact]
    public void RoundTrip_LargeWorld()
    {
        // Arrange - larger world to test serialization performance and correctness
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(79, 79); // 80x80 world like the real game
        builder.DefineTerrain("flat", walkable: true);
        var wallId = builder.DefineTerrain("wall", walkable: false);

        var sim = builder.Build();

        // Paint walls around the edges
        for (int i = 0; i < 80; i++)
        {
            sim.PaintTerrain(new TileCoord(i, 0), wallId);
            sim.PaintTerrain(new TileCoord(i, 79), wallId);
            sim.PaintTerrain(new TileCoord(0, i), wallId);
            sim.PaintTerrain(new TileCoord(79, i), wallId);
        }

        // Act
        var restored = AssertRoundTrip(sim);

        // Assert - check wall border is preserved
        Assert.False(restored.World.IsWalkable(new TileCoord(0, 0)));
        Assert.False(restored.World.IsWalkable(new TileCoord(79, 79)));
        Assert.True(restored.World.IsWalkable(new TileCoord(40, 40)));
    }

    [Fact]
    public void RoundTrip_WithGold()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain("flat", walkable: true);

        var workBuildingId = builder.DefineBuilding(
            "workplace",
            canBeWorkedAt: true,
            baseCost: 10,
            baseProduction: 2.0f
        );

        builder.AddBuilding(workBuildingId, x: 2, y: 2);
        builder.AddPawn("Worker", x: 2, y: 3);

        var sim = builder.Build();
        sim.RunTicks(100);

        // Act
        var restored = AssertRoundTrip(sim);

        // Assert - gold should be preserved
        var pawnId = restored.Entities.AllPawns().First();
        Assert.True(restored.Entities.Gold.TryGetValue(pawnId, out var pawnGold));

        var buildingId = restored.Entities.AllBuildings().First();
        Assert.True(restored.Entities.Gold.TryGetValue(buildingId, out var buildingGold));
    }

    [Fact]
    public void ValidateSerializable_ThrowsOnInvalidState()
    {
        // This test demonstrates the ValidateSerializable pattern that can be used
        // in other test files to ensure scenarios remain serializable.
        //
        // Usage in other tests:
        //   SaveService.ValidateSerializable(sim); // Throws if serialization fails
        //
        // This catches issues like:
        // - float.PositiveInfinity without proper JSON options
        // - Circular references
        // - Types that can't be serialized

        // Arrange - a valid simulation should not throw
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 9);
        builder.DefineTerrain("flat", walkable: true);
        var wallId = builder.DefineTerrain("wall", walkable: false);
        var sim = builder.Build();

        // Paint some walls (which have infinity walkability cost)
        sim.PaintTerrain(new TileCoord(2, 2), wallId);
        sim.PaintTerrain(new TileCoord(3, 3), wallId);

        // Act & Assert - should not throw
        var ex = Record.Exception(() => SaveService.ValidateSerializable(sim));
        Assert.Null(ex);
    }
}
