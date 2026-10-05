using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Dano das armadilhas nos INIMIGOS (dá para atrair inimigos para elas).
    /// Inimigos levam 60% do dano que o herói levaria (vida deles é bem menor).
    /// </summary>
    public static class TrapHits
    {
        public const float EnemyFactor = 0.6f;

        public static int EnemyDamage(float baseDamage, Component trap)
        {
            float mult = FloorRoot.Of(trap) != null ? FloorRoot.TrapMult(trap) : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * mult * EnemyFactor));
        }

        /// <summary>Fere inimigos num cilindro (raio no plano, altura de -1 a height). Retorna quantos acertou.</summary>
        public static int HurtEnemiesInRadius(Vector3 center, float radius, float height, int dmg)
        {
            var g = Game.I;
            if (g == null || dmg <= 0) return 0;
            int n = 0;
            foreach (var e in g.enemies.ToArray())
            {
                if (e == null || e.dead) continue;
                Vector3 d = e.transform.position - center;
                if (U.Flat(d).magnitude > radius + e.radius || d.y < -1f || d.y > height) continue;
                e.TakeHit(dmg, center, false);
                n++;
            }
            return n;
        }
    }
}
