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
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 5, 0, 5)
            .DefineObject("TestObject", "Test Object")
            .Build();

        var objectDefId = sim.Content.GetObjectId("TestObject");
        Assert.NotNull(objectDefId);

        // Act: Create object with color index 5 (blue)
        var objectId = sim.CreateObject(objectDefId.Value, 2, 2, colorIndex: 5);

        // Assert: Verify the color index is stored
        Assert.True(sim.Entities.Objects.TryGetValue(objectId, out var objComp));
        Assert.Equal(5, objComp.ColorIndex);
    }

    [Fact]
    public void CreateObject_WithoutColorIndex_DefaultsToZero()
    {
        // Arrange
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 5, 0, 5)
            .DefineObject("TestObject", "Test Object")
            .Build();

        var objectDefId = sim.Content.GetObjectId("TestObject");
        Assert.NotNull(objectDefId);

        // Act: Create object without specifying color index
        var objectId = sim.CreateObject(objectDefId.Value, 2, 2);

        // Assert: Verify the color index defaults to 0 (green)
        Assert.True(sim.Entities.Objects.TryGetValue(objectId, out var objComp));
        Assert.Equal(0, objComp.ColorIndex);
    }

    [Fact]
    public void PaintTerrain_WithColorIndex_StoresColorCorrectly()
    {
        // Arrange
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 5, 0, 5)
            .DefineTerrain("Grass", "Grass", walkable: true)
            .DefineTerrain("Stone", "Stone", walkable: true)
            .Build();

        var stoneDefId = sim.Content.GetTerrainId("Stone");
        Assert.NotNull(stoneDefId);

        // Act: Paint terrain with color index 4 (dark gray)
        sim.PaintTerrain(3, 3, stoneDefId.Value, colorIndex: 4);

        // Assert: Verify the color index is stored
        var tile = sim.World.GetTile(new TileCoord(3, 3));
        Assert.Equal(4, tile.ColorIndex);
        Assert.Equal(stoneDefId.Value, tile.TerrainTypeId);
    }

    [Fact]
    public void PaintTerrain_WithoutColorIndex_DefaultsToZero()
    {
        // Arrange
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 5, 0, 5)
            .DefineTerrain("Grass", "Grass", walkable: true)
            .DefineTerrain("Concrete", "Concrete", walkable: true)
            .Build();

        var concreteDefId = sim.Content.GetTerrainId("Concrete");
        Assert.NotNull(concreteDefId);

        // Act: Paint terrain without specifying color index
        sim.PaintTerrain(2, 2, concreteDefId.Value);

        // Assert: Verify the color index defaults to 0 (green)
        var tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(0, tile.ColorIndex);
        Assert.Equal(concreteDefId.Value, tile.TerrainTypeId);
    }

    [Fact]
    public void RenderSnapshot_IncludesObjectColorIndex()
    {
        // Arrange
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 5, 0, 5)
            .DefineObject("Bed", "Bed")
            .Build();

        var bedDefId = sim.Content.GetObjectId("Bed");
        Assert.NotNull(bedDefId);

        // Create objects with different colors
        sim.CreateObject(bedDefId.Value, 1, 1, colorIndex: 1); // Brown
        sim.CreateObject(bedDefId.Value, 2, 2, colorIndex: 6); // Red
        sim.CreateObject(bedDefId.Value, 3, 3, colorIndex: 11); // White

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
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 5, 0, 5)
            .DefineObject("Bed", "Bed")
            .Build();

        var bedDefId = sim.Content.GetObjectId("Bed");
        Assert.NotNull(bedDefId);

        // Act: Create multiple beds with different colors
        var brownBed = sim.CreateObject(bedDefId.Value, 1, 1, colorIndex: 1);
        var redBed = sim.CreateObject(bedDefId.Value, 2, 2, colorIndex: 6);
        var purpleBed = sim.CreateObject(bedDefId.Value, 3, 3, colorIndex: 8);

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
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 5, 0, 5)
            .DefineTerrain("Grass", "Grass", walkable: true)
            .Build();

        var grassDefId = sim.Content.GetTerrainId("Grass");
        Assert.NotNull(grassDefId);

        var coord = new TileCoord(2, 2);

        // Act: Paint the same tile with different colors
        sim.PaintTerrain(2, 2, grassDefId.Value, colorIndex: 1); // Brown
        var tile1 = sim.World.GetTile(coord);
        Assert.Equal(1, tile1.ColorIndex);

        sim.PaintTerrain(2, 2, grassDefId.Value, colorIndex: 6); // Red
        var tile2 = sim.World.GetTile(coord);
        Assert.Equal(6, tile2.ColorIndex);

        sim.PaintTerrain(2, 2, grassDefId.Value, colorIndex: 10); // Cyan
        var tile3 = sim.World.GetTile(coord);
        Assert.Equal(10, tile3.ColorIndex);
    }

    [Fact]
    public void DeleteObject_DoesNotAffectOtherObjectColors()
    {
        // Arrange
        var sim = new TestSimulationBuilder()
            .WithWorldBounds(0, 5, 0, 5)
            .DefineObject("Bed", "Bed")
            .Build();

        var bedDefId = sim.Content.GetObjectId("Bed");
        Assert.NotNull(bedDefId);

        var redBed = sim.CreateObject(bedDefId.Value, 1, 1, colorIndex: 6);
        var blueBed = sim.CreateObject(bedDefId.Value, 2, 2, colorIndex: 5);

        // Act: Delete the red bed
        sim.TryDeleteObject(1, 1);

        // Assert: Blue bed still has correct color
        Assert.False(sim.Entities.Objects.ContainsKey(redBed));
        Assert.True(sim.Entities.Objects.TryGetValue(blueBed, out var blueObj));
        Assert.Equal(5, blueObj!.ColorIndex);

        var snapshot = sim.CreateRenderSnapshot();
        Assert.Single(snapshot.Objects);
        Assert.Equal(5, snapshot.Objects[0].ColorIndex);
    }

    [Fact]
    public void BootstrappedObjects_HaveVariedColors()
    {
        // Arrange & Act: Create simulation with default bootstrap
        var content = ContentLoader.LoadAll(System.IO.Path.Combine(
            System.IO.Directory.GetCurrentDirectory(),
            "../../../../../content"
        ));
        var sim = new Simulation(content);

        // Assert: Bootstrapped objects should have intentionally varied colors (for debugging/demo)
        // This ensures the color system works out of the box
        var snapshot = sim.CreateRenderSnapshot();
        Assert.True(snapshot.Objects.Count > 0, "Should have bootstrapped objects");

        // Verify at least some objects have non-zero colors (proves color system is working)
        var coloredObjects = snapshot.Objects.Where(o => o.ColorIndex != 0).ToList();
        Assert.True(coloredObjects.Count > 0, "Should have some colored objects in bootstrap");
    }
}
