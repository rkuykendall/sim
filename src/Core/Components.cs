using System.Collections.Generic;

namespace SimGame.Core;

// Position
public sealed class PositionComponent
{
    public TileCoord Coord;
}

// Pawn identity
public sealed class PawnComponent
{
    public string Name = "";
    public int Age;
}

// Needs (hunger, energy, etc.)
public sealed class NeedsComponent
{
    public Dictionary<int, float> Needs = new(); // needId -> 0..100
}

// Mood
public sealed class MoodComponent
{
    public float Mood; // -100..100
}

// Active buffs
public sealed class BuffComponent
{
    public List<BuffInstance> ActiveBuffs = new();
}

// Action queue
public sealed class ActionComponent
{
    public ActionDef? CurrentAction = null;
    public int ActionStartTick = 0;
    public Queue<ActionDef> ActionQueue = new();
    public List<TileCoord>? CurrentPath = null;
    public int PathIndex = 0;
    public int BlockedSinceTick = -1;  // Tick when pawn first got blocked, -1 = not blocked
}

// World object (furniture, etc.)
public sealed class ObjectComponent
{
    public int ObjectDefId;
    public bool InUse = false;
    public EntityId? UsedBy = null;
}
