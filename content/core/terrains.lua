-- Terrain definitions
-- Terrains define the properties of tile types

Terrains = {
    Flat = {
        walkable = true,
        spriteKey = "flat"
    },
    Block = {
        walkable = false,
        spriteKey = "block"
    },
    Grass = {
        walkable = true,
        spriteKey = "grass"
    },
    Dirt = {
        walkable = true,
        spriteKey = "dirt"
    },
    Concrete = {
        walkable = true,
        spriteKey = "concrete"
    },
    WoodFloor = {
        walkable = true,
        spriteKey = "wood_floor"
    },
    Stone = {
        walkable = true,
        spriteKey = "stone"
    },
    Water = {
        walkable = false,
        spriteKey = "water"
    },
    Brick = {
        walkable = false,
        spriteKey = "brick",
    },
    Path = {
        walkable = true,
        spriteKey = "path",
        isPath = true
    }
}

return Terrains
