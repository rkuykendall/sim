using Godot;
using SimGame.Godot;

public partial class PawnView : Node2D
{
    [Export] public NodePath BodyPath { get; set; } = null!;

    private ColorRect? _body;
    private AnimatedSprite2D? _sprite;
    private bool _usesSprite = false;

    private bool _selected = false;
    private float _baseMood = 0f;

    // Smooth movement interpolation
    private Vector2 _visualPosition;
    private Vector2 _targetPosition;
    private const float LERP_SPEED = 0.01f;

    // Animation state
    private Vector2 _lastPosition;
    private float _animationTime = 0f;
    private const float ANIMATION_SPEED = 6f; // Frames per second

    public override void _Ready()
    {
        _body = GetNodeOrNull<ColorRect>(BodyPath);

        _visualPosition = Position;
        _targetPosition = Position;
        _lastPosition = Position;
    }

    public override void _Process(double delta)
    {
        // Smooth movement interpolation
        _visualPosition = _visualPosition.Lerp(_targetPosition, LERP_SPEED);
        Position = _visualPosition;

        // Update animation if using sprite
        if (_usesSprite && _sprite != null)
        {
            UpdateAnimation((float)delta);
        }
    }

    /// <summary>
    /// Initialize the pawn view with a sprite and animation.
    /// Call this after instantiation to use sprite instead of ColorRect.
    /// </summary>
    public void InitializeWithSprite(Texture2D spriteSheet)
    {
        // Hide ColorRect body
        if (_body != null)
        {
            _body.Visible = false;
        }

        // Create animated sprite
        _sprite = new AnimatedSprite2D
        {
            Centered = true,
            Name = "Sprite",
            Scale = new Vector2(2f, 2f)  // Scale 16x16 sprite to 32x32
        };

        // Create sprite frames for animation
        var spriteFrames = new SpriteFrames();

        // Add walking animation (8 frames from sprite sheet)
        spriteFrames.AddAnimation("walk");
        spriteFrames.SetAnimationLoop("walk", true);
        spriteFrames.SetAnimationSpeed("walk", ANIMATION_SPEED);

        // Split the sprite sheet into individual frames
        // The sprite sheet is 128x16 (8 frames of 16x16)
        for (int i = 0; i < 8; i++)
        {
            var atlasTexture = new AtlasTexture
            {
                Atlas = spriteSheet,
                Region = new Rect2(i * 16, 0, 16, 16)
            };
            spriteFrames.AddFrame("walk", atlasTexture);
        }

        // Add idle animation (use first frame)
        spriteFrames.AddAnimation("idle");
        spriteFrames.SetAnimationLoop("idle", false);
        var idleTexture = new AtlasTexture
        {
            Atlas = spriteSheet,
            Region = new Rect2(0, 0, 16, 16)
        };
        spriteFrames.AddFrame("idle", idleTexture);

        _sprite.SpriteFrames = spriteFrames;
        _sprite.Animation = "idle";
        _sprite.Play();

        AddChild(_sprite);
        _usesSprite = true;
    }

    /// <summary>
    /// Set the initial visual position without tweening.
    /// Used when spawning pawns to position them at an entry point.
    /// </summary>
    public void SetInitialPosition(Vector2 position)
    {
        _visualPosition = position;
        Position = position;
        _lastPosition = position;
    }

    /// <summary>
    /// Set the target position for smooth interpolation.
    /// </summary>
    public void SetTargetPosition(Vector2 target)
    {
        _targetPosition = target;
    }

    private void UpdateAnimation(float delta)
    {
        if (_sprite == null) return;

        // Check if pawn is moving
        var velocity = _targetPosition - _visualPosition;
        bool isMoving = velocity.LengthSquared() > 0.1f;

        if (isMoving)
        {
            // Play walking animation
            if (_sprite.Animation != "walk")
            {
                _sprite.Animation = "walk";
                _sprite.Play();
            }

            // Flip sprite based on movement direction
            if (velocity.X < 0)
                _sprite.FlipH = true;
            else if (velocity.X > 0)
                _sprite.FlipH = false;
        }
        else
        {
            // Play idle animation
            if (_sprite.Animation != "idle")
            {
                _sprite.Animation = "idle";
                _sprite.Play();
            }
        }

        _lastPosition = _visualPosition;
    }

    public void SetMood(float mood)
    {
        _baseMood = mood;
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        UpdateColors();
    }

    private void UpdateColors()
    {
        if (_usesSprite && _sprite != null)
        {
            // Use modulate for sprite tinting when selected
            if (_selected)
                _sprite.Modulate = Colors.White;
            else
                _sprite.Modulate = new Color(1f, 1f, 1f); // Normal color
        }
        else if (_body != null)
        {
            // Fallback to ColorRect
            if (_selected)
                _body.Color = Colors.White;
            else
                _body.Color = new Color(0.2f, 0.6f, 1f);
        }
    }
}
