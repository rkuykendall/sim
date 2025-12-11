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

    /// <summary>
    /// Check if a tile is occupied by any pawn other than the excluded one.
    /// </summary>
    public bool IsTileOccupiedByPawn(TileCoord coord, EntityId? excludePawn = null)
    {
        foreach (var pawnId in Pawns.Keys)
        {
            if (excludePawn.HasValue && pawnId.Value == excludePawn.Value.Value)
                continue;

            if (Positions.TryGetValue(pawnId, out var pos) && 
                pos.Coord.X == coord.X && pos.Coord.Y == coord.Y)
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
            if (excludePawn.HasValue && pawnId.Value == excludePawn.Value.Value)
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
            if (excludePawn.HasValue && pawnId.Value == excludePawn.Value.Value)
                continue;

            if (Positions.TryGetValue(pawnId, out var pos) && 
                pos.Coord.X == coord.X && pos.Coord.Y == coord.Y)
            {
                return pawnId;
            }
        }
        return null;
    }
}
