# Paint Town

## Itch.io — Short description / tagline

> Paint a town. Watch it come alive.

## Itch.io — Description

> Paint Town is a pixel simulation painter. Think KidPix mixed with a Colony Sim but for 4-year-olds. Pick up a tool, paint some terrain, drop a few buildings, and tiny pixel people show up and start living their lives. Flood-fill a forest. Outline a town square. Stamp down a farm and a tavern.

## Requirements

- [Godot 4.5+](https://godotengine.org/download) — pure GDScript, no .NET/C# toolchain needed

## Quick Start

```bash
# Open in Godot
godot project.godot

# Or run directly from the command line
godot
```

## Project Structure

```
├── src/
│   ├── Core/          # Engine-agnostic simulation (pure GDScript, no Godot nodes)
│   └── Godot/         # Godot rendering & input
├── scenes/            # Godot scene files (.tscn)
├── tests/             # Headless GDScript test suite
├── DESIGN.md          # Full game design document
└── project.godot      # Godot project config
```

## Running Tests

The test suite (`tests/`) is plain GDScript, run headless via Godot's `--script` flag — no separate test runner or build step required:

```bash
godot --headless --script tests/run_tests.gd
```

This runs every suite registered in `tests/run_tests.gd` and prints a `PASS`/`FAIL` line per test, ending with a pass/fail summary. The process exits non-zero if any test fails, so it's CI-friendly.

If `godot` isn't on your `PATH`, use the full binary path instead, e.g. on macOS:

```bash
/Applications/Godot.app/Contents/MacOS/Godot --headless --script tests/run_tests.gd
```

## Dev Tools

`tools/inspect_save.gd` prints a health report for a save file — need distribution (min/max/avg/critical/low counts against each need's real thresholds), mood distribution, and building resource levels. Useful when tuning economy balance:

```bash
godot --headless --path . --script tools/inspect_save.gd -- "Save 1"
godot --headless --path . --script tools/inspect_save.gd -- 1       # numeric shorthand
godot --headless --path . --script tools/inspect_save.gd            # defaults to the most recent save
```

`tools/economy_report.gd` runs a save forward in memory (never writes it back) and prints hunger/mood/building-stock trends — used to A/B test balance constants like `AISystem.HAUL_AMOUNT` and `ActionSystem.CONSUMER_DEPLETION_AMOUNT` before committing to a value:

```bash
godot --headless --path . --script tools/economy_report.gd -- "Save 1" 40   # slot, days (both optional; default: most recent save, 40 days)
```

`tools/advance_save.gd` advances a save by N in-game days and **writes the result back to the same slot** — used to fast-forward a save to its economy equilibrium (e.g. after a balance tweak) so play resumes from a settled state:

```bash
godot --headless --path . --script tools/advance_save.gd -- "Save 1" 100   # slot, days (both optional; default: most recent save, 10 days)
```

## Building for Release

To build distributable executables for Windows, macOS, and Linux:

```bash
./build.sh
```

**Prerequisites:**

- Godot export templates must be installed (Editor → Manage Export Templates → Download)

**Output locations:**

- `builds/windows/SimGame.exe`
- `builds/macos/SimGame.app`
- `builds/linux/SimGame.x86_64`

The build script automatically copies the `content/` folder to each platform's build directory.

## Running the Web Build

Browsers require the `Cross-Origin-Opener-Policy` / `Cross-Origin-Embedder-Policy` headers to load Godot's web export (needed for `SharedArrayBuffer`), so you can't just open `builds/paint-town-web/index.html` directly from disk. Use the included local server instead:

```bash
python3 serve_web.py
```

Then open the printed URL (`http://localhost:8080` by default) in a browser. Pass a different port as an argument if 8080 is taken:

```bash
python3 serve_web.py 3000
```

## Architecture

The simulation runs independently of rendering at 20 ticks/second. Godot receives read-only `RenderSnapshot` data each frame. See `DESIGN.md` for details.

## Credits

- Engine: [Godot](https://godotengine.org)
- Characters: [Pixel Plains by SnowHex](https://snowhex.itch.io/pixel-plains)
- Music: [Cozy Tunes by Pizza Doggy](https://pizzadoggy.itch.io/cozy-tunes)
- Sound effects: [Universal UI Soundpack by Nathan Gibson](https://nathangibson.myportfolio.com) (CC BY 4.0)
- Inspiration: [RimWorld by Tynan Sylvester](https://rimworldgame.com)
- Inspiration: [KidPix by Craig Hickman](https://en.wikipedia.org/wiki/Kid_Pix)
- Pixels: [Tileset Templates by Patrik](https://patrik-arts.itch.io/tileset-templates)
- Pixels: [Puny World by Shade](https://merchant-shade.itch.io/16x16-puny-world)
- Pixels: [Tinyworld by Fliegevogel](https://fliegevogel.itch.io/)
- Thanks to: [Webtyler by Alexander Nadeau](https://wareya.github.io/webtyler/)
- Characters (early version): [Pixel Simple Human Character 16x16 px by Brysia](https://brysiaa.itch.io/pixel-simple-human-character-16x16-px)
- Font: [BoldPixels by YukiPixels](https://yukipixels.itch.io/boldpixels) (CC BY-SA 4.0)
- Building a bad Rimworld clone for kids then removing the mechanics one-by-one until you get KidPix with Sims: Robert Kuykendall

## Art guide

```
#FFF <- highlights
#EEE
#DDD <- most pixels
#CCC
#BBB <- subtle shading
#AAA
#999 <- less subtle
#888
#777 <- grass
#666
#555 <- path shadow
#444
#333 <- Brick outline
#222
#111
#000
```
