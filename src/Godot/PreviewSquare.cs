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
        _colorRect = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        _colorRect.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_colorRect);
        MoveChild(_colorRect, 0);

        // Create texture layer (sprite)
        _textureRect = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.FitWidth,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };
        _textureRect.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_textureRect);
        MoveChild(_textureRect, 1); // Above color rect
    }

    /// <summary>
    /// Updates the preview to show the current color and selected sprite.
    /// </summary>
    /// <param name="colorIndex">Index into GameColorPalette</param>
    /// <param name="objectDefId">Selected object ID, or null</param>
    /// <param name="terrainDefId">Selected terrain ID, or null</param>
    /// <param name="content">Content registry for looking up definitions</param>
    public void UpdatePreview(int colorIndex, int? objectDefId, int? terrainDefId, ContentRegistry? content)
    {
        if (_colorRect == null || _textureRect == null)
            return;

        var baseColor = GameColorPalette.Colors[colorIndex];
        Texture2D? texture = null;

        // Determine which sprite to show
        if (content != null)
        {
            if (objectDefId.HasValue && content.Objects.TryGetValue(objectDefId.Value, out var objDef))
            {
                texture = SpriteResourceManager.GetTexture(objDef.SpriteKey);
            }
            else if (terrainDefId.HasValue && content.Terrains.TryGetValue(terrainDefId.Value, out var terrainDef))
            {
                texture = SpriteResourceManager.GetTexture(terrainDef.SpriteKey);
            }
        }

        // Fallback to unknown.png if sprite key exists but texture not found
        if (texture == null && (objectDefId.HasValue || terrainDefId.HasValue))
        {
            texture = GD.Load<Texture2D>("res://sprites/unknown.png");
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
            // Show solid color only
            _colorRect.Color = baseColor;
            _colorRect.Visible = true;
            _textureRect.Visible = false;
        }
    }
}
