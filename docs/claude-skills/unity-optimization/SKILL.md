---
name: unity-optimization
description: Diagnose and optimize Unity CPU, GPU, memory, rendering, physics, loading, and garbage-collection performance using evidence.
---

# Unity Optimization

Never optimize blindly.

## First classify
Determine whether the problem is primarily:
- CPU;
- GPU;
- memory;
- garbage collection;
- rendering;
- physics;
- loading/streaming;
- asset size;
- scripting.

## Inspect
Use Unity Profiler or available profiling evidence when possible.

## Common areas
- excessive Update calls;
- allocations in hot paths;
- expensive GetComponent/find operations;
- excessive Instantiate/Destroy;
- rendering cost;
- overdraw;
- texture/material size;
- physics queries;
- object counts;
- scene loading;
- LOD/culling.

## Rule
Measure -> change one meaningful variable -> validate.

Do not sacrifice maintainability for speculative micro-optimizations.
