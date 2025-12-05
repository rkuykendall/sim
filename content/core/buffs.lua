-- Buff definitions
-- Buffs are temporary mood modifiers applied to pawns

Buffs = {
    -- Need-based debuffs (auto-applied based on need levels)
    Starving = {
        id = 101,
        name = "Starving!",
        moodOffset = -30,
        isFromNeed = true
    },
    Hungry = {
        id = 102,
        name = "Hungry",
        moodOffset = -10,
        isFromNeed = true
    },
    Exhausted = {
        id = 103,
        name = "Exhausted!",
        moodOffset = -25,
        isFromNeed = true
    },
    Tired = {
        id = 104,
        name = "Tired",
        moodOffset = -8,
        isFromNeed = true
    },
    Bored = {
        id = 105,
        name = "Bored!",
        moodOffset = -20,
        isFromNeed = true
    },
    Understimulated = {
        id = 106,
        name = "Understimulated",
        moodOffset = -5,
        isFromNeed = true
    },
    Lonely = {
        id = 107,
        name = "Lonely!",
        moodOffset = -25,
        isFromNeed = true
    },
    Isolated = {
        id = 108,
        name = "Isolated",
        moodOffset = -8,
        isFromNeed = true
    },
    Filthy = {
        id = 111,
        name = "Filthy!",
        moodOffset = -20,
        isFromNeed = true
    },
    Dirty = {
        id = 112,
        name = "Dirty",
        moodOffset = -8,
        isFromNeed = true
    },

    -- Positive buffs from actions
    GoodMeal = {
        id = 201,
        name = "Good Meal",
        moodOffset = 15,
        durationTicks = 2400  -- 2 minutes at 20 ticks/sec
    },
    WellRested = {
        id = 202,
        name = "Well Rested",
        moodOffset = 20,
        durationTicks = 4800  -- 4 minutes
    },
    HadFun = {
        id = 203,
        name = "Had Fun",
        moodOffset = 10,
        durationTicks = 1800  -- 1.5 minutes
    },
    FeelingFresh = {
        id = 204,
        name = "Feeling Fresh",
        moodOffset = 8,
        durationTicks = 2000
    },
}

return Buffs
