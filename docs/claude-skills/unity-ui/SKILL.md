---
name: unity-ui
description: Build Unity UI systems for HUDs, menus, inventory, health, settings, notifications, and gameplay feedback while preserving separation from game logic.
---

# Unity UI

UI displays and requests state; it should not become the source of truth for gameplay state.

## Rules
- Reuse existing UI architecture.
- Avoid direct references from every gameplay class to UI widgets.
- Prefer events or presentation adapters when appropriate.
- Keep update frequency proportional to actual state changes.

## Performance
Do not rebuild complex UI every frame if the displayed data has not changed.

## Validation
Check different resolutions/aspect ratios when relevant and verify disabled/destroyed UI objects do not receive updates.
