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

## Running Tests

To run the automated test suite (unit and integration tests):

```bash
dotnet test
```

## Code Formatting (CSharpier)

This project uses [CSharpier](https://csharpier.com/) to enforce consistent C# code style.

- **Format all code:**
	```bash
	csharpier format .
	```
- **Check formatting (CI/pre-commit):**
	```bash
	csharpier check .
	```
- **Pre-commit enforcement:**
	A pre-commit git hook will block commits if code is not formatted. To fix formatting errors, run the format command above.

If you just installed CSharpier, you may need to add it to your PATH:
```bash
export PATH="$PATH:/Users/rkuykendall/.dotnet/tools"
```

This will build and execute all tests in the solution, including those in `tests/SimGame.Tests/`. You can also run tests for a specific project or file:

```bash
dotnet test tests/SimGame.Tests/SimGame.Tests.csproj
```

For more options, see the [dotnet test documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test).

## Architecture

The simulation runs independently of rendering at 20 ticks/second. Godot receives read-only `RenderSnapshot` data each frame. See `DESIGN.md` for details.

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
