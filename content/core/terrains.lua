-- Terrain definitions
-- Terrains define the properties of tile types
-- passability: "Low" (water/pits), "Ground" (walkable), "High" (walls/blocks)
-- blocksLight: true if terrain should cast shadows and block sunlight

Terrains = {
    Flat = {
        passability = "Ground",
        blocksLight = false,
        spriteKey = "flat",
    },
    Grass = {
        passability = "Ground",
        blocksLight = false,
        spriteKey = "grass",
    },
    Dirt = {
        passability = "Ground",
        blocksLight = false,
        spriteKey = "dirt",
    },
    Concrete = {
        passability = "Ground",
        blocksLight = false,
        spriteKey = "concrete",
    },
    WoodFloor = {
        passability = "Ground",
        blocksLight = false,
        spriteKey = "wood_floor",
    },
    Stone = {
        passability = "Ground",
        blocksLight = false,
        spriteKey = "stone",
    },
    Path = {
        passability = "Ground",
        blocksLight = false,
        spriteKey = "path",
        isAutotiling = true,
    },
    Water = {
        passability = "Low",
        blocksLight = false,
        spriteKey = "water",
        isAutotiling = true,
    },
    Block = {
        passability = "High",
        blocksLight = true,
        spriteKey = "block",
    },
    Brick = {
        passability = "High",
        blocksLight = true,
        spriteKey = "brick",
    },
    Wall = {
        passability = "High",
        blocksLight = true,
        spriteKey = "wall",
        isAutotiling = true,
    },
}

return Terrains
