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
    
    // Debug: pathfinding info
    public (int X, int Y)? TargetTile { get; init; }
    public IReadOnlyList<(int X, int Y)>? CurrentPath { get; init; }
    public int PathIndex { get; init; }
}

public sealed class RenderObject
{
    public EntityId Id { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int ObjectDefId { get; init; }
    public string Name { get; init; } = "";
    public bool InUse { get; init; }
    public string? UsedByName { get; init; }
}

public sealed class RenderTime
{
    public int Hour { get; init; }
    public int Minute { get; init; }
    public int Day { get; init; }
    public bool IsNight { get; init; }
    public string TimeString { get; init; } = "";
}

public sealed class RenderSnapshot
{
    public IReadOnlyList<RenderPawn> Pawns { get; init; } = Array.Empty<RenderPawn>();
    public IReadOnlyList<RenderObject> Objects { get; init; } = Array.Empty<RenderObject>();
    public RenderTime Time { get; init; } = new();
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

            if (pos == null) continue;

            string? actionName = null;
            if (action?.CurrentAction != null)
            {
                // Use DisplayName if set, otherwise fall back to type-based naming
                if (!string.IsNullOrEmpty(action.CurrentAction.DisplayName))
                {
                    actionName = action.CurrentAction.DisplayName;
                }
                else if (action.CurrentAction.Type == ActionType.UseObject && action.CurrentAction.TargetEntity.HasValue)
                {
                    var targetId = action.CurrentAction.TargetEntity.Value;
                    if (sim.Entities.Objects.TryGetValue(targetId, out var objComp))
                    {
                        var objDef = ContentDatabase.Objects[objComp.ObjectDefId];
                        actionName = $"Using {objDef.Name}";
                    }
                }
                else
                {
                    actionName = action.CurrentAction.Type.ToString();
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

            pawns.Add(new RenderPawn
            {
                Id = pawnId,
                X = pos.Coord.X,
                Y = pos.Coord.Y,
                Mood = mood?.Mood ?? 0,
                Name = pawn?.Name ?? $"Pawn {pawnId.Value}",
                CurrentAction = actionName,
                TargetTile = targetTile,
                CurrentPath = pathCoords,
                PathIndex = pathIndex
            });
        }

        var objects = new List<RenderObject>();

        foreach (var objId in sim.Entities.AllObjects())
        {
            sim.Entities.Positions.TryGetValue(objId, out var pos);
            sim.Entities.Objects.TryGetValue(objId, out var obj);

            if (pos == null || obj == null) continue;

            var objDef = ContentDatabase.Objects[obj.ObjectDefId];

            // Get name of pawn using this object
            string? usedByName = null;
            if (obj.InUse && obj.UsedBy.HasValue)
            {
                if (sim.Entities.Pawns.TryGetValue(obj.UsedBy.Value, out var userPawn))
                    usedByName = userPawn.Name;
            }

            objects.Add(new RenderObject
            {
                Id = objId,
                X = pos.Coord.X,
                Y = pos.Coord.Y,
                ObjectDefId = obj.ObjectDefId,
                Name = objDef.Name,
                InUse = obj.InUse,
                UsedByName = usedByName
            });
        }

        var time = new RenderTime
        {
            Hour = sim.Time.Hour,
            Minute = sim.Time.Minute,
            Day = sim.Time.Day,
            IsNight = sim.Time.IsNight,
            TimeString = sim.Time.TimeString
        };

        return new RenderSnapshot { Pawns = pawns, Objects = objects, Time = time };
    }
}
