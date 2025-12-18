-- Object definitions
-- Objects are interactable items that satisfy needs
-- useAreas: list of {dx, dy} relative tile offsets where pawn can stand to use object

Objects = {
    Fridge = {
        name = "Fridge",
        satisfiesNeed = "Hunger",
        satisfactionAmount = 50,
        interactionDuration = 20,
        grantsBuff = "GoodMeal",
        useAreas = { {0, 1} },  -- stand in front (south)
        spriteKey = "fridge"
    },
    Bed = {
        name = "Bed",
        satisfiesNeed = "Energy",
        satisfactionAmount = 80,
        interactionDuration = 40,
        grantsBuff = "WellRested",
        walkable = true,  -- pawn stands on the bed to sleep
        useAreas = { {0, 0} },
        spriteKey = "bed"
    },
    TV = {
        name = "TV",
        satisfiesNeed = "Fun",
        satisfactionAmount = 40,
        interactionDuration = 30,
        grantsBuff = "HadFun",
        useAreas = { {0, 1}, {0, 2}, {-1, 1}, {1, 1}, {-1, 2}, {1, 2} },  -- viewing area in front
        spriteKey = "tv"
    },
    Shower = {
        name = "Shower",
        satisfiesNeed = "Hygiene",
        satisfactionAmount = 60,
        interactionDuration = 20,
        grantsBuff = "FeelingFresh",
        walkable = true,  -- pawn stands in the shower
        useAreas = { {0, 0} },
        spriteKey = "shower"
    },

    -- Walls (blocking objects)
    Wall = {
        name = "Wall",
        walkable = false,      -- blocks movement
        interactable = false,  -- cannot be used
        useAreas = {},
        spriteKey = "wall",
        isAutoTiled = false    -- Enable when wall_grid.png is ready
    },

    -- Decorations (non-interactive, walkable)
    Plant = {
        name = "Plant",
        walkable = true,       -- can walk through
        interactable = false,  -- no interaction
        useAreas = {},
        spriteKey = "plant"
    },

    Rug = {
        name = "Rug",
        walkable = true,
        interactable = false,
        useAreas = {},
        spriteKey = "rug"
    },

    Lamp = {
        name = "Lamp",
        walkable = true,
        interactable = false,
        useAreas = {},
        spriteKey = "lamp"
    },
}

return Objects
