using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Escada/rampa gerada por blocos (só dados para o editor e gizmo; a colisão é a rampa invisível filha).</summary>
    public class FloorStairs : MonoBehaviour
    {
        public float height = 1.2f;
        public float length = 2.4f;
        public float width = 4f;
        public bool steps = true;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, height * 0.5f, length * 0.5f), new Vector3(width, height, length));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
