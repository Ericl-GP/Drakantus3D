using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Arena: quando o herói entra na caixa e há inimigos vivos dentro, envia sinal false (fecha portas);
    /// quando todos morrem, envia true (abre portas, libera baús).
    /// </summary>
    public class RoomClearTrigger : MonoBehaviour
    {
        public Vector3 size = new Vector3(12f, 6f, 12f);
        public string lockMessage = "As portas se fecharam! Derrote todos os inimigos.";
        public string clearMessage = "Sala limpa!";

        int stage;   // 0 esperando, 1 trancada, 2 concluída
        readonly List<Enemy> inside = new List<Enemy>();
        float clock;

        bool Contains(Vector3 world)
        {
            Vector3 l = transform.InverseTransformPoint(world);
            return Mathf.Abs(l.x) <= size.x * 0.5f && Mathf.Abs(l.z) <= size.z * 0.5f && l.y > -2f && l.y < size.y;
        }

        void Update()
        {
            if (stage == 2 || !FloorRoot.IsRuntime(this)) return;
            clock -= Time.deltaTime;
            if (clock > 0f) return;
            clock = 0.2f;
            var g = Game.I;
            var p = FloorRoot.LivePlayer();
            if (g == null) return;

            if (stage == 0)
            {
                if (p == null || !Contains(p.transform.position)) return;
                inside.Clear();
                foreach (var e in g.enemies)
                    if (e != null && !e.dead && Contains(e.transform.position)) inside.Add(e);
                if (inside.Count == 0) { stage = 2; return; }
                stage = 1;
                TriggerLink.Send(gameObject, false);
                Sfx.Play("enemy_alert", p.transform.position);
                if (HUD.I != null && !string.IsNullOrEmpty(lockMessage)) HUD.I.Toast(lockMessage);
                foreach (var e in inside) e.Alert(false);
                return;
            }

            // stage 1: espera todos (inclusive invocados dentro da sala) morrerem
            int alive = 0;
            foreach (var e in inside) if (e != null && !e.dead) alive++;
            foreach (var e in g.enemies) if (e != null && !e.dead && !inside.Contains(e) && Contains(e.transform.position)) alive++;
            if (alive > 0) return;
            stage = 2;
            TriggerLink.Send(gameObject, true);
            Sfx.Play("portal", transform.position, 0.8f);
            if (HUD.I != null && !string.IsNullOrEmpty(clearMessage)) HUD.I.Toast(clearMessage);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, size.y * 0.5f, 0), size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
