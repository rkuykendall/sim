using Godot;
using SimGame.Core;

namespace SimGame.Godot;

/// <summary>
/// Manages music playback in response to MusicSystem state changes.
/// Reads theme changes from RenderSnapshot and controls the AudioStreamPlayer.
/// Signals back to Core when music finishes playing.
/// </summary>
public partial class MusicManager : Node
{
    private AudioStreamPlayer? _audioPlayer;
    private string? _currentPlayingFile;
    private string? _lastThemeName;
    private GameRoot? _gameRoot;

    [Export]
    public NodePath AudioPlayerPath { get; set; } = "";

    [Export]
    public NodePath GameRootPath { get; set; } = "";

    public override void _Ready()
    {
        if (!string.IsNullOrEmpty(AudioPlayerPath))
        {
            _audioPlayer = GetNodeOrNull<AudioStreamPlayer>(AudioPlayerPath);
            if (_audioPlayer == null)
            {
                GD.PushWarning(
                    $"MusicManager: Could not find AudioStreamPlayer at {AudioPlayerPath}"
                );
            }
            else
            {
                // Connect to AudioStreamPlayer's "finished" signal
                _audioPlayer.Finished += OnMusicFinished;
            }
        }
        else
        {
            GD.PrintErr("[MusicManager] ERROR: AudioPlayerPath is empty!");
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
        if (_audioPlayer == null)
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
        if (_audioPlayer == null)
        {
            GD.PrintErr("[MusicManager] ERROR: AudioStreamPlayer is null!");
            return;
        }

        GD.Print($"[MusicManager] Playing: {filePath}");

        // Stop current playback
        _audioPlayer.Stop();

        // Load and set the audio stream
        var audioStream = GD.Load<AudioStream>(filePath);
        if (audioStream == null)
        {
            GD.PrintErr($"[MusicManager] ERROR: Could not load audio file at {filePath}");
            return;
        }

        _audioPlayer.Stream = audioStream;

        // Start playback
        _audioPlayer.Play();

        _currentPlayingFile = filePath;
    }

    private void StopMusic()
    {
        if (_audioPlayer == null)
            return;

        _audioPlayer.Stop();
        _currentPlayingFile = null;
    }

    /// <summary>
    /// Called by AudioStreamPlayer when music file finishes.
    /// Notifies the Core simulation that the music has completed.
    /// </summary>
    private void OnMusicFinished()
    {
        // Notify Core that music finished
        _gameRoot?.OnMusicFinished();

        _currentPlayingFile = null;
    }
}
