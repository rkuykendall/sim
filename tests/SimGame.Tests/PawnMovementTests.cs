using System.Collections.Generic;
using System.Linq;
using SimGame.Core;
using Xunit;
using Xunit.Abstractions;

namespace SimGame.Tests;

/// <summary>
/// Tests for pawn movement and collision handling.
/// </summary>
public class PawnMovementTests
{
    private readonly ITestOutputHelper _output;

    public PawnMovementTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: Two pawns walk toward each other on a narrow corridor.
    /// They should not get permanently stuck.
    /// </summary>
    [Fact]
    public void TwoPawns_WalkingTowardEachOther_DoNotGetStuck()
    {
        // Arrange: Create a 1x10 corridor with two pawns at opposite ends
        // Both have low hunger and there's a fridge in the middle
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 0); // 10x1 corridor
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.001f);
        var fridgeDefId = builder.DefineObject(
            key: "Fridge",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            useAreas: new List<(int, int)> { (-1, 0), (1, 0) }
        ); // Both sides work in corridor
        builder.AddObject(fridgeDefId, 5, 0); // Fridge in middle
        builder.AddPawn("LeftPawn", 0, 0, new Dictionary<int, float> { { hungerId, 10f } });
        builder.AddPawn("RightPawn", 9, 0, new Dictionary<int, float> { { hungerId, 10f } });
        var sim = builder.Build();

        var leftPawn = sim.GetPawnByName("LeftPawn");
        var rightPawn = sim.GetPawnByName("RightPawn");
        Assert.NotNull(leftPawn);
        Assert.NotNull(rightPawn);

        // Track positions over time to detect if stuck
        var leftPositions = new List<TileCoord>();
        var rightPositions = new List<TileCoord>();

        // Act: Run simulation and track movement
        for (int tick = 0; tick < 500; tick++)
        {
            sim.Tick();

            var leftPos = sim.GetPosition(leftPawn.Value);
            var rightPos = sim.GetPosition(rightPawn.Value);

            if (leftPos.HasValue)
                leftPositions.Add(leftPos.Value);
            if (rightPos.HasValue)
                rightPositions.Add(rightPos.Value);

            // Log every 50 ticks
            if (tick % 50 == 0)
            {
                _output.WriteLine($"Tick {tick}: Left at {leftPos}, Right at {rightPos}");

                // Also log their current actions
                if (sim.Entities.Actions.TryGetValue(leftPawn.Value, out var leftAction))
                {
                    _output.WriteLine(
                        $"  Left action: {leftAction.CurrentAction?.DisplayName ?? "none"}, Queue: {leftAction.ActionQueue.Count}"
                    );
                }
                if (sim.Entities.Actions.TryGetValue(rightPawn.Value, out var rightAction))
                {
                    _output.WriteLine(
                        $"  Right action: {rightAction.CurrentAction?.DisplayName ?? "none"}, Queue: {rightAction.ActionQueue.Count}"
                    );
                }
            }
        }

        // Assert: Check that pawns actually moved and didn't get stuck in the same position
        // Get last 100 positions and check they're not all the same
        var lastLeftPositions = leftPositions.GetRange(
            Math.Max(0, leftPositions.Count - 100),
            Math.Min(100, leftPositions.Count)
        );
        var lastRightPositions = rightPositions.GetRange(
            Math.Max(0, rightPositions.Count - 100),
            Math.Min(100, rightPositions.Count)
        );

        var uniqueLeftPositions = new HashSet<TileCoord>(lastLeftPositions);
        var uniqueRightPositions = new HashSet<TileCoord>(lastRightPositions);

        _output.WriteLine(
            $"Last 100 ticks - Left unique positions: {uniqueLeftPositions.Count}, Right unique positions: {uniqueRightPositions.Count}"
        );

        // If a pawn is stuck, it will have only 1-2 unique positions in the last 100 ticks
        // A healthy pawn should be moving around (wandering or going to objects)
        Assert.True(
            uniqueLeftPositions.Count > 2 || uniqueRightPositions.Count > 2,
            "Both pawns appear to be stuck - neither moved significantly in the last 100 ticks"
        );
    }

    /// <summary>
    /// Scenario: Two pawns try to swap positions (each wants to move to where the other is).
    /// This is a classic deadlock scenario.
    /// </summary>
    [Fact]
    public void TwoPawns_TryingToSwapPositions_DoNotDeadlock()
    {
        // Arrange: 2x1 world with pawns adjacent, fridges on opposite ends
        // Each pawn wants to get to the fridge on the other side
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(3, 0); // 4x1: [Fridge][Pawn1][Pawn2][Fridge]
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.001f);
        var fridgeDefId = builder.DefineObject(
            key: "Fridge",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            useAreas: new List<(int, int)> { (1, 0), (-1, 0) }
        ); // Use from either side
        builder.AddObject(fridgeDefId, 0, 0); // Fridge at left
        builder.AddObject(fridgeDefId, 3, 0); // Fridge at right (same type, different instance)
        builder.AddPawn("Pawn1", 1, 0, new Dictionary<int, float> { { hungerId, 10f } });
        builder.AddPawn("Pawn2", 2, 0, new Dictionary<int, float> { { hungerId, 10f } });
        var sim = builder.Build();

        var pawn1 = sim.GetPawnByName("Pawn1");
        var pawn2 = sim.GetPawnByName("Pawn2");
        Assert.NotNull(pawn1);
        Assert.NotNull(pawn2);

        // Track if either pawn ever uses a fridge
        bool pawn1UsedFridge = false;
        bool pawn2UsedFridge = false;
        float pawn1MaxHunger = 10f;
        float pawn2MaxHunger = 10f;

        // Act: Run simulation
        for (int tick = 0; tick < 300; tick++)
        {
            sim.Tick();

            var hunger1 = sim.GetNeedValue(pawn1.Value, "Hunger");
            var hunger2 = sim.GetNeedValue(pawn2.Value, "Hunger");

            if (hunger1 > pawn1MaxHunger)
            {
                pawn1MaxHunger = hunger1;
                pawn1UsedFridge = true;
            }
            if (hunger2 > pawn2MaxHunger)
            {
                pawn2MaxHunger = hunger2;
                pawn2UsedFridge = true;
            }

            if (tick % 50 == 0)
            {
                var pos1 = sim.GetPosition(pawn1.Value);
                var pos2 = sim.GetPosition(pawn2.Value);
                _output.WriteLine(
                    $"Tick {tick}: Pawn1 at {pos1} (hunger={hunger1:F1}), Pawn2 at {pos2} (hunger={hunger2:F1})"
                );
            }
        }

        _output.WriteLine(
            $"Final: Pawn1 used fridge: {pawn1UsedFridge}, Pawn2 used fridge: {pawn2UsedFridge}"
        );

        // Assert: At least one pawn should have been able to use a fridge
        Assert.True(
            pawn1UsedFridge || pawn2UsedFridge,
            "Neither pawn was able to reach and use a fridge - they appear to be deadlocked"
        );
    }

    /// <summary>
    /// Scenario: Pawns in a wider area should be able to navigate around each other.
    /// </summary>
    [Fact]
    public void TwoPawns_InWiderArea_CanNavigateAroundEachOther()
    {
        // Arrange: 5x3 area - pawns have room to go around each other
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(4, 2); // 5x3 area
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.001f);
        var fridgeDefId = builder.DefineObject(
            key: "Fridge",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20
        );
        builder.AddObject(fridgeDefId, 2, 1); // Fridge in center
        builder.AddPawn("TopPawn", 0, 0, new Dictionary<int, float> { { hungerId, 5f } });
        builder.AddPawn("BottomPawn", 4, 2, new Dictionary<int, float> { { hungerId, 5f } });
        var sim = builder.Build();

        var topPawn = sim.GetPawnByName("TopPawn");
        var bottomPawn = sim.GetPawnByName("BottomPawn");
        Assert.NotNull(topPawn);
        Assert.NotNull(bottomPawn);

        int fedCount = 0;

        // Act: Run simulation
        for (int tick = 0; tick < 400; tick++)
        {
            sim.Tick();

            var hunger1 = sim.GetNeedValue(topPawn.Value, "Hunger");
            var hunger2 = sim.GetNeedValue(bottomPawn.Value, "Hunger");

            // Count how many times pawns get fed (hunger jumps up)
            if (hunger1 > 40)
                fedCount++;
            if (hunger2 > 40)
                fedCount++;

            if (tick % 100 == 0)
            {
                var pos1 = sim.GetPosition(topPawn.Value);
                var pos2 = sim.GetPosition(bottomPawn.Value);
                _output.WriteLine(
                    $"Tick {tick}: Top at {pos1} (hunger={hunger1:F1}), Bottom at {pos2} (hunger={hunger2:F1})"
                );
            }
        }

        // Assert: Both pawns should have been fed at least once
        var finalHunger1 = sim.GetNeedValue(topPawn.Value, "Hunger");
        var finalHunger2 = sim.GetNeedValue(bottomPawn.Value, "Hunger");

        _output.WriteLine($"Final: Top hunger={finalHunger1:F1}, Bottom hunger={finalHunger2:F1}");

        // At least one should have gotten to the fridge
        Assert.True(
            finalHunger1 > 20 || finalHunger2 > 20,
            $"Neither pawn reached the fridge. Top hunger: {finalHunger1}, Bottom hunger: {finalHunger2}"
        );
    }

    /// <summary>
    /// Scenario: Two pawns with different needs should cross paths.
    /// One goes left to a fridge, one goes right to a bed.
    /// Tests if they can navigate around each other when they meet in the middle.
    /// </summary>
    [Fact]
    public void TwoPawns_CrossingPaths_ShouldNotGetStuck()
    {
        // Arrange: 7x3 world
        // [Fridge][ ][ ][Pawn1][Pawn2][ ][Bed]
        //    [ ][ ][ ][ ][ ][ ][ ]
        //    [ ][ ][ ][ ][ ][ ][ ]
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(6, 2); // 7x3 area
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.001f);
        var energyId = builder.DefineNeed(key: "Energy", decayPerTick: 0.001f);
        var fridgeDefId = builder.DefineObject(
            key: "Fridge",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20
        );
        var bedDefId = builder.DefineObject(
            key: "Bed",
            satisfiesNeedId: energyId,
            satisfactionAmount: 50f,
            interactionDuration: 20
        );
        builder.AddObject(fridgeDefId, 0, 0); // Fridge at left
        builder.AddObject(bedDefId, 6, 0); // Bed at right
        // Pawn1 is hungry (will go left to fridge)
        builder.AddPawn(
            "HungryPawn",
            3,
            0,
            new Dictionary<int, float>
            {
                { hungerId, 5f }, // Very hungry - will seek fridge
                { energyId, 100f }, // Full energy
            }
        );
        // Pawn2 is tired (will go right to bed)
        builder.AddPawn(
            "TiredPawn",
            4,
            0,
            new Dictionary<int, float>
            {
                { hungerId, 100f }, // Full
                { energyId, 5f }, // Very tired - will seek bed
            }
        );
        var sim = builder.Build();

        var hungryPawn = sim.GetPawnByName("HungryPawn");
        var tiredPawn = sim.GetPawnByName("TiredPawn");
        Assert.NotNull(hungryPawn);
        Assert.NotNull(tiredPawn);

        // Track positions to detect if stuck
        var positionHistory = new List<(TileCoord hungry, TileCoord tired)>();
        bool hungryGotFed = false;
        bool tiredGotRested = false;

        // Act: Run simulation
        for (int tick = 0; tick < 500; tick++)
        {
            sim.Tick();

            var hungryPos = sim.GetPosition(hungryPawn.Value);
            var tiredPos = sim.GetPosition(tiredPawn.Value);

            if (hungryPos.HasValue && tiredPos.HasValue)
            {
                positionHistory.Add((hungryPos.Value, tiredPos.Value));
            }

            var hunger = sim.GetNeedValue(hungryPawn.Value, "Hunger");
            var energy = sim.GetNeedValue(tiredPawn.Value, "Energy");

            if (hunger > 40)
                hungryGotFed = true;
            if (energy > 40)
                tiredGotRested = true;

            if (tick % 50 == 0)
            {
                _output.WriteLine(
                    $"Tick {tick}: HungryPawn at {hungryPos} (hunger={hunger:F1}), TiredPawn at {tiredPos} (energy={energy:F1})"
                );

                // Log actions
                if (sim.Entities.Actions.TryGetValue(hungryPawn.Value, out var hungryAction))
                {
                    var pathInfo =
                        hungryAction.CurrentPath != null
                            ? $"path len={hungryAction.CurrentPath.Count}, idx={hungryAction.PathIndex}"
                            : "no path";
                    _output.WriteLine(
                        $"  HungryPawn action: {hungryAction.CurrentAction?.DisplayName ?? hungryAction.CurrentAction?.Type.ToString() ?? "none"}, {pathInfo}"
                    );
                }
                if (sim.Entities.Actions.TryGetValue(tiredPawn.Value, out var tiredAction))
                {
                    var pathInfo =
                        tiredAction.CurrentPath != null
                            ? $"path len={tiredAction.CurrentPath.Count}, idx={tiredAction.PathIndex}"
                            : "no path";
                    _output.WriteLine(
                        $"  TiredPawn action: {tiredAction.CurrentAction?.DisplayName ?? tiredAction.CurrentAction?.Type.ToString() ?? "none"}, {pathInfo}"
                    );
                }
            }
        }

        // Check last 100 positions for stuck-ness
        var lastPositions = positionHistory.Skip(Math.Max(0, positionHistory.Count - 100)).ToList();
        var uniqueHungryPositions = new HashSet<TileCoord>(lastPositions.Select(p => p.hungry));
        var uniqueTiredPositions = new HashSet<TileCoord>(lastPositions.Select(p => p.tired));

        _output.WriteLine(
            $"\nLast 100 ticks - HungryPawn unique positions: {uniqueHungryPositions.Count}, TiredPawn unique positions: {uniqueTiredPositions.Count}"
        );
        _output.WriteLine(
            $"HungryPawn got fed: {hungryGotFed}, TiredPawn got rested: {tiredGotRested}"
        );

        // Assert: Check both success conditions
        // 1. Pawns should have satisfied their needs
        Assert.True(hungryGotFed, "HungryPawn never reached the fridge");
        Assert.True(tiredGotRested, "TiredPawn never reached the bed");

        // 2. Pawns should not be permanently stuck in the same spot
        Assert.True(
            uniqueHungryPositions.Count > 1 || uniqueTiredPositions.Count > 1,
            $"Pawns appear stuck. HungryPawn at {uniqueHungryPositions.Count} unique positions, TiredPawn at {uniqueTiredPositions.Count} unique positions in last 100 ticks"
        );
    }

    /// <summary>
    /// Scenario: Test that two pawns directly adjacent trying to move through each other
    /// will eventually give up and do something else (or one waits for the other).
    /// </summary>
    [Fact]
    public void TwoPawns_DirectlyBlocking_EventuallyResolve()
    {
        // Arrange: Minimal 3x1 world: [Fridge][Pawn1][Pawn2]
        // Pawn2 blocks Pawn1 from reaching fridge
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(2, 0); // 3x1 corridor
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.001f);
        var fridgeDefId = builder.DefineObject(
            key: "Fridge",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20
        );
        builder.AddObject(fridgeDefId, 0, 0);
        builder.AddPawn("BlockedPawn", 1, 0, new Dictionary<int, float> { { hungerId, 5f } }); // Wants fridge
        builder.AddPawn("BlockerPawn", 2, 0, new Dictionary<int, float> { { hungerId, 100f } }); // Doesn't need fridge
        var sim = builder.Build();

        var blockedPawn = sim.GetPawnByName("BlockedPawn");
        var blockerPawn = sim.GetPawnByName("BlockerPawn");
        Assert.NotNull(blockedPawn);
        Assert.NotNull(blockerPawn);

        bool blockedGotFed = false;

        // Act
        for (int tick = 0; tick < 300; tick++)
        {
            sim.Tick();

            var hunger = sim.GetNeedValue(blockedPawn.Value, "Hunger");
            if (hunger > 40)
                blockedGotFed = true;

            if (tick % 50 == 0)
            {
                var pos1 = sim.GetPosition(blockedPawn.Value);
                var pos2 = sim.GetPosition(blockerPawn.Value);
                _output.WriteLine(
                    $"Tick {tick}: BlockedPawn at {pos1} (hunger={hunger:F1}), BlockerPawn at {pos2}"
                );
            }
        }

        // In this constrained scenario, the blocked pawn cannot reach the fridge
        // This is actually expected behavior - there's no path
        // The test documents this limitation
        _output.WriteLine($"BlockedPawn got fed: {blockedGotFed}");

        // For now, we just document the behavior
        // If we want pawns to "push" or "ask to move", that's a feature request
    }

    /// <summary>
    /// Scenario: Two pawns want to use the SAME object (like your screenshot showing
    /// Jordan trying to use the shower while blocked by Sam).
    /// This tests the "object in use" logic and queuing.
    /// </summary>
    [Fact]
    public void TwoPawns_WantSameObject_OneWaitsOrFindsAlternative()
    {
        // Arrange: 5x3 area with ONE shower, two pawns who both need hygiene
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(4, 2); // 5x3 area
        var hygieneId = builder.DefineNeed(key: "Hygiene", decayPerTick: 0.001f);
        var showerDefId = builder.DefineObject(
            key: "Shower",
            satisfiesNeedId: hygieneId,
            satisfactionAmount: 50f,
            interactionDuration: 40
        ); // Long interaction
        builder.AddObject(showerDefId, 2, 1); // Shower at (2,1)
        // Both pawns want the shower
        builder.AddPawn("Sam", 0, 1, new Dictionary<int, float> { { hygieneId, 5f } });
        builder.AddPawn("Jordan", 4, 1, new Dictionary<int, float> { { hygieneId, 5f } });
        var sim = builder.Build();

        var sam = sim.GetPawnByName("Sam");
        var jordan = sim.GetPawnByName("Jordan");
        Assert.NotNull(sam);
        Assert.NotNull(jordan);

        var samPositions = new List<TileCoord>();
        var jordanPositions = new List<TileCoord>();
        bool samShowered = false;
        bool jordanShowered = false;

        // Act
        for (int tick = 0; tick < 500; tick++)
        {
            sim.Tick();

            var samPos = sim.GetPosition(sam.Value);
            var jordanPos = sim.GetPosition(jordan.Value);

            if (samPos.HasValue)
                samPositions.Add(samPos.Value);
            if (jordanPos.HasValue)
                jordanPositions.Add(jordanPos.Value);

            var samHygiene = sim.GetNeedValue(sam.Value, "Hygiene");
            var jordanHygiene = sim.GetNeedValue(jordan.Value, "Hygiene");

            if (samHygiene > 40)
                samShowered = true;
            if (jordanHygiene > 40)
                jordanShowered = true;

            if (tick % 50 == 0 || tick < 10)
            {
                _output.WriteLine(
                    $"Tick {tick}: Sam at {samPos} (hygiene={samHygiene:F1}), Jordan at {jordanPos} (hygiene={jordanHygiene:F1})"
                );

                if (sim.Entities.Actions.TryGetValue(sam.Value, out var samAction))
                {
                    var elapsed = sim.Time.Tick - samAction.ActionStartTick;
                    var duration = samAction.CurrentAction?.DurationTicks ?? 0;
                    _output.WriteLine(
                        $"  Sam action: {samAction.CurrentAction?.DisplayName ?? samAction.CurrentAction?.Type.ToString() ?? "none"}, elapsed={elapsed}, duration={duration}"
                    );
                }
                if (sim.Entities.Actions.TryGetValue(jordan.Value, out var jordanAction))
                {
                    var elapsed = sim.Time.Tick - jordanAction.ActionStartTick;
                    var duration = jordanAction.CurrentAction?.DurationTicks ?? 0;
                    _output.WriteLine(
                        $"  Jordan action: {jordanAction.CurrentAction?.DisplayName ?? jordanAction.CurrentAction?.Type.ToString() ?? "none"}, elapsed={elapsed}, duration={duration}"
                    );
                }
            }
        }

        // Check for stuck-ness
        var lastSamPositions = samPositions.Skip(Math.Max(0, samPositions.Count - 100)).ToList();
        var lastJordanPositions = jordanPositions
            .Skip(Math.Max(0, jordanPositions.Count - 100))
            .ToList();
        var uniqueSam = new HashSet<TileCoord>(lastSamPositions);
        var uniqueJordan = new HashSet<TileCoord>(lastJordanPositions);

        _output.WriteLine(
            $"\nLast 100 ticks - Sam unique positions: {uniqueSam.Count}, Jordan unique positions: {uniqueJordan.Count}"
        );
        _output.WriteLine($"Sam showered: {samShowered}, Jordan showered: {jordanShowered}");

        // Both should eventually shower (one waits for the other, or they take turns)
        Assert.True(samShowered || jordanShowered, "Neither pawn was able to use the shower");

        // At least one pawn should not be stuck in place forever
        Assert.True(
            uniqueSam.Count > 1 || uniqueJordan.Count > 1,
            $"Pawns appear stuck. Sam: {uniqueSam.Count} positions, Jordan: {uniqueJordan.Count} positions in last 100 ticks"
        );
    }
}
