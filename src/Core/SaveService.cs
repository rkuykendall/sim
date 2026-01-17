using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimGame.Core;

public static class SaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>
    /// Validate that a simulation can be serialized and deserialized without errors.
    /// Throws JsonException or InvalidOperationException if serialization fails.
    /// </summary>
    public static void ValidateSerializable(Simulation sim, string testName = "test")
    {
        var saveData = ToSaveData(sim, testName);
        var json = JsonSerializer.Serialize(saveData, JsonOptions);
        var restored = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);

        if (restored == null)
        {
            throw new InvalidOperationException("Deserialization returned null");
        }
    }

    /// <summary>
    /// Serialize a Simulation to a SaveData DTO.
    /// </summary>
    public static SaveData ToSaveData(Simulation sim, string saveName)
    {
        var data = new SaveData
        {
            Name = saveName,
            SavedAt = DateTime.UtcNow,
            Seed = sim.Seed,
            CurrentTick = sim.Time.Tick,
            SelectedPaletteId = sim.SelectedPaletteId,
            NextEntityId = sim.Entities.NextId,
            World = SerializeWorld(sim.World),
            Entities = SerializeEntities(sim),
        };
        return data;
    }

    private static WorldSaveData SerializeWorld(World world)
    {
        var worldData = new WorldSaveData
        {
            Width = world.Width,
            Height = world.Height,
            Tiles = new List<TileSaveData>(),
        };

        for (int x = 0; x < world.Width; x++)
        {
            for (int y = 0; y < world.Height; y++)
            {
                var tile = world.GetTile(x, y);
                worldData.Tiles.Add(
                    new TileSaveData
                    {
                        X = x,
                        Y = y,
                        BaseTerrainTypeId = tile.BaseTerrainTypeId,
                        BaseVariantIndex = tile.BaseVariantIndex,
                        OverlayTerrainTypeId = tile.OverlayTerrainTypeId,
                        OverlayVariantIndex = tile.OverlayVariantIndex,
                        ColorIndex = tile.ColorIndex,
                        OverlayColorIndex = tile.OverlayColorIndex,
                        WalkabilityCost = tile.WalkabilityCost,
                        BlocksLight = tile.BlocksLight,
                        BuildingBlocksMovement = tile.BuildingBlocksMovement,
                    }
                );
            }
        }

        return worldData;
    }

    private static List<EntitySaveData> SerializeEntities(Simulation sim)
    {
        var entities = new List<EntitySaveData>();
        var em = sim.Entities;

        // Serialize pawns
        foreach (var id in em.AllPawns())
        {
            var entity = new EntitySaveData { Id = id.Value, Type = "Pawn" };

            if (em.Positions.TryGetValue(id, out var pos))
            {
                entity.X = pos.Coord.X;
                entity.Y = pos.Coord.Y;
            }

            if (em.Pawns.TryGetValue(id, out var pawn))
            {
                entity.Name = pawn.Name;
            }

            if (em.Needs.TryGetValue(id, out var needs))
            {
                entity.Needs = new Dictionary<int, float>(needs.Needs);
            }

            if (em.Moods.TryGetValue(id, out var mood))
            {
                entity.Mood = mood.Mood;
            }

            if (em.Buffs.TryGetValue(id, out var buffs))
            {
                entity.Buffs = buffs
                    .ActiveBuffs.Select(b => new BuffSaveData
                    {
                        Source = (int)b.Source,
                        SourceId = b.SourceId,
                        MoodOffset = b.MoodOffset,
                        StartTick = b.StartTick,
                        EndTick = b.EndTick,
                    })
                    .ToList();
            }

            if (em.Gold.TryGetValue(id, out var gold))
            {
                entity.Gold = gold.Amount;
            }

            if (em.Inventory.TryGetValue(id, out var inv))
            {
                entity.Inventory = new InventorySaveData
                {
                    ResourceType = inv.ResourceType,
                    Amount = inv.Amount,
                    MaxAmount = inv.MaxAmount,
                };
            }

            entities.Add(entity);
        }

        // Serialize buildings
        foreach (var id in em.AllBuildings())
        {
            var entity = new EntitySaveData { Id = id.Value, Type = "Building" };

            if (em.Positions.TryGetValue(id, out var pos))
            {
                entity.X = pos.Coord.X;
                entity.Y = pos.Coord.Y;
            }

            if (em.Buildings.TryGetValue(id, out var building))
            {
                entity.BuildingDefId = building.BuildingDefId;
                entity.BuildingColorIndex = building.ColorIndex;
                entity.InUse = building.InUse;
                entity.UsedBy = building.UsedBy?.Value;
            }

            if (em.Resources.TryGetValue(id, out var resource))
            {
                entity.Resource = new ResourceSaveData
                {
                    ResourceType = resource.ResourceType,
                    CurrentAmount = resource.CurrentAmount,
                    MaxAmount = resource.MaxAmount,
                    DepletionMult = resource.DepletionMult,
                };
            }

            if (em.Attachments.TryGetValue(id, out var attachments))
            {
                entity.Attachments = attachments.UserAttachments.ToDictionary(
                    kv => kv.Key.Value,
                    kv => kv.Value
                );
            }

            if (em.Gold.TryGetValue(id, out var gold))
            {
                entity.BuildingGold = gold.Amount;
            }

            entities.Add(entity);
        }

        return entities;
    }

    /// <summary>
    /// Get metadata about a save without loading the full data.
    /// </summary>
    public static SaveMetadata ToMetadata(SaveData data)
    {
        int pawnCount = data.Entities.Count(e => e.Type == "Pawn");
        int day = (data.CurrentTick / TimeService.TicksPerDay) + 1;

        return new SaveMetadata
        {
            SlotName = data.Name,
            DisplayName = data.Name,
            SavedAt = data.SavedAt,
            Day = day,
            PawnCount = pawnCount,
        };
    }
}
