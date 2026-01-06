using Godot;
using SimGame.Core;

public partial class TimeDisplay : PanelContainer
{
    [Export]
    public NodePath TimeLabelPath { get; set; } = null!;

    private Label? _timeLabel;
    private string _currentSpeed = "1x";

    public override void _Ready()
    {
        _timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
    }

    public void UpdateTime(RenderTime time)
    {
        if (_timeLabel != null)
        {
            // Append speed to time string
            _timeLabel.Text = $"{time.TimeString} ({_currentSpeed})";
        }
    }

    public void UpdateSpeed(string speedText)
    {
        _currentSpeed = speedText;
        // No need to update immediately; next UpdateTime() call will show it
    }
}
