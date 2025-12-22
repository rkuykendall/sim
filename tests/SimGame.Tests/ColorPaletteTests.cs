using System.Linq;
using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

/// <summary>
/// Tests for the color palette system that allows users to assign colors to objects and terrain.
/// </summary>
public class ColorPaletteTests
{
    [Fact]
    public void CreateObject_WithColorIndex_StoresColorCorrectly()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var objectDefId = builder.DefineObject();
        var sim = builder.Build();

        // Act: Create object with color index 5 (blue)
        var objectId = sim.CreateObject(objectDefId, 2, 2, colorIndex: 5);

        // Assert: Verify the color index is stored
        Assert.True(sim.Entities.Objects.TryGetValue(objectId, out var objComp));
        Assert.Equal(5, objComp.ColorIndex);
    }

    [Fact]
    public void CreateObject_WithoutColorIndex_DefaultsToZero()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var objectDefId = builder.DefineObject();
        var sim = builder.Build();

        // Act: Create object without specifying color index
        var objectId = sim.CreateObject(objectDefId, 2, 2);

        // Assert: Verify the color index defaults to 0 (green)
        Assert.True(sim.Entities.Objects.TryGetValue(objectId, out var objComp));
        Assert.Equal(0, objComp.ColorIndex);
    }

    [Fact]
    public void PaintTerrain_WithColorIndex_StoresColorCorrectly()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain(spriteKey: "grass");
        var stoneDefId = builder.DefineTerrain(spriteKey: "stone");
        var sim = builder.Build();

        // Act: Paint terrain with color index 4 (dark gray)
        sim.PaintTerrain(3, 3, stoneDefId, colorIndex: 4);

        // Assert: Verify the color index is stored
        var tile = sim.World.GetTile(new TileCoord(3, 3));
        Assert.Equal(4, tile.ColorIndex);
        Assert.Equal(stoneDefId, tile.BaseTerrainTypeId);
    }

    [Fact]
    public void PaintTerrain_WithoutColorIndex_DefaultsToZero()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        builder.DefineTerrain(spriteKey: "grass");
        var concreteDefId = builder.DefineTerrain(spriteKey: "concrete");
        var sim = builder.Build();

        // Act: Paint terrain without specifying color index
        sim.PaintTerrain(2, 2, concreteDefId);

        // Assert: Verify the color index defaults to 0 (green)
        var tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(0, tile.ColorIndex);
        Assert.Equal(concreteDefId, tile.BaseTerrainTypeId);
    }

    [Fact]
    public void RenderSnapshot_IncludesObjectColorIndex()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var bedDefId = builder.DefineObject(key: "Bed");
        var sim = builder.Build();

        // Create objects with different colors
        sim.CreateObject(bedDefId, 1, 1, colorIndex: 1); // Brown
        sim.CreateObject(bedDefId, 2, 2, colorIndex: 6); // Red
        sim.CreateObject(bedDefId, 3, 3, colorIndex: 11); // White

        // Act: Get render snapshot
        var snapshot = sim.CreateRenderSnapshot();

        // Assert: Verify all objects have correct color indices
        Assert.Equal(3, snapshot.Objects.Count);

        var obj1 = snapshot.Objects.First(o => o.X == 1 && o.Y == 1);
        Assert.Equal(1, obj1.ColorIndex);

        var obj2 = snapshot.Objects.First(o => o.X == 2 && o.Y == 2);
        Assert.Equal(6, obj2.ColorIndex);

        var obj3 = snapshot.Objects.First(o => o.X == 3 && o.Y == 3);
        Assert.Equal(11, obj3.ColorIndex);
    }

    [Fact]
    public void MultipleObjects_CanHaveDifferentColors()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var bedDefId = builder.DefineObject(key: "Bed");
        var sim = builder.Build();

        // Act: Create multiple beds with different colors
        var brownBed = sim.CreateObject(bedDefId, 1, 1, colorIndex: 1);
        var redBed = sim.CreateObject(bedDefId, 2, 2, colorIndex: 6);
        var purpleBed = sim.CreateObject(bedDefId, 3, 3, colorIndex: 8);

        // Assert: Each object has its own color
        sim.Entities.Objects.TryGetValue(brownBed, out var brownObj);
        Assert.Equal(1, brownObj!.ColorIndex);

        sim.Entities.Objects.TryGetValue(redBed, out var redObj);
        Assert.Equal(6, redObj!.ColorIndex);

        sim.Entities.Objects.TryGetValue(purpleBed, out var purpleObj);
        Assert.Equal(8, purpleObj!.ColorIndex);
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
        sim.PaintTerrain(2, 2, grassDefId, colorIndex: 1); // Brown
        var tile1 = sim.World.GetTile(coord);
        Assert.Equal(1, tile1.ColorIndex);

        sim.PaintTerrain(2, 2, grassDefId, colorIndex: 6); // Red
        var tile2 = sim.World.GetTile(coord);
        Assert.Equal(6, tile2.ColorIndex);

        sim.PaintTerrain(2, 2, grassDefId, colorIndex: 10); // Cyan
        var tile3 = sim.World.GetTile(coord);
        Assert.Equal(10, tile3.ColorIndex);
    }

    [Fact]
    public void DeleteObject_DoesNotAffectOtherObjectColors()
    {
        // Arrange
        var builder = new TestSimulationBuilder();
        var bedDefId = builder.DefineObject(key: "Bed");
        var sim = builder.Build();
        var redBed = sim.CreateObject(bedDefId, 1, 1, colorIndex: 6);
        var blueBed = sim.CreateObject(bedDefId, 2, 2, colorIndex: 5);

        // Act: Delete the red bed
        sim.TryDeleteObject(1, 1);

        // Assert: Blue bed still has correct color
        Assert.False(sim.Entities.Objects.ContainsKey(redBed));
        Assert.True(sim.Entities.Objects.TryGetValue(blueBed, out var blueObj));
        Assert.Equal(5, blueObj.ColorIndex);

        var snapshot = sim.CreateRenderSnapshot();
        Assert.Single(snapshot.Objects);
        Assert.Equal(5, snapshot.Objects[0].ColorIndex);
    }
}
