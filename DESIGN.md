# Game Design Document — “Untitled Life & City Simulation Sandbox”

## High-Level Vision

A top-down, grid-based life simulation sandbox.  
Players manage a small modern neighborhood filled with dynamic characters ("pawns") who have:

- rich social interactions
- jobs and careers
- emotional states (buffs & debuffs)
- memories and histories
- personal needs and motivations

The game world evolves through stories, events, and player-built spaces.  
The player can watch emergent behavior or directly intervene.

The systems are entirely data-driven and moddable using Lua, with a clear engine-agnostic simulation core.

---

# Core Pillars

## 1. Simulation-First

All gameplay logic runs in a deterministic C# simulation core that is:

- grid-based
- tick-based
- fully separated from rendering
- deterministic and debuggable
- moddable through Lua content definitions

The Godot client is just a viewer and input layer.

---

## 2. Emergent Characters

Each pawn has:

- Needs (hunger, energy, fun, social, comfort, hygiene)
- Mood (a combined calculation of needs, buffs, traits, memories)
- Traits (optimistic, neat, shy, ambitious)
- Jobs and careers
- Relationships (friendships, rivalries, crushes, coworkers)
- Memories and history (first day at work, fights, breakups, promotions)

Pawns autonomously pursue tasks based on needs and AI rules.

---

## 3. Watchable, Relaxing World

The game should be enjoyable to sit and watch, with:

- a clean, satisfying grid
- fluid pawn movement
- meaningful decorative objects
- visually readable activity loops
- player-secured layouts and zoning

The simulation continues even without player intervention.

---

## 4. Player Expression

Players control the environment through:

- placing buildings
- decorating interiors
- assigning zones
- purchasing items
- managing pawn roles and relationships

Players build a home, a business, or an entire district.

---

## 5. Storytellers & Events

Interchangeable storyteller engines generate events that shape the simulation:

- neighborhood drama
- job opportunities
- disasters
- new arrivals
- bills or rent increases
- interpersonal conflicts

Each storyteller has a different pacing and tone.

---

# Target Feel

The game should feel:

- lively
- cozy
- expressive
- deep but readable
- moddable
- simulation-forward
- character-driven

---

# Core Systems Overview

## 1. World & Grid

- 2D orthogonal tile grid
- Chunked world (32×32 tiles per chunk)
- Tiles store environment info (walkable, indoors, terrain type)
- Buildings occupy multiple tiles
- Supports zoning, pathfinding, and future expansions

---

## 2. Entities & Components

A simple ECS-like architecture with components such as:

- PositionComponent
- PawnComponent
- NeedsComponent
- MoodComponent
- BuffComponent
- RelationshipComponent
- JobComponent
- BuildingComponent

This allows scalable and moddable systems.

---

## 3. Needs System

Needs decay over time:

- hunger
- energy
- fun
- social
- comfort
- hygiene

Low needs cause debuffs; satisfied needs may create buffs.

---

## 4. Mood System

Mood is a sum of:

- needs contribution
- active buffs
- trait modifiers

Mood ranges from -100 to +100 and influences performance, behavior, and event likelihood.

---

## 5. Buffs & Debuffs

Buffs are temporary effects with mood impact. Examples:

- “Had Delicious Meal” +10
- “Bad Sleep” -20
- “Awkward Conversation” -15
- “Promotion” +25

Buffs come from social interactions, events, jobs, or items.

---

## 6. Relationships & Social AI

Each pawn tracks:

- opinion score (-100 to +100)
- familiarity level
- relationship categories (coworker, roommate, ex-partner)
- history logs (fights, important moments, shared events)

Social AI includes chatting, arguing, bonding, flirting, gossiping, and group behaviors.

---

## 7. Career & Economy System

Pawns follow job paths that include:

- skills
- promotion rules
- schedules
- wages and income
- performance affected by mood and traits

Businesses can be player-managed or NPC-owned.

---

## 8. Buildings & Decor

Buildings provide:

- needs satisfaction (beds, fridges, TVs)
- workstations (cash registers, kitchens)
- income potential
- skill improvement
- social spaces
- aesthetic bonuses

All building definitions are Lua-driven.

---

## 9. Storytellers & Events

Storytellers examine world state and generate incidents:

- visitors
- interpersonal events
- economic changes
- emergencies
- weather or environmental changes

Storytellers define pacing and conflict.

---

## 10. Modding with Lua

Modders define:

- buildings
- items
- buffs
- needs
- traits
- storytellers
- incidents
- jobs
- recipes
- décor
- UI text

Example of a Lua buff:

```lua
define_buff{
  id = "ate_good_meal",
  mood_offset = 10,
  duration_ticks = 6000
}
```

The simulation exposes safe APIs for reading and modifying state.

---

# Technical Architecture

## Simulation Core (C#)

Handles:

- world and grid
- ECS-style entities
- systems (needs, mood, buffs, AI, jobs, relationships)
- storytellers and incidents
- Lua execution and mod loading
- deterministic logic and fixed tick-rate updates

Runs at a fixed rate (20 ticks per second by default).  
The core can be swapped underneath without affecting the Godot client.

---

## Godot Client (C#)

Responsible for:

- rendering world tiles and buildings
- animating pawns
- UI and interaction
- input mapping to high-level commands
- camera, zoom, overlays (needs, jobs, etc.)

The client is presentation-only and receives data from the simulation via RenderSnapshots.

---

## Render Snapshot Pattern

1. The simulation advances one or more ticks internally.
2. On each Godot frame, the client requests a RenderSnapshot that contains:
   - pawn positions and mood
   - building positions and types
   - tile data
   - UI-relevant state
3. Godot displays the snapshot without modifying simulation data.
4. Input events (clicks, building placement, selecting a pawn) are sent back as high-level commands.

This maintains a strict separation between logic and presentation.

---

# Current Development Progress

Completed so far:

- Simulation core scaffolding
- Needs, buffs, and mood systems
- Pawn spawning
- Tile/world grid
- RenderSnapshot builder
- Godot adapter (GameRoot)
- Pawn rendering and mood coloring
- Basic ECS-style entity setup

Next upcoming tasks:

- implement pawn movement and pathfinding
- add basic jobs like Eat, Sleep, Socialize
- add buildings and furniture as Lua-defined items
- add storyteller architecture
- add relationship and memory tracking
- implement UI panels (inspector, needs, buffs, etc.)
- build a simple save/load system

---

# MVP Gameplay Loop

1. Spawn a pawn in a simple house or room.
2. Needs decay over time.
3. Pawn autonomously tries to satisfy needs (eat, sleep, socialize).
4. Player can place objects (bed, fridge, table).
5. Buffs and debuffs modify the pawn's mood.
6. Mood affects behavior, efficiency, and social interactions.
7. A simple storyteller generates events (visitor, mood shift, minor hazard).
8. Over time, the world grows with more pawns, jobs, and buildings.

---

# High-Level Roadmap (Optional)

**Phase 1: Core Simulation MVP**

- Pawn needs → mood
- Basic interaction objects
- Pathfinding
- Simple storyteller

**Phase 2: Social & Career Expansion**

- Relationships
- Jobs and schedules
- Skills and career progression

**Phase 3: Player Agency & Construction**

- Building placement
- Decor and room scoring
- Zones

**Phase 4: Modding Framework**

- Lua mod loader
- Content definitions (needs, buffs, buildings, traits)

**Phase 5: Polishing & Presentation**

- Animations
- UI panels
- Effects and sound

---
