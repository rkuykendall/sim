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
    /// <param name="texture">The texture atlas to use</param>
    /// <param name="terrainName">Name of the terrain</param>
    /// <param name="blocksLight">Whether tiles should block light (cast shadows)</param>
    public static TileSet CreateAutoTileSet(
        Texture2D texture,
        string terrainName,
        bool blocksLight = false
    )
    {
        var tileSet = new TileSet();
        var atlasSource = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = new Vector2I(16, 16),
        };

        // Add occlusion layer if we'll be blocking light (must be added before creating tiles)
        if (blocksLight)
        {
            tileSet.AddOcclusionLayer(-1); // -1 means append to end
            // Enable SDF collision for this occlusion layer (required for SDF shadows)
            tileSet.SetOcclusionLayerSdfCollision(0, true);
        }

        // Create terrain set and terrain
        tileSet.AddTerrainSet(0);
        tileSet.SetTerrainSetMode(0, (TileSet.TerrainMode)0); // MatchCornersAndSides
        tileSet.AddTerrain(0, 0);
        tileSet.SetTerrainName(0, 0, terrainName);
        tileSet.SetTerrainColor(0, 0, new Color(1, 1, 1)); // White color

        // Add the source to the tileset BEFORE configuring tiles
        tileSet.AddSource(atlasSource, 0);

        // Configure all 47 tiles with their peering bits (after source is added)
        Configure47TileTemplate(tileSet, atlasSource, 0, 0, blocksLight);
        return tileSet;
    }

    private static void Configure47TileTemplate(
        TileSet tileSet,
        TileSetAtlasSource atlasSource,
        int terrainSet,
        int terrain,
        bool blocksLight
    )
    {
        foreach (var pattern in AutoTileConfig.Standard47TilePatterns)
        {
            ConfigureTile(
                tileSet,
                atlasSource,
                pattern.X,
                pattern.Y,
                terrainSet,
                terrain,
                pattern.PeeringBits,
                blocksLight
            );
        }
    }

    private static void ConfigureTile(
        TileSet tileSet,
        TileSetAtlasSource atlasSource,
        int atlasX,
        int atlasY,
        int terrainSet,
        int terrain,
        byte peeringBits,
        bool blocksLight
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

        // Add light occluder if this terrain blocks light
        if (blocksLight)
        {
            var occluder = new OccluderPolygon2D();
            // Create a square polygon covering the whole tile (16x16, centered at origin)
            occluder.Polygon = new Vector2[]
            {
                new Vector2(-8, -8), // Top-left
                new Vector2(8, -8), // Top-right
                new Vector2(8, 8), // Bottom-right
                new Vector2(-8, 8), // Bottom-left
            };
            // Set the polygon count for this layer first, then set the polygon
            tileData.SetOccluderPolygonsCount(0, 1);
            tileData.SetOccluderPolygon(0, 0, occluder);
        }
    }
}
