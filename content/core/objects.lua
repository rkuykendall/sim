-- Object definitions (Medieval Fantasy Buildings)
-- Buildings are interactable structures that satisfy needs
-- useAreas: automatically derived from walkable property and tileSize

Objects = {
    Home = {
        satisfiesNeed = "Energy",
        satisfactionAmount = 80,
        interactionDuration = 4000,
        grantsBuff = "WellRested",
        walkable = true,  -- pawns stand in home to sleep
        spriteKey = "home",
        tileSize = 2
    },
    Farm = {
        satisfiesNeed = "Hunger",
        satisfactionAmount = 60,
        interactionDuration = 3000,
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
        satisfactionAmount = 50,
        interactionDuration = 2000,
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
        satisfactionAmount = 60,
        interactionDuration = 2000,
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
        satisfactionAmount = 50,
        interactionDuration = 2500,
        grantsBuff = "Socialized",
        walkable = false,
        spriteKey = "tavern",
        tileSize = 2
    }
}

return Objects
