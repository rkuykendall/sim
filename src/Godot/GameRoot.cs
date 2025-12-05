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
    private const float PawnHitboxSize = 24f; // Match the ColorRect size

    private readonly Dictionary<int, Node2D> _pawnNodes = new();
    private readonly Dictionary<int, Node2D> _objectNodes = new();
    
    private int? _selectedPawnId = null;
    private bool _debugMode = false;

    [Export] public PackedScene PawnScene { get; set; } = null!;
    [Export] public PackedScene ObjectScene { get; set; } = null!;
    [Export] public NodePath PawnsRootPath { get; set; } = ".";
    [Export] public NodePath ObjectsRootPath { get; set; } = ".";
    [Export] public NodePath InfoPanelPath { get; set; } = "";

    private Node2D _pawnsRoot = null!;
    private Node2D _objectsRoot = null!;
    private PawnInfoPanel? _infoPanel;

    public override void _Ready()
    {
        // Load content from Lua files before creating simulation
        var contentPath = ProjectSettings.GlobalizePath("res://content");
        ContentLoader.LoadAll(contentPath);

        _sim = new Simulation();
        _tickDelta = 1f / Simulation.TickRate;
        _pawnsRoot = GetNode<Node2D>(PawnsRootPath);
        _objectsRoot = GetNode<Node2D>(ObjectsRootPath);
        
        if (!string.IsNullOrEmpty(InfoPanelPath))
            _infoPanel = GetNodeOrNull<PawnInfoPanel>(InfoPanelPath);
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
        SyncPawns(snapshot);
        SyncObjects(snapshot);
        UpdateInfoPanel(snapshot);
        
        // Redraw debug visuals
        if (_debugMode)
            QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Toggle debug mode with F3
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.F3)
        {
            _debugMode = !_debugMode;
            GD.Print($"Debug mode: {_debugMode}");
            QueueRedraw();
            return;
        }

        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var localPos = GetLocalMousePosition();
            var globalPos = GetGlobalMousePosition();
            var viewportPos = GetViewport().GetMousePosition();
            
            GD.Print($"=== CLICK ===");
            GD.Print($"  Viewport mouse pos: {viewportPos}");
            GD.Print($"  Local mouse pos: {localPos}");
            GD.Print($"  Global mouse pos: {globalPos}");
            GD.Print($"  Pawn count: {_pawnNodes.Count}");
            
            const float halfSize = PawnHitboxSize / 2f;
            foreach (var (id, node) in _pawnNodes)
            {
                var pawnPos = node.Position;
                var hitbox = $"({pawnPos.X - halfSize}, {pawnPos.Y - halfSize}) to ({pawnPos.X + halfSize}, {pawnPos.Y + halfSize})";
                GD.Print($"  Pawn {id}: pos={pawnPos}, hitbox={hitbox}");
            }
            
            var clickedPawnId = FindPawnAtPosition(localPos);
            GD.Print($"  Clicked pawn: {clickedPawnId?.ToString() ?? "none"}");
            
            // Update selection
            if (_selectedPawnId.HasValue && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var oldNode))
            {
                if (oldNode is PawnView oldPv)
                    oldPv.SetSelected(false);
            }
            
            _selectedPawnId = clickedPawnId;
            
            if (_selectedPawnId.HasValue && _pawnNodes.TryGetValue(_selectedPawnId.Value, out var newNode))
            {
                if (newNode is PawnView newPv)
                    newPv.SetSelected(true);
            }
            else
            {
                _infoPanel?.Hide();
            }
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
            
            if (_debugMode)
                GD.Print($"  Pawn {id} at {pawnPos}, hit={hit}");
            
            if (hit)
                return id;
        }
        return null;
    }

    public override void _Draw()
    {
        if (!_debugMode) return;

        // Draw hitboxes for all pawns
        const float halfSize = PawnHitboxSize / 2f;
        foreach (var (id, node) in _pawnNodes)
        {
            var rect = new Rect2(
                node.Position.X - halfSize,
                node.Position.Y - halfSize,
                PawnHitboxSize,
                PawnHitboxSize
            );
            DrawRect(rect, Colors.Magenta, false, 2f);
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
        _infoPanel.ShowPawn(pawn, needs, buffs);
    }

    private void SyncPawns(RenderSnapshot snapshot)
    {
        foreach (var pawn in snapshot.Pawns)
        {
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
    }

    private void SyncObjects(RenderSnapshot snapshot)
    {
        foreach (var obj in snapshot.Objects)
        {
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
    }
}
