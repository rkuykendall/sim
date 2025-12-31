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

        Time = new TimeService(config?.StartHour ?? TimeService.DefaultStartHour);

        Seed = config?.Seed ?? Environment.TickCount;
        Random = new Random(Seed);

        SelectedPaletteId = SelectColorPalette(content, Seed);

        if (config?.WorldBounds != null)
        {
            var bounds = config.WorldBounds.Value;
            World = new World(bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY);
        }
        else
        {
            World = new World();
        }

        InitializeWorldTerrain();

        _systems.Add(new NeedsSystem());
        _systems.Add(new ProximitySocialSystem());
        _systems.Add(new BuffSystem());
        _systems.Add(new MoodSystem());
        _systems.Add(new ActionSystem());
        _systems.Add(new AISystem());

        if (config != null)
        {
            foreach (var (objectDefId, x, y) in config.Objects)
            {
                CreateObject(objectDefId, new TileCoord(x, y));
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
        var flatTerrainId = Content.Terrains.FirstOrDefault(kv => kv.Value.SpriteKey == "flat").Key;

        if (flatTerrainId == 0 && !Content.Terrains.ContainsKey(0))
        {
            return;
        }

        for (int x = 0; x < World.Width; x++)
        {
            for (int y = 0; y < World.Height; y++)
            {
                var tile = World.GetTile(x, y);
                tile.BaseTerrainTypeId = flatTerrainId;

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
    public EntityId CreateObject(int objectDefId, TileCoord coord, int colorIndex = 0)
    {
        if (!Content.Objects.ContainsKey(objectDefId))
            throw new ArgumentException(
                $"Unknown object definition ID: {objectDefId}",
                nameof(objectDefId)
            );

        var objDef = Content.Objects[objectDefId];

        if (!World.GetTile(coord).Walkable)
            throw new InvalidOperationException(
                $"Cannot place object at {coord}: tile is already occupied"
            );

        int paletteSize = 1;
        if (Content.ColorPalettes.TryGetValue(SelectedPaletteId, out var paletteDef))
            paletteSize = paletteDef.Colors.Count;
        int safeColorIndex = GetSafeColorIndex(colorIndex, paletteSize);

        var id = Entities.CreateObject(coord, objectDefId, safeColorIndex);

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
    /// Returns the painted tile plus all its 8-neighbors for autotiling updates.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when terrainDefId is not a valid terrain definition.</exception>
    /// <returns>Array containing the painted tile and its neighbors (for rendering updates)</returns>
    public TileCoord[] PaintTerrain(TileCoord coord, int terrainDefId, int colorIndex = 0)
    {
        if (!Content.Terrains.ContainsKey(terrainDefId))
            throw new ArgumentException(
                $"Unknown terrain definition ID: {terrainDefId}",
                nameof(terrainDefId)
            );

        if (!World.IsInBounds(coord))
            return Array.Empty<TileCoord>();

        var tile = World.GetTile(coord);
        var terrainDef = Content.Terrains[terrainDefId];

        int paletteSize = 1;
        if (Content.ColorPalettes.TryGetValue(SelectedPaletteId, out var paletteDef))
            paletteSize = paletteDef.Colors.Count;
        int safeColorIndex = GetSafeColorIndex(colorIndex, paletteSize);

        if (terrainDef.PaintsToBase)
        {
            tile.BaseTerrainTypeId = terrainDefId;
            tile.ColorIndex = safeColorIndex;
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
            if (terrainDef.VariantCount > 1)
                tile.OverlayVariantIndex = Random.Next(terrainDef.VariantCount);
            else
                tile.OverlayVariantIndex = 0;
        }

        tile.Passability = terrainDef.Passability;
        tile.BlocksLight = terrainDef.BlocksLight;

        return GetTilesWithNeighbors(new[] { coord });
    }

    /// <summary>
    /// Calculate all tiles affected by a flood fill starting from a coordinate.
    /// Only returns tiles that match the starting tile's hash (exact terrain, overlay, and color match).
    /// </summary>
    /// <param name="start">Starting tile coordinate for the flood fill</param>
    /// <returns>Array of all tiles that match the starting tile's hash</returns>
    private TileCoord[] GetFloodTiles(TileCoord start)
    {
        if (!World.IsInBounds(start))
            return Array.Empty<TileCoord>();

        var tile = World.GetTile(start);
        int targetHash = tile.TileHash;

        var visited = new HashSet<TileCoord>();
        var queue = new Queue<TileCoord>();
        queue.Enqueue(start);
        visited.Add(start);

        int[] dx = { 0, 1, 0, -1 };
        int[] dy = { -1, 0, 1, 0 };

        while (queue.Count > 0)
        {
            var coord = queue.Dequeue();

            for (int dir = 0; dir < 4; dir++)
            {
                int nx = coord.X + dx[dir];
                int ny = coord.Y + dy[dir];
                var ncoord = new TileCoord(nx, ny);

                if (World.IsInBounds(ncoord) && !visited.Contains(ncoord))
                {
                    var ntile = World.GetTile(ncoord);
                    if (ntile.TileHash == targetHash)
                    {
                        queue.Enqueue(ncoord);
                        visited.Add(ncoord);
                    }
                }
            }
        }

        return visited.ToArray();
    }

    /// <summary>
    /// Flood fill all connected tiles of the same terrain and color with a new terrain and color.
    /// Only fills tiles that exactly match the starting tile's base terrain, overlay terrain, and color.
    /// Returns all affected tiles including neighbors for autotiling updates.
    /// </summary>
    /// <param name="start">Starting tile coordinate for the flood fill</param>
    /// <param name="newTerrainId">The terrain ID to fill with</param>
    /// <param name="newColorIndex">The color index to fill with</param>
    /// <returns>Array of all tiles affected by the flood fill (painted tiles and their neighbors)</returns>
    public TileCoord[] FloodFill(TileCoord start, int newTerrainId, int newColorIndex)
    {
        var tilesToPaint = GetFloodTiles(start);
        if (tilesToPaint.Length == 0)
            return Array.Empty<TileCoord>();

        // Check if we're already this terrain+color (optimization)
        var firstTile = World.GetTile(tilesToPaint[0]);
        if (firstTile.BaseTerrainTypeId == newTerrainId && firstTile.ColorIndex == newColorIndex)
            return Array.Empty<TileCoord>();

        var affectedTiles = new HashSet<TileCoord>();
        foreach (var coord in tilesToPaint)
        {
            var tiles = PaintTerrain(coord, newTerrainId, newColorIndex);
            foreach (var t in tiles)
            {
                affectedTiles.Add(t);
            }
        }

        return affectedTiles.ToArray();
    }

    /// <summary>
    /// Calculate all tiles in a filled rectangle.
    /// </summary>
    /// <param name="start">Starting corner of the rectangle</param>
    /// <param name="end">Ending corner of the rectangle</param>
    /// <returns>Array of all tile coordinates in the rectangle</returns>
    private TileCoord[] GetRectangleTiles(TileCoord start, TileCoord end)
    {
        int minX = Math.Min(start.X, end.X);
        int maxX = Math.Max(start.X, end.X);
        int minY = Math.Min(start.Y, end.Y);
        int maxY = Math.Max(start.Y, end.Y);

        var tiles = new List<TileCoord>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                tiles.Add(new TileCoord(x, y));
            }
        }
        return tiles.ToArray();
    }

    /// <summary>
    /// Paint a filled rectangle of tiles with the specified terrain and color.
    /// Returns the coordinates of all tiles that were painted.
    /// </summary>
    /// <param name="start">Starting corner of the rectangle</param>
    /// <param name="end">Ending corner of the rectangle</param>
    /// <param name="terrainDefId">The terrain ID to paint</param>
    /// <param name="colorIndex">The color index to use</param>
    /// <returns>Array of tile coordinates that were painted</returns>
    public TileCoord[] PaintRectangle(
        TileCoord start,
        TileCoord end,
        int terrainDefId,
        int colorIndex = 0
    )
    {
        var tilesToPaint = GetRectangleTiles(start, end);
        var affectedTiles = new HashSet<TileCoord>();

        foreach (var coord in tilesToPaint)
        {
            var tiles = PaintTerrain(coord, terrainDefId, colorIndex);
            foreach (var t in tiles)
            {
                affectedTiles.Add(t);
            }
        }

        return affectedTiles.ToArray();
    }

    /// <summary>
    /// Calculate all tiles in a rectangle outline.
    /// </summary>
    /// <param name="start">Starting corner of the rectangle</param>
    /// <param name="end">Ending corner of the rectangle</param>
    /// <returns>Array of all tile coordinates in the rectangle outline</returns>
    private TileCoord[] GetRectangleOutlineTiles(TileCoord start, TileCoord end)
    {
        int minX = Math.Min(start.X, end.X);
        int maxX = Math.Max(start.X, end.X);
        int minY = Math.Min(start.Y, end.Y);
        int maxY = Math.Max(start.Y, end.Y);

        var tiles = new HashSet<TileCoord>();

        // Top and bottom edges
        for (int x = minX; x <= maxX; x++)
        {
            tiles.Add(new TileCoord(x, minY));
            tiles.Add(new TileCoord(x, maxY));
        }

        // Left and right edges (excluding corners already added)
        for (int y = minY + 1; y < maxY; y++)
        {
            tiles.Add(new TileCoord(minX, y));
            tiles.Add(new TileCoord(maxX, y));
        }

        return tiles.ToArray();
    }

    /// <summary>
    /// Paint only the outline of a rectangle with the specified terrain and color.
    /// Returns the coordinates of all tiles that were painted.
    /// </summary>
    /// <param name="start">Starting corner of the rectangle</param>
    /// <param name="end">Ending corner of the rectangle</param>
    /// <param name="terrainDefId">The terrain ID to paint</param>
    /// <param name="colorIndex">The color index to use</param>
    /// <returns>Array of tile coordinates that were painted</returns>
    public TileCoord[] PaintRectangleOutline(
        TileCoord start,
        TileCoord end,
        int terrainDefId,
        int colorIndex = 0
    )
    {
        var tilesToPaint = GetRectangleOutlineTiles(start, end);
        var affectedTiles = new HashSet<TileCoord>();

        foreach (var coord in tilesToPaint)
        {
            var tiles = PaintTerrain(coord, terrainDefId, colorIndex);
            foreach (var t in tiles)
            {
                affectedTiles.Add(t);
            }
        }

        return affectedTiles.ToArray();
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
    public bool TryDeleteObject(TileCoord coord)
    {
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
    /// Returns the deleted tile plus all its 8-neighbors for autotiling updates.
    /// </summary>
    /// <returns>Array containing the deleted tile and its neighbors (for rendering updates)</returns>
    public TileCoord[] DeleteAtTile(TileCoord coord)
    {
        if (!World.IsInBounds(coord))
            return Array.Empty<TileCoord>();

        if (TryDeleteObject(coord))
            return GetTilesWithNeighbors(new[] { coord });

        var tile = World.GetTile(coord);

        if (tile.OverlayTerrainTypeId.HasValue)
        {
            tile.OverlayTerrainTypeId = null;
            if (Content.Terrains.TryGetValue(tile.BaseTerrainTypeId, out var baseTerrain))
            {
                tile.Passability = baseTerrain.Passability;
                tile.BlocksLight = baseTerrain.BlocksLight;
            }
            return GetTilesWithNeighbors(new[] { coord });
        }

        var flatTerrainId = Content.Terrains.FirstOrDefault(kv => kv.Value.SpriteKey == "flat").Key;
        if (Content.Terrains.TryGetValue(flatTerrainId, out var flatTerrain))
        {
            tile.BaseTerrainTypeId = flatTerrainId;
            tile.Passability = flatTerrain.Passability;
            tile.BlocksLight = flatTerrain.BlocksLight;
            tile.ColorIndex = 0;
        }

        return GetTilesWithNeighbors(new[] { coord });
    }

    /// <summary>
    /// Flood delete all connected tiles that match the starting tile.
    /// Applies DeleteAtTile logic to each tile in the contiguous area.
    /// Returns all affected tiles including neighbors for autotiling updates.
    /// </summary>
    /// <param name="start">Starting tile coordinate for the flood delete</param>
    /// <returns>Array of all tiles affected by the flood delete (deleted tiles and their neighbors)</returns>
    public TileCoord[] FloodDelete(TileCoord start)
    {
        var tilesToDelete = GetFloodTiles(start);
        var affectedTiles = new HashSet<TileCoord>();

        foreach (var coord in tilesToDelete)
        {
            var tiles = DeleteAtTile(coord);
            foreach (var t in tiles)
            {
                affectedTiles.Add(t);
            }
        }

        return affectedTiles.ToArray();
    }

    /// <summary>
    /// Delete all tiles in a filled rectangle.
    /// Applies DeleteAtTile logic to each tile in the rectangle.
    /// Returns all affected tiles including neighbors for autotiling updates.
    /// </summary>
    /// <param name="start">Starting corner of the rectangle</param>
    /// <param name="end">Ending corner of the rectangle</param>
    /// <returns>Array of all tiles affected by the delete (deleted tiles and their neighbors)</returns>
    public TileCoord[] DeleteRectangle(TileCoord start, TileCoord end)
    {
        var tilesToDelete = GetRectangleTiles(start, end);
        var affectedTiles = new HashSet<TileCoord>();

        foreach (var coord in tilesToDelete)
        {
            var tiles = DeleteAtTile(coord);
            foreach (var t in tiles)
            {
                affectedTiles.Add(t);
            }
        }

        return affectedTiles.ToArray();
    }

    /// <summary>
    /// Delete all tiles in a rectangle outline.
    /// Applies DeleteAtTile logic to each tile in the outline.
    /// Returns all affected tiles including neighbors for autotiling updates.
    /// </summary>
    /// <param name="start">Starting corner of the rectangle</param>
    /// <param name="end">Ending corner of the rectangle</param>
    /// <returns>Array of all tiles affected by the delete (deleted tiles and their neighbors)</returns>
    public TileCoord[] DeleteRectangleOutline(TileCoord start, TileCoord end)
    {
        var tilesToDelete = GetRectangleOutlineTiles(start, end);
        var affectedTiles = new HashSet<TileCoord>();

        foreach (var coord in tilesToDelete)
        {
            var tiles = DeleteAtTile(coord);
            foreach (var t in tiles)
            {
                affectedTiles.Add(t);
            }
        }

        return affectedTiles.ToArray();
    }

    /// <summary>
    /// Create a pawn in the world with the specified configuration.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when config contains invalid need IDs.</exception>
    public EntityId CreatePawn(PawnConfig config)
    {
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

                if (x > 0)
                {
                    xScore = scores[x - 1, y];
                    var xTile = World.GetTile(x - 1, y);
                    if (tileHash != xTile.TileHash)
                        xScore += 1;
                    else if (xScore > 0)
                        xScore -= 1;
                }

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

        if (Time.Tick % PawnSpawnInterval == 0)
        {
            var currentPawnCount = Entities.AllPawns().Count();
            if (currentPawnCount < GetMaxPawns())
            {
                try
                {
                    CreatePawn();
                }
                catch (InvalidOperationException) { }
            }
        }
    }

    public RenderSnapshot CreateRenderSnapshot()
    {
        return RenderSnapshotBuilder.Build(this);
    }
}
