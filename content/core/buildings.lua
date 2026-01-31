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
        spriteVariants = 2,     -- 2 visual variants (rows)
        spritePhases = 5,       -- 5 development phases (columns) based on pawn wealth
        capacityPerPhase = {1, 2, 4, 8, 16}, -- Housing capacity scales with pawn wealth
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
        baseCost = 4,           -- Low cost, entry-level job (0 buy-in)
        baseProduction = 2.5,   -- Payout = 10g
        workType = "direct",    -- Work creates food here
        canSellToConsumers = false, -- Pawns buy food at Market, not Farm
        capacity = 3,           -- Max concurrent workers/users
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
        baseCost = 8,           -- Medium cost, requires capital
        baseProduction = 2.5,   -- Payout = 20g
        workType = "haulFromBuilding", -- Work = haul food from Farm
        haulSourceResourceType = "food",
        canSellToConsumers = true, -- Pawns buy food here
        capacity = 20,          -- Large capacity for public building
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
        capacity = 6,           -- Medium capacity public utility
    },
    Tavern = {
        satisfiesNeed = "Social",
        grantsBuff = 12,        -- Socialized
        buffDuration = 3000,    -- 2.5 minutes
        spriteKey = "tavern",
        resourceType = "drinks",
        maxResourceAmount = 100,
        depletionMult = 1.0,    -- Drinks deplete when served
        canBeWorkedAt = true,
        tileSize = 2,
        baseCost = 6,           -- Medium-low cost social building
        baseProduction = 2.0,   -- Payout = 12g
        workType = "direct",    -- Bartender makes drinks on-site
        canSellToConsumers = true,
        capacity = 20,          -- Large capacity social building
    },
    LumberMill = {
        satisfiesNeed = "Purpose",
        grantsBuff = 10,        -- Productive
        buffDuration = 2400,    -- 2 minutes
        spriteKey = "lumber_mill",     -- TODO: add lumber mill sprite
        resourceType = "lumber",
        maxResourceAmount = 100,
        depletionMult = 0.0,    -- Lumber doesn't deplete from mill use (it's sold elsewhere)
        canBeWorkedAt = true,
        tileSize = 2,
        baseCost = 7,           -- Medium cost
        baseProduction = 3.0,   -- Payout = 21g
        workType = "haulFromTerrain", -- Work = chop trees, bring lumber
        haulSourceTerrainKey = "Trees",
        canSellToConsumers = false, -- Just a production building
        capacity = 3,           -- Small work building capacity
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
