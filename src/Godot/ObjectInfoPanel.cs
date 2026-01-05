using System.Collections.Generic;
using System.Linq;
using Godot;
using SimGame.Core;

public partial class ObjectInfoPanel : PanelContainer
{
    [Export]
    public NodePath NameLabelPath { get; set; } = null!;

    [Export]
    public NodePath StatusLabelPath { get; set; } = null!;

    [Export]
    public NodePath DescriptionLabelPath { get; set; } = null!;

    [Export]
    public NodePath DebugContainerPath { get; set; } = null!;

    private Label? _nameLabel;
    private Label? _statusLabel;
    private Label? _descriptionLabel;
    private VBoxContainer? _debugContainer;
    private Simulation? _sim;

    private readonly List<Label> _debugLabels = new();

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>(NameLabelPath);
        _statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
        _descriptionLabel = GetNodeOrNull<Label>(DescriptionLabelPath);
        _debugContainer = GetNodeOrNull<VBoxContainer>(DebugContainerPath);

        Visible = false;
    }

    public void ShowObject(RenderObject obj, ContentRegistry content, Simulation sim)
    {
        _sim = sim;
        Visible = true;

        if (_nameLabel != null)
            _nameLabel.Text = obj.Name;

        if (_statusLabel != null)
        {
            if (obj.InUse && obj.UsedByName != null)
            {
                _statusLabel.Text = $"In use by {obj.UsedByName}";
                _statusLabel.Modulate = Colors.Yellow;
            }
            else
            {
                _statusLabel.Text = "Available";
                _statusLabel.Modulate = Colors.Lime;
            }
        }

        if (
            _descriptionLabel != null
            && content.Objects.TryGetValue(obj.ObjectDefId, out var objDef)
        )
        {
            string needName = "nothing";
            if (
                objDef.SatisfiesNeedId.HasValue
                && content.Needs.TryGetValue(objDef.SatisfiesNeedId.Value, out var needDef)
            )
                needName = needDef.Name;

            _descriptionLabel.Text = $"Satisfies: {needName} (+{objDef.NeedSatisfactionAmount:0})";
        }

        // Update debug info
        UpdateDebugDisplay(obj);
    }

    private void UpdateDebugDisplay(RenderObject obj)
    {
        if (_debugContainer == null)
            return;

        // Clear old debug labels
        foreach (var label in _debugLabels)
            label.QueueFree();
        _debugLabels.Clear();

        // Attachment info
        if (obj.Attachments != null && obj.Attachments.Count > 0)
        {
            var attachHeader = new Label { Text = "Attachments:" };
            attachHeader.AddThemeFontSizeOverride("font_size", 14);
            attachHeader.Modulate = Colors.Gray;
            _debugContainer.AddChild(attachHeader);
            _debugLabels.Add(attachHeader);

            foreach (var (pawnId, strength) in obj.Attachments.OrderByDescending(kv => kv.Value))
            {
                var formattedId = _sim?.FormatEntityId(pawnId) ?? $"Pawn #{pawnId.Value}";
                var attachLabel = new Label { Text = $"  {formattedId}: {strength}/10" };
                attachLabel.AddThemeFontSizeOverride("font_size", 14);

                // Color based on attachment strength
                if (strength >= 8)
                    attachLabel.Modulate = Colors.Lime;
                else if (strength >= 5)
                    attachLabel.Modulate = Colors.Yellow;
                else
                    attachLabel.Modulate = Colors.White;

                _debugContainer.AddChild(attachLabel);
                _debugLabels.Add(attachLabel);
            }
        }
    }

    public new void Hide()
    {
        Visible = false;
    }
}
