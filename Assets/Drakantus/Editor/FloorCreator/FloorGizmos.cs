using UnityEditor;
using UnityEngine;

namespace Drakantus.EditorTools
{
    /// <summary>Nomes flutuantes na Scene View para os marcadores do andar (inimigos, baús, portas...).</summary>
    static class FloorGizmos
    {
        static GUIStyle style;

        static GUIStyle Style
        {
            get
            {
                if (style == null)
                {
                    style = new GUIStyle(EditorStyles.miniBoldLabel);
                    style.normal.textColor = Color.white;
                    style.alignment = TextAnchor.MiddleCenter;
                }
                return style;
            }
        }

        static bool Near(Vector3 p)
        {
            var sv = SceneView.currentDrawingSceneView;
            if (sv == null || sv.camera == null) return false;
            return (sv.camera.transform.position - p).sqrMagnitude < 90f * 90f;
        }

        static void Label(Vector3 p, string text, Color c)
        {
            if (!Near(p)) return;
            var old = Style.normal.textColor;
            Style.normal.textColor = c;
            Handles.Label(p, text, Style);
            Style.normal.textColor = old;
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawEnemy(EnemySpawnMarker m, GizmoType t)
        {
            string role = m.role == EnemyRole.Chefe ? "CHEFE " : m.role == EnemyRole.MiniChefe ? "Mini-chefe " : m.role == EnemyRole.Elite ? "Elite " : "";
            string ab = m.abilities != null && m.abilities.Length > 0 ? "\n" + string.Join(", ", m.abilities) : "";
            Label(m.transform.position + Vector3.up * 2.8f * Mathf.Max(0.6f, m.scale), role + m.enemyId + ab, m.GizmoColor());
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawChest(ChestMarker m, GizmoType t)
        {
            Label(m.transform.position + Vector3.up * 1.3f, (m.tier >= 2 ? "Baú raro" : "Baú") + (m.waitSignal ? " (sinal)" : ""), new Color(1f, 0.85f, 0.4f));
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawDoor(Door d, GizmoType t)
        {
            Label(d.transform.position + Vector3.up * 3.6f, d.secret ? "Parede secreta" : d.startOpen ? "Porta (aberta)" : "Porta (fechada)", new Color(1f, 0.6f, 0.4f));
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawLever(Lever l, GizmoType t)
        {
            if (l.hidden) return;
            Label(l.transform.position + Vector3.up * 1.6f, "Alavanca", new Color(1f, 0.9f, 0.3f));
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawSpawn(FloorSpawnPoint s, GizmoType t)
        {
            Label(s.transform.position + Vector3.up * 2f, "Entrada (" + s.key + ")", new Color(0.4f, 1f, 0.5f));
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawSanct(SanctuaryMarker s, GizmoType t)
        {
            Label(s.transform.position + Vector3.up * 3.2f, "Santuário", new Color(0.6f, 1f, 0.85f));
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawPortal(FloorPortalMarker p, GizmoType t)
        {
            Label(p.transform.position + Vector3.up * 2.6f, "Portal: " + p.label, new Color(0.8f, 0.6f, 1f));
        }

        [DrawGizmo(GizmoType.Selected)]
        static void DrawLink(TriggerLink l, GizmoType t)
        {
            Handles.color = new Color(1f, 0.9f, 0.2f);
            foreach (var r in l.targets)
                if (r != null) Handles.DrawDottedLine(l.transform.position + Vector3.up * 0.5f, r.transform.position + Vector3.up * 0.5f, 4f);
        }
    }
}
