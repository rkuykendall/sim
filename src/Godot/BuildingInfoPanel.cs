using System.Collections.Generic;
using Godot;
using SimGame.Core;

public partial class BuildingInfoPanel : PanelContainer
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

    public void ShowBuilding(RenderBuilding building, ContentRegistry content, Simulation sim)
    {
        _sim = sim;
        Visible = true;

        if (_nameLabel != null)
            _nameLabel.Text = sim.FormatEntityId(building.Id);

        if (_statusLabel != null)
        {
            if (building.InUse && building.UsedByName != null)
            {
                _statusLabel.Text = $"In use by {building.UsedByName}";
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
            && content.Buildings.TryGetValue(building.BuildingDefId, out var buildingDef)
        )
        {
            string needName = "nothing";
            if (
                buildingDef.SatisfiesNeedId.HasValue
                && content.Needs.TryGetValue(buildingDef.SatisfiesNeedId.Value, out var needDef)
            )
                needName = needDef.Name;

            _descriptionLabel.Text =
                $"Satisfies: {needName} (+{buildingDef.NeedSatisfactionAmount:0})";
        }

        // Update debug info
        UpdateDebugDisplay(building);
    }

    private void UpdateDebugDisplay(RenderBuilding building)
    {
        if (_debugContainer == null)
            return;

        // Clear old debug labels
        foreach (var label in _debugLabels)
            label.QueueFree();
        _debugLabels.Clear();

        // Resource info
        if (
            building.ResourceType != null
            && building.CurrentResource.HasValue
            && building.MaxResource.HasValue
        )
        {
            var resourceLabel = new Label
            {
                Text =
                    $"Resources: {building.CurrentResource.Value:0}/{building.MaxResource.Value:0} {building.ResourceType}",
            };
            resourceLabel.AddThemeFontSizeOverride("font_size", 14);

            // Color based on resource level
            float percent = building.CurrentResource.Value / building.MaxResource.Value;
            if (percent < 0.2f)
                resourceLabel.Modulate = Colors.Red;
            else if (percent < 0.5f)
                resourceLabel.Modulate = Colors.Orange;
            else
                resourceLabel.Modulate = Colors.Lime;

            _debugContainer.AddChild(resourceLabel);
            _debugLabels.Add(resourceLabel);

            // Can be worked at flag
            if (building.CanBeWorkedAt == true)
            {
                var workLabel = new Label { Text = "Can be worked at" };
                workLabel.AddThemeFontSizeOverride("font_size", 14);
                workLabel.Modulate = Colors.Cyan;
                _debugContainer.AddChild(workLabel);
                _debugLabels.Add(workLabel);
            }
        }

        // Attachment info
        if (building.Attachments != null && building.Attachments.Count > 0)
        {
            var attachHeader = new Label { Text = "Attachments:" };
            attachHeader.AddThemeFontSizeOverride("font_size", 14);
            attachHeader.Modulate = Colors.Gray;
            _debugContainer.AddChild(attachHeader);
            _debugLabels.Add(attachHeader);

            foreach (var (pawnId, strength) in building.Attachments)
            {
                var formattedId = _sim?.FormatEntityId(pawnId) ?? pawnId.ToString();
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
