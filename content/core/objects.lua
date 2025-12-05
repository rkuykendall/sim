-- Object definitions
-- Objects are interactable items that satisfy needs

Objects = {
    Fridge = {
        id = 1,
        name = "Fridge",
        satisfiesNeed = "Hunger",
        satisfactionAmount = 50,
        interactionDuration = 20,
        grantsBuff = "GoodMeal"
    },
    Bed = {
        id = 2,
        name = "Bed",
        satisfiesNeed = "Energy",
        satisfactionAmount = 80,
        interactionDuration = 40,
        grantsBuff = "WellRested"
    },
    TV = {
        id = 3,
        name = "TV",
        satisfiesNeed = "Fun",
        satisfactionAmount = 40,
        interactionDuration = 30,
        grantsBuff = "HadFun"
    },
    Shower = {
        id = 4,
        name = "Shower",
        satisfiesNeed = "Hygiene",
        satisfactionAmount = 60,
        interactionDuration = 20,
        grantsBuff = "FeelingFresh"
    },
}

return Objects
