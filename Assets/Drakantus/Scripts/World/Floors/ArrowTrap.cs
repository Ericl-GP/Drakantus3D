using UnityEngine;

namespace Drakantus
{
    /// <summary>Lançador de flechas na parede: atira para a frente (eixo Z azul) em ciclo ou por sinal.</summary>
    public class ArrowTrap : FloorReceiver
    {
        public float period = 2.6f;
        public float offset;
        public float damage = 8f;
        [Tooltip("Só atira quando o herói está a esta distância.")]
        public float range = 14f;
        public bool signalOnly;
        public int volley = 1;

        float clock;

        void Start() { clock = offset; }

        public override void Signal(bool on)
        {
            if (on) Shoot();
        }

        void Update()
        {
            if (signalOnly || !FloorRoot.IsRuntime(this)) return;
            clock += Time.deltaTime;
            if (clock < period) return;
            clock = 0f;
            var p = FloorRoot.LivePlayer();
            if (p == null || Vector3.Distance(p.transform.position, transform.position) > range) return;
            Shoot();
        }

        public void Shoot()
        {
            if (!FloorRoot.IsRuntime(this)) return;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(damage * FloorRoot.TrapMult(this)));
            Vector3 dir = U.Flat(transform.forward);
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            int n = Mathf.Max(1, volley);
            for (int i = 0; i < n; i++)
            {
                Vector3 o = transform.position + dir * 0.45f + side * ((i - (n - 1) * 0.5f) * 0.6f);
                var pm = Projectile.Spawn("arrow", o, dir, dmg, false, null);
                if (pm != null) pm.hitsEnemies = true;   // as flechas também ferem inimigos no caminho
            }
            FX.Dust(transform.position + dir * 0.4f, U.Hex("a09080"), 0.4f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.5f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3f);
        }
    }
}
