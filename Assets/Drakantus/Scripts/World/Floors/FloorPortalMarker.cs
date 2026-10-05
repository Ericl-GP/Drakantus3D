using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Portal (padrão: voltar para a porta da Torre na cidade).</summary>
    public class FloorPortalMarker : MonoBehaviour
    {
        public string targetMap = "town";
        public string targetSpawn = "tower_door";
        public string label = "Sair da Torre";
        public float radius = 1.6f;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.75f, 0.55f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, radius);
        }
    }
}
