using System;
using System.Collections.Generic;

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

    public World World { get; }
    public EntityManager Entities { get; } = new();
    public TimeService Time { get; }
    public Random Random { get; }
    public ContentRegistry Content { get; }

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

        // Initialize random number generator with optional seed
        Random = config?.Seed != null ? new Random(config.Seed.Value) : new Random();

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

        // Initialize all tiles with default terrain (Grass)
        // Terrain IDs start at 1, so we need to set a default instead of leaving them at 0
        var defaultTerrainId = Content.GetTerrainId("Grass") ?? 1;
        for (int x = 0; x < World.Width; x++)
        {
            for (int y = 0; y < World.Height; y++)
            {
                var tile = World.GetTile(x, y);
                tile.TerrainTypeId = defaultTerrainId;
            }
        }

        _systems.Add(new NeedsSystem());
        _systems.Add(new ProximitySocialSystem());
        _systems.Add(new BuffSystem());
        _systems.Add(new MoodSystem());
        _systems.Add(new ActionSystem());
        _systems.Add(new AISystem());

        if (config == null || !config.SkipDefaultBootstrap)
        {
            BootstrapWorld();
            BootstrapPawns();
        }

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

        var id = Entities.Create();
        Entities.Positions[id] = new PositionComponent { Coord = coord };
        Entities.Objects[id] = new ObjectComponent
        {
            ObjectDefId = objectDefId,
            ColorIndex = colorIndex
        };

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

        tile.TerrainTypeId = terrainDefId;
        tile.Walkable = terrainDef.Walkable;
        tile.Buildable = terrainDef.Buildable;
        tile.Indoors = terrainDef.Indoors;
        tile.ColorIndex = colorIndex;
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

        var id = Entities.Create();
        var coord = new TileCoord(config.X, config.Y);

        Entities.Pawns[id] = new PawnComponent { Name = config.Name, Age = config.Age };
        Entities.Positions[id] = new PositionComponent { Coord = coord };
        Entities.Moods[id] = new MoodComponent { Mood = 0 };
        Entities.Needs[id] = new NeedsComponent { Needs = new Dictionary<int, float>(config.Needs) };
        Entities.Buffs[id] = new BuffComponent();
        Entities.Actions[id] = new ActionComponent();

        return id;
    }

    private void BootstrapWorld()
    {
        // Create a fridge (blue)
        CreateObject(Content.GetObjectId("Fridge")
            ?? throw new InvalidOperationException("Required object 'Fridge' not found in content"), 2, 3, colorIndex: 5);

        // Create beds with different colors
        var bedObjectId = Content.GetObjectId("Bed")
            ?? throw new InvalidOperationException("Required object 'Bed' not found in content");
        var bedPositions = new[] {
            (8, 2, 1),  // Brown bed
            (8, 4, 6),  // Red bed
            (8, 6, 8),  // Purple bed
            (8, 8, 9)   // Orange bed
        };
        foreach (var (x, y, color) in bedPositions)
        {
            CreateObject(bedObjectId, x, y, colorIndex: color);
        }

        // Create a TV (dark gray)
        CreateObject(Content.GetObjectId("TV")
            ?? throw new InvalidOperationException("Required object 'TV' not found in content"), 6, 2, colorIndex: 4);

        // Create a shower (white)
        CreateObject(Content.GetObjectId("Shower")
            ?? throw new InvalidOperationException("Required object 'Shower' not found in content"), 10, 5, colorIndex: 11);
    }

    private void BootstrapPawns()
    {
        // Cache need IDs (fail fast if any are missing)
        var hungerNeedId = Content.GetNeedId("Hunger") 
            ?? throw new InvalidOperationException("Required need 'Hunger' not found in content");
        var energyNeedId = Content.GetNeedId("Energy") 
            ?? throw new InvalidOperationException("Required need 'Energy' not found in content");
        var funNeedId = Content.GetNeedId("Fun") 
            ?? throw new InvalidOperationException("Required need 'Fun' not found in content");
        var socialNeedId = Content.GetNeedId("Social") 
            ?? throw new InvalidOperationException("Required need 'Social' not found in content");
        var hygieneNeedId = Content.GetNeedId("Hygiene") 
            ?? throw new InvalidOperationException("Required need 'Hygiene' not found in content");

        // Pawn data: (name, age, x, y, hunger, energy, fun, social, hygiene)
        var pawnData = new[]
        {
            ("Alex", 25, 5, 5, 70f, 60f, 50f, 80f, 65f),
            ("Jordan", 32, 3, 7, 55f, 75f, 40f, 60f, 70f),
            ("Sam", 28, 9, 3, 80f, 45f, 65f, 50f, 85f),
            ("Riley", 22, 7, 9, 60f, 55f, 75f, 70f, 50f),
        };

        foreach (var (name, age, x, y, hunger, energy, fun, social, hygiene) in pawnData)
        {
            var id = Entities.Create();

            Entities.Pawns[id] = new PawnComponent { Name = name, Age = age };
            Entities.Positions[id] = new PositionComponent { Coord = new TileCoord(x, y) };
            Entities.Moods[id] = new MoodComponent { Mood = 0 };
            Entities.Needs[id] = new NeedsComponent
            {
                Needs = new Dictionary<int, float>
                {
                    { hungerNeedId, hunger },
                    { energyNeedId, energy },
                    { funNeedId, fun },
                    { socialNeedId, social },
                    { hygieneNeedId, hygiene }
                }
            };
            Entities.Buffs[id] = new BuffComponent();
            Entities.Actions[id] = new ActionComponent();
        }
    }

    public void Tick()
    {
        var ctx = new SimContext(this);
        _systems.TickAll(ctx);
        Time.AdvanceTick();
    }

    public RenderSnapshot CreateRenderSnapshot()
    {
        return RenderSnapshotBuilder.Build(this);
    }
}
