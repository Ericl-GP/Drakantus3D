---
name: unity-csharp
description: Write and modify production-quality C# for Unity using the project's existing conventions. Use for Unity gameplay and systems programming.
---

# Unity C#

## Principles
- Follow the project's existing C# style.
- Prefer clear components with one primary responsibility.
- Use serialized configuration instead of unnecessary hard-coded values.
- Avoid expensive work in Update when event-driven or cached approaches are practical.
- Cache component references when appropriate.
- Avoid unnecessary allocations in hot paths.
- Do not introduce architecture solely for theoretical future needs.

## Unity lifecycle
Choose Awake, OnEnable, Start, Update, FixedUpdate, LateUpdate, and teardown methods based on actual responsibility.

Do not move lifecycle logic casually; lifecycle order can be a dependency.

## Inspector and serialization
Treat serialized fields as part of the project's data contract.
Do not casually rename serialized fields or change serialized types.

When a serialized rename is necessary, preserve data using an appropriate Unity-supported migration mechanism.

## Validation
After code changes, inspect affected call sites, serialized references, and lifecycle assumptions.
