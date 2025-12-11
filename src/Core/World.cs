using System;
using System.Collections.Generic;

namespace SimGame.Core;

public readonly struct TileCoord : IEquatable<TileCoord>
{
    public readonly int X;
    public readonly int Y;
    public TileCoord(int x, int y) { X = x; Y = y; }

    public bool Equals(TileCoord other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is TileCoord other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(TileCoord left, TileCoord right) => left.Equals(right);
    public static bool operator !=(TileCoord left, TileCoord right) => !left.Equals(right);
    public override string ToString() => $"({X}, {Y})";
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

    // Default play area bounds (in tiles) - matches 640x360 viewport with 32px tiles
    public const int DefaultMinX = 0;
    public const int DefaultMaxX = 19;  // 640 / 32 = 20 tiles (0-19)
    public const int DefaultMinY = 0;
    public const int DefaultMaxY = 10;  // 360 / 32 = 11.25, use 11 tiles (0-10)

    // Instance bounds (can be customized)
    public int MinX { get; }
    public int MaxX { get; }
    public int MinY { get; }
    public int MaxY { get; }

    public World() : this(DefaultMinX, DefaultMaxX, DefaultMinY, DefaultMaxY)
    {
    }

    public World(int minX, int maxX, int minY, int maxY)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    public bool IsInBounds(TileCoord coord) =>
        coord.X >= MinX && coord.X <= MaxX && coord.Y >= MinY && coord.Y <= MaxY;

    // Static version for backward compatibility (uses default bounds)
    public static bool IsInBoundsStatic(TileCoord coord) =>
        coord.X >= DefaultMinX && coord.X <= DefaultMaxX && coord.Y >= DefaultMinY && coord.Y <= DefaultMaxY;

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
