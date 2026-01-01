using Godot;
using SimGame.Godot;

public partial class ObjectView : Node2D
{
    [Export]
    public Label? NameLabel { get; set; }

    [Export]
    public ColorRect? Body { get; set; }

    private Sprite2D? _sprite;
    private bool _usesSprite = false;

    public override void _Ready()
    {
        // Fallback: Get Body manually if export didn't wire up
        if (Body == null)
            Body = GetNodeOrNull<ColorRect>("Body");
    }

    /// <summary>
    /// Initialize the object view with a sprite texture.
    /// Call this after instantiation if object has a sprite.
    /// The sprite will be positioned so its bottom aligns with the bottom of the tile.
    /// </summary>
    public void InitializeWithSprite(Texture2D texture)
    {
        // Hide/remove ColorRect body
        if (Body != null)
        {
            Body.Visible = false;
        }

        // Create sprite
        _sprite = new Sprite2D
        {
            Texture = texture,
            Centered = true,
            Name = "Sprite",
        };

        _sprite.Scale = new Vector2(RenderingConstants.SpriteScale, RenderingConstants.SpriteScale);

        // Position sprite so its bottom aligns with the bottom of the tile
        // Since sprite is centered and scaled 2x, offset = RenderedTileSize/2 - texture height
        _sprite.Position = new Vector2(
            0,
            RenderingConstants.RenderedTileSize / 2 - texture.GetHeight()
        );

        AddChild(_sprite);
        _usesSprite = true;
    }

    public void SetObjectInfo(string name, bool inUse, int colorIndex, Color[] palette)
    {
        if (NameLabel != null)
            NameLabel.Text = name;

        var baseColor = palette[colorIndex];
        var displayColor = inUse ? baseColor.Lightened(0.3f) : baseColor;

        if (_usesSprite && _sprite != null)
        {
            // Use modulate for sprite tinting
            _sprite.Modulate = displayColor;
        }
        else if (Body != null)
        {
            // Fallback to ColorRect
            Body.Color = displayColor;
        }
    }
}
