# Game Design Document — “Colony Painter”

## High-Level Vision

A top-down, grid-based tile painting simulation sandbox.

Players manage a small modern neighborhood filled with dynamic characters ("pawns") who have personal needs which lead to emotional states (buffs & debuffs). The game world evolves through player-built ("painted") spaces as well as random events and pawn interactions. The player can watch emergent behavior or directly intervene.

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

## 2. Characters

Each pawn has:

- Needs (hunger, energy, fun, social, hygiene)
- Mood (a combined calculation of needs, buffs, traits, memories)
- Pawns autonomously pursue tasks based on needs and AI rules.

Possible future expansion into:

- Traits (optimistic, neat, shy, ambitious)
- Jobs and careers
- Relationships (friendships, rivalries, crushes, coworkers)
- Memories and history (first day at work, fights, breakups, promotions)

---

## 4. Painting

Players control the environment through:

- placing objects
- placing walls, paths
- decorating interiors and exteriors

The painting can be done with brushes that are either a click-and-drag single cell, line, square (outline), box, paint-bucket, or other creative brush choices.

Then the player may pick from a variety of colors and textures.

Players paint a home, a business, or an entire district.

---

## 5. Events

The game should be able to generate events that shape the simulation:

- neighborhood drama
- job opportunities
- disasters
- new arrivals
- interpersonal conflicts

Each storyteller has a different pacing and tone.

---

# Roguelite elements

This does not have to be handled early-on, but it would be fun for each seed to generate a unique experience. This might mean picking from a random color pallete, how the pawns are generated, the n eeds or objects they have, the textures available, etc.
