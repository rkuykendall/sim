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
    public List<(int dx, int dy)> UseAreas = new(); // Relative tile offsets where pawn can use this object
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
/// Content database populated from Lua files.
/// Use ContentLoader.LoadAll() to populate before starting simulation.
/// </summary>
public static class ContentDatabase
{
    // These are populated by ContentLoader from Lua files
    public static readonly Dictionary<int, BuffDef> Buffs = new();
    public static readonly Dictionary<int, NeedDef> Needs = new();
    public static readonly Dictionary<int, ObjectDef> Objects = new();

    // Convenience accessors for commonly-used IDs (looked up after Lua load)
    public static int NeedHunger => ContentLoader.GetNeedId("Hunger") ?? 1;
    public static int NeedEnergy => ContentLoader.GetNeedId("Energy") ?? 2;
    public static int NeedFun => ContentLoader.GetNeedId("Fun") ?? 3;
    public static int NeedSocial => ContentLoader.GetNeedId("Social") ?? 4;
    public static int NeedHygiene => ContentLoader.GetNeedId("Hygiene") ?? 6;

    public static int ObjectFridge => 1;
    public static int ObjectBed => 2;
    public static int ObjectTV => 3;
    public static int ObjectShower => 4;
}
