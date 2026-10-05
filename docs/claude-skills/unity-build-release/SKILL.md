---
name: unity-build-release
description: Prepare and validate Unity builds for target platforms. Use for build configuration, quality settings, platform settings, and release preparation.
---

# Unity Build and Release

Before changing build settings, identify the target platform and existing release configuration.

## Verify
- scenes included in build;
- platform;
- scripting backend/configuration where relevant;
- quality settings;
- input;
- required packages;
- addressable/streaming assets if used;
- development versus release settings.

## Rules
Do not change project-wide settings casually during feature work.

## Release validation
Prefer a development build first when debugging release-specific problems, then validate the production configuration.
