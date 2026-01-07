-- Need definitions
-- Needs decay over time and trigger debuffs when low
-- criticalDebuff/lowDebuff are mood offsets (negative values for debuffs)
-- spriteKey is the 16x16 icon shown in expression bubbles

Needs = {
    Hunger = {
        name = "Hunger",
        decayPerTick = 0.02,
        criticalThreshold = 15,
        lowThreshold = 35,
        criticalDebuff = -30,  -- Starving!
        lowDebuff = -10,       -- Hungry
        spriteKey = "hunger"
    },
    Energy = {
        name = "Energy",
        decayPerTick = 0.01,
        criticalThreshold = 10,
        lowThreshold = 30,
        criticalDebuff = -25,  -- Exhausted!
        lowDebuff = -8,        -- Tired
        spriteKey = "energy"
    },
    Social = {
        name = "Social",
        decayPerTick = 0.005,
        criticalThreshold = 15,
        lowThreshold = 35,
        criticalDebuff = -25,  -- Lonely!
        lowDebuff = -8,        -- Isolated
        spriteKey = "social"
    },
    Hygiene = {
        name = "Hygiene",
        decayPerTick = 0.008,
        criticalThreshold = 15,
        lowThreshold = 30,
        criticalDebuff = -20,  -- Filthy!
        lowDebuff = -8,        -- Dirty
        spriteKey = "hygine"  -- Placeholder
    },
    Purpose = {
        name = "Purpose",
        decayPerTick = 0.006,
        criticalThreshold = 20,
        lowThreshold = 40,
        criticalDebuff = -22,  -- Aimless!
        lowDebuff = -7,        -- Unfulfilled
        spriteKey = "purpose"  -- Placeholder
    },
}

return Needs
