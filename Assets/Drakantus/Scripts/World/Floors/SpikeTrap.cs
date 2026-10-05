using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Espetos. Escondidos (hidden): invisíveis sob o piso até o herói pisar → sobem após um aviso curto.
    /// À vista: sobem e descem em ciclo. Também disparam com sinal (TriggerLink).
    /// </summary>
    public class SpikeTrap : FloorReceiver
    {
        public bool hidden = true;
        [Tooltip("Lado do quadrado de espetos (m).")]
        public float size = 2f;
        public float damage = 12f;
        [Tooltip("Ciclo das armadilhas à vista (s).")]
        public float period = 3f;
        public float upTime = 1.1f;
        public float offset;
        public Transform spikes;
        public float upY = 0f, downY = -0.75f;

        // 0 parado, 1 aviso, 2 em cima, 3 rearmando
        int stage;
        float t, hitCd, h, enemyHitCd;
        bool signaled;

        bool Inside(Vector3 world, float extra)
        {
            Vector3 l = transform.InverseTransformPoint(world);
            float half = size * 0.5f + extra;
            return Mathf.Abs(l.x) <= half && Mathf.Abs(l.z) <= half && Mathf.Abs(l.y) < 1.2f;
        }

        bool AnyEnemyInside()
        {
            var g = Game.I;
            if (g == null) return false;
            foreach (var e in g.enemies)
                if (e != null && !e.dead && Inside(e.transform.position, e.radius * 0.5f)) return true;
            return false;
        }

        /// <summary>Espetos em cima: fere os inimigos sobre o quadrado.</summary>
        bool HurtEnemies()
        {
            var g = Game.I;
            if (g == null) return false;
            int dmg = TrapHits.EnemyDamage(damage, this);
            bool any = false;
            foreach (var e in g.enemies.ToArray())
            {
                if (e == null || e.dead || !Inside(e.transform.position, e.radius * 0.5f)) continue;
                e.TakeHit(dmg, transform.position, false);
                FX.Burst(e.transform.position + Vector3.up * 0.5f, U.Hex("ff5a4a"), 0.5f, 8);
                any = true;
            }
            return any;
        }

        void Start()
        {
            h = hidden ? 0f : 0.15f;
            ApplyHeight();
        }

        bool PlayerInside(out Player p)
        {
            p = FloorRoot.LivePlayer();
            if (p == null) return false;
            Vector3 l = transform.InverseTransformPoint(p.transform.position);
            float half = size * 0.5f + 0.25f;
            return Mathf.Abs(l.x) <= half && Mathf.Abs(l.z) <= half && Mathf.Abs(l.y) < 1.2f;
        }

        public override void Signal(bool on)
        {
            if (on) signaled = true;
        }

        void Update()
        {
            if (!FloorRoot.IsRuntime(this)) return;
            float dt = Time.deltaTime;
            hitCd -= dt;
            bool inside = PlayerInside(out var p);
            float target;

            if (!hidden && !signaled)
            {
                // ciclo: aviso 0,4 s antes de subir
                float c = Mathf.Repeat(Time.time + offset, Mathf.Max(0.5f, period));
                float warn = period - 0.4f;
                if (c < upTime) target = 1f;
                else if (c >= warn) target = 0.35f;
                else target = 0.15f;
            }
            else
            {
                t += dt;
                switch (stage)
                {
                    case 0:
                        if (inside || signaled || AnyEnemyInside()) { stage = 1; t = 0f; signaled = false; Sfx.Play("block", transform.position, 0.5f); FX.Dust(transform.position, U.Hex("8a7a6a"), 0.6f); }
                        break;
                    case 1: if (t >= 0.35f) { stage = 2; t = 0f; Sfx.Play("swing_heavy", transform.position, 0.8f); } break;
                    case 2: if (t >= upTime) { stage = 3; t = 0f; } break;
                    case 3: if (t >= 1.4f) { stage = 0; t = 0f; } break;
                }
                target = stage == 1 ? 0.3f : stage == 2 ? 1f : (hidden ? 0f : 0.15f);
            }

            float speed = target > h ? 14f : 3f;
            h = Mathf.MoveTowards(h, target, dt * speed);
            ApplyHeight();
            if (h > 0.8f && inside && hitCd <= 0f)
            {
                hitCd = 0.9f;
                FloorRoot.HurtPlayer(damage * FloorRoot.TrapMult(this), transform.position);
                FX.Burst(p.transform.position + Vector3.up * 0.5f, U.Hex("ff5a4a"), 0.6f, 10);
            }
            enemyHitCd -= dt;
            if (h > 0.8f && enemyHitCd <= 0f && HurtEnemies()) enemyHitCd = 0.9f;
        }

        void ApplyHeight()
        {
            if (spikes == null) return;
            var lp = spikes.localPosition;
            lp.y = Mathf.Lerp(downY, upY, h);
            spikes.localPosition = lp;
            bool vis = h > 0.01f;
            if (spikes.gameObject.activeSelf != vis) spikes.gameObject.SetActive(vis);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = hidden ? new Color(1f, 0.3f, 0.3f, 0.8f) : new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, 0.1f, 0), new Vector3(size, 0.2f, size));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
