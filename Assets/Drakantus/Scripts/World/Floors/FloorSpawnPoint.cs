using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Ponto de chegada do herói. key "entrance" (padrão) também vira "default".</summary>
    public class FloorSpawnPoint : MonoBehaviour
    {
        public string key = "entrance";

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f);
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p + Vector3.up * 0.9f, 0.6f);
            Gizmos.DrawLine(p + Vector3.up * 0.9f, p + Vector3.up * 0.9f + transform.forward * 1.5f);
        }
    }
}
