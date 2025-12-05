using Godot;

public partial class PawnView : Node2D
{
    [Export] public Label NameLabel { get; set; } = null!;
    [Export] public Label ActionLabel { get; set; } = null!;

    public void SetMood(float mood)
    {
        if (NameLabel == null) return;

        if (mood > 20) NameLabel.Modulate = Colors.Lime;
        else if (mood < -20) NameLabel.Modulate = Colors.Red;
        else NameLabel.Modulate = Colors.White;
    }

    public void SetNameLabel(string name)
    {
        if (NameLabel != null)
            NameLabel.Text = name;
    }

    public void SetAction(string? action)
    {
        if (ActionLabel != null)
            ActionLabel.Text = action ?? "Idle";
    }
}
