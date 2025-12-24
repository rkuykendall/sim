using Godot;

namespace SimGame.Godot;

/// <summary>
/// Programmatically builds TileSets with 47-tile autotiling.
/// </summary>
public static class AutoTileSetBuilder
{
    /// <summary>
    /// Create a TileSet with 47-tile autotiling for the given texture.
    /// </summary>
    public static TileSet CreateAutoTileSet(Texture2D texture, string terrainName)
    {
        var tileSet = new TileSet();
        var atlasSource = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = new Vector2I(16, 16),
        };

        // Create terrain set and terrain
        tileSet.AddTerrainSet(0);
        tileSet.SetTerrainSetMode(0, (TileSet.TerrainMode)0); // MatchCornersAndSides
        tileSet.AddTerrain(0, 0);
        tileSet.SetTerrainName(0, 0, terrainName);
        tileSet.SetTerrainColor(0, 0, new Color(1, 1, 1)); // White color

        // Configure all 47 tiles with their peering bits
        Configure47TileTemplate(atlasSource, 0, 0);

        tileSet.AddSource(atlasSource, 0);
        return tileSet;
    }

    private static void Configure47TileTemplate(
        TileSetAtlasSource atlasSource,
        int terrainSet,
        int terrain
    )
    {
        foreach (var pattern in AutoTileConfig.Standard47TilePatterns)
        {
            ConfigureTile(
                atlasSource,
                pattern.X,
                pattern.Y,
                terrainSet,
                terrain,
                pattern.PeeringBits
            );
        }
    }

    private static void ConfigureTile(
        TileSetAtlasSource atlasSource,
        int atlasX,
        int atlasY,
        int terrainSet,
        int terrain,
        byte peeringBits
    )
    {
        var atlasCoords = new Vector2I(atlasX, atlasY);
        atlasSource.CreateTile(atlasCoords);

        var tileData = atlasSource.GetTileData(atlasCoords, 0);
        tileData.TerrainSet = terrainSet;
        tileData.Terrain = terrain;

        // Set peering bits based on bitmask
        // Bit 0 = TopLeft, 1 = TopSide, 2 = TopRight, 3 = LeftSide, 4 = RightSide, 5 = BottomLeft, 6 = BottomSide, 7 = BottomRight
        if ((peeringBits & 0b00000001) != 0)
            tileData.SetTerrainPeeringBit(TileSet.CellNeighbor.TopLeftCorner, terrain);
        if ((peeringBits & 0b00000010) != 0)
            tileData.SetTerrainPeeringBit(TileSet.CellNeighbor.TopSide, terrain);
        if ((peeringBits & 0b00000100) != 0)
            tileData.SetTerrainPeeringBit(TileSet.CellNeighbor.TopRightCorner, terrain);
        if ((peeringBits & 0b00001000) != 0)
            tileData.SetTerrainPeeringBit(TileSet.CellNeighbor.LeftSide, terrain);
        if ((peeringBits & 0b00010000) != 0)
            tileData.SetTerrainPeeringBit(TileSet.CellNeighbor.RightSide, terrain);
        if ((peeringBits & 0b00100000) != 0)
            tileData.SetTerrainPeeringBit(TileSet.CellNeighbor.BottomLeftCorner, terrain);
        if ((peeringBits & 0b01000000) != 0)
            tileData.SetTerrainPeeringBit(TileSet.CellNeighbor.BottomSide, terrain);
        if ((peeringBits & 0b10000000) != 0)
            tileData.SetTerrainPeeringBit(TileSet.CellNeighbor.BottomRightCorner, terrain);
    }
}
