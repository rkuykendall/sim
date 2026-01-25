using Godot;
using SimGame.Godot;

public partial class BuildingView : Node2D
{
    [Export]
    public Label? NameLabel { get; set; }

    [Export]
    public ColorRect? Body { get; set; }

    private Sprite2D? _sprite;
    private bool _usesSprite = false;

    // Sprite sheet support
    private int _spriteVariants = 1;
    private int _spritePhases = 1;
    private int _frameWidth;
    private int _frameHeight;
    private int _variantIndex;

    public override void _Ready()
    {
        // Fallback: Get Body manually if export didn't wire up
        if (Body == null)
            Body = GetNodeOrNull<ColorRect>("Body");
    }

    /// <summary>
    /// Initialize the building view with a sprite texture.
    /// Call this after instantiation if building has a sprite.
    /// For multi-tile buildings, the sprite is centered within the building's footprint.
    /// </summary>
    /// <param name="texture">The sprite texture (may be a sprite sheet)</param>
    /// <param name="tileSize">Building footprint size in tiles</param>
    /// <param name="spriteVariants">Number of variant rows in sprite sheet (default 1)</param>
    /// <param name="spritePhases">Number of phase columns in sprite sheet (default 1)</param>
    /// <param name="entityId">Building entity ID for consistent variant selection</param>
    public void InitializeWithSprite(
        Texture2D texture,
        int tileSize = 1,
        int spriteVariants = 1,
        int spritePhases = 1,
        int entityId = 0
    )
    {
        // Hide/remove ColorRect body
        if (Body != null)
        {
            Body.Visible = false;
        }

        _spriteVariants = spriteVariants;
        _spritePhases = spritePhases;

        // Calculate frame dimensions from the sprite sheet
        _frameWidth = texture.GetWidth() / spritePhases;
        _frameHeight = texture.GetHeight() / spriteVariants;

        // Use entity ID to select a consistent variant for this building
        _variantIndex = entityId % spriteVariants;

        // Create sprite with region enabled for sprite sheet support
        _sprite = new Sprite2D
        {
            Texture = texture,
            Centered = true,
            Name = "Sprite",
            RegionEnabled = spriteVariants > 1 || spritePhases > 1,
        };

        // Set initial region to first phase of the selected variant
        if (_sprite.RegionEnabled)
        {
            _sprite.RegionRect = new Rect2(
                0,
                _variantIndex * _frameHeight,
                _frameWidth,
                _frameHeight
            );
        }

        _sprite.Scale = new Vector2(RenderingConstants.SpriteScale, RenderingConstants.SpriteScale);

        // For multi-tile buildings, center the sprite horizontally and align bottom with footprint bottom
        // The building's node position is at the anchor (top-left tile center)
        float footprintCenterOffsetX = (tileSize - 1) * RenderingConstants.RenderedTileSize / 2f;

        // Bottom of footprint from node center: (tileSize - 0.5) * tileSize
        // For 1x1: (1 - 0.5) * 32 = 16
        // For 2x2: (2 - 0.5) * 32 = 48
        float footprintBottom = (tileSize - 0.5f) * RenderingConstants.RenderedTileSize;

        // Position sprite so its bottom aligns with the bottom of the entire footprint
        // Use frame height instead of full texture height for sprite sheets
        _sprite.Position = new Vector2(footprintCenterOffsetX, footprintBottom - _frameHeight);

        AddChild(_sprite);
        _usesSprite = true;
    }

    public void SetBuildingInfo(string name, bool inUse, int colorIndex, Color[] palette)
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

    /// <summary>
    /// Update the sprite phase based on pawn wealth.
    /// Phase 0 = poorest, Phase (spritePhases-1) = wealthiest.
    /// </summary>
    /// <param name="maxPawnWealth">The wealth of the wealthiest attached pawn</param>
    public void UpdateSpritePhase(int maxPawnWealth)
    {
        if (!_usesSprite || _sprite == null || !_sprite.RegionEnabled || _spritePhases <= 1)
            return;

        // Calculate phase based on wealth thresholds
        // Wealth thresholds: 0-99 = phase 0, 100-199 = phase 1, 200-299 = phase 2, etc.
        int phase = maxPawnWealth / 100;
        phase = System.Math.Clamp(phase, 0, _spritePhases - 1);

        // Update the region rect to show the correct phase
        _sprite.RegionRect = new Rect2(
            phase * _frameWidth,
            _variantIndex * _frameHeight,
            _frameWidth,
            _frameHeight
        );
    }
}
