---
name: unity-ai-navigation
description: Develop enemy and NPC AI in Unity, including state machines, perception, navigation, patrol, chase, attack, and retreat behavior.
---

# Unity AI

Use the project's existing navigation and AI architecture.

## Inspect first
Look for:
- NavMesh/NavMeshAgent;
- custom navigation;
- state machines;
- behavior trees;
- perception systems;
- enemy base classes;
- combat interfaces.

## Architecture
Separate:
- perception;
- decision/state;
- movement;
- combat;
- animation.

Do not put every behavior into one monolithic enemy script.

## Performance
Avoid expensive full-world searches every frame. Cache references and use event-driven or timed checks when practical.

## Validation
Test state transitions and edge cases such as losing targets, blocked paths, death during pursuit, and disabled agents.
