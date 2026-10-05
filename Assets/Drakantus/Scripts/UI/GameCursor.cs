using UnityEngine;
using UnityEngine.UI;

namespace Drakantus
{
    /// <summary>
    /// Cursor temático desenhado no topo de tudo (o do sistema fica escondido).
    /// Fora de combate / em menus: seta medieval de bronze (gerada em código).
    /// Em combate (herói pode lutar, sem janela aberta e fora da HUD): mira circular com 4 traços que abre ao
    /// atacar, fecha e fica dourada ao carregar o ataque e fica vermelha sobre um inimigo.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public class GameCursor : MonoBehaviour
    {
        public static GameCursor I;

        RectTransform arrowRt, crossRt;
        Image arrowImg, ring, dot;
        readonly Image[] ticks = new Image[4];
        readonly RectTransform[] tickRt = new RectTransform[4];
        float spread, redK, goldK, showCross;

        static readonly Color CrossCol = new Color(1f, 0.93f, 0.8f, 0.92f);
        static readonly Color EnemyCol = new Color(1f, 0.28f, 0.2f, 1f);
        static readonly Color ChargeCol = new Color(1f, 0.8f, 0.35f, 1f);

        public static void Ensure()
        {
            if (I != null) return;
            var g = new GameObject("Cursor");
            g.layer = 5;
            I = g.AddComponent<GameCursor>();
        }

        void Awake()
        {
            if (I == null) I = this;
            var c = gameObject.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 30000;
            var sc = gameObject.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(HUD.ReferenceW, HUD.ReferenceH);
            sc.matchWidthOrHeight = 0.5f;

            // seta (ponta = pivô)
            arrowImg = UIKit.Img(transform, "seta", ArrowSprite(), Color.white);
            arrowRt = arrowImg.rectTransform;
            arrowRt.anchorMin = arrowRt.anchorMax = Vector2.zero;
            arrowRt.pivot = new Vector2(3f / 32f, 29f / 32f);
            arrowRt.sizeDelta = new Vector2(40f, 40f);

            // mira
            crossRt = UIKit.R(transform, "mira");
            crossRt.anchorMin = crossRt.anchorMax = Vector2.zero;
            crossRt.pivot = new Vector2(0.5f, 0.5f);
            crossRt.sizeDelta = new Vector2(60f, 60f);
            ring = UIKit.Img(crossRt, "anel", RingSprite(), CrossCol);
            UIKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            dot = UIKit.Img(crossRt, "ponto", UIKit.DiamondSprite(), CrossCol);
            UIKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            for (int i = 0; i < 4; i++)
            {
                var t = UIKit.Img(crossRt, "traco" + i, U.WhiteSprite(), CrossCol);
                var r = t.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(3f, 10f);
                r.localEulerAngles = new Vector3(0f, 0f, i * 90f);
                var sh = t.gameObject.AddComponent<Outline>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.6f); sh.effectDistance = new Vector2(1f, -1f);
                ticks[i] = t; tickRt[i] = r;
            }
            var rs = ring.gameObject.AddComponent<Shadow>();
            rs.effectColor = new Color(0f, 0f, 0f, 0.6f); rs.effectDistance = new Vector2(1f, -1f);
            crossRt.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            Cursor.visible = true;
            if (I == this) I = null;
        }

        void OnApplicationFocus(bool f) { if (f) Cursor.visible = false; }

        void LateUpdate()
        {
            if (Cursor.visible) Cursor.visible = false;
            Vector2 mp = InputW.MousePos;
            float udt = Time.unscaledDeltaTime;

            var g = Game.I;
            var p = g != null ? g.player : null;
            bool combat = p != null && p.canFight && p.state != "dead" && !p.inputLocked
                          && HUD.I != null && !HUD.I.HasModal && !HUD.PointerOverUI();
            showCross = Mathf.MoveTowards(showCross, combat ? 1f : 0f, udt * 10f);
            bool cross = showCross > 0.5f;
            if (crossRt.gameObject.activeSelf != cross) crossRt.gameObject.SetActive(cross);
            if (arrowRt.gameObject.activeSelf == cross) arrowRt.gameObject.SetActive(!cross);

            // na Overlay, position = pixels de tela
            if (!cross) { arrowRt.position = new Vector3(mp.x, mp.y, 0f); return; }
            crossRt.position = new Vector3(mp.x, mp.y, 0f);

            // inimigo sob o cursor?
            bool onEnemy = false;
            if (U.MouseOnGround(CameraRig.Cam, p.transform.position.y, out var pt) && g.enemies != null)
            {
                foreach (var e in g.enemies)
                {
                    if (e == null || e.dead) continue;
                    if (U.Flat(e.transform.position - pt).magnitude <= e.radius + 0.55f) { onEnemy = true; break; }
                }
            }
            bool attacking = p.state == "attack" || InputW.MouseDown(0);
            if (attacking && !p.charging) spread = Mathf.Max(spread, 1f);
            spread = Mathf.MoveTowards(spread, 0f, udt * 4f);
            float charge = p.charging ? Mathf.Clamp01(p.chargeLevel) : 0f;
            redK = Mathf.MoveTowards(redK, onEnemy ? 1f : 0f, udt * 12f);
            goldK = Mathf.MoveTowards(goldK, p.charging ? 1f : 0f, udt * 8f);

            Color col = Color.Lerp(CrossCol, EnemyCol, redK);
            col = Color.Lerp(col, ChargeCol, goldK * 0.8f);
            float r = 13f + 9f * spread - 6f * charge + (onEnemy ? -1.5f : 0f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                tickRt[i].anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (r + 6f);
                if (ticks[i].color != col) ticks[i].color = col;
            }
            float rs = 1f + 0.35f * spread - 0.25f * charge;
            ring.rectTransform.localScale = new Vector3(rs, rs, 1f);
            var rc = col; rc.a *= 0.85f;
            if (ring.color != rc) ring.color = rc;
            if (dot.color != col) dot.color = col;
            crossRt.localEulerAngles = new Vector3(0f, 0f, charge * 45f);
        }

        // ------------------------------------------------------------------ sprites gerados
        static Sprite arrowSpr, ringSpr;

        static readonly Vector2[] ArrowPoly =
        {
            new Vector2(3f, 29f), new Vector2(3f, 6f), new Vector2(8.5f, 11.5f), new Vector2(12.5f, 3f),
            new Vector2(16.5f, 4.8f), new Vector2(12.6f, 13.2f), new Vector2(20.5f, 13.2f)
        };

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) c = !c;
            }
            return c;
        }

        /// <summary>Seta medieval: corpo de bronze com relevo, contorno escuro e filete dourado.</summary>
        static Sprite ArrowSprite()
        {
            if (arrowSpr != null) return arrowSpr;
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            Color dark = new Color(0.12f, 0.07f, 0.03f, 1f);
            Color bronze = new Color(0.78f, 0.56f, 0.3f, 1f);
            Color light = new Color(1f, 0.9f, 0.62f, 1f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = float.MaxValue;
                    for (int i = 0, j = ArrowPoly.Length - 1; i < ArrowPoly.Length; j = i++)
                        d = Mathf.Min(d, SegDist(p, ArrowPoly[j], ArrowPoly[i]));
                    bool inside = Inside(p, ArrowPoly);
                    float sd = inside ? -d : d;   // negativo = dentro
                    Color c;
                    float a;
                    if (sd > 1.6f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                    if (sd > -0.4f) { c = dark; a = Mathf.Clamp01(1.6f - sd); }        // contorno
                    else
                    {
                        float k = Mathf.Clamp01((y - 3f) / 26f);                      // mais claro perto da ponta
                        c = Color.Lerp(bronze * 0.8f, bronze, k);
                        if (-sd < 1.6f) c = Color.Lerp(c, light, 0.55f);               // filete interno
                        c.a = 1f; a = 1f;
                    }
                    px[y * n + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            arrowSpr = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(3f / 32f, 29f / 32f), 100f);
            return arrowSpr;
        }

        static Sprite RingSprite()
        {
            if (ringSpr != null) return ringSpr;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float h = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x + 0.5f - h, y + 0.5f - h).magnitude;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 27f) / 1.6f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            ringSpr = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return ringSpr;
        }
    }
}
