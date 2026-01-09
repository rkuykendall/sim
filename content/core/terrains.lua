-- Terrain definitions
-- Terrains define the properties of tile types
-- walkabilityCost: Movement cost for pathfinding (1.0 = normal, < 1.0 = faster, > 1.0 = slower, math.huge = impassable)
-- blocksLight: true if terrain should cast shadows and block sunlight
Terrains = {
    Flat = {
        walkabilityCost = 1.0,
        spriteKey = "flat",
        paintsToBase = true,
    },
    WoodFloor = {
        walkabilityCost = 0.8,
        spriteKey = "wood_floor",
    },
    Grass = {
        walkabilityCost = 1.0,
        spriteKey = "grass",
        variantCount = 4,
    },
    Trees = {
        walkabilityCost = 5.0,
        spriteKey = "trees",
        isAutotiling = true,
    },
    Rock = {
        walkabilityCost = 5.0,
        spriteKey = "rock",
        variantCount = 12,
    },
    Plant = {
        walkabilityCost = 2.0,
        spriteKey = "plant",
        variantCount = 10,
    },
    Dirt = {
        walkabilityCost = 0.8,
        spriteKey = "dirt",
        variantCount = 4,
    },
    Stone = {
        walkabilityCost = 0.05,
        spriteKey = "stone",
        isAutotiling = true,
    },
    Path = {
        walkabilityCost = 0.05,
        spriteKey = "path",
        isAutotiling = true,
    },
    Water = {
        walkabilityCost = math.huge,
        spriteKey = "water",
        isAutotiling = true,
    },
    Block = {
        walkabilityCost = math.huge,
        blocksLight = true,
        spriteKey = "block",
        isAutotiling = true,
    },
    Wall = {
        walkabilityCost = math.huge,
        blocksLight = true,
        spriteKey = "wall",
        isAutotiling = true,
    },
}
return Terrains
