-- Building definitions
-- Buildings are interactable structures that satisfy needs
-- All buildings are non-walkable and use areas are automatically generated around them
-- grantsBuff is the mood offset (positive for buffs), buffDuration is in ticks (20 ticks/sec)

Buildings = {
    Home = {
        satisfiesNeed = "Energy",
        grantsBuff = 20,        -- Well Rested
        buffDuration = 4800,    -- 4 minutes
        spriteKey = "home",
        tileSize = 2
    },
    Farm = {
        satisfiesNeed = "Hunger",
        grantsBuff = 15,        -- Good Meal
        buffDuration = 2400,    -- 2 minutes
        spriteKey = "farm",
        resourceType = "food",
        maxResourceAmount = 100,
        depletionMult = 1.0,
        canBeWorkedAt = true,
        tileSize = 2
    },
    Market = {
        satisfiesNeed = "Hunger",
        grantsBuff = 15,        -- Good Meal
        buffDuration = 2400,    -- 2 minutes
        spriteKey = "market",
        resourceType = "food",
        maxResourceAmount = 100,
        depletionMult = 1.0,
        canBeWorkedAt = true,
        tileSize = 2
    },
    Well = {
        satisfiesNeed = "Hygiene",
        grantsBuff = 8,         -- Feeling Fresh
        buffDuration = 2000,    -- ~1.7 minutes
        spriteKey = "well",
        resourceType = "water",
        maxResourceAmount = 999,
        depletionMult = 0.0,  -- Infinite water
        tileSize = 1
    },
    Tavern = {
        satisfiesNeed = "Social",
        grantsBuff = 12,        -- Socialized
        buffDuration = 3000,    -- 2.5 minutes
        spriteKey = "tavern",
        tileSize = 2
    }
}

return Buildings
