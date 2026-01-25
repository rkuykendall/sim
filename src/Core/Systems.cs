using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SimGame.Core;

public interface ISystem
{
    void Tick(SimContext ctx);
}

/// <summary>
/// Stores profiling results for a single system.
/// </summary>
public sealed class SystemProfile
{
    public string SystemName { get; init; } = "";
    public long TotalTicks { get; set; }
    public int CallCount { get; set; }
    public double TotalMilliseconds => TotalTicks * 1000.0 / Stopwatch.Frequency;
    public double AverageMilliseconds => CallCount > 0 ? TotalMilliseconds / CallCount : 0;
}

public sealed class SystemManager
{
    private readonly List<ISystem> _systems = new();
    private readonly Dictionary<ISystem, SystemProfile> _profiles = new();
    private readonly Stopwatch _stopwatch = new();
    private bool _profilingEnabled;

    public void Add(ISystem s)
    {
        _systems.Add(s);
        _profiles[s] = new SystemProfile { SystemName = s.GetType().Name };
    }

    /// <summary>
    /// Enable or disable profiling. When enabled, each system's Tick time is measured.
    /// </summary>
    public void SetProfilingEnabled(bool enabled) => _profilingEnabled = enabled;

    /// <summary>
    /// Reset all profiling counters.
    /// </summary>
    public void ResetProfiles()
    {
        foreach (var profile in _profiles.Values)
        {
            profile.TotalTicks = 0;
            profile.CallCount = 0;
        }
    }

    /// <summary>
    /// Get profiling results for all systems, sorted by total time descending.
    /// </summary>
    public IReadOnlyList<SystemProfile> GetProfiles()
    {
        return _profiles.Values.OrderByDescending(p => p.TotalTicks).ToList();
    }

    public void TickAll(SimContext ctx)
    {
        if (_profilingEnabled)
        {
            foreach (var s in _systems)
            {
                _stopwatch.Restart();
                s.Tick(ctx);
                _stopwatch.Stop();

                var profile = _profiles[s];
                profile.TotalTicks += _stopwatch.ElapsedTicks;
                profile.CallCount++;
            }
        }
        else
        {
            foreach (var s in _systems)
                s.Tick(ctx);
        }
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

    /// <summary>
    /// Set the current tick. Used when restoring from save data.
    /// </summary>
    internal void SetTick(int tick) => Tick = tick;
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
        buffs.ActiveBuffs.RemoveAll(b =>
            (b.Source == BuffSource.NeedCritical || b.Source == BuffSource.NeedLow)
            && b.SourceId == needDef.Id
        );

        // Apply appropriate debuff based on current value
        if (value < needDef.CriticalThreshold && needDef.CriticalDebuff != 0)
        {
            buffs.ActiveBuffs.Add(
                new BuffInstance
                {
                    Source = BuffSource.NeedCritical,
                    SourceId = needDef.Id,
                    MoodOffset = needDef.CriticalDebuff,
                    StartTick = 0,
                    EndTick = -1, // Permanent until need recovers
                }
            );
        }
        else if (value < needDef.LowThreshold && needDef.LowDebuff != 0)
        {
            buffs.ActiveBuffs.Add(
                new BuffInstance
                {
                    Source = BuffSource.NeedLow,
                    SourceId = needDef.Id,
                    MoodOffset = needDef.LowDebuff,
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
                mood += inst.MoodOffset;
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
                case ActionType.PickUp:
                    ExecutePickUp(ctx, pawnId, actionComp);
                    break;
                case ActionType.DropOff:
                    ExecuteDropOff(ctx, pawnId, actionComp);
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

        if (actionComp.CurrentPath == null)
        {
            actionComp.CurrentPath = Pathfinder.FindPath(ctx.World, pos.Coord, target);
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

            // Economic transaction: pawn pays cost to building
            if (hasResources && buildingComp != null)
            {
                var buildingDefForCost = ctx.Content.Buildings[buildingComp.BuildingDefId];
                int cost = buildingDefForCost.GetCost();

                // Check if pawn can afford it
                if (ctx.Entities.Gold.TryGetValue(pawnId, out var pawnGold))
                {
                    if (pawnGold.Amount < cost)
                    {
                        // Can't afford - cancel the benefit
                        hasResources = false;
                    }
                    else
                    {
                        pawnGold.Amount -= cost;
                        if (ctx.Entities.Gold.TryGetValue(targetId, out var buildingGold))
                        {
                            buildingGold.Amount += cost;
                        }
                    }
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
                    buildingDef2.GrantsBuff != 0
                    && ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs)
                )
                {
                    // Remove existing instance of this buff (refresh it)
                    buffs.ActiveBuffs.RemoveAll(b =>
                        b.Source == BuffSource.Building && b.SourceId == buildingDef2.Id
                    );

                    buffs.ActiveBuffs.Add(
                        new BuffInstance
                        {
                            Source = BuffSource.Building,
                            SourceId = buildingDef2.Id,
                            MoodOffset = buildingDef2.GrantsBuff,
                            StartTick = ctx.Time.Tick,
                            EndTick = ctx.Time.Tick + buildingDef2.BuffDuration,
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
                        ExpressionIconDefId = action.SatisfiesNeedId.Value, // Show the need icon
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

            // Economic transaction: pawn pays buy-in, receives payout from building stores
            if (buildingComp2 != null)
            {
                var buildingDefForPay = ctx.Content.Buildings[buildingComp2.BuildingDefId];
                int buyIn = buildingDefForPay.GetWorkBuyIn();
                int payout = buildingDefForPay.GetPayout();

                if (ctx.Entities.Gold.TryGetValue(pawnId, out var pawnGold))
                {
                    // Deduct buy-in from pawn and give to building
                    pawnGold.Amount -= buyIn;
                    if (ctx.Entities.Gold.TryGetValue(targetId, out var buildingGoldForBuyIn))
                    {
                        buildingGoldForBuyIn.Amount += buyIn;
                    }

                    if (buildingDefForPay.IsGoldSource)
                    {
                        // Gold source buildings create money from nothing
                        pawnGold.Amount += payout;
                    }
                    else if (ctx.Entities.Gold.TryGetValue(targetId, out var buildingGold))
                    {
                        // Pay from building's gold stores (limited to what's available)
                        int actualPayout = Math.Min(payout, buildingGold.Amount);
                        buildingGold.Amount -= actualPayout;
                        pawnGold.Amount += actualPayout;
                    }
                }
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

            // Grant "Productive" buff (hardcoded work satisfaction)
            if (buildingComp2 != null && ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs))
            {
                var buildingDef3 = ctx.Content.Buildings[buildingComp2.BuildingDefId];

                // Remove existing work buff for this building (refresh it)
                buffs.ActiveBuffs.RemoveAll(b =>
                    b.Source == BuffSource.Work && b.SourceId == buildingDef3.Id
                );

                buffs.ActiveBuffs.Add(
                    new BuffInstance
                    {
                        Source = BuffSource.Work,
                        SourceId = buildingDef3.Id,
                        MoodOffset = 15f, // Productive feeling from working
                        StartTick = ctx.Time.Tick,
                        EndTick = ctx.Time.Tick + 2400, // Duration: 2400 ticks
                    }
                );

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

            // Add a brief happy idle action showing satisfaction
            if (buildingComp2 != null && action.SatisfiesNeedId.HasValue)
            {
                actionComp.ActionQueue.Enqueue(
                    new ActionDef
                    {
                        Type = ActionType.Idle,
                        Animation = AnimationType.Idle,
                        DurationTicks = 10, // Brief moment
                        DisplayName = "Feeling Productive",
                        Expression = ExpressionType.Happy,
                        ExpressionIconDefId = action.SatisfiesNeedId.Value, // Show the need icon
                    }
                );
            }

            actionComp.CurrentAction = null;
        }
    }

    private void ExecutePickUp(SimContext ctx, EntityId pawnId, ActionComponent actionComp)
    {
        var action = actionComp.CurrentAction!;

        // Get inventory
        if (!ctx.Entities.Inventory.TryGetValue(pawnId, out var inventory))
            return;

        // Picking up from a building
        if (action.TargetEntity.HasValue)
        {
            var sourceId = action.TargetEntity.Value;

            if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
                return;
            if (!ctx.Entities.Positions.TryGetValue(sourceId, out var objPos))
                return;
            if (!ctx.Entities.Buildings.TryGetValue(sourceId, out var buildingComp))
                return;

            var buildingDef = ctx.Content.Buildings[buildingComp.BuildingDefId];

            // Check if pawn is in a valid use area for this building
            bool inUseArea = IsInUseArea(pawnPos.Coord, objPos.Coord, buildingDef);

            if (!inUseArea)
            {
                // Need to move to a valid use area first
                var useAreaTarget = FindValidUseArea(
                    ctx.World,
                    ctx.Entities,
                    objPos.Coord,
                    pawnPos.Coord,
                    buildingDef,
                    pawnId
                );

                if (useAreaTarget == null)
                {
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
                    DisplayName = $"Going to pick up {action.ResourceType}",
                };
                actionComp.ActionStartTick = ctx.Time.Tick;
                return;
            }

            int elapsed = ctx.Time.Tick - actionComp.ActionStartTick;
            if (elapsed >= action.DurationTicks)
            {
                // Transfer resources from building to pawn inventory
                if (ctx.Entities.Resources.TryGetValue(sourceId, out var sourceResource))
                {
                    float transferAmount = Math.Min(
                        action.ResourceAmount > 0 ? action.ResourceAmount : 30f,
                        sourceResource.CurrentAmount
                    );

                    if (transferAmount > 0)
                    {
                        sourceResource.CurrentAmount -= transferAmount;
                        inventory.ResourceType = sourceResource.ResourceType;
                        inventory.Amount = transferAmount;
                    }
                }

                actionComp.CurrentAction = null;
            }
        }
        // Picking up from terrain (harvesting)
        else if (action.TerrainTargetCoord.HasValue)
        {
            if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
                return;

            var targetCoord = action.TerrainTargetCoord.Value;

            // Check if we need to move adjacent to the terrain tile
            int dist =
                Math.Abs(pawnPos.Coord.X - targetCoord.X)
                + Math.Abs(pawnPos.Coord.Y - targetCoord.Y);

            if (dist > 1)
            {
                // Find an adjacent walkable tile to harvest from
                var adjacentTile = FindAdjacentWalkable(ctx.World, targetCoord, pawnPos.Coord);

                if (adjacentTile == null)
                {
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
                    TargetCoord = adjacentTile,
                    DurationTicks = 0,
                    DisplayName = $"Going to harvest",
                };
                actionComp.ActionStartTick = ctx.Time.Tick;
                return;
            }

            int elapsed = ctx.Time.Tick - actionComp.ActionStartTick;
            if (elapsed >= action.DurationTicks)
            {
                // Terrain harvesting is infinite - just give resources
                inventory.ResourceType = action.ResourceType;
                inventory.Amount = action.ResourceAmount > 0 ? action.ResourceAmount : 30f;
                actionComp.CurrentAction = null;
            }
        }
        else
        {
            // No valid target
            actionComp.CurrentAction = null;
        }
    }

    private void ExecuteDropOff(SimContext ctx, EntityId pawnId, ActionComponent actionComp)
    {
        var action = actionComp.CurrentAction!;

        if (!action.TargetEntity.HasValue)
        {
            actionComp.CurrentAction = null;
            return;
        }

        var targetId = action.TargetEntity.Value;

        if (!ctx.Entities.Inventory.TryGetValue(pawnId, out var inventory))
            return;
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return;
        if (!ctx.Entities.Positions.TryGetValue(targetId, out var objPos))
            return;
        if (!ctx.Entities.Buildings.TryGetValue(targetId, out var buildingComp))
            return;

        var buildingDef = ctx.Content.Buildings[buildingComp.BuildingDefId];

        // Check if pawn is in a valid use area for this building
        bool inUseArea = IsInUseArea(pawnPos.Coord, objPos.Coord, buildingDef);

        if (!inUseArea)
        {
            // Need to move to a valid use area first
            var useAreaTarget = FindValidUseArea(
                ctx.World,
                ctx.Entities,
                objPos.Coord,
                pawnPos.Coord,
                buildingDef,
                pawnId
            );

            if (useAreaTarget == null)
            {
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
                DisplayName = $"Going to deliver {inventory.ResourceType}",
            };
            actionComp.ActionStartTick = ctx.Time.Tick;
            return;
        }

        int elapsed = ctx.Time.Tick - actionComp.ActionStartTick;
        if (elapsed >= action.DurationTicks)
        {
            // Transfer resources from inventory to building
            float transferAmount = 0f;
            if (
                ctx.Entities.Resources.TryGetValue(targetId, out var destResource)
                && inventory.ResourceType == destResource.ResourceType
                && inventory.Amount > 0
            )
            {
                transferAmount = Math.Min(
                    inventory.Amount,
                    destResource.MaxAmount - destResource.CurrentAmount
                );
                destResource.CurrentAmount += transferAmount;
                inventory.Amount -= transferAmount;
                if (inventory.Amount <= 0)
                {
                    inventory.ResourceType = null;
                    inventory.Amount = 0;
                }
            }

            // Building-to-building wholesale payment: destination pays source for goods
            // Rate: 1g per 15 units (wholesale discount)
            if (
                action.SourceEntity.HasValue
                && transferAmount > 0
                && ctx.Entities.Gold.TryGetValue(targetId, out var destGold)
                && ctx.Entities.Gold.TryGetValue(action.SourceEntity.Value, out var sourceGold)
            )
            {
                int wholesalePayment = (int)(transferAmount / 15.0f); // 1g per 15 units
                int actualPayment = Math.Min(wholesalePayment, destGold.Amount);
                destGold.Amount -= actualPayment;
                sourceGold.Amount += actualPayment;
            }

            // Economic transaction: pawn pays buy-in, receives payout from building stores
            int buyIn = buildingDef.GetWorkBuyIn();
            int payout = buildingDef.GetPayout();

            if (ctx.Entities.Gold.TryGetValue(pawnId, out var pawnGold))
            {
                pawnGold.Amount -= buyIn;

                if (ctx.Entities.Gold.TryGetValue(targetId, out var buildingGold))
                {
                    // Buy-in goes to building
                    buildingGold.Amount += buyIn;

                    int actualPayout = Math.Min(payout, buildingGold.Amount);
                    buildingGold.Amount -= actualPayout;
                    pawnGold.Amount += actualPayout;
                }
            }

            // Satisfy the Purpose need if specified
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
            if (ctx.Entities.Buffs.TryGetValue(pawnId, out var buffs))
            {
                buffs.ActiveBuffs.RemoveAll(b =>
                    b.Source == BuffSource.Work && b.SourceId == buildingDef.Id
                );

                buffs.ActiveBuffs.Add(
                    new BuffInstance
                    {
                        Source = BuffSource.Work,
                        SourceId = buildingDef.Id,
                        MoodOffset = 15f,
                        StartTick = ctx.Time.Tick,
                        EndTick = ctx.Time.Tick + 2400,
                    }
                );
            }

            // Increment attachment
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

            // Add a brief happy idle action
            if (action.SatisfiesNeedId.HasValue)
            {
                actionComp.ActionQueue.Enqueue(
                    new ActionDef
                    {
                        Type = ActionType.Idle,
                        Animation = AnimationType.Idle,
                        DurationTicks = 10,
                        DisplayName = "Feeling Productive",
                        Expression = ExpressionType.Happy,
                        ExpressionIconDefId = action.SatisfiesNeedId.Value,
                    }
                );
            }

            actionComp.CurrentAction = null;
        }
    }

    /// <summary>
    /// Find an adjacent walkable tile to a given coordinate.
    /// </summary>
    private TileCoord? FindAdjacentWalkable(World world, TileCoord target, TileCoord from)
    {
        var directions = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) };
        TileCoord? best = null;
        int bestDist = int.MaxValue;

        foreach (var (dx, dy) in directions)
        {
            var adj = new TileCoord(target.X + dx, target.Y + dy);
            if (!world.IsInBounds(adj))
                continue;
            if (!world.IsWalkable(adj))
                continue;

            int dist = Math.Abs(adj.X - from.X) + Math.Abs(adj.Y - from.Y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = adj;
            }
        }

        return best;
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
    /// Find the closest valid use area for a building that is walkable and reachable.
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

            // Check if tile is walkable and reachable
            if (world.GetTile(useAreaCoord).Walkable)
            {
                // Verify that pathfinding succeeds - tile must be reachable
                var path = Pathfinder.FindPath(world, from, useAreaCoord);
                if (path != null && path.Count > 0)
                {
                    int dist =
                        Math.Abs(useAreaCoord.X - from.X) + Math.Abs(useAreaCoord.Y - from.Y);
                    candidates.Add((useAreaCoord, dist));
                }
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
        // Needs are already filtered (< 90) and sorted by value (lowest first)
        EntityId? targetBuilding = null;
        bool isWorkAction = false;
        foreach (var (needId, _) in urgentNeeds)
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

        if (targetBuilding != null)
        {
            if (isWorkAction)
            {
                QueueWorkAtBuilding(
                    ctx,
                    pawnId,
                    actionComp,
                    targetBuilding.Value,
                    purposeNeedId!.Value
                );
            }
            else
            {
                QueueUseBuilding(ctx, actionComp, targetBuilding.Value);
            }
        }
        else
        {
            // No available buildings - wander (will show thought bubble for unmet needs)
            WanderRandomly(ctx, pawnId, actionComp);
        }
    }

    private int CountPawnsTargeting(SimContext ctx, EntityId buildingId, EntityId excludePawn)
    {
        int count = 0;
        foreach (var otherId in ctx.Entities.AllPawns())
        {
            if (otherId == excludePawn)
                continue;
            if (!ctx.Entities.Actions.TryGetValue(otherId, out var otherAction))
                continue;

            if (otherAction.CurrentAction?.TargetEntity == buildingId)
                count++;
            foreach (var qa in otherAction.ActionQueue)
            {
                if (qa.TargetEntity == buildingId)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Calculate which needs require attention, sorted by value (lowest first = most urgent).
    /// Only returns needs below the threshold.
    /// </summary>
    private List<(int needId, float value)> CalculateUrgentNeeds(
        SimContext ctx,
        EntityId pawnId,
        NeedsComponent needs
    )
    {
        const float NeedThreshold = 90f;

        var urgentNeeds = new List<(int needId, float value)>();

        foreach (var (needId, value) in needs.Needs)
        {
            if (value < NeedThreshold)
            {
                urgentNeeds.Add((needId, value));
            }
        }

        // Sort by value ascending - lowest need first
        urgentNeeds.Sort((a, b) => a.value.CompareTo(b.value));
        return urgentNeeds;
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
    /// Routes to different work types based on building configuration.
    /// </summary>
    private void QueueWorkAtBuilding(
        SimContext ctx,
        EntityId pawnId,
        ActionComponent actionComp,
        EntityId targetBuilding,
        int purposeNeedId
    )
    {
        var buildingComp = ctx.Entities.Buildings[targetBuilding];
        var buildingDef = ctx.Content.Buildings[buildingComp.BuildingDefId];

        switch (buildingDef.WorkType)
        {
            case BuildingWorkType.Direct:
                QueueDirectWork(ctx, actionComp, targetBuilding, buildingDef, purposeNeedId);
                break;
            case BuildingWorkType.HaulFromBuilding:
                QueueHaulFromBuildingWork(
                    ctx,
                    pawnId,
                    actionComp,
                    targetBuilding,
                    buildingDef,
                    purposeNeedId
                );
                break;
            case BuildingWorkType.HaulFromTerrain:
                QueueHaulFromTerrainWork(
                    ctx,
                    actionComp,
                    targetBuilding,
                    buildingDef,
                    purposeNeedId
                );
                break;
        }
    }

    /// <summary>
    /// Queue direct work at a building (original behavior - work creates resources).
    /// </summary>
    private void QueueDirectWork(
        SimContext ctx,
        ActionComponent actionComp,
        EntityId targetBuilding,
        BuildingDef buildingDef,
        int purposeNeedId
    )
    {
        actionComp.ActionQueue.Enqueue(
            new ActionDef
            {
                Type = ActionType.Work,
                Animation = AnimationType.Pickaxe,
                TargetEntity = targetBuilding,
                DurationTicks = 2500,
                SatisfiesNeedId = purposeNeedId,
                NeedSatisfactionAmount = 40f,
                DisplayName = $"Going to work at {buildingDef.Name}",
            }
        );
    }

    /// <summary>
    /// Queue hauling work from another building to the destination.
    /// </summary>
    private void QueueHaulFromBuildingWork(
        SimContext ctx,
        EntityId pawnId,
        ActionComponent actionComp,
        EntityId destinationId,
        BuildingDef destDef,
        int purposeNeedId
    )
    {
        // Find source building with the required resource
        var sourceBuilding = FindSourceBuilding(
            ctx,
            pawnId,
            destDef.HaulSourceResourceType,
            destinationId
        );
        if (sourceBuilding == null)
        {
            // No source available - fall back to wandering
            return;
        }

        var sourceBuildingComp = ctx.Entities.Buildings[sourceBuilding.Value];
        var sourceDef = ctx.Content.Buildings[sourceBuildingComp.BuildingDefId];

        // Queue: PickUp from source -> DropOff at destination
        actionComp.ActionQueue.Enqueue(
            new ActionDef
            {
                Type = ActionType.PickUp,
                Animation = AnimationType.Idle, // Pawn is hidden during pickup
                TargetEntity = sourceBuilding,
                DurationTicks = 100, // Quick loading time
                ResourceType = destDef.HaulSourceResourceType,
                ResourceAmount = 30f,
                DisplayName = $"Loading {destDef.HaulSourceResourceType} from {sourceDef.Name}",
            }
        );

        actionComp.ActionQueue.Enqueue(
            new ActionDef
            {
                Type = ActionType.DropOff,
                Animation = AnimationType.Idle, // Pawn is hidden during dropoff
                TargetEntity = destinationId,
                SourceEntity = sourceBuilding, // For wholesale payment to source
                DurationTicks = 100, // Quick unloading time
                ResourceType = destDef.HaulSourceResourceType,
                ResourceAmount = 30f,
                SatisfiesNeedId = purposeNeedId,
                NeedSatisfactionAmount = 40f,
                DisplayName = $"Delivering to {destDef.Name}",
            }
        );
    }

    /// <summary>
    /// Queue hauling work from terrain to the destination building.
    /// </summary>
    private void QueueHaulFromTerrainWork(
        SimContext ctx,
        ActionComponent actionComp,
        EntityId destinationId,
        BuildingDef destDef,
        int purposeNeedId
    )
    {
        // Find nearest terrain tile of the correct type
        var terrainTile = FindNearestTerrain(ctx, destinationId, destDef.HaulSourceTerrainKey);
        if (terrainTile == null)
        {
            // No terrain available - fall back to wandering
            return;
        }

        // Queue: PickUp from terrain -> DropOff at destination
        actionComp.ActionQueue.Enqueue(
            new ActionDef
            {
                Type = ActionType.PickUp,
                Animation = AnimationType.Axe, // Chopping animation for trees
                TerrainTargetCoord = terrainTile,
                DurationTicks = 1500, // Longer harvest time
                ResourceType = destDef.ResourceType, // Output resource type (e.g., "lumber")
                ResourceAmount = 30f,
                DisplayName = $"Harvesting {destDef.HaulSourceTerrainKey}",
            }
        );

        actionComp.ActionQueue.Enqueue(
            new ActionDef
            {
                Type = ActionType.DropOff,
                Animation = AnimationType.Idle, // Pawn is hidden during dropoff
                TargetEntity = destinationId,
                DurationTicks = 100, // Quick unloading time
                ResourceType = destDef.ResourceType,
                ResourceAmount = 30f,
                SatisfiesNeedId = purposeNeedId,
                NeedSatisfactionAmount = 40f,
                DisplayName = $"Delivering to {destDef.Name}",
            }
        );
    }

    /// <summary>
    /// Find a source building with the required resource type.
    /// </summary>
    private EntityId? FindSourceBuilding(
        SimContext ctx,
        EntityId pawnId,
        string? resourceType,
        EntityId excludeId
    )
    {
        if (resourceType == null)
            return null;

        return FindBestReachableBuilding(
            ctx,
            pawnId,
            filter: b =>
                b.ObjId != excludeId
                && b.ResourceComp != null
                && b.ResourceComp.ResourceType == resourceType
                && b.ResourceComp.CurrentAmount >= 10, // Minimum threshold
            scorer: (b, _) => b.ResourceComp!.CurrentAmount
        );
    }

    /// <summary>
    /// Find the nearest terrain tile of the specified type near a building.
    /// </summary>
    private TileCoord? FindNearestTerrain(
        SimContext ctx,
        EntityId nearBuildingId,
        string? terrainKey
    )
    {
        if (terrainKey == null)
            return null;

        // Get terrain def ID from key
        var terrainDefId = ctx.Content.GetTerrainId(terrainKey);
        if (!terrainDefId.HasValue)
            return null;

        if (!ctx.Entities.Positions.TryGetValue(nearBuildingId, out var buildingPos))
            return null;

        var center = buildingPos.Coord;
        TileCoord? best = null;
        int bestDist = int.MaxValue;

        // Search in expanding radius
        int searchRadius = 20;
        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                var coord = new TileCoord(center.X + dx, center.Y + dy);
                if (!ctx.World.IsInBounds(coord))
                    continue;

                var tile = ctx.World.GetTile(coord);

                // Check base or overlay terrain
                bool matches =
                    tile.BaseTerrainTypeId == terrainDefId.Value
                    || tile.OverlayTerrainTypeId == terrainDefId.Value;

                if (!matches)
                    continue;

                // Must have an adjacent walkable tile
                if (!HasAdjacentWalkable(ctx.World, coord))
                    continue;

                int dist = Math.Abs(dx) + Math.Abs(dy);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = coord;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Check if a tile has at least one adjacent walkable tile.
    /// </summary>
    private bool HasAdjacentWalkable(World world, TileCoord coord)
    {
        var directions = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) };
        foreach (var (dx, dy) in directions)
        {
            var adj = new TileCoord(coord.X + dx, coord.Y + dy);
            if (world.IsInBounds(adj) && world.IsWalkable(adj))
                return true;
        }
        return false;
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

        // Step 2: Filter to valid candidates (walkable, not current position)
        var candidates = potentialTargets
            .Where(target => !target.Equals(pos.Coord))
            .Where(target => ctx.World.IsWalkable(target))
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

    /// <summary>
    /// Context passed to building filter/scorer functions.
    /// </summary>
    private readonly struct BuildingSearchContext
    {
        public EntityId ObjId { get; init; }
        public BuildingComponent ObjComp { get; init; }
        public BuildingDef ObjDef { get; init; }
        public TileCoord ObjPos { get; init; }
        public int Distance { get; init; }
        public ResourceComponent? ResourceComp { get; init; }
        public AttachmentComponent? AttachmentComp { get; init; }
        public int OtherPawnsTargeting { get; init; }
    }

    /// <summary>
    /// Count how many other pawns are currently targeting a building
    /// (have it as TargetEntity in their current action or queue).
    /// </summary>
    private static int CountPawnsTargetingBuilding(
        EntityManager entities,
        EntityId buildingId,
        EntityId excludePawnId
    )
    {
        int count = 0;
        foreach (var pawnId in entities.AllPawns())
        {
            if (pawnId == excludePawnId)
                continue;

            if (!entities.Actions.TryGetValue(pawnId, out var actionComp))
                continue;

            // Check current action
            if (actionComp.CurrentAction?.TargetEntity == buildingId)
            {
                count++;
                continue;
            }

            // Check queued actions
            foreach (var queued in actionComp.ActionQueue)
            {
                if (queued.TargetEntity == buildingId)
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Generic building search: filters candidates, scores them, returns first reachable by score.
    /// </summary>
    private EntityId? FindBestReachableBuilding(
        SimContext ctx,
        EntityId pawnId,
        Func<BuildingSearchContext, bool> filter,
        Func<BuildingSearchContext, EntityId, float> scorer
    )
    {
        if (!ctx.Entities.Positions.TryGetValue(pawnId, out var pawnPos))
            return null;

        var candidates = new List<(EntityId id, float score)>();

        foreach (var objId in ctx.Entities.AllBuildings())
        {
            var objComp = ctx.Entities.Buildings[objId];
            var objDef = ctx.Content.Buildings[objComp.BuildingDefId];

            if (!ctx.Entities.Positions.TryGetValue(objId, out var objPos))
                continue;

            int dist =
                Math.Abs(pawnPos.Coord.X - objPos.Coord.X)
                + Math.Abs(pawnPos.Coord.Y - objPos.Coord.Y);

            ctx.Entities.Resources.TryGetValue(objId, out var resourceComp);
            ctx.Entities.Attachments.TryGetValue(objId, out var attachmentComp);
            int otherPawnsTargeting = CountPawnsTargetingBuilding(ctx.Entities, objId, pawnId);

            var searchCtx = new BuildingSearchContext
            {
                ObjId = objId,
                ObjComp = objComp,
                ObjDef = objDef,
                ObjPos = objPos.Coord,
                Distance = dist,
                ResourceComp = resourceComp,
                AttachmentComp = attachmentComp,
                OtherPawnsTargeting = otherPawnsTargeting,
            };

            // Apply universal filters first: in-use state and pawn targeting limits
            if (objComp.InUse(ctx.Entities, objId))
                continue;
            if (otherPawnsTargeting >= 2)
                continue;

            // Then apply custom filter
            if (!filter(searchCtx))
                continue;

            // Calculate base score (common across all searches)
            float baseScore = -(searchCtx.Distance * 0.5f) - (otherPawnsTargeting * 10);

            // Add custom scoring on top
            float customScore = scorer(searchCtx, pawnId);
            float finalScore = baseScore + customScore;

            candidates.Add((objId, finalScore));
        }

        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        foreach (var (objId, _) in candidates)
        {
            if (IsBuildingReachable(ctx, pawnId, objId))
                return objId;
        }

        return null;
    }

    /// <summary>
    /// Calculate attachment score modifier for a pawn at a building.
    /// </summary>
    private static float GetAttachmentScore(
        AttachmentComponent? attachmentComp,
        EntityId pawnId,
        float myWeight,
        float otherWeight
    )
    {
        if (attachmentComp == null)
            return 0;

        float score = 0;
        int myAttachment = attachmentComp.UserAttachments.GetValueOrDefault(pawnId, 0);
        score += myAttachment * myWeight;

        int highestOtherAttachment = 0;
        foreach (var (otherId, attachment) in attachmentComp.UserAttachments)
        {
            if (otherId != pawnId && attachment > highestOtherAttachment)
                highestOtherAttachment = attachment;
        }
        score -= highestOtherAttachment * otherWeight;

        return score;
    }

    private EntityId? FindBuildingForNeed(SimContext ctx, EntityId pawnId, int needId)
    {
        int pawnGold = ctx.Entities.Gold.TryGetValue(pawnId, out var goldComp)
            ? goldComp.Amount
            : 0;

        return FindBestReachableBuilding(
            ctx,
            pawnId,
            filter: b =>
                b.ObjDef.SatisfiesNeedId == needId
                && b.ObjDef.CanSellToConsumers
                && pawnGold >= b.ObjDef.GetCost()
                && (b.ResourceComp == null || b.ResourceComp.CurrentAmount > 0),
            scorer: (b, pid) =>
                GetAttachmentScore(b.AttachmentComp, pid, myWeight: 20, otherWeight: 15)
        );
    }

    private EntityId? FindBuildingToWorkAt(SimContext ctx, EntityId pawnId)
    {
        int pawnGold = ctx.Entities.Gold.TryGetValue(pawnId, out var goldComp)
            ? goldComp.Amount
            : 0;

        return FindBestReachableBuilding(
            ctx,
            pawnId,
            filter: b =>
                b.ObjDef.CanBeWorkedAt
                && pawnGold >= b.ObjDef.GetWorkBuyIn()
                && b.ResourceComp != null
                && (b.ResourceComp.CurrentAmount / b.ResourceComp.MaxAmount) < 0.8f,
            scorer: (b, pid) =>
            {
                float resourcePercent = b.ResourceComp!.CurrentAmount / b.ResourceComp.MaxAmount;
                float urgency = 100 - resourcePercent * 100;
                return urgency
                    + GetAttachmentScore(b.AttachmentComp, pid, myWeight: 10, otherWeight: 5);
            }
        );
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

        foreach (var (dx, dy) in useAreas)
        {
            var target = new TileCoord(objPos.Coord.X + dx, objPos.Coord.Y + dy);
            if (!ctx.World.IsWalkable(target))
                continue;

            var path = Pathfinder.FindPath(ctx.World, pawnPos.Coord, target);
            if (path != null && path.Count > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get the icon ID for a buff based on its source.
    /// </summary>
    private int? GetBuffIconId(SimContext ctx, BuffInstance buff)
    {
        switch (buff.Source)
        {
            case BuffSource.Building:
            case BuffSource.Work:
                // Show the need icon that this building satisfies
                if (ctx.Content.Buildings.TryGetValue(buff.SourceId, out var buildingDef))
                {
                    return buildingDef.SatisfiesNeedId; // Return need ID
                }
                return null;

            case BuffSource.NeedCritical:
            case BuffSource.NeedLow:
                // Show the need icon directly
                return buff.SourceId; // Already a need ID

            default:
                return null;
        }
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
                .ActiveBuffs.Where(b => b.MoodOffset > 0)
                .OrderByDescending(b => b.MoodOffset)
                .FirstOrDefault();

            if (positiveBuff != null)
            {
                // Happy expression with buff-related icon
                int? iconId = GetBuffIconId(ctx, positiveBuff);
                if (iconId.HasValue)
                    return (ExpressionType.Happy, iconId.Value);
            }

            // Check for strongest negative buff
            var negativeBuff = buffs
                .ActiveBuffs.Where(b => b.MoodOffset < 0)
                .OrderBy(b => b.MoodOffset)
                .FirstOrDefault();

            if (negativeBuff != null)
            {
                // Complaint expression with buff-related icon
                int? iconId = GetBuffIconId(ctx, negativeBuff);
                if (iconId.HasValue)
                    return (ExpressionType.Complaint, iconId.Value);
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
                // Show thought bubble with the need icon
                return (ExpressionType.Thought, lowestNeedId.Value);
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
                Console.WriteLine(
                    $"[ThemeSystem] Queuing new theme: {nextTheme.Name} (different from current: {_currentTheme?.Name})"
                );
                _queuedTheme = nextTheme;

                // If current theme has no music, transition immediately
                // (No need to wait for a non-existent song to finish)
                if (_currentTheme?.MusicFile == null)
                {
                    Console.WriteLine(
                        "[ThemeSystem] Current theme has no music, transitioning immediately"
                    );
                    TransitionToNextTheme(ctx);
                    return; // Exit early since we just transitioned
                }
                else
                {
                    Console.WriteLine(
                        $"[ThemeSystem] Queued theme will play after current music finishes: {_currentTheme.MusicFile}"
                    );
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
        Console.WriteLine(
            $"[ThemeSystem] OnMusicFinished called. Current theme: {_currentTheme?.Name ?? "null"}, Queued theme: {_queuedTheme?.Name ?? "null"}"
        );
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
        Console.WriteLine(
            $"[ThemeSystem] StartTheme: {theme.Name}, Music: {theme.MusicFile ?? "null"}"
        );

        if (_currentTheme != null)
        {
            Console.WriteLine($"[ThemeSystem] Ending previous theme: {_currentTheme.Name}");
            _currentTheme.OnEnd(ctx);
        }

        _currentTheme = theme;
        _currentThemeStartTick = ctx.Time.Tick;
        theme.OnStart(ctx);

        Console.WriteLine($"[ThemeSystem] Theme started successfully at tick {ctx.Time.Tick}");
    }

    private void TransitionToNextTheme(SimContext ctx)
    {
        Console.WriteLine($"[ThemeSystem] TransitionToNextTheme called");

        if (_queuedTheme != null)
        {
            Console.WriteLine($"[ThemeSystem] Starting queued theme: {_queuedTheme.Name}");
            StartTheme(ctx, _queuedTheme);
            _queuedTheme = null;
        }
        else
        {
            Console.WriteLine("[ThemeSystem] No queued theme, selecting by priority");
            // Select highest priority theme
            var nextTheme = SelectThemeByPriority(ctx);
            Console.WriteLine($"[ThemeSystem] Selected theme by priority: {nextTheme.Name}");
            StartTheme(ctx, nextTheme);
        }
    }
}
