using Godot;

public partial class PawnView : Node2D
{
    [Export] public NodePath NameLabelPath { get; set; } = null!;
    [Export] public NodePath ActionLabelPath { get; set; } = null!;
    [Export] public NodePath BodyPath { get; set; } = null!;

    private Label? _nameLabel;
    private Label? _actionLabel;
    private ColorRect? _body;

    private bool _selected = false;
    private float _baseMood = 0f;

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>(NameLabelPath);
        _actionLabel = GetNodeOrNull<Label>(ActionLabelPath);
        _body = GetNodeOrNull<ColorRect>(BodyPath);
    }

    public void SetMood(float mood)
    {
        _baseMood = mood;
        UpdateColors();
    }

    public void SetNameLabel(string name)
    {
        if (_nameLabel != null)
            _nameLabel.Text = name;
    }

    public void SetAction(string? action)
    {
        if (_actionLabel != null)
            _actionLabel.Text = action ?? "Idle";
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        UpdateColors();
    }

    private void UpdateColors()
    {
        if (_nameLabel != null)
        {
            if (_baseMood > 20) _nameLabel.Modulate = Colors.Lime;
            else if (_baseMood < -20) _nameLabel.Modulate = Colors.Red;
            else _nameLabel.Modulate = Colors.White;
        }

        if (_body != null)
        {
            if (_selected)
                _body.Color = Colors.White;
            else
                _body.Color = new Color(0.2f, 0.6f, 1f);
        }
    }
}
