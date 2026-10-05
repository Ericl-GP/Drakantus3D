---
name: unity-scriptable-data
description: Design and maintain Unity ScriptableObjects for reusable configuration and game data. Use when game data should be shared or edited independently of runtime instances.
---

# Unity ScriptableObjects

Use ScriptableObjects for appropriate shared/configuration data, not as a universal replacement for runtime state.

## Good candidates
- item definitions;
- weapon definitions;
- enemy configurations;
- abilities;
- balancing data;
- audio references;
- progression definitions.

## Rules
- Separate immutable/configuration data from runtime mutable state.
- Avoid accidentally storing per-session state in shared assets.
- Preserve asset references.
- Avoid creating many unnecessary asset types for trivial values.

## Validation
Consider asset lifecycle, runtime mutation, duplication, and editor-time behavior.
