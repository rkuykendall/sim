using System.Collections.Generic;
using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

public class EconomyTests
{
    // ===========================================
    // Integration Tests
    // ===========================================

    [Fact]
    public void Economy_ThreeHomesThreeFarms_ObserveAfter30Days()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(20, 20);
        builder.WithThemesEnabled();

        // Define needs
        int restNeedId = builder.DefineNeed("rest", decayPerTick: 0.02f);
        int hungerNeedId = builder.DefineNeed("hunger", decayPerTick: 0.03f);
        int purposeNeedId = builder.DefineNeed("purpose", decayPerTick: 0.01f);

        // Define a Home: FREE to use (baseCost = 0)
        // Named "Home" so simulation will spawn pawns for each one
        int homeId = builder.DefineBuilding(
            "Home",
            satisfiesNeedId: restNeedId,
            satisfactionAmount: 80f,
            interactionDuration: 50,
            baseCost: 0
        );

        // Define a Farm: costs 10g to use (eat), can be worked at for payout
        int farmId = builder.DefineBuilding(
            "Farm",
            satisfiesNeedId: hungerNeedId,
            satisfactionAmount: 60f,
            interactionDuration: 30,
            baseCost: 10,
            baseProduction: 2.0f, // Payout = 20g
            canBeWorkedAt: true,
            resourceType: "food",
            maxResourceAmount: 100f
        );

        // Place 3 homes (simulation will spawn up to 3 pawns)
        builder.AddBuilding(homeId, 3, 3);
        builder.AddBuilding(homeId, 7, 3);
        builder.AddBuilding(homeId, 11, 3);

        // Place 3 farms
        builder.AddBuilding(farmId, 3, 10);
        builder.AddBuilding(farmId, 7, 10);
        builder.AddBuilding(farmId, 11, 10);

        // No pawns added - let them spawn naturally with 100g each

        var sim = builder.Build();

        // Calculate ticks for 30 days
        int ticksPerDay = 10 * 60 * 24; // TicksPerMinute * MinutesPerHour * HoursPerDay
        int totalTicks = ticksPerDay * 30;

        // Run simulation
        sim.RunTicks(totalTicks);

        // Gather results
        var results = new List<string>();
        results.Add($"=== After 30 days (Day {sim.Time.Day}) ===");
        results.Add($"Total pawns: {sim.Entities.AllPawns().Count()}");

        int totalPawnGold = 0;
        int totalBuildingGold = 0;

        results.Add("\nPawns:");
        foreach (var pawnId in sim.Entities.AllPawns())
        {
            var pawn = sim.Entities.Pawns[pawnId];
            var gold = sim.Entities.Gold[pawnId].Amount;
            totalPawnGold += gold;
            results.Add($"  {pawn.Name}: {gold}g");
        }

        results.Add("\nBuildings:");
        foreach (var buildingId in sim.Entities.AllBuildings())
        {
            var building = sim.Entities.Buildings[buildingId];
            var buildingDef = sim.Content.Buildings[building.BuildingDefId];
            var gold = sim.Entities.Gold[buildingId].Amount;
            totalBuildingGold += gold;
            results.Add($"  {buildingDef.Name}: {gold}g");
        }

        results.Add($"\nTotal pawn gold: {totalPawnGold}g");
        results.Add($"Total building gold: {totalBuildingGold}g");
        results.Add($"Total gold in economy: {totalPawnGold + totalBuildingGold}g");

        // Output results
        string output = string.Join("\n", results);
        Console.WriteLine(output);

        // Assertions
        Assert.True(
            sim.Time.Day >= 30,
            $"Should have simulated at least 30 days, got day {sim.Time.Day}"
        );

        // 3 pawns should have spawned (one per Home)
        Assert.Equal(3, sim.Entities.AllPawns().Count());

        // Gold is conserved: 3 pawns × 100g starting = 300g total
        Assert.Equal(300, totalPawnGold + totalBuildingGold);

        // No pawn went broke
        foreach (var pawnId in sim.Entities.AllPawns())
        {
            var gold = sim.Entities.Gold[pawnId].Amount;
            Assert.True(gold > 0, $"Pawn should not be broke, has {gold}g");
        }

        // Economy is roughly balanced (pawns have 40-60% of total gold)
        float pawnShare = (float)totalPawnGold / (totalPawnGold + totalBuildingGold);
        Assert.True(
            pawnShare >= 0.4f && pawnShare <= 0.6f,
            $"Economy should be balanced. Pawns have {pawnShare:P0} of gold"
        );
    }

    [Fact]
    public void Economy_VariedBuildings_ObserveStratification()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(30, 30);
        builder.WithThemesEnabled();

        // Define needs
        // Balance decay rates so pawns work roughly as often as they consume
        int restNeedId = builder.DefineNeed("rest", decayPerTick: 0.02f);
        int hungerNeedId = builder.DefineNeed("hunger", decayPerTick: 0.02f);
        int purposeNeedId = builder.DefineNeed("purpose", decayPerTick: 0.025f); // Work more often
        int funNeedId = builder.DefineNeed("fun", decayPerTick: 0.01f);

        // Home: FREE (baseCost = 0)
        int homeId = builder.DefineBuilding(
            "Home",
            satisfiesNeedId: restNeedId,
            satisfactionAmount: 80f,
            interactionDuration: 50,
            baseCost: 0
        );

        // Farm: Low cost (5g), low payout (10g), 0 buy-in - entry level job
        int farmId = builder.DefineBuilding(
            "Farm",
            satisfiesNeedId: hungerNeedId,
            satisfactionAmount: 60f,
            interactionDuration: 30,
            baseCost: 5,
            baseProduction: 2.0f, // Payout = 10g, buy-in = 0 (at threshold)
            canBeWorkedAt: true,
            resourceType: "food",
            maxResourceAmount: 100f
        );

        // Market: Medium cost (10g), medium payout (20g), 10g buy-in
        int marketId = builder.DefineBuilding(
            "Market",
            satisfiesNeedId: hungerNeedId,
            satisfactionAmount: 80f,
            interactionDuration: 25,
            baseCost: 10,
            baseProduction: 2.0f, // Payout = 20g, buy-in = 10g
            canBeWorkedAt: true,
            resourceType: "goods",
            maxResourceAmount: 100f
        );

        // Theatre: High cost (20g), high payout (60g), 30g buy-in
        int theatreId = builder.DefineBuilding(
            "Theatre",
            satisfiesNeedId: funNeedId,
            satisfactionAmount: 100f,
            interactionDuration: 60,
            baseCost: 20,
            baseProduction: 3.0f, // Payout = 60g, buy-in = 30g
            canBeWorkedAt: true,
            resourceType: "entertainment",
            maxResourceAmount: 100f
        );

        // Place 5 homes
        builder.AddBuilding(homeId, 5, 5);
        builder.AddBuilding(homeId, 10, 5);
        builder.AddBuilding(homeId, 15, 5);
        builder.AddBuilding(homeId, 20, 5);
        builder.AddBuilding(homeId, 25, 5);

        // Place varied work buildings
        builder.AddBuilding(farmId, 5, 15);
        builder.AddBuilding(farmId, 10, 15);
        builder.AddBuilding(marketId, 15, 15);
        builder.AddBuilding(marketId, 20, 15);
        builder.AddBuilding(theatreId, 25, 15);

        var sim = builder.Build();

        // Run for 30 days
        int ticksPerDay = 10 * 60 * 24;
        int totalTicks = ticksPerDay * 30;
        sim.RunTicks(totalTicks);

        // Gather results
        var results = new List<string>();
        results.Add($"=== Varied Economy After 30 Days (Day {sim.Time.Day}) ===");
        results.Add($"Total pawns: {sim.Entities.AllPawns().Count()}");

        int totalPawnGold = 0;
        int totalBuildingGold = 0;

        // Collect pawn gold and sort by wealth
        var pawnWealth = new List<(string name, int gold)>();
        foreach (var pawnId in sim.Entities.AllPawns())
        {
            var pawn = sim.Entities.Pawns[pawnId];
            var gold = sim.Entities.Gold[pawnId].Amount;
            totalPawnGold += gold;
            pawnWealth.Add((pawn.Name, gold));
        }
        pawnWealth.Sort((a, b) => b.gold.CompareTo(a.gold)); // Sort descending

        results.Add("\nPawns (sorted by wealth):");
        foreach (var (name, gold) in pawnWealth)
        {
            results.Add($"  {name}: {gold}g");
        }

        // Collect building gold
        var buildingWealth = new List<(string name, int gold)>();
        foreach (var buildingId in sim.Entities.AllBuildings())
        {
            var building = sim.Entities.Buildings[buildingId];
            var buildingDef = sim.Content.Buildings[building.BuildingDefId];
            var gold = sim.Entities.Gold[buildingId].Amount;
            totalBuildingGold += gold;
            buildingWealth.Add((buildingDef.Name, gold));
        }

        results.Add("\nBuildings:");
        foreach (var (name, gold) in buildingWealth)
        {
            results.Add($"  {name}: {gold}g");
        }

        results.Add($"\nTotal pawn gold: {totalPawnGold}g");
        results.Add($"Total building gold: {totalBuildingGold}g");
        results.Add($"Total gold in economy: {totalPawnGold + totalBuildingGold}g");

        // Calculate wealth inequality (difference between richest and poorest pawn)
        if (pawnWealth.Count > 0)
        {
            int richest = pawnWealth.First().gold;
            int poorest = pawnWealth.Last().gold;
            results.Add(
                $"\nWealth gap: {richest - poorest}g (richest: {richest}g, poorest: {poorest}g)"
            );
        }

        string output = string.Join("\n", results);
        Console.WriteLine(output);

        // === Goal-focused assertions ===

        // Simulation ran for expected duration
        Assert.True(sim.Time.Day >= 30, $"Should have simulated at least 30 days");

        // All homes spawned pawns
        Assert.Equal(5, sim.Entities.AllPawns().Count());

        // Gold is conserved (no money created or destroyed)
        int expectedGold = 5 * 100; // 5 pawns × 100g starting
        Assert.Equal(expectedGold, totalPawnGold + totalBuildingGold);

        // No pawn is broke - everyone can still participate in economy
        foreach (var pawnId in sim.Entities.AllPawns())
        {
            var gold = sim.Entities.Gold[pawnId].Amount;
            Assert.True(gold > 0, $"No pawn should be broke");
        }

        // Economy is balanced - pawns hold 30-70% of gold (not all stuck in buildings)
        float pawnShare = (float)totalPawnGold / (totalPawnGold + totalBuildingGold);
        Assert.True(
            pawnShare >= 0.3f && pawnShare <= 0.7f,
            $"Economy should be balanced. Pawns have {pawnShare:P0} of gold (expected 30-70%)"
        );

        // Wealth stratification exists but isn't extreme
        if (pawnWealth.Count > 1)
        {
            int richest = pawnWealth.First().gold;
            int poorest = pawnWealth.Last().gold;
            int wealthGap = richest - poorest;

            // Some gap should exist (stratification)
            Assert.True(wealthGap > 0, "Some wealth stratification should emerge");

            // But poorest shouldn't be destitute compared to richest
            Assert.True(
                poorest >= richest / 3,
                $"Poorest pawn ({poorest}g) shouldn't have less than 1/3 of richest ({richest}g)"
            );
        }
    }

    [Fact]
    public void Pawn_UsesHome_IsRestedAndKeepsMoney()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(10, 10);

        // Define a rest need
        int restNeedId = builder.DefineNeed("rest", decayPerTick: 0.1f);

        // Define a Home: FREE to use (baseCost = 0)
        // Homes don't participate in the economy - they level by pawn wealth instead
        int homeId = builder.DefineBuilding(
            "home",
            satisfiesNeedId: restNeedId,
            satisfactionAmount: 100f,
            interactionDuration: 10,
            baseCost: 0
        );

        // Place a home at (2,2)
        builder.AddBuilding(homeId, 2, 2);

        // Add a pawn with low rest (20) and 100 gold, next to the home
        builder.AddPawn(
            name: "Tester",
            x: 2,
            y: 3, // Adjacent to home's use area
            needs: new Dictionary<int, float> { { restNeedId, 20f } }
        );

        var sim = builder.Build();

        // Get initial state
        var pawnId = sim.GetPawnByName("Tester")!.Value;
        int initialGold = sim.Entities.Gold[pawnId].Amount;
        float initialRest = sim.GetNeedValue(pawnId, "rest");

        // Run simulation long enough for pawn to use the home
        sim.RunTicks(100);

        // Get final state
        int finalGold = sim.Entities.Gold[pawnId].Amount;
        float finalRest = sim.GetNeedValue(pawnId, "rest");

        // Assert: pawn should be more rested
        Assert.True(
            finalRest > initialRest,
            $"Rest should increase. Was {initialRest}, now {finalRest}"
        );

        // Assert: pawn should have same gold (homes are free)
        Assert.Equal(initialGold, finalGold);
    }

    // ===========================================
    // BuildingDef Formula Tests (pure unit tests)
    // ===========================================

    [Fact]
    public void GetCost_WithDefaultBaseCost_Returns10()
    {
        var building = new BuildingDef(); // Uses defaults

        Assert.Equal(10, building.GetCost());
    }

    [Fact]
    public void GetCost_AtLevel1_ScalesBy115Percent()
    {
        var building = new BuildingDef { BaseCost = 10 };

        // 10 * 1.15^1 = 11.5, truncated to 11
        Assert.Equal(11, building.GetCost(level: 1));
    }

    [Fact]
    public void GetCost_AtLevel2_ScalesExponentially()
    {
        var building = new BuildingDef { BaseCost = 10 };

        // 10 * 1.15^2 = 13.225, truncated to 13
        Assert.Equal(13, building.GetCost(level: 2));
    }

    [Fact]
    public void GetPayout_IsCostTimesBaseProduction()
    {
        var building = new BuildingDef { BaseCost = 10, BaseProduction = 2f };

        // cost(0) = 10, payout = 10 * 2 = 20
        Assert.Equal(20, building.GetPayout());
    }

    [Fact]
    public void GetWorkBuyIn_WhenPayoutAtOrBelowThreshold_ReturnsZero()
    {
        // Payout of 10 should have 0 buy-in (threshold is 10)
        var building = new BuildingDef { BaseCost = 10, BaseProduction = 1f };

        Assert.Equal(10, building.GetPayout());
        Assert.Equal(0, building.GetWorkBuyIn());
    }

    [Fact]
    public void GetWorkBuyIn_WhenPayoutAboveThreshold_ReturnsPayoutDividedBy2()
    {
        // Payout of 20 should have buy-in of 10 (20 / 2)
        var building = new BuildingDef { BaseCost = 10, BaseProduction = 2f };

        Assert.Equal(20, building.GetPayout());
        Assert.Equal(10, building.GetWorkBuyIn());
    }

    [Fact]
    public void IsGoldSource_WhenBaseCostIsZero_ReturnsTrue()
    {
        var goldMine = new BuildingDef { BaseCost = 0 };

        Assert.True(goldMine.IsGoldSource);
    }

    [Fact]
    public void IsGoldSource_WhenBaseCostIsPositive_ReturnsFalse()
    {
        var farm = new BuildingDef { BaseCost = 10 };

        Assert.False(farm.IsGoldSource);
    }
}
