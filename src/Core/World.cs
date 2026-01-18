using System;

namespace SimGame.Core;

public readonly struct TileCoord : IEquatable<TileCoord>
{
    public readonly int X;
    public readonly int Y;

    public TileCoord(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(TileCoord other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is TileCoord other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(TileCoord left, TileCoord right) => left.Equals(right);

    public static bool operator !=(TileCoord left, TileCoord right) => !left.Equals(right);

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// A single tile in the world grid.
/// </summary>
/// <remarks>
/// Tiles store terrain data (texture, walkability). Buildings placed on tiles
/// are separate entities tracked by EntityManager, not stored on the tile itself.
/// When a building is placed, it may modify tile properties (e.g., BuildingBlocksMovement = true).
/// Tiles now support layering with a base terrain (always visible) and optional overlay terrain (autotiling paths, etc.)
/// </remarks>
public sealed class Tile
{
    /// <summary>
    /// Returns a hash representing the key properties of this tile for comparison.
    /// </summary>
    public int TileHash
    {
        get
        {
            // Combine all relevant properties for comparison
            // Use HashCode.Combine for value types and handle nullable overlay
            return HashCode.Combine(
                BaseTerrainTypeId,
                ColorIndex,
                OverlayTerrainTypeId ?? -1,
                OverlayColorIndex
            );
        }
    }

    /// <summary>Index into color palette for base terrain's visual appearance.</summary>
    public int ColorIndex { get; set; } = 0; // Default to first color

    /// <summary>ID of the base terrain type (grass, dirt, wood floor, etc.) - always renders underneath.</summary>
    public int BaseTerrainTypeId { get; set; }

    /// <summary>Variant index for base terrain (0-based, randomized on paint).</summary>
    public int BaseVariantIndex { get; set; } = 0;

    /// <summary>ID of the optional overlay terrain type (paths, etc.) - renders on top with transparency.</summary>
    public int? OverlayTerrainTypeId { get; set; } = null;

    /// <summary>Index into color palette for overlay terrain's visual appearance.</summary>
    public int OverlayColorIndex { get; set; } = 0; // Default to first color

    /// <summary>Variant index for overlay terrain (0-based, randomized on paint).</summary>
    public int OverlayVariantIndex { get; set; } = 0;

    /// <summary>Movement cost for pathfinding (1.0 = normal, less = faster, more = slower, infinity = impassable).</summary>
    public float WalkabilityCost { get; set; } = 1.0f;

    /// <summary>Whether this tile blocks light (from terrain or placed buildings).</summary>
    public bool BlocksLight { get; set; } = false;

    /// <summary>Whether a placed building blocks movement on this tile.</summary>
    public bool BuildingBlocksMovement { get; set; } = false;

    /// <summary>Whether pawns can walk through this tile. Computed from WalkabilityCost and BuildingBlocksMovement.</summary>
    public bool Walkable => !float.IsPositiveInfinity(WalkabilityCost) && !BuildingBlocksMovement;
}

/// <summary>
/// The game world - a fixed-size grid of tiles.
/// </summary>
/// <remarks>
/// The world is a simple 2D array of tiles with defined bounds.
/// Buildings are stored as entities in EntityManager, not on tiles.
/// Placing a building should update the tile's Walkable properties as needed.
/// </remarks>
public sealed class World
{
    // Default play area bounds (in tiles) - 3:2 ratio to better fit widescreen monitors
    public const int DefaultWidth = 150;
    public const int DefaultHeight = 100;

    private readonly Tile[,] _tiles;

    /// <summary>Width of the world in tiles.</summary>
    public int Width { get; }

    /// <summary>Height of the world in tiles.</summary>
    public int Height { get; }

    // Bounds for compatibility with existing code (0-based)
    public int MinX => 0;
    public int MaxX => Width - 1;
    public int MinY => 0;
    public int MaxY => Height - 1;

    public World()
        : this(DefaultWidth, DefaultHeight) { }

    public World(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new Tile[width, height];

        // Initialize all tiles
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                _tiles[x, y] = new Tile();
            }
        }
    }

    /// <summary>
    /// Constructor for compatibility with existing code that uses min/max bounds.
    /// </summary>
    public World(int minX, int maxX, int minY, int maxY)
        : this(maxX - minX + 1, maxY - minY + 1)
    {
        if (minX != 0 || minY != 0)
            throw new ArgumentException(
                "Non-zero minimum bounds are not supported. Use width/height constructor."
            );
    }

    public bool IsInBounds(TileCoord coord) =>
        coord.X >= 0 && coord.X < Width && coord.Y >= 0 && coord.Y < Height;

    /// <summary>
    /// Check if a tile is walkable (in bounds and not blocked by terrain/buildings).
    /// </summary>
    public bool IsWalkable(TileCoord coord) =>
        IsInBounds(coord) && _tiles[coord.X, coord.Y].Walkable;

    public Tile GetTile(TileCoord coord)
    {
        if (!IsInBounds(coord))
            throw new ArgumentOutOfRangeException(
                nameof(coord),
                $"Coordinate {coord} is out of bounds (0-{Width - 1}, 0-{Height - 1})"
            );

        return _tiles[coord.X, coord.Y];
    }

    /// <summary>
    /// Get a tile by x,y coordinates directly.
    /// </summary>
    public Tile GetTile(int x, int y) => GetTile(new TileCoord(x, y));
}
