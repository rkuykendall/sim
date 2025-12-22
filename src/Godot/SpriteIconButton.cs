using Godot;

namespace SimGame.Godot;

/// <summary>
/// A button that displays a sprite icon with optional color modulation.
/// Used for visual selection of objects and terrains in the build toolbar.
/// </summary>
public partial class SpriteIconButton : Button
{
    private TextureRect? _textureRect;
    private bool _selected = false;

    public override void _Ready()
    {
        EnsureTextureRectExists();
    }

    private void EnsureTextureRectExists()
    {
        if (_textureRect != null)
            return;

        // Create TextureRect for displaying the sprite
        _textureRect = new TextureRect
        {
            Name = "TextureRect",
            ExpandMode = TextureRect.ExpandModeEnum.FitWidth,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _textureRect.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_textureRect);
        MoveChild(_textureRect, 0); // Move to back behind button content
    }

    /// <summary>
    /// Sets the sprite texture and optional color modulation.
    /// </summary>
    /// <param name="texture">The texture to display</param>
    /// <param name="modulation">Optional color to modulate the sprite with</param>
    public void SetSprite(Texture2D? texture, Color? modulation = null)
    {
        EnsureTextureRectExists();

        if (_textureRect == null)
            return;

        _textureRect.Texture = texture;

        if (modulation.HasValue)
        {
            _textureRect.Modulate = modulation.Value;
        }
        else
        {
            _textureRect.Modulate = Colors.White;
        }
    }

    /// <summary>
    /// Updates only the color modulation without changing the texture.
    /// </summary>
    /// <param name="modulation">The color to modulate the sprite with</param>
    public void UpdateColor(Color modulation)
    {
        EnsureTextureRectExists();

        if (_textureRect == null)
            return;

        _textureRect.Modulate = modulation;
    }

    /// <summary>
    /// Sets the selection state of the button, showing a visual highlight.
    /// </summary>
    /// <param name="selected">True to show selected state</param>
    public void SetSelected(bool selected)
    {
        _selected = selected;

        if (selected)
        {
            // Show white outline border
            var styleBox = new StyleBoxFlat
            {
                BorderColor = Colors.White,
                BorderWidthLeft = 3,
                BorderWidthRight = 3,
                BorderWidthTop = 3,
                BorderWidthBottom = 3,
                DrawCenter = false,
            };
            AddThemeStyleboxOverride("normal", styleBox);
            AddThemeStyleboxOverride("hover", styleBox);
            AddThemeStyleboxOverride("pressed", styleBox);

            // Inset TextureRect to leave room for border
            if (_textureRect != null)
            {
                _textureRect.OffsetLeft = 3;
                _textureRect.OffsetTop = 3;
                _textureRect.OffsetRight = -3;
                _textureRect.OffsetBottom = -3;
            }
        }
        else
        {
            // Remove outline
            RemoveThemeStyleboxOverride("normal");
            RemoveThemeStyleboxOverride("hover");
            RemoveThemeStyleboxOverride("pressed");

            // Reset TextureRect to full size
            if (_textureRect != null)
            {
                _textureRect.OffsetLeft = 0;
                _textureRect.OffsetTop = 0;
                _textureRect.OffsetRight = 0;
                _textureRect.OffsetBottom = 0;
            }
        }
    }
}
