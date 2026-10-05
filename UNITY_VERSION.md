# Unity Version Lock

## Required Unity Editor
**6000.6.4f1**

Do not assume APIs, package behavior, editor behavior, or workflows from another Unity version when a version-specific difference matters.

## Version policy
- Prefer APIs documented for Unity 6 / 6000.6.4f1.
- If an older tutorial or code pattern conflicts with current Unity APIs, adapt it rather than blindly copying it.
- Do not recommend changing the Unity version as a solution unless the user explicitly asks about upgrading/downgrading or a verified blocker requires it.
- When a package version matters, inspect `Packages/manifest.json` and `Packages/packages-lock.json` rather than guessing.

## Current release awareness
This project uses a very recent Unity 6 release. Version-specific behavior should be verified against the official Unity documentation/release notes when needed.

## Known areas to verify when relevant
- Input System
- AI/NavMesh
- UI Toolkit
- SRP/Render Graph
- shader behavior
- Build Profiles
- serialization/editor behavior
