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
        tileSize = 2,
        baseCost = 0,           -- Free to use (levels by pawn wealth instead)
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
        tileSize = 2,
        baseCost = 5,           -- Low cost, entry-level job (0 buy-in)
        baseProduction = 2.0,   -- Payout = 10g
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
        tileSize = 2,
        baseCost = 10,          -- Medium cost, requires capital (10g buy-in)
        baseProduction = 2.0,   -- Payout = 20g
    },
    Well = {
        satisfiesNeed = "Hygiene",
        grantsBuff = 8,         -- Feeling Fresh
        buffDuration = 2000,    -- ~1.7 minutes
        spriteKey = "well",
        resourceType = "water",
        maxResourceAmount = 999,
        depletionMult = 0.0,  -- Infinite water
        tileSize = 1,
        baseCost = 0,
        baseProduction = 0.0,
    },
    Tavern = {
        satisfiesNeed = "Social",
        grantsBuff = 12,        -- Socialized
        buffDuration = 3000,    -- 2.5 minutes
        spriteKey = "tavern",
        tileSize = 2,
        baseCost = 8,           -- Medium-low cost social building
        baseProduction = 1.5,
    },
    -- Theatre = {
    --     satisfiesNeed = "Social",
    --     grantsBuff = 25,        -- Entertained
    --     buffDuration = 4000,    -- ~3.3 minutes
    --     spriteKey = "tavern",   -- TODO: add theatre sprite
    --     resourceType = "entertainment",
    --     maxResourceAmount = 100,
    --     depletionMult = 1.0,
    --     canBeWorkedAt = true,
    --     tileSize = 2,
    --     baseCost = 20,          -- High cost, requires significant capital (30g buy-in)
    --     baseProduction = 3.0,   -- Payout = 60g
    -- },
}

return Buildings
