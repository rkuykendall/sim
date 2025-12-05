using System.Collections.Generic;

namespace SimGame.Core;

public readonly struct EntityId
{
    public readonly int Value;
    public EntityId(int v) => Value = v;
    public override string ToString() => Value.ToString();
}

public sealed class EntityManager
{
    private int _nextId = 1;

    public EntityId Create() => new EntityId(_nextId++);

    public readonly Dictionary<EntityId, PositionComponent> Positions = new();
    public readonly Dictionary<EntityId, PawnComponent> Pawns = new();
    public readonly Dictionary<EntityId, NeedsComponent> Needs = new();
    public readonly Dictionary<EntityId, MoodComponent> Moods = new();
    public readonly Dictionary<EntityId, BuffComponent> Buffs = new();
    public readonly Dictionary<EntityId, ActionComponent> Actions = new();
    public readonly Dictionary<EntityId, ObjectComponent> Objects = new();

    public IEnumerable<EntityId> AllPawns() => Pawns.Keys;
    public IEnumerable<EntityId> AllObjects() => Objects.Keys;
}
