using SimGame.Core;
using Xunit;

namespace SimGame.Tests;

/// <summary>
/// Tests for the smart delete tool that removes objects, overlay terrain, or resets to flat.
/// </summary>
public class DeleteToolTests
{
    [Fact]
    public void DeleteAtTile_WithObject_RemovesObjectOnly()
    {
        // Arrange: Create a world with grass base + path overlay + object
        var builder = new TestSimulationBuilder();

        builder.DefineTerrain("Grass", walkable: true, spriteKey: "grass");
        builder.DefineTerrain("Path", walkable: true, spriteKey: "path", isAutotiling: true);
        builder.DefineObject("Bed", "Bed");
        var sim = builder.Build();

        var grassId = sim.Content.Terrains.First(kv => kv.Value.SpriteKey == "grass").Key;
        var pathId = sim.Content.Terrains.First(kv => kv.Value.SpriteKey == "path").Key;
        var bedId = sim.Content.GetObjectId("Bed")!.Value;

        // Paint grass, then path, then place object
        sim.PaintTerrain(2, 2, grassId);
        sim.PaintTerrain(2, 2, pathId);
        sim.CreateObject(bedId, 2, 2);

        var tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(grassId, tile.BaseTerrainTypeId);
        Assert.Equal(pathId, tile.OverlayTerrainTypeId);

        // Act: First delete - should remove object only
        sim.DeleteAtTile(2, 2);

        // Assert: Object removed, path and grass remain
        tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(grassId, tile.BaseTerrainTypeId);
        Assert.Equal(pathId, tile.OverlayTerrainTypeId);
        Assert.False(sim.Entities.AllObjects().Any()); // No objects remain
    }

    [Fact]
    public void DeleteAtTile_WithOverlay_ClearsOverlayOnly()
    {
        // Arrange: Create a world with grass base + path overlay (no object)
        var builder = new TestSimulationBuilder();

        builder.DefineTerrain("Grass", walkable: true, spriteKey: "grass");
        builder.DefineTerrain("Path", walkable: true, spriteKey: "path", isAutotiling: true);
        var sim = builder.Build();

        var grassId = sim.Content.Terrains.First(kv => kv.Value.SpriteKey == "grass").Key;
        var pathId = sim.Content.Terrains.First(kv => kv.Value.SpriteKey == "path").Key;

        // Paint grass, then path
        sim.PaintTerrain(2, 2, grassId);
        sim.PaintTerrain(2, 2, pathId);

        var tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(grassId, tile.BaseTerrainTypeId);
        Assert.Equal(pathId, tile.OverlayTerrainTypeId);

        // Act: Delete - should clear overlay only
        sim.DeleteAtTile(2, 2);

        // Assert: Overlay cleared, grass base remains
        tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(grassId, tile.BaseTerrainTypeId);
        Assert.Null(tile.OverlayTerrainTypeId);
    }

    [Fact]
    public void DeleteAtTile_WithoutOverlay_ResetsToFlat()
    {
        // Arrange: Create a world with just grass base (no overlay, no object)
        var builder = new TestSimulationBuilder();

        builder.DefineTerrain("Grass", walkable: true, spriteKey: "grass");
        builder.DefineTerrain("Flat", walkable: true, spriteKey: "flat");
        var sim = builder.Build();

        var grassId = sim.Content.Terrains.First(kv => kv.Value.SpriteKey == "grass").Key;
        var flatId = sim.Content.Terrains.First(kv => kv.Value.SpriteKey == "flat").Key;

        // Paint grass
        sim.PaintTerrain(2, 2, grassId);

        var tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(grassId, tile.BaseTerrainTypeId);
        Assert.Null(tile.OverlayTerrainTypeId);

        // Act: Delete - should reset to flat
        sim.DeleteAtTile(2, 2);

        // Assert: Base reset to flat
        tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(flatId, tile.BaseTerrainTypeId);
        Assert.Null(tile.OverlayTerrainTypeId);
        Assert.Equal(0, tile.ColorIndex); // Color reset to default
    }

    [Fact]
    public void DeleteAtTile_ThreeClicks_RemovesAllLayers()
    {
        // Arrange: Create a world with grass + path + object
        var builder = new TestSimulationBuilder();

        builder.DefineTerrain("Grass", walkable: true, spriteKey: "grass");
        builder.DefineTerrain("Path", walkable: true, spriteKey: "path", isAutotiling: true);
        builder.DefineTerrain("Flat", walkable: true, spriteKey: "flat");
        builder.DefineObject("Bed", "Bed");
        var sim = builder.Build();

        var grassId = sim.Content.Terrains.First(kv => kv.Value.SpriteKey == "grass").Key;
        var pathId = sim.Content.Terrains.First(kv => kv.Value.SpriteKey == "path").Key;
        var flatId = sim.Content.Terrains.First(kv => kv.Value.SpriteKey == "flat").Key;
        var bedId = sim.Content.GetObjectId("Bed")!.Value;

        // Setup: grass + path + object
        sim.PaintTerrain(2, 2, grassId);
        sim.PaintTerrain(2, 2, pathId);
        sim.CreateObject(bedId, 2, 2);

        // Act & Assert: Click 1 - Remove object
        sim.DeleteAtTile(2, 2);
        var tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(grassId, tile.BaseTerrainTypeId);
        Assert.Equal(pathId, tile.OverlayTerrainTypeId);
        Assert.False(sim.Entities.AllObjects().Any());

        // Act & Assert: Click 2 - Clear overlay
        sim.DeleteAtTile(2, 2);
        tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(grassId, tile.BaseTerrainTypeId);
        Assert.Null(tile.OverlayTerrainTypeId);

        // Act & Assert: Click 3 - Reset to flat
        sim.DeleteAtTile(2, 2);
        tile = sim.World.GetTile(new TileCoord(2, 2));
        Assert.Equal(flatId, tile.BaseTerrainTypeId);
        Assert.Null(tile.OverlayTerrainTypeId);
    }
}
