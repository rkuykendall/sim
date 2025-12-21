
using System;
using Godot;
using SimGame.Core;
using System.Collections.Generic;
using System.Linq;

namespace SimGame.Godot;

public partial class BuildToolbar : HBoxContainer
{
    public void SetDebugMode(bool debugMode)
    {
        if (_debugMode != debugMode)
        {
            _debugMode = debugMode;
            CreateColorAndToolButtons();
            RebuildOptions();
            UpdateAllButtons();
        }
    }
    [Export] public NodePath LeftPanelPath { get; set; } = "";
    [Export] public NodePath OptionsContainerPath { get; set; } = "";

    private GridContainer? _toolsGrid;
    private FlowContainer? _optionsContainer;
    private ContentRegistry? _content;
    private Color[] _currentPalette = Enumerable.Repeat(Colors.White, 12).ToArray();

    private readonly List<Button> _colorButtons = new();
    private readonly List<Button> _toolButtons = new();
    private readonly List<BuildToolMode> _toolButtonModes = new();
    private readonly List<SpriteIconButton> _optionButtons = new();

    public override void _Ready()
    {
        if (!string.IsNullOrEmpty(LeftPanelPath))
            _toolsGrid = GetNodeOrNull<GridContainer>(LeftPanelPath);

        if (!string.IsNullOrEmpty(OptionsContainerPath))
            _optionsContainer = GetNodeOrNull<FlowContainer>(OptionsContainerPath);
    }

    private bool _debugMode = false;
    public void Initialize(ContentRegistry content, bool debugMode = false)
    {
        _content = content;
        _debugMode = debugMode;

        if (_toolsGrid == null && !string.IsNullOrEmpty(LeftPanelPath))
            _toolsGrid = GetNodeOrNull<GridContainer>(LeftPanelPath);

        if (_optionsContainer == null && !string.IsNullOrEmpty(OptionsContainerPath))
            _optionsContainer = GetNodeOrNull<FlowContainer>(OptionsContainerPath);

        if (_toolsGrid == null || _optionsContainer == null)
        {
            GD.PrintErr("BuildToolbar: Required containers not found!");
            return;
        }

        CreateColorAndToolButtons();
        RebuildOptions();
        UpdateAllButtons();
    }

    public void UpdatePalette(Color[] palette)
    {
        _currentPalette = palette;
        // Rebuild left panel if color count changed
        CreateColorAndToolButtons();
        UpdateAllButtons();
    }

    private void CreateColorAndToolButtons()
    {
        // Clear existing buttons
        _colorButtons.Clear();
        _toolButtons.Clear();
        _toolButtonModes.Clear();

        if (_toolsGrid != null)
        {
            foreach (Node child in _toolsGrid.GetChildren())
            {
                child.QueueFree();
            }
        }

        // Calculate max rows needed
        int colorRows = _currentPalette.Length;
        // Determine which tools to show
        var toolDefs = new List<(Func<Button> create, BuildToolMode mode)>
        {
            (() => CreatePaintToolButton(), BuildToolMode.PlaceTerrain),
            (() => CreateFillSquareToolButton(), BuildToolMode.FillSquare),
            (() => CreateOutlineSquareToolButton(), BuildToolMode.OutlineSquare),
            (() => CreateFloodFillToolButton(), BuildToolMode.FloodFill),
            (() => CreateToolButton("generic-object.png", BuildToolMode.PlaceObject, "Place Object"), BuildToolMode.PlaceObject),
            (() => CreateToolButton("delete.png", BuildToolMode.Delete, "Delete"), BuildToolMode.Delete)
        };
        if (_debugMode)
        {
            toolDefs.Add((() => CreateToolButton("select.png", BuildToolMode.Select, "Select"), BuildToolMode.Select));
        }
        int toolRows = toolDefs.Count;
        int totalRows = System.Math.Max(colorRows, toolRows);

        // Create buttons row by row (GridContainer with 2 columns fills left-to-right)
        for (int row = 0; row < totalRows; row++)
        {
            // Column 0: Color button
            if (row < colorRows)
            {
                var colorIndex = row;
                var colorButton = new PreviewSquare
                {
                    CustomMinimumSize = new Vector2(96, 96)
                };
                colorButton.Pressed += () => OnColorSelected(colorIndex);
                _toolsGrid?.AddChild(colorButton);
                _colorButtons.Add(colorButton);
            }
            else
            {
                // Spacer if we have more tools than colors
                _toolsGrid?.AddChild(new Control { CustomMinimumSize = new Vector2(96, 96) });
            }

            // Column 1: Tool button
            if (row < toolRows)
            {
                var (create, mode) = toolDefs[row];
                Button toolButton = create();
                _toolsGrid?.AddChild(toolButton);
                _toolButtons.Add(toolButton);
                _toolButtonModes.Add(mode);
            }
            else
            {
                // Spacer if we have more colors than tools
                _toolsGrid?.AddChild(new Control { CustomMinimumSize = new Vector2(96, 96) });
            }
        }

        // Update all color buttons with palette colors
        for (int i = 0; i < _colorButtons.Count; i++)
        {
            UpdateColorButton(i);
        }
    }

    private Button CreatePaintToolButton ()
    {
        var paintBtn = new PreviewSquare
        {
            CustomMinimumSize = new Vector2(96, 96),
            TooltipText = "Paint Terrain"
        };
        paintBtn.Pressed += () => OnToolSelected(BuildToolMode.PlaceTerrain);
        return paintBtn;
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

    private void RebuildOptions()
    {
        // Clear existing option buttons
        foreach (var button in _optionButtons)
        {
            button.QueueFree();
        }
        _optionButtons.Clear();

        if (_optionsContainer != null)
        {
            foreach (Node child in _optionsContainer.GetChildren())
            {
                child.QueueFree();
            }
        }

        if (_content == null)
            return;

        // Get options list based on current mode
        var optionsList = new List<(int id, string spriteKey, string name, bool isObject)>();

        switch (BuildToolState.Mode)
        {
            case BuildToolMode.PlaceObject:
                foreach (var (id, def) in _content.Objects.OrderBy(kv => kv.Value.Name))
                {
                    optionsList.Add((id, def.SpriteKey, def.Name, true));
                }
                break;

            // Show terrain options for all paint tools
            case BuildToolMode.PlaceTerrain:
            case BuildToolMode.FillSquare:
            case BuildToolMode.OutlineSquare:
            case BuildToolMode.FloodFill:
                foreach (var (id, def) in _content.Terrains.OrderBy(kv => kv.Key))
                {
                    optionsList.Add((id, def.SpriteKey, id.ToString(), false));
                }
                break;
        }

        // Create option buttons
        foreach (var (id, spriteKey, name, isObject) in optionsList)
        {
            var button = CreateOptionButton(id, spriteKey, name, isObject);
            _optionButtons.Add(button);
            _optionsContainer?.AddChild(button);
        }

        // Auto-select first option if none selected, but do not trigger if already selected
        if (optionsList.Count > 0)
        {
            if (BuildToolState.Mode == BuildToolMode.PlaceObject && !BuildToolState.SelectedObjectDefId.HasValue)
            {
                BuildToolState.SelectedObjectDefId = optionsList[0].id;
                UpdateAllButtons();
            }
            else if (BuildToolState.Mode == BuildToolMode.PlaceTerrain && !BuildToolState.SelectedTerrainDefId.HasValue)
            {
                BuildToolState.SelectedTerrainDefId = optionsList[0].id;
                UpdateAllButtons();
            }
        }
    }

    private SpriteIconButton CreateOptionButton(int id, string spriteKey, string name, bool isObject)
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

            // Highlight selected color with white outline
            bool isSelected = colorIndex == BuildToolState.SelectedColorIndex;
            preview.SetSelected(isSelected);
        }
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

        // Rebuild options
        RebuildOptions();
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
        // Only set mode if not already PlaceTerrain to avoid resetting tool
        if (BuildToolState.Mode != BuildToolMode.PlaceTerrain)
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
            var mode = _toolButtonModes[i];
            var isActive = mode == BuildToolState.Mode;

            // Always update the paint tool button (PreviewSquare)
            if (button is PreviewSquare paintPreview)
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
                paintPreview.SetSelected(isActive);
            }
            // Handle sprite icon buttons (Select, Object, Delete)
            else if (button is SpriteIconButton spriteBtn)
            {
                // Update color modulation
                var textureRect = spriteBtn.GetNodeOrNull<TextureRect>("TextureRect");
                if (textureRect?.Texture != null)
                {
                    spriteBtn.SetSprite(textureRect.Texture, _currentPalette[BuildToolState.SelectedColorIndex]);
                }
                spriteBtn.SetSelected(isActive);
            }
        }

        // Update option button highlights and colors
        foreach (var button in _optionButtons)
        {
            bool isSelected = false;

            if (BuildToolState.Mode == BuildToolMode.PlaceObject && BuildToolState.SelectedObjectDefId.HasValue)
            {
                // Check if this button's ID matches the selected object
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
            else if ((BuildToolState.Mode == BuildToolMode.PlaceTerrain
                      || BuildToolState.Mode == BuildToolMode.FillSquare
                      || BuildToolState.Mode == BuildToolMode.OutlineSquare)
                     && BuildToolState.SelectedTerrainDefId.HasValue)
            {
                var terrains = _content?.Terrains.OrderBy(kv => kv.Key).ToList();
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

    private Button CreateFloodFillToolButton()
    {
        var fillBtn = new SpriteIconButton
        {
            CustomMinimumSize = new Vector2(96, 96),
            TooltipText = "Flood Fill"
        };
        var texture = GD.Load<Texture2D>("res://sprites/fill.png");
        if (texture != null)
        {
            fillBtn.SetSprite(texture, _currentPalette[BuildToolState.SelectedColorIndex]);
        }
        fillBtn.Pressed += () => OnToolSelected(BuildToolMode.FloodFill);
        return fillBtn;
    }

    private Button CreateFillSquareToolButton()
    {
        var fillBtn = new SpriteIconButton
        {
            CustomMinimumSize = new Vector2(96, 96),
            TooltipText = "Fill Square"
        };
        var texture = GD.Load<Texture2D>("res://sprites/box.png");
        if (texture != null)
        {
            fillBtn.SetSprite(texture, _currentPalette[BuildToolState.SelectedColorIndex]);
        }
        fillBtn.Pressed += () => OnToolSelected(BuildToolMode.FillSquare);
        return fillBtn;
    }

    private Button CreateOutlineSquareToolButton()
    {
        var outlineBtn = new SpriteIconButton
        {
            CustomMinimumSize = new Vector2(96, 96),
            TooltipText = "Outline Square"
        };
        var texture = GD.Load<Texture2D>("res://sprites/square.png");
        if (texture != null)
        {
            outlineBtn.SetSprite(texture, _currentPalette[BuildToolState.SelectedColorIndex]);
        }
        outlineBtn.Pressed += () => OnToolSelected(BuildToolMode.OutlineSquare);
        return outlineBtn;
    }
}
