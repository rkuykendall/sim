-- Building definitions
-- Buildings are interactable structures that satisfy needs
-- All buildings are non-walkable and use areas are automatically generated around them

Buildings = {
    Home = {
        satisfiesNeed = "Energy",
        grantsBuff = "WellRested",
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
        tileSize = 2
    },
    Market = {
        satisfiesNeed = "Hunger",
        grantsBuff = "GoodMeal",
        spriteKey = "market",
        resourceType = "food",
        maxResourceAmount = 100,
        depletionMult = 1.0,
        canBeWorkedAt = true,
        tileSize = 2
    },
    Well = {
        satisfiesNeed = "Hygiene",
        grantsBuff = "FeelingFresh",
        spriteKey = "well",
        resourceType = "water",
        maxResourceAmount = 999,
        depletionMult = 0.0,  -- Infinite water
        tileSize = 1
    },
    Tavern = {
        satisfiesNeed = "Social",
        grantsBuff = "Socialized",
        spriteKey = "tavern",
        tileSize = 2
    }
}

return Buildings
