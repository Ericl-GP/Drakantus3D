using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Drakantus
{
    // =====================================================================================
    //  [Inimigos] Padrões de ataque dos chefes (Resources/Data/bosses.json → "patterns")
    // =====================================================================================

    /// <summary>Padrões de um chefe: listas por fase (100–66%, 66–33%, 33–0%).</summary>
    [Serializable]
    public class BossPatternDef
    {
        public string id = "";
        public string color = "ff7a4a";     // cor dos efeitos/projéteis do chefe
        public string summon = "minion";    // id invocado por "guard"
        public string wallColor = "e8e0c8"; // cor da muralha ("bonewall")
        public float hpMult = 1.5f;         // vida extra (chefes aguentam mais que mobs)
        public string[] phase1, phase2, phase3;
    }

    [Serializable] class BossPatternList { public BossPatternDef[] patterns; }

    /// <summary>
    /// Chefes com ataques próprios em 3 fases. Ids de padrão:
    ///  shockwave (pancada + ondas concêntricas telegrafadas em anel) · spin (giro com a arma) · leap (salto esmagador)
    ///  guard (invoca guarda) · spiral (orbes em espiral) · bonewall (muralha que fecha a arena) · clones (teleporte + ilusões)
    ///  drain (raio drenante que gira devagar) · fan (leque de projéteis) · firegrid (zonas de fogo em xadrez)
    ///  charges (investidas múltiplas) · icerain (estacas de gelo) · meteor (meteoros que deixam fogo) · nova (anel de projéteis)
    ///  plague (poças de veneno).
    /// Transição de fase: invulnerável 1,5 s, rugido, FX e tremor. Adicionado pelo Enemy.Init (chefes com entrada no JSON).
    /// Enquanto um padrão roda, a IA base fica parada (Enemy.hold) e o EnemyAbilities espera.
    /// </summary>
    public class BossPatterns : MonoBehaviour
    {
        static Dictionary<string, BossPatternDef> defs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { defs = null; }

        public static void Reload() { defs = null; }

        static void LoadDefs()
        {
            if (defs != null) return;
            defs = new Dictionary<string, BossPatternDef>();
            var ta = Resources.Load<TextAsset>("Data/bosses");
            if (ta == null) return;
            try
            {
                var l = JsonUtility.FromJson<BossPatternList>(ta.text);
                if (l != null && l.patterns != null)
                    foreach (var d in l.patterns)
                        if (d != null && !string.IsNullOrEmpty(d.id)) defs[d.id] = d;
            }
            catch (Exception ex) { Debug.LogError("[Drakantus] Erro lendo padrões em bosses.json: " + ex.Message); }
        }

        public static BossPatternDef Def(string enemyId)
        {
            LoadDefs();
            return enemyId != null && defs.TryGetValue(enemyId, out var d) ? d : null;
        }

        public static BossPatterns Attach(Enemy e)
        {
            if (e == null || e.def == null) return null;
            var d = Def(e.def.id);
            if (d == null) return null;
            var bp = e.GetComponent<BossPatterns>();
            if (bp == null) bp = e.gameObject.AddComponent<BossPatterns>();
            bp.e = e;
            bp.pd = d;
            return bp;
        }

        // ------------------------------------------------------------------ estado
        Enemy e;
        BossPatternDef pd;
        EnemyAbilities abil;
        int phase;
        bool busy, transitioning, pendingPhase, engaged, started;
        float nextPattern, dmgMult = 1f, teleMult = 1f;
        string last = "";
        Color col = Color.white;
        LineRenderer beam;
        readonly List<LineRenderer> aimLines = new List<LineRenderer>();
        readonly List<Enemy> guards = new List<Enemy>();
        readonly List<Enemy> clones = new List<Enemy>();
        readonly List<GameObject> temp = new List<GameObject>();
        GameObject wall;
        ParticleSystem aura;

        static readonly Color Red = new Color(1f, 0.2f, 0.15f, 0.85f);

        /// <summary>Executando padrão ou trocando de fase (o EnemyAbilities espera).</summary>
        public bool Busy => busy || transitioning || pendingPhase;
        public int Phase => phase;

        /// <summary>O padrão atual deve parar (morte, troca de fase, atordoado).</summary>
        bool Cut => e == null || e.dead || pendingPhase || e.Stunned;

        static Player P
        {
            get
            {
                var p = FloorRoot.LivePlayer();
                return p != null && p.canFight ? p : null;
            }
        }

        float D(float mult) => Mathf.Max(1f, (e.def != null ? e.def.dmg : 10f) * mult * dmgMult);
        float Sc => Mathf.Max(1f, e.scale);

        void Start()
        {
            if (e == null) e = GetComponent<Enemy>();
            if (e == null || pd == null) { enabled = false; return; }
            started = true;
            col = U.Hex(string.IsNullOrEmpty(pd.color) ? "ff7a4a" : pd.color);
            // vida extra; nos andares fixos (sem FloorRoot) também escala pela dificuldade do FloorRegistry
            float hpM = Mathf.Max(1f, pd.hpMult);
            var fe = Game.I != null ? FloorRegistry.Get(Game.I.mapId) : null;
            if (fe != null && fe.builtIn)
            {
                int diff = Mathf.Clamp(fe.difficulty, 1, 10);
                hpM *= 1f + (diff - 1) * 0.08f;
                dmgMult = 1f + (diff - 1) * 0.06f;
            }
            e.maxHp *= hpM;
            e.hp = e.maxHp;
            nextPattern = Time.time + 2.5f;
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (!started || e == null) return;
            if (e.dead) { Cleanup(); enabled = false; return; }

            float k = e.maxHp > 0f ? e.hp / e.maxHp : 1f;
            if (!pendingPhase && !transitioning && phase < 2 && k <= (phase == 0 ? 0.66f : 0.33f)) pendingPhase = true;
            if (pendingPhase && !busy)
            {
                pendingPhase = false;
                StartCoroutine(PhaseTransition());
                return;
            }

            if (!e.alerted) { engaged = false; return; }
            if (!engaged) { engaged = true; nextPattern = Mathf.Max(nextPattern, Time.time + 2f); }
            if (busy || transitioning || e.Stunned || Time.time < nextPattern) return;
            if (e.state == "windup") return;   // deixa o golpe normal terminar
            if (abil == null) abil = GetComponent<EnemyAbilities>();
            if (abil != null && abil.Busy) return;
            var p = P;
            if (p == null) return;
            string id = Pick(p);
            if (id == null) { nextPattern = Time.time + 1f; return; }
            StartCoroutine(RunPattern(id));
        }

        string[] PhaseList()
        {
            string[] l = phase == 0 ? pd.phase1 : phase == 1 ? pd.phase2 : pd.phase3;
            if ((l == null || l.Length == 0) && phase >= 2) l = pd.phase2;
            if (l == null || l.Length == 0) l = pd.phase1;
            return l;
        }

        string Pick(Player p)
        {
            var l = PhaseList();
            if (l == null || l.Length == 0) return null;
            float dist = U.Flat(p.transform.position - transform.position).magnitude;
            var opts = new List<string>();
            foreach (var id in l)
                if (!string.IsNullOrEmpty(id) && Usable(id, dist, p)) opts.Add(id);
            if (opts.Count > 1) opts.Remove(last);
            if (opts.Count == 0) return null;
            return opts[Random.Range(0, opts.Count)];
        }

        bool Usable(string id, float dist, Player p)
        {
            switch (id)
            {
                case "guard": guards.RemoveAll(g => g == null || g.dead); return guards.Count < 3;
                case "clones": clones.RemoveAll(c => c == null || c.dead); return clones.Count == 0;
                case "bonewall": return wall == null && dist < 9.5f;
                case "leap": return dist > 4f && dist < 16f;
                case "spin": return dist < 7f;
                case "charges": return dist > 3f && e.cc != null && e.cc.enabled;
                case "shockwave": return dist < 13f;
                case "drain": return dist < 13f && !Projectile.WallBetween(transform.position + Vector3.up * 1.2f, p.transform.position + Vector3.up);
            }
            return true;
        }

        IEnumerator RunPattern(string id)
        {
            busy = true;
            last = id;
            e.hold = true;
            e.CancelAttack();
            IEnumerator body = null;
            switch (id)
            {
                case "shockwave": body = Shockwave(3 + phase); break;
                case "spin": body = Spin(); break;
                case "leap": body = Leap(); break;
                case "guard": body = Guard(); break;
                case "spiral": body = Spiral(); break;
                case "bonewall": body = BoneWall(); break;
                case "clones": body = Clones(); break;
                case "drain": body = Drain(); break;
                case "fan": body = Fan(); break;
                case "firegrid": body = FireGrid(); break;
                case "charges": body = Charges(); break;
                case "icerain": body = Rain(5 + phase, 1.6f, U.Hex("bfeaff"), false); break;
                case "meteor": body = Rain(3 + phase, 2.1f, U.Hex("ff7a2a"), true); break;
                case "nova": body = Nova(); break;
                case "plague": body = Plague(); break;
                default: Debug.LogWarning("[Drakantus] Padrão de chefe desconhecido: " + id); break;
            }
            if (body != null) yield return body;
            if (e != null && !transitioning) e.hold = false;
            busy = false;
            float baseCd = phase == 0 ? 5.5f : phase == 1 ? 4.4f : 3.4f;
            nextPattern = Time.time + baseCd * Random.Range(0.85f, 1.2f);
        }

        // ------------------------------------------------------------------ fases
        IEnumerator PhaseTransition()
        {
            transitioning = true;
            phase = Mathf.Min(2, phase + 1);
            e.hold = true;
            e.CancelAttack();
            e.invulnUntil = Time.time + 1.5f;
            teleMult = phase >= 2 ? 0.85f : 0.92f;
            Vector3 pos = transform.position;
            Sfx.Play("boss_roar", pos);
            if (Game.I != null) Game.I.Shake(0.6f);
            FX.Pillar(pos, col, 1.6f);
            FX.Ring(pos, 6f, col, 0.7f);
            FX.Burst(pos + Vector3.up * 1.5f * Sc, col, 1.4f, 40);
            e.Act("Taunt", 1.5f);
            HUD.Popup(pos + Vector3.up * (e.Top + 0.8f), "FASE " + (phase + 1), col, true);
            if (HUD.I != null) HUD.I.Toast(e.def.name + (phase == 1 ? " se enfurece! (fase 2)" : " libera todo o seu poder! (fase 3)"));
            if (e.visual != null)
            {
                Color baseTint = e.def != null && !string.IsNullOrEmpty(e.def.tint) ? U.Hex(e.def.tint) : Color.white;
                e.visual.SetTint(Color.Lerp(baseTint, col, 0.22f * phase));
            }
            if (aura == null) aura = LevelDecor.Motes(transform, new Vector3(0f, 1.2f * Sc, 0f), new Vector3(1.8f * Sc, 2f * Sc, 1.8f * Sc), col, 30, 0.4f);
            float t = 0f;
            bool pushed = false;
            while (t < 1.5f && e != null && !e.dead)
            {
                t += Time.deltaTime;
                if (!pushed && t > 0.6f)
                {
                    pushed = true;
                    FX.Ring(transform.position, 4.5f, Color.white, 0.4f);
                    FX.Dust(transform.position, U.Hex("8a7a6a"), 2f);
                    if (Game.I != null) Game.I.Shake(0.3f);
                }
                e.Drive(Vector3.zero, transform.forward);
                yield return null;
            }
            if (e != null) e.hold = false;
            transitioning = false;
            last = "";
            nextPattern = Time.time + 0.4f;
        }

        // ------------------------------------------------------------------ utilitários
        IEnumerator Hold(float t, bool facePlayer = true)
        {
            float k = 0f;
            while (k < t && !Cut)
            {
                k += Time.deltaTime;
                Vector3 f = transform.forward;
                if (facePlayer) { var p = P; if (p != null) f = p.transform.position - transform.position; }
                e.Drive(Vector3.zero, f);
                yield return null;
            }
        }

        static bool PlayerIn(Player p, Vector3 c, float r)
        {
            return p != null && U.Flat(p.transform.position - c).magnitude <= r && Mathf.Abs(p.transform.position.y - c.y) < 2.5f;
        }

        static bool PlayerInBand(Player p, Vector3 c, float r, float half)
        {
            if (p == null || Mathf.Abs(p.transform.position.y - c.y) >= 2.5f) return false;
            float d = U.Flat(p.transform.position - c).magnitude;
            return Mathf.Abs(d - r) <= half;
        }

        string Hurt(float mult, Vector3 from) => FloorRoot.HurtPlayer(D(mult), from);

        GameObject Track(GameObject g) { if (g != null) temp.Add(g); return g; }

        void Kill(GameObject g)
        {
            if (g == null) return;
            temp.Remove(g);
            Destroy(g);
        }

        void KillAll(List<GameObject> l)
        {
            foreach (var g in l) Kill(g);
            l.Clear();
        }

        /// <summary>Anel (faixa entre inner e outer) desenhado no chão: telegrafa ondas de choque.</summary>
        GameObject Band(Vector3 c, float inner, float outer, Color color)
        {
            inner = Mathf.Max(0f, inner);
            var go = new GameObject("aviso_anel");
            go.transform.SetParent(FX.Root, false);
            go.transform.position = c + Vector3.up * 0.06f;
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            const int seg = 64;
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tris = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d * inner;
                v[i * 2 + 1] = d * outer;
                uv[i * 2] = new Vector2(0.5f, 0.5f);
                uv[i * 2 + 1] = new Vector2(0.5f, 0.5f);
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = i * 2, b0 = i * 2 + 1, a1 = i * 2 + 2, b1 = i * 2 + 3;
                int t = i * 6;
                // horário visto de cima (normal para cima)
                tris[t] = a0; tris[t + 1] = b1; tris[t + 2] = b0;
                tris[t + 3] = a0; tris[t + 4] = a1; tris[t + 5] = b1;
            }
            var m = new Mesh { name = "anel" };
            m.vertices = v;
            m.uv = uv;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            mf.sharedMesh = m;
            mr.sharedMaterial = U.Fx(false, U.WhiteTexture());
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            FX.SetColor(mr, color);
            go.AddComponent<MeshOwner>().mesh = m;
            return Track(go);
        }

        GameObject Square(Vector3 c, float size, Color color)
        {
            var q = FX.FlatQuad("aviso_quadro", c + Vector3.up * 0.05f, U.WhiteTexture(), color, false);
            q.transform.localScale = new Vector3(size, size, 1f);
            return Track(q);
        }

        LineRenderer Line(string name)
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

        Vector3 Chest => transform.position + Vector3.up * 1.1f * Sc;

        bool FindSpot(Vector3 around, float rMin, float rMax, out Vector3 spot, float angle = -1f)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = angle >= 0f && i < 4 ? angle + Random.Range(-0.4f, 0.4f) : Random.Range(0f, Mathf.PI * 2f);
                Vector3 c = around + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(rMin, rMax);
                if (EnemyAbilities.GroundAt(c, around.y, out var g) && !EnemyAbilities.Blocked(g)) { spot = g; return true; }
            }
            spot = around;
            return false;
        }

        // ------------------------------------------------------------------ shockwave: pancada + ondas concêntricas
        IEnumerator Shockwave(int rings)
        {
            float tele = 1.05f * teleMult;
            Vector3 c = transform.position;
            float slamR = 2.1f + e.radius;
            const float band = 0.8f, gap = 2.8f;
            var marks = new List<GameObject>();
            var slamMark = Track(FX.Marker(c, slamR, Red, tele));
            var radii = new List<float>();
            for (int i = 0; i < rings; i++)
            {
                float r = slamR + 1.7f + i * gap;
                radii.Add(r);
                marks.Add(Band(c, r - band, r + band, new Color(col.r, col.g * 0.6f, col.b * 0.5f, 0.32f)));
            }
            e.Act("Taunt", tele);
            Sfx.Play("swing_heavy", c);
            yield return Hold(tele);
            Kill(slamMark);
            if (Cut) { KillAll(marks); yield break; }

            e.Act("Attack2", 0.6f);
            FX.Dust(c, U.Hex("8a7a6a"), 2.2f);
            FX.Ring(c, slamR, col, 0.3f);
            FX.Burst(c + Vector3.up * 0.3f, col, 1.2f, 30);
            Sfx.Play("explosion", c);
            if (Game.I != null) Game.I.Shake(0.45f);
            if (PlayerIn(P, c, slamR + 0.3f)) Hurt(1.3f, c);

            // as ondas já foram lançadas: terminam mesmo se o chefe trocar de fase
            for (int i = 0; i < radii.Count; i++)
            {
                yield return new WaitForSeconds(0.28f);
                float r = radii[i];
                FX.Ring(c, r + band, col, 0.25f, (r - band) / (r + band));
                Sfx.Play("sk_quake", c, 0.5f);
                if (i < marks.Count) Kill(marks[i]);
                if (PlayerInBand(P, c, r, band + 0.2f)) Hurt(0.85f, c);
            }
            marks.Clear();
            yield return Hold(0.4f);
        }

        // ------------------------------------------------------------------ spin: giro com a arma
        IEnumerator Spin()
        {
            float r = 2.5f + e.radius;
            float tele = 0.75f * teleMult;
            var mk = Track(FX.Marker(transform.position, r, Red, tele));
            mk.transform.SetParent(transform, true);
            e.Act("Taunt", tele);
            Sfx.Play("swing_heavy", transform.position);
            yield return Hold(tele);
            Kill(mk);
            if (Cut) yield break;

            var area = Band(transform.position, r - 0.12f, r + 0.12f, new Color(1f, 0.3f, 0.2f, 0.6f));
            area.transform.SetParent(transform, true);
            float dur = phase >= 2 ? 3.2f : 2.6f;
            float t = 0f, tick = 0f, ang = 0f;
            while (t < dur && !Cut)
            {
                float dt = Time.deltaTime;
                t += dt;
                tick -= dt;
                ang += 900f * dt;
                var p = P;
                Vector3 to = p != null ? U.Flat(p.transform.position - transform.position) : Vector3.zero;
                Vector3 v = to.sqrMagnitude > 0.25f ? EnemyArchetype.SteerFrom(transform.position, to.normalized, 1.4f + e.radius) * e.def.speed * 0.7f : Vector3.zero;
                e.Drive(v, to.sqrMagnitude > 0.01f ? to : transform.forward);
                if (tick <= 0f)
                {
                    tick = 0.33f;
                    e.Act("Spin", 0.4f);
                    Vector3 d = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                    Vector3 o = transform.position + Vector3.up * 0.9f * Sc;
                    FX.SlashArc(o, d, col, r, 190f, true, 0.2f, 0.6f, 8);
                    FX.SlashArc(o, -d, col, r, 190f, true, 0.2f, 0.6f, 8);
                    Sfx.Play("swing_heavy", transform.position, 0.7f);
                    if (PlayerIn(p, transform.position, r + 0.3f)) Hurt(0.7f, transform.position);
                }
                yield return null;
            }
            Kill(area);
            if (e == null || e.dead) yield break;
            // tontura: janela para punir
            HUD.Popup(transform.position + Vector3.up * (e.Top + 0.4f), "tonto", U.Hex("ffe08a"));
            yield return Hold(0.9f, false);
        }

        // ------------------------------------------------------------------ leap: salto esmagador
        IEnumerator Leap()
        {
            var p = P;
            if (p == null || e.cc == null) yield break;
            Vector3 start = transform.position;
            Vector3 target = p.transform.position;
            Vector3 d = U.Flat(target - start);
            if (d.magnitude > 14f) target = start + d.normalized * 14f;
            Vector3 g = target;
            bool ok = EnemyAbilities.GroundAt(target, start.y, out g) && !EnemyAbilities.Blocked(g);
            for (float f = 0.8f; !ok && f > 0.1f; f -= 0.2f)
                ok = EnemyAbilities.GroundAt(Vector3.Lerp(start, target, f), start.y, out g) && !EnemyAbilities.Blocked(g);
            if (!ok) yield break;
            target = g;

            float r = 3.1f;
            float tele = 1.15f * teleMult;
            var marks = new List<GameObject> { Track(FX.Marker(target, r, Red, tele)) };
            bool outer = phase >= 1;
            if (outer) marks.Add(Band(target, r + 1.2f, r + 2.8f, new Color(col.r, col.g * 0.6f, col.b * 0.5f, 0.3f)));
            e.Act("Taunt", 0.35f);
            FX.Dust(start, U.Hex("8a7a6a"), 1.2f);
            yield return Hold(0.35f);
            if (Cut) { KillAll(marks); yield break; }

            e.Act("Jump", tele);
            Sfx.Play("sk_leap", start);
            e.cc.enabled = false;
            float air = Mathf.Max(0.4f, tele - 0.35f), t = 0f;
            float height = 4f + U.Flat(target - start).magnitude * 0.15f;
            while (t < air && !e.dead)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / air);
                Vector3 pos = Vector3.Lerp(start, target, k);
                pos.y += Mathf.Sin(k * Mathf.PI) * height;
                transform.position = pos;
                e.Drive(Vector3.zero, target - start);
                yield return null;
            }
            transform.position = target;
            if (!e.dead) e.cc.enabled = true;
            Kill(marks[0]);
            if (e.dead) { KillAll(marks); yield break; }

            e.Act("Attack2", 0.6f);
            FX.Dust(target, U.Hex("8a7a6a"), 2.4f);
            FX.Ring(target, r, col, 0.3f);
            FX.Burst(target + Vector3.up * 0.4f, col, 1.4f, 34);
            Sfx.Play("explosion", target);
            Sfx.Play("hit_heavy", target);
            if (Game.I != null) Game.I.Shake(0.6f);
            if (PlayerIn(P, target, r + 0.3f)) Hurt(1.6f, target);
            if (outer)
            {
                yield return new WaitForSeconds(0.25f);
                FX.Ring(target, r + 2.8f, col, 0.25f, (r + 1.2f) / (r + 2.8f));
                if (PlayerInBand(P, target, r + 2f, 0.8f + 0.2f)) Hurt(0.9f, target);
            }
            KillAll(marks);
            yield return Hold(0.6f);
        }

        // ------------------------------------------------------------------ guard: invoca a guarda
        IEnumerator Guard()
        {
            guards.RemoveAll(x => x == null || x.dead);
            int n = Mathf.Min(phase >= 2 ? 3 : 2, 4 - guards.Count);
            if (n <= 0 || Game.I == null) yield break;
            string id = pd.summon;
            if (string.IsNullOrEmpty(id) || GameData.Enemy(id) == null) id = "minion";
            var spots = new List<Vector3>();
            for (int i = 0; i < n; i++)
                if (FindSpot(transform.position, 2.4f, 3.6f, out var s, i * Mathf.PI * 2f / n)) spots.Add(s);
            if (spots.Count == 0) yield break;
            var marks = new List<GameObject>();
            foreach (var s in spots) marks.Add(Track(FX.Marker(s, 0.9f, new Color(col.r, col.g, col.b, 0.8f), 0.9f)));
            e.Act("Taunt", 1f);
            Sfx.Play("enemy_alert", transform.position);
            HUD.Popup(transform.position + Vector3.up * (e.Top + 0.6f), "Às armas!", col, true);
            yield return Hold(0.9f);
            KillAll(marks);
            if (e == null || e.dead || Game.I == null) yield break;
            foreach (var s in spots)
            {
                FX.Pillar(s, col, 0.8f);
                var m = Game.I.SpawnEnemy(id, s + Vector3.up * 0.1f, 1.05f);
                if (m == null) continue;
                m.customRewards = true;   // sem farm infinito
                if (m.visual != null) m.visual.SetTint(Color.Lerp(Color.white, col, 0.35f));
                m.Alert(false);
                guards.Add(m);
            }
            Sfx.Play("portal", transform.position, 0.7f);
        }

        // ------------------------------------------------------------------ spiral: chuva de orbes em espiral
        IEnumerator Spiral()
        {
            int arms = 3 + phase;
            float dur = 3f, rate = 0.14f, rot = (Random.value < 0.5f ? 1f : -1f) * 14f;
            e.Act("Cast", 0.8f);
            FX.Ring(transform.position, 2f * Sc, col, 0.7f);
            FX.Sparkle(Chest, col, 1f, 0.6f);
            Sfx.Play("magic_cast", transform.position);
            yield return Hold(0.7f * teleMult);
            if (Cut) yield break;
            float a = Random.Range(0f, 360f), t = 0f, emit = 0f;
            int shots = 0;
            while (t < dur && !Cut)
            {
                float dt = Time.deltaTime;
                t += dt;
                emit -= dt;
                if (emit <= 0f)
                {
                    emit += rate;
                    for (int k = 0; k < arms; k++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, a + k * 360f / arms, 0f) * Vector3.forward;
                        EnemyBolt.Spawn(Chest + dir * (e.radius + 0.3f), dir, 6.5f, D(0.55f), col, 3.2f, 0.34f, false);
                    }
                    a += rot;
                    if (shots++ % 3 == 0) { Sfx.Play("magic_cast", transform.position, 0.35f); e.Act("Cast", 0.5f); }
                }
                e.Drive(Vector3.zero, transform.forward);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ bonewall: muralha que fecha a arena
        IEnumerator BoneWall()
        {
            var p = P;
            if (p == null) yield break;
            Vector3 c = transform.position;
            float R = Mathf.Clamp(U.Flat(p.transform.position - c).magnitude + 3f, 7f, 10.5f);
            int n = Mathf.CeilToInt(2f * Mathf.PI * R / 1.15f);
            var spots = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Vector3 s = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * R;
                if (!EnemyAbilities.GroundAt(s, c.y, out var g)) continue;
                spots.Add(g);
            }
            if (spots.Count == 0) yield break;
            Color wc = U.Hex(string.IsNullOrEmpty(pd.wallColor) ? "e8e0c8" : pd.wallColor);
            var marks = new List<GameObject>();
            foreach (var s in spots) marks.Add(Track(FX.Marker(s, 0.55f, new Color(wc.r, wc.g, wc.b, 0.85f), 0.9f)));
            e.Act("Summon", 1.1f);
            Sfx.Play("sk_quake", c);
            HUD.Popup(transform.position + Vector3.up * (e.Top + 0.6f), "Não há saída!", col, true);
            yield return Hold(0.9f);
            KillAll(marks);
            if (e == null || e.dead) yield break;

            wall = new GameObject("muralha_chefe");
            wall.transform.SetParent(FX.Root, false);
            wall.transform.position = Vector3.zero;
            Track(wall);
            var pillars = new List<Transform>();
            var tops = new List<float>();
            var pp = P;
            foreach (var s in spots)
            {
                if (pp != null && U.Flat(pp.transform.position - s).magnitude < 1.2f) continue;   // nunca prende o herói dentro do pilar
                float h = Random.Range(2.3f, 3f);
                var pil = U.Prim(PrimitiveType.Cube, wall.transform, s + Vector3.down * h, new Vector3(0.95f, h, 0.95f), wc, true);
                pil.name = "pilar";
                pil.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 90f), Random.Range(-6f, 6f));
                pil.GetComponent<Renderer>().sharedMaterial = U.Lit(wc, 0.15f, wc * 0.15f);
                var skull = U.Prim(PrimitiveType.Sphere, pil.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.75f, 0.32f, 0.75f), wc);
                skull.GetComponent<Renderer>().sharedMaterial = U.Lit(wc, 0.3f);
                pillars.Add(pil.transform);
                tops.Add(s.y + h * 0.5f);
            }
            StartCoroutine(WallLife(wall, pillars, tops, 10f));
            FX.Ring(c, R, wc, 0.5f, 0.9f);
            if (Game.I != null) Game.I.Shake(0.3f);
            yield return Hold(0.3f);
        }

        IEnumerator WallLife(GameObject w, List<Transform> pillars, List<float> tops, float life)
        {
            float t = 0f;
            while (t < 0.45f && w != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.45f);
                for (int i = 0; i < pillars.Count; i++)
                    if (pillars[i] != null)
                    {
                        var pos = pillars[i].position;
                        float h = pillars[i].localScale.y;
                        pos.y = Mathf.Lerp(tops[i] - h, tops[i], 1f - (1f - k) * (1f - k));
                        pillars[i].position = pos;
                    }
                yield return null;
            }
            float wait = 0f;
            while (wait < life && w != null) { wait += Time.deltaTime; yield return null; }
            if (w == null) yield break;
            t = 0f;
            while (t < 0.6f && w != null)
            {
                t += Time.deltaTime;
                foreach (var pl in pillars) if (pl != null) pl.position += Vector3.down * 5f * Time.deltaTime;
                yield return null;
            }
            if (w != null) { temp.Remove(w); Destroy(w); }
            if (wall == w) wall = null;
        }

        // ------------------------------------------------------------------ clones: teleporte + ilusões
        IEnumerator Clones()
        {
            var p = P;
            if (p == null || Game.I == null) yield break;
            e.Act("Cast", 0.6f);
            FX.Burst(Chest, col, 1f, 26);
            Sfx.Play("portal", transform.position, 0.8f);
            yield return Hold(0.5f * teleMult);
            if (Cut) yield break;
            p = P;
            if (p == null) yield break;
            float a0 = Random.Range(0f, Mathf.PI * 2f);
            var spots = new List<Vector3>();
            for (int k = 0; k < 3; k++)
                if (FindSpot(p.transform.position, 5.5f, 7.5f, out var s, a0 + k * Mathf.PI * 2f / 3f)) spots.Add(s);
            if (spots.Count == 0) yield break;
            // o chefe vai para um lugar aleatório entre os pontos (o herói precisa descobrir qual é o verdadeiro)
            int real = Random.Range(0, spots.Count);
            FX.Burst(Chest, col, 1f, 24);
            e.TeleportTo(spots[real]);
            FX.Burst(spots[real] + Vector3.up, col, 1f, 24);
            FX.Ring(spots[real], 1.6f, col, 0.35f);
            float cloneScale = Mathf.Max(0.5f, e.scale);
            for (int i = 0; i < spots.Count; i++)
            {
                if (i == real) continue;
                var c = Game.I.SpawnEnemy("necro_clone", spots[i] + Vector3.up * 0.1f, cloneScale);
                if (c == null) continue;
                c.customRewards = true;
                if (c.visual != null && e.def != null && !string.IsNullOrEmpty(e.def.tint)) c.visual.SetTint(U.Hex(e.def.tint));
                c.Died += x => { FX.Burst(x.transform.position + Vector3.up, col, 1f, 20); HUD.Popup(x.transform.position + Vector3.up * 2.5f, "Ilusão!", col); };
                c.Alert(false);
                clones.Add(c);
                FX.Burst(spots[i] + Vector3.up, col, 1f, 24);
                StartCoroutine(CloneLife(c, 11f));
            }
            HUD.Popup(transform.position + Vector3.up * (e.Top + 0.6f), "Qual é o verdadeiro?", col, true);
            yield return Hold(0.5f);
        }

        IEnumerator CloneLife(Enemy c, float life)
        {
            yield return new WaitForSeconds(life);
            if (c != null && !c.dead)
            {
                FX.Burst(c.transform.position + Vector3.up, col, 0.9f, 18);
                Destroy(c.gameObject);
            }
        }

        // ------------------------------------------------------------------ drain: raio drenante
        IEnumerator Drain()
        {
            var p = P;
            if (p == null) yield break;
            if (beam == null) beam = Line("raio_drenante");
            const float maxLen = 14f;
            Vector3 dir = U.Flat(p.transform.position - transform.position).normalized;
            float tele = 0.85f * teleMult;
            e.Act("Cast", tele + 0.3f);
            Sfx.Play("magic_cast", transform.position);
            beam.enabled = true;
            float t = 0f;
            while (t < tele && !Cut)
            {
                t += Time.deltaTime;
                var pp = P;
                if (pp != null)
                {
                    Vector3 to = U.Flat(pp.transform.position - transform.position);
                    if (to.sqrMagnitude > 0.01f) dir = Vector3.RotateTowards(dir, to.normalized, 3f * Time.deltaTime, 0f);
                }
                SetBeam(dir, maxLen, 0.04f + 0.06f * t / tele, new Color(col.r, col.g, col.b, 0.3f + 0.4f * t / tele));
                e.Drive(Vector3.zero, dir);
                yield return null;
            }
            if (Cut) { beam.enabled = false; yield break; }

            Sfx.Play("lightning", transform.position);
            float dur = phase >= 2 ? 3.4f : 2.8f;
            float turn = (phase >= 2 ? 70f : 50f) * Mathf.Deg2Rad;
            float tick = 0f, crackle = 0f;
            t = 0f;
            while (t < dur && !Cut)
            {
                float dt = Time.deltaTime;
                t += dt;
                tick -= dt;
                crackle -= dt;
                var pp = P;
                if (pp != null)
                {
                    Vector3 to = U.Flat(pp.transform.position - transform.position);
                    if (to.sqrMagnitude > 0.01f) dir = Vector3.RotateTowards(dir, to.normalized, turn * dt, 0f);
                }
                float len = SetBeam(dir, maxLen, 0.45f + Mathf.Sin(t * 30f) * 0.06f, new Color(col.r, col.g, col.b, 0.85f));
                Vector3 a = Chest, b = Chest + dir * len;
                if (crackle <= 0f) { crackle = 0.15f; FX.Lightning(a, b, col, 0.08f, 1, 0.12f); }
                if (tick <= 0f && pp != null)
                {
                    tick = 0.3f;
                    if (SegDist(a, b, pp.transform.position) < 0.85f && Mathf.Abs(pp.transform.position.y - transform.position.y) < 2.5f)
                    {
                        string r = Hurt(0.4f, transform.position);
                        if (r == "hit")
                        {
                            e.Heal(D(0.4f) * 1.5f);
                            FX.Burst(pp.transform.position + Vector3.up, col, 0.6f, 10);
                        }
                    }
                }
                e.Drive(Vector3.zero, dir);
                yield return null;
            }
            beam.enabled = false;
            yield return Hold(0.4f);
        }

        float SetBeam(Vector3 dir, float maxLen, float width, Color c)
        {
            Vector3 o = Chest;
            float len = maxLen;
            if (Projectile.WallHit(o, dir, maxLen, out var hit)) len = hit.distance;
            beam.SetPosition(0, o + dir * 0.4f);
            beam.SetPosition(1, o + dir * len);
            beam.widthMultiplier = width;
            beam.startColor = c;
            beam.endColor = new Color(c.r, c.g, c.b, c.a * 0.6f);
            return len;
        }

        static float SegDist(Vector3 a, Vector3 b, Vector3 p)
        {
            a.y = 0f; b.y = 0f; p.y = 0f;
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            return Vector3.Distance(a + ab * t, p);
        }

        // ------------------------------------------------------------------ fan: leque de projéteis
        IEnumerator Fan()
        {
            int waves = phase >= 2 ? 4 : 3;
            int count = phase >= 1 ? 9 : 7;
            const float spread = 80f, speed = 11f;
            while (aimLines.Count < count) aimLines.Add(Line("mira_leque"));
            for (int w = 0; w < waves; w++)
            {
                var p = P;
                if (p == null || Cut) break;
                Vector3 dir = U.Flat(p.transform.position - transform.position);
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
                float off = (w % 2 == 1) ? spread / (count - 1) * 0.5f : 0f;
                float tele = (w == 0 ? 0.8f : 0.5f) * teleMult;
                e.Act("Cast", tele + 0.2f);
                float t = 0f;
                while (t < tele && !Cut)
                {
                    t += Time.deltaTime;
                    float k = t / tele;
                    for (int i = 0; i < count; i++)
                    {
                        var lr = aimLines[i];
                        Vector3 d = Quaternion.Euler(0f, -spread * 0.5f + off + i * spread / (count - 1), 0f) * dir;
                        Vector3 o = transform.position + Vector3.up * 0.15f;
                        lr.enabled = true;
                        lr.SetPosition(0, o + d * (e.radius + 0.3f));
                        lr.SetPosition(1, o + d * 9f);
                        lr.widthMultiplier = 0.04f + 0.06f * k;
                        var c = new Color(col.r, col.g, col.b, 0.2f + 0.5f * k);
                        lr.startColor = c;
                        lr.endColor = new Color(c.r, c.g, c.b, 0f);
                    }
                    e.Drive(Vector3.zero, dir);
                    yield return null;
                }
                foreach (var lr in aimLines) lr.enabled = false;
                if (Cut) break;
                for (int i = 0; i < count; i++)
                {
                    Vector3 d = Quaternion.Euler(0f, -spread * 0.5f + off + i * spread / (count - 1), 0f) * dir;
                    EnemyBolt.Spawn(Chest + d * (e.radius + 0.3f), d, speed, D(0.7f), col, 1.6f, 0.3f, false);
                }
                Sfx.Play("fireball", transform.position, 0.8f);
                FX.FlashLight(Chest, col, 3f, 5f, 0.2f);
                yield return Hold(0.25f);
            }
            foreach (var lr in aimLines) lr.enabled = false;
        }

        // ------------------------------------------------------------------ firegrid: zonas em xadrez
        IEnumerator FireGrid()
        {
            var p = P;
            if (p == null) yield break;
            const int N = 5;
            const float cell = 2.6f;
            Vector3 center = p.transform.position;
            int waves = phase >= 2 ? 3 : 2;
            e.Act("Cast", 1f);
            Sfx.Play("magic_cast", transform.position);
            for (int w = 0; w < waves; w++)
            {
                if (Cut) yield break;
                int parity = w % 2;
                var cells = new List<Vector3>();
                for (int i = 0; i < N; i++)
                    for (int j = 0; j < N; j++)
                    {
                        if ((i + j) % 2 != parity) continue;
                        Vector3 c = center + new Vector3((i - (N - 1) * 0.5f) * cell, 0f, (j - (N - 1) * 0.5f) * cell);
                        if (EnemyAbilities.GroundAt(c, center.y, out var g)) cells.Add(g);
                    }
                var marks = new List<GameObject>();
                foreach (var c in cells) marks.Add(Square(c, cell * 0.94f, new Color(col.r, col.g * 0.7f, col.b * 0.4f, 0.38f)));
                float tele = (w == 0 ? 1.1f : 0.9f) * teleMult;
                yield return Hold(tele);
                KillAll(marks);
                if (Cut) yield break;
                bool hit = false;
                var pp = P;
                foreach (var c in cells)
                {
                    FX.Burst(c + Vector3.up * 0.4f, col, 0.7f, 14);
                    FX.Ring(c, cell * 0.5f, col, 0.3f);
                    if (!hit && pp != null)
                    {
                        Vector3 d = pp.transform.position - c;
                        if (Mathf.Abs(d.x) <= cell * 0.5f + 0.15f && Mathf.Abs(d.z) <= cell * 0.5f + 0.15f && Mathf.Abs(d.y) < 2.5f) hit = true;
                    }
                }
                FX.FlashLight(center + Vector3.up * 1.5f, col, 5f, 10f, 0.3f);
                Sfx.Play("explosion", center, 0.8f);
                if (Game.I != null) Game.I.Shake(0.2f);
                if (hit) Hurt(1f, center);
                e.Act("Cast", 0.6f);
            }
            yield return Hold(0.4f);
        }

        // ------------------------------------------------------------------ charges: investidas em sequência
        IEnumerator Charges()
        {
            int n = 2 + phase;
            for (int i = 0; i < n; i++)
            {
                var p = P;
                if (p == null || Cut || e.cc == null || !e.cc.enabled) break;
                Vector3 dir = U.Flat(p.transform.position - transform.position);
                float len = Mathf.Min(13f, dir.magnitude + 3f);
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
                float w = 1.6f * Sc * 0.8f;
                var lane = Track(FX.FlatQuad("investida", transform.position, U.WhiteTexture(), new Color(1f, 0.2f, 0.15f, 0.3f), false));
                float tele = (i == 0 ? 0.8f : 0.55f) * teleMult;
                e.Act("Taunt", tele);
                Sfx.Play("enemy_alert", transform.position, 0.7f);
                float t = 0f;
                while (t < tele && !Cut)
                {
                    t += Time.deltaTime;
                    var pp = P;
                    if (pp != null && t < tele * 0.5f)
                    {
                        Vector3 to = U.Flat(pp.transform.position - transform.position);
                        if (to.sqrMagnitude > 0.01f) dir = Vector3.RotateTowards(dir, to.normalized, 4f * Time.deltaTime, 0f);
                    }
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    lane.transform.position = transform.position + dir * len * 0.5f + Vector3.up * 0.06f;
                    lane.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                    lane.transform.localScale = new Vector3(w, len, 1f);
                    FX.SetColor(lane.GetComponent<Renderer>(), new Color(1f, 0.2f, 0.15f, 0.2f + 0.35f * (t / tele)));
                    e.Drive(Vector3.zero, dir);
                    yield return null;
                }
                Kill(lane);
                if (Cut || e.cc == null || !e.cc.enabled) yield break;
                Sfx.Play("dash", transform.position);
                e.Act("Attack1", 0.5f);
                const float speed = 19f;
                float gone = 0f;
                bool hitP = false;
                while (gone < len && !e.dead && e.cc.enabled)
                {
                    float dt = Time.deltaTime;
                    float step = speed * dt;
                    if (Projectile.WallHit(transform.position + Vector3.up * 0.8f, dir, step + e.radius + 0.25f, out _))
                    {
                        FX.Dust(transform.position + dir * e.radius, U.Hex("8a7a6a"), 1.5f);
                        Sfx.Play("hit_heavy", transform.position);
                        if (Game.I != null) Game.I.Shake(0.3f);
                        break;
                    }
                    e.cc.Move(dir * step + Vector3.down * 2f * dt);
                    gone += step;
                    if (Random.value < 0.4f) FX.Dust(transform.position, U.Hex("a09080"), 0.6f);
                    var pp = FloorRoot.LivePlayer();
                    if (!hitP && pp != null && U.Flat(pp.transform.position - transform.position).magnitude < 1.2f + e.radius)
                    {
                        hitP = true;
                        Hurt(1.3f, transform.position - dir);
                        if (Game.I != null) Game.I.Shake(0.3f);
                    }
                    e.Drive(Vector3.zero, dir);
                    yield return null;
                }
                yield return Hold(0.25f, false);
            }
            yield return Hold(0.7f, false);   // cansado: janela para punir
        }

        // ------------------------------------------------------------------ rain: estacas de gelo / meteoros
        IEnumerator Rain(int n, float rad, Color c, bool fire)
        {
            var p = P;
            if (p == null) yield break;
            float fall = 1.15f * teleMult;
            var centers = new List<Vector3> { p.transform.position };
            for (int i = 1; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle * 4.8f;
                centers.Add(p.transform.position + new Vector3(r.x, 0f, r.y));
            }
            e.Act("Cast", 0.8f);
            Sfx.Play(fire ? "fireball" : "ice", transform.position);
            var marks = new List<GameObject>();
            var drops = new List<GameObject>();
            foreach (var ce in centers)
            {
                marks.Add(Track(FX.Marker(ce, rad, new Color(c.r, c.g, c.b, 0.85f), fall)));
                GameObject d;
                if (fire)
                {
                    d = U.Prim(PrimitiveType.Sphere, FX.Root, ce + Vector3.up * 10f, Vector3.one * 0.9f, c);
                    d.GetComponent<Renderer>().sharedMaterial = U.Lit(c, 0.4f, c * 2.2f);
                }
                else
                {
                    d = U.Prim(PrimitiveType.Cube, FX.Root, ce + Vector3.up * 10f, new Vector3(0.35f, 1.5f, 0.35f), c);
                    d.GetComponent<Renderer>().sharedMaterial = U.Lit(c, 0.9f, c * 0.6f);
                    d.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 45f * 0.2f);
                }
                d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                drops.Add(Track(d));
            }
            float t = 0f;
            while (t < fall && e != null && !e.dead)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fall);
                for (int i = 0; i < drops.Count; i++)
                    if (drops[i] != null) drops[i].transform.position = centers[i] + Vector3.up * Mathf.Lerp(10f, 0.5f, k * k);
                if (!Cut) e.Drive(Vector3.zero, p != null ? p.transform.position - transform.position : transform.forward);
                yield return null;
            }
            KillAll(marks);
            bool hit = false;
            var pp = P;
            for (int i = 0; i < centers.Count; i++)
            {
                Vector3 ce = centers[i];
                FX.Burst(ce + Vector3.up * 0.3f, c, 1f, 18);
                FX.Dust(ce, fire ? U.Hex("5a4a3a") : U.Hex("dff6ff"), 1.2f);
                if (drops[i] != null) Kill(drops[i]);
                if (!hit && PlayerIn(pp, ce, rad + 0.3f)) { hit = true; Hurt(1.1f, ce); }
                if (fire) FireZone.Spawn(ce, rad * 0.8f, D(0.5f), 2.5f, 0.2f);
            }
            drops.Clear();
            Sfx.Play(fire ? "explosion" : "ice", centers[0]);
            if (Game.I != null) Game.I.Shake(0.25f);
            yield return Hold(0.3f);
        }

        // ------------------------------------------------------------------ nova: anéis de projéteis
        IEnumerator Nova()
        {
            int waves = phase >= 2 ? 4 : 3;
            const int count = 16;
            var mk = Track(FX.Marker(transform.position, 1.8f * Sc, new Color(col.r, col.g, col.b, 0.8f), 0.6f * teleMult));
            e.Act("Cast", 0.8f);
            Sfx.Play("magic_cast", transform.position);
            yield return Hold(0.6f * teleMult);
            Kill(mk);
            for (int w = 0; w < waves && !Cut; w++)
            {
                float off = w * (180f / count);
                for (int k = 0; k < count; k++)
                {
                    Vector3 d = Quaternion.Euler(0f, off + k * 360f / count, 0f) * Vector3.forward;
                    EnemyBolt.Spawn(Chest + d * (e.radius + 0.3f), d, 8f, D(0.6f), col, 2.6f, 0.32f, false);
                }
                FX.Ring(transform.position, 2.2f * Sc, col, 0.3f);
                Sfx.Play("ice", transform.position, 0.7f);
                e.Act("Cast", 0.4f);
                yield return Hold(0.45f);
            }
        }

        // ------------------------------------------------------------------ plague: poças de veneno
        IEnumerator Plague()
        {
            var p = P;
            if (p == null) yield break;
            int n = 3 + phase;
            var centers = new List<Vector3> { p.transform.position };
            for (int i = 1; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle * 5f;
                Vector3 c = p.transform.position + new Vector3(r.x, 0f, r.y);
                if (EnemyAbilities.GroundAt(c, p.transform.position.y, out var g)) centers.Add(g);
            }
            var marks = new List<GameObject>();
            foreach (var c in centers) marks.Add(Track(FX.Marker(c, 2f, new Color(0.45f, 1f, 0.3f, 0.8f), 1f * teleMult)));
            e.Act("Cast", 1f);
            Sfx.Play("magic_cast", transform.position);
            yield return Hold(1f * teleMult);
            KillAll(marks);
            if (e == null || e.dead) yield break;
            foreach (var c in centers)
            {
                FX.Burst(c + Vector3.up * 0.3f, U.Hex("7aff5a"), 1f, 18);
                PoisonFog.SpawnPuddle(c, 2f, D(0.45f), 6f);
            }
            if (PlayerIn(P, centers[0], 2.3f)) Hurt(0.6f, centers[0]);
            yield return Hold(0.3f);
        }

        // ------------------------------------------------------------------ limpeza
        void Cleanup()
        {
            if (beam != null) beam.enabled = false;
            foreach (var lr in aimLines) if (lr != null) lr.enabled = false;
            foreach (var g in temp.ToArray()) if (g != null) Destroy(g);
            temp.Clear();
            wall = null;
            foreach (var c in clones)
                if (c != null && !c.dead) { FX.Burst(c.transform.position + Vector3.up, col, 0.9f, 18); Destroy(c.gameObject); }
            clones.Clear();
            if (aura != null) { var em = aura.emission; em.enabled = false; }
        }

        void OnDestroy()
        {
            foreach (var g in temp) if (g != null) Destroy(g);
            temp.Clear();
        }
    }

    /// <summary>Destrói a malha gerada junto com o objeto.</summary>
    public class MeshOwner : MonoBehaviour
    {
        public Mesh mesh;
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }

    // =====================================================================================
    //  [Inimigos] Projétil simples dos inimigos/chefes (velocidade, cor e vida controladas)
    // =====================================================================================
    /// <summary>Projétil de inimigo independente do Projectile (velocidade/alcance livres para padrões de chefe).</summary>
    public class EnemyBolt : MonoBehaviour
    {
        Vector3 dir;
        float speed, life, dmg;
        bool done;
        Color color;
        TrailRenderer trail;

        public static EnemyBolt Spawn(Vector3 pos, Vector3 dir, float speed, float dmg, Color c, float life = 3f, float size = 0.32f, bool streak = false)
        {
            dir = U.Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            var go = new GameObject("bolt_inimigo");
            go.transform.SetParent(FX.Root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
            var core = U.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, streak ? new Vector3(size * 0.6f, size * 0.6f, size * 2.8f) : Vector3.one * size, c);
            var r = core.GetComponent<Renderer>();
            r.sharedMaterial = U.Lit(c, 0.5f, c * 2.4f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var b = go.AddComponent<EnemyBolt>();
            b.dir = dir; b.speed = Mathf.Max(0.5f, speed); b.dmg = Mathf.Max(1f, dmg); b.color = c; b.life = Mathf.Max(0.1f, life);
            b.trail = go.AddComponent<TrailRenderer>();
            b.trail.sharedMaterial = U.Fx(true);
            b.trail.time = streak ? 0.12f : 0.2f;
            b.trail.widthMultiplier = size * (streak ? 0.6f : 0.8f);
            b.trail.startColor = new Color(c.r, c.g, c.b, 0.8f);
            b.trail.endColor = new Color(c.r, c.g, c.b, 0f);
            b.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.trail.receiveShadows = false;
            return b;
        }

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            Vector3 prev = transform.position;
            float step = speed * dt;
            if (Projectile.WallHit(prev, dir, step + 0.1f, out RaycastHit wh))
            {
                transform.position = wh.point - dir * 0.1f;
                Pop(true);
                return;
            }
            Vector3 next = prev + dir * step;
            transform.position = next;

            var g = Game.I;
            var p = g != null ? g.player : null;
            if (p != null && p.state != "dead")
            {
                float py = p.transform.position.y;
                if (next.y >= py - 0.5f && next.y <= py + 2.4f && SegDist(prev, next, p.transform.position) <= 0.75f)
                {
                    string r = p.TakeDamage(dmg, next - dir * 2f);
                    switch (r)
                    {
                        case "ignored": break;   // esquiva: passa direto
                        case "parried": Sfx.Play("parry", next); Pop(true); return;
                        case "blocked": Sfx.Play("block", next); FX.Burst(next, Color.white, 0.5f, 10); Pop(false); return;
                        default: Sfx.Play("player_hurt", next); Pop(true); return;
                    }
                }
            }
            life -= dt;
            if (life <= 0f) Pop(false);
        }

        static float SegDist(Vector3 a, Vector3 b, Vector3 p)
        {
            a.y = 0f; b.y = 0f; p.y = 0f;
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            return Vector3.Distance(a + ab * t, p);
        }

        void Pop(bool burst)
        {
            if (done) return;
            done = true;
            if (burst) FX.Burst(transform.position, color, 0.5f, 10);
            if (trail != null)
            {
                trail.transform.SetParent(FX.Root, true);
                trail.emitting = false;
            }
            Destroy(gameObject, trail != null ? trail.time + 0.05f : 0f);
            foreach (Transform c in transform) c.gameObject.SetActive(false);
        }
    }

    // =====================================================================================
    //  [Inimigos] Chão em chamas (inimigos de fogo ao morrer, meteoros)
    // =====================================================================================
    public class FireZone : MonoBehaviour
    {
        float radius, dps, life, delay, age, tick;
        ParticleSystem flames;
        GameObject disc;
        bool ending;

        public static FireZone Spawn(Vector3 pos, float radius, float dps, float life, float delay = 0.4f)
        {
            var go = new GameObject("Chao_em_chamas");
            Transform parent = Game.I != null && Game.I.levelRoot != null ? Game.I.levelRoot : FX.Root;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var f = go.AddComponent<FireZone>();
            f.radius = Mathf.Max(0.5f, radius); f.dps = Mathf.Max(0f, dps); f.life = Mathf.Max(0.5f, life); f.delay = Mathf.Max(0f, delay);
            f.Build();
            return f;
        }

        void Build()
        {
            disc = FX.FlatQuad("fogo_chao", transform.position + Vector3.up * 0.05f, U.SoftTexture(), new Color(1f, 0.4f, 0.1f, 0.6f), true);
            disc.transform.SetParent(transform, true);
            disc.transform.localScale = Vector3.one * radius * 2.3f;
            flames = LevelDecor.Flame(transform, new Vector3(0f, 0.1f, 0f), 1.4f, U.Hex("ff7a2a"), Mathf.Clamp(Mathf.RoundToInt(radius * 14f), 10, 40));
            var sh = flames.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = radius * 0.85f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            FX.FlashLight(transform.position + Vector3.up, U.Hex("ff8a2a"), 3f, 5f, 0.4f);
        }

        void OnDestroy()
        {
            if (disc != null) Destroy(disc);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (ending) return;
            if (age >= delay + life)
            {
                ending = true;
                if (flames != null) { var em = flames.emission; em.enabled = false; }
                if (disc != null) disc.SetActive(false);
                Destroy(gameObject, 1f);
                return;
            }
            if (age < delay) return;
            tick -= dt;
            if (tick > 0f) return;
            tick = 0.5f;
            var p = FloorRoot.LivePlayer();
            if (p == null) return;
            Vector3 d = p.transform.position - transform.position;
            if (U.Flat(d).magnitude > radius || Mathf.Abs(d.y) > 2.5f) return;
            string r = FloorRoot.HurtPlayer(dps * 0.5f, transform.position);
            if (r == "hit") HUD.Popup(p.transform.position + Vector3.up * 2.6f, "queimando", U.Hex("ff8a2a"));
        }
    }
}
