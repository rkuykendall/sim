using System;
using System.Collections.Generic;

namespace SimGame.Core;

/// <summary>
/// Definition of a single color (RGB tuple).
/// </summary>
public sealed class ColorDef
{
    public string Name { get; init; } = "";
    public float R { get; init; }
    public float G { get; init; }
    public float B { get; init; }
}

/// <summary>
/// Definition of a color palette containing exactly 12 colors.
/// Color palettes are loaded from YAML files and selected deterministically based on world seed.
/// </summary>
public sealed class ColorPaletteDef : IContentDef
{
    public int Id { get; set; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<ColorDef> Colors { get; init; } = Array.Empty<ColorDef>();

    /// <summary>
    /// Returns true if this palette has at least 1 color (required for gameplay).
    /// </summary>
    public bool IsValid => Colors.Count >= 1;
}
