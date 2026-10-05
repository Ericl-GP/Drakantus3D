using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Alavanca: tecla E perto dela envia o sinal (via TriggerLink no mesmo objeto).</summary>
    public class Lever : MonoBehaviour
    {
        public string label = "[E] Puxar alavanca";
        public bool once = true;
        public float radius = 2.2f;
        public Transform handle;
        [Tooltip("Sem visual (ex.: parede rachada secreta).")]
        public bool hidden;

        bool on, used;
        FloorInteractable it;
        float anim;

        void Start()
        {
            if (!FloorRoot.IsRuntime(this)) return;
            it = FloorInteractable.Add(gameObject, label, radius, Use);
            if (handle != null) handle.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        }

        public void Use()
        {
            if (once && used) return;
            used = true;
            on = !on;
            anim = 0f;
            Sfx.Play(hidden ? "footstep_stone" : "ui_click", transform.position);
            Sfx.Play("block", transform.position, 0.6f);
            FX.Sparkle(transform.position + Vector3.up * 1f, U.Hex("ffe08a"), 0.6f, 0.4f);
            TriggerLink.Send(gameObject, on);
            if (once && it != null) it.enabled = false;
        }

        void Update()
        {
            if (handle == null || anim >= 1f) return;
            anim = Mathf.Min(1f, anim + Time.deltaTime * 4f);
            float a = on ? Mathf.Lerp(-35f, 35f, anim) : Mathf.Lerp(35f, -35f, anim);
            handle.localRotation = Quaternion.Euler(a, 0f, 0f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.6f, 0.35f);
        }
    }
}
