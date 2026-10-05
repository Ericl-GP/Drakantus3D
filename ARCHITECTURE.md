# Architecture

## Rules
- Reuse existing systems before creating new ones.
- Keep feature changes focused.
- Avoid unrelated refactors.
- Treat serialized Unity data and references as contracts.
- Separate configuration data from runtime state where appropriate.
- Prefer composition and small responsibilities.
- Validate after meaningful changes (compilar na Unity e ler `Logs/Editor.log`).

## System map (Assets/Drakantus/Scripts, namespace Drakantus)
- **Core**: `Game` (singleton, viagem entre mapas, spawn, hitstop/shake), `Bootstrap`, `GameState` (perfil/save, 12 slots de equipamento, bolsa 36), `GameData` (defs JSON), `InputW` (wrapper dos dois input systems), `Sfx` (áudio com pool), `U` (utilitários/materiais).
- **Actors**: `Player` (+ `PlayerSkills`, `PlayerUltimate` partial), `Enemy` (+ `EnemyAbilities`), `Projectile`, `Trap`, `Chest`, `CharacterVisual` (modelo/animação/armas/tinta), `Equipment` (itens visíveis).
- **FX**: `FX` (partículas, slash em arco, luzes temporárias com pool), `SkillFX` (efeito por habilidade e ultimates).
- **World**: `CameraRig` (zoom isométrico→3ª pessoa, `Punch`), `OcclusionFader`, `LevelBuilder`/`LevelDecor`/`LevelInfo`, `Interactable` (NPCs/portais/santuário), `ModelLibrary`, `Floors/*` (FloorRoot, marcadores, armadilhas, portas/alavancas, `FloorRegistry`).
- **Guild**: `GuildState`, `Quests`, `Ranks`, `Pets`, `Loot` (itens no chão).
- **UI** (partial class `HUD`): `HUD`, `UIKit`, `Windows`, `Inventory`, `Appearance`, `Ultimate`, `GuildWindows`, `Minimap`.
- **Editor** (`Drakantus.EditorTools`): `DrakantusSetup` (menu Drakantus/1–5), `KayKitImporter`, `AudioImport`, `EnsureURP`, `ForceRebuild`, `FloorCreator/*` (Criador de Andares).

## Contratos
Assinaturas entre módulos documentadas em `CONTRACT.md` (pode estar parcialmente desatualizado; conferir no código).
