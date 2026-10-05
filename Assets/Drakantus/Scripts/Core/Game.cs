using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Drakantus
{
    /// <summary>
    /// Núcleo do jogo: cria luz, pós-processamento, câmera, EventSystem e HUD; viaja entre mapas;
    /// guarda inimigos/interativos do mapa atual; trata teclas globais, morte, santuário e torre.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I;

        public Player player;
        public readonly List<Enemy> enemies = new();
        public readonly List<Interactable> interactables = new();
        public LevelInfo level;
        public Transform levelRoot;
        public string mapId = "";

        public bool InDungeon => level != null && level.kind == "dungeon";
        public bool Traveling => traveling;
        public bool Started => started;

        Light sun;
        Camera cam;
        bool started, traveling, playerDead, hitstopActive, pendingClassWindow, floorDone;
        float floorTime, infoClock;
        string lastHint = null;

        // ------------------------------------------------------------------ criação
        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Time.timeScale = 1f;
            GameData.Load();
            SetupLight();
            SetupCamera();
            SetupPostProcessing();
            SetupEventSystem();
        }

        void Start()
        {
            HUD.Create();
            if (HUD.I != null) HUD.I.InstallGuild();   // [Guilda] rastreador de missões, selo de rank e pets
            BuildBackdrop();
            Sfx.Music("title");
            if (HUD.I != null) HUD.I.ShowTitleScreen();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void OnApplicationQuit()
        {
            if (started) GameState.Save();
            if (started) GuildState.Flush();   // [Guilda]
        }

        void SetupLight()
        {
            // desliga luzes direcionais que vieram na cena
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            var lg = new GameObject("Sol");
            lg.transform.SetParent(transform, false);
            lg.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun = lg.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.95f, 0.85f);
            RenderSettings.sun = sun;
        }

        void SetupCamera()
        {
            // desliga câmeras que vieram na cena
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            var cg = new GameObject("Camera");
            cg.tag = "MainCamera";
            cam = cg.AddComponent<Camera>();
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 250f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.16f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.renderShadows = true;
            }
            if (FindFirstObjectByType<AudioListener>() == null) cg.AddComponent<AudioListener>();
            cg.AddComponent<CameraRig>();
            CameraRig.Cam = cam;
        }

        void SetupPostProcessing()
        {
            var vg = new GameObject("PosProcessamento");
            vg.transform.SetParent(transform, false);
            var volume = vg.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.6f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.65f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.2f);
            color.contrast.Override(8f);
            color.saturation.Override(10f);
            volume.sharedProfile = profile;
        }

        void SetupEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>Monta a cidade atrás da tela de título (sem jogador).</summary>
        void BuildBackdrop()
        {
            try
            {
                var root = new GameObject("Nivel_titulo").transform;
                levelRoot = root;
                level = LevelBuilder.Build("town", root);
                if (level != null)
                {
                    ApplyLighting(level);
                    if (CameraRig.I != null) CameraRig.I.Focus(level.Spawn("default"));
                }
            }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        // ------------------------------------------------------------------ fluxo principal (chamado pelo HUD)
        /// <summary>Começa a jogar (depois de GameState.NewGame ou GameState.Load feitos pela tela de título).</summary>
        public void StartGame()
        {
            if (traveling) return;
            GameData.Load();
            started = true;
            playerDead = false;
            GameState.Recalc();
            pendingClassWindow = string.IsNullOrEmpty(GameState.P.classId) || !GameData.Classes.ContainsKey(GameState.P.classId);
            GuildState.OnStartGame();   // [Guilda] carrega (ou zera, se for herói novo) o save da Guilda
            Travel("town", "default");
        }

        public void Travel(string mapId, string spawn)
        {
            if (traveling || string.IsNullOrEmpty(mapId)) return;
            StartCoroutine(TravelRoutine(mapId, spawn));
        }

        IEnumerator TravelRoutine(string map, string spawn)
        {
            traveling = true;
            if (player != null) player.inputLocked = true;
            if (HUD.I != null) { HUD.I.SetInteractHint(""); lastHint = ""; HUD.I.FadeOut(0.25f); }
            yield return new WaitForSecondsRealtime(0.3f);
            Time.timeScale = 1f;
            hitstopActive = false;
            if (HUD.I != null) HUD.I.SetInstanceInfo("");

            ClearLevel();
            yield return null; // deixa o Unity destruir o mapa antigo

            mapId = map;
            var root = new GameObject("Nivel_" + map).transform;
            levelRoot = root;
            LevelInfo info = null;
            try { info = LevelBuilder.Build(map, root); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            if (info == null)
            {
                Debug.LogWarning("[Drakantus] LevelBuilder não montou o mapa " + map + "; usando chão simples.");
                info = new LevelInfo { id = map, title = map };
                U.Prim(PrimitiveType.Cube, root, new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60), U.Hex("5a6a4a"), true);
            }
            level = info;
            ApplyLighting(level);

            // jogador
            Vector3 sp = level.Spawn(spawn);
            if (player == null) player = Player.Create(sp);
            else player.Teleport(sp);
            if (playerDead || player.state == "dead") { player.Revive(); playerDead = false; }
            player.canFight = level.kind == "dungeon";
            player.SetTorch(level.dark);
            player.inputLocked = true;
            if (CameraRig.I != null) { CameraRig.I.target = player.transform; CameraRig.I.Snap(); }

            // população
            foreach (var es in level.enemies) SpawnEnemy(es.id, es.pos, es.scale > 0 ? es.scale : 1f);
            foreach (var n in level.npcs) Register(Interactable.SpawnNpc(n.npcId, n.pos, n.yaw, levelRoot));
            foreach (var p in level.portals) Register(Interactable.SpawnPortal(p, levelRoot));
            if (level.hasSanctuary) Register(Interactable.SpawnSanctuary(level.sanctuary, levelRoot));
            // [Guilda] Lumi (loja de pets) na praça, num canto livre perto do canteiro noroeste; pet acompanha o herói
            if (map == "town" && !level.npcs.Exists(ns => ns.npcId == "lumi"))
            {
                Vector3 lumiPos = new Vector3(-6f, 0f, 9f);
                float lumiYaw = Quaternion.LookRotation(-U.Flat(lumiPos)).eulerAngles.y;
                Register(Interactable.SpawnNpc("lumi", lumiPos, lumiYaw, levelRoot));
            }
            Pets.OnTravel(player);
            // [/Guilda]
            RandomEvents.OnFloorStarted(this);   // [Inimigos] Goblin de Ouro / Horda surpresa (só andares da Torre)

            floorTime = 0f;
            floorDone = false;
            infoClock = 0f;

            Sfx.Music(level.music);
            if (HUD.I != null)
            {
                HUD.I.ShowAreaTitle(level.title, level.subtitle);
                HUD.I.SetInstanceInfo(InDungeon ? InstanceText() : "");
                HUD.I.FadeIn(0.35f);
            }
            GameState.Save();
            traveling = false;

            if (pendingClassWindow)
            {
                pendingClassWindow = false;
                if (HUD.I != null) HUD.I.ClassWindow(true);
            }
        }

        void Register(Interactable it)
        {
            if (it != null && !interactables.Contains(it)) interactables.Add(it);
        }

        void ClearLevel()
        {
            foreach (var e in enemies) if (e != null) Destroy(e.gameObject);
            enemies.Clear();
            foreach (var it in interactables) if (it != null) Destroy(it.gameObject);
            interactables.Clear();
            var fr = FX.Root;
            for (int k = fr.childCount - 1; k >= 0; k--) Destroy(fr.GetChild(k).gameObject);
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = null;
        }

        void ApplyLighting(LevelInfo l)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = l.ambient;
            RenderSettings.fog = l.fogDensity > 0f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = l.fog;
            RenderSettings.fogDensity = l.fogDensity;
            if (sun != null) { sun.color = l.sun; sun.intensity = l.sunIntensity; }
            if (cam != null) cam.backgroundColor = l.fog;
        }

        public Enemy SpawnEnemy(string enemyId, Vector3 pos, float scale = 1f)
        {
            var e = Enemy.Spawn(enemyId, pos, levelRoot, scale);
            if (e != null && !enemies.Contains(e)) enemies.Add(e);
            return e;
        }

        // ------------------------------------------------------------------ loop / teclas globais
        void Update()
        {
            if (!started || HUD.I == null) return;
            var hud = HUD.I;
            bool modal = hud.HasModal;
            if (player != null) player.inputLocked = modal || traveling || playerDead;

            if (traveling) return;

            if (InputW.Down(K.Escape) && !playerDead)
            {
                bool mustChooseClass = string.IsNullOrEmpty(GameState.P.classId);
                if (modal) { if (!mustChooseClass) hud.CloseModal(); }
                else hud.PauseWindow();
            }
            else if (!playerDead && player != null)
            {
                if (!modal)
                {
                    if (InputW.Down(K.E)) Interact();
                    else if (InputW.Down(K.I)) hud.InventoryWindow();
                    else if (InputW.Down(K.Kk)) hud.SkillsWindow();
                    else if (InputW.Down(K.Alpha1)) UsePotion("health_potion");
                    else if (InputW.Down(K.Alpha2)) UsePotion("mana_potion");
                }
                else
                {
                    // I / K de novo fecham a própria janela
                    string mk = (hud.ModalKind ?? "").ToLowerInvariant();
                    if (InputW.Down(K.I) && mk.Contains("invent")) hud.CloseModal();
                    else if (InputW.Down(K.Kk) && (mk.Contains("skill") || mk.Contains("habil"))) hud.CloseModal();
                }
                if (InputW.Down(K.F10)) Admin();
            }

            // dica de interação
            string hint = "";
            if (!modal && !playerDead)
            {
                var it = NearestInteractable();
                if (it != null) hint = it.label ?? "";
            }
            if (hint != lastHint) { lastHint = hint; hud.SetInteractHint(hint); }

            // painel do andar
            if (InDungeon)
            {
                if (!floorDone && !modal) floorTime += Time.deltaTime;
                infoClock -= Time.unscaledDeltaTime;
                if (infoClock <= 0f) { infoClock = 0.5f; hud.SetInstanceInfo(InstanceText()); }
            }

            // caiu do mapa
            if (player != null && player.transform.position.y < -25f && level != null)
                player.Teleport(level.Spawn("default"));
        }

        string InstanceText()
        {
            int alive = AliveCount();
            int s = Mathf.FloorToInt(floorTime);
            string obj;
            var boss = AliveBoss();
            if (floorDone) obj = "Andar concluído!";
            else if (boss != null) obj = "Derrote " + boss.def.name;
            else if (level != null && level.hasSanctuary) obj = "Alcance o santuário";
            else obj = "Derrote as criaturas";
            return "Criaturas restantes: " + alive + " · Tempo " + (s / 60).ToString("00") + ":" + (s % 60).ToString("00") + " · Objetivo: " + obj;
        }

        public int AliveCount()
        {
            int n = 0;
            foreach (var e in enemies) if (e != null && !e.dead) n++;
            return n;
        }

        /// <summary>Chefe ainda vivo no mapa (ou null).</summary>
        public Enemy AliveBoss()
        {
            foreach (var e in enemies) if (e != null && !e.dead && e.def != null && e.def.boss) return e;
            return null;
        }

        void Admin()
        {
            GameState.AddXp(Mathf.Max(1, GameState.XpToNext() - GameState.P.xp));
            GameState.P.hp = GameState.maxHp;
            GameState.P.mp = GameState.maxMp;
            GameState.stamina = GameState.maxStamina;
            GameState.AddCoins(500);
            GameState.Save();
            if (HUD.I != null) HUD.I.Toast("Admin: +1 nível, vida cheia e +500 moedas.");
        }

        // ------------------------------------------------------------------ interação
        public void Interact()
        {
            var it = NearestInteractable();
            if (it != null) it.Interact();
        }

        Interactable NearestInteractable()
        {
            if (player == null) return null;
            Interactable best = null;
            float bd = float.MaxValue;
            Vector3 pp = player.transform.position;
            foreach (var it in interactables)
            {
                if (it == null || !it.isActiveAndEnabled) continue;
                float d = U.Flat(it.transform.position - pp).magnitude;
                if (d <= it.radius && d < bd) { bd = d; best = it; }
            }
            return best;
        }

        public void UsePotion(string itemId)
        {
            if (player == null || playerDead || traveling) return;
            string fx = GameState.Use(itemId);
            Vector3 p = player.transform.position;
            switch (fx)
            {
                case "heal":
                    FX.Pillar(p, U.Hex("8fe08a"), 0.9f);
                    Sfx.Play("heal", p);
                    break;
                case "mana":
                    FX.Pillar(p, U.Hex("6fb4ff"), 0.9f);
                    Sfx.Play("heal", p);
                    break;
                case "buff":
                    FX.Pillar(p, U.Hex("ffa53d"), 1f);
                    Sfx.Play("buff", p);
                    break;
                case "return":
                    FX.Pillar(p, U.Hex("b98aff"), 1.2f);
                    Sfx.Play("portal", p);
                    Travel("town", "default");
                    break;
                default:
                    Sfx.Play("ui_error");
                    return;
            }
            GameState.Save();
        }

        // ------------------------------------------------------------------ combate / feedback
        public void OnEnemyDied(Enemy e)
        {
            if (e == null || e.def == null) return;
            if (e.def.boss)
            {
                Shake(0.6f);
                if (HUD.I != null) HUD.I.Toast(e.def.name + " foi derrotado!");
                if (e.def.chest > 0) Chest.Spawn(e.transform.position, e.def.chest);
            }
            if (InDungeon)
            {
                int alive = AliveCount();
                if (alive == 0 && HUD.I != null)
                    HUD.I.Toast(level.hasSanctuary ? "Andar limpo! Vá até o santuário." : "Andar limpo!");
                if (HUD.I != null) { HUD.I.SetInstanceInfo(InstanceText()); infoClock = 0.5f; }
            }
        }

        public void OnPlayerDied()
        {
            if (playerDead) return;
            playerDead = true;
            Time.timeScale = 1f;
            Sfx.Play("death");
            Shake(0.5f);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (playerDead && HUD.I != null) HUD.I.DeathWindow();
        }

        /// <summary>Renasce na cidade (perde 10% das moedas, vida e mana cheias).</summary>
        public void Respawn()
        {
            if (traveling) return;
            if (HUD.I != null && HUD.I.HasModal) HUD.I.CloseModal();
            int lost = Mathf.FloorToInt(GameState.P.coins * 0.1f);
            GameState.P.coins -= lost;
            GameState.Recalc();
            GameState.P.hp = GameState.maxHp;
            GameState.P.mp = GameState.maxMp;
            GameState.stamina = GameState.maxStamina;
            GameState.Emit();
            if (player != null) player.Revive();
            playerDead = false;
            if (lost > 0) GameState.Notify("Você perdeu " + lost + " moedas.");
            Travel("town", "default");
        }

        public void Shake(float trauma)
        {
            if (CameraRig.I != null) CameraRig.I.AddTrauma(trauma);
        }

        public void Hitstop(float seconds)
        {
            if (hitstopActive || seconds <= 0f || traveling) return;
            if (Time.timeScale < 0.5f) return; // pausado ou em outro efeito
            StartCoroutine(HitstopRoutine(seconds));
        }

        IEnumerator HitstopRoutine(float seconds)
        {
            hitstopActive = true;
            Time.timeScale = 0.08f;
            yield return new WaitForSecondsRealtime(seconds);
            if (Mathf.Approximately(Time.timeScale, 0.08f)) Time.timeScale = 1f;
            hitstopActive = false;
        }

        // ------------------------------------------------------------------ torre / santuário
        /// <summary>Chamado pelo santuário: cura total, marca o andar como concluído e abre a janela.</summary>
        public void CompleteFloor()
        {
            bool firstClear = !floorDone;   // [Guilda] o santuário pode ser ativado mais de uma vez
            GameState.Recalc();
            GameState.P.hp = GameState.maxHp;
            GameState.P.mp = GameState.maxMp;
            GameState.stamina = GameState.maxStamina;
            if (level != null && level.floor > 0 && !GameState.P.floorsCleared.Contains(level.floor))
                GameState.P.floorsCleared.Add(level.floor);
            floorDone = true;
            if (firstClear && level != null && level.floor > 0) Quests.OnFloorCleared(level.floor);   // [Guilda]
            GameState.Emit();
            GameState.Save();
            if (player != null)
            {
                FX.Pillar(player.transform.position, U.Hex("b8f08a"), 1.4f);
                Sfx.Play("heal", player.transform.position);
            }
            if (HUD.I != null)
            {
                HUD.I.SetInstanceInfo(InstanceText());
                HUD.I.SanctuaryWindow(level != null && !string.IsNullOrEmpty(level.next));
            }
        }

        /// <summary>Vai para o próximo andar (botão da janela do santuário).</summary>
        public void NextFloor()
        {
            if (HUD.I != null && HUD.I.HasModal) HUD.I.CloseModal();
            if (level != null && !string.IsNullOrEmpty(level.next)) Travel(level.next, "default");
            else LeaveTower();
        }

        /// <summary>Volta para a cidade, na porta da torre.</summary>
        public void LeaveTower()
        {
            if (HUD.I != null && HUD.I.HasModal) HUD.I.CloseModal();
            Travel("town", "tower_door");
        }

        /// <summary>Entra num andar da torre (janela da torre). Exige registro na Guilda.</summary>
        public void EnterTower(string floorMap)
        {
            if (!GameState.P.registered)
            {
                Sfx.Play("ui_error");
                if (HUD.I != null) HUD.I.Toast("Registre-se na Guilda antes de entrar na Torre.");
                return;
            }
            if (HUD.I != null && HUD.I.HasModal) HUD.I.CloseModal();
            Travel(string.IsNullOrEmpty(floorMap) ? "tower_f1" : floorMap, "default");
        }
    }
}
