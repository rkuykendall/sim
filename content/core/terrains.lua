-- Terrain definitions
-- Terrains define the properties of tile types

Terrains = {
    Flat = {
        walkable = true,
        buildable = true,
        spriteKey = "flat"
    },
    Block = {
        walkable = false,
        buildable = false,
        spriteKey = "block"
    },
    Grass = {
        walkable = true,
        buildable = true,
        spriteKey = "grass"
    },
    Dirt = {
        walkable = true,
        buildable = true,
        spriteKey = "dirt"
    },
    Concrete = {
        walkable = true,
        buildable = true,
        spriteKey = "concrete"
    },
    WoodFloor = {
        walkable = true,
        buildable = true,
        spriteKey = "wood_floor"
    },
    Stone = {
        walkable = true,
        buildable = true,
        spriteKey = "stone"
    },
    Water = {
        walkable = false,
        buildable = false,
        spriteKey = "water"
    },
    Brick = {
        walkable = false,
        buildable = true,
        spriteKey = "brick",
    },
    Path = {
        walkable = true,
        buildable = true,
        spriteKey = "path",
        isPath = true  -- Enables autotiling with dual-grid edge detection
    }
}

return Terrains
