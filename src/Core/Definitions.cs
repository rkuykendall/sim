using System;
using System.Collections.Generic;

namespace SimGame.Core;

// Buff source types
public enum BuffSource
{
    NeedCritical, // Applied when need below critical threshold
    NeedLow, // Applied when need below low threshold
    Building, // Applied by building use
    Work, // Applied by working at a building
}

// Building work types for the hauling system
public enum BuildingWorkType
{
    Direct, // Work creates resources at this building (default behavior)
    HaulFromBuilding, // Work = haul resources from another building
    HaulFromTerrain, // Work = harvest resources from terrain tiles
}

public sealed class BuffInstance
{
    public BuffSource Source;
    public int SourceId; // NeedDef.Id or BuildingDef.Id depending on Source
    public float MoodOffset;
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
    public float CriticalDebuff { get; init; } // Mood penalty when below critical threshold (e.g., -25)
    public float LowDebuff { get; init; } // Mood penalty when below low threshold (e.g., -8)
    public string SpriteKey { get; init; } = ""; // Icon sprite for expression bubbles (16x16)
}

// Building definition
public sealed class BuildingDef : IContentDef
{
    public int Id { get; set; }
    public string Name { get; init; } = "";
    public bool Interactable { get; init; } = true;
    public int TileSize { get; init; } = 1; // 1 = 1x1, 2 = 2x2, 3 = 3x3 (square buildings only)
    public int? SatisfiesNeedId { get; init; } // Set during content loading
    public float NeedSatisfactionAmount { get; init; } = 100f;
    public int InteractionDurationTicks { get; init; } = 1000;
    public float GrantsBuff { get; init; } // Mood bonus when interaction completes (e.g., 15)
    public int BuffDuration { get; init; } // Ticks the buff lasts (e.g., 2400)
    public IReadOnlyList<(int dx, int dy)> UseAreas { get; init; } = Array.Empty<(int, int)>(); // Relative tile offsets where pawn can use this building
    public string SpriteKey { get; init; } = ""; // Path to sprite texture

    // Resource system
    public string? ResourceType { get; init; } // "food", "water", etc. - null if building doesn't use resources
    public float MaxResourceAmount { get; init; } = 100f;
    public float DepletionMult { get; init; } = 1f; // 0 = infinite resources, 1 = normal depletion
    public bool CanBeWorkedAt { get; init; } = false; // Can pawns work here to replenish resources?

    // Hauling system
    public BuildingWorkType WorkType { get; init; } = BuildingWorkType.Direct;
    public string? HaulSourceResourceType { get; init; } // Resource type to haul (for HaulFromBuilding)
    public string? HaulSourceTerrainKey { get; init; } // Terrain type to harvest from (for HaulFromTerrain, e.g., "Trees")
    public bool CanSellToConsumers { get; init; } = true; // Whether consumers can buy resources here

    // Economic system
    public const int DefaultBaseCost = 10;
    public const float DefaultBaseProduction = 2f;
    public const int WorkBuyInThreshold = 10; // Payouts at or below this require no buy-in
    public const int WorkBuyInDivisor = 2; // Buy-in = payout / this (for payouts above threshold)

    public int BaseCost { get; init; } = DefaultBaseCost; // Base cost to use this building (scales with level)
    public float BaseProduction { get; init; } = DefaultBaseProduction; // Payout multiplier (payout = cost × baseProduction)

    /// <summary>
    /// Get the cost to use this building at the given level.
    /// </summary>
    public int GetCost(int level = 0)
    {
        return (int)(BaseCost * Math.Pow(1.15, level));
    }

    /// <summary>
    /// Get the payout for working at this building at the given level.
    /// </summary>
    public int GetPayout(int level = 0)
    {
        return (int)(GetCost(level) * BaseProduction);
    }

    /// <summary>
    /// Get the buy-in required to work at this building at the given level.
    /// Low-paying jobs (payout <= threshold) have no buy-in.
    /// Higher-paying jobs require capital (payout / divisor).
    /// </summary>
    public int GetWorkBuyIn(int level = 0)
    {
        int payout = GetPayout(level);
        if (payout <= WorkBuyInThreshold)
            return 0;
        return payout / WorkBuyInDivisor;
    }

    /// <summary>
    /// Returns true if this building is a "gold source" (creates money from nothing when worked).
    /// Currently, buildings with BaseCost = 0 are treated as gold sources.
    /// </summary>
    public bool IsGoldSource => BaseCost == 0;
}

// Action definition
public enum ActionType
{
    Idle,
    MoveTo,
    UseBuilding,
    Work,
    PickUp, // Pick up resources from building or terrain
    DropOff, // Drop off resources at destination building
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
    public int? ExpressionIconDefId { get; init; } // Need def ID for icon (shows need sprite)

    // Hauling context
    public TileCoord? TerrainTargetCoord { get; init; } // For terrain harvesting (pickup from tile)
    public string? ResourceType { get; init; } // Resource being hauled
    public float ResourceAmount { get; init; } // Amount to transfer
    public EntityId? SourceEntity { get; init; } // Source building for wholesale payment (building-to-building)
}
