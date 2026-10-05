---
name: unity-combat
description: Build and maintain Unity combat systems including damage, health, attacks, hit detection, weapons, cooldowns, status effects, and death.
---

# Unity Combat

First find the existing combat contracts.

## Separate concerns
Prefer clear boundaries between:
- attack intent;
- hit detection;
- damage;
- health;
- effects;
- animation;
- death.

## Rules
- Reuse existing damage/health interfaces.
- Do not duplicate health or damage authority.
- Do not tie damage directly to UI.
- Avoid expensive per-frame overlap queries when event/timing-based detection is appropriate.
- Make timing and damage configuration data-driven where useful.

## Validation
Check:
- multiple hits;
- invulnerability windows;
- death;
- friendly fire rules if applicable;
- attack interruption;
- animation timing;
- pooled objects if used.
