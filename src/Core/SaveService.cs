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
            Palette = sim.Palette.Select(ColorDefToHex).ToList(),
            NextEntityId = sim.Entities.NextId,
            TaxPool = sim.TaxPool,
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
                // Save building name for stable content lookup (IDs can change between loads)
                if (sim.Content.Buildings.TryGetValue(building.BuildingDefId, out var buildingDef))
                {
                    entity.BuildingDefName = buildingDef.Name;
                }
                entity.BuildingDefId = building.BuildingDefId; // Keep for backward compat
                entity.BuildingColorIndex = building.ColorIndex;
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

    private static string ColorDefToHex(ColorDef c)
    {
        int r = (int)Math.Round(Math.Clamp(c.R, 0f, 1f) * 255);
        int g = (int)Math.Round(Math.Clamp(c.G, 0f, 1f) * 255);
        int b = (int)Math.Round(Math.Clamp(c.B, 0f, 1f) * 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    internal static ColorDef HexToColorDef(string hex)
    {
        hex = hex.TrimStart('#');
        int r = Convert.ToInt32(hex.Substring(0, 2), 16);
        int g = Convert.ToInt32(hex.Substring(2, 2), 16);
        int b = Convert.ToInt32(hex.Substring(4, 2), 16);
        return new ColorDef
        {
            R = r / 255f,
            G = g / 255f,
            B = b / 255f,
        };
    }
}
