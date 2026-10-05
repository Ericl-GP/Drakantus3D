using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Drakantus
{
    /// <summary>
    /// Canto superior direito: minimapa circular (câmera ortográfica própria renderizando numa RenderTexture),
    /// marcadores (jogador, inimigos, NPCs, portais, santuário), nome do lugar, moedas e painel de objetivo.
    /// </summary>
    public partial class HUD
    {
        Camera mmCam;
        GameObject mmCamGo;
        RenderTexture mmTex;
        RawImage mmImg;
        RectTransform mmMarkers, mmArrow;
        readonly List<Image> mmDots = new List<Image>();
        const float MmSize = 200f;                 // diâmetro do minimapa
        const float MmRadius = MmSize * 0.5f - 7f; // raio útil (dentro do aro)
        const float MmWorld = 24f;   // metros do centro até a borda

        Text placeText, coinsText;
        RectTransform coinIconRt;
        float coinPop;
        int lastCoins = -1;
        string lastPlace;

        RectTransform objRt;
        Text objText;

        static readonly Color DotEnemy = new Color(1f, 0.3f, 0.26f, 1f);
        static readonly Color DotBoss = new Color(1f, 0.15f, 0.45f, 1f);
        static readonly Color DotNpc = new Color(1f, 0.86f, 0.35f, 1f);
        static readonly Color DotPortal = new Color(0.72f, 0.45f, 1f, 1f);
        static readonly Color DotSanct = new Color(0.45f, 0.95f, 1f, 1f);

        void BuildMinimap()
        {
            // bloco compacto: aro de bronze ornamentado, nome do lugar e moedas logo abaixo
            var pr = UIKit.R(hudRoot, "Mapa");
            UIKit.Place(pr, TR, TR, new Vector2(-16f, -16f), new Vector2(MmSize + 16f, MmSize + 70f));

            var back = UIKit.Img(pr, "fundo", U.CircleSprite(), new Color(0.05f, 0.035f, 0.025f, 0.9f));
            UIKit.Place(back.rectTransform, TC, TC, new Vector2(0f, -4f), new Vector2(MmSize, MmSize));
            var maskImg = UIKit.Img(back.rectTransform, "mascara", U.CircleSprite(), Color.white, true);
            UIKit.Stretch(maskImg.rectTransform, 7f);
            var mask = maskImg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var rr = UIKit.R(maskImg.rectTransform, "render");
            UIKit.Stretch(rr);
            mmImg = rr.gameObject.AddComponent<RawImage>();
            mmImg.raycastTarget = false;
            mmImg.color = new Color(0.09f, 0.07f, 0.05f, 1f);

            mmMarkers = UIKit.R(maskImg.rectTransform, "marcadores");
            UIKit.Stretch(mmMarkers);

            var arrowImg = UIKit.Img(maskImg.rectTransform, "jogador", UIKit.ArrowSprite(), Color.white);
            mmArrow = UIKit.Place(arrowImg.rectTransform, MID, MID, Vector2.zero, new Vector2(18f, 18f));
            var arrowSh = arrowImg.gameObject.AddComponent<Shadow>();
            arrowSh.effectColor = new Color(0f, 0f, 0f, 0.8f); arrowSh.effectDistance = new Vector2(1f, -1f);

            // moldura: aro de bronze com contas + pontos cardeais em losango
            var ring = UIKit.Img(back.rectTransform, "aro", UltFrameSprite(), UIKit.Bronze);
            UIKit.Stretch(ring.rectTransform, -3f);
            var north = UIKit.Diamond(back.rectTransform, "norte", UIKit.Gold, 12f);
            UIKit.Place(north.rectTransform, TC, MID, new Vector2(0f, 1f), new Vector2(12f, 12f));

            UITip.Add(maskImg.gameObject, () => lastPlace ?? "Mapa",
                () => "<color=#ff4d42>●</color> inimigos  <color=#ffdb59>●</color> pessoas  <color=#b873ff>●</color> portais  <color=#73f2ff>●</color> santuário");

            placeText = UIKit.Txt(pr, "lugar", "", 16, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(placeText.rectTransform, TC, TC, new Vector2(0f, -MmSize - 8f), new Vector2(MmSize + 60f, 22f));
            placeText.horizontalOverflow = HorizontalWrapMode.Overflow;

            var row = UIKit.R(pr, "moedas");
            UIKit.Place(row, TC, TC, new Vector2(0f, -MmSize - 32f), new Vector2(200f, 26f));
            UIKit.HLayout(row.gameObject, 6f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            var coinIcon = UIKit.Img(row, "icone", U.Icon(2, 8), Color.white);
            coinIcon.preserveAspect = true;
            UIKit.LE(coinIcon, 22f, 22f);
            coinIconRt = coinIcon.rectTransform;
            coinsText = UIKit.Txt(row, "valor", "0", 18, UIKit.TextCol, TextAnchor.MiddleLeft, FontStyle.Bold);
            coinsText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // painel de objetivo do andar
            var op = UIKit.Panel(hudRoot, "Objetivo");
            op.raycastTarget = false;
            objRt = UIKit.Place(op.rectTransform, TR, TR, new Vector2(-16f, -MmSize - 96f), new Vector2(232f, 70f));
            UIKit.VLayout(op.gameObject, 3f, new RectOffset(13, 13, 9, 10));
            UIKit.Fit(op.gameObject, false, true);
            var head = UIKit.Txt(objRt, "titulo", "OBJETIVO", 12, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.LE(head, -1f, 15f);
            objText = UIKit.Txt(objRt, "texto", "", 15, UIKit.TextCol, TextAnchor.UpperLeft);
            objText.lineSpacing = 1.08f;
            objRt.gameObject.SetActive(false);
        }

        /// <summary>Painel de objetivo do andar ("" esconde). Partes separadas por " · " viram linhas.</summary>
        public void SetInstanceInfo(string text)
        {
            if (objRt == null) return;
            if (string.IsNullOrEmpty(text)) { if (objRt.gameObject.activeSelf) objRt.gameObject.SetActive(false); return; }
            string s = text.Replace(" · ", "\n");
            int k = s.IndexOf("Objetivo:", StringComparison.Ordinal);
            if (k >= 0) s = s.Substring(0, k) + "<color=#FFD76A>" + s.Substring(k) + "</color>";
            UIKit.Set(objText, s);
            if (!objRt.gameObject.activeSelf) objRt.gameObject.SetActive(true);
        }

        void UpdateCoins(int coins)
        {
            if (coinsText == null) return;
            if (lastCoins >= 0 && coins > lastCoins) coinPop = 1f;
            lastCoins = coins;
            UIKit.Set(coinsText, coins.ToString());
        }

        void CreateMinimapCam()
        {
            mmTex = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { name = "Minimapa", antiAliasing = 1 };
            mmTex.Create();
            mmCamGo = new GameObject("CameraMinimapa");
            mmCam = mmCamGo.AddComponent<Camera>();
            mmCam.orthographic = true;
            mmCam.orthographicSize = MmWorld;
            mmCam.clearFlags = CameraClearFlags.SolidColor;
            mmCam.backgroundColor = new Color(0.05f, 0.06f, 0.1f, 1f);
            mmCam.cullingMask = ~0;
            mmCam.nearClipPlane = 0.5f;
            mmCam.farClipPlane = 120f;
            mmCam.depth = -20f;
            mmCam.allowHDR = false;
            mmCam.allowMSAA = false;
            mmCam.useOcclusionCulling = false;
            mmCam.targetTexture = mmTex;
            // URP: minimapa sem sombras nem pós-processamento (nomes qualificados para não trazer o namespace inteiro)
            var urp = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(mmCam);
            if (urp != null) { urp.renderShadows = false; urp.renderPostProcessing = false; }
            mmImg.texture = mmTex;
            mmImg.color = Color.white;
        }

        void DestroyMinimap()
        {
            if (mmCamGo != null) Destroy(mmCamGo);
            if (mmTex != null) { mmTex.Release(); Destroy(mmTex); }
            mmCamGo = null; mmCam = null; mmTex = null;
        }

        void UpdateMinimap(Player p, float dt)
        {
            var g = Game.I;
            if (g == null) return;
            string place = g.level != null ? g.level.title : "";
            if (place != lastPlace) { lastPlace = place; UIKit.Set(placeText, place); }

            if (coinPop > 0f)
            {
                coinPop = Mathf.Max(0f, coinPop - dt * 3f);
                coinIconRt.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(coinPop * Mathf.PI));
            }

            if (mmCam == null) CreateMinimapCam();
            if (!mmCam.enabled) mmCam.enabled = true;

            float yaw = CameraRig.Cam != null ? CameraRig.Cam.transform.eulerAngles.y : 45f;
            Vector3 pp = p.transform.position;
            mmCamGo.transform.SetPositionAndRotation(pp + Vector3.up * 45f, Quaternion.Euler(90f, yaw, 0f));

            Quaternion inv = Quaternion.Euler(0f, -yaw, 0f);
            float k = MmRadius / MmWorld;
            int n = 0;
            if (g.interactables != null)
                foreach (var it in g.interactables)
                {
                    if (it == null) continue;
                    string kind = it.kind ?? "";
                    if (kind == "portal") Dot(ref n, inv, k, it.transform.position - pp, DotPortal, 12f, true);
                    else if (kind == "sanctuary") Dot(ref n, inv, k, it.transform.position - pp, DotSanct, 11f, true);
                    else Dot(ref n, inv, k, it.transform.position - pp, DotNpc, 8f, false);
                }
            if (g.enemies != null)
                foreach (var e in g.enemies)
                {
                    if (e == null || e.dead) continue;
                    bool boss = e.def != null && e.def.boss;
                    Dot(ref n, inv, k, e.transform.position - pp, boss ? DotBoss : DotEnemy, boss ? 12f : 7f, boss);
                }
            for (int i = n; i < mmDots.Count; i++)
                if (mmDots[i].enabled) mmDots[i].enabled = false;

            mmArrow.localEulerAngles = new Vector3(0f, 0f, -(p.transform.eulerAngles.y - yaw));
        }

        void Dot(ref int n, Quaternion inv, float k, Vector3 off, Color c, float size, bool clamp)
        {
            Vector3 l = inv * off;
            Vector2 pos = new Vector2(l.x, l.z) * k;
            float r = MmRadius - 8f;
            if (pos.sqrMagnitude > r * r)
            {
                if (!clamp) return;
                pos = pos.normalized * r;
            }
            if (n >= mmDots.Count)
            {
                var im = UIKit.Img(mmMarkers, "ponto", U.CircleSprite(), c);
                im.rectTransform.anchorMin = im.rectTransform.anchorMax = MID;
                im.rectTransform.pivot = MID;
                var o = im.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.6f); o.effectDistance = new Vector2(1f, -1f);
                mmDots.Add(im);
            }
            var d = mmDots[n++];
            if (!d.enabled) d.enabled = true;
            if (d.color != c) d.color = c;
            var rt = d.rectTransform;
            rt.anchoredPosition = pos;
            if (rt.sizeDelta.x != size) rt.sizeDelta = new Vector2(size, size);
        }
    }
}
