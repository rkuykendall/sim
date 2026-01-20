using System;
using System.Collections.Generic;
using System.Linq;

namespace SimGame.Core;

// Position
public sealed class PositionComponent
{
    public TileCoord Coord { get; set; }
}

// Pawn identity
public sealed class PawnComponent
{
    public string Name { get; set; } = "";
}

// Needs (hunger, energy, etc.)
public sealed class NeedsComponent
{
    public Dictionary<int, float> Needs { get; set; } = new(); // needId -> 0..100
}

// Mood
public sealed class MoodComponent
{
    public float Mood { get; set; } // -100..100
}

// Active buffs
public sealed class BuffComponent
{
    public List<BuffInstance> ActiveBuffs { get; set; } = new();
}

// Action queue
public sealed class ActionComponent
{
    public ActionDef? CurrentAction { get; set; }
    public int ActionStartTick { get; set; }
    public Queue<ActionDef> ActionQueue { get; set; } = new();
    public List<TileCoord>? CurrentPath { get; set; }
    public int PathIndex { get; set; }
}

// World building (furniture, etc.)
public sealed class BuildingComponent
{
    public int BuildingDefId { get; set; }
    public int ColorIndex { get; set; } = 0; // Index into color palette

    /// <summary>
    /// Computed: Is any pawn currently targeting this building?
    /// This is calculated from pawn ActionComponents, not stored.
    /// </summary>
    public bool InUse(EntityManager entities, EntityId buildingId)
    {
        // Check if any pawn is targeting this building
        foreach (var pawnId in entities.AllPawns())
        {
            if (entities.Actions.TryGetValue(pawnId, out var actionComp))
            {
                // Check current action or any queued action
                if (
                    (actionComp.CurrentAction?.TargetEntity == buildingId)
                    || actionComp.ActionQueue.Any(a => a.TargetEntity == buildingId)
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Computed: Which pawn is currently using this building (if any)?
    /// This is calculated from pawn ActionComponents, not stored.
    /// </summary>
    public EntityId? UsedBy(EntityManager entities, EntityId buildingId)
    {
        // Find the pawn currently targeting this building
        foreach (var pawnId in entities.AllPawns())
        {
            if (entities.Actions.TryGetValue(pawnId, out var actionComp))
            {
                // Current action takes priority
                if (actionComp.CurrentAction?.TargetEntity == buildingId)
                {
                    return pawnId;
                }

                // Then check queued actions
                if (actionComp.ActionQueue.Any(a => a.TargetEntity == buildingId))
                {
                    return pawnId;
                }
            }
        }

        return null;
    }
}

// Resource storage (for buildings that provide resources like food, water)
public sealed class ResourceComponent
{
    public string ResourceType { get; set; } = ""; // "food", "water", etc.
    public float CurrentAmount { get; set; } = 100f; // 0..MaxAmount
    public float MaxAmount { get; set; } = 100f;
    public float DepletionMult { get; set; } = 1f; // Multiplier for resource depletion (0 = never depletes)
}

// Attachment tracking (which pawns use this building regularly)
public sealed class AttachmentComponent
{
    // Pawn ID → attachment strength (0-10)
    // Higher values = pawn uses this building more often
    public Dictionary<EntityId, int> UserAttachments { get; set; } = new();
}

// Gold/currency storage (for pawns and buildings)
public sealed class GoldComponent
{
    public int Amount { get; set; } = 0;
}

// Inventory for pawns carrying resources during hauling
public sealed class InventoryComponent
{
    public string? ResourceType { get; set; } = null; // null = empty hands
    public float Amount { get; set; } = 0f;
    public float MaxAmount { get; set; } = 50f; // Default carry capacity
}
