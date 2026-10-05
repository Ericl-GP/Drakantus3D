---
name: unity-render-pipeline
description: Identify and respect the project's Built-in, URP, or HDRP rendering pipeline before making rendering, lighting, materials, shaders, post-processing, or performance changes.
---

# Unity Render Pipeline

Never assume the project's render pipeline.

## Before rendering changes
Inspect project settings and package dependencies to determine:
- Built-in Render Pipeline;
- Universal Render Pipeline (URP);
- High Definition Render Pipeline (HDRP).

Then use the matching APIs, materials, shaders, lighting workflow, and performance techniques.

## Rules
- Do not migrate render pipelines during an unrelated feature.
- Do not mix Built-in/URP/HDRP shader instructions.
- Reuse the project's existing pipeline and renderer configuration.
- Treat pipeline migration as a separate planned project task.

## Context economy
Only load this Skill for rendering/lighting/material/shader/post-processing/performance tasks.
