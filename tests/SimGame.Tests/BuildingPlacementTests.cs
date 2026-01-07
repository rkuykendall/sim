using System;
using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

/// <summary>
/// Tests for building placement validation logic.
/// Ensures buildings cannot be placed on top of each other.
/// </summary>
public class BuildingPlacementTests
{
    /// <summary>
    /// Test that placing a 2x2 building (Home) on the same tile twice throws an exception.
    /// </summary>
    [Fact]
    public void CreateBuilding_2x2Building_CannotOverlap()
    {
        // Arrange: Create a simulation with a 2x2 Home building
        var builder = new TestSimulationBuilder();
        var energyNeedId = builder.DefineNeed("Energy", decayPerTick: 0.01f);
        var homeDefId = builder.DefineBuilding(
            key: "Home",
            satisfiesNeedId: energyNeedId,
            grantsBuff: 10f,
            buffDuration: 2400,
            tileSize: 2
        );
        var sim = builder.Build();

        // Act & Assert: Place first home at (1,1) - should succeed
        var home1Id = sim.CreateBuilding(homeDefId, new TileCoord(1, 1));

        // Try to place second home at (1,1) - should throw because tiles are blocked
        var ex1 = Assert.Throws<InvalidOperationException>(() =>
            sim.CreateBuilding(homeDefId, new TileCoord(1, 1))
        );
        Assert.Contains("already occupied", ex1.Message);

        // Try to place second home at (1,2) - should throw because tiles overlap
        // First home occupies (1,1), (2,1), (1,2), (2,2)
        // Second home at (1,2) would occupy (1,2), (2,2), (1,3), (2,3)
        // Tiles (1,2) and (2,2) overlap with first home
        var ex2 = Assert.Throws<InvalidOperationException>(() =>
            sim.CreateBuilding(homeDefId, new TileCoord(1, 2))
        );
        Assert.Contains("already occupied", ex2.Message);

        // Try to place second home at (2,1) - should throw because tiles overlap
        var ex3 = Assert.Throws<InvalidOperationException>(() =>
            sim.CreateBuilding(homeDefId, new TileCoord(2, 1))
        );
        Assert.Contains("already occupied", ex3.Message);

        // Place second home at (3,1) - should succeed (no overlap)
        var home2Id = sim.CreateBuilding(homeDefId, new TileCoord(3, 1));
    }

    /// <summary>
    /// Test that placing a 2x2 building (Market) on the same tile twice throws an exception.
    /// </summary>
    [Fact]
    public void CreateBuilding_Market_CannotOverlap()
    {
        // Arrange: Create a simulation with a 2x2 Market building
        var builder = new TestSimulationBuilder();
        var hungerNeedId = builder.DefineNeed("Hunger", decayPerTick: 0.01f);
        var marketDefId = builder.DefineBuilding(
            key: "Market",
            satisfiesNeedId: hungerNeedId,
            grantsBuff: 15f,
            buffDuration: 2400,
            tileSize: 2
        );
        var sim = builder.Build();

        // Act & Assert: Place first market at (1,1) - should succeed
        var market1Id = sim.CreateBuilding(marketDefId, new TileCoord(1, 1));

        // Try to place second market at (1,1) - should throw because tiles are blocked
        var ex1 = Assert.Throws<InvalidOperationException>(() =>
            sim.CreateBuilding(marketDefId, new TileCoord(1, 1))
        );
        Assert.Contains("already occupied", ex1.Message);

        // Try to place second market at (1,2) - should throw because tiles overlap
        var ex2 = Assert.Throws<InvalidOperationException>(() =>
            sim.CreateBuilding(marketDefId, new TileCoord(1, 2))
        );
        Assert.Contains("already occupied", ex2.Message);

        // Place second market at (3,1) - should succeed (no overlap)
        var market2Id = sim.CreateBuilding(marketDefId, new TileCoord(3, 1));
    }

    /// <summary>
    /// Test that a 1x1 building can be placed adjacent to another 1x1 building.
    /// </summary>
    [Fact]
    public void CreateBuilding_AdjacentBuildings_Allowed()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var hygieneNeedId = builder.DefineNeed("Hygiene", decayPerTick: 0.01f);
        var wellDefId = builder.DefineBuilding(
            key: "Well",
            satisfiesNeedId: hygieneNeedId,
            grantsBuff: 10f,
            buffDuration: 2400,
            tileSize: 1
        );
        var sim = builder.Build();

        // Act & Assert: Place wells next to each other - should all succeed
        var well1 = sim.CreateBuilding(wellDefId, new TileCoord(1, 1));
        var well2 = sim.CreateBuilding(wellDefId, new TileCoord(2, 1)); // Adjacent
        var well3 = sim.CreateBuilding(wellDefId, new TileCoord(1, 2)); // Adjacent
    }
}
