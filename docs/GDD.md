# Drakantus 3D — Documento de Design (GDD)

Versão 0.1 · Unity 6 · RPG de ação isométrico · Inspiração: Drakantos

## 1. Visão

Um RPG de ação curto, rápido e legível. O jogador sai da praça, se registra na Guilda, escolhe uma classe e sobe uma torre de andares em instância cheios de esqueletos. Cada luta dura segundos, cada andar dura minutos e cada volta para a cidade traz algo novo: nível, habilidade ou item.

Pilares:
- **Combate legível.** Todo golpe inimigo é telegrafado (marca vermelha no chão). Morrer é culpa do jogador, nunca do jogo.
- **Impacto.** Hitstop, tremor de câmera, números de dano grandes, partículas e som em todo acerto.
- **Recompensa frequente.** Algo bom acontece a cada 30–60 s: nível, combo, baú, moedas, habilidade nova.
- **Sessões curtas.** Um andar leva de 6 a 10 minutos. Dá para jogar "só mais um".

## 2. Loop principal

```
Praça (comprar / vender / conversar)
   └─> Guilda (registro, classe, habilidades, missões)
         └─> Guardião da Torre -> Andar N (instância)
               ├─ salas com inimigos -> combo, XP, moedas
               ├─ santuário (cura, opção de seguir)
               └─ chefe -> baú (raro / lendário)
         <── volta à Praça (portal ou Pergaminho de retorno)
   └─> equipar loot, comprar poções, subir de nível -> próximo andar
```

Primeira sessão esperada (cerca de 25 min): criar herói (2 min) → registro e classe (2 min) → Andar 1 (8 min, termina perto do nível 3–4) → loja (3 min) → Andar 2 (10 min, chega ao nível 6–7).

## 3. Controles

| Ação | Tecla |
|---|---|
| Mover (relativo à câmera) | WASD / setas |
| Mirar | Mouse |
| Ataque básico | Clique esquerdo / Espaço |
| Defender (segurar) | Botão direito / Shift |
| Parry (janela de 0,25 s) | F |
| Esquiva (invulnerável) | Q |
| Habilidades | Z X C V |
| Poção de vida / mana | 1 / 2 |
| Interagir | E |
| Inventário / Habilidades | I / K |
| Pausa | Esc |

Recursos: **Vida**, **Mana** (habilidades), **Vigor** (ataque 8, esquiva 18, parry 6, bloquear drena aos poucos; regenera rápido parado).

## 4. Classes

Fórmulas: dano = Poder de Ataque (PA) × multiplicador. PA = 1 + atk da classe + atk da arma/2 + (nível-1)/3. Vida máx. = (100 + bônus + (nível-1)×8) × hp da classe. Dano recebido = golpe − DEF×0,5.

| Classe | Papel | Vida | atk | DEF | Veloc. | Bloqueio* | Básico |
|---|---|---|---|---|---|---|---|
| Guerreiro | Dano corpo a corpo, mobilidade | ×1,15 | 1 | 0 | ×1,05 | 20% | Machado, alcance 2,4 m |
| Tank | Linha de frente, controle | ×1,5 | 1 | 4 | ×0,92 | 8% | Espada + escudo, 2,0 m |
| Arqueiro | Dano à distância, kite | ×0,85 | 0 | 0 | ×1,1 | 30% | Flecha |
| Mago | Área, explosão, cura | ×0,8 | 0 | 0 | ×1,0 | 30% | Orbe (+1 dano), +40 MP |

*Bloqueio = fração do dano que passa ao defender.

Os de perto têm PA maior e mais vida. Os de longe são frágeis, mas causam dano sem se expor e têm as habilidades com multiplicador mais alto.

### Guerreiro: "entra, gira, sai"
| Nv | Habilidade | MP / Recarga | Efeito | Quando usar |
|---|---|---|---|---|
| 1 | Golpe Giratório | 12 / 4 s | ×2,0 em 2,5 m | Cercado por 3 ou mais |
| 1 | Investida | 8 / 3,5 s | Avança atravessando, ×1,6, invulnerável | Fechar distância de arqueiros e magos, fugir de telegrafia |
| 3 | Grito de Guerra | 16 / 15 s | +3 PA e +15% velocidade por 6 s | Antes de entrar numa sala ou no chefe |
| 5 | Salto Esmagador | 20 / 7 s | Salta até 6,9 m, ×2,6, atordoa 1 s | Abrir luta em grupo, interromper chefe |
| 7 | Redemoinho de Aço | 26 / 10 s | ×0,7 a cada 0,25 s por 2,5 s (≈ ×7) | Com o Grito ativo, no meio do bando |
| 9 | Corte Executor | 22 / 8 s | Cone ×3,5, dobra em alvo abaixo de 40% | Finalizar chefe e brutamontes |

### Tank: "segura a linha"
| Nv | Habilidade | MP / Recarga | Efeito | Quando usar |
|---|---|---|---|---|
| 1 | Impacto | 12 / 5 s | ×1,3 em 2,6 m, atordoa 1,2 s | Interromper golpes telegrafados |
| 1 | Fortaleza | 16 / 14 s | −65% de dano recebido e provocação por 5 s | Chefe ou sala lotada |
| 3 | Escudada | 10 / 4 s | Investida curta ×1,5, atordoa 1,2 s | Pegar o mago ou arqueiro do fundo |
| 5 | Provocação | 10 / 9 s | Puxa todos em 5,6 m e dá escudo de 30 | Começo de sala |
| 7 | Terremoto | 24 / 10 s | Linha de 6 impactos ×1,8, atordoa | Corredores |
| 9 | Bênção do Guardião | 26 / 18 s | Cura 25% e −40% de dano por 6 s | Vida baixa no chefe |

### Arqueiro: "nunca para de andar"
| Nv | Habilidade | MP / Recarga | Efeito | Quando usar |
|---|---|---|---|---|
| 1 | Leque de Flechas | 14 / 4,5 s | 5 flechas ×0,9 | Grupo à frente; à queima-roupa num alvo grande |
| 1 | Flecha Perfurante | 10 / 3 s | ×2,8 atravessa | Inimigos em fila no corredor |
| 3 | Flecha Congelante | 12 / 5 s | ×1,8, congela a área por 1,2 s | Parar brutamontes ou chefe |
| 5 | Armadilha Explosiva | 14 / 8 s | ×3,0 em 2,5 m, atordoa | Plantar e recuar (kite) |
| 7 | Rolamento | 8 / 4 s | Recua 4 m, invulnerável, +30% de velocidade | Saída de emergência |
| 9 | Chuva de Flechas | 28 / 12 s | 14 flechas ×1,2 em 3 m | Chefe parado, grupo atordoado |

### Mago: "posiciona, explode, cura"
| Nv | Habilidade | MP / Recarga | Efeito | Quando usar |
|---|---|---|---|---|
| 1 | Bola de Fogo | 12 / 2,5 s | ×2,2, explosão de 1,9 m | Principal fonte de dano |
| 1 | Luz Curativa | 20 / 10 s | Cura 35% + 10 | Depois de levar dano, antes do chefe |
| 3 | Explosão Arcana | 20 / 5 s | ×3,0 em 2,25 m no ponto mirado | Grupos que avançam |
| 5 | Corrente de Raios | 20 / 6 s | ×1,6 saltando entre 5 inimigos | Salas cheias e espalhadas |
| 7 | Teleporte | 14 / 6 s | 5 m à frente, onda ×1,2, invulnerável | Escapar do cerco |
| 9 | Meteoro | 36 / 14 s | ×5,5 em 3,25 m, atordoa 1 s (0,9 s de atraso) | Chefe e bandos |

Regras de balanceamento: nenhuma habilidade passa de ×8 de dano total por uso. Habilidades de controle (atordoar) têm recarga de pelo menos 4 s. Buffs de defesa não se acumulam (o mesmo `buffTime` é compartilhado).

## 5. Progressão (nível 1 → 12)

XP para o próximo nível = 40 + (nível−1)×35. A cada nível: +8 de vida base (× classe), +4 MP, +1 DEF, vida e mana cheias. A cada 3 níveis: +1 PA.

| Nível | XP para subir | XP total | O que libera | Onde costuma acontecer |
|---|---|---|---|---|
| 1 | 40 | 0 | 2 habilidades iniciais, Andar 1 | Começo |
| 2 | 75 | 40 | — | Andar 1, 1ª sala |
| 3 | 110 | 115 | 3ª habilidade | Andar 1, metade |
| 4 | 145 | 225 | +1 PA. Andar 2 recomendado | Chefe do Andar 1 |
| 5 | 180 | 370 | 4ª habilidade (barra cheia) | Andar 2, início (ou 2ª volta no Andar 1) |
| 6 | 215 | 550 | — | Andar 2 |
| 7 | 250 | 765 | 5ª habilidade, +1 PA | Chefe do Andar 2 |
| 8 | 285 | 1015 | — | Repetir Andar 2 |
| 9 | 320 | 1300 | 6ª habilidade: final da classe | Repetir Andar 2 |
| 10 | 355 | 1620 | +1 PA. Marco: título "Veterano" (futuro) | Repetir Andar 2 |
| 11 | 390 | 1975 | — | — |
| 12 | — | 2365 | Nível máximo desta versão. Marco: "Campeão da Torre" (futuro) | — |

A partir do nível 5 o jogador tem mais habilidades que slots e passa a montar a barra (K): é a primeira escolha de build.

## 6. A Torre

Cada andar é uma instância (mapa próprio) com salas ligadas por corredores, um santuário antes do chefe e um portal de saída depois dele. Objetivo exibido no painel: "Derrote os inimigos (x/y)" e depois "Derrote o chefe".

### Andar 1: Catacumbas Esquecidas (`tower_f1`)
- **Tema:** criptas de pedra com tochas, teias e ossos. Escuro, com a tocha do herói acesa. Música `dungeon`, depois `boss`.
- **Nível alvo:** 1–3 (vencível no 1).
- **Inimigos:** lacaio (`minion`), ladino (`rogue`), arqueiro (`archer`), guerreiro (`warrior`).
- **Layout (5 salas, cerca de 24 inimigos):**
  1. Entrada: 4 lacaios (tutorial de ataque e esquiva).
  2. Corredor em L: 3 lacaios e 2 ladinos (ensina a defender).
  3. Salão: 2 guerreiros, 3 lacaios e 1 arqueiro no fundo (ensina a fechar distância).
  4. Galeria: 2 arqueiros, 2 ladinos e 2 guerreiros, mais 1 baú comum opcional.
  5. Santuário e depois a sala do trono do chefe (arena circular de 14 m).
- **Chefe: Rei Esqueleto Grumak** (`king`, 60 HP, 5× um guerreiro). Lento, golpe em área telegrafado. Ensina: sair da marca e punir a recuperação. Baú tier 1.
- **Tempo esperado:** 6–8 min. Recompensa média: cerca de 300 XP e 150 a 200 moedas, mais o baú.

### Andar 2: Cripta do Necromante (`tower_f2`)
- **Tema:** santuário profano com velas roxas, sarcófagos e névoa. Mais escuro e mais vertical (pilares para quebrar a linha de visão).
- **Nível alvo:** 4–7.
- **Inimigos:** guerreiro, arqueiro, mago (`mage`), brutamontes (`brute`, elite) e alguns lacaios como "bucha".
- **Layout (6 salas, cerca de 28 inimigos):**
  1. Átrio: 3 guerreiros e 2 arqueiros.
  2. Corredor de colunas: 2 magos e 3 lacaios (ensina a usar cobertura).
  3. Ossuário: 1 brutamontes e 4 lacaios.
  4. Biblioteca: 2 magos, 2 arqueiros e 2 guerreiros, mais 1 baú comum.
  5. Ponte: 2 brutamontes e 1 mago.
  6. Santuário e depois o altar do chefe (arena de 16 m com 4 pilares).
- **Chefe: Necromante Ancião** (`necro`, 120 HP, 6× um mago). Atira orbes de longe e mantém distância. Ideal: invocar lacaios a cada 25% de vida, se a programação de Gameplay permitir. Baú tier 2 (lendário com 45% de chance).
- **Tempo esperado:** 8–10 min. Recompensa média: cerca de 700 XP e 400 moedas, mais o baú.

### Tabela de inimigos
| id | Andar | HP | Dano | Veloc. | XP | Moedas | Comportamento |
|---|---|---|---|---|---|---|---|
| minion | 1–2 | 6 | 6 | 2,6 | 7 | 2–5 | Corpo a corpo fraco, em bando |
| rogue | 1 | 8 | 7 | 3,0 | 10 | 3–7 | Rápido, telegrafia curta (0,4 s) |
| archer | 1–2 | 7 | 6 | 2,2 | 11 | 4–8 | Atira a 9 m e foge |
| warrior | 1–2 | 12 | 9 | 2,2 | 13 | 4–9 | Corpo a corpo padrão |
| mage | 2 | 20 | 12 | 2,0 | 20 | 8–14 | Orbe a 8 m |
| brute | 2 | 34 | 18 | 1,8 | 28 | 10–18 | Elite lento, golpe pesado (0,8 s) |
| king | chefe 1 | 60 | 14 | 2,0 | 60 | 30–45 | Golpe em área |
| necro | chefe 2 | 120 | 20 | 1,8 | 140 | 80–110 | Orbes a 9 m |

Verificação: no nível 1 (PA 2–3) um lacaio cai em 2 a 3 golpes e um guerreiro em 4 a 6. No nível 5 (PA 4–6 com arma comum) um mago cai em 4 a 5 golpes ou com 2 habilidades. Um brutamontes acerta cerca de 15 numa vida de cerca de 130: dá para errar 8 vezes.

## 7. Economia

- **Moedas iniciais:** 650 (perfil padrão). Dá para comprar uma peça comum e poções logo de cara.
- **Fontes:** inimigos (2–18), chefes (30–110), venda de itens (cerca de 50% do preço) e marcos de combo (XP).
- **Ritmo:** uma arma ou armadura comum custa cerca de 1 Andar 1; uma rara custa cerca de 1 Andar 2. Lendários só saem de baú de chefe e não são vendidos.

| Categoria | Comum | Raro | Lendário |
|---|---|---|---|
| Armas | 140–170 | 300–380 | só baú |
| Armaduras | 120 | 300–380 | só baú |
| Joias | inicial | 160–260 | — |
| Poções | vida 40, mana 30, elixir 90, retorno 60, grande 120 | | |

**Baú de chefe** (`GameState.RollChest`): sempre 1 poção de vida; mana com 60%; elixir ou pergaminho com 50%; tier 2 traz uma poção grande. Arma ou armadura rara: 60% no tier 1 e 90% no tier 2. Lendária: 12% no tier 1 e 45% no tier 2.

**Itens novos nesta versão:** Martelo de ferro (`iron_hammer`, comum, ATQ +5 DEF +1, 170), Adaga sombria (`shadow_dagger`, rara, ATQ +8 MP +10, 340), Cajado Estelar (`star_staff`, lendário, ATQ +13 MP +40, só baú) e Anel do guardião (`guardian_ring`, raro, HP +15 MP +15, 260).

## 8. NPCs (`Resources/Data/npcs.json`)

| id | Nome | Local | Função |
|---|---|---|---|
| bram | Bram, ferreiro | Praça | `shop:armas` |
| rurik | Rurik, armeira | Praça | `shop:armaduras` |
| iria | Iria, joalheira | Praça | `shop:joias` |
| mika | Mika, alquimista | Praça | `shop:pocoes` |
| guardiao | Guardião Teodor | Porta da torre | `tower` |
| tomas | Tomás, guarda | Praça | `talk` (dicas de defesa) |
| pip | Pip, criança | Praça | `talk` |
| lia | Lia, criança | Praça | `talk` |
| lirio | Lírio, bardo | Praça (sentado) | `talk` |
| dario | Dário, mercador | Praça | `talk` (rumores do Andar 2) |
| marta | Marta, aldeã | Praça | `talk` |
| ezio | Velho Ézio | Praça (sentado) | `talk` (dica de parry) |
| sera | Sera, recepcionista | Guilda | `register` |
| aldra | Mestra Aldra | Guilda | `class_master` |
| kael | Instrutor Kael | Guilda | `skills` |
| oren | Oren, missões | Guilda | `quests` |
| nyx | Nyx, aventureira | Guilda (sentada) | `talk` (dica de esquiva) |
| borin | Borin, aventureiro | Guilda | `talk` (fala de combo e loot) |

As falas dos NPCs `talk` servem como tutorial disfarçado: defender, parry, esquiva, marca vermelha, combo e loot.

## 9. Metas de "viciante"

- **Combo:** contador na tela; marcos de 10, 25, 50 e 100 dão XP bônus, um popup grande e o som `combo`. O combo zera depois de cerca de 3 s sem acertar.
- **Impacto:** hitstop de 0,04 a 0,12 s, tremor proporcional, crítico (12%) com número grande e cor diferente, knockback e flash branco no inimigo.
- **Recompensa a cada 30–60 s:** sala limpa, nível, habilidade nova, baú opcional, moedas pingando com som.
- **Marcos visíveis:** faixa "NÍVEL X", "NOVA HABILIDADE" e o primeiro item raro e lendário com brilho e som próprios (`item_rare`, `item_legendary`).
- **Fim de andar:** tela de resumo (tempo, maior combo, XP, moedas) e botão "Próximo andar" ou "Voltar à cidade".
- **Morte barata:** volta à cidade mantendo XP e moedas, sem frustração; perde só o progresso do andar.
- **Gancho de saída:** o bardo e os aventureiros comentam os feitos ("derrubou o Grumak?"), e a próxima habilidade aparece como "Libera no nível X" no menu K.

## 10. Próximos passos (versões futuras)

1. Andares 3–5 (floresta corrompida, forja de lava, topo do dragão) com inimigos novos (não-esqueletos) e chefe final dragão.
2. Mercenário de apoio (a Luz Curativa já menciona "cura o mercenário").
3. Sistema de missões real no Oren: "mate 20 lacaios", "termine o Andar 1 em menos de 6 min", recompensando moedas e itens.
4. Afixos aleatórios em itens raros e lendários (+crítico, +vigor, roubo de vida) e joias lendárias.
5. Árvore de talentos dos níveis 10 a 12 e títulos.
6. Modo desafio: andar com modificadores (inimigos rápidos, sem poções) e placar de melhor tempo.
7. Regeneração de mana maior ou mana por acerto (hoje é 0,8 MP/s, ver notas de balanceamento).
8. Cooperativo para 2 jogadores na torre.

## Notas de balanceamento para a equipe

- A regeneração de mana (0,8/s em `Player.cs`) é baixa: um mago gasta cerca de 50 MP em 20 s de luta. Por isso os custos de MP caíram de 10 a 25%. Recomendo subir para cerca de 2 MP/s ou dar +1 MP por acerto básico.
- O dano recebido é reduzido em DEF×0,5: o Tank (DEF +4) anula cerca de 2 pontos por golpe a mais que os outros.
- `EnemySpawn.scale` pode ser usado para criar "elites" (por exemplo, um guerreiro em escala 1,2 no Andar 2), se o Enemy multiplicar a vida pela escala.
