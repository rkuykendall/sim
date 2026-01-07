-- Need definitions
-- Needs decay over time and trigger debuffs when low
-- criticalDebuff/lowDebuff are mood offsets (negative values for debuffs)

Needs = {
    Hunger = {
        name = "Hunger",
        decayPerTick = 0.02,
        criticalThreshold = 15,
        lowThreshold = 35,
        criticalDebuff = -30,  -- Starving!
        lowDebuff = -10        -- Hungry
    },
    Energy = {
        name = "Energy",
        decayPerTick = 0.01,
        criticalThreshold = 10,
        lowThreshold = 30,
        criticalDebuff = -25,  -- Exhausted!
        lowDebuff = -8         -- Tired
    },
    Social = {
        name = "Social",
        decayPerTick = 0.005,
        criticalThreshold = 15,
        lowThreshold = 35,
        criticalDebuff = -25,  -- Lonely!
        lowDebuff = -8         -- Isolated
    },
    Hygiene = {
        name = "Hygiene",
        decayPerTick = 0.008,
        criticalThreshold = 15,
        lowThreshold = 30,
        criticalDebuff = -20,  -- Filthy!
        lowDebuff = -8         -- Dirty
    },
    Purpose = {
        name = "Purpose",
        decayPerTick = 0.006,
        criticalThreshold = 20,
        lowThreshold = 40,
        criticalDebuff = -22,  -- Aimless!
        lowDebuff = -7         -- Unfulfilled
    },
}

return Needs
