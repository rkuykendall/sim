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
    // Time configuration
    public const int TicksPerMinute = 10;      // 0.5 real seconds = 1 game minute (2x speed)
    public const int MinutesPerHour = 60;
    public const int HoursPerDay = 24;
    public const int TicksPerHour = TicksPerMinute * MinutesPerHour;
    public const int TicksPerDay = TicksPerHour * HoursPerDay;

    // Starting time: 8:00 AM on day 1
    public int Tick { get; private set; } = 8 * TicksPerHour;

    public int TotalMinutes => Tick / TicksPerMinute;
    public int Minute => TotalMinutes % MinutesPerHour;
    public int Hour => (Tick / TicksPerHour) % HoursPerDay;
    public int Day => (Tick / TicksPerDay) + 1;

    public bool IsNight => Hour < 6 || Hour >= 22;  // 10 PM - 6 AM
    public bool IsSleepTime => Hour < 6 || Hour >= 23;  // 11 PM - 6 AM

    public string TimeString => $"Day {Day}, {Hour:D2}:{Minute:D2}";

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
        bool isNight = ctx.Time.IsNight;
        bool isSleepTime = ctx.Time.IsSleepTime;

        foreach (var pawnId in ctx.Entities.AllPawns())
        {
            if (!ctx.Entities.Needs.TryGetValue(pawnId, out var needs))
                continue;
            if (!ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs))
                continue;

            foreach (var needId in needs.Needs.Keys.ToList())
            {
                if (!ContentDatabase.Needs.TryGetValue(needId, out var needDef))
                    continue;

                // Calculate decay rate with time-of-day modifiers
                float decay = needDef.DecayPerTick;

                // Energy decays faster at night (pawns get sleepy)
                if (needId == ContentDatabase.NeedEnergy && isNight)
                {
                    decay *= isSleepTime ? 2.5f : 1.5f;
                }

                // Decay the need
                float oldValue = needs.Needs[needId];
                float newValue = Math.Clamp(oldValue - decay, 0f, 100f);
                needs.Needs[needId] = newValue;

                // Apply/remove need-based debuffs
                UpdateNeedDebuffs(buffs, needDef, newValue);
            }
        }
    }

    private void UpdateNeedDebuffs(BuffComponent buffs, NeedDef needDef, float value)
    {
        // Remove existing debuffs for this need first
        if (needDef.CriticalDebuffId.HasValue)
            buffs.ActiveBuffs.RemoveAll(b => b.BuffDefId == needDef.CriticalDebuffId.Value);
        if (needDef.LowDebuffId.HasValue)
            buffs.ActiveBuffs.RemoveAll(b => b.BuffDefId == needDef.LowDebuffId.Value);

        // Apply appropriate debuff based on current value
        if (value < needDef.CriticalThreshold && needDef.CriticalDebuffId.HasValue)
        {
            buffs.ActiveBuffs.Add(new BuffInstance
            {
                BuffDefId = needDef.CriticalDebuffId.Value,
                StartTick = 0,
                EndTick = -1 // Permanent until need recovers
            });
        }
        else if (value < needDef.LowThreshold && needDef.LowDebuffId.HasValue)
        {
            buffs.ActiveBuffs.Add(new BuffInstance
            {
                BuffDefId = needDef.LowDebuffId.Value,
                StartTick = 0,
                EndTick = -1 // Permanent until need recovers
            });
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
            // Only remove buffs with a positive end tick (not permanent ones)
            buffComp.ActiveBuffs.RemoveAll(b => b.EndTick > 0 && b.EndTick <= now);
        }
    }
}

// Mood is calculated purely from active buffs
public sealed class MoodSystem : ISystem
{
    public void Tick(SimContext ctx)
    {
        foreach (var pawnId in ctx.Entities.AllPawns())
        {
            if (!ctx.Entities.Moods.TryGetValue(pawnId, out var moodComp)) continue;
            if (!ctx.Entities.Buffs.TryGetValue(pawnId, out var buffComp)) continue;

            float mood = 0f;

            foreach (var inst in buffComp.ActiveBuffs)
            {
                if (ContentDatabase.Buffs.TryGetValue(inst.BuffDefId, out var buffDef))
                    mood += buffDef.MoodOffset;
            }

            moodComp.Mood = Math.Clamp(mood, -100f, 100f);
        }
    }
}

// Action execution (movement, using objects)
public sealed class ActionSystem : ISystem
{
    private const int MoveTicksPerTile = 10;
    private const int MaxBlockedTicks = 50;  // Give up on move after being blocked this long
    private readonly Random _random = new();

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

        // Get tiles occupied by other pawns for pathfinding
        var occupiedTiles = ctx.Entities.GetOccupiedTiles(pawnId);

        if (actionComp.CurrentPath == null)
        {
            actionComp.CurrentPath = Pathfinder.FindPath(ctx.World, pos.Coord, target, occupiedTiles);
            actionComp.PathIndex = 0;

            if (actionComp.CurrentPath == null || actionComp.CurrentPath.Count == 0)
            {
                // Can't reach destination - clear this action AND any queued actions
                // (e.g., if we can't reach an object, don't keep trying to use it)
                actionComp.CurrentAction = null;
                actionComp.ActionQueue.Clear();
                return;
            }
        }

        int ticksInAction = ctx.Time.Tick - actionComp.ActionStartTick;
        int expectedPathIndex = Math.Min(ticksInAction / MoveTicksPerTile, actionComp.CurrentPath.Count - 1);

        if (expectedPathIndex > actionComp.PathIndex)
        {
            var nextTile = actionComp.CurrentPath[expectedPathIndex];
            
            // Check if the next tile is occupied by another pawn
            var blockingPawnId = ctx.Entities.GetPawnAtTile(nextTile, pawnId);
            if (blockingPawnId != null)
            {
                // Track how long we've been blocked
                if (actionComp.BlockedSinceTick < 0)
                    actionComp.BlockedSinceTick = ctx.Time.Tick;
                
                int blockedDuration = ctx.Time.Tick - actionComp.BlockedSinceTick;
                
                // Give up if blocked too long
                if (blockedDuration >= MaxBlockedTicks)
                {
                    actionComp.CurrentAction = null;
                    actionComp.CurrentPath = null;
                    actionComp.BlockedSinceTick = -1;
                    actionComp.WaitUntilTick = -1;
                    actionComp.ActionQueue.Clear();  // Clear queued actions too
                    return;
                }
                
                // Priority-based yielding: Wandering pawns yield to goal-driven pawns
                bool iAmWandering = action.DisplayName == "Wandering";
                bool blockerIsWandering = false;
                if (ctx.Entities.Actions.TryGetValue(blockingPawnId.Value, out var blockerAction))
                {
                    blockerIsWandering = blockerAction.CurrentAction?.DisplayName == "Wandering";
                }
                
                // If I'm wandering and blocker has a goal, I should yield (cancel my action)
                if (iAmWandering && !blockerIsWandering)
                {
                    actionComp.CurrentAction = null;
                    actionComp.CurrentPath = null;
                    actionComp.BlockedSinceTick = -1;
                    actionComp.WaitUntilTick = -1;
                    return;
                }
                
                // Randomized wait before repathing to break the "sidewalk dance"
                if (actionComp.WaitUntilTick < 0)
                {
                    // Wait a random amount of time (5-20 ticks) before trying to repath
                    actionComp.WaitUntilTick = ctx.Time.Tick + _random.Next(5, 21);
                    return;
                }
                
                if (ctx.Time.Tick < actionComp.WaitUntilTick)
                {
                    // Still waiting
                    return;
                }
                
                // Done waiting - try to find a new path around the obstacle
                actionComp.WaitUntilTick = -1;  // Reset wait timer
                var newPath = Pathfinder.FindPath(ctx.World, pos.Coord, target, occupiedTiles);
                
                if (newPath != null && newPath.Count > 0)
                {
                    // Found alternate path - use it
                    actionComp.CurrentPath = newPath;
                    actionComp.PathIndex = 0;
                    actionComp.ActionStartTick = ctx.Time.Tick;
                    actionComp.BlockedSinceTick = -1;  // Reset blocked timer
                }
                // If no path found, will wait again with new random time next tick
                return;
            }
            
            // Not blocked anymore
            actionComp.BlockedSinceTick = -1;
            actionComp.WaitUntilTick = -1;
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
            // Get object name for display
            string objName = "Object";
            if (ctx.Entities.Objects.TryGetValue(targetId, out var tempObjComp))
                objName = ContentDatabase.Objects[tempObjComp.ObjectDefId].Name;
            
            actionComp.ActionQueue = new Queue<ActionDef>(new[] { action }.Concat(actionComp.ActionQueue));
            actionComp.CurrentAction = new ActionDef
            {
                Type = ActionType.MoveTo,
                TargetCoord = FindAdjacentWalkable(ctx.World, ctx.Entities, objPos.Coord, pawnPos.Coord, pawnId),
                DurationTicks = 0,
                DisplayName = $"Going to {objName}"
            };
            actionComp.ActionStartTick = ctx.Time.Tick;
            return;
        }

        if (ctx.Entities.Objects.TryGetValue(targetId, out var objComp))
        {
            objComp.InUse = true;
            objComp.UsedBy = pawnId;
            
            // Update display name to "Using X" now that we're actually using it
            var objDef = ContentDatabase.Objects[objComp.ObjectDefId];
            action.DisplayName = $"Using {objDef.Name}";
        }

        int elapsed = ctx.Time.Tick - actionComp.ActionStartTick;
        if (elapsed >= action.DurationTicks)
        {
            // Satisfy the need
            if (action.SatisfiesNeedId.HasValue && ctx.Entities.Needs.TryGetValue(pawnId, out var needs))
            {
                if (needs.Needs.ContainsKey(action.SatisfiesNeedId.Value))
                {
                    needs.Needs[action.SatisfiesNeedId.Value] = Math.Clamp(
                        needs.Needs[action.SatisfiesNeedId.Value] + action.NeedSatisfactionAmount,
                        0f, 100f);
                }
            }

            // Grant buff from object if applicable
            if (objComp != null)
            {
                var objDef = ContentDatabase.Objects[objComp.ObjectDefId];
                if (objDef.GrantsBuffId.HasValue && ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs))
                {
                    var buffDef = ContentDatabase.Buffs[objDef.GrantsBuffId.Value];
                    
                    // Remove existing instance of this buff (refresh it)
                    buffs.ActiveBuffs.RemoveAll(b => b.BuffDefId == objDef.GrantsBuffId.Value);
                    
                    buffs.ActiveBuffs.Add(new BuffInstance
                    {
                        BuffDefId = objDef.GrantsBuffId.Value,
                        StartTick = ctx.Time.Tick,
                        EndTick = ctx.Time.Tick + buffDef.DurationTicks
                    });
                }

                objComp.InUse = false;
                objComp.UsedBy = null;
            }

            actionComp.CurrentAction = null;
        }
    }

    private TileCoord FindAdjacentWalkable(World world, EntityManager entities, TileCoord target, TileCoord from, EntityId? excludePawn = null)
    {
        var candidates = new List<(TileCoord coord, int dist)>();
        foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            var adj = new TileCoord(target.X + dx, target.Y + dy);
            if (world.GetTile(adj).Walkable && !entities.IsTileOccupiedByPawn(adj, excludePawn))
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
    private readonly Random _random = new();
    
    // Don't seek objects if need is above this threshold
    private const float NeedSatisfiedThreshold = 80f;
    
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
                
                // Don't consider needs that are already satisfied
                if (value >= NeedSatisfiedThreshold) continue;

                float urgency = value;
                if (value < needDef.CriticalThreshold) urgency -= 50;
                else if (value < needDef.LowThreshold) urgency -= 20;

                if (urgency < lowestNeed)
                {
                    lowestNeed = urgency;
                    urgentNeedId = needId;
                }
            }

            EntityId? targetObject = null;
            if (urgentNeedId != null)
            {
                targetObject = FindObjectForNeed(ctx, pawnId, urgentNeedId.Value);
            }

            if (targetObject != null)
            {
                var objComp = ctx.Entities.Objects[targetObject.Value];
                var objDef = ContentDatabase.Objects[objComp.ObjectDefId];

                actionComp.ActionQueue.Enqueue(new ActionDef
                {
                    Type = ActionType.UseObject,
                    TargetEntity = targetObject,
                    DurationTicks = objDef.InteractionDurationTicks,
                    SatisfiesNeedId = objDef.SatisfiesNeedId,
                    NeedSatisfactionAmount = objDef.NeedSatisfactionAmount,
                    DisplayName = $"Going to {objDef.Name}"
                });
            }
            else
            {
                // No urgent need or no object available - wander randomly
                WanderRandomly(ctx, pawnId, actionComp);
            }
        }
    }

    private void WanderRandomly(SimContext ctx, EntityId pawnId, ActionComponent actionComp)
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pos))
            return;

        // Pick a random nearby tile to walk to
        var directions = new[] { (0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1) };
        var shuffled = directions.OrderBy(_ => _random.Next()).ToArray();

        foreach (var (dx, dy) in shuffled)
        {
            int wanderDist = _random.Next(1, 4); // 1-3 tiles
            var target = new TileCoord(pos.Coord.X + dx * wanderDist, pos.Coord.Y + dy * wanderDist);
            
            // Check if target is in bounds, walkable, and not occupied
            if (!ctx.World.IsInBounds(target)) continue;
            
            var tile = ctx.World.GetTile(target);
            if (tile.Walkable && !ctx.Entities.IsTileOccupiedByPawn(target, pawnId))
            {
                actionComp.ActionQueue.Enqueue(new ActionDef
                {
                    Type = ActionType.MoveTo,
                    TargetCoord = target,
                    DisplayName = "Wandering"
                });
                return;
            }
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
