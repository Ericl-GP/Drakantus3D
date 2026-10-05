---
name: unity-animation
description: Maintain Unity Animator, animation states, transitions, parameters, blend trees, and gameplay animation integration.
---

# Unity Animation

Treat Animator parameters and state names as integration contracts.

## Before changes
Inspect:
- Animator Controller;
- parameters;
- layers;
- state machine;
- animation events;
- scripts setting parameters.

Do not rename parameters casually.

## Integration
Keep gameplay authority in gameplay systems. Animation should represent state unless the project deliberately uses animation-driven authority.

## Validation
Check transitions, missing clips, invalid parameters, animation events, and state interruption.
