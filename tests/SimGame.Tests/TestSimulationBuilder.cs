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
        SkipDefaultBootstrap = true
    };

    private readonly List<(string Key, BuffDef Def)> _buffs = new();
    private readonly List<(string Key, NeedDef Def, string? CriticalDebuff, string? LowDebuff)> _needs = new();
    private readonly List<(string Key, ObjectDef Def, string? SatisfiesNeed, string? GrantsBuff)> _objects = new();
    private readonly List<(string ObjectKey, int X, int Y)> _objectPlacements = new();
    private readonly List<(string Name, int X, int Y, Dictionary<string, float> Needs)> _pawns = new();

    /// <summary>
    /// Set custom world bounds (default is a 5x5 world from 0,0 to 4,4).
    /// </summary>
    public TestSimulationBuilder WithWorldBounds(int minX, int maxX, int minY, int maxY)
    {
        _config.WorldBounds = (minX, maxX, minY, maxY);
        return this;
    }

    /// <summary>
    /// Define a buff that can be granted by objects or applied by needs.
    /// ID is auto-generated.
    /// </summary>
    public TestSimulationBuilder DefineBuff(string key, string name, float moodOffset, int durationTicks = 1000)
    {
        _buffs.Add((key, new BuffDef
        {
            Id = 0,  // Auto-generated
            Name = name,
            MoodOffset = moodOffset,
            DurationTicks = durationTicks
        }));
        return this;
    }

    /// <summary>
    /// Define a need type that can be used by pawns and objects.
    /// ID is auto-generated. Use buff key names for debuff references.
    /// </summary>
    public TestSimulationBuilder DefineNeed(string key, string name, 
        float decayPerTick = 0.02f,
        float criticalThreshold = 15f,
        float lowThreshold = 35f,
        string? criticalDebuff = null,
        string? lowDebuff = null)
    {
        _needs.Add((key, new NeedDef
        {
            Id = 0,  // Auto-generated
            Name = name,
            DecayPerTick = decayPerTick,
            CriticalThreshold = criticalThreshold,
            LowThreshold = lowThreshold
        }, criticalDebuff, lowDebuff));
        return this;
    }

    /// <summary>
    /// Define an object type that can be placed in the world.
    /// ID is auto-generated. Use key names for need/buff references.
    /// </summary>
    public TestSimulationBuilder DefineObject(string key, string name, 
        string? satisfiesNeed = null, 
        float satisfactionAmount = 50f, 
        int interactionDuration = 20, 
        string? grantsBuff = null,
        List<(int, int)>? useAreas = null)
    {
        _objects.Add((key, new ObjectDef
        {
            Id = 0,  // Auto-generated
            Name = name,
            NeedSatisfactionAmount = satisfactionAmount,
            InteractionDurationTicks = interactionDuration,
            UseAreas = useAreas ?? new List<(int dx, int dy)> { (0, 1) }
        }, satisfiesNeed, grantsBuff));
        return this;
    }

    /// <summary>
    /// Add an object instance to the world by its key name.
    /// </summary>
    public TestSimulationBuilder AddObject(string objectKey, int x, int y)
    {
        _objectPlacements.Add((objectKey, x, y));
        return this;
    }

    /// <summary>
    /// Add a pawn to the world with specified needs (by need key names).
    /// </summary>
    public TestSimulationBuilder AddPawn(string name, int x, int y, Dictionary<string, float> needs)
    {
        _pawns.Add((name, x, y, needs));
        return this;
    }

    /// <summary>
    /// Build the simulation with the configured content and entities.
    /// </summary>
    public Simulation Build()
    {
        // Clear any previous test content
        ContentLoader.ClearAll();

        // Register buffs first (needs reference them)
        foreach (var (key, buff) in _buffs)
        {
            ContentLoader.RegisterBuff(key, buff);
        }

        // Register needs (with debuff references resolved)
        foreach (var (key, need, criticalDebuff, lowDebuff) in _needs)
        {
            if (criticalDebuff != null)
                need.CriticalDebuffId = ContentLoader.GetBuffId(criticalDebuff);
            if (lowDebuff != null)
                need.LowDebuffId = ContentLoader.GetBuffId(lowDebuff);
            ContentLoader.RegisterNeed(key, need);
        }

        // Register objects (with need/buff references resolved)
        foreach (var (key, obj, satisfiesNeed, grantsBuff) in _objects)
        {
            if (satisfiesNeed != null)
                obj.SatisfiesNeedId = ContentLoader.GetNeedId(satisfiesNeed);
            if (grantsBuff != null)
                obj.GrantsBuffId = ContentLoader.GetBuffId(grantsBuff);
            ContentLoader.RegisterObject(key, obj);
        }

        // Convert object placements to use resolved IDs
        foreach (var (objectKey, x, y) in _objectPlacements)
        {
            var objectId = ContentLoader.GetObjectId(objectKey);
            if (objectId.HasValue)
            {
                _config.Objects.Add((objectId.Value, x, y));
            }
        }

        // Convert pawn needs to use resolved IDs
        foreach (var (name, x, y, needsByName) in _pawns)
        {
            var needsById = new Dictionary<int, float>();
            foreach (var (needKey, value) in needsByName)
            {
                var needId = ContentLoader.GetNeedId(needKey);
                if (needId.HasValue)
                {
                    needsById[needId.Value] = value;
                }
            }
            _config.Pawns.Add(new PawnConfig
            {
                Name = name,
                X = x,
                Y = y,
                Needs = needsById
            });
        }

        // Create and return the simulation
        return new Simulation(_config);
    }
}

/// <summary>
/// Extension methods for querying simulation state in tests.
/// </summary>
public static class SimulationTestExtensions
{
    /// <summary>
    /// Get the need value for a pawn by entity ID and need key name.
    /// </summary>
    public static float GetNeedValue(this Simulation sim, EntityId pawnId, string needKey)
    {
        var needId = ContentLoader.GetNeedId(needKey);
        if (!needId.HasValue) return 0f;
        
        if (sim.Entities.Needs.TryGetValue(pawnId, out var needs) &&
            needs.Needs.TryGetValue(needId.Value, out var value))
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
