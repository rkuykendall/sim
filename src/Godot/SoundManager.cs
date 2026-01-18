using Godot;

namespace SimGame.Godot;

/// <summary>
/// Manages sound effect playback for UI and game actions.
/// Preloads all sounds on startup for instant playback.
/// </summary>
public partial class SoundManager : Node
{
    private const string SfxPath = "res://sfx/";

    // Multiple players for overlapping sounds
    private AudioStreamPlayer _uiPlayer = null!;
    private AudioStreamPlayer _actionPlayer = null!;
    private AudioStreamPlayer _softActionPlayer = null!;

    // Preloaded sounds
    private AudioStream? _clickSound;
    private AudioStream? _selectSound;
    private AudioStream? _paintSound;
    private AudioStream? _paintTickSound;
    private AudioStream? _buildSound;
    private AudioStream? _deleteSound;

    [Export]
    public float UiVolume { get; set; } = -5f;

    [Export]
    public float ActionVolume { get; set; } = -5f;

    public override void _Ready()
    {
        // Create audio players
        _uiPlayer = new AudioStreamPlayer();
        _uiPlayer.VolumeDb = UiVolume;
        _uiPlayer.Bus = "Master";
        AddChild(_uiPlayer);

        _actionPlayer = new AudioStreamPlayer();
        _actionPlayer.VolumeDb = ActionVolume;
        _actionPlayer.Bus = "Master";
        AddChild(_actionPlayer);

        _softActionPlayer = new AudioStreamPlayer();
        _softActionPlayer.VolumeDb = ActionVolume - 15f; // Half volume (-6dB)
        _softActionPlayer.Bus = "Master";
        AddChild(_softActionPlayer);

        // Preload sounds
        _clickSound = LoadSound("ui_click.ogg");
        _selectSound = LoadSound("ui_select.ogg");
        _paintSound = LoadSound("paint.ogg");
        _paintTickSound = LoadSound("paint_tick.ogg");
        _buildSound = LoadSound("build.ogg");
        _deleteSound = LoadSound("delete.ogg");
    }

    private AudioStream? LoadSound(string filename)
    {
        var path = SfxPath + filename;
        var sound = GD.Load<AudioStream>(path);
        if (sound == null)
        {
            GD.PushWarning($"[SoundManager] Could not load sound: {path}");
        }
        return sound;
    }

    /// <summary>
    /// Play UI click sound (button clicks, tool selection).
    /// </summary>
    public void PlayClick()
    {
        PlaySound(_uiPlayer, _clickSound);
    }

    /// <summary>
    /// Play UI select sound (option selection, menu navigation).
    /// </summary>
    public void PlaySelect()
    {
        PlaySound(_uiPlayer, _selectSound);
    }

    /// <summary>
    /// Play terrain painting sound.
    /// </summary>
    public void PlayPaint()
    {
        PlaySound(_actionPlayer, _paintSound);
    }

    /// <summary>
    /// Play lighter tick sound for drag-painting tiles.
    /// </summary>
    public void PlayPaintTick()
    {
        PlaySound(_softActionPlayer, _paintTickSound);
    }

    /// <summary>
    /// Play building placement sound.
    /// </summary>
    public void PlayBuild()
    {
        PlaySound(_actionPlayer, _buildSound);
    }

    /// <summary>
    /// Play deletion sound.
    /// </summary>
    public void PlayDelete()
    {
        PlaySound(_actionPlayer, _deleteSound);
    }

    private void PlaySound(AudioStreamPlayer player, AudioStream? sound)
    {
        if (sound == null)
            return;

        player.Stream = sound;
        player.Play();
    }
}
