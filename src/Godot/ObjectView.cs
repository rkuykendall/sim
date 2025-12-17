using Godot;
using SimGame.Godot;

public partial class ObjectView : Node2D
{
    [Export] public Label? NameLabel { get; set; }
    [Export] public ColorRect? Body { get; set; }

    public override void _Ready()
    {
        // Fallback: Get Body manually if export didn't wire up
        if (Body == null)
            Body = GetNodeOrNull<ColorRect>("Body");
    }

    public void SetObjectInfo(string name, bool inUse, int colorIndex)
    {
        if (NameLabel != null)
            NameLabel.Text = name;

        if (Body != null)
        {
            // Use palette color, brighten if in use
            var baseColor = GameColorPalette.Colors[colorIndex];
            Body.Color = inUse ? baseColor.Lightened(0.3f) : baseColor;
        }
    }
}
