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
    public Random Random => Sim.Random;
    public ContentRegistry Content => Sim.Content;

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

            var energyNeedId = ctx.Content.GetNeedId("Energy");
            
            foreach (var needId in needs.Needs.Keys.ToList())
            {
                if (!ctx.Content.Needs.TryGetValue(needId, out var needDef))
                    continue;

                // Calculate decay rate with time-of-day modifiers
                float decay = needDef.DecayPerTick;

                // Energy decays faster at night (pawns get sleepy)
                if (needId == energyNeedId && isNight)
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
                if (ctx.Content.Buffs.TryGetValue(inst.BuffDefId, out var buffDef))
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
                    actionComp.WaitUntilTick = ctx.Time.Tick + ctx.Random.Next(5, 21);
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

        if (pos.Coord == target)
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
        if (!ctx.Entities.Objects.TryGetValue(targetId, out var objCompCheck)) return;
        
        var objDefForCheck = ctx.Content.Objects[objCompCheck.ObjectDefId];
        
        // Check if pawn is in a valid use area for this object
        bool inUseArea = IsInUseArea(pawnPos.Coord, objPos.Coord, objDefForCheck);
        
        if (!inUseArea)
        {
            // Need to move to a valid use area first
            var useAreaTarget = FindValidUseArea(ctx.World, ctx.Entities, objPos.Coord, pawnPos.Coord, objDefForCheck, pawnId);
            
            if (useAreaTarget == null)
            {
                // No valid use area available (all blocked) - cancel action
                actionComp.CurrentAction = null;
                actionComp.ActionQueue.Clear();
                return;
            }
            
            actionComp.ActionQueue = new Queue<ActionDef>(new[] { action }.Concat(actionComp.ActionQueue));
            actionComp.CurrentAction = new ActionDef
            {
                Type = ActionType.MoveTo,
                TargetCoord = useAreaTarget,
                DurationTicks = 0,
                DisplayName = $"Going to {objDefForCheck.Name}"
            };
            actionComp.ActionStartTick = ctx.Time.Tick;
            return;
        }

        if (ctx.Entities.Objects.TryGetValue(targetId, out var objComp))
        {
            objComp.InUse = true;
            objComp.UsedBy = pawnId;
            
            // Update display name to "Using X" now that we're actually using it
            // Create a new ActionDef since ActionDef is immutable
            var objDef = ctx.Content.Objects[objComp.ObjectDefId];
            if (action.DisplayName != $"Using {objDef.Name}")
            {
                actionComp.CurrentAction = new ActionDef
                {
                    Type = action.Type,
                    TargetCoord = action.TargetCoord,
                    TargetEntity = action.TargetEntity,
                    DurationTicks = action.DurationTicks,
                    SatisfiesNeedId = action.SatisfiesNeedId,
                    NeedSatisfactionAmount = action.NeedSatisfactionAmount,
                    DisplayName = $"Using {objDef.Name}"
                };
                action = actionComp.CurrentAction;
            }
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
                var objDef2 = ctx.Content.Objects[objComp.ObjectDefId];
                if (objDef2.GrantsBuffId.HasValue && ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs))
                {
                    var buffDef = ctx.Content.Buffs[objDef2.GrantsBuffId.Value];
                    
                    // Remove existing instance of this buff (refresh it)
                    buffs.ActiveBuffs.RemoveAll(b => b.BuffDefId == objDef2.GrantsBuffId.Value);
                    
                    buffs.ActiveBuffs.Add(new BuffInstance
                    {
                        BuffDefId = objDef2.GrantsBuffId.Value,
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

    /// <summary>
    /// Check if a pawn position is within a valid use area for an object.
    /// </summary>
    private bool IsInUseArea(TileCoord pawnCoord, TileCoord objCoord, ObjectDef objDef)
    {
        // If no use areas defined, fall back to adjacent (distance 1)
        if (objDef.UseAreas.Count == 0)
        {
            int dist = Math.Abs(pawnCoord.X - objCoord.X) + Math.Abs(pawnCoord.Y - objCoord.Y);
            return dist <= 1;
        }
        
        foreach (var (dx, dy) in objDef.UseAreas)
        {
            var useAreaCoord = new TileCoord(objCoord.X + dx, objCoord.Y + dy);
            if (pawnCoord == useAreaCoord)
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Find the closest valid use area for an object that is walkable and not occupied.
    /// Returns null if no valid use area is available.
    /// </summary>
    private TileCoord? FindValidUseArea(World world, EntityManager entities, TileCoord objCoord, TileCoord from, ObjectDef objDef, EntityId? excludePawn = null)
    {
        var candidates = new List<(TileCoord coord, int dist)>();
        
        // Use the object's defined use areas, or fall back to cardinal directions
        var useAreas = objDef.UseAreas.Count > 0 
            ? objDef.UseAreas 
            : new List<(int dx, int dy)> { (0, 1), (0, -1), (1, 0), (-1, 0) };
        
        foreach (var (dx, dy) in useAreas)
        {
            var useAreaCoord = new TileCoord(objCoord.X + dx, objCoord.Y + dy);
            
            // Check bounds
            if (!world.IsInBounds(useAreaCoord)) continue;
            
            // Check if tile is walkable and not occupied by another pawn
            if (world.GetTile(useAreaCoord).Walkable && !entities.IsTileOccupiedByPawn(useAreaCoord, excludePawn))
            {
                int dist = Math.Abs(useAreaCoord.X - from.X) + Math.Abs(useAreaCoord.Y - from.Y);
                candidates.Add((useAreaCoord, dist));
            }
        }
        
        return candidates.Count > 0
            ? candidates.OrderBy(c => c.dist).First().coord
            : null;
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

            // Skip pawns that already have something to do
            if (actionComp.CurrentAction != null || actionComp.ActionQueue.Count > 0)
                continue;

            DecideNextAction(ctx, pawnId, actionComp, needs);
        }
    }

    /// <summary>
    /// Main decision entry point for a pawn that needs something to do.
    /// </summary>
    private void DecideNextAction(SimContext ctx, EntityId pawnId, ActionComponent actionComp, NeedsComponent needs)
    {
        var urgentNeeds = CalculateUrgentNeeds(ctx, pawnId, needs);

        // Try to find an available object for any of our urgent needs
        EntityId? targetObject = null;
        foreach (var (needId, _) in urgentNeeds)
        {
            targetObject = FindObjectForNeed(ctx, pawnId, needId);
            if (targetObject != null)
                break;
        }

        if (targetObject != null)
        {
            QueueUseObject(ctx, actionComp, targetObject.Value);
        }
        else if (urgentNeeds.Count > 0 && urgentNeeds[0].urgency < -50)
        {
            // Critical needs but no available objects - wait near an object
            QueueWaitForObject(ctx, pawnId, actionComp, urgentNeeds);
        }
        else
        {
            WanderRandomly(ctx, pawnId, actionComp);
        }
    }

    /// <summary>
    /// Calculate which needs require attention, sorted by urgency (most urgent first).
    /// </summary>
    private List<(int needId, float urgency)> CalculateUrgentNeeds(SimContext ctx, EntityId pawnId, NeedsComponent needs)
    {
        var urgentNeeds = new List<(int needId, float urgency)>();

        // Get active debuffs to check which needs are causing problems
        var activeDebuffIds = new HashSet<int>();
        if (ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs))
        {
            foreach (var buff in buffs.ActiveBuffs)
                activeDebuffIds.Add(buff.BuffDefId);
        }

        foreach (var (needId, value) in needs.Needs)
        {
            if (!ctx.Content.Needs.TryGetValue(needId, out var needDef)) continue;

            float? urgency = CalculateNeedUrgency(needDef, value, activeDebuffIds);
            if (urgency.HasValue)
                urgentNeeds.Add((needId, urgency.Value));
        }

        // Sort by urgency (lowest first = most urgent)
        urgentNeeds.Sort((a, b) => a.urgency.CompareTo(b.urgency));
        return urgentNeeds;
    }

    /// <summary>
    /// Calculate urgency for a single need. Returns null if the need doesn't require attention.
    /// Lower values = more urgent.
    /// </summary>
    private float? CalculateNeedUrgency(NeedDef needDef, float value, HashSet<int> activeDebuffIds)
    {
        bool hasDebuffFromNeed =
            (needDef.CriticalDebuffId.HasValue && activeDebuffIds.Contains(needDef.CriticalDebuffId.Value)) ||
            (needDef.LowDebuffId.HasValue && activeDebuffIds.Contains(needDef.LowDebuffId.Value));

        // Skip needs that are high AND not causing debuffs
        if (value >= 90f && !hasDebuffFromNeed)
            return null;

        float urgency = value;
        if (hasDebuffFromNeed)
        {
            // Having a debuff makes this very urgent
            if (needDef.CriticalDebuffId.HasValue && activeDebuffIds.Contains(needDef.CriticalDebuffId.Value))
                urgency -= 100;  // Critical debuff - highest priority
            else
                urgency -= 50;   // Low debuff - high priority
        }
        else if (value < needDef.LowThreshold)
        {
            urgency -= 20;  // About to get a debuff
        }

        return urgency;
    }

    /// <summary>
    /// Queue an action to use a specific object.
    /// </summary>
    private void QueueUseObject(SimContext ctx, ActionComponent actionComp, EntityId targetObject)
    {
        var objComp = ctx.Entities.Objects[targetObject];
        var objDef = ctx.Content.Objects[objComp.ObjectDefId];

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

    /// <summary>
    /// Queue an action to wait near an object when all relevant objects are in use.
    /// </summary>
    private void QueueWaitForObject(SimContext ctx, EntityId pawnId, ActionComponent actionComp, List<(int needId, float urgency)> urgentNeeds)
    {
        var waitTarget = FindAnyObjectForNeeds(ctx, pawnId, urgentNeeds.Select(n => n.needId).ToList());
        if (waitTarget == null)
        {
            // No objects exist at all for our needs - wander
            WanderRandomly(ctx, pawnId, actionComp);
            return;
        }

        var objComp = ctx.Entities.Objects[waitTarget.Value];
        var objDef = ctx.Content.Objects[objComp.ObjectDefId];
        var objPos = ctx.Entities.Positions[waitTarget.Value];

        var waitSpot = FindWaitingSpot(ctx, objPos.Coord, pawnId);
        if (waitSpot != null)
        {
            actionComp.ActionQueue.Enqueue(new ActionDef
            {
                Type = ActionType.MoveTo,
                TargetCoord = waitSpot,
                DisplayName = $"Waiting for {objDef.Name}"
            });
        }
        else
        {
            // Can't find waiting spot, just idle briefly
            actionComp.ActionQueue.Enqueue(new ActionDef
            {
                Type = ActionType.Idle,
                DurationTicks = 20,
                DisplayName = $"Waiting for {objDef.Name}"
            });
        }
    }

    private void WanderRandomly(SimContext ctx, EntityId pawnId, ActionComponent actionComp)
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pos))
            return;

        // Pick a random nearby tile to walk to
        var directions = new[] { (0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1) };
        var shuffled = directions.OrderBy(_ => ctx.Random.Next()).ToArray();

        foreach (var (dx, dy) in shuffled)
        {
            int wanderDist = ctx.Random.Next(1, 4); // 1-3 tiles
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
            var objDef = ctx.Content.Objects[objComp.ObjectDefId];

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

    /// <summary>
    /// Find any object (even if in use) that could satisfy one of the given needs.
    /// Used for finding a place to wait when all relevant objects are busy.
    /// </summary>
    private EntityId? FindAnyObjectForNeeds(SimContext ctx, EntityId pawnId, List<int> needIds)
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return null;

        EntityId? best = null;
        int bestDist = int.MaxValue;

        foreach (var objId in ctx.Entities.AllObjects())
        {
            var objComp = ctx.Entities.Objects[objId];
            var objDef = ctx.Content.Objects[objComp.ObjectDefId];

            // Check if this object satisfies any of our needs
            if (!objDef.SatisfiesNeedId.HasValue || !needIds.Contains(objDef.SatisfiesNeedId.Value)) 
                continue;

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

    /// <summary>
    /// Find a walkable tile near an object where a pawn can wait.
    /// </summary>
    private TileCoord? FindWaitingSpot(SimContext ctx, TileCoord objectPos, EntityId pawnId)
    {
        // Look for walkable tiles within 2 tiles of the object
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                if (dx == 0 && dy == 0) continue;  // Skip the object tile itself
                
                var candidate = new TileCoord(objectPos.X + dx, objectPos.Y + dy);
                
                if (!ctx.World.IsInBounds(candidate)) continue;
                
                var tile = ctx.World.GetTile(candidate);
                if (tile.Walkable && !ctx.Entities.IsTileOccupiedByPawn(candidate, pawnId))
                {
                    return candidate;
                }
            }
        }
        
        return null;
    }
}
