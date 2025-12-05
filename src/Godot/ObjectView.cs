using Godot;

public partial class ObjectView : Node2D
{
    [Export] public Label NameLabel { get; set; } = null!;
    [Export] public ColorRect Body { get; set; } = null!;

    public void SetObjectInfo(string name, bool inUse)
    {
        if (NameLabel != null)
            NameLabel.Text = name;

        if (Body != null)
            Body.Color = inUse ? Colors.Yellow : new Color(0.6f, 0.4f, 0.2f);
    }
}
