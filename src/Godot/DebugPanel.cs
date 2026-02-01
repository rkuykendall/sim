using System.Collections.Generic;
using Godot;
using SimGame.Core;

namespace SimGame.Godot;

/// <summary>
/// Unified debug panel that shows time, pawn info, or building info.
/// Only visible when debug mode is enabled.
/// </summary>
public partial class DebugPanel : PanelContainer
{
    private enum DisplayMode
    {
        Time,
        Pawn,
        Building,
    }

    private bool _debugMode = false;
    private DisplayMode _mode = DisplayMode.Time;
    private Simulation? _sim;
    private ContentRegistry? _content;

    // Time display
    private string _currentSpeed = "1x";
    private RenderTime _currentTime = new();

    // Containers for different modes
    private VBoxContainer _mainContainer = null!;
    private VBoxContainer _timeContainer = null!;
    private VBoxContainer _pawnContainer = null!;
    private VBoxContainer _buildingContainer = null!;

    // Time labels
    private Label _timeLabel = null!;
    private Button _paletteButton = null!;

    // Pawn labels and containers
    private Label _pawnNameLabel = null!;
    private Label _pawnMoodLabel = null!;
    private Label _pawnGoldLabel = null!;
    private Label _pawnActionLabel = null!;
    private VBoxContainer _pawnNeedsContainer = null!;
    private VBoxContainer _pawnBuffsContainer = null!;
    private VBoxContainer _pawnAttachmentsContainer = null!;
    private readonly Dictionary<int, ProgressBar> _needBars = new();
    private readonly List<Label> _buffLabels = new();
    private readonly List<Label> _pawnAttachmentLabels = new();

    // Building labels and containers
    private Label _buildingNameLabel = null!;
    private Label _buildingStatusLabel = null!;
    private Label _buildingDescriptionLabel = null!;
    private VBoxContainer _buildingDebugContainer = null!;
    private readonly List<Label> _buildingDebugLabels = new();

    public override void _Ready()
    {
        BuildUI();
        Visible = false;
    }

    private void BuildUI()
    {
        // Style the panel
        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        styleBox.SetCornerRadiusAll(4);
        styleBox.SetContentMarginAll(10);
        AddThemeStyleboxOverride("panel", styleBox);

        CustomMinimumSize = new Vector2(250, 0);

        _mainContainer = new VBoxContainer();
        AddChild(_mainContainer);

        // Build time container
        _timeContainer = new VBoxContainer { Name = "TimeContainer" };
        _mainContainer.AddChild(_timeContainer);

        _timeLabel = new Label { Name = "TimeLabel" };
        _timeLabel.AddThemeFontSizeOverride("font_size", 20);
        _timeContainer.AddChild(_timeLabel);

        _paletteButton = new Button { Text = "Cycle Palette" };
        _paletteButton.Pressed += OnPaletteButtonPressed;
        _timeContainer.AddChild(_paletteButton);

        // Build pawn container
        _pawnContainer = new VBoxContainer { Name = "PawnContainer" };
        _mainContainer.AddChild(_pawnContainer);
        BuildPawnUI();

        // Build building container
        _buildingContainer = new VBoxContainer { Name = "BuildingContainer" };
        _mainContainer.AddChild(_buildingContainer);
        BuildBuildingUI();

        UpdateContainerVisibility();
    }

    private void BuildPawnUI()
    {
        _pawnNameLabel = new Label();
        _pawnNameLabel.AddThemeFontSizeOverride("font_size", 20);
        _pawnContainer.AddChild(_pawnNameLabel);

        _pawnMoodLabel = new Label();
        _pawnMoodLabel.AddThemeFontSizeOverride("font_size", 16);
        _pawnContainer.AddChild(_pawnMoodLabel);

        _pawnGoldLabel = new Label();
        _pawnGoldLabel.AddThemeFontSizeOverride("font_size", 16);
        _pawnContainer.AddChild(_pawnGoldLabel);

        _pawnActionLabel = new Label();
        _pawnActionLabel.AddThemeFontSizeOverride("font_size", 16);
        _pawnContainer.AddChild(_pawnActionLabel);

        // Needs section
        var needsHeader = new Label { Text = "Needs:" };
        needsHeader.AddThemeFontSizeOverride("font_size", 16);
        needsHeader.Modulate = Colors.Gray;
        _pawnContainer.AddChild(needsHeader);

        _pawnNeedsContainer = new VBoxContainer { Name = "NeedsContainer" };
        _pawnContainer.AddChild(_pawnNeedsContainer);

        // Buffs section
        var buffsHeader = new Label { Text = "Buffs:" };
        buffsHeader.AddThemeFontSizeOverride("font_size", 16);
        buffsHeader.Modulate = Colors.Gray;
        _pawnContainer.AddChild(buffsHeader);

        _pawnBuffsContainer = new VBoxContainer { Name = "BuffsContainer" };
        _pawnContainer.AddChild(_pawnBuffsContainer);

        // Attachments section
        var attachmentsHeader = new Label { Text = "Attachments:" };
        attachmentsHeader.AddThemeFontSizeOverride("font_size", 16);
        attachmentsHeader.Modulate = Colors.Gray;
        _pawnContainer.AddChild(attachmentsHeader);

        _pawnAttachmentsContainer = new VBoxContainer { Name = "AttachmentsContainer" };
        _pawnContainer.AddChild(_pawnAttachmentsContainer);
    }

    private void BuildBuildingUI()
    {
        _buildingNameLabel = new Label();
        _buildingNameLabel.AddThemeFontSizeOverride("font_size", 20);
        _buildingContainer.AddChild(_buildingNameLabel);

        _buildingStatusLabel = new Label();
        _buildingStatusLabel.AddThemeFontSizeOverride("font_size", 16);
        _buildingContainer.AddChild(_buildingStatusLabel);

        _buildingDescriptionLabel = new Label();
        _buildingDescriptionLabel.AddThemeFontSizeOverride("font_size", 16);
        _buildingContainer.AddChild(_buildingDescriptionLabel);

        // Debug info section
        var debugHeader = new Label { Text = "Details:" };
        debugHeader.AddThemeFontSizeOverride("font_size", 16);
        debugHeader.Modulate = Colors.Gray;
        _buildingContainer.AddChild(debugHeader);

        _buildingDebugContainer = new VBoxContainer { Name = "DebugContainer" };
        _buildingContainer.AddChild(_buildingDebugContainer);
    }

    public void Initialize(ContentRegistry content)
    {
        _content = content;
    }

    public void SetSimulation(Simulation sim)
    {
        _sim = sim;
    }

    private void OnPaletteButtonPressed()
    {
        _sim?.CyclePalette();
    }

    public void SetDebugMode(bool enabled)
    {
        _debugMode = enabled;
        UpdateVisibility();
    }

    public void UpdateTime(RenderTime time)
    {
        _currentTime = time;
        if (_mode == DisplayMode.Time)
        {
            _timeLabel.Text = $"{time.TimeString} ({_currentSpeed})";
        }
    }

    public void UpdateSpeed(string speed)
    {
        _currentSpeed = speed;
        if (_mode == DisplayMode.Time)
        {
            _timeLabel.Text = $"{_currentTime.TimeString} ({_currentSpeed})";
        }
    }

    public void ShowPawn(
        RenderPawn pawn,
        NeedsComponent? needs,
        BuffComponent? buffs,
        Simulation sim
    )
    {
        _sim = sim;
        _mode = DisplayMode.Pawn;
        UpdateContainerVisibility();
        UpdateVisibility();

        _pawnNameLabel.Text = sim.FormatEntityId(pawn.Id);

        _pawnMoodLabel.Text = $"Mood: {pawn.Mood:+0;-0;0}";
        _pawnMoodLabel.Modulate =
            pawn.Mood > 20 ? Colors.Lime
            : pawn.Mood < -20 ? Colors.Red
            : Colors.White;

        _pawnGoldLabel.Text = $"Gold: {pawn.Gold}";
        _pawnGoldLabel.Modulate =
            pawn.Gold >= 100 ? Colors.Gold
            : pawn.Gold >= 50 ? Colors.Yellow
            : pawn.Gold > 0 ? Colors.White
            : Colors.Red;

        _pawnActionLabel.Text = pawn.CurrentAction ?? "Idle";

        UpdateNeedsDisplay(needs);
        UpdateBuffsDisplay(buffs);
        UpdatePawnAttachmentsDisplay(pawn);
    }

    public void ShowBuilding(RenderBuilding building, Simulation sim)
    {
        _sim = sim;
        _mode = DisplayMode.Building;
        UpdateContainerVisibility();
        UpdateVisibility();

        _buildingNameLabel.Text = sim.FormatEntityId(building.Id);

        if (building.InUse && building.UsedByName != null)
        {
            _buildingStatusLabel.Text = $"In use by {building.UsedByName}";
            _buildingStatusLabel.Modulate = Colors.Yellow;
        }
        else
        {
            _buildingStatusLabel.Text = "Available";
            _buildingStatusLabel.Modulate = Colors.Lime;
        }

        if (
            _content != null
            && _content.Buildings.TryGetValue(building.BuildingDefId, out var buildingDef)
        )
        {
            string needName = "nothing";
            if (
                buildingDef.SatisfiesNeedId.HasValue
                && _content.Needs.TryGetValue(buildingDef.SatisfiesNeedId.Value, out var needDef)
            )
                needName = needDef.Name;

            _buildingDescriptionLabel.Text =
                $"Satisfies: {needName} (+{buildingDef.NeedSatisfactionAmount:0})";
        }

        UpdateBuildingDebugDisplay(building);
    }

    public void ClearSelection()
    {
        _mode = DisplayMode.Time;
        UpdateContainerVisibility();
        UpdateVisibility();
        _timeLabel.Text = $"{_currentTime.TimeString} ({_currentSpeed})";
    }

    private void UpdateVisibility()
    {
        Visible = _debugMode;
    }

    private void UpdateContainerVisibility()
    {
        _timeContainer.Visible = _mode == DisplayMode.Time;
        _pawnContainer.Visible = _mode == DisplayMode.Pawn;
        _buildingContainer.Visible = _mode == DisplayMode.Building;
    }

    private void UpdateNeedsDisplay(NeedsComponent? needs)
    {
        if (_content == null || needs == null)
            return;

        foreach (var (needId, value) in needs.Needs)
        {
            if (!_content.Needs.TryGetValue(needId, out var needDef))
                continue;

            if (!_needBars.TryGetValue(needId, out var bar))
            {
                bar = CreateNeedBar(needDef.Name);
                _needBars[needId] = bar;
            }

            bar.Value = value;

            if (value < needDef.CriticalThreshold)
                bar.Modulate = Colors.Red;
            else if (value < needDef.LowThreshold)
                bar.Modulate = Colors.Yellow;
            else
                bar.Modulate = Colors.Lime;
        }
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
        _pawnNeedsContainer.AddChild(container);

        return bar;
    }

    private void UpdateBuffsDisplay(BuffComponent? buffs)
    {
        foreach (var label in _buffLabels)
            label.QueueFree();
        _buffLabels.Clear();

        if (_content == null || buffs == null || buffs.ActiveBuffs.Count == 0)
        {
            var noBuffsLabel = new Label { Text = "(none)", Modulate = Colors.Gray };
            noBuffsLabel.AddThemeFontSizeOverride("font_size", 16);
            _pawnBuffsContainer.AddChild(noBuffsLabel);
            _buffLabels.Add(noBuffsLabel);
            return;
        }

        foreach (var inst in buffs.ActiveBuffs)
        {
            string buffName = inst.Source switch
            {
                BuffSource.Building => _content.Buildings.TryGetValue(inst.SourceId, out var bDef)
                    ? bDef.Name
                    : "Building",
                BuffSource.Work => "Productive",
                BuffSource.NeedCritical => _content.Needs.TryGetValue(inst.SourceId, out var nDef)
                    ? $"{nDef.Name} (Critical)"
                    : "Critical Need",
                BuffSource.NeedLow => _content.Needs.TryGetValue(inst.SourceId, out var nDef2)
                    ? $"{nDef2.Name} (Low)"
                    : "Low Need",
                _ => "Unknown",
            };

            var label = new Label { Text = $"{buffName} ({inst.MoodOffset:+0;-0})" };
            label.AddThemeFontSizeOverride("font_size", 16);

            if (inst.MoodOffset > 0)
                label.Modulate = Colors.Lime;
            else if (inst.MoodOffset < 0)
                label.Modulate = Colors.Orange;
            else
                label.Modulate = Colors.White;

            _pawnBuffsContainer.AddChild(label);
            _buffLabels.Add(label);
        }
    }

    private void UpdatePawnAttachmentsDisplay(RenderPawn pawn)
    {
        foreach (var label in _pawnAttachmentLabels)
            label.QueueFree();
        _pawnAttachmentLabels.Clear();

        if (pawn.Attachments == null || pawn.Attachments.Count == 0)
        {
            var noAttachLabel = new Label { Text = "(none)", Modulate = Colors.Gray };
            noAttachLabel.AddThemeFontSizeOverride("font_size", 14);
            _pawnAttachmentsContainer.AddChild(noAttachLabel);
            _pawnAttachmentLabels.Add(noAttachLabel);
            return;
        }

        foreach (var (buildingId, strength) in pawn.Attachments)
        {
            var formattedId = _sim?.FormatEntityId(buildingId) ?? buildingId.ToString();
            var label = new Label { Text = $"{formattedId}: {strength}/10" };
            label.AddThemeFontSizeOverride("font_size", 14);

            if (strength >= 8)
                label.Modulate = Colors.Lime;
            else if (strength >= 5)
                label.Modulate = Colors.Yellow;
            else
                label.Modulate = Colors.White;

            _pawnAttachmentsContainer.AddChild(label);
            _pawnAttachmentLabels.Add(label);
        }
    }

    private void UpdateBuildingDebugDisplay(RenderBuilding building)
    {
        foreach (var label in _buildingDebugLabels)
            label.QueueFree();
        _buildingDebugLabels.Clear();

        // Gold info
        var goldLabel = new Label { Text = $"Gold: {building.Gold}" };
        goldLabel.AddThemeFontSizeOverride("font_size", 14);
        goldLabel.Modulate =
            building.Gold >= 100 ? Colors.Gold
            : building.Gold >= 50 ? Colors.Yellow
            : building.Gold > 0 ? Colors.White
            : Colors.Gray;
        _buildingDebugContainer.AddChild(goldLabel);
        _buildingDebugLabels.Add(goldLabel);

        // Capacity info
        var capacityLabel = new Label
        {
            Text =
                $"Capacity: {building.CurrentUsers}/{building.Capacity}"
                + (building.Phase > 0 ? $" (phase {building.Phase})" : ""),
        };
        capacityLabel.AddThemeFontSizeOverride("font_size", 14);
        capacityLabel.Modulate =
            building.CurrentUsers >= building.Capacity ? Colors.Orange
            : building.CurrentUsers > 0 ? Colors.Yellow
            : Colors.White;
        _buildingDebugContainer.AddChild(capacityLabel);
        _buildingDebugLabels.Add(capacityLabel);

        // Use cost
        if (building.Cost > 0)
        {
            var useCostLabel = new Label { Text = $"Use: {building.Cost}g" };
            useCostLabel.AddThemeFontSizeOverride("font_size", 14);
            useCostLabel.Modulate = Colors.Cyan;
            _buildingDebugContainer.AddChild(useCostLabel);
            _buildingDebugLabels.Add(useCostLabel);
        }

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

            float percent = building.CurrentResource.Value / building.MaxResource.Value;
            if (percent < 0.2f)
                resourceLabel.Modulate = Colors.Red;
            else if (percent < 0.5f)
                resourceLabel.Modulate = Colors.Orange;
            else
                resourceLabel.Modulate = Colors.Lime;

            _buildingDebugContainer.AddChild(resourceLabel);
            _buildingDebugLabels.Add(resourceLabel);

            // Work economics
            if (building.CanBeWorkedAt == true)
            {
                string workText =
                    building.WorkBuyIn > 0
                        ? $"Work: {building.WorkBuyIn}g in / {building.Payout}g out"
                        : $"Work: {building.Payout}g out";
                var workLabel = new Label { Text = workText };
                workLabel.AddThemeFontSizeOverride("font_size", 14);
                workLabel.Modulate = Colors.Green;
                _buildingDebugContainer.AddChild(workLabel);
                _buildingDebugLabels.Add(workLabel);
            }
        }

        // Attachment info
        if (building.Attachments != null && building.Attachments.Count > 0)
        {
            var attachHeader = new Label { Text = "Attachments:" };
            attachHeader.AddThemeFontSizeOverride("font_size", 14);
            attachHeader.Modulate = Colors.Gray;
            _buildingDebugContainer.AddChild(attachHeader);
            _buildingDebugLabels.Add(attachHeader);

            foreach (var (pawnId, strength) in building.Attachments)
            {
                var formattedId = _sim?.FormatEntityId(pawnId) ?? pawnId.ToString();
                var attachLabel = new Label { Text = $"  {formattedId}: {strength}/10" };
                attachLabel.AddThemeFontSizeOverride("font_size", 14);

                if (strength >= 8)
                    attachLabel.Modulate = Colors.Lime;
                else if (strength >= 5)
                    attachLabel.Modulate = Colors.Yellow;
                else
                    attachLabel.Modulate = Colors.White;

                _buildingDebugContainer.AddChild(attachLabel);
                _buildingDebugLabels.Add(attachLabel);
            }
        }
    }
}
