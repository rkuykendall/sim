using Godot;
using SimGame.Core;

namespace SimGame.Godot;

/// <summary>
/// Manages music playback in response to MusicSystem state changes.
/// Reads theme changes from RenderSnapshot and controls the MidiPlayer.
/// Signals back to Core when music finishes playing.
/// </summary>
public partial class MusicManager : Node
{
    private GodotObject? _midiPlayer;
    private string? _currentPlayingFile;
    private string? _lastThemeName;
    private GameRoot? _gameRoot;

    [Export]
    public NodePath MidiPlayerPath { get; set; } = "";

    [Export]
    public NodePath GameRootPath { get; set; } = "";

    public override void _Ready()
    {
        if (!string.IsNullOrEmpty(MidiPlayerPath))
        {
            _midiPlayer = GetNodeOrNull(MidiPlayerPath);
            if (_midiPlayer == null)
            {
                GD.PushWarning($"MusicManager: Could not find MidiPlayer at {MidiPlayerPath}");
            }
            else
            {
                // Connect to MidiPlayer's "finished" signal
                _midiPlayer.Connect("finished", Callable.From(OnMusicFinished));

                // Log initial MidiPlayer state
                var initialFile = _midiPlayer.Get("file");
                var initialPlaying = _midiPlayer.Get("playing");
                var volume = _midiPlayer.Get("volume_db");
            }
        }
        else
        {
            GD.PrintErr("[MusicManager] ERROR: MidiPlayerPath is empty!");
        }

        if (!string.IsNullOrEmpty(GameRootPath))
        {
            _gameRoot = GetNodeOrNull<GameRoot>(GameRootPath);
            if (_gameRoot == null)
            {
                GD.PushWarning($"MusicManager: Could not find GameRoot at {GameRootPath}");
            }
        }
        else
        {
            GD.PrintErr("[MusicManager] ERROR: GameRootPath is empty!");
        }
    }

    /// <summary>
    /// Called by GameRoot with the latest RenderSnapshot.
    /// Detects theme changes and updates music playback accordingly.
    /// </summary>
    public void UpdateMusicState(RenderSnapshot snapshot)
    {
        if (_midiPlayer == null)
            return;

        var themeState = snapshot.Theme;

        // Detect theme change
        if (themeState.CurrentThemeName != _lastThemeName)
        {
            _lastThemeName = themeState.CurrentThemeName;

            GD.Print($"[MusicManager] Theme changed to: {themeState.CurrentThemeName ?? "null"}");

            // Handle music file change
            if (themeState.CurrentMusicFile != _currentPlayingFile)
            {
                if (themeState.CurrentMusicFile == null)
                {
                    // Stop music (silent theme)
                    StopMusic();
                }
                else
                {
                    // Play new music file
                    PlayMusicFile(themeState.CurrentMusicFile);
                }
            }
        }
    }

    private void PlayMusicFile(string filePath)
    {
        if (_midiPlayer == null)
        {
            GD.PrintErr("[MusicManager] ERROR: MidiPlayer is null!");
            return;
        }

        GD.Print($"[MusicManager] Playing: {filePath}");

        // Stop current playback
        _midiPlayer.Call("stop");

        // Set new file
        _midiPlayer.Set("file", filePath);
        _midiPlayer.Set("loop", false); // Don't loop - theme decides duration

        // Start playback using play() method (not just setting playing property)
        _midiPlayer.Call("play");

        // Verify the file was set correctly
        var actualFile = _midiPlayer.Get("file");
        var isPlaying = _midiPlayer.Get("playing");

        _currentPlayingFile = filePath;
    }

    private void StopMusic()
    {
        if (_midiPlayer == null)
            return;

        _midiPlayer.Call("stop");
        _currentPlayingFile = null;
    }

    /// <summary>
    /// Called by MidiPlayer when music file finishes.
    /// Notifies the Core simulation that the music has completed.
    /// </summary>
    private void OnMusicFinished()
    {
        // Notify Core that music finished
        _gameRoot?.OnMusicFinished();

        _currentPlayingFile = null;
    }
}
