using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using SimGame.Core;
using SimGame.Godot;

public partial class GameRoot : Node2D
{
    // For fill/outline brush drag
    private TileCoord? _brushDragStart = null;
    private TileCoord? _brushDragCurrent = null;
    private Simulation _sim = null!;
    private float _accumulator = 0f;
    private float _tickDelta;
    private const int MaxTicksPerFrame = 100; // Safety cap for frame spikes
    private SimulationSpeed _simulationSpeed = SimulationSpeed.Normal;

    // Autosave settings
    private const float AutosaveIntervalSeconds = 60f; // Autosave every 60 seconds
    private float _timeSinceLastAutosave = 0f;

    private enum SimulationSpeed
    {
        Paused = 0,
        Normal = 1,
        Fast4x = 4,
        Fast16x = 16,
        Fast64x = 64,
    }

    // App state
    private enum AppScreen
    {
        Home,
        Game,
    }

    private AppScreen _currentScreen = AppScreen.Home;
    private string? _currentSaveSlot = null;
    private UserSettings _userSettings = null!;
    private ContentRegistry _content = null!;

    private const float PawnHitboxSize = 24f;
    private const float BuildingHitboxSize = 28f;

    private readonly Dictionary<int, Node2D> _pawnNodes = new();
    private readonly Dictionary<int, Node2D> _buildingNodes = new();
    private readonly Dictionary<TileCoord, Node2D> _tileNodes = new();

    // Reusable collections for sync operations (avoid per-frame allocations)
    private readonly HashSet<int> _activeIds = new();
    private readonly List<int> _idsToRemove = new();

    // Current color palette from snapshot (fallback to white if empty)
    private Color[] _currentPalette = Enumerable.Repeat(Colors.White, 12).ToArray();
    private int _currentPaletteId = -1; // Track which palette is currently loaded

    // Character sprites for pawns
    private Texture2D? _characterSprite = null;
    private Texture2D? _idleSprite = null;
    private Texture2D? _axeSprite = null;
    private Texture2D? _pickaxeSprite = null;
    private Texture2D? _lookDownSprite = null;
    private Texture2D? _lookUpSprite = null;

    private int? _selectedPawnId = null;
    private int? _selectedBuildingId = null;
    private bool _debugMode = false;
    private TileCoord? _hoveredTile = null;
    private RenderSnapshot? _lastSnapshot = null;

    // Track mouse button state for drag painting
    private bool _isPaintingTerrain = false;
    private TileCoord? _lastPaintedTile = null;

    [Export]
    public PackedScene PawnScene { get; set; } = null!;

    [Export]
    public PackedScene BuildingScene { get; set; } = null!;

    [Export]
    public NodePath PawnsRootPath { get; set; } = ".";

    [Export]
    public NodePath BuildingsRootPath { get; set; } = ".";

    [Export]
    public NodePath TilesRootPath { get; set; } = ".";

    [Export]
    public NodePath ShadowRectPath { get; set; } = "";

    [Export]
    public NodePath CRTShaderLayerPath { get; set; } = "";

    [Export]
    public NodePath CameraPath { get; set; } = "";

    [Export]
    public NodePath UILayerPath { get; set; } = "";

    [Export]
    public NodePath ToolbarPath { get; set; } = "";

    [Export]
    public NodePath MusicManagerPath { get; set; } = "";

    [Export]
    public NodePath SoundManagerPath { get; set; } = "";

    [Export]
    public NodePath HomeScreenPath { get; set; } = "";

    private Node2D _pawnsRoot = null!;
    private HomeScreen? _homeScreen;
    private Node2D _buildingsRoot = null!;
    private Node2D _tilesRoot = null!;
    private Dictionary<int, ModulatableTileMapLayer> _autoTileLayers = new();
    private Dictionary<TileCoord, (Sprite2D baseSprite, Sprite2D overlaySprite)> _tileSprites =
        new();
    private Dictionary<int, List<(Vector2I coord, Color color)>> _autotileUpdates = new();
    private Dictionary<int, List<Vector2I>> _autotileClearCells = new();
    private DebugPanel? _debugPanel;
    private ColorRect? _shadowRect;
    private ShaderMaterial? _shadowShaderMaterial;
    private CRTShaderController? _crtShaderController;
    private CameraController? _camera;
    private CanvasLayer? _uiLayer;
    private BuildToolbar? _toolbar;
    private MusicManager? _musicManager;
    private SoundManager? _soundManager;

    private bool DebugMode => _debugMode;

    public override void _Ready()
    {
        ZIndex = ZIndexConstants.UIOverlay;

        // Load user settings and apply fullscreen
        _userSettings = UserSettings.Load();
        ApplyFullscreenSetting();

        // Load content once
        var contentPath = GetContentPath();
        GD.Print($"[GameRoot] Content path: {contentPath}");
        _content = ContentLoader.LoadAll(contentPath);
        _tickDelta = 1f / Simulation.TickRate;

        // Load sprite resources
        _characterSprite = SpriteResourceManager.GetTexture("character_walk");
        _idleSprite = SpriteResourceManager.GetTexture("character_idle");
        _axeSprite = SpriteResourceManager.GetTexture("character_axe");
        _pickaxeSprite = SpriteResourceManager.GetTexture("character_pickaxe");
        _lookDownSprite = SpriteResourceManager.GetTexture("character_look_down");
        _lookUpSprite = SpriteResourceManager.GetTexture("character_look_up");

        // Get node references
        _pawnsRoot = GetNode<Node2D>(PawnsRootPath);
        _buildingsRoot = GetNode<Node2D>(BuildingsRootPath);
        _tilesRoot = GetNode<Node2D>(TilesRootPath);

        if (!string.IsNullOrEmpty(CRTShaderLayerPath))
        {
            _crtShaderController = GetNodeOrNull<CRTShaderController>(CRTShaderLayerPath);
        }
        if (!string.IsNullOrEmpty(ShadowRectPath))
        {
            _shadowRect = GetNodeOrNull<ColorRect>(ShadowRectPath);
            if (_shadowRect != null)
            {
                var shader = GD.Load<Shader>("res://shaders/sdf_shadows.gdshader");
                _shadowShaderMaterial = new ShaderMaterial { Shader = shader };
                _shadowRect.Material = _shadowShaderMaterial;

                var gradient = new Gradient();
                gradient.AddPoint(0.0f, Colors.White);
                gradient.AddPoint(0.9f, Colors.White);
                gradient.AddPoint(1.0f, Colors.Black);

                var gradientTexture = new GradientTexture2D
                {
                    Gradient = gradient,
                    Width = 256,
                    Height = 1,
                };

                _shadowShaderMaterial.SetShaderParameter("shadow_gradient", gradientTexture);
            }
        }
        if (!string.IsNullOrEmpty(CameraPath))
            _camera = GetNodeOrNull<CameraController>(CameraPath);
        if (!string.IsNullOrEmpty(UILayerPath))
            _uiLayer = GetNodeOrNull<CanvasLayer>(UILayerPath);

        // Create debug panel programmatically
        _debugPanel = new DebugPanel();
        _debugPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _debugPanel.Position = new Vector2(-260, 10);
        _uiLayer?.AddChild(_debugPanel);

        if (!string.IsNullOrEmpty(ToolbarPath))
        {
            _toolbar = GetNodeOrNull<BuildToolbar>(ToolbarPath);
            if (_toolbar != null)
            {
                _toolbar.HomeButtonPressed += OnHomeButtonPressed;
            }
        }
        if (!string.IsNullOrEmpty(MusicManagerPath))
            _musicManager = GetNodeOrNull<MusicManager>(MusicManagerPath);
        if (!string.IsNullOrEmpty(SoundManagerPath))
            _soundManager = GetNodeOrNull<SoundManager>(SoundManagerPath);

        // Initialize home screen
        if (!string.IsNullOrEmpty(HomeScreenPath))
        {
            _homeScreen = GetNodeOrNull<HomeScreen>(HomeScreenPath);
            if (_homeScreen != null)
            {
                _homeScreen.NewGameRequested += OnNewGameRequested;
                _homeScreen.LoadGameRequested += OnLoadGameRequested;
                _homeScreen.QuitRequested += OnQuitRequested;
                _homeScreen.Initialize(_content, _soundManager);
            }
        }

        // Start on home screen
        ShowHomeScreen();
    }

    private void ApplyFullscreenSetting()
    {
        if (_userSettings.Fullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        }
        else
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        }
    }

    private void ShowHomeScreen()
    {
        _currentScreen = AppScreen.Home;

        // Hide game UI elements
        _toolbar?.Hide();
        _debugPanel?.SetDebugMode(false);
        _pawnsRoot?.Hide();
        _buildingsRoot?.Hide();
        _tilesRoot?.Hide();

        // Show home screen
        _homeScreen?.Show();
        _homeScreen?.RefreshSavesList();
    }

    private void ShowGame()
    {
        _currentScreen = AppScreen.Game;

        // Hide home screen
        _homeScreen?.Hide();

        // Show game UI elements
        _toolbar?.Show();
        _pawnsRoot?.Show();
        _buildingsRoot?.Show();
        _tilesRoot?.Show();

        // Initialize speed display
        UpdateSpeedDisplay();
    }

    private void OnNewGameRequested()
    {
        // Generate save name
        _currentSaveSlot = SaveFileManager.GenerateSaveName();

        // Create new simulation
        _sim = new Simulation(_content);

        // Initialize the game world
        InitializeGameWorld();

        // Show game
        ShowGame();

        GD.Print($"Started new game: {_currentSaveSlot}");
    }

    private void OnLoadGameRequested(string slotName)
    {
        var saveData = SaveFileManager.LoadSave(slotName);
        if (saveData == null)
        {
            GD.PrintErr($"Failed to load save: {slotName}");
            return;
        }

        _currentSaveSlot = slotName;

        // Restore simulation from save data
        _sim = Simulation.FromSaveData(saveData, _content);

        // Initialize the game world
        InitializeGameWorld();

        // Show game
        ShowGame();

        GD.Print($"Loaded game: {slotName}");
    }

    private void OnQuitRequested()
    {
        GD.Print("Quit requested");
        GetTree().Quit();
    }

    private void InitializeGameWorld()
    {
        // Clear existing nodes
        ClearAllNodes();

        // Initialize autotile layers
        InitializeAutoTileLayers();

        // Update palette from simulation
        var initialSnapshot = _sim.CreateRenderSnapshot();
        _currentPalette = GameColorPalette.ToGodotColors(initialSnapshot.ColorPalette);
        _currentPaletteId = -1; // Force palette update

        // Initialize tile nodes
        InitializeTileNodes();

        // Sync all tiles
        var allTiles = new List<TileCoord>();
        for (int x = 0; x < _sim.World.Width; x++)
        {
            for (int y = 0; y < _sim.World.Height; y++)
            {
                allTiles.Add(new TileCoord(x, y));
            }
        }
        SyncTiles(allTiles.ToArray());

        // Initialize toolbar with content
        _toolbar?.Initialize(_sim.Content, _soundManager, DebugMode);

        // Initialize debug panel
        _debugPanel?.Initialize(_sim.Content);
        _debugPanel?.SetDebugMode(_debugMode);

        // Center camera and set bounds
        if (_camera != null)
        {
            var worldWidth = _sim.World.Width * RenderingConstants.RenderedTileSize;
            var worldHeight = _sim.World.Height * RenderingConstants.RenderedTileSize;
            _camera.Position = new Vector2(worldWidth / 2f, worldHeight / 2f);
            _camera.SetWorldBounds(worldWidth, worldHeight);
        }

        // Reset selection state
        _selectedPawnId = null;
        _selectedBuildingId = null;
        _accumulator = 0f;
        _timeSinceLastAutosave = 0f;
    }

    private void ClearAllNodes()
    {
        // Clear pawn nodes
        foreach (var node in _pawnNodes.Values)
        {
            node.QueueFree();
        }
        _pawnNodes.Clear();

        // Clear building nodes
        foreach (var node in _buildingNodes.Values)
        {
            node.QueueFree();
        }
        _buildingNodes.Clear();

        // Clear tile sprites
        foreach (var (baseSprite, overlaySprite) in _tileSprites.Values)
        {
            baseSprite.QueueFree();
            overlaySprite.QueueFree();
        }
        _tileSprites.Clear();

        // Clear autotile layers
        foreach (var layer in _autoTileLayers.Values)
        {
            layer.QueueFree();
        }
        _autoTileLayers.Clear();
    }

    private void ReturnToHome()
    {
        // Auto-save current game
        if (_sim != null && _currentSaveSlot != null)
        {
            var saveData = SaveService.ToSaveData(_sim, _currentSaveSlot);
            SaveFileManager.WriteSave(_currentSaveSlot, saveData);
            GD.Print($"Auto-saved game: {_currentSaveSlot}");
        }

        ShowHomeScreen();
    }

    /// <summary>
    /// Public method that can be called by the toolbar's Home button.
    /// </summary>
    public void OnHomeButtonPressed()
    {
        ReturnToHome();
    }

    /// <summary>
    /// Performs an autosave of the current game state.
    /// </summary>
    private void PerformAutosave()
    {
        if (_sim == null || _currentSaveSlot == null)
            return;

        var saveData = SaveService.ToSaveData(_sim, _currentSaveSlot);
        SaveFileManager.WriteSave(_currentSaveSlot, saveData);
        _timeSinceLastAutosave = 0f;
        GD.Print($"Autosaved game: {_currentSaveSlot}");
    }

    public override void _Process(double delta)
    {
        // Only process game logic when in game mode
        if (_currentScreen != AppScreen.Game || _sim == null)
            return;

        // Apply speed multiplier to delta (0 when paused)
        float effectiveDelta =
            _simulationSpeed == SimulationSpeed.Paused ? 0f : (float)delta * (int)_simulationSpeed;

        _accumulator += effectiveDelta;

        // Safety: cap ticks per frame to prevent runaway on frame spikes
        int ticksProcessed = 0;
        while (_accumulator >= _tickDelta && ticksProcessed < MaxTicksPerFrame)
        {
            _sim.Tick();
            _accumulator -= _tickDelta;
            ticksProcessed++;
        }

        // If we hit the cap, reset accumulator and warn
        if (ticksProcessed >= MaxTicksPerFrame && _accumulator >= _tickDelta)
        {
            _accumulator = 0;
            GD.PushWarning($"Simulation tick cap reached, resetting accumulator");
        }

        var snapshot = _sim.CreateRenderSnapshot();
        _lastSnapshot = snapshot;

        if (_sim.SelectedPaletteId != _currentPaletteId)
        {
            _currentPalette = GameColorPalette.ToGodotColors(snapshot.ColorPalette);
            _currentPaletteId = _sim.SelectedPaletteId;
            _toolbar?.UpdatePalette(_currentPalette);
        }

        // Update music manager with current theme state
        _musicManager?.UpdateMusicState(snapshot);

        // Autosave logic (based on real time, not game time)
        _timeSinceLastAutosave += (float)delta;
        if (_timeSinceLastAutosave >= AutosaveIntervalSeconds)
        {
            PerformAutosave();
        }

        SyncPawns(snapshot);
        SyncBuildings(snapshot);
        UpdateInfoPanel(snapshot);
        UpdateBuildingInfoPanel(snapshot);
        UpdateTimeDisplay(snapshot);
        UpdateNightOverlay(snapshot);

        var mousePos = GetLocalMousePosition();
        _hoveredTile = ScreenToTileCoord(mousePos);

        if (_debugMode || BuildToolState.Mode != BuildToolMode.Select)
            QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed)
        {
            // F11: Toggle fullscreen (works in any screen)
            if (key.Keycode == Key.F11)
            {
                _userSettings.Fullscreen = !_userSettings.Fullscreen;
                _userSettings.Save();
                ApplyFullscreenSetting();
                GD.Print($"Fullscreen: {_userSettings.Fullscreen}");
                return;
            }

            // Escape: Return to home screen (only in game mode)
            if (key.Keycode == Key.Escape && _currentScreen == AppScreen.Game)
            {
                ReturnToHome();
                return;
            }

            // Game-only keyboard controls
            if (_currentScreen != AppScreen.Game)
                return;

            if (key.Keycode == Key.F3)
            {
                _debugMode = !_debugMode;
                GD.Print($"Debug mode: {_debugMode}");
                _toolbar?.SetDebugMode(_debugMode);
                _debugPanel?.SetDebugMode(_debugMode);
                QueueRedraw();
                return;
            }

            // Speed control keys 0-4
            if (key.Keycode == Key.Key0)
            {
                _simulationSpeed = SimulationSpeed.Paused;
                GD.Print("Simulation paused");
                UpdateSpeedDisplay();
                return;
            }
            if (key.Keycode == Key.Key1)
            {
                _simulationSpeed = SimulationSpeed.Normal;
                GD.Print("Simulation speed: 1x (normal)");
                UpdateSpeedDisplay();
                return;
            }
            if (key.Keycode == Key.Key2)
            {
                _simulationSpeed = SimulationSpeed.Fast4x;
                GD.Print("Simulation speed: 4x");
                UpdateSpeedDisplay();
                return;
            }
            if (key.Keycode == Key.Key3)
            {
                _simulationSpeed = SimulationSpeed.Fast16x;
                GD.Print("Simulation speed: 16x");
                UpdateSpeedDisplay();
                return;
            }
            if (key.Keycode == Key.Key4)
            {
                _simulationSpeed = SimulationSpeed.Fast64x;
                GD.Print("Simulation speed: 64x");
                UpdateSpeedDisplay();
                return;
            }
        }

        // Only process mouse input when in game mode
        if (_currentScreen != AppScreen.Game)
            return;

        if (@event is InputEventMouseButton mb)
        {
            var localPos = GetLocalMousePosition();
            var tileCoord = ScreenToTileCoord(localPos);

            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    if (BuildToolState.Mode == BuildToolMode.PlaceTerrain)
                    {
                        _isPaintingTerrain = true;
                        _lastPaintedTile = tileCoord;
                        TileCoord[] tilesToUpdate;
                        if (BuildToolState.SelectedTerrainDefId.HasValue)
                        {
                            tilesToUpdate = _sim.PaintTerrain(
                                tileCoord,
                                BuildToolState.SelectedTerrainDefId.Value,
                                BuildToolState.SelectedColorIndex
                            );
                            _soundManager?.PlayPaint();
                        }
                        else
                        {
                            tilesToUpdate = _sim.DeleteAtTile(tileCoord);
                            _soundManager?.PlayDelete();
                        }
                        SyncTiles(tilesToUpdate);
                        return;
                    }
                    if (
                        BuildToolState.Mode == BuildToolMode.FillSquare
                        || BuildToolState.Mode == BuildToolMode.OutlineSquare
                    )
                    {
                        _brushDragStart = tileCoord;
                        _brushDragCurrent = tileCoord;
                        QueueRedraw();
                        return;
                    }
                    if (BuildToolState.Mode == BuildToolMode.FloodFill)
                    {
                        TileCoord[] tilesToUpdate;
                        if (BuildToolState.SelectedTerrainDefId.HasValue)
                        {
                            tilesToUpdate = _sim.FloodFill(
                                tileCoord,
                                BuildToolState.SelectedTerrainDefId.Value,
                                BuildToolState.SelectedColorIndex
                            );
                            _soundManager?.PlayPaint();
                        }
                        else
                        {
                            tilesToUpdate = _sim.FloodDelete(tileCoord);
                            _soundManager?.PlayDelete();
                        }
                        SyncTiles(tilesToUpdate);
                        return;
                    }
                    if (
                        BuildToolState.Mode == BuildToolMode.PlaceBuilding
                        && BuildToolState.SelectedBuildingDefId.HasValue
                    )
                    {
                        try
                        {
                            _sim.CreateBuilding(
                                BuildToolState.SelectedBuildingDefId.Value,
                                tileCoord,
                                BuildToolState.SelectedColorIndex
                            );
                            _soundManager?.PlayBuild();
                        }
                        catch (System.InvalidOperationException)
                        {
                            GD.Print($"Cannot place building at {tileCoord}: tile occupied");
                        }
                        catch (System.ArgumentException ex)
                        {
                            GD.PrintErr($"Invalid building placement: {ex.Message}");
                        }
                        return;
                    }
                }
                else
                {
                    _isPaintingTerrain = false;
                    _lastPaintedTile = null;
                    if (
                        (
                            BuildToolState.Mode == BuildToolMode.FillSquare
                            || BuildToolState.Mode == BuildToolMode.OutlineSquare
                        )
                        && _brushDragStart.HasValue
                        && _brushDragCurrent.HasValue
                    )
                    {
                        if (BuildToolState.Mode == BuildToolMode.FillSquare)
                        {
                            if (BuildToolState.SelectedTerrainDefId.HasValue)
                            {
                                FillRectangle(
                                    _brushDragStart.Value,
                                    _brushDragCurrent.Value,
                                    BuildToolState.SelectedTerrainDefId.Value,
                                    BuildToolState.SelectedColorIndex
                                );
                            }
                            else
                            {
                                var tilesToUpdate = _sim.DeleteRectangle(
                                    _brushDragStart.Value,
                                    _brushDragCurrent.Value
                                );
                                SyncTiles(tilesToUpdate);
                            }
                        }
                        else if (BuildToolState.Mode == BuildToolMode.OutlineSquare)
                        {
                            if (BuildToolState.SelectedTerrainDefId.HasValue)
                            {
                                OutlineRectangle(
                                    _brushDragStart.Value,
                                    _brushDragCurrent.Value,
                                    BuildToolState.SelectedTerrainDefId.Value,
                                    BuildToolState.SelectedColorIndex
                                );
                            }
                            else
                            {
                                var tilesToUpdate = _sim.DeleteRectangleOutline(
                                    _brushDragStart.Value,
                                    _brushDragCurrent.Value
                                );
                                SyncTiles(tilesToUpdate);
                            }
                        }
                        _brushDragStart = null;
                        _brushDragCurrent = null;
                        QueueRedraw();
                        return;
                    }
                }
            }

            var clickedPawnId = FindPawnAtPosition(localPos);

            if (clickedPawnId.HasValue)
            {
                if (
                    _selectedPawnId.HasValue
                    && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var oldNode)
                )
                {
                    if (oldNode is PawnView oldPv)
                        oldPv.SetSelected(false);
                }

                _selectedPawnId = clickedPawnId;
                _selectedBuildingId = null;

                if (_pawnNodes.TryGetValue(_selectedPawnId.Value, out var newNode))
                {
                    if (newNode is PawnView newPv)
                        newPv.SetSelected(true);
                }
                return;
            }

            var clickedBuildingId = FindBuildingAtPosition(localPos);

            if (clickedBuildingId.HasValue)
            {
                if (
                    _selectedPawnId.HasValue
                    && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var oldPawnNode)
                )
                {
                    if (oldPawnNode is PawnView oldPv)
                        oldPv.SetSelected(false);
                }

                _selectedPawnId = null;
                _selectedBuildingId = clickedBuildingId;
                return;
            }

            if (
                _selectedPawnId.HasValue
                && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var pawnNode)
            )
            {
                if (pawnNode is PawnView pv)
                    pv.SetSelected(false);
            }
            _selectedPawnId = null;
            _selectedBuildingId = null;
            _debugPanel?.ClearSelection();
        }

        if (@event is InputEventMouseMotion)
        {
            if (_isPaintingTerrain && BuildToolState.Mode == BuildToolMode.PlaceTerrain)
            {
                var localPos = GetLocalMousePosition();
                var tileCoord = ScreenToTileCoord(localPos);

                // Skip if we're still on the same tile we just painted
                if (tileCoord == _lastPaintedTile)
                {
                    return;
                }

                _lastPaintedTile = tileCoord;
                TileCoord[] tilesToUpdate;
                if (BuildToolState.SelectedTerrainDefId.HasValue)
                {
                    tilesToUpdate = _sim.PaintTerrain(
                        tileCoord,
                        BuildToolState.SelectedTerrainDefId.Value,
                        BuildToolState.SelectedColorIndex
                    );
                    _soundManager?.PlayPaintTick();
                }
                else
                {
                    tilesToUpdate = _sim.DeleteAtTile(tileCoord);
                    _soundManager?.PlayPaintTick();
                }
                SyncTiles(tilesToUpdate);
                return;
            }
            if (
                (
                    BuildToolState.Mode == BuildToolMode.FillSquare
                    || BuildToolState.Mode == BuildToolMode.OutlineSquare
                ) && _brushDragStart.HasValue
            )
            {
                var localPos = GetLocalMousePosition();
                var tileCoord = ScreenToTileCoord(localPos);
                _brushDragCurrent = tileCoord;
                QueueRedraw();
                return;
            }
        }
    }

    private void OutlineRectangle(TileCoord start, TileCoord end, int terrainId, int colorIndex)
    {
        var paintedTiles = _sim.PaintRectangleOutline(start, end, terrainId, colorIndex);
        var tilesToUpdate = _sim.GetTilesWithNeighbors(paintedTiles);
        SyncTiles(tilesToUpdate);
    }

    private void FillRectangle(TileCoord start, TileCoord end, int terrainId, int colorIndex)
    {
        var paintedTiles = _sim.PaintRectangle(start, end, terrainId, colorIndex);
        var tilesToUpdate = _sim.GetTilesWithNeighbors(paintedTiles);
        SyncTiles(tilesToUpdate);
    }

    private TileCoord ScreenToTileCoord(Vector2 screenPos)
    {
        return new TileCoord(
            Mathf.FloorToInt(screenPos.X / RenderingConstants.RenderedTileSize),
            Mathf.FloorToInt(screenPos.Y / RenderingConstants.RenderedTileSize)
        );
    }

    private void InitializeAutoTileLayers()
    {
        foreach (var (terrainId, terrainDef) in _sim.Content.Terrains)
        {
            if (!terrainDef.IsAutotiling)
                continue;

            var texture = SpriteResourceManager.GetTexture(terrainDef.SpriteKey);
            if (texture == null)
            {
                GD.PushError(
                    $"Failed to load texture '{terrainDef.SpriteKey}' for autotiling terrain {terrainId}"
                );
                continue;
            }

            var layer = new ModulatableTileMapLayer
            {
                Name = $"{terrainDef.SpriteKey}TileMapLayer",
                TileSet = AutoTileSetBuilder.CreateAutoTileSet(
                    texture,
                    terrainDef.SpriteKey,
                    terrainDef.BlocksLight
                ),
                Scale = new Vector2(RenderingConstants.SpriteScale, RenderingConstants.SpriteScale),
            };

            if (terrainDef.BlocksLight)
            {
                layer.ZIndex = ZIndexConstants.TerrainBlockingAndPawns;
                layer.YSortEnabled = true;
                // TileMapLayer uses tile coordinates for y_sort_origin, not pixels. Each tile is 16x16 in the texture, scaled 2x to 32x32. MapToLocal returns center of tile, so to sort by bottom edge: offset from center (Y=8) to bottom (Y=16) = 8 pixels in local space
                layer.YSortOrigin = 4;
                _pawnsRoot.GetParent().CallDeferred(Node.MethodName.AddChild, layer);
            }
            else
            {
                layer.ZIndex = ZIndexConstants.TerrainNonBlocking;
                _tilesRoot.AddChild(layer);
            }

            _autoTileLayers[terrainId] = layer;
        }
    }

    private void InitializeTileNodes()
    {
        for (int x = 0; x < _sim.World.Width; x++)
        {
            for (int y = 0; y < _sim.World.Height; y++)
            {
                var coord = new TileCoord(x, y);
                var tileNode = new Node2D
                {
                    Position = new Vector2(
                        x * RenderingConstants.RenderedTileSize,
                        y * RenderingConstants.RenderedTileSize
                    ),
                    Name = $"Tile_{x}_{y}",
                    ZIndex = ZIndexConstants.TileNodes,
                };
                _tilesRoot.AddChild(tileNode);

                var baseLayer = new Node2D { Name = "BaseLayer", ZIndex = -1 };
                tileNode.AddChild(baseLayer);

                var baseTileSprite = new Sprite2D
                {
                    Name = "BaseTileSprite",
                    Position = new Vector2(
                        RenderingConstants.RenderedTileSize / 2,
                        RenderingConstants.RenderedTileSize / 2
                    ),
                    Centered = true,
                    Visible = false,
                    Scale = new Vector2(
                        RenderingConstants.SpriteScale,
                        RenderingConstants.SpriteScale
                    ),
                };
                baseLayer.AddChild(baseTileSprite);

                var overlayLayer = new Node2D { Name = "OverlayLayer", ZIndex = 0 };
                tileNode.AddChild(overlayLayer);

                var overlayTileSprite = new Sprite2D
                {
                    Name = "OverlayTileSprite",
                    Position = new Vector2(
                        RenderingConstants.RenderedTileSize / 2,
                        RenderingConstants.RenderedTileSize / 2
                    ),
                    Centered = true,
                    Visible = false,
                    Scale = new Vector2(
                        RenderingConstants.SpriteScale,
                        RenderingConstants.SpriteScale
                    ),
                };
                overlayLayer.AddChild(overlayTileSprite);

                _tileNodes[coord] = tileNode;
                _tileSprites[coord] = (baseTileSprite, overlayTileSprite);
            }
        }

        SyncTiles(_tileNodes.Keys.ToArray());
    }

    private void DrawHoverPreview(TileCoord coord)
    {
        var rect = new Rect2(
            coord.X * RenderingConstants.RenderedTileSize,
            coord.Y * RenderingConstants.RenderedTileSize,
            RenderingConstants.RenderedTileSize,
            RenderingConstants.RenderedTileSize
        );

        if (BuildToolState.Mode == BuildToolMode.PlaceTerrain)
        {
            if (BuildToolState.SelectedTerrainDefId.HasValue)
            {
                var color = _currentPalette[BuildToolState.SelectedColorIndex];
                color.A = 0.5f;
                DrawRect(rect, color, true);
            }
            else
            {
                var color = new Color(1.0f, 0.0f, 0.0f, 0.3f);
                DrawRect(rect, color, true);
            }
        }
        else if (
            (
                BuildToolState.Mode == BuildToolMode.FillSquare
                || BuildToolState.Mode == BuildToolMode.OutlineSquare
            )
            && _brushDragStart.HasValue
            && _brushDragCurrent.HasValue
        )
        {
            int x0 = Mathf.Min(_brushDragStart.Value.X, _brushDragCurrent.Value.X);
            int x1 = Mathf.Max(_brushDragStart.Value.X, _brushDragCurrent.Value.X);
            int y0 = Mathf.Min(_brushDragStart.Value.Y, _brushDragCurrent.Value.Y);
            int y1 = Mathf.Max(_brushDragStart.Value.Y, _brushDragCurrent.Value.Y);
            Color color;
            if (BuildToolState.SelectedTerrainDefId.HasValue)
            {
                color = _currentPalette[BuildToolState.SelectedColorIndex];
                color.A = 0.3f;
            }
            else
            {
                color = new Color(1.0f, 0.0f, 0.0f, 0.3f);
            }
            var previewRect = new Rect2(
                x0 * RenderingConstants.RenderedTileSize,
                y0 * RenderingConstants.RenderedTileSize,
                (x1 - x0 + 1) * RenderingConstants.RenderedTileSize,
                (y1 - y0 + 1) * RenderingConstants.RenderedTileSize
            );
            if (BuildToolState.Mode == BuildToolMode.FillSquare)
            {
                DrawRect(previewRect, color, true);
            }
            DrawRect(previewRect, Colors.White, false, 2f);
            return;
        }
        else if (BuildToolState.Mode == BuildToolMode.FloodFill)
        {
            if (BuildToolState.SelectedTerrainDefId.HasValue)
            {
                var color = _currentPalette[BuildToolState.SelectedColorIndex];
                color.A = 0.5f;
                DrawRect(rect, color, true);
            }
            else
            {
                var color = new Color(1.0f, 0.0f, 0.0f, 0.3f);
                DrawRect(rect, color, true);
            }
        }
        else if (
            BuildToolState.Mode == BuildToolMode.PlaceBuilding
            && BuildToolState.SelectedBuildingDefId.HasValue
        )
        {
            // Get building definition to determine its size
            if (
                _sim.Content.Buildings.TryGetValue(
                    BuildToolState.SelectedBuildingDefId.Value,
                    out var buildingDef
                )
            )
            {
                // Calculate all tiles that will be occupied
                var occupiedTiles = BuildingUtilities.GetOccupiedTiles(coord, buildingDef);

                var color = _currentPalette[BuildToolState.SelectedColorIndex];
                color.A = 0.5f;

                // Draw all occupied tiles for multi-tile buildings
                foreach (var tile in occupiedTiles)
                {
                    var tileRect = new Rect2(
                        tile.X * RenderingConstants.RenderedTileSize,
                        tile.Y * RenderingConstants.RenderedTileSize,
                        RenderingConstants.RenderedTileSize,
                        RenderingConstants.RenderedTileSize
                    );
                    DrawRect(tileRect, color, true);
                }
            }
        }

        if (
            !(
                BuildToolState.Mode == BuildToolMode.FillSquare
                && _brushDragStart.HasValue
                && _brushDragCurrent.HasValue
            )
        )
        {
            // For multi-tile buildings, draw borders around all occupied tiles
            if (
                BuildToolState.Mode == BuildToolMode.PlaceBuilding
                && BuildToolState.SelectedBuildingDefId.HasValue
                && _sim.Content.Buildings.TryGetValue(
                    BuildToolState.SelectedBuildingDefId.Value,
                    out var buildingDef2
                )
            )
            {
                var occupiedTiles = BuildingUtilities.GetOccupiedTiles(coord, buildingDef2);
                foreach (var tile in occupiedTiles)
                {
                    var tileRect = new Rect2(
                        tile.X * RenderingConstants.RenderedTileSize,
                        tile.Y * RenderingConstants.RenderedTileSize,
                        RenderingConstants.RenderedTileSize,
                        RenderingConstants.RenderedTileSize
                    );
                    DrawRect(tileRect, Colors.White, false, 2f);
                }
            }
            else
            {
                // For single-tile tools, draw single border
                DrawRect(rect, Colors.White, false, 2f);
            }
        }
    }

    private int? FindPawnAtPosition(Vector2 pos)
    {
        const float halfSize = PawnHitboxSize / 2f;

        foreach (var (id, node) in _pawnNodes)
        {
            var pawnPos = node.Position;
            bool hit =
                pos.X >= pawnPos.X - halfSize
                && pos.X <= pawnPos.X + halfSize
                && pos.Y >= pawnPos.Y - halfSize
                && pos.Y <= pawnPos.Y + halfSize;

            if (hit)
                return id;
        }
        return null;
    }

    private int? FindBuildingAtPosition(Vector2 pos)
    {
        const float halfSize = BuildingHitboxSize / 2f;

        foreach (var (id, node) in _buildingNodes)
        {
            // Get building definition to determine size
            var objSnapshot = _lastSnapshot?.Buildings.FirstOrDefault(o => o.Id.Value == id);
            if (objSnapshot == null)
                continue;

            if (
                !_sim.Content.Buildings.TryGetValue(objSnapshot.BuildingDefId, out var buildingDef3)
            )
                continue;

            // Calculate hitbox based on tile size
            // For a 2x2 building, the hitbox should cover 2x2 tiles
            var objPos = node.Position;

            // Expand hitbox for multi-tile buildings
            // The anchor is at tile center, need to extend to cover all tiles
            float rightExpand =
                (buildingDef3.TileSize - 1) * RenderingConstants.RenderedTileSize + halfSize;
            float downExpand =
                (buildingDef3.TileSize - 1) * RenderingConstants.RenderedTileSize + halfSize;

            bool hit =
                pos.X >= objPos.X - halfSize
                && pos.X <= objPos.X + rightExpand
                && pos.Y >= objPos.Y - halfSize
                && pos.Y <= objPos.Y + downExpand;

            if (hit)
                return id;
        }
        return null;
    }

    public override void _Draw()
    {
        if (_hoveredTile.HasValue && BuildToolState.Mode != BuildToolMode.Select)
        {
            DrawHoverPreview(_hoveredTile.Value);
        }

        if (!_debugMode)
            return;

        const float pawnHalf = PawnHitboxSize / 2f;
        foreach (var (id, node) in _pawnNodes)
        {
            var rect = new Rect2(
                node.Position.X - pawnHalf,
                node.Position.Y - pawnHalf,
                PawnHitboxSize,
                PawnHitboxSize
            );
            DrawRect(rect, Colors.Magenta, false, 2f);
        }

        // Draw occupied tiles for multi-tile buildings
        foreach (var (id, node) in _buildingNodes)
        {
            var objSnapshot = _lastSnapshot?.Buildings.FirstOrDefault(o => o.Id.Value == id);
            if (
                objSnapshot != null
                && _sim.Content.Buildings.TryGetValue(
                    objSnapshot.BuildingDefId,
                    out var buildingDef4
                )
            )
            {
                // Draw occupied tiles for multi-tile buildings
                var occupiedTiles = BuildingUtilities.GetOccupiedTiles(
                    new TileCoord(objSnapshot.X, objSnapshot.Y),
                    buildingDef4
                );

                foreach (var tile in occupiedTiles)
                {
                    var tileRect = new Rect2(
                        tile.X * RenderingConstants.RenderedTileSize,
                        tile.Y * RenderingConstants.RenderedTileSize,
                        RenderingConstants.RenderedTileSize,
                        RenderingConstants.RenderedTileSize
                    );
                    DrawRect(tileRect, Colors.Cyan, false, 2f);
                }
            }
        }

        if (_lastSnapshot != null)
        {
            foreach (var pawn in _lastSnapshot.Pawns)
            {
                var pawnCenter = new Vector2(
                    pawn.X * RenderingConstants.RenderedTileSize
                        + RenderingConstants.RenderedTileSize / 2,
                    pawn.Y * RenderingConstants.RenderedTileSize
                        + RenderingConstants.RenderedTileSize / 2
                );

                if (pawn.CurrentPath != null && pawn.CurrentPath.Count > 0)
                {
                    for (int i = pawn.PathIndex; i < pawn.CurrentPath.Count - 1; i++)
                    {
                        var from = pawn.CurrentPath[i];
                        var to = pawn.CurrentPath[i + 1];
                        var fromPos = new Vector2(
                            from.X * RenderingConstants.RenderedTileSize
                                + RenderingConstants.RenderedTileSize / 2,
                            from.Y * RenderingConstants.RenderedTileSize
                                + RenderingConstants.RenderedTileSize / 2
                        );
                        var toPos = new Vector2(
                            to.X * RenderingConstants.RenderedTileSize
                                + RenderingConstants.RenderedTileSize / 2,
                            to.Y * RenderingConstants.RenderedTileSize
                                + RenderingConstants.RenderedTileSize / 2
                        );
                        DrawLine(fromPos, toPos, Colors.Orange, 2f);
                    }

                    if (pawn.PathIndex < pawn.CurrentPath.Count)
                    {
                        var nextTile = pawn.CurrentPath[pawn.PathIndex];
                        var nextPos = new Vector2(
                            nextTile.X * RenderingConstants.RenderedTileSize
                                + RenderingConstants.RenderedTileSize / 2,
                            nextTile.Y * RenderingConstants.RenderedTileSize
                                + RenderingConstants.RenderedTileSize / 2
                        );
                        DrawLine(pawnCenter, nextPos, Colors.White, 2f);
                    }
                }

                if (pawn.TargetTile.HasValue)
                {
                    var targetRect = new Rect2(
                        pawn.TargetTile.Value.X * RenderingConstants.RenderedTileSize + 4,
                        pawn.TargetTile.Value.Y * RenderingConstants.RenderedTileSize + 4,
                        RenderingConstants.RenderedTileSize - 8,
                        RenderingConstants.RenderedTileSize - 8
                    );
                    DrawRect(targetRect, new Color(1, 0.5f, 0, 0.3f), true);
                    DrawRect(targetRect, Colors.Orange, false, 2f);
                }
            }
        }

        var mousePos = GetLocalMousePosition();
        DrawCircle(mousePos, 5f, Colors.Yellow);
    }

    private void UpdateInfoPanel(RenderSnapshot snapshot)
    {
        if (_debugPanel == null)
            return;

        if (!_selectedPawnId.HasValue)
            return;

        var pawn = snapshot.Pawns.FirstOrDefault(p => p.Id.Value == _selectedPawnId.Value);
        if (pawn == null)
        {
            _selectedPawnId = null;
            _debugPanel.ClearSelection();
            return;
        }

        var entityId = new EntityId(_selectedPawnId.Value);
        _sim.Entities.Needs.TryGetValue(entityId, out var needs);
        _sim.Entities.Buffs.TryGetValue(entityId, out var buffs);
        _debugPanel.ShowPawn(pawn, needs, buffs, _sim);
    }

    /// <summary>
    /// Synchronizes tile rendering state for the specified tile coordinates.
    /// Updates both sprite-based tiles and autotile layers.
    /// </summary>
    private void SyncTiles(TileCoord[] coords)
    {
        PrepareAutotileBatches();

        foreach (var coord in coords)
        {
            SyncSingleTile(coord);
        }

        ApplyAutotileBatches();
    }

    /// <summary>
    /// Prepares autotile batch collections for updates.
    /// </summary>
    private void PrepareAutotileBatches()
    {
        foreach (var (terrainId, _) in _autoTileLayers)
        {
            if (!_autotileUpdates.ContainsKey(terrainId))
            {
                _autotileUpdates[terrainId] = new List<(Vector2I, Color)>();
                _autotileClearCells[terrainId] = new List<Vector2I>();
            }
            else
            {
                _autotileUpdates[terrainId].Clear();
                _autotileClearCells[terrainId].Clear();
            }
        }
    }

    /// <summary>
    /// Synchronizes rendering state for a single tile.
    /// </summary>
    private void SyncSingleTile(TileCoord coord)
    {
        if (!_tileSprites.TryGetValue(coord, out var sprites))
            return;

        var (baseSprite, overlaySprite) = sprites;
        var tile = _sim.World.GetTile(coord);
        var tileMapCoord = new Vector2I(coord.X, coord.Y);

        // Get terrain definitions
        _sim.Content.Terrains.TryGetValue(tile.BaseTerrainTypeId, out var baseTerrainDef);
        TerrainDef? overlayTerrainDef = null;
        if (tile.OverlayTerrainTypeId.HasValue)
        {
            _sim.Content.Terrains.TryGetValue(
                tile.OverlayTerrainTypeId.Value,
                out overlayTerrainDef
            );
        }

        ProcessAutotileLayers(tile, baseTerrainDef, overlayTerrainDef, tileMapCoord);
        UpdateTerrainSprite(
            baseSprite,
            baseTerrainDef,
            tile.ColorIndex,
            tile.BaseVariantIndex,
            baseTerrainDef?.IsAutotiling ?? false
        );
        UpdateTerrainSprite(
            overlaySprite,
            overlayTerrainDef,
            tile.OverlayColorIndex,
            tile.OverlayVariantIndex,
            overlayTerrainDef?.IsAutotiling ?? false
        );
    }

    /// <summary>
    /// Processes autotile layers for a tile, adding it to active layers and marking all layers for clearing.
    /// Clearing all layers ensures SetCellsTerrainConnect properly recalculates terrain connections.
    /// </summary>
    private void ProcessAutotileLayers(
        Tile tile,
        TerrainDef? baseTerrainDef,
        TerrainDef? overlayTerrainDef,
        Vector2I tileMapCoord
    )
    {
        // Add to active autotile layers
        if (baseTerrainDef != null && baseTerrainDef.IsAutotiling)
        {
            var color = _currentPalette[tile.ColorIndex];
            _autotileUpdates[tile.BaseTerrainTypeId].Add((tileMapCoord, color));
        }

        if (
            overlayTerrainDef != null
            && overlayTerrainDef.IsAutotiling
            && tile.OverlayTerrainTypeId.HasValue
        )
        {
            var color = _currentPalette[tile.OverlayColorIndex];
            _autotileUpdates[tile.OverlayTerrainTypeId.Value].Add((tileMapCoord, color));
        }

        // Mark ALL layers for clearing to ensure SetCellsTerrainConnect properly recalculates
        foreach (var (terrainId, _) in _autoTileLayers)
        {
            _autotileClearCells[terrainId].Add(tileMapCoord);
        }
    }

    /// <summary>
    /// Updates a terrain sprite with texture, color, and variant information.
    /// </summary>
    private void UpdateTerrainSprite(
        Sprite2D sprite,
        TerrainDef? terrainDef,
        int colorIndex,
        int variantIndex,
        bool isAutotiling
    )
    {
        if (terrainDef == null || isAutotiling)
        {
            sprite.Visible = false;
            return;
        }

        var texture = SpriteResourceManager.GetTexture(terrainDef.SpriteKey);
        if (texture == null)
        {
            sprite.Visible = false;
            return;
        }

        sprite.Texture = texture;
        sprite.Modulate = _currentPalette[colorIndex];

        if (terrainDef.VariantCount > 1)
        {
            int atlasX =
                (variantIndex % RenderingConstants.VariantsPerRow)
                * RenderingConstants.SourceTileSize;
            int atlasY =
                (variantIndex / RenderingConstants.VariantsPerRow)
                * RenderingConstants.SourceTileSize;

            sprite.RegionEnabled = true;
            sprite.RegionRect = new Rect2(
                atlasX,
                atlasY,
                RenderingConstants.SourceTileSize,
                RenderingConstants.SourceTileSize
            );
        }
        else
        {
            sprite.RegionEnabled = false;
        }

        sprite.Visible = true;
    }

    /// <summary>
    /// Applies all batched autotile updates to their respective layers.
    /// Clears inactive cells first so SetCellsTerrainConnect can see which cells are empty.
    /// </summary>
    private void ApplyAutotileBatches()
    {
        foreach (var (terrainId, layer) in _autoTileLayers)
        {
            ClearInactiveCellsFromLayer(layer, terrainId);
            ApplyAutotileUpdatesToLayer(layer, terrainId);
        }
    }

    /// <summary>
    /// Applies batched updates to a single autotile layer.
    /// </summary>
    private void ApplyAutotileUpdatesToLayer(ModulatableTileMapLayer layer, int terrainId)
    {
        var updates = _autotileUpdates[terrainId];
        if (updates.Count == 0)
            return;

        var cellsArray = new Godot.Collections.Array<Vector2I>();
        foreach (var (coord, _) in updates)
        {
            cellsArray.Add(coord);
        }

        layer.SetCellsTerrainConnect(cellsArray, 0, 0, false);

        // Apply colors
        foreach (var (coord, color) in updates)
        {
            layer.SetTileColor(coord, color);
        }
    }

    /// <summary>
    /// Clears inactive cells from a single autotile layer.
    /// </summary>
    private void ClearInactiveCellsFromLayer(ModulatableTileMapLayer layer, int terrainId)
    {
        var clears = _autotileClearCells[terrainId];
        if (clears.Count == 0)
            return;

        foreach (var cell in clears)
        {
            layer.EraseCell(cell);
            layer.ClearTileColor(cell);
        }
    }

    /// <summary>
    /// Calculate the entry position for a pawn spawning at the given tile coordinates.
    /// Places the pawn one tile outside the nearest edge.
    /// </summary>
    private Vector2 CalculateEntryPosition(int tileX, int tileY)
    {
        var worldWidth = _sim.World.Width;
        var worldHeight = _sim.World.Height;

        var centerX =
            tileX * RenderingConstants.RenderedTileSize + RenderingConstants.RenderedTileSize / 2;
        var centerY =
            tileY * RenderingConstants.RenderedTileSize + RenderingConstants.RenderedTileSize / 2;

        bool onLeftEdge = tileX == 0;
        bool onRightEdge = tileX == worldWidth - 1;
        bool onTopEdge = tileY == 0;
        bool onBottomEdge = tileY == worldHeight - 1;

        if (onLeftEdge && onTopEdge)
            return new Vector2(
                centerX - RenderingConstants.RenderedTileSize,
                centerY - RenderingConstants.RenderedTileSize
            );
        if (onRightEdge && onTopEdge)
            return new Vector2(
                centerX + RenderingConstants.RenderedTileSize,
                centerY - RenderingConstants.RenderedTileSize
            );
        if (onLeftEdge && onBottomEdge)
            return new Vector2(
                centerX - RenderingConstants.RenderedTileSize,
                centerY + RenderingConstants.RenderedTileSize
            );
        if (onRightEdge && onBottomEdge)
            return new Vector2(
                centerX + RenderingConstants.RenderedTileSize,
                centerY + RenderingConstants.RenderedTileSize
            );

        if (onLeftEdge)
            return new Vector2(centerX - RenderingConstants.RenderedTileSize, centerY);
        if (onRightEdge)
            return new Vector2(centerX + RenderingConstants.RenderedTileSize, centerY);
        if (onTopEdge)
            return new Vector2(centerX, centerY - RenderingConstants.RenderedTileSize);
        if (onBottomEdge)
            return new Vector2(centerX, centerY + RenderingConstants.RenderedTileSize);

        return new Vector2(centerX, centerY);
    }

    private void SyncPawns(RenderSnapshot snapshot)
    {
        _activeIds.Clear();

        foreach (var pawn in snapshot.Pawns)
        {
            _activeIds.Add(pawn.Id.Value);

            bool isNewPawn = !_pawnNodes.TryGetValue(pawn.Id.Value, out var node);

            if (isNewPawn)
            {
                node = PawnScene.Instantiate<Node2D>();
                _pawnsRoot.GetParent().AddChild(node);
                _pawnNodes.Add(pawn.Id.Value, node);

                if (node is PawnView pawnView && _characterSprite != null)
                {
                    pawnView.InitializeWithSprite(
                        _characterSprite,
                        _idleSprite,
                        _axeSprite,
                        _pickaxeSprite,
                        _lookDownSprite,
                        _lookUpSprite
                    );
                }
            }

            var targetPosition = new Vector2(
                pawn.X * RenderingConstants.RenderedTileSize
                    + RenderingConstants.RenderedTileSize / 2,
                pawn.Y * RenderingConstants.RenderedTileSize
                    + RenderingConstants.RenderedTileSize / 2
            );

            if (node is PawnView pv)
            {
                if (isNewPawn)
                {
                    var entryPosition = CalculateEntryPosition(pawn.X, pawn.Y);
                    pv.SetInitialPosition(entryPosition);
                }

                pv.SetTargetPosition(targetPosition);
                pv.SetCurrentAnimation(pawn.Animation);
                pv.SetMood(pawn.Mood);
                pv.SetSelected(pawn.Id.Value == _selectedPawnId);
                pv.SetExpression(pawn.Expression, pawn.ExpressionIconDefId, _sim.Content);

                // Hide pawn when using or working at a building (classic RTS style)
                bool isInsideBuilding =
                    pawn.CurrentActionType == SimGame.Core.ActionType.UseBuilding
                    || pawn.CurrentActionType == SimGame.Core.ActionType.Work
                    || pawn.CurrentActionType == SimGame.Core.ActionType.PickUp
                    || pawn.CurrentActionType == SimGame.Core.ActionType.DropOff;
                pv.Visible = !isInsideBuilding;
            }
        }

        _idsToRemove.Clear();
        foreach (var id in _pawnNodes.Keys)
        {
            if (!_activeIds.Contains(id))
                _idsToRemove.Add(id);
        }
        foreach (var id in _idsToRemove)
        {
            _pawnNodes[id].QueueFree();
            _pawnNodes.Remove(id);

            if (_selectedPawnId == id)
            {
                _selectedPawnId = null;
                _debugPanel?.ClearSelection();
            }
        }
    }

    private void SyncBuildings(RenderSnapshot snapshot)
    {
        _activeIds.Clear();

        foreach (var obj in snapshot.Buildings)
        {
            _activeIds.Add(obj.Id.Value);

            if (!_buildingNodes.TryGetValue(obj.Id.Value, out var node))
            {
                node = BuildingScene?.Instantiate<Node2D>() ?? new Node2D();
                node.ZIndex = ZIndexConstants.Buildings;
                _buildingsRoot.GetParent().AddChild(node);
                _buildingNodes.Add(obj.Id.Value, node);

                if (node is BuildingView ovInit)
                {
                    if (_sim.Content.Buildings.TryGetValue(obj.BuildingDefId, out var buildingDef6))
                    {
                        var texture = SpriteResourceManager.GetTexture(buildingDef6.SpriteKey);
                        if (texture != null)
                        {
                            ovInit.InitializeWithSprite(texture, buildingDef6.TileSize);
                        }
                    }
                }
            }

            node.Position = new Vector2(
                obj.X * RenderingConstants.RenderedTileSize
                    + RenderingConstants.RenderedTileSize / 2,
                obj.Y * RenderingConstants.RenderedTileSize
                    + RenderingConstants.RenderedTileSize / 2
            );

            if (node is BuildingView ov)
            {
                ov.SetBuildingInfo(obj.Name, obj.InUse, obj.ColorIndex, _currentPalette);
            }
        }

        _idsToRemove.Clear();
        foreach (var id in _buildingNodes.Keys)
        {
            if (!_activeIds.Contains(id))
                _idsToRemove.Add(id);
        }
        foreach (var id in _idsToRemove)
        {
            _buildingNodes[id].QueueFree();
            _buildingNodes.Remove(id);

            if (_selectedBuildingId == id)
            {
                _selectedBuildingId = null;
                _debugPanel?.ClearSelection();
            }
        }
    }

    private void UpdateBuildingInfoPanel(RenderSnapshot snapshot)
    {
        if (_debugPanel == null)
            return;

        if (!_selectedBuildingId.HasValue)
            return;

        var obj = snapshot.Buildings.FirstOrDefault(o => o.Id.Value == _selectedBuildingId.Value);
        if (obj == null)
        {
            _selectedBuildingId = null;
            _debugPanel.ClearSelection();
            return;
        }

        _debugPanel.ShowBuilding(obj, _sim);
    }

    private void UpdateTimeDisplay(RenderSnapshot snapshot)
    {
        if (_debugPanel == null)
            return;

        // Always update time - the panel decides what to show based on mode
        _debugPanel.UpdateTime(snapshot.Time);

        // If nothing selected, ensure we're in time mode
        if (!_selectedPawnId.HasValue && !_selectedBuildingId.HasValue)
        {
            _debugPanel.ClearSelection();
        }
    }

    private void UpdateSpeedDisplay()
    {
        if (_debugPanel == null)
            return;

        string speedText = _simulationSpeed switch
        {
            SimulationSpeed.Paused => "PAUSED",
            SimulationSpeed.Normal => "1x",
            SimulationSpeed.Fast4x => "4x",
            SimulationSpeed.Fast16x => "16x",
            SimulationSpeed.Fast64x => "64x",
            _ => "1x",
        };

        _debugPanel.UpdateSpeed(speedText);
    }

    private void UpdateNightOverlay(RenderSnapshot snapshot)
    {
        float timeOfDay = snapshot.Time.DayFraction;

        if (_crtShaderController != null)
        {
            _crtShaderController.SetTimeOfDay(timeOfDay);
        }

        if (_shadowShaderMaterial != null)
        {
            float sunAngle = (timeOfDay - 0.5f) * 180f;
            _shadowShaderMaterial.SetShaderParameter("sun_angle", sunAngle + 90f);

            float sunElevation = Mathf.Max(0.0f, Mathf.Cos(sunAngle * Mathf.Pi / 180f));

            float baseShadowDistance = 16.0f;
            float shadowMultiplier = 1.0f + (1.0f - sunElevation) * 4.0f;
            float shadowDistance = baseShadowDistance * shadowMultiplier;

            _shadowShaderMaterial.SetShaderParameter("max_shadow_distance", shadowDistance);

            float baseShadowAlpha = 0.3f;
            // Square the elevation to make shadows fade faster
            float shadowFade = sunElevation * sunElevation;
            float shadowAlpha = baseShadowAlpha * shadowFade;
            var shadowColor = new Color(0.0f, 0.0f, 0.0f, shadowAlpha);

            _shadowShaderMaterial.SetShaderParameter("shadow_color", shadowColor);
        }
    }

    /// <summary>
    /// Called by MusicManager when a music file finishes playing.
    /// Notifies the Core simulation's ThemeSystem to transition themes.
    /// </summary>
    public void OnMusicFinished()
    {
        GD.Print("[GameRoot] OnMusicFinished called");

        if (_sim.ThemeSystem != null)
        {
            GD.Print("[GameRoot] Notifying ThemeSystem that music finished");
            _sim.ThemeSystem.OnMusicFinished();
        }
        else
        {
            GD.PrintErr("[GameRoot] ERROR: ThemeSystem is null!");
        }
    }

    /// <summary>
    /// Get the path to the content folder, handling both editor and exported builds.
    /// </summary>
    private static string GetContentPath()
    {
        // In editor, use res:// which points to project root
        if (OS.HasFeature("editor"))
        {
            return ProjectSettings.GlobalizePath("res://content");
        }

        // In exported build, find content relative to executable
        var exePath = OS.GetExecutablePath();
        var exeDir = Path.GetDirectoryName(exePath) ?? "";

        if (OS.HasFeature("macos"))
        {
            // macOS: executable is at SimGame.app/Contents/MacOS/SimGame
            // content is at SimGame.app/Contents/Resources/content
            var resourcesPath = Path.Combine(exeDir, "..", "Resources", "content");
            return Path.GetFullPath(resourcesPath);
        }
        else
        {
            // Windows/Linux: content is next to executable
            return Path.Combine(exeDir, "content");
        }
    }
}
