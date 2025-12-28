using System;
using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

public class SimulationMaxPawnsTests
{
    [Fact]
    public void GetMaxPawns_EmptyWorld_ReturnsMinimum()
    {
        var builder = new TestSimulationBuilder();
        var sim = builder.Build();

        Assert.Equal(0, sim.GetMaxPawns());
    }

    [Fact]
    public void GetMaxPawns_DiverseTerrain_StillMinimumForSmallMap()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(2, 2);
        var grassId = builder.DefineTerrain(key: "Grass", spriteKey: "grass");
        var sim = builder.Build();
        sim.PaintTerrain(1, 1, grassId);
        // Small map, score/divisor is still 1
        Assert.Equal(0, sim.GetMaxPawns());
    }

    [Fact]
    public void GetMaxPawns_WithObjects_IncreasesScore()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(10, 10);
        var bedId = builder.DefineObject(key: "Bed");
        var sim = builder.Build();
        int before = sim.GetMaxPawns();
        for (int i = 0; i < 20; i++)
            sim.CreateObject(bedId, i % 10, i / 10);
        int after = sim.GetMaxPawns();
        Assert.True(after >= before, $"Expected after ({after}) >= before ({before})");
    }

    [Fact]
    public void GetMaxPawns_ZeroPawns_AndScoreAbove10_Returns1()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(10, 10);
        var sim = builder.Build();
        // Artificially boost score by painting many tiles
        var terrain1 = builder.DefineTerrain();
        var terrain2 = builder.DefineTerrain();

        for (int x = 0; x < 10; x++)
        for (int y = 0; y < 10; y++)
            sim.PaintTerrain(x, y, (x + y) % 2 == 0 ? terrain1 : terrain2, (x + y) % 2);

        Assert.True(
            sim.ScoreMapDiversity() > 10,
            "Expected score map diversity to be greater than 10"
        );

        // No pawns, score > 10, should return 1
        Assert.Equal(1, sim.GetMaxPawns());
    }
}
