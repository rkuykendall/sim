Paint Town — Content Modding
============================

Files in this folder are loaded additively on top of the built-in content.
You can add new entries or override existing ones by name.

Supported files (place inside a "core/" subfolder):

  palettes.json   — color palettes available in the build toolbar
  terrains.json   — terrain types (grass, water, stone, etc.)
  buildings.json  — placeable buildings
  needs.json      — pawn needs (hunger, social, etc.)


palettes.json format
--------------------
Each palette is a named list of exactly 8 hex colors.

{
    "MyPalette": ["#rrggbb", "#rrggbb", "#rrggbb", "#rrggbb",
                  "#rrggbb", "#rrggbb", "#rrggbb", "#rrggbb"]
}

The included "Neon" palette in core/palettes.json is an example.
Rename it, edit the colors, or add more palettes below it.
