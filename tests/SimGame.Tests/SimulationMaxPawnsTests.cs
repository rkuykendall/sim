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

        Assert.Equal(1, sim.GetMaxPawns());
    }

    [Fact]
    public void GetMaxPawns_NoHomes_ReturnsZero()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(10, 10);
        var sim = builder.Build();

        // No homes placed
        Assert.Equal(1, sim.GetMaxPawns());
    }

    [Fact]
    public void GetMaxPawns_WithHomes_ReturnsHomeCount()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(10, 10);
        var homeId = builder.DefineBuilding(key: "Home");
        var sim = builder.Build();

        // Place 5 homes
        for (int i = 0; i < 5; i++)
            sim.CreateBuilding(homeId, new TileCoord(i, 0));
        Assert.Equal(5, sim.GetMaxPawns());

        // Place 15 more homes (total 20)
        for (int i = 5; i < 20; i++)
            sim.CreateBuilding(homeId, new TileCoord(i % 10, i / 10));

        Assert.Equal(20, sim.GetMaxPawns());
    }

    [Fact]
    public void GetMaxPawns_IgnoresNonHomeBuildings()
    {
        var builder = new TestSimulationBuilder();
        builder.WithWorldBounds(10, 10);
        var homeId = builder.DefineBuilding(key: "Home");
        var marketId = builder.DefineBuilding(key: "Market");
        var sim = builder.Build();

        // Place 3 homes and 5 markets
        sim.CreateBuilding(homeId, new TileCoord(0, 0));
        sim.CreateBuilding(homeId, new TileCoord(1, 0));
        sim.CreateBuilding(homeId, new TileCoord(2, 0));
        sim.CreateBuilding(marketId, new TileCoord(3, 0));
        sim.CreateBuilding(marketId, new TileCoord(4, 0));
        sim.CreateBuilding(marketId, new TileCoord(5, 0));
        sim.CreateBuilding(marketId, new TileCoord(6, 0));
        sim.CreateBuilding(marketId, new TileCoord(7, 0));

        // Max pawns should only count homes
        Assert.Equal(3, sim.GetMaxPawns());
    }
}
