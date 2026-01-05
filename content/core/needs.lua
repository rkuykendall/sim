-- Need definitions
-- Needs decay over time and trigger debuffs when low

Needs = {
    Hunger = {
        name = "Hunger",
        decayPerTick = 0.02,
        criticalThreshold = 15,
        lowThreshold = 35,
        criticalDebuff = "Starving",
        lowDebuff = "Hungry"
    },
    Energy = {
        name = "Energy",
        decayPerTick = 0.01,
        criticalThreshold = 10,
        lowThreshold = 30,
        criticalDebuff = "Exhausted",
        lowDebuff = "Tired"
    },
    Fun = {
        name = "Fun",
        decayPerTick = 0.008,
        criticalThreshold = 20,
        lowThreshold = 40,
        criticalDebuff = "Bored",
        lowDebuff = "Understimulated"
    },
    Social = {
        name = "Social",
        decayPerTick = 0.005,
        criticalThreshold = 15,
        lowThreshold = 35,
        criticalDebuff = "Lonely",
        lowDebuff = "Isolated"
    },
    Hygiene = {
        name = "Hygiene",
        decayPerTick = 0.008,
        criticalThreshold = 15,
        lowThreshold = 30,
        criticalDebuff = "Filthy",
        lowDebuff = "Dirty"
    },
    Purpose = {
        name = "Purpose",
        decayPerTick = 0.006,
        criticalThreshold = 20,
        lowThreshold = 40,
        criticalDebuff = "Aimless",
        lowDebuff = "Unfulfilled"
    },
}

return Needs
