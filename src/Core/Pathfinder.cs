using System;
using System.Collections.Generic;

namespace SimGame.Core;

public static class Pathfinder
{
    private static readonly (int dx, int dy)[] Directions = { (0, 1), (0, -1), (1, 0), (-1, 0) };

    public static List<TileCoord>? FindPath(World world, TileCoord start, TileCoord goal)
    {
        var openSet = new PriorityQueue<TileCoord, float>();
        var cameFrom = new Dictionary<TileCoord, TileCoord>();
        var gScore = new Dictionary<TileCoord, float> { [start] = 0 };

        openSet.Enqueue(start, Heuristic(start, goal));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current.X == goal.X && current.Y == goal.Y)
                return ReconstructPath(cameFrom, current);

            foreach (var (dx, dy) in Directions)
            {
                var neighbor = new TileCoord(current.X + dx, current.Y + dy);
                var tile = world.GetTile(neighbor);

                if (!tile.Walkable) continue;

                float tentativeG = gScore[current] + 1;

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    float fScore = tentativeG + Heuristic(neighbor, goal);
                    openSet.Enqueue(neighbor, fScore);
                }
            }
        }

        return null;
    }

    private static float Heuristic(TileCoord a, TileCoord b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static List<TileCoord> ReconstructPath(Dictionary<TileCoord, TileCoord> cameFrom, TileCoord current)
    {
        var path = new List<TileCoord> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }
}
