using Godot;

namespace SimGame.Godot;

/// <summary>
/// A button that displays a sprite icon with optional color modulation.
/// Used for visual selection of buildings and terrains in the build toolbar.
/// </summary>
public partial class SpriteIconButton : Button
{
    private TextureRect? _textureRect;
    private bool _selected = false;

    public override void _Ready()
    {
        EnsureTextureRectExists();
        InitializeBorder();
    }

    private void InitializeBorder()
    {
        // 4px transparent border (outer clickable edge) + 4px content margin (selection indicator)
        // BgColor fills the content margin area, TextureRect covers the center
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

        // Inset TextureRect: 4px border + 4px margin = 8px total
        if (_textureRect != null)
        {
            _textureRect.OffsetLeft = 8;
            _textureRect.OffsetTop = 8;
            _textureRect.OffsetRight = -8;
            _textureRect.OffsetBottom = -8;
        }
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
