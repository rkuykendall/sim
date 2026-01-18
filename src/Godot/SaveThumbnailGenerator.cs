using System.Linq;
using Godot;
using SimGame.Core;

namespace SimGame.Godot;

/// <summary>
/// Generates minimap thumbnail textures from save data for the home screen.
/// Renders tile colors only (no buildings or pawns) for a simple visual preview.
/// </summary>
public static class SaveThumbnailGenerator
{
    // Thumbnail matches world aspect ratio (3:2)
    public const int ThumbnailWidth = 150;
    public const int ThumbnailHeight = 100;

    /// <summary>
    /// Generate a thumbnail texture from save data.
    /// </summary>
    public static ImageTexture Generate(SaveData saveData, ContentRegistry content)
    {
        var image = Image.CreateEmpty(ThumbnailWidth, ThumbnailHeight, false, Image.Format.Rgb8);

        // Get the color palette used by this save
        var palette = content.ColorPalettes.TryGetValue(saveData.SelectedPaletteId, out var p)
            ? p
            : content.ColorPalettes.Values.FirstOrDefault();

        if (palette == null)
        {
            // Fallback: fill with gray if no palette
            image.Fill(new Color(0.3f, 0.3f, 0.3f));
            return ImageTexture.CreateFromImage(image);
        }

        var worldWidth = saveData.World.Width;
        var worldHeight = saveData.World.Height;

        for (int y = 0; y < ThumbnailHeight; y++)
        {
            for (int x = 0; x < ThumbnailWidth; x++)
            {
                // Map thumbnail pixel to world tile
                int tileX = x * worldWidth / ThumbnailWidth;
                int tileY = y * worldHeight / ThumbnailHeight;

                // Tiles are stored row by row (x + y * width)
                int tileIndex = tileX + tileY * worldWidth;

                if (tileIndex >= 0 && tileIndex < saveData.World.Tiles.Count)
                {
                    var tile = saveData.World.Tiles[tileIndex];

                    // Use overlay color if overlay exists, otherwise base color
                    var colorIndex = tile.OverlayTerrainTypeId.HasValue
                        ? tile.OverlayColorIndex
                        : tile.ColorIndex;

                    if (colorIndex >= 0 && colorIndex < palette.Colors.Count)
                    {
                        var colorDef = palette.Colors[colorIndex];
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
    /// </summary>
    public static ImageTexture GenerateNewGamePlaceholder()
    {
        var image = Image.CreateEmpty(ThumbnailWidth, ThumbnailHeight, false, Image.Format.Rgb8);

        // Simple gradient or solid color for new game
        var baseColor = new Color(0.15f, 0.4f, 0.15f); // Dark green

        for (int y = 0; y < ThumbnailHeight; y++)
        {
            for (int x = 0; x < ThumbnailWidth; x++)
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
