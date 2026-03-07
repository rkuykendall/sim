# GDScript Port Plan

Tracking the research and decisions needed to port Paint Town from C# (.NET) to pure GDScript.

---

## Blocker Levels

- **HARD** — Needs a real solution before port can proceed. Requires research or a decision.
- **MEDIUM** — Has a known path but non-trivial effort.
- **EASY** — Straightforward translation, mostly mechanical work.

---

## Open Questions / Blockers

### 1. Lua → GDScript content files
**Level: EASY** ✅ Resolved

Currently uses MoonSharp (.NET Lua interpreter) to load `buildings.lua`, `needs.lua`, `terrains.lua`, `palettes.lua` at runtime. GDScript has no Lua runtime.

**Decision: JSON files + `user://mods/` folder scan for additive content.**

- Base game ships `res://content/core/palettes.json`, `buildings.json`, etc.
- At startup, loader scans `user://mods/*/` for additional JSON files and merges them in.
- Full overrides (replace a building, reskin a palette) ship as `.pck` files via `ProjectSettings.load_resource_pack()`, which replaces the `res://` file entirely.
- Two mods can both add palettes without conflicting — each drops its own file, both get merged.

**Why not GDScript dict files?** Can execute arbitrary code — acceptable for your own content, risky for third-party mods.
**Why not Godot Resources (`.tres`)?** Modders would need the Godot editor. JSON needs only a text editor, same as Lua.

**Loader sketch (GDScript):**
```gdscript
func load_all() -> ContentRegistry:
    var registry = ContentRegistry.new()
    _load_json(registry, "res://content/core/palettes.json", _parse_palette)
    _load_json(registry, "res://content/core/needs.json", _parse_need)
    _load_json(registry, "res://content/core/terrains.json", _parse_terrain)
    _load_json(registry, "res://content/core/buildings.json", _parse_building)
    _load_mods(registry)
    return registry

func _load_mods(registry: ContentRegistry) -> void:
    var mod_dir = DirAccess.open("user://mods")
    if mod_dir == null:
        return
    for mod_name in mod_dir.get_directories():
        var base = "user://mods/" + mod_name + "/"
        for file in ["palettes.json", "needs.json", "terrains.json", "buildings.json"]:
            var path = base + file
            if FileAccess.file_exists(path):
                _load_json(registry, path, _get_parser(file))

func _load_json(registry: ContentRegistry, path: String, parser: Callable) -> void:
    var text = FileAccess.get_file_as_string(path)
    var data = JSON.parse_string(text)
    for key in data:
        parser.call(registry, key, data[key])
```

---

### 2. Generic `ContentStore<T>`
**Level: MEDIUM**

`ContentStore<NeedDef>`, `ContentStore<BuildingDef>`, `ContentStore<TerrainDef>` use C# generics for a typed id↔name registry.

**Proposed solution:** One untyped `ContentStore` class with `Dictionary` internals, typed with GDScript's `Dictionary[String, int]` / `Dictionary[int, Variant]`. Separate registries per type (needs_store, buildings_store, etc.). No generics needed since there are only 4 content types.

**Research needed:**
- [ ] Confirm `Dictionary[String, CustomClass]` works as expected in GDScript static typing
- [ ] Check if Godot Resources (`.tres`) would replace the registry entirely

---

### 3. `EntityId` and `TileCoord` as value types / dictionary keys
**Level: EASY** ✅ Resolved

Both are `readonly struct` with custom equality and hash, used as dictionary keys everywhere. GDScript has no structs — all objects are reference types.

**Decision:**
- `EntityId` → raw `int`. It's just a newtype wrapper around an int. No class needed.
- `TileCoord` → `Vector2i`. Built-in Godot type, equality is by value, works as a `Dictionary` key. In Godot 4.4+ typed dicts (`Dictionary[Vector2i, Tile]`) are supported.

**Notes:**
- Early Godot 4 had a `Vector2i` hash collision bug (Issue #51023) but it is fixed and closed.
- For hot paths (e.g. pathfinding), can pack to a single `int64` key: `(x << 32) | y` to reduce hashing overhead.
- No research remaining — this is a clean solved problem.

---

### 4. Interfaces (`ISystem`, `IContentDef`)
**Level: EASY**

C# interfaces used for `ISystem` (6 implementations) and `IContentDef` (3 implementations).

**Proposed solution:** Abstract base class with a stub method, or just duck typing. GDScript's `@warning_ignore` can suppress missing-method warnings. Given there are only ~9 total implementations, duck typing is fine.

---

### 5. LINQ (52 usages)
**Level: EASY**

Scattered `.Any()`, `.OrderByDescending()`, `.RemoveAll()`, `.ToList()`, `.IndexOf()` etc.

**Proposed solution:** GDScript `Array` has `.any()`, `.filter()`, `.map()`, `.sort_custom()` as of Godot 4. A small `ArrayUtils` helper can cover the rest. Mechanical translation.

**Research needed:**
- [ ] Confirm GDScript 4 `Array.any(callable)` syntax and availability
- [ ] Check `Dictionary.values()` returns an `Array` that supports these methods

---

### 6. Tuples
**Level: EASY**

~6 tuple definitions: `(int BuildingDefId, int X, int Y)`, `(int dx, int dy)`, etc.

**Proposed solution:** Small inner classes or just `Array` with documented indices. Not worth full class definitions for 2-3 field structs used in one place.

---

### 7. XUnit test suite (19 files, ~200 tests)
**Level: MEDIUM** ✅ Resolved

Full unit/integration test coverage of Core simulation logic. XUnit doesn't run in GDScript.

**Decision:** Full copy of `src/`, `tests/`, and `content/` saved to `REFERENCE/SimGame-CSharp-*/` (gitignored). Use as a read-only reference during the port — compare behaviour, port test cases to GDUnit4 incrementally alongside each system being ported.

---

### 8. Sealed classes
**Level: EASY**

50+ `sealed class` definitions. In GDScript there's no equivalent keyword — classes are open by default.

**Proposed solution:** Nothing to do. Document intent in comments if needed.

---

### 9. JSON save/load
**Level: EASY**

Uses `System.Text.Json`. Godot has a built-in `JSON` class and `FileAccess`.

**Proposed solution:** Direct swap. Save format (the JSON schema) stays identical for save compatibility.

---

### 10. `@export` / `@signal` in Godot layer
**Level: EASY**

C# `[Export]` and `[Signal]` attributes map directly to GDScript `@export` and `signal`.

---

### 11. `CSharpier` formatter / pre-commit hook
**Level: EASY**

Drop CSharpier, use `gdformat` or `gdtoolkit` instead. Update pre-commit hook.

**Research needed:**
- [ ] Confirm `gdtoolkit` / `gdformat` installation and Godot 4 compatibility

---

### 12. Dotnet build step in `build.sh`
**Level: EASY**

`dotnet build -c Release` and `.csproj` / `.sln` files go away. Build script simplifies significantly.

---

## Phase Plan (draft)

| Phase | Scope | Blockers to resolve first |
|---|---|---|
| 1 | Convert Lua → JSON, write GDScript loader | — |
| 2 | Core data types: EntityId, TileCoord, Components | — |
| 3 | EntityManager + ContentStore | #2, #3 |
| 4 | World + Pathfinder | — |
| 5 | Systems (NeedsSystem → AISystem) | #4, #5 |
| 6 | Save/load | #9 |
| 7 | Godot rendering layer | — |
| 8 | Tests | #7 |
| 9 | Build pipeline | #11, #12 |

---

## Decisions Made

- **`TileCoord` → `Vector2i`** — works as a `Dictionary` key in Godot 4 with value equality. `Dictionary[Vector2i, Tile]` supported in 4.4+.
- **`EntityId` → raw `int`** — it's a thin wrapper with no behaviour; just use `int` everywhere.
- **Content format → JSON** — `res://content/core/*.json` for base game, `user://mods/*/` folder scan for additive mods, `.pck` override for full replacements. Text-editor friendly, data-only (no code execution risk), no Godot editor required for modders.

---

## Decisions Still Open

*(none)*
