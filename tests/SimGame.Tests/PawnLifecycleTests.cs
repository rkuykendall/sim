using System.Collections.Generic;
using SimGame.Core;
using Xunit;
using Xunit.Abstractions;

namespace SimGame.Tests;

/// <summary>
/// Tests for the full pawn lifecycle - satisfying needs, wandering, then returning to satisfy again.
/// </summary>
public class PawnLifecycleTests
{
    private readonly ITestOutputHelper _output;

    public PawnLifecycleTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const int NeedIdHunger = 1;
    private const int ObjectIdFridge = 1;

    /// <summary>
    /// Scenario: A pawn uses a fridge, gets satisfied, wanders, then returns to the fridge
    /// when hunger drops below threshold again.
    /// </summary>
    [Fact]
    public void Pawn_SatisfiesNeed_Wanders_ThenReturnsWhenNeedDrops()
    {
        // Use fast decay so the test doesn't take forever
        // 0.5 decay per tick means 100 -> 80 in 40 ticks, 100 -> 0 in 200 ticks
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 9, 0, 9)  // 10x10 world
            .DefineNeed("Hunger", NeedIdHunger, "Hunger", decayPerTick: 0.5f)
            .DefineObject("Fridge", ObjectIdFridge, "Fridge",
                satisfiesNeedId: NeedIdHunger,
                satisfactionAmount: 50f,
                interactionDuration: 20)
            .AddObject(ObjectIdFridge, 5, 5)  // Fridge in center
            .AddPawn("TestPawn", 0, 0, new Dictionary<int, float>
            {
                { NeedIdHunger, 10f }  // Start hungry so they go to fridge first
            })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Track the pawn's journey
        int timesUsedFridge = 0;
        float lastHunger = 10f;
        bool wasWandering = false;
        bool wentBackToFridge = false;

        _output.WriteLine("=== Pawn Lifecycle Test ===");
        _output.WriteLine($"NeedSatisfiedThreshold is 80 - pawn should wander above 80, seek fridge below 80");

        for (int tick = 0; tick < 500; tick++)
        {
            sim.Tick();

            var hunger = sim.GetNeedValue(pawnId.Value, NeedIdHunger);
            var pos = sim.GetPosition(pawnId.Value);
            
            string actionName = "none";
            if (sim.Entities.Actions.TryGetValue(pawnId.Value, out var actionComp) && actionComp.CurrentAction != null)
            {
                actionName = actionComp.CurrentAction.DisplayName ?? actionComp.CurrentAction.Type.ToString();
            }

            // Detect when pawn uses fridge (hunger jumps up significantly)
            if (hunger > lastHunger + 10)
            {
                timesUsedFridge++;
                _output.WriteLine($"Tick {tick}: USED FRIDGE! Hunger jumped from {lastHunger:F1} to {hunger:F1}");
                
                if (wasWandering && timesUsedFridge >= 2)
                {
                    wentBackToFridge = true;
                }
            }

            // Detect wandering (high hunger + wandering action)
            if (hunger > 80 && actionName.Contains("Wander"))
            {
                if (!wasWandering)
                {
                    _output.WriteLine($"Tick {tick}: Started wandering with hunger={hunger:F1}");
                    wasWandering = true;
                }
            }

            // Log periodically
            if (tick % 50 == 0)
            {
                _output.WriteLine($"Tick {tick}: pos={pos}, hunger={hunger:F1}, action={actionName}");
            }

            lastHunger = hunger;

            // Early exit if we've proven the lifecycle works
            if (wentBackToFridge)
            {
                _output.WriteLine($"Tick {tick}: SUCCESS - Pawn went back to fridge after wandering!");
                break;
            }
        }

        _output.WriteLine($"\n=== Results ===");
        _output.WriteLine($"Times used fridge: {timesUsedFridge}");
        _output.WriteLine($"Was wandering at some point: {wasWandering}");
        _output.WriteLine($"Went back to fridge after wandering: {wentBackToFridge}");

        // Assertions
        Assert.True(timesUsedFridge >= 1, "Pawn should have used the fridge at least once");
        Assert.True(wasWandering, "Pawn should have wandered when hunger was high");
        Assert.True(timesUsedFridge >= 2, $"Pawn should have used the fridge at least twice (used {timesUsedFridge} times)");
        Assert.True(wentBackToFridge, "Pawn should have gone back to the fridge after wandering");
    }

    /// <summary>
    /// Test that verifies the exact threshold behavior - pawn should seek food when hunger drops below 80.
    /// </summary>
    [Fact]
    public void Pawn_SeeksFoodWhenBelowThreshold()
    {
        // Start at exactly 79 (just below threshold) - should immediately seek food
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 4, 0, 0)  // Simple 5x1 corridor
            .DefineNeed("Hunger", NeedIdHunger, "Hunger", decayPerTick: 0.01f)  // Slow decay
            .DefineObject("Fridge", ObjectIdFridge, "Fridge",
                satisfiesNeedId: NeedIdHunger,
                satisfactionAmount: 50f,
                interactionDuration: 20)
            .AddObject(ObjectIdFridge, 4, 0)  // Fridge at end
            .AddPawn("TestPawn", 0, 0, new Dictionary<int, float>
            {
                { NeedIdHunger, 79f }  // Just below threshold
            })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Run a few ticks and check if pawn is going to fridge
        sim.RunTicks(5);

        var actionComp = sim.Entities.Actions[pawnId.Value];
        var actionName = actionComp.CurrentAction?.DisplayName ?? actionComp.CurrentAction?.Type.ToString() ?? "none";

        _output.WriteLine($"At hunger=79, action after 5 ticks: {actionName}");

        Assert.Contains("Fridge", actionName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test that verifies pawn wanders when above threshold.
    /// </summary>
    [Fact]
    public void Pawn_WandersWhenAboveThreshold()
    {
        // Start at exactly 81 (just above threshold) - should wander, not seek food
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 4, 0, 0)  // Simple 5x1 corridor
            .DefineNeed("Hunger", NeedIdHunger, "Hunger", decayPerTick: 0.001f)  // Very slow decay
            .DefineObject("Fridge", ObjectIdFridge, "Fridge",
                satisfiesNeedId: NeedIdHunger,
                satisfactionAmount: 50f,
                interactionDuration: 20)
            .AddObject(ObjectIdFridge, 4, 0)  // Fridge at end
            .AddPawn("TestPawn", 0, 0, new Dictionary<int, float>
            {
                { NeedIdHunger, 81f }  // Just above threshold
            })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Run a few ticks and check if pawn is wandering (not going to fridge)
        sim.RunTicks(5);

        var actionComp = sim.Entities.Actions[pawnId.Value];
        var actionName = actionComp.CurrentAction?.DisplayName ?? actionComp.CurrentAction?.Type.ToString() ?? "none";

        _output.WriteLine($"At hunger=81, action after 5 ticks: {actionName}");

        Assert.DoesNotContain("Fridge", actionName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Longer survival test - pawn should survive indefinitely by cycling between eating and wandering.
    /// </summary>
    [Fact]
    public void Pawn_SurvivesLongTerm_ByCyclingBetweenEatingAndWandering()
    {
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 9, 0, 9)
            .DefineNeed("Hunger", NeedIdHunger, "Hunger", decayPerTick: 0.3f)  // Moderate decay
            .DefineObject("Fridge", ObjectIdFridge, "Fridge",
                satisfiesNeedId: NeedIdHunger,
                satisfactionAmount: 60f,
                interactionDuration: 20)
            .AddObject(ObjectIdFridge, 5, 5)
            .AddPawn("TestPawn", 0, 0, new Dictionary<int, float>
            {
                { NeedIdHunger, 50f }
            })
            .Build();

        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        int timesUsedFridge = 0;
        float minHunger = 50f;
        float lastHunger = 50f;

        // Run for a long time (simulating several eat/wander cycles)
        for (int tick = 0; tick < 1000; tick++)
        {
            sim.Tick();

            var hunger = sim.GetNeedValue(pawnId.Value, NeedIdHunger);
            
            if (hunger > lastHunger + 10)
            {
                timesUsedFridge++;
            }
            
            if (hunger < minHunger)
            {
                minHunger = hunger;
            }

            lastHunger = hunger;

            if (tick % 200 == 0)
            {
                _output.WriteLine($"Tick {tick}: hunger={hunger:F1}, times fed={timesUsedFridge}, min hunger so far={minHunger:F1}");
            }
        }

        var finalHunger = sim.GetNeedValue(pawnId.Value, NeedIdHunger);

        _output.WriteLine($"\n=== Final Results ===");
        _output.WriteLine($"Final hunger: {finalHunger:F1}");
        _output.WriteLine($"Minimum hunger reached: {minHunger:F1}");
        _output.WriteLine($"Times used fridge: {timesUsedFridge}");

        // Pawn should have eaten multiple times
        Assert.True(timesUsedFridge >= 3, $"Pawn should have eaten at least 3 times over 1000 ticks (ate {timesUsedFridge} times)");
        
        // Pawn should never have starved (hunger should never hit 0)
        Assert.True(minHunger > 0, $"Pawn should never have starved (min hunger was {minHunger:F1})");
        
        // Pawn should still be alive (hunger > 0 at end)
        Assert.True(finalHunger > 0, $"Pawn should still be alive at end (hunger was {finalHunger:F1})");
    }
}
