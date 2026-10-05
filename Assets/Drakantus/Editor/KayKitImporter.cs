using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Configura a importação dos modelos KayKit (Assets/KayKit/**) e da folha de ícones.
    ///
    /// Decisões:
    /// - Personagens e FBX de animação: rig Generic, avatar criado a partir do próprio modelo.
    ///   Todos usam a mesma hierarquia "Rig_Medium/root/hips/...", então os clipes Generic
    ///   (que se ligam por caminho de transform) tocam em qualquer personagem.
    /// - Só os FBX de Animations importam animação; personagens e props não.
    /// - Materiais: ImportViaMaterialDescription + materialLocation InPrefab (materiais embutidos no FBX).
    ///   O URP converte a descrição do FBX em URP/Lit e o Unity acha a textura pelo nome do arquivo
    ///   na mesma pasta do FBX (todas as texturas KayKit estão ao lado dos modelos). Assim não
    ///   precisamos extrair .mat nem fazer remapeamento manual. OnPostprocessMaterial deixa todos
    ///   opacos, com cor base branca e pouco brilho (estilo low-poly).
    /// - Escala: os FBX vêm com UnitScaleFactor 100 (metros) -> useFileUnits + globalScale 1 = 1 unidade = 1 m.
    /// </summary>
    public class KayKitImporter : AssetPostprocessor
    {
        const string Root = "Assets/KayKit/";
        const string IconsPath = "Assets/Drakantus/Resources/UI/icons_32.png";

        // aumente quando mudar as regras abaixo para forçar reimportação
        public override uint GetVersion() => 3;

        /// <summary>Clipes que devem tocar em loop.</summary>
        public static readonly HashSet<string> LoopClips = new HashSet<string>
        {
            "Idle_A", "Idle_B", "Running_A", "Running_B", "Walking_A", "Walking_B", "Walking_C",
            "Melee_Blocking", "Melee_2H_Idle", "Ranged_Bow_Idle", "Ranged_Bow_Aiming_Idle",
            "Skeletons_Idle", "Skeletons_Walking", "Sit_Floor_Idle", "Sit_Chair_Idle", "Waving",
            "Melee_2H_Attack_Spinning", "Ranged_Magic_Spellcasting", "Running_HoldingBow",
            "Running_HoldingRifle", "Crouching", "Sneaking", "Crawling", "Running_Strafe_Left",
            "Running_Strafe_Right", "Walking_Backwards", "Cheering", "Jump_Idle"
        };

        static bool InKayKit(string path) => path.Replace('\\', '/').StartsWith(Root);
        static bool IsCharacter(string path) => path.Replace('\\', '/').StartsWith(Root + "Characters/");
        static bool IsAnimation(string path) => path.Replace('\\', '/').StartsWith(Root + "Animations/");

        /// <summary>Nome do clipe sem prefixos tipo "Armature|" ou "Rig_Medium|".</summary>
        public static string CleanClipName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            int i = n.LastIndexOf('|');
            return i >= 0 ? n.Substring(i + 1) : n;
        }

        // ------------------------------------------------------------------ modelos
        void OnPreprocessModel()
        {
            if (!InKayKit(assetPath)) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null) return;

            bool character = IsCharacter(assetPath);
            bool anim = IsAnimation(assetPath);

            // escala
            mi.globalScale = 1f;
            mi.useFileUnits = true;
            mi.bakeAxisConversion = false;

            // malha
            mi.isReadable = false;
            mi.addCollider = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = true;
            mi.importBlendShapes = false;
            mi.generateSecondaryUV = false;

            // rig / animação
            if (character || anim)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.optimizeGameObjects = false; // precisamos dos ossos handslot.r / handslot.l
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
            }
            mi.importAnimation = anim;
            if (anim)
            {
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
                mi.resampleCurves = true;
            }

            // materiais embutidos (ver comentário da classe)
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.materialSearch = ModelImporterMaterialSearch.Local;
            mi.materialName = ModelImporterMaterialName.BasedOnTextureName;
        }

        void OnPreprocessAnimation()
        {
            if (!IsAnimation(assetPath)) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null) return;
            var defs = mi.defaultClipAnimations;
            if (defs == null || defs.Length == 0) return;
            var clips = new ModelImporterClipAnimation[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var c = defs[i];
                string n = CleanClipName(c.name);
                bool loop = LoopClips.Contains(n);
                c.loopTime = loop;
                c.loopPose = false;
                c.lockRootPositionXZ = true;
                c.lockRootHeightY = false;
                c.lockRootRotation = false;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
                c.keepOriginalOrientation = true;
                clips[i] = c;
            }
            mi.clipAnimations = clips;
        }

        // ------------------------------------------------------------------ materiais
        void OnPostprocessMaterial(Material m)
        {
            if (!InKayKit(assetPath) || m == null) return;
            // opaco, sem brilho plástico
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_BLENDMODE_ADD");
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = -1;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            // com textura, a cor base fica branca (a cor vem da paleta da textura)
            Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : (m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null);
            if (tex != null)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            }
        }

        // ------------------------------------------------------------------ texturas
        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            var ti = assetImporter as TextureImporter;
            if (ti == null) return;

            if (p == IconsPath)
            {
                ti.textureType = TextureImporterType.Default;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 8192;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.alphaIsTransparency = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.isReadable = false;
                ti.sRGBTexture = true;
                return;
            }

            if (InKayKit(p))
            {
                // texturas-paleta pequenas: bilinear, mipmaps padrão
                ti.textureType = TextureImporterType.Default;
                ti.filterMode = FilterMode.Bilinear;
                ti.mipmapEnabled = true;
                ti.sRGBTexture = true;
            }
        }
    }
}
