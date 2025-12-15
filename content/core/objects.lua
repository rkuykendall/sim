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
        useAreas = { {0, 1} }  -- stand in front (south)
    },
    Bed = {
        name = "Bed",
        satisfiesNeed = "Energy",
        satisfactionAmount = 80,
        interactionDuration = 40,
        grantsBuff = "WellRested",
        walkable = true,  -- pawn stands on the bed to sleep
        useAreas = { {0, 0} }
    },
    TV = {
        name = "TV",
        satisfiesNeed = "Fun",
        satisfactionAmount = 40,
        interactionDuration = 30,
        grantsBuff = "HadFun",
        useAreas = { {0, 1}, {0, 2}, {-1, 1}, {1, 1}, {-1, 2}, {1, 2} }  -- viewing area in front
    },
    Shower = {
        name = "Shower",
        satisfiesNeed = "Hygiene",
        satisfactionAmount = 60,
        interactionDuration = 20,
        grantsBuff = "FeelingFresh",
        walkable = true,  -- pawn stands in the shower
        useAreas = { {0, 0} }
    },
}

return Objects
