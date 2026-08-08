# Paint Town

## Itch.io — Short description / tagline

> Paint a town. Watch it come alive.

## Itch.io — Description

> Paint Town is a pixel simulation painter. Think KidPix mixed with a Colony Sim but for 4-year-olds. Pick up a tool, paint some terrain, drop a few buildings, and tiny pixel people show up and start living their lives. Flood-fill a forest. Outline a town square. Stamp down a farm and a tavern.

# SimGame

A top-down, grid-based life simulation sandbox.

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

- [Pixel Simple Human Character 16x16 px by Brysia](https://brysiaa.itch.io/pixel-simple-human-character-16x16-px)
- [KidPix 1.0 by Craig Hickman](https://en.wikipedia.org/wiki/Kid_Pix)
- [Tileset Templates by Patrik](https://patrik-arts.itch.io/tileset-templates)
- [Puny World by Shade](https://merchant-shade.itch.io/16x16-puny-world)
- [Tinyworld by Fliegevogel](https://fliegevogel.itch.io/)
- [Webtyler by Alexander Nadeau](https://wareya.github.io/webtyler/)
- [Cozy Tunes by Pizza Doggy](https://pizzadoggy.itch.io/cozy-tunes)
- [Universal UI Soundpack by Nathan Gibson](https://nathangibson.myportfolio.com) (CC BY 4.0)

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
