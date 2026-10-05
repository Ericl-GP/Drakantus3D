using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Utilitários: cores, materiais (URP ou Built-in), texturas geradas e formas simples.</summary>
    public static class U
    {
        static readonly Dictionary<string, Material> matCache = new();
        static Texture2D softTex, ringTex, whiteTex;
        static Sprite whiteSprite, roundSprite, circleSprite;

        public static Color Hex(string hex, float a = 1f)
        {
            if (string.IsNullOrEmpty(hex)) return new Color(1, 1, 1, a);
            if (!hex.StartsWith("#")) hex = "#" + hex;
            if (ColorUtility.TryParseHtmlString(hex, out var c)) { c.a = a; return c; }
            return new Color(1, 1, 1, a);
        }

        public static Shader FindShader(params string[] names)
        {
            foreach (var n in names) { var s = Shader.Find(n); if (s != null) return s; }
            return Shader.Find("Sprites/Default");
        }

        static Material Template(string res)
        {
            var m = Resources.Load<Material>("Materials/" + res);
            return m;
        }

        /// <summary>Material opaco com iluminação (URP Lit ou Standard).</summary>
        public static Material Lit(Color c, float smooth = 0.2f, Color? emission = null)
        {
            string key = "lit" + ColorUtility.ToHtmlStringRGBA(c) + smooth + (emission.HasValue ? ColorUtility.ToHtmlStringRGB(emission.Value) : "");
            if (matCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var t = Template("Lit");
            var m = t != null ? new Material(t) : new Material(FindShader("Universal Render Pipeline/Lit", "Standard"));
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            matCache[key] = m;
            return m;
        }

        /// <summary>Material aditivo/transparente para efeitos (brilhos, partículas, anéis).</summary>
        public static Material Fx(bool additive = true, Texture tex = null)
        {
            string key = "fx" + additive + (tex != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(tex).ToString() : "");
            if (matCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var t = Template(additive ? "FxAdd" : "FxAlpha");
            Material m;
            if (t != null) m = new Material(t);
            else
            {
                m = new Material(FindShader("Universal Render Pipeline/Particles/Unlit", "Legacy Shaders/Particles/Additive", "Sprites/Default"));
                SetupTransparent(m, additive);
            }
            m.mainTexture = tex != null ? tex : SoftTexture();
            matCache[key] = m;
            return m;
        }

        /// <summary>Configura um material URP Particles/Unlit como transparente (aditivo ou alpha).</summary>
        public static void SetupTransparent(Material m, bool additive)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = 3000;
        }

        public static Texture2D SoftTexture()
        {
            if (softTex != null) return softTex;
            const int n = 64;
            softTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "soft" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1 - d);
                    a = a * a * (3 - 2 * a);
                    softTex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            softTex.Apply();
            return softTex;
        }

        /// <summary>Anel suave (marcas no chão, ondas de choque).</summary>
        public static Texture2D RingTexture()
        {
            if (ringTex != null) return ringTex;
            const int n = 128;
            ringTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ring" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01(1 - Mathf.Abs(d - 0.9f) / 0.08f);
                    float fill = d < 0.9f ? 0.22f * d : 0f;
                    ringTex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Max(edge, fill)));
                }
            ringTex.Apply();
            return ringTex;
        }

        public static Texture2D WhiteTexture()
        {
            if (whiteTex != null) return whiteTex;
            whiteTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
            whiteTex.SetPixels(px); whiteTex.Apply();
            return whiteTex;
        }

        public static Sprite WhiteSprite()
        {
            if (whiteSprite == null) whiteSprite = Sprite.Create(WhiteTexture(), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100);
            return whiteSprite;
        }

        /// <summary>Retângulo arredondado 9-slice para painéis e botões.</summary>
        public static Sprite RoundSprite()
        {
            if (roundSprite != null) return roundSprite;
            const int n = 48; const float r = 14f;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
                }
            t.Apply();
            roundSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
            return roundSprite;
        }

        public static Sprite CircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d)));
                }
            t.Apply();
            circleSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
            return circleSprite;
        }

        // ------------------------------------------------------------------ ícones (Raven Fantasy Icons 32x32)
        static Texture2D iconSheet;
        static readonly Dictionary<int, Sprite> iconCache = new();

        public static Sprite Icon(int[] cr) => cr != null && cr.Length >= 2 ? Icon(cr[0], cr[1]) : null;

        public static Sprite Icon(int col, int row)
        {
            int key = row * 1000 + col;
            if (iconCache.TryGetValue(key, out var s) && s != null) return s;
            if (iconSheet == null)
            {
                iconSheet = Resources.Load<Texture2D>("UI/icons_32");
                if (iconSheet == null) return null;
                iconSheet.filterMode = FilterMode.Point;
            }
            int h = iconSheet.height;
            var rect = new Rect(col * 32, h - (row + 1) * 32, 32, 32);
            if (rect.y < 0 || rect.x + 32 > iconSheet.width) return null;
            s = Sprite.Create(iconSheet, rect, new Vector2(0.5f, 0.5f), 32);
            iconCache[key] = s;
            return s;
        }

        // ------------------------------------------------------------------ formas simples (usadas quando falta o modelo 3D)
        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, bool collider = false)
        {
            var g = GameObject.CreatePrimitive(t);
            if (!collider) Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Lit(c);
            return g;
        }

        /// <summary>Ponto do mouse no chão (plano y = h).</summary>
        public static bool MouseOnGround(Camera cam, float h, out Vector3 point)
        {
            point = Vector3.zero;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(InputW.MousePos);
            var plane = new Plane(Vector3.up, new Vector3(0, h, 0));
            if (plane.Raycast(ray, out float d)) { point = ray.GetPoint(d); return true; }
            return false;
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        public static Transform FindDeep(Transform root, string contains)
        {
            if (root == null) return null;
            string c = contains.ToLowerInvariant();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().Contains(c)) return t;
            return null;
        }

        public static void SetLayerColor(GameObject g, Color c)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
