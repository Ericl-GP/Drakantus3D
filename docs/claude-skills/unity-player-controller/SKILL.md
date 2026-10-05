---
name: unity-player-controller
description: Build and maintain a Unity 3D player controller including movement, camera interaction, jumping, sprinting, crouching, and locomotion states.
---

# Unity Player Controller

Preserve separation between input, movement, animation, and camera responsibilities when practical.

## Before changes
Inspect existing:
- movement controller;
- CharacterController or Rigidbody usage;
- camera system;
- animator;
- input actions;
- player stats/state.

Do not create a second movement system.

## Physics
Use CharacterController or Rigidbody according to the existing project architecture. Do not mix movement models casually.

## Feature implementation
For movement features:
1. identify state/data;
2. identify input;
3. identify movement authority;
4. implement;
5. update animation/camera only where necessary;
6. validate edge cases.

Avoid giant player scripts when existing components can own separate responsibilities.
