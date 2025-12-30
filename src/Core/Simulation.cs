using System;
using System.Collections.Generic;
using System.Linq;

namespace SimGame.Core;

/// <summary>
/// Configuration for creating a simulation. If null/empty, uses default bootstrap.
/// </summary>
public sealed class SimulationConfig
{
    /// <summary>
    /// Seed for the random number generator. If null, uses a random seed.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// Custom world bounds. If null, uses World defaults.
    /// </summary>
    public (int MinX, int MaxX, int MinY, int MaxY)? WorldBounds { get; set; }

    /// <summary>
    /// Starting hour of day (0-23). If null, defaults to 8 (8:00 AM).
    /// </summary>
    public int? StartHour { get; set; }

    /// <summary>
    /// Objects to place in the world: (ObjectDefId, X, Y)
    /// </summary>
    public List<(int ObjectDefId, int X, int Y)> Objects { get; set; } = new();

    /// <summary>
    /// Pawns to create: (Name, X, Y, NeedsDict)
    /// </summary>
    public List<PawnConfig> Pawns { get; set; } = new();
}

public sealed class PawnConfig
{
    public string Name { get; set; } = "Pawn";
    public int X { get; set; }
    public int Y { get; set; }
    public Dictionary<int, float> Needs { get; set; } = new();
}

public sealed class Simulation
{
    public const int TickRate = 20;

    /// <summary>
    /// Spawn a new pawn every in-game day (if under max pawns).
    /// </summary>
    private const int PawnSpawnInterval = TimeService.TicksPerDay / 48;

    public World World { get; }
    public EntityManager Entities { get; } = new();
    public TimeService Time { get; }
    public Random Random { get; }
    public ContentRegistry Content { get; }
    public int Seed { get; }
    public int SelectedPaletteId { get; }

    private readonly SystemManager _systems = new();

    /// <summary>
    /// Create a simulation with content and default configuration.
    /// </summary>
    public Simulation(ContentRegistry content)
        : this(content, null) { }

    /// <summary>
    /// Create a simulation with content and custom configuration.
    /// </summary>
    public Simulation(ContentRegistry content, SimulationConfig? config)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));

        // Initialize time service with optional start hour
        Time = new TimeService(config?.StartHour ?? TimeService.DefaultStartHour);

        // Store seed for deterministic behavior (use Environment.TickCount if not provided)
        Seed = config?.Seed ?? Environment.TickCount;
        Random = new Random(Seed);

        // Select color palette deterministically based on seed
        SelectedPaletteId = SelectColorPalette(content, Seed);

        // Create world with optional custom bounds
        if (config?.WorldBounds != null)
        {
            var bounds = config.WorldBounds.Value;
            World = new World(bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY);
        }
        else
        {
            World = new World();
        }

        // Initialize all tiles with Flat terrain as the default base
        InitializeWorldTerrain();

        _systems.Add(new NeedsSystem());
        _systems.Add(new ProximitySocialSystem());
        _systems.Add(new BuffSystem());
        _systems.Add(new MoodSystem());
        _systems.Add(new ActionSystem());
        _systems.Add(new AISystem());

        // Apply custom configuration
        if (config != null)
        {
            foreach (var (objectDefId, x, y) in config.Objects)
            {
                CreateObject(objectDefId, x, y);
            }

            foreach (var pawnConfig in config.Pawns)
            {
                CreatePawn(pawnConfig);
            }
        }
    }

    /// <summary>
    /// Initialize all world tiles with Flat terrain as the default base terrain.
    /// </summary>
    private void InitializeWorldTerrain()
    {
        // Find the Flat terrain (sprite key "flat")
        var flatTerrainId = Content.Terrains.FirstOrDefault(kv => kv.Value.SpriteKey == "flat").Key;

        // If no "flat" terrain found, use terrain ID 0 as fallback
        if (flatTerrainId == 0 && !Content.Terrains.ContainsKey(0))
        {
            // No valid terrain to initialize with - tiles will remain at default (0)
            return;
        }

        // Initialize all tiles to Flat terrain
        for (int x = 0; x < World.Width; x++)
        {
            for (int y = 0; y < World.Height; y++)
            {
                var tile = World.GetTile(x, y);
                tile.BaseTerrainTypeId = flatTerrainId;

                // Set terrain properties based on terrain definition
                if (Content.Terrains.TryGetValue(flatTerrainId, out var terrainDef))
                {
                    tile.Passability = terrainDef.Passability;
                    tile.BlocksLight = terrainDef.BlocksLight;
                }
            }
        }
    }

    /// <summary>
    /// Create an object in the world at the specified position.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when objectDefId is not a valid object definition.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the tile is already occupied by another object.</exception>
    public EntityId CreateObject(int objectDefId, int x, int y, int colorIndex = 0)
    {
        if (!Content.Objects.ContainsKey(objectDefId))
            throw new ArgumentException(
                $"Unknown object definition ID: {objectDefId}",
                nameof(objectDefId)
            );

        var objDef = Content.Objects[objectDefId];
        var coord = new TileCoord(x, y);

        // Check if tile is already occupied by an object
        if (!World.GetTile(coord).Walkable)
            throw new InvalidOperationException(
                $"Cannot place object at ({x}, {y}): tile is already occupied"
            );

        // Clamp colorIndex to palette size
        int paletteSize = 1;
        if (Content.ColorPalettes.TryGetValue(SelectedPaletteId, out var paletteDef))
            paletteSize = paletteDef.Colors.Count;
        int safeColorIndex = GetSafeColorIndex(colorIndex, paletteSize);

        var id = Entities.CreateObject(coord, objectDefId, safeColorIndex);

        // Only block the tile if this object is not walkable (e.g., fridge blocks, bed doesn't)
        if (!objDef.Walkable)
        {
            World.GetTile(coord).ObjectBlocksMovement = true;
        }
        return id;
    }

    /// <summary>
    /// Destroy an entity and clean up world state (e.g., restore tile walkability for objects).
    /// </summary>
    public void DestroyEntity(EntityId id)
    {
        // If this is a non-walkable object, restore tile walkability
        if (
            Entities.Objects.TryGetValue(id, out var objComp)
            && Entities.Positions.TryGetValue(id, out var pos)
        )
        {
            var objDef = Content.Objects[objComp.ObjectDefId];
            if (!objDef.Walkable)
            {
                World.GetTile(pos.Coord).ObjectBlocksMovement = false;
            }
        }

        Entities.Destroy(id);
    }

    /// <summary>
    /// Paint terrain at a tile, updating its properties based on the terrain definition.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when terrainDefId is not a valid terrain definition.</exception>
    public void PaintTerrain(int x, int y, int terrainDefId, int colorIndex = 0)
    {
        if (!Content.Terrains.ContainsKey(terrainDefId))
            throw new ArgumentException(
                $"Unknown terrain definition ID: {terrainDefId}",
                nameof(terrainDefId)
            );

        var coord = new TileCoord(x, y);
        if (!World.IsInBounds(coord))
            return;

        var tile = World.GetTile(coord);
        var terrainDef = Content.Terrains[terrainDefId];

        // Clamp colorIndex to palette size
        int paletteSize = 1;
        if (Content.ColorPalettes.TryGetValue(SelectedPaletteId, out var paletteDef))
            paletteSize = paletteDef.Colors.Count;
        int safeColorIndex = GetSafeColorIndex(colorIndex, paletteSize);

        // Most terrains go in the overlay layer (grass, walls, paths, etc.)
        // Foundation terrains go in the base layer (flat, wood floor)
        if (terrainDef.PaintsToBase)
        {
            tile.BaseTerrainTypeId = terrainDefId;
            tile.ColorIndex = safeColorIndex;
            // Randomize variant for this terrain
            if (terrainDef.VariantCount > 1)
                tile.BaseVariantIndex = Random.Next(terrainDef.VariantCount);
            else
                tile.BaseVariantIndex = 0;
            // Keep overlay - allows grass/decorations on top of floors
        }
        else
        {
            tile.OverlayTerrainTypeId = terrainDefId;
            tile.OverlayColorIndex = safeColorIndex;
            // Randomize variant for this terrain
            if (terrainDef.VariantCount > 1)
                tile.OverlayVariantIndex = Random.Next(terrainDef.VariantCount);
            else
                tile.OverlayVariantIndex = 0;
        }

        tile.Passability = terrainDef.Passability;
        tile.BlocksLight = terrainDef.BlocksLight;
    }

    /// <summary>
    /// Flood fill all connected tiles of the same terrain and color with a new terrain and color.
    /// Only fills tiles that exactly match the starting tile's base terrain, overlay terrain, and color.
    /// </summary>
    /// <param name="startX">X coordinate of the starting tile</param>
    /// <param name="startY">Y coordinate of the starting tile</param>
    /// <param name="newTerrainId">The terrain ID to fill with</param>
    /// <param name="newColorIndex">The color index to fill with</param>
    public void FloodFill(int startX, int startY, int newTerrainId, int newColorIndex)
    {
        var start = new TileCoord(startX, startY);
        if (!World.IsInBounds(start))
            return;

        var tile = World.GetTile(start);
        int oldTileHash = tile.TileHash;

        // Check if we're already the target terrain/color
        if (tile.BaseTerrainTypeId == newTerrainId && tile.ColorIndex == newColorIndex)
            return;

        var width = World.Width;
        var height = World.Height;
        var visited = new HashSet<TileCoord>();
        var queue = new Queue<TileCoord>();
        queue.Enqueue(start);
        visited.Add(start);

        int[] dx = { 0, 1, 0, -1 };
        int[] dy = { -1, 0, 1, 0 };

        while (queue.Count > 0)
        {
            var coord = queue.Dequeue();
            var t = World.GetTile(coord);

            // Use TileHash to match identical tiles
            if (t.TileHash == oldTileHash)
            {
                PaintTerrain(coord.X, coord.Y, newTerrainId, newColorIndex);

                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = coord.X + dx[dir];
                    int ny = coord.Y + dy[dir];
                    var ncoord = new TileCoord(nx, ny);

                    if (
                        nx >= 0
                        && nx < width
                        && ny >= 0
                        && ny < height
                        && !visited.Contains(ncoord)
                    )
                    {
                        var ntile = World.GetTile(ncoord);

                        // Use TileHash for consistent tile identity checking
                        if (ntile.TileHash == oldTileHash)
                        {
                            queue.Enqueue(ncoord);
                            visited.Add(ncoord);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Paint a filled rectangle of tiles with the specified terrain and color.
    /// Returns the coordinates of all tiles that were painted.
    /// </summary>
    /// <param name="x0">Starting X coordinate (inclusive)</param>
    /// <param name="y0">Starting Y coordinate (inclusive)</param>
    /// <param name="x1">Ending X coordinate (inclusive)</param>
    /// <param name="y1">Ending Y coordinate (inclusive)</param>
    /// <param name="terrainDefId">The terrain ID to paint</param>
    /// <param name="colorIndex">The color index to use</param>
    /// <returns>Array of tile coordinates that were painted</returns>
    public TileCoord[] PaintRectangle(
        int x0,
        int y0,
        int x1,
        int y1,
        int terrainDefId,
        int colorIndex = 0
    )
    {
        // Normalize coordinates to ensure x0 <= x1 and y0 <= y1
        int minX = Math.Min(x0, x1);
        int maxX = Math.Max(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxY = Math.Max(y0, y1);

        var paintedTiles = new List<TileCoord>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                PaintTerrain(x, y, terrainDefId, colorIndex);
                paintedTiles.Add(new TileCoord(x, y));
            }
        }

        return paintedTiles.ToArray();
    }

    /// <summary>
    /// Paint only the outline of a rectangle with the specified terrain and color.
    /// Returns the coordinates of all tiles that were painted.
    /// </summary>
    /// <param name="x0">Starting X coordinate (inclusive)</param>
    /// <param name="y0">Starting Y coordinate (inclusive)</param>
    /// <param name="x1">Ending X coordinate (inclusive)</param>
    /// <param name="y1">Ending Y coordinate (inclusive)</param>
    /// <param name="terrainDefId">The terrain ID to paint</param>
    /// <param name="colorIndex">The color index to use</param>
    /// <returns>Array of tile coordinates that were painted</returns>
    public TileCoord[] PaintRectangleOutline(
        int x0,
        int y0,
        int x1,
        int y1,
        int terrainDefId,
        int colorIndex = 0
    )
    {
        // Normalize coordinates to ensure x0 <= x1 and y0 <= y1
        int minX = Math.Min(x0, x1);
        int maxX = Math.Max(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxY = Math.Max(y0, y1);

        var paintedTiles = new List<TileCoord>();

        // Paint top and bottom edges
        for (int x = minX; x <= maxX; x++)
        {
            PaintTerrain(x, minY, terrainDefId, colorIndex);
            paintedTiles.Add(new TileCoord(x, minY));
            PaintTerrain(x, maxY, terrainDefId, colorIndex);
            paintedTiles.Add(new TileCoord(x, maxY));
        }

        // Paint left and right edges (excluding corners already painted)
        for (int y = minY + 1; y < maxY; y++)
        {
            PaintTerrain(minX, y, terrainDefId, colorIndex);
            paintedTiles.Add(new TileCoord(minX, y));
            PaintTerrain(maxX, y, terrainDefId, colorIndex);
            paintedTiles.Add(new TileCoord(maxX, y));
        }

        return paintedTiles.ToArray();
    }

    /// <summary>
    /// Expands an array of tile coordinates to include all 8-neighbors of each tile.
    /// Useful for autotiling updates where changing a tile affects its neighbors.
    /// </summary>
    /// <param name="tiles">Array of tile coordinates to expand</param>
    /// <returns>Array containing all input tiles plus their 8-neighbors (deduplicated)</returns>
    public TileCoord[] GetTilesWithNeighbors(TileCoord[] tiles)
    {
        var result = new HashSet<TileCoord>();

        // 8-directional offsets (including diagonals)
        var offsets = new[]
        {
            new TileCoord(-1, -1), // top-left
            new TileCoord(0, -1), // top
            new TileCoord(1, -1), // top-right
            new TileCoord(-1, 0), // left
            new TileCoord(0, 0), // center (the tile itself)
            new TileCoord(1, 0), // right
            new TileCoord(-1, 1), // bottom-left
            new TileCoord(0, 1), // bottom
            new TileCoord(1, 1), // bottom-right
        };

        foreach (var tile in tiles)
        {
            foreach (var offset in offsets)
            {
                var neighborCoord = new TileCoord(tile.X + offset.X, tile.Y + offset.Y);
                // Only include tiles that are within world bounds
                if (World.IsInBounds(neighborCoord))
                {
                    result.Add(neighborCoord);
                }
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Delete an object at the specified position (if any). Returns true if an object was deleted.
    /// </summary>
    public bool TryDeleteObject(int x, int y)
    {
        var coord = new TileCoord(x, y);

        // Find object at this position
        foreach (var objId in Entities.AllObjects())
        {
            if (Entities.Positions.TryGetValue(objId, out var pos) && pos.Coord == coord)
            {
                DestroyEntity(objId);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Smart delete tool that removes objects, overlay terrain, or resets to flat terrain.
    /// Priority: 1) Delete object if present, 2) Clear overlay terrain if present, 3) Reset base to flat.
    /// </summary>
    public void DeleteAtTile(int x, int y)
    {
        var coord = new TileCoord(x, y);
        if (!World.IsInBounds(coord))
            return;

        // Priority 1: Try to delete an object at this position
        if (TryDeleteObject(x, y))
            return;

        var tile = World.GetTile(coord);

        // Priority 2: Clear overlay terrain if present
        if (tile.OverlayTerrainTypeId.HasValue)
        {
            tile.OverlayTerrainTypeId = null;
            // Restore terrain properties from base terrain
            if (Content.Terrains.TryGetValue(tile.BaseTerrainTypeId, out var baseTerrain))
            {
                tile.Passability = baseTerrain.Passability;
                tile.BlocksLight = baseTerrain.BlocksLight;
            }
            return;
        }

        // Priority 3: Reset base terrain to flat
        var flatTerrainId = Content.Terrains.FirstOrDefault(kv => kv.Value.SpriteKey == "flat").Key;
        if (Content.Terrains.TryGetValue(flatTerrainId, out var flatTerrain))
        {
            tile.BaseTerrainTypeId = flatTerrainId;
            tile.Passability = flatTerrain.Passability;
            tile.BlocksLight = flatTerrain.BlocksLight;
            tile.ColorIndex = 0; // Reset to default color
        }
    }

    /// <summary>
    /// Create a pawn in the world with the specified configuration.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when config contains invalid need IDs.</exception>
    public EntityId CreatePawn(PawnConfig config)
    {
        // Validate all need IDs before creating the entity
        foreach (var needId in config.Needs.Keys)
        {
            if (!Content.Needs.ContainsKey(needId))
                throw new ArgumentException(
                    $"Unknown need definition ID: {needId}",
                    nameof(config)
                );
        }

        var coord = new TileCoord(config.X, config.Y);
        return Entities.CreatePawn(coord, config.Name, config.Needs);
    }

    /// <summary>
    /// Create a pawn with default values at a random walkable location.
    /// </summary>
    /// <param name="name">The pawn's name (defaults to "Pawn")</param>
    /// <returns>The created entity ID</returns>
    /// <exception cref="InvalidOperationException">Thrown when no walkable tiles are available.</exception>
    public EntityId CreatePawn(string name = "Pawn", int age = 1)
    {
        var position =
            GetRandomWalkableTile()
            ?? throw new InvalidOperationException("No walkable tiles available to spawn pawn");

        return Entities.CreatePawn(position, name, GetFullNeeds());
    }

    /// <summary>
    /// Gets the maximum number of pawns allowed in the simulation based on map diversity and object count.
    /// </summary>
    public int GetMaxPawns()
    {
        int score = ScoreMapDiversity();
        int numPawns = Entities.AllPawns().Count();

        if (score > 10 && numPawns < 1)
        {
            return 1;
        }

        return Math.Min(10, score / 50);
    }

    /// <summary>
    /// Generates a 2D grid of diversity scores for each tile.
    /// Each cell's score is based on differences with the tile to the left and above.
    /// </summary>
    public int[,] GetDiversityMap()
    {
        int width = World.Width;
        int height = World.Height;
        int[,] scores = new int[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int xScore = 0;
                int yScore = 0;
                var tile = World.GetTile(x, y);
                var tileHash = tile.TileHash;

                // Compare to left neighbor using TileHash
                if (x > 0)
                {
                    xScore = scores[x - 1, y];
                    var xTile = World.GetTile(x - 1, y);
                    if (tileHash != xTile.TileHash)
                        xScore += 1;
                    else if (xScore > 0)
                        xScore -= 1;
                }

                // Compare to above neighbor using TileHash
                if (y > 0)
                {
                    yScore = scores[x, y - 1];
                    var yTile = World.GetTile(x, y - 1);
                    if (tileHash != yTile.TileHash)
                        yScore += 1;
                    else if (yScore > 0)
                        yScore -= 1;
                }
                scores[x, y] = Math.Min(9, (xScore + yScore) / 2);
            }
        }
        return scores;
    }

    /// <summary>
    /// Scores the map based on tile diversity (adjacent tile differences).
    /// </summary>
    public int ScoreMapDiversity()
    {
        int[,] diversityMap = GetDiversityMap();
        int score = 0;
        int width = diversityMap.GetLength(0);
        int height = diversityMap.GetLength(1);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                score += diversityMap[x, y];
            }
        }
        score += Entities.AllObjects().Count();
        return score;
    }

    /// <summary>
    /// Creates a dictionary of all needs initialized to full (100f).
    /// </summary>
    private Dictionary<int, float> GetFullNeeds()
    {
        var needs = new Dictionary<int, float>();
        foreach (var needId in Content.Needs.Keys)
        {
            needs[needId] = 100f;
        }
        return needs;
    }

    /// <summary>
    /// Finds a random walkable edge tile that is not occupied by a pawn.
    /// Edge tiles are on the borders of the world (x=0, x=Width-1, y=0, or y=Height-1).
    /// </summary>
    /// <returns>A random unoccupied walkable edge tile coordinate, or null if none available.</returns>
    private TileCoord? GetRandomWalkableTile()
    {
        var occupiedTiles = Entities.GetOccupiedTiles();
        var walkableTiles = new List<TileCoord>();

        for (int x = 0; x < World.Width; x++)
        {
            for (int y = 0; y < World.Height; y++)
            {
                // Only consider edge tiles
                bool isEdge = x == 0 || x == World.Width - 1 || y == 0 || y == World.Height - 1;
                if (!isEdge)
                    continue;

                var coord = new TileCoord(x, y);
                if (World.IsWalkable(coord) && !occupiedTiles.Contains(coord))
                {
                    walkableTiles.Add(coord);
                }
            }
        }

        if (walkableTiles.Count == 0)
            return null;

        return walkableTiles[Random.Next(walkableTiles.Count)];
    }

    /// <summary>
    /// Select a color palette deterministically based on the world seed.
    /// Uses a separate Random instance to avoid affecting the main simulation RNG.
    /// </summary>
    private int SelectColorPalette(ContentRegistry content, int seed)
    {
        if (content.ColorPalettes.Count == 0)
            throw new InvalidOperationException(
                "No color palettes loaded. Ensure content/core/palettes.lua exists and is valid."
            );

        // Use seed to deterministically select a palette
        var rng = new Random(seed);
        var paletteIds = content.ColorPalettes.Keys.ToArray();
        return paletteIds[rng.Next(paletteIds.Length)];
    }

    /// <summary>
    /// Example: When initializing test/demo objects, assign color indices safely.
    /// </summary>
    private int GetSafeColorIndex(int requestedIndex, int paletteSize)
    {
        if (paletteSize < 1)
            return 0;
        return requestedIndex % paletteSize;
    }

    public void Tick()
    {
        var ctx = new SimContext(this);
        _systems.TickAll(ctx);
        Time.AdvanceTick();

        // Spawn new pawns at intervals if under the maximum
        if (Time.Tick % PawnSpawnInterval == 0)
        {
            var currentPawnCount = Entities.AllPawns().Count();
            if (currentPawnCount < GetMaxPawns())
            {
                try
                {
                    CreatePawn();
                }
                catch (InvalidOperationException)
                {
                    // No walkable tiles available, skip spawning
                }
            }
        }
    }

    public RenderSnapshot CreateRenderSnapshot()
    {
        return RenderSnapshotBuilder.Build(this);
    }
}
