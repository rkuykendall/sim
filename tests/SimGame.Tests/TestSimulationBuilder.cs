using System.Collections.Generic;
using SimGame.Core;

namespace SimGame.Tests;

/// <summary>
/// Helper class for setting up test scenarios.
/// Provides a fluent API for building simulations in a black-box testing style.
/// IDs are auto-generated - use string keys to reference content.
/// </summary>
public sealed class TestSimulationBuilder
{
    private readonly SimulationConfig _config = new()
    {
        Seed = 12345, // Fixed seed to ensure deterministic palette selection
        WorldBounds = (0, 4, 0, 4), // Default 5x5 world
    };

    private readonly ContentRegistry _content = new();

    public TestSimulationBuilder()
    {
        // Register only the test color palette to ensure it is always selected
        var testPalette = new ColorPaletteDef
        {
            Name = "test",
            Colors = new List<ColorDef>
            {
                new ColorDef
                {
                    Name = "Green",
                    R = 0.2f,
                    G = 0.6f,
                    B = 0.2f,
                },
                new ColorDef
                {
                    Name = "Brown",
                    R = 0.5f,
                    G = 0.3f,
                    B = 0.1f,
                },
                new ColorDef
                {
                    Name = "Light Gray",
                    R = 0.7f,
                    G = 0.7f,
                    B = 0.7f,
                },
                new ColorDef
                {
                    Name = "Tan",
                    R = 0.8f,
                    G = 0.6f,
                    B = 0.3f,
                },
            },
        };
        _content.RegisterColorPalette("test", testPalette);
    }

    /// <summary>
    /// Set custom world bounds (default is a 5x5 world from 0,0 to 4,4).
    /// </summary>
    public void WithWorldBounds(int maxX, int maxY)
    {
        _config.WorldBounds = (0, maxX, 0, maxY);
    }

    /// <summary>
    /// Define a buff that can be granted by objects or applied by needs.
    /// ID is auto-generated.
    /// </summary>
    public int DefineBuff(
        string key = "",
        string name = "",
        float moodOffset = 0,
        int durationTicks = 1000
    )
    {
        var buff = new BuffDef
        {
            Id = 0, // Auto-generated
            Name = name,
            MoodOffset = moodOffset,
            DurationTicks = durationTicks,
        };
        return _content.RegisterBuff(key, buff);
    }

    /// <summary>
    /// Define a need type that can be used by pawns and objects.
    /// ID is auto-generated. Accepts debuff IDs directly.
    /// </summary>
    public int DefineNeed(
        string key = "",
        float decayPerTick = 0.02f,
        float criticalThreshold = 15f,
        float lowThreshold = 35f,
        int? criticalDebuffId = null,
        int? lowDebuffId = null
    )
    {
        var need = new NeedDef
        {
            Id = 0, // Auto-generated
            Name = key,
            DecayPerTick = decayPerTick,
            CriticalThreshold = criticalThreshold,
            LowThreshold = lowThreshold,
            CriticalDebuffId = criticalDebuffId,
            LowDebuffId = lowDebuffId,
        };
        return _content.RegisterNeed(key, need);
    }

    /// <summary>
    /// Define an object type that can be placed in the world.
    /// ID is auto-generated. Accepts need and buff IDs directly.
    /// </summary>
    public int DefineObject(
        string key = "",
        int? satisfiesNeedId = null,
        float satisfactionAmount = 50f,
        int interactionDuration = 20,
        int? grantsBuffId = null,
        List<(int, int)>? useAreas = null,
        bool walkable = false
    )
    {
        var obj = new ObjectDef
        {
            Id = 0, // Auto-generated
            Name = key,
            Walkable = walkable,
            NeedSatisfactionAmount = satisfactionAmount,
            InteractionDurationTicks = interactionDuration,
            UseAreas = useAreas ?? new List<(int dx, int dy)> { (0, 1) },
            SatisfiesNeedId = satisfiesNeedId,
            GrantsBuffId = grantsBuffId,
        };
        return _content.RegisterObject(key, obj);
    }

    /// <summary>
    /// Define a terrain type that can be painted on tiles.
    /// ID is auto-generated.
    /// </summary>
    public int DefineTerrain(
        string key = "",
        bool walkable = true,
        string spriteKey = "",
        bool isAutotiling = false,
        bool? paintsToBase = null // If null, defaults based on isAutotiling
    )
    {
        // Smart default: autotiling terrains paint to overlay, others to base
        bool effectivePaintsToBase = paintsToBase ?? !isAutotiling;

        var terrain = new TerrainDef
        {
            Id = 0, // Auto-generated
            Passability = walkable ? TerrainPassability.Ground : TerrainPassability.High,
            BlocksLight = !walkable,
            SpriteKey = spriteKey,
            IsAutotiling = isAutotiling,
            PaintsToBase = effectivePaintsToBase,
        };
        return _content.RegisterTerrain(key, terrain);
    }

    /// <summary>
    /// Add an object instance to the world by its object ID.
    /// </summary>
    public void AddObject(int objectId, int x = 0, int y = 0)
    {
        _config.Objects.Add((objectId, x, y));
    }

    /// <summary>
    /// Add a pawn to the world with specified needs (by need IDs).
    /// </summary>
    public void AddPawn(
        string name = "",
        int x = 0,
        int y = 0,
        Dictionary<int, float>? needs = null
    )
    {
        _config.Pawns.Add(
            new PawnConfig
            {
                Name = name,
                X = x,
                Y = y,
                Needs = needs ?? new Dictionary<int, float>(),
            }
        );
    }

    /// <summary>
    /// Build the simulation with the configured content and entities.
    /// </summary>
    public Simulation Build()
    {
        return new Simulation(_content, _config);
    }
}

/// <summary>
/// Extension methods for querying simulation state in tests.
/// </summary>
public static class SimulationTestExtensions
{
    /// <summary>
    /// Render the world state as ASCII for debugging.
    /// </summary>
    public static void RenderWorld(this Simulation sim)
    {
        var pawnPositions = sim.GetAllPawnPositions() ?? new HashSet<TileCoord>();
        var objectPositions = sim.GetAllObjectPositions() ?? new HashSet<TileCoord>();
        int width = sim.World.Width;
        int height = sim.World.Height;

        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                var coord = new TileCoord(x, y);
                if (pawnPositions.Contains(coord))
                    Console.Write("P"); // Pawn
                else if (objectPositions.Contains(coord))
                    Console.Write("O"); // Object
                else if (!sim.World.IsWalkable(coord))
                    Console.Write("#"); // Wall/non-walkable
                else
                    Console.Write("."); // Walkable floor
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Get all pawn positions in the simulation.
    /// </summary>
    public static HashSet<TileCoord>? GetAllPawnPositions(this Simulation sim)
    {
        var result = new HashSet<TileCoord>();
        foreach (var id in sim.Entities.AllPawns())
        {
            if (sim.Entities.Positions.TryGetValue(id, out var pos))
                result.Add(pos.Coord);
        }
        return result;
    }

    /// <summary>
    /// Get all object positions in the simulation.
    /// </summary>
    public static HashSet<TileCoord>? GetAllObjectPositions(this Simulation sim)
    {
        var result = new HashSet<TileCoord>();
        foreach (var id in sim.Entities.AllObjects())
        {
            if (sim.Entities.Positions.TryGetValue(id, out var pos))
                result.Add(pos.Coord);
        }
        return result;
    }

    /// <summary>
    /// Get the need value for a pawn by entity ID and need key name.
    /// </summary>
    public static float GetNeedValue(this Simulation sim, EntityId pawnId, string needKey)
    {
        var needId = sim.Content.GetNeedId(needKey);
        if (!needId.HasValue)
            return 0f;

        if (
            sim.Entities.Needs.TryGetValue(pawnId, out var needs)
            && needs.Needs.TryGetValue(needId.Value, out var value)
        )
        {
            return value;
        }
        return 0f;
    }

    /// <summary>
    /// Get the position of an entity.
    /// </summary>
    public static TileCoord? GetPosition(this Simulation sim, EntityId entityId)
    {
        if (sim.Entities.Positions.TryGetValue(entityId, out var pos))
        {
            return pos.Coord;
        }
        return null;
    }

    /// <summary>
    /// Get the first pawn in the simulation.
    /// </summary>
    public static EntityId? GetFirstPawn(this Simulation sim)
    {
        foreach (var id in sim.Entities.AllPawns())
        {
            return id;
        }
        return null;
    }

    /// <summary>
    /// Get a pawn by name.
    /// </summary>
    public static EntityId? GetPawnByName(this Simulation sim, string name)
    {
        foreach (var id in sim.Entities.AllPawns())
        {
            if (sim.Entities.Pawns.TryGetValue(id, out var pawn) && pawn.Name == name)
            {
                return id;
            }
        }
        return null;
    }

    /// <summary>
    /// Run the simulation for a specified number of ticks.
    /// </summary>
    public static void RunTicks(this Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }
}
