using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Resultado de LevelBuilder.Build: tudo que o Game precisa para popular o mapa.</summary>
    public class LevelInfo
    {
        public string id = "";
        public string title = "";            // ex.: "Praça de Aster"
        public string subtitle = "";         // ex.: "Andar 01 · Bosque Esmeralda"
        public string kind = "hub";          // "hub", "interior" ou "dungeon"
        public int floor;                    // 0 = fora da torre
        public string next = "";             // id do próximo andar ("" = nenhum)
        public string music = "town";        // id para Sfx.Music
        public Color ambient = new Color(0.55f, 0.55f, 0.6f);
        public Color sun = new Color(1f, 0.95f, 0.85f);
        public float sunIntensity = 1.2f;
        public bool dark;                    // true = masmorra escura (tocha do herói acesa)
        public Color fog = new Color(0.6f, 0.7f, 0.8f);
        public float fogDensity = 0.012f;
        public Bounds bounds = new Bounds(Vector3.zero, new Vector3(60, 10, 60));
        public readonly Dictionary<string, Vector3> spawns = new();     // "default", "entrance", "tower_door"...
        public readonly List<EnemySpawn> enemies = new();
        public readonly List<NpcSpawn> npcs = new();
        public readonly List<PortalSpawn> portals = new();
        public bool hasSanctuary;
        public Vector3 sanctuary;

        public Vector3 Spawn(string key)
        {
            if (key != null && spawns.TryGetValue(key, out var p)) return p;
            if (spawns.TryGetValue("default", out var d)) return d;
            return Vector3.zero;
        }
    }

    public struct EnemySpawn
    {
        public string id; public Vector3 pos; public float scale;
        public EnemySpawn(string id, Vector3 pos, float scale = 1f) { this.id = id; this.pos = pos; this.scale = scale; }
    }

    public struct NpcSpawn
    {
        public string npcId; public Vector3 pos; public float yaw;
        public NpcSpawn(string npcId, Vector3 pos, float yaw = 180f) { this.npcId = npcId; this.pos = pos; this.yaw = yaw; }
    }

    public struct PortalSpawn
    {
        public Vector3 pos; public float radius; public string targetMap, targetSpawn, label; public bool needsRegistration;
        public PortalSpawn(Vector3 pos, string targetMap, string targetSpawn, string label, float radius = 1.6f, bool needsRegistration = false)
        { this.pos = pos; this.targetMap = targetMap; this.targetSpawn = targetSpawn; this.label = label; this.radius = radius; this.needsRegistration = needsRegistration; }
    }
}
