using System.Collections.Generic;
using SimGame.Core;

namespace SimGame.Tests;

/// <summary>
/// Helper class for setting up test scenarios.
/// Provides a fluent API for building simulations in a black-box testing style.
/// </summary>
public sealed class TestSimulationBuilder
{
    private readonly SimulationConfig _config = new()
    {
        SkipDefaultBootstrap = true
    };

    private readonly List<(string Key, NeedDef Def)> _needs = new();
    private readonly List<(string Key, BuffDef Def)> _buffs = new();
    private readonly List<(string Key, ObjectDef Def)> _objects = new();

    /// <summary>
    /// Set custom world bounds (default is a 5x5 world from 0,0 to 4,4).
    /// </summary>
    public TestSimulationBuilder WithWorldBounds(int minX, int maxX, int minY, int maxY)
    {
        _config.WorldBounds = (minX, maxX, minY, maxY);
        return this;
    }

    /// <summary>
    /// Define a need type that can be used by pawns and objects.
    /// </summary>
    public TestSimulationBuilder DefineNeed(string key, int id, string name, 
        float decayPerTick = 0.02f,
        float criticalThreshold = 15f,
        float lowThreshold = 35f,
        int? criticalDebuffId = null,
        int? lowDebuffId = null)
    {
        _needs.Add((key, new NeedDef
        {
            Id = id,
            Name = name,
            DecayPerTick = decayPerTick,
            CriticalThreshold = criticalThreshold,
            LowThreshold = lowThreshold,
            CriticalDebuffId = criticalDebuffId,
            LowDebuffId = lowDebuffId
        }));
        return this;
    }

    /// <summary>
    /// Define a buff that can be granted by objects.
    /// </summary>
    public TestSimulationBuilder DefineBuff(string key, int id, string name, float moodOffset, int durationTicks = 1000)
    {
        _buffs.Add((key, new BuffDef
        {
            Id = id,
            Name = name,
            MoodOffset = moodOffset,
            DurationTicks = durationTicks
        }));
        return this;
    }

    /// <summary>
    /// Define an object type that can be placed in the world.
    /// </summary>
    public TestSimulationBuilder DefineObject(string key, int id, string name, int? satisfiesNeedId = null, 
        float satisfactionAmount = 50f, int interactionDuration = 20, int? grantsBuffId = null,
        List<(int, int)>? useAreas = null)
    {
        _objects.Add((key, new ObjectDef
        {
            Id = id,
            Name = name,
            SatisfiesNeedId = satisfiesNeedId,
            NeedSatisfactionAmount = satisfactionAmount,
            InteractionDurationTicks = interactionDuration,
            GrantsBuffId = grantsBuffId,
            UseAreas = useAreas ?? new List<(int dx, int dy)> { (0, 1) }
        }));
        return this;
    }

    /// <summary>
    /// Add an object instance to the world.
    /// </summary>
    public TestSimulationBuilder AddObject(int objectDefId, int x, int y)
    {
        _config.Objects.Add((objectDefId, x, y));
        return this;
    }

    /// <summary>
    /// Add a pawn to the world with specified needs.
    /// </summary>
    public TestSimulationBuilder AddPawn(string name, int x, int y, Dictionary<int, float> needs)
    {
        _config.Pawns.Add(new PawnConfig
        {
            Name = name,
            X = x,
            Y = y,
            Needs = needs
        });
        return this;
    }

    /// <summary>
    /// Build the simulation with the configured content and entities.
    /// </summary>
    public Simulation Build()
    {
        // Clear any previous test content
        ContentLoader.ClearAll();

        // Register all content definitions
        foreach (var (key, buff) in _buffs)
        {
            ContentLoader.RegisterBuff(key, buff);
        }

        foreach (var (key, need) in _needs)
        {
            ContentLoader.RegisterNeed(key, need);
        }

        foreach (var (key, obj) in _objects)
        {
            ContentLoader.RegisterObject(key, obj);
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
    /// Get the need value for a pawn by entity ID.
    /// </summary>
    public static float GetNeedValue(this Simulation sim, EntityId pawnId, int needId)
    {
        if (sim.Entities.Needs.TryGetValue(pawnId, out var needs) &&
            needs.Needs.TryGetValue(needId, out var value))
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
