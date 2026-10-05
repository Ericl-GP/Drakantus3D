# Drakantus 3D — Contrato técnico da equipe (LEIA INTEIRO ANTES DE ESCREVER CÓDIGO)

Projeto Unity **6000.6.4f1**, URP 17.6, Input System 1.19 (Active Input Handling = Both), UGUI 2.6.
Raiz do projeto: `/home/claude/d3d`. Todo o código do jogo: `Assets/Drakantus/` (namespace **`Drakantus`**; editor: `Drakantus.EditorTools`).
**NÃO há compilador C# disponível aqui.** O código vai direto para o Unity do usuário. Portanto:
- Escreva C# conservador e 100% compatível com Unity 6 (C# 9). Sem pacotes extras (sem TextMeshPro, sem NavMesh, sem DOTween).
- Use apenas as APIs listadas aqui para falar com os outros módulos. Assinaturas EXATAS.
- Releia seu arquivo inteiro no fim procurando erros de compilação (tipos, using, ponto-e-vírgula, chaves, nomes de métodos Unity).
- APIs Unity 6: use `FindFirstObjectByType`, não `FindObjectOfType`. `Rigidbody.linearVelocity` (evite Rigidbody; usamos CharacterController). Use `UnityEngine.UI.Text` com fonte `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.
- Input: SEMPRE via `InputW` (nunca `UnityEngine.Input` direto).
- Não use `layers`/`tags` customizados (TagManager não tem nenhum). Combate usa listas (`Game.I.enemies`), não física.
- Cada agente só cria/edita os arquivos que são dele (lista no fim). Se precisar de algo de outro módulo que não existe aqui, NÃO invente: deixe um `// TODO(contrato)` e reporte.

Escala: 1 unidade = 1 metro. Herói KayKit ≈ 1,8–2,0 m. Câmera isométrica (pitch ~50°, yaw 45°).

## Já existe (escrito pelo líder) — leia os arquivos para detalhes
- `Scripts/Core/InputW.cs` — `enum K { W,A,S,D,Up,Down,Left,Right,Space,Shift,Q,E,F,I,Kk,Z,X,C,V,Alpha1,Alpha2,Escape,Enter,Tab,F10,T,M }`; `InputW.Held(K)`, `InputW.Down(K)`, `InputW.MousePos`, `InputW.MouseHeld(int)`, `InputW.MouseDown(int)`, `InputW.Scroll`, `InputW.Move()`.
- `Scripts/Core/GameData.cs` — `GameData.Load()`, `Classes`, `ClassOrder`, `Skills`, `Enemies`, `Items`, `ItemOrder`, `Rarity`, `SkillKeys`, `Class(id)`, `Skill(id)`, `Item(id)`, `Enemy(id)`, `SkillsOf(classId)`, `RarityColor(ItemDef)`, `RarityGlow(ItemDef)`, `RarityName(ItemDef)`. Tipos: `ClassDef, SkillDef, EnemyDef, ItemDef, RarityDef, VfxDef`.
- `Scripts/Core/GameState.cs` — perfil salvo: `GameState.P` (Profile: playerName, classId, level, xp, coins, hp, mp, registered, items, weaponUid/armorUid/ringUid, loadouts, floorsCleared), `maxHp, maxMp, stamina, maxStamina, atkBuff, skillAtkBonus, skillSpeed`; eventos `Changed, InventoryChanged, LoadoutChanged, LeveledUp(int), SkillsUnlocked(List<string>), Notified(string)`; `Emit(), Notify(msg), HasSave(), NewGame(name), Save(), Load(), Class, ClassId, SetClass(id), AttackPower(), Defense(), MoveSpeed(), Recalc(), XpToNext(), AddXp(n), AddCoins(n), SkillUnlocked(id), CurrentLoadout(), SlotSkill(i), SetSlot(i,id), Owned(uid), Equipped(slot "arma"|"armadura"|"joia"), IsEquipped(uid), Grant(id,qty,equip), Count(id), Equip(uid), Buy(id), Sell(uid), Use(id)->string ("heal","mana","buff","return",""), RollChest(tier)->List<string>`.
- `Scripts/Core/U.cs` — `U.Hex(hex,a)`, `U.Lit(color, smooth, emission?)`, `U.Fx(additive, tex)`, `U.SoftTexture()`, `U.RingTexture()`, `U.WhiteSprite()`, `U.RoundSprite()` (9-slice arredondado), `U.CircleSprite()`, `U.Icon(int[] colRow)` / `U.Icon(col,row)` (ícones 32px Raven), `U.Prim(type,parent,pos,scale,color,collider)`, `U.MouseOnGround(cam,h,out p)`, `U.Flat(v)`, `U.FindDeep(root,"nome")`.
- `Scripts/FX/FX.cs` — `FX.Root`, `FX.Prefab(id,pos,scale)`, `FX.Burst(pos,color,size,count)`, `FX.Dust(pos,color,size)`, `FX.Sparkle(pos,color,size,time)`, `FX.Pillar(pos,color,size)`, `FX.Ring(pos,radius,color,time,startScale)`, `FX.Marker(pos,radius,color,time)->GameObject`, `FX.Slash(pos,dir,color,radius)`, `FX.Bolt(a,b,color)`, `FX.Flash(pos,color,intensity,time)`, `FX.FlatQuad(...)`, `FX.SetColor(renderer,color)`, `FX.Style(style,pos,color,size)`.
- `Scripts/World/ModelLibrary.cs` — ScriptableObject em `Resources/ModelLibrary.asset` (criado pelo menu de editor). `ModelLibrary.I`, `.Get(id)`, `.Set(id,prefab)`, `.entries`, `.characterAnimator`. Estático `Models.Has(id)`, `Models.Spawn(id,parent,localPos,yaw,scale)` (null se não houver), `Models.SpawnOr(...)` (cai para forma simples), `Models.Fallback(...)`.
- `Scripts/World/LevelInfo.cs` — `LevelInfo`, `EnemySpawn`, `NpcSpawn`, `PortalSpawn` (ver arquivo).
- `Scripts/Actors/CharacterVisual.cs` — `Build(modelId, bodyColor, scale)`, `SetWeapon(weaponModel, offhandModel, glowHex)` (procura modelo `"w_"+weaponModel`), `SetTint(color)`, `Flash()`, `Play(state, fade, lockTime, restart)`, `SetMoving(bool)`, `height`, `handR`, `handL`, `animator`.
  Estados de animação (nomes EXATOS no AnimatorController): `Idle, Run, Walk, Attack1, Attack2, Spin, Cast, Shoot, Hit, Death, Dodge, Block, Jump, Cheer, Interact, Wave, Sit, SkelIdle, SkelWalk, SkelAttack, SkelDeath, SkelRise, Taunt, Summon`.
- `Scripts/Actors/Player.cs` + `PlayerSkills.cs` — `Player.Create(pos)`, `Rebuild()`, `RefreshEquipment()`, `SetTorch(bool)`, `TakeDamage(amount, fromPos)->"parried"|"blocked"|"hit"|"ignored"`, `Revive()`, `Teleport(pos)`, campos `canFight, inputLocked, state, tauntTime, buffTime, buffSkill, shieldHp, shieldTime, visual, cooldowns`, `SkillCooldown(i)->Vector2(left,total)`, `UseSkill(i)`, `AimDir()`, `AreaDamage(...)`, `TryAttack()`, `TryDash(Vector3)`, `TryParry()`.

## A ser escrito pela equipe (assinaturas obrigatórias)

### Game (Programador de Sistemas) — `Scripts/Core/Game.cs`, `Scripts/Core/Bootstrap.cs`, `Scripts/World/CameraRig.cs`
```csharp
public class Game : MonoBehaviour {
  public static Game I;
  public Player player;
  public readonly List<Enemy> enemies;            // inimigos vivos/mortos do mapa atual
  public readonly List<Interactable> interactables;
  public LevelInfo level; public Transform levelRoot; public string mapId;
  public void Travel(string mapId, string spawn);  // fade, destrói levelRoot, LevelBuilder.Build, spawna player/inimigos/NPCs/portais
  public Enemy SpawnEnemy(string enemyId, Vector3 pos, float scale = 1f);
  public void OnEnemyDied(Enemy e);              // XP/moedas já são dados pelo Enemy; aqui: baú de chefe, contagem do andar, quest
  public void OnPlayerDied();
  public void Shake(float trauma);               // 0..1
  public void Hitstop(float seconds);
  public void Interact();                        // tecla E: interage com o Interactable mais próximo
  public void UsePotion(string itemId);
  public bool InDungeon { get; }
}
public class CameraRig : MonoBehaviour {
  public static Camera Cam;                      // câmera principal
  public static CameraRig I;
  public static Vector3 ToWorld(Vector2 input);  // WASD -> direção no chão relativa à câmera (y=0, normalizado se >1)
  public Transform target; public void Snap(); public void AddTrauma(float t);
}
```
Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` cria `Game` se não existir E se a cena ativa se chamar "Drakantus" (ou tiver um objeto `DrakantusBoot`). Game cria: luz direcional (sombras), Volume URP (bloom, vignette, tonemapping ACES, color adjustments) via `UnityEngine.Rendering`/`UnityEngine.Rendering.Universal`, câmera + CameraRig, EventSystem (com `UnityEngine.InputSystem.UI.InputSystemUIInputModule` sob `#if ENABLE_INPUT_SYSTEM`, senão `StandaloneInputModule`), HUD (`HUD.Create()`), e mostra a tela de título (`HUD.I.ShowTitleScreen()`).

### LevelBuilder (Artista 3D / Ambientes) — `Scripts/World/LevelBuilder.cs`
```csharp
public static class LevelBuilder {
  public static LevelInfo Build(string mapId, Transform root);  // mapIds: "town", "guild", "tower_f1", "tower_f2"
}
```
Monta SÓ o cenário estático (chão, paredes, casas, árvores, props, luzes de tocha, colisores BoxCollider/MeshCollider nos obstáculos, um chão com BoxCollider para o CharacterController) e devolve `LevelInfo` com spawns, inimigos, NPCs, portais, santuário, iluminação. Usa `Models.SpawnOr(id, ...)` com ids = nomes dos FBX KayKit (ver lista abaixo) para que o jogo rode mesmo sem modelos.

### Inimigos e objetos (Programador de Gameplay) — `Scripts/Actors/Enemy.cs`, `Projectile.cs`, `Trap.cs`, `Chest.cs`, `Scripts/World/Interactable.cs`
```csharp
public class Enemy : MonoBehaviour {
  public EnemyDef def; public float hp, maxHp, radius; public bool dead; public bool alerted;
  public static Enemy Spawn(string enemyId, Vector3 pos, Transform parent, float scaleMult = 1f);
  public void TakeHit(int dmg, Vector3 fromPos, bool crit);   // popup de dano (HUD.Popup), flash, knockback, HUD.I.OnHit, morte -> GameState.AddXp/AddCoins + Game.I.OnEnemyDied
  public void Stun(float seconds);
  public void SetTargeted(bool on);                           // anel vermelho no chão quando está no alcance do golpe
}
public static class Projectile { public static void Spawn(string kind, Vector3 pos, Vector3 dir, int dmg, bool fromPlayer, SkillDef s); }
// kinds: "arrow","pierce","orb","fireball","frost","arcane" (do jogador e de inimigos). Se s != null usa s.aoe, s.stun, s.pierce, s.color.
public static class Trap { public static void Spawn(Vector3 pos, SkillDef s, int dmg); }
public class Chest : MonoBehaviour { public static Chest Spawn(Vector3 pos, int tier); }      // abre ao encostar, solta itens (GameState.RollChest)
public class Interactable : MonoBehaviour {
  public string kind;     // "npc", "portal", "sanctuary"
  public string label;    // texto "[E] Falar com Sera"
  public float radius = 2f;
  public virtual void Interact();   // NPC -> HUD.I.Dialog(...)/janelas; portal -> Game.I.Travel; santuário -> cura + HUD.I.SanctuaryWindow
  public static Interactable SpawnNpc(string npcId, Vector3 pos, float yaw, Transform parent);   // usa Resources/Data/npcs.json
  public static Interactable SpawnPortal(PortalSpawn p, Transform parent);
  public static Interactable SpawnSanctuary(Vector3 pos, Transform parent);
}
```
IA dos inimigos: idle/patrulha → alerta (ícone "!") → persegue → **telegrafa o golpe com marca vermelha no chão** (FX.Marker) → ataca → recupera; à distância ("ranged" = "arrow"/"orb") mantém distância e atira com Projectile. Atordoado: estrelas/anel. Respeita `Game.I.player.tauntTime > 0` (vão no herói). Chefes (`def.boss`): mais lentos, golpe em área, nome sobre a cabeça; ao morrer `Game.I.OnEnemyDied` gera baú (def.chest). Modelo: `CharacterVisual.Build("enemy_" + def.model, ...)` e arma `def.weapon`.

### HUD e janelas (Programador de UI) — `Scripts/UI/HUD.cs`, `Scripts/UI/UIKit.cs`, `Scripts/UI/Windows.cs` (pode ser `partial class HUD`)
```csharp
public partial class HUD : MonoBehaviour {
  public static HUD I;
  public static HUD Create();
  public static void Popup(Vector3 worldPos, string text, Color color, bool big = false);  // número de dano flutuante
  public static bool PointerOverUI();
  public void FlashSlot(int i, Color c);
  public void OnHit(int dmg, bool crit, Enemy e);        // combo
  public void Toast(string msg);                         // também ligado a GameState.Notified
  public bool HasModal { get; } public string ModalKind { get; } public void CloseModal();
  public void ShowTitleScreen();                         // Novo jogo (nome) / Continuar
  public void ShowAreaTitle(string title, string subtitle);
  public void SetInstanceInfo(string text);              // painel de objetivo do andar ("" esconde)
  public void SetInteractHint(string text);              // "[E] Falar com Sera" ("" esconde)
  public void Dialog(string npcName, string role, string text, List<(string label, System.Action action)> buttons);
  public void ClassWindow(bool firstTime); public void SkillsWindow(); public void InventoryWindow();
  public void ShopWindow(string category); public void TowerWindow(); public void SanctuaryWindow(bool hasNext);
  public void DeathWindow(); public void PauseWindow();
  public void FadeOut(float t); public void FadeIn(float t);   // tela preta entre mapas
}
```
Visual: HUD limpo e moderno (painéis escuros translúcidos arredondados com `U.RoundSprite()`), canto sup. esq. retrato/emblema da classe + nome + nível + barras HP/MP/vigor com "rastro" de dano; centro inferior: barra de ações (Clique, Z X C V com recarga radial `Image.Type.Filled radial360` + número, Q esquiva, 1/2 poções) e barra de XP acima com "+XP"; canto sup. dir.: minimapa (segunda câmera ortográfica top-down renderizando numa RenderTexture com `RawImage`) + moedas + nome do lugar; vida do chefe no topo quando houver chefe com `alerted` perto; contador de COMBO (marcos 10/25/50/100 dão XP bônus via `GameState.AddXp`); faixa grande "NÍVEL X" e "NOVA HABILIDADE"; toasts. Menu Habilidades (K): cards de todas as habilidades da classe (bloqueadas mostram "Libera no nível X"), botões Z X C V para equipar (GameState.SetSlot) e drag&drop (IBeginDragHandler etc.). Teclas de janela tratadas pelo Game (I, K, Esc) chamando os métodos acima. Fonte: LegacyRuntime.ttf. CanvasScaler ScaleWithScreenSize 1920x1080, match 0.5.

### Áudio (Sound Designer) — `Scripts/Core/Sfx.cs`, `Resources/Audio/*.wav` (gerados por `Tools/audio/gen_audio.py`)
```csharp
public static class Sfx {
  public static void Play(string id, Vector3? pos = null, float volume = 1f, float pitchVar = 0.08f);  // id sem som = ignora em silêncio
  public static void Music(string id, float fade = 1.5f);   // "title","town","dungeon","boss" ("" para)
  public static float MasterVolume, MusicVolume, SfxVolume;
}
```
Ids de efeito (todos devem existir): `swing, swing_heavy, hit, hit_crit, bow, magic_cast, fireball, explosion, ice, lightning, heal, buff, shield, dash, step, enemy_hit, enemy_die, enemy_alert, enemy_attack, player_hurt, parry, block, coin, chest_open, item_rare, item_legendary, levelup, skill_unlock, ui_click, ui_open, ui_close, ui_error, portal, death, combo, boss_roar, footstep_stone`.
Todos os outros módulos DEVEM chamar `Sfx.Play(...)` nos momentos certos (o arquivo Sfx.cs será criado em paralelo; só use a assinatura acima).

### Editor + animação (Animador 3D) — `Assets/Drakantus/Editor/*.cs` (namespace `Drakantus.EditorTools`)
- AssetPostprocessor para `Assets/KayKit/**`: modelos de personagem e animações como **Generic** (os FBX de animação têm a mesma hierarquia `Rig_Medium/root/hips/...` dos personagens), clipes em loop (Idle_A/B, Running_A/B, Walking_A/B/C, Melee_Blocking, Skeletons_Idle, Skeletons_Walking, Sit_*_Idle, Waving, Melee_2H_Attack_Spinning, Ranged_Magic_Spellcasting), escala/materiais adequados, `Resources/UI/icons_32.png` com filtro Point, sem compressão, maxSize 8192.
- Menu `Drakantus/1 - Configurar tudo` (e roda sozinho uma vez via `[InitializeOnLoad]` + `EditorApplication.delayCall` se `Resources/ModelLibrary.asset` não existir): cria materiais `Resources/Materials/Lit.mat` (URP/Lit), `FxAdd.mat` e `FxAlpha.mat` (URP/Particles/Unlit transparente aditivo/alpha), monta o `AnimatorController` `Assets/Drakantus/Resources/CharacterAnimator.controller` com os estados listados acima mapeados para os clipes KayKit (tabela abaixo), preenche `ModelLibrary.asset` com TODOS os FBX de `Assets/KayKit` (id = nome do arquivo sem extensão) + os aliases abaixo, cria a cena `Assets/Scenes/Drakantus.unity` (vazia com objeto `DrakantusBoot`), coloca na Build Settings e abre.
- Aliases: `hero_Barbarian, hero_Knight, hero_Mage, hero_Ranger, hero_Rogue, hero_Rogue_Hooded` → Characters/*.fbx; `enemy_Skeleton_Minion/Warrior/Rogue/Mage` → Characters/Skeleton_*.fbx; armas: `w_sword`→sword_1handed, `w_sword2h`→sword_2handed, `w_axe`→axe_1handed, `w_axe2h`→axe_2handed, `w_bow`→bow_withString, `w_crossbow`→crossbow_1handed, `w_dagger`→dagger, `w_staff`→staff, `w_wand`→wand, `w_shield`→shield_round, `w_spear`→spear_A, `w_hammer`→hammer_A, `w_skel_sword`→Skeleton_Blade, `w_skel_dagger`→Skeleton_Blade, `w_skel_axe`→Skeleton_Axe, `w_skel_crossbow`→Skeleton_Crossbow, `w_skel_staff`→Skeleton_Staff, `w_skel_shield`→Skeleton_Shield_Small_A.
- Mapa estado→clipe: Idle=Idle_A, Run=Running_A, Walk=Walking_A, Attack1=Melee_1H_Attack_Slice_Diagonal, Attack2=Melee_1H_Attack_Chop, Spin=Melee_2H_Attack_Spin, Cast=Ranged_Magic_Shoot, Shoot=Ranged_Bow_Release, Hit=Hit_A, Death=Death_A, Dodge=Dodge_Forward, Block=Melee_Blocking, Jump=Jump_Full_Short, Cheer=Cheering, Interact=Interact, Wave=Waving, Sit=Sit_Floor_Idle, SkelIdle=Skeletons_Idle, SkelWalk=Skeletons_Walking, SkelAttack=Melee_1H_Attack_Chop, SkelDeath=Skeletons_Death, SkelRise=Skeletons_Awaken_Floor, Taunt=Skeletons_Taunt, Summon=Ranged_Magic_Summon. Clipes ficam dentro de `Assets/KayKit/Animations/Rig_Medium_*.fbx`.

### Dados e design (Game Designer) — `docs/GDD.md`, `Resources/Data/*.json`
Mantém os esquemas atuais de `classes.json`, `skills.json`, `enemies.json`, `items.json` (só balanceia/adiciona). Cria `Resources/Data/npcs.json`:
`{"npcs":[{"id":"sera","name":"Sera","role":"Recepcionista da Guilda","model":"hero_Rogue","tint":"ffffff","weapon":"","anim":"Idle","function":"register|shop:armas|shop:armaduras|shop:joias|shop:pocoes|class_master|skills|tower|quests|talk","lines":["...","..."]}]}`
(Interactable.SpawnNpc lê isto; `function` decide a ação ao falar.)

## Assets KayKit disponíveis (ids para Models.Spawn = nome do arquivo)
Ver `ls -R /home/claude/d3d/Assets/KayKit`. Pastas: Characters, Animations, Weapons, WeaponBits, Forest (Tree_*_Color1, Bush_*, Rock_*, Grass_*), Medieval (building_*, wall_*, fence_*, props como barrel, crate_A_big, tent, flag_red, weaponrack, target, trees_A_large...), Tools (torch, lantern, anvil, grindstone, bucket_metal, rope_bundle_A), Blocks (cubos 1x1? bricks_A, stone, stone_dark, gravel, dirt, dirt_with_grass, grass, wood, metal, lava), Resources (Gold_Nuggets, Wood_Log_Stack, ...).
Os tamanhos reais dos FBX são desconhecidos: quem posiciona deve medir (parse do FBX binário: nós "Vertices" + "UnitScaleFactor" em GlobalSettings; os arquivos em "fbx(unity)" já vêm em escala Unity) ou usar `Renderer.bounds` em tempo de execução para normalizar (ex.: escalar casas para ~6 m de altura).

## Donos dos arquivos
- Líder (não mexer sem pedir): Core/InputW, GameData, GameState, U; FX/FX; Actors/Player, PlayerSkills; World/ModelLibrary, LevelInfo.
- Programador de Sistemas: Core/Game.cs, Core/Bootstrap.cs, World/CameraRig.cs, World/Minimap? (não: minimapa é da UI).
- Programador de Gameplay: Actors/Enemy.cs, Projectile.cs, Trap.cs, Chest.cs, World/Interactable.cs.
- Programador de UI: UI/*.cs.
- Artista 3D: World/LevelBuilder.cs (+ World/LevelDecor.cs se quiser).
- Animador 3D: Editor/*.cs; pode editar Actors/CharacterVisual.cs (só a parte de animação/armas).
- Sound Designer: Core/Sfx.cs, Tools/audio/*, Resources/Audio/*.
- Game Designer: docs/GDD.md, Resources/Data/*.json, Tools/gen_data.py.
