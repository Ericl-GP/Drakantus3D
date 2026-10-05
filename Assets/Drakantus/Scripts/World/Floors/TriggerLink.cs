using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Liga uma fonte (PressurePlate, Lever, RoomClearTrigger — no MESMO objeto) a alvos FloorReceiver
    /// (Door, ChestMarker, armadilhas). Fonte chama TriggerLink.Send(gameObject, on).
    /// </summary>
    public class TriggerLink : MonoBehaviour
    {
        public List<FloorReceiver> targets = new List<FloorReceiver>();
        [Tooltip("Inverte o sinal (ex.: placa que FECHA uma porta).")]
        public bool invert;
        [Tooltip("Só dispara uma vez.")]
        public bool once;
        public float delay;
        bool fired;

        public static void Send(GameObject source, bool on)
        {
            if (source == null) return;
            foreach (var l in source.GetComponents<TriggerLink>()) l.Fire(on);
        }

        public void Fire(bool on)
        {
            if (once && fired) return;
            fired = true;
            if (delay > 0f && isActiveAndEnabled) StartCoroutine(Later(on));
            else Deliver(on);
        }

        IEnumerator Later(bool on)
        {
            yield return new WaitForSeconds(delay);
            Deliver(on);
        }

        void Deliver(bool on)
        {
            bool v = invert ? !on : on;
            foreach (var t in targets) if (t != null) t.Signal(v);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f);
            foreach (var t in targets)
                if (t != null) Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, t.transform.position + Vector3.up * 0.5f);
        }
    }
}
