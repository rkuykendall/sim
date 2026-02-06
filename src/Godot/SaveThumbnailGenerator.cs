using System.Collections.Generic;
using System.Linq;
using Godot;
using SimGame.Core;

namespace SimGame.Godot;

/// <summary>
/// Generates minimap thumbnail textures from save data for the home screen.
/// Renders 1 pixel per tile using tile colors for a simple visual preview.
/// </summary>
public static class SaveThumbnailGenerator
{
    /// <summary>
    /// Generate a thumbnail texture from save data.
    /// Creates a 1:1 pixel-per-tile image matching world dimensions.
    /// </summary>
    public static ImageTexture Generate(SaveData saveData, ContentRegistry content)
    {
        var worldWidth = saveData.World.Width;
        var worldHeight = saveData.World.Height;

        var image = Image.CreateEmpty(worldWidth, worldHeight, false, Image.Format.Rgb8);

        // Use saved palette if available, otherwise fall back to content lookup
        List<ColorDef> palette;
        if (saveData.Palette != null && saveData.Palette.Count > 0)
        {
            palette = saveData.Palette.Select(SaveService.HexToColorDef).ToList();
        }
        else if (content.ColorPalettes.TryGetValue(saveData.SelectedPaletteId, out var p))
        {
            palette = p.Colors.ToList();
        }
        else
        {
            // Fallback: fill with gray if no palette
            image.Fill(new Color(0.3f, 0.3f, 0.3f));
            return ImageTexture.CreateFromImage(image);
        }

        // Render 1 pixel per tile
        for (int y = 0; y < worldHeight; y++)
        {
            for (int x = 0; x < worldWidth; x++)
            {
                // Tiles are stored column by column: (0,0), (0,1)... (0,H-1), (1,0)...
                int tileIndex = y + x * worldHeight;

                if (tileIndex >= 0 && tileIndex < saveData.World.Tiles.Count)
                {
                    var tile = saveData.World.Tiles[tileIndex];

                    // Use overlay color if overlay exists, otherwise base color
                    var colorIndex = tile.OverlayTerrainTypeId.HasValue
                        ? tile.OverlayColorIndex
                        : tile.ColorIndex;

                    if (colorIndex >= 0 && colorIndex < palette.Count)
                    {
                        var colorDef = palette[colorIndex];
                        var color = new Color(colorDef.R, colorDef.G, colorDef.B);
                        image.SetPixel(x, y, color);
                    }
                    else
                    {
                        // Fallback for invalid color index
                        image.SetPixel(x, y, new Color(0.2f, 0.2f, 0.2f));
                    }
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Generate a placeholder thumbnail for the "New Game" button.
    /// Uses default world dimensions (150x100).
    /// </summary>
    public static ImageTexture GenerateNewGamePlaceholder()
    {
        int width = World.DefaultWidth;
        int height = World.DefaultHeight;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgb8);

        // Simple gradient or solid color for new game
        var baseColor = new Color(0.15f, 0.4f, 0.15f); // Dark green

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Simple crosshatch pattern
                bool pattern = ((x + y) % 8 < 4);
                var color = pattern ? baseColor : baseColor.Lightened(0.1f);
                image.SetPixel(x, y, color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
