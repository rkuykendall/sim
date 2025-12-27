using Godot;

namespace SimGame.Godot;

public partial class CRTShaderController : CanvasLayer
{
    private ColorRect? _shaderRect;
    private ShaderMaterial? _shaderMaterial;
    private bool _isEnabled = true;

    public override void _Ready()
    {
        _shaderRect = GetNode<ColorRect>("CRTShaderRect");

        // Get the shader material
        if (_shaderRect != null)
        {
            _shaderMaterial = _shaderRect.Material as ShaderMaterial;
            // Start with shader disabled
            _shaderRect.Visible = true;
        }
    }

    public void SetTimeOfDay(float timeOfDay)
    {
        if (_shaderMaterial != null)
        {
            _shaderMaterial.SetShaderParameter("time_of_day", timeOfDay);
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
