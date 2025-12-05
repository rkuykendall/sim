using System;
using System.Collections.Generic;

namespace SimGame.Core;

public readonly struct TileCoord
{
    public readonly int X;
    public readonly int Y;
    public TileCoord(int x, int y) { X = x; Y = y; }
}

public sealed class Tile
{
    public int TerrainTypeId;
    public int? StructureId;
    public bool Walkable = true;
    public bool Buildable = true;
    public bool Indoors = false;
}

public sealed class Chunk
{
    public const int ChunkSize = 32;
    public readonly Tile[,] Tiles = new Tile[ChunkSize, ChunkSize];

    public Chunk()
    {
        for (int x = 0; x < ChunkSize; x++)
            for (int y = 0; y < ChunkSize; y++)
                Tiles[x, y] = new Tile();
    }
}

public sealed class World
{
    private readonly Dictionary<(int, int), Chunk> _chunks = new();

    // Play area bounds (in tiles) - matches 640x360 viewport with 32px tiles
    public const int MinX = 0;
    public const int MaxX = 19;  // 640 / 32 = 20 tiles (0-19)
    public const int MinY = 0;
    public const int MaxY = 10;  // 360 / 32 = 11.25, use 11 tiles (0-10)

    public static bool IsInBounds(TileCoord coord) =>
        coord.X >= MinX && coord.X <= MaxX && coord.Y >= MinY && coord.Y <= MaxY;

    public Chunk GetOrCreateChunk(int cx, int cy)
    {
        if (!_chunks.TryGetValue((cx, cy), out var chunk))
        {
            chunk = new Chunk();
            _chunks[(cx, cy)] = chunk;
        }
        return chunk;
    }

    public Tile GetTile(TileCoord coord)
    {
        int cx = (int)Math.Floor((double)coord.X / Chunk.ChunkSize);
        int cy = (int)Math.Floor((double)coord.Y / Chunk.ChunkSize);
        int lx = ((coord.X % Chunk.ChunkSize) + Chunk.ChunkSize) % Chunk.ChunkSize;
        int ly = ((coord.Y % Chunk.ChunkSize) + Chunk.ChunkSize) % Chunk.ChunkSize;
        return GetOrCreateChunk(cx, cy).Tiles[lx, ly];
    }
}
