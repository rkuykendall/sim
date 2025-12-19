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

        // Block all input from passing through the modal
        MouseFilter = MouseFilterEnum.Stop;

        // Get node references
        if (!string.IsNullOrEmpty(BackgroundPath))
        {
            _background = GetNodeOrNull<ColorRect>(BackgroundPath);
            // Background should not block input to buttons
            if (_background != null)
            {
                _background.MouseFilter = MouseFilterEnum.Ignore;
            }
        }

        if (!string.IsNullOrEmpty(ColorGridPath))
        {
            _colorGrid = GetNodeOrNull<GridContainer>(ColorGridPath);
            // Grid uses default MouseFilter (Pass) to allow buttons to receive input
        }

        // Handle clicks on the modal itself (outside the color grid) to close
        GuiInput += OnModalInput;
    }

    /// <summary>
    /// Populate the color grid with buttons for each color in the palette.
    /// Should be called by the parent (e.g., BuildToolbar) with the current palette.
    /// </summary>
    public void PopulateColorGrid(Color[] palette)
    {
        if (_colorGrid == null)
            return;

        // Clear existing buttons
        foreach (var child in _colorGrid.GetChildren())
        {
            child.QueueFree();
        }

        // Create button for each color in palette
        for (int i = 0; i < palette.Length; i++)
        {
            int colorIndex = i; // Capture for lambda
            var button = new Button
            {
                CustomMinimumSize = new Vector2(64, 64),
                Text = "",
                FocusMode = FocusModeEnum.None // Prevent focus issues
            };

            // Create a ColorRect as background to show the color
            var colorRect = new ColorRect
            {
                Color = palette[i],
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

    private void OnModalInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
            {
                // Check if the click was on the color grid or any of its children
                if (_colorGrid != null)
                {
                    var gridRect = _colorGrid.GetGlobalRect();
                    if (gridRect.HasPoint(mouseButton.GlobalPosition))
                    {
                        // Click was inside the grid, don't close
                        return;
                    }
                }

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
