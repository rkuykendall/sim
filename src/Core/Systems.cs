using System;
using System.Collections.Generic;
using System.Linq;

namespace SimGame.Core;

public interface ISystem
{
    void Tick(SimContext ctx);
}

public sealed class SystemManager
{
    private readonly List<ISystem> _systems = new();

    public void Add(ISystem s) => _systems.Add(s);

    public void TickAll(SimContext ctx)
    {
        foreach (var s in _systems)
            s.Tick(ctx);
    }
}

public sealed class TimeService
{
    public int Tick { get; private set; } = 0;
    public void AdvanceTick() => Tick++;
}

public readonly struct SimContext
{
    public Simulation Sim { get; }
    public World World => Sim.World;
    public EntityManager Entities => Sim.Entities;
    public TimeService Time => Sim.Time;

    public SimContext(Simulation sim) => Sim = sim;
}

// Needs decay over time
public sealed class NeedsSystem : ISystem
{
    public void Tick(SimContext ctx)
    {
        foreach (var pawnId in ctx.Entities.AllPawns())
        {
            if (!ctx.Entities.Needs.TryGetValue(pawnId, out var needs))
                continue;

            foreach (var key in needs.Needs.Keys)
            {
                float decay = ContentDatabase.Needs.TryGetValue(key, out var needDef)
                    ? needDef.DecayPerTick
                    : 0.005f;
                needs.Needs[key] = Math.Clamp(needs.Needs[key] - decay, 0f, 100f);
            }
        }
    }
}

// Buffs expire over time
public sealed class BuffSystem : ISystem
{
    public void Tick(SimContext ctx)
    {
        int now = ctx.Time.Tick;

        foreach (var buffComp in ctx.Entities.Buffs.Values)
        {
            buffComp.ActiveBuffs.RemoveAll(b => b.EndTick <= now);
        }
    }
}

// Mood is calculated from needs and buffs
public sealed class MoodSystem : ISystem
{
    public void Tick(SimContext ctx)
    {
        foreach (var pawnId in ctx.Entities.AllPawns())
        {
            ctx.Entities.Moods.TryGetValue(pawnId, out var moodComp);
            ctx.Entities.Needs.TryGetValue(pawnId, out var needs);
            ctx.Entities.Buffs.TryGetValue(pawnId, out var buffComp);

            if (moodComp == null) continue;

            float mood = 0f;

            if (needs != null)
            {
                foreach (var v in needs.Needs.Values)
                    mood += (v - 50f) / 50f * 10f;
            }

            if (buffComp != null)
            {
                foreach (var inst in buffComp.ActiveBuffs)
                    mood += ContentDatabase.Buffs[inst.BuffDefId].MoodOffset;
            }

            moodComp.Mood = Math.Clamp(mood, -100f, 100f);
        }
    }
}

// Action execution (movement, using objects)
public sealed class ActionSystem : ISystem
{
    private const int MoveTicksPerTile = 10;

    public void Tick(SimContext ctx)
    {
        foreach (var pawnId in ctx.Entities.AllPawns())
        {
            if (!ctx.Entities.Actions.TryGetValue(pawnId, out var actionComp))
                continue;

            if (actionComp.CurrentAction == null)
            {
                if (actionComp.ActionQueue.Count > 0)
                {
                    actionComp.CurrentAction = actionComp.ActionQueue.Dequeue();
                    actionComp.ActionStartTick = ctx.Time.Tick;
                    actionComp.CurrentPath = null;
                    actionComp.PathIndex = 0;
                }
                else continue;
            }

            var action = actionComp.CurrentAction;

            switch (action.Type)
            {
                case ActionType.MoveTo:
                    ExecuteMoveTo(ctx, pawnId, actionComp);
                    break;
                case ActionType.UseObject:
                    ExecuteUseObject(ctx, pawnId, actionComp);
                    break;
                case ActionType.Idle:
                    if (ctx.Time.Tick - actionComp.ActionStartTick >= action.DurationTicks)
                        actionComp.CurrentAction = null;
                    break;
            }
        }
    }

    private void ExecuteMoveTo(SimContext ctx, EntityId pawnId, ActionComponent actionComp)
    {
        var action = actionComp.CurrentAction!;
        if (action.TargetCoord == null) { actionComp.CurrentAction = null; return; }

        var pos = ctx.Entities.Positions[pawnId];
        var target = action.TargetCoord.Value;

        if (actionComp.CurrentPath == null)
        {
            actionComp.CurrentPath = Pathfinder.FindPath(ctx.World, pos.Coord, target);
            actionComp.PathIndex = 0;

            if (actionComp.CurrentPath == null || actionComp.CurrentPath.Count == 0)
            {
                actionComp.CurrentAction = null;
                return;
            }
        }

        int ticksInAction = ctx.Time.Tick - actionComp.ActionStartTick;
        int expectedPathIndex = Math.Min(ticksInAction / MoveTicksPerTile, actionComp.CurrentPath.Count - 1);

        if (expectedPathIndex > actionComp.PathIndex)
        {
            actionComp.PathIndex = expectedPathIndex;
            pos.Coord = actionComp.CurrentPath[actionComp.PathIndex];
        }

        if (pos.Coord.X == target.X && pos.Coord.Y == target.Y)
        {
            actionComp.CurrentAction = null;
            actionComp.CurrentPath = null;
        }
    }

    private void ExecuteUseObject(SimContext ctx, EntityId pawnId, ActionComponent actionComp)
    {
        var action = actionComp.CurrentAction!;
        if (action.TargetEntity == null) { actionComp.CurrentAction = null; return; }

        var targetId = action.TargetEntity.Value;

        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos)) return;
        if (!ctx.Entities.Positions.TryGetValue(targetId, out var objPos)) return;

        int dist = Math.Abs(pawnPos.Coord.X - objPos.Coord.X) + Math.Abs(pawnPos.Coord.Y - objPos.Coord.Y);
        if (dist > 1)
        {
            actionComp.ActionQueue = new Queue<ActionDef>(new[] { action }.Concat(actionComp.ActionQueue));
            actionComp.CurrentAction = new ActionDef
            {
                Type = ActionType.MoveTo,
                TargetCoord = FindAdjacentWalkable(ctx.World, objPos.Coord, pawnPos.Coord),
                DurationTicks = 0
            };
            actionComp.ActionStartTick = ctx.Time.Tick;
            return;
        }

        if (ctx.Entities.Objects.TryGetValue(targetId, out var objComp))
        {
            objComp.InUse = true;
            objComp.UsedBy = pawnId;
        }

        int elapsed = ctx.Time.Tick - actionComp.ActionStartTick;
        if (elapsed >= action.DurationTicks)
        {
            if (action.SatisfiesNeedId.HasValue && ctx.Entities.Needs.TryGetValue(pawnId, out var needs))
            {
                if (needs.Needs.ContainsKey(action.SatisfiesNeedId.Value))
                {
                    needs.Needs[action.SatisfiesNeedId.Value] = Math.Clamp(
                        needs.Needs[action.SatisfiesNeedId.Value] + action.NeedSatisfactionAmount,
                        0f, 100f);
                }
            }

            if (objComp != null)
            {
                objComp.InUse = false;
                objComp.UsedBy = null;
            }

            actionComp.CurrentAction = null;
        }
    }

    private TileCoord FindAdjacentWalkable(World world, TileCoord target, TileCoord from)
    {
        var candidates = new List<(TileCoord coord, int dist)>();
        foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            var adj = new TileCoord(target.X + dx, target.Y + dy);
            if (world.GetTile(adj).Walkable)
            {
                int dist = Math.Abs(adj.X - from.X) + Math.Abs(adj.Y - from.Y);
                candidates.Add((adj, dist));
            }
        }
        return candidates.Count > 0
            ? candidates.OrderBy(c => c.dist).First().coord
            : target;
    }
}

// Utility AI - decides what action to take
public sealed class AISystem : ISystem
{
    public void Tick(SimContext ctx)
    {
        foreach (var pawnId in ctx.Entities.AllPawns())
        {
            if (!ctx.Entities.Actions.TryGetValue(pawnId, out var actionComp)) continue;
            if (!ctx.Entities.Needs.TryGetValue(pawnId, out var needs)) continue;

            if (actionComp.CurrentAction != null || actionComp.ActionQueue.Count > 0)
                continue;

            int? urgentNeedId = null;
            float lowestNeed = float.MaxValue;

            foreach (var (needId, value) in needs.Needs)
            {
                if (!ContentDatabase.Needs.TryGetValue(needId, out var needDef)) continue;

                float urgency = value;
                if (value < needDef.CriticalThreshold) urgency -= 50;
                else if (value < needDef.LowThreshold) urgency -= 20;

                if (urgency < lowestNeed)
                {
                    lowestNeed = urgency;
                    urgentNeedId = needId;
                }
            }

            if (urgentNeedId == null) continue;

            EntityId? targetObject = FindObjectForNeed(ctx, pawnId, urgentNeedId.Value);
            if (targetObject == null) continue;

            var objComp = ctx.Entities.Objects[targetObject.Value];
            var objDef = ContentDatabase.Objects[objComp.ObjectDefId];

            actionComp.ActionQueue.Enqueue(new ActionDef
            {
                Type = ActionType.UseObject,
                TargetEntity = targetObject,
                DurationTicks = objDef.InteractionDurationTicks,
                SatisfiesNeedId = objDef.SatisfiesNeedId,
                NeedSatisfactionAmount = objDef.NeedSatisfactionAmount
            });
        }
    }

    private EntityId? FindObjectForNeed(SimContext ctx, EntityId pawnId, int needId)
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return null;

        EntityId? best = null;
        int bestDist = int.MaxValue;

        foreach (var objId in ctx.Entities.AllObjects())
        {
            var objComp = ctx.Entities.Objects[objId];
            var objDef = ContentDatabase.Objects[objComp.ObjectDefId];

            if (objDef.SatisfiesNeedId != needId) continue;
            if (objComp.InUse) continue;

            if (!ctx.Entities.Positions.TryGetValue(objId, out var objPos)) continue;

            int dist = Math.Abs(pawnPos.Coord.X - objPos.Coord.X) +
                       Math.Abs(pawnPos.Coord.Y - objPos.Coord.Y);

            if (dist < bestDist)
            {
                bestDist = dist;
                best = objId;
            }
        }

        return best;
    }
}
