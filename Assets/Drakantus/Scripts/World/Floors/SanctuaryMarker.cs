using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Santuário do fim do andar (cura e conclui o andar). Selado enquanto houver chefe vivo.</summary>
    public class SanctuaryMarker : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 2.2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        }
    }
}
