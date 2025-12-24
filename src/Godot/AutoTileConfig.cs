using System.Collections.Generic;

namespace SimGame.Godot;

/// <summary>
/// Configuration for 47-tile autotiling patterns.
/// Maps atlas positions to their neighbor connection bitmasks.
/// </summary>
public static class AutoTileConfig
{
    public record TilePattern(int X, int Y, byte PeeringBits);

    public static byte MakePeeringBits(
        bool topLeft = false,
        bool top = false,
        bool topRight = false,
        bool left = false,
        bool right = false,
        bool bottomLeft = false,
        bool bottom = false,
        bool bottomRight = false
    )
    {
        byte bits = 0;
        if (topLeft)
            bits |= 0b00000001;
        if (top)
            bits |= 0b00000010;
        if (topRight)
            bits |= 0b00000100;
        if (left)
            bits |= 0b00001000;
        if (right)
            bits |= 0b00010000;
        if (bottomLeft)
            bits |= 0b00100000;
        if (bottom)
            bits |= 0b01000000;
        if (bottomRight)
            bits |= 0b10000000;
        return bits;
    }

    public static readonly List<TilePattern> PathPatterns = new()
    {
        // Row 0
        new(0, 0, MakePeeringBits(false, false, false, false, false, false, true, false)),
        new(1, 0, MakePeeringBits(false, false, false, false, true, false, true, false)),
        new(2, 0, MakePeeringBits(false, false, false, true, true, false, true, false)),
        new(3, 0, MakePeeringBits(false, false, false, true, false, false, true, false)),
        new(4, 0, MakePeeringBits(true, true, false, true, true, false, true, false)),
        new(5, 0, MakePeeringBits(false, false, false, true, true, false, true, true)),
        new(6, 0, MakePeeringBits(false, false, false, true, true, true, true, false)),
        new(7, 0, MakePeeringBits(false, true, true, true, true, false, true, false)),
        new(8, 0, MakePeeringBits(false, false, false, false, true, false, true, true)),
        new(9, 0, MakePeeringBits(false, true, false, true, true, true, true, true)),
        new(10, 0, MakePeeringBits(false, false, false, true, true, true, true, true)),
        new(11, 0, MakePeeringBits(false, false, false, true, false, true, true, false)),
        // Row 1
        new(0, 1, MakePeeringBits(false, true, false, false, false, false, true, false)),
        new(1, 1, MakePeeringBits(false, true, false, false, true, false, true, false)),
        new(2, 1, MakePeeringBits(false, true, false, true, true, false, true, false)),
        new(3, 1, MakePeeringBits(false, true, false, true, false, false, true, false)),
        new(4, 1, MakePeeringBits(false, true, false, false, true, false, true, true)),
        new(5, 1, MakePeeringBits(false, true, true, true, true, true, true, true)),
        new(6, 1, MakePeeringBits(true, true, false, true, true, true, true, true)),
        new(7, 1, MakePeeringBits(false, true, false, true, false, true, true, false)),
        new(8, 1, MakePeeringBits(false, true, true, false, true, false, true, true)),
        new(9, 1, MakePeeringBits(false, true, true, true, true, true, true, false)),
        new(10, 1, MakePeeringBits(false, false, false, false, false, false, false, false)),
        new(11, 1, MakePeeringBits(true, true, false, true, true, true, true, false)),
        // Row 2
        new(0, 2, MakePeeringBits(false, true, false, false, false, false, false, false)),
        new(1, 2, MakePeeringBits(false, true, false, false, true, false, false, false)),
        new(2, 2, MakePeeringBits(false, true, false, true, true, false, false, false)),
        new(3, 2, MakePeeringBits(false, true, false, true, false, false, false, false)),
        new(4, 2, MakePeeringBits(false, true, true, false, true, false, true, false)),
        new(5, 2, MakePeeringBits(true, true, true, true, true, false, true, true)),
        new(6, 2, MakePeeringBits(true, true, true, true, true, true, true, false)),
        new(7, 2, MakePeeringBits(true, true, false, true, false, false, true, false)),
        new(8, 2, MakePeeringBits(false, true, true, true, true, false, true, true)),
        new(9, 2, MakePeeringBits(true, true, true, true, true, true, true, true)),
        new(10, 2, MakePeeringBits(true, true, false, true, true, false, true, true)),
        new(11, 2, MakePeeringBits(true, true, false, true, false, true, true, false)),
        // Row 3
        new(0, 3, MakePeeringBits(false, false, false, false, false, false, false, false)),
        new(1, 3, MakePeeringBits(false, false, false, false, true, false, false, false)),
        new(2, 3, MakePeeringBits(false, false, false, true, true, false, false, false)),
        new(3, 3, MakePeeringBits(false, false, false, true, false, false, false, false)),
        new(4, 3, MakePeeringBits(false, true, false, true, true, true, true, false)),
        new(5, 3, MakePeeringBits(false, true, true, true, true, false, false, false)),
        new(6, 3, MakePeeringBits(true, true, false, true, true, false, false, false)),
        new(7, 3, MakePeeringBits(false, true, false, true, true, false, true, true)),
        new(8, 3, MakePeeringBits(false, true, true, false, true, false, false, false)),
        new(9, 3, MakePeeringBits(true, true, true, true, true, false, false, false)),
        new(10, 3, MakePeeringBits(true, true, true, true, true, false, true, false)),
        new(11, 3, MakePeeringBits(true, true, false, true, false, false, false, false)),
    };
}
