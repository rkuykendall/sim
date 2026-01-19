using System.Collections.Generic;
using System.Diagnostics;
using SimGame.Core;
using Xunit;
using Xunit.Abstractions;

namespace SimGame.Tests;

/// <summary>
/// Performance tests to identify bottlenecks in the simulation.
/// Run with: dotnet test --filter "FullyQualifiedName~PerformanceTests" -- xunit.diagnostics=true
/// </summary>
public class PerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Profile a small simulation (5x5 world, 1 pawn) over many ticks.
    /// </summary>
    [Fact]
    public void Profile_SmallSimulation_1000Ticks()
    {
        var builder = new TestSimulationBuilder();
        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.02f);
        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20
        );
        builder.AddBuilding(marketDefId, 4, 0);
        builder.AddPawn("TestPawn", 0, 0, new Dictionary<int, float> { { hungerId, 50f } });
        var sim = builder.Build();

        RunProfiledSimulation(sim, tickCount: 1000, label: "Small (5x5, 1 pawn)");
    }

    /// <summary>
    /// Profile a medium simulation (20x20 world, 10 pawns) over many ticks.
    /// </summary>
    [Fact]
    public void Profile_MediumSimulation_1000Ticks()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(19, 19);

        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.02f);
        var energyId = builder.DefineNeed(key: "Energy", decayPerTick: 0.01f);

        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20
        );
        var bedDefId = builder.DefineBuilding(
            key: "Bed",
            satisfiesNeedId: energyId,
            satisfactionAmount: 80f,
            interactionDuration: 100
        );

        // Add several buildings
        builder.AddBuilding(marketDefId, 5, 5);
        builder.AddBuilding(marketDefId, 15, 5);
        builder.AddBuilding(bedDefId, 5, 15);
        builder.AddBuilding(bedDefId, 15, 15);

        // Add 10 pawns spread around
        for (int i = 0; i < 10; i++)
        {
            builder.AddPawn(
                $"Pawn{i}",
                i * 2,
                10,
                new Dictionary<int, float> { { hungerId, 50f + i * 5 }, { energyId, 60f + i * 3 } }
            );
        }

        var sim = builder.Build();
        RunProfiledSimulation(sim, tickCount: 1000, label: "Medium (20x20, 10 pawns)");
    }

    /// <summary>
    /// Profile a large simulation (50x50 world, 50 pawns) over many ticks.
    /// </summary>
    [Fact]
    public void Profile_LargeSimulation_1000Ticks()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(49, 49);

        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.02f);
        var energyId = builder.DefineNeed(key: "Energy", decayPerTick: 0.01f);
        var funId = builder.DefineNeed(key: "Fun", decayPerTick: 0.015f);

        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20
        );
        var bedDefId = builder.DefineBuilding(
            key: "Bed",
            satisfiesNeedId: energyId,
            satisfactionAmount: 80f,
            interactionDuration: 100
        );
        var parkDefId = builder.DefineBuilding(
            key: "Park",
            satisfiesNeedId: funId,
            satisfactionAmount: 40f,
            interactionDuration: 30
        );

        // Add buildings in a grid pattern
        for (int x = 5; x < 50; x += 10)
        {
            for (int y = 5; y < 50; y += 10)
            {
                builder.AddBuilding(marketDefId, x, y);
                if (x + 2 < 50)
                    builder.AddBuilding(bedDefId, x + 2, y);
                if (y + 2 < 50)
                    builder.AddBuilding(parkDefId, x, y + 2);
            }
        }

        // Add 50 pawns
        for (int i = 0; i < 50; i++)
        {
            int x = (i * 7) % 50;
            int y = (i * 11) % 50;
            builder.AddPawn(
                $"Pawn{i}",
                x,
                y,
                new Dictionary<int, float>
                {
                    { hungerId, 30f + (i % 70) },
                    { energyId, 40f + (i % 60) },
                    { funId, 50f + (i % 50) },
                }
            );
        }

        var sim = builder.Build();
        RunProfiledSimulation(sim, tickCount: 1000, label: "Large (50x50, 50 pawns)");
    }

    /// <summary>
    /// Profile with themes enabled to see ThemeSystem impact.
    /// </summary>
    [Fact]
    public void Profile_WithThemes_1000Ticks()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(19, 19);
        builder.WithThemesEnabled();

        var hungerId = builder.DefineNeed(key: "Hunger", decayPerTick: 0.02f);
        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerId,
            satisfactionAmount: 50f,
            interactionDuration: 20
        );

        builder.AddBuilding(marketDefId, 10, 10);

        for (int i = 0; i < 10; i++)
        {
            builder.AddPawn($"Pawn{i}", i * 2, 5, new Dictionary<int, float> { { hungerId, 50f } });
        }

        var sim = builder.Build();
        RunProfiledSimulation(sim, tickCount: 1000, label: "With Themes (20x20, 10 pawns)");
    }

    private void RunProfiledSimulation(Simulation sim, int tickCount, string label)
    {
        // Enable profiling
        sim.Systems.SetProfilingEnabled(true);
        sim.Systems.ResetProfiles();

        // Warmup
        sim.RunTicks(100);
        sim.Systems.ResetProfiles();

        // Measure total time
        var totalStopwatch = Stopwatch.StartNew();
        sim.RunTicks(tickCount);
        totalStopwatch.Stop();

        // Output results
        _output.WriteLine($"\n=== {label} - {tickCount} ticks ===");
        _output.WriteLine($"Total time: {totalStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine(
            $"Avg per tick: {totalStopwatch.ElapsedMilliseconds / (double)tickCount:F3}ms"
        );
        _output.WriteLine("");
        _output.WriteLine("Per-system breakdown:");
        _output.WriteLine(
            $"{"System", -20} {"Total (ms)", 12} {"Avg (ms)", 12} {"% of Total", 12}"
        );
        _output.WriteLine(new string('-', 58));

        var profiles = sim.Systems.GetProfiles();
        double totalSystemMs = 0;
        foreach (var p in profiles)
            totalSystemMs += p.TotalMilliseconds;

        foreach (var profile in profiles)
        {
            double pct = totalSystemMs > 0 ? (profile.TotalMilliseconds / totalSystemMs) * 100 : 0;
            _output.WriteLine(
                $"{profile.SystemName, -20} {profile.TotalMilliseconds, 12:F3} {profile.AverageMilliseconds, 12:F6} {pct, 11:F1}%"
            );
        }

        _output.WriteLine("");
    }
}
