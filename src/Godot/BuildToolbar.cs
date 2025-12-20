using Godot;
using SimGame.Core;
using System.Collections.Generic;
using System.Linq;

namespace SimGame.Godot;

public partial class BuildToolbar : PanelContainer
{
    [Export] public NodePath GridContainerPath { get; set; } = "";

    private GridContainer? _gridContainer;
    private ContentRegistry? _content;
    private Color[] _currentPalette = Enumerable.Repeat(Colors.White, 12).ToArray();

    private readonly List<Button> _colorButtons = new();
    private readonly List<Button> _toolButtons = new();
    private readonly List<SpriteIconButton> _optionButtons = new();

    private const int GRID_COLUMNS = 6;

    public override void _Ready()
    {
        if (!string.IsNullOrEmpty(GridContainerPath))
            _gridContainer = GetNodeOrNull<GridContainer>(GridContainerPath);
    }

    public void Initialize(ContentRegistry content)
    {
        _content = content;

        if (_gridContainer == null && !string.IsNullOrEmpty(GridContainerPath))
            _gridContainer = GetNodeOrNull<GridContainer>(GridContainerPath);

        if (_gridContainer == null)
        {
            GD.PrintErr("BuildToolbar: GridContainer not found!");
            return;
        }

        RebuildEntireGrid();
        UpdateAllButtons();
    }

    public void UpdatePalette(Color[] palette)
    {
        _currentPalette = palette;
        // Rebuild grid if color count changed
        RebuildEntireGrid();
        UpdateAllButtons();
    }

    private void PositionInGrid(int row, int col, Control node)
    {
        if (_gridContainer == null) return;

        int targetPosition = row * GRID_COLUMNS + col;
        int currentChildCount = _gridContainer.GetChildCount();

        // Add spacer nodes to reach target position
        while (currentChildCount < targetPosition)
        {
            var spacer = new Control { CustomMinimumSize = new Vector2(96, 96) };
            _gridContainer.AddChild(spacer);
            currentChildCount++;
        }

        _gridContainer.AddChild(node);
    }

    private void RebuildEntireGrid()
    {
        // Clear existing buttons
        _colorButtons.Clear();
        _toolButtons.Clear();
        foreach (var button in _optionButtons)
        {
            button.QueueFree();
        }
        _optionButtons.Clear();

        // Clear the grid
        if (_gridContainer != null)
        {
            foreach (Node child in _gridContainer.GetChildren())
            {
                child.QueueFree();
            }
        }

        // Get options list based on current mode
        var optionsList = GetCurrentOptions();

        // Calculate number of rows needed (max of colors or option rows)
        int colorRows = _currentPalette.Length;
        int optionRows = (optionsList.Count + 3) / 4; // 4 options per row, round up
        int totalRows = System.Math.Max(colorRows, optionRows);

        // Create grid in position order: row by row, left to right
        for (int row = 0; row < totalRows; row++)
        {
            for (int col = 0; col < 6; col++)
            {
                Control? nodeToAdd = null;

                if (col == 0 && row < colorRows)
                {
                    // Color button
                    var colorIndex = row;
                    var colorButton = new PreviewSquare
                    {
                        CustomMinimumSize = new Vector2(96, 96)
                    };
                    colorButton.Pressed += () => OnColorSelected(colorIndex);
                    _colorButtons.Add(colorButton);
                    nodeToAdd = colorButton;
                }
                else if (col == 1 && row < 4)
                {
                    // Tool button
                    Button toolButton = row switch
                    {
                        0 => CreateToolButton("select.png", BuildToolMode.Select, "Select"),
                        1 => CreatePaintToolButton(),
                        2 => CreateToolButton("generic-object.png", BuildToolMode.PlaceObject, "Place Object"),
                        3 => CreateToolButton("delete.png", BuildToolMode.Delete, "Delete"),
                        _ => new Button()
                    };
                    _toolButtons.Add(toolButton);
                    nodeToAdd = toolButton;
                }
                else if (col >= 2 && col < 6)
                {
                    // Option button
                    int optionIndex = row * 4 + (col - 2);
                    if (optionIndex < optionsList.Count)
                    {
                        var (id, spriteKey, name, isObject) = optionsList[optionIndex];
                        var optionButton = CreateOptionButtonDirect(id, spriteKey, name, isObject);
                        _optionButtons.Add(optionButton);
                        nodeToAdd = optionButton;
                    }
                    else
                    {
                        // Spacer for empty option slot
                        nodeToAdd = new Control { CustomMinimumSize = new Vector2(96, 96) };
                    }
                }
                else
                {
                    // Spacer
                    nodeToAdd = new Control { CustomMinimumSize = new Vector2(96, 96) };
                }

                if (nodeToAdd != null && _gridContainer != null)
                {
                    _gridContainer.AddChild(nodeToAdd);
                }
            }
        }

        // Update all color buttons with palette colors
        for (int i = 0; i < _colorButtons.Count; i++)
        {
            UpdateColorButton(i);
        }
    }

    private Button CreatePaintToolButton()
    {
        var paintBtn = new PreviewSquare
        {
            CustomMinimumSize = new Vector2(96, 96),
            TooltipText = "Paint Terrain"
        };
        paintBtn.Pressed += () => OnToolSelected(BuildToolMode.PlaceTerrain);
        return paintBtn;
    }

    private List<(int id, string spriteKey, string name, bool isObject)> GetCurrentOptions()
    {
        var options = new List<(int, string, string, bool)>();

        if (_content == null)
            return options;

        switch (BuildToolState.Mode)
        {
            case BuildToolMode.PlaceObject:
                foreach (var (id, def) in _content.Objects.OrderBy(kv => kv.Value.Name))
                {
                    options.Add((id, def.SpriteKey, def.Name, true));
                }
                break;

            case BuildToolMode.PlaceTerrain:
                foreach (var (id, def) in _content.Terrains.OrderBy(kv => kv.Value.Name))
                {
                    options.Add((id, def.SpriteKey, def.Name, false));
                }
                break;
        }

        return options;
    }

    private void UpdateColorButton(int colorIndex)
    {
        if (colorIndex >= _colorButtons.Count || colorIndex >= _currentPalette.Length) return;

        var button = _colorButtons[colorIndex];
        if (button is PreviewSquare preview)
        {
            preview.UpdatePreview(
                colorIndex,
                null,
                null,
                _content,
                _currentPalette,
                isObjectPreview: false,
                isTerrainPreview: false,
                isDeletePreview: false,
                isSelectPreview: false
            );

            // Highlight selected color
            button.Modulate = (colorIndex == BuildToolState.SelectedColorIndex)
                ? new Color(0.7f, 1.0f, 0.7f)
                : Colors.White;
        }
    }


    private SpriteIconButton CreateToolButton(string spriteKey, BuildToolMode mode, string tooltip)
    {
        var button = new SpriteIconButton
        {
            CustomMinimumSize = new Vector2(96, 96),
            TooltipText = tooltip
        };

        var texture = GD.Load<Texture2D>($"res://sprites/{spriteKey}");
        if (texture != null)
        {
            button.SetSprite(texture, _currentPalette[BuildToolState.SelectedColorIndex]);
        }

        button.Pressed += () => OnToolSelected(mode);

        return button;
    }

    private SpriteIconButton CreateOptionButtonDirect(int id, string spriteKey, string name, bool isObject)
    {
        var button = new SpriteIconButton
        {
            CustomMinimumSize = new Vector2(96, 96),
            TooltipText = name
        };

        var texture = SpriteResourceManager.GetTexture(spriteKey);
        if (texture != null)
        {
            button.SetSprite(texture, _currentPalette[BuildToolState.SelectedColorIndex]);
        }

        if (isObject)
            button.Pressed += () => OnObjectOptionSelected(id);
        else
            button.Pressed += () => OnTerrainOptionSelected(id);

        return button;
    }

    private void OnColorSelected(int colorIndex)
    {
        BuildToolState.SelectedColorIndex = colorIndex;
        UpdateAllButtons();
    }

    private void OnToolSelected(BuildToolMode mode)
    {
        BuildToolState.Mode = mode;

        // Clear selections when switching to Select or Delete modes
        if (mode == BuildToolMode.Select || mode == BuildToolMode.Delete)
        {
            BuildToolState.SelectedObjectDefId = null;
            BuildToolState.SelectedTerrainDefId = null;
        }

        // Rebuild entire grid with new options
        RebuildEntireGrid();

        // Auto-select first option if switching to a tool with options
        if (_content != null)
        {
            if (mode == BuildToolMode.PlaceObject && !BuildToolState.SelectedObjectDefId.HasValue)
            {
                var firstObject = _content.Objects.OrderBy(kv => kv.Value.Name).FirstOrDefault();
                if (firstObject.Value != null)
                {
                    OnObjectOptionSelected(firstObject.Key);
                }
            }
            else if (mode == BuildToolMode.PlaceTerrain && !BuildToolState.SelectedTerrainDefId.HasValue)
            {
                var firstTerrain = _content.Terrains.OrderBy(kv => kv.Value.Name).FirstOrDefault();
                if (firstTerrain.Value != null)
                {
                    OnTerrainOptionSelected(firstTerrain.Key);
                }
            }
        }

        UpdateAllButtons();
    }

    private void OnObjectOptionSelected(int objectDefId)
    {
        BuildToolState.Mode = BuildToolMode.PlaceObject;
        BuildToolState.SelectedObjectDefId = objectDefId;
        BuildToolState.SelectedTerrainDefId = null;
        UpdateAllButtons();
    }

    private void OnTerrainOptionSelected(int terrainDefId)
    {
        BuildToolState.Mode = BuildToolMode.PlaceTerrain;
        BuildToolState.SelectedTerrainDefId = terrainDefId;
        BuildToolState.SelectedObjectDefId = null;
        UpdateAllButtons();
    }

    private void UpdateAllButtons()
    {
        // Update color button highlights
        for (int i = 0; i < _colorButtons.Count; i++)
        {
            UpdateColorButton(i);
        }

        // Update tool button highlights
        for (int i = 0; i < _toolButtons.Count; i++)
        {
            var button = _toolButtons[i];
            var mode = GetToolModeForButtonIndex(i);
            var isActive = mode == BuildToolState.Mode;

            // Special handling for Paint button (PreviewSquare)
            if (button is PreviewSquare paintPreview && i == 1)
            {
                paintPreview.UpdatePreview(
                    BuildToolState.SelectedColorIndex,
                    null,
                    null,
                    _content,
                    _currentPalette,
                    isObjectPreview: false,
                    isTerrainPreview: true,
                    isDeletePreview: false,
                    isSelectPreview: false
                );
                button.Modulate = isActive ? new Color(0.7f, 1.0f, 0.7f) : Colors.White;
            }
            else
            {
                button.Modulate = isActive ? new Color(0.7f, 1.0f, 0.7f) : Colors.White;

                // Update color modulation on sprite buttons
                if (button is SpriteIconButton spriteBtn)
                {
                    var textureRect = spriteBtn.GetNodeOrNull<TextureRect>("TextureRect");
                    if (textureRect?.Texture != null)
                    {
                        spriteBtn.SetSprite(textureRect.Texture, _currentPalette[BuildToolState.SelectedColorIndex]);
                    }
                }
            }
        }

        // Update option button highlights and colors
        foreach (var button in _optionButtons)
        {
            bool isSelected = false;

            if (BuildToolState.Mode == BuildToolMode.PlaceObject && BuildToolState.SelectedObjectDefId.HasValue)
            {
                // Check if this button's ID matches the selected object
                // We need to track IDs somehow - for now use index matching
                var objects = _content?.Objects.OrderBy(kv => kv.Value.Name).ToList();
                if (objects != null)
                {
                    var index = _optionButtons.IndexOf(button);
                    if (index >= 0 && index < objects.Count)
                    {
                        isSelected = objects[index].Key == BuildToolState.SelectedObjectDefId.Value;
                    }
                }
            }
            else if (BuildToolState.Mode == BuildToolMode.PlaceTerrain && BuildToolState.SelectedTerrainDefId.HasValue)
            {
                var terrains = _content?.Terrains.OrderBy(kv => kv.Value.Name).ToList();
                if (terrains != null)
                {
                    var index = _optionButtons.IndexOf(button);
                    if (index >= 0 && index < terrains.Count)
                    {
                        isSelected = terrains[index].Key == BuildToolState.SelectedTerrainDefId.Value;
                    }
                }
            }

            button.SetSelected(isSelected);

            // Update color modulation
            var textureRect = button.GetNodeOrNull<TextureRect>("TextureRect");
            if (textureRect?.Texture != null)
            {
                button.SetSprite(textureRect.Texture, _currentPalette[BuildToolState.SelectedColorIndex]);
            }
        }
    }

    private BuildToolMode GetToolModeForButtonIndex(int index)
    {
        return index switch
        {
            0 => BuildToolMode.Select,
            1 => BuildToolMode.PlaceTerrain,
            2 => BuildToolMode.PlaceObject,
            3 => BuildToolMode.Delete,
            _ => BuildToolMode.Select
        };
    }
}
