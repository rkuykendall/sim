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
    /// If true, skip all default world/pawn bootstrap and only use explicit config.
    /// </summary>
    public bool SkipDefaultBootstrap { get; set; } = false;

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
    public int Age { get; set; } = 25;
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
    public Simulation(ContentRegistry content) : this(content, null)
    {
    }

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
    /// Create an object in the world at the specified position.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when objectDefId is not a valid object definition.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the tile is already occupied by another object.</exception>
    public EntityId CreateObject(int objectDefId, int x, int y, int colorIndex = 0)
    {
        if (!Content.Objects.ContainsKey(objectDefId))
            throw new ArgumentException($"Unknown object definition ID: {objectDefId}", nameof(objectDefId));

        var objDef = Content.Objects[objectDefId];
        var coord = new TileCoord(x, y);

        // Check if tile is already occupied by an object
        if (!World.GetTile(coord).Walkable)
            throw new InvalidOperationException($"Cannot place object at ({x}, {y}): tile is already occupied");

        // Clamp colorIndex to palette size
        int paletteSize = 1;
        if (Content.ColorPalettes.TryGetValue(SelectedPaletteId, out var paletteDef))
            paletteSize = paletteDef.Colors.Count;
        int safeColorIndex = GetSafeColorIndex(colorIndex, paletteSize);

        var id = Entities.CreateObject(coord, objectDefId, safeColorIndex);

        // Only block the tile if this object is not walkable (e.g., fridge blocks, bed doesn't)
        if (!objDef.Walkable)
        {
            World.GetTile(coord).Walkable = false;
        }
        return id;
    }

    /// <summary>
    /// Destroy an entity and clean up world state (e.g., restore tile walkability for objects).
    /// </summary>
    public void DestroyEntity(EntityId id)
    {
        // If this is a non-walkable object, restore tile walkability
        if (Entities.Objects.TryGetValue(id, out var objComp) &&
            Entities.Positions.TryGetValue(id, out var pos))
        {
            var objDef = Content.Objects[objComp.ObjectDefId];
            if (!objDef.Walkable)
            {
                World.GetTile(pos.Coord).Walkable = true;
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
            throw new ArgumentException($"Unknown terrain definition ID: {terrainDefId}", nameof(terrainDefId));

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

        tile.TerrainTypeId = terrainDefId;
        tile.Walkable = terrainDef.Walkable;
        tile.Buildable = terrainDef.Buildable;
        tile.Indoors = terrainDef.Indoors;
        tile.ColorIndex = safeColorIndex;
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
            if (Entities.Positions.TryGetValue(objId, out var pos) &&
                pos.Coord == coord)
            {
                DestroyEntity(objId);
                return true;
            }
        }
        return false;
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
                throw new ArgumentException($"Unknown need definition ID: {needId}", nameof(config));
        }

        var coord = new TileCoord(config.X, config.Y);
        return Entities.CreatePawn(coord, config.Name, config.Age, config.Needs);
    }

    /// <summary>
    /// Create a pawn with default values at a random walkable location.
    /// </summary>
    /// <param name="name">The pawn's name (defaults to "Pawn")</param>
    /// <param name="age">The pawn's age (defaults to 1)</param>
    /// <returns>The created entity ID</returns>
    /// <exception cref="InvalidOperationException">Thrown when no walkable tiles are available.</exception>
    public EntityId CreatePawn(string name = "Pawn", int age = 1)
    {
        var position = GetRandomWalkableTile()
            ?? throw new InvalidOperationException("No walkable tiles available to spawn pawn");

        return Entities.CreatePawn(position, name, age, GetFullNeeds());
    }

    /// <summary>
    /// Gets the maximum number of pawns allowed in the simulation.
    /// </summary>
    public int GetMaxPawns() => 5;

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
                "No color palettes loaded. Ensure content/core/palettes.lua exists and is valid.");

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
        if (paletteSize < 1) return 0;
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
