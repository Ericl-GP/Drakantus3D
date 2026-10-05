using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// [Classes] Visual de status preso a um personagem (inimigo ou herói). Um visual por tipo; Attach renova.
    /// Tipos: poison, burn, chill, freeze, bleed, root_shadow, stun, blind, mark, frenzy, holy_shield, guard_buff,
    /// bless, overload, mana_shield, stealth, smoke, reflect (+ apelidos: slow, fire, ice, frozen, root, shield...).
    /// Tipos desconhecidos viram um brilho genérico (nunca lança exceção).
    /// </summary>
    public static class StatusFX
    {
        /// <summary>Liga (ou renova) o visual. duration = tempo restante; height = altura do personagem.</summary>
        public static void Attach(Transform target, string kind, float duration, float height = 1.8f)
        {
            if (target == null || string.IsNullOrEmpty(kind)) return;
            try
            {
                var host = target.GetComponent<StatusFxHost>();
                if (host == null) host = target.gameObject.AddComponent<StatusFxHost>();
                host.Attach(Normalize(kind), duration, height);
            }
            catch (System.Exception ex) { Debug.LogWarning("[StatusFX] " + kind + ": " + ex.Message); }
        }

        /// <summary>Tira um visual (com fade).</summary>
        public static void Detach(Transform target, string kind)
        {
            if (target == null || string.IsNullOrEmpty(kind)) return;
            try
            {
                string k = Normalize(kind);
                // a aura da Fúria é a mesma do buff Rugido: não apaga enquanto o Berserker ainda está em Fúria
                if (k == "frenzy")
                {
                    var p = target.GetComponent<Player>();
                    if (p != null && p.Frenzy) return;
                }
                var host = target.GetComponent<StatusFxHost>();
                if (host != null) host.Detach(k);
            }
            catch (System.Exception ex) { Debug.LogWarning("[StatusFX] detach " + kind + ": " + ex.Message); }
        }

        /// <summary>Tira todos os visuais do alvo.</summary>
        public static void Detach(Transform target)
        {
            if (target == null) return;
            try
            {
                var host = target.GetComponent<StatusFxHost>();
                if (host != null) host.DetachAll();
            }
            catch (System.Exception ex) { Debug.LogWarning("[StatusFX] detach all: " + ex.Message); }
        }

        public static bool Has(Transform target, string kind)
        {
            if (target == null || string.IsNullOrEmpty(kind)) return false;
            var host = target.GetComponent<StatusFxHost>();
            return host != null && host.Has(Normalize(kind));
        }

        static string Normalize(string kind)
        {
            string k = kind.Trim().ToLowerInvariant();
            switch (k)
            {
                case "slow": case "cold": case "frost": case "ice": return "chill";
                case "frozen": return "freeze";
                case "fire": case "ignite": case "burning": return "burn";
                case "venom": case "toxic": return "poison";
                case "blood": return "bleed";
                case "root": case "rooted": case "shadow_root": case "chains": return "root_shadow";
                case "shield": case "aegis": return "holy_shield";
                case "guard": case "shieldwall": case "banner": case "ironstance": return "guard_buff";
                case "blessing": case "buff": return "bless";
                case "manashield": return "mana_shield";
                case "invisible": case "cloak": return "stealth";
                case "rage": case "fury": return "frenzy";
                case "deathmark": case "marked": return "mark";
                case "dazed": case "stunned": return "stun";
                default: return k;
            }
        }
    }

    /// <summary>Guarda os visuais de status ativos de um personagem.</summary>
    public class StatusFxHost : MonoBehaviour
    {
        readonly Dictionary<string, StatusFxInst> active = new();
        static readonly List<string> keys = new();

        public bool Has(string kind) => active.TryGetValue(kind, out var i) && i != null && !i.Ending;

        public void Attach(string kind, float duration, float height)
        {
            duration = Mathf.Max(0.05f, duration);
            if (active.TryGetValue(kind, out var inst) && inst != null && !inst.Ending)
            {
                inst.Renew(duration);
                return;
            }
            var go = new GameObject("status_" + kind);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            inst = go.AddComponent<StatusFxInst>();
            float h = Mathf.Max(0.8f, height);
            var p = GetComponent<Player>();
            if (p != null && p.visual != null) h *= Mathf.Clamp(p.visual.transform.localScale.y, 0.7f, 1.6f);
            inst.Init(kind, duration, h, p != null);
            active[kind] = inst;
        }

        public void Detach(string kind)
        {
            if (active.TryGetValue(kind, out var inst))
            {
                active.Remove(kind);
                if (inst != null) inst.End();
            }
        }

        public void DetachAll()
        {
            keys.Clear();
            foreach (var k in active.Keys) keys.Add(k);
            foreach (var k in keys) Detach(k);
        }

        void LateUpdate()
        {
            if (active.Count == 0) return;
            keys.Clear();
            foreach (var kv in active) if (kv.Value == null || kv.Value.Ending) keys.Add(kv.Key);
            foreach (var k in keys) active.Remove(k);
        }
    }

    /// <summary>Um visual de status: partículas/luzes/malhas filhas, pulsos, giro, fim com fade.</summary>
    public class StatusFxInst : MonoBehaviour
    {
        public string kind;
        public float remaining, height;
        public bool Ending { get; private set; }

        readonly List<ParticleSystem> systems = new();
        readonly List<Renderer> rends = new();
        readonly List<Color> rendCols = new();
        readonly List<Light> lights = new();
        readonly List<float> lightI = new();
        readonly List<Transform> spins = new();
        readonly List<float> spinSpeeds = new();
        readonly List<Pulse> pulses = new();
        Transform grow; float growT = -1f, growTime = 0.15f;
        Transform bob; Vector3 bobBase;
        public float tickEvery;
        public System.Action onTick, onEnd;
        float tickT, fade = 1f, age;
        const float FadeTime = 0.3f;

        struct Pulse { public Renderer r; public Color c; public float baseScale, amount, speed, alphaAmt; public Transform t; }

        public void Init(string kind, float duration, float h, bool isPlayer)
        {
            this.kind = kind; remaining = duration; height = h;
            StatusLooks.Build(this, kind, h, isPlayer);
        }

        public void Renew(float duration) { remaining = Mathf.Max(remaining, duration); }

        // ------------------------------------------------------------------ registro (usado pelos construtores)
        public ParticleSystem AddSystem(ParticleSystem ps) { if (ps != null) systems.Add(ps); return ps; }
        public void AddRenderer(Renderer r, Color c) { if (r == null) return; rends.Add(r); rendCols.Add(c); FxUtil.Tint(r, c); }
        public void AddLight(Light l) { if (l == null) return; lights.Add(l); lightI.Add(l.intensity); }
        public void AddSpin(Transform t, float degPerSec) { if (t == null) return; spins.Add(t); spinSpeeds.Add(degPerSec); }
        public void AddPulse(Renderer r, Color c, float baseScale, float amount, float speed, float alphaAmt = 0.4f)
        {
            if (r == null) return;
            pulses.Add(new Pulse { r = r, c = c, baseScale = baseScale, amount = amount, speed = speed, alphaAmt = alphaAmt, t = r.transform });
        }
        public void SetGrow(Transform t, float time = 0.15f) { grow = t; growT = 0f; growTime = Mathf.Max(0.02f, time); if (t != null) t.localScale = Vector3.one * 0.2f; }
        public void SetBob(Transform t) { bob = t; if (t != null) bobBase = t.localPosition; }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (!Ending)
            {
                remaining -= dt;
                if (remaining <= 0f) { End(); return; }
                if (tickEvery > 0f && onTick != null)
                {
                    tickT += dt;
                    if (tickT >= tickEvery) { tickT = 0f; try { onTick(); } catch (System.Exception e) { Debug.LogWarning("[StatusFX] tick: " + e.Message); onTick = null; } }
                }
            }
            else
            {
                fade -= dt / FadeTime;
                if (fade <= 0f) fade = 0f;
            }
            if (grow != null && growT >= 0f)
            {
                growT += dt;
                float k = Mathf.Clamp01(growT / growTime);
                float e = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);     // leve "estalo" ao crescer
                grow.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, 1f - (1f - k) * (1f - k)) * e;
                if (k >= 1f) { grow.localScale = Vector3.one; growT = -1f; }
            }
            if (bob != null) bob.localPosition = bobBase + Vector3.up * (Mathf.Sin(age * 3f) * 0.06f);
            for (int i = 0; i < spins.Count; i++) if (spins[i] != null) spins[i].Rotate(Vector3.up, spinSpeeds[i] * dt, Space.World);
            float fin = Mathf.Clamp01(age / 0.12f) * fade;   // fade-in curto + fade-out
            for (int i = 0; i < rends.Count; i++)
            {
                if (rends[i] == null) continue;
                var c = rendCols[i]; c.a *= fin;
                FxUtil.Tint(rends[i], c);
            }
            for (int i = 0; i < pulses.Count; i++)
            {
                var p = pulses[i];
                if (p.r == null) continue;
                float s = Mathf.Sin(age * p.speed);
                if (p.amount != 0f) p.t.localScale = Vector3.one * p.baseScale * (1f + p.amount * s);
                var c = p.c; c.a *= (1f - p.alphaAmt * 0.5f + p.alphaAmt * 0.5f * s) * fin;
                FxUtil.Tint(p.r, c);
            }
            for (int i = 0; i < lights.Count; i++) if (lights[i] != null && Ending) lights[i].intensity = lightI[i] * fade;
            if (Ending && fade <= 0f) FinishDestroy();
        }

        bool destroyed;
        void FinishDestroy()
        {
            if (destroyed) return;
            destroyed = true;
            // as partículas que já saíram continuam no mundo até acabarem
            for (int i = 0; i < systems.Count; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                try { ps.transform.SetParent(FX.Root, true); } catch { }
                Destroy(ps.gameObject, 1.6f);
            }
            Destroy(gameObject);
        }

        /// <summary>Encerra: para de emitir, apaga com fade e se destrói.</summary>
        public void End()
        {
            if (Ending) return;
            Ending = true;
            for (int i = 0; i < systems.Count; i++) if (systems[i] != null) systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            for (int i = 0; i < lights.Count; i++) if (lights[i] != null) { var fl = lights[i].GetComponent<FxFlicker>(); if (fl != null) fl.enabled = false; }
            var e = onEnd; onEnd = null; onTick = null;
            if (e != null) { try { e(); } catch (System.Exception ex) { Debug.LogWarning("[StatusFX] end: " + ex.Message); } }
        }
    }

    /// <summary>Correntes animadas (LineRenderer com textura de elos) saindo do chão e prendendo o alvo.</summary>
    public class ChainBind : MonoBehaviour
    {
        class Chain
        {
            public LineRenderer links, glow;
            public Vector3 anchor, end, ctrl;
            public Vector3[] pts;
            public float phase, delay;
        }
        readonly List<Chain> chains = new();
        float t, rise = 0.18f;
        StatusFxInst inst;
        Color linkCol, glowCol;
        const int Seg = 9;

        public void Setup(StatusFxInst owner, int count, float radius, float h, float width, Color link, Color glow)
        {
            inst = owner; linkCol = link; glowCol = glow;
            float a0 = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float a = (a0 + i * 360f / count + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 side = new Vector3(-d.z, 0f, d.x);
                var c = new Chain
                {
                    anchor = d * radius + Vector3.up * 0.02f,
                    end = d * radius * 0.12f + Vector3.up * h * Random.Range(0.35f, 0.62f),
                    phase = Random.value * 10f,
                    delay = i * 0.04f,
                    pts = new Vector3[Seg + 1]
                };
                // ponto de controle "enrolando" em volta do corpo
                c.ctrl = d * radius * 0.55f + side * radius * 0.55f * (i % 2 == 0 ? 1f : -1f) + Vector3.up * h * 0.5f;
                c.glow = MakeLine("corrente_brilho", width * 2.6f, true, FxTex.Band(), glow);
                c.links = MakeLine("corrente", width, false, FxTex.Chain(), link);
                c.links.textureMode = LineTextureMode.RepeatPerSegment;
                chains.Add(c);
                Apply(c);
            }
        }

        LineRenderer MakeLine(string name, float width, bool additive, Texture tex, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = Seg + 1;
            lr.widthMultiplier = width;
            lr.sharedMaterial = U.Fx(additive, tex);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 0;
            lr.startColor = c; lr.endColor = c;
            return lr;
        }

        void Update()
        {
            t += Time.deltaTime;
            float a = inst != null && inst.Ending ? 0f : 1f;
            fadeA = Mathf.MoveTowards(fadeA, a, Time.deltaTime / 0.3f);
            for (int i = 0; i < chains.Count; i++) Apply(chains[i]);
        }

        float fadeA = 1f;
        void Apply(Chain c)
        {
            float g = Mathf.Clamp01((t - c.delay) / rise);
            g = 1f - (1f - g) * (1f - g);
            float shiver = Mathf.Sin(t * 23f + c.phase) * 0.035f;
            Vector3 ctrl = c.ctrl + new Vector3(shiver, Mathf.Sin(t * 3f + c.phase) * 0.05f, -shiver);
            for (int j = 0; j <= Seg; j++)
            {
                float u = (float)j / Seg * g;
                float iu = 1f - u;
                c.pts[j] = iu * iu * c.anchor + 2f * iu * u * ctrl + u * u * c.end;
            }
            c.links.SetPositions(c.pts);
            c.glow.SetPositions(c.pts);
            float al = fadeA * (g > 0f ? 1f : 0f);
            var lc = linkCol; lc.a *= al; c.links.startColor = lc; c.links.endColor = lc;
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 6f + c.phase);
            var gc = glowCol; gc.a *= al * pulse; c.glow.startColor = gc; var ge = gc; ge.a *= 0.5f; c.glow.endColor = ge;
        }
    }

    /// <summary>Construtores dos visuais de status.</summary>
    static class StatusLooks
    {
        static readonly Color Venom = new Color(0.5f, 1f, 0.28f);
        static readonly Color VenomDeep = new Color(0.18f, 0.6f, 0.12f);
        static readonly Color ShadowPurple = new Color(0.58f, 0.28f, 1f);

        public static void Build(StatusFxInst inst, string kind, float h, bool isPlayer)
        {
            float s = Mathf.Clamp(h / 1.8f, 0.7f, 2.6f);
            Transform t = inst.transform;
            switch (kind)
            {
                case "poison": Poison(inst, t, h, s, isPlayer); break;
                case "burn": Burn(inst, t, h, s); break;
                case "chill": Chill(inst, t, h, s); break;
                case "freeze": Freeze(inst, t, h, s); break;
                case "bleed": Bleed(inst, t, h, s); break;
                case "root_shadow": RootShadow(inst, t, h, s); break;
                case "stun": Stun(inst, t, h, s); break;
                case "blind": Blind(inst, t, h, s); break;
                case "mark": Mark(inst, t, h, s); break;
                case "frenzy": Frenzy(inst, t, h, s); break;
                case "holy_shield": Dome(inst, t, h, s, SkillFX.Gold, SkillFX.Holy, true); break;
                case "mana_shield": Dome(inst, t, h, s, new Color(0.45f, 0.62f, 1f), SkillFX.Arcane, false); break;
                case "guard_buff": GuardBuff(inst, t, h, s); break;
                case "bless": Bless(inst, t, h, s, SkillFX.Gold); break;
                case "overload": Overload(inst, t, h, s); break;
                case "stealth": Stealth(inst, t, h, s, isPlayer); break;
                case "smoke": SmokeAura(inst, t, h, s); break;
                case "reflect": Reflect(inst, t, h, s); break;
                default: Bless(inst, t, h, s, new Color(0.9f, 0.9f, 1f)); break;
            }
        }

        // ------------------------------------------------------------------ utilitários
        static ParticleSystem Loop(StatusFxInst inst, string name, Vector3 local, Color c, bool additive,
                                   float life0, float life1, float spd0, float spd1, float sz0, float sz1, float gravity, float rate, int max = 80)
        {
            var ps = FxUtil.LoopSys(name, inst.transform, local, c, additive, life0, life1, spd0, spd1, sz0, sz1, gravity, rate, max);
            inst.AddSystem(ps);
            return ps;
        }

        static GameObject Flat(StatusFxInst inst, string name, Texture tex, Color c, bool additive, float size, float y = 0.04f)
        {
            var q = FX.FlatQuad(name, inst.transform.position + Vector3.up * y, tex, c, additive);
            q.transform.SetParent(inst.transform, true);
            q.transform.localScale = Vector3.one * size;
            return q;
        }

        static Light AddLight(StatusFxInst inst, Vector3 local, Color c, float intensity, float range, float flicker)
        {
            var lg = new GameObject("luz_status");
            lg.transform.SetParent(inst.transform, false);
            lg.transform.localPosition = local;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            if (flicker > 0f) { var f = lg.AddComponent<FxFlicker>(); f.baseIntensity = intensity; f.amount = flicker; f.speed = 11f; }
            inst.AddLight(l);
            return l;
        }

        static Vector3 WorldAt(StatusFxInst inst, float y) => inst != null ? inst.transform.position + Vector3.up * y : Vector3.zero;

        // ------------------------------------------------------------------ veneno: bolhas, gotas, névoa verde
        static void Poison(StatusFxInst inst, Transform t, float h, float s, bool isPlayer)
        {
            var bub = Loop(inst, "veneno_bolhas", new Vector3(0f, h * (isPlayer ? 0.5f : 0.45f), 0f), Venom, true, 0.6f, 1.1f, 0.15f, 0.5f, 0.07f * s, 0.16f * s, -0.25f, isPlayer ? 8f : 13f);
            FxUtil.Sphere(bub, 0.35f * s);
            bub.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, U.RingTexture());
            SkillFX.Noise(bub, 0.35f, 1.4f);
            SkillFX.Grad(bub, new Color(0.85f, 1f, 0.7f), Venom, 0.9f);
            bub.Play();

            var drip = Loop(inst, "veneno_gotas", new Vector3(0f, h * 0.55f, 0f), Venom, true, 0.35f, 0.6f, 0f, 0.25f, 0.05f * s, 0.08f * s, 1.3f, isPlayer ? 9f : 6f);
            FxUtil.Sphere(drip, 0.32f * s);
            SkillFX.Stretch(drip, 1.2f, 0.08f);
            SkillFX.Grad(drip, new Color(0.8f, 1f, 0.6f), VenomDeep, 1f);
            drip.Play();

            if (!isPlayer)
            {
                var mist = Loop(inst, "veneno_nevoa", new Vector3(0f, 0.25f, 0f), new Color(0.4f, 0.8f, 0.2f, 0.35f), false, 1f, 1.6f, 0.1f, 0.35f, 0.5f * s, 0.9f * s, -0.04f, 5f, 30);
                FxUtil.FlatRing(mist, 0.5f * s);
                SkillFX.Noise(mist, 0.3f, 0.8f);
                FxUtil.AlphaFade(mist, new Color(0.55f, 0.95f, 0.3f), VenomDeep, 0.35f);
                mist.Play();
                var pool = Flat(inst, "veneno_poca", U.SoftTexture(), new Color(0.3f, 0.75f, 0.15f, 0.35f), true, 1.5f * s);
                inst.AddPulse(pool.GetComponent<Renderer>(), new Color(0.3f, 0.75f, 0.15f, 0.35f), 1.5f * s, 0.08f, 3.2f);
            }
            else
            {
                // lâminas envenenadas: brilho verde nas mãos
                var glow = Loop(inst, "veneno_laminas", new Vector3(0f, h * 0.5f, 0f), Venom, true, 0.25f, 0.45f, 0.1f, 0.4f, 0.12f, 0.22f, -0.1f, 14f);
                FxUtil.FlatRing(glow, 0.45f * s);
                SkillFX.Grad(glow, Color.white, Venom, 0.7f);
                glow.Play();
            }
            inst.tickEvery = 0.5f;
            inst.onTick = () => { if (bub != null && !isPlayer) bub.Emit(3); };
        }

        // ------------------------------------------------------------------ queimando: chamas em camadas, brasas, fumaça, luz tremulando
        static void Burn(StatusFxInst inst, Transform t, float h, float s)
        {
            var fl = Loop(inst, "fogo_chamas", new Vector3(0f, 0.15f, 0f), SkillFX.Fire, true, 0.35f, 0.7f, 1.2f * s, 2.4f * s, 0.22f * s, 0.48f * s, -0.2f, 26f);
            FxUtil.ConeUp(fl, 12f, 0.35f * s);
            SkillFX.Noise(fl, 0.6f, 2f);
            FxUtil.FireGrad(fl);
            fl.Play();
            var core = Loop(inst, "fogo_nucleo", new Vector3(0f, h * 0.35f, 0f), SkillFX.FireCore, true, 0.2f, 0.4f, 0.4f, 1f, 0.15f * s, 0.3f * s, -0.4f, 14f);
            FxUtil.Sphere(core, 0.25f * s);
            SkillFX.Grad(core, Color.white, SkillFX.Fire, 0.8f);
            core.Play();
            var emb = Loop(inst, "fogo_brasas", new Vector3(0f, h * 0.4f, 0f), SkillFX.Ember, true, 0.6f, 1.1f, 0.6f, 2f, 0.03f, 0.07f, -0.35f, 10f);
            FxUtil.Sphere(emb, 0.35f * s);
            SkillFX.Noise(emb, 1.2f, 1.5f);
            SkillFX.Grad(emb, SkillFX.FireCore, SkillFX.Ember, 1f);
            emb.Play();
            var sm = Loop(inst, "fogo_fumaca", new Vector3(0f, h * 0.85f, 0f), new Color(0.15f, 0.12f, 0.1f, 0.5f), false, 0.9f, 1.4f, 0.4f, 0.9f, 0.4f * s, 0.8f * s, -0.15f, 4f, 20);
            FxUtil.Sphere(sm, 0.2f * s);
            FxUtil.AlphaFade(sm, new Color(0.3f, 0.2f, 0.15f), new Color(0.1f, 0.1f, 0.1f), 0.5f);
            sm.Play();
            AddLight(inst, new Vector3(0f, h * 0.5f, 0f), SkillFX.Fire, 1.3f, 3f * s, 0.45f);
        }

        // ------------------------------------------------------------------ frio/lentidão: névoa, flocos, geada no chão
        static void Chill(StatusFxInst inst, Transform t, float h, float s)
        {
            var mist = Loop(inst, "frio_nevoa", new Vector3(0f, 0.2f, 0f), new Color(0.75f, 0.92f, 1f, 0.35f), false, 0.8f, 1.4f, 0.1f, 0.35f, 0.4f * s, 0.8f * s, -0.02f, 8f, 30);
            FxUtil.FlatRing(mist, 0.55f * s);
            SkillFX.Noise(mist, 0.25f, 0.8f);
            FxUtil.AlphaFade(mist, Color.white, SkillFX.Ice, 0.35f);
            mist.Play();
            var snow = Loop(inst, "frio_flocos", new Vector3(0f, h + 0.15f, 0f), new Color(0.85f, 0.95f, 1f), true, 0.9f, 1.4f, 0f, 0.15f, 0.04f, 0.08f, 0.15f, 10f);
            FxUtil.FlatRing(snow, 0.5f * s, 1f);
            SkillFX.Noise(snow, 0.3f, 1f);
            SkillFX.Grad(snow, Color.white, SkillFX.Ice, 1f);
            snow.Play();
            var frost = Flat(inst, "frio_geada", U.RingTexture(), new Color(0.6f, 0.88f, 1f, 0.5f), true, 1.6f * s);
            inst.AddPulse(frost.GetComponent<Renderer>(), new Color(0.6f, 0.88f, 1f, 0.5f), 1.6f * s, 0.05f, 2.5f);
        }

        // ------------------------------------------------------------------ congelado: casca de cristais de gelo que estilhaça no fim
        static void Freeze(StatusFxInst inst, Transform t, float h, float s)
        {
            var shell = new GameObject("gelo_casca").transform;
            shell.SetParent(t, false);
            Color iceA = new Color(0.7f, 0.9f, 1f, 0.5f);
            Color iceGlow = new Color(0.55f, 0.85f, 1f, 0.35f);
            int n = 7;
            for (int i = 0; i < n; i++)
            {
                float a = (i * 360f / n + Random.Range(-14f, 14f)) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float len = h * Random.Range(0.55f, 0.85f);
                var sh = FxUtil.Shape(PrimitiveType.Cube, shell, d * 0.42f * s + Vector3.up * len * 0.45f, new Vector3(0.22f * s, len, 0.2f * s));
                sh.transform.localRotation = Quaternion.AngleAxis(Random.Range(10f, 22f), new Vector3(d.z, 0f, -d.x)) * Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
                var r = sh.GetComponent<Renderer>();
                r.sharedMaterial = U.Fx(false, U.WhiteTexture());
                inst.AddRenderer(r, iceA);
                // aresta brilhante (aditiva) dentro do cristal
                var core = FxUtil.Shape(PrimitiveType.Cube, sh.transform, Vector3.zero, new Vector3(0.35f, 1.02f, 0.35f));
                var cr = core.GetComponent<Renderer>();
                cr.sharedMaterial = U.Fx(true, U.WhiteTexture());
                inst.AddRenderer(cr, iceGlow);
            }
            var cap = FxUtil.Shape(PrimitiveType.Cube, shell, Vector3.up * h * 0.95f, new Vector3(0.3f * s, 0.3f * s, 0.3f * s));
            cap.transform.localRotation = Quaternion.Euler(45f, 30f, 45f);
            var capR = cap.GetComponent<Renderer>(); capR.sharedMaterial = U.Fx(false, U.WhiteTexture()); inst.AddRenderer(capR, iceA);
            inst.SetGrow(shell, 0.14f);

            var glint = Loop(inst, "gelo_brilho", new Vector3(0f, h * 0.5f, 0f), Color.white, true, 0.25f, 0.5f, 0f, 0.1f, 0.08f, 0.18f, 0f, 6f);
            FxUtil.Sphere(glint, 0.5f * s);
            glint.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            SkillFX.Grad(glint, Color.white, SkillFX.Ice, 1f);
            glint.Play();
            var mist = Loop(inst, "gelo_nevoa", new Vector3(0f, 0.15f, 0f), new Color(0.8f, 0.95f, 1f, 0.4f), false, 1f, 1.5f, 0.05f, 0.25f, 0.5f * s, 0.9f * s, -0.01f, 6f, 30);
            FxUtil.FlatRing(mist, 0.6f * s);
            FxUtil.AlphaFade(mist, Color.white, SkillFX.Ice, 0.4f);
            mist.Play();
            var floor = Flat(inst, "gelo_chao", U.SoftTexture(), new Color(0.65f, 0.9f, 1f, 0.45f), true, 2.1f * s);
            inst.AddRenderer(floor.GetComponent<Renderer>(), new Color(0.65f, 0.9f, 1f, 0.45f));
            AddLight(inst, new Vector3(0f, h * 0.5f, 0f), SkillFX.Ice, 0.9f, 2.6f * s, 0f);

            inst.onEnd = () =>
            {
                if (inst == null) return;
                Vector3 c = WorldAt(inst, h * 0.5f);
                SkillFX.IceShards(c, Vector3.zero, Mathf.RoundToInt(16 * s), 0.8f * s);
                SkillFX.Mist(c - Vector3.up * h * 0.3f, SkillFX.Ice, 8, 0.5f * s);
                FX.Ring(inst.transform.position, 1.3f * s, SkillFX.Ice, 0.3f, 0.3f);
                if (shell != null) shell.gameObject.SetActive(false);
            };
        }

        // ------------------------------------------------------------------ sangramento: gotas escuras pulsando a cada tique
        static void Bleed(StatusFxInst inst, Transform t, float h, float s)
        {
            Color blood = new Color(0.62f, 0.03f, 0.04f, 0.95f);
            var drops = Loop(inst, "sangue_gotas", new Vector3(0f, h * 0.55f, 0f), blood, false, 0.4f, 0.7f, 0.3f, 1.1f, 0.05f * s, 0.09f * s, 1.5f, 7f, 60);
            FxUtil.Sphere(drops, 0.3f * s);
            SkillFX.Stretch(drops, 1.3f, 0.06f);
            FxUtil.AlphaFade(drops, new Color(0.85f, 0.08f, 0.06f), new Color(0.4f, 0f, 0f), 0.95f);
            drops.Play();
            var mist = Loop(inst, "sangue_nevoa", new Vector3(0f, h * 0.5f, 0f), new Color(0.6f, 0.05f, 0.05f, 0.3f), false, 0.5f, 0.9f, 0.1f, 0.4f, 0.25f * s, 0.45f * s, 0f, 3f, 20);
            FxUtil.Sphere(mist, 0.3f * s);
            FxUtil.AlphaFade(mist, new Color(0.7f, 0.08f, 0.06f), new Color(0.3f, 0f, 0f), 0.3f);
            mist.Play();
            var shine = Loop(inst, "sangue_brilho", new Vector3(0f, h * 0.55f, 0f), SkillFX.Blood, true, 0.2f, 0.35f, 0.5f, 1.2f, 0.05f, 0.1f, 1f, 4f);
            FxUtil.Sphere(shine, 0.3f * s);
            SkillFX.Grad(shine, new Color(1f, 0.6f, 0.5f), SkillFX.Blood, 0.8f);
            shine.Play();
            inst.tickEvery = 0.5f;
            inst.onTick = () => { if (drops != null) drops.Emit(5); };
        }

        // ------------------------------------------------------------------ prisão de sombra: poça escura, runas, correntes de sombra, fiapos
        static void RootShadow(StatusFxInst inst, Transform t, float h, float s)
        {
            var pool = Flat(inst, "sombra_poca", U.SoftTexture(), new Color(0.04f, 0f, 0.07f, 0.9f), false, 2.6f * s, 0.03f);
            inst.AddPulse(pool.GetComponent<Renderer>(), new Color(0.04f, 0f, 0.07f, 0.9f), 2.6f * s, 0.05f, 2.2f, 0.2f);
            var rune = Flat(inst, "sombra_runas", SkillFX.RuneTexture(), new Color(0.55f, 0.25f, 1f, 0.55f), true, 2.6f * s, 0.05f);
            inst.AddRenderer(rune.GetComponent<Renderer>(), new Color(0.55f, 0.25f, 1f, 0.55f));
            inst.AddSpin(rune.transform, -45f);
            var rim = Flat(inst, "sombra_borda", U.RingTexture(), new Color(0.45f, 0.15f, 0.9f, 0.6f), true, 2.4f * s, 0.055f);
            inst.AddPulse(rim.GetComponent<Renderer>(), new Color(0.45f, 0.15f, 0.9f, 0.6f), 2.4f * s, 0.06f, 4f);

            var wisp = Loop(inst, "sombra_fiapos", new Vector3(0f, 0.1f, 0f), new Color(0.08f, 0.02f, 0.12f, 0.75f), false, 0.8f, 1.3f, 0.3f, 0.8f, 0.25f * s, 0.45f * s, -0.2f, 14f, 50);
            FxUtil.FlatRing(wisp, 0.9f * s);
            SkillFX.Noise(wisp, 0.5f, 1.2f);
            FxUtil.AlphaFade(wisp, new Color(0.2f, 0.05f, 0.3f), new Color(0.02f, 0f, 0.04f), 0.75f);
            wisp.Play();
            var mote = Loop(inst, "sombra_brilho", new Vector3(0f, 0.1f, 0f), ShadowPurple, true, 0.6f, 1.1f, 0.4f, 1.2f, 0.04f, 0.09f, -0.2f, 9f);
            FxUtil.FlatRing(mote, 1f * s);
            SkillFX.Noise(mote, 0.6f, 1.5f);
            SkillFX.Grad(mote, new Color(0.85f, 0.7f, 1f), ShadowPurple, 1f);
            mote.Play();

            var cb = inst.gameObject.AddComponent<ChainBind>();
            cb.Setup(inst, 4, 1.15f * s, h, 0.15f * s, new Color(0.16f, 0.07f, 0.26f, 1f), new Color(0.6f, 0.3f, 1f, 0.55f));
            Vector3 c0 = inst.transform.position;
            var burst = SkillFX.Sys("sombra_irrompe", c0 + Vector3.up * 0.1f, new Color(0.1f, 0.02f, 0.15f, 0.8f), false, 0.3f, 0.6f, 2f, 5f, 0.25f, 0.5f, -0.3f);
            burst.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            SkillFX.Cone(burst, 20f, 0.8f * s); SkillFX.Bursts(burst, 14); SkillFX.Stretch(burst, 1.6f, 0.05f);
            FxUtil.AlphaFade(burst, new Color(0.3f, 0.1f, 0.45f), new Color(0.02f, 0f, 0.05f), 0.8f);
            burst.Play();

            inst.onEnd = () =>
            {
                if (inst == null) return;
                Vector3 p = inst.transform.position;
                var ps = SkillFX.Sys("sombra_solta", p + Vector3.up * 0.3f, ShadowPurple, true, 0.3f, 0.6f, 1.5f, 4f, 0.05f, 0.12f, -0.2f);
                SkillFX.Hemisphere(ps, 0.6f * s); SkillFX.Bursts(ps, 18); SkillFX.Grad(ps, Color.white, ShadowPurple, 1f); ps.Play();
                FX.Ring(p, 1.4f * s, ShadowPurple, 0.3f, 0.6f);
            };
        }

        // ------------------------------------------------------------------ atordoado: estrelas girando na cabeça
        static void Stun(StatusFxInst inst, Transform t, float h, float s)
        {
            var st = Loop(inst, "atordoado", new Vector3(0f, h + 0.25f, 0f), new Color(1f, 0.92f, 0.45f), true, 0.5f, 0.7f, 0f, 0f, 0.14f, 0.2f, 0f, 9f, 20);
            var m = st.main; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            FxUtil.FlatRing(st, 0.38f * s);
            FxUtil.Orbit(st, 6f, 0f);
            st.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            SkillFX.Grad(st, Color.white, new Color(1f, 0.85f, 0.3f), 1f);
            st.Play();
        }

        // ------------------------------------------------------------------ cego: nuvem escura nos olhos
        static void Blind(StatusFxInst inst, Transform t, float h, float s)
        {
            var cl = Loop(inst, "cego_nuvem", new Vector3(0f, h * 0.88f, 0f), new Color(0.2f, 0.18f, 0.22f, 0.6f), false, 0.7f, 1.1f, 0.05f, 0.25f, 0.35f * s, 0.6f * s, -0.05f, 7f, 30);
            FxUtil.Sphere(cl, 0.3f * s);
            SkillFX.Noise(cl, 0.3f, 1f);
            FxUtil.AlphaFade(cl, new Color(0.35f, 0.32f, 0.38f), new Color(0.12f, 0.1f, 0.14f), 0.6f);
            cl.Play();
            var sw = Loop(inst, "cego_giro", new Vector3(0f, h * 0.9f, 0f), new Color(0.75f, 0.72f, 0.85f), true, 0.5f, 0.8f, 0f, 0f, 0.05f, 0.09f, 0f, 10f, 20);
            var m = sw.main; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            FxUtil.FlatRing(sw, 0.35f * s);
            FxUtil.Orbit(sw, -5f, 0f);
            SkillFX.Grad(sw, Color.white, new Color(0.6f, 0.55f, 0.75f), 0.8f);
            sw.Play();
        }

        // ------------------------------------------------------------------ marcado: runa no chão + losango sobre a cabeça
        static void Mark(StatusFxInst inst, Transform t, float h, float s)
        {
            Color c = new Color(0.68f, 0.38f, 1f);
            var rune = Flat(inst, "marca_runa", SkillFX.RuneTexture(), new Color(c.r, c.g, c.b, 0.7f), true, 1.8f * s, 0.05f);
            inst.AddRenderer(rune.GetComponent<Renderer>(), new Color(c.r, c.g, c.b, 0.7f));
            inst.AddSpin(rune.transform, 90f);
            var ring = Flat(inst, "marca_anel", U.RingTexture(), new Color(c.r, c.g, c.b, 0.8f), true, 1.4f * s, 0.06f);
            inst.AddPulse(ring.GetComponent<Renderer>(), new Color(c.r, c.g, c.b, 0.8f), 1.4f * s, 0.12f, 7f);

            var holder = new GameObject("marca_cima").transform;
            holder.SetParent(t, false);
            holder.localPosition = Vector3.up * (h + 0.45f);
            var gem = FxUtil.Shape(PrimitiveType.Cube, holder, Vector3.zero, Vector3.one * 0.22f);
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            gem.GetComponent<Renderer>().sharedMaterial = U.Lit(c, 0.8f, c * 2.5f);
            var halo = FX.FlatQuad("marca_halo", holder.position, U.SoftTexture(), new Color(c.r, c.g, c.b, 0.7f), true);
            halo.transform.SetParent(holder, true);
            halo.transform.localScale = Vector3.one * 0.9f;
            halo.AddComponent<FxBillboard>();
            inst.AddPulse(halo.GetComponent<Renderer>(), new Color(c.r, c.g, c.b, 0.7f), 0.9f, 0.2f, 6f);
            inst.AddSpin(holder, 160f);
            inst.SetBob(holder);
            inst.SetGrow(holder, 0.12f);
            var sp = Loop(inst, "marca_brilho", new Vector3(0f, h + 0.45f, 0f), c, true, 0.3f, 0.6f, 0.2f, 0.6f, 0.04f, 0.08f, 0.3f, 8f);
            FxUtil.Sphere(sp, 0.15f);
            SkillFX.Grad(sp, Color.white, c, 1f);
            sp.Play();
        }

        // ------------------------------------------------------------------ Fúria do Berserker: chamas de sangue, batimento, luz vermelha
        static void Frenzy(StatusFxInst inst, Transform t, float h, float s)
        {
            Color hot = new Color(1f, 0.55f, 0.4f);
            Color red = new Color(1f, 0.12f, 0.08f);
            Color dark = new Color(0.35f, 0f, 0.02f);
            var fl = Loop(inst, "furia_chamas", new Vector3(0f, 0.1f, 0f), red, true, 0.4f, 0.8f, 1.4f * s, 3f * s, 0.14f * s, 0.32f * s, -0.15f, 30f, 120);
            FxUtil.ConeUp(fl, 8f, 0.45f * s);
            SkillFX.Noise(fl, 0.9f, 1.8f);
            var col = fl.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(hot, 0f), new GradientColorKey(red, 0.35f), new GradientColorKey(dark, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            fl.Play();
            var mist = Loop(inst, "furia_nevoa", new Vector3(0f, h * 0.4f, 0f), new Color(0.3f, 0.02f, 0.02f, 0.5f), false, 0.7f, 1.2f, 0.2f, 0.6f, 0.4f * s, 0.7f * s, -0.1f, 6f, 30);
            FxUtil.Sphere(mist, 0.45f * s);
            FxUtil.AlphaFade(mist, new Color(0.45f, 0.04f, 0.03f), new Color(0.1f, 0f, 0f), 0.45f);
            mist.Play();
            var emb = Loop(inst, "furia_brasas", new Vector3(0f, h * 0.3f, 0f), red, true, 0.5f, 1f, 1f, 2.5f, 0.03f, 0.07f, -0.3f, 14f);
            FxUtil.Sphere(emb, 0.4f * s);
            SkillFX.Stretch(emb, 1.4f, 0.04f);
            SkillFX.Grad(emb, hot, red, 1f);
            emb.Play();
            var ring = Flat(inst, "furia_anel", U.RingTexture(), new Color(1f, 0.1f, 0.06f, 0.7f), true, 1.9f * s);
            inst.AddPulse(ring.GetComponent<Renderer>(), new Color(1f, 0.1f, 0.06f, 0.7f), 1.9f * s, 0.08f, 6.5f, 0.6f);
            var pool = Flat(inst, "furia_chao", U.SoftTexture(), new Color(0.5f, 0f, 0f, 0.5f), true, 2.4f * s, 0.035f);
            inst.AddRenderer(pool.GetComponent<Renderer>(), new Color(0.5f, 0f, 0f, 0.5f));
            AddLight(inst, new Vector3(0f, h * 0.6f, 0f), red, 1.8f, 4.5f * s, 0.4f);
            // "batimento" de raiva: anel de sangue a cada ~1 s
            inst.tickEvery = 1.05f;
            inst.onTick = () =>
            {
                if (inst == null) return;
                Vector3 p = inst.transform.position;
                FX.Ring(p, 1.7f * s, SkillFX.Blood, 0.35f, 0.45f);
                emb.Emit(10);
            };
        }

        // ------------------------------------------------------------------ escudos em cúpula (Égide sagrada / Escudo de Mana)
        static void Dome(StatusFxInst inst, Transform t, float h, float s, Color main, Color accent, bool holy)
        {
            var holder = new GameObject("cupula").transform;
            holder.SetParent(t, false);
            var dome = SkillFX.Dome(inst.transform.position, 1f, new Color(main.r, main.g, main.b, 0.45f));
            dome.transform.SetParent(holder, false);
            dome.transform.localPosition = Vector3.zero;
            dome.transform.localRotation = Quaternion.identity;
            dome.transform.localScale = new Vector3(1.05f * s, h * 0.95f, 1.05f * s);
            // amount 0 = só pulsa o alpha (não mexe na escala da cúpula)
            inst.AddPulse(dome.GetComponent<Renderer>(), new Color(main.r, main.g, main.b, 0.42f), 1f, 0f, 3f, 0.5f);
            inst.SetGrow(holder, 0.18f);
            inst.AddSpin(holder, holy ? 25f : -40f);
            var ring = Flat(inst, "cupula_runas", SkillFX.RuneTexture(), new Color(accent.r, accent.g, accent.b, 0.45f), true, 2.3f * s, 0.05f);
            inst.AddRenderer(ring.GetComponent<Renderer>(), new Color(accent.r, accent.g, accent.b, 0.45f));
            inst.AddSpin(ring.transform, holy ? 40f : -60f);
            var orb = Loop(inst, "cupula_brilho", new Vector3(0f, h * 0.35f, 0f), main, true, 0.8f, 1.2f, 0f, 0f, 0.05f, 0.12f, -0.1f, 10f);
            FxUtil.FlatRing(orb, 1f * s);
            FxUtil.Orbit(orb, holy ? 2.5f : -3f, 0.5f);
            orb.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            SkillFX.Grad(orb, Color.white, main, 1f);
            orb.Play();
            AddLight(inst, new Vector3(0f, h * 0.6f, 0f), main, 0.9f, 3f * s, 0f);
            inst.onEnd = () =>
            {
                if (inst == null) return;
                Vector3 p = inst.transform.position;
                var ps = SkillFX.Sys("cupula_quebra", p + Vector3.up * h * 0.5f, main, true, 0.3f, 0.6f, 2f, 4.5f, 0.05f, 0.12f, 0.4f);
                SkillFX.Sphere(ps, 0.9f * s); SkillFX.Bursts(ps, 24); SkillFX.Grad(ps, Color.white, main, 1f); ps.Play();
                FX.Ring(p, 1.3f * s, main, 0.3f, 0.7f);
            };
        }

        // ------------------------------------------------------------------ Muralha / Estandarte / Postura de Ferro: anel de aço e motas douradas
        static void GuardBuff(StatusFxInst inst, Transform t, float h, float s)
        {
            Color steel = new Color(0.5f, 0.7f, 1f);
            var rune = Flat(inst, "guarda_runas", SkillFX.RuneTexture(), new Color(steel.r, steel.g, steel.b, 0.45f), true, 2f * s, 0.05f);
            inst.AddRenderer(rune.GetComponent<Renderer>(), new Color(steel.r, steel.g, steel.b, 0.45f));
            inst.AddSpin(rune.transform, 30f);
            var ring = Flat(inst, "guarda_anel", U.RingTexture(), new Color(SkillFX.Gold.r, SkillFX.Gold.g, SkillFX.Gold.b, 0.55f), true, 1.5f * s, 0.06f);
            inst.AddPulse(ring.GetComponent<Renderer>(), new Color(SkillFX.Gold.r, SkillFX.Gold.g, SkillFX.Gold.b, 0.55f), 1.5f * s, 0.06f, 3.5f);
            var mot = Loop(inst, "guarda_motas", new Vector3(0f, 0.25f, 0f), SkillFX.Gold, true, 0.9f, 1.4f, 0f, 0f, 0.05f, 0.1f, -0.12f, 9f);
            FxUtil.FlatRing(mot, 0.8f * s);
            FxUtil.Orbit(mot, 2f, 0.6f);
            SkillFX.Grad(mot, Color.white, SkillFX.Gold, 1f);
            mot.Play();
            var glint = Loop(inst, "guarda_brilho", new Vector3(0f, h * 0.55f, 0f), steel, true, 0.2f, 0.35f, 0f, 0f, 0.15f, 0.25f, 0f, 2.5f, 10);
            FxUtil.Sphere(glint, 0.45f * s);
            glint.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            SkillFX.Grad(glint, Color.white, steel, 1f);
            glint.Play();
        }

        static void Bless(StatusFxInst inst, Transform t, float h, float s, Color c)
        {
            var mot = Loop(inst, "bencao_motas", new Vector3(0f, 0.15f, 0f), c, true, 0.8f, 1.3f, 0.5f, 1.3f, 0.05f, 0.11f, -0.15f, 12f);
            FxUtil.FlatRing(mot, 0.6f * s, 0.6f);
            SkillFX.Noise(mot, 0.4f, 1f);
            SkillFX.Grad(mot, Color.white, c, 1f);
            mot.Play();
            var ring = Flat(inst, "bencao_anel", U.RingTexture(), new Color(c.r, c.g, c.b, 0.35f), true, 1.4f * s);
            inst.AddPulse(ring.GetComponent<Renderer>(), new Color(c.r, c.g, c.b, 0.35f), 1.4f * s, 0.06f, 3f);
        }

        // ------------------------------------------------------------------ Sobrecarga: faíscas arcanas + mini raios
        static void Overload(StatusFxInst inst, Transform t, float h, float s)
        {
            Color c = SkillFX.Arcane;
            var sp = Loop(inst, "sobrecarga_faiscas", new Vector3(0f, h * 0.55f, 0f), c, true, 0.1f, 0.25f, 2f, 4f, 0.03f, 0.06f, 0f, 24f);
            FxUtil.Sphere(sp, 0.5f * s);
            SkillFX.Stretch(sp, 1.6f, 0.05f);
            SkillFX.Grad(sp, Color.white, c, 1f);
            sp.Play();
            var mot = Loop(inst, "sobrecarga_motas", new Vector3(0f, 0.2f, 0f), SkillFX.ArcaneDeep, true, 0.8f, 1.2f, 0f, 0f, 0.06f, 0.12f, -0.2f, 12f);
            FxUtil.FlatRing(mot, 0.7f * s);
            FxUtil.Orbit(mot, -3.5f, 1f);
            SkillFX.Grad(mot, Color.white, SkillFX.ArcaneDeep, 1f);
            mot.Play();
            var ring = Flat(inst, "sobrecarga_runas", SkillFX.RuneTexture(), new Color(c.r, c.g, c.b, 0.4f), true, 1.8f * s);
            inst.AddRenderer(ring.GetComponent<Renderer>(), new Color(c.r, c.g, c.b, 0.4f));
            inst.AddSpin(ring.transform, -120f);
            AddLight(inst, new Vector3(0f, h * 0.6f, 0f), c, 1.4f, 3.5f * s, 0.6f);
            inst.tickEvery = 0.35f;
            inst.onTick = () =>
            {
                if (inst == null) return;
                Vector3 p = inst.transform.position + Vector3.up * h * Random.Range(0.3f, 0.9f);
                Vector3 a = p + Random.onUnitSphere * 0.25f * s;
                Vector3 b = p + Random.onUnitSphere * 0.7f * s;
                FxUtil.MiniBolt(a, b, c, 0.04f, 0.12f);
            };
        }

        // ------------------------------------------------------------------ furtividade: fiapos de sombra e fantasmas
        static void Stealth(StatusFxInst inst, Transform t, float h, float s, bool isPlayer)
        {
            var wisp = Loop(inst, "furtivo_fiapos", new Vector3(0f, 0.15f, 0f), new Color(0.12f, 0.06f, 0.2f, 0.55f), false, 0.6f, 1.1f, 0.3f, 0.7f, 0.25f * s, 0.45f * s, -0.15f, 10f, 40);
            FxUtil.FlatRing(wisp, 0.45f * s);
            SkillFX.Noise(wisp, 0.6f, 1.2f);
            FxUtil.AlphaFade(wisp, new Color(0.3f, 0.15f, 0.45f), new Color(0.05f, 0.02f, 0.08f), 0.55f);
            wisp.Play();
            var mot = Loop(inst, "furtivo_brilho", new Vector3(0f, h * 0.5f, 0f), new Color(0.6f, 0.4f, 1f), true, 0.5f, 0.9f, 0.1f, 0.4f, 0.03f, 0.06f, -0.1f, 7f);
            FxUtil.Sphere(mot, 0.45f * s);
            SkillFX.Grad(mot, Color.white, new Color(0.6f, 0.4f, 1f), 0.7f);
            mot.Play();
            var pool = Flat(inst, "furtivo_sombra", U.SoftTexture(), new Color(0.05f, 0f, 0.1f, 0.55f), false, 1.6f * s, 0.03f);
            inst.AddRenderer(pool.GetComponent<Renderer>(), new Color(0.05f, 0f, 0.1f, 0.55f));
            if (isPlayer)
            {
                inst.tickEvery = 0.5f;
                var pl = t.GetComponentInParent<Player>();
                inst.onTick = () =>
                {
                    if (pl == null || pl.visual == null) return;
                    SkillFX.Afterimage(pl.visual.transform, new Color(0.25f, 0.12f, 0.45f), 0.35f);
                };
            }
        }

        static void SmokeAura(StatusFxInst inst, Transform t, float h, float s)
        {
            var sm = Loop(inst, "fumaca", new Vector3(0f, h * 0.4f, 0f), new Color(0.35f, 0.33f, 0.38f, 0.5f), false, 0.8f, 1.3f, 0.2f, 0.5f, 0.4f * s, 0.7f * s, -0.05f, 7f, 30);
            FxUtil.Sphere(sm, 0.5f * s);
            SkillFX.Noise(sm, 0.4f, 0.8f);
            FxUtil.AlphaFade(sm, new Color(0.45f, 0.42f, 0.5f), new Color(0.2f, 0.18f, 0.22f), 0.5f);
            sm.Play();
        }

        // ------------------------------------------------------------------ Espelho de Aço: painéis espelhados girando
        static void Reflect(StatusFxInst inst, Transform t, float h, float s)
        {
            Color steel = new Color(0.75f, 0.85f, 1f, 0.5f);
            var holder = new GameObject("espelho").transform;
            holder.SetParent(t, false);
            int n = 4;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n;
                Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                for (int side = 0; side < 2; side++)
                {
                    var q = FxUtil.Shape(PrimitiveType.Quad, holder, d * 0.9f * s + Vector3.up * h * 0.5f, new Vector3(0.75f * s, h * 0.75f, 1f));
                    q.transform.localRotation = Quaternion.LookRotation(side == 0 ? d : -d, Vector3.up);
                    var r = q.GetComponent<Renderer>();
                    r.sharedMaterial = U.Fx(true, SkillFX.StripeTexture());
                    inst.AddRenderer(r, steel);
                }
            }
            inst.AddSpin(holder, 140f);
            inst.SetGrow(holder, 0.15f);
            var ring = Flat(inst, "espelho_anel", U.RingTexture(), new Color(0.7f, 0.85f, 1f, 0.6f), true, 2f * s);
            inst.AddPulse(ring.GetComponent<Renderer>(), new Color(0.7f, 0.85f, 1f, 0.6f), 2f * s, 0.07f, 8f);
            var glint = Loop(inst, "espelho_brilho", new Vector3(0f, h * 0.5f, 0f), Color.white, true, 0.15f, 0.3f, 0f, 0f, 0.15f, 0.3f, 0f, 7f, 15);
            FxUtil.Sphere(glint, 0.9f * s);
            glint.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            SkillFX.Grad(glint, Color.white, new Color(0.7f, 0.85f, 1f), 1f);
            glint.Play();
            AddLight(inst, new Vector3(0f, h * 0.6f, 0f), new Color(0.7f, 0.85f, 1f), 1f, 3f * s, 0f);
        }
    }
}
