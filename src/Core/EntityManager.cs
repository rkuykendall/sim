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

    public readonly Dictionary<EntityId, PositionComponent> Positions = new();
    public readonly Dictionary<EntityId, PawnComponent> Pawns = new();
    public readonly Dictionary<EntityId, NeedsComponent> Needs = new();
    public readonly Dictionary<EntityId, MoodComponent> Moods = new();
    public readonly Dictionary<EntityId, BuffComponent> Buffs = new();
    public readonly Dictionary<EntityId, ActionComponent> Actions = new();
    public readonly Dictionary<EntityId, ObjectComponent> Objects = new();

    public IEnumerable<EntityId> AllPawns() => Pawns.Keys;
    public IEnumerable<EntityId> AllObjects() => Objects.Keys;

    /// <summary>
    /// Check if a tile is occupied by any pawn other than the excluded one.
    /// </summary>
    public bool IsTileOccupiedByPawn(TileCoord coord, EntityId? excludePawn = null)
    {
        foreach (var pawnId in Pawns.Keys)
        {
            if (excludePawn.HasValue && pawnId == excludePawn.Value)
                continue;

            if (Positions.TryGetValue(pawnId, out var pos) && pos.Coord == coord)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get all tiles occupied by pawns, optionally excluding one pawn.
    /// </summary>
    public HashSet<TileCoord> GetOccupiedTiles(EntityId? excludePawn = null)
    {
        var occupied = new HashSet<TileCoord>();
        foreach (var pawnId in Pawns.Keys)
        {
            if (excludePawn.HasValue && pawnId == excludePawn.Value)
                continue;

            if (Positions.TryGetValue(pawnId, out var pos))
            {
                occupied.Add(pos.Coord);
            }
        }
        return occupied;
    }

    /// <summary>
    /// Get the pawn at a specific tile, or null if none (optionally excluding one pawn).
    /// </summary>
    public EntityId? GetPawnAtTile(TileCoord coord, EntityId? excludePawn = null)
    {
        foreach (var pawnId in Pawns.Keys)
        {
            if (excludePawn.HasValue && pawnId == excludePawn.Value)
                continue;

            if (Positions.TryGetValue(pawnId, out var pos) && pos.Coord == coord)
            {
                return pawnId;
            }
        }
        return null;
    }

    /// <summary>
    /// Destroy an entity and remove all its components.
    /// </summary>
    public void Destroy(EntityId id)
    {
        Positions.Remove(id);
        Pawns.Remove(id);
        Needs.Remove(id);
        Moods.Remove(id);
        Buffs.Remove(id);
        Actions.Remove(id);
        Objects.Remove(id);
    }
}
