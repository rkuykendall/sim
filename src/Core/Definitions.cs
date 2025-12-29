using System;
using System.Collections.Generic;

namespace SimGame.Core;

// Buff definition
public sealed class BuffDef : IContentDef
{
    public int Id { get; set; }
    public string Name { get; init; } = "";
    public float MoodOffset { get; init; }
    public int DurationTicks { get; init; } // 0 = permanent (recalculated each tick based on conditions)
    public bool IsFromNeed { get; init; } // True if this buff is auto-applied based on need levels
}

public sealed class BuffInstance
{
    public int BuffDefId;
    public int StartTick;
    public int EndTick; // -1 = permanent until removed
}

// Need definition
public sealed class NeedDef : IContentDef
{
    public int Id { get; set; }
    public string Name { get; init; } = "";
    public float DecayPerTick { get; init; } = 0.05f; // 10x faster default
    public float CriticalThreshold { get; init; } = 20f;
    public float LowThreshold { get; init; } = 40f;
    public int? CriticalDebuffId { get; init; } // Buff applied when below critical (set during content loading)
    public int? LowDebuffId { get; init; } // Buff applied when below low threshold (set during content loading)
}

// Object/building definition
public sealed class ObjectDef : IContentDef
{
    public int Id { get; set; }
    public string Name { get; init; } = "";
    public bool Walkable { get; init; } = false;
    public bool Interactable { get; init; } = true;
    public int? SatisfiesNeedId { get; init; } // Set during content loading
    public float NeedSatisfactionAmount { get; init; } = 30f;
    public int InteractionDurationTicks { get; init; } = 100;
    public int? GrantsBuffId { get; init; } // Buff to apply when interaction completes (set during content loading)
    public IReadOnlyList<(int dx, int dy)> UseAreas { get; init; } = Array.Empty<(int, int)>(); // Relative tile offsets where pawn can use this object
    public string SpriteKey { get; init; } = ""; // Path to sprite texture
}

// Action definition
public enum ActionType
{
    Idle,
    MoveTo,
    UseObject,
    Socialize,
}

/// <summary>
/// Animation type for visual representation of actions.
/// </summary>
public enum AnimationType
{
    Idle,
    Walk,
    Axe,
    Pickaxe,
    LookUp,
    LookDown,
}

/// <summary>
/// Expression types for pawn communication bubbles.
/// </summary>
public enum ExpressionType
{
    Thought, // Cloud bubble - wanting something
    Speech, // Speech bubble - neutral/talking
    Happy, // Heart bubble - satisfied/happy
    Complaint, // Jagged bubble - frustrated/angry
    Question, // Question bubble - confused/waiting
}

/// <summary>
/// Immutable definition of an action a pawn can perform.
/// Use object initializer syntax to create instances.
/// </summary>
public sealed class ActionDef
{
    public ActionType Type { get; init; }
    public AnimationType Animation { get; init; } = AnimationType.Idle;
    public TileCoord? TargetCoord { get; init; }
    public EntityId? TargetEntity { get; init; }
    public int DurationTicks { get; init; }
    public int? SatisfiesNeedId { get; init; }
    public float NeedSatisfactionAmount { get; init; }
    public string? DisplayName { get; init; }

    // Expression bubble data (shown while performing this action)
    public ExpressionType? Expression { get; init; }
    public int? ExpressionIconDefId { get; init; } // Object/Terrain/Buff def ID for icon
}
