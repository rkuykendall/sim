# SimGame

A top-down, grid-based life simulation sandbox.

## Requirements

- [Godot 4.2+](https://godotengine.org/download) with .NET support
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)

## Quick Start

```bash
# Open in Godot
godot project.godot

# Or build from command line
dotnet build
godot --headless --build-solutions --quit
godot
```

## Project Structure

```
├── src/
│   ├── Core/          # Engine-agnostic simulation (pure C#)
│   └── Godot/         # Godot rendering & input
├── scenes/            # Godot scene files (.tscn)
├── DESIGN.md          # Full game design document
└── project.godot      # Godot project config
```

## Architecture

The simulation runs independently of rendering at 20 ticks/second. Godot receives read-only `RenderSnapshot` data each frame. See `DESIGN.md` for details.
