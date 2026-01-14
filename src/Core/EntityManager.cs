using System;
using System.Collections.Generic;

namespace SimGame.Core;

public readonly struct EntityId : IEquatable<EntityId>
{
    public readonly int Value;

    public EntityId(int v) => Value = v;

    public bool Equals(EntityId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Value;

    public override string ToString() => Value.ToString();

    public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

    public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
}

public sealed class EntityManager
{
    private int _nextId = 1;

    public EntityId Create() => new EntityId(_nextId++);

    /// <summary>
    /// Factory method to create a pawn with all required components.
    /// </summary>
    /// <param name="position">The position to place the pawn</param>
    /// <param name="name">The pawn's name (defaults to "Pawn")</param>
    /// <param name="needs">The pawn's needs (defaults to empty dictionary)</param>
    /// <param name="startingGold">The pawn's starting gold (defaults to 100)</param>
    public EntityId CreatePawn(
        TileCoord position,
        string name = "Pawn",
        Dictionary<int, float>? needs = null,
        int startingGold = 100
    )
    {
        var id = Create();
        Pawns[id] = new PawnComponent { Name = name };
        Positions[id] = new PositionComponent { Coord = position };
        Moods[id] = new MoodComponent { Mood = 0 };
        Needs[id] = new NeedsComponent
        {
            Needs =
                needs != null ? new Dictionary<int, float>(needs) : new Dictionary<int, float>(),
        };
        Buffs[id] = new BuffComponent();
        Actions[id] = new ActionComponent();
        Gold[id] = new GoldComponent { Amount = startingGold };
        Inventory[id] = new InventoryComponent();
        return id;
    }

    /// <summary>
    /// Factory method to create a building with all required components.
    /// </summary>
    public EntityId CreateBuilding(TileCoord position, int buildingDefId, int colorIndex)
    {
        var id = Create();
        Positions[id] = new PositionComponent { Coord = position };
        Buildings[id] = new BuildingComponent
        {
            BuildingDefId = buildingDefId,
            ColorIndex = colorIndex,
        };
        Gold[id] = new GoldComponent { Amount = 0 };
        return id;
    }

    public readonly Dictionary<EntityId, PositionComponent> Positions = new();
    public readonly Dictionary<EntityId, PawnComponent> Pawns = new();
    public readonly Dictionary<EntityId, NeedsComponent> Needs = new();
    public readonly Dictionary<EntityId, MoodComponent> Moods = new();
    public readonly Dictionary<EntityId, BuffComponent> Buffs = new();
    public readonly Dictionary<EntityId, ActionComponent> Actions = new();
    public readonly Dictionary<EntityId, BuildingComponent> Buildings = new();
    public readonly Dictionary<EntityId, ResourceComponent> Resources = new();
    public readonly Dictionary<EntityId, AttachmentComponent> Attachments = new();
    public readonly Dictionary<EntityId, GoldComponent> Gold = new();
    public readonly Dictionary<EntityId, InventoryComponent> Inventory = new();

    public IEnumerable<EntityId> AllPawns() => Pawns.Keys;

    public IEnumerable<EntityId> AllBuildings() => Buildings.Keys;

    /// <summary>
    /// Remove all components for an entity. Internal use only.
    /// Use Simulation.DestroyEntity() for proper cleanup including world state.
    /// </summary>
    internal void Destroy(EntityId id)
    {
        Positions.Remove(id);
        Pawns.Remove(id);
        Needs.Remove(id);
        Moods.Remove(id);
        Buffs.Remove(id);
        Actions.Remove(id);
        Buildings.Remove(id);
        Resources.Remove(id);
        Attachments.Remove(id);
        Gold.Remove(id);
        Inventory.Remove(id);
    }
}
