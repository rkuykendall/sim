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
    /// Path to the music file to play, or null for no music.
    /// Example: "res://music/classics/gymnopedie_no_1.ogg"
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
/// Daytime theme with relaxing ambient music.
/// Plays during daytime hours, randomly selecting from available tracks.
/// </summary>
public sealed class DayTheme : Theme
{
    private static readonly string[] DayTracks = new[]
    {
        "res://music/tracks/cuddle_clouds.ogg",
        "res://music/tracks/drifting_memories.ogg",
        "res://music/tracks/evening_harmony.ogg",
        "res://music/tracks/floating_dream.ogg",
        "res://music/tracks/forgotten_biomes.ogg",
        "res://music/tracks/gentle_breeze.ogg",
        "res://music/tracks/golden_gleam.ogg",
        "res://music/tracks/polar_lights.ogg",
        "res://music/tracks/strange_worlds.ogg",
        "res://music/tracks/sunlight_through_leaves.ogg",
        "res://music/tracks/wanderers_tale.ogg",
        "res://music/tracks/whispering_woods.ogg",
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
        // Randomly select a day track
        _selectedMusicFile = DayTracks[ctx.Random.Next(DayTracks.Length)];
    }

    public override void OnTick(SimContext ctx) { }

    /// <summary>
    /// Day theme never completes on its own - only when music finishes.
    /// </summary>
    public override bool IsComplete(SimContext ctx, int themeStartTick) => false;

    public override void OnEnd(SimContext ctx) { }
}

/// <summary>
/// Nighttime theme with calming Satie Gymnopédie.
/// Plays during nighttime hours.
/// Sets all pawn Energy to 0 on first transition to night, encouraging sleep.
/// </summary>
public sealed class NightTheme : Theme
{
    private int _lastDayRan = -1;

    public override string Name => "Night";
    public override string? MusicFile => "res://music/classics/gymnopedie_no_1.ogg";

    /// <summary>
    /// Night theme has priority 10 during nighttime (higher than day theme).
    /// </summary>
    public override int GetPriority(SimContext ctx) => ctx.Time.IsNight ? 10 : 0;

    public override void OnStart(SimContext ctx)
    {
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
