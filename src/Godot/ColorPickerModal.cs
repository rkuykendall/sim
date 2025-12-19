using Godot;
using System;

namespace SimGame.Godot;

/// <summary>
/// Modal popup for selecting colors from the game's color palette.
/// Displays as a centered overlay with a semi-transparent background.
/// </summary>
public partial class ColorPickerModal : Control
{
    [Export] public NodePath BackgroundPath { get; set; } = "";
    [Export] public NodePath ColorGridPath { get; set; } = "";

    private ColorRect? _background;
    private GridContainer? _colorGrid;
    private Button? _activeColorButton;

    [Signal]
    public delegate void ColorSelectedEventHandler(int colorIndex);

    public override void _Ready()
    {
        // Set to full screen
        SetAnchorsPreset(LayoutPreset.FullRect);

        // Get node references
        if (!string.IsNullOrEmpty(BackgroundPath))
            _background = GetNodeOrNull<ColorRect>(BackgroundPath);

        if (!string.IsNullOrEmpty(ColorGridPath))
            _colorGrid = GetNodeOrNull<GridContainer>(ColorGridPath);

        // Connect background click to close
        if (_background != null)
        {
            _background.GuiInput += OnBackgroundInput;
        }

        PopulateColorGrid();
    }

    private void PopulateColorGrid()
    {
        if (_colorGrid == null)
            return;

        // Clear existing buttons
        foreach (var child in _colorGrid.GetChildren())
        {
            child.QueueFree();
        }

        // Create button for each color in palette
        for (int i = 0; i < GameColorPalette.Colors.Length; i++)
        {
            int colorIndex = i; // Capture for lambda
            var button = new Button
            {
                CustomMinimumSize = new Vector2(64, 64),
                Text = ""
            };

            // Create a ColorRect as background to show the color
            var colorRect = new ColorRect
            {
                Color = GameColorPalette.Colors[i],
                MouseFilter = MouseFilterEnum.Ignore
            };
            colorRect.SetAnchorsPreset(LayoutPreset.FullRect);
            button.AddChild(colorRect);
            button.MoveChild(colorRect, 0); // Move to back

            button.Pressed += () => OnColorClicked(colorIndex, button);
            _colorGrid.AddChild(button);
        }
    }

    private void OnColorClicked(int colorIndex, Button button)
    {
        EmitSignal(SignalName.ColorSelected, colorIndex);
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
