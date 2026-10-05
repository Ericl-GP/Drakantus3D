using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Porta que afunda no chão ao abrir (sinal true) e sobe ao fechar (sinal false).
    /// O painel (primeiro filho ou "panel") leva o colisor junto.
    /// </summary>
    public class Door : FloorReceiver
    {
        public Transform panel;
        public bool startOpen;
        [Tooltip("Quanto o painel desce ao abrir (m).")]
        public float openDepth = 3.6f;
        public float speed = 2.5f;
        [Tooltip("Texto mostrado perto da porta enquanto fechada (\"\" = nada).")]
        public string lockedHint = "";
        public bool secret;

        bool open;
        float k;
        Vector3 closedPos;
        FloorInteractable hint;

        public bool IsOpen => open;

        void Start()
        {
            if (panel == null && transform.childCount > 0) panel = transform.GetChild(0);
            if (panel != null) closedPos = panel.localPosition;
            open = startOpen;
            k = open ? 1f : 0f;
            Apply();
            if (FloorRoot.IsRuntime(this) && !string.IsNullOrEmpty(lockedHint))
                hint = FloorInteractable.Add(gameObject, lockedHint, 2.6f, () =>
                {
                    Sfx.Play("ui_error");
                    if (HUD.I != null) HUD.I.Toast(lockedHint);
                });
            UpdateHint();
        }

        public override void Signal(bool on) => SetOpen(on);

        public void SetOpen(bool o)
        {
            if (o == open) return;
            open = o;
            UpdateHint();
            if (!Application.isPlaying) { k = open ? 1f : 0f; Apply(); return; }
            Sfx.Play(secret ? "footstep_stone" : "block", transform.position, 0.9f);
            FX.Dust(transform.position, U.Hex("9a8a78"), 1.2f);
            if (Game.I != null) Game.I.Shake(0.08f);
        }

        void UpdateHint()
        {
            if (hint != null) hint.enabled = !open;
        }

        void Update()
        {
            float t = open ? 1f : 0f;
            if (Mathf.Approximately(k, t)) return;
            k = Mathf.MoveTowards(k, t, Time.deltaTime * speed / Mathf.Max(0.5f, openDepth) * 2f);
            Apply();
        }

        void Apply()
        {
            if (panel == null) return;
            panel.localPosition = closedPos + Vector3.down * openDepth * k;
            // painel escondido some (evita "tampa" aparecendo no chão)
            bool vis = k < 0.98f;
            if (panel.gameObject.activeSelf != vis) panel.gameObject.SetActive(vis);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = secret ? new Color(0.6f, 0.6f, 0.7f) : (startOpen ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.4f, 0.2f));
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, 1.5f, 0), new Vector3(4f, 3f, 0.6f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
