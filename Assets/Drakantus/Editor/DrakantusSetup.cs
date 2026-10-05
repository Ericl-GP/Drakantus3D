using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Menus "Drakantus/..." que preparam o projeto: materiais, AnimatorController,
    /// biblioteca de modelos e cena do jogo. Roda sozinho uma vez quando o projeto é aberto
    /// e ainda não existe Assets/Drakantus/Resources/ModelLibrary.asset.
    /// </summary>
    [InitializeOnLoad]
    public static class DrakantusSetup
    {
        const string ResDir = "Assets/Drakantus/Resources";
        const string MatDir = ResDir + "/Materials";
        const string LibraryPath = ResDir + "/ModelLibrary.asset";
        const string ControllerPath = ResDir + "/CharacterAnimator.controller";
        const string SoftTexPath = MatDir + "/soft.png";
        const string SceneDir = "Assets/Scenes";
        const string ScenePath = SceneDir + "/Drakantus.unity";
        const string KayKitDir = "Assets/KayKit";
        const string AnimDir = KayKitDir + "/Animations";
        const string AutoKey = "Drakantus.AutoSetupDone";

        /// <summary>Estado do AnimatorController -> clipe KayKit (contrato).</summary>
        public static readonly string[,] StateClips =
        {
            { "Idle", "Idle_A" },
            { "Run", "Running_A" },
            { "Walk", "Walking_A" },
            { "Attack1", "Melee_1H_Attack_Slice_Diagonal" },
            { "Attack2", "Melee_1H_Attack_Chop" },
            { "Spin", "Melee_2H_Attack_Spin" },
            { "Cast", "Ranged_Magic_Shoot" },
            { "Shoot", "Ranged_Bow_Release" },
            { "Hit", "Hit_A" },
            { "Death", "Death_A" },
            { "Dodge", "Dodge_Forward" },
            { "Block", "Melee_Blocking" },
            { "Jump", "Jump_Full_Short" },
            { "Cheer", "Cheering" },
            { "Interact", "Interact" },
            { "Wave", "Waving" },
            { "Sit", "Sit_Floor_Idle" },
            { "SkelIdle", "Skeletons_Idle" },
            { "SkelWalk", "Skeletons_Walking" },
            { "SkelAttack", "Melee_1H_Attack_Chop" },
            { "SkelDeath", "Skeletons_Death" },
            { "SkelRise", "Skeletons_Awaken_Floor" },
            { "Taunt", "Skeletons_Taunt" },
            { "Summon", "Ranged_Magic_Summon" },
        };

        /// <summary>Velocidades especiais por estado (o resto fica 1).</summary>
        static readonly Dictionary<string, float> StateSpeeds = new Dictionary<string, float>
        {
            { "Attack1", 1.3f }, { "Attack2", 1.3f }, { "Shoot", 1.4f }, { "Cast", 1.3f }, { "Run", 1.0f },
            { "SkelAttack", 1.2f },
        };

        /// <summary>Aliases do contrato: id lógico -> nome do FBX (sem extensão).</summary>
        static readonly string[,] Aliases =
        {
            { "hero_Barbarian", "Barbarian" },
            { "hero_Knight", "Knight" },
            { "hero_Mage", "Mage" },
            { "hero_Ranger", "Ranger" },
            { "hero_Rogue", "Rogue" },
            { "hero_Rogue_Hooded", "Rogue_Hooded" },
            { "enemy_Skeleton_Minion", "Skeleton_Minion" },
            { "enemy_Skeleton_Warrior", "Skeleton_Warrior" },
            { "enemy_Skeleton_Rogue", "Skeleton_Rogue" },
            { "enemy_Skeleton_Mage", "Skeleton_Mage" },
            { "w_sword", "sword_1handed" },
            { "w_sword2h", "sword_2handed" },
            { "w_axe", "axe_1handed" },
            { "w_axe2h", "axe_2handed" },
            { "w_bow", "bow_withString" },
            { "w_crossbow", "crossbow_1handed" },
            { "w_dagger", "dagger" },
            { "w_staff", "staff" },
            { "w_wand", "wand" },
            { "w_shield", "shield_round" },
            { "w_spear", "spear_A" },
            { "w_hammer", "hammer_A" },
            { "w_skel_sword", "Skeleton_Blade" },
            { "w_skel_dagger", "Skeleton_Blade" },
            { "w_skel_axe", "Skeleton_Axe" },
            { "w_skel_crossbow", "Skeleton_Crossbow" },
            { "w_skel_staff", "Skeleton_Staff" },
            { "w_skel_shield", "Skeleton_Shield_Small_A" },
        };

        static int retries;

        static DrakantusSetup()
        {
            if (Application.isBatchMode) return;
            if (SessionState.GetBool(AutoKey, false)) return;
            if (File.Exists(LibraryPath)) return;
            EditorApplication.delayCall += AutoRun;
        }

        static void AutoRun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // tenta de novo mais tarde (não roda durante compilação/importação/Play)
                if (retries++ < 600) EditorApplication.delayCall += AutoRun;
                return;
            }
            if (SessionState.GetBool(AutoKey, false)) return;
            SessionState.SetBool(AutoKey, true);
            if (File.Exists(LibraryPath)) return;
            Debug.Log("[Drakantus] Primeira abertura do projeto: configurando tudo automaticamente...");
            ConfigurarTudoInterno(false);
        }

        // ------------------------------------------------------------------ menus
        [MenuItem("Drakantus/1 - Configurar tudo", false, 1)]
        public static void ConfigurarTudo() => ConfigurarTudoInterno(true);

        [MenuItem("Drakantus/2 - Remontar biblioteca de modelos", false, 2)]
        public static void MenuBiblioteca()
        {
            EnsureFolder(ResDir);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            BuildLibrary(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[Drakantus] Biblioteca de modelos remontada.");
        }

        [MenuItem("Drakantus/3 - Recriar animações", false, 3)]
        public static void MenuAnimacoes()
        {
            EnsureFolder(ResDir);
            // reimporta os FBX de animação para aplicar loops/configurações do KayKitImporter
            if (AssetDatabase.IsValidFolder(AnimDir))
                AssetDatabase.ImportAsset(AnimDir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            var ctrl = BuildController();
            var lib = LoadOrCreateLibrary();
            lib.characterAnimator = ctrl;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Drakantus] AnimatorController recriado.");
        }

        [MenuItem("Drakantus/4 - Abrir cena do jogo", false, 4)]
        public static void MenuAbrirCena()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static void ConfigurarTudoInterno(bool fromMenu)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (fromMenu) EditorUtility.DisplayDialog("Drakantus", "Saia do modo Play antes de configurar.", "OK");
                return;
            }
            try
            {
                EditorUtility.DisplayProgressBar("Drakantus", "Criando materiais...", 0.1f);
                EnsureFolder(ResDir);
                CreateMaterials();

                EditorUtility.DisplayProgressBar("Drakantus", "Montando animações...", 0.35f);
                var ctrl = BuildController();

                EditorUtility.DisplayProgressBar("Drakantus", "Montando biblioteca de modelos...", 0.6f);
                BuildLibrary(ctrl);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("Drakantus", "Criando cena...", 0.85f);
                EditorUtility.ClearProgressBar();
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("[Drakantus] Cena não criada (cancelado). Use 'Drakantus/4 - Abrir cena do jogo'.");
                    return;
                }
                CreateScene();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[Drakantus] Configuração concluída. Aperte Play.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Drakantus] Erro na configuração: " + ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            if (fromMenu) EditorUtility.DisplayDialog("Drakantus", "Pronto! Aperte Play.", "OK");
        }

        // ------------------------------------------------------------------ util
        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        // ------------------------------------------------------------------ (a) materiais
        static void CreateMaterials()
        {
            EnsureFolder(MatDir);
            var soft = CreateSoftTexture();

            // Lit
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) { litShader = Shader.Find("Standard"); Debug.LogWarning("[Drakantus] Shader URP/Lit não encontrado; usando Standard."); }
            var lit = LoadOrCreateMaterial(MatDir + "/Lit.mat", litShader);
            if (lit.HasProperty("_Smoothness")) lit.SetFloat("_Smoothness", 0.2f);
            if (lit.HasProperty("_BaseColor")) lit.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(lit);

            // FX
            var fxShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (fxShader == null) { fxShader = Shader.Find("Sprites/Default"); Debug.LogWarning("[Drakantus] Shader URP/Particles/Unlit não encontrado."); }
            var add = LoadOrCreateMaterial(MatDir + "/FxAdd.mat", fxShader);
            SetupFx(add, true, soft);
            var alpha = LoadOrCreateMaterial(MatDir + "/FxAlpha.mat", fxShader);
            SetupFx(alpha, false, soft);
            AssetDatabase.SaveAssets();
            Debug.Log("[Drakantus] Materiais criados em " + MatDir);
        }

        static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (shader != null && m.shader != shader) m.shader = shader;
            return m;
        }

        static void SetupFx(Material m, bool additive, Texture2D tex)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // dois lados (quads de efeito)
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD"); else m.DisableKeyword("_BLENDMODE_ADD");
            m.renderQueue = 3000;
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
        }

        static Texture2D CreateSoftTexture()
        {
            if (!File.Exists(SoftTexPath))
            {
                const int n = 64;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1 - d);
                        a = a * a * (3 - 2 * a);
                        t.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                t.Apply();
                File.WriteAllBytes(SoftTexPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(SoftTexPath, ImportAssetOptions.ForceSynchronousImport);
                var ti = AssetImporter.GetAtPath(SoftTexPath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    ti.alphaIsTransparency = true;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.mipmapEnabled = false;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SoftTexPath);
        }

        // ------------------------------------------------------------------ (b) animações
        /// <summary>Todos os clipes dos FBX de animação, por nome limpo (sem "Armature|").</summary>
        static Dictionary<string, AnimationClip> LoadAllClips()
        {
            var map = new Dictionary<string, AnimationClip>();
            if (!AssetDatabase.IsValidFolder(AnimDir))
            {
                Debug.LogWarning("[Drakantus] Pasta " + AnimDir + " não encontrada.");
                return map;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { AnimDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var clip = o as AnimationClip;
                    if (clip == null || clip.name.StartsWith("__preview__")) continue;
                    string n = KayKitImporter.CleanClipName(clip.name);
                    if (!map.ContainsKey(n)) map[n] = clip;
                }
            }
            Debug.Log("[Drakantus] " + map.Count + " clipes de animação encontrados.");
            return map;
        }

        static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            // tolerante: compara pelo final do nome, sem diferenciar maiúsculas
            string low = name.ToLowerInvariant();
            foreach (var kv in clips)
                if (kv.Key.ToLowerInvariant().EndsWith(low)) return kv.Value;
            return null;
        }

        static AnimatorController BuildController()
        {
            var clips = LoadAllClips();
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = ctrl.layers[0].stateMachine;
            var idleClip = FindClip(clips, "Idle_A");
            if (idleClip == null) Debug.LogWarning("[Drakantus] Clipe Idle_A não encontrado: os personagens ficarão parados.");

            AnimatorState idleState = null;
            int n = StateClips.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                string state = StateClips[i, 0];
                string clipName = StateClips[i, 1];
                var clip = FindClip(clips, clipName);
                if (clip == null)
                {
                    Debug.LogWarning("[Drakantus] Clipe '" + clipName + "' não encontrado para o estado '" + state + "'. Usando Idle.");
                    clip = idleClip;
                }
                var st = sm.AddState(state, new Vector3(260 + (i % 4) * 230, 40 + (i / 4) * 70, 0));
                st.motion = clip;
                st.speed = StateSpeeds.TryGetValue(state, out var sp) ? sp : 1f;
                st.writeDefaultValues = true;
                if (state == "Idle") idleState = st;
            }
            if (idleState != null) sm.defaultState = idleState;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[Drakantus] AnimatorController criado em " + ControllerPath + " com " + n + " estados.");
            return ctrl;
        }

        // ------------------------------------------------------------------ (c) biblioteca de modelos
        static ModelLibrary LoadOrCreateLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ModelLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ModelLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            return lib;
        }

        static void BuildLibrary(RuntimeAnimatorController ctrl)
        {
            var lib = LoadOrCreateLibrary();
            var byName = new Dictionary<string, GameObject>();
            int count = 0;
            if (AssetDatabase.IsValidFolder(KayKitDir))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { KayKitDir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                    if (!path.ToLowerInvariant().EndsWith(".fbx")) continue;
                    if (path.StartsWith(AnimDir + "/")) continue; // FBX só de animação
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    string id = Path.GetFileNameWithoutExtension(path);
                    if (byName.ContainsKey(id))
                    {
                        Debug.LogWarning("[Drakantus] Modelo com nome repetido: " + path + " (mantendo o primeiro).");
                        continue;
                    }
                    byName[id] = go;
                    lib.Set(id, go);
                    count++;
                }
            }
            else Debug.LogWarning("[Drakantus] Pasta " + KayKitDir + " não encontrada.");

            int al = 0;
            for (int i = 0; i < Aliases.GetLength(0); i++)
            {
                string alias = Aliases[i, 0], target = Aliases[i, 1];
                if (byName.TryGetValue(target, out var go)) { lib.Set(alias, go); al++; }
                else Debug.LogWarning("[Drakantus] Alias '" + alias + "': modelo '" + target + "' não encontrado.");
            }

            if (ctrl != null) lib.characterAnimator = ctrl;
            else Debug.LogWarning("[Drakantus] Sem AnimatorController: use 'Drakantus/3 - Recriar animações'.");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Drakantus] ModelLibrary: " + count + " modelos + " + al + " aliases.");
        }

        // ------------------------------------------------------------------ (d) cena
        static void CreateScene()
        {
            EnsureFolder(SceneDir);
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("DrakantusBoot");
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[Drakantus] Cena criada em " + ScenePath);
            }
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[Drakantus] Cena adicionada como primeira na Build.");
        }
    }
}
