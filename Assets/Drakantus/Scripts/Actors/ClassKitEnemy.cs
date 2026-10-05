using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// [Classes] Efeitos de status nos inimigos: Root, Slow, Dot (veneno/queimar/sangrar com acúmulos), Blind,
    /// Freeze, Mark (Marca da Morte), Pull. Chefes recebem controle ×0,4 (<see cref="BossCC"/>).
    /// O visual de cada status é do StatusFX (agente EFEITOS).
    /// </summary>
    public partial class Enemy
    {
        public const float BossCC = 0.4f;

        class DotState { public string kind; public float dps, time, tick; public int stacks, max; public Player src; }

        readonly List<DotState> dots = new();
        float rootT, slowT, slowMult = 1f, blindT, freezeT, pullT, pullSpeed, markT, markStore, markExecute;
        Vector3 pullPoint;
        Player markOwner;
        bool markBoom;

        public bool Rooted => rootT > 0f || freezeT > 0f;
        public bool Frozen => freezeT > 0f;
        public bool Blinded => blindT > 0f;
        public bool Marked => markT > 0f;
        public int DotStacks(string kind) { foreach (var d in dots) if (d.kind == kind) return d.stacks; return 0; }

        float CC(float s) => def != null && def.boss ? s * BossCC : s;
        float FxH => Mathf.Max(1.2f, top);

        // ------------------------------------------------------------------ API
        /// <summary>Imobiliza (não anda; ainda ataca o que estiver ao alcance).</summary>
        public void Root(float seconds, string fx = "root_shadow")
        {
            if (dead || seconds <= 0f) return;
            seconds = CC(seconds);
            if (seconds > rootT) rootT = seconds;
            Alert(true);
            StatusFX.Attach(transform, string.IsNullOrEmpty(fx) ? "root_shadow" : fx, rootT, FxH);
        }

        /// <summary>mult = multiplicador de velocidade (0,6 = 40% mais lento). Fica o mais forte.</summary>
        public void Slow(float mult, float seconds)
        {
            if (dead || seconds <= 0f) return;
            mult = Mathf.Clamp(mult, 0.1f, 1f);
            seconds = CC(seconds);
            if (slowT <= 0f || mult < slowMult) slowMult = mult;
            slowT = Mathf.Max(slowT, seconds);
            StatusFX.Attach(transform, "chill", slowT, FxH);
        }

        /// <summary>Dano por segundo (kind: poison, burn, bleed). Reaplicar soma 1 acúmulo (até maxStacks) e renova.</summary>
        public void Dot(string kind, float dps, float seconds, int maxStacks, Player src = null)
        {
            if (dead || dps <= 0f || seconds <= 0f || string.IsNullOrEmpty(kind)) return;
            DotState d = null;
            foreach (var x in dots) if (x.kind == kind) { d = x; break; }
            if (d == null) { d = new DotState { kind = kind, tick = 0.5f }; dots.Add(d); }
            d.max = Mathf.Max(1, maxStacks);
            d.stacks = Mathf.Min(d.max, d.stacks + 1);
            d.dps = Mathf.Max(d.dps, dps);
            d.time = Mathf.Max(d.time, seconds);
            if (src != null) d.src = src;
            StatusFX.Attach(transform, kind, d.time, FxH);
        }

        /// <summary>Cego: golpes corpo a corpo erram 65% das vezes, tiros saem tortos e anda mais devagar.</summary>
        public void Blind(float seconds)
        {
            if (dead || seconds <= 0f) return;
            seconds = CC(seconds);
            blindT = Mathf.Max(blindT, seconds);
            StatusFX.Attach(transform, "blind", blindT, FxH);
        }

        /// <summary>Congelado: não anda nem ataca.</summary>
        public void Freeze(float seconds)
        {
            if (dead || seconds <= 0f) return;
            seconds = CC(seconds);
            freezeT = Mathf.Max(freezeT, seconds);
            stunT = Mathf.Max(stunT, freezeT);
            CancelWindup();
            SetState("stunned");
            Alert(true);
            StatusFX.Attach(transform, "freeze", freezeT, FxH);
        }

        /// <summary>Marca da Morte: guarda store × dano recebido e explode no fim; abaixo de execute (fração) morre (não chefes).</summary>
        public void Mark(float seconds, float store, float execute, Player owner)
        {
            if (dead || seconds <= 0f) return;
            markT = seconds;
            markStore = 0f;
            markExecute = execute;
            markOwner = owner;
            markBoom = store > 0f;
            markFrac = store;
            Alert(true);
            StatusFX.Attach(transform, "mark", seconds, FxH);
        }
        float markFrac;

        /// <summary>Marca só visual (mira do Olho Morto).</summary>
        public void MarkVisual(float seconds) { if (!dead) StatusFX.Attach(transform, "mark", seconds, FxH); }

        /// <summary>Puxa em direção a um ponto (chamar todo quadro enquanto durar o efeito).</summary>
        public void Pull(Vector3 toward, float strength)
        {
            if (dead || strength <= 0f) return;
            pullPoint = toward;
            pullSpeed = def != null && def.boss ? strength * BossCC : strength;
            pullT = 0.15f;
        }

        // ------------------------------------------------------------------ ganchos usados no Enemy.cs
        void TickStatus(float dt)
        {
            if (rootT > 0f) { rootT -= dt; if (rootT <= 0f) { StatusFX.Detach(transform, "root_shadow"); } }
            if (slowT > 0f) { slowT -= dt; if (slowT <= 0f) { slowMult = 1f; StatusFX.Detach(transform, "chill"); } }
            if (blindT > 0f) { blindT -= dt; if (blindT <= 0f) StatusFX.Detach(transform, "blind"); }
            if (freezeT > 0f) { freezeT -= dt; if (freezeT <= 0f) StatusFX.Detach(transform, "freeze"); }
            if (pullT > 0f) pullT -= dt;
            if (markT > 0f)
            {
                markT -= dt;
                if (markT <= 0f) MarkExplode();
            }
            for (int i = dots.Count - 1; i >= 0 && !dead; i--)
            {
                var d = dots[i];
                d.time -= dt;
                d.tick -= dt;
                if (d.tick <= 0f)
                {
                    d.tick += 0.5f;
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(d.dps * 0.5f * d.stacks));
                    TakeDot(dmg, DotColor(d.kind), d.src);
                }
                if (dead) return;
                if (d.time <= 0f)
                {
                    StatusFX.Detach(transform, d.kind);
                    dots.RemoveAt(i);
                }
            }
        }

        static Color DotColor(string kind)
        {
            switch (kind)
            {
                case "poison": return U.Hex("8fe05a");
                case "burn": return U.Hex("ff9a3a");
                case "bleed": return U.Hex("ff4a4a");
                default: return Color.white;
            }
        }

        /// <summary>Dano de status: sem empurrão e sem interromper o golpe.</summary>
        void TakeDot(int dmg, Color c, Player src)
        {
            if (dead || dmg <= 0) return;
            if (Time.time < invulnUntil) return;
            hp -= dmg;
            OnStatusHit(dmg);
            barShowT = 3.5f;
            HUD.Popup(transform.position + Vector3.up * (top + 0.1f), dmg.ToString(), c, false);
            if (hp <= 0f) Die();
            if (src != null) src.RegisterHit(this, dead, false, dmg);
        }

        Vector3 StatusMove(Vector3 move, float dt)
        {
            if (rootT > 0f || freezeT > 0f) move = Vector3.zero;
            else
            {
                float m = 1f;
                if (slowT > 0f) m *= slowMult;
                if (blindT > 0f) m *= 0.7f;
                move *= m;
            }
            if (pullT > 0f && freezeT <= 0f)
            {
                Vector3 to = U.Flat(pullPoint - transform.position);
                float d = to.magnitude;
                if (d > 0.6f) move += to / d * Mathf.Min(pullSpeed, d / Mathf.Max(0.02f, dt));
            }
            return move;
        }

        bool BlindMiss()
        {
            if (blindT <= 0f || Random.value > 0.65f) return false;
            HUD.Popup(transform.position + Vector3.up * (top + 0.3f), "ERROU", U.Hex("c8c8d8"));
            return true;
        }

        float BlindSpread() => blindT > 0f ? Random.Range(-35f, 35f) : 0f;

        void OnStatusHit(int dmg)
        {
            if (markT <= 0f) return;
            if (markBoom) markStore += dmg * markFrac;
            if (markExecute > 0f && def != null && !def.boss && hp > 0f && hp <= maxHp * markExecute)
            {
                hp = 0f;   // TakeHit/TakeDot chamam Die()
                HUD.Popup(transform.position + Vector3.up * (top + 0.6f), "EXECUTADO!", U.Hex("c070ff"), true);
                SkillFX.Phase("as_deathmark", "end", transform.position, Vector3.forward, 1.5f, U.Hex("9b6bff"));
                markT = 0f;
                StatusFX.Detach(transform, "mark");
            }
        }

        void MarkExplode()
        {
            StatusFX.Detach(transform, "mark");
            int boom = Mathf.RoundToInt(markStore);
            markStore = 0f;
            if (dead || boom <= 0) return;
            bool was = markBoom;
            markBoom = false;
            SkillFX.Phase("as_deathmark", "end", transform.position, Vector3.forward, 1.5f, U.Hex("9b6bff"));
            TakeHit(boom, transform.position + transform.forward, true);
            if (markOwner != null) markOwner.RegisterHit(this, dead, false, boom);
            markBoom = was;
        }

        void ClearStatus()
        {
            dots.Clear();
            rootT = slowT = blindT = freezeT = pullT = markT = 0f;
            slowMult = 1f;
            StatusFX.Detach(transform);
        }
    }
}
