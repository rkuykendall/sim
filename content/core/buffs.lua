-- Buff definitions
-- Buffs are temporary mood modifiers applied to pawns

Buffs = {
    -- Need-based debuffs (auto-applied based on need levels)
    Starving = {
        name = "Starving!",
        moodOffset = -30,
        isFromNeed = true
    },
    Hungry = {
        name = "Hungry",
        moodOffset = -10,
        isFromNeed = true
    },
    Exhausted = {
        name = "Exhausted!",
        moodOffset = -25,
        isFromNeed = true
    },
    Tired = {
        name = "Tired",
        moodOffset = -8,
        isFromNeed = true
    },
    Lonely = {
        name = "Lonely!",
        moodOffset = -25,
        isFromNeed = true
    },
    Isolated = {
        name = "Isolated",
        moodOffset = -8,
        isFromNeed = true
    },
    Filthy = {
        name = "Filthy!",
        moodOffset = -20,
        isFromNeed = true
    },
    Dirty = {
        name = "Dirty",
        moodOffset = -8,
        isFromNeed = true
    },
    Aimless = {
        name = "Aimless!",
        moodOffset = -22,
        isFromNeed = true
    },
    Unfulfilled = {
        name = "Unfulfilled",
        moodOffset = -7,
        isFromNeed = true
    },

    -- Positive buffs from actions
    GoodMeal = {
        name = "Good Meal",
        moodOffset = 15,
        durationTicks = 2400  -- 2 minutes at 20 ticks/sec
    },
    WellRested = {
        name = "Well Rested",
        moodOffset = 20,
        durationTicks = 4800  -- 4 minutes
    },
    FeelingFresh = {
        name = "Feeling Fresh",
        moodOffset = 8,
        durationTicks = 2000
    },
    Productive = {
        name = "Productive",
        moodOffset = 12,
        durationTicks = 3000  -- 2.5 minutes
    },
    Socialized = {
        name = "Socialized",
        moodOffset = 12,
        durationTicks = 3000  -- 2.5 minutes
    },
}

return Buffs
