using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Pacotes da Asset Store feitos para o pipeline antigo (Built-in) ficam ROSA no URP.
    /// Este conversor acha esses materiais (fora de Assets/Drakantus e Assets/Floors)
    /// e troca o sombreador para o URP, mantendo textura, cor, normal, brilho e emissão.
    /// Roda sozinho uma vez por sessão e também pelo menu Drakantus.
    /// </summary>
    [InitializeOnLoad]
    public static class PackMaterialConverter
    {
        const string SessionKey = "Drakantus.PackMatConv.v1";

        static PackMaterialConverter()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Convert(false);
            };
        }

        [MenuItem("Drakantus/6 - Corrigir materiais rosa dos pacotes (URP)")]
        static void MenuConvert() => Convert(true);

        static readonly HashSet<string> UrpTags = new HashSet<string>
        { "", "UniversalForward", "UniversalForwardOnly", "SRPDefaultUnlit", "Universal2D", "UniversalGBuffer" };

        /// <summary>O sombreador tem algum passe que o URP desenha?</summary>
        static bool Renderable(Shader s)
        {
            if (s == null || s.name == "Hidden/InternalErrorShader") return false;
            if (!s.isSupported) return false;
            if (s.name.StartsWith("Skybox/")) return true; // céus funcionam no URP
            var lightMode = new ShaderTagId("LightMode");
            // Passes do sub-sombreador ativo (o que a placa de vídeo usa).
            for (int p = 0; p < s.passCount; p++)
            {
                string tag = s.FindPassTagValue(p, lightMode).name ?? "";
                if (UrpTags.Contains(tag)) return true;
            }
            return false;
        }

        static bool Skip(string path) =>
            path.StartsWith("Assets/Drakantus/") || path.StartsWith("Assets/Floors/") ||
            path.StartsWith("Packages/") || !path.EndsWith(".mat");

        public static int Convert(bool verbose)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var vcol = Shader.Find("Drakantus/VertexColorLit");
            if (lit == null) { if (verbose) Debug.LogWarning("[Drakantus] URP Lit não encontrado — rode o menu 5 antes."); return 0; }

            var log = new StringBuilder();
            int n = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Skip(path)) continue;
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m == null || Renderable(m.shader)) continue;

                    string oldName = m.shader != null ? m.shader.name : "(nenhum)";
                    string lower = (oldName + " " + m.name).ToLowerInvariant();
                    Shader target =
                        lower.Contains("vertex") && vcol != null ? vcol :
                        lower.StartsWith("particles/") && particles != null ? particles :
                        (lower.StartsWith("unlit/") || lower.Contains("unlit")) && unlit != null ? unlit : lit;

                    ConvertOne(m, target);
                    EditorUtility.SetDirty(m);
                    n++;
                    log.AppendLine($"  {path}: {oldName} -> {target.name}");
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();

            if (n > 0 || verbose)
                Debug.Log($"[Drakantus] Materiais convertidos para URP: {n}\n{log}");
            return n;
        }

        static void ConvertOne(Material m, Shader target)
        {
            // Lê do formato antigo antes de trocar.
            Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Vector2 st = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 off = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            Texture emiTex = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            Color emi = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.2f;
            float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f; // 0 opaco 1 recorte 2 fade 3 transparente
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            bool emissive = m.IsKeywordEnabled("_EMISSION") || emi.maxColorComponent > 0.01f;

            m.shader = target;

            if (m.HasProperty("_BaseMap"))
            {
                if (mainTex != null) m.SetTexture("_BaseMap", mainTex);
                m.SetTextureScale("_BaseMap", st);
                m.SetTextureOffset("_BaseMap", off);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (bump != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Min(gloss, 0.6f));
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (emissive && m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", emi);
                if (emiTex != null) m.SetTexture("_EmissionMap", emiTex);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (target.name.StartsWith("Universal Render Pipeline/"))
            {
                if (mode == 1f && m.HasProperty("_AlphaClip"))
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", cutoff);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                }
                else if (mode >= 2f && m.HasProperty("_Surface"))
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)RenderQueue.Transparent;
                    m.SetOverrideTag("RenderType", "Transparent");
                }
            }
        }
    }
}
