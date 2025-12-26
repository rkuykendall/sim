using Godot;

namespace SimGame.Godot;

public partial class CRTShaderController : CanvasLayer
{
    private ColorRect? _shaderRect;
    private bool _isEnabled = false;

    public override void _Ready()
    {
        _shaderRect = GetNode<ColorRect>("CRTShaderRect");

        // Start with shader disabled
        if (_shaderRect != null)
        {
            _shaderRect.Visible = false;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.F4)
            {
                ToggleShader();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void ToggleShader()
    {
        _isEnabled = !_isEnabled;

        if (_shaderRect != null)
        {
            _shaderRect.Visible = _isEnabled;
        }
    }
}
