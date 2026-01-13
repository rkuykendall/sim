using System.Collections.Generic;
using Godot;
using SimGame.Core;

public partial class PawnInfoPanel : PanelContainer
{
    [Export]
    public NodePath NameLabelPath { get; set; } = null!;

    [Export]
    public NodePath MoodLabelPath { get; set; } = null!;

    [Export]
    public NodePath GoldLabelPath { get; set; } = null!;

    [Export]
    public NodePath ActionLabelPath { get; set; } = null!;

    [Export]
    public NodePath NeedsContainerPath { get; set; } = null!;

    [Export]
    public NodePath BuffsContainerPath { get; set; } = null!;

    [Export]
    public NodePath AttachmentsContainerPath { get; set; } = null!;

    private Label? _nameLabel;
    private Label? _moodLabel;
    private Label? _goldLabel;
    private Label? _actionLabel;
    private VBoxContainer? _needsContainer;
    private VBoxContainer? _buffsContainer;
    private VBoxContainer? _attachmentsContainer;
    private Simulation? _sim;

    private readonly Dictionary<int, ProgressBar> _needBars = new();
    private readonly List<Label> _buffLabels = new();
    private readonly List<Label> _attachmentLabels = new();

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>(NameLabelPath);
        _moodLabel = GetNodeOrNull<Label>(MoodLabelPath);
        _goldLabel = GetNodeOrNull<Label>(GoldLabelPath);
        _actionLabel = GetNodeOrNull<Label>(ActionLabelPath);
        _needsContainer = GetNodeOrNull<VBoxContainer>(NeedsContainerPath);
        _buffsContainer = GetNodeOrNull<VBoxContainer>(BuffsContainerPath);
        _attachmentsContainer = GetNodeOrNull<VBoxContainer>(AttachmentsContainerPath);

        // Start hidden until a pawn is selected
        Visible = false;
    }

    public void ShowPawn(
        RenderPawn pawn,
        NeedsComponent? needs,
        BuffComponent? buffs,
        ContentRegistry content,
        Simulation sim
    )
    {
        _sim = sim;
        Visible = true;

        if (_nameLabel != null)
            _nameLabel.Text = sim.FormatEntityId(pawn.Id);

        if (_moodLabel != null)
        {
            _moodLabel.Text = $"Mood: {pawn.Mood:+0;-0;0}";
            _moodLabel.Modulate =
                pawn.Mood > 20 ? Colors.Lime
                : pawn.Mood < -20 ? Colors.Red
                : Colors.White;
        }

        if (_goldLabel != null)
        {
            _goldLabel.Text = $"Gold: {pawn.Gold}";
            _goldLabel.Modulate =
                pawn.Gold >= 100 ? Colors.Gold
                : pawn.Gold >= 50 ? Colors.Yellow
                : pawn.Gold > 0 ? Colors.White
                : Colors.Red;
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

        // Update attachments
        UpdateAttachmentsDisplay(pawn);
    }

    private void UpdateBuffsDisplay(BuffComponent? buffs, ContentRegistry content)
    {
        if (_buffsContainer == null)
            return;

        // Clear old buff labels
        foreach (var label in _buffLabels)
            label.QueueFree();
        _buffLabels.Clear();

        if (buffs == null || buffs.ActiveBuffs.Count == 0)
        {
            var noBuffsLabel = new Label { Text = "(none)", Modulate = Colors.Gray };
            noBuffsLabel.AddThemeFontSizeOverride("font_size", 16);
            _buffsContainer.AddChild(noBuffsLabel);
            _buffLabels.Add(noBuffsLabel);
            return;
        }

        foreach (var inst in buffs.ActiveBuffs)
        {
            // Get buff name from source
            string buffName = inst.Source switch
            {
                SimGame.Core.BuffSource.Building => content.Buildings.TryGetValue(
                    inst.SourceId,
                    out var bDef
                )
                    ? bDef.Name
                    : "Building",
                SimGame.Core.BuffSource.Work => "Productive",
                SimGame.Core.BuffSource.NeedCritical => content.Needs.TryGetValue(
                    inst.SourceId,
                    out var nDef
                )
                    ? $"{nDef.Name} (Critical)"
                    : "Critical Need",
                SimGame.Core.BuffSource.NeedLow => content.Needs.TryGetValue(
                    inst.SourceId,
                    out var nDef2
                )
                    ? $"{nDef2.Name} (Low)"
                    : "Low Need",
                _ => "Unknown",
            };

            var label = new Label { Text = $"{buffName} ({inst.MoodOffset:+0;-0})" };
            label.AddThemeFontSizeOverride("font_size", 16);

            // Color based on mood impact
            if (inst.MoodOffset > 0)
                label.Modulate = Colors.Lime;
            else if (inst.MoodOffset < 0)
                label.Modulate = Colors.Orange;
            else
                label.Modulate = Colors.White;

            _buffsContainer.AddChild(label);
            _buffLabels.Add(label);
        }
    }

    private void UpdateAttachmentsDisplay(RenderPawn pawn)
    {
        if (_attachmentsContainer == null)
            return;

        // Clear old attachment labels
        foreach (var label in _attachmentLabels)
            label.QueueFree();
        _attachmentLabels.Clear();

        if (pawn.Attachments == null || pawn.Attachments.Count == 0)
        {
            var noAttachLabel = new Label { Text = "(none)", Modulate = Colors.Gray };
            noAttachLabel.AddThemeFontSizeOverride("font_size", 14);
            _attachmentsContainer.AddChild(noAttachLabel);
            _attachmentLabels.Add(noAttachLabel);
            return;
        }

        foreach (var (buildingId, strength) in pawn.Attachments)
        {
            var formattedId = _sim?.FormatEntityId(buildingId) ?? buildingId.ToString();
            var label = new Label { Text = $"{formattedId}: {strength}/10" };
            label.AddThemeFontSizeOverride("font_size", 14);

            // Color based on attachment strength
            if (strength >= 8)
                label.Modulate = Colors.Lime;
            else if (strength >= 5)
                label.Modulate = Colors.Yellow;
            else
                label.Modulate = Colors.White;

            _attachmentsContainer.AddChild(label);
            _attachmentLabels.Add(label);
        }
    }

    public new void Hide()
    {
        Visible = false;
    }

    private ProgressBar CreateNeedBar(string needName)
    {
        var container = new HBoxContainer();

        var label = new Label { Text = needName, CustomMinimumSize = new Vector2(70, 0) };
        label.AddThemeFontSizeOverride("font_size", 16);

        var bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(50, 10),
            MaxValue = 100,
            ShowPercentage = false,
        };

        container.AddChild(label);
        container.AddChild(bar);
        _needsContainer!.AddChild(container);

        return bar;
    }
}
