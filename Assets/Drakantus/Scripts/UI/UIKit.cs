using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Drakantus
{
    /// <summary>
    /// Kit de construção de interface em código (UGUI): painéis arredondados, textos com sombra,
    /// botões, barras, áreas com rolagem, sliders e campos de texto. Tudo usado pelo HUD.
    /// </summary>
    public static class UIKit
    {
        // ------------------------------------------------------------------ paleta (pergaminho escuro / couro / bronze)
        public static readonly Color PanelCol = new Color(0.085f, 0.064f, 0.047f, 0.9f);    // couro escuro
        public static readonly Color PanelSolid = new Color(0.075f, 0.057f, 0.042f, 0.975f);
        public static readonly Color CardCol = new Color(0.13f, 0.098f, 0.07f, 0.97f);
        public static readonly Color RowCol = new Color(0.115f, 0.087f, 0.063f, 0.95f);
        public static readonly Color SlotBg = new Color(0.06f, 0.047f, 0.036f, 1f);
        public static readonly Color Gold = new Color(1f, 0.843f, 0.416f, 1f);              // #FFD76A (texto/acentos)
        public static readonly Color GoldDark = new Color(0.17f, 0.11f, 0.04f, 1f);
        public static readonly Color Bronze = new Color(0.78f, 0.58f, 0.34f, 1f);           // molduras
        public static readonly Color BronzeDark = new Color(0.42f, 0.29f, 0.16f, 1f);
        public static readonly Color PrimaryCol = new Color(0.86f, 0.66f, 0.34f, 1f);       // botão principal (latão)
        public static readonly Color HeaderCol = new Color(0.36f, 0.12f, 0.09f, 1f);        // faixa de título (couro vermelho)
        public static readonly Color TextCol = new Color(0.94f, 0.89f, 0.79f, 1f);          // pergaminho
        public static readonly Color DimText = new Color(0.67f, 0.6f, 0.5f, 1f);
        public static readonly Color BorderCol = new Color(0.78f, 0.58f, 0.34f, 0.35f);
        public static readonly Color BtnCol = new Color(0.25f, 0.18f, 0.12f, 1f);
        public static readonly Color DangerCol = new Color(0.52f, 0.16f, 0.12f, 1f);
        public static readonly Color HpCol = new Color(0.8f, 0.2f, 0.16f, 1f);
        public static readonly Color MpCol = new Color(0.22f, 0.44f, 0.82f, 1f);
        public static readonly Color StCol = new Color(0.86f, 0.7f, 0.3f, 1f);
        public static readonly Color XpCol = new Color(0.6f, 0.44f, 0.9f, 1f);
        public static readonly Color RareCol = new Color(0.29f, 0.64f, 1f, 1f);             // #4aa3ff
        public static readonly Color LegendCol = new Color(1f, 0.6f, 0.18f, 1f);            // #ff9a2e
        public static readonly Color CommonCol = new Color(0.5f, 0.46f, 0.4f, 1f);

        static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Color RarityBorder(ItemDef d)
        {
            if (d == null) return new Color(0.34f, 0.28f, 0.21f, 1f);
            if (d.rarity == "lendario") return LegendCol;
            if (d.rarity == "raro") return RareCol;
            return CommonCol;
        }

        public static string ColorHex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ------------------------------------------------------------------ RectTransform
        public static RectTransform R(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform RT(Component c) => c != null ? (RectTransform)c.transform : null;

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Ocupa o pai inteiro com margens (esquerda, direita, topo, base).</summary>
        public static RectTransform Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float all) => Stretch(rt, all, all, all, all);

        // ------------------------------------------------------------------ imagens
        public static Image Img(Transform p, string name, Sprite s, Color c, bool raycast = false)
        {
            var rt = R(p, name);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = s; im.color = c; im.raycastTarget = raycast;
            if (s != null && s.border.sqrMagnitude > 0f) im.type = Image.Type.Sliced;
            return im;
        }

        /// <summary>Retângulo com cantos chanfrados (corte em 45°). corner &gt; 1 deixa os cantos menores.</summary>
        public static Image Round(Transform p, string name, Color c, float corner = 1f, bool raycast = false)
        {
            var im = Img(p, name, ChamferSprite(), c, raycast);
            im.type = Image.Type.Sliced;
            im.pixelsPerUnitMultiplier = corner;
            return im;
        }

        /// <summary>Painel de couro escuro (gradiente sutil) com moldura fina de bronze e rebites em losango.</summary>
        public static Image Panel(Transform p, string name, Color? c = null, float corner = 1f)
        {
            var im = Img(p, name, PlateSprite(), c ?? PanelCol, true);
            im.type = Image.Type.Sliced;
            im.pixelsPerUnitMultiplier = Mathf.Max(0.5f, corner);
            Frame(im.rectTransform);
            return im;
        }

        /// <summary>
        /// Moldura ornamentada (bronze, chanfrada, rebites em losango nos cantos) por cima de um retângulo.
        /// Ignora LayoutGroups do pai. thin = moldura fina para barras, botões e campos.
        /// </summary>
        public static Image Frame(RectTransform target, Color? c = null, bool thin = false, float inset = 0f)
        {
            var f = Img(target, "moldura", thin ? FrameThinSprite() : FrameSprite(), c ?? Bronze, false);
            f.type = Image.Type.Sliced;
            Stretch(f.rectTransform, inset);
            var le = f.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            return f;
        }

        /// <summary>Moldura metálica de ícone/slot (chanfro com relevo). A cor tinge o metal (classe/raridade).</summary>
        public static Image MetalFrame(Transform p, string name, Color c, bool raycast = false)
        {
            var im = Img(p, name, MetalSprite(), c, raycast);
            im.type = Image.Type.Sliced;
            return im;
        }

        /// <summary>Losango decorativo (ornamento de cabeçalhos e barras).</summary>
        public static Image Diamond(Transform p, string name, Color c, float size)
        {
            var im = Img(p, name, DiamondSprite(), c, false);
            im.rectTransform.sizeDelta = new Vector2(size, size);
            return im;
        }

        public static Image Line(Transform p, string name, Color c)
        {
            return Img(p, name, U.WhiteSprite(), c, false);
        }

        // ------------------------------------------------------------------ texto
        public static Text Txt(Transform p, string name, string s, int size, Color c,
                               TextAnchor a = TextAnchor.MiddleLeft, FontStyle st = FontStyle.Normal, bool outline = false)
        {
            var rt = R(p, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = s ?? ""; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = st;
            t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.75f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.85f); o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        /// <summary>Muda o texto só se for diferente (evita reconstruir a malha todo frame).</summary>
        public static void Set(Text t, string s)
        {
            if (t != null && t.text != s) t.text = s;
        }

        // ------------------------------------------------------------------ botões
        public static Button Btn(Transform p, string label, Vector2 size, Action onClick, Color? color = null, int fontSize = 22, Color? textColor = null)
        {
            Color col = color ?? BtnCol;
            if (col == Gold) col = PrimaryCol;   // botão principal: placa de latão
            var bg = Img(p, "Botao_" + label, PlateSprite(), Color.white, true);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 1.6f;
            bg.rectTransform.sizeDelta = size;
            Frame(bg.rectTransform, new Color(Bronze.r, Bronze.g, Bronze.b, 0.9f), true);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var cb = b.colors;
            cb.normalColor = col;
            cb.highlightedColor = Color.Lerp(col, Color.white, 0.2f);
            cb.selectedColor = col;
            cb.pressedColor = Color.Lerp(col, Color.black, 0.25f);
            cb.disabledColor = new Color(0.2f, 0.17f, 0.14f, 0.9f);
            cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            Color tc = textColor ?? (col.grayscale > 0.6f ? GoldDark : TextCol);
            var t = Txt(bg.rectTransform, "texto", label, fontSize, tc, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 8, 8, 2, 2);
            b.onClick.AddListener(() =>
            {
                Sfx.Play("ui_click");
                onClick?.Invoke();
            });
            bg.gameObject.AddComponent<UIHover>().scale = 1.03f;
            return b;
        }

        /// <summary>Botão desabilitado com texto apagado.</summary>
        public static void Disable(Button b)
        {
            if (b == null) return;
            b.interactable = false;
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.color = new Color(0.55f, 0.5f, 0.44f, 1f);
            var h = b.GetComponent<UIHover>();
            if (h != null) h.enabled = false;
        }

        // ------------------------------------------------------------------ layout
        public static ContentSizeFitter Fit(GameObject g, bool w, bool h)
        {
            var f = g.AddComponent<ContentSizeFitter>();
            f.horizontalFit = w ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = h ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        public static VerticalLayoutGroup VLayout(GameObject g, float spacing, RectOffset pad, TextAnchor align = TextAnchor.UpperLeft)
        {
            var l = g.AddComponent<VerticalLayoutGroup>();
            l.spacing = spacing; l.padding = pad ?? new RectOffset(0, 0, 0, 0); l.childAlignment = align;
            l.childControlWidth = true; l.childControlHeight = true;
            l.childForceExpandWidth = true; l.childForceExpandHeight = false;
            return l;
        }

        public static HorizontalLayoutGroup HLayout(GameObject g, float spacing, RectOffset pad, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var l = g.AddComponent<HorizontalLayoutGroup>();
            l.spacing = spacing; l.padding = pad ?? new RectOffset(0, 0, 0, 0); l.childAlignment = align;
            l.childControlWidth = true; l.childControlHeight = true;
            l.childForceExpandWidth = false; l.childForceExpandHeight = false;
            return l;
        }

        public static LayoutElement LE(Component c, float w = -1, float h = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            return le;
        }

        /// <summary>Área com rolagem vertical. Devolve o "content" (ancorado no topo). O root é o pai do viewport.</summary>
        public static RectTransform ScrollArea(Transform parent, string name, out ScrollRect sr)
        {
            var bg = Round(parent, name, new Color(0f, 0f, 0f, 0.22f), 1.5f, true);
            var root = bg.rectTransform;
            var vp = R(root, "viewport");
            Stretch(vp, 6, 6, 6, 6);
            vp.gameObject.AddComponent<RectMask2D>();
            var content = R(vp, "content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            sr = root.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = content;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f; sr.inertia = true; sr.decelerationRate = 0.12f;
            return content;
        }

        // ------------------------------------------------------------------ slider / campo de texto
        public static Slider MakeSlider(Transform p, string name, float value, Action<float> onChange, Color fillCol)
        {
            var root = R(p, name);
            var bg = Round(root, "fundo", new Color(0.035f, 0.026f, 0.02f, 1f), 3f, true);
            bg.rectTransform.anchorMin = new Vector2(0f, 0.32f); bg.rectTransform.anchorMax = new Vector2(1f, 0.68f);
            bg.rectTransform.offsetMin = Vector2.zero; bg.rectTransform.offsetMax = Vector2.zero;
            var area = R(root, "area");
            area.anchorMin = new Vector2(0f, 0.32f); area.anchorMax = new Vector2(1f, 0.68f);
            area.offsetMin = new Vector2(2f, 2f); area.offsetMax = new Vector2(-2f, -2f);
            var fill = Img(area, "preenche", BarFillSprite(), fillCol, false);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(value, 1f);
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
            var fr = Img(root, "moldura", FrameThinSprite(), Bronze, false);   // por cima do preenchimento
            fr.type = Image.Type.Sliced;
            fr.rectTransform.anchorMin = bg.rectTransform.anchorMin; fr.rectTransform.anchorMax = bg.rectTransform.anchorMax;
            fr.rectTransform.offsetMin = new Vector2(-1f, -1f); fr.rectTransform.offsetMax = new Vector2(1f, 1f);
            var handleArea = R(root, "alca_area");
            Stretch(handleArea, 10, 10, 0, 0);
            var handle = Img(handleArea, "alca", DiamondSprite(), Gold, true);
            handle.rectTransform.anchorMin = new Vector2(value, 0f); handle.rectTransform.anchorMax = new Vector2(value, 1f);
            handle.rectTransform.sizeDelta = new Vector2(22f, 0f);
            handle.preserveAspect = true;
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight; s.minValue = 0f; s.maxValue = 1f;
            s.navigation = new Navigation { mode = Navigation.Mode.None };
            s.value = Mathf.Clamp01(value);
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return s;
        }

        public static InputField MakeInput(Transform p, string name, string placeholder, int limit)
        {
            var bg = Round(p, name, new Color(0.045f, 0.034f, 0.026f, 1f), 1.6f, true);
            Frame(bg.rectTransform, null, true);
            var txt = Txt(bg.rectTransform, "texto", "", 26, TextCol, TextAnchor.MiddleLeft);
            Stretch(txt.rectTransform, 18, 18, 4, 4);
            txt.supportRichText = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ph = Txt(bg.rectTransform, "dica", placeholder, 26, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleLeft, FontStyle.Italic);
            Stretch(ph.rectTransform, 18, 18, 4, 4);
            var input = bg.gameObject.AddComponent<InputField>();
            input.textComponent = txt; input.placeholder = ph; input.targetGraphic = bg;
            input.characterLimit = limit; input.lineType = InputField.LineType.SingleLine;
            input.caretColor = Gold; input.customCaretColor = true;
            input.selectionColor = new Color(1f, 0.84f, 0.42f, 0.35f);
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            return input;
        }

        // ------------------------------------------------------------------ sprites gerados
        static Sprite arrowSprite, softSprite, bandSprite;

        /// <summary>Seta (chevron) apontando para cima, para o minimapa.</summary>
        public static Sprite ArrowSprite()
        {
            if (arrowSprite != null) return arrowSprite;
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Vector2 a = new Vector2(16f, 30f), b = new Vector2(4f, 3f), c = new Vector2(16f, 10f), d = new Vector2(28f, 3f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            var p = new Vector2(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f);
                            if (InTri(p, a, b, c) || InTri(p, a, c, d)) hits++;
                        }
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, hits / 9f));
                }
            t.Apply();
            arrowSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
            return arrowSprite;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        /// <summary>Brilho radial suave.</summary>
        public static Sprite SoftSprite()
        {
            if (softSprite != null) return softSprite;
            var tex = U.SoftTexture();
            softSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
            return softSprite;
        }

        /// <summary>Faixa horizontal que some nas pontas (para títulos grandes).</summary>
        public static Sprite BandSprite()
        {
            if (bandSprite != null) return bandSprite;
            const int w = 128, h = 8;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < w; x++)
            {
                float k = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float a = 1f - Mathf.SmoothStep(0.45f, 1f, k);
                for (int y = 0; y < h; y++) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            bandSprite = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100);
            return bandSprite;
        }

        // ------------------------------------------------------------------ sprites ornamentados (gerados uma vez)
        static Sprite chamferSpr, plateSpr, frameSpr, frameThinSpr, metalSpr, barFillSpr, diamondSpr, headerSpr;

        static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        }

        static Sprite SlicedOf(Texture2D t, float border)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>Distância (px) de um ponto até a borda de um retângulo w×h com cantos chanfrados (negativo = fora).</summary>
        static float ChamferDist(float x, float y, float w, float h, float cut)
        {
            const float k = 0.70710678f;
            float d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - x, h - y));
            d = Mathf.Min(d, (x + y - cut) * k);
            d = Mathf.Min(d, ((w - x) + y - cut) * k);
            d = Mathf.Min(d, (x + (h - y) - cut) * k);
            d = Mathf.Min(d, ((w - x) + (h - y) - cut) * k);
            return d;
        }

        static float Hash(int x, int y)
        {
            int n = x * 73856093 ^ y * 19349663;
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        /// <summary>Forma branca chanfrada (substitui o retângulo arredondado genérico).</summary>
        public static Sprite ChamferSprite()
        {
            if (chamferSpr != null) return chamferSpr;
            const int n = 32;
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = Mathf.Clamp01(ChamferDist(x + 0.5f, y + 0.5f, n, n, 6f) + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            chamferSpr = SlicedOf(t, 10f);
            return chamferSpr;
        }

        /// <summary>Placa de painel: chanfrada, gradiente vertical sutil (luz de cima), grão leve e borda escurecida.</summary>
        public static Sprite PlateSprite()
        {
            if (plateSpr != null) return plateSpr;
            const int n = 64;
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = ChamferDist(x + 0.5f, y + 0.5f, n, n, 8f);
                    float a = Mathf.Clamp01(d + 0.5f);
                    float v = Mathf.Lerp(0.8f, 1f, (y + 0.5f) / n);
                    v *= 0.96f + 0.04f * Hash(x, y);
                    if (d < 4f) v *= Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(d / 4f));
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                    px[y * n + x] = new Color32(b, b, b, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            plateSpr = SlicedOf(t, 16f);
            return plateSpr;
        }

        /// <summary>Moldura ornamentada: contorno escuro, faixa de bronze em relevo, filete interno e losangos nos cantos (centro vazio).</summary>
        public static Sprite FrameSprite()
        {
            if (frameSpr != null) return frameSpr;
            const int n = 64;
            const float cut = 8f, rv = 9.5f;
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = ChamferDist(fx, fy, n, n, cut);
                    float v = 0f, a = 0f;
                    if (d >= -0.5f)
                    {
                        float ao = Mathf.Clamp01(d + 0.5f);
                        if (d < 1.2f) { v = 0f; a = 0.9f * ao; }                                   // contorno
                        else if (d < 4.2f)
                        {                                                                             // faixa de bronze
                            float tb = (d - 1.2f) / 3f;
                            v = (0.62f + 0.38f * (1f - Mathf.Abs(tb - 0.35f) * 1.8f)) * Mathf.Lerp(0.78f, 1f, fy / n);
                            a = 1f;
                        }
                        else if (d < 5.2f) { v = 0f; a = 0.55f; }                                    // filete escuro
                    }
                    // losangos (rebites) nos 4 cantos
                    float cx = fx < n * 0.5f ? rv : n - rv, cy = fy < n * 0.5f ? rv : n - rv;
                    float m = Mathf.Abs(fx - cx) + Mathf.Abs(fy - cy);
                    if (m < 4.6f)
                    {
                        float ra = Mathf.Clamp01(4.6f - m);
                        float rvv = m < 3.4f ? 0.75f + 0.25f * Mathf.Clamp01((fy - cy + 2f) / 4f) + (m < 1.2f ? 0.15f : 0f) : 0.05f;
                        v = Mathf.Lerp(v, Mathf.Clamp01(rvv), ra);
                        a = Mathf.Max(a, ra);
                    }
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                    px[y * n + x] = new Color32(b, b, b, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px); t.Apply();
            frameSpr = SlicedOf(t, 16f);
            return frameSpr;
        }

        /// <summary>Moldura fina (barras, botões, campos): contorno escuro + filete de bronze em relevo.</summary>
        public static Sprite FrameThinSprite()
        {
            if (frameThinSpr != null) return frameThinSpr;
            const int n = 32;
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fy = y + 0.5f;
                    float d = ChamferDist(x + 0.5f, fy, n, n, 4f);
                    float v = 0f, a = 0f;
                    if (d >= -0.5f)
                    {
                        float ao = Mathf.Clamp01(d + 0.5f);
                        if (d < 1f) { v = 0f; a = 0.85f * ao; }
                        else if (d < 2.6f) { v = Mathf.Lerp(0.66f, 1f, fy / n); a = 1f; }
                        else if (d < 3.4f) { v = 0f; a = 0.4f; }
                    }
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                    px[y * n + x] = new Color32(b, b, b, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px); t.Apply();
            frameThinSpr = SlicedOf(t, 8f);
            return frameThinSpr;
        }

        /// <summary>Moldura metálica de slot/ícone: placa chanfrada com relevo (luz em cima/esquerda, sombra embaixo/direita).</summary>
        public static Sprite MetalSprite()
        {
            if (metalSpr != null) return metalSpr;
            const int n = 48;
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = ChamferDist(fx, fy, n, n, 6f);
                    float a = Mathf.Clamp01(d + 0.5f);
                    float v;
                    if (d < 1f) v = 0.18f;                                   // contorno
                    else if (d < 3.6f)
                    {
                        // relevo: lados de cima/esquerda claros, baixo/direita escuros
                        float dTop = n - fy, dBot = fy, dLeft = fx, dRight = n - fx;
                        float near = Mathf.Min(Mathf.Min(dTop, dBot), Mathf.Min(dLeft, dRight));
                        bool lit = near == dTop || near == dLeft;
                        v = lit ? 1f : 0.55f;
                        v *= 0.9f + 0.1f * (1f - Mathf.Abs((d - 2.3f) / 1.3f));
                    }
                    else if (d < 4.6f) v = 0.3f;                             // sulco
                    else v = Mathf.Lerp(0.62f, 0.8f, fy / n);
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                    px[y * n + x] = new Color32(b, b, b, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            metalSpr = SlicedOf(t, 13f);
            return metalSpr;
        }

        /// <summary>Preenchimento de barra: gradiente vertical com brilho no topo e sombra na base (textura).</summary>
        public static Sprite BarFillSprite()
        {
            if (barFillSpr != null) return barFillSpr;
            const int w = 4, h = 32;
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float k = (y + 0.5f) / h;
                float v = 0.62f + 0.38f * Mathf.Pow(k, 0.7f);
                if (k > 0.78f && k < 0.9f) v = Mathf.Min(1f, v + 0.18f);    // reflexo
                if (k < 0.12f) v *= 0.75f;                                  // sombra da base
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(b, b, b, 255);
            }
            t.SetPixels32(px); t.Apply();
            barFillSpr = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return barFillSpr;
        }

        /// <summary>Losango com relevo (alça de slider, ornamentos).</summary>
        public static Sprite DiamondSprite()
        {
            if (diamondSpr != null) return diamondSpr;
            const int n = 32;
            var t = NewTex(n, n);
            var px = new Color32[n * n];
            float h = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h;
                    float m = Mathf.Abs(dx) + Mathf.Abs(dy);
                    float a = Mathf.Clamp01((0.96f - m) * h);
                    float v = m > 0.78f ? 0.25f : 0.72f + 0.28f * Mathf.Clamp01(dy - dx * 0.3f + 0.3f);
                    if (m < 0.22f) v = 1f;
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                    px[y * n + x] = new Color32(b, b, b, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            diamondSpr = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return diamondSpr;
        }

        /// <summary>Faixa de cabeçalho (fita com pontas em rabo de andorinha), filetes claros em cima e escuros embaixo.</summary>
        public static Sprite HeaderSprite()
        {
            if (headerSpr != null) return headerSpr;
            const int w = 128, h = 40;
            const float notch = 12f, pad = 2f;
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            float cy = h * 0.5f, hh = h * 0.5f - pad;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float fx = x + (sx + 0.5f) / 3f, fy = y + (sy + 0.5f) / 3f;
                            if (fy < pad || fy > h - pad) continue;
                            float cut = notch * (1f - Mathf.Abs(fy - cy) / hh);
                            if (fx < cut || fx > w - cut) continue;
                            hits++;
                        }
                    float a = hits / 9f;
                    float k = (y + 0.5f) / h;
                    float v = Mathf.Lerp(0.72f, 1f, k);
                    if (y >= h - pad - 2 && y < h - pad) v = 1f;          // filete claro (topo)
                    else if (y >= pad && y < pad + 2) v = 0.45f;          // filete escuro (base)
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                    px[y * w + x] = new Color32(b, b, b, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            headerSpr = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                                      SpriteMeshType.FullRect, new Vector4(24f, 0f, 24f, 0f));
            return headerSpr;
        }
    }

    // ====================================================================== barra (vida/mana/vigor/XP/chefe)
    /// <summary>Barra com fundo, preenchimento, "rastro" branco que desce devagar e brilho ao ganhar.</summary>
    public class UIBar
    {
        public RectTransform root;
        public Image bg, trail, fill, glow;
        public Text label;
        public bool useTrail = true;
        float shown = -1f, trailV, trailDelay, glowT, lastFill = -1f, lastTrail = -1f;
        int lastCur = int.MinValue, lastMax = int.MinValue;

        public static UIBar Create(Transform parent, string name, Color col, int fontSize, float corner = 3f)
        {
            var b = new UIBar();
            b.bg = UIKit.Round(parent, name, new Color(0.04f, 0.028f, 0.02f, 0.94f), corner);
            b.root = b.bg.rectTransform;
            var area = UIKit.R(b.root, "area");
            UIKit.Stretch(area, 2, 2, 2, 2);
            b.trail = UIKit.Img(area, "rastro", U.WhiteSprite(), new Color(1f, 0.93f, 0.8f, 0.8f));
            b.fill = UIKit.Img(area, "barra", UIKit.BarFillSprite(), col);
            b.glow = UIKit.Img(area, "ganho", U.WhiteSprite(), new Color(1f, 1f, 1f, 0f));
            UIKit.Stretch(b.glow.rectTransform);
            UIKit.Frame(b.root, null, true, -1f);   // moldura fina de bronze por cima
            SetFrac(b.trail.rectTransform, 1f);
            SetFrac(b.fill.rectTransform, 1f);
            if (fontSize > 0)
            {
                b.label = UIKit.Txt(b.root, "texto", "", fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(b.label.rectTransform);
                b.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return b;
        }

        static void SetFrac(RectTransform r, float f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        /// <summary>Faz a próxima atualização pular direto para o valor (sem rastro).</summary>
        public void Reset() { shown = -1f; }

        public void Set(float cur, float max, float dt)
        {
            float v = max > 0.0001f ? Mathf.Clamp01(cur / max) : 0f;
            if (shown < 0f) { shown = v; trailV = v; }
            if (v < shown)
            {
                if (trailV < shown) trailV = shown;
                shown = v;
                trailDelay = 0.45f;
            }
            else if (v > shown)
            {
                float before = shown;
                shown = Mathf.MoveTowards(shown, v, dt * 1.6f);
                if (v - before > 0.01f) glowT = 1f;
            }
            if (!useTrail || trailV < shown) trailV = shown;
            else if (trailDelay > 0f) trailDelay -= dt;
            else trailV = Mathf.MoveTowards(trailV, shown, dt * 0.55f);
            glowT = Mathf.Max(0f, glowT - dt * 2.2f);

            if (Mathf.Abs(shown - lastFill) > 0.0005f) { lastFill = shown; SetFrac(fill.rectTransform, shown); }
            if (Mathf.Abs(trailV - lastTrail) > 0.0005f) { lastTrail = trailV; SetFrac(trail.rectTransform, trailV); }
            bool tOn = trailV > shown + 0.001f;
            if (trail.enabled != tOn) trail.enabled = tOn;
            float ga = glowT * 0.4f;
            if (Mathf.Abs(glow.color.a - ga) > 0.003f || (ga == 0f && glow.color.a != 0f)) glow.color = new Color(1f, 1f, 1f, ga);

            if (label != null)
            {
                int c = Mathf.CeilToInt(Mathf.Max(0f, cur) - 0.001f), m = Mathf.RoundToInt(max);
                if (c != lastCur || m != lastMax) { lastCur = c; lastMax = m; label.text = c + " / " + m; }
            }
        }
    }

    // ====================================================================== componentes pequenos
    /// <summary>Clique esquerdo/direito em qualquer Graphic com raycastTarget.</summary>
    public class UIClick : MonoBehaviour, IPointerClickHandler
    {
        public Action onLeft, onRight;
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) onLeft?.Invoke();
            else if (e.button == PointerEventData.InputButton.Right) onRight?.Invoke();
        }
    }

    /// <summary>Leve aumento de escala ao passar o mouse.</summary>
    public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float scale = 1.05f;
        bool over; float k;
        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = false; }
        void OnDisable() { over = false; k = 0f; transform.localScale = Vector3.one; }
        void Update()
        {
            float target = over ? 1f : 0f;
            if (Mathf.Approximately(k, target)) return;
            k = Mathf.MoveTowards(k, target, Time.unscaledDeltaTime * 8f);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, scale, k);
        }
    }

    /// <summary>Pulsa a cor de um Graphic (e/ou de um efeito Outline) — brilho de itens lendários.</summary>
    public class UIPulse : MonoBehaviour
    {
        public Graphic target;
        public Shadow effect;
        public Color a = Color.white, b = Color.white, fxA = Color.clear, fxB = Color.clear;
        public float speed = 3f;
        void Update()
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
            if (target != null) target.color = Color.Lerp(a, b, t);
            if (effect != null) effect.effectColor = Color.Lerp(fxA, fxB, t);
        }
    }

    /// <summary>Mostra a dica (tooltip) do HUD ao passar o mouse.</summary>
    public class UITip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<string> title, body;
        public void OnPointerEnter(PointerEventData e)
        {
            if (HUD.I == null) return;
            string t = title != null ? title() : "";
            if (string.IsNullOrEmpty(t)) return;
            HUD.I.ShowTip(this, t, body != null ? body() : "");
        }
        public void OnPointerExit(PointerEventData e) { if (HUD.I != null) HUD.I.HideTip(this); }
        void OnDisable() { if (HUD.I != null) HUD.I.HideTip(this); }

        public static UITip Add(GameObject g, Func<string> title, Func<string> body)
        {
            var t = g.GetComponent<UITip>();
            if (t == null) t = g.AddComponent<UITip>();
            t.title = title; t.body = body;
            return t;
        }
    }

    /// <summary>Card/slot de habilidade que pode ser arrastado.</summary>
    public class SkillDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string skillId;
        RectTransform ghost;

        public void OnBeginDrag(PointerEventData e)
        {
            if (HUD.I == null || string.IsNullOrEmpty(skillId)) return;
            ghost = HUD.I.BeginSkillDrag(skillId);
            if (ghost != null) HUD.I.MoveToPointer(ghost, e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            if (ghost != null && HUD.I != null) HUD.I.MoveToPointer(ghost, e.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
        }

        void OnDisable()
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
        }
    }

    /// <summary>Slot que recebe uma habilidade arrastada.</summary>
    public class SkillDropSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int index;
        public Graphic highlight;

        public void OnDrop(PointerEventData e)
        {
            if (highlight != null) highlight.enabled = false;
            var src = e.pointerDrag != null ? e.pointerDrag.GetComponent<SkillDragSource>() : null;
            if (src != null && !string.IsNullOrEmpty(src.skillId) && HUD.I != null) HUD.I.DropSkill(index, src.skillId);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (highlight == null) return;
            highlight.enabled = e.dragging && e.pointerDrag != null && e.pointerDrag.GetComponent<SkillDragSource>() != null;
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (highlight != null) highlight.enabled = false;
        }
    }
}
