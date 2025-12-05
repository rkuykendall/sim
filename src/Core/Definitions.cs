using System.Collections.Generic;

namespace SimGame.Core;

// Buff definition
public sealed class BuffDef
{
    public int Id;
    public string Name = "";
    public float MoodOffset;
    public int DurationTicks;
}

public sealed class BuffInstance
{
    public int BuffDefId;
    public int StartTick;
    public int EndTick;
}

// Need definition
public sealed class NeedDef
{
    public int Id;
    public string Name = "";
    public float DecayPerTick = 0.005f;
    public float CriticalThreshold = 20f;
    public float LowThreshold = 40f;
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

    public static readonly Dictionary<int, BuffDef> Buffs = new()
    {
        { 1, new BuffDef { Id = 1, Name = "TestBuff", MoodOffset = +15, DurationTicks = 1000 } },
        { 2, new BuffDef { Id = 2, Name = "Good Meal", MoodOffset = +10, DurationTicks = 2000 } },
        { 3, new BuffDef { Id = 3, Name = "Well Rested", MoodOffset = +15, DurationTicks = 3000 } },
    };

    public static readonly Dictionary<int, NeedDef> Needs = new()
    {
        { NeedHunger, new NeedDef { Id = NeedHunger, Name = "Hunger", DecayPerTick = 0.008f, CriticalThreshold = 15f, LowThreshold = 35f } },
        { NeedEnergy, new NeedDef { Id = NeedEnergy, Name = "Energy", DecayPerTick = 0.004f, CriticalThreshold = 10f, LowThreshold = 30f } },
        { NeedFun, new NeedDef { Id = NeedFun, Name = "Fun", DecayPerTick = 0.003f, CriticalThreshold = 20f, LowThreshold = 40f } },
        { NeedSocial, new NeedDef { Id = NeedSocial, Name = "Social", DecayPerTick = 0.002f, CriticalThreshold = 15f, LowThreshold = 35f } },
        { NeedComfort, new NeedDef { Id = NeedComfort, Name = "Comfort", DecayPerTick = 0.002f, CriticalThreshold = 20f, LowThreshold = 40f } },
        { NeedHygiene, new NeedDef { Id = NeedHygiene, Name = "Hygiene", DecayPerTick = 0.003f, CriticalThreshold = 15f, LowThreshold = 30f } },
    };

    public static readonly Dictionary<int, ObjectDef> Objects = new()
    {
        { ObjectFridge, new ObjectDef { Id = ObjectFridge, Name = "Fridge", SatisfiesNeedId = NeedHunger, NeedSatisfactionAmount = 50f, InteractionDurationTicks = 60 } },
        { ObjectBed, new ObjectDef { Id = ObjectBed, Name = "Bed", SatisfiesNeedId = NeedEnergy, NeedSatisfactionAmount = 80f, InteractionDurationTicks = 200 } },
        { ObjectTV, new ObjectDef { Id = ObjectTV, Name = "TV", SatisfiesNeedId = NeedFun, NeedSatisfactionAmount = 40f, InteractionDurationTicks = 100 } },
        { ObjectShower, new ObjectDef { Id = ObjectShower, Name = "Shower", SatisfiesNeedId = NeedHygiene, NeedSatisfactionAmount = 60f, InteractionDurationTicks = 80 } },
    };
}
