---
name: unity-save-system
description: Design and maintain robust Unity save/load systems without coupling persistence directly to UI or transient scene objects.
---

# Unity Save System

Separate saved data from live scene objects.

## Workflow
1. Identify what is durable game state.
2. Define a serializable save representation.
3. Identify stable identifiers for entities.
4. Define load order and reconstruction.
5. Handle versioning where the project requires it.
6. Validate missing or changed data.

## Rules
Never serialize arbitrary scene object references as if they were stable save identifiers.

Do not make every gameplay class independently write save files.
