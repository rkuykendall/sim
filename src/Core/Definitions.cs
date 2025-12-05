using System.Collections.Generic;

namespace SimGame.Core;

// Buff definition
public sealed class BuffDef
{
    public int Id;
    public string Name = "";
    public float MoodOffset;
    public int DurationTicks; // 0 = permanent (recalculated each tick based on conditions)
    public bool IsFromNeed; // True if this buff is auto-applied based on need levels
}

public sealed class BuffInstance
{
    public int BuffDefId;
    public int StartTick;
    public int EndTick; // -1 = permanent until removed
}

// Need definition
public sealed class NeedDef
{
    public int Id;
    public string Name = "";
    public float DecayPerTick = 0.05f; // 10x faster default
    public float CriticalThreshold = 20f;
    public float LowThreshold = 40f;
    public int? CriticalDebuffId; // Buff applied when below critical
    public int? LowDebuffId; // Buff applied when below low threshold
}

// Object/building definition
public sealed class ObjectDef
{
    public int Id;
    public string Name = "";
    public bool Walkable = false;
    public bool Interactable = true;
    public int? SatisfiesNeedId;
    public float NeedSatisfactionAmount = 30f;
    public int InteractionDurationTicks = 100;
    public int? GrantsBuffId; // Buff to apply when interaction completes
}

// Action definition
public enum ActionType
{
    Idle,
    MoveTo,
    UseObject,
    Socialize
}

public sealed class ActionDef
{
    public ActionType Type;
    public TileCoord? TargetCoord;
    public EntityId? TargetEntity;
    public int DurationTicks;
    public int? SatisfiesNeedId;
    public float NeedSatisfactionAmount;
}

/// <summary>
/// Hardcoded content database. Will be replaced with Lua definitions.
/// </summary>
public static class ContentDatabase
{
    // Need IDs
    public const int NeedHunger = 1;
    public const int NeedEnergy = 2;
    public const int NeedFun = 3;
    public const int NeedSocial = 4;
    public const int NeedComfort = 5;
    public const int NeedHygiene = 6;

    // Object IDs
    public const int ObjectFridge = 1;
    public const int ObjectBed = 2;
    public const int ObjectTV = 3;
    public const int ObjectShower = 4;

    // Buff IDs - Need-based debuffs (auto-applied)
    public const int BuffStarving = 101;
    public const int BuffHungry = 102;
    public const int BuffExhausted = 103;
    public const int BuffTired = 104;
    public const int BuffBored = 105;
    public const int BuffUnstimulated = 106;
    public const int BuffLonely = 107;
    public const int BuffIsolated = 108;
    public const int BuffSore = 109;
    public const int BuffUncomfortable = 110;
    public const int BuffFilthy = 111;
    public const int BuffDirty = 112;

    // Buff IDs - Positive buffs (from actions)
    public const int BuffGoodMeal = 201;
    public const int BuffWellRested = 202;
    public const int BuffHadFun = 203;
    public const int BuffFeelingFresh = 204;

    public static readonly Dictionary<int, BuffDef> Buffs = new()
    {
        // Hunger debuffs
        { BuffStarving, new BuffDef { Id = BuffStarving, Name = "Starving!", MoodOffset = -30, IsFromNeed = true } },
        { BuffHungry, new BuffDef { Id = BuffHungry, Name = "Hungry", MoodOffset = -10, IsFromNeed = true } },
        
        // Energy debuffs
        { BuffExhausted, new BuffDef { Id = BuffExhausted, Name = "Exhausted!", MoodOffset = -25, IsFromNeed = true } },
        { BuffTired, new BuffDef { Id = BuffTired, Name = "Tired", MoodOffset = -8, IsFromNeed = true } },
        
        // Fun debuffs
        { BuffBored, new BuffDef { Id = BuffBored, Name = "Bored!", MoodOffset = -20, IsFromNeed = true } },
        { BuffUnstimulated, new BuffDef { Id = BuffUnstimulated, Name = "Understimulated", MoodOffset = -5, IsFromNeed = true } },
        
        // Social debuffs
        { BuffLonely, new BuffDef { Id = BuffLonely, Name = "Lonely!", MoodOffset = -25, IsFromNeed = true } },
        { BuffIsolated, new BuffDef { Id = BuffIsolated, Name = "Isolated", MoodOffset = -8, IsFromNeed = true } },
        
        // Comfort debuffs
        { BuffSore, new BuffDef { Id = BuffSore, Name = "Sore!", MoodOffset = -15, IsFromNeed = true } },
        { BuffUncomfortable, new BuffDef { Id = BuffUncomfortable, Name = "Uncomfortable", MoodOffset = -5, IsFromNeed = true } },
        
        // Hygiene debuffs
        { BuffFilthy, new BuffDef { Id = BuffFilthy, Name = "Filthy!", MoodOffset = -20, IsFromNeed = true } },
        { BuffDirty, new BuffDef { Id = BuffDirty, Name = "Dirty", MoodOffset = -8, IsFromNeed = true } },
        
        // Positive buffs from actions
        { BuffGoodMeal, new BuffDef { Id = BuffGoodMeal, Name = "Good Meal", MoodOffset = +15, DurationTicks = 2400 } }, // 2 minutes
        { BuffWellRested, new BuffDef { Id = BuffWellRested, Name = "Well Rested", MoodOffset = +20, DurationTicks = 4800 } }, // 4 minutes
        { BuffHadFun, new BuffDef { Id = BuffHadFun, Name = "Had Fun", MoodOffset = +10, DurationTicks = 1800 } }, // 1.5 minutes
        { BuffFeelingFresh, new BuffDef { Id = BuffFeelingFresh, Name = "Feeling Fresh", MoodOffset = +8, DurationTicks = 2000 } },
    };

    public static readonly Dictionary<int, NeedDef> Needs = new()
    {
        { NeedHunger, new NeedDef { Id = NeedHunger, Name = "Hunger", DecayPerTick = 0.08f, CriticalThreshold = 15f, LowThreshold = 35f, CriticalDebuffId = BuffStarving, LowDebuffId = BuffHungry } },
        { NeedEnergy, new NeedDef { Id = NeedEnergy, Name = "Energy", DecayPerTick = 0.04f, CriticalThreshold = 10f, LowThreshold = 30f, CriticalDebuffId = BuffExhausted, LowDebuffId = BuffTired } },
        { NeedFun, new NeedDef { Id = NeedFun, Name = "Fun", DecayPerTick = 0.03f, CriticalThreshold = 20f, LowThreshold = 40f, CriticalDebuffId = BuffBored, LowDebuffId = BuffUnstimulated } },
        { NeedSocial, new NeedDef { Id = NeedSocial, Name = "Social", DecayPerTick = 0.02f, CriticalThreshold = 15f, LowThreshold = 35f, CriticalDebuffId = BuffLonely, LowDebuffId = BuffIsolated } },
        { NeedComfort, new NeedDef { Id = NeedComfort, Name = "Comfort", DecayPerTick = 0.02f, CriticalThreshold = 20f, LowThreshold = 40f, CriticalDebuffId = BuffSore, LowDebuffId = BuffUncomfortable } },
        { NeedHygiene, new NeedDef { Id = NeedHygiene, Name = "Hygiene", DecayPerTick = 0.03f, CriticalThreshold = 15f, LowThreshold = 30f, CriticalDebuffId = BuffFilthy, LowDebuffId = BuffDirty } },
    };

    public static readonly Dictionary<int, ObjectDef> Objects = new()
    {
        { ObjectFridge, new ObjectDef { Id = ObjectFridge, Name = "Fridge", SatisfiesNeedId = NeedHunger, NeedSatisfactionAmount = 50f, InteractionDurationTicks = 60, GrantsBuffId = BuffGoodMeal } },
        { ObjectBed, new ObjectDef { Id = ObjectBed, Name = "Bed", SatisfiesNeedId = NeedEnergy, NeedSatisfactionAmount = 80f, InteractionDurationTicks = 200, GrantsBuffId = BuffWellRested } },
        { ObjectTV, new ObjectDef { Id = ObjectTV, Name = "TV", SatisfiesNeedId = NeedFun, NeedSatisfactionAmount = 40f, InteractionDurationTicks = 100, GrantsBuffId = BuffHadFun } },
        { ObjectShower, new ObjectDef { Id = ObjectShower, Name = "Shower", SatisfiesNeedId = NeedHygiene, NeedSatisfactionAmount = 60f, InteractionDurationTicks = 80, GrantsBuffId = BuffFeelingFresh } },
    };
}
