---
name: unity-input
description: Implement player and gameplay input in Unity while respecting the project's current input system. Use when adding movement, actions, controls, rebinding, or controller support.
---

# Unity Input

First determine whether the project uses Unity's newer Input System, legacy input, or an abstraction layer.

## Rules
- Reuse the project's current input architecture.
- Do not introduce a second input system without an explicit migration plan.
- Keep raw input separate from gameplay decisions where the project's architecture supports it.
- Centralize actions that need rebinding or multiple control schemes.

## Before implementation
Search for:
- input actions;
- player input components;
- existing action maps;
- input wrappers/services;
- movement/combat input handlers.

## Validation
Check keyboard/mouse and controller paths when supported by the project.
