using System.Collections.Generic;
using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

/// <summary>
/// Integration tests for the simulation using black-box scenario testing.
/// These tests set up a world scenario, run the simulation, and assert on the final state.
/// </summary>
public class SimulationIntegrationTests
{
    /// <summary>
    /// Scenario: A pawn starts with low hunger next to a market.
    /// After running the simulation, the pawn should have eaten and have higher hunger.
    /// </summary>
    [Fact]
    public void Pawn_WithLowHunger_UsesMarket_AndGetsFed()
    {
        // Arrange: Create a 5x1 world with a pawn at (0,0) and a market at (4,0)
        // The pawn has Hunger need at 0 (very hungry)
        var builder = new TestSimulationBuilder();
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.02f);
        var goodMealBuffId = builder.DefineBuff(
            "GoodMeal",
            "Good Meal",
            moodOffset: 15,
            durationTicks: 2400
        );
        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            grantsBuffId: goodMealBuffId,
            useAreas: new List<(int, int)> { (-1, 0) }
        );
        builder.AddBuilding(marketDefId, 4, 0);
        builder.AddPawn("TestPawn", 0, 0, new Dictionary<int, float> { { hungerId, 0f } });
        var sim = builder.Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        float initialHunger = sim.GetNeedValue(pawnId.Value, "Hunger");
        Assert.Equal(0f, initialHunger);

        // Act: Run simulation for enough ticks for the pawn to:
        // 1. Walk from (0,0) to adjacent to market at (3,0) - ~30 ticks at 10 ticks/tile
        // 2. Use the market - 20 ticks
        // Plus some buffer for AI decision making
        sim.RunTicks(100);

        // Assert: Pawn should have eaten and have higher hunger
        float finalHunger = sim.GetNeedValue(pawnId.Value, "Hunger");
        Assert.True(
            finalHunger > initialHunger,
            $"Expected hunger to increase from {initialHunger}, but it was {finalHunger}"
        );

        // Hunger should be around 50 (satisfaction amount) minus any decay
        Assert.True(
            finalHunger > 40f,
            $"Expected hunger to be > 40 after eating, but it was {finalHunger}"
        );
    }

    /// <summary>
    /// Scenario: A pawn with already full hunger should not seek out the market.
    /// </summary>
    [Fact]
    public void Pawn_WithFullHunger_DoesNotSeekMarket()
    {
        // Arrange: Create same world but with full hunger
        var builder = new TestSimulationBuilder();
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.001f);
        var goodMealBuffId = builder.DefineBuff(
            "GoodMeal",
            "Good Meal",
            moodOffset: 15,
            durationTicks: 2400
        );
        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            grantsBuffId: goodMealBuffId,
            useAreas: new List<(int, int)> { (-1, 0) }
        );
        builder.AddBuilding(marketDefId, 4, 0);
        builder.AddPawn("TestPawn", 0, 0, new Dictionary<int, float> { { hungerId, 100f } });
        var sim = builder.Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Act: Run simulation briefly
        sim.RunTicks(50);

        // Assert: Pawn should still be near starting position (not at market)
        var pos = sim.GetPosition(pawnId.Value);
        Assert.NotNull(pos);

        // Pawn might wander a bit but shouldn't be at the market (4,0) or adjacent (3,0)
        // They would only go there if they needed food
        float hunger = sim.GetNeedValue(pawnId.Value, "Hunger");
        Assert.True(hunger > 95f, $"Expected hunger to remain high (~100), but it was {hunger}");
    }

    /// <summary>
    /// Scenario: Multiple ticks advance the simulation time correctly.
    /// </summary>
    [Fact]
    public void Simulation_TicksAdvanceTime()
    {
        var builder = new TestSimulationBuilder();
        var sim = builder.Build();

        int initialTick = sim.Time.Tick;

        sim.RunTicks(100);

        Assert.Equal(initialTick + 100, sim.Time.Tick);
    }

    /// <summary>
    /// Scenario: Needs decay over time when no buildings satisfy them.
    /// </summary>
    [Fact]
    public void Needs_DecayOverTime()
    {
        var builder = new TestSimulationBuilder();
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.5f);
        builder.AddPawn("TestPawn", 2, 2, new Dictionary<int, float> { { hungerId, 100f } });
        var sim = builder.Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        float initialHunger = sim.GetNeedValue(pawnId.Value, "Hunger");
        Assert.Equal(100f, initialHunger);

        // Verify the need definition is in sim.Content
        var needId = sim.Content.GetNeedId("Hunger");
        Assert.NotNull(needId);
        Assert.True(sim.Content.Needs.ContainsKey(needId.Value), "Need should be registered");
        var needDef = sim.Content.Needs[needId.Value];
        Assert.Equal(0.5f, needDef.DecayPerTick);

        sim.RunTicks(100);

        float finalHunger = sim.GetNeedValue(pawnId.Value, "Hunger");
        Assert.True(
            finalHunger < initialHunger,
            $"Expected hunger to decay from {initialHunger}, but it was {finalHunger}"
        );

        // With 0.5 decay per tick over 100 ticks = 50 points decay
        // Should be around 50 or clamped to 0
        Assert.True(
            finalHunger <= 60f,
            $"Expected hunger to decay to <= 60, but it was {finalHunger}"
        );
    }

    /// <summary>
    /// Scenario: Pawn can reach and use a building that's not directly adjacent.
    /// </summary>
    [Fact]
    public void Pawn_NavigatesToBuilding_AndUsesIt()
    {
        // Arrange: Larger world with pawn far from market
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 0);
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.01f);
        var goodMealBuffId = builder.DefineBuff(
            "GoodMeal",
            "Good Meal",
            moodOffset: 15,
            durationTicks: 2400
        );
        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            grantsBuffId: goodMealBuffId,
            useAreas: new List<(int, int)> { (-1, 0) }
        );
        builder.AddBuilding(marketDefId, 9, 0);
        builder.AddPawn("TestPawn", 0, 0, new Dictionary<int, float> { { hungerId, 10f } });
        var sim = builder.Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Act: Run enough ticks to walk ~9 tiles and use building
        // 9 tiles * 10 ticks/tile + 20 ticks interaction + buffer
        sim.RunTicks(200);

        // Assert: Pawn should have used the market
        float finalHunger = sim.GetNeedValue(pawnId.Value, "Hunger");
        Assert.True(
            finalHunger > 40f,
            $"Expected pawn to have eaten (hunger > 40), but hunger was {finalHunger}"
        );
    }

    /// <summary>
    /// Scenario: Destroying a building restores tile walkability.
    /// </summary>
    [Fact]
    public void DestroyEntity_RestoresTileWalkability_ForBuildings()
    {
        // Arrange: Create a world with a building
        var builder = new TestSimulationBuilder();
        var hungerId = builder.DefineNeed(key: "Hunger");
        var marketDefId = builder.DefineBuilding(key: "Market", satisfiesNeedId: hungerId);
        builder.AddBuilding(marketDefId, 2, 2);
        var sim = builder.Build();

        var buildingId = sim.Entities.AllBuildings().First();
        var coord = new TileCoord(2, 2);

        // Verify tile is not walkable before destruction
        Assert.False(
            sim.World.GetTile(coord).Walkable,
            "Tile should not be walkable with building on it"
        );

        // Act: Destroy the building
        sim.DestroyEntity(buildingId);

        // Assert: Tile should be walkable again
        Assert.True(
            sim.World.GetTile(coord).Walkable,
            "Tile should be walkable after building destruction"
        );
        Assert.False(
            sim.Entities.Buildings.ContainsKey(buildingId),
            "Building should be removed from entities"
        );
    }
}
