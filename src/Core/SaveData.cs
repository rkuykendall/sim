using System;
using System.Collections.Generic;

namespace SimGame.Core;

/// <summary>
/// Root save data structure containing all simulation state.
/// </summary>
public sealed class SaveData
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public DateTime SavedAt { get; set; }
    public int Seed { get; set; }
    public int CurrentTick { get; set; }
    public int SelectedPaletteId { get; set; }
    public int NextEntityId { get; set; }
    public WorldSaveData World { get; set; } = new();
    public List<EntitySaveData> Entities { get; set; } = new();
}

public sealed class WorldSaveData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public List<TileSaveData> Tiles { get; set; } = new();
}

public sealed class TileSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int BaseTerrainTypeId { get; set; }
    public int BaseVariantIndex { get; set; }
    public int? OverlayTerrainTypeId { get; set; }
    public int OverlayVariantIndex { get; set; }
    public int ColorIndex { get; set; }
    public int OverlayColorIndex { get; set; }
    public float WalkabilityCost { get; set; }
    public bool BlocksLight { get; set; }
    public bool BuildingBlocksMovement { get; set; }
}

public sealed class EntitySaveData
{
    public int Id { get; set; }
    public string Type { get; set; } = ""; // "Pawn" or "Building"

    // Position (shared)
    public int X { get; set; }
    public int Y { get; set; }

    // Pawn-specific
    public string? Name { get; set; }
    public Dictionary<int, float>? Needs { get; set; }
    public float? Mood { get; set; }
    public List<BuffSaveData>? Buffs { get; set; }
    public int? Gold { get; set; }
    public InventorySaveData? Inventory { get; set; }

    // Building-specific
    public int? BuildingDefId { get; set; }
    public int? BuildingColorIndex { get; set; }
    public bool? InUse { get; set; }
    public int? UsedBy { get; set; }
    public ResourceSaveData? Resource { get; set; }
    public Dictionary<int, int>? Attachments { get; set; }
    public int? BuildingGold { get; set; }
}

public sealed class BuffSaveData
{
    public int Source { get; set; } // BuffSource enum value
    public int SourceId { get; set; }
    public float MoodOffset { get; set; }
    public int StartTick { get; set; }
    public int EndTick { get; set; }
}

public sealed class ResourceSaveData
{
    public string ResourceType { get; set; } = "";
    public float CurrentAmount { get; set; }
    public float MaxAmount { get; set; }
    public float DepletionMult { get; set; }
}

public sealed class InventorySaveData
{
    public string? ResourceType { get; set; }
    public float Amount { get; set; }
    public float MaxAmount { get; set; }
}

/// <summary>
/// Metadata about a save file for display in the home screen.
/// </summary>
public sealed class SaveMetadata
{
    public string SlotName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime SavedAt { get; set; }
    public int Day { get; set; }
    public int PawnCount { get; set; }
}
