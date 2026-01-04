using System;

namespace SimGame.Core;

/// <summary>
/// Abstract base class for themes that govern simulation behavior.
/// Themes can control music, pawn behavior, pathfinding, weather, and other gameplay effects.
/// They define what happens when they start, tick, and end.
/// </summary>
public abstract class Theme
{
    /// <summary>Name of the theme for debugging and display.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Path to the MIDI file to play, or null for no music.
    /// Example: "res://music/gymnopedie_1.mid"
    /// </summary>
    public abstract string? MusicFile { get; }

    /// <summary>
    /// Calculate the priority of this theme for the current simulation state.
    /// Returns 0 if the theme should not run, 1 for default/fallback priority,
    /// or higher numbers for increased priority.
    /// ThemeSystem will randomly select from themes with the highest priority.
    /// </summary>
    public abstract int GetPriority(SimContext ctx);

    /// <summary>Called once when the theme starts.</summary>
    public abstract void OnStart(SimContext ctx);

    /// <summary>Called every tick while the theme is active.</summary>
    public abstract void OnTick(SimContext ctx);

    /// <summary>
    /// Returns true when the theme should end.
    /// For music-driven themes, this is typically signaled externally via OnMusicFinished.
    /// </summary>
    public abstract bool IsComplete(SimContext ctx, int themeStartTick);

    /// <summary>Called once when the theme ends.</summary>
    public abstract void OnEnd(SimContext ctx);
}

/// <summary>
/// Daytime theme with relaxing Satie Gnossiennes.
/// Plays during daytime hours, randomly selecting from 3 Gnossienne pieces.
/// </summary>
public sealed class DayTheme : Theme
{
    private static readonly string[] GnossiennePieces = new[]
    {
        "res://music/2035_gnossienne_1.mid",
        "res://music/2130_gnossienne_2.mid",
        "res://music/2131_gnossienne_3.mid",
    };

    private string? _selectedMusicFile;

    public override string Name => "Day";
    public override string? MusicFile => _selectedMusicFile;

    /// <summary>
    /// Day theme has priority 5 during daytime (higher than default but lower than night theme).
    /// </summary>
    public override int GetPriority(SimContext ctx) => ctx.Time.IsNight ? 0 : 5;

    public override void OnStart(SimContext ctx)
    {
        // Randomly select a Gnossienne piece
        _selectedMusicFile = GnossiennePieces[ctx.Random.Next(GnossiennePieces.Length)];
    }

    public override void OnTick(SimContext ctx) { }

    /// <summary>
    /// Day theme never completes on its own - only when music finishes.
    /// </summary>
    public override bool IsComplete(SimContext ctx, int themeStartTick) => false;

    public override void OnEnd(SimContext ctx) { }
}

/// <summary>
/// Nighttime theme with calming Satie Gymnopédies.
/// Plays during nighttime hours, randomly selecting from 3 Gymnopédie pieces.
/// Sets all pawn Energy to 0 on first transition to night, encouraging sleep.
/// </summary>
public sealed class NightTheme : Theme
{
    private static readonly string[] GymnopeidiePieces = new[]
    {
        "res://music/37_gymnopedie_1.mid",
        "res://music/38_gymnopedie_2.mid",
        "res://music/39_gymnopedie_3.mid",
    };

    private int _lastDayRan = -1;
    private string? _selectedMusicFile;

    public override string Name => "Night";
    public override string? MusicFile => _selectedMusicFile;

    /// <summary>
    /// Night theme has priority 10 during nighttime (higher than day theme).
    /// </summary>
    public override int GetPriority(SimContext ctx) => ctx.Time.IsNight ? 10 : 0;

    public override void OnStart(SimContext ctx)
    {
        // Randomly select a Gymnopédie piece
        _selectedMusicFile = GymnopeidiePieces[ctx.Random.Next(GymnopeidiePieces.Length)];

        // Set all pawn energy to 0 when first starting night (once per day)
        if (_lastDayRan != ctx.Time.Day)
        {
            _lastDayRan = ctx.Time.Day;

            var energyNeedId = ctx.Content.GetNeedId("Energy");
            if (!energyNeedId.HasValue)
            {
                return;
            }

            foreach (var pawnId in ctx.Entities.AllPawns())
            {
                if (ctx.Entities.Needs.TryGetValue(pawnId, out var needs))
                {
                    if (needs.Needs.ContainsKey(energyNeedId.Value))
                    {
                        needs.Needs[energyNeedId.Value] = 0f;
                    }
                }
            }
        }
    }

    public override void OnTick(SimContext ctx) { }

    /// <summary>
    /// Theme completes when music finishes (signaled externally via ThemeSystem.OnMusicFinished).
    /// </summary>
    public override bool IsComplete(SimContext ctx, int themeStartTick) => false;

    public override void OnEnd(SimContext ctx) { }
}
