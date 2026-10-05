using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Baú: aparece quando o andar começa (ou ao receber sinal, se esperarSinal).</summary>
    public class ChestMarker : FloorReceiver
    {
        [Tooltip("1 = comum, 2 = raro")]
        public int tier = 1;
        [Tooltip("Só aparece quando receber sinal (ex.: sala limpa, alavanca).")]
        public bool waitSignal;
        bool spawned;

        void Start()
        {
            if (!FloorRoot.IsRuntime(this) || waitSignal) return;
            StartCoroutine(SpawnLater(0.3f));
        }

        IEnumerator SpawnLater(float t)
        {
            yield return new WaitForSeconds(t);
            SpawnNow();
        }

        public override void Signal(bool on)
        {
            if (!on || spawned || !FloorRoot.IsRuntime(this)) return;
            FX.Pillar(transform.position, tier >= 2 ? U.Hex("ffd04a") : U.Hex("d8c8a0"), 1f);
            Sfx.Play("item_rare", transform.position, 0.7f);
            SpawnNow();
        }

        void SpawnNow()
        {
            if (spawned) return;
            spawned = true;
            Chest.Spawn(transform.position, Mathf.Max(1, tier));
        }

        void OnDrawGizmos()
        {
            Gizmos.color = tier >= 2 ? new Color(1f, 0.82f, 0.2f) : new Color(0.75f, 0.55f, 0.3f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.4f, new Vector3(1.1f, 0.8f, 0.75f));
            if (waitSignal) Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.2f, 0.2f);
        }
    }
}
