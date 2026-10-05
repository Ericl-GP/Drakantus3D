using UnityEngine;

namespace Drakantus
{
    /// <summary>Névoa venenosa: partículas verdes; dano por segundo enquanto o herói estiver dentro.</summary>
    public class PoisonFog : MonoBehaviour
    {
        public float radius = 3f;
        public float dps = 6f;
        [Tooltip("Liga e desliga em ciclo (0 = sempre ligada).")]
        public float pulsePeriod;
        [Tooltip("Some depois de X s (0 = permanente). Usado pelas poças das habilidades.")]
        public float life;
        [Tooltip("A névoa também fere inimigos (as poças criadas pelos próprios inimigos não ferem).")]
        public bool hurtsEnemies = true;
        public Color color = new Color(0.45f, 1f, 0.35f, 0.5f);

        ParticleSystem ps;
        float tick, age;
        bool active = true;
        GameObject disc;

        /// <summary>Poça temporária (habilidade "veneno" dos inimigos).</summary>
        public static PoisonFog SpawnPuddle(Vector3 pos, float radius, float dps, float life)
        {
            var go = new GameObject("Poca_Veneno");
            Transform parent = Game.I != null && Game.I.levelRoot != null ? Game.I.levelRoot : FX.Root;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var f = go.AddComponent<PoisonFog>();
            f.radius = radius; f.dps = dps; f.life = life;
            f.hurtsEnemies = false;
            f.Build();
            return f;
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            Build();
        }

        void Build()
        {
            if (ps != null) return;
            ps = LevelDecor.Motes(transform, new Vector3(0, 0.8f, 0), new Vector3(radius * 1.6f, 1.4f, radius * 1.6f), color, Mathf.Clamp(Mathf.RoundToInt(radius * 14f), 12, 90), 0.03f);
            var m = ps.main;
            m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = U.Fx(false);
            disc = FX.FlatQuad("veneno_chao", transform.position + Vector3.up * 0.05f, U.SoftTexture(), new Color(color.r * 0.6f, color.g * 0.8f, color.b * 0.4f, 0.55f), false);
            disc.transform.SetParent(transform, true);
            disc.transform.localScale = Vector3.one * radius * 2.4f;
        }

        void OnDestroy()
        {
            if (disc != null) Destroy(disc);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float dt = Time.deltaTime;
            age += dt;
            if (life > 0f && age >= life)
            {
                if (ps != null) { var em = ps.emission; em.enabled = false; }
                Destroy(gameObject, 1.5f);
                life = 0f; active = false; enabled = false;
                return;
            }
            if (pulsePeriod > 0f)
            {
                bool on = Mathf.Repeat(Time.time, pulsePeriod) < pulsePeriod * 0.6f;
                if (on != active)
                {
                    active = on;
                    if (ps != null) { var em = ps.emission; em.enabled = on; }
                    if (disc != null) disc.SetActive(on);
                }
            }
            if (!active) return;
            tick -= dt;
            if (tick > 0f) return;
            tick = 0.5f;
            float mult = FloorRoot.Of(this) != null ? FloorRoot.TrapMult(this) : 1f;
            if (hurtsEnemies && Game.I != null)
            {
                int ed = TrapHits.EnemyDamage(dps * 0.5f, this);
                foreach (var e in Game.I.enemies.ToArray())
                {
                    // só quem está em combate (atraído pelo herói): inimigos parados perto da névoa não morrem sozinhos
                    if (e == null || e.dead || !e.alerted) continue;
                    Vector3 de = e.transform.position - transform.position;
                    if (U.Flat(de).magnitude > radius || Mathf.Abs(de.y) > 2.5f) continue;
                    e.TakeHit(ed, e.transform.position + e.transform.forward * 0.3f, false);   // leve recuo (contra o avanço)
                }
            }
            var p = FloorRoot.LivePlayer();
            if (p == null) return;
            Vector3 d = p.transform.position - transform.position;
            if (U.Flat(d).magnitude > radius || Mathf.Abs(d.y) > 2.5f) return;
            string r = FloorRoot.HurtPlayer(dps * 0.5f * mult, transform.position);
            if (r == "hit") HUD.Popup(p.transform.position + Vector3.up * 2.6f, "veneno", U.Hex("8fff6a"));
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, radius);
        }
    }
}
