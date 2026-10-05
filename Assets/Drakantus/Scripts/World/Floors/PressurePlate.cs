using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Placa de pressão: o herói pisa → sinal true; sai → sinal false (se não for "once").</summary>
    public class PressurePlate : MonoBehaviour
    {
        public float radius = 1f;
        public bool once;
        public Transform plate;
        bool pressed, used;
        Vector3 rest;
        bool hasRest;

        void Start()
        {
            if (plate != null) { rest = plate.localPosition; hasRest = true; }
        }

        void Update()
        {
            if (!FloorRoot.IsRuntime(this)) return;
            var p = FloorRoot.LivePlayer();
            bool inside = false;
            if (p != null)
            {
                Vector3 d = p.transform.position - transform.position;
                inside = Mathf.Abs(d.y) < 1.2f && U.Flat(d).magnitude <= radius;
            }
            if (inside == pressed) return;
            pressed = inside;
            if (hasRest) plate.localPosition = rest + (pressed ? Vector3.down * 0.06f : Vector3.zero);
            if (once && used) return;
            if (pressed)
            {
                used = true;
                Sfx.Play("block", transform.position, 0.7f);
                TriggerLink.Send(gameObject, true);
            }
            else if (!once) TriggerLink.Send(gameObject, false);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.7f, 0.3f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.05f, new Vector3(radius * 2f, 0.1f, radius * 2f));
        }
    }
}
