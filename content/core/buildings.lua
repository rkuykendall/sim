-- Building definitions
-- Buildings are interactable structures that satisfy needs
-- useAreas: automatically derived from walkable property and tileSize

Buildings = {
    Home = {
        satisfiesNeed = "Energy",
        grantsBuff = "WellRested",
        walkable = true,  -- pawns stand in home to sleep
        spriteKey = "home",
        tileSize = 2
    },
    Farm = {
        satisfiesNeed = "Hunger",
        grantsBuff = "GoodMeal",
        spriteKey = "farm",
        resourceType = "food",
        maxResourceAmount = 100,
        depletionMult = 1.0,
        canBeWorkedAt = true,
        tileSize = 2,
        walkable = false
    },
    Market = {
        satisfiesNeed = "Hunger",
        grantsBuff = "GoodMeal",
        spriteKey = "market",
        resourceType = "food",
        maxResourceAmount = 100,
        depletionMult = 1.0,
        canBeWorkedAt = true,
        tileSize = 2,
        walkable = false
    },
    Well = {
        satisfiesNeed = "Hygiene",
        grantsBuff = "FeelingFresh",
        spriteKey = "well",
        resourceType = "water",
        maxResourceAmount = 999,
        depletionMult = 0.0,  -- Infinite water
        walkable = false,
        tileSize = 1
    },
    Tavern = {
        satisfiesNeed = "Social",
        grantsBuff = "Socialized",
        walkable = false,
        spriteKey = "tavern",
        tileSize = 2
    }
}

return Buildings
