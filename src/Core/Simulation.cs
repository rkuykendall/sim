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
    /// If true, disables the theme system. Useful for tests that need deterministic behavior.
    /// </summary>
    public bool DisableThemes { get; set; } = false;

    /// <summary>
    /// Tax multiplier for idle-game economy growth. Default 1.0 (no change).
    /// Values > 1 grow the economy, values &lt; 1 shrink it.
    /// </summary>
    public float? TaxMultiplier { get; set; }

    /// <summary>
    /// Buildings to place in the world: (BuildingDefId, X, Y)
    /// </summary>
    public List<(int BuildingDefId, int X, int Y)> Buildings { get; set; } = new();

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

    /// <summary>
    /// Tax redistribution interval (every N days).
    /// </summary>
    private const int TaxInterval = TimeService.TicksPerDay; // Every day

    /// <summary>
    /// Tax rate as a percentage (0-100).
    /// </summary>
    private const float TaxRate = 15f; // 15%

    /// <summary>
    /// Default idle-game multiplier applied to collected taxes before redistribution.
    /// Values > 1 grow the economy, values &lt; 1 shrink it.
    /// </summary>
    private const float DefaultTaxMultiplier = 1.1f;

    /// <summary>
    /// Attachment decay interval in ticks.
    /// </summary>
    private const int AttachmentDecayInterval = TimeService.TicksPerDay;

    /// <summary>
    /// Only decay attachments at or below this threshold.
    /// Attachments above this are considered "regulars" and don't decay.
    /// </summary>
    private const int AttachmentDecayThreshold = 5;

    public World World { get; }
    public EntityManager Entities { get; } = new();
    public TimeService Time { get; }
    public Random Random { get; }
    public ContentRegistry Content { get; }
    public int Seed { get; }
    public int SelectedPaletteId { get; private set; }
    public ThemeSystem ThemeSystem { get; }
    public SystemManager Systems => _systems;

    /// <summary>
    /// Accumulated tax pool for redistribution. Includes collected taxes and gold from deleted buildings.
    /// </summary>
    public int TaxPool { get; set; } = 0;

    /// <summary>
    /// Total wealth in the economy (all pawn gold + all building gold + tax pool).
    /// </summary>
    public int TotalWealth
    {
        get
        {
            int total = TaxPool;
            foreach (var (_, gold) in Entities.Gold)
            {
                total += gold.Amount;
            }
            return total;
        }
    }

    /// <summary>
    /// Cycle to the next color palette in order.
    /// </summary>
    public void CyclePalette()
    {
        var paletteIds = Content.ColorPalettes.Keys.OrderBy(id => id).ToList();
        int currentIndex = paletteIds.IndexOf(SelectedPaletteId);
        int nextIndex = (currentIndex + 1) % paletteIds.Count;
        SelectedPaletteId = paletteIds[nextIndex];
    }

    /// <summary>
    /// Tax multiplier for idle-game economy. Configured at construction, defaults to 1.1.
    /// </summary>
    private readonly float _taxMultiplier;

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
        _taxMultiplier = config?.TaxMultiplier ?? DefaultTaxMultiplier;

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

        ThemeSystem = new ThemeSystem(this, config?.DisableThemes ?? false);

        _systems.Add(new NeedsSystem());
        _systems.Add(new BuffSystem());
        _systems.Add(new MoodSystem());
        if (!(config?.DisableThemes ?? false))
        {
            _systems.Add(ThemeSystem);
        }
        _systems.Add(new ActionSystem());
        _systems.Add(new AISystem());

        if (config != null)
        {
            foreach (var (buildingDefId, x, y) in config.Buildings)
            {
                CreateBuilding(buildingDefId, new TileCoord(x, y));
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
                tile.ColorIndex = 2; // Default to color #3

                if (Content.Terrains.TryGetValue(flatTerrainId, out var terrainDef))
                {
                    tile.WalkabilityCost = terrainDef.WalkabilityCost;
                    tile.BlocksLight = terrainDef.BlocksLight;
                }
            }
        }
    }

    /// <summary>
    /// Create a building in the world at the specified position.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when buildingDefId is not a valid building definition.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the tile is already occupied by another building.</exception>
    public EntityId CreateBuilding(int buildingDefId, TileCoord coord, int colorIndex = 0)
    {
        if (!Content.Buildings.ContainsKey(buildingDefId))
            throw new ArgumentException(
                $"Unknown building definition ID: {buildingDefId}",
                nameof(buildingDefId)
            );

        var buildingDef = Content.Buildings[buildingDefId];

        // Get all tiles this building will occupy
        var occupiedTiles = BuildingUtilities.GetOccupiedTiles(coord, buildingDef);

        // Validate ALL occupied tiles are in bounds
        foreach (var tile in occupiedTiles)
        {
            if (!World.IsInBounds(tile))
                throw new InvalidOperationException(
                    $"Cannot place {buildingDef.TileSize}x{buildingDef.TileSize} building at {coord}: "
                        + $"tile {tile} is out of bounds"
                );
        }

        // Validate ALL occupied tiles are walkable (not blocked)
        // Since all buildings are non-walkable and block tiles, this check prevents building overlap
        foreach (var tile in occupiedTiles)
        {
            if (!World.GetTile(tile).Walkable)
                throw new InvalidOperationException(
                    $"Cannot place {buildingDef.TileSize}x{buildingDef.TileSize} building at {coord}: "
                        + $"tile {tile} is already occupied"
                );
        }

        int paletteSize = 1;
        if (Content.ColorPalettes.TryGetValue(SelectedPaletteId, out var paletteDef))
            paletteSize = paletteDef.Colors.Count;
        int safeColorIndex = GetSafeColorIndex(colorIndex, paletteSize);

        var id = Entities.CreateBuilding(coord, buildingDefId, safeColorIndex);

        // Create resource component if this building has a resource type
        if (buildingDef.ResourceType != null)
        {
            Entities.Resources[id] = new ResourceComponent
            {
                ResourceType = buildingDef.ResourceType,
                CurrentAmount = buildingDef.MaxResourceAmount,
                MaxAmount = buildingDef.MaxResourceAmount,
                DepletionMult = buildingDef.DepletionMult,
            };
        }
        // Create attachment component for all buildings (tracks which pawns use them)
        Entities.Attachments[id] = new AttachmentComponent();

        // Mark ALL occupied tiles as blocked (all buildings are non-walkable)
        foreach (var tile in occupiedTiles)
        {
            World.GetTile(tile).BuildingBlocksMovement = true;
        }

        return id;
    }

    /// <summary>
    /// Destroy an entity and clean up world state (e.g., restore tile walkability for buildings).
    /// </summary>
    public void DestroyEntity(EntityId id)
    {
        if (
            Entities.Buildings.TryGetValue(id, out var buildingComp)
            && Entities.Positions.TryGetValue(id, out var pos)
        )
        {
            var buildingDef = Content.Buildings[buildingComp.BuildingDefId];
            // Unblock ALL occupied tiles (all buildings are non-walkable)
            var occupiedTiles = BuildingUtilities.GetOccupiedTiles(pos.Coord, buildingDef);
            foreach (var tile in occupiedTiles)
            {
                if (World.IsInBounds(tile))
                {
                    World.GetTile(tile).BuildingBlocksMovement = false;
                }
            }

            // Capture building's gold into tax pool before destruction
            if (Entities.Gold.TryGetValue(id, out var gold) && gold.Amount > 0)
            {
                TaxPool += gold.Amount;
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

        tile.WalkabilityCost = terrainDef.WalkabilityCost;
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
    /// Delete a building at the specified position (if any). Returns true if a building was deleted.
    /// For multi-tile buildings, clicking any occupied tile will delete the entire building.
    /// </summary>
    public bool TryDeleteBuilding(TileCoord coord)
    {
        foreach (var objId in Entities.AllBuildings())
        {
            if (
                Entities.Positions.TryGetValue(objId, out var pos)
                && Entities.Buildings.TryGetValue(objId, out var buildingComp)
            )
            {
                var buildingDef = Content.Buildings[buildingComp.BuildingDefId];
                var occupiedTiles = BuildingUtilities.GetOccupiedTiles(pos.Coord, buildingDef);

                // Check if clicked tile is within any occupied tile
                if (occupiedTiles.Any(t => t == coord))
                {
                    DestroyEntity(objId);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Smart delete tool that removes buildings, overlay terrain, or resets to flat terrain.
    /// Priority: 1) Delete building if present, 2) Clear overlay terrain if present, 3) Reset base to flat.
    /// Returns the deleted tile plus all its 8-neighbors for autotiling updates.
    /// </summary>
    /// <returns>Array containing the deleted tile and its neighbors (for rendering updates)</returns>
    public TileCoord[] DeleteAtTile(TileCoord coord)
    {
        if (!World.IsInBounds(coord))
            return Array.Empty<TileCoord>();

        if (TryDeleteBuilding(coord))
            return GetTilesWithNeighbors(new[] { coord });

        var tile = World.GetTile(coord);

        if (tile.OverlayTerrainTypeId.HasValue)
        {
            tile.OverlayTerrainTypeId = null;
            if (Content.Terrains.TryGetValue(tile.BaseTerrainTypeId, out var baseTerrain))
            {
                tile.WalkabilityCost = baseTerrain.WalkabilityCost;
                tile.BlocksLight = baseTerrain.BlocksLight;
            }
            return GetTilesWithNeighbors(new[] { coord });
        }

        var flatTerrainId = Content.Terrains.FirstOrDefault(kv => kv.Value.SpriteKey == "flat").Key;
        if (Content.Terrains.TryGetValue(flatTerrainId, out var flatTerrain))
        {
            tile.BaseTerrainTypeId = flatTerrainId;
            tile.WalkabilityCost = flatTerrain.WalkabilityCost;
            tile.BlocksLight = flatTerrain.BlocksLight;
            tile.ColorIndex = 2; // Default to color #3
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
    /// Gets the maximum number of pawns allowed in the simulation based on housing capacity.
    /// Capacity scales with the wealth of attached pawns (home development phase).
    /// </summary>
    public int GetMaxPawns()
    {
        var homeId = Content.GetBuildingId("Home");
        if (!homeId.HasValue)
            return 1;

        var homeDef = Content.Buildings[homeId.Value];
        int totalCapacity = 0;

        foreach (var objId in Entities.AllBuildings())
        {
            if (Entities.Buildings.TryGetValue(objId, out var buildingComp))
            {
                if (buildingComp.BuildingDefId == homeId.Value)
                {
                    // Calculate phase based on max wealth of attached pawns
                    int phase = GetBuildingPhase(objId);
                    totalCapacity += homeDef.GetCapacity(phase);
                }
            }
        }

        return Math.Max(1, totalCapacity);
    }

    /// <summary>
    /// Calculate the development phase of a building based on the wealth of attached pawns.
    /// Phase = max pawn wealth / 100 (clamped to valid range).
    /// </summary>
    private int GetBuildingPhase(EntityId buildingId)
    {
        if (!Entities.Attachments.TryGetValue(buildingId, out var attachmentComp))
            return 0;

        int maxWealth = 0;
        foreach (var pawnId in attachmentComp.UserAttachments.Keys)
        {
            if (Entities.Gold.TryGetValue(pawnId, out var goldComp))
            {
                maxWealth = Math.Max(maxWealth, goldComp.Amount);
            }
        }

        return maxWealth / 100;
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
        score += Entities.AllBuildings().Count();
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
    /// Finds a random walkable edge tile.
    /// Edge tiles are on the borders of the world (x=0, x=Width-1, y=0, or y=Height-1).
    /// </summary>
    /// <returns>A random walkable edge tile coordinate, or null if none available.</returns>
    private TileCoord? GetRandomWalkableTile()
    {
        var walkableTiles = new List<TileCoord>();

        for (int x = 0; x < World.Width; x++)
        {
            for (int y = 0; y < World.Height; y++)
            {
                bool isEdge = x == 0 || x == World.Width - 1 || y == 0 || y == World.Height - 1;
                if (!isEdge)
                    continue;

                var coord = new TileCoord(x, y);
                if (World.IsWalkable(coord))
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
    /// Example: When initializing test/demo buildings, assign color indices safely.
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

        // Progressive tax redistribution
        if (Time.Tick % TaxInterval == 0 && Time.Tick > 0)
        {
            PerformTaxRedistribution();
        }

        // Attachment decay - weak attachments fade over time
        if (Time.Tick % AttachmentDecayInterval == 0 && Time.Tick > 0)
        {
            PerformAttachmentDecay();
        }
    }

    /// <summary>
    /// Collect tax from all pawns and buildings, redistribute equally to pawns and workable buildings.
    /// This acts as an equalizing force to unstick the economy.
    /// Gold from deleted buildings is also included via TaxPool.
    /// </summary>
    private void PerformTaxRedistribution()
    {
        // Collect from all pawns
        foreach (var (pawnId, _) in Entities.Pawns)
        {
            if (Entities.Gold.TryGetValue(pawnId, out var gold) && gold.Amount > 0)
            {
                int tax = (int)(gold.Amount * TaxRate / 100f);
                gold.Amount -= tax;
                TaxPool += tax;
            }
        }

        // Collect from all buildings
        foreach (var (buildingId, _) in Entities.Buildings)
        {
            if (Entities.Gold.TryGetValue(buildingId, out var gold) && gold.Amount > 0)
            {
                int tax = (int)(gold.Amount * TaxRate / 100f);
                gold.Amount -= tax;
                TaxPool += tax;
            }
        }

        // Apply idle-game multiplier to collected taxes before redistribution
        TaxPool = (int)(TaxPool * _taxMultiplier);

        if (TaxPool == 0)
            return;

        // Count recipients: all pawns + buildings that can be worked at
        var recipients = new List<EntityId>();

        foreach (var (pawnId, _) in Entities.Pawns)
        {
            recipients.Add(pawnId);
        }

        foreach (var (buildingId, buildingComp) in Entities.Buildings)
        {
            var buildingDef = Content.Buildings[buildingComp.BuildingDefId];
            if (buildingDef.CanBeWorkedAt)
            {
                recipients.Add(buildingId);
            }
        }

        if (recipients.Count == 0)
            return;

        // Distribute equally, save remainder for next time
        int perRecipient = TaxPool / recipients.Count;
        int remainder = TaxPool % recipients.Count;

        foreach (var recipientId in recipients)
        {
            if (Entities.Gold.TryGetValue(recipientId, out var gold))
            {
                gold.Amount += perRecipient;
            }
        }

        // Save remainder for next redistribution (no gold lost)
        TaxPool = remainder;
    }

    /// <summary>
    /// Decay weak attachments over time. Only attachments at or below the threshold decay.
    /// This cleans up "noise" from one-time visitors while preserving regular customer relationships.
    /// </summary>
    private void PerformAttachmentDecay()
    {
        foreach (var (buildingId, _) in Entities.Buildings)
        {
            if (!Entities.Attachments.TryGetValue(buildingId, out var attachmentComp))
                continue;

            // Collect keys to remove (can't modify during iteration)
            var toRemove = new List<EntityId>();

            foreach (var (pawnId, strength) in attachmentComp.UserAttachments)
            {
                if (strength <= AttachmentDecayThreshold)
                {
                    int newStrength = strength - 1;
                    if (newStrength <= 0)
                    {
                        toRemove.Add(pawnId);
                    }
                    else
                    {
                        attachmentComp.UserAttachments[pawnId] = newStrength;
                    }
                }
            }

            // Remove fully decayed attachments
            foreach (var pawnId in toRemove)
            {
                attachmentComp.UserAttachments.Remove(pawnId);
            }
        }
    }

    public RenderSnapshot CreateRenderSnapshot()
    {
        return RenderSnapshotBuilder.Build(this);
    }

    /// <summary>
    /// Format an EntityId as "Pawn #X" or "BuildingType #X" for debugging and UI display.
    /// </summary>
    public string FormatEntityId(EntityId id)
    {
        if (Entities.Pawns.ContainsKey(id))
            return $"Pawn #{id.Value}";
        if (Entities.Buildings.TryGetValue(id, out var buildingComp))
        {
            var buildingDef = Content.Buildings[buildingComp.BuildingDefId];
            return $"{buildingDef.Name} #{id.Value}";
        }
        return $"Entity #{id.Value}";
    }

    /// <summary>
    /// Create a simulation from saved data.
    /// </summary>
    public static Simulation FromSaveData(SaveData data, ContentRegistry content)
    {
        // Create minimal simulation config - we'll restore state manually
        var config = new SimulationConfig
        {
            Seed = data.Seed,
            WorldBounds = (0, data.World.Width - 1, 0, data.World.Height - 1),
            DisableThemes = false,
        };

        // Create simulation with empty config to avoid bootstrap
        var sim = new Simulation(content, config);

        // Clear any auto-generated entities
        foreach (var id in sim.Entities.AllPawns().ToList())
            sim.Entities.Destroy(id);
        foreach (var id in sim.Entities.AllBuildings().ToList())
            sim.DestroyEntity(id);

        // Restore world tiles
        foreach (var tileSave in data.World.Tiles)
        {
            var tile = sim.World.GetTile(tileSave.X, tileSave.Y);
            tile.BaseTerrainTypeId = tileSave.BaseTerrainTypeId;
            tile.BaseVariantIndex = tileSave.BaseVariantIndex;
            tile.OverlayTerrainTypeId = tileSave.OverlayTerrainTypeId;
            tile.OverlayVariantIndex = tileSave.OverlayVariantIndex;
            tile.ColorIndex = tileSave.ColorIndex;
            tile.OverlayColorIndex = tileSave.OverlayColorIndex;
            tile.WalkabilityCost = tileSave.WalkabilityCost;
            tile.BlocksLight = tileSave.BlocksLight;
            tile.BuildingBlocksMovement = tileSave.BuildingBlocksMovement;
        }

        // Restore entities - buildings first (pawns may reference them)
        foreach (var entitySave in data.Entities.Where(e => e.Type == "Building"))
        {
            RestoreBuilding(sim, entitySave);
        }

        foreach (var entitySave in data.Entities.Where(e => e.Type == "Pawn"))
        {
            RestorePawn(sim, entitySave);
        }

        // Set next entity ID to avoid collisions
        sim.Entities.SetNextId(data.NextEntityId);

        // Restore time
        sim.Time.SetTick(data.CurrentTick);

        // Restore tax pool
        sim.TaxPool = data.TaxPool;

        return sim;
    }

    private static void RestoreBuilding(Simulation sim, EntitySaveData save)
    {
        var id = new EntityId(save.Id);
        var coord = new TileCoord(save.X, save.Y);

        // Prefer name-based lookup (stable), fall back to ID (legacy saves)
        int buildingDefId;
        if (!string.IsNullOrEmpty(save.BuildingDefName))
        {
            buildingDefId = sim.Content.GetBuildingId(save.BuildingDefName) ?? 0;
        }
        else
        {
            buildingDefId = save.BuildingDefId ?? 0;
        }

        sim.Entities.Positions[id] = new PositionComponent { Coord = coord };
        sim.Entities.Buildings[id] = new BuildingComponent
        {
            BuildingDefId = buildingDefId,
            ColorIndex = save.BuildingColorIndex ?? 0,
        };
        sim.Entities.Gold[id] = new GoldComponent { Amount = save.BuildingGold ?? 0 };

        if (save.Resource != null)
        {
            sim.Entities.Resources[id] = new ResourceComponent
            {
                ResourceType = save.Resource.ResourceType,
                CurrentAmount = save.Resource.CurrentAmount,
                MaxAmount = save.Resource.MaxAmount,
                DepletionMult = save.Resource.DepletionMult,
            };
        }
        else if (
            sim.Content.Buildings.TryGetValue(buildingDefId, out var buildingDef)
            && buildingDef.ResourceType != null
        )
        {
            // Initialize missing resource from definition (legacy save compatibility)
            sim.Entities.Resources[id] = new ResourceComponent
            {
                ResourceType = buildingDef.ResourceType,
                CurrentAmount = buildingDef.MaxResourceAmount,
                MaxAmount = buildingDef.MaxResourceAmount,
                DepletionMult = buildingDef.DepletionMult,
            };
        }

        if (save.Attachments != null)
        {
            sim.Entities.Attachments[id] = new AttachmentComponent
            {
                UserAttachments = save.Attachments.ToDictionary(
                    kv => new EntityId(kv.Key),
                    kv => kv.Value
                ),
            };
        }
        else
        {
            sim.Entities.Attachments[id] = new AttachmentComponent();
        }
    }

    private static void RestorePawn(Simulation sim, EntitySaveData save)
    {
        var id = new EntityId(save.Id);
        var coord = new TileCoord(save.X, save.Y);

        sim.Entities.Positions[id] = new PositionComponent { Coord = coord };
        sim.Entities.Pawns[id] = new PawnComponent { Name = save.Name ?? "Pawn" };
        sim.Entities.Needs[id] = new NeedsComponent
        {
            Needs = save.Needs != null ? new Dictionary<int, float>(save.Needs) : new(),
        };
        sim.Entities.Moods[id] = new MoodComponent { Mood = save.Mood ?? 0 };
        sim.Entities.Gold[id] = new GoldComponent { Amount = save.Gold ?? 0 };

        // Restore buffs
        var buffComponent = new BuffComponent();
        if (save.Buffs != null)
        {
            foreach (var buffSave in save.Buffs)
            {
                buffComponent.ActiveBuffs.Add(
                    new BuffInstance
                    {
                        Source = (BuffSource)buffSave.Source,
                        SourceId = buffSave.SourceId,
                        MoodOffset = buffSave.MoodOffset,
                        StartTick = buffSave.StartTick,
                        EndTick = buffSave.EndTick,
                    }
                );
            }
        }
        sim.Entities.Buffs[id] = buffComponent;

        // Clear action state - pawns will re-decide what to do
        sim.Entities.Actions[id] = new ActionComponent();

        // Restore inventory
        if (save.Inventory != null)
        {
            sim.Entities.Inventory[id] = new InventoryComponent
            {
                ResourceType = save.Inventory.ResourceType,
                Amount = save.Inventory.Amount,
                MaxAmount = save.Inventory.MaxAmount,
            };
        }
        else
        {
            sim.Entities.Inventory[id] = new InventoryComponent();
        }
    }
}
