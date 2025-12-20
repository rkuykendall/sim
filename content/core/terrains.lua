-- Terrain definitions
-- Terrains define the properties of tile types

Terrains = {
    Flat = {
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "flat"
    },
    Block = {
        walkable = false,
        buildable = false,
        indoors = false,
        spriteKey = "block"
    },
    Grass = {
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "grass"
    },
    Dirt = {
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "dirt"
    },
    Concrete = {
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "concrete"
    },
    WoodFloor = {
        walkable = true,
        buildable = true,
        indoors = true,
        spriteKey = "wood_floor"
    },
    Stone = {
        walkable = true,
        buildable = true,
        indoors = true,
        spriteKey = "stone"
    },
    Water = {
        walkable = false,
        buildable = false,
        indoors = false,
        spriteKey = "water"
    },
    Brick = {
        walkable = false,
        buildable = true,
        indoors = false,
        spriteKey = "brick",
        isPath = false
    },
    Path = {
        walkable = true,
        buildable = true,
        indoors = false,
        spriteKey = "path",
        isPath = true  -- Enables autotiling with dual-grid edge detection
    }
}

return Terrains
