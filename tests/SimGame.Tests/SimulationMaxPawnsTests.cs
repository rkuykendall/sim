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
    public void GetMaxPawns_NoBeds_ReturnsZero()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(10, 10);
        var sim = builder.Build();
        // No beds placed
        Assert.Equal(0, sim.GetMaxPawns());
    }

    [Fact]
    public void GetMaxPawns_WithBeds_ReturnsBedCount()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(10, 10);
        var bedId = builder.DefineBuilding(key: "Home");
        var sim = builder.Build();

        // Place 5 beds
        for (int i = 0; i < 5; i++)
            sim.CreateBuilding(bedId, new TileCoord(i, 0));

        Assert.Equal(5, sim.GetMaxPawns());

        // Place 15 more beds (total 20)
        for (int i = 5; i < 20; i++)
            sim.CreateBuilding(bedId, new TileCoord(i % 10, i / 10));

        Assert.Equal(20, sim.GetMaxPawns());
    }

    [Fact]
    public void GetMaxPawns_IgnoresNonBedBuildings()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(10, 10);
        var bedId = builder.DefineBuilding(key: "Home");
        var fridgeId = builder.DefineBuilding(key: "Fridge");
        var sim = builder.Build();

        // Place 3 beds and 5 fridges
        sim.CreateBuilding(bedId, new TileCoord(0, 0));
        sim.CreateBuilding(bedId, new TileCoord(1, 0));
        sim.CreateBuilding(bedId, new TileCoord(2, 0));
        sim.CreateBuilding(fridgeId, new TileCoord(3, 0));
        sim.CreateBuilding(fridgeId, new TileCoord(4, 0));
        sim.CreateBuilding(fridgeId, new TileCoord(5, 0));
        sim.CreateBuilding(fridgeId, new TileCoord(6, 0));
        sim.CreateBuilding(fridgeId, new TileCoord(7, 0));

        // Max pawns should only count beds
        Assert.Equal(3, sim.GetMaxPawns());
    }
}
