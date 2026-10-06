using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Ponto onde um NPC aparece (id de Resources/Data/npcs.json). A rotação em Y do objeto é para onde o NPC olha.
    /// Usado nos mapas exportados (Drakantus > Mapas) e em andares do Criador de Andares.
    /// </summary>
    public class NpcMarker : MonoBehaviour
    {
        public string npcId = "";

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.35f, 0.8f, 1f);
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p + Vector3.up * 0.9f, 0.45f);
            Gizmos.DrawLine(p + Vector3.up * 0.9f, p + Vector3.up * 0.9f + transform.forward * 1.2f);
        }
    }
}
