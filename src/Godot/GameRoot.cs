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
    private Dictionary<TileCoord, (Sprite2D baseSprite, Sprite2D overlaySprite)> _tileSprites =
        new();
    private Dictionary<int, List<(Vector2I coord, Color color)>> _autotileUpdates = new();
    private Dictionary<int, List<Vector2I>> _autotileClearCells = new();
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
        ZIndex = ZIndexConstants.UIOverlay;

        var contentPath = ProjectSettings.GlobalizePath("res://content");
        var content = ContentLoader.LoadAll(contentPath);

        _sim = new Simulation(content);
        _tickDelta = 1f / Simulation.TickRate;

        _characterSprite = SpriteResourceManager.GetTexture("character_walk");
        _idleSprite = SpriteResourceManager.GetTexture("character_idle");
        _axeSprite = SpriteResourceManager.GetTexture("character_axe");
        _pickaxeSprite = SpriteResourceManager.GetTexture("character_pickaxe");
        _lookDownSprite = SpriteResourceManager.GetTexture("character_look_down");
        _lookUpSprite = SpriteResourceManager.GetTexture("character_look_up");

        _pawnsRoot = GetNode<Node2D>(PawnsRootPath);
        _objectsRoot = GetNode<Node2D>(ObjectsRootPath);
        _tilesRoot = GetNode<Node2D>(TilesRootPath);

        InitializeAutoTileLayers();

        var initialSnapshot = _sim.CreateRenderSnapshot();
        _currentPalette = GameColorPalette.ToGodotColors(initialSnapshot.ColorPalette);
        // Don't set _currentPaletteId yet - let _Process() call UpdatePalette on first frame when the ColorPickerModal is actually ready

        InitializeTileNodes();

        var allTiles = new List<TileCoord>();
        for (int x = 0; x < _sim.World.Width; x++)
        {
            for (int y = 0; y < _sim.World.Height; y++)
            {
                allTiles.Add(new TileCoord(x, y));
            }
        }
        SyncTiles(allTiles.ToArray());

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
                var worldCenterX = (_sim.World.Width * RenderingConstants.RenderedTileSize) / 2f;
                var worldCenterY = (_sim.World.Height * RenderingConstants.RenderedTileSize) / 2f;
                _camera.Position = new Vector2(worldCenterX, worldCenterY);
            }
        }
        if (!string.IsNullOrEmpty(UILayerPath))
            _uiLayer = GetNodeOrNull<CanvasLayer>(UILayerPath);

        if (!string.IsNullOrEmpty(ToolbarPath))
        {
            _toolbar = GetNodeOrNull<BuildToolbar>(ToolbarPath);
            _toolbar?.Initialize(_sim.Content, DebugMode);
            // Don't call UpdatePalette here - modal isn't ready yet, _Process() will call it on first frame
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

        if (_sim.SelectedPaletteId != _currentPaletteId)
        {
            _currentPalette = GameColorPalette.ToGodotColors(snapshot.ColorPalette);
            _currentPaletteId = _sim.SelectedPaletteId;
            _toolbar?.UpdatePalette(_currentPalette);
        }

        SyncPawns(snapshot);
        SyncObjects(snapshot);
        UpdateInfoPanel(snapshot);
        UpdateObjectInfoPanel(snapshot);
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
                    if (BuildToolState.Mode == BuildToolMode.PlaceTerrain)
                    {
                        _isPaintingTerrain = true;
                        TileCoord[] tilesToUpdate;
                        if (BuildToolState.SelectedTerrainDefId.HasValue)
                        {
                            tilesToUpdate = _sim.PaintTerrain(
                                tileCoord,
                                BuildToolState.SelectedTerrainDefId.Value,
                                BuildToolState.SelectedColorIndex
                            );
                        }
                        else
                        {
                            tilesToUpdate = _sim.DeleteAtTile(tileCoord);
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
                        }
                        else
                        {
                            tilesToUpdate = _sim.FloodDelete(tileCoord);
                        }
                        SyncTiles(tilesToUpdate);
                        return;
                    }
                }
                else
                {
                    _isPaintingTerrain = false;
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

            if (
                BuildToolState.Mode == BuildToolMode.PlaceObject
                && BuildToolState.SelectedObjectDefId.HasValue
            )
            {
                try
                {
                    _sim.CreateObject(
                        BuildToolState.SelectedObjectDefId.Value,
                        tileCoord,
                        BuildToolState.SelectedColorIndex
                    );
                }
                catch (System.InvalidOperationException)
                {
                    GD.Print($"Cannot place object at {tileCoord}: tile occupied");
                }
                catch (System.ArgumentException ex)
                {
                    GD.PrintErr($"Invalid object placement: {ex.Message}");
                }
                return;
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
                _selectedObjectId = null;
                _objectInfoPanel?.Hide();

                if (_pawnNodes.TryGetValue(_selectedPawnId.Value, out var newNode))
                {
                    if (newNode is PawnView newPv)
                        newPv.SetSelected(true);
                }
                return;
            }

            var clickedObjectId = FindObjectAtPosition(localPos);

            if (clickedObjectId.HasValue)
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
                _selectedObjectId = clickedObjectId;
                _infoPanel?.Hide();
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
            _selectedObjectId = null;
            _infoPanel?.Hide();
            _objectInfoPanel?.Hide();
        }

        if (@event is InputEventMouseMotion)
        {
            if (_isPaintingTerrain && BuildToolState.Mode == BuildToolMode.PlaceTerrain)
            {
                var localPos = GetLocalMousePosition();
                var tileCoord = ScreenToTileCoord(localPos);
                TileCoord[] tilesToUpdate;
                if (BuildToolState.SelectedTerrainDefId.HasValue)
                {
                    tilesToUpdate = _sim.PaintTerrain(
                        tileCoord,
                        BuildToolState.SelectedTerrainDefId.Value,
                        BuildToolState.SelectedColorIndex
                    );
                }
                else
                {
                    tilesToUpdate = _sim.DeleteAtTile(tileCoord);
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
            BuildToolState.Mode == BuildToolMode.PlaceObject
            && BuildToolState.SelectedObjectDefId.HasValue
        )
        {
            var color = _currentPalette[BuildToolState.SelectedColorIndex];
            color.A = 0.5f;
            DrawRect(rect, color, true);
        }

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

        if (_lastSnapshot != null)
        {
            foreach (var obj in _lastSnapshot.Objects)
            {
                if (_sim.Content.Objects.TryGetValue(obj.ObjectDefId, out var objDef))
                {
                    foreach (var (dx, dy) in objDef.UseAreas)
                    {
                        var useAreaRect = new Rect2(
                            (obj.X + dx) * RenderingConstants.RenderedTileSize,
                            (obj.Y + dy) * RenderingConstants.RenderedTileSize,
                            RenderingConstants.RenderedTileSize,
                            RenderingConstants.RenderedTileSize
                        );
                        DrawRect(useAreaRect, new Color(0, 1, 0, 0.2f), true);
                        DrawRect(useAreaRect, Colors.Yellow, false, 1f);
                    }
                }
            }

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
                node.ZIndex = ZIndexConstants.Objects;
                _objectsRoot.GetParent().AddChild(node);
                _objectNodes.Add(obj.Id.Value, node);

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
                obj.X * RenderingConstants.RenderedTileSize
                    + RenderingConstants.RenderedTileSize / 2,
                obj.Y * RenderingConstants.RenderedTileSize
                    + RenderingConstants.RenderedTileSize / 2
            );

            if (node is ObjectView ov)
            {
                ov.SetObjectInfo(obj.Name, obj.InUse, obj.ColorIndex, _currentPalette);
            }
        }

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
}
