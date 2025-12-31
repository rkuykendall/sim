-- Terrain definitions
-- Terrains define the properties of tile types
-- passability: "Low" (water/pits), "Ground" (walkable), "High" (walls/blocks)
-- blocksLight: true if terrain should cast shadows and block sunlight
Terrains = {
    Flat = {
        passability = "Ground",
        spriteKey = "flat",
        paintsToBase = true,
    },
    WoodFloor = {
        passability = "Ground",
        spriteKey = "wood_floor",
    },
    Grass = {
        passability = "Ground",
        spriteKey = "grass",
        variantCount = 4,
    },
    Trees = {
        passability = "High",
        spriteKey = "trees",
        isAutotiling = true,
    },
    Rock = {
        passability = "High",
        spriteKey = "rock",
        variantCount = 12,
    },
    Plant = {
        passability = "Ground",
        spriteKey = "plant",
        variantCount = 10,
    },
    Dirt = {
        passability = "Ground",
        spriteKey = "dirt",
        variantCount = 4,
    },
    Stone = {
        passability = "Ground",
        spriteKey = "stone",
        isAutotiling = true,
    },
    Path = {
        passability = "Ground",
        spriteKey = "path",
        isAutotiling = true,
    },
    Water = {
        passability = "Low",
        spriteKey = "water",
        isAutotiling = true,
    },
    Block = {
        passability = "High",
        blocksLight = true,
        spriteKey = "block",
        isAutotiling = true,
    },
    Wall = {
        passability = "High",
        blocksLight = true,
        spriteKey = "wall",
        isAutotiling = true,
    },
}
return Terrains
