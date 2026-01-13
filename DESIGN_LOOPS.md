# Game Loop Design

## Primary Loop: Drawing (KidPix-inspired) - 80%

The core experience: a creative, tactile drawing tool.

### Done
- Basic brush system
- Tile painting
- Building placement
- Visual feedback (pawns using buildings, paths forming)

### TODO
- [ ] **Spray-can brush** - Paint tiles with fuzzy X/Y offset around cursor for natural terrain (grass, rocks). Consider:
  - Falloff weighting (center more likely than edges)
  - Should it respect existing buildings or paint over?
- [ ] **Audio feedback** - Source from `REFERENCE/kidpix/sndmp3`
  - Brush stroke sounds
  - Building placed sounds
  - Ambient pawn activity sounds
- [ ] 1-2 additional brushes (TBD)

---

## Secondary Loop: City Builder - 60%

The progression system that makes players want to keep building.

### Done
- Buildings with buffs/needs
- Pawns with states and pathfinding
- Resource flow (pawns seeking what they need)

### Economic Model (Inspired by Caesar + Incremental Games + VAT)

**Core concept:** Gold circulates through the economy. Pawns pay to use buildings, buildings pay workers.

#### Minimal Data Model (2 integers per building type)

```
BuildingDef {
  baseCost: int,        // Cost to use this building
  baseProduction: float // Payout multiplier (payout = cost × baseProduction)
}
```

**Derived values (given building level):**
- `cost = baseCost × 1.15^level`
- `payout = cost × baseProduction`
- `workBuyIn = payout <= 10 ? 0 : payout / 2`

#### Circular Economy

```
USE BUILDING:  Pawn --[cost]--> Building   (gold flows into building)
WORK:          Building --[payout]--> Pawn (gold flows from building stores)
```

**Gold Source:** Buildings with `baseCost = 0` are gold sources (e.g., Gold Mine).
Work payout is created from nothing, not from stores.

#### Work Buy-in System

Low-paying jobs are accessible to anyone. Higher-paying jobs require capital.

| Payout | Work Buy-in | Who can work |
|--------|-------------|--------------|
| ≤10 | 0 | Anyone (bootstrap) |
| 15 | 7 | Need 7g capital |
| 20 | 10 | Need 10g capital |
| 50 | 25 | Need 25g capital |

This creates natural economic stratification:
1. Broke pawn → works gold mine or low-tier jobs
2. Pawn with capital → can afford better jobs → earns more
3. Wealthy pawns work high-tier jobs, use nicer buildings

#### Building Level System (TODO)

Buildings track gold flowing through them. Level upgrades/downgrades naturally based on economic activity (exact mechanics TBD after observing simulation).

Visual indicator shows building level on the map.

### TODO
- [x] Add gold storage to pawns and buildings
- [x] Give starter pawns 100 gold
- [x] Add baseCost and baseProduction to BuildingDef
- [x] Implement use cost (pawn pays building)
- [x] Implement work payout (building pays pawn from stores)
- [x] Implement work buy-in (calculated from payout)
- [x] Gold source buildings create money when worked
- [x] Add gold to info panels
- [ ] Configure baseCost/baseProduction per building type in content files
- [ ] Add level to BuildingComponent
- [ ] Implement level-based cost/payout scaling
- [ ] Visual indicator for building level
- [ ] **Housing evolution** (Caesar 3-style)
  - Tent → Shack → Cottage → House (tied to building level)
  - Level upgrades based on gold flowing through

---

## Tertiary Loop: New Sim / Save States - 0%

Starting fresh, saving progress, sharing creations.

### Deferred until baseline is stable
- Save/load game state
- New sim creation
- Potentially: sharing, scenarios, challenges
