using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Habilidades extras de elites, mini-chefes e chefes. Fica AO LADO do Enemy (não altera Enemy.cs).
    /// Ids: investida, escudo, invocar, veneno, teleporte, furia, fogo, pedras.
    /// Chefes com fases: a cada 33% de vida perdida ganham a próxima habilidade de phaseAbilities,
    /// rugem e ficam mais agressivos.
    /// O FloorRoot adiciona este componente depois que o Game cria os inimigos do andar.
    /// </summary>
    public class EnemyAbilities : MonoBehaviour
    {
        public static readonly string[] AllIds = { "investida", "escudo", "invocar", "veneno", "teleporte", "furia", "fogo", "pedras" };

        public static string Nome(string id)
        {
            switch (id)
            {
                case "investida": return "Investida";
                case "escudo": return "Escudo";
                case "invocar": return "Invocar lacaios";
                case "veneno": return "Poça de veneno";
                case "teleporte": return "Teleporte";
                case "furia": return "Fúria";
                case "fogo": return "Área de fogo";
                case "pedras": return "Lançar pedras";
            }
            return id;
        }

        public List<string> abilities = new List<string>();
        public List<string> phaseAbilities = new List<string>();
        public bool phases;
        public bool elite;
        public float globalCooldown = 4.5f;
        public string summonId = "minion";

        Enemy e;
        readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();
        float nextCast;
        int phase;
        bool busy, enraged;
        float shieldUntil, shieldHp;
        GameObject bubble;
        ParticleSystem auraFx;
        Light auraLight;
        readonly List<Enemy> summons = new List<Enemy>();
        BossPatterns patterns;   // [Inimigos] chefes com padrões próprios: as fases ficam com o BossPatterns

        /// <summary>Executando uma habilidade agora?</summary>
        public bool Busy => busy;

        // ------------------------------------------------------------------ criação
        /// <summary>Adiciona (ou completa) as habilidades de um inimigo já criado.</summary>
        public static EnemyAbilities Attach(Enemy enemy, IList<string> abil, IList<string> phaseAbil, bool elite, bool phases)
        {
            if (enemy == null) return null;
            var a = enemy.GetComponent<EnemyAbilities>();
            if (a == null) a = enemy.gameObject.AddComponent<EnemyAbilities>();
            a.e = enemy;
            if (abil != null) foreach (var s in abil) if (!string.IsNullOrEmpty(s) && !a.abilities.Contains(s)) a.abilities.Add(s);
            if (phaseAbil != null) foreach (var s in phaseAbil) if (!string.IsNullOrEmpty(s) && !a.phaseAbilities.Contains(s)) a.phaseAbilities.Add(s);
            a.phases = phases;
            a.elite = elite;
            bool boss = enemy.def != null && enemy.def.boss;
            a.globalCooldown = boss ? 3.6f : elite ? 5.5f : 4.5f;
            a.nextCast = Time.time + Random.Range(1.5f, 3f);
            if (elite)
            {
                enemy.maxHp *= 1.8f;
                enemy.hp = enemy.maxHp;
                if (enemy.visual != null) enemy.visual.SetTint(new Color(1f, 0.85f, 0.55f));
                a.SetAura(new Color(1f, 0.75f, 0.25f));
            }
            return a;
        }

        /// <summary>Dificuldade: multiplica vida, dano e XP (clona o EnemyDef para não afetar os outros).</summary>
        public static void Scale(Enemy enemy, float hpMult, float dmgMult, float xpMult)
        {
            if (enemy == null || enemy.def == null) return;
            if (Mathf.Approximately(hpMult, 1f) && Mathf.Approximately(dmgMult, 1f) && Mathf.Approximately(xpMult, 1f)) return;
            OwnDef(enemy);
            enemy.def.dmg *= dmgMult;
            enemy.def.xp = Mathf.Max(1, Mathf.RoundToInt(enemy.def.xp * xpMult));
            enemy.maxHp *= hpMult;
            enemy.hp = enemy.maxHp;
        }

        static readonly HashSet<EnemyDef> owned = new HashSet<EnemyDef>();

        /// <summary>Garante que o inimigo tem uma cópia própria do EnemyDef.</summary>
        static void OwnDef(Enemy enemy)
        {
            if (owned.Contains(enemy.def)) return;
            var c = JsonUtility.FromJson<EnemyDef>(JsonUtility.ToJson(enemy.def));
            if (c.coins == null || c.coins.Length < 2) c.coins = new[] { 1, 3 };
            enemy.def = c;
            owned.Add(c);
        }

        /// <summary>Aura colorida (partículas + luz fraca).</summary>
        public void SetAura(Color c)
        {
            if (auraFx != null) Destroy(auraFx.gameObject);
            float sc = e != null ? Mathf.Max(1f, e.scale) : 1f;
            auraFx = LevelDecor.Motes(transform, new Vector3(0, 0.9f * sc, 0), new Vector3(1.4f * sc, 1.6f * sc, 1.4f * sc), c, 18, 0.35f);
            if (auraLight == null)
            {
                var lg = new GameObject("aura_luz");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = new Vector3(0, 1.2f * sc, 0);
                auraLight = lg.AddComponent<Light>();
                auraLight.type = LightType.Point;
                auraLight.shadows = LightShadows.None;
                auraLight.range = 3.5f * sc;
                auraLight.intensity = 1.2f;
            }
            auraLight.color = c;
        }

        // ------------------------------------------------------------------ loop
        Player P => FloorRoot.LivePlayer();
        bool Has(string id) => abilities.Contains(id);

        void Update()
        {
            if (e == null) { e = GetComponent<Enemy>(); if (e == null) return; }
            if (e.dead) { Cleanup(); enabled = false; return; }

            // escudo: desfaz o dano recebido enquanto a bolha existe
            if (shieldUntil > 0f)
            {
                if (Time.time < shieldUntil)
                {
                    if (e.hp < shieldHp)
                    {
                        e.hp = shieldHp;
                        HUD.Popup(transform.position + Vector3.up * 2.6f * Mathf.Max(1f, e.scale), "IMUNE", U.Hex("9fd0ff"));
                    }
                }
                else EndShield();
            }

            // [Inimigos] chefe com BossPatterns: ele cuida das fases; habilidades extras só fora dos padrões
            if (patterns == null) patterns = GetComponent<BossPatterns>();
            if (patterns != null)
            {
                phases = false;
                if (patterns.Busy) return;
            }

            if (phases) CheckPhase();
            if (!enraged && Has("furia") && e.hp <= e.maxHp * 0.3f) Enrage();

            if (busy || !e.alerted || e.state == "stunned" || e.state == "dead") return;
            if (Time.time < nextCast) return;
            var p = P;
            if (p == null) return;
            float dist = U.Flat(p.transform.position - transform.position).magnitude;

            var options = new List<string>();
            foreach (var id in abilities)
            {
                if (id == "furia") continue;
                if (readyAt.TryGetValue(id, out var t) && Time.time < t) continue;
                if (!Usable(id, dist)) continue;
                options.Add(id);
            }
            if (options.Count == 0) { nextCast = Time.time + 0.5f; return; }
            string pick = options[Random.Range(0, options.Count)];
            readyAt[pick] = Time.time + Cooldown(pick);
            nextCast = Time.time + globalCooldown * Random.Range(0.8f, 1.3f);
            StartCoroutine(Run(pick, p));
        }

        bool Usable(string id, float dist)
        {
            switch (id)
            {
                case "investida": return dist > 2.8f && dist < 10f && e.cc != null && e.cc.enabled;
                case "escudo": return e.hp < e.maxHp * 0.85f;
                case "invocar": { summons.RemoveAll(s => s == null || s.dead); return summons.Count < 4; }
                case "veneno": return dist < 12f;
                case "teleporte": return dist > 7f || dist < 2f;
                case "fogo": return dist < 10f;
                case "pedras": return dist < 14f;
            }
            return false;
        }

        float Cooldown(string id)
        {
            switch (id)
            {
                case "investida": return 7f;
                case "escudo": return 12f;
                case "invocar": return 16f;
                case "veneno": return 9f;
                case "teleporte": return 8f;
                case "fogo": return 10f;
                case "pedras": return 9f;
            }
            return 8f;
        }

        IEnumerator Run(string id, Player p)
        {
            busy = true;
            switch (id)
            {
                case "investida": yield return Charge(p); break;
                case "escudo": Shield(); break;
                case "invocar": yield return Summon(); break;
                case "veneno": yield return Poison(p); break;
                case "teleporte": Teleport(p); break;
                case "fogo": yield return FireArea(p); break;
                case "pedras": yield return Rocks(p); break;
            }
            busy = false;
        }

        float Dmg(float mult) => Mathf.Max(1f, (e.def != null ? e.def.dmg : 8f) * mult);

        bool Alive => e != null && !e.dead;

        // ------------------------------------------------------------------ investida
        IEnumerator Charge(Player p)
        {
            Vector3 start = transform.position;
            Vector3 dir = U.Flat(p.transform.position - start);
            float dist = Mathf.Clamp(dir.magnitude + 1.5f, 3f, 9f);
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var lane = FX.FlatQuad("investida", start + dir * dist * 0.5f + Vector3.up * 0.06f, U.WhiteTexture(), new Color(1f, 0.2f, 0.15f, 0.35f), false);
            lane.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            lane.transform.localScale = new Vector3(1.4f * Mathf.Max(1f, e.scale * 0.8f), dist, 1f);
            if (e.visual != null) e.visual.Play("Taunt", 0.1f, 0.7f, true);
            Sfx.Play("enemy_alert", transform.position, 0.8f);
            float wind = e.def != null && e.def.boss ? 0.6f : 0.75f;
            yield return new WaitForSeconds(wind);
            if (lane != null) Destroy(lane);
            if (!Alive || e.cc == null || !e.cc.enabled) yield break;

            Sfx.Play("dash", transform.position);
            float t = 0f, dur = 0.32f;
            bool hit = false;
            float speed = dist / dur;
            while (t < dur && Alive && e.cc != null && e.cc.enabled)
            {
                float dt = Time.deltaTime;
                t += dt;
                e.cc.Move(dir * speed * dt + Vector3.down * 2f * dt);
                if (Random.value < 0.4f) FX.Dust(transform.position, U.Hex("a09080"), 0.5f);
                var pp = P;
                if (!hit && pp != null && U.Flat(pp.transform.position - transform.position).magnitude < 1.3f + e.radius)
                {
                    hit = true;
                    FloorRoot.HurtPlayer(Dmg(1.3f), transform.position);
                    if (Game.I != null) Game.I.Shake(0.25f);
                }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ escudo
        void Shield()
        {
            shieldHp = e.hp;
            shieldUntil = Time.time + 2.2f;
            Sfx.Play("shield", transform.position);
            if (bubble == null)
            {
                bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bubble.name = "escudo";
                Destroy(bubble.GetComponent<Collider>());
                bubble.transform.SetParent(transform, false);
                float sc = Mathf.Max(1f, e.scale);
                bubble.transform.localPosition = new Vector3(0, 1f * sc, 0);
                bubble.transform.localScale = Vector3.one * 2.6f * sc;
                var r = bubble.GetComponent<Renderer>();
                r.sharedMaterial = U.Fx(true);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                FX.SetColor(r, new Color(0.45f, 0.75f, 1f, 0.35f));
            }
            bubble.SetActive(true);
            FX.Ring(transform.position, 1.8f * Mathf.Max(1f, e.scale), U.Hex("9fd0ff"), 0.4f);
        }

        void EndShield()
        {
            shieldUntil = 0f;
            if (bubble != null) bubble.SetActive(false);
        }

        // ------------------------------------------------------------------ invocar
        IEnumerator Summon()
        {
            if (e.visual != null) e.visual.Play("Summon", 0.1f, 1f, true);
            Sfx.Play("magic_cast", transform.position);
            FX.Ring(transform.position, 3f, U.Hex("b98aff"), 0.8f);
            yield return new WaitForSeconds(0.8f);
            if (!Alive || Game.I == null) yield break;
            int n = Random.Range(2, 4);
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + Random.Range(0f, 0.6f);
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.4f;
                FX.Pillar(pos, U.Hex("b98aff"), 0.7f);
                var s = Game.I.SpawnEnemy(summonId, pos + Vector3.up * 0.2f, 0.9f);
                if (s != null) { summons.Add(s); s.Alert(false); }
            }
            Sfx.Play("portal", transform.position, 0.7f);
        }

        // ------------------------------------------------------------------ veneno
        IEnumerator Poison(Player p)
        {
            Vector3 at = p.transform.position;
            if (e.visual != null) e.visual.Play("Cast", 0.1f, 0.8f, true);
            var mk = FX.Marker(at, 2.2f, new Color(0.4f, 1f, 0.3f, 0.8f), 0.8f);
            Sfx.Play("magic_cast", transform.position, 0.8f);
            yield return new WaitForSeconds(0.8f);
            if (mk != null) Destroy(mk);
            if (!Alive) yield break;
            FX.Burst(at + Vector3.up * 0.3f, U.Hex("7aff5a"), 1f, 20);
            PoisonFog.SpawnPuddle(at, 2.2f, Dmg(0.45f), 6f);
        }

        // ------------------------------------------------------------------ teleporte
        void Teleport(Player p)
        {
            Vector3 center = p.transform.position;
            for (int tries = 0; tries < 10; tries++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(3f, 5f);
                Vector3 c = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!GroundAt(c, center.y, out var g)) continue;
                if (Blocked(g)) continue;
                FX.Burst(transform.position + Vector3.up, U.Hex("b98aff"), 1f, 24);
                Sfx.Play("portal", transform.position, 0.8f);
                if (e.cc != null) e.cc.enabled = false;
                transform.position = g;
                if (e.cc != null) e.cc.enabled = true;
                FX.Burst(g + Vector3.up, U.Hex("b98aff"), 1f, 24);
                FX.Ring(g, 1.6f, U.Hex("b98aff"), 0.35f);
                return;
            }
        }

        /// <summary>Chão perto de c (até 1,5 m de diferença de altura de refY). [Inimigos] público para arquétipos/eventos.</summary>
        public static bool GroundAt(Vector3 c, float refY, out Vector3 point)
        {
            point = c;
            var hits = Physics.RaycastAll(new Vector3(c.x, refY + 3f, c.z), Vector3.down, 7f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider == null || Projectile.IsActor(h.collider)) continue;
                if (h.distance < best) { best = h.distance; point = h.point; found = true; }
            }
            return found && Mathf.Abs(point.y - refY) < 1.5f;
        }

        /// <summary>Há parede/objeto ocupando o ponto g? [Inimigos] público para arquétipos/eventos.</summary>
        public static bool Blocked(Vector3 g)
        {
            var cols = Physics.OverlapCapsule(g + Vector3.up * 0.6f, g + Vector3.up * 1.6f, 0.45f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in cols) if (c != null && !Projectile.IsActor(c)) return true;
            return false;
        }

        // ------------------------------------------------------------------ fúria
        void Enrage()
        {
            enraged = true;
            OwnDef(e);
            e.def.speed *= 1.45f;
            e.def.dmg *= 1.25f;
            e.def.windup = Mathf.Max(0.2f, e.def.windup * 0.75f);
            globalCooldown *= 0.75f;
            if (e.visual != null) e.visual.SetTint(new Color(1f, 0.55f, 0.5f));
            SetAura(new Color(1f, 0.25f, 0.15f));
            Sfx.Play("boss_roar", transform.position, 0.8f);
            HUD.Popup(transform.position + Vector3.up * 3f * Mathf.Max(1f, e.scale), "FÚRIA!", U.Hex("ff5a3a"), true);
            FX.Ring(transform.position, 3f, U.Hex("ff5a3a"), 0.5f);
            if (Game.I != null) Game.I.Shake(0.2f);
        }

        // ------------------------------------------------------------------ área de fogo
        IEnumerator FireArea(Player p)
        {
            bool boss = e.def != null && e.def.boss;
            int n = boss ? 5 : 3;
            float rad = 1.9f;
            var centers = new List<Vector3> { p.transform.position };
            for (int i = 1; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle * 4.5f;
                centers.Add(p.transform.position + new Vector3(r.x, 0f, r.y));
            }
            var marks = new List<GameObject>();
            foreach (var c in centers) marks.Add(FX.Marker(c, rad, new Color(1f, 0.45f, 0.1f, 0.85f), 1.1f));
            if (e.visual != null) e.visual.Play("Cast", 0.1f, 1f, true);
            Sfx.Play("magic_cast", transform.position);
            yield return new WaitForSeconds(1.1f);
            foreach (var m in marks) if (m != null) Destroy(m);
            if (!Alive) yield break;
            bool hit = false;
            foreach (var c in centers)
            {
                FX.Burst(c + Vector3.up * 0.4f, U.Hex("ff8a2a"), 1.3f, 26);
                FX.Ring(c, rad, U.Hex("ffb347"), 0.4f);
                FX.Flash(c + Vector3.up, U.Hex("ff8a2a"), 3f, 0.3f);
                var pp = P;
                if (!hit && pp != null && U.Flat(pp.transform.position - c).magnitude <= rad + 0.3f)
                {
                    hit = true;
                    FloorRoot.HurtPlayer(Dmg(1.1f), c);
                }
            }
            Sfx.Play("explosion", centers[0], 0.8f);
            if (Game.I != null) Game.I.Shake(0.2f);
        }

        // ------------------------------------------------------------------ pedras
        IEnumerator Rocks(Player p)
        {
            int n = 3;
            float rad = 1.5f, fall = 1.1f;
            var centers = new List<Vector3> { p.transform.position };
            for (int i = 1; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle * 3.5f;
                centers.Add(p.transform.position + new Vector3(r.x, 0f, r.y));
            }
            if (e.visual != null) e.visual.Play("Attack2", 0.1f, 0.6f, true);
            Sfx.Play("swing_heavy", transform.position);
            var marks = new List<GameObject>();
            var rocks = new List<GameObject>();
            foreach (var c in centers)
            {
                marks.Add(FX.Marker(c, rad, new Color(0.9f, 0.6f, 0.3f, 0.85f), fall));
                var rock = U.Prim(PrimitiveType.Cube, FX.Root, c + Vector3.up * 9f, Vector3.one * Random.Range(0.7f, 1.1f), U.Hex("7a7068"));
                rock.transform.rotation = Random.rotation;
                rocks.Add(rock);
            }
            float t = 0f;
            while (t < fall)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fall);
                for (int i = 0; i < rocks.Count; i++)
                    if (rocks[i] != null)
                    {
                        rocks[i].transform.position = centers[i] + Vector3.up * Mathf.Lerp(9f, 0.4f, k * k);
                        rocks[i].transform.Rotate(200f * Time.deltaTime, 90f * Time.deltaTime, 0f);
                    }
                yield return null;
            }
            foreach (var m in marks) if (m != null) Destroy(m);
            bool hit = false;
            for (int i = 0; i < centers.Count; i++)
            {
                var c = centers[i];
                FX.Dust(c, U.Hex("8a7a6a"), 1.4f);
                FX.Burst(c + Vector3.up * 0.3f, U.Hex("b0a090"), 0.9f, 14);
                if (rocks[i] != null) Destroy(rocks[i], 0.15f);
                var pp = P;
                if (!hit && Alive && pp != null && U.Flat(pp.transform.position - c).magnitude <= rad + 0.3f)
                {
                    hit = true;
                    FloorRoot.HurtPlayer(Dmg(1.2f), c);
                }
            }
            Sfx.Play("explosion", centers[0], 0.6f);
            if (Game.I != null) Game.I.Shake(0.25f);
        }

        // ------------------------------------------------------------------ fases
        void CheckPhase()
        {
            float k = e.maxHp > 0f ? e.hp / e.maxHp : 1f;
            float threshold = phase == 0 ? 0.66f : phase == 1 ? 0.33f : -1f;
            if (k > threshold) return;
            phase++;
            if (phase - 1 < phaseAbilities.Count)
            {
                string add = phaseAbilities[phase - 1];
                if (!abilities.Contains(add)) abilities.Add(add);
                readyAt[add] = 0f;
            }
            globalCooldown = Mathf.Max(1.8f, globalCooldown * 0.85f);
            nextCast = Time.time + 0.8f;
            Vector3 pos = transform.position;
            Sfx.Play("boss_roar", pos);
            FX.Ring(pos, 5f, U.Hex("ff4a3a"), 0.6f);
            FX.Pillar(pos, U.Hex("ff7a4a"), 1.4f);
            if (Game.I != null) Game.I.Shake(0.45f);
            if (e.visual != null) e.visual.Play("Taunt", 0.1f, 1f, true);
            string nm = e.def != null ? e.def.name : "O chefe";
            HUD.Popup(pos + Vector3.up * 3.4f * Mathf.Max(1f, e.scale), "FASE " + (phase + 1), U.Hex("ff7a4a"), true);
            if (HUD.I != null) HUD.I.Toast(nm + " fica mais forte! (fase " + (phase + 1) + ")");
        }

        void Cleanup()
        {
            EndShield();
            if (auraFx != null) { var em = auraFx.emission; em.enabled = false; }
            if (auraLight != null) auraLight.enabled = false;
        }
    }

    // =====================================================================================
    //  [Inimigos] Arquétipos (EnemyDef.ai) e elementos (EnemyDef.element)
    // =====================================================================================
    /// <summary>
    /// Comportamento próprio por arquétipo. Adicionado pelo Enemy.Init quando def.ai/def.element pedem.
    ///  - bomber: corre até o herói, pisca vermelho (marca no chão) e explode; morto antes, explode só nos aliados.
    ///  - shield: escudo frontal (bloqueia 85% do dano pela frente), +30% pelas costas, vulnerável ao atacar; gira devagar.
    ///  - summoner: fica longe e invoca lacaios (def.summon) a cada def.cooldown s.
    ///  - charger: prepara (faixa vermelha) e dá investida em linha; se bater na parede fica atordoado.
    ///  - healer: fica atrás dos aliados e cura o mais ferido com raio verde (acertá-lo interrompe); "+" verde na cabeça.
    ///  - sniper: mira 1,2 s com laser vermelho e dispara um projétil rápido de longe (acertá-lo interrompe).
    ///  - swarm: só zigue-zague (na IA base do Enemy).
    ///  - element fire: deixa o chão em chamas ao morrer · ice: golpe gelado (+25%) · poison: poça de veneno ao morrer.
    /// A IA base do Enemy continua cuidando de física, atordoamento, animação de andar e barra de vida.
    /// </summary>
    public class EnemyArchetype : MonoBehaviour
    {
        public static void Attach(Enemy e)
        {
            if (e == null || e.def == null) return;
            string ai = e.Ai;
            string el = e.def.element ?? "";
            bool special = ai == "bomber" || ai == "shield" || ai == "summoner" || ai == "charger" || ai == "healer" || ai == "sniper";
            if (!special && el != "fire" && el != "ice" && el != "poison") return;
            var a = e.GetComponent<EnemyArchetype>();
            if (a == null) a = e.gameObject.AddComponent<EnemyArchetype>();
            a.e = e;
            a.Setup();
        }

        Enemy e;
        string ai = "melee";
        float cd, strafeT, strafeSign = 1f, shieldMsgT, vulnerableUntil;
        bool busy, selfExploded, setup;
        TextMesh icon;
        LineRenderer beam;
        readonly List<Enemy> minions = new List<Enemy>();

        static Font font;
        static readonly Color Red = new Color(1f, 0.22f, 0.15f, 0.9f);

        bool FullDrive => ai == "bomber" || ai == "summoner" || ai == "healer" || ai == "sniper";
        bool Alive => e != null && !e.dead;
        float Cooldown(float fallback) => e.def != null && e.def.cooldown > 0f ? e.def.cooldown : fallback;
        float Dmg(float mult) => Mathf.Max(1f, (e.def != null ? e.def.dmg : 6f) * mult);

        static Player P
        {
            get
            {
                var p = FloorRoot.LivePlayer();
                return p != null && p.canFight ? p : null;
            }
        }

        void Setup()
        {
            if (setup) return;
            setup = true;
            ai = e.Ai;
            cd = Random.Range(1.5f, 3f);
            e.Died += OnDied;
            string el = e.def.element ?? "";
            float sc = Mathf.Max(0.6f, e.scale);
            if (el == "fire") LevelDecor.Motes(transform, new Vector3(0f, 1f * sc, 0f), new Vector3(1f * sc, 1.4f * sc, 1f * sc), U.Hex("ff7a2a"), 16, 0.4f);
            else if (el == "ice") LevelDecor.Motes(transform, new Vector3(0f, 1f * sc, 0f), new Vector3(1f * sc, 1.4f * sc, 1f * sc), U.Hex("9fe4ff"), 14, -0.1f);
            else if (el == "poison") LevelDecor.Motes(transform, new Vector3(0f, 0.8f * sc, 0f), new Vector3(1f * sc, 1.2f * sc, 1f * sc), U.Hex("7aff5a"), 12, 0.1f);

            switch (ai)
            {
                case "shield":
                    e.hitFilter = ShieldFilter;
                    e.turnRate = 3.2f;
                    break;
                case "healer":
                    icon = MakeIcon("+", U.Hex("7aff8a"));
                    break;
                case "bomber":
                    LevelDecor.Flame(transform, new Vector3(0f, e.Top + 0.15f, 0f), 0.6f, U.Hex("ff8a2a"), 10);   // pavio aceso
                    break;
            }
        }

        TextMesh MakeIcon(string txt, Color c)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var g = new GameObject("icone");
            g.transform.SetParent(transform, false);
            g.transform.localPosition = new Vector3(0f, e.Top + 0.95f, 0f);
            var tm = g.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = txt;
            tm.fontSize = 96;
            tm.characterSize = 0.07f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            var mr = g.GetComponent<MeshRenderer>();
            if (font != null) mr.sharedMaterial = font.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return tm;
        }

        void LateUpdate()
        {
            if (icon != null && CameraRig.Cam != null)
            {
                if (icon.gameObject.activeSelf == (e == null || e.dead)) icon.gameObject.SetActive(e != null && !e.dead);
                icon.transform.rotation = CameraRig.Cam.transform.rotation;
            }
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (!Alive) { if (beam != null) beam.enabled = false; return; }
            float dt = Time.deltaTime;
            cd -= dt;
            var p = P;

            if (!FullDrive)
            {
                if (ai == "charger" && !busy && cd <= 0f && p != null && e.alerted && e.state == "chase" && !e.Stunned)
                {
                    float d = U.Flat(p.transform.position - transform.position).magnitude;
                    if (d > 3.5f && d < 11f && !Projectile.WallBetween(transform.position + Vector3.up, p.transform.position + Vector3.up))
                        StartCoroutine(Run(ChargeRoutine()));
                }
                return;
            }

            // arquétipos que dirigem o movimento sozinhos (a IA base só cuida da física)
            if (!e.alerted || p == null)
            {
                if (e.hold && !busy) e.hold = false;
                return;
            }
            if (!e.hold) { e.hold = true; e.CancelAttack(); }
            if (busy || e.Stunned) return;

            Vector3 to = U.Flat(p.transform.position - transform.position);
            float dist = to.magnitude;
            Vector3 dir = dist > 0.001f ? to / dist : transform.forward;
            if (dist > 24f && p.tauntTime <= 0f)
            {
                // perdeu o herói de vista: volta para a IA base (parado/patrulha)
                e.alerted = false;
                e.hold = false;
                return;
            }
            switch (ai)
            {
                case "bomber":
                    if (dist < 1.9f + e.radius) { StartCoroutine(Run(BomberFuse())); return; }
                    e.Drive(Steer(dir) * e.def.speed, dir);
                    break;
                case "summoner":
                    Kite(dir, dist, 6.5f, 10f, 0.8f);
                    minions.RemoveAll(m => m == null || m.dead);
                    if (cd <= 0f && minions.Count < 4) StartCoroutine(Run(SummonRoutine()));
                    break;
                case "healer":
                    HealerMove(p, dir, dist);
                    break;
                case "sniper":
                {
                    float reach = Mathf.Max(9f, e.def.reach);
                    Kite(dir, dist, Mathf.Min(8.5f, reach - 3f), reach - 1f, 0.6f);
                    if (cd <= 0f && dist <= reach && !Projectile.WallBetween(transform.position + Vector3.up * 1.2f, p.transform.position + Vector3.up))
                        StartCoroutine(Run(SniperAim()));
                    break;
                }
            }
        }

        IEnumerator Run(IEnumerator body)
        {
            busy = true;
            yield return body;
            busy = false;
            if (e != null && !FullDrive) e.hold = false;
        }

        // ------------------------------------------------------------------ movimento
        /// <summary>Mantém distância [near, far] do herói, andando de lado no meio.</summary>
        void Kite(Vector3 dirP, float dist, float near, float far, float strafeMul)
        {
            float sp = e.def.speed;
            Vector3 v;
            if (dist > far) v = Steer(dirP) * sp;
            else if (dist < near) v = Steer(-dirP) * sp * 1.05f;
            else
            {
                strafeT -= Time.deltaTime;
                if (strafeT <= 0f) { strafeT = Random.Range(1.2f, 2.4f); strafeSign = Random.value < 0.5f ? -1f : 1f; }
                v = Steer(Vector3.Cross(Vector3.up, dirP) * strafeSign) * sp * strafeMul;
                if (v.sqrMagnitude < 0.01f) strafeSign = -strafeSign;
            }
            e.Drive(v, dirP);
        }

        /// <summary>Desvia de paredes: tenta a direção desejada e ângulos cada vez mais abertos.</summary>
        Vector3 Steer(Vector3 desired)
        {
            desired = U.Flat(desired);
            if (desired.sqrMagnitude < 0.0001f) return Vector3.zero;
            desired.Normalize();
            return SteerFrom(transform.position, desired, 1.4f + e.radius);
        }

        public static Vector3 SteerFrom(Vector3 pos, Vector3 desired, float probe)
        {
            Vector3 o = pos + Vector3.up * 0.7f;
            float[] angles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };
            foreach (float a in angles)
            {
                Vector3 d = Quaternion.Euler(0f, a, 0f) * desired;
                if (!Projectile.WallHit(o, d, probe, out _)) return d;
            }
            return Vector3.zero;
        }

        // ------------------------------------------------------------------ bombardeiro
        IEnumerator BomberFuse()
        {
            const float fuse = 0.9f, rad = 2.6f;
            Vector3 c = transform.position;
            var mk = FX.Marker(c, rad, Red, fuse);
            Sfx.Play("enemy_alert", c, 0.9f);
            HUD.Popup(c + Vector3.up * (e.Top + 0.3f), "!!", U.Hex("ff5a3a"), true);
            e.Act("Taunt", fuse);
            float t = 0f, blink = 0f;
            while (t < fuse)
            {
                if (!Alive || e.Stunned)
                {
                    if (mk != null) Destroy(mk);
                    yield break;   // morto (explode no OnDied) ou atordoado (pavio apagado)
                }
                float dt = Time.deltaTime;
                t += dt;
                blink -= dt;
                if (blink <= 0f)
                {
                    blink = Mathf.Lerp(0.22f, 0.06f, t / fuse);
                    if (e.visual != null) e.visual.Flash();
                    FX.FlashLight(transform.position + Vector3.up, U.Hex("ff3a2a"), 2.2f, 3.5f, 0.08f);
                }
                e.Drive(Vector3.zero, transform.forward);
                yield return null;
            }
            if (mk != null) Destroy(mk);
            if (!Alive) yield break;
            selfExploded = true;
            Explode(transform.position, rad, true);
            e.Kill();
        }

        void Explode(Vector3 c, float rad, bool hurtPlayer)
        {
            FX.Burst(c + Vector3.up * 0.6f, U.Hex("ff8a2a"), 1.6f, 40);
            FX.Ring(c, rad, U.Hex("ffb347"), 0.35f);
            FX.Dust(c, U.Hex("6a5a4a"), 1.5f);
            FX.FlashLight(c + Vector3.up, U.Hex("ff8a2a"), 5f, 7f, 0.35f);
            Sfx.Play("explosion", c);
            if (Game.I != null) Game.I.Shake(hurtPlayer ? 0.35f : 0.2f);
            if (hurtPlayer)
            {
                var p = FloorRoot.LivePlayer();
                if (p != null && U.Flat(p.transform.position - c).magnitude <= rad + 0.4f && Mathf.Abs(p.transform.position.y - c.y) < 2.5f)
                    FloorRoot.HurtPlayer(Dmg(1f), c);
            }
            // fogo amigo: aliados próximos também sofrem (e outros bombardeiros explodem em cadeia)
            if (Game.I == null) return;
            int ally = Mathf.Max(1, Mathf.RoundToInt(Dmg(hurtPlayer ? 0.5f : 1.5f)));
            foreach (var o in Game.I.enemies.ToArray())
            {
                if (o == null || o == e || o.dead) continue;
                if (U.Flat(o.transform.position - c).magnitude > rad + o.radius) continue;
                o.TakeHit(ally, c, false);
            }
        }

        // ------------------------------------------------------------------ escudeiro
        int ShieldFilter(int dmg, Vector3 from)
        {
            if (!Alive) return dmg;
            if (e.Stunned || e.state == "recover" || e.state == "windup" || Time.time < vulnerableUntil) return dmg;
            Vector3 to = U.Flat(from - transform.position);
            if (to.magnitude < 0.6f) return dmg;   // explosão em cima dele: sem lado
            float dot = Vector3.Dot(transform.forward, to.normalized);
            Vector3 head = transform.position + Vector3.up * (e.Top + 0.2f);
            if (dot > 0.35f)
            {
                int left = Mathf.FloorToInt(dmg * 0.15f);
                FX.Burst(transform.position + Vector3.up * 1.1f * e.scale + to.normalized * (e.radius + 0.3f), Color.white, 0.5f, 10);
                if (Time.time >= shieldMsgT)
                {
                    shieldMsgT = Time.time + 0.35f;
                    Sfx.Play("block", transform.position);
                    HUD.Popup(head, left > 0 ? "escudo -" + left : "BLOQUEADO", U.Hex("c7d6e8"));
                }
                return left;
            }
            if (dot < -0.3f)
            {
                if (Time.time >= shieldMsgT) { shieldMsgT = Time.time + 0.35f; HUD.Popup(head + Vector3.up * 0.4f, "COSTAS!", U.Hex("ffd04a"), true); }
                vulnerableUntil = Time.time + 0.6f;
                return Mathf.RoundToInt(dmg * 1.3f);
            }
            return dmg;
        }

        // ------------------------------------------------------------------ invocador
        IEnumerator SummonRoutine()
        {
            cd = Cooldown(7f);
            string id = !string.IsNullOrEmpty(e.def.summon) ? e.def.summon : "swarm_ossinho";
            if (GameData.Enemy(id) == null) id = "minion";
            int n = Random.Range(2, 4);
            var spots = new List<Vector3>();
            for (int i = 0; i < n * 3 && spots.Count < n; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 c = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(1.8f, 3f);
                if (EnemyAbilities.GroundAt(c, transform.position.y, out var g) && !EnemyAbilities.Blocked(g)) spots.Add(g);
            }
            if (spots.Count == 0) yield break;
            e.Act("Summon", 1f);
            Sfx.Play("magic_cast", transform.position);
            FX.Ring(transform.position, 2.5f, U.Hex("8aff9a"), 0.8f);
            var marks = new List<GameObject>();
            foreach (var s in spots) marks.Add(FX.Marker(s, 0.8f, new Color(0.5f, 1f, 0.55f, 0.8f), 0.9f));
            float t = 0f;
            while (t < 0.9f)
            {
                if (!Alive || e.Stunned) { foreach (var m in marks) if (m != null) Destroy(m); yield break; }
                t += Time.deltaTime;
                e.Drive(Vector3.zero, transform.forward);
                yield return null;
            }
            foreach (var m in marks) if (m != null) Destroy(m);
            if (Game.I == null) yield break;
            foreach (var s in spots)
            {
                FX.Pillar(s, U.Hex("8aff9a"), 0.6f);
                var m = Game.I.SpawnEnemy(id, s + Vector3.up * 0.1f, 1f);
                if (m != null) { m.customRewards = true; minions.Add(m); m.Alert(false); }
            }
            Sfx.Play("portal", transform.position, 0.6f);
        }

        // ------------------------------------------------------------------ curandeiro
        Enemy HurtAlly(float range)
        {
            if (Game.I == null) return null;
            Enemy best = null;
            float bk = 0.9f;
            foreach (var o in Game.I.enemies)
            {
                if (o == null || o == e || o.dead || o.maxHp <= 0f || o.Ai == "goblin") continue;
                if (U.Flat(o.transform.position - transform.position).magnitude > range) continue;
                float k = o.hp / o.maxHp;
                if (o.def != null && o.def.boss) k -= 0.15f;   // chefes têm prioridade
                if (k < bk) { bk = k; best = o; }
            }
            return best;
        }

        void HealerMove(Player p, Vector3 dirP, float dist)
        {
            var ally = HurtAlly(12f);
            if (ally != null && cd <= 0f && U.Flat(ally.transform.position - transform.position).magnitude <= 8f
                && !Projectile.WallBetween(transform.position + Vector3.up, ally.transform.position + Vector3.up))
            {
                StartCoroutine(Run(HealRoutine(ally)));
                return;
            }
            if (dist < 4f) { e.Drive(Steer(-dirP) * e.def.speed * 1.1f, -dirP); return; }
            if (ally != null)
            {
                // fica atrás do aliado ferido (do ponto de vista do herói)
                Vector3 away = U.Flat(ally.transform.position - p.transform.position);
                away = away.sqrMagnitude > 0.01f ? away.normalized : -dirP;
                Vector3 spot = ally.transform.position + away * 2.5f;
                Vector3 to = U.Flat(spot - transform.position);
                if (to.magnitude > 0.6f) e.Drive(Steer(to.normalized) * e.def.speed, ally.transform.position - transform.position);
                else e.Drive(Vector3.zero, ally.transform.position - transform.position);
                return;
            }
            Kite(dirP, dist, 6f, 9f, 0.5f);
        }

        IEnumerator HealRoutine(Enemy ally)
        {
            cd = Cooldown(4.5f);
            const float channel = 0.7f;
            float hp0 = e.hp;
            if (beam == null) beam = MakeLine("raio_cura");
            beam.enabled = true;
            e.Act("Cast", channel + 0.2f);
            Sfx.Play("magic_cast", transform.position, 0.7f);
            float t = 0f;
            while (t < channel)
            {
                if (!Alive || e.Stunned || ally == null || ally.dead || e.hp < hp0)
                {
                    if (Alive && e.hp < hp0) HUD.Popup(transform.position + Vector3.up * (e.Top + 0.4f), "interrompido", U.Hex("c7d6e8"));
                    beam.enabled = false;
                    yield break;
                }
                t += Time.deltaTime;
                float k = t / channel;
                Vector3 a = transform.position + Vector3.up * 1.3f * e.scale + transform.forward * 0.4f;
                Vector3 b = ally.transform.position + Vector3.up * ally.Top * 0.55f;
                beam.SetPosition(0, a);
                beam.SetPosition(1, b);
                beam.widthMultiplier = 0.05f + 0.2f * k;
                var c = new Color(0.45f, 1f, 0.5f, 0.3f + 0.6f * k);
                beam.startColor = c; beam.endColor = c;
                e.Drive(Vector3.zero, ally.transform.position - transform.position);
                yield return null;
            }
            beam.enabled = false;
            if (!Alive || ally == null || ally.dead) yield break;
            Vector3 ap = ally.transform.position;
            FX.Lightning(transform.position + Vector3.up * 1.3f * e.scale, ap + Vector3.up * ally.Top * 0.55f, U.Hex("7aff8a"), 0.12f, 1, 0.25f);
            FX.Pillar(ap, U.Hex("7aff8a"), 0.8f);
            Sfx.Play("heal", ap, 0.8f);
            ally.Heal(ally.maxHp * (ally.def != null && ally.def.boss ? 0.06f : 0.3f));
            // respingo em quem estiver colado no alvo
            if (Game.I != null)
                foreach (var o in Game.I.enemies)
                    if (o != null && o != ally && o != e && !o.dead && U.Flat(o.transform.position - ap).magnitude < 2.5f)
                        o.Heal(o.maxHp * 0.12f);
        }

        LineRenderer MakeLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = U.Fx(true);
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        // ------------------------------------------------------------------ atirador de elite
        IEnumerator SniperAim()
        {
            cd = Cooldown(2.8f);
            const float aim = 1.2f;
            float reach = Mathf.Max(9f, e.def.reach) + 4f;
            float hp0 = e.hp;
            if (beam == null) beam = MakeLine("mira_laser");
            beam.enabled = true;
            e.Act("Shoot", aim);
            Sfx.Play("enemy_alert", transform.position, 0.5f);
            Vector3 dir = transform.forward;
            float t = 0f;
            while (t < aim)
            {
                var p = P;
                if (!Alive || e.Stunned || e.hp < hp0)
                {
                    if (Alive && e.hp < hp0) HUD.Popup(transform.position + Vector3.up * (e.Top + 0.4f), "interrompido", U.Hex("c7d6e8"));
                    beam.enabled = false;
                    yield break;
                }
                t += Time.deltaTime;
                float k = t / aim;
                if (p != null && k < 0.75f)   // segue o herói e trava nos últimos 0,3 s
                {
                    Vector3 to = U.Flat(p.transform.position - transform.position);
                    if (to.sqrMagnitude > 0.01f) dir = Vector3.RotateTowards(dir, to.normalized, 6f * Time.deltaTime, 0f);
                }
                Vector3 o = transform.position + Vector3.up * 1.2f * e.scale;
                float len = reach;
                if (Projectile.WallHit(o, dir, reach, out var wh)) len = wh.distance;
                beam.SetPosition(0, o + dir * 0.4f);
                beam.SetPosition(1, o + dir * len);
                bool locked = k >= 0.75f;
                float pulse = locked ? (Mathf.Repeat(t * 14f, 1f) < 0.5f ? 1f : 0.6f) : 1f;
                beam.widthMultiplier = (0.03f + 0.07f * k) * pulse;
                var c = new Color(1f, locked ? 0.1f : 0.25f, 0.15f, (0.35f + 0.6f * k) * pulse);
                beam.startColor = c; beam.endColor = new Color(c.r, c.g, c.b, c.a * 0.5f);
                e.Drive(Vector3.zero, dir);
                yield return null;
            }
            beam.enabled = false;
            if (!Alive) yield break;
            e.Act("Shoot", 0.4f);
            Vector3 origin = transform.position + Vector3.up * 1.2f * e.scale + dir * (e.radius + 0.3f);
            EnemyBolt.Spawn(origin, dir, 30f, Dmg(1f), U.Hex("ff4a3a"), reach / 30f + 0.1f, 0.2f, true);
            Sfx.Play("bow", transform.position);
            FX.FlashLight(origin, U.Hex("ff4a3a"), 3f, 4f, 0.12f);
            // recua um pouco depois do tiro
            float back = 0f;
            while (back < 0.35f && Alive)
            {
                back += Time.deltaTime;
                e.Drive(Steer(-dir) * e.def.speed * 0.8f, dir);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ aríete (investida)
        IEnumerator ChargeRoutine()
        {
            var p = P;
            if (p == null || e.cc == null) yield break;
            cd = Cooldown(6f);
            e.hold = true;
            e.CancelAttack();
            float hp0 = e.hp;
            const float tele = 0.9f;
            Vector3 dir = U.Flat(p.transform.position - transform.position).normalized;
            float len = Mathf.Min(12f, U.Flat(p.transform.position - transform.position).magnitude + 3f);
            float w = 1.5f * Mathf.Max(1f, e.scale * 0.8f);
            var lane = FX.FlatQuad("investida", transform.position, U.WhiteTexture(), new Color(1f, 0.2f, 0.15f, 0.3f), false);
            e.Act("Taunt", tele);
            Sfx.Play("enemy_alert", transform.position, 0.8f);
            float t = 0f;
            while (t < tele)
            {
                if (!Alive || e.Stunned) { if (lane != null) Destroy(lane); yield break; }
                t += Time.deltaTime;
                var pp = P;
                if (pp != null && t < tele * 0.55f)
                {
                    Vector3 to = U.Flat(pp.transform.position - transform.position);
                    if (to.sqrMagnitude > 0.01f) dir = Vector3.RotateTowards(dir, to.normalized, 4f * Time.deltaTime, 0f);
                }
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                lane.transform.position = transform.position + dir * len * 0.5f + Vector3.up * 0.06f;
                lane.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                lane.transform.localScale = new Vector3(w, len, 1f);
                FX.SetColor(lane.GetComponent<Renderer>(), new Color(1f, 0.2f, 0.15f, 0.2f + 0.35f * (t / tele)));
                if (Random.value < 0.15f) FX.Dust(transform.position, U.Hex("a09080"), 0.4f);
                e.Drive(Vector3.zero, dir);
                yield return null;
            }
            if (lane != null) Destroy(lane);
            if (!Alive || e.cc == null || !e.cc.enabled) yield break;

            Sfx.Play("dash", transform.position);
            e.Act("Attack1", 0.6f);
            const float speed = 16f;
            float gone = 0f;
            bool hitPlayer = false;
            while (gone < len && Alive && e.cc != null && e.cc.enabled && !e.Stunned)
            {
                float dt = Time.deltaTime;
                float step = speed * dt;
                if (Projectile.WallHit(transform.position + Vector3.up * 0.8f, dir, step + e.radius + 0.25f, out _))
                {
                    // bateu na parede: atordoado
                    FX.Dust(transform.position + dir * e.radius, U.Hex("8a7a6a"), 1.3f);
                    FX.Burst(transform.position + Vector3.up + dir * e.radius, U.Hex("e8e0d0"), 0.8f, 18);
                    Sfx.Play("hit_heavy", transform.position);
                    if (Game.I != null) Game.I.Shake(0.3f);
                    HUD.Popup(transform.position + Vector3.up * (e.Top + 0.3f), "ZONZO!", U.Hex("ffe08a"), true);
                    e.Stun(2.4f);
                    yield break;
                }
                e.cc.Move(dir * step + Vector3.down * 2f * dt);
                gone += step;
                if (Random.value < 0.4f) FX.Dust(transform.position, U.Hex("a09080"), 0.5f);
                var pp = FloorRoot.LivePlayer();
                if (!hitPlayer && pp != null && U.Flat(pp.transform.position - transform.position).magnitude < 1.1f + e.radius)
                {
                    hitPlayer = true;
                    string r = FloorRoot.HurtPlayer(Dmg(1.4f), transform.position - dir);
                    if (r == "parried") { e.Stun(1.6f); yield break; }
                    if (Game.I != null) Game.I.Shake(0.3f);
                    break;
                }
                e.Drive(Vector3.zero, dir);
                yield return null;
            }
            // recuperação: vulnerável
            float rec = 0f;
            while (rec < 0.6f && Alive && !e.Stunned)
            {
                rec += Time.deltaTime;
                e.Drive(Vector3.zero, dir);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ elementos / morte
        void OnDied(Enemy dead)
        {
            if (beam != null) beam.enabled = false;
            Vector3 c = transform.position;
            if (ai == "bomber" && !selfExploded) Explode(c, 2.6f, false);
            string el = e.def != null ? e.def.element ?? "" : "";
            if (el == "fire") FireZone.Spawn(c, 1.9f, Dmg(0.7f), 4f, 0.5f);
            else if (el == "poison") PoisonFog.SpawnPuddle(c, 2f, Dmg(0.5f), 5f);
        }

        /// <summary>Golpe gelado no herói (o Player não tem lentidão: só efeito visual + dano extra já aplicado).</summary>
        public static void FrostHit(Player p)
        {
            if (p == null) return;
            Vector3 pos = p.transform.position;
            FX.Burst(pos + Vector3.up, U.Hex("bfeaff"), 0.8f, 18);
            FX.Ring(pos, 1.2f, U.Hex("9fe4ff"), 0.4f);
            FX.Sparkle(pos + Vector3.up * 0.5f, U.Hex("dff6ff"), 0.6f, 0.4f);
            Sfx.Play("ice", pos, 0.7f);
            HUD.Popup(pos + Vector3.up * 2.7f, "congelado", U.Hex("9fe4ff"));
        }
    }
}
