---
name: unity-scenes-prefabs
description: Safely modify Unity scenes, prefabs, prefab variants, and GameObjects. Use when a task involves scene hierarchy, prefabs, components, references, or serialized Unity data.
---

# Unity Scenes and Prefabs

Treat scene and prefab data as structured state, not ordinary text.

## Before editing
Identify:
- source prefab versus instance;
- prefab variants;
- scene objects;
- serialized references;
- components affected.

## Rules
- Prefer editing the source prefab when the behavior should be shared.
- Preserve prefab overrides when working on instances.
- Do not replace a prefab hierarchy merely to change one component.
- Do not delete and recreate objects when preserving references matters.
- Avoid mass scene modifications for a local feature.

## References
Whenever an object/component is renamed, moved, replaced, or deleted, consider:
- Inspector references;
- scripts;
- animation bindings;
- UnityEvents;
- prefab references;
- scene references.

## Validation
Confirm the intended scene/prefab still opens coherently and relevant references remain valid.
