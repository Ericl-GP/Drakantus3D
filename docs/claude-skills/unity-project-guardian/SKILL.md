---
name: unity-project-guardian
description: Protect and maintain a Unity project while making changes. Use before substantial Unity edits, scene changes, package changes, prefab changes, or architectural changes.
---

# Unity Project Guardian

Protect project integrity above all else.

## Before changes
Inspect only the relevant Unity files and determine:
- Unity project structure;
- active packages/dependencies;
- relevant scenes;
- relevant prefabs;
- relevant scripts;
- serialized references that may be affected.

Never assume a Unity object reference exists just because a field exists in code.

## Unity safety
Be careful with:
- serialized fields;
- prefab overrides;
- scene object references;
- ScriptableObjects;
- Animator references;
- layers and tags;
- input configuration;
- package dependencies;
- GUID/meta relationships.

Do not manually alter generated Unity files or .meta files unless the task specifically requires it.

## Change policy
Prefer focused changes. Do not reorganize folders, rename assets, replace prefabs, or change packages unless required.

## Completion gate
Before finishing, verify that changed scripts, prefabs, scenes, assets, and references remain coherent.
