using Godot;
using SimGame.Core;
using System.Linq;

namespace SimGame.Godot;

public partial class BuildToolbar : PanelContainer
{
    [Export] public NodePath ObjectListPath { get; set; } = "";
    [Export] public NodePath TerrainListPath { get; set; } = "";
    [Export] public NodePath ColorPaletteContainerPath { get; set; } = "";
    [Export] public NodePath SelectButtonPath { get; set; } = "";
    [Export] public NodePath PlaceObjectButtonPath { get; set; } = "";
    [Export] public NodePath PlaceTerrainButtonPath { get; set; } = "";
    [Export] public NodePath DeleteButtonPath { get; set; } = "";

    private VBoxContainer? _objectList;
    private VBoxContainer? _terrainList;
    private GridContainer? _colorPaletteContainer;
    private Button? _selectButton;
    private Button? _placeObjectButton;
    private Button? _placeTerrainButton;
    private Button? _deleteButton;
    private ContentRegistry? _content;

    private Button? _activeToolButton = null;
    private Button? _activeContentButton = null;
    private Button? _activeColorButton = null;

    public override void _Ready()
    {
        // Get node references
        if (!string.IsNullOrEmpty(ObjectListPath))
            _objectList = GetNodeOrNull<VBoxContainer>(ObjectListPath);

        if (!string.IsNullOrEmpty(TerrainListPath))
            _terrainList = GetNodeOrNull<VBoxContainer>(TerrainListPath);

        if (!string.IsNullOrEmpty(ColorPaletteContainerPath))
            _colorPaletteContainer = GetNodeOrNull<GridContainer>(ColorPaletteContainerPath);

        if (!string.IsNullOrEmpty(SelectButtonPath))
            _selectButton = GetNodeOrNull<Button>(SelectButtonPath);
        if (!string.IsNullOrEmpty(PlaceObjectButtonPath))
            _placeObjectButton = GetNodeOrNull<Button>(PlaceObjectButtonPath);
        if (!string.IsNullOrEmpty(PlaceTerrainButtonPath))
            _placeTerrainButton = GetNodeOrNull<Button>(PlaceTerrainButtonPath);
        if (!string.IsNullOrEmpty(DeleteButtonPath))
            _deleteButton = GetNodeOrNull<Button>(DeleteButtonPath);

        // Connect tool button signals
        if (_selectButton != null)
        {
            _selectButton.Pressed += OnSelectClicked;
            _activeToolButton = _selectButton; // Default
            HighlightToolButton(_selectButton);
        }
        if (_placeObjectButton != null)
            _placeObjectButton.Pressed += OnPlaceObjectClicked;
        if (_placeTerrainButton != null)
            _placeTerrainButton.Pressed += OnPlaceTerrainClicked;
        if (_deleteButton != null)
            _deleteButton.Pressed += OnDeleteClicked;
    }

    public void Initialize(ContentRegistry content)
    {
        _content = content;

        // Ensure nodes are loaded (in case Initialize is called before _Ready)
        if (_objectList == null && !string.IsNullOrEmpty(ObjectListPath))
            _objectList = GetNodeOrNull<VBoxContainer>(ObjectListPath);

        if (_terrainList == null && !string.IsNullOrEmpty(TerrainListPath))
            _terrainList = GetNodeOrNull<VBoxContainer>(TerrainListPath);

        if (_colorPaletteContainer == null && !string.IsNullOrEmpty(ColorPaletteContainerPath))
            _colorPaletteContainer = GetNodeOrNull<GridContainer>(ColorPaletteContainerPath);

        PopulateColorPalette();
        PopulateObjectList();
        PopulateTerrainList();
    }

    private void PopulateObjectList()
    {
        if (_objectList == null || _content == null)
            return;

        // Clear existing buttons
        foreach (var child in _objectList.GetChildren())
        {
            child.QueueFree();
        }

        // Create button for each ObjectDef
        foreach (var (id, objDef) in _content.Objects.OrderBy(kv => kv.Value.Name))
        {
            var button = new Button
            {
                Text = objDef.Name,
                CustomMinimumSize = new Vector2(0, 30)
            };
            button.Pressed += () => OnObjectSelected(id, button);
            _objectList.AddChild(button);
        }
    }

    private void PopulateTerrainList()
    {
        if (_terrainList == null || _content == null)
            return;

        // Clear existing buttons
        foreach (var child in _terrainList.GetChildren())
        {
            child.QueueFree();
        }

        // Create button for each TerrainDef
        foreach (var (id, terrainDef) in _content.Terrains.OrderBy(kv => kv.Value.Name))
        {
            var button = new Button
            {
                Text = terrainDef.Name,
                CustomMinimumSize = new Vector2(0, 30)
            };
            button.Pressed += () => OnTerrainSelected(id, button);
            _terrainList.AddChild(button);
        }
    }

    private void OnSelectClicked()
    {
        BuildToolState.Mode = BuildToolMode.Select;
        BuildToolState.SelectedObjectDefId = null;
        BuildToolState.SelectedTerrainDefId = null;
        HighlightToolButton(_selectButton);
        ClearContentHighlight();
    }

    private void OnPlaceObjectClicked()
    {
        BuildToolState.Mode = BuildToolMode.PlaceObject;
        BuildToolState.SelectedTerrainDefId = null;
        HighlightToolButton(_placeObjectButton);
    }

    private void OnPlaceTerrainClicked()
    {
        BuildToolState.Mode = BuildToolMode.PlaceTerrain;
        BuildToolState.SelectedObjectDefId = null;
        HighlightToolButton(_placeTerrainButton);
    }

    private void OnDeleteClicked()
    {
        BuildToolState.Mode = BuildToolMode.Delete;
        BuildToolState.SelectedObjectDefId = null;
        BuildToolState.SelectedTerrainDefId = null;
        HighlightToolButton(_deleteButton);
        ClearContentHighlight();
    }

    private void OnObjectSelected(int objectDefId, Button button)
    {
        BuildToolState.Mode = BuildToolMode.PlaceObject;
        BuildToolState.SelectedObjectDefId = objectDefId;
        BuildToolState.SelectedTerrainDefId = null;
        HighlightToolButton(_placeObjectButton);
        HighlightContentButton(button);
    }

    private void OnTerrainSelected(int terrainDefId, Button button)
    {
        BuildToolState.Mode = BuildToolMode.PlaceTerrain;
        BuildToolState.SelectedTerrainDefId = terrainDefId;
        BuildToolState.SelectedObjectDefId = null;
        HighlightToolButton(_placeTerrainButton);
        HighlightContentButton(button);
    }

    private void HighlightToolButton(Button? button)
    {
        // Remove highlight from previous button
        if (_activeToolButton != null)
        {
            _activeToolButton.Modulate = Colors.White;
        }

        // Highlight new button
        if (button != null)
        {
            button.Modulate = new Color(0.7f, 1.0f, 0.7f); // Light green tint
            _activeToolButton = button;
        }
    }

    private void HighlightContentButton(Button? button)
    {
        // Remove highlight from previous content button
        if (_activeContentButton != null)
        {
            _activeContentButton.Modulate = Colors.White;
        }

        // Highlight new button
        if (button != null)
        {
            button.Modulate = new Color(0.7f, 1.0f, 0.7f); // Light green tint
            _activeContentButton = button;
        }
    }

    private void ClearContentHighlight()
    {
        if (_activeContentButton != null)
        {
            _activeContentButton.Modulate = Colors.White;
            _activeContentButton = null;
        }
    }

    private void PopulateColorPalette()
    {
        if (_colorPaletteContainer == null)
            return;

        // Clear existing buttons
        foreach (var child in _colorPaletteContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Create button for each color in palette
        for (int i = 0; i < GameColorPalette.Colors.Length; i++)
        {
            int colorIndex = i; // Capture for lambda
            var button = new Button
            {
                CustomMinimumSize = new Vector2(32, 32),
                Text = ""
            };

            // Create a ColorRect as background to show the color
            var colorRect = new ColorRect
            {
                Color = GameColorPalette.Colors[i],
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            colorRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            button.AddChild(colorRect);
            button.MoveChild(colorRect, 0); // Move to back

            button.Pressed += () => OnColorSelected(colorIndex, button);
            _colorPaletteContainer.AddChild(button);

            // Highlight the first color by default
            if (i == 0)
            {
                _activeColorButton = button;
                HighlightColorButton(button);
            }
        }
    }

    private void OnColorSelected(int colorIndex, Button button)
    {
        BuildToolState.SelectedColorIndex = colorIndex;
        HighlightColorButton(button);
    }

    private void HighlightColorButton(Button? button)
    {
        // Remove highlight from previous color button
        if (_activeColorButton != null)
        {
            _activeColorButton.Modulate = Colors.White;
        }

        // Highlight new button
        if (button != null)
        {
            button.Modulate = new Color(1.3f, 1.3f, 1.3f); // Brighten
            _activeColorButton = button;
        }
    }
}
