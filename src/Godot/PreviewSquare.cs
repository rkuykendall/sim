using Godot;
using SimGame.Core;

namespace SimGame.Godot;

/// <summary>
/// Large preview square showing the current color and selected sprite.
/// Displays either a solid color or a sprite with color modulation applied.
/// Clicking opens the color picker modal.
/// </summary>
public partial class PreviewSquare : Button
{
    private ColorRect? _colorRect;
    private TextureRect? _textureRect;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(96, 96);

        // Create color layer (background)
        _colorRect = new ColorRect { MouseFilter = MouseFilterEnum.Ignore };
        _colorRect.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_colorRect);
        MoveChild(_colorRect, 0);

        // Create texture layer (sprite)
        _textureRect = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.FitWidth,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _textureRect.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_textureRect);
        MoveChild(_textureRect, 1); // Above color rect

        InitializeBorder();
    }

    private void InitializeBorder()
    {
        // 4px transparent border (outer clickable edge) + 4px content margin (selection indicator)
        // BgColor fills the content margin area, ColorRect/TextureRect covers the center
        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0), // Transparent (white when selected)
            BorderColor = new Color(1, 1, 1, 0), // Always transparent
            BorderWidthLeft = 4,
            BorderWidthRight = 4,
            BorderWidthTop = 4,
            BorderWidthBottom = 4,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            DrawCenter = true,
        };
        AddThemeStyleboxOverride("normal", styleBox);
        AddThemeStyleboxOverride("hover", styleBox);
        AddThemeStyleboxOverride("pressed", styleBox);
        AddThemeStyleboxOverride("focus", styleBox);

        // Inset both rects: 4px border + 4px margin = 8px total
        if (_colorRect != null)
        {
            _colorRect.OffsetLeft = 8;
            _colorRect.OffsetTop = 8;
            _colorRect.OffsetRight = -8;
            _colorRect.OffsetBottom = -8;
        }
        if (_textureRect != null)
        {
            _textureRect.OffsetLeft = 8;
            _textureRect.OffsetTop = 8;
            _textureRect.OffsetRight = -8;
            _textureRect.OffsetBottom = -8;
        }
    }

    /// <summary>
    /// Updates the preview to show the current color and selected sprite.
    /// </summary>
    /// <param name="colorIndex">Index into color palette</param>
    /// <param name="buildingDefId">Selected building ID, or null</param>
    /// <param name="terrainDefId">Selected terrain ID, or null</param>
    /// <param name="content">Content registry for looking up definitions</param>
    /// <param name="palette">Current color palette</param>
    /// <param name="isBuildingPreview">True if this is the building preview (shows menu/build.png when null)</param>
    /// <param name="isTerrainPreview">True if this is the terrain preview (shows menu/paint.png when null)</param>
    /// <param name="isDeletePreview">True if this is the delete preview (shows delete.png)</param>
    /// <param name="isSelectPreview">True if this is the select preview (shows select.png)</param>
    public void UpdatePreview(
        int colorIndex,
        int? buildingDefId,
        int? terrainDefId,
        ContentRegistry? content,
        Color[] palette,
        bool isBuildingPreview = false,
        bool isTerrainPreview = false,
        bool isDeletePreview = false,
        bool isSelectPreview = false
    )
    {
        if (_colorRect == null || _textureRect == null)
            return;

        var baseColor = palette[colorIndex];
        Texture2D? texture = null;

        // Determine which sprite to show
        if (content != null)
        {
            if (
                buildingDefId.HasValue
                && content.Buildings.TryGetValue(buildingDefId.Value, out var buildingDef)
            )
            {
                texture = SpriteResourceManager.GetTexture(buildingDef.SpriteKey);
            }
            else if (
                terrainDefId.HasValue
                && content.Terrains.TryGetValue(terrainDefId.Value, out var terrainDef)
            )
            {
                texture = SpriteResourceManager.GetTexture(terrainDef.SpriteKey);
            }
        }

        // Fallback to unknown.png if sprite key exists but texture not found
        if (texture == null && (buildingDefId.HasValue || terrainDefId.HasValue))
        {
            texture = GD.Load<Texture2D>("res://sprites/placeholders/unknown.png");
        }

        // Show generic icons when nothing is selected
        if (texture == null && isSelectPreview)
        {
            texture = GD.Load<Texture2D>("res://sprites/tools/select.png");
        }
        else if (texture == null && isBuildingPreview)
        {
            texture = GD.Load<Texture2D>("res://sprites/menu/build.png");
        }
        else if (texture == null && isTerrainPreview)
        {
            texture = GD.Load<Texture2D>("res://sprites/menu/paint.png");
        }
        else if (texture == null && isDeletePreview)
        {
            texture = GD.Load<Texture2D>("res://sprites/tools/delete.png");
        }

        if (texture != null)
        {
            // Show sprite with color modulation
            _textureRect.Texture = texture;
            _textureRect.Modulate = baseColor; // Color tints the sprite
            _textureRect.Visible = true;
            _colorRect.Visible = false;
        }
        else
        {
            // Show solid color only (for color preview)
            _colorRect.Color = baseColor;
            _colorRect.Visible = true;
            _textureRect.Visible = false;
        }
    }

    /// <summary>
    /// Sets the selection state of the button, showing a visual highlight.
    /// </summary>
    /// <param name="selected">True to show selected state</param>
    public void SetSelected(bool selected)
    {
        // BgColor shows in the content margin area (inner 4px ring)
        // Border is always transparent (outer 4px clickable edge)
        var styleBox = new StyleBoxFlat
        {
            BgColor = selected ? Colors.White : new Color(0, 0, 0, 0),
            BorderColor = new Color(1, 1, 1, 0), // Always transparent
            BorderWidthLeft = 4,
            BorderWidthRight = 4,
            BorderWidthTop = 4,
            BorderWidthBottom = 4,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            DrawCenter = true,
        };
        AddThemeStyleboxOverride("normal", styleBox);
        AddThemeStyleboxOverride("hover", styleBox);
        AddThemeStyleboxOverride("pressed", styleBox);
        AddThemeStyleboxOverride("focus", styleBox);
    }
}
