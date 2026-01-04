-- Object definitions
-- Objects are interactable items that satisfy needs
-- useAreas: list of {dx, dy} relative tile offsets where pawn can stand to use object

Objects = {
    Fridge = {
        satisfiesNeed = "Hunger",
        satisfactionAmount = 50,
        interactionDuration = 2000,
        grantsBuff = "GoodMeal",
        spriteKey = "fridge"
    },
    Oven = {
        satisfiesNeed = "Hunger",
        satisfactionAmount = 70,
        interactionDuration = 3000,
        grantsBuff = "GoodMeal",
        spriteKey = "oven"
    },
    Stove = {
        satisfiesNeed = "Hunger",
        satisfactionAmount = 60,
        interactionDuration = 2500,
        grantsBuff = "GoodMeal",
        spriteKey = "stove"
    },
    Bed = {
        satisfiesNeed = "Energy",
        satisfactionAmount = 80,
        interactionDuration = 4000,
        grantsBuff = "WellRested",
        walkable = true,  -- pawn stands on the bed to sleep
        spriteKey = "bed"
    },
    Sink = {
        satisfiesNeed = "Hygiene",
        satisfactionAmount = 60,
        interactionDuration = 2000,
        grantsBuff = "FeelingFresh",
        walkable = true,  -- pawn stands in the sink
        spriteKey = "sink"
    },
    Castle = {
        satisfiesNeed = "Fun",
        satisfactionAmount = 40,
        interactionDuration = 3000,
        grantsBuff = "HadFun",
        walkable = false,
        interactable = true,
        spriteKey = "castle"
    },
}

return Objects
