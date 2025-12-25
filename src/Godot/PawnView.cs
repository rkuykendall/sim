using System;
using Godot;
using SimGame.Godot;

public partial class PawnView : Node2D
{
    [Export]
    public NodePath BodyPath { get; set; } = null!;

    private ColorRect? _body;
    private AnimatedSprite2D? _sprite;
    private bool _usesSprite = false;
    private SimGame.Core.AnimationType _currentAnimation = SimGame.Core.AnimationType.Idle;

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
    /// Initialize the pawn view with sprite animations.
    /// Call this after instantiation to use sprite instead of ColorRect.
    /// </summary>
    public void InitializeWithSprite(
        Texture2D? walkSheet,
        Texture2D? idleSheet,
        Texture2D? axeSheet,
        Texture2D? pickaxeSheet,
        Texture2D? lookDownSheet,
        Texture2D? lookUpSheet
    )
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
            Scale = new Vector2(2f, 2f), // Scale 16x16 sprite to 32x32
        };

        // Create sprite frames for animation
        var spriteFrames = new SpriteFrames();

        // Walk animation (8 frames, 128x16 sprite sheet)
        if (walkSheet != null)
        {
            spriteFrames.AddAnimation("walk");
            spriteFrames.SetAnimationLoop("walk", true);
            spriteFrames.SetAnimationSpeed("walk", ANIMATION_SPEED);
            for (int i = 0; i < 8; i++)
            {
                var atlasTexture = new AtlasTexture
                {
                    Atlas = walkSheet,
                    Region = new Rect2(i * 16, 0, 16, 16),
                };
                spriteFrames.AddFrame("walk", atlasTexture);
            }
        }

        // Idle animation (3 frames, 48x16 sprite sheet)
        if (idleSheet != null)
        {
            spriteFrames.AddAnimation("idle");
            spriteFrames.SetAnimationLoop("idle", true);
            spriteFrames.SetAnimationSpeed("idle", ANIMATION_SPEED / 2); // Slower idle
            for (int i = 0; i < 3; i++)
            {
                var atlasTexture = new AtlasTexture
                {
                    Atlas = idleSheet,
                    Region = new Rect2(i * 16, 0, 16, 16),
                };
                spriteFrames.AddFrame("idle", atlasTexture);
            }
        }

        // Axe animation (5 frames, 80x16 sprite sheet)
        if (axeSheet != null)
        {
            spriteFrames.AddAnimation("axe");
            spriteFrames.SetAnimationLoop("axe", true);
            spriteFrames.SetAnimationSpeed("axe", ANIMATION_SPEED);
            for (int i = 0; i < 5; i++)
            {
                var atlasTexture = new AtlasTexture
                {
                    Atlas = axeSheet,
                    Region = new Rect2(i * 16, 0, 16, 16),
                };
                spriteFrames.AddFrame("axe", atlasTexture);
            }
        }

        // Pickaxe animation (5 frames, 80x16 sprite sheet)
        if (pickaxeSheet != null)
        {
            spriteFrames.AddAnimation("pickaxe");
            spriteFrames.SetAnimationLoop("pickaxe", true);
            spriteFrames.SetAnimationSpeed("pickaxe", ANIMATION_SPEED);
            for (int i = 0; i < 5; i++)
            {
                var atlasTexture = new AtlasTexture
                {
                    Atlas = pickaxeSheet,
                    Region = new Rect2(i * 16, 0, 16, 16),
                };
                spriteFrames.AddFrame("pickaxe", atlasTexture);
            }
        }

        // Look down (1 frame, 16x16 sprite)
        if (lookDownSheet != null)
        {
            spriteFrames.AddAnimation("look_down");
            spriteFrames.SetAnimationLoop("look_down", false);
            var atlasTexture = new AtlasTexture
            {
                Atlas = lookDownSheet,
                Region = new Rect2(0, 0, 16, 16),
            };
            spriteFrames.AddFrame("look_down", atlasTexture);
        }

        // Look up (1 frame, 16x16 sprite)
        if (lookUpSheet != null)
        {
            spriteFrames.AddAnimation("look_up");
            spriteFrames.SetAnimationLoop("look_up", false);
            var atlasTexture = new AtlasTexture
            {
                Atlas = lookUpSheet,
                Region = new Rect2(0, 0, 16, 16),
            };
            spriteFrames.AddFrame("look_up", atlasTexture);
        }

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

    /// <summary>
    /// Set the current animation type.
    /// </summary>
    public void SetCurrentAnimation(SimGame.Core.AnimationType animation)
    {
        _currentAnimation = animation;
    }

    private void UpdateAnimation(float delta)
    {
        if (_sprite == null)
            return;

        // Check if pawn is moving
        var velocity = _targetPosition - _visualPosition;
        bool isMoving = velocity.LengthSquared() > 0.1f;

        // Map AnimationType enum to animation name
        string targetAnimation = _currentAnimation switch
        {
            SimGame.Core.AnimationType.Idle => "idle",
            SimGame.Core.AnimationType.Walk => "walk",
            SimGame.Core.AnimationType.Axe => "axe",
            SimGame.Core.AnimationType.Pickaxe => "pickaxe",
            SimGame.Core.AnimationType.LookUp => "look_up",
            SimGame.Core.AnimationType.LookDown => "look_down",
            _ => "idle",
        };

        // Apply animation if different
        if (_sprite.Animation != targetAnimation)
        {
            _sprite.Animation = targetAnimation;
            _sprite.Play();
        }

        // Flip sprite based on movement direction (for walk animation)
        if (isMoving)
        {
            if (velocity.X < 0)
                _sprite.FlipH = true;
            else if (velocity.X > 0)
                _sprite.FlipH = false;
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
