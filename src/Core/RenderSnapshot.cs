using System;
using System.Collections.Generic;
using System.Linq;

namespace SimGame.Core;

public sealed class RenderPawn
{
    public EntityId Id { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public float Mood { get; init; }
    public string Name { get; init; } = "";
    public string? CurrentAction { get; init; }
    public ActionType CurrentActionType { get; init; } = ActionType.Idle;
    public AnimationType Animation { get; init; } = AnimationType.Idle;
    public ExpressionType? Expression { get; init; }
    public int? ExpressionIconDefId { get; init; } // Need def ID for expression icon

    // Economic info
    public int Gold { get; init; }

    // Inventory for hauling
    public string? CarryingResourceType { get; init; }
    public float CarryingAmount { get; init; }

    // Debug: pathfinding info
    public (int X, int Y)? TargetTile { get; init; }
    public IReadOnlyList<(int X, int Y)>? CurrentPath { get; init; }
    public int PathIndex { get; init; }

    // Debug: attachment info (building ID -> attachment strength)
    public IReadOnlyDictionary<EntityId, int>? Attachments { get; init; }
}

public sealed class RenderBuilding
{
    public EntityId Id { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int BuildingDefId { get; init; }
    public string Name { get; init; } = "";
    public bool InUse { get; init; }
    public string? UsedByName { get; init; }
    public int ColorIndex { get; init; }

    // Economic info
    public int Gold { get; init; }
    public int Cost { get; init; }
    public int Payout { get; init; }
    public int WorkBuyIn { get; init; }

    // Debug: Resource info
    public string? ResourceType { get; init; }
    public float? CurrentResource { get; init; }
    public float? MaxResource { get; init; }
    public bool? CanBeWorkedAt { get; init; }

    // Debug: Attachment info (pawn ID -> attachment strength)
    public IReadOnlyDictionary<EntityId, int>? Attachments { get; init; }

    // Sprite sheet info for rendering variants and phases
    public int SpriteVariants { get; init; } = 1;
    public int SpritePhases { get; init; } = 1;
    public int MaxPawnWealth { get; init; } // Wealth of the wealthiest attached pawn (for phase selection)
}

public sealed class RenderTime
{
    public int Hour { get; init; }
    public int Minute { get; init; }
    public int Day { get; init; }
    public bool IsNight { get; init; }
    public string TimeString { get; init; } = "";

    // 0.0 = midnight, 0.5 = noon, 1.0 = next midnight
    public float DayFraction { get; init; }
}

public sealed class RenderTheme
{
    public string? CurrentThemeName { get; init; }
    public string? CurrentMusicFile { get; init; }
    public string? QueuedThemeName { get; init; }
}

public sealed class RenderSnapshot
{
    public IReadOnlyList<RenderPawn> Pawns { get; init; } = Array.Empty<RenderPawn>();
    public IReadOnlyList<RenderBuilding> Buildings { get; init; } = Array.Empty<RenderBuilding>();
    public RenderTime Time { get; init; } = new();
    public RenderTheme Theme { get; init; } = new();
    public IReadOnlyList<ColorDef> ColorPalette { get; init; } = Array.Empty<ColorDef>();
}

public static class RenderSnapshotBuilder
{
    public static RenderSnapshot Build(Simulation sim)
    {
        var pawns = new List<RenderPawn>();

        foreach (var pawnId in sim.Entities.AllPawns())
        {
            sim.Entities.Positions.TryGetValue(pawnId, out var pos);
            sim.Entities.Moods.TryGetValue(pawnId, out var mood);
            sim.Entities.Pawns.TryGetValue(pawnId, out var pawn);
            sim.Entities.Actions.TryGetValue(pawnId, out var action);

            if (pos == null)
                continue;

            string? actionName = action?.CurrentAction?.DisplayName;

            // Reconstruct a friendly action name when DisplayName is missing/blank
            if (string.IsNullOrWhiteSpace(actionName) && action?.CurrentAction != null)
            {
                var current = action.CurrentAction;

                if (current.TargetEntity.HasValue)
                {
                    var targetId = current.TargetEntity.Value;
                    string targetName = sim.FormatEntityId(targetId);

                    if (
                        sim.Entities.Buildings.TryGetValue(targetId, out var buildingComp)
                        && sim.Content.Buildings.TryGetValue(
                            buildingComp.BuildingDefId,
                            out var buildingDef
                        )
                    )
                    {
                        targetName = buildingDef.Name;
                    }

                    actionName = current.Type switch
                    {
                        ActionType.UseBuilding => $"Using {targetName}",
                        ActionType.Work => $"Working at {targetName}",
                        ActionType.MoveTo => $"Going to {targetName}",
                        _ => current.Type.ToString(),
                    };
                }
                else if (current.TargetCoord.HasValue && current.Type == ActionType.MoveTo)
                {
                    var tc = current.TargetCoord.Value;
                    actionName = $"Going to ({tc.X},{tc.Y})";
                }
                else
                {
                    actionName = current.Type.ToString();
                }
            }

            // Get path debug info
            (int, int)? targetTile = null;
            List<(int, int)>? pathCoords = null;
            int pathIndex = 0;

            if (action?.CurrentAction?.TargetCoord != null)
            {
                var tc = action.CurrentAction.TargetCoord.Value;
                targetTile = (tc.X, tc.Y);
            }
            if (action?.CurrentPath != null)
            {
                pathCoords = action.CurrentPath.Select(c => (c.X, c.Y)).ToList();
                pathIndex = action.PathIndex;
            }

            // Gather attachment info (which buildings this pawn is attached to)
            var pawnAttachments = new Dictionary<EntityId, int>();
            foreach (var objId in sim.Entities.AllBuildings())
            {
                if (
                    sim.Entities.Attachments.TryGetValue(objId, out var attachComp)
                    && attachComp.UserAttachments.TryGetValue(pawnId, out var strength)
                    && strength > 0
                )
                {
                    pawnAttachments[objId] = strength;
                }
            }

            // Get pawn gold
            int pawnGold = 0;
            if (sim.Entities.Gold.TryGetValue(pawnId, out var goldComp))
            {
                pawnGold = goldComp.Amount;
            }

            // Get pawn inventory (what they're carrying)
            string? carryingType = null;
            float carryingAmount = 0f;
            if (sim.Entities.Inventory.TryGetValue(pawnId, out var inventory))
            {
                carryingType = inventory.ResourceType;
                carryingAmount = inventory.Amount;
            }

            pawns.Add(
                new RenderPawn
                {
                    Id = pawnId,
                    X = pos.Coord.X,
                    Y = pos.Coord.Y,
                    Mood = mood?.Mood ?? 0,
                    Name = pawn?.Name ?? $"Pawn {pawnId.Value}",
                    CurrentAction = actionName,
                    CurrentActionType = action?.CurrentAction?.Type ?? ActionType.Idle,
                    Animation = action?.CurrentAction?.Animation ?? AnimationType.Idle,
                    Expression = action?.CurrentAction?.Expression,
                    ExpressionIconDefId = action?.CurrentAction?.ExpressionIconDefId,
                    Gold = pawnGold,
                    CarryingResourceType = carryingType,
                    CarryingAmount = carryingAmount,
                    TargetTile = targetTile,
                    CurrentPath = pathCoords,
                    PathIndex = pathIndex,
                    Attachments = pawnAttachments,
                }
            );
        }

        var buildings = new List<RenderBuilding>();

        foreach (var objId in sim.Entities.AllBuildings())
        {
            sim.Entities.Positions.TryGetValue(objId, out var pos);
            sim.Entities.Buildings.TryGetValue(objId, out var obj);

            if (pos == null || obj == null)
                continue;

            if (!sim.Content.Buildings.TryGetValue(obj.BuildingDefId, out var buildingDef))
                continue;

            // Get name of pawn using this building
            string? usedByName = null;
            var usedById = obj.UsedBy(sim.Entities, objId);
            if (obj.InUse(sim.Entities, objId) && usedById.HasValue)
            {
                if (sim.Entities.Pawns.TryGetValue(usedById.Value, out var userPawn))
                    usedByName = userPawn.Name;
            }

            // Get resource info
            string? resourceType = null;
            float? currentResource = null;
            float? maxResource = null;
            bool? canBeWorkedAt = null;

            if (sim.Entities.Resources.TryGetValue(objId, out var resourceComp))
            {
                resourceType = resourceComp.ResourceType;
                currentResource = resourceComp.CurrentAmount;
                maxResource = resourceComp.MaxAmount;
            }

            if (buildingDef.CanBeWorkedAt)
            {
                canBeWorkedAt = true;
            }

            // Get attachment info (which pawns are attached to this building)
            // and calculate the max wealth of attached pawns for phase rendering
            var buildingAttachments = new Dictionary<EntityId, int>();
            int maxPawnWealth = 0;
            if (sim.Entities.Attachments.TryGetValue(objId, out var attachComp))
            {
                foreach (var (attachedPawnId, strength) in attachComp.UserAttachments)
                {
                    if (strength > 0)
                    {
                        buildingAttachments[attachedPawnId] = strength;

                        // Track the wealthiest attached pawn for phase rendering
                        if (sim.Entities.Gold.TryGetValue(attachedPawnId, out var attachedPawnGold))
                        {
                            if (attachedPawnGold.Amount > maxPawnWealth)
                            {
                                maxPawnWealth = attachedPawnGold.Amount;
                            }
                        }
                    }
                }
            }

            // Get building gold
            int buildingGold = 0;
            if (sim.Entities.Gold.TryGetValue(objId, out var buildingGoldComp))
            {
                buildingGold = buildingGoldComp.Amount;
            }

            buildings.Add(
                new RenderBuilding
                {
                    Id = objId,
                    X = pos.Coord.X,
                    Y = pos.Coord.Y,
                    BuildingDefId = obj.BuildingDefId,
                    Name = buildingDef.Name,
                    InUse = obj.InUse(sim.Entities, objId),
                    UsedByName = usedByName,
                    ColorIndex = obj.ColorIndex,
                    Gold = buildingGold,
                    Cost = buildingDef.GetCost(),
                    Payout = buildingDef.GetPayout(),
                    WorkBuyIn = buildingDef.GetWorkBuyIn(),
                    ResourceType = resourceType,
                    CurrentResource = currentResource,
                    MaxResource = maxResource,
                    CanBeWorkedAt = canBeWorkedAt,
                    Attachments = buildingAttachments,
                    SpriteVariants = buildingDef.SpriteVariants,
                    SpritePhases = buildingDef.SpritePhases,
                    MaxPawnWealth = maxPawnWealth,
                }
            );
        }

        var time = new RenderTime
        {
            Hour = sim.Time.Hour,
            Minute = sim.Time.Minute,
            Day = sim.Time.Day,
            IsNight = sim.Time.IsNight,
            TimeString = sim.Time.TimeString,
            DayFraction =
                (sim.Time.Tick % TimeService.TicksPerDay) / (float)TimeService.TicksPerDay,
        };

        // Get the selected color palette
        var colorPalette = sim.Content.ColorPalettes.TryGetValue(
            sim.SelectedPaletteId,
            out var palette
        )
            ? palette.Colors
            : Array.Empty<ColorDef>();

        // Get current theme state
        var theme = new RenderTheme
        {
            CurrentThemeName = sim.ThemeSystem?.CurrentTheme?.Name,
            CurrentMusicFile = sim.ThemeSystem?.CurrentTheme?.MusicFile,
            QueuedThemeName = sim.ThemeSystem?.QueuedTheme?.Name,
        };

        return new RenderSnapshot
        {
            Pawns = pawns,
            Buildings = buildings,
            Time = time,
            Theme = theme,
            ColorPalette = colorPalette,
        };
    }
}
