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
    /// Custom world bounds. If null, uses World defaults.
    /// </summary>
    public (int MinX, int MaxX, int MinY, int MaxY)? WorldBounds { get; set; }

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
    public TimeService Time { get; } = new();

    private readonly SystemManager _systems = new();

    /// <summary>
    /// Create a simulation with default configuration.
    /// </summary>
    public Simulation() : this(null)
    {
    }

    /// <summary>
    /// Create a simulation with custom configuration.
    /// </summary>
    public Simulation(SimulationConfig? config)
    {
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
    public EntityId CreateObject(int objectDefId, int x, int y)
    {
        var id = Entities.Create();
        var coord = new TileCoord(x, y);
        Entities.Positions[id] = new PositionComponent { Coord = coord };
        Entities.Objects[id] = new ObjectComponent { ObjectDefId = objectDefId };
        World.GetTile(coord).Walkable = false;
        return id;
    }

    /// <summary>
    /// Create a pawn in the world with the specified configuration.
    /// </summary>
    public EntityId CreatePawn(PawnConfig config)
    {
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
        // Create a fridge
        var fridgeId = Entities.Create();
        Entities.Positions[fridgeId] = new PositionComponent { Coord = new TileCoord(2, 3) };
        Entities.Objects[fridgeId] = new ObjectComponent { ObjectDefId = ContentLoader.GetObjectId("Fridge") ?? 0 };
        World.GetTile(new TileCoord(2, 3)).Walkable = false;

        // Create beds (one per pawn)
        var bedPositions = new[] { (8, 2), (8, 4), (8, 6), (8, 8) };
        foreach (var (x, y) in bedPositions)
        {
            var bedId = Entities.Create();
            Entities.Positions[bedId] = new PositionComponent { Coord = new TileCoord(x, y) };
            Entities.Objects[bedId] = new ObjectComponent { ObjectDefId = ContentLoader.GetObjectId("Bed") ?? 0 };
            World.GetTile(new TileCoord(x, y)).Walkable = false;
        }

        // Create a TV for fun
        var tvId = Entities.Create();
        Entities.Positions[tvId] = new PositionComponent { Coord = new TileCoord(6, 2) };
        Entities.Objects[tvId] = new ObjectComponent { ObjectDefId = ContentLoader.GetObjectId("TV") ?? 0 };
        World.GetTile(new TileCoord(6, 2)).Walkable = false;

        // Create a shower for hygiene
        var showerId = Entities.Create();
        Entities.Positions[showerId] = new PositionComponent { Coord = new TileCoord(10, 5) };
        Entities.Objects[showerId] = new ObjectComponent { ObjectDefId = ContentLoader.GetObjectId("Shower") ?? 0 };
        World.GetTile(new TileCoord(10, 5)).Walkable = false;
    }

    private void BootstrapPawns()
    {
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
                    { ContentLoader.GetNeedId("Hunger") ?? 0, hunger },
                    { ContentLoader.GetNeedId("Energy") ?? 0, energy },
                    { ContentLoader.GetNeedId("Fun") ?? 0, fun },
                    { ContentLoader.GetNeedId("Social") ?? 0, social },
                    { ContentLoader.GetNeedId("Hygiene") ?? 0, hygiene }
                }
            };
            Entities.Buffs[id] = new BuffComponent();
            Entities.Actions[id] = new ActionComponent();
        }
    }

    public void Tick()
    {
        Time.AdvanceTick();
        var ctx = new SimContext(this);
        _systems.TickAll(ctx);
    }

    public RenderSnapshot CreateRenderSnapshot()
    {
        return RenderSnapshotBuilder.Build(this);
    }
}
