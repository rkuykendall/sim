using Godot;
using SimGame.Core;
using System.Collections.Generic;

public partial class GameRoot : Node2D
{
    private Simulation _sim = null!;
    private float _accumulator = 0f;
    private float _tickDelta;

    private readonly Dictionary<int, Node2D> _pawnNodes = new();
    private readonly Dictionary<int, Node2D> _objectNodes = new();

    [Export] public PackedScene PawnScene { get; set; } = null!;
    [Export] public PackedScene ObjectScene { get; set; } = null!;
    [Export] public NodePath PawnsRootPath { get; set; } = ".";
    [Export] public NodePath ObjectsRootPath { get; set; } = ".";

    private Node2D _pawnsRoot = null!;
    private Node2D _objectsRoot = null!;

    public override void _Ready()
    {
        _sim = new Simulation();
        _tickDelta = 1f / Simulation.TickRate;
        _pawnsRoot = GetNode<Node2D>(PawnsRootPath);
        _objectsRoot = GetNode<Node2D>(ObjectsRootPath);
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
    }

    private void SyncPawns(RenderSnapshot snapshot)
    {
        const int tileSize = 32;

        foreach (var pawn in snapshot.Pawns)
        {
            if (!_pawnNodes.TryGetValue(pawn.Id.Value, out var node))
            {
                node = PawnScene.Instantiate<Node2D>();
                _pawnsRoot.AddChild(node);
                _pawnNodes.Add(pawn.Id.Value, node);
            }

            node.Position = new Vector2(
                pawn.X * tileSize + tileSize / 2,
                pawn.Y * tileSize + tileSize / 2
            );

            if (node is PawnView pv)
            {
                pv.SetMood(pawn.Mood);
                pv.SetNameLabel(pawn.Name);
                pv.SetAction(pawn.CurrentAction);
            }
        }
    }

    private void SyncObjects(RenderSnapshot snapshot)
    {
        const int tileSize = 32;

        foreach (var obj in snapshot.Objects)
        {
            if (!_objectNodes.TryGetValue(obj.Id.Value, out var node))
            {
                node = ObjectScene?.Instantiate<Node2D>() ?? new Node2D();
                _objectsRoot.AddChild(node);
                _objectNodes.Add(obj.Id.Value, node);
            }

            node.Position = new Vector2(
                obj.X * tileSize + tileSize / 2,
                obj.Y * tileSize + tileSize / 2
            );

            if (node is ObjectView ov)
            {
                ov.SetObjectInfo(obj.Name, obj.InUse);
            }
        }
    }
}
