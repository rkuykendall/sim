using Godot;
using SimGame.Core;

public partial class ObjectInfoPanel : PanelContainer
{
    [Export] public NodePath NameLabelPath { get; set; } = null!;
    [Export] public NodePath StatusLabelPath { get; set; } = null!;
    [Export] public NodePath DescriptionLabelPath { get; set; } = null!;

    private Label? _nameLabel;
    private Label? _statusLabel;
    private Label? _descriptionLabel;

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>(NameLabelPath);
        _statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
        _descriptionLabel = GetNodeOrNull<Label>(DescriptionLabelPath);
        
        Visible = false;
    }

    public void ShowObject(RenderObject obj)
    {
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

        if (_descriptionLabel != null && ContentDatabase.Objects.TryGetValue(obj.ObjectDefId, out var objDef))
        {
            string needName = "nothing";
            if (objDef.SatisfiesNeedId.HasValue && ContentDatabase.Needs.TryGetValue(objDef.SatisfiesNeedId.Value, out var needDef))
                needName = needDef.Name;

            _descriptionLabel.Text = $"Satisfies: {needName} (+{objDef.NeedSatisfactionAmount:0})";
        }
    }

    public new void Hide()
    {
        Visible = false;
    }
}
