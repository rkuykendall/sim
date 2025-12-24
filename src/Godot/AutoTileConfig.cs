using System.Collections.Generic;

namespace SimGame.Godot;

/// <summary>
/// Configuration for 47-tile autotiling patterns.
/// Bitmask: bit0=TL, bit1=T, bit2=TR, bit3=L, bit4=R, bit5=BL, bit6=B, bit7=BR
/// </summary>
public static class AutoTileConfig
{
    public record TilePattern(int X, int Y, byte PeeringBits);

    /// <summary>
    /// Standard 47-tile template used by all autotiling textures.
    /// </summary>
    public static readonly List<TilePattern> Standard47TilePatterns = new()
    {
        // Row 0
        new(0, 0, 0x40),
        new(1, 0, 0x50),
        new(2, 0, 0x58),
        new(3, 0, 0x48),
        new(4, 0, 0x5B),
        new(5, 0, 0xD8),
        new(6, 0, 0x78),
        new(7, 0, 0x5E),
        new(8, 0, 0xD0),
        new(9, 0, 0xFA),
        new(10, 0, 0xF8),
        new(11, 0, 0x68),
        // Row 1
        new(0, 1, 0x42),
        new(1, 1, 0x52),
        new(2, 1, 0x5A),
        new(3, 1, 0x4A),
        new(4, 1, 0xD2),
        new(5, 1, 0xFE),
        new(6, 1, 0xFB),
        new(7, 1, 0x6A),
        new(8, 1, 0xD6),
        new(9, 1, 0x7E),
        // new(10, 1, 0x00), // Empty
        new(11, 1, 0x7B),
        // Row 2
        new(0, 2, 0x02),
        new(1, 2, 0x12),
        new(2, 2, 0x1A),
        new(3, 2, 0x0A),
        new(4, 2, 0x56),
        new(5, 2, 0xDF),
        new(6, 2, 0x7F),
        new(7, 2, 0x4B),
        new(8, 2, 0xDE),
        new(9, 2, 0xFF),
        new(10, 2, 0xDB),
        new(11, 2, 0x6B),
        // Row 3
        new(0, 3, 0x00),
        new(1, 3, 0x10),
        new(2, 3, 0x18),
        new(3, 3, 0x08),
        new(4, 3, 0x7A),
        new(5, 3, 0x1E),
        new(6, 3, 0x1B),
        new(7, 3, 0xDA),
        new(8, 3, 0x16),
        new(9, 3, 0x1F),
        new(10, 3, 0x5F),
        new(11, 3, 0x0B),
    };
}
