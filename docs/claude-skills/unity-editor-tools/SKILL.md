---
name: unity-editor-tools
description: Create or modify Unity Editor tools, custom inspectors, editor windows, menu commands, and asset-processing helpers.
---

# Unity Editor Tools

Keep editor-only code separated from runtime code.

## Rules
- Place editor-only code in appropriate editor assemblies/folders according to project architecture.
- Never introduce UnityEditor dependencies into runtime assemblies accidentally.
- Make tools safe to run repeatedly.
- Avoid destructive automatic scene or asset processing.

## Validation
Confirm editor tools do not execute in player builds and do not silently rewrite project assets.
