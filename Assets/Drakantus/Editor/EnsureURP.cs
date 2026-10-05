using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Garante que o projeto usa o URP. Sem um Render Pipeline Asset ativo a Unity volta
    /// para o pipeline Built-in e todos os materiais URP ficam rosa.
    /// </summary>
    [InitializeOnLoad]
    public static class EnsureURP
    {
        static EnsureURP()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                var cur = GraphicsSettings.defaultRenderPipeline;
                // sem pipeline, ou com o asset de emergência (sem renderer) quando o do template existe
                bool pcExists = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset") != null;
                if (cur == null || (pcExists && cur.name != "PC_RPAsset")) Fix();
            };
        }

        [MenuItem("Drakantus/5 - Corrigir tela rosa (URP)")]
        public static void Fix()
        {
            UniversalRenderPipelineAsset asset = null;
            // prefere o asset de PC do template; senão qualquer URP asset do projeto
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (a == null) continue;
                if (asset == null || path.Contains("PC_")) asset = a;
            }
            if (asset == null)
            {
                const string dir = "Assets/Settings";
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Settings");
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, dir + "/Drakantus_Renderer.asset");
                asset = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(asset, dir + "/Drakantus_URP.asset");
                AssetDatabase.SaveAssets();
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Drakantus] URP ativado: " + AssetDatabase.GetAssetPath(asset));
        }
    }
}
