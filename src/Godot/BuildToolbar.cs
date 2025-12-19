using Godot;
using SimGame.Core;
using System.Linq;

namespace SimGame.Godot;

public partial class BuildToolbar : PanelContainer
{
    [Export] public NodePath PreviewSquarePath { get; set; } = "";
    [Export] public NodePath ObjectPreviewPath { get; set; } = "";
    [Export] public NodePath TerrainPreviewPath { get; set; } = "";
    [Export] public NodePath DeletePreviewPath { get; set; } = "";
    [Export] public NodePath SelectButtonPath { get; set; } = "";
    [Export] public NodePath PlaceObjectButtonPath { get; set; } = "";
    [Export] public NodePath PlaceTerrainButtonPath { get; set; } = "";

    private PreviewSquare? _previewSquare;
    private PreviewSquare? _objectPreview;
    private PreviewSquare? _terrainPreview;
    private PreviewSquare? _deletePreview;
    private Button? _selectButton;
    private Button? _placeObjectButton;
    private Button? _placeTerrainButton;
    private ContentRegistry? _content;
    private ColorPickerModal? _colorPickerModal;
    private ContentPickerModal? _objectPickerModal;
    private ContentPickerModal? _terrainPickerModal;

    private Button? _activeToolButton = null;

    public override void _Ready()
    {
        // Get node references
        if (!string.IsNullOrEmpty(PreviewSquarePath))
            _previewSquare = GetNodeOrNull<PreviewSquare>(PreviewSquarePath);

        if (!string.IsNullOrEmpty(ObjectPreviewPath))
            _objectPreview = GetNodeOrNull<PreviewSquare>(ObjectPreviewPath);

        if (!string.IsNullOrEmpty(TerrainPreviewPath))
            _terrainPreview = GetNodeOrNull<PreviewSquare>(TerrainPreviewPath);

        if (!string.IsNullOrEmpty(DeletePreviewPath))
            _deletePreview = GetNodeOrNull<PreviewSquare>(DeletePreviewPath);

        if (!string.IsNullOrEmpty(SelectButtonPath))
            _selectButton = GetNodeOrNull<Button>(SelectButtonPath);
        if (!string.IsNullOrEmpty(PlaceObjectButtonPath))
            _placeObjectButton = GetNodeOrNull<Button>(PlaceObjectButtonPath);
        if (!string.IsNullOrEmpty(PlaceTerrainButtonPath))
            _placeTerrainButton = GetNodeOrNull<Button>(PlaceTerrainButtonPath);

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

        // Connect preview square clicks
        if (_previewSquare != null)
        {
            _previewSquare.Pressed += OnColorPreviewClicked;
        }
        if (_objectPreview != null)
        {
            _objectPreview.Pressed += OnObjectPreviewClicked;
        }
        if (_terrainPreview != null)
        {
            _terrainPreview.Pressed += OnTerrainPreviewClicked;
        }
        if (_deletePreview != null)
        {
            _deletePreview.Pressed += OnDeletePreviewClicked;
        }

        // Initialize previews with default color before content loads
        UpdateAllPreviews();
    }

    public void Initialize(ContentRegistry content)
    {
        _content = content;

        // Ensure nodes are loaded (in case Initialize is called before _Ready)
        if (_previewSquare == null && !string.IsNullOrEmpty(PreviewSquarePath))
            _previewSquare = GetNodeOrNull<PreviewSquare>(PreviewSquarePath);

        if (_objectPreview == null && !string.IsNullOrEmpty(ObjectPreviewPath))
            _objectPreview = GetNodeOrNull<PreviewSquare>(ObjectPreviewPath);

        if (_terrainPreview == null && !string.IsNullOrEmpty(TerrainPreviewPath))
            _terrainPreview = GetNodeOrNull<PreviewSquare>(TerrainPreviewPath);

        if (_deletePreview == null && !string.IsNullOrEmpty(DeletePreviewPath))
            _deletePreview = GetNodeOrNull<PreviewSquare>(DeletePreviewPath);

        var uiLayer = GetNode<CanvasLayer>("/root/Main/UI");

        // Create color picker modal
        var colorModalScene = GD.Load<PackedScene>("res://scenes/ColorPickerModal.tscn");
        _colorPickerModal = colorModalScene.Instantiate<ColorPickerModal>();
        _colorPickerModal.ColorSelected += OnColorSelected;
        _colorPickerModal.Visible = false;
        _colorPickerModal.ZIndex = 1000;
        uiLayer.AddChild(_colorPickerModal);

        // Create object picker modal
        var objectModalScene = GD.Load<PackedScene>("res://scenes/ContentPickerModal.tscn");
        _objectPickerModal = objectModalScene.Instantiate<ContentPickerModal>();
        _objectPickerModal.ItemSelected += OnObjectSelected;
        _objectPickerModal.Visible = false;
        _objectPickerModal.ZIndex = 1000;
        uiLayer.AddChild(_objectPickerModal);

        // Create terrain picker modal
        var terrainModalScene = GD.Load<PackedScene>("res://scenes/ContentPickerModal.tscn");
        _terrainPickerModal = terrainModalScene.Instantiate<ContentPickerModal>();
        _terrainPickerModal.ItemSelected += OnTerrainSelected;
        _terrainPickerModal.Visible = false;
        _terrainPickerModal.ZIndex = 1000;
        uiLayer.AddChild(_terrainPickerModal);

        UpdateAllPreviews();
    }

    private void OnColorPreviewClicked()
    {
        _colorPickerModal?.Show();
    }

    private void OnObjectPreviewClicked()
    {
        if (_objectPickerModal != null && _content != null)
        {
            var items = _content.Objects.Select(kv => (kv.Key, kv.Value.SpriteKey, kv.Value.Name));
            _objectPickerModal.PopulateGrid("Select Object", items, BuildToolState.SelectedColorIndex);
            _objectPickerModal.Show();
        }
    }

    private void OnTerrainPreviewClicked()
    {
        if (_terrainPickerModal != null && _content != null)
        {
            var items = _content.Terrains.Select(kv => (kv.Key, kv.Value.SpriteKey, kv.Value.Name));
            _terrainPickerModal.PopulateGrid("Select Terrain", items, BuildToolState.SelectedColorIndex);
            _terrainPickerModal.Show();
        }
    }

    private void OnDeletePreviewClicked()
    {
        BuildToolState.Mode = BuildToolMode.Delete;
        BuildToolState.SelectedObjectDefId = null;
        BuildToolState.SelectedTerrainDefId = null;
        HighlightToolButton(null); // No tool button highlighted for delete preview
        UpdateAllPreviews();
    }

    private void OnColorSelected(int colorIndex)
    {
        BuildToolState.SelectedColorIndex = colorIndex;
        _colorPickerModal?.Hide();
        UpdateAllPreviews();
    }

    private void OnObjectSelected(int objectDefId)
    {
        BuildToolState.Mode = BuildToolMode.PlaceObject;
        BuildToolState.SelectedObjectDefId = objectDefId;
        BuildToolState.SelectedTerrainDefId = null;
        HighlightToolButton(_placeObjectButton);
        UpdateAllPreviews();
    }

    private void OnTerrainSelected(int terrainDefId)
    {
        BuildToolState.Mode = BuildToolMode.PlaceTerrain;
        BuildToolState.SelectedTerrainDefId = terrainDefId;
        BuildToolState.SelectedObjectDefId = null;
        HighlightToolButton(_placeTerrainButton);
        UpdateAllPreviews();
    }

    private void UpdateAllPreviews()
    {
        // Update color preview
        _previewSquare?.UpdatePreview(
            BuildToolState.SelectedColorIndex,
            null,
            null,
            _content,
            isObjectPreview: false,
            isTerrainPreview: false
        );

        // Update object preview
        _objectPreview?.UpdatePreview(
            BuildToolState.SelectedColorIndex,
            BuildToolState.SelectedObjectDefId,
            null,
            _content,
            isObjectPreview: true,
            isTerrainPreview: false
        );

        // Update terrain preview
        _terrainPreview?.UpdatePreview(
            BuildToolState.SelectedColorIndex,
            null,
            BuildToolState.SelectedTerrainDefId,
            _content,
            isObjectPreview: false,
            isTerrainPreview: true
        );

        // Update delete preview
        _deletePreview?.UpdatePreview(
            BuildToolState.SelectedColorIndex,
            null,
            null,
            _content,
            isObjectPreview: false,
            isTerrainPreview: false,
            isDeletePreview: true
        );

        // Highlight the active preview based on current mode
        HighlightActivePreview();
    }

    private void HighlightActivePreview()
    {
        // Reset all preview highlights
        if (_previewSquare != null)
            _previewSquare.Modulate = Colors.White;
        if (_objectPreview != null)
            _objectPreview.Modulate = Colors.White;
        if (_terrainPreview != null)
            _terrainPreview.Modulate = Colors.White;
        if (_deletePreview != null)
            _deletePreview.Modulate = Colors.White;

        // Highlight the active preview based on mode
        switch (BuildToolState.Mode)
        {
            case BuildToolMode.PlaceObject:
                if (_objectPreview != null)
                    _objectPreview.Modulate = new Color(0.7f, 1.0f, 0.7f); // Light green tint
                break;
            case BuildToolMode.PlaceTerrain:
                if (_terrainPreview != null)
                    _terrainPreview.Modulate = new Color(0.7f, 1.0f, 0.7f); // Light green tint
                break;
            case BuildToolMode.Delete:
                if (_deletePreview != null)
                    _deletePreview.Modulate = new Color(0.7f, 1.0f, 0.7f); // Light green tint
                break;
            case BuildToolMode.Select:
                // No preview is highlighted in select mode
                break;
        }
    }

    private void OnSelectClicked()
    {
        BuildToolState.Mode = BuildToolMode.Select;
        BuildToolState.SelectedObjectDefId = null;
        BuildToolState.SelectedTerrainDefId = null;
        HighlightToolButton(_selectButton);
        UpdateAllPreviews();
    }

    private void OnPlaceObjectClicked()
    {
        BuildToolState.Mode = BuildToolMode.PlaceObject;
        BuildToolState.SelectedTerrainDefId = null;
        HighlightToolButton(_placeObjectButton);
        UpdateAllPreviews();
    }

    private void OnPlaceTerrainClicked()
    {
        BuildToolState.Mode = BuildToolMode.PlaceTerrain;
        BuildToolState.SelectedObjectDefId = null;
        HighlightToolButton(_placeTerrainButton);
        UpdateAllPreviews();
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
}
