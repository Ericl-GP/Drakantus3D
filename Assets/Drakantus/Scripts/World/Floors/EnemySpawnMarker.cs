using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Ponto onde um inimigo aparece. O Game cria o inimigo; o FloorRoot aplica elite/habilidades.</summary>
    public class EnemySpawnMarker : MonoBehaviour
    {
        public string enemyId = "minion";
        [Tooltip("Multiplica o tamanho (e a vida, se > 1).")]
        public float scale = 1f;
        public EnemyRole role = EnemyRole.Normal;
        [Tooltip("investida, escudo, invocar, veneno, teleporte, furia, fogo, pedras")]
        public string[] abilities = new string[0];
        [Tooltip("Vida extra (elite = 1,8).")]
        public float hpMult = 1f;

        [System.NonSerialized] public Enemy spawned;

        public Color GizmoColor()
        {
            switch (role)
            {
                case EnemyRole.Elite: return new Color(1f, 0.75f, 0.15f);
                case EnemyRole.MiniChefe: return new Color(1f, 0.4f, 0.1f);
                case EnemyRole.Chefe: return new Color(0.85f, 0.1f, 0.9f);
            }
            return new Color(1f, 0.25f, 0.2f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor();
            float s = Mathf.Max(0.5f, scale);
            float r = role == EnemyRole.Chefe ? 1.2f : role == EnemyRole.MiniChefe ? 0.9f : 0.5f;
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p + Vector3.up * r * s, r * s);
            Gizmos.DrawLine(p, p + Vector3.up * 2.2f * s);
            Gizmos.DrawSphere(p + Vector3.up * 2.2f * s, 0.15f);
        }
    }
}
