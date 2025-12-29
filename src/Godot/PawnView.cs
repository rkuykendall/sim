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

    // Expression bubble
    private Node2D? _expressionBubble = null;
    private Sprite2D? _bubbleWrapper = null;
    private Sprite2D? _bubbleIcon = null;
    private float _bubbleFloatOffset = 0f;
    private const float BUBBLE_FLOAT_SPEED = 2f;
    private const float BUBBLE_FLOAT_AMPLITUDE = 3f;
    private const float BUBBLE_Y_OFFSET = -40f;

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

        // Animate bubble floating
        if (_expressionBubble != null && _expressionBubble.Visible)
        {
            _bubbleFloatOffset += BUBBLE_FLOAT_SPEED * (float)delta;
            float yOffset =
                BUBBLE_Y_OFFSET + Mathf.Sin(_bubbleFloatOffset) * BUBBLE_FLOAT_AMPLITUDE;
            _expressionBubble.Position = new Vector2(0, yOffset);
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

    /// <summary>
    /// Set the expression bubble to display above the pawn.
    /// </summary>
    public void SetExpression(
        SimGame.Core.ExpressionType? type,
        int? iconDefId,
        SimGame.Core.ContentRegistry content
    )
    {
        if (_expressionBubble == null)
            CreateExpressionBubble();

        if (type == null || iconDefId == null)
        {
            _expressionBubble!.Visible = false;
            return;
        }

        // Determine bubble wrapper sprite key based on expression type
        string wrapperKey = type.Value switch
        {
            SimGame.Core.ExpressionType.Thought => "thought_bubble",
            SimGame.Core.ExpressionType.Happy => "heart_bubble",
            SimGame.Core.ExpressionType.Complaint => "complaint_bubble",
            _ => "thought_bubble", // Fallback for unused types (Speech, Question)
        };

        // Get icon sprite key from content def
        string? iconKey = null;

        // Try to get sprite key from ObjectDef
        if (content.Objects.TryGetValue(iconDefId.Value, out var objDef))
        {
            iconKey = objDef.SpriteKey;
        }
        // Try BuffDef (buffs don't currently have sprite keys, so we'll use placeholder icons)
        else if (content.Buffs.TryGetValue(iconDefId.Value, out var buffDef))
        {
            // Map buff names to icon sprite keys
            iconKey = buffDef.Name.ToLower() switch
            {
                "well fed" => "heart",
                "rested" => "zzz",
                "starving" => "hungry",
                "exhausted" => "exclamation",
                _ => "question",
            };
        }

        if (iconKey == null)
        {
            _expressionBubble!.Visible = false;
            return;
        }

        // Load textures
        var wrapperTexture = SpriteResourceManager.GetTexture(wrapperKey);
        var iconTexture = SpriteResourceManager.GetTexture(iconKey);

        if (wrapperTexture != null && iconTexture != null)
        {
            _bubbleWrapper!.Texture = wrapperTexture;
            _bubbleIcon!.Texture = iconTexture;
            _expressionBubble!.Visible = true;
        }
        else
        {
            _expressionBubble!.Visible = false;
        }
    }

    /// <summary>
    /// Create the expression bubble node hierarchy.
    /// </summary>
    private void CreateExpressionBubble()
    {
        _expressionBubble = new Node2D
        {
            Name = "ExpressionBubble",
            Position = new Vector2(0, BUBBLE_Y_OFFSET),
            Visible = false,
            ZIndex = 100,
        };
        AddChild(_expressionBubble);

        _bubbleWrapper = new Sprite2D
        {
            Name = "Wrapper",
            Centered = true,
            ZIndex = 0,
        };
        _expressionBubble.AddChild(_bubbleWrapper);

        _bubbleIcon = new Sprite2D
        {
            Name = "Icon",
            Centered = true,
            ZIndex = 1,
        };
        _expressionBubble.AddChild(_bubbleIcon);
    }
}
