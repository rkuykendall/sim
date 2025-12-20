-- Terrain definitions
-- Terrains define the properties of tile types

Terrains = {
    Flat = {
        name = "Flat",
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "flat"
    },
    Block = {
        name = "Block",
        walkable = false,
        buildable = false,
        indoors = false,
        spriteKey = "block"
    },
    Grass = {
        name = "Grass",
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "grass"
    },
    Dirt = {
        name = "Dirt",
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "dirt"
    },
    Concrete = {
        name = "Concrete",
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "concrete"
    },
    WoodFloor = {
        name = "Wood Floor",
        walkable = true,
        buildable = true,
        indoors = true,
        spriteKey = "wood_floor"
    },
    Stone = {
        name = "Stone Floor",
        walkable = true,
        buildable = true,
        indoors = true,
        spriteKey = "stone"
    },
    Water = {
        name = "Water",
        walkable = false,
        buildable = false,
        indoors = false,
        spriteKey = "water"
    },
    Brick = {
        name = "Brick",
        walkable = false,
        buildable = true,
        indoors = false,
        spriteKey = "brick",
        isPath = false
    },
    Path = {
        name = "Path",
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "path",
        isPath = true  -- Enables autotiling with dual-grid edge detection
    }
}

return Terrains
