using Godot;
using SimGame.Core;

public partial class PawnInfoPanel : PanelContainer
{
    [Export] public NodePath NameLabelPath { get; set; } = null!;
    [Export] public NodePath MoodLabelPath { get; set; } = null!;
    [Export] public NodePath ActionLabelPath { get; set; } = null!;
    [Export] public NodePath NeedsContainerPath { get; set; } = null!;

    private Label? _nameLabel;
    private Label? _moodLabel;
    private Label? _actionLabel;
    private VBoxContainer? _needsContainer;

    private readonly System.Collections.Generic.Dictionary<int, ProgressBar> _needBars = new();

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>(NameLabelPath);
        _moodLabel = GetNodeOrNull<Label>(MoodLabelPath);
        _actionLabel = GetNodeOrNull<Label>(ActionLabelPath);
        _needsContainer = GetNodeOrNull<VBoxContainer>(NeedsContainerPath);
        
        // Start hidden until a pawn is selected
        Visible = false;
    }

    public void ShowPawn(RenderPawn pawn, NeedsComponent? needs)
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

        if (_needsContainer == null)
        {
            GD.PrintErr("PawnInfoPanel: NeedsContainer is null!");
            return;
        }

        if (needs == null)
        {
            GD.Print($"PawnInfoPanel: No needs component for pawn {pawn.Name}");
            return;
        }

        foreach (var (needId, value) in needs.Needs)
        {
            if (!ContentDatabase.Needs.TryGetValue(needId, out var needDef))
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
    public void Hide()
    {
        Visible = false;
    }

    private ProgressBar CreateNeedBar(string needName)
    {
        var container = new HBoxContainer();
        
        var label = new Label
        {
            Text = needName,
            CustomMinimumSize = new Vector2(70, 0)
        };
        
        var bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(100, 20),
            MaxValue = 100,
            ShowPercentage = false
        };

        container.AddChild(label);
        container.AddChild(bar);
        _needsContainer!.AddChild(container);

        return bar;
    }
}
