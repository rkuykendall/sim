using System.Collections.Generic;
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
    private const int TileSize = 32;
    private const float PawnHitboxSize = 24f;
    private const float ObjectHitboxSize = 28f;

    private readonly Dictionary<int, Node2D> _pawnNodes = new();
    private readonly Dictionary<int, Node2D> _objectNodes = new();
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
    private int? _selectedObjectId = null;
    private bool _debugMode = false;
    private TileCoord? _hoveredTile = null;
    private RenderSnapshot? _lastSnapshot = null;

    // Track mouse button state for drag painting
    private bool _isPaintingTerrain = false;

    [Export]
    public PackedScene PawnScene { get; set; } = null!;

    [Export]
    public PackedScene ObjectScene { get; set; } = null!;

    [Export]
    public NodePath PawnsRootPath { get; set; } = ".";

    [Export]
    public NodePath ObjectsRootPath { get; set; } = ".";

    [Export]
    public NodePath TilesRootPath { get; set; } = ".";

    [Export]
    public NodePath InfoPanelPath { get; set; } = "";

    [Export]
    public NodePath ObjectInfoPanelPath { get; set; } = "";

    [Export]
    public NodePath TimeDisplayPath { get; set; } = "";

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

    private Node2D _pawnsRoot = null!;
    private Node2D _objectsRoot = null!;
    private Node2D _tilesRoot = null!;
    private Dictionary<int, ModulatableTileMapLayer> _autoTileLayers = new();
    private PawnInfoPanel? _infoPanel;
    private ObjectInfoPanel? _objectInfoPanel;
    private TimeDisplay? _timeDisplay;
    private ColorRect? _shadowRect;
    private ShaderMaterial? _shadowShaderMaterial;
    private CRTShaderController? _crtShaderController;
    private Camera2D? _camera;
    private CanvasLayer? _uiLayer;
    private BuildToolbar? _toolbar;

    private bool DebugMode => _debugMode;

    public override void _Ready()
    {
        // Load content from Lua files and create simulation with it
        var contentPath = ProjectSettings.GlobalizePath("res://content");
        var content = ContentLoader.LoadAll(contentPath);

        _sim = new Simulation(content);
        _tickDelta = 1f / Simulation.TickRate;

        // Load character sprites
        _characterSprite = SpriteResourceManager.GetTexture("character_walk");
        _idleSprite = SpriteResourceManager.GetTexture("character_idle");
        _axeSprite = SpriteResourceManager.GetTexture("character_axe");
        _pickaxeSprite = SpriteResourceManager.GetTexture("character_pickaxe");
        _lookDownSprite = SpriteResourceManager.GetTexture("character_look_down");
        _lookUpSprite = SpriteResourceManager.GetTexture("character_look_up");

        _pawnsRoot = GetNode<Node2D>(PawnsRootPath);
        _objectsRoot = GetNode<Node2D>(ObjectsRootPath);
        _tilesRoot = GetNode<Node2D>(TilesRootPath);

        // Initialize TileMapLayers for all autotiling terrains
        InitializeAutoTileLayers();

        // Initialize palette immediately so tiles render with correct colors
        var initialSnapshot = _sim.CreateRenderSnapshot();
        _currentPalette = GameColorPalette.ToGodotColors(initialSnapshot.ColorPalette);
        // Don't set _currentPaletteId yet - let _Process() call UpdatePalette on first frame
        // when the ColorPickerModal is actually ready

        // Create tile visualization nodes
        InitializeTileNodes();

        // Do an initial sync of all tiles to show the world
        for (int x = 0; x < _sim.World.Width; x++)
        {
            for (int y = 0; y < _sim.World.Height; y++)
            {
                UpdateSingleTile(new TileCoord(x, y));
            }
        }

        if (!string.IsNullOrEmpty(InfoPanelPath))
            _infoPanel = GetNodeOrNull<PawnInfoPanel>(InfoPanelPath);
        if (!string.IsNullOrEmpty(ObjectInfoPanelPath))
            _objectInfoPanel = GetNodeOrNull<ObjectInfoPanel>(ObjectInfoPanelPath);
        if (!string.IsNullOrEmpty(TimeDisplayPath))
            _timeDisplay = GetNodeOrNull<TimeDisplay>(TimeDisplayPath);
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

                // Create a hard shadow gradient texture
                // For hard shadows: sharp cutoff at 90% (0.0-0.9 = full shadow, 0.9-1.0 = no shadow)
                var gradient = new Gradient();
                gradient.AddPoint(0.0f, Colors.White); // Alpha 0.0 position = white (full shadow)
                gradient.AddPoint(0.9f, Colors.White); // Alpha 0.9 position = white (full shadow)
                gradient.AddPoint(1.0f, Colors.Black); // Alpha 1.0 position = black (no shadow)

                var gradientTexture = new GradientTexture2D
                {
                    Gradient = gradient,
                    Width = 256,
                    Height = 1,
                };

                _shadowShaderMaterial.SetShaderParameter("shadow_gradient", gradientTexture);

                GD.Print("[DEBUG] SDF shadow system initialized with hard shadow gradient");
            }
        }
        if (!string.IsNullOrEmpty(CameraPath))
        {
            _camera = GetNodeOrNull<Camera2D>(CameraPath);
            if (_camera != null)
            {
                // Center camera over the world grid
                var worldCenterX = (_sim.World.Width * TileSize) / 2f;
                var worldCenterY = (_sim.World.Height * TileSize) / 2f;
                _camera.Position = new Vector2(worldCenterX, worldCenterY);
            }
        }
        if (!string.IsNullOrEmpty(UILayerPath))
            _uiLayer = GetNodeOrNull<CanvasLayer>(UILayerPath);

        // Initialize build toolbar
        if (!string.IsNullOrEmpty(ToolbarPath))
        {
            _toolbar = GetNodeOrNull<BuildToolbar>(ToolbarPath);
            _toolbar?.Initialize(_sim.Content, DebugMode);
            // Don't call UpdatePalette here - modal isn't ready yet
            // _Process() will call it on first frame
        }
    }

    public override void _Process(double delta)
    {
        _accumulator += (float)delta;

        while (_accumulator >= _tickDelta)
        {
            _sim.Tick();
            _accumulator -= _tickDelta;
        }

        var snapshot = _sim.CreateRenderSnapshot();
        _lastSnapshot = snapshot;

        // Update current palette from snapshot (only when it changes)
        if (_sim.SelectedPaletteId != _currentPaletteId)
        {
            _currentPalette = GameColorPalette.ToGodotColors(snapshot.ColorPalette);
            _currentPaletteId = _sim.SelectedPaletteId;
            _toolbar?.UpdatePalette(_currentPalette);
        }

        // Note: SyncTiles() removed from main loop for performance
        // Tiles are now updated only when they change (via UpdateTileAndNeighbors after PaintTerrain)
        SyncPawns(snapshot);
        SyncObjects(snapshot);
        UpdateInfoPanel(snapshot);
        UpdateObjectInfoPanel(snapshot);
        UpdateTimeDisplay(snapshot);
        UpdateNightOverlay(snapshot);

        // Update hovered tile for preview
        var mousePos = GetLocalMousePosition();
        _hoveredTile = ScreenToTileCoord(mousePos);

        // Redraw debug visuals and hover preview
        if (_debugMode || BuildToolState.Mode != BuildToolMode.Select)
            QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed)
        {
            // Toggle debug mode with F3
            if (key.Keycode == Key.F3)
            {
                _debugMode = !_debugMode;
                GD.Print($"Debug mode: {_debugMode}");
                _toolbar?.SetDebugMode(_debugMode);
                QueueRedraw();
                return;
            }
        }

        if (@event is InputEventMouseButton mb)
        {
            var localPos = GetLocalMousePosition();
            var tileCoord = ScreenToTileCoord(localPos);

            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    // Start painting terrain on mouse down
                    if (
                        BuildToolState.Mode == BuildToolMode.PlaceTerrain
                        && BuildToolState.SelectedTerrainDefId.HasValue
                    )
                    {
                        _isPaintingTerrain = true;
                        _sim.PaintTerrain(
                            tileCoord.X,
                            tileCoord.Y,
                            BuildToolState.SelectedTerrainDefId.Value,
                            BuildToolState.SelectedColorIndex
                        );
                        UpdateTileAndNeighbors(tileCoord);
                        return;
                    }
                    // Start fill/outline square drag
                    if (
                        (
                            BuildToolState.Mode == BuildToolMode.FillSquare
                            || BuildToolState.Mode == BuildToolMode.OutlineSquare
                        ) && BuildToolState.SelectedTerrainDefId.HasValue
                    )
                    {
                        _brushDragStart = tileCoord;
                        _brushDragCurrent = tileCoord;
                        QueueRedraw();
                        return;
                    }
                    // Flood fill brush
                    if (
                        BuildToolState.Mode == BuildToolMode.FloodFill
                        && BuildToolState.SelectedTerrainDefId.HasValue
                    )
                    {
                        _sim.FloodFill(
                            tileCoord.X,
                            tileCoord.Y,
                            BuildToolState.SelectedTerrainDefId.Value,
                            BuildToolState.SelectedColorIndex
                        );
                        // Update all tiles since flood fill can affect many tiles
                        SyncTiles();
                        return;
                    }
                }
                else
                {
                    // Stop painting terrain on mouse up
                    _isPaintingTerrain = false;
                    // Complete fill/outline square drag
                    if (
                        (
                            BuildToolState.Mode == BuildToolMode.FillSquare
                            || BuildToolState.Mode == BuildToolMode.OutlineSquare
                        )
                        && BuildToolState.SelectedTerrainDefId.HasValue
                        && _brushDragStart.HasValue
                        && _brushDragCurrent.HasValue
                    )
                    {
                        if (BuildToolState.Mode == BuildToolMode.FillSquare)
                        {
                            FillRectangle(
                                _brushDragStart.Value,
                                _brushDragCurrent.Value,
                                BuildToolState.SelectedTerrainDefId.Value,
                                BuildToolState.SelectedColorIndex
                            );
                        }
                        else if (BuildToolState.Mode == BuildToolMode.OutlineSquare)
                        {
                            OutlineRectangle(
                                _brushDragStart.Value,
                                _brushDragCurrent.Value,
                                BuildToolState.SelectedTerrainDefId.Value,
                                BuildToolState.SelectedColorIndex
                            );
                        }
                        _brushDragStart = null;
                        _brushDragCurrent = null;
                        QueueRedraw();
                        return;
                    }
                }
            }

            // Handle object placement and selection as before
            if (
                BuildToolState.Mode == BuildToolMode.PlaceObject
                && BuildToolState.SelectedObjectDefId.HasValue
            )
            {
                try
                {
                    _sim.CreateObject(
                        BuildToolState.SelectedObjectDefId.Value,
                        tileCoord.X,
                        tileCoord.Y,
                        BuildToolState.SelectedColorIndex
                    );
                }
                catch (System.InvalidOperationException)
                {
                    // Tile occupied, show error feedback (future: visual shake/red flash)
                    GD.Print(
                        $"Cannot place object at ({tileCoord.X}, {tileCoord.Y}): tile occupied"
                    );
                }
                catch (System.ArgumentException ex)
                {
                    GD.PrintErr($"Invalid object placement: {ex.Message}");
                }
                return; // Consume event
            }

            if (BuildToolState.Mode == BuildToolMode.Delete)
            {
                _sim.DeleteAtTile(tileCoord.X, tileCoord.Y);
                UpdateTileAndNeighbors(tileCoord);
                return; // Consume event
            }

            // Select mode: try to click a pawn first
            var clickedPawnId = FindPawnAtPosition(localPos);

            if (clickedPawnId.HasValue)
            {
                // Deselect old pawn
                if (
                    _selectedPawnId.HasValue
                    && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var oldNode)
                )
                {
                    if (oldNode is PawnView oldPv)
                        oldPv.SetSelected(false);
                }

                _selectedPawnId = clickedPawnId;
                _selectedObjectId = null;
                _objectInfoPanel?.Hide();

                if (_pawnNodes.TryGetValue(_selectedPawnId.Value, out var newNode))
                {
                    if (newNode is PawnView newPv)
                        newPv.SetSelected(true);
                }
                return;
            }

            // Try to click an object
            var clickedObjectId = FindObjectAtPosition(localPos);

            if (clickedObjectId.HasValue)
            {
                // Deselect pawn if one was selected
                if (
                    _selectedPawnId.HasValue
                    && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var oldPawnNode)
                )
                {
                    if (oldPawnNode is PawnView oldPv)
                        oldPv.SetSelected(false);
                }

                _selectedPawnId = null;
                _selectedObjectId = clickedObjectId;
                _infoPanel?.Hide();
                return;
            }

            // Clicked empty space - deselect everything
            if (
                _selectedPawnId.HasValue
                && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var pawnNode)
            )
            {
                if (pawnNode is PawnView pv)
                    pv.SetSelected(false);
            }
            _selectedPawnId = null;
            _selectedObjectId = null;
            _infoPanel?.Hide();
            _objectInfoPanel?.Hide();
        }

        // Handle mouse motion for drag painting (outside mouse button handler)
        if (@event is InputEventMouseMotion)
        {
            if (
                _isPaintingTerrain
                && BuildToolState.Mode == BuildToolMode.PlaceTerrain
                && BuildToolState.SelectedTerrainDefId.HasValue
            )
            {
                var localPos = GetLocalMousePosition();
                var tileCoord = ScreenToTileCoord(localPos);
                _sim.PaintTerrain(
                    tileCoord.X,
                    tileCoord.Y,
                    BuildToolState.SelectedTerrainDefId.Value,
                    BuildToolState.SelectedColorIndex
                );
                UpdateTileAndNeighbors(tileCoord);
                return;
            }
            if (
                (
                    BuildToolState.Mode == BuildToolMode.FillSquare
                    || BuildToolState.Mode == BuildToolMode.OutlineSquare
                )
                && BuildToolState.SelectedTerrainDefId.HasValue
                && _brushDragStart.HasValue
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

    // Paint only the outline of a rectangle of tiles
    private void OutlineRectangle(TileCoord start, TileCoord end, int terrainId, int colorIndex)
    {
        int x0 = Mathf.Min(start.X, end.X);
        int x1 = Mathf.Max(start.X, end.X);
        int y0 = Mathf.Min(start.Y, end.Y);
        int y1 = Mathf.Max(start.Y, end.Y);
        for (int x = x0; x <= x1; x++)
        {
            _sim.PaintTerrain(x, y0, terrainId, colorIndex);
            _sim.PaintTerrain(x, y1, terrainId, colorIndex);
            UpdateTileAndNeighbors(new TileCoord(x, y0));
            UpdateTileAndNeighbors(new TileCoord(x, y1));
        }
        for (int y = y0 + 1; y < y1; y++)
        {
            _sim.PaintTerrain(x0, y, terrainId, colorIndex);
            _sim.PaintTerrain(x1, y, terrainId, colorIndex);
            UpdateTileAndNeighbors(new TileCoord(x0, y));
            UpdateTileAndNeighbors(new TileCoord(x1, y));
        }
    }

    // Fill a rectangle of tiles with the selected terrain/color
    private void FillRectangle(TileCoord start, TileCoord end, int terrainId, int colorIndex)
    {
        int x0 = Mathf.Min(start.X, end.X);
        int x1 = Mathf.Max(start.X, end.X);
        int y0 = Mathf.Min(start.Y, end.Y);
        int y1 = Mathf.Max(start.Y, end.Y);
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                _sim.PaintTerrain(x, y, terrainId, colorIndex);
                UpdateTileAndNeighbors(new TileCoord(x, y));
            }
        }
    }

    private TileCoord ScreenToTileCoord(Vector2 screenPos)
    {
        return new TileCoord(
            Mathf.FloorToInt(screenPos.X / TileSize),
            Mathf.FloorToInt(screenPos.Y / TileSize)
        );
    }

    private void InitializeAutoTileLayers()
    {
        // Create TileMapLayers for all autotiling terrains
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
                Scale = new Vector2(2, 2),
            };

            // Walls need to y-sort with pawns/objects, so add them to Main as siblings
            // Other autotile layers (water, etc.) stay in Tiles at z=-10
            if (terrainDef.BlocksLight)
            {
                layer.ZIndex = 1; // Same z-index as pawns for y-sorting (above objects at z=0)
                layer.YSortEnabled = true; // Enable y-sorting
                // TileMapLayer uses tile coordinates for y_sort_origin, not pixels
                // Each tile is 16x16 in the texture, scaled 2x to 32x32
                // MapToLocal returns center of tile, so to sort by bottom edge:
                // offset from center (Y=8) to bottom (Y=16) = 8 pixels in local space
                layer.YSortOrigin = 4; // Sort by bottom edge (offset from tile center)
                // Add to the same parent as Pawns/Objects/Tiles (the Main node)
                // Use CallDeferred to add after scene tree is ready
                _pawnsRoot.GetParent().CallDeferred(Node.MethodName.AddChild, layer);
            }
            else
            {
                layer.ZIndex = 0; // Relative to Tiles parent (-10) = absolute -10
                _tilesRoot.AddChild(layer); // Add to Tiles (background layer)
            }

            _autoTileLayers[terrainId] = layer;
        }
    }

    private void InitializeTileNodes()
    {
        // Create display tiles for standard grid system
        // Display grid matches world grid dimensions (Width x Height)
        // Display tiles are positioned at tile origins (standard grid alignment)
        for (int x = 0; x < _sim.World.Width; x++)
        {
            for (int y = 0; y < _sim.World.Height; y++)
            {
                var coord = new TileCoord(x, y);
                var tileNode = new Node2D
                {
                    Position = new Vector2(x * TileSize, y * TileSize),
                    Name = $"Tile_{x}_{y}",
                    ZIndex = -10,
                };
                _tilesRoot.AddChild(tileNode);

                // Base layer for foundation terrains (flat, wood floor)
                var baseLayer = new Node2D { Name = "BaseLayer", ZIndex = -1 };
                tileNode.AddChild(baseLayer);

                // Sprite for base layer textures (16x16 scaled to 32x32)
                var baseTileSprite = new Sprite2D
                {
                    Name = "BaseTileSprite",
                    Position = new Vector2(TileSize / 2, TileSize / 2), // Center on tile
                    Centered = true,
                    Visible = false,
                    Scale = new Vector2(2, 2), // Scale 16x16 texture to 32x32
                };
                baseLayer.AddChild(baseTileSprite);

                // Overlay layer for decorative terrains (grass, dirt, etc.)
                var overlayLayer = new Node2D { Name = "OverlayLayer", ZIndex = 0 };
                tileNode.AddChild(overlayLayer);

                // Sprite for overlay layer textures (16x16 scaled to 32x32)
                var overlayTileSprite = new Sprite2D
                {
                    Name = "OverlayTileSprite",
                    Position = new Vector2(TileSize / 2, TileSize / 2), // Center on tile
                    Centered = true,
                    Visible = false,
                    Scale = new Vector2(2, 2), // Scale 16x16 texture to 32x32
                };
                overlayLayer.AddChild(overlayTileSprite);

                _tileNodes[coord] = tileNode;
            }
        }

        // Update all tiles to calculate autotile variants for paths
        SyncTiles();
    }

    private void DrawHoverPreview(TileCoord coord)
    {
        var rect = new Rect2(coord.X * TileSize, coord.Y * TileSize, TileSize, TileSize);

        // Draw preview based on mode
        if (
            BuildToolState.Mode == BuildToolMode.PlaceTerrain
            && BuildToolState.SelectedTerrainDefId.HasValue
        )
        {
            var color = _currentPalette[BuildToolState.SelectedColorIndex];
            color.A = 0.5f; // Semi-transparent
            DrawRect(rect, color, true);
        }
        else if (
            (
                BuildToolState.Mode == BuildToolMode.FillSquare
                || BuildToolState.Mode == BuildToolMode.OutlineSquare
            )
            && BuildToolState.SelectedTerrainDefId.HasValue
            && _brushDragStart.HasValue
            && _brushDragCurrent.HasValue
        )
        {
            // Draw preview rectangle for fill or outline
            int x0 = Mathf.Min(_brushDragStart.Value.X, _brushDragCurrent.Value.X);
            int x1 = Mathf.Max(_brushDragStart.Value.X, _brushDragCurrent.Value.X);
            int y0 = Mathf.Min(_brushDragStart.Value.Y, _brushDragCurrent.Value.Y);
            int y1 = Mathf.Max(_brushDragStart.Value.Y, _brushDragCurrent.Value.Y);
            var color = _currentPalette[BuildToolState.SelectedColorIndex];
            color.A = 0.3f;
            var previewRect = new Rect2(
                x0 * TileSize,
                y0 * TileSize,
                (x1 - x0 + 1) * TileSize,
                (y1 - y0 + 1) * TileSize
            );
            if (BuildToolState.Mode == BuildToolMode.FillSquare)
            {
                DrawRect(previewRect, color, true);
            }
            DrawRect(previewRect, Colors.White, false, 2f);
            return;
        }
        else if (
            BuildToolState.Mode == BuildToolMode.PlaceObject
            && BuildToolState.SelectedObjectDefId.HasValue
        )
        {
            var color = _currentPalette[BuildToolState.SelectedColorIndex];
            color.A = 0.5f; // Semi-transparent
            DrawRect(rect, color, true);
        }
        else if (BuildToolState.Mode == BuildToolMode.Delete)
        {
            var color = new Color(1.0f, 0.0f, 0.0f, 0.3f); // Red semi-transparent
            DrawRect(rect, color, true);
        }

        // Always draw outline around hovered tile (unless fill preview is active)
        if (
            !(
                BuildToolState.Mode == BuildToolMode.FillSquare
                && _brushDragStart.HasValue
                && _brushDragCurrent.HasValue
            )
        )
        {
            DrawRect(rect, Colors.White, false, 2f);
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

    private int? FindObjectAtPosition(Vector2 pos)
    {
        const float halfSize = ObjectHitboxSize / 2f;

        foreach (var (id, node) in _objectNodes)
        {
            var objPos = node.Position;
            bool hit =
                pos.X >= objPos.X - halfSize
                && pos.X <= objPos.X + halfSize
                && pos.Y >= objPos.Y - halfSize
                && pos.Y <= objPos.Y + halfSize;

            if (hit)
                return id;
        }
        return null;
    }

    public override void _Draw()
    {
        // Draw hover preview for build tools
        if (_hoveredTile.HasValue && BuildToolState.Mode != BuildToolMode.Select)
        {
            DrawHoverPreview(_hoveredTile.Value);
        }

        if (!_debugMode)
            return;

        // Draw hitboxes for all pawns
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

        // Draw hitboxes for all objects
        const float objHalf = ObjectHitboxSize / 2f;
        foreach (var (id, node) in _objectNodes)
        {
            var rect = new Rect2(
                node.Position.X - objHalf,
                node.Position.Y - objHalf,
                ObjectHitboxSize,
                ObjectHitboxSize
            );
            DrawRect(rect, Colors.Cyan, false, 2f);
        }

        // Draw use areas for objects
        if (_lastSnapshot != null)
        {
            foreach (var obj in _lastSnapshot.Objects)
            {
                if (_sim.Content.Objects.TryGetValue(obj.ObjectDefId, out var objDef))
                {
                    foreach (var (dx, dy) in objDef.UseAreas)
                    {
                        var useAreaRect = new Rect2(
                            (obj.X + dx) * TileSize,
                            (obj.Y + dy) * TileSize,
                            TileSize,
                            TileSize
                        );
                        // Green fill with transparency, yellow outline
                        DrawRect(useAreaRect, new Color(0, 1, 0, 0.2f), true);
                        DrawRect(useAreaRect, Colors.Yellow, false, 1f);
                    }
                }
            }

            // Draw pawn paths and targets
            foreach (var pawn in _lastSnapshot.Pawns)
            {
                var pawnCenter = new Vector2(
                    pawn.X * TileSize + TileSize / 2,
                    pawn.Y * TileSize + TileSize / 2
                );

                // Draw the full path
                if (pawn.CurrentPath != null && pawn.CurrentPath.Count > 0)
                {
                    // Draw remaining path segments
                    for (int i = pawn.PathIndex; i < pawn.CurrentPath.Count - 1; i++)
                    {
                        var from = pawn.CurrentPath[i];
                        var to = pawn.CurrentPath[i + 1];
                        var fromPos = new Vector2(
                            from.X * TileSize + TileSize / 2,
                            from.Y * TileSize + TileSize / 2
                        );
                        var toPos = new Vector2(
                            to.X * TileSize + TileSize / 2,
                            to.Y * TileSize + TileSize / 2
                        );
                        DrawLine(fromPos, toPos, Colors.Orange, 2f);
                    }

                    // Draw line from pawn to next tile in path
                    if (pawn.PathIndex < pawn.CurrentPath.Count)
                    {
                        var nextTile = pawn.CurrentPath[pawn.PathIndex];
                        var nextPos = new Vector2(
                            nextTile.X * TileSize + TileSize / 2,
                            nextTile.Y * TileSize + TileSize / 2
                        );
                        DrawLine(pawnCenter, nextPos, Colors.White, 2f);
                    }
                }

                // Draw target tile (final destination)
                if (pawn.TargetTile.HasValue)
                {
                    var targetRect = new Rect2(
                        pawn.TargetTile.Value.X * TileSize + 4,
                        pawn.TargetTile.Value.Y * TileSize + 4,
                        TileSize - 8,
                        TileSize - 8
                    );
                    DrawRect(targetRect, new Color(1, 0.5f, 0, 0.3f), true); // Orange fill
                    DrawRect(targetRect, Colors.Orange, false, 2f); // Orange outline
                }
            }
        }

        // Draw mouse position (local coordinates)
        var mousePos = GetLocalMousePosition();
        DrawCircle(mousePos, 5f, Colors.Yellow);
    }

    private void UpdateInfoPanel(RenderSnapshot snapshot)
    {
        if (_infoPanel == null)
            return;

        if (!_selectedPawnId.HasValue)
        {
            _infoPanel.Hide();
            return;
        }

        var pawn = snapshot.Pawns.FirstOrDefault(p => p.Id.Value == _selectedPawnId.Value);
        if (pawn == null)
        {
            _infoPanel.Hide();
            _selectedPawnId = null;
            return;
        }

        var entityId = new EntityId(_selectedPawnId.Value);
        _sim.Entities.Needs.TryGetValue(entityId, out var needs);
        _sim.Entities.Buffs.TryGetValue(entityId, out var buffs);
        _infoPanel.ShowPawn(pawn, needs, buffs, _sim.Content);
    }

    private void SyncTiles()
    {
        // Update all tile colors based on current terrain state
        foreach (var (coord, tileNode) in _tileNodes)
        {
            UpdateSingleTile(coord);
        }
    }

    /// <summary>
    /// Update display tiles affected by a world tile change.
    /// In standard grid system, painting world tile at (x,y) affects 9 tiles:
    /// the tile itself plus its 8 neighbors for autotiling.
    /// </summary>
    private void UpdateTileAndNeighbors(TileCoord coord)
    {
        // Update the tile and its 8 neighbors for autotiling
        // In standard grid system, changing a tile affects itself and all adjacent tiles
        var displayOffsets = new[]
        {
            new TileCoord(-1, -1), // top-left
            new TileCoord(0, -1), // top
            new TileCoord(1, -1), // top-right
            new TileCoord(-1, 0), // left
            new TileCoord(0, 0), // center (the tile itself)
            new TileCoord(1, 0), // right
            new TileCoord(-1, 1), // bottom-left
            new TileCoord(0, 1), // bottom
            new TileCoord(1, 1), // bottom-right
        };

        foreach (var offset in displayOffsets)
        {
            var displayCoord = new TileCoord(coord.X + offset.X, coord.Y + offset.Y);
            if (_tileNodes.ContainsKey(displayCoord))
            {
                UpdateSingleTile(displayCoord);
            }
        }

        // Note: SDF is automatically regenerated by Godot from LightOccluder2D nodes
    }

    /// <summary>
    /// Update a single display tile by updating both base and overlay layers.
    /// </summary>
    private void UpdateSingleTile(TileCoord coord)
    {
        if (!_tileNodes.TryGetValue(coord, out var tileNode))
            return;

        UpdateBaseLayer(coord, tileNode);
        UpdateOverlayLayer(coord, tileNode);
    }

    /// <summary>
    /// Update the base layer which renders full-tile textures for flat terrains.
    /// In dual-grid system, display tile at coord shows the world tile at the same coord.
    /// </summary>
    private void UpdateBaseLayer(TileCoord coord, Node2D tileNode)
    {
        var baseLayer = tileNode.GetNode<Node2D>("BaseLayer");
        var sprite = baseLayer.GetNode<Sprite2D>("BaseTileSprite");
        var tileMapCoord = new Vector2I(coord.X, coord.Y);

        // Check if this world tile exists and should be rendered
        if (!_sim.World.IsInBounds(coord))
        {
            sprite.Visible = false;
            return;
        }

        var tile = _sim.World.GetTile(coord);

        if (!_sim.Content.Terrains.TryGetValue(tile.BaseTerrainTypeId, out var terrainDef))
        {
            sprite.Visible = false;
            return;
        }

        // If base terrain is autotiling, use TileMapLayer
        if (
            terrainDef.IsAutotiling
            && _autoTileLayers.TryGetValue(tile.BaseTerrainTypeId, out var layer)
        )
        {
            sprite.Visible = false;
            var color = _currentPalette[tile.ColorIndex];
            var cellsArray = new Godot.Collections.Array<Vector2I> { tileMapCoord };
            layer.SetCellsTerrainConnect(cellsArray, 0, 0, false);
            layer.SetTileColor(tileMapCoord, color);
        }
        else
        {
            // Render terrain using sprite (supports variants)
            var flatTexture = SpriteResourceManager.GetTexture(terrainDef.SpriteKey);
            if (flatTexture != null)
            {
                sprite.Texture = flatTexture;
                sprite.Modulate = _currentPalette[tile.ColorIndex];

                // Handle texture variants if terrain has multiple variants
                if (terrainDef.VariantCount > 1)
                {
                    // Use stored variant index from tile (randomized on paint)
                    int variantIndex = tile.BaseVariantIndex;

                    // Calculate texture region for 2x2 atlas layout
                    // Assumes 16x16 tiles arranged in a 2x2 grid for 4 variants
                    int variantsPerRow = 2; // 2x2 layout
                    int tileSize = 16;
                    int atlasX = (variantIndex % variantsPerRow) * tileSize;
                    int atlasY = (variantIndex / variantsPerRow) * tileSize;

                    sprite.RegionEnabled = true;
                    sprite.RegionRect = new Rect2(atlasX, atlasY, tileSize, tileSize);
                }
                else
                {
                    // No variants - use full texture
                    sprite.RegionEnabled = false;
                }

                sprite.Visible = true;
            }
            else
            {
                sprite.Visible = false;
            }
        }
    }

    /// <summary>
    /// Update the overlay layer which renders decorative terrains (grass, walls, paths).
    /// Supports both autotiling (TileMapLayer) and sprite-based (with variants).
    /// </summary>
    private void UpdateOverlayLayer(TileCoord coord, Node2D tileNode)
    {
        var overlayLayer = tileNode.GetNode<Node2D>("OverlayLayer");
        var sprite = overlayLayer.GetNode<Sprite2D>("OverlayTileSprite");
        var tileMapCoord = new Vector2I(coord.X, coord.Y);

        if (!_sim.World.IsInBounds(coord))
        {
            sprite.Visible = false;
            return;
        }

        var tile = _sim.World.GetTile(coord);

        // Clear all autotiling layers at this coord first
        foreach (var (terrainId, layer) in _autoTileLayers)
        {
            // Skip if this is a base terrain autotiling layer
            if (terrainId == tile.BaseTerrainTypeId)
                continue;

            layer.EraseCell(tileMapCoord);
            layer.ClearTileColor(tileMapCoord);
        }

        // If no overlay terrain, hide sprite and return
        if (!tile.OverlayTerrainTypeId.HasValue)
        {
            sprite.Visible = false;
            return;
        }

        // Get the overlay terrain definition
        if (
            !_sim.Content.Terrains.TryGetValue(
                tile.OverlayTerrainTypeId.Value,
                out var overlayTerrainDef
            )
        )
        {
            sprite.Visible = false;
            return;
        }

        // Render overlay terrain - either autotiling or sprite-based
        if (
            overlayTerrainDef.IsAutotiling
            && _autoTileLayers.TryGetValue(tile.OverlayTerrainTypeId.Value, out var autoTileLayer)
        )
        {
            // Use TileMapLayer for autotiling terrain
            sprite.Visible = false;
            var color = _currentPalette[tile.OverlayColorIndex];
            var cellsArray = new Godot.Collections.Array<Vector2I> { tileMapCoord };
            autoTileLayer.SetCellsTerrainConnect(cellsArray, 0, 0, false);
            autoTileLayer.SetTileColor(tileMapCoord, color);
        }
        else
        {
            // Use sprite for non-autotiling overlay terrain (supports variants)
            var overlayTexture = SpriteResourceManager.GetTexture(overlayTerrainDef.SpriteKey);
            if (overlayTexture != null)
            {
                sprite.Texture = overlayTexture;
                sprite.Modulate = _currentPalette[tile.OverlayColorIndex];

                // Handle texture variants if terrain has multiple variants
                if (overlayTerrainDef.VariantCount > 1)
                {
                    // Use stored variant index from tile (randomized on paint)
                    int variantIndex = tile.OverlayVariantIndex;

                    // Calculate texture region for 2x2 atlas layout
                    // Assumes 16x16 tiles arranged in a 2x2 grid for 4 variants
                    int variantsPerRow = 2; // 2x2 layout
                    int tileSize = 16;
                    int atlasX = (variantIndex % variantsPerRow) * tileSize;
                    int atlasY = (variantIndex / variantsPerRow) * tileSize;

                    sprite.RegionEnabled = true;
                    sprite.RegionRect = new Rect2(atlasX, atlasY, tileSize, tileSize);
                }
                else
                {
                    // No variants - use full texture
                    sprite.RegionEnabled = false;
                }

                sprite.Visible = true;
            }
            else
            {
                sprite.Visible = false;
            }
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

        // Center position of the spawn tile
        var centerX = tileX * TileSize + TileSize / 2;
        var centerY = tileY * TileSize + TileSize / 2;

        // Determine which edge(s) the pawn is on and offset accordingly
        // If on multiple edges (corner), choose the offset that makes most sense

        bool onLeftEdge = tileX == 0;
        bool onRightEdge = tileX == worldWidth - 1;
        bool onTopEdge = tileY == 0;
        bool onBottomEdge = tileY == worldHeight - 1;

        // Corners: offset diagonally
        if (onLeftEdge && onTopEdge)
            return new Vector2(centerX - TileSize, centerY - TileSize);
        if (onRightEdge && onTopEdge)
            return new Vector2(centerX + TileSize, centerY - TileSize);
        if (onLeftEdge && onBottomEdge)
            return new Vector2(centerX - TileSize, centerY + TileSize);
        if (onRightEdge && onBottomEdge)
            return new Vector2(centerX + TileSize, centerY + TileSize);

        // Single edges
        if (onLeftEdge)
            return new Vector2(centerX - TileSize, centerY);
        if (onRightEdge)
            return new Vector2(centerX + TileSize, centerY);
        if (onTopEdge)
            return new Vector2(centerX, centerY - TileSize);
        if (onBottomEdge)
            return new Vector2(centerX, centerY + TileSize);

        // Fallback (should not happen if spawning only on edges)
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
                // Add pawns directly to Main for y-sorting with walls
                _pawnsRoot.GetParent().AddChild(node);
                _pawnNodes.Add(pawn.Id.Value, node);

                // Initialize with sprite if available
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
                pawn.X * TileSize + TileSize / 2,
                pawn.Y * TileSize + TileSize / 2
            );

            if (node is PawnView pv)
            {
                // For new pawns, calculate entry position from nearest edge
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
            }
        }

        // Remove nodes for pawns that no longer exist
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

            // Clear selection if the removed pawn was selected
            if (_selectedPawnId == id)
            {
                _selectedPawnId = null;
                _infoPanel?.Hide();
            }
        }
    }

    private void SyncObjects(RenderSnapshot snapshot)
    {
        _activeIds.Clear();

        foreach (var obj in snapshot.Objects)
        {
            _activeIds.Add(obj.Id.Value);

            if (!_objectNodes.TryGetValue(obj.Id.Value, out var node))
            {
                node = ObjectScene?.Instantiate<Node2D>() ?? new Node2D();
                // Add objects directly to Main for y-sorting with walls
                _objectsRoot.GetParent().AddChild(node);
                _objectNodes.Add(obj.Id.Value, node);

                // Initialize with sprite if object has one
                if (node is ObjectView ovInit)
                {
                    if (_sim.Content.Objects.TryGetValue(obj.ObjectDefId, out var objDef))
                    {
                        var texture = SpriteResourceManager.GetTexture(objDef.SpriteKey);
                        if (texture != null)
                        {
                            ovInit.InitializeWithSprite(texture);
                        }
                    }
                }
            }

            node.Position = new Vector2(
                obj.X * TileSize + TileSize / 2,
                obj.Y * TileSize + TileSize / 2
            );

            if (node is ObjectView ov)
            {
                ov.SetObjectInfo(obj.Name, obj.InUse, obj.ColorIndex, _currentPalette);
            }
        }

        // Remove nodes for objects that no longer exist
        _idsToRemove.Clear();
        foreach (var id in _objectNodes.Keys)
        {
            if (!_activeIds.Contains(id))
                _idsToRemove.Add(id);
        }
        foreach (var id in _idsToRemove)
        {
            _objectNodes[id].QueueFree();
            _objectNodes.Remove(id);

            // Clear selection if the removed object was selected
            if (_selectedObjectId == id)
            {
                _selectedObjectId = null;
                _objectInfoPanel?.Hide();
            }
        }
    }

    private void UpdateObjectInfoPanel(RenderSnapshot snapshot)
    {
        if (_objectInfoPanel == null)
            return;

        if (!_selectedObjectId.HasValue)
        {
            _objectInfoPanel.Hide();
            return;
        }

        var obj = snapshot.Objects.FirstOrDefault(o => o.Id.Value == _selectedObjectId.Value);
        if (obj == null)
        {
            _objectInfoPanel.Hide();
            _selectedObjectId = null;
            return;
        }

        _objectInfoPanel.ShowObject(obj, _sim.Content);
    }

    private void UpdateTimeDisplay(RenderSnapshot snapshot)
    {
        if (_timeDisplay == null)
            return;

        // Show TimeDisplay when nothing is selected, hide when something is selected
        if (!_selectedPawnId.HasValue && !_selectedObjectId.HasValue)
        {
            _timeDisplay.Show();
            _timeDisplay.UpdateTime(snapshot.Time);
        }
        else
        {
            _timeDisplay.Hide();
        }
    }

    private void UpdateNightOverlay(RenderSnapshot snapshot)
    {
        // Normalized day fraction from simulation tick: 0.0 = midnight, 0.5 = noon, 1.0 = next midnight
        float timeOfDay = snapshot.Time.DayFraction;

        // Update CRT shader's time of day parameter
        if (_crtShaderController != null)
        {
            _crtShaderController.SetTimeOfDay(timeOfDay);
        }

        // Update shadow shader sun angle and length
        if (_shadowShaderMaterial != null)
        {
            // Calculate sun angle: -90° at midnight, 0° at noon (overhead), 90° at next midnight
            // timeOfDay goes from 0 (midnight) -> 0.5 (noon) -> 1.0 (next midnight)
            float sunAngle = (timeOfDay - 0.5f) * 180f; // -90° to 90°
            // Convert to degrees (shader expects 0-360)
            _shadowShaderMaterial.SetShaderParameter("sun_angle", sunAngle + 90f);

            // Calculate sun elevation for shadow length (1.0 at noon, 0.0 at horizon/midnight)
            // Using cosine: high when sun is overhead, low when sun is at horizon
            float sunElevation = Mathf.Max(0.0f, Mathf.Cos(sunAngle * Mathf.Pi / 180f));

            // Shadow length inversely proportional to sun elevation
            // At noon (elevation = 1.0): shadows shortest (multiplier = 1.0)
            // At sunrise/sunset (elevation = 0.0): shadows longest (multiplier = 3.0)
            float baseShadowDistance = 16.0f;
            float shadowMultiplier = 1.0f + (1.0f - sunElevation) * 4.0f;
            float shadowDistance = baseShadowDistance * shadowMultiplier;

            _shadowShaderMaterial.SetShaderParameter("max_shadow_distance", shadowDistance);

            // Fade shadow opacity based on sun elevation
            // Shadows should fade out before midnight - use a steeper curve
            // Apply power function to make shadows disappear faster as sun gets lower
            float baseShadowAlpha = 0.3f;
            // Square the elevation to make shadows fade faster near sunset/sunrise
            // This makes shadows disappear well before midnight
            float shadowFade = sunElevation * sunElevation;
            float shadowAlpha = baseShadowAlpha * shadowFade;
            var shadowColor = new Color(0.0f, 0.0f, 0.0f, shadowAlpha);

            _shadowShaderMaterial.SetShaderParameter("shadow_color", shadowColor);
        }
    }
}
