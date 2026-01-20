using System.Collections.Generic;
using System.Linq;
using SimGame.Core;
using Xunit;
using Xunit.Abstractions;

namespace SimGame.Tests;

/// <summary>
/// Tests for pawn AI decision-making when needs are critical.
/// Diagnoses issues where pawns wander instead of using available buildings.
/// </summary>
public class PawnNeedDecisionTests
{
    private readonly ITestOutputHelper _output;

    public PawnNeedDecisionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Core test: A pawn with critical Energy (0) should go to Home, not wander.
    /// </summary>
    [Fact]
    public void Pawn_WithCriticalEnergy_GoesToHome_NotWanders()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 9);

        // Define Energy need with critical threshold
        var energyNeedId = builder.DefineNeed(
            key: "Energy",
            decayPerTick: 0.0f, // No decay - we control the value
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        // Define Home building that satisfies Energy (free to use)
        var homeDefId = builder.DefineBuilding(
            key: "Home",
            satisfiesNeedId: energyNeedId,
            satisfactionAmount: 100f,
            interactionDuration: 100,
            baseCost: 0, // Free to use
            canSellToConsumers: true
        );

        // Place Home and pawn
        builder.AddBuilding(homeDefId, 5, 5);
        builder.AddPawn("TestPawn", 0, 0, new Dictionary<int, float> { { energyNeedId, 0f } }); // Critical!

        var sim = builder.Build();
        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        _output.WriteLine("=== Pawn Critical Energy Test ===");
        _output.WriteLine($"Pawn starts at (0,0) with Energy=0 (critical)");
        _output.WriteLine($"Home at (5,5) satisfies Energy, costs 0g");

        // Diagnose the initial state
        DiagnoseState(sim, pawnId.Value, energyNeedId, "Initial");

        // Run a few ticks and observe behavior
        bool foundGoingToHome = false;
        bool foundWandering = false;
        string lastAction = "";

        for (int tick = 0; tick < 50; tick++)
        {
            sim.Tick();

            var actionComp = sim.Entities.Actions[pawnId.Value];
            var actionName =
                actionComp.CurrentAction?.DisplayName
                ?? actionComp.CurrentAction?.Type.ToString()
                ?? "idle";

            if (actionName != lastAction)
            {
                _output.WriteLine($"Tick {tick}: Action changed to '{actionName}'");
                lastAction = actionName;
            }

            if (
                actionName.Contains("Home", System.StringComparison.OrdinalIgnoreCase)
                || actionName.Contains("Going to", System.StringComparison.OrdinalIgnoreCase)
            )
            {
                foundGoingToHome = true;
            }

            if (actionName.Contains("Wander", System.StringComparison.OrdinalIgnoreCase))
            {
                foundWandering = true;
                _output.WriteLine($"WARNING: Pawn is wandering at tick {tick}!");
                DiagnoseState(sim, pawnId.Value, energyNeedId, $"Tick {tick}");
            }
        }

        _output.WriteLine($"\n=== Results ===");
        _output.WriteLine($"Found going to Home: {foundGoingToHome}");
        _output.WriteLine($"Found wandering: {foundWandering}");

        Assert.True(foundGoingToHome, "Pawn with critical Energy should go to Home");
        Assert.False(
            foundWandering,
            "Pawn with critical Energy should NOT wander when Home is available"
        );
    }

    /// <summary>
    /// Test with multiple pawns and multiple homes to check for blocking issues.
    /// </summary>
    [Fact]
    public void MultiplePawns_WithCriticalEnergy_AllFindHomes()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(19, 19);

        var energyNeedId = builder.DefineNeed(
            key: "Energy",
            decayPerTick: 0.0f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        var homeDefId = builder.DefineBuilding(
            key: "Home",
            satisfiesNeedId: energyNeedId,
            satisfactionAmount: 100f,
            interactionDuration: 100,
            baseCost: 0,
            canSellToConsumers: true
        );

        // Add 3 homes
        builder.AddBuilding(homeDefId, 5, 5);
        builder.AddBuilding(homeDefId, 10, 5);
        builder.AddBuilding(homeDefId, 15, 5);

        // Add 3 pawns, all with critical energy
        builder.AddPawn("Pawn1", 0, 0, new Dictionary<int, float> { { energyNeedId, 0f } });
        builder.AddPawn("Pawn2", 0, 10, new Dictionary<int, float> { { energyNeedId, 0f } });
        builder.AddPawn("Pawn3", 0, 19, new Dictionary<int, float> { { energyNeedId, 0f } });

        var sim = builder.Build();

        _output.WriteLine("=== Multiple Pawns Critical Energy Test ===");
        _output.WriteLine("3 pawns with critical Energy, 3 Homes available");

        var pawns = sim.Entities.AllPawns().ToList();
        Assert.Equal(3, pawns.Count);

        // Track each pawn's behavior
        var pawnFoundHome = new Dictionary<EntityId, bool>();
        var pawnFoundWander = new Dictionary<EntityId, bool>();
        foreach (var p in pawns)
        {
            pawnFoundHome[p] = false;
            pawnFoundWander[p] = false;
        }

        for (int tick = 0; tick < 100; tick++)
        {
            sim.Tick();

            foreach (var pawnId in pawns)
            {
                var actionComp = sim.Entities.Actions[pawnId];
                var actionName =
                    actionComp.CurrentAction?.DisplayName
                    ?? actionComp.CurrentAction?.Type.ToString()
                    ?? "idle";

                if (actionName.Contains("Home", System.StringComparison.OrdinalIgnoreCase))
                {
                    pawnFoundHome[pawnId] = true;
                }

                if (actionName.Contains("Wander", System.StringComparison.OrdinalIgnoreCase))
                {
                    pawnFoundWander[pawnId] = true;
                    if (tick < 10) // Only log early wandering
                    {
                        _output.WriteLine($"Tick {tick}: Pawn {pawnId.Value} is wandering!");
                    }
                }
            }
        }

        _output.WriteLine($"\n=== Results ===");
        foreach (var pawnId in pawns)
        {
            _output.WriteLine(
                $"Pawn {pawnId.Value}: foundHome={pawnFoundHome[pawnId]}, foundWander={pawnFoundWander[pawnId]}"
            );
        }

        foreach (var pawnId in pawns)
        {
            Assert.True(pawnFoundHome[pawnId], $"Pawn {pawnId.Value} should have gone to Home");
        }
    }

    /// <summary>
    /// Diagnostic test that logs all the decision-making steps.
    /// </summary>
    [Fact]
    public void Diagnose_WhyPawnWanders_WhenShouldSleep()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 9);

        var energyNeedId = builder.DefineNeed(
            key: "Energy",
            decayPerTick: 0.0f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        var homeDefId = builder.DefineBuilding(
            key: "Home",
            satisfiesNeedId: energyNeedId,
            satisfactionAmount: 100f,
            interactionDuration: 100,
            baseCost: 0,
            canSellToConsumers: true
        );

        builder.AddBuilding(homeDefId, 5, 5);
        builder.AddPawn("TestPawn", 0, 0, new Dictionary<int, float> { { energyNeedId, 0f } });

        var sim = builder.Build();
        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        _output.WriteLine("=== Diagnostic Test ===");

        // Check initial state thoroughly
        DiagnoseState(sim, pawnId.Value, energyNeedId, "Before any ticks");

        // Run one tick and check
        sim.Tick();
        DiagnoseState(sim, pawnId.Value, energyNeedId, "After 1 tick");

        // Run a few more ticks
        for (int i = 0; i < 5; i++)
        {
            sim.Tick();
        }
        DiagnoseState(sim, pawnId.Value, energyNeedId, "After 6 ticks");

        // The pawn should be going to Home, not wandering
        var actionComp = sim.Entities.Actions[pawnId.Value];
        var actionName = actionComp.CurrentAction?.DisplayName ?? "none";

        _output.WriteLine($"\nFinal action: {actionName}");

        // This assertion will fail if the bug exists, helping us identify the issue
        Assert.DoesNotContain("Wander", actionName, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Helper to diagnose the current state of a pawn and buildings.
    /// </summary>
    private void DiagnoseState(Simulation sim, EntityId pawnId, int energyNeedId, string label)
    {
        _output.WriteLine($"\n--- {label} ---");

        // Pawn state
        var pawnPos = sim.Entities.Positions[pawnId];
        var pawnGold = sim.Entities.Gold.TryGetValue(pawnId, out var goldComp)
            ? goldComp.Amount
            : 0;
        var needs = sim.Entities.Needs[pawnId];
        var energyValue = needs.Needs.GetValueOrDefault(energyNeedId, -1);

        _output.WriteLine(
            $"Pawn: pos=({pawnPos.Coord.X},{pawnPos.Coord.Y}), gold={pawnGold}, energy={energyValue}"
        );

        // Check for debuffs
        if (sim.Entities.Buffs.TryGetValue(pawnId, out var buffs))
        {
            foreach (var buff in buffs.ActiveBuffs)
            {
                _output.WriteLine(
                    $"  Buff: source={buff.Source}, sourceId={buff.SourceId}, mood={buff.MoodOffset}"
                );
            }
        }

        // Action state
        var actionComp = sim.Entities.Actions[pawnId];
        var currentAction =
            actionComp.CurrentAction?.DisplayName
            ?? actionComp.CurrentAction?.Type.ToString()
            ?? "none";
        var queueCount = actionComp.ActionQueue.Count;
        _output.WriteLine($"Action: current='{currentAction}', queued={queueCount}");

        // Buildings
        _output.WriteLine("Buildings:");
        foreach (var buildingId in sim.Entities.AllBuildings())
        {
            var buildingComp = sim.Entities.Buildings[buildingId];
            var buildingDef = sim.Content.Buildings[buildingComp.BuildingDefId];
            var buildingPos = sim.Entities.Positions[buildingId];
            var buildingGold = sim.Entities.Gold.TryGetValue(buildingId, out var bgold)
                ? bgold.Amount
                : 0;

            // Count pawns targeting this building
            int targeting = 0;
            foreach (var otherId in sim.Entities.AllPawns())
            {
                if (otherId == pawnId)
                    continue;
                if (sim.Entities.Actions.TryGetValue(otherId, out var otherAction))
                {
                    if (otherAction.CurrentAction?.TargetEntity == buildingId)
                        targeting++;
                    foreach (var queuedAction in otherAction.ActionQueue)
                    {
                        if (queuedAction.TargetEntity == buildingId)
                            targeting++;
                    }
                }
            }

            _output.WriteLine(
                $"  {buildingDef.Name} @ ({buildingPos.Coord.X},{buildingPos.Coord.Y}): "
                    + $"inUse={buildingComp.InUse(sim.Entities, buildingId)}, gold={buildingGold}, "
                    + $"satisfiesNeed={buildingDef.SatisfiesNeedId}, cost={buildingDef.GetCost()}, "
                    + $"canSell={buildingDef.CanSellToConsumers}, othersTargeting={targeting}"
            );
        }
    }

    /// <summary>
    /// Realistic test with ALL game needs and buildings to see why pawns wander.
    /// This mirrors the actual game setup more closely.
    /// </summary>
    [Fact]
    public void RealisticScenario_AllNeeds_PawnShouldNotWander()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(19, 19);

        // Define ALL needs like the real game
        var energyNeedId = builder.DefineNeed(
            key: "Energy",
            decayPerTick: 0.02f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        var hungerNeedId = builder.DefineNeed(
            key: "Hunger",
            decayPerTick: 0.03f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        var hygieneNeedId = builder.DefineNeed(
            key: "Hygiene",
            decayPerTick: 0.01f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -15f,
            lowDebuff: -3f
        );

        var socialNeedId = builder.DefineNeed(
            key: "Social",
            decayPerTick: 0.01f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -15f,
            lowDebuff: -3f
        );

        var purposeNeedId = builder.DefineNeed(
            key: "Purpose",
            decayPerTick: 0.015f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -15f,
            lowDebuff: -3f
        );

        // Define buildings like the real game
        var homeDefId = builder.DefineBuilding(
            key: "Home",
            satisfiesNeedId: energyNeedId,
            satisfactionAmount: 80f,
            interactionDuration: 50,
            baseCost: 0, // Free
            canSellToConsumers: true
        );

        var farmDefId = builder.DefineBuilding(
            key: "Farm",
            satisfiesNeedId: hungerNeedId,
            satisfactionAmount: 60f,
            interactionDuration: 30,
            baseCost: 5,
            baseProduction: 2.0f,
            canBeWorkedAt: true,
            resourceType: "food",
            maxResourceAmount: 100f,
            canSellToConsumers: false // Can't buy at farm!
        );

        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerNeedId,
            satisfactionAmount: 60f,
            interactionDuration: 30,
            baseCost: 5,
            baseProduction: 2.0f,
            canBeWorkedAt: true,
            resourceType: "food",
            maxResourceAmount: 100f,
            canSellToConsumers: true // Can buy at market
        );

        var wellDefId = builder.DefineBuilding(
            key: "Well",
            satisfiesNeedId: hygieneNeedId,
            satisfactionAmount: 50f,
            interactionDuration: 20,
            baseCost: 0, // Free
            canSellToConsumers: true
        );

        var tavernDefId = builder.DefineBuilding(
            key: "Tavern",
            satisfiesNeedId: socialNeedId,
            satisfactionAmount: 60f,
            interactionDuration: 30,
            baseCost: 8,
            baseProduction: 1.5f,
            canBeWorkedAt: true,
            resourceType: "drinks",
            maxResourceAmount: 100f,
            canSellToConsumers: true
        );

        // Place buildings
        builder.AddBuilding(homeDefId, 5, 5);
        builder.AddBuilding(farmDefId, 10, 5);
        builder.AddBuilding(marketDefId, 15, 5);
        builder.AddBuilding(wellDefId, 5, 10);
        builder.AddBuilding(tavernDefId, 10, 10);

        // Add a pawn with ALL needs at 0 (critical!)
        builder.AddPawn(
            "CriticalPawn",
            0,
            0,
            new Dictionary<int, float>
            {
                { energyNeedId, 0f },
                { hungerNeedId, 0f },
                { hygieneNeedId, 0f },
                { socialNeedId, 0f },
                { purposeNeedId, 0f },
            }
        );

        var sim = builder.Build();
        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Initialize buildings with resources
        foreach (var buildingId in sim.Entities.AllBuildings())
        {
            var building = sim.Entities.Buildings[buildingId];
            var buildingDef = sim.Content.Buildings[building.BuildingDefId];
            if (buildingDef.ResourceType != null)
            {
                sim.Entities.Resources[buildingId] = new ResourceComponent
                {
                    ResourceType = buildingDef.ResourceType,
                    CurrentAmount = 100f,
                    MaxAmount = buildingDef.MaxResourceAmount,
                };
            }
            if (buildingDef.CanBeWorkedAt)
            {
                sim.Entities.Gold[buildingId].Amount = 100;
            }
        }

        _output.WriteLine("=== Realistic Multi-Need Test ===");
        _output.WriteLine("Pawn at (0,0) with ALL needs at 0 (critical)");
        _output.WriteLine(
            "Buildings: Home(Energy), Farm(Hunger,noSell), Market(Hunger,sell), Well(Hygiene), Tavern(Social)"
        );

        DiagnoseState(sim, pawnId.Value, energyNeedId, "Initial");

        bool foundGoingToBuilding = false;
        bool foundWandering = false;
        string lastAction = "";

        for (int tick = 0; tick < 30; tick++)
        {
            sim.Tick();

            var actionComp = sim.Entities.Actions[pawnId.Value];
            var actionName =
                actionComp.CurrentAction?.DisplayName
                ?? actionComp.CurrentAction?.Type.ToString()
                ?? "idle";

            if (actionName != lastAction)
            {
                _output.WriteLine($"Tick {tick}: Action changed to '{actionName}'");

                // Log need urgencies
                var needs = sim.Entities.Needs[pawnId.Value];
                var needValues = new List<string>();
                foreach (var (nid, val) in needs.Needs)
                {
                    var needDef = sim.Content.Needs[nid];
                    needValues.Add($"{needDef.Name}={val:F0}");
                }
                _output.WriteLine($"  Needs: {string.Join(", ", needValues)}");

                lastAction = actionName;
            }

            if (
                actionName.Contains("Going to", System.StringComparison.OrdinalIgnoreCase)
                || actionName.Contains("Home", System.StringComparison.OrdinalIgnoreCase)
                || actionName.Contains("Market", System.StringComparison.OrdinalIgnoreCase)
                || actionName.Contains("Well", System.StringComparison.OrdinalIgnoreCase)
                || actionName.Contains("Tavern", System.StringComparison.OrdinalIgnoreCase)
                || actionName.Contains("Work", System.StringComparison.OrdinalIgnoreCase)
            )
            {
                foundGoingToBuilding = true;
            }

            if (actionName.Contains("Wander", System.StringComparison.OrdinalIgnoreCase))
            {
                foundWandering = true;
                _output.WriteLine($"WARNING: Pawn is wandering at tick {tick}!");
                DiagnoseState(sim, pawnId.Value, energyNeedId, $"Tick {tick} Wandering");
            }
        }

        _output.WriteLine($"\n=== Results ===");
        _output.WriteLine($"Found going to building: {foundGoingToBuilding}");
        _output.WriteLine($"Found wandering: {foundWandering}");

        Assert.True(foundGoingToBuilding, "Pawn with critical needs should go to a building");
        Assert.False(
            foundWandering,
            "Pawn with critical needs should NOT wander when buildings are available"
        );
    }

    /// <summary>
    /// Test what happens when Purpose is the most critical need but no work is available.
    /// This could cause issues if Purpose handling blocks other needs.
    /// </summary>
    [Fact]
    public void PurposeMostCritical_NoWorkAvailable_ShouldStillSatisfyOtherNeeds()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 9);

        var energyNeedId = builder.DefineNeed(
            key: "Energy",
            decayPerTick: 0.0f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        var purposeNeedId = builder.DefineNeed(
            key: "Purpose",
            decayPerTick: 0.0f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -25f, // Higher debuff than energy
            lowDebuff: -5f
        );

        // Only Home available (no workable buildings)
        var homeDefId = builder.DefineBuilding(
            key: "Home",
            satisfiesNeedId: energyNeedId,
            satisfactionAmount: 100f,
            interactionDuration: 50,
            baseCost: 0,
            canSellToConsumers: true
        );

        builder.AddBuilding(homeDefId, 5, 5);

        // Purpose is MORE urgent than Energy due to higher debuff
        builder.AddPawn(
            "TestPawn",
            0,
            0,
            new Dictionary<int, float>
            {
                { energyNeedId, 0f }, // Critical
                { purposeNeedId, 0f }, // Also critical, but with higher debuff
            }
        );

        var sim = builder.Build();
        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        _output.WriteLine("=== Purpose vs Energy Priority Test ===");
        _output.WriteLine("Pawn has Purpose=0 (debuff -25) and Energy=0 (debuff -20)");
        _output.WriteLine("Only Home available (satisfies Energy, not Purpose)");
        _output.WriteLine("Expected: Pawn should go to Home since Purpose can't be satisfied");

        DiagnoseState(sim, pawnId.Value, energyNeedId, "Initial");

        bool foundGoingToHome = false;
        bool foundWandering = false;

        for (int tick = 0; tick < 30; tick++)
        {
            sim.Tick();

            var actionComp = sim.Entities.Actions[pawnId.Value];
            var actionName =
                actionComp.CurrentAction?.DisplayName
                ?? actionComp.CurrentAction?.Type.ToString()
                ?? "idle";

            if (tick < 10 || actionName.Contains("Wander"))
            {
                _output.WriteLine($"Tick {tick}: {actionName}");
            }

            if (actionName.Contains("Home", System.StringComparison.OrdinalIgnoreCase))
            {
                foundGoingToHome = true;
            }

            if (actionName.Contains("Wander", System.StringComparison.OrdinalIgnoreCase))
            {
                foundWandering = true;
            }
        }

        _output.WriteLine($"\n=== Results ===");
        _output.WriteLine($"Found going to Home: {foundGoingToHome}");
        _output.WriteLine($"Found wandering: {foundWandering}");

        // The pawn should fall back to Home even though Purpose is more urgent
        Assert.True(foundGoingToHome, "Pawn should go to Home when Purpose can't be satisfied");
    }

    /// <summary>
    /// Test: When Home is in use, pawn should wait or find another Home, not wander.
    /// </summary>
    [Fact]
    public void HomeInUse_PawnShouldWaitOrFindAnother()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 9);

        var energyNeedId = builder.DefineNeed(
            key: "Energy",
            decayPerTick: 0.0f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        var homeDefId = builder.DefineBuilding(
            key: "Home",
            satisfiesNeedId: energyNeedId,
            satisfactionAmount: 100f,
            interactionDuration: 100,
            baseCost: 0,
            canSellToConsumers: true
        );

        // Only ONE home
        builder.AddBuilding(homeDefId, 5, 5);

        // Two pawns, both need sleep
        builder.AddPawn("Pawn1", 0, 0, new Dictionary<int, float> { { energyNeedId, 0f } });
        builder.AddPawn("Pawn2", 8, 8, new Dictionary<int, float> { { energyNeedId, 0f } });

        var sim = builder.Build();

        _output.WriteLine("=== Home In Use Test ===");
        _output.WriteLine("2 pawns with critical Energy, only 1 Home");
        _output.WriteLine("Expected: One pawn uses Home, other waits (maybe wanders)");

        var pawns = sim.Entities.AllPawns().ToList();
        var pawnActions = new Dictionary<EntityId, List<string>>();
        foreach (var p in pawns)
        {
            pawnActions[p] = new List<string>();
        }

        for (int tick = 0; tick < 50; tick++)
        {
            sim.Tick();

            foreach (var pawnId in pawns)
            {
                var actionComp = sim.Entities.Actions[pawnId];
                var actionName =
                    actionComp.CurrentAction?.DisplayName
                    ?? actionComp.CurrentAction?.Type.ToString()
                    ?? "idle";

                if (pawnActions[pawnId].Count == 0 || pawnActions[pawnId].Last() != actionName)
                {
                    pawnActions[pawnId].Add(actionName);
                    if (tick < 20)
                    {
                        _output.WriteLine($"Tick {tick}: Pawn {pawnId.Value} -> {actionName}");
                    }
                }
            }
        }

        // At least one pawn should have gone to Home
        bool anyWentToHome = pawnActions.Values.Any(actions =>
            actions.Any(a => a.Contains("Home", System.StringComparison.OrdinalIgnoreCase))
        );

        Assert.True(anyWentToHome, "At least one pawn should go to Home");

        _output.WriteLine("\n=== Action History ===");
        foreach (var (pawnId, actions) in pawnActions)
        {
            _output.WriteLine($"Pawn {pawnId.Value}: {string.Join(" -> ", actions)}");
        }
    }

    /// <summary>
    /// Test: When pawn can't afford the building, they should wander (expected behavior).
    /// </summary>
    [Fact]
    public void PawnBroke_CantAffordBuilding_ShouldWander()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(9, 9);

        var hungerNeedId = builder.DefineNeed(
            key: "Hunger",
            decayPerTick: 0.0f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        // Market costs 10g to use
        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerNeedId,
            satisfactionAmount: 60f,
            interactionDuration: 30,
            baseCost: 10, // Costs 10g!
            resourceType: "food",
            maxResourceAmount: 100f,
            canSellToConsumers: true
        );

        builder.AddBuilding(marketDefId, 5, 5);

        // Pawn with 0g can't afford market
        builder.AddPawn("BrokePawn", 0, 0, new Dictionary<int, float> { { hungerNeedId, 0f } });

        var sim = builder.Build();
        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Set pawn to broke
        sim.Entities.Gold[pawnId.Value].Amount = 0;

        // Initialize market with food
        var marketId = sim.Entities.AllBuildings().First();
        sim.Entities.Resources[marketId] = new ResourceComponent
        {
            ResourceType = "food",
            CurrentAmount = 100f,
            MaxAmount = 100f,
        };

        _output.WriteLine("=== Broke Pawn Test ===");
        _output.WriteLine("Pawn has 0g, Market costs 10g");
        _output.WriteLine("Expected: Pawn should wander (can't afford food)");

        DiagnoseState(sim, pawnId.Value, hungerNeedId, "Initial");

        bool foundWandering = false;
        bool foundGoingToMarket = false;

        for (int tick = 0; tick < 30; tick++)
        {
            sim.Tick();

            var actionComp = sim.Entities.Actions[pawnId.Value];
            var actionName = actionComp.CurrentAction?.DisplayName ?? "idle";

            if (tick < 10)
            {
                _output.WriteLine($"Tick {tick}: {actionName}");
            }

            if (actionName.Contains("Wander", System.StringComparison.OrdinalIgnoreCase))
            {
                foundWandering = true;
            }
            if (actionName.Contains("Market", System.StringComparison.OrdinalIgnoreCase))
            {
                foundGoingToMarket = true;
            }
        }

        _output.WriteLine($"\n=== Results ===");
        _output.WriteLine($"Found wandering: {foundWandering}");
        _output.WriteLine($"Found going to Market: {foundGoingToMarket}");

        // Pawn SHOULD wander because they can't afford anything
        Assert.True(foundWandering, "Broke pawn should wander when they can't afford any building");
        Assert.False(foundGoingToMarket, "Broke pawn should NOT go to Market they can't afford");
    }

    /// <summary>
    /// Test: Mix of free and paid buildings - pawn should use free one.
    /// </summary>
    [Fact]
    public void BrokePawn_FreeHomeAvailable_ShouldGoToHome()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(14, 9);

        var energyNeedId = builder.DefineNeed(
            key: "Energy",
            decayPerTick: 0.0f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        var hungerNeedId = builder.DefineNeed(
            key: "Hunger",
            decayPerTick: 0.0f,
            criticalThreshold: 15f,
            lowThreshold: 35f,
            criticalDebuff: -20f,
            lowDebuff: -5f
        );

        // Home is FREE
        var homeDefId = builder.DefineBuilding(
            key: "Home",
            satisfiesNeedId: energyNeedId,
            satisfactionAmount: 100f,
            interactionDuration: 50,
            baseCost: 0,
            canSellToConsumers: true
        );

        // Market costs 10g
        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerNeedId,
            satisfactionAmount: 60f,
            interactionDuration: 30,
            baseCost: 10,
            resourceType: "food",
            maxResourceAmount: 100f,
            canSellToConsumers: true
        );

        builder.AddBuilding(homeDefId, 5, 5);
        builder.AddBuilding(marketDefId, 10, 5);

        // Pawn with 0g, both needs critical
        builder.AddPawn(
            "BrokePawn",
            0,
            0,
            new Dictionary<int, float> { { energyNeedId, 0f }, { hungerNeedId, 0f } }
        );

        var sim = builder.Build();
        var pawnId = sim.GetFirstPawn();
        Assert.NotNull(pawnId);

        // Set pawn to broke
        sim.Entities.Gold[pawnId.Value].Amount = 0;

        // Initialize market with food
        var marketId = sim.Entities.AllBuildings().Last();
        sim.Entities.Resources[marketId] = new ResourceComponent
        {
            ResourceType = "food",
            CurrentAmount = 100f,
            MaxAmount = 100f,
        };

        _output.WriteLine("=== Broke Pawn With Free Home Test ===");
        _output.WriteLine("Pawn has 0g, Energy=0, Hunger=0");
        _output.WriteLine("Home is free (satisfies Energy)");
        _output.WriteLine("Market costs 10g (satisfies Hunger)");
        _output.WriteLine("Expected: Pawn should go to free Home");

        DiagnoseState(sim, pawnId.Value, energyNeedId, "Initial");

        bool foundGoingToHome = false;
        bool foundWandering = false;

        for (int tick = 0; tick < 30; tick++)
        {
            sim.Tick();

            var actionComp = sim.Entities.Actions[pawnId.Value];
            var actionName = actionComp.CurrentAction?.DisplayName ?? "idle";

            if (tick < 10)
            {
                _output.WriteLine($"Tick {tick}: {actionName}");
            }

            if (actionName.Contains("Home", System.StringComparison.OrdinalIgnoreCase))
            {
                foundGoingToHome = true;
            }
            if (actionName.Contains("Wander", System.StringComparison.OrdinalIgnoreCase))
            {
                foundWandering = true;
            }
        }

        _output.WriteLine($"\n=== Results ===");
        _output.WriteLine($"Found going to Home: {foundGoingToHome}");
        _output.WriteLine($"Found wandering: {foundWandering}");

        Assert.True(foundGoingToHome, "Broke pawn should go to free Home");
        Assert.False(foundWandering, "Broke pawn should NOT wander when free Home available");
    }
}
