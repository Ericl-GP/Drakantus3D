using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Drakantus
{
    // =====================================================================================
    //  [Inimigos] Eventos aleatórios dos andares da Torre
    // =====================================================================================
    /// <summary>
    /// Sorteia no máximo 1 evento por andar da Torre (chamado pelo Game depois de popular o mapa).
    /// Eventos prontos: "goblin" (Goblin de Ouro que foge, ~12%) e "horda" (Horda surpresa de ossinhos, ~10%).
    /// Para criar outro (baú amaldiçoado, etc.): <c>RandomEvents.Register(new RandomEvents.EventDef { id, chance, canRun, run })</c>.
    /// O runner fica dentro do levelRoot: ao trocar de mapa tudo é cancelado sozinho.
    /// Teste: <c>RandomEvents.Force("goblin")</c> (dispara no andar atual, sem esperar).
    /// </summary>
    public static class RandomEvents
    {
        public class EventDef
        {
            public string id = "";
            public string name = "";
            /// <summary>Chance por andar (0..1).</summary>
            public float chance = 0.1f;
            /// <summary>Pode acontecer neste andar? (null = sempre)</summary>
            public Func<LevelInfo, bool> canRun;
            /// <summary>Corrotina do evento (roda no runner do andar). O bool diz se é forçado (sem espera inicial).</summary>
            public Func<RandomEventsRunner, bool, IEnumerator> run;
        }

        static readonly List<EventDef> registry = new List<EventDef>();
        static bool defaultsAdded;
        static int floorsSinceGoblin;
        /// <summary>Garantia: depois de tantos andares sem goblin, o próximo andar tem goblin.</summary>
        public const int GoblinPity = 8;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { registry.Clear(); defaultsAdded = false; floorsSinceGoblin = 0; }

        static void EnsureDefaults()
        {
            if (defaultsAdded) return;
            defaultsAdded = true;
            registry.Add(new EventDef { id = "goblin", name = "Goblin de Ouro", chance = 0.12f, canRun = l => GameData.Enemy("goblin_ouro") != null, run = GoldenGoblin.EventRoutine });
            registry.Add(new EventDef { id = "horda", name = "Horda surpresa", chance = 0.10f, canRun = l => GameData.Enemy("swarm_ossinho") != null, run = HordeRoutine });
        }

        public static void Register(EventDef d)
        {
            EnsureDefaults();
            if (d == null || string.IsNullOrEmpty(d.id) || d.run == null) return;
            registry.RemoveAll(x => x.id == d.id);
            registry.Add(d);
        }

        public static IReadOnlyList<EventDef> All { get { EnsureDefaults(); return registry; } }

        /// <summary>Chamado pelo Game ao entrar num mapa (só age nos andares da Torre).</summary>
        public static void OnFloorStarted(Game g)
        {
            if (g == null || !g.InDungeon || g.level == null || g.level.floor <= 0 || g.levelRoot == null) return;
            EnsureDefaults();
            floorsSinceGoblin++;
            EventDef pick = null;
            if (floorsSinceGoblin >= GoblinPity) pick = registry.Find(x => x.id == "goblin" && (x.canRun == null || x.canRun(g.level)));
            if (pick == null)
            {
                var order = new List<EventDef>(registry);
                for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var t = order[i]; order[i] = order[j]; order[j] = t; }
                foreach (var d in order)
                {
                    if (d.canRun != null && !d.canRun(g.level)) continue;
                    if (Random.value < d.chance) { pick = d; break; }
                }
            }
            if (pick == null) return;
            Start(g, pick, false);
        }

        /// <summary>Dispara um evento agora no andar atual (teste/admin).</summary>
        public static bool Force(string id)
        {
            EnsureDefaults();
            var g = Game.I;
            if (g == null || !g.InDungeon || g.levelRoot == null) return false;
            var d = registry.Find(x => x.id == id);
            if (d == null) return false;
            Start(g, d, true);
            return true;
        }

        static void Start(Game g, EventDef d, bool forced)
        {
            if (d.id == "goblin") floorsSinceGoblin = 0;
            var runner = RandomEventsRunner.For(g.levelRoot);
            runner.StartCoroutine(d.run(runner, forced));
        }

        // ------------------------------------------------------------------ utilidades para os eventos
        /// <summary>Ponto de chão livre a [rMin, rMax] de center (de preferência à vista).</summary>
        public static bool FindSpot(Vector3 center, float rMin, float rMax, out Vector3 spot, bool needSight = false)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 c = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(rMin, rMax);
                if (!EnemyAbilities.GroundAt(c, center.y, out var g) || EnemyAbilities.Blocked(g)) continue;
                if ((needSight || i < 12) && Projectile.WallBetween(center + Vector3.up, g + Vector3.up)) continue;   // primeiro tenta pontos à vista
                spot = g;
                return true;
            }
            // sem visão: aceita qualquer chão livre
            for (int i = 0; i < 12 && !needSight; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 c = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(rMin, rMax);
                if (EnemyAbilities.GroundAt(c, center.y, out var g) && !EnemyAbilities.Blocked(g)) { spot = g; return true; }
            }
            spot = center;
            return false;
        }

        /// <summary>Espera até o herói estar vivo e lutando (e sem chefe em combate), por no máximo maxWait s.</summary>
        public static IEnumerator WaitForCalm(float maxWait)
        {
            float t = 0f;
            while (t < maxWait)
            {
                var g = Game.I;
                var p = FloorRoot.LivePlayer();
                var boss = g != null ? g.AliveBoss() : null;
                bool bossFight = boss != null && boss.alerted;
                if (p != null && p.canFight && !bossFight && (HUD.I == null || !HUD.I.HasModal)) yield break;
                t += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }
        }

        /// <summary>Recompensa em moedas no chão (n montinhos que pulam do ponto).</summary>
        public static void CoinRain(Vector3 pos, int total, int piles)
        {
            piles = Mathf.Max(1, piles);
            int each = Mathf.Max(1, total / piles);
            for (int i = 0; i < piles; i++) Loot.Spawn("coin", "", each, pos);
        }

        // ------------------------------------------------------------------ evento: horda surpresa
        static IEnumerator HordeRoutine(RandomEventsRunner r, bool forced)
        {
            if (!forced) yield return new WaitForSeconds(Random.Range(15f, 35f));
            yield return WaitForCalm(60f);
            var g = Game.I;
            var p = FloorRoot.LivePlayer();
            if (g == null || p == null || !p.canFight || g.level == null) yield break;
            int floor = Mathf.Max(1, g.level.floor);
            int n = Mathf.Min(6 + floor, 11);
            var spots = new List<Vector3>();
            for (int i = 0; i < n * 2 && spots.Count < n; i++)
                if (FindSpot(p.transform.position, 5.5f, 8.5f, out var s)) spots.Add(s);
            if (spots.Count == 0) yield break;

            if (HUD.I != null) HUD.I.Toast("Horda surpresa! Sobreviva à onda de ossinhos!");
            Sfx.Play("boss_roar", p.transform.position, 0.6f);
            g.Shake(0.3f);
            var marks = new List<GameObject>();
            foreach (var s in spots) marks.Add(FX.Marker(s, 0.9f, new Color(1f, 0.25f, 0.2f, 0.85f), 1.1f));
            yield return new WaitForSeconds(1.1f);
            foreach (var m in marks) if (m != null) UnityEngine.Object.Destroy(m);
            if (Game.I == null || Game.I.levelRoot == null) yield break;

            var horde = new List<Enemy>();
            for (int i = 0; i < spots.Count; i++)
            {
                string id = floor >= 3 && i % 4 == 3 && GameData.Enemy("bomber") != null ? "bomber" : "swarm_ossinho";
                FX.Pillar(spots[i], U.Hex("8a6aff"), 0.6f);
                var e = Game.I.SpawnEnemy(id, spots[i] + Vector3.up * 0.1f, 1f);
                if (e == null) continue;
                e.Alert(false);
                horde.Add(e);
            }
            Sfx.Play("portal", p.transform.position, 0.6f);
            if (horde.Count == 0) yield break;

            float t = 0f;
            while (t < 90f)
            {
                bool alive = false;
                foreach (var e in horde) if (e != null && !e.dead) { alive = true; break; }
                if (!alive) break;
                t += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }
            bool won = true;
            foreach (var e in horde) if (e != null && !e.dead) { won = false; break; }
            if (!won) yield break;

            var pl = FloorRoot.LivePlayer();
            if (pl == null) yield break;
            Vector3 pos = pl.transform.position + pl.transform.forward * 1.5f;
            int total = 40 + 25 * floor;
            CoinRain(pos, total, 8);
            int xp = 15 * floor;
            GameState.AddXp(xp);
            FX.Pillar(pos, U.Hex("ffd04a"), 1.2f);
            Sfx.Play("chest_open", pos);
            HUD.Popup(pl.transform.position + Vector3.up * 3f, "+" + xp + " XP", U.Hex("c9a6ff"), true);
            if (HUD.I != null) HUD.I.Toast("Horda derrotada! Recompensa: moedas e XP.");
        }
    }

    /// <summary>Roda as corrotinas dos eventos do andar (filho do levelRoot: some com o mapa).</summary>
    public class RandomEventsRunner : MonoBehaviour
    {
        public static RandomEventsRunner For(Transform levelRoot)
        {
            var r = levelRoot != null ? levelRoot.GetComponentInChildren<RandomEventsRunner>() : null;
            if (r != null) return r;
            var go = new GameObject("EventosAleatorios");
            if (levelRoot != null) go.transform.SetParent(levelRoot, false);
            return go.AddComponent<RandomEventsRunner>();
        }
    }

    // =====================================================================================
    //  [Inimigos] Goblin de Ouro: foge, fica invisível, some em 25 s; abatido = chuva de moedas + item valioso
    // =====================================================================================
    public class GoldenGoblin : MonoBehaviour
    {
        public const float Lifetime = 25f;
        static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

        Enemy e;
        int floor = 1;
        float timeLeft = Lifetime, invisCd, invisT, dodgeCd, dodgeT, sparkT, coinT, wobble;
        bool invisible, gone;
        int lastToast = -1;
        Vector3 dodgeVel;
        TextMesh timer;
        Light glow;
        static Font font;

        // ------------------------------------------------------------------ evento
        public static IEnumerator EventRoutine(RandomEventsRunner r, bool forced)
        {
            if (!forced) yield return new WaitForSeconds(Random.Range(10f, 28f));
            yield return RandomEvents.WaitForCalm(60f);
            var g = Game.I;
            var p = FloorRoot.LivePlayer();
            if (g == null || p == null || !p.canFight || g.level == null) yield break;
            if (!RandomEvents.FindSpot(p.transform.position, 7f, 11f, out var spot)) yield break;
            var e = g.SpawnEnemy("goblin_ouro", spot + Vector3.up * 0.1f, 1f);
            if (e == null) yield break;
            var gg = e.gameObject.AddComponent<GoldenGoblin>();
            gg.Setup(e, Mathf.Max(1, g.level.floor));
        }

        void Setup(Enemy enemy, int fl)
        {
            e = enemy;
            floor = fl;
            e.customRewards = true;
            e.hold = true;
            e.maxHp = Mathf.Max(1f, e.def.hp) * (1f + 0.3f * (floor - 1));
            e.hp = e.maxHp;
            e.Died += OnKilled;
            invisCd = Random.Range(4f, 6f);
            wobble = Random.value * 10f;
            if (e.visual != null) e.visual.SetTint(Gold);
            float sc = Mathf.Max(0.6f, e.scale);
            LevelDecor.Motes(transform, new Vector3(0f, 0.8f * sc, 0f), new Vector3(1.2f * sc, 1.4f * sc, 1.2f * sc), Gold, 24, 0.2f);
            var lg = new GameObject("brilho_ouro");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 1f, 0f);
            glow = lg.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = Gold;
            glow.range = 4f;
            glow.intensity = 2f;
            glow.shadows = LightShadows.None;
            lg.AddComponent<FxFlicker>().baseIntensity = 2f;
            timer = MakeText(e.Top + 0.95f);

            Vector3 pos = transform.position;
            FX.Pillar(pos, Gold, 1.2f);
            FX.Burst(pos + Vector3.up, Gold, 1f, 30);
            Sfx.Play("item_legendary", pos);
            Sfx.Play("coin", pos);
            if (HUD.I != null) HUD.I.Toast("Um Goblin de Ouro apareceu! Pegue-o antes que fuja!");
        }

        TextMesh MakeText(float y)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var g = new GameObject("contador");
            g.transform.SetParent(transform, false);
            g.transform.localPosition = new Vector3(0f, y, 0f);
            var tm = g.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.06f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Gold;
            var mr = g.GetComponent<MeshRenderer>();
            if (font != null) mr.sharedMaterial = font.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return tm;
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (e == null || e.dead || gone) return;
            float dt = Time.deltaTime;
            timeLeft -= dt;
            int secs = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
            if (timer != null) timer.text = "OURO! " + secs + "s";
            if ((secs == 15 || secs == 5) && secs != lastToast)
            {
                lastToast = secs;
                if (HUD.I != null) HUD.I.Toast("O Goblin de Ouro está fugindo! (" + secs + " s)");
            }
            if (timeLeft <= 0f) { Escape(); return; }
            e.hold = true;

            // moedinhas caindo (rastro dourado; de vez em quando uma moeda de verdade)
            sparkT -= dt;
            if (sparkT <= 0f)
            {
                sparkT = invisible ? 0.12f : 0.22f;
                FX.Sparkle(transform.position + Vector3.up * 0.4f, Gold, invisible ? 0.25f : 0.35f, 0.1f);
            }
            coinT += dt;
            if (coinT >= 3f)
            {
                coinT = 0f;
                Loot.Spawn("coin", "", 1 + floor, transform.position);
                Sfx.Play("coin", transform.position, 0.4f);
            }

            // invisibilidade curta
            if (invisible)
            {
                invisT -= dt;
                if (invisT <= 0f) SetVisible(true);
            }
            else
            {
                invisCd -= dt;
                if (invisCd <= 0f)
                {
                    invisT = Random.Range(1.1f, 1.6f);
                    SetVisible(false);
                }
            }

            var p = FloorRoot.LivePlayer();
            if (p == null || e.Stunned) { e.Drive(Vector3.zero, transform.forward); return; }
            Vector3 away = U.Flat(transform.position - p.transform.position);
            float dist = away.magnitude;
            away = dist > 0.01f ? away / dist : -transform.forward;
            dodgeCd -= dt;

            // pulo lateral quando o herói chega perto
            if (dodgeT > 0f)
            {
                dodgeT -= dt;
                e.Drive(dodgeVel, dodgeVel);
                return;
            }
            if (dist < 3f && dodgeCd <= 0f)
            {
                Vector3 side = Vector3.Cross(Vector3.up, away) * (Random.value < 0.5f ? -1f : 1f);
                Vector3 d = EnemyArchetype.SteerFrom(transform.position, (side + away * 0.6f).normalized, 2.2f);
                if (d.sqrMagnitude < 0.01f) d = EnemyArchetype.SteerFrom(transform.position, (-side + away * 0.6f).normalized, 2.2f);
                if (d.sqrMagnitude > 0.01f)
                {
                    dodgeVel = d * 11f;
                    dodgeT = 0.22f;
                    dodgeCd = Random.Range(2f, 3f);
                    FX.Dust(transform.position, U.Hex("e0c070"), 0.7f);
                    Sfx.Play("dash", transform.position, 0.6f);
                    return;
                }
            }

            float speed = e.def.speed;
            Vector3 v;
            if (dist < 13f)
            {
                // foge em zigue-zague, desviando das paredes
                Vector3 want = (away + Vector3.Cross(Vector3.up, away) * Mathf.Sin(Time.time * 3f + wobble) * 0.5f).normalized;
                v = EnemyArchetype.SteerFrom(transform.position, want, 1.6f) * speed;
                if (v.sqrMagnitude < 0.01f && dodgeCd <= 0f) { Blink(p); return; }   // encurralado: some e aparece longe
            }
            else
            {
                // longe: dança e provoca
                v = Vector3.zero;
                if (Random.value < 0.01f) e.Act("Taunt", 0.8f);
            }
            e.Drive(v, v.sqrMagnitude > 0.01f ? v : -away);
        }

        void LateUpdate()
        {
            if (timer != null && CameraRig.Cam != null) timer.transform.rotation = CameraRig.Cam.transform.rotation;
        }

        void SetVisible(bool on)
        {
            invisible = !on;
            if (!on) invisT = Mathf.Max(invisT, 0.8f);
            else invisCd = Random.Range(4.5f, 7.5f);
            if (e != null && e.visual != null)
                foreach (var r in e.visual.GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = on;
            if (glow != null) glow.enabled = on;
            Vector3 pos = transform.position + Vector3.up;
            FX.Burst(pos, Gold, 0.7f, 16);
            Sfx.Play("portal", transform.position, 0.4f);
            if (!on) HUD.Popup(pos + Vector3.up * 1.2f, "sumiu!", Gold);
        }

        void Blink(Player p)
        {
            if (!RandomEvents.FindSpot(p.transform.position, 8f, 12f, out var s)) { dodgeCd = 1f; return; }
            FX.Burst(transform.position + Vector3.up, Gold, 0.9f, 20);
            e.TeleportTo(s);
            FX.Burst(s + Vector3.up, Gold, 0.9f, 20);
            Sfx.Play("portal", s, 0.6f);
            dodgeCd = 3f;
        }

        void Escape()
        {
            if (gone) return;
            gone = true;
            Vector3 pos = transform.position;
            FX.Pillar(pos, Gold, 1.3f);
            FX.Burst(pos + Vector3.up, Gold, 1f, 30);
            Sfx.Play("portal", pos);
            if (HUD.I != null) HUD.I.Toast("O Goblin de Ouro escapou com o tesouro...");
            if (e != null) e.Died -= OnKilled;
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ recompensa
        void OnKilled(Enemy dead)
        {
            if (invisible) SetVisible(true);
            if (timer != null) timer.gameObject.SetActive(false);
            Vector3 pos = transform.position;
            int total = 200 + 100 * floor;
            RandomEvents.CoinRain(pos, total, 20);
            string item = ValuableItem();
            if (item != null) Loot.Spawn("item", item, 1, pos);
            int xp = dead.def != null ? dead.def.xp * floor : 40;
            GameState.AddXp(xp);
            HUD.Popup(pos + Vector3.up * 2.4f, "+" + total + " ◈", Gold, true);
            HUD.Popup(pos + Vector3.up * 3f, "+" + xp + " XP", U.Hex("c9a6ff"), false);
            FX.Pillar(pos, Gold, 2f);
            FX.Burst(pos + Vector3.up, Gold, 1.6f, 60);
            FX.Sparkle(pos + Vector3.up * 0.5f, Gold, 1.5f, 1.2f);
            FX.FlashLight(pos + Vector3.up * 1.5f, Gold, 6f, 9f, 0.6f);
            Sfx.Play("item_legendary", pos);
            Sfx.Play("coin", pos);
            Sfx.Play("chest_open", pos, 0.7f);
            if (Game.I != null) Game.I.Shake(0.35f);
            if (HUD.I != null) HUD.I.Toast("Goblin de Ouro derrotado! Chuva de moedas!");
        }

        /// <summary>Item valioso para revender: lendário (35%) ou raro, de preço alto.</summary>
        static string ValuableItem()
        {
            string want = Random.value < 0.35f ? "lendario" : "raro";
            var l = new List<ItemDef>();
            foreach (var d in GameData.ItemOrder)
                if (d != null && GameData.IsEquipSlotType(d.slot) && d.slot != "asa" && d.rarity == want) l.Add(d);
            if (l.Count == 0)
                foreach (var d in GameData.ItemOrder)
                    if (d != null && GameData.IsEquipSlotType(d.slot) && d.slot != "asa" && d.rarity != "comum") l.Add(d);
            if (l.Count == 0) return null;
            // prefere os mais caros (metade de cima da lista)
            l.Sort((a, b) => b.price.CompareTo(a.price));
            int top = Mathf.Max(1, (l.Count + 1) / 2);
            return l[Random.Range(0, top)].id;
        }
    }
}
