using Godot;
using SimGame.Core;

public partial class TimeDisplay : PanelContainer
{
    [Export]
    public NodePath TimeLabelPath { get; set; } = null!;

    private Label? _timeLabel;

    public override void _Ready()
    {
        _timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
    }

    public void UpdateTime(RenderTime time)
    {
        if (_timeLabel != null)
            _timeLabel.Text = time.TimeString;
    }
}
