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
/// The default silent theme (4'33" - named after John Cage's composition).
/// Plays no music and has no gameplay effects.
/// Always available as a fallback with priority 1.
/// </summary>
public sealed class SilentTheme : Theme
{
    public override string Name => "4'33\"";
    public override string? MusicFile => null;

    /// <summary>
    /// Silent theme always returns priority 1 - it's the default fallback
    /// when no other themes have higher priority.
    /// </summary>
    public override int GetPriority(SimContext ctx) => 1;

    public override void OnStart(SimContext ctx) { }

    public override void OnTick(SimContext ctx) { }

    /// <summary>
    /// Silent theme never completes on its own - it only ends when replaced by another theme.
    /// </summary>
    public override bool IsComplete(SimContext ctx, int themeStartTick) => false;

    public override void OnEnd(SimContext ctx) { }
}

/// <summary>
/// Sleepytime theme that triggers near sunset.
/// Sets all pawn Energy to 0, forcing them to seek beds.
/// Plays Satie's Gymnopédie No. 1 until completion.
/// Only runs once per day.
/// </summary>
public sealed class SleepytimeTheme : Theme
{
    private int _lastDayRan = -1;

    public override string Name => "Sleepytime";
    public override string? MusicFile => "res://music/gymnopedie_1.mid";

    /// <summary>
    /// Returns priority 10 if it's nighttime and we haven't run yet today.
    /// Returns 0 otherwise.
    /// </summary>
    public override int GetPriority(SimContext ctx)
    {
        // Only run once per day
        if (_lastDayRan == ctx.Time.Day)
        {
            return 0;
        }

        // Only run at night
        if (!ctx.Time.IsNight)
        {
            return 0;
        }

        return 10;
    }

    public override void OnStart(SimContext ctx)
    {
        // Track that we ran on this day
        _lastDayRan = ctx.Time.Day;

        // Set all pawn energy to 0 when theme starts
        var energyNeedId = ctx.Content.GetNeedId("Energy");
        if (!energyNeedId.HasValue)
        {
            return;
        }

        int pawnCount = 0;
        foreach (var pawnId in ctx.Entities.AllPawns())
        {
            if (ctx.Entities.Needs.TryGetValue(pawnId, out var needs))
            {
                if (needs.Needs.ContainsKey(energyNeedId.Value))
                {
                    needs.Needs[energyNeedId.Value] = 0f;
                    pawnCount++;
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
