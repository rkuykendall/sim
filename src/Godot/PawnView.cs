using Godot;

public partial class PawnView : Node2D
{
    [Export] public Label NameLabel { get; set; } = null!;
    [Export] public Label ActionLabel { get; set; } = null!;
    [Export] public ColorRect Body { get; set; } = null!;

    private bool _selected = false;
    private float _baseMood = 0f;

    public void SetMood(float mood)
    {
        _baseMood = mood;
        UpdateColors();
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

    public void SetSelected(bool selected)
    {
        _selected = selected;
        UpdateColors();
    }

    private void UpdateColors()
    {
        if (NameLabel != null)
        {
            if (_baseMood > 20) NameLabel.Modulate = Colors.Lime;
            else if (_baseMood < -20) NameLabel.Modulate = Colors.Red;
            else NameLabel.Modulate = Colors.White;
        }

        if (Body != null)
        {
            if (_selected)
                Body.Color = Colors.White;
            else
                Body.Color = new Color(0.2f, 0.6f, 1f);
        }
    }
}
