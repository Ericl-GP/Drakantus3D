# Drakantus 3D (Unity 6)

RPG de ação isométrico low-poly: Praça de Aster → Guilda → Torre (2 andares com chefes) → loot → evoluir.

## Como abrir
1. Unity Hub → **Add → Add project from disk** → escolha esta pasta `Drakantus3D` (versão 6000.6.4f1).
2. Espere a importação inicial (alguns minutos). O menu **Drakantus/1 - Configurar tudo** roda sozinho uma vez:
   cria materiais, o Animator, a biblioteca de modelos e a cena `Assets/Scenes/Drakantus.unity`.
   Se não rodar, clique nele manualmente.
3. Abra a cena **Drakantus** e aperte **Play**.

## Controles
| Tecla | Ação |
|---|---|
| WASD | Andar |
| Mouse | Mirar |
| Clique esq. / Espaço | Atacar |
| Clique dir. / Shift | Defender |
| F | Parry (no tempo certo devolve o golpe) |
| Q | Esquiva |
| Z X C V | Habilidades |
| 1 / 2 | Poção de vida / mana |
| E | Falar / usar portal |
| I / K | Bolsa / Habilidades |
| Esc | Fechar janela / Pausa |
| Scroll | Zoom |
| F10 | Admin (teste): +1 nível, cura, moedas |

## Fluxo do jogo
Novo jogo → escolha a classe → fale com a **Sera** na Guilda para se registrar → fale com o **Guardião Teodor** ao lado da Torre → Andar 1 (chefe Rei Grumak) → santuário → Andar 2 (Necromante Ancião).

## Estrutura
- `Assets/Drakantus/Scripts` — código (Core, Actors, World, FX, UI); `Assets/Drakantus/Editor` — importação e setup.
- `Assets/Drakantus/Resources/Data` — classes, habilidades, inimigos, itens e NPCs em JSON (edite números aqui).
- `Assets/Drakantus/Resources/Audio` — sons e músicas sintetizados (originais). Regenerar: `python Tools/audio/gen_audio.py`.
- `Assets/KayKit` — modelos KayKit (CC0, uso comercial livre).
- `docs/GDD.md` — documento de design.
- `CONTRACT.md` — contrato técnico entre os módulos.

## Licenças
KayKit (Kay Lousberg) é CC0. Sons/música gerados pelo projeto. Os ícones `Resources/UI/icons_32.png` (Raven Fantasy Icons) vieram do projeto Godot — confirme a licença antes de vender.
