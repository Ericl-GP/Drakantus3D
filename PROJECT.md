# Drakantus 3D — Unity Game

## Purpose
Compact project memory for Claude. Keep this file short and durable.

## Engine
Unity 6000.6.4f1 · URP 17.6 · Input System 1.19 (Active Input Handling = Both) · UGUI legado (sem TextMeshPro).

## Current milestone
Núcleo jogável: Praça de Aster → Guilda → Torre (andares 1–2 + andares criados no Criador de Andares). Combate com combo de 3 golpes, 24 habilidades com efeitos próprios e ultimate por classe (R). Bolsa com 12 slots, missões, Rank F→DEUS e pets.

## Architecture decisions
- Jogo montado em runtime a partir de uma cena quase vazia (`Assets/Scenes/Drakantus.unity` com `DrakantusBoot`); mapas gerados por código (`LevelBuilder`) ou prefabs em `Resources/Floors/`.
- Dados em JSON em `Resources/Data` (classes, skills, enemies, items, npcs, quests, ranks, pets, biomes, bosses, floors). `Tools/gen_data.py` está DESATUALIZADO: não rodar (apagaria itens/inimigos novos).
- Modelos KayKit (CC0) acessados por id via `ModelLibrary` (ScriptableObject em Resources), com fallback em primitivas.
- Sem layers/tags customizados, sem NavMesh; combate por listas (`Game.I.enemies`).
- Save principal: `persistentDataPath/drakantus_save.json`; Guilda em `drakantus_guild.json`.

## Version lock
Unity 6000.6.4f1. Do not silently target another Unity version.
`Object.GetInstanceID()` é ERRO de compilação nesta versão; `FindObjectsSortMode`/`FindFirstObjectByType` são só avisos.

## Current systems
Ver ARCHITECTURE.md.

## Important constraints
- PC (Windows). Smart App Control do Windows precisa ficar DESLIGADO (bloqueava Unity.SourceGenerators/Burst).
- Ao trocar arquivos pelo bridge, usar stagedPath novo a cada envio (cache de caminho).
- Comercial: só assets CC0/licença comercial. Ícones Raven (icons_32.png) — confirmar licença antes de vender.
