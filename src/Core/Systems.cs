using System;
using System.Collections.Generic;
using System.Linq;

namespace SimGame.Core;

// Passive social need gain when pawns are near each other
public sealed class ProximitySocialSystem : ISystem
{
    private const float SocialGainPerTick = 0.15f;
    private const int ProximityRadius = 2;

    public void Tick(SimContext ctx)
    {
        var socialNeedIdNullable = ctx.Content.GetNeedId("Social");
        if (!socialNeedIdNullable.HasValue)
            return;
        var socialNeedId = socialNeedIdNullable.Value;
        // Build a dictionary of pawn positions
        var pawnPositions = new Dictionary<EntityId, TileCoord>();
        foreach (var pawnId in ctx.Entities.AllPawns())
        {
            if (ctx.Entities.Positions.TryGetValue(pawnId, out var pos))
                pawnPositions[pawnId] = pos.Coord;
        }

        foreach (var kv in pawnPositions)
        {
            var pawnId = kv.Key;
            var pos = kv.Value;
            if (!ctx.Entities.Needs.TryGetValue(pawnId, out var needs))
                continue;
            if (!needs.Needs.ContainsKey(socialNeedId))
                continue;

            // Count other pawns within radius
            int nearby = 0;
            foreach (var other in pawnPositions)
            {
                if (other.Key == pawnId)
                    continue;
                int dist = Math.Abs(other.Value.X - pos.X) + Math.Abs(other.Value.Y - pos.Y);
                if (dist <= ProximityRadius)
                    nearby++;
            }

            if (nearby > 0)
            {
                needs.Needs[socialNeedId] = Math.Clamp(
                    needs.Needs[socialNeedId] + SocialGainPerTick * nearby,
                    0f,
                    100f
                );
            }
        }
    }
}

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
    public const int TicksPerMinute = 10; // 0.5 real seconds = 1 game minute (2x speed)
    public const int MinutesPerHour = 60;
    public const int HoursPerDay = 24;
    public const int TicksPerHour = TicksPerMinute * MinutesPerHour;
    public const int TicksPerDay = TicksPerHour * HoursPerDay;
    public const int DefaultStartHour = 8;

    public int Tick { get; private set; }

    public TimeService()
        : this(DefaultStartHour) { }

    public TimeService(int startHour)
    {
        Tick = startHour * TicksPerHour;
    }

    public int TotalMinutes => Tick / TicksPerMinute;
    public int Minute => TotalMinutes % MinutesPerHour;
    public int Hour => (Tick / TicksPerHour) % HoursPerDay;
    public int Day => (Tick / TicksPerDay) + 1;

    public bool IsNight => Hour < 6 || Hour >= 22; // 10 PM - 6 AM
    public bool IsSleepTime => Hour < 6 || Hour >= 23; // 11 PM - 6 AM

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
            buffs.ActiveBuffs.Add(
                new BuffInstance
                {
                    BuffDefId = needDef.CriticalDebuffId.Value,
                    StartTick = 0,
                    EndTick = -1, // Permanent until need recovers
                }
            );
        }
        else if (value < needDef.LowThreshold && needDef.LowDebuffId.HasValue)
        {
            buffs.ActiveBuffs.Add(
                new BuffInstance
                {
                    BuffDefId = needDef.LowDebuffId.Value,
                    StartTick = 0,
                    EndTick = -1, // Permanent until need recovers
                }
            );
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
            if (!ctx.Entities.Moods.TryGetValue(pawnId, out var moodComp))
                continue;
            if (!ctx.Entities.Buffs.TryGetValue(pawnId, out var buffComp))
                continue;

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

// Action execution (movement, using buildings)
public sealed class ActionSystem : ISystem
{
    private const int MoveTicksPerTile = 10;
    private const int MaxBlockedTicks = 50; // Give up on move after being blocked this long

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
                else
                    continue;
            }

            var action = actionComp.CurrentAction;

            switch (action.Type)
            {
                case ActionType.MoveTo:
                    ExecuteMoveTo(ctx, pawnId, actionComp);
                    break;
                case ActionType.UseBuilding:
                    ExecuteUseBuilding(ctx, pawnId, actionComp);
                    break;
                case ActionType.Work:
                    ExecuteWork(ctx, pawnId, actionComp);
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
        if (action.TargetCoord == null)
        {
            actionComp.CurrentAction = null;
            return;
        }

        var pos = ctx.Entities.Positions[pawnId];
        var target = action.TargetCoord.Value;

        // Get tiles occupied by other pawns for pathfinding
        var occupiedTiles = ctx.Entities.GetOccupiedTiles(pawnId);

        if (actionComp.CurrentPath == null)
        {
            actionComp.CurrentPath = Pathfinder.FindPath(
                ctx.World,
                pos.Coord,
                target,
                occupiedTiles
            );
            actionComp.PathIndex = 0;

            if (actionComp.CurrentPath == null || actionComp.CurrentPath.Count == 0)
            {
                // Can't reach destination - clear this action AND any queued actions
                // (e.g., if we can't reach a building, don't keep trying to use it)
                actionComp.CurrentAction = null;
                actionComp.ActionQueue.Clear();
                return;
            }
        }

        int ticksInAction = ctx.Time.Tick - actionComp.ActionStartTick;
        int expectedPathIndex = Math.Min(
            ticksInAction / MoveTicksPerTile,
            actionComp.CurrentPath.Count - 1
        );

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
                    actionComp.ActionQueue.Clear();
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
                    actionComp.ActionQueue.Clear(); // Also clear queued actions (e.g., the Idle after wandering)
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
                actionComp.WaitUntilTick = -1; // Reset wait timer
                var newPath = Pathfinder.FindPath(ctx.World, pos.Coord, target, occupiedTiles);

                if (newPath != null && newPath.Count > 0)
                {
                    // Found alternate path - use it
                    actionComp.CurrentPath = newPath;
                    actionComp.PathIndex = 0;
                    actionComp.ActionStartTick = ctx.Time.Tick;
                    actionComp.BlockedSinceTick = -1;
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

    private void ExecuteUseBuilding(SimContext ctx, EntityId pawnId, ActionComponent actionComp)
    {
        var action = actionComp.CurrentAction!;
        if (action.TargetEntity == null)
        {
            actionComp.CurrentAction = null;
            return;
        }

        var targetId = action.TargetEntity.Value;

        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return;
        if (!ctx.Entities.Positions.TryGetValue(targetId, out var objPos))
            return;
        if (!ctx.Entities.Buildings.TryGetValue(targetId, out var objCompCheck))
            return;

        var objDefForCheck = ctx.Content.Buildings[objCompCheck.BuildingDefId];

        // Check if pawn is in a valid use area for this building
        bool inUseArea = IsInUseArea(pawnPos.Coord, objPos.Coord, objDefForCheck);

        if (!inUseArea)
        {
            // Need to move to a valid use area first
            var useAreaTarget = FindValidUseArea(
                ctx.World,
                ctx.Entities,
                objPos.Coord,
                pawnPos.Coord,
                objDefForCheck,
                pawnId
            );

            if (useAreaTarget == null)
            {
                // No valid use area available (all blocked) - cancel action
                actionComp.CurrentAction = null;
                actionComp.ActionQueue.Clear();
                return;
            }

            actionComp.ActionQueue = new Queue<ActionDef>(
                new[] { action }.Concat(actionComp.ActionQueue)
            );
            actionComp.CurrentAction = new ActionDef
            {
                Type = ActionType.MoveTo,
                Animation = AnimationType.Walk,
                TargetCoord = useAreaTarget,
                DurationTicks = 0,
                DisplayName = $"Going to {objDefForCheck.Name}",
            };
            actionComp.ActionStartTick = ctx.Time.Tick;
            return;
        }

        if (ctx.Entities.Buildings.TryGetValue(targetId, out var buildingComp))
        {
            buildingComp.InUse = true;
            buildingComp.UsedBy = pawnId;

            // Update display name to "Using X" now that we're actually using it
            var buildingDef = ctx.Content.Buildings[buildingComp.BuildingDefId];
            if (action.DisplayName != $"Using {buildingDef.Name}")
            {
                actionComp.CurrentAction = new ActionDef
                {
                    Type = action.Type,
                    Animation = action.Animation,
                    TargetCoord = action.TargetCoord,
                    TargetEntity = action.TargetEntity,
                    DurationTicks = action.DurationTicks,
                    SatisfiesNeedId = action.SatisfiesNeedId,
                    NeedSatisfactionAmount = action.NeedSatisfactionAmount,
                    DisplayName = $"Using {buildingDef.Name}",
                };
                action = actionComp.CurrentAction;
            }
        }

        int elapsed = ctx.Time.Tick - actionComp.ActionStartTick;
        if (elapsed >= action.DurationTicks)
        {
            // Check if building has resources and deplete them
            bool hasResources = true;
            if (
                buildingComp != null
                && ctx.Entities.Resources.TryGetValue(targetId, out var resourceComp)
            )
            {
                if (resourceComp.CurrentAmount > 0)
                {
                    // Deplete resources (20 per use, scaled by depletionMult)
                    float depletionAmount = 20f * resourceComp.DepletionMult;
                    resourceComp.CurrentAmount = Math.Max(
                        0f,
                        resourceComp.CurrentAmount - depletionAmount
                    );
                }
                else
                {
                    hasResources = false;
                }
            }

            // Satisfy the need only if resources are available
            if (
                hasResources
                && action.SatisfiesNeedId.HasValue
                && ctx.Entities.Needs.TryGetValue(pawnId, out var needs)
            )
            {
                if (needs.Needs.ContainsKey(action.SatisfiesNeedId.Value))
                {
                    needs.Needs[action.SatisfiesNeedId.Value] = Math.Clamp(
                        needs.Needs[action.SatisfiesNeedId.Value] + action.NeedSatisfactionAmount,
                        0f,
                        100f
                    );
                }
            }

            // Grant buff from building if applicable (only if resources were available)
            if (buildingComp != null && hasResources)
            {
                var buildingDef2 = ctx.Content.Buildings[buildingComp.BuildingDefId];
                if (
                    buildingDef2.GrantsBuffId.HasValue
                    && ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs)
                )
                {
                    var buffDef = ctx.Content.Buffs[buildingDef2.GrantsBuffId.Value];

                    // Remove existing instance of this buff (refresh it)
                    buffs.ActiveBuffs.RemoveAll(b =>
                        b.BuffDefId == buildingDef2.GrantsBuffId.Value
                    );

                    buffs.ActiveBuffs.Add(
                        new BuffInstance
                        {
                            BuffDefId = buildingDef2.GrantsBuffId.Value,
                            StartTick = ctx.Time.Tick,
                            EndTick = ctx.Time.Tick + buffDef.DurationTicks,
                        }
                    );
                }

                // Increment attachment (cap at 10)
                if (ctx.Entities.Attachments.TryGetValue(targetId, out var attachmentComp))
                {
                    if (!attachmentComp.UserAttachments.ContainsKey(pawnId))
                    {
                        attachmentComp.UserAttachments[pawnId] = 0;
                    }
                    attachmentComp.UserAttachments[pawnId] = Math.Min(
                        10,
                        attachmentComp.UserAttachments[pawnId] + 1
                    );
                }
            }

            // Release the building
            if (buildingComp != null)
            {
                buildingComp.InUse = false;
                buildingComp.UsedBy = null;
            }

            // Add a brief idle action showing result
            if (buildingComp != null && action.SatisfiesNeedId.HasValue)
            {
                var buildingDef3 = ctx.Content.Buildings[buildingComp.BuildingDefId];
                actionComp.ActionQueue.Enqueue(
                    new ActionDef
                    {
                        Type = ActionType.Idle,
                        Animation = AnimationType.Idle,
                        DurationTicks = 10, // Brief moment
                        DisplayName = hasResources ? "Satisfied" : "Out of Resources",
                        Expression = hasResources ? ExpressionType.Happy : ExpressionType.Complaint,
                        ExpressionIconDefId = buildingDef3.Id,
                    }
                );
            }

            actionComp.CurrentAction = null;
        }
    }

    private void ExecuteWork(SimContext ctx, EntityId pawnId, ActionComponent actionComp)
    {
        var action = actionComp.CurrentAction!;
        if (action.TargetEntity == null)
        {
            actionComp.CurrentAction = null;
            return;
        }

        var targetId = action.TargetEntity.Value;

        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return;
        if (!ctx.Entities.Positions.TryGetValue(targetId, out var objPos))
            return;
        if (!ctx.Entities.Buildings.TryGetValue(targetId, out var objCompCheck))
            return;

        var objDefForCheck = ctx.Content.Buildings[objCompCheck.BuildingDefId];

        // Check if pawn is in a valid use area for this building
        bool inUseArea = IsInUseArea(pawnPos.Coord, objPos.Coord, objDefForCheck);

        if (!inUseArea)
        {
            // Need to move to a valid use area first
            var useAreaTarget = FindValidUseArea(
                ctx.World,
                ctx.Entities,
                objPos.Coord,
                pawnPos.Coord,
                objDefForCheck,
                pawnId
            );

            if (useAreaTarget == null)
            {
                // No valid use area available (all blocked) - cancel action
                actionComp.CurrentAction = null;
                actionComp.ActionQueue.Clear();
                return;
            }

            actionComp.ActionQueue = new Queue<ActionDef>(
                new[] { action }.Concat(actionComp.ActionQueue)
            );
            actionComp.CurrentAction = new ActionDef
            {
                Type = ActionType.MoveTo,
                Animation = AnimationType.Walk,
                TargetCoord = useAreaTarget,
                DurationTicks = 0,
                DisplayName = $"Going to work at {objDefForCheck.Name}",
            };
            actionComp.ActionStartTick = ctx.Time.Tick;
            return;
        }

        if (ctx.Entities.Buildings.TryGetValue(targetId, out var buildingComp2))
        {
            buildingComp2.InUse = true;
            buildingComp2.UsedBy = pawnId;

            // Update display name to "Working at X" now that we're actually working
            var buildingDef2 = ctx.Content.Buildings[buildingComp2.BuildingDefId];
            if (action.DisplayName != $"Working at {buildingDef2.Name}")
            {
                actionComp.CurrentAction = new ActionDef
                {
                    Type = action.Type,
                    Animation = AnimationType.Pickaxe,
                    TargetCoord = action.TargetCoord,
                    TargetEntity = action.TargetEntity,
                    DurationTicks = action.DurationTicks,
                    SatisfiesNeedId = action.SatisfiesNeedId,
                    NeedSatisfactionAmount = action.NeedSatisfactionAmount,
                    DisplayName = $"Working at {buildingDef2.Name}",
                };
                action = actionComp.CurrentAction;
            }
        }

        int elapsed = ctx.Time.Tick - actionComp.ActionStartTick;
        if (elapsed >= action.DurationTicks)
        {
            // Replenish building resources if applicable
            if (
                buildingComp2 != null
                && ctx.Entities.Resources.TryGetValue(targetId, out var resourceComp)
            )
            {
                // Add 30 resources per work action
                resourceComp.CurrentAmount = Math.Min(
                    resourceComp.MaxAmount,
                    resourceComp.CurrentAmount + 30f
                );
            }

            // Satisfy the Purpose need
            if (
                action.SatisfiesNeedId.HasValue
                && ctx.Entities.Needs.TryGetValue(pawnId, out var needs)
            )
            {
                if (needs.Needs.ContainsKey(action.SatisfiesNeedId.Value))
                {
                    needs.Needs[action.SatisfiesNeedId.Value] = Math.Clamp(
                        needs.Needs[action.SatisfiesNeedId.Value] + action.NeedSatisfactionAmount,
                        0f,
                        100f
                    );
                }
            }

            // Grant "Productive" buff
            if (buildingComp2 != null)
            {
                var productiveBuffId = ctx.Content.GetBuffId("Productive");
                if (
                    productiveBuffId.HasValue
                    && ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs)
                )
                {
                    var buffDef = ctx.Content.Buffs[productiveBuffId.Value];

                    // Remove existing instance of this buff (refresh it)
                    buffs.ActiveBuffs.RemoveAll(b => b.BuffDefId == productiveBuffId.Value);

                    buffs.ActiveBuffs.Add(
                        new BuffInstance
                        {
                            BuffDefId = productiveBuffId.Value,
                            StartTick = ctx.Time.Tick,
                            EndTick = ctx.Time.Tick + buffDef.DurationTicks,
                        }
                    );
                }

                // Increment attachment to this work building (cap at 10)
                if (ctx.Entities.Attachments.TryGetValue(targetId, out var attachmentComp))
                {
                    if (!attachmentComp.UserAttachments.ContainsKey(pawnId))
                    {
                        attachmentComp.UserAttachments[pawnId] = 0;
                    }
                    attachmentComp.UserAttachments[pawnId] = Math.Min(
                        10,
                        attachmentComp.UserAttachments[pawnId] + 1
                    );
                }
            }

            // Release the building
            if (buildingComp2 != null)
            {
                buildingComp2.InUse = false;
                buildingComp2.UsedBy = null;
            }

            // Add a brief happy idle action showing satisfaction
            if (buildingComp2 != null && action.SatisfiesNeedId.HasValue)
            {
                var buildingDef3 = ctx.Content.Buildings[buildingComp2.BuildingDefId];
                actionComp.ActionQueue.Enqueue(
                    new ActionDef
                    {
                        Type = ActionType.Idle,
                        Animation = AnimationType.Idle,
                        DurationTicks = 10, // Brief moment
                        DisplayName = "Feeling Productive",
                        Expression = ExpressionType.Happy,
                        ExpressionIconDefId = buildingDef3.Id,
                    }
                );
            }

            actionComp.CurrentAction = null;
        }
    }

    /// <summary>
    /// Check if a pawn position is within a valid use area for a building.
    /// </summary>
    private bool IsInUseArea(TileCoord pawnCoord, TileCoord objCoord, BuildingDef buildingDef)
    {
        // If no use areas defined, fall back to adjacent (distance 1)
        if (buildingDef.UseAreas.Count == 0)
        {
            int dist = Math.Abs(pawnCoord.X - objCoord.X) + Math.Abs(pawnCoord.Y - objCoord.Y);
            return dist <= 1;
        }

        foreach (var (dx, dy) in buildingDef.UseAreas)
        {
            var useAreaCoord = new TileCoord(objCoord.X + dx, objCoord.Y + dy);
            if (pawnCoord == useAreaCoord)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Find the closest valid use area for a building that is walkable and not occupied.
    /// Returns null if no valid use area is available.
    /// </summary>
    private TileCoord? FindValidUseArea(
        World world,
        EntityManager entities,
        TileCoord objCoord,
        TileCoord from,
        BuildingDef buildingDef,
        EntityId? excludePawn = null
    )
    {
        var candidates = new List<(TileCoord coord, int dist)>();

        // Use the building's defined use areas, or fall back to cardinal directions
        var useAreas =
            buildingDef.UseAreas.Count > 0
                ? buildingDef.UseAreas
                : new List<(int dx, int dy)> { (0, 1), (0, -1), (1, 0), (-1, 0) };

        foreach (var (dx, dy) in useAreas)
        {
            var useAreaCoord = new TileCoord(objCoord.X + dx, objCoord.Y + dy);

            // Check bounds
            if (!world.IsInBounds(useAreaCoord))
                continue;

            // Check if tile is walkable and not occupied by another pawn
            if (
                world.GetTile(useAreaCoord).Walkable
                && !entities.IsTileOccupiedByPawn(useAreaCoord, excludePawn)
            )
            {
                int dist = Math.Abs(useAreaCoord.X - from.X) + Math.Abs(useAreaCoord.Y - from.Y);
                candidates.Add((useAreaCoord, dist));
            }
        }

        return candidates.Count > 0 ? candidates.OrderBy(c => c.dist).First().coord : null;
    }
}

// Utility AI - decides what action to take
public sealed class AISystem : ISystem
{
    public void Tick(SimContext ctx)
    {
        foreach (var pawnId in ctx.Entities.AllPawns())
        {
            if (!ctx.Entities.Actions.TryGetValue(pawnId, out var actionComp))
                continue;
            if (!ctx.Entities.Needs.TryGetValue(pawnId, out var needs))
                continue;

            // Skip pawns that already have something to do
            if (actionComp.CurrentAction != null || actionComp.ActionQueue.Count > 0)
                continue;

            DecideNextAction(ctx, pawnId, actionComp, needs);
        }
    }

    /// <summary>
    /// Main decision entry point for a pawn that needs something to do.
    /// </summary>
    private void DecideNextAction(
        SimContext ctx,
        EntityId pawnId,
        ActionComponent actionComp,
        NeedsComponent needs
    )
    {
        var urgentNeeds = CalculateUrgentNeeds(ctx, pawnId, needs);

        // Get Purpose need ID for special handling
        var purposeNeedId = ctx.Content.GetNeedId("Purpose");

        // Try to find an available building for any of our urgent needs
        // Only seek buildings when the need is actually pressing (urgency < 50)
        EntityId? targetBuilding = null;
        bool isWorkAction = false;
        foreach (var (needId, urgency) in urgentNeeds)
        {
            // Only pursue buildings for needs that are actually low or causing issues
            // Urgency < 50 means either:
            // - Need value is very low (< 50), OR
            // - Need has a debuff (urgency gets -50 or -100 modifier)
            if (urgency < 50)
            {
                // Purpose need is satisfied by working, not consuming
                if (purposeNeedId.HasValue && needId == purposeNeedId.Value)
                {
                    targetBuilding = FindBuildingToWorkAt(ctx, pawnId);
                    if (targetBuilding != null)
                    {
                        isWorkAction = true;
                        break;
                    }
                }
                else
                {
                    targetBuilding = FindBuildingForNeed(ctx, pawnId, needId);
                    if (targetBuilding != null)
                        break;
                }
            }
        }

        if (targetBuilding != null)
        {
            if (isWorkAction)
            {
                QueueWorkAtBuilding(ctx, actionComp, targetBuilding.Value, purposeNeedId!.Value);
            }
            else
            {
                QueueUseBuilding(ctx, actionComp, targetBuilding.Value);
            }
        }
        else if (urgentNeeds.Count > 0 && urgentNeeds[0].urgency < -50)
        {
            // Critical needs but no available buildings - wait near a building
            QueueWaitForBuilding(ctx, pawnId, actionComp, urgentNeeds);
        }
        else
        {
            WanderRandomly(ctx, pawnId, actionComp);
        }
    }

    /// <summary>
    /// Calculate which needs require attention, sorted by urgency (most urgent first).
    /// </summary>
    private List<(int needId, float urgency)> CalculateUrgentNeeds(
        SimContext ctx,
        EntityId pawnId,
        NeedsComponent needs
    )
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
            if (!ctx.Content.Needs.TryGetValue(needId, out var needDef))
                continue;

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
            (
                needDef.CriticalDebuffId.HasValue
                && activeDebuffIds.Contains(needDef.CriticalDebuffId.Value)
            )
            || (
                needDef.LowDebuffId.HasValue && activeDebuffIds.Contains(needDef.LowDebuffId.Value)
            );

        // Skip needs that are high AND not causing debuffs
        if (value >= 90f && !hasDebuffFromNeed)
            return null;

        float urgency = value;
        if (hasDebuffFromNeed)
        {
            // Having a debuff makes this very urgent
            if (
                needDef.CriticalDebuffId.HasValue
                && activeDebuffIds.Contains(needDef.CriticalDebuffId.Value)
            )
                urgency -= 100; // Critical debuff - highest priority
            else
                urgency -= 50; // Low debuff - high priority
        }
        else if (value < needDef.LowThreshold)
        {
            urgency -= 20; // About to get a debuff
        }

        return urgency;
    }

    /// <summary>
    /// Queue an action to use a specific building.
    /// </summary>
    private void QueueUseBuilding(
        SimContext ctx,
        ActionComponent actionComp,
        EntityId targetBuilding
    )
    {
        var buildingComp = ctx.Entities.Buildings[targetBuilding];
        var buildingDef = ctx.Content.Buildings[buildingComp.BuildingDefId];
        actionComp.ActionQueue.Enqueue(
            new ActionDef
            {
                Type = ActionType.UseBuilding,
                Animation = AnimationType.Idle,
                TargetEntity = targetBuilding,
                DurationTicks = buildingDef.InteractionDurationTicks,
                SatisfiesNeedId = buildingDef.SatisfiesNeedId,
                NeedSatisfactionAmount = buildingDef.NeedSatisfactionAmount,
                DisplayName = $"Going to {buildingDef.Name}",
            }
        );
    }

    /// <summary>
    /// Queue an action to work at a specific building (replenish resources).
    /// </summary>
    private void QueueWorkAtBuilding(
        SimContext ctx,
        ActionComponent actionComp,
        EntityId targetBuilding,
        int purposeNeedId
    )
    {
        var buildingComp2 = ctx.Entities.Buildings[targetBuilding];
        var buildingDef2 = ctx.Content.Buildings[buildingComp2.BuildingDefId];
        actionComp.ActionQueue.Enqueue(
            new ActionDef
            {
                Type = ActionType.Work,
                Animation = AnimationType.Pickaxe,
                TargetEntity = targetBuilding,
                DurationTicks = 2500, // Work takes 2.5 seconds
                SatisfiesNeedId = purposeNeedId,
                NeedSatisfactionAmount = 40f, // Working satisfies Purpose moderately
                DisplayName = $"Going to work at {buildingDef2.Name}",
            }
        );
    }

    /// <summary>
    /// Queue an action to wait near a building when all relevant buildings are in use.
    /// </summary>
    private void QueueWaitForBuilding(
        SimContext ctx,
        EntityId pawnId,
        ActionComponent actionComp,
        List<(int needId, float urgency)> urgentNeeds
    )
    {
        var waitTarget = FindAnyBuildingForNeeds(
            ctx,
            pawnId,
            urgentNeeds.Select(n => n.needId).ToList()
        );
        if (waitTarget == null)
        {
            // No buildings exist at all for our needs - wander
            WanderRandomly(ctx, pawnId, actionComp);
            return;
        }

        var objComp = ctx.Entities.Buildings[waitTarget.Value];
        var objDef = ctx.Content.Buildings[objComp.BuildingDefId];
        var objPos = ctx.Entities.Positions[waitTarget.Value];

        var waitSpot = FindWaitingSpot(ctx, objPos.Coord, pawnId);
        if (waitSpot != null)
        {
            actionComp.ActionQueue.Enqueue(
                new ActionDef
                {
                    Type = ActionType.MoveTo,
                    Animation = AnimationType.Walk,
                    TargetCoord = waitSpot,
                    DisplayName = $"Waiting for {objDef.Name}",
                }
            );
        }
        else
        {
            // Can't find waiting spot, just idle briefly
            actionComp.ActionQueue.Enqueue(
                new ActionDef
                {
                    Type = ActionType.Idle,
                    Animation = AnimationType.Idle,
                    DurationTicks = 20,
                    DisplayName = $"Waiting for {objDef.Name}",
                }
            );
        }
    }

    private void WanderRandomly(SimContext ctx, EntityId pawnId, ActionComponent actionComp)
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pos))
            return;

        // Get diversity map for the whole world (cached per tick in simulation)
        var diversityMap = ctx.Sim.GetDiversityMap();

        // Step 1: Gather all potential wander destinations
        var potentialTargets = new List<TileCoord>();

        // Add 10 random tiles from anywhere on the map
        for (int i = 0; i < 10; i++)
        {
            int x = ctx.Random.Next(0, ctx.World.Width);
            int y = ctx.Random.Next(0, ctx.World.Height);
            potentialTargets.Add(new TileCoord(x, y));
        }

        // Add nearby tiles in all directions (distances 1-3)
        var directions = new[]
        {
            (0, 1),
            (0, -1),
            (1, 0),
            (-1, 0),
            (1, 1),
            (1, -1),
            (-1, 1),
            (-1, -1),
        };
        foreach (var (dx, dy) in directions)
        {
            for (int dist = 1; dist <= 3; dist++)
            {
                var newCoord = new TileCoord(pos.Coord.X + dx * dist, pos.Coord.Y + dy * dist);
                potentialTargets.Add(newCoord);
                // Cap diversity to max 1 for nearby tiles to avoid pawns getting stuck
                if (ctx.World.IsInBounds(newCoord))
                {
                    diversityMap[newCoord.X, newCoord.Y] = Math.Min(
                        1,
                        diversityMap[newCoord.X, newCoord.Y]
                    );
                }
            }
        }

        // Step 2: Filter to valid candidates (walkable, unoccupied, not current position)
        var candidates = potentialTargets
            .Where(target => !target.Equals(pos.Coord))
            .Where(target => ctx.World.IsWalkable(target))
            .Where(target => !ctx.Entities.IsTileOccupiedByPawn(target, pawnId))
            .Select(target => (coord: target, diversity: diversityMap[target.X, target.Y]))
            .ToList();

        // If no valid candidates, pawn is completely stuck
        if (candidates.Count == 0)
            return;

        // Step 3: Sort by diversity (descending) with random tiebreaker
        // This creates strong preference for diverse/interesting areas
        candidates = candidates
            .OrderByDescending(c => c.diversity)
            .ThenBy(_ => ctx.Random.Next())
            .ToList();

        // Step 4: Pick the best candidate
        var selected = candidates[0];

        // If all tiles have zero diversity, prefer closer tiles instead
        if (candidates.All(c => c.diversity == 0))
        {
            selected = candidates
                .OrderBy(c => Math.Abs(c.coord.X - pos.Coord.X) + Math.Abs(c.coord.Y - pos.Coord.Y))
                .First();
        }

        // Queue a walk action
        actionComp.ActionQueue.Enqueue(
            new ActionDef
            {
                Type = ActionType.MoveTo,
                Animation = AnimationType.Walk,
                TargetCoord = selected.coord,
                DisplayName = "Wandering",
            }
        );

        // Then queue an idle action (stand around for a bit)
        // Idle longer on more diverse tiles (but cap to avoid getting stuck)
        int baseIdleDuration = ctx.Random.Next(20, 40); // 1-2 seconds at 20 ticks/sec
        int diversityBonus = selected.diversity * 3; // +0.15 second per diversity point (0-9 scale)
        int idleDuration = Math.Min(50, baseIdleDuration + diversityBonus); // Cap at 2.5 seconds

        // Decide expression for idle time (pout or preen based on buffs/needs)
        var (exprType, exprIconDefId) = DecideExpression(ctx, pawnId);

        actionComp.ActionQueue.Enqueue(
            new ActionDef
            {
                Type = ActionType.Idle,
                Animation = AnimationType.Idle,
                DurationTicks = idleDuration,
                DisplayName = "Idle",
                Expression = exprType,
                ExpressionIconDefId = exprIconDefId,
            }
        );
    }

    private EntityId? FindBuildingForNeed(SimContext ctx, EntityId pawnId, int needId)
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return null;

        EntityId? best = null;
        float bestScore = float.MinValue;

        foreach (var objId in ctx.Entities.AllBuildings())
        {
            var objComp = ctx.Entities.Buildings[objId];
            var objDef = ctx.Content.Buildings[objComp.BuildingDefId];

            if (objDef.SatisfiesNeedId != needId)
                continue;
            if (objComp.InUse)
                continue;

            // Skip buildings that have resources but are empty
            if (ctx.Entities.Resources.TryGetValue(objId, out var resourceComp))
            {
                if (resourceComp.CurrentAmount <= 0)
                    continue;
            }

            if (!IsBuildingReachable(ctx, pawnId, objId))
                continue;

            if (!ctx.Entities.Positions.TryGetValue(objId, out var objPos))
                continue;

            int dist =
                Math.Abs(pawnPos.Coord.X - objPos.Coord.X)
                + Math.Abs(pawnPos.Coord.Y - objPos.Coord.Y);

            // Calculate preference score based on attachment
            float score = -dist; // Closer is better

            if (ctx.Entities.Attachments.TryGetValue(objId, out var attachmentComp))
            {
                // My attachment increases preference
                int myAttachment = attachmentComp.UserAttachments.GetValueOrDefault(pawnId, 0);
                score += myAttachment * 20;

                // Others' attachment decreases preference
                int highestOtherAttachment = 0;
                foreach (var (otherId, attachment) in attachmentComp.UserAttachments)
                {
                    if (otherId != pawnId && attachment > highestOtherAttachment)
                    {
                        highestOtherAttachment = attachment;
                    }
                }
                score -= highestOtherAttachment * 15;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = objId;
            }
        }

        return best;
    }

    /// <summary>
    /// Find an building that needs workers (has depleted resources and can be worked at).
    /// </summary>
    private EntityId? FindBuildingToWorkAt(SimContext ctx, EntityId pawnId)
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return null;

        EntityId? best = null;
        float bestScore = float.MinValue;

        foreach (var objId in ctx.Entities.AllBuildings())
        {
            var objComp = ctx.Entities.Buildings[objId];
            var objDef = ctx.Content.Buildings[objComp.BuildingDefId];

            // Only consider buildings that can be worked at
            if (!objDef.CanBeWorkedAt)
                continue;
            if (objComp.InUse)
                continue;

            // Only work at buildings that have resources and need replenishment
            if (!ctx.Entities.Resources.TryGetValue(objId, out var resourceComp))
                continue;

            // Calculate resource percentage
            float resourcePercent = resourceComp.CurrentAmount / resourceComp.MaxAmount;

            // Only work at buildings that are below 80% capacity
            if (resourcePercent >= 0.8f)
                continue;

            if (!IsBuildingReachable(ctx, pawnId, objId))
                continue;

            if (!ctx.Entities.Positions.TryGetValue(objId, out var objPos))
                continue;

            int dist =
                Math.Abs(pawnPos.Coord.X - objPos.Coord.X)
                + Math.Abs(pawnPos.Coord.Y - objPos.Coord.Y);

            // Calculate work preference score
            // Higher urgency (low resources) and attachment increase score
            float score = (100 - resourcePercent * 100) - (dist * 0.5f);

            if (ctx.Entities.Attachments.TryGetValue(objId, out var attachmentComp))
            {
                // My attachment to this job increases preference
                int myAttachment = attachmentComp.UserAttachments.GetValueOrDefault(pawnId, 0);
                score += myAttachment * 10;

                // Others' attachment slightly decreases preference (but urgency can override)
                int highestOtherAttachment = 0;
                foreach (var (otherId, attachment) in attachmentComp.UserAttachments)
                {
                    if (otherId != pawnId && attachment > highestOtherAttachment)
                    {
                        highestOtherAttachment = attachment;
                    }
                }
                score -= highestOtherAttachment * 5;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = objId;
            }
        }

        return best;
    }

    /// <summary>
    /// Find any building (even if in use) that could satisfy one of the given needs.
    /// Used for finding a place to wait when all relevant bu are busy.
    /// </summary>
    private EntityId? FindAnyBuildingForNeeds(SimContext ctx, EntityId pawnId, List<int> needIds)
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return null;

        EntityId? best = null;
        int bestDist = int.MaxValue;

        foreach (var objId in ctx.Entities.AllBuildings())
        {
            var objComp = ctx.Entities.Buildings[objId];
            var objDef = ctx.Content.Buildings[objComp.BuildingDefId];

            // Check if this building satisfies any of our needs
            if (!objDef.SatisfiesNeedId.HasValue || !needIds.Contains(objDef.SatisfiesNeedId.Value))
                continue;
            if (!IsBuildingReachable(ctx, pawnId, objId))
                continue;

            if (!ctx.Entities.Positions.TryGetValue(objId, out var objPos))
                continue;

            int dist =
                Math.Abs(pawnPos.Coord.X - objPos.Coord.X)
                + Math.Abs(pawnPos.Coord.Y - objPos.Coord.Y);

            if (dist < bestDist)
            {
                bestDist = dist;
                best = objId;
            }
        }

        return best;
    }

    /// <summary>
    /// Find a walkable tile near a building where a pawn can wait.
    /// </summary>
    private TileCoord? FindWaitingSpot(SimContext ctx, TileCoord buildingPos, EntityId pawnId)
    {
        // Look for walkable tiles within 2 tiles of the building
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue; // Skip the building tile itself

                var candidate = new TileCoord(buildingPos.X + dx, buildingPos.Y + dy);

                if (
                    ctx.World.IsWalkable(candidate)
                    && !ctx.Entities.IsTileOccupiedByPawn(candidate, pawnId)
                )
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private bool IsBuildingReachable(SimContext ctx, EntityId pawnId, EntityId objId)
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return false;
        if (!ctx.Entities.Positions.TryGetValue(objId, out var objPos))
            return false;
        if (!ctx.Entities.Buildings.TryGetValue(objId, out var objComp))
            return false;

        var objDef = ctx.Content.Buildings[objComp.BuildingDefId];
        var useAreas =
            objDef.UseAreas.Count > 0
                ? objDef.UseAreas
                : new List<(int dx, int dy)> { (0, 1), (0, -1), (1, 0), (-1, 0) };

        var occupiedTiles = ctx.Entities.GetOccupiedTiles(pawnId);

        foreach (var (dx, dy) in useAreas)
        {
            var target = new TileCoord(objPos.Coord.X + dx, objPos.Coord.Y + dy);
            if (!ctx.World.IsWalkable(target))
                continue;
            if (ctx.Entities.IsTileOccupiedByPawn(target, pawnId))
                continue;

            var path = Pathfinder.FindPath(ctx.World, pawnPos.Coord, target, occupiedTiles);
            if (path != null && path.Count > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Decides what expression a pawn should display during idle/wander actions
    /// based on their current buffs and needs.
    /// Returns (ExpressionType, IconDefId) or (null, null) if no expression.
    /// </summary>
    private (ExpressionType?, int?) DecideExpression(SimContext ctx, EntityId pawnId)
    {
        // Check for active buffs first (highest priority)
        if (ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs))
        {
            // Check for strongest positive buff
            var positiveBuff = buffs
                .ActiveBuffs.Where(b =>
                    ctx.Content.Buffs.TryGetValue(b.BuffDefId, out var def) && def.MoodOffset > 0
                )
                .OrderByDescending(b => ctx.Content.Buffs[b.BuffDefId].MoodOffset)
                .FirstOrDefault();

            if (positiveBuff != null)
            {
                // Happy expression with buff-related icon
                var buffDef = ctx.Content.Buffs[positiveBuff.BuffDefId];
                return (ExpressionType.Happy, buffDef.Id);
            }

            // Check for strongest negative buff
            var negativeBuff = buffs
                .ActiveBuffs.Where(b =>
                    ctx.Content.Buffs.TryGetValue(b.BuffDefId, out var def) && def.MoodOffset < 0
                )
                .OrderBy(b => ctx.Content.Buffs[b.BuffDefId].MoodOffset)
                .FirstOrDefault();

            if (negativeBuff != null)
            {
                // Complaint expression with buff-related icon
                var buffDef = ctx.Content.Buffs[negativeBuff.BuffDefId];
                return (ExpressionType.Complaint, buffDef.Id);
            }
        }

        // No active buffs - check for low needs
        if (ctx.Entities.Needs.TryGetValue(pawnId, out var needs))
        {
            // Find lowest need below LowThreshold
            int? lowestNeedId = null;
            float lowestValue = 100f;

            foreach (var (needId, value) in needs.Needs)
            {
                if (!ctx.Content.Needs.TryGetValue(needId, out var needDef))
                    continue;

                if (value < needDef.LowThreshold && value < lowestValue)
                {
                    lowestValue = value;
                    lowestNeedId = needId;
                }
            }

            if (lowestNeedId.HasValue)
            {
                // Find an building that satisfies this need
                var satisfyingBuilding = ctx.Content.Buildings.Values.FirstOrDefault(o =>
                    o.SatisfiesNeedId.HasValue && o.SatisfiesNeedId.Value == lowestNeedId.Value
                );

                if (satisfyingBuilding != null)
                {
                    return (ExpressionType.Thought, satisfyingBuilding.Id);
                }
            }
        }

        // No expression needed
        return (null, null);
    }
}

/// <summary>
/// System that manages themes and their transitions.
/// Themes can control music, pawn behavior, pathfinding, weather, and other gameplay effects.
/// Themes transition smoothly when the current theme completes.
/// Uses a priority-based selection system where themes determine their own priority.
/// </summary>
public sealed class ThemeSystem : ISystem
{
    private readonly Simulation _sim;
    private readonly List<Theme> _availableThemes;
    private readonly bool _disabled;
    private Theme? _currentTheme;
    private Theme? _queuedTheme;
    private int _currentThemeStartTick;

    public Theme? CurrentTheme => _currentTheme;
    public Theme? QueuedTheme => _queuedTheme;

    public ThemeSystem(Simulation sim, bool disabled = false)
    {
        _sim = sim ?? throw new ArgumentNullException(nameof(sim));
        _disabled = disabled;

        // Register all available themes
        _availableThemes = new List<Theme>
        {
            new DayTheme(),
            new NightTheme(),
            // Future themes can be added here
        };
    }

    public void Tick(SimContext ctx)
    {
        // Skip all theme logic if disabled
        if (_disabled)
            return;

        // Initialize with the highest priority theme if no theme is active
        if (_currentTheme == null)
        {
            var initialTheme = SelectThemeByPriority(ctx);
            StartTheme(ctx, initialTheme);
        }

        // Theme composer/DJ logic: select next theme if none queued
        if (_queuedTheme == null)
        {
            var nextTheme = SelectThemeByPriority(ctx);

            // Only queue if different from current theme
            if (nextTheme != null && nextTheme.GetType() != _currentTheme?.GetType())
            {
                _queuedTheme = nextTheme;

                // If current theme has no music, transition immediately
                // (No need to wait for a non-existent song to finish)
                if (_currentTheme?.MusicFile == null)
                {
                    TransitionToNextTheme(ctx);
                    return; // Exit early since we just transitioned
                }
            }
        }

        // Tick current theme
        _currentTheme?.OnTick(ctx);

        // Check if current theme is complete
        if (_currentTheme != null && _currentTheme.IsComplete(ctx, _currentThemeStartTick))
        {
            TransitionToNextTheme(ctx);
        }
    }

    /// <summary>
    /// Called by Godot MusicManager when a music file finishes playing.
    /// Triggers transition to the next queued theme.
    /// </summary>
    public void OnMusicFinished()
    {
        var ctx = new SimContext(_sim);
        TransitionToNextTheme(ctx);
    }

    /// <summary>
    /// Selects a theme based on priority.
    /// Loops through all available themes, finds the highest priority,
    /// and randomly selects from themes with that priority.
    /// </summary>
    private Theme SelectThemeByPriority(SimContext ctx)
    {
        // Calculate priority for each theme
        var themesWithPriority = new List<(Theme theme, int priority)>();
        foreach (var theme in _availableThemes)
        {
            int priority = theme.GetPriority(ctx);
            if (priority > 0)
            {
                themesWithPriority.Add((theme, priority));
            }
        }

        // If no themes have priority, fallback to DayTheme
        if (themesWithPriority.Count == 0)
        {
            return new DayTheme();
        }

        // Find max priority
        int maxPriority = themesWithPriority.Max(t => t.priority);

        // Get all themes with max priority
        var topThemes = themesWithPriority
            .Where(t => t.priority == maxPriority)
            .Select(t => t.theme)
            .ToList();

        // Randomly select from top themes
        var selected = topThemes[ctx.Random.Next(topThemes.Count)];
        return selected;
    }

    private void StartTheme(SimContext ctx, Theme theme)
    {
        if (_currentTheme != null)
        {
            _currentTheme.OnEnd(ctx);
        }

        _currentTheme = theme;
        _currentThemeStartTick = ctx.Time.Tick;
        theme.OnStart(ctx);
    }

    private void TransitionToNextTheme(SimContext ctx)
    {
        if (_queuedTheme != null)
        {
            StartTheme(ctx, _queuedTheme);
            _queuedTheme = null;
        }
        else
        {
            // Select highest priority theme
            var nextTheme = SelectThemeByPriority(ctx);
            StartTheme(ctx, nextTheme);
        }
    }
}
