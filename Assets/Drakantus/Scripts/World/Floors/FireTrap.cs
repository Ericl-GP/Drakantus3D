using UnityEngine;

namespace Drakantus
{
    /// <summary>Respiradouro de fogo: jato vertical periódico (ou por sinal) com aviso de faíscas.</summary>
    public class FireTrap : FloorReceiver
    {
        public float period = 4f;
        public float onTime = 1.4f;
        public float offset;
        public float damage = 10f;
        public float radius = 1f;
        public float height = 2.2f;
        [Tooltip("Só dispara com sinal (placa/alavanca).")]
        public bool signalOnly;
        public Color color = new Color(1f, 0.5f, 0.12f);

        ParticleSystem flame;
        float hitCd, signalT = -99f, sparkClock, enemyHitCd;
        bool burning;

        void Start()
        {
            if (!FloorRoot.IsRuntime(this)) return;
            flame = LevelDecor.Flame(transform, new Vector3(0, 0.1f, 0), 2.8f, color, 45);
            var m = flame.main; m.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
            SetBurning(false);
        }

        public override void Signal(bool on)
        {
            if (on) signalT = Time.time;
        }

        void SetBurning(bool b)
        {
            burning = b;
            if (flame == null) return;
            var em = flame.emission;
            em.enabled = b;
            if (b)
            {
                Sfx.Play("fireball", transform.position, 0.6f);
                FX.Flash(transform.position + Vector3.up * 1f, color, 2.5f, onTime);
            }
        }

        void Update()
        {
            if (flame == null) return;
            float dt = Time.deltaTime;
            hitCd -= dt;
            bool want;
            bool warn;
            if (signalOnly)
            {
                float s = Time.time - signalT;
                warn = s >= 0f && s < 0.6f;
                want = s >= 0.6f && s < 0.6f + onTime;
            }
            else
            {
                float c = Mathf.Repeat(Time.time + offset, Mathf.Max(onTime + 0.8f, period));
                want = c < onTime;
                warn = c > period - 0.7f;
                float s = Time.time - signalT;
                if (s >= 0f && s < onTime) want = true;
            }
            if (want != burning) SetBurning(want);
            if (warn && !burning)
            {
                sparkClock -= dt;
                if (sparkClock <= 0f) { sparkClock = 0.15f; FX.Sparkle(transform.position + Vector3.up * 0.2f, color, 0.5f, 0.3f); }
            }
            if (!burning) return;
            // inimigos dentro do jato também queimam
            enemyHitCd -= dt;
            if (enemyHitCd <= 0f && TrapHits.HurtEnemiesInRadius(transform.position, radius + 0.2f, height, TrapHits.EnemyDamage(damage, this)) > 0)
                enemyHitCd = 0.6f;
            if (hitCd > 0f) return;
            var p = FloorRoot.LivePlayer();
            if (p == null) return;
            Vector3 d = p.transform.position - transform.position;
            if (U.Flat(d).magnitude <= radius + 0.3f && d.y > -1f && d.y < height)
            {
                hitCd = 0.6f;
                FloorRoot.HurtPlayer(damage * FloorRoot.TrapMult(this), transform.position);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, radius);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * height);
        }
    }
}
