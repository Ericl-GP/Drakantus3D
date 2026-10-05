---
name: unity-camera
description: Implement and maintain third-person or first-person Unity camera systems without coupling unrelated gameplay systems.
---

# Unity Camera

Keep camera behavior independent from gameplay rules where possible.

## Consider
- target following;
- look input;
- smoothing;
- collision;
- zoom;
- shoulder switching;
- aim mode;
- sensitivity;
- camera state.

## Rules
- Reuse the project's camera solution if one exists.
- Avoid putting camera logic into unrelated player combat or stats classes.
- Avoid frame-rate-dependent smoothing.
- Validate camera behavior around walls, slopes, stairs, and rapid player movement where relevant.
