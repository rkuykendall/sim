-- Terrain definitions
-- Terrains define the properties of tile types

Terrains = {
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
