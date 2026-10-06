using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Drakantus > Mapas: transforma um mapa gerado por código (praça, guilda) num PREFAB editável.
    ///
    /// Como usar: aperte Play, espere o jogo carregar, e escolha o menu. O exportador gera o mapa de novo
    /// (mesma semente = mesmo resultado), salva em Assets/Drakantus/Resources/Floors/&lt;id&gt;.prefab e
    /// guarda em Assets/Drakantus/MapAssets/&lt;id&gt;/ os materiais, texturas e malhas que o código criou
    /// (sem isso o prefab ficaria rosa/vazio). Daí em diante o LevelBuilder usa o prefab no lugar do código.
    /// Para voltar ao código: Drakantus > Mapas > Apagar prefab e voltar ao mapa por código.
    ///
    /// NPCs, portais e pontos de chegada viram objetos marcadores (NpcMarker, FloorPortalMarker, FloorSpawnPoint)
    /// dentro de "Marcadores": mova, apague ou duplique à vontade.
    /// </summary>
    public static class MapExporter
    {
        const string FloorsDir = "Assets/Drakantus/Resources/Floors";
        const string AssetsRoot = "Assets/Drakantus/MapAssets";

        [MenuItem("Drakantus/Mapas/Exportar Praça (town) para prefab editável")]
        static void ExportTown() { Export("town"); }
        [MenuItem("Drakantus/Mapas/Exportar Praça (town) para prefab editável", true)]
        static bool ExportTownOk() { return Application.isPlaying; }

        [MenuItem("Drakantus/Mapas/Exportar Guilda (guild) para prefab editável")]
        static void ExportGuild() { Export("guild"); }
        [MenuItem("Drakantus/Mapas/Exportar Guilda (guild) para prefab editável", true)]
        static bool ExportGuildOk() { return Application.isPlaying; }

        [MenuItem("Drakantus/Mapas/Agrupar hitboxes soltas no prefab da Praça")]
        static void GroupTown() { GroupInPrefab("town"); }
        [MenuItem("Drakantus/Mapas/Agrupar hitboxes soltas no prefab da Praça", true)]
        static bool GroupTownOk() { return !Application.isPlaying; }

        [MenuItem("Drakantus/Mapas/Agrupar hitboxes soltas no prefab da Guilda")]
        static void GroupGuild() { GroupInPrefab("guild"); }
        [MenuItem("Drakantus/Mapas/Agrupar hitboxes soltas no prefab da Guilda", true)]
        static bool GroupGuildOk() { return !Application.isPlaying; }

        [MenuItem("Drakantus/Mapas/Apagar prefab da Praça e voltar ao mapa por código")]
        static void DeleteTown() { DeletePrefab("town"); }
        [MenuItem("Drakantus/Mapas/Apagar prefab da Guilda e voltar ao mapa por código")]
        static void DeleteGuild() { DeletePrefab("guild"); }

        static void DeletePrefab(string id)
        {
            string path = FloorsDir + "/" + id + ".prefab";
            if (!File.Exists(path)) { EditorUtility.DisplayDialog("Mapas", "Não existe prefab de " + id + ".", "OK"); return; }
            if (!EditorUtility.DisplayDialog("Apagar prefab?", "Isto apaga " + path + " e o jogo volta a gerar \"" + id + "\" por código. As suas edições no prefab serão perdidas.", "Apagar", "Cancelar")) return;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ exportação
        static void Export(string mapId)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Mapas", "Aperte Play primeiro (o mapa precisa ser gerado com o jogo rodando).", "OK");
                return;
            }
            string prefabPath = FloorsDir + "/" + mapId + ".prefab";
            if (File.Exists(prefabPath) && !EditorUtility.DisplayDialog("Substituir prefab?",
                prefabPath + " já existe. Exportar de novo SUBSTITUI o prefab e perde as edições feitas nele.", "Substituir", "Cancelar")) return;

            Directory.CreateDirectory(FloorsDir);
            var sun = RenderSettings.sun != null ? RenderSettings.sun.transform : null;
            Quaternion oldSun = sun != null ? sun.rotation : Quaternion.identity;

            var root = new GameObject(mapId);
            try
            {
                LevelInfo info = LevelBuilder.BuildFromCode(mapId, root.transform);
                if (info == null) throw new System.Exception("LevelBuilder devolveu null para " + mapId);

                // ângulos do sol que o LevelBuilder acabou de aplicar
                float sunPitch = 50f, sunYaw = -30f;
                if (sun != null) { var e = sun.eulerAngles; sunPitch = e.x; sunYaw = e.y; sun.rotation = oldSun; }

                BuildRoot(root, info, mapId, sunPitch, sunYaw);
                int orphans;
                int grouped = GroupHitboxes(root, out orphans);
                Debug.Log("[Drakantus] Hitboxes agrupadas com o modelo: " + grouped + (orphans > 0 ? " (sem par: " + orphans + ")" : ""));
                int saved = PersistAssets(root, mapId);

                bool ok;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out ok);
                if (!ok) throw new System.Exception("SaveAsPrefabAsset falhou");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (asset != null) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
                Debug.Log("[Drakantus] Mapa \"" + mapId + "\" exportado: " + prefabPath + " (" + saved + " assets em " + AssetsRoot + "/" + mapId + ")");
                EditorUtility.DisplayDialog("Mapas",
                    "Exportado: " + prefabPath + "\n\nPare o Play e abra o prefab (duplo clique) para editar. Ao reiniciar o Play o jogo já usa o prefab.", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Mapas", "Falhou: " + ex.Message + "\n(veja o Console)", "OK");
            }
            finally
            {
                if (sun != null) sun.rotation = oldSun;
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>FloorRoot + marcadores (chegadas, NPCs, portais, inimigos, santuário) a partir do LevelInfo.</summary>
        static void BuildRoot(GameObject root, LevelInfo info, string mapId, float sunPitch, float sunYaw)
        {
            var fr = root.AddComponent<FloorRoot>();
            fr.floorId = mapId;
            fr.title = info.title; fr.subtitle = info.subtitle; fr.floorName = info.title;
            fr.kind = info.kind; fr.floor = info.floor; fr.next = info.next; fr.music = info.music;
            fr.ambient = info.ambient; fr.sun = info.sun; fr.sunIntensity = info.sunIntensity;
            fr.sunPitch = sunPitch; fr.sunYaw = sunYaw;
            fr.dark = info.dark; fr.fog = info.fog; fr.fogDensity = info.fogDensity;
            fr.boundsCenter = info.bounds.center; fr.boundsSize = info.bounds.size;
            fr.biome = ""; fr.generatorSettings = "Exportado de LevelBuilder.BuildFromCode(\"" + mapId + "\")";

            var mk = new GameObject("Marcadores").transform;
            mk.SetParent(root.transform, false);

            foreach (var kv in info.spawns)
            {
                var g = new GameObject("Chegada_" + kv.Key);
                g.transform.SetParent(mk, false);
                g.transform.position = kv.Value;
                g.AddComponent<FloorSpawnPoint>().key = kv.Key;
            }
            foreach (var n in info.npcs)
            {
                var g = new GameObject("Npc_" + n.npcId);
                g.transform.SetParent(mk, false);
                g.transform.SetPositionAndRotation(n.pos, Quaternion.Euler(0f, n.yaw, 0f));
                g.AddComponent<NpcMarker>().npcId = n.npcId;
            }
            foreach (var p in info.portals)
            {
                var g = new GameObject("Portal_" + p.targetMap);
                g.transform.SetParent(mk, false);
                g.transform.position = p.pos;
                var m = g.AddComponent<FloorPortalMarker>();
                m.targetMap = p.targetMap; m.targetSpawn = p.targetSpawn; m.label = p.label;
                m.radius = p.radius; m.needsRegistration = p.needsRegistration;
            }
            foreach (var e in info.enemies)
            {
                var g = new GameObject("Inimigo_" + e.id);
                g.transform.SetParent(mk, false);
                g.transform.position = e.pos;
                var m = g.AddComponent<EnemySpawnMarker>();
                m.enemyId = e.id; m.scale = e.scale;
            }
            if (info.hasSanctuary)
            {
                var g = new GameObject("Santuario");
                g.transform.SetParent(mk, false);
                g.transform.position = info.sanctuary;
                g.AddComponent<SanctuaryMarker>();
            }
        }

        // ------------------------------------------------------------------ hitboxes soltas
        // O código cria o colisor de bancos, barracas, postes e troncos como um objeto invisível IRMÃO do modelo.
        // Ao mover só o modelo, o hitbox fica para trás. Aqui cada par vira "<modelo>_Grupo" (escala 1) com os dois dentro.
        static readonly string[] PropHitboxes = { "Colisor_Banco", "Colisor_Barraca", "Colisor_Poste", "Tronco" };

        static bool IsPropHitbox(Transform t)
        {
            if (t.GetComponent<Collider>() == null || t.GetComponent<Renderer>() != null) return false;
            foreach (var n in PropHitboxes) if (t.name == n) return true;
            return false;
        }

        static bool HasRenderer(Transform t) { return t.GetComponentInChildren<Renderer>(true) != null; }

        static bool BoundsOf(Transform t, out Bounds b)
        {
            b = new Bounds(t.position, Vector3.zero);
            bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>Junta cada hitbox solta ao modelo mais próximo. Devolve quantas agrupou; orphans = sem modelo por perto.</summary>
        public static int GroupHitboxes(GameObject root, out int orphans)
        {
            orphans = 0;
            var cols = new List<Transform>();
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (IsPropHitbox(c.transform) && !cols.Contains(c.transform)) cols.Add(c.transform);

            // passo 1: escolher o par de cada hitbox (antes de mexer na hierarquia)
            var pairs = new List<KeyValuePair<Transform, Transform>>();
            foreach (var ct in cols)
            {
                var parent = ct.parent;
                if (parent == null || parent.name.EndsWith("_Grupo")) continue;   // já agrupada
                Transform best = null;
                float bestD = 1.0f;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var v = parent.GetChild(i);
                    if (v == ct || IsPropHitbox(v) || v.name.EndsWith("_Grupo") || !HasRenderer(v)) continue;
                    Vector3 d = v.position - ct.position; d.y = 0f;
                    if (d.magnitude < bestD) { bestD = d.magnitude; best = v; }
                }
                if (best == null)   // plano B: o centro da hitbox cai dentro da caixa do modelo (em XZ)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var v = parent.GetChild(i);
                        if (v == ct || IsPropHitbox(v) || v.name.EndsWith("_Grupo")) continue;
                        Bounds b;
                        if (!BoundsOf(v, out b)) continue;
                        b.Expand(new Vector3(0.3f, 100f, 0.3f));
                        if (b.Contains(new Vector3(ct.position.x, b.center.y, ct.position.z))) { best = v; break; }
                    }
                }
                if (best == null) { orphans++; continue; }
                pairs.Add(new KeyValuePair<Transform, Transform>(ct, best));
            }

            // passo 2: criar o grupo (escala 1, na posição/rotação do modelo) e mover os dois para dentro
            var wrappers = new Dictionary<Transform, Transform>();
            foreach (var pr in pairs)
            {
                Transform wrap;
                if (!wrappers.TryGetValue(pr.Value, out wrap))
                {
                    var parent = pr.Value.parent;
                    var g = new GameObject(pr.Value.name + "_Grupo");
                    g.transform.SetParent(parent, false);
                    g.transform.SetPositionAndRotation(pr.Value.position, pr.Value.rotation);
                    pr.Value.SetParent(g.transform, true);
                    wrap = g.transform;
                    wrappers[pr.Value] = wrap;
                }
                pr.Key.SetParent(wrap, true);
            }
            return pairs.Count;
        }

        /// <summary>Roda o agrupamento num prefab já exportado (fora do Play).</summary>
        static void GroupInPrefab(string id)
        {
            string path = FloorsDir + "/" + id + ".prefab";
            if (!File.Exists(path)) { EditorUtility.DisplayDialog("Mapas", "Não existe " + path + ". Exporte o mapa primeiro.", "OK"); return; }
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int orphans;
                int n = GroupHitboxes(contents, out orphans);
                if (n > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
                EditorUtility.DisplayDialog("Mapas", "Hitboxes agrupadas com o modelo: " + n + (orphans > 0 ? "\nSem modelo por perto (movidos antes?): " + orphans : "") +
                    (n == 0 ? "\nNada a fazer (já agrupadas ou não encontradas)." : ""), "OK");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // ------------------------------------------------------------------ materiais/malhas/texturas criados por código
        /// <summary>Salva como assets tudo que o código criou em runtime (não é asset) e troca as referências.</summary>
        static int PersistAssets(GameObject root, string mapId)
        {
            string dir = AssetsRoot + "/" + mapId;
            EnsureFolder(AssetsRoot);
            EnsureFolder(dir);
            // limpa a exportação anterior deste mapa
            foreach (var guid in AssetDatabase.FindAssets("", new[] { dir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            var map = new Dictionary<Object, Object>();
            int count = 0;

            T Persist<T>(T obj, string ext) where T : Object
            {
                if (obj == null || EditorUtility.IsPersistent(obj)) return obj;
                if (map.TryGetValue(obj, out var done)) return (T)done;
                string name = string.IsNullOrEmpty(obj.name) ? typeof(T).Name : obj.name;
                foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                string path = dir + "/" + (count++).ToString("0000") + "_" + name + ext;
                AssetDatabase.CreateAsset(obj, path);
                map[obj] = obj;   // o próprio objeto vira o asset
                return obj;
            }

            Material FixMaterial(Material m)
            {
                if (m == null || EditorUtility.IsPersistent(m)) return m;
                if (map.ContainsKey(m)) return m;
                // texturas primeiro (senão a referência ficaria presa a um objeto de cena)
                foreach (var prop in m.GetTexturePropertyNames())
                {
                    var t = m.GetTexture(prop);
                    if (t != null && !EditorUtility.IsPersistent(t)) m.SetTexture(prop, Persist(t, ".asset"));
                }
                return Persist(m, ".mat");
            }

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var fixedMat = FixMaterial(mats[i]);
                    if (fixedMat != mats[i]) { mats[i] = fixedMat; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !EditorUtility.IsPersistent(mf.sharedMesh)) mf.sharedMesh = Persist(mf.sharedMesh, ".asset");
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                if (mc.sharedMesh != null && !EditorUtility.IsPersistent(mc.sharedMesh)) mc.sharedMesh = Persist(mc.sharedMesh, ".asset");

            return count;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
