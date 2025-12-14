using Godot;
using SimGame.Core;
using System.Collections.Generic;

public partial class PawnInfoPanel : PanelContainer
{
    [Export] public NodePath NameLabelPath { get; set; } = null!;
    [Export] public NodePath MoodLabelPath { get; set; } = null!;
    [Export] public NodePath ActionLabelPath { get; set; } = null!;
    [Export] public NodePath NeedsContainerPath { get; set; } = null!;
    [Export] public NodePath BuffsContainerPath { get; set; } = null!;

    private Label? _nameLabel;
    private Label? _moodLabel;
    private Label? _actionLabel;
    private VBoxContainer? _needsContainer;
    private VBoxContainer? _buffsContainer;

    private readonly Dictionary<int, ProgressBar> _needBars = new();
    private readonly List<Label> _buffLabels = new();

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>(NameLabelPath);
        _moodLabel = GetNodeOrNull<Label>(MoodLabelPath);
        _actionLabel = GetNodeOrNull<Label>(ActionLabelPath);
        _needsContainer = GetNodeOrNull<VBoxContainer>(NeedsContainerPath);
        _buffsContainer = GetNodeOrNull<VBoxContainer>(BuffsContainerPath);
        
        // Start hidden until a pawn is selected
        Visible = false;
    }

    public void ShowPawn(RenderPawn pawn, NeedsComponent? needs, BuffComponent? buffs, ContentRegistry content)
    {
        Visible = true;

        if (_nameLabel != null)
            _nameLabel.Text = pawn.Name;
        
        if (_moodLabel != null)
        {
            _moodLabel.Text = $"Mood: {pawn.Mood:+0;-0;0}";
            _moodLabel.Modulate = pawn.Mood > 20 ? Colors.Lime : pawn.Mood < -20 ? Colors.Red : Colors.White;
        }
        
        if (_actionLabel != null)
            _actionLabel.Text = pawn.CurrentAction ?? "Idle";

        // Update needs
        if (_needsContainer != null && needs != null)
        {
            foreach (var (needId, value) in needs.Needs)
            {
                if (!content.Needs.TryGetValue(needId, out var needDef))
                    continue;

                if (!_needBars.TryGetValue(needId, out var bar))
                {
                    bar = CreateNeedBar(needDef.Name);
                    _needBars[needId] = bar;
                }

                bar.Value = value;
                
                // Color based on thresholds
                if (value < needDef.CriticalThreshold)
                    bar.Modulate = Colors.Red;
                else if (value < needDef.LowThreshold)
                    bar.Modulate = Colors.Yellow;
                else
                    bar.Modulate = Colors.Lime;
            }
        }

        // Update buffs
        UpdateBuffsDisplay(buffs, content);
    }

    private void UpdateBuffsDisplay(BuffComponent? buffs, ContentRegistry content)
    {
        if (_buffsContainer == null) return;

        // Clear old buff labels
        foreach (var label in _buffLabels)
            label.QueueFree();
        _buffLabels.Clear();

        if (buffs == null || buffs.ActiveBuffs.Count == 0)
        {
            var noBuffsLabel = new Label
            {
                Text = "(none)",
                Modulate = Colors.Gray
            };
            noBuffsLabel.AddThemeFontSizeOverride("font_size", 8);
            _buffsContainer.AddChild(noBuffsLabel);
            _buffLabels.Add(noBuffsLabel);
            return;
        }

        foreach (var inst in buffs.ActiveBuffs)
        {
            if (!content.Buffs.TryGetValue(inst.BuffDefId, out var buffDef))
                continue;

            var label = new Label
            {
                Text = $"{buffDef.Name} ({buffDef.MoodOffset:+0;-0})"
            };
            label.AddThemeFontSizeOverride("font_size", 8);

            // Color based on mood impact
            if (buffDef.MoodOffset > 0)
                label.Modulate = Colors.Lime;
            else if (buffDef.MoodOffset < 0)
                label.Modulate = Colors.Orange;
            else
                label.Modulate = Colors.White;

            _buffsContainer.AddChild(label);
            _buffLabels.Add(label);
        }
    }

    public new void Hide()
    {
        Visible = false;
    }

    private ProgressBar CreateNeedBar(string needName)
    {
        var container = new HBoxContainer();
        
        var label = new Label
        {
            Text = needName,
            CustomMinimumSize = new Vector2(35, 0)
        };
        label.AddThemeFontSizeOverride("font_size", 8);
        
        var bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(50, 10),
            MaxValue = 100,
            ShowPercentage = false
        };

        container.AddChild(label);
        container.AddChild(bar);
        _needsContainer!.AddChild(container);

        return bar;
    }
}
