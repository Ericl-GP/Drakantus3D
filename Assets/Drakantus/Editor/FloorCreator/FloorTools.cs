using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drakantus.EditorTools
{
    /// <summary>Gerar na cena do andar, salvar prefab + floors.json, testar no Play (com cópia do save).</summary>
    public static class FloorTools
    {
        const string KeyReturnScene = "Drakantus.FloorCreator.ReturnScene";
        const string KeyBackup = "Drakantus.FloorCreator.SaveBackup";

        static string SavePath => Path.Combine(Application.persistentDataPath, "drakantus_save.json");
        static string BackupPath => SavePath + ".criador_bak";

        // ------------------------------------------------------------------ cena do andar
        public static string ScenePathFor(FloorSettings s) => FloorKit.FloorsDir + "/" + s.SceneName + ".unity";

        /// <summary>Abre (ou cria) Assets/Floors/Andar_XX.unity. false se o usuário cancelou.</summary>
        public static bool OpenFloorScene(FloorSettings s)
        {
            string path = ScenePathFor(s);
            var active = SceneManager.GetActiveScene();
            if (active.path == path) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            FloorKit.EnsureFolder(FloorKit.FloorsDir);
            if (File.Exists(path)) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            else
            {
                var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(sc, path);
            }
            return true;
        }

        public static FloorRoot FindRoot(string floorId)
        {
            foreach (var fr in Object.FindObjectsByType<FloorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (fr.floorId == floorId) return fr;
            return null;
        }

        public static FloorRoot SelectedOrAny()
        {
            var sel = Selection.activeGameObject;
            if (sel != null)
            {
                var fr = sel.GetComponentInParent<FloorRoot>();
                if (fr != null) return fr;
            }
            return Object.FindFirstObjectByType<FloorRoot>();
        }

        /// <summary>GERAR ANDAR COMPLETO: abre a cena do andar, apaga o andar antigo e gera de novo.</summary>
        public static FloorRoot GenerateInScene(FloorSettings s)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Criador de Andares", "Saia do modo Play antes de gerar.", "OK");
                return null;
            }
            if (!OpenFloorScene(s)) return null;
            try
            {
                EditorUtility.DisplayProgressBar("Criador de Andares", "Gerando " + s.SceneName + "...", 0.3f);
                foreach (var old in Object.FindObjectsByType<FloorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (old.floorId == s.FloorId || old.gameObject.name == s.SceneName) Undo.DestroyObjectImmediate(old.gameObject);
                var go = FloorGenerator.Generate(s);
                Undo.RegisterCreatedObjectUndo(go, "Gerar andar");
                var fr = go.GetComponent<FloorRoot>();
                PreviewLighting(fr);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(go.scene);
                EditorSceneManager.SaveScene(go.scene);
                Selection.activeGameObject = go;
                Frame(fr);
                return fr;
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Criador de Andares", "Erro ao gerar: " + ex.Message, "OK");
                return null;
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        /// <summary>Sol/ambiente/névoa da cena do andar parecidos com o jogo (só para ver no editor).</summary>
        public static void PreviewLighting(FloorRoot fr)
        {
            if (fr == null) return;
            GameObject sunGo = GameObject.Find("Previa_Sol");
            if (sunGo == null)
            {
                sunGo = new GameObject("Previa_Sol");
                sunGo.tag = "EditorOnly";
                var l = sunGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.shadows = LightShadows.Soft;
            }
            var sun = sunGo.GetComponent<Light>();
            sunGo.transform.rotation = Quaternion.Euler(fr.sunPitch, fr.sunYaw, 0f);
            sun.color = fr.sun;
            sun.intensity = fr.sunIntensity;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = fr.ambient;
            RenderSettings.fog = fr.fogDensity > 0f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fr.fog;
            RenderSettings.fogDensity = fr.fogDensity * 0.5f;   // mais leve no editor para enxergar o andar todo
        }

        public static void Frame(FloorRoot fr)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null || fr == null) return;
            Vector3 c = fr.transform.TransformPoint(fr.boundsCenter);
            float size = Mathf.Max(fr.boundsSize.x, fr.boundsSize.z) * 0.6f;
            sv.LookAt(c, Quaternion.Euler(55f, 45f, 0f), size);
            sv.Repaint();
        }

        // ------------------------------------------------------------------ salvar
        public static string PrefabPath(string id) => FloorKit.PrefabDir + "/" + id + ".prefab";

        /// <summary>Salva o prefab em Resources/Floors e registra em floors.json.</summary>
        public static bool SaveFloor(FloorRoot fr)
        {
            if (fr == null) { EditorUtility.DisplayDialog("Criador de Andares", "Nenhum andar (FloorRoot) na cena.", "OK"); return false; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (string.IsNullOrEmpty(fr.floorId)) fr.floorId = "tower_f" + fr.floor;
            FloorKit.EnsureFolder(FloorKit.PrefabDir);
            AssetDatabase.SaveAssets();   // materiais novos antes do prefab
            bool ok;
            PrefabUtility.SaveAsPrefabAsset(fr.gameObject, PrefabPath(fr.floorId), out ok);
            if (!ok)
            {
                EditorUtility.DisplayDialog("Criador de Andares", "Não foi possível salvar o prefab do andar.", "OK");
                return false;
            }
            Register(fr);
            if (fr.gameObject.scene.IsValid() && !string.IsNullOrEmpty(fr.gameObject.scene.path))
                EditorSceneManager.SaveScene(fr.gameObject.scene);
            Debug.Log("[Drakantus] Andar salvo: " + PrefabPath(fr.floorId) + " (id " + fr.floorId + ")");
            return true;
        }

        static void Register(FloorRoot fr)
        {
            var list = new FloorList();
            if (File.Exists(FloorKit.FloorsJson))
            {
                try
                {
                    var l = JsonUtility.FromJson<FloorList>(File.ReadAllText(FloorKit.FloorsJson));
                    if (l != null && l.floors != null) list = l;
                }
                catch (System.Exception ex) { Debug.LogWarning("[Drakantus] floors.json inválido, recriando: " + ex.Message); }
            }
            list.floors.RemoveAll(f => f == null || f.id == fr.floorId);
            list.floors.Add(new FloorEntry
            {
                id = fr.floorId,
                name = fr.floorName,
                subtitle = fr.subtitle,
                biome = fr.biome,
                next = fr.next,
                floor = fr.floor,
                recommendedLevel = fr.recommendedLevel,
                difficulty = fr.difficulty,
                builtIn = false,
            });
            list.floors.Sort((a, b) => a.floor.CompareTo(b.floor));
            FloorKit.EnsureFolder(Path.GetDirectoryName(FloorKit.FloorsJson).Replace('\\', '/'));
            File.WriteAllText(FloorKit.FloorsJson, JsonUtility.ToJson(list, true));
            AssetDatabase.ImportAsset(FloorKit.FloorsJson);
            FloorRegistry.Reload();
        }

        /// <summary>Remove o andar da lista e apaga o prefab (a cena fica).</summary>
        public static void Unregister(string id)
        {
            if (File.Exists(FloorKit.FloorsJson))
            {
                var l = JsonUtility.FromJson<FloorList>(File.ReadAllText(FloorKit.FloorsJson));
                if (l != null && l.floors != null)
                {
                    l.floors.RemoveAll(f => f == null || f.id == id);
                    File.WriteAllText(FloorKit.FloorsJson, JsonUtility.ToJson(l, true));
                    AssetDatabase.ImportAsset(FloorKit.FloorsJson);
                }
            }
            if (File.Exists(PrefabPath(id))) AssetDatabase.DeleteAsset(PrefabPath(id));
            FloorRegistry.Reload();
        }

        // ------------------------------------------------------------------ testar
        public static void TestFloor(FloorRoot fr, int level, string classId)
        {
            if (!File.Exists(FloorKit.GameScene))
            {
                EditorUtility.DisplayDialog("Criador de Andares", "Cena do jogo não encontrada (" + FloorKit.GameScene + ").\nRode o menu Drakantus > 1 - Configurar tudo.", "OK");
                return;
            }
            if (!SaveFloor(fr)) return;
            string ret = fr.gameObject.scene.path;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BackupSave();
            EditorPrefs.SetString(KeyReturnScene, ret ?? "");
            PlayerPrefs.SetString(FloorTestBoot.KeyFloor, fr.floorId);
            PlayerPrefs.SetInt(FloorTestBoot.KeyLevel, Mathf.Max(1, level));
            PlayerPrefs.SetString(FloorTestBoot.KeyClass, classId ?? "");
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene(FloorKit.GameScene, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void BackupSave()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Copy(SavePath, BackupPath, true);
                    EditorPrefs.SetString(KeyBackup, "copy");
                }
                else EditorPrefs.SetString(KeyBackup, "none");
            }
            catch (System.Exception ex) { Debug.LogWarning("[Drakantus] Não consegui copiar o save antes do teste: " + ex.Message); }
        }

        internal static void RestoreSave()
        {
            string mode = EditorPrefs.GetString(KeyBackup, "");
            if (string.IsNullOrEmpty(mode)) return;
            try
            {
                if (mode == "copy" && File.Exists(BackupPath))
                {
                    File.Copy(BackupPath, SavePath, true);
                    File.Delete(BackupPath);
                }
                else if (mode == "none" && File.Exists(SavePath)) File.Delete(SavePath);
                Debug.Log("[Drakantus] Save original restaurado depois do teste do andar.");
            }
            catch (System.Exception ex) { Debug.LogWarning("[Drakantus] Não consegui restaurar o save: " + ex.Message + " (cópia em " + BackupPath + ")"); }
            EditorPrefs.DeleteKey(KeyBackup);
        }

        internal static void AfterTest()
        {
            RestoreSave();
            if (PlayerPrefs.HasKey(FloorTestBoot.KeyFloor)) { PlayerPrefs.DeleteKey(FloorTestBoot.KeyFloor); PlayerPrefs.Save(); }
            string ret = EditorPrefs.GetString(KeyReturnScene, "");
            EditorPrefs.DeleteKey(KeyReturnScene);
            if (!string.IsNullOrEmpty(ret) && File.Exists(ret))
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().path != ret)
                        EditorSceneManager.OpenScene(ret, OpenSceneMode.Single);
                };
        }
    }

    /// <summary>Ao sair do Play depois de "Testar andar": devolve o save e reabre a cena do andar.</summary>
    [InitializeOnLoad]
    static class FloorTestWatcher
    {
        static FloorTestWatcher()
        {
            EditorApplication.playModeStateChanged += OnState;
        }

        static void OnState(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredEditMode) FloorTools.AfterTest();
        }
    }
}
