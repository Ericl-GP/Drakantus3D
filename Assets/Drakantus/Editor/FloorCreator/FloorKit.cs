using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Utilidades do Criador de Andares que funcionam FORA do Play (no editor):
    /// materiais salvos como assets (para o prefab do andar não perder as cores), modelos KayKit como
    /// instâncias de prefab normalizadas pelo tamanho, cubos/primitivas e colisores.
    /// (As funções de runtime U.Prim / LevelDecor.Slab usam Destroy e materiais temporários — não servem aqui.)
    /// </summary>
    public static class FloorKit
    {
        public const string FloorsDir = "Assets/Floors";
        public const string MatDir = "Assets/Floors/Materiais";
        public const string TintDir = "Assets/Floors/Materiais/Tinta";
        public const string PrefabDir = "Assets/Drakantus/Resources/Floors";
        public const string FloorsJson = "Assets/Drakantus/Resources/Data/floors.json";
        public const string GameScene = "Assets/Scenes/Drakantus.unity";
        public const float TILE = 4f;

        /// <summary>Objetos criados na geração atual (limite ~2500).</summary>
        public static int count;
        public static int limit = 2500;
        public static bool Full => count >= limit;

        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        public static void ResetCaches() { matCache.Clear(); }

        // ------------------------------------------------------------------ pastas
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        public static Transform Group(Transform root, string name) => LevelDecor.Group(root, name);

        // ------------------------------------------------------------------ cores
        public static Color Hex(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        public static Color Hex(string hex) => Hex(hex, Color.white);

        static bool IsWhite(Color c) => Mathf.Abs(c.r - 1f) < 0.01f && Mathf.Abs(c.g - 1f) < 0.01f && Mathf.Abs(c.b - 1f) < 0.01f;

        // ------------------------------------------------------------------ materiais (assets)
        static Material LitTemplate()
        {
            return AssetDatabase.LoadAssetAtPath<Material>("Assets/Drakantus/Resources/Materials/Lit.mat");
        }

        static Shader LitShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        /// <summary>Material URP/Lit de cor sólida salvo em Assets/Floors/Materiais.</summary>
        public static Material Mat(Color c, float smooth = 0.15f, Color? emission = null, bool transparent = false)
        {
            string key = "m_" + ColorUtility.ToHtmlStringRGBA(c) + "_" + Mathf.RoundToInt(smooth * 100f)
                         + (emission.HasValue ? "_e" + ColorUtility.ToHtmlStringRGB(emission.Value) : "")
                         + (transparent ? "_t" : "");
            if (matCache.TryGetValue(key, out var cached) && cached != null) return cached;
            string path = MatDir + "/" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                EnsureFolder(MatDir);
                var t = LitTemplate();
                m = t != null ? new Material(t) : new Material(LitShader());
                SetColor(m, c);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
                if (emission.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
                }
                if (transparent) MakeTransparent(m);
                AssetDatabase.CreateAsset(m, path);
            }
            matCache[key] = m;
            return m;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static Color GetColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.white;
        }

        /// <summary>URP/Lit transparente (gelo, cristais).</summary>
        static void MakeTransparent(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>Cópia tingida de um material (salva em Assets/Floors/Materiais/Tinta). Branco = original.</summary>
        public static Material Tinted(Material src, Color tint)
        {
            if (src == null || IsWhite(tint)) return src;
            string safe = src.name.Replace(" ", "_").Replace("/", "_").Replace("(", "").Replace(")", "").Replace(":", "_");
            string key = "t_" + safe + "_" + ColorUtility.ToHtmlStringRGB(tint);
            if (matCache.TryGetValue(key, out var cached) && cached != null) return cached;
            string path = TintDir + "/" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                EnsureFolder(TintDir);
                m = new Material(src);
                Color c = GetColor(src);
                SetColor(m, new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a));
                AssetDatabase.CreateAsset(m, path);
            }
            matCache[key] = m;
            return m;
        }

        /// <summary>Tinge todos os renderers (troca por materiais tingidos salvos).</summary>
        public static void Tint(GameObject g, Color tint)
        {
            if (g == null || IsWhite(tint)) return;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Tinted(mats[i], tint);
                r.sharedMaterials = mats;
            }
        }

        // ------------------------------------------------------------------ modelos KayKit (ModelLibrary)
        public static bool HasModel(string id) => !string.IsNullOrEmpty(id) && !id.StartsWith("#") && ModelLibrary.I.Get(id) != null;

        /// <summary>Instância de prefab do modelo (null se não existir na biblioteca).</summary>
        public static GameObject Model(string id, Transform parent, Vector3 localPos, float yaw)
        {
            if (!HasModel(id)) return null;
            var e = ModelLibrary.I.Get(id);
            var g = PrefabUtility.InstantiatePrefab(e.prefab, parent) as GameObject;
            if (g == null) g = Object.Instantiate(e.prefab, parent);
            g.name = id;
            g.transform.localPosition = localPos;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(e.rotation);
            g.transform.localScale = Vector3.one * (e.scale <= 0 ? 1f : e.scale);
            count++;
            return g;
        }

        /// <summary>
        /// Modelo com altura normalizada (como LevelDecor.SpawnSized) e base em pos.y.
        /// Sem modelo: caixa simples cinza (TODO(modelos)).
        /// </summary>
        public static GameObject Sized(string id, Transform parent, Vector3 pos, float yaw, float height, Color tint, float tiltX = 0f, bool centerXZ = false)
        {
            var g = Model(id, parent, pos, yaw);
            if (g == null)
            {
                g = Cube(parent, id + "_simples", pos + Vector3.up * height * 0.5f, new Vector3(height * 0.6f, height, height * 0.6f), Quaternion.Euler(0, yaw, 0), Mat(new Color(0.6f, 0.6f, 0.62f) * tint), false);
                return g;
            }
            if (tiltX != 0f)
            {
                Quaternion y = Quaternion.Euler(0, yaw, 0);
                g.transform.localRotation = y * Quaternion.Euler(tiltX, 0, 0) * (Quaternion.Inverse(y) * g.transform.localRotation);
            }
            Bounds b;
            if (LevelDecor.WorldBounds(g, out b) && b.size.y > 0.0001f)
            {
                float s = height / b.size.y;
                Vector3 pivot = g.transform.position;
                g.transform.localScale = g.transform.localScale * s;
                float newMinY = pivot.y + (b.min.y - pivot.y) * s;
                Vector3 want = parent != null ? parent.TransformPoint(pos) : pos;
                Vector3 delta = new Vector3(0, want.y - newMinY, 0);
                if (centerXZ)
                {
                    delta.x = want.x - (pivot.x + (b.center.x - pivot.x) * s);
                    delta.z = want.z - (pivot.z + (b.center.z - pivot.z) * s);
                }
                g.transform.position += delta;
            }
            Tint(g, tint);
            return g;
        }

        /// <summary>Objeto deitado (ossos/armas no chão).</summary>
        public static GameObject Lying(string id, Transform parent, Vector3 pos, float yaw, float length, Color tint)
        {
            var g = Sized(id, parent, pos, yaw, length, tint);
            if (!HasModel(id)) return g;
            g.transform.rotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90f, 0, 0) * Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * g.transform.rotation;
            Bounds b;
            if (LevelDecor.WorldBounds(g, out b))
            {
                Vector3 want = parent != null ? parent.TransformPoint(pos) : pos;
                g.transform.position += new Vector3(0, want.y + 0.01f - b.min.y, 0);
            }
            return g;
        }

        /// <summary>
        /// Bloco (BlockBits) esticado para ocupar a caixa center/size (como LevelDecor.SpawnBox).
        /// Sem modelo: cubo com a cor fallback.
        /// </summary>
        public static GameObject Box(string id, Transform parent, Vector3 center, Vector3 size, float yaw, Color fallback, Color tint)
        {
            var g = Model(id, parent, center, yaw);
            if (g == null)
                return Cube(parent, id + "_simples", center, size, Quaternion.Euler(0, yaw, 0), Mat(fallback * tint), false);
            Bounds lb;
            if (LevelDecor.LocalBounds(g, out lb))
            {
                g.transform.localScale = new Vector3(
                    lb.size.x > 0.0001f ? size.x / lb.size.x : 1f,
                    lb.size.y > 0.0001f ? size.y / lb.size.y : 1f,
                    lb.size.z > 0.0001f ? size.z / lb.size.z : 1f);
                Vector3 cWorld = g.transform.TransformPoint(lb.center);
                Vector3 want = parent != null ? parent.TransformPoint(center) : center;
                g.transform.position += want - cWorld;
            }
            Tint(g, tint);
            return g;
        }

        /// <summary>Bloco inclinado (rampa): topo vai de hIn (atrás) a hOut (frente) ao longo de dir.</summary>
        public static GameObject RampBox(string id, Transform parent, Vector3 cellCenter, Vector2Int dir, float hIn, float hOut, float width, float length, Color fallback, Color tint)
        {
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            float dh = hOut - hIn;
            float len = Mathf.Sqrt(length * length + dh * dh);
            float pitch = -Mathf.Atan2(dh, length) * Mathf.Rad2Deg;
            var holder = new GameObject("Rampa");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(cellCenter.x, (hIn + hOut) * 0.5f, cellCenter.z);
            holder.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            count++;
            const float thick = 0.5f;
            var g = Box(id, holder.transform, new Vector3(0, -thick * 0.5f, 0), new Vector3(width, thick, len), 0f, fallback, tint);
            var bc = holder.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, -thick * 0.5f, 0);
            bc.size = new Vector3(width, thick, len);
            return holder;
        }

        // ------------------------------------------------------------------ primitivas e colisores
        public static GameObject Cube(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rot, Material m, bool collider)
        {
            return Prim(PrimitiveType.Cube, parent, name, center, size, rot, m, collider);
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 center, Vector3 size, Quaternion rot, Material m, bool collider)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            if (!collider)
            {
                var c = g.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localRotation = rot;
            g.transform.localScale = size;
            var r = g.GetComponent<Renderer>();
            if (r != null && m != null) r.sharedMaterial = m;
            count++;
            return g;
        }

        public static GameObject ColliderBox(Transform parent, Vector3 center, Vector3 size, float yaw = 0f, string name = "Colisor")
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var bc = g.AddComponent<BoxCollider>();
            bc.size = size;
            count++;
            return g;
        }

        public static BoxCollider AddCollider(GameObject g, float shrink = 0.85f, float minH = 1.2f) => LevelDecor.AddCollider(g, shrink, minH);

        public static GameObject Occluder(GameObject g) => LevelDecor.MarkOccluder(g);

        public static GameObject Empty(Transform parent, string name, Vector3 pos, float yaw = 0f)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            count++;
            return g;
        }

        // ------------------------------------------------------------------ luz
        /// <summary>Luz pontual (sem sombra) com tremor opcional; shadows = spot para baixo com sombra.</summary>
        public static Light PointLight(Transform parent, Vector3 pos, Color c, float range, float intensity, bool flicker, bool shadows = false)
        {
            var g = new GameObject(shadows ? "Luz_Sombra" : "Luz");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            if (shadows)
            {
                l.type = LightType.Spot;
                l.spotAngle = 160f;
                l.innerSpotAngle = 110f;
                l.range = range * 1.1f;
                l.intensity = intensity * 1.15f;
                g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.8f;
                l.shadowBias = 0.05f;
                l.shadowNormalBias = 0.4f;
                l.shadowNearPlane = 0.2f;
            }
            if (flicker)
            {
                var f = g.AddComponent<TorchFlicker>();
                f.baseIntensity = l.intensity;
                f.baseRange = l.range;
                f.amount = 0.22f;
                f.seed = pos.x * 0.37f + pos.z * 0.71f;
            }
            count++;
            return l;
        }

        public static FloorFx Fx(Transform parent, Vector3 pos, FloorFxKind kind, Vector3 size, Color c, int amount, float scale = 1f)
        {
            var g = new GameObject("Fx_" + kind);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            var f = g.AddComponent<FloorFx>();
            f.kind = kind;
            f.size = size;
            f.color = c;
            f.amount = Mathf.Clamp(amount, 1, 400);
            f.scale = scale;
            count++;
            return f;
        }

        public static FloorFxKind FxKindFromName(string s, out bool ok)
        {
            ok = true;
            switch ((s ?? "").ToLowerInvariant())
            {
                case "neve": return FloorFxKind.Neve;
                case "areia": return FloorFxKind.Areia;
                case "brasas": return FloorFxKind.Brasas;
                case "vagalumes": return FloorFxKind.Vagalumes;
                case "poeira": return FloorFxKind.Poeira;
                case "cinzas": return FloorFxKind.Cinzas;
                case "fumaca": return FloorFxKind.Fumaca;
                case "nevoa": return FloorFxKind.Nevoa;
                case "folhas": return FloorFxKind.Folhas;
            }
            ok = false;
            return FloorFxKind.Poeira;
        }
    }
}
