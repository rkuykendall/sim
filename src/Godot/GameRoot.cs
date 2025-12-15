using Godot;
using SimGame.Core;
using System.Collections.Generic;
using System.Linq;

public partial class GameRoot : Node2D
{
    private Simulation _sim = null!;
    private float _accumulator = 0f;
    private float _tickDelta;
    private const int TileSize = 32;
    private const float PawnHitboxSize = 24f;
    private const float ObjectHitboxSize = 28f;

    private readonly Dictionary<int, Node2D> _pawnNodes = new();
    private readonly Dictionary<int, Node2D> _objectNodes = new();
    
    // Reusable collections for sync operations (avoid per-frame allocations)
    private readonly HashSet<int> _activeIds = new();
    private readonly List<int> _idsToRemove = new();
    
    private int? _selectedPawnId = null;
    private int? _selectedObjectId = null;
    private bool _debugMode = false;
    private RenderSnapshot? _lastSnapshot = null;

    [Export] public PackedScene PawnScene { get; set; } = null!;
    [Export] public PackedScene ObjectScene { get; set; } = null!;
    [Export] public NodePath PawnsRootPath { get; set; } = ".";
    [Export] public NodePath ObjectsRootPath { get; set; } = ".";
    [Export] public NodePath InfoPanelPath { get; set; } = "";
    [Export] public NodePath ObjectInfoPanelPath { get; set; } = "";
    [Export] public NodePath TimeDisplayPath { get; set; } = "";
    [Export] public NodePath NightOverlayPath { get; set; } = "";
    [Export] public NodePath CameraPath { get; set; } = "";
    [Export] public NodePath UILayerPath { get; set; } = "";

    private Node2D _pawnsRoot = null!;
    private Node2D _objectsRoot = null!;
    private PawnInfoPanel? _infoPanel;
    private ObjectInfoPanel? _objectInfoPanel;
    private TimeDisplay? _timeDisplay;
    private ColorRect? _nightOverlay;
    private Camera2D? _camera;
    private CanvasLayer? _uiLayer;

    public override void _Ready()
    {
        // Load content from Lua files and create simulation with it
        var contentPath = ProjectSettings.GlobalizePath("res://content");
        var content = ContentLoader.LoadAll(contentPath);

        _sim = new Simulation(content);
        _tickDelta = 1f / Simulation.TickRate;
        _pawnsRoot = GetNode<Node2D>(PawnsRootPath);
        _objectsRoot = GetNode<Node2D>(ObjectsRootPath);
        
        if (!string.IsNullOrEmpty(InfoPanelPath))
            _infoPanel = GetNodeOrNull<PawnInfoPanel>(InfoPanelPath);
        if (!string.IsNullOrEmpty(ObjectInfoPanelPath))
            _objectInfoPanel = GetNodeOrNull<ObjectInfoPanel>(ObjectInfoPanelPath);
        if (!string.IsNullOrEmpty(TimeDisplayPath))
            _timeDisplay = GetNodeOrNull<TimeDisplay>(TimeDisplayPath);
        if (!string.IsNullOrEmpty(NightOverlayPath))
            _nightOverlay = GetNodeOrNull<ColorRect>(NightOverlayPath);
        if (!string.IsNullOrEmpty(CameraPath))
            _camera = GetNodeOrNull<Camera2D>(CameraPath);
        if (!string.IsNullOrEmpty(UILayerPath))
            _uiLayer = GetNodeOrNull<CanvasLayer>(UILayerPath);
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
        SyncPawns(snapshot);
        SyncObjects(snapshot);
        UpdateInfoPanel(snapshot);
        UpdateObjectInfoPanel(snapshot);
        UpdateTimeDisplay(snapshot);
        UpdateNightOverlay(snapshot);
        
        // Redraw debug visuals
        if (_debugMode)
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
                QueueRedraw();
                return;
            }
        }

        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var localPos = GetLocalMousePosition();
            
            // Try to click a pawn first
            var clickedPawnId = FindPawnAtPosition(localPos);
            
            if (clickedPawnId.HasValue)
            {
                // Deselect old pawn
                if (_selectedPawnId.HasValue && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var oldNode))
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
                if (_selectedPawnId.HasValue && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var oldPawnNode))
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
            if (_selectedPawnId.HasValue && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var pawnNode))
            {
                if (pawnNode is PawnView pv)
                    pv.SetSelected(false);
            }
            _selectedPawnId = null;
            _selectedObjectId = null;
            _infoPanel?.Hide();
            _objectInfoPanel?.Hide();
        }
    }

    private int? FindPawnAtPosition(Vector2 pos)
    {
        const float halfSize = PawnHitboxSize / 2f;
        
        foreach (var (id, node) in _pawnNodes)
        {
            var pawnPos = node.Position;
            bool hit = pos.X >= pawnPos.X - halfSize && pos.X <= pawnPos.X + halfSize &&
                       pos.Y >= pawnPos.Y - halfSize && pos.Y <= pawnPos.Y + halfSize;
            
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
            bool hit = pos.X >= objPos.X - halfSize && pos.X <= objPos.X + halfSize &&
                       pos.Y >= objPos.Y - halfSize && pos.Y <= objPos.Y + halfSize;
            
            if (hit)
                return id;
        }
        return null;
    }

    public override void _Draw()
    {
        if (!_debugMode) return;

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
                        var fromPos = new Vector2(from.X * TileSize + TileSize / 2, from.Y * TileSize + TileSize / 2);
                        var toPos = new Vector2(to.X * TileSize + TileSize / 2, to.Y * TileSize + TileSize / 2);
                        DrawLine(fromPos, toPos, Colors.Orange, 2f);
                    }

                    // Draw line from pawn to next tile in path
                    if (pawn.PathIndex < pawn.CurrentPath.Count)
                    {
                        var nextTile = pawn.CurrentPath[pawn.PathIndex];
                        var nextPos = new Vector2(nextTile.X * TileSize + TileSize / 2, nextTile.Y * TileSize + TileSize / 2);
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
                    DrawRect(targetRect, new Color(1, 0.5f, 0, 0.3f), true);  // Orange fill
                    DrawRect(targetRect, Colors.Orange, false, 2f);           // Orange outline
                }
            }
        }
        
        // Draw mouse position (local coordinates)
        var mousePos = GetLocalMousePosition();
        DrawCircle(mousePos, 5f, Colors.Yellow);
    }

    private void UpdateInfoPanel(RenderSnapshot snapshot)
    {
        if (_infoPanel == null || !_selectedPawnId.HasValue)
            return;

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

    private void SyncPawns(RenderSnapshot snapshot)
    {
        _activeIds.Clear();
        
        foreach (var pawn in snapshot.Pawns)
        {
            _activeIds.Add(pawn.Id.Value);
            
            if (!_pawnNodes.TryGetValue(pawn.Id.Value, out var node))
            {
                node = PawnScene.Instantiate<Node2D>();
                _pawnsRoot.AddChild(node);
                _pawnNodes.Add(pawn.Id.Value, node);
            }

            node.Position = new Vector2(
                pawn.X * TileSize + TileSize / 2,
                pawn.Y * TileSize + TileSize / 2
            );

            if (node is PawnView pv)
            {
                pv.SetMood(pawn.Mood);
                pv.SetNameLabel(pawn.Name);
                pv.SetAction(pawn.CurrentAction);
                pv.SetSelected(pawn.Id.Value == _selectedPawnId);
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
                _objectsRoot.AddChild(node);
                _objectNodes.Add(obj.Id.Value, node);
            }

            node.Position = new Vector2(
                obj.X * TileSize + TileSize / 2,
                obj.Y * TileSize + TileSize / 2
            );

            if (node is ObjectView ov)
            {
                ov.SetObjectInfo(obj.Name, obj.InUse);
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
        if (_objectInfoPanel == null || !_selectedObjectId.HasValue)
            return;

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
        _timeDisplay?.UpdateTime(snapshot.Time);
    }

    private void UpdateNightOverlay(RenderSnapshot snapshot)
    {
        if (_nightOverlay == null) return;

        // Smoothly transition night overlay based on time
        float targetAlpha = 0f;
        int hour = snapshot.Time.Hour;

        if (hour >= 22 || hour < 5)
        {
            // Deep night: 10 PM - 5 AM
            targetAlpha = 0.4f;
        }
        else if (hour >= 20)
        {
            // Dusk: 8 PM - 10 PM
            targetAlpha = (hour - 20) * 0.2f + (snapshot.Time.Minute / 60f) * 0.2f;
        }
        else if (hour < 7)
        {
            // Dawn: 5 AM - 7 AM
            targetAlpha = 0.4f - ((hour - 5) * 0.2f + (snapshot.Time.Minute / 60f) * 0.2f);
        }

        var color = _nightOverlay.Color;
        color.A = Mathf.Lerp(color.A, targetAlpha, 0.05f);
        _nightOverlay.Color = color;
    }
}
