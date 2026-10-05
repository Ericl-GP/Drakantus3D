---
name: unity-version-guard
description: Keep Unity-specific work compatible with Unity 6000.6.4f1 and prevent accidental use of outdated or mismatched Unity APIs. Use whenever a task depends on Unity version, package APIs, editor behavior, or tutorials.
---

# Unity Version Guard

The project is locked to **Unity 6000.6.4f1**.

## Rules
1. Treat 6000.6.4f1 as the target editor.
2. Do not silently downgrade or target another Unity release.
3. For package-specific APIs, inspect the project's package manifest/lock file.
4. Prefer current Unity 6 APIs over old tutorial patterns when they differ.
5. If a version-specific behavior is uncertain and materially affects the implementation, verify official Unity documentation/release notes before deciding.
6. Never add compatibility code for old Unity versions unless the project actually requires it.

## Context economy
Do not research version details for ordinary C# work that is clearly version-independent. Verify only when the task is version-sensitive.
