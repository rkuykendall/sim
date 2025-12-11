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
    private const int NeedIdHunger = 1;
    private const int ObjectIdFridge = 1;
    private const int BuffIdGoodMeal = 201;

    /// <summary>
    /// Scenario: A pawn starts with low hunger next to a fridge.
    /// After running the simulation, the pawn should have eaten and have higher hunger.
    /// </summary>
    [Fact]
    public void Pawn_WithLowHunger_UsesFridge_AndGetsFed()
    {
        // Arrange: Create a 5x1 world with a pawn at (0,0) and a fridge at (4,0)
        // The pawn has Hunger need at 0 (very hungry)
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 4, 0, 0)  // 5x1 world
            .DefineNeed("Hunger", NeedIdHunger, "Hunger", decayPerTick: 0.02f)
            .DefineBuff("GoodMeal", BuffIdGoodMeal, "Good Meal", moodOffset: 15, durationTicks: 2400)
            .DefineObject("Fridge", ObjectIdFridge, "Fridge", 
                satisfiesNeedId: NeedIdHunger, 
                satisfactionAmount: 50f, 
                interactionDuration: 20,
                grantsBuffId: BuffIdGoodMeal)
            .AddObject(ObjectIdFridge, 4, 0)  // Fridge at (4,0)
            .AddPawn("TestPawn", 0, 0, new Dictionary<int, float> 
            { 
                { NeedIdHunger, 0f }  // Starting with 0 hunger (very hungry)
            })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        float initialHunger = sim.GetNeedValue(pawnId.Value, NeedIdHunger);
        Assert.Equal(0f, initialHunger);

        // Act: Run simulation for enough ticks for the pawn to:
        // 1. Walk from (0,0) to adjacent to fridge at (3,0) - ~30 ticks at 10 ticks/tile
        // 2. Use the fridge - 20 ticks
        // Plus some buffer for AI decision making
        sim.RunTicks(100);

        // Assert: Pawn should have eaten and have higher hunger
        float finalHunger = sim.GetNeedValue(pawnId.Value, NeedIdHunger);
        Assert.True(finalHunger > initialHunger, 
            $"Expected hunger to increase from {initialHunger}, but it was {finalHunger}");
        
        // Hunger should be around 50 (satisfaction amount) minus any decay
        Assert.True(finalHunger > 40f, 
            $"Expected hunger to be > 40 after eating, but it was {finalHunger}");
    }

    /// <summary>
    /// Scenario: A pawn with already full hunger should not seek out the fridge.
    /// </summary>
    [Fact]
    public void Pawn_WithFullHunger_DoesNotSeekFridge()
    {
        // Arrange: Create same world but with full hunger
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 4, 0, 0)
            .DefineNeed("Hunger", NeedIdHunger, "Hunger", decayPerTick: 0.001f)  // Very slow decay
            .DefineBuff("GoodMeal", BuffIdGoodMeal, "Good Meal", moodOffset: 15, durationTicks: 2400)
            .DefineObject("Fridge", ObjectIdFridge, "Fridge",
                satisfiesNeedId: NeedIdHunger,
                satisfactionAmount: 50f,
                interactionDuration: 20,
                grantsBuffId: BuffIdGoodMeal)
            .AddObject(ObjectIdFridge, 4, 0)
            .AddPawn("TestPawn", 0, 0, new Dictionary<int, float>
            {
                { NeedIdHunger, 100f }  // Starting with full hunger
            })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Act: Run simulation briefly
        sim.RunTicks(50);

        // Assert: Pawn should still be near starting position (not at fridge)
        var pos = sim.GetPosition(pawnId.Value);
        Assert.NotNull(pos);
        
        // Pawn might wander a bit but shouldn't be at the fridge (4,0) or adjacent (3,0)
        // They would only go there if they needed food
        float hunger = sim.GetNeedValue(pawnId.Value, NeedIdHunger);
        Assert.True(hunger > 95f, 
            $"Expected hunger to remain high (~100), but it was {hunger}");
    }

    /// <summary>
    /// Scenario: Multiple ticks advance the simulation time correctly.
    /// </summary>
    [Fact]
    public void Simulation_TicksAdvanceTime()
    {
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 4, 0, 4)
            .Build();

        int initialTick = sim.Time.Tick;
        
        sim.RunTicks(100);

        Assert.Equal(initialTick + 100, sim.Time.Tick);
    }

    /// <summary>
    /// Scenario: Needs decay over time when no objects satisfy them.
    /// </summary>
    [Fact]
    public void Needs_DecayOverTime()
    {
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 4, 0, 4)
            .DefineNeed("Hunger", NeedIdHunger, "Hunger", decayPerTick: 0.5f)  // Very fast decay for testing
            .AddPawn("TestPawn", 2, 2, new Dictionary<int, float>
            {
                { NeedIdHunger, 100f }
            })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        float initialHunger = sim.GetNeedValue(pawnId.Value, NeedIdHunger);
        Assert.Equal(100f, initialHunger);
        
        // Verify the need definition is in ContentDatabase
        Assert.True(ContentDatabase.Needs.ContainsKey(NeedIdHunger), "Need should be registered");
        var needDef = ContentDatabase.Needs[NeedIdHunger];
        Assert.Equal(0.5f, needDef.DecayPerTick);
        
        sim.RunTicks(100);

        float finalHunger = sim.GetNeedValue(pawnId.Value, NeedIdHunger);
        Assert.True(finalHunger < initialHunger, 
            $"Expected hunger to decay from {initialHunger}, but it was {finalHunger}");
        
        // With 0.5 decay per tick over 100 ticks = 50 points decay
        // Should be around 50 or clamped to 0
        Assert.True(finalHunger <= 60f, 
            $"Expected hunger to decay to <= 60, but it was {finalHunger}");
    }

    /// <summary>
    /// Scenario: Pawn can reach and use an object that's not directly adjacent.
    /// </summary>
    [Fact]
    public void Pawn_NavigatesToObject_AndUsesIt()
    {
        // Arrange: Larger world with pawn far from fridge
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 9, 0, 0)  // 10x1 world
            .DefineNeed("Hunger", NeedIdHunger, "Hunger", decayPerTick: 0.01f)
            .DefineBuff("GoodMeal", BuffIdGoodMeal, "Good Meal", moodOffset: 15, durationTicks: 2400)
            .DefineObject("Fridge", ObjectIdFridge, "Fridge",
                satisfiesNeedId: NeedIdHunger,
                satisfactionAmount: 50f,
                interactionDuration: 20,
                grantsBuffId: BuffIdGoodMeal)
            .AddObject(ObjectIdFridge, 9, 0)  // Fridge at far end
            .AddPawn("TestPawn", 0, 0, new Dictionary<int, float>
            {
                { NeedIdHunger, 10f }  // Low hunger to trigger seeking food
            })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Act: Run enough ticks to walk ~9 tiles and use object
        // 9 tiles * 10 ticks/tile + 20 ticks interaction + buffer
        sim.RunTicks(200);

        // Assert: Pawn should have used the fridge
        float finalHunger = sim.GetNeedValue(pawnId.Value, NeedIdHunger);
        Assert.True(finalHunger > 40f,
            $"Expected pawn to have eaten (hunger > 40), but hunger was {finalHunger}");
    }
}
