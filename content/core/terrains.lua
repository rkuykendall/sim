-- Terrain definitions
-- Terrains define the properties of tile types

Terrains = {
    Flat = {
        walkable = true,
        spriteKey = "flat",
    },
    Grass = {
        walkable = true,
        spriteKey = "grass",
    },
    Dirt = {
        walkable = true,
        spriteKey = "dirt",
    },
    Concrete = {
        walkable = true,
        spriteKey = "concrete",
    },
    WoodFloor = {
        walkable = true,
        spriteKey = "wood_floor",
    },
    Stone = {
        walkable = true,
        spriteKey = "stone",
    },
    Path = {
        walkable = true,
        spriteKey = "path",
        isAutotiling = true,
    },
    Water = {
        walkable = false,
        spriteKey = "water",
        isAutotiling = true,
    },
    Block = {
        walkable = false,
        spriteKey = "block",
    },
    Brick = {
        walkable = false,
        spriteKey = "brick",
    },
    Wall = {
        walkable = false,
        spriteKey = "wall",
        isAutotiling = true,
    },
}

return Terrains
