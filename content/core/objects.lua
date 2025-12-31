-- Object definitions
-- Objects are interactable items that satisfy needs
-- useAreas: list of {dx, dy} relative tile offsets where pawn can stand to use object

Objects = {
    Fridge = {
        satisfiesNeed = "Hunger",
        satisfactionAmount = 50,
        interactionDuration = 200,
        grantsBuff = "GoodMeal",
        spriteKey = "fridge"
    },
    Bed = {
        satisfiesNeed = "Energy",
        satisfactionAmount = 80,
        interactionDuration = 400,
        grantsBuff = "WellRested",
        walkable = true,  -- pawn stands on the bed to sleep
        spriteKey = "bed"
    },
    TV = {
        satisfiesNeed = "Fun",
        satisfactionAmount = 40,
        interactionDuration = 300,
        grantsBuff = "HadFun",
        spriteKey = "tv"
    },
    Shower = {
        satisfiesNeed = "Hygiene",
        satisfactionAmount = 60,
        interactionDuration = 200,
        grantsBuff = "FeelingFresh",
        walkable = true,  -- pawn stands in the shower
        spriteKey = "shower"
    },
    Castle = {
        satisfiesNeed = "Fun",
        satisfactionAmount = 40,
        interactionDuration = 300,
        grantsBuff = "HadFun",
        walkable = false,
        interactable = true,
        spriteKey = "castle"
    },
}

return Objects
