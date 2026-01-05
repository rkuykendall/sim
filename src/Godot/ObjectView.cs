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
    /// For multi-tile objects, the sprite is centered within the object's footprint.
    /// </summary>
    public void InitializeWithSprite(Texture2D texture, int tileSize = 1)
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

        // For multi-tile objects, center the sprite horizontally and align bottom with footprint bottom
        // The object's node position is at the anchor (top-left tile center)
        float footprintCenterOffsetX = (tileSize - 1) * RenderingConstants.RenderedTileSize / 2f;

        // Bottom of footprint from node center: (tileSize - 0.5) * tileSize
        // For 1x1: (1 - 0.5) * 32 = 16
        // For 2x2: (2 - 0.5) * 32 = 48
        float footprintBottom = (tileSize - 0.5f) * RenderingConstants.RenderedTileSize;

        // Position sprite so its bottom aligns with the bottom of the entire footprint
        _sprite.Position = new Vector2(
            footprintCenterOffsetX,
            footprintBottom - texture.GetHeight()
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
