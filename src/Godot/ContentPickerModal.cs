using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimGame.Godot;

/// <summary>
/// Modal popup for selecting objects or terrains from a sprite grid.
/// Displays as a centered overlay with a semi-transparent background.
/// </summary>
public partial class ContentPickerModal : Control
{
    [Export] public NodePath BackgroundPath { get; set; } = "";
    [Export] public NodePath TitleLabelPath { get; set; } = "";
    [Export] public NodePath ContentGridPath { get; set; } = "";

    private ColorRect? _background;
    private Label? _titleLabel;
    private GridContainer? _contentGrid;

    [Signal]
    public delegate void ItemSelectedEventHandler(int itemId);

    public override void _Ready()
    {
        // Set to full screen
        SetAnchorsPreset(LayoutPreset.FullRect);

        // Get node references
        if (!string.IsNullOrEmpty(BackgroundPath))
            _background = GetNodeOrNull<ColorRect>(BackgroundPath);

        if (!string.IsNullOrEmpty(TitleLabelPath))
            _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);

        if (!string.IsNullOrEmpty(ContentGridPath))
            _contentGrid = GetNodeOrNull<GridContainer>(ContentGridPath);

        // Connect background click to close
        if (_background != null)
        {
            _background.GuiInput += OnBackgroundInput;
        }
    }

    /// <summary>
    /// Populate the grid with sprite icons from the provided items.
    /// </summary>
    public void PopulateGrid(string title, IEnumerable<(int id, string spriteKey, string name)> items, int currentColorIndex)
    {
        if (_titleLabel != null)
        {
            _titleLabel.Text = title;
        }

        if (_contentGrid == null)
            return;

        // Clear existing buttons
        foreach (var child in _contentGrid.GetChildren())
        {
            child.QueueFree();
        }

        var currentColor = GameColorPalette.Colors[currentColorIndex];

        // Create button for each item
        foreach (var (id, spriteKey, name) in items.OrderBy(x => x.name))
        {
            int itemId = id; // Capture for lambda

            var button = new SpriteIconButton
            {
                CustomMinimumSize = new Vector2(64, 64)
            };

            var texture = SpriteResourceManager.GetTexture(spriteKey);
            if (texture == null)
            {
                // Load fallback sprite
                texture = GD.Load<Texture2D>("res://sprites/unknown.png");
            }

            button.SetSprite(texture, currentColor);
            button.Pressed += () => OnItemClicked(itemId);

            // Add tooltip with name
            button.TooltipText = name;

            _contentGrid.AddChild(button);
        }
    }

    private void OnItemClicked(int itemId)
    {
        EmitSignal(SignalName.ItemSelected, itemId);
        Hide();
    }

    private void OnBackgroundInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
            {
                Hide();
            }
        }
    }

    public new void Show()
    {
        Visible = true;
    }

    public new void Hide()
    {
        Visible = false;
    }
}
