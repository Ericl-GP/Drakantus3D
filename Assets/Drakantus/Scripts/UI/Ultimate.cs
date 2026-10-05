using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Drakantus
{
    /// <summary>
    /// ULTIMATE no HUD: slot circular grande à direita da barra de ações (tecla R), anel de carga em volta do retrato,
    /// efeitos quando está pronta (brilho, raios girando, faíscas, "ULTIMATE!") e flash + nome grande ao usar.
    /// O Player trata a tecla R e o som "ult_ready"; aqui só mostramos e permitimos o clique.
    /// </summary>
    public partial class HUD
    {
        // ------------------------------------------------------------------ campos
        RectTransform ultRt, ultRaysA, ultRaysB, ultReadyRt;
        Image ultRaysImgA, ultRaysImgB, ultGlow, ultBg, ultFill, ultTrack, ultRing, ultIcon, ultFrame, ultGem;
        readonly List<Image> ultStuds = new List<Image>();
        Text ultPct, ultKey, ultReadyText;
        Image portraitUltTrack, portraitUltRing;

        Image ultFlash;
        Text ultCastName, ultCastSub;
        float ultFlashT = -1f, ultNameT = -1f;
        Color ultFlashCol = Color.white;
        const float UltFlashDur = 0.4f, UltNameDur = 1.2f;

        string ultClass;
        Color ultCol = Color.white;
        float ultShown, ultPrev = -1f, ultPop, ultReadyT;
        bool ultWasReady;
        int ultLastPct = -1;
        Player ultPlayer;

        class UltSpark { public Image img; public RectTransform rt; public float t, life, vx, vy, size; public bool on; }
        readonly List<UltSpark> ultSparks = new List<UltSpark>();
        float ultSparkTimer;

        const float UltSize = 132f, UltScale = 0.66f;

        // ------------------------------------------------------------------ construção
        void BuildUltimate()
        {
            // --- slot grande à direita da barra de ações
            // caixa reduzida (UltScale): o conteúdo mantém as medidas originais e é escalado junto
            var box = UIKit.R(hudRoot, "UltimateCaixa");
            float cx = actionW * 0.5f + 18f + UltSize * UltScale * 0.5f;
            UIKit.Place(box, BC, MID, new Vector2(cx, ActionBottom + ActionH * 0.5f), new Vector2(UltSize, UltSize));
            box.localScale = Vector3.one * UltScale;
            ultRt = UIKit.R(box, "Ultimate");
            UIKit.Place(ultRt, MID, MID, Vector2.zero, new Vector2(UltSize, UltSize));

            ultRaysImgA = UIKit.Img(ultRt, "raios", UltRaysSprite(), new Color(1f, 1f, 1f, 0f));
            ultRaysA = UIKit.Place(ultRaysImgA.rectTransform, MID, MID, Vector2.zero, new Vector2(300f, 300f));
            ultRaysImgB = UIKit.Img(ultRt, "raios2", UltRaysSprite(), new Color(1f, 1f, 1f, 0f));
            ultRaysB = UIKit.Place(ultRaysImgB.rectTransform, MID, MID, Vector2.zero, new Vector2(230f, 230f));
            ultRaysB.localEulerAngles = new Vector3(0f, 0f, 15f);

            ultGlow = UIKit.Img(ultRt, "brilho", UIKit.SoftSprite(), new Color(1f, 1f, 1f, 0f));
            UIKit.Place(ultGlow.rectTransform, MID, MID, Vector2.zero, new Vector2(230f, 230f));

            // fundo (recebe clique e dica)
            ultBg = UIKit.Img(ultRt, "fundo", UltDiscSprite(), new Color(0.07f, 0.05f, 0.035f, 0.96f), true);
            UIKit.Place(ultBg.rectTransform, MID, MID, Vector2.zero, new Vector2(UltSize - 10f, UltSize - 10f));
            ultBg.gameObject.AddComponent<UIClick>().onLeft = OnUltClick;
            UITip.Add(ultBg.gameObject, UltTipTitle, UltTipBody);

            // "líquido" que sobe de baixo para cima
            ultFill = UIKit.Img(ultBg.rectTransform, "carga", UltDiscSprite(), new Color(1f, 1f, 1f, 0.5f));
            UIKit.Stretch(ultFill.rectTransform, 4f);
            ultFill.type = Image.Type.Filled;
            ultFill.fillMethod = Image.FillMethod.Vertical;
            ultFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            ultFill.fillAmount = 0f;

            var sheen = UIKit.Img(ultBg.rectTransform, "luz", UIKit.SoftSprite(), new Color(1f, 1f, 1f, 0.1f));
            UIKit.Place(sheen.rectTransform, MID, MID, new Vector2(0f, 22f), new Vector2(110f, 70f));

            // anel radial de progresso (dentro da moldura)
            ultTrack = UIKit.Img(ultRt, "anel_fundo", UltRingSprite(), new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(ultTrack.rectTransform, MID, MID, Vector2.zero, new Vector2(UltSize - 14f, UltSize - 14f));
            ultRing = UIKit.Img(ultRt, "anel", UltRingSprite(), Color.white);
            UIKit.Place(ultRing.rectTransform, MID, MID, Vector2.zero, new Vector2(UltSize - 14f, UltSize - 14f));
            ultRing.type = Image.Type.Filled;
            ultRing.fillMethod = Image.FillMethod.Radial360;
            ultRing.fillOrigin = (int)Image.Origin360.Bottom;
            ultRing.fillClockwise = true;
            ultRing.fillAmount = 0f;

            // ícone da classe ampliado
            ultIcon = UIKit.Img(ultRt, "icone", null, Color.white);
            UIKit.Place(ultIcon.rectTransform, MID, MID, new Vector2(0f, 4f), new Vector2(76f, 76f));
            ultIcon.preserveAspect = true;
            ultIcon.enabled = false;

            // moldura dourada ornamentada
            ultFrame = UIKit.Img(ultRt, "moldura", UltFrameSprite(), UIKit.Bronze);
            UIKit.Place(ultFrame.rectTransform, MID, MID, Vector2.zero, new Vector2(UltSize + 8f, UltSize + 8f));
            for (int k = 0; k < 4; k++)
            {
                // pontas em losango nos lados (cima tem a gema)
                if (k == 0) continue;
                float ang = k * 90f * Mathf.Deg2Rad;
                var st = UIKit.Img(ultRt, "ponta" + k, U.WhiteSprite(), UIKit.Bronze);
                float r = UltSize * 0.5f + 5f;
                UIKit.Place(st.rectTransform, MID, MID, new Vector2(Mathf.Sin(ang) * r, Mathf.Cos(ang) * r), new Vector2(13f, 13f));
                st.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
                ultStuds.Add(st);
            }
            var crest = UIKit.Img(ultRt, "brasao", U.WhiteSprite(), UIKit.Bronze);
            UIKit.Place(crest.rectTransform, MID, MID, new Vector2(0f, UltSize * 0.5f + 4f), new Vector2(24f, 24f));
            crest.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            ultStuds.Add(crest);
            ultGem = UIKit.Img(crest.rectTransform, "gema", U.WhiteSprite(), Color.white);
            UIKit.Stretch(ultGem.rectTransform, 5f);

            // porcentagem e tecla
            ultPct = UIKit.Txt(ultRt, "porcentagem", "0%", 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Place(ultPct.rectTransform, MID, MID, new Vector2(0f, -38f), new Vector2(90f, 24f));
            ultPct.horizontalOverflow = HorizontalWrapMode.Overflow;

            var kb = UIKit.Img(ultRt, "tecla", UltDiscSprite(), UIKit.Gold);
            UIKit.Place(kb.rectTransform, MID, MID, new Vector2(-UltSize * 0.36f, -UltSize * 0.36f), new Vector2(34f, 34f));
            var kbIn = UIKit.Img(kb.rectTransform, "in", UltDiscSprite(), new Color(0.06f, 0.045f, 0.02f, 1f));
            UIKit.Stretch(kbIn.rectTransform, 2.5f);
            ultKey = UIKit.Txt(kbIn.rectTransform, "t", "R", 18, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(ultKey.rectTransform);
            ultKey.horizontalOverflow = HorizontalWrapMode.Overflow;

            // "ULTIMATE!" acima
            ultReadyText = UIKit.Txt(ultRt, "pronta", "ULTIMATE!", 26, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            ultReadyRt = UIKit.Place(ultReadyText.rectTransform, MID, MID, new Vector2(0f, UltSize * 0.5f + 36f), new Vector2(240f, 34f));
            ultReadyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            ultReadyText.gameObject.SetActive(false);

            // faíscas (pool)
            for (int i = 0; i < 14; i++)
            {
                var im = UIKit.Img(ultRt, "faisca", UIKit.SoftSprite(), new Color(1f, 1f, 1f, 0f));
                var rt = UIKit.Place(im.rectTransform, MID, MID, Vector2.zero, new Vector2(12f, 12f));
                im.gameObject.SetActive(false);
                ultSparks.Add(new UltSpark { img = im, rt = rt });
            }

            // --- anel fino em volta do retrato
            if (portraitRt != null)
            {
                portraitUltTrack = UIKit.Img(portraitRt, "ult_fundo", UltThinRingSprite(), new Color(0f, 0f, 0f, 0.45f));
                UIKit.Place(portraitUltTrack.rectTransform, MID, MID, Vector2.zero, new Vector2(104f, 104f));
                portraitUltTrack.transform.SetSiblingIndex(0);
                portraitUltRing = UIKit.Img(portraitRt, "ult_carga", UltThinRingSprite(), Color.white);
                UIKit.Place(portraitUltRing.rectTransform, MID, MID, Vector2.zero, new Vector2(104f, 104f));
                portraitUltRing.type = Image.Type.Filled;
                portraitUltRing.fillMethod = Image.FillMethod.Radial360;
                portraitUltRing.fillOrigin = (int)Image.Origin360.Bottom;
                portraitUltRing.fillClockwise = true;
                portraitUltRing.fillAmount = 0f;
                portraitUltRing.transform.SetSiblingIndex(1);
            }

            // --- flash de tela e nome grande (na camada de faixas, fora do hudRoot)
            ultFlash = UIKit.Img(bannerLayer, "ult_flash", U.WhiteSprite(), new Color(1f, 1f, 1f, 0f));
            UIKit.Stretch(ultFlash.rectTransform);
            ultFlash.transform.SetAsFirstSibling();
            ultFlash.gameObject.SetActive(false);

            ultCastName = UIKit.Txt(bannerLayer, "ult_nome", "", 70, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Place(ultCastName.rectTransform, MID, MID, new Vector2(0f, 90f), new Vector2(1600f, 120f));
            ultCastName.horizontalOverflow = HorizontalWrapMode.Overflow;
            ultCastName.gameObject.SetActive(false);
            ultCastSub = UIKit.Txt(bannerLayer, "ult_sub", "— ULTIMATE —", 22, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            UIKit.Place(ultCastSub.rectTransform, MID, MID, new Vector2(0f, 164f), new Vector2(800f, 36f));
            ultCastSub.horizontalOverflow = HorizontalWrapMode.Overflow;
            ultCastSub.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ interação
        void OnUltClick()
        {
            var p = Hero;
            if (p == null) return;
            if (p.UltReady) p.UseUltimate();
            else { Sfx.Play("ui_error"); ultPop = 0.6f; }
        }

        string UltTipTitle()
        {
            var p = Hero;
            if (p == null) return "Ultimate";
            string n = p.UltName();
            return "Ultimate: " + (string.IsNullOrEmpty(n) ? "?" : n);
        }

        string UltTipBody()
        {
            var p = Hero;
            if (p == null) return "";
            string d = p.UltDesc() ?? "";
            int pct = Mathf.FloorToInt(Mathf.Clamp01(p.ultCharge) * 100f + 0.001f);
            string state = p.UltReady
                ? "<color=#FFD76A><b>PRONTA!</b> Aperte [R] ou clique.</color>"
                : "Carga: <color=#FFD76A>" + pct + "%</color>  ·  enche ao causar e receber dano.";
            return (d.Length > 0 ? d + "\n" : "") + state;
        }

        // ------------------------------------------------------------------ atualização (dentro do HUD visível)
        void UpdateUltimate(Player p, float udt)
        {
            if (ultRt == null || p == null) return;

            if (ultClass != GameState.ClassId)
            {
                ultClass = GameState.ClassId;
                var cd = GameState.Class;
                ultCol = cd != null ? U.Hex(cd.color) : Color.white;
                ultIcon.sprite = cd != null ? U.Icon(cd.icon) : null;
                ultIcon.enabled = ultIcon.sprite != null;
                ultFill.color = new Color(ultCol.r, ultCol.g, ultCol.b, 0.55f);
                ultRing.color = ultCol;
                ultGem.color = ultCol;
                ultGlow.color = new Color(ultCol.r, ultCol.g, ultCol.b, 0f);
            }

            if (ultPlayer != p) { ultPlayer = p; ultPrev = -1f; ultShown = Mathf.Clamp01(p.ultCharge); ultWasReady = p.UltReady; }

            float c = Mathf.Clamp01(p.ultCharge);
            bool ready = p.UltReady;

            // usou a ultimate: carga caiu de cheia para quase zero
            if (ultPrev >= 0.999f && c < 0.1f) TriggerUltCast(p);
            if (ready && !ultWasReady) { ultPop = 1f; ultReadyT = 0f; for (int i = 0; i < 6; i++) SpawnUltSpark(true); }
            ultWasReady = ready;
            ultPrev = c;

            // preenchimento suave ao subir, imediato ao descer
            ultShown = c < ultShown ? c : Mathf.MoveTowards(ultShown, c, udt * 1.5f);
            ultFill.fillAmount = ultShown;
            ultRing.fillAmount = ultShown;
            if (portraitUltRing != null) portraitUltRing.fillAmount = ultShown;

            int pct = Mathf.FloorToInt(c * 100f + 0.001f);
            if (pct != ultLastPct) { ultLastPct = pct; ultPct.text = pct + "%"; }

            float t = Time.unscaledTime;
            if (ready)
            {
                ultReadyT += udt;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
                ultGlow.color = new Color(ultCol.r, ultCol.g, ultCol.b, 0.45f + 0.35f * pulse);
                ultGlow.rectTransform.localScale = Vector3.one * (1f + 0.12f * pulse);
                float raysA = Mathf.Clamp01(ultReadyT * 2f);
                ultRaysImgA.color = new Color(Mathf.Lerp(ultCol.r, 1f, 0.35f), Mathf.Lerp(ultCol.g, 1f, 0.35f), Mathf.Lerp(ultCol.b, 1f, 0.35f), 0.55f * raysA);
                ultRaysImgB.color = new Color(1f, 0.9f, 0.55f, (0.35f + 0.2f * pulse) * raysA);
                ultRaysA.localEulerAngles = new Vector3(0f, 0f, -t * 35f);
                ultRaysB.localEulerAngles = new Vector3(0f, 0f, t * 55f + 15f);
                Color fc = Color.Lerp(UIKit.Gold, Color.white, pulse * 0.6f);
                ultFrame.color = fc;
                for (int i = 0; i < ultStuds.Count; i++) ultStuds[i].color = fc;
                ultIcon.color = Color.Lerp(Color.white, ultCol, 0.25f * (1f - pulse));
                ultPct.color = UIKit.Gold;

                if (!ultReadyText.gameObject.activeSelf) ultReadyText.gameObject.SetActive(true);
                bool blink = Mathf.Repeat(t * 2.6f, 1f) < 0.68f;
                var rc = Color.Lerp(UIKit.Gold, Color.white, pulse * 0.5f);
                rc.a = blink ? 1f : 0.25f;
                ultReadyText.color = rc;
                ultReadyRt.anchoredPosition = new Vector2(0f, UltSize * 0.5f + 36f + 4f * Mathf.Sin(t * 3f));
                ultReadyRt.localScale = Vector3.one * (1f + 0.06f * pulse);

                if (portraitUltRing != null) portraitUltRing.color = Color.Lerp(ultCol, UIKit.Gold, pulse);

                ultSparkTimer -= udt;
                if (ultSparkTimer <= 0f) { ultSparkTimer = 0.11f; SpawnUltSpark(false); }

                ultRt.localScale = Vector3.one * (1f + 0.035f * pulse + 0.2f * Mathf.Sin(ultPop * Mathf.PI));
            }
            else
            {
                if (ultGlow.color.a != 0f) ultGlow.color = new Color(ultCol.r, ultCol.g, ultCol.b, 0f);
                if (ultRaysImgA.color.a != 0f) ultRaysImgA.color = new Color(1f, 1f, 1f, 0f);
                if (ultRaysImgB.color.a != 0f) ultRaysImgB.color = new Color(1f, 1f, 1f, 0f);
                if (ultFrame.color != UIKit.Bronze)
                {
                    ultFrame.color = UIKit.Bronze;
                    for (int i = 0; i < ultStuds.Count; i++) ultStuds[i].color = UIKit.Bronze;
                }
                var ic = Color.Lerp(new Color(0.55f, 0.57f, 0.64f, 1f), Color.Lerp(Color.white, ultCol, 0.35f), 0.35f + 0.65f * c);
                if (ultIcon.color != ic) ultIcon.color = ic;
                if (ultPct.color != Color.white) ultPct.color = Color.white;
                if (ultReadyText.gameObject.activeSelf) ultReadyText.gameObject.SetActive(false);
                if (portraitUltRing != null && portraitUltRing.color != ultCol) portraitUltRing.color = ultCol;
                ultRt.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(ultPop * Mathf.PI));
            }
            ultPop = Mathf.Max(0f, ultPop - udt * 2.6f);

            UpdateUltSparks(udt);
        }

        void SpawnUltSpark(bool burst)
        {
            UltSpark s = null;
            for (int i = 0; i < ultSparks.Count; i++) if (!ultSparks[i].on) { s = ultSparks[i]; break; }
            if (s == null) return;
            s.on = true;
            s.t = 0f;
            s.life = UnityEngine.Random.Range(0.8f, 1.3f);
            s.size = UnityEngine.Random.Range(8f, 18f);
            float x = UnityEngine.Random.Range(-UltSize * 0.42f, UltSize * 0.42f);
            float y = burst ? UnityEngine.Random.Range(-20f, 20f) : -UltSize * 0.3f + UnityEngine.Random.Range(-10f, 10f);
            s.vx = burst ? x * 1.6f : UnityEngine.Random.Range(-12f, 12f);
            s.vy = burst ? UnityEngine.Random.Range(80f, 160f) : UnityEngine.Random.Range(70f, 120f);
            s.rt.anchoredPosition = new Vector2(x, y);
            s.rt.sizeDelta = new Vector2(s.size, s.size);
            s.img.color = UnityEngine.Random.value < 0.5f ? UIKit.Gold : Color.Lerp(ultCol, Color.white, 0.4f);
            s.img.gameObject.SetActive(true);
            s.rt.SetAsLastSibling();
        }

        void UpdateUltSparks(float udt)
        {
            for (int i = 0; i < ultSparks.Count; i++)
            {
                var s = ultSparks[i];
                if (!s.on) continue;
                s.t += udt;
                if (s.t >= s.life) { s.on = false; s.img.gameObject.SetActive(false); continue; }
                float k = s.t / s.life;
                s.rt.anchoredPosition += new Vector2(s.vx, s.vy) * udt;
                var col = s.img.color;
                col.a = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
                s.img.color = col;
                float sz = s.size * (1f - 0.5f * k);
                s.rt.sizeDelta = new Vector2(sz, sz);
            }
        }

        void HideUltSparks()
        {
            for (int i = 0; i < ultSparks.Count; i++)
                if (ultSparks[i].on) { ultSparks[i].on = false; ultSparks[i].img.gameObject.SetActive(false); }
        }

        // ------------------------------------------------------------------ flash + nome ao usar
        void TriggerUltCast(Player p)
        {
            string n = p != null ? p.UltName() : "";
            ultFlashCol = Color.Lerp(Color.white, ultCol, 0.45f);
            ultFlashT = 0f;
            ultFlash.color = new Color(ultFlashCol.r, ultFlashCol.g, ultFlashCol.b, 0.35f);
            ultFlash.gameObject.SetActive(true);

            ultNameT = 0f;
            ultCastName.text = string.IsNullOrEmpty(n) ? "ULTIMATE" : n.ToUpperInvariant();
            ultCastName.color = Color.Lerp(ultCol, Color.white, 0.3f);
            ultCastName.gameObject.SetActive(true);
            ultCastSub.gameObject.SetActive(true);
            ultCastName.transform.SetAsLastSibling();
            ultCastSub.transform.SetAsLastSibling();
            HideUltSparks();
        }

        /// <summary>Roda sempre (mesmo com o HUD escondido) para terminar o flash e o nome.</summary>
        void UpdateUltFx(float udt)
        {
            if (ultFlash == null) return;
            if (ultFlashT >= 0f)
            {
                ultFlashT += udt;
                float k = Mathf.Clamp01(ultFlashT / UltFlashDur);
                ultFlash.color = new Color(ultFlashCol.r, ultFlashCol.g, ultFlashCol.b, 0.35f * (1f - k));
                if (k >= 1f) { ultFlashT = -1f; ultFlash.gameObject.SetActive(false); }
            }
            if (ultNameT >= 0f)
            {
                ultNameT += udt;
                float k = ultNameT / UltNameDur;
                if (k >= 1f)
                {
                    ultNameT = -1f;
                    ultCastName.gameObject.SetActive(false);
                    ultCastSub.gameObject.SetActive(false);
                    return;
                }
                float a = ultNameT < 0.1f ? ultNameT / 0.1f : ultNameT > UltNameDur - 0.3f ? Mathf.Clamp01((UltNameDur - ultNameT) / 0.3f) : 1f;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(ultNameT / 0.25f), 3f);
                ultCastName.rectTransform.localScale = Vector3.one * (Mathf.Lerp(1.7f, 1f, e) + 0.06f * k);
                var nc = ultCastName.color; nc.a = a; ultCastName.color = nc;
                var sc = ultCastSub.color; sc.a = a; ultCastSub.color = sc;
            }
        }

        // ------------------------------------------------------------------ sprites gerados
        static Sprite ultDisc, ultRingSpr, ultThinRing, ultFrameSpr, ultRaysSpr;

        static Texture2D UltTex(int n)
        {
            return new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        }

        static Sprite UltSpriteOf(Texture2D t)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100);
        }

        /// <summary>Anel suavizado. inner/outer em fração do raio (0..1).</summary>
        static Sprite MakeUltRing(int n, float inner, float outer)
        {
            var t = UltTex(n);
            var px = new Color32[n * n];
            float h = n * 0.5f, aa = 1.5f / h;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((outer - d) / aa) * Mathf.Clamp01((d - inner) / aa);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return UltSpriteOf(t);
        }

        static Sprite UltDiscSprite()
        {
            if (ultDisc == null) ultDisc = MakeUltRing(128, -1f, 0.985f);
            return ultDisc;
        }

        static Sprite UltRingSprite()
        {
            if (ultRingSpr == null) ultRingSpr = MakeUltRing(256, 0.84f, 0.97f);
            return ultRingSpr;
        }

        static Sprite UltThinRingSprite()
        {
            if (ultThinRing == null) ultThinRing = MakeUltRing(256, 0.9f, 0.98f);
            return ultThinRing;
        }

        /// <summary>Moldura: aro grosso com chanfro, filete interno e 8 contas ao redor (branco sombreado, tingido de dourado).</summary>
        static Sprite UltFrameSprite()
        {
            if (ultFrameSpr != null) return ultFrameSpr;
            const int n = 256;
            var t = UltTex(n);
            var px = new Color32[n * n];
            float h = n * 0.5f, aa = 1.5f / h;
            const float outer = 0.95f, inner = 0.84f, beadR = 0.06f, beadAt = 0.895f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // aro principal
                    float band = Mathf.Clamp01((outer - d) / aa) * Mathf.Clamp01((d - inner) / aa);
                    float tb = Mathf.Clamp01((d - inner) / (outer - inner));
                    float bright = 0.55f + 0.45f * (1f - Mathf.Abs(tb - 0.4f) * 1.6f);
                    // sulco no meio do aro
                    float groove = 1f - 0.35f * Mathf.Clamp01(1f - Mathf.Abs(tb - 0.72f) / 0.07f);
                    bright *= groove;
                    // contas (8)
                    float ang = Mathf.Atan2(dy, dx);
                    float step = Mathf.PI / 4f;
                    float near = Mathf.Round(ang / step) * step;
                    float bx = Mathf.Cos(near) * beadAt, by = Mathf.Sin(near) * beadAt;
                    float bd = Mathf.Sqrt((dx - bx) * (dx - bx) + (dy - by) * (dy - by));
                    float bead = Mathf.Clamp01((beadR - bd) / aa);
                    float beadLight = 0.7f + 0.3f * Mathf.Clamp01(1f - bd / beadR) + 0.25f * Mathf.Clamp01((dy - by) / beadR);
                    // filete interno fino
                    float fil = Mathf.Clamp01((0.815f - d) / aa) * Mathf.Clamp01((d - 0.79f) / aa) * 0.85f;
                    float a = Mathf.Max(band, Mathf.Max(bead, fil));
                    float b = bead > band ? beadLight : (band > 0f ? bright : 0.8f);
                    b *= 0.85f + 0.15f * (dy * 0.5f + 0.5f); // luz vinda de cima
                    byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(b) * 255f);
                    px[y * n + x] = new Color32(v, v, v, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            ultFrameSpr = UltSpriteOf(t);
            return ultFrameSpr;
        }

        /// <summary>Estrela de raios (12 largos + 12 finos) com núcleo brilhante, some nas bordas.</summary>
        static Sprite UltRaysSprite()
        {
            if (ultRaysSpr != null) return ultRaysSpr;
            const int n = 256;
            var t = UltTex(n);
            var px = new Color32[n * n];
            float h = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float wide = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 6f)), 10f);
                    float thin = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 6f + Mathf.PI * 0.5f)), 40f) * 0.6f;
                    float fall = Mathf.Clamp01(1f - d);
                    fall = fall * fall;
                    float core = Mathf.Clamp01(1f - d / 0.35f);
                    float a = Mathf.Max((wide + thin) * fall, core * core * 0.6f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            ultRaysSpr = UltSpriteOf(t);
            return ultRaysSpr;
        }
    }
}
