-- Terrain definitions
-- Terrains define the properties of tile types
-- passability: "Low" (water/pits), "Ground" (walkable), "High" (walls/blocks)
-- blocksLight: true if terrain should cast shadows and block sunlight
Terrains = {
    Flat = {
        passability = "Ground",
        spriteKey = "flat",
    },
    Grass = {
        passability = "Ground",
        spriteKey = "grass",
    },
    Dirt = {
        passability = "Ground",
        spriteKey = "dirt",
    },
    Concrete = {
        passability = "Ground",
        spriteKey = "concrete",
    },
    WoodFloor = {
        passability = "Ground",
        spriteKey = "wood_floor",
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
