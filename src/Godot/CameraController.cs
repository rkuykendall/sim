using Godot;

namespace SimGame.Godot;

public partial class CameraController : Camera2D
{
    [Export]
    public float ZoomSpeed = 10f;

    [Export]
    public float PanSpeed = 1000f;

    // World bounds for camera clamping (set by GameRoot)
    private Vector2 _worldSize = Vector2.Zero;
    private bool _hasBounds = false;

    private Vector2 _zoomTarget;
    private Vector2 _dragStartMousePos;
    private Vector2 _dragStartCameraPos;
    private bool _isDragging;

    public override void _Ready()
    {
        _zoomTarget = Zoom;
    }

    /// <summary>
    /// Set the world bounds for camera clamping.
    /// Camera will be constrained so the world edge stops at the center of the viewport.
    /// </summary>
    public void SetWorldBounds(float worldWidth, float worldHeight)
    {
        _worldSize = new Vector2(worldWidth, worldHeight);
        _hasBounds = true;
    }

    /// <summary>
    /// Clamp camera position to world bounds.
    /// The world edge will stop at the center of the viewport.
    /// </summary>
    private void ClampToWorldBounds()
    {
        if (!_hasBounds)
            return;

        Position = new Vector2(
            Mathf.Clamp(Position.X, 0, _worldSize.X),
            Mathf.Clamp(Position.Y, 0, _worldSize.Y)
        );
    }

    public override void _Process(double delta)
    {
        ProcessZoom((float)delta);
        ProcessSimplePan((float)delta);
        ProcessClickAndDrag();
        ClampToWorldBounds();
    }

    private void ProcessZoom(float delta)
    {
        if (Input.IsActionJustPressed("camera_zoom_in"))
        {
            _zoomTarget *= 2f;
        }

        if (Input.IsActionJustPressed("camera_zoom_out"))
        {
            _zoomTarget *= 0.5f;
        }

        // Use Slerp for smooth interpolated zooming
        Zoom = Zoom.Slerp(_zoomTarget, ZoomSpeed * delta);
    }

    private void ProcessSimplePan(float delta)
    {
        var moveAmount = Vector2.Zero;

        if (Input.IsActionPressed("camera_move_right"))
            moveAmount.X += 1;

        if (Input.IsActionPressed("camera_move_left"))
            moveAmount.X -= 1;

        if (Input.IsActionPressed("camera_move_up"))
            moveAmount.Y -= 1;

        if (Input.IsActionPressed("camera_move_down"))
            moveAmount.Y += 1;

        moveAmount = moveAmount.Normalized();

        // Apply zoom-aware speed so panning feels consistent regardless of zoom level
        Position += moveAmount * delta * PanSpeed * (1 / Zoom.X);
    }

    private void ProcessClickAndDrag()
    {
        if (!_isDragging && Input.IsActionJustPressed("camera_pan"))
        {
            _dragStartMousePos = GetViewport().GetMousePosition();
            _dragStartCameraPos = Position;
            _isDragging = true;
        }

        if (_isDragging && Input.IsActionJustReleased("camera_pan"))
        {
            _isDragging = false;
        }

        if (_isDragging)
        {
            var moveVector = GetViewport().GetMousePosition() - _dragStartMousePos;
            Position = _dragStartCameraPos - moveVector * (1 / Zoom.X);
        }
    }
}
