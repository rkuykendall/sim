using System.Linq;
using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

/// <summary>
/// Tests for the color palette system that allows users to assign colors to buildings and terrain.
/// </summary>
public class ColorPaletteTests
{
    [Fact]
    public void CreateBuilding_WithColorIndex_StoresColorCorrectly()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var buildingDefId = builder.DefineBuilding();
        var sim = builder.Build();

        // Act: Create building with color index 2
        var buildingId = sim.CreateBuilding(buildingDefId, new TileCoord(2, 2), colorIndex: 2);

        // Assert: Verify the color index is stored
        Assert.True(sim.Entities.Buildings.TryGetValue(buildingId, out var buildingComp));
        Assert.Equal(2, buildingComp.ColorIndex);
    }

    [Fact]
    public void CreateBuilding_WithoutColorIndex_DefaultsToZero()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var buildingDefId = builder.DefineBuilding();
        var sim = builder.Build();

        // Act: Create building without specifying color index
        var buildingId = sim.CreateBuilding(buildingDefId, new TileCoord(2, 2));

        // Assert: Verify the color index defaults to 0 (green)
        Assert.True(sim.Entities.Buildings.TryGetValue(buildingId, out var buildingComp));
        Assert.Equal(0, buildingComp.ColorIndex);
    }

    [Fact]
    public void PaintTerrain_WithColorIndex_StoresColorCorrectly()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain(spriteKey: "grass");
        var stoneDefId = builder.DefineTerrain(spriteKey: "stone");
        var sim = builder.Build();

        // Act: Paint terrain with color index
        sim.PaintTerrain(new TileCoord(3, 3), stoneDefId, colorIndex: 3);

        // Assert: Verify the color index is stored
        var tile = sim.World.GetTile(new TileCoord(3, 3));
        Assert.Equal(3, tile.ColorIndex);
        Assert.Equal(stoneDefId, tile.BaseTerrainTypeId);
    }

    [Fact]
    public void PaintTerrain_WithoutColorIndex_DefaultsToZero()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain();
        var newDefId = builder.DefineTerrain();
        var sim = builder.Build();

        // Act: Paint terrain without specifying color index
        sim.PaintTerrain(new TileCoord(2, 2), newDefId);

        // Assert: Verify the color index defaults to 0
        var tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(0, tile.ColorIndex);
        Assert.Equal(newDefId, tile.BaseTerrainTypeId);
    }

    [Fact]
    public void RenderSnapshot_IncludesBuildingColorIndex()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var bedDefId = builder.DefineBuilding(key: "Home");
        var sim = builder.Build();

        // Create buildings with different colors
        sim.CreateBuilding(bedDefId, new TileCoord(1, 1), colorIndex: 1);
        sim.CreateBuilding(bedDefId, new TileCoord(2, 2), colorIndex: 2);
        sim.CreateBuilding(bedDefId, new TileCoord(3, 3), colorIndex: 3);

        // Act: Get render snapshot
        var snapshot = sim.CreateRenderSnapshot();

        // Assert: Verify all buildings have correct color indices
        Assert.Equal(3, snapshot.Buildings.Count);

        var obj1 = snapshot.Buildings.First(o => o.X == 1 && o.Y == 1);
        Assert.Equal(1, obj1.ColorIndex);

        var obj2 = snapshot.Buildings.First(o => o.X == 2 && o.Y == 2);
        Assert.Equal(2, obj2.ColorIndex);

        var obj3 = snapshot.Buildings.First(o => o.X == 3 && o.Y == 3);
        Assert.Equal(3, obj3.ColorIndex);
    }

    [Fact]
    public void MultipleBuildings_CanHaveDifferentColors()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var bedDefId = builder.DefineBuilding(key: "Home");
        var sim = builder.Build();

        // Act: Create multiple beds with different colors
        var brownBed = sim.CreateBuilding(bedDefId, new TileCoord(1, 1), colorIndex: 1);
        var redBed = sim.CreateBuilding(bedDefId, new TileCoord(2, 2), colorIndex: 2);
        var purpleBed = sim.CreateBuilding(bedDefId, new TileCoord(3, 3), colorIndex: 3);

        // Assert: Each building has its own color
        sim.Entities.Buildings.TryGetValue(brownBed, out var brownObj);
        Assert.Equal(1, brownObj!.ColorIndex);

        sim.Entities.Buildings.TryGetValue(redBed, out var redObj);
        Assert.Equal(2, redObj!.ColorIndex);

        sim.Entities.Buildings.TryGetValue(purpleBed, out var purpleObj);
        Assert.Equal(3, purpleObj!.ColorIndex);
    }

    [Fact]
    public void PaintTerrain_OverwritesPreviousColor()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var grassDefId = builder.DefineTerrain(key: "Grass", spriteKey: "grass");
        var sim = builder.Build();

        var coord = new TileCoord(2, 2);

        // Act: Paint the same tile with different colors
        sim.PaintTerrain(new TileCoord(2, 2), grassDefId, colorIndex: 1);
        var tile1 = sim.World.GetTile(coord);
        Assert.Equal(1, tile1.ColorIndex);

        sim.PaintTerrain(new TileCoord(2, 2), grassDefId, colorIndex: 2);
        var tile2 = sim.World.GetTile(coord);
        Assert.Equal(2, tile2.ColorIndex);

        sim.PaintTerrain(new TileCoord(2, 2), grassDefId, colorIndex: 3);
        var tile3 = sim.World.GetTile(coord);
        Assert.Equal(3, tile3.ColorIndex);
    }

    [Fact]
    public void DeleteBuilding_DoesNotAffectOtherBuildingColors()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var bedDefId = builder.DefineBuilding(key: "Home");
        var sim = builder.Build();
        var redBed = sim.CreateBuilding(bedDefId, new TileCoord(1, 1), colorIndex: 1);
        var blueBed = sim.CreateBuilding(bedDefId, new TileCoord(2, 2), colorIndex: 2);

        // Act: Delete the red bed
        sim.TryDeleteBuilding(new TileCoord(1, 1));

        // Assert: Blue bed still has correct color
        Assert.False(sim.Entities.Buildings.ContainsKey(redBed));
        Assert.True(sim.Entities.Buildings.TryGetValue(blueBed, out var blueObj));
        Assert.Equal(2, blueObj.ColorIndex);

        var snapshot = sim.CreateRenderSnapshot();
        Assert.Single(snapshot.Buildings);
        Assert.Equal(2, snapshot.Buildings[0].ColorIndex);
    }
}
