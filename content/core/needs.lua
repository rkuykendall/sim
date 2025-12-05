-- Need definitions
-- Needs decay over time and trigger debuffs when low

Needs = {
    Hunger = {
        id = 1,
        name = "Hunger",
        decayPerTick = 0.02,
        criticalThreshold = 15,
        lowThreshold = 35,
        criticalDebuff = "Starving",
        lowDebuff = "Hungry"
    },
    Energy = {
        id = 2,
        name = "Energy",
        decayPerTick = 0.01,
        criticalThreshold = 10,
        lowThreshold = 30,
        criticalDebuff = "Exhausted",
        lowDebuff = "Tired"
    },
    Fun = {
        id = 3,
        name = "Fun",
        decayPerTick = 0.008,
        criticalThreshold = 20,
        lowThreshold = 40,
        criticalDebuff = "Bored",
        lowDebuff = "Understimulated"
    },
    Social = {
        id = 4,
        name = "Social",
        decayPerTick = 0.005,
        criticalThreshold = 15,
        lowThreshold = 35,
        criticalDebuff = "Lonely",
        lowDebuff = "Isolated"
    },
    Hygiene = {
        id = 6,
        name = "Hygiene",
        decayPerTick = 0.008,
        criticalThreshold = 15,
        lowThreshold = 30,
        criticalDebuff = "Filthy",
        lowDebuff = "Dirty"
    },
}

return Needs
