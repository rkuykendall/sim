-- Object definitions
-- Objects are interactable items that satisfy needs

Objects = {
    Fridge = {
        id = 1,
        name = "Fridge",
        satisfiesNeed = "Hunger",
        satisfactionAmount = 50,
        interactionDuration = 60,  -- ticks
        grantsBuff = "GoodMeal"
    },
    Bed = {
        id = 2,
        name = "Bed",
        satisfiesNeed = "Energy",
        satisfactionAmount = 80,
        interactionDuration = 200,
        grantsBuff = "WellRested"
    },
    TV = {
        id = 3,
        name = "TV",
        satisfiesNeed = "Fun",
        satisfactionAmount = 40,
        interactionDuration = 100,
        grantsBuff = "HadFun"
    },
    Shower = {
        id = 4,
        name = "Shower",
        satisfiesNeed = "Hygiene",
        satisfactionAmount = 60,
        interactionDuration = 80,
        grantsBuff = "FeelingFresh"
    },
}

return Objects
