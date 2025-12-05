-- Object definitions
-- Objects are interactable items that satisfy needs
-- useAreas: list of {dx, dy} relative tile offsets where pawn can stand to use object

Objects = {
    Fridge = {
        id = 1,
        name = "Fridge",
        satisfiesNeed = "Hunger",
        satisfactionAmount = 50,
        interactionDuration = 20,
        grantsBuff = "GoodMeal",
        useAreas = { {0, 1} }  -- stand in front (south)
    },
    Bed = {
        id = 2,
        name = "Bed",
        satisfiesNeed = "Energy",
        satisfactionAmount = 80,
        interactionDuration = 40,
        grantsBuff = "WellRested",
        useAreas = { {0, 0} }  -- on the bed itself
    },
    TV = {
        id = 3,
        name = "TV",
        satisfiesNeed = "Fun",
        satisfactionAmount = 40,
        interactionDuration = 30,
        grantsBuff = "HadFun",
        useAreas = { {0, 1}, {0, 2}, {-1, 1}, {1, 1}, {-1, 2}, {1, 2} }  -- viewing area in front
    },
    Shower = {
        id = 4,
        name = "Shower",
        satisfiesNeed = "Hygiene",
        satisfactionAmount = 60,
        interactionDuration = 20,
        grantsBuff = "FeelingFresh",
        useAreas = { {0, 0} }  -- in the shower
    },
}

return Objects
