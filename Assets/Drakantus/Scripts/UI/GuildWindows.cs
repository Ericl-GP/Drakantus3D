using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Drakantus
{
    /// <summary>
    /// Interface da Guilda (partial do HUD): rastreador de missões (abaixo do objetivo), selo de rank
    /// ao lado do retrato, cerimônia de promoção e as janelas Quadro de Missões, Rank, Loja de Pets e Meus Pets.
    /// Instalado pelo Game com HUD.I.InstallGuild().
    /// </summary>
    public partial class HUD
    {
        // rastreador / selo
        RectTransform guildTrackerRt, guildSealRt;
        Text guildTrackerText, guildSealText;
        Image guildSealRing;
        bool guildDirty = true;
        int guildSealShown = -1;
        // cerimônia
        RectTransform guildCereRt, guildCereBandRt;
        CanvasGroup guildCereCg;
        Text guildCereTitle, guildCereSub, guildCereRewards, guildCereLetter;
        Image guildCereRing, guildCereGlow;
        float guildCereT = -1f;
        int guildCereRank;

        static readonly Color PaperCol = new Color(0.96f, 0.92f, 0.81f, 1f);
        static readonly Color PaperGold = new Color(1f, 0.9f, 0.62f, 1f);
        static readonly Color PaperLocked = new Color(0.7f, 0.67f, 0.6f, 1f);
        static readonly Color InkCol = new Color(0.24f, 0.16f, 0.09f, 1f);
        static readonly Color InkDim = new Color(0.43f, 0.34f, 0.25f, 1f);

        // ================================================================== instalação
        public void InstallGuild()
        {
            if (GetComponent<GuildHud>() != null) return;
            GuildData.Load();
            BuildGuildTracker();
            BuildGuildSeal();
            BuildGuildCeremony();
            BuildMetaButtons();   // [Progressão] Conquistas, Coleção e Códex
            var gh = gameObject.AddComponent<GuildHud>();
            gh.hud = this;
        }

        internal void GuildMarkDirty()
        {
            guildDirty = true;
            if (modalGo != null && (modalKind == "quests" || modalKind == "rank" || modalKind == "achievements" || modalKind == "collection")) windowDirty = true;
        }

        internal void GuildTick(float udt)
        {
            if (guildTrackerRt == null) return;
            if (guildDirty)
            {
                guildDirty = false;
                RefreshGuildTracker();
                RefreshGuildSeal();
                RefreshCodexBadge();
            }
            if (guildSealRing != null)
            {
                Color c = Ranks.AnimColor(GuildState.G.rank, Time.unscaledTime);
                guildSealRing.color = c;
                guildSealText.color = Color.Lerp(c, Color.white, 0.35f);
            }
            if (guildTrackerRt.gameObject.activeSelf)
            {
                float y = -384f;
                if (objRt != null && objRt.gameObject.activeSelf) y = objRt.anchoredPosition.y - objRt.rect.height - 12f;
                var ap = new Vector2(-24f, y);
                if ((guildTrackerRt.anchoredPosition - ap).sqrMagnitude > 0.25f) guildTrackerRt.anchoredPosition = ap;
            }
            UpdateGuildCeremony(udt);
            UpdateCodexFx(udt);   // [Progressão] carimbo de nota e aviso de conquista
        }

        // ================================================================== rastreador de missões
        void BuildGuildTracker()
        {
            var p = UIKit.Panel(hudRoot, "Missoes_Guilda");
            guildTrackerRt = UIKit.Place(p.rectTransform, TR, TR, new Vector2(-24f, -384f), new Vector2(274f, 60f));
            UIKit.VLayout(p.gameObject, 4f, new RectOffset(16, 16, 10, 12));
            UIKit.Fit(p.gameObject, false, true);
            var head = UIKit.Txt(guildTrackerRt, "titulo", "MISSÕES DA GUILDA", 14, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.LE(head, -1f, 18f);
            guildTrackerText = UIKit.Txt(guildTrackerRt, "texto", "", 16, UIKit.TextCol, TextAnchor.UpperLeft);
            guildTrackerText.lineSpacing = 1.08f;
            p.gameObject.AddComponent<UIClick>().onLeft = QuestBoardWindow;
            UITip.Add(p.gameObject, () => "Missões da Guilda", () => "Clique para abrir o quadro de missões.\nEntregue as concluídas ao Oren, na Guilda.");
            guildTrackerRt.gameObject.SetActive(false);
        }

        void RefreshGuildTracker()
        {
            var G = GuildState.G;
            if (G.active.Count == 0)
            {
                if (guildTrackerRt.gameObject.activeSelf) guildTrackerRt.gameObject.SetActive(false);
                return;
            }
            var sb = new StringBuilder();
            foreach (var a in G.active)
            {
                var d = Quests.Def(a.id);
                if (d == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Quests.TypeColor(d))).Append(">●</color> <b>").Append(d.title).Append("</b>\n");
                if (a.done) sb.Append("   <color=#8fe08a>✓ Concluída — entregue na Guilda</color>");
                else sb.Append("   <color=#c8cdd8><size=14>").Append(Quests.GoalText(d)).Append("</size></color>  <color=#FFD76A>")
                       .Append(a.progress).Append('/').Append(d.count).Append("</color>");
            }
            UIKit.Set(guildTrackerText, sb.ToString());
            if (!guildTrackerRt.gameObject.activeSelf) guildTrackerRt.gameObject.SetActive(true);
        }

        // ================================================================== selo de rank (ao lado do retrato)
        void BuildGuildSeal()
        {
            guildSealRing = UIKit.Img(hudRoot, "Selo_Rank", U.CircleSprite(), Color.gray, true);
            guildSealRt = UIKit.Place(guildSealRing.rectTransform, TL, MID, new Vector2(512f, -62f), new Vector2(66f, 66f));
            var inner = UIKit.Img(guildSealRt, "in", U.CircleSprite(), new Color(0.07f, 0.08f, 0.13f, 1f));
            UIKit.Stretch(inner.rectTransform, 4f);
            guildSealText = UIKit.Txt(inner.rectTransform, "letra", "F", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Stretch(guildSealText.rectTransform);
            guildSealText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var cap = UIKit.Txt(hudRoot, "rank_rotulo", "RANK", 12, UIKit.DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(cap.rectTransform, TL, MID, new Vector2(512f, -104f), new Vector2(80f, 16f));
            guildSealRing.gameObject.AddComponent<UIClick>().onLeft = RankWindow;
            guildSealRing.gameObject.AddComponent<UIHover>().scale = 1.08f;
            UITip.Add(guildSealRing.gameObject, () => "Rank " + Ranks.Letter(GuildState.G.rank) + " · " + GuildState.G.title, () =>
            {
                int have, need;
                Ranks.Progress(out have, out need);
                string s = "Pontos de Guilda: " + have;
                if (!Ranks.IsMax) s += " / " + need + " (Rank " + Ranks.NextDef.name + ")";
                if (Ranks.TrialReady) s += "\nProva de Promoção liberada no quadro!";
                return s + "\nClique para ver o rank.";
            });

            var pb = UIKit.Btn(hudRoot, "Pets", new Vector2(70f, 30f), PetsWindow, UIKit.BtnCol, 15);
            UIKit.Place(UIKit.RT(pb), TL, MID, new Vector2(512f, -132f), new Vector2(70f, 30f));
            UITip.Add(pb.gameObject, () => "Meus pets", () => "Equipe, renomeie e veja o nível dos seus pets.\nCompre novos com a Lumi, na praça.");
        }

        void RefreshGuildSeal()
        {
            int r = GuildState.G.rank;
            if (r == guildSealShown) return;
            guildSealShown = r;
            string l = Ranks.Letter(r);
            guildSealText.text = l;
            guildSealText.fontSize = l.Length <= 1 ? 30 : l.Length == 2 ? 24 : l.Length == 3 ? 19 : 15;
        }

        // ================================================================== cerimônia de promoção
        void BuildGuildCeremony()
        {
            var r = UIKit.R(transform, "Cerimonia_Rank");
            UIKit.Place(r, new Vector2(0.5f, 0.56f), MID, Vector2.zero, new Vector2(1920f, 380f));
            guildCereRt = r;
            guildCereCg = r.gameObject.AddComponent<CanvasGroup>();
            guildCereCg.alpha = 0f; guildCereCg.blocksRaycasts = false; guildCereCg.interactable = false;

            guildCereGlow = UIKit.Img(r, "brilho", UIKit.SoftSprite(), new Color(1f, 0.8f, 0.3f, 0.45f));
            UIKit.Place(guildCereGlow.rectTransform, MID, MID, Vector2.zero, new Vector2(1500f, 520f));
            var band = UIKit.Img(r, "faixa_ouro", UIKit.BandSprite(), new Color(1f, 0.78f, 0.25f, 0.95f));
            guildCereBandRt = UIKit.Place(band.rectTransform, MID, MID, Vector2.zero, new Vector2(1900f, 230f));
            var inner = UIKit.Img(guildCereBandRt, "faixa_escura", UIKit.BandSprite(), new Color(0.14f, 0.09f, 0.02f, 0.82f));
            UIKit.Stretch(inner.rectTransform, 40f, 40f, 12f, 12f);

            guildCereRing = UIKit.Img(r, "insignia", U.CircleSprite(), UIKit.Gold);
            UIKit.Place(guildCereRing.rectTransform, MID, MID, new Vector2(-470f, 0f), new Vector2(250f, 250f));
            var ci = UIKit.Img(guildCereRing.rectTransform, "in", U.CircleSprite(), new Color(0.08f, 0.06f, 0.03f, 1f));
            UIKit.Stretch(ci.rectTransform, 10f);
            guildCereLetter = UIKit.Txt(ci.rectTransform, "letra", "", 110, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Stretch(guildCereLetter.rectTransform);
            guildCereLetter.horizontalOverflow = HorizontalWrapMode.Overflow;

            guildCereTitle = UIKit.Txt(r, "titulo", "PROMOVIDO!", 76, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UIKit.Place(guildCereTitle.rectTransform, MID, ML, new Vector2(-300f, 44f), new Vector2(1000f, 96f));
            guildCereTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            guildCereSub = UIKit.Txt(r, "sub", "", 32, Color.white, TextAnchor.MiddleLeft, FontStyle.Italic, true);
            UIKit.Place(guildCereSub.rectTransform, MID, ML, new Vector2(-300f, -22f), new Vector2(1000f, 44f));
            guildCereSub.horizontalOverflow = HorizontalWrapMode.Overflow;
            guildCereRewards = UIKit.Txt(r, "recompensas", "", 20, UIKit.TextCol, TextAnchor.MiddleLeft);
            UIKit.Place(guildCereRewards.rectTransform, MID, ML, new Vector2(-300f, -70f), new Vector2(1000f, 30f));
            guildCereRewards.horizontalOverflow = HorizontalWrapMode.Overflow;
            r.gameObject.SetActive(false);
        }

        /// <summary>Faixa dourada grande no centro anunciando o novo rank.</summary>
        public void RankCeremony(int rank)
        {
            if (guildCereRt == null) return;
            var d = Ranks.Def(rank);
            guildCereRank = rank;
            guildCereLetter.text = d.name;
            guildCereLetter.fontSize = d.name.Length <= 1 ? 120 : d.name.Length == 2 ? 92 : d.name.Length == 3 ? 72 : 56;
            guildCereTitle.text = "RANK " + d.name + "!";
            guildCereSub.text = "Promovido pela Guilda  ·  “" + d.title + "”";
            guildCereRewards.text = "Recompensas: " + Ranks.RewardText(rank);
            guildCereRt.SetAsLastSibling();
            guildCereRt.gameObject.SetActive(true);
            guildCereT = 0f;
        }

        void UpdateGuildCeremony(float udt)
        {
            if (guildCereT < 0f) return;
            const float Dur = 4.6f;
            guildCereT += udt;
            float t = guildCereT;
            float a = t < 0.3f ? t / 0.3f : t > Dur - 0.7f ? Mathf.Clamp01((Dur - t) / 0.7f) : 1f;
            guildCereCg.alpha = a;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.45f), 3f);
            guildCereBandRt.localScale = new Vector3(Mathf.Lerp(0.05f, 1f, e), 1f, 1f);
            float pop = Mathf.Clamp01((t - 0.15f) / 0.35f);
            guildCereRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, 1f - Mathf.Pow(1f - pop, 3f));
            guildCereTitle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, e);
            Color c = Ranks.AnimColor(guildCereRank, Time.unscaledTime);
            guildCereRing.color = c;
            guildCereLetter.color = Color.Lerp(c, Color.white, 0.3f);
            guildCereGlow.color = new Color(c.r, c.g, c.b, 0.35f + 0.15f * Mathf.Sin(t * 6f));
            if (t >= Dur)
            {
                guildCereT = -1f;
                guildCereRt.gameObject.SetActive(false);
            }
        }

        // ================================================================== helpers visuais
        static int GHash(string s)
        {
            int h = 7;
            if (s != null) foreach (char ch in s) h = h * 31 + ch;
            return h;
        }

        Text GInk(RectTransform p, string s, int size, Color c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sz,
                  TextAnchor a = TextAnchor.MiddleLeft, FontStyle st = FontStyle.Normal)
        {
            var t = Label(p, s, size, c, anchor, pivot, pos, sz, a, st);
            var sh = t.GetComponent<Shadow>();
            if (sh != null) sh.effectColor = new Color(1f, 1f, 1f, 0.22f);
            return t;
        }

        void GPin(RectTransform card, Color c)
        {
            var sh = UIKit.Img(card, "sombra_pino", U.CircleSprite(), new Color(0f, 0f, 0f, 0.35f));
            UIKit.Place(sh.rectTransform, TC, MID, new Vector2(4f, -19f), new Vector2(26f, 26f));
            var pin = UIKit.Img(card, "pino", U.CircleSprite(), c);
            UIKit.Place(pin.rectTransform, TC, MID, new Vector2(0f, -14f), new Vector2(26f, 26f));
            var hl = UIKit.Img(pin.rectTransform, "luz", U.CircleSprite(), new Color(1f, 1f, 1f, 0.65f));
            UIKit.Place(hl.rectTransform, MID, MID, new Vector2(-4f, 4f), new Vector2(9f, 9f));
        }

        /// <summary>Papel pregado (com sombra, leve rotação estável e alfinete). Devolve o papel.</summary>
        RectTransform GPaper(RectTransform parent, string id, Color paper, Color pin, float maxTilt)
        {
            var holder = UIKit.R(parent, "papel_" + id);
            var shadow = UIKit.Round(holder, "sombra", new Color(0f, 0f, 0f, 0.32f), 2.5f);
            UIKit.Stretch(shadow.rectTransform, 7f, -7f, 7f, -7f);
            var pp = UIKit.Round(holder, "papel", paper, 2.5f, true);
            UIKit.Stretch(pp.rectTransform);
            float tilt = ((GHash(id) & 1023) / 1023f - 0.5f) * 2f * maxTilt;
            holder.localRotation = Quaternion.Euler(0f, 0f, tilt);
            GPin(pp.rectTransform, pin);
            return pp.rectTransform;
        }

        void GBar(RectTransform p, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float frac, Color fillCol, Color bgCol)
        {
            var bg = UIKit.Round(p, "barra", bgCol, 3f);
            UIKit.Place(bg.rectTransform, anchor, pivot, pos, size);
            var area = UIKit.R(bg.rectTransform, "area");
            UIKit.Stretch(area, 2f);
            var fill = UIKit.Round(area, "cheio", fillCol, 3f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            fill.enabled = frac > 0.001f;
        }

        /// <summary>Insígnia redonda com a letra do rank (cores animadas para SSS e DEUS).</summary>
        RectTransform GRankBadge(RectTransform p, int rank, Vector2 anchor, Vector2 pivot, Vector2 pos, float size)
        {
            string l = Ranks.Letter(rank);
            var holder = UIKit.R(p, "insignia_" + l);
            UIKit.Place(holder, anchor, pivot, pos, new Vector2(size, size));
            var glow = UIKit.Img(holder, "brilho", UIKit.SoftSprite(), Color.white);
            UIKit.Place(glow.rectTransform, MID, MID, Vector2.zero, new Vector2(size * 1.55f, size * 1.55f));
            var ring = UIKit.Img(holder, "aro", U.CircleSprite(), Color.white);
            UIKit.Stretch(ring.rectTransform);
            var inner = UIKit.Img(ring.rectTransform, "in", U.CircleSprite(), new Color(0.07f, 0.06f, 0.08f, 1f));
            UIKit.Stretch(inner.rectTransform, Mathf.Max(3f, size * 0.05f));
            var inRing = UIKit.Img(inner.rectTransform, "aro2", U.CircleSprite(), Color.white);
            UIKit.Stretch(inRing.rectTransform, size * 0.08f);
            var inner2 = UIKit.Img(inRing.rectTransform, "in2", U.CircleSprite(), new Color(0.09f, 0.08f, 0.1f, 1f));
            UIKit.Stretch(inner2.rectTransform, Mathf.Max(1.5f, size * 0.012f));
            int fs = Mathf.Max(10, Mathf.RoundToInt(size * (l.Length <= 1 ? 0.5f : l.Length == 2 ? 0.38f : l.Length == 3 ? 0.3f : 0.23f)));
            var t = UIKit.Txt(inner2.rectTransform, "letra", l, fs, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Stretch(t.rectTransform);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var gl = holder.gameObject.AddComponent<GuildRankGlow>();
            gl.rank = rank;
            gl.targets = new Graphic[] { ring, glow, inRing, t };
            gl.alphas = new float[] { 1f, 0.35f, 0.3f, 1f };
            gl.whiten = new float[] { 0f, 0f, 0f, 0.3f };
            return holder;
        }

        Image GDot(RectTransform p, Color c, float x, float y, float w, float h, float u, float rot = 0f, bool box = false)
        {
            var im = UIKit.Img(p, "p", box ? U.WhiteSprite() : U.CircleSprite(), c);
            UIKit.Place(im.rectTransform, MID, MID, new Vector2(x * u, y * u), new Vector2(w * u, h * u));
            if (rot != 0f) im.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
            return im;
        }

        /// <summary>Retrato fofo do pet feito de círculos (sem modelo 3D).</summary>
        RectTransform GPetPortrait(RectTransform p, PetDef d, Vector2 anchor, Vector2 pivot, Vector2 pos, float size, bool evolved)
        {
            var bg = UIKit.Img(p, "retrato", U.CircleSprite(), new Color(0.06f, 0.08f, 0.14f, 1f));
            var r = UIKit.Place(bg.rectTransform, anchor, pivot, pos, new Vector2(size, size));
            Color c = U.Hex(d.color), c2 = U.Hex(string.IsNullOrEmpty(d.color2) ? d.color : d.color2), eye = U.Hex(string.IsNullOrEmpty(d.eyes) ? "7af0ff" : d.eyes);
            Color pink = U.Hex("ff8fb0"), dark = new Color(0.06f, 0.05f, 0.08f, 1f), white = new Color(0.98f, 0.98f, 1f, 1f);
            var glow = UIKit.Img(r, "brilho", UIKit.SoftSprite(), new Color(c.r, c.g, c.b, evolved ? 0.6f : 0.35f));
            UIKit.Place(glow.rectTransform, MID, MID, Vector2.zero, new Vector2(size * 1.05f, size * 1.05f));
            float u = size / 100f;
            float eyeY = 0f, spread = 12f, es = 16f;
            switch (d.shape)
            {
                case "dragaozinho":
                    GDot(r, c2, -33f, 10f, 32f, 18f, u, 25f, true);
                    GDot(r, c2, 33f, 10f, 32f, 18f, u, -25f, true);
                    GDot(r, c2, -13f, 28f, 9f, 14f, u, 20f, true);
                    GDot(r, c2, 13f, 28f, 9f, 14f, u, -20f, true);
                    GDot(r, c, 0f, -4f, 62f, 58f, u);
                    GDot(r, c2, 0f, -16f, 40f, 28f, u);
                    eyeY = 2f;
                    break;
                case "coruja":
                    GDot(r, c, -18f, 28f, 10f, 20f, u, 25f, true);
                    GDot(r, c, 18f, 28f, 10f, 20f, u, -25f, true);
                    GDot(r, c, -32f, -6f, 16f, 34f, u);
                    GDot(r, c, 32f, -6f, 16f, 34f, u);
                    GDot(r, c, 0f, -2f, 60f, 64f, u);
                    GDot(r, c2, 0f, -12f, 42f, 38f, u);
                    GDot(r, Color.Lerp(c2, white, 0.5f), -12f, 4f, 24f, 24f, u);
                    GDot(r, Color.Lerp(c2, white, 0.5f), 12f, 4f, 24f, 24f, u);
                    eyeY = 4f; es = 18f;
                    break;
                case "gatinho":
                    GDot(r, c, -18f, 22f, 20f, 20f, u, 45f, true);
                    GDot(r, c, 18f, 22f, 20f, 20f, u, 45f, true);
                    GDot(r, pink, -18f, 22f, 10f, 10f, u, 45f, true);
                    GDot(r, pink, 18f, 22f, 10f, 10f, u, 45f, true);
                    GDot(r, c, 0f, -6f, 62f, 54f, u);
                    GDot(r, c2, 0f, -18f, 38f, 26f, u);
                    GDot(r, pink, 0f, -8f, 6f, 4f, u);
                    eyeY = 0f;
                    break;
                case "cogumelo":
                    GDot(r, U.Hex("f3e6cf"), 0f, -18f, 46f, 44f, u);
                    GDot(r, c, 0f, 12f, 82f, 44f, u);
                    GDot(r, white, -20f, 18f, 11f, 8f, u);
                    GDot(r, white, 6f, 24f, 9f, 7f, u);
                    GDot(r, white, 24f, 12f, 10f, 7f, u);
                    GDot(r, white, -6f, 8f, 7f, 5f, u);
                    eyeY = -16f; spread = 10f; es = 13f;
                    break;
                case "fantasminha":
                    GDot(r, c, -18f, -28f, 20f, 20f, u);
                    GDot(r, c, 0f, -31f, 20f, 20f, u);
                    GDot(r, c, 18f, -28f, 20f, 20f, u);
                    GDot(r, c, 0f, 0f, 58f, 64f, u);
                    GDot(r, c, -31f, -2f, 14f, 10f, u);
                    GDot(r, c, 31f, -2f, 14f, 10f, u);
                    eyeY = 4f; spread = 11f;
                    break;
                default: // slime
                    GDot(r, c, 0f, 20f, 18f, 18f, u);
                    GDot(r, c, 0f, -10f, 72f, 52f, u);
                    GDot(r, new Color(1f, 1f, 1f, 0.6f), 20f, 2f, 10f, 6f, u);
                    eyeY = -8f;
                    break;
            }
            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * spread;
                GDot(r, white, x, eyeY, es, es * 1.1f, u);
                GDot(r, eye, x, eyeY - 1f, es * 0.7f, es * 0.78f, u);
                GDot(r, dark, x, eyeY - 1f, es * 0.38f, es * 0.44f, u);
                GDot(r, white, x + es * 0.16f, eyeY + es * 0.2f, es * 0.24f, es * 0.24f, u);
                GDot(r, new Color(pink.r, pink.g, pink.b, 0.85f), k * (spread + es * 0.75f), eyeY - es * 0.65f, es * 0.5f, es * 0.3f, u);
            }
            GDot(r, dark, 0f, eyeY - es * 0.7f, es * 0.25f, es * 0.14f, u);
            if (evolved)
            {
                Color gold = U.Hex("ffd04a");
                float top = d.shape == "cogumelo" ? 36f : d.shape == "coruja" ? 36f : 32f;
                GDot(r, gold, 0f, top, 26f, 6f, u, 0f, true);
                GDot(r, gold, -9f, top + 5f, 7f, 7f, u, 45f, true);
                GDot(r, gold, 0f, top + 7f, 8f, 8f, u, 45f, true);
                GDot(r, gold, 9f, top + 5f, 7f, 7f, u, 45f, true);
            }
            return r;
        }

        // ================================================================== QUADRO DE MISSÕES
        public void QuestBoardWindow()
        {
            GuildData.Load();
            Quests.RefreshDaily();
            var c = OpenModal("quests", "Quadro de Missões da Guilda", new Vector2(1640f, 920f), true);
            modalRebuild = QuestBoardWindow;
            var G = GuildState.G;

            // ---- barra do topo
            var top = UIKit.R(c, "topo");
            UIKit.Place(top, TL, TL, Vector2.zero, new Vector2(1584f, 50f));
            GRankBadge(top, G.rank, ML, ML, new Vector2(0f, 0f), 46f);
            int have, need;
            Ranks.Progress(out have, out need);
            string info = "Rank <b><color=#" + UIKit.ColorHex(Ranks.BaseColor(G.rank)) + ">" + Ranks.Letter(G.rank) + "</color></b> · " + G.title +
                          "    <color=#FFD76A>★ " + have + " PG</color>" + (Ranks.IsMax ? "" : "  <color=#9aa3b5>(Rank " + Ranks.NextDef.name + " em " + need + ")</color>") +
                          "    <color=#9aa3b5>Ativas: " + Quests.ActiveCount() + "/" + Quests.MaxActive + "</color>";
            Label(top, info, 21, UIKit.TextCol, ML, ML, new Vector2(60f, 0f), new Vector2(1000f, 40f));
            float bx = 0f;
            var brank = UIKit.Btn(top, "Ver rank", new Vector2(160f, 44f), RankWindow, UIKit.BtnCol, 19);
            UIKit.Place(UIKit.RT(brank), MR, MR, new Vector2(-bx, 0f), new Vector2(160f, 44f));
            bx += 172f;
            int ready = Quests.ReadyCount();
            if (ready > 0)
            {
                var ball = UIKit.Btn(top, "Entregar tudo (" + ready + ")", new Vector2(250f, 44f), () => { Quests.TurnInAll(); windowDirty = true; }, UIKit.Gold, 19);
                UIKit.Place(UIKit.RT(ball), MR, MR, new Vector2(-bx, 0f), new Vector2(250f, 44f));
            }

            // ---- moldura de madeira + cortiça
            var frame = UIKit.Round(c, "moldura", new Color(0.33f, 0.2f, 0.1f, 1f), 1.2f, true);
            var fr = UIKit.Place(frame.rectTransform, TL, TL, new Vector2(0f, -58f), new Vector2(1584f, 696f));
            var fo = frame.gameObject.AddComponent<Outline>();
            fo.effectColor = new Color(0f, 0f, 0f, 0.6f); fo.effectDistance = new Vector2(2f, -2f); fo.useGraphicAlpha = false;
            var cork = UIKit.Round(fr, "cortica", new Color(0.66f, 0.48f, 0.29f, 1f), 1.6f, true);
            var cr = cork.rectTransform;
            UIKit.Stretch(cr, 14f);
            var rng = new System.Random(7);
            for (int i = 0; i < 90; i++)
            {
                float x = (float)rng.NextDouble() * 1540f, y = -(float)rng.NextDouble() * 652f, sz = 3f + (float)rng.NextDouble() * 8f;
                bool darkSpot = rng.NextDouble() < 0.65;
                var sp = UIKit.Img(cr, "grao", U.CircleSprite(), darkSpot ? new Color(0.35f, 0.22f, 0.1f, 0.35f) : new Color(1f, 0.85f, 0.6f, 0.25f));
                UIKit.Place(sp.rectTransform, TL, MID, new Vector2(x + 8f, y - 8f), new Vector2(sz, sz * (0.6f + (float)rng.NextDouble() * 0.6f)));
            }
            var vign = UIKit.Img(cr, "sombra_borda", UIKit.SoftSprite(), new Color(1f, 0.9f, 0.7f, 0.12f));
            UIKit.Place(vign.rectTransform, MID, MID, Vector2.zero, new Vector2(1400f, 700f));

            // ---- disponíveis
            GInk(cr, "DISPONÍVEIS NO QUADRO", 18, new Color(0.18f, 0.11f, 0.05f, 1f), TL, TL, new Vector2(24f, -10f), new Vector2(600f, 28f), TextAnchor.MiddleLeft, FontStyle.Bold);
            ScrollRect sr;
            var content = UIKit.ScrollArea(cr, "disponiveis", out sr);
            sr.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            UIKit.Place(UIKit.RT(sr), TL, TL, new Vector2(10f, -40f), new Vector2(1010f, 618f));
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 344f);
            grid.spacing = new Vector2(24f, 30f);
            grid.padding = new RectOffset(18, 10, 24, 18);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;
            UIKit.Fit(content.gameObject, false, true);

            var avail = Quests.Available();
            foreach (var d in avail) BoardPaper(content, d, false);
            foreach (var d in Quests.LockedByRank()) if (!Quests.IsActive(d.id)) BoardPaper(content, d, true);
            if (content.childCount == 0)
            {
                var note = GPaper(cr, "vazio", PaperCol, U.Hex("9a9a9a"), 3f);
                UIKit.Place(UIKit.RT(note.parent), TL, TL, new Vector2(300f, -200f), new Vector2(420f, 160f));
                GInk(note, "Nada novo no quadro hoje.\nVolte amanhã para novas diárias!", 20, InkCol, MID, MID, new Vector2(0f, -6f), new Vector2(380f, 90f), TextAnchor.MiddleCenter, FontStyle.Italic);
            }

            // ---- suas missões
            var div = UIKit.Img(cr, "divisoria", U.WhiteSprite(), new Color(0.3f, 0.18f, 0.08f, 0.5f));
            UIKit.Place(div.rectTransform, TL, TL, new Vector2(1028f, -14f), new Vector2(3f, 640f));
            GInk(cr, "SUAS MISSÕES  (" + Quests.ActiveCount() + "/" + Quests.MaxActive + ")", 18, new Color(0.18f, 0.11f, 0.05f, 1f), TL, TL,
                new Vector2(1048f, -10f), new Vector2(480f, 28f), TextAnchor.MiddleLeft, FontStyle.Bold);
            ScrollRect sr2;
            var act = UIKit.ScrollArea(cr, "ativas", out sr2);
            sr2.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            UIKit.Place(UIKit.RT(sr2), TL, TL, new Vector2(1036f, -40f), new Vector2(518f, 618f));
            var vl = UIKit.VLayout(act.gameObject, 20f, new RectOffset(10, 14, 20, 14));
            vl.childControlHeight = false;
            UIKit.Fit(act.gameObject, false, true);
            if (G.active.Count == 0)
            {
                var t = UIKit.Txt(act, "vazio", "Nenhuma missão aceita.\nEscolha um papel no quadro e clique em <b>Aceitar</b>.", 18, new Color(0.2f, 0.12f, 0.05f, 1f), TextAnchor.MiddleCenter, FontStyle.Italic);
                t.rectTransform.sizeDelta = new Vector2(0f, 120f);
                var tsh = t.GetComponent<Shadow>();
                if (tsh != null) tsh.effectColor = new Color(1f, 1f, 1f, 0.2f);
            }
            else
            {
                var ids = new List<QuestProgress>(G.active);
                foreach (var a in ids) ActivePaper(act, a);
            }
        }

        void BoardPaper(RectTransform parent, QuestDef d, bool locked)
        {
            bool chain = d.type == "chain";
            bool promo = d.type == "promo" || chain;   // sequência também usa papel dourado
            Color tc = Quests.TypeColor(d);
            var p = GPaper(parent, d.id, locked ? PaperLocked : promo ? PaperGold : PaperCol, locked ? U.Hex("777777") : tc, 3f);
            var best = QuestGrading.Best(d.id);
            if (best != null && !locked)
            {
                var bg = GInk(p, QuestGrading.Letter(best.grade), 22, Color.Lerp(QuestGrading.GradeColor(best.grade), Color.black, 0.2f), TR, TR,
                    new Vector2(-14f, -20f), new Vector2(50f, 30f), TextAnchor.MiddleRight, FontStyle.Bold);
                bg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            }
            if (promo)
            {
                var o = p.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(1f, 0.75f, 0.2f, 0.9f); o.effectDistance = new Vector2(3f, -3f); o.useGraphicAlpha = false;
            }
            GInk(p, Quests.TypeName(d), 13, locked ? InkDim : Color.Lerp(tc, Color.black, 0.2f), TC, TC, new Vector2(0f, -32f), new Vector2(270f, 18f), TextAnchor.MiddleCenter, FontStyle.Bold);
            GInk(p, d.title, 21, InkCol, TC, TC, new Vector2(0f, -52f), new Vector2(272f, 52f), TextAnchor.UpperCenter, FontStyle.Bold);
            var line = UIKit.Img(p, "linha", UIKit.BandSprite(), new Color(InkCol.r, InkCol.g, InkCol.b, 0.35f));
            UIKit.Place(line.rectTransform, TC, TC, new Vector2(0f, -108f), new Vector2(240f, 2f));
            var desc = GInk(p, d.desc, 14, InkDim, TC, TC, new Vector2(0f, -116f), new Vector2(266f, 70f), TextAnchor.UpperCenter, FontStyle.Italic);
            desc.verticalOverflow = VerticalWrapMode.Truncate;
            GInk(p, "◎ " + Quests.GoalText(d), 15, InkCol, TC, TC, new Vector2(0f, -190f), new Vector2(270f, 42f), TextAnchor.UpperCenter, FontStyle.Bold);
            GInk(p, "RECOMPENSAS", 12, InkDim, TC, TC, new Vector2(0f, -232f), new Vector2(270f, 16f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var rw = GInk(p, Quests.RewardText(d), 14, InkCol, TC, TC, new Vector2(0f, -250f), new Vector2(276f, 40f), TextAnchor.UpperCenter);
            rw.verticalOverflow = VerticalWrapMode.Truncate;

            Button b;
            if (locked)
            {
                b = UIKit.Btn(p, "Requer Rank " + Ranks.Letter(d.minRank), new Vector2(220f, 42f), null);
                UIKit.Disable(b);
            }
            else
            {
                string id = d.id;
                b = UIKit.Btn(p, chain ? "Aceitar a sequência" : promo ? "Aceitar a prova" : "Aceitar", new Vector2(220f, 42f), () =>
                {
                    if (Quests.Accept(id)) Sfx.Play("ui_open");
                    else Sfx.Play("ui_error");
                    windowDirty = true;
                }, promo ? UIKit.Gold : new Color(0.36f, 0.24f, 0.12f, 1f), 19, promo ? UIKit.GoldDark : new Color(1f, 0.95f, 0.85f, 1f));
                if (!promo && Quests.ActiveCount() >= Quests.MaxActive) UIKit.Disable(b);
            }
            UIKit.Place(UIKit.RT(b), BC, BC, new Vector2(0f, 14f), new Vector2(220f, 42f));
            string tipTitle = d.title, tipBody = d.desc + "\n\nObjetivo: " + Quests.GoalText(d) + "\nRecompensas: " + Quests.RewardText(d, false) +
                "\nMeta de tempo para nota alta: " + QuestGrading.FormatTime(QuestGrading.TargetTime(d)) +
                (best != null ? "\nSua melhor nota: " + QuestGrading.Letter(best.grade) : "");
            UITip.Add(p.gameObject, () => tipTitle, () => tipBody);
        }

        void ActivePaper(RectTransform parent, QuestProgress a)
        {
            var d = Quests.Def(a.id);
            if (d == null) return;
            bool promo = d.type == "promo" || d.type == "chain";
            Color tc = Quests.TypeColor(d);
            var p = GPaper(parent, "ativa_" + d.id, promo ? PaperGold : PaperCol, tc, 1.5f);
            UIKit.RT(p.parent).sizeDelta = new Vector2(0f, 176f);
            GInk(p, Quests.TypeName(d), 12, Color.Lerp(tc, Color.black, 0.2f), TL, TL, new Vector2(20f, -26f), new Vector2(300f, 16f), TextAnchor.MiddleLeft, FontStyle.Bold);
            GInk(p, d.title, 20, InkCol, TL, TL, new Vector2(20f, -42f), new Vector2(310f, 26f), TextAnchor.MiddleLeft, FontStyle.Bold);
            GInk(p, Quests.GoalText(d), 15, InkDim, TL, TL, new Vector2(20f, -70f), new Vector2(310f, 22f));
            float frac = a.done ? 1f : a.progress / (float)Mathf.Max(1, d.count);
            GBar(p, TL, TL, new Vector2(20f, -98f), new Vector2(250f, 18f), frac, a.done ? U.Hex("5fbf5a") : tc, new Color(0.3f, 0.22f, 0.14f, 0.45f));
            GInk(p, a.done ? "✓ Concluída!" : a.progress + " / " + d.count, 15, a.done ? U.Hex("2f8a2a") : InkCol, TL, TL, new Vector2(280f, -96f), new Vector2(120f, 22f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var rw = GInk(p, Quests.RewardText(d), 13, InkCol, TL, TL, new Vector2(20f, -122f), new Vector2(330f, 20f), TextAnchor.UpperLeft);
            rw.verticalOverflow = VerticalWrapMode.Truncate;
            if (a.v > 0)
            {
                int gr = QuestGrading.Grade(a, d);
                GInk(p, "Tempo " + QuestGrading.FormatTime(a.time) + " / " + QuestGrading.FormatTime(QuestGrading.TargetTime(d)) + "   ·   dano " + Mathf.RoundToInt(a.dmg) +
                        "   ·   nota " + (a.done ? "" : "atual ") + "<b><color=#" + QuestGrading.GradeHex(gr) + ">" + QuestGrading.Letter(gr) + "</color></b>",
                    13, InkDim, TL, TL, new Vector2(20f, -146f), new Vector2(340f, 20f));
            }

            string id = d.id;
            if (a.done)
            {
                var bt = UIKit.Btn(p, "Entregar", new Vector2(130f, 46f), () => { Quests.TurnIn(id); windowDirty = true; }, UIKit.Gold, 20);
                UIKit.Place(UIKit.RT(bt), MR, MR, new Vector2(-14f, 24f), new Vector2(130f, 46f));
            }
            var ba = UIKit.Btn(p, "Abandonar", new Vector2(130f, 36f), () => { Quests.Abandon(id); windowDirty = true; }, UIKit.DangerCol, 16);
            UIKit.Place(UIKit.RT(ba), MR, MR, new Vector2(-14f, a.done ? -28f : 0f), new Vector2(130f, 36f));
        }

        // ================================================================== RANK
        public void RankWindow()
        {
            GuildData.Load();
            var c = OpenModal("rank", "Rank da Guilda", new Vector2(1480f, 880f), true);
            modalRebuild = RankWindow;
            var G = GuildState.G;
            int rank = G.rank;

            // ---- esquerda: insígnia, progresso e prova
            var left = UIKit.Round(c, "atual", UIKit.CardCol, 1f, true);
            var lr = UIKit.Place(left.rectTransform, TL, TL, Vector2.zero, new Vector2(560f, 714f));
            GRankBadge(lr, rank, TC, TC, new Vector2(0f, -40f), 250f);
            Label(lr, "Rank " + Ranks.Letter(rank), 40, Color.white, TC, TC, new Vector2(0f, -318f), new Vector2(520f, 50f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Label(lr, "“" + G.title + "”", 24, UIKit.Gold, TC, TC, new Vector2(0f, -366f), new Vector2(520f, 32f), TextAnchor.MiddleCenter, FontStyle.Italic);
            var dsc = Ranks.CurrentDef.desc;
            if (!string.IsNullOrEmpty(dsc)) Label(lr, dsc, 16, UIKit.DimText, TC, TC, new Vector2(0f, -400f), new Vector2(500f, 24f), TextAnchor.MiddleCenter);

            int have, need;
            float frac = Ranks.Progress(out have, out need);
            Label(lr, "Pontos de Guilda: <b><color=#FFD76A>" + have + "</color></b>    ·    Missões entregues: " + G.totalQuests, 18, UIKit.TextCol, TC, TC,
                new Vector2(0f, -436f), new Vector2(520f, 26f), TextAnchor.MiddleCenter);
            GBar(lr, TC, TC, new Vector2(0f, -470f), new Vector2(460f, 26f), frac, Ranks.IsMax ? UIKit.Gold : Ranks.BaseColor(Mathf.Min(rank + 1, Ranks.Count - 1)), new Color(0.02f, 0.03f, 0.06f, 0.9f));
            Label(lr, Ranks.IsMax ? "Rank máximo alcançado!" : have + " / " + need + " PG para o Rank " + Ranks.NextDef.name, 15, UIKit.TextCol, TC, TC,
                new Vector2(0f, -470f), new Vector2(460f, 26f), TextAnchor.MiddleCenter, FontStyle.Bold);

            // caixa da prova
            var box = UIKit.Round(lr, "prova", new Color(0f, 0f, 0f, 0.25f), 1.4f);
            var br = UIKit.Place(box.rectTransform, TC, TC, new Vector2(0f, -514f), new Vector2(500f, 180f));
            if (Ranks.IsMax)
            {
                Label(br, "Você está no topo da Guilda.\nNem os deuses têm rank acima do seu.", 18, UIKit.Gold, MID, MID, Vector2.zero, new Vector2(460f, 80f), TextAnchor.MiddleCenter, FontStyle.Italic);
            }
            else
            {
                string pid = Quests.PromoPrefix + (rank + 1);
                var pd = Quests.Def(pid);
                var pa = Quests.Active(pid);
                Label(br, "PROVA DE PROMOÇÃO — RANK " + Ranks.NextDef.name, 15, U.Hex("f2b632"), TC, TC, new Vector2(0f, -10f), new Vector2(470f, 22f), TextAnchor.MiddleCenter, FontStyle.Bold);
                if (pd != null) Label(br, pd.desc + "\n<color=#c8cdd8>" + Quests.GoalText(pd) + "</color>", 16, UIKit.TextCol, TC, TC, new Vector2(0f, -36f), new Vector2(470f, 60f), TextAnchor.UpperCenter);
                if (!Ranks.TrialReady)
                {
                    Label(br, "Junte mais " + (need - have) + " PG em missões para liberar a prova.", 16, UIKit.DimText, BC, BC, new Vector2(0f, 18f), new Vector2(470f, 40f), TextAnchor.MiddleCenter, FontStyle.Italic);
                }
                else if (pa == null)
                {
                    var b = UIKit.Btn(br, "Aceitar a prova", new Vector2(260f, 48f), () => { Quests.Accept(pid); windowDirty = true; }, UIKit.Gold, 20);
                    UIKit.Place(UIKit.RT(b), BC, BC, new Vector2(0f, 14f), new Vector2(260f, 48f));
                }
                else if (pa.done)
                {
                    var b = UIKit.Btn(br, "Entregar a prova!", new Vector2(260f, 48f), () => { Quests.TurnIn(pid); windowDirty = true; }, UIKit.Gold, 20);
                    UIKit.Place(UIKit.RT(b), BC, BC, new Vector2(0f, 14f), new Vector2(260f, 48f));
                }
                else
                {
                    GBar(br, BC, BC, new Vector2(0f, 30f), new Vector2(360f, 20f), pa.progress / (float)Mathf.Max(1, pd != null ? pd.count : 1), U.Hex("f2b632"), new Color(0.02f, 0.03f, 0.06f, 0.9f));
                    Label(br, "Em andamento: " + pa.progress + " / " + (pd != null ? pd.count : 1), 15, UIKit.TextCol, BC, BC, new Vector2(0f, 30f), new Vector2(360f, 20f), TextAnchor.MiddleCenter, FontStyle.Bold);
                }
            }

            // ---- direita: todos os ranks e recompensas
            ScrollRect sr;
            var list = UIKit.ScrollArea(c, "ranks", out sr);
            UIKit.Place(UIKit.RT(sr), TL, TL, new Vector2(580f, 0f), new Vector2(844f, 714f));
            var vl = UIKit.VLayout(list.gameObject, 8f, new RectOffset(6, 6, 6, 6));
            vl.childControlHeight = false;
            UIKit.Fit(list.gameObject, false, true);
            for (int i = 0; i < Ranks.Count; i++)
            {
                var d = Ranks.Def(i);
                bool cur = i == rank, got = i <= rank;
                var row = UIKit.Round(list, "rank_" + d.id, cur ? new Color(0.16f, 0.14f, 0.08f, 0.98f) : UIKit.RowCol, 1.2f, true);
                var rr = row.rectTransform;
                rr.sizeDelta = new Vector2(0f, 96f);
                if (cur)
                {
                    var o = row.gameObject.AddComponent<Outline>();
                    o.effectColor = new Color(1f, 0.84f, 0.42f, 0.7f); o.effectDistance = new Vector2(2f, -2f); o.useGraphicAlpha = false;
                }
                GRankBadge(rr, i, ML, ML, new Vector2(14f, 0f), 70f);
                Label(rr, "Rank " + d.name + "  <color=#9aa3b5><size=16>— " + d.title + "</size></color>", 22, got ? Color.white : UIKit.DimText, TL, TL,
                    new Vector2(100f, -10f), new Vector2(520f, 28f), TextAnchor.MiddleLeft, FontStyle.Bold);
                Label(rr, i == 0 ? "Rank inicial" : "A partir de " + d.points + " PG  ·  " + (d.trial != null && !string.IsNullOrEmpty(d.trial.title) ? d.trial.title : "Prova de Promoção"), 14, UIKit.DimText, TL, TL,
                    new Vector2(100f, -40f), new Vector2(560f, 20f));
                var rw = Label(rr, Ranks.RewardText(i), 15, got ? UIKit.TextCol : new Color(0.75f, 0.77f, 0.82f), TL, TL, new Vector2(100f, -62f), new Vector2(580f, 24f));
                rw.verticalOverflow = VerticalWrapMode.Truncate;
                string st = cur ? "ATUAL" : got ? "✓ Recebido" : "Bloqueado";
                Color sc = cur ? UIKit.Gold : got ? new Color(0.55f, 0.9f, 0.55f) : UIKit.DimText;
                Label(rr, st, 17, sc, MR, MR, new Vector2(-16f, 0f), new Vector2(130f, 30f), TextAnchor.MiddleRight, FontStyle.Bold);
            }
        }

        // ================================================================== LOJA DE PETS
        public void PetShopWindow()
        {
            GuildData.Load();
            var c = OpenModal("petshop", "Loja de Pets da Lumi", new Vector2(1500f, 900f), true);
            modalRebuild = PetShopWindow;

            var top = UIKit.R(c, "topo");
            UIKit.Place(top, TL, TL, Vector2.zero, new Vector2(1444f, 50f));
            var ci = UIKit.Img(top, "moeda", U.Icon(2, 8), Color.white);
            UIKit.Place(ci.rectTransform, ML, ML, Vector2.zero, new Vector2(34f, 34f));
            Label(top, "Suas moedas: <b><color=#FFD76A>" + GameState.P.coins + "</color></b>     <color=#9aa3b5>Pets buscam o loot do chão, ganham nível e lutam ao seu lado.</color>", 20, UIKit.TextCol, ML, ML,
                new Vector2(44f, 0f), new Vector2(1100f, 40f));
            var bm = UIKit.Btn(top, "Meus pets", new Vector2(200f, 46f), PetsWindow, UIKit.BtnCol, 20);
            UIKit.Place(UIKit.RT(bm), MR, MR, Vector2.zero, new Vector2(200f, 46f));

            var gridRt = UIKit.R(c, "grade");
            UIKit.Place(gridRt, TL, TL, new Vector2(0f, -62f), new Vector2(1444f, 672f));
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(460f, 324f);
            grid.spacing = new Vector2(32f, 22f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;

            foreach (var d in GuildData.PetOrder) PetShopCard(gridRt, d);
            if (GuildData.PetOrder.Count == 0)
                Label(c, "A Lumi ainda está arrumando a loja (faltando pets.json).", 22, UIKit.DimText, MID, MID, Vector2.zero, new Vector2(900f, 40f), TextAnchor.MiddleCenter);
        }

        void PetShopCard(RectTransform parent, PetDef d)
        {
            bool owned = Pets.Owns(d.id);
            Color pc = U.Hex(d.color);
            var card = UIKit.Round(parent, "pet_" + d.id, UIKit.CardCol, 1f, true);
            var cr = card.rectTransform;
            var o = card.gameObject.AddComponent<Outline>();
            o.useGraphicAlpha = false; o.effectDistance = new Vector2(2f, -2f);
            o.effectColor = new Color(pc.r, pc.g, pc.b, owned ? 0.25f : 0.55f);
            card.gameObject.AddComponent<UIHover>().scale = 1.02f;

            GPetPortrait(cr, d, TL, TL, new Vector2(16f, -16f), 150f, false);
            Label(cr, d.name, 28, pc, TL, TL, new Vector2(184f, -18f), new Vector2(260f, 34f), TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(cr, d.species, 16, UIKit.DimText, TL, TL, new Vector2(184f, -52f), new Vector2(260f, 22f), TextAnchor.MiddleLeft, FontStyle.Italic);
            var desc = Label(cr, d.desc, 15, UIKit.TextCol, TL, TL, new Vector2(184f, -80f), new Vector2(262f, 86f), TextAnchor.UpperLeft);
            desc.verticalOverflow = VerticalWrapMode.Truncate;
            Label(cr, "Velocidade <b>" + d.speed.ToString("0.0") + "</b>   ·   Coleta <b>" + d.radius.ToString("0") + " m</b>   ·   Dano <b>" + d.dmg.ToString("0.0#") + "</b>/nível",
                15, UIKit.Gold, TL, TL, new Vector2(16f, -178f), new Vector2(430f, 22f));
            var sp = Label(cr, "<color=#9aa3b5>Nv 3:</color> ataca inimigos   <color=#9aa3b5>Nv 6:</color> <b>" + d.special + "</b> — " + d.specialDesc +
                "   <color=#9aa3b5>Nv 10:</color> evolui!", 14, UIKit.TextCol, TL, TL, new Vector2(16f, -204f), new Vector2(430f, 56f), TextAnchor.UpperLeft);
            sp.verticalOverflow = VerticalWrapMode.Truncate;

            bool afford = GameState.P.coins >= d.price;
            var coin = UIKit.Img(cr, "moeda", U.Icon(2, 8), Color.white);
            UIKit.Place(coin.rectTransform, BL, BL, new Vector2(18f, 20f), new Vector2(30f, 30f));
            Label(cr, d.price.ToString(), 26, afford || owned ? UIKit.Gold : new Color(1f, 0.45f, 0.4f), BL, BL, new Vector2(54f, 18f), new Vector2(140f, 34f), TextAnchor.MiddleLeft, FontStyle.Bold);
            string id = d.id;
            Button b;
            if (owned)
            {
                b = UIKit.Btn(cr, "✓ Já é seu", new Vector2(190f, 50f), null, UIKit.BtnCol, 20);
                UIKit.Disable(b);
            }
            else
            {
                b = UIKit.Btn(cr, "Adotar", new Vector2(190f, 50f), () =>
                {
                    if (Pets.Buy(id)) Sfx.Play("coin"); else Sfx.Play("ui_error");
                    windowDirty = true;
                }, UIKit.Gold, 22);
                if (!afford) UIKit.Disable(b);
            }
            UIKit.Place(UIKit.RT(b), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(190f, 50f));
        }

        // ================================================================== MEUS PETS
        public void PetsWindow()
        {
            GuildData.Load();
            var c = OpenModal("pets", "Meus Pets", new Vector2(1320f, 860f), true);
            modalRebuild = PetsWindow;
            var G = GuildState.G;

            if (G.pets.Count == 0)
            {
                var pd = GuildData.PetOrder.Count > 0 ? GuildData.PetOrder[0] : null;
                if (pd != null) GPetPortrait(c, pd, TC, TC, new Vector2(0f, -60f), 200f, false);
                Label(c, "Você ainda não tem nenhum pet.", 28, Color.white, TC, TC, new Vector2(0f, -300f), new Vector2(1000f, 40f), TextAnchor.MiddleCenter, FontStyle.Bold);
                Label(c, "Visite a <color=#ffb0e0><b>Lumi</b></color>, a criadora de pets da Praça de Aster. Eles buscam o loot do chão e lutam ao seu lado!", 19, UIKit.DimText, TC, TC,
                    new Vector2(0f, -346f), new Vector2(1000f, 60f), TextAnchor.MiddleCenter);
                var b = UIKit.Btn(c, "Ver a loja", new Vector2(260f, 52f), PetShopWindow, UIKit.Gold, 22);
                UIKit.Place(UIKit.RT(b), TC, TC, new Vector2(0f, -430f), new Vector2(260f, 52f));
                return;
            }

            Label(c, "Equipe um pet para ele te acompanhar. Ele ganha XP coletando loot e quando você derrota inimigos.", 17, UIKit.DimText, TL, TL,
                Vector2.zero, new Vector2(1264f, 26f));
            ScrollRect sr;
            var list = UIKit.ScrollArea(c, "lista", out sr);
            UIKit.Place(UIKit.RT(sr), TL, TL, new Vector2(0f, -36f), new Vector2(1264f, 658f));
            var vl = UIKit.VLayout(list.gameObject, 10f, new RectOffset(4, 4, 4, 4));
            vl.childControlHeight = false;
            UIKit.Fit(list.gameObject, false, true);
            foreach (var op in G.pets) PetRow(list, op);
        }

        void PetRow(RectTransform parent, OwnedPet op)
        {
            var d = GuildData.Pet(op.petId);
            if (d == null) return;
            bool active = GuildState.G.activePet == op.uid;
            bool evo = op.level >= Pets.MaxLevel;
            Color pc = U.Hex(d.color);
            var row = UIKit.Round(parent, "pet_" + op.uid, active ? new Color(0.13f, 0.16f, 0.22f, 0.98f) : UIKit.RowCol, 1.2f, true);
            var rr = row.rectTransform;
            rr.sizeDelta = new Vector2(0f, 162f);
            if (active)
            {
                var o = row.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(pc.r, pc.g, pc.b, 0.8f); o.effectDistance = new Vector2(2f, -2f); o.useGraphicAlpha = false;
            }
            GPetPortrait(rr, d, ML, ML, new Vector2(14f, 0f), 130f, evo);
            Label(rr, op.name + (evo ? "  <color=#FFD76A>★</color>" : ""), 28, pc, TL, TL, new Vector2(162f, -12f), new Vector2(420f, 34f), TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(rr, d.species + (active ? "   <color=#8fe08a>● te acompanhando</color>" : ""), 16, UIKit.DimText, TL, TL, new Vector2(162f, -46f), new Vector2(420f, 22f), TextAnchor.MiddleLeft, FontStyle.Italic);
            int need = Pets.XpToNext(op.level);
            Label(rr, "Nível <b>" + op.level + "</b>/" + Pets.MaxLevel, 19, Color.white, TL, TL, new Vector2(162f, -76f), new Vector2(140f, 26f), TextAnchor.MiddleLeft);
            GBar(rr, TL, TL, new Vector2(290f, -79f), new Vector2(260f, 20f), evo ? 1f : op.xp / (float)Mathf.Max(1, need), evo ? UIKit.Gold : UIKit.XpCol, new Color(0.02f, 0.03f, 0.06f, 0.9f));
            Label(rr, evo ? "MÁX — evoluído!" : op.xp + " / " + need + " XP", 13, Color.white, TL, TL, new Vector2(290f, -79f), new Vector2(260f, 20f), TextAnchor.MiddleCenter, FontStyle.Bold);
            string perks =
                (op.level >= Pets.AttackLevel ? "<color=#8fe08a>✓</color>" : "<color=#9aa3b5>Nv 3</color>") + " Ataque (" + Pets.Damage(op) + " de dano)    " +
                (op.level >= Pets.SpecialLevel ? "<color=#8fe08a>✓</color>" : "<color=#9aa3b5>Nv 6</color>") + " " + d.special + "    " +
                (evo ? "<color=#8fe08a>✓</color>" : "<color=#9aa3b5>Nv 10</color>") + " Evolução";
            Label(rr, perks, 15, UIKit.TextCol, TL, TL, new Vector2(162f, -108f), new Vector2(560f, 22f));
            Label(rr, "Coleta loot num raio de " + d.radius.ToString("0") + " m.", 14, UIKit.DimText, TL, TL, new Vector2(162f, -132f), new Vector2(560f, 20f));

            // renomear
            var input = UIKit.MakeInput(rr, "nome_" + op.uid, "Novo nome...", 16);
            UIKit.Place(UIKit.RT(input), MR, MR, new Vector2(-376f, 22f), new Vector2(240f, 46f));
            int uid = op.uid;
            var brn = UIKit.Btn(rr, "Renomear", new Vector2(150f, 46f), () =>
            {
                Pets.Rename(uid, input.text);
                windowDirty = true;
            }, UIKit.BtnCol, 18);
            UIKit.Place(UIKit.RT(brn), MR, MR, new Vector2(-218f, 22f), new Vector2(150f, 46f));

            Button be;
            if (active)
                be = UIKit.Btn(rr, "Guardar", new Vector2(180f, 52f), () => { Pets.Equip(-1); windowDirty = true; }, UIKit.BtnCol, 20);
            else
                be = UIKit.Btn(rr, "Equipar", new Vector2(180f, 52f), () => { Pets.Equip(uid); windowDirty = true; }, UIKit.Gold, 22);
            UIKit.Place(UIKit.RT(be), MR, MR, new Vector2(-18f, 22f), new Vector2(180f, 52f));
        }
    }

    // ======================================================================
    /// <summary>Loop da Guilda preso ao HUD: salva pendências, vira o dia das diárias, detecta habilidades, anima o HUD.</summary>
    public class GuildHud : MonoBehaviour
    {
        public HUD hud;

        void OnEnable() { GuildState.Changed += OnChanged; }
        void OnDisable() { GuildState.Changed -= OnChanged; }
        void OnChanged() { if (hud != null) hud.GuildMarkDirty(); }

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            GuildState.Tick(udt);
            if (Game.I != null && Game.I.Started) Quests.Tick(Time.deltaTime);
            if (hud != null) hud.GuildTick(udt);
        }

        void OnApplicationQuit() { GuildState.Flush(); }
    }

    /// <summary>Anima as cores de uma insígnia de rank (SSS arco-íris, DEUS dourado/branco pulsante).</summary>
    public class GuildRankGlow : MonoBehaviour
    {
        public int rank;
        public Graphic[] targets;
        public float[] alphas;
        public float[] whiten;

        void Update()
        {
            if (targets == null) return;
            Color c = Ranks.AnimColor(rank, Time.unscaledTime);
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                float a = alphas != null && i < alphas.Length ? alphas[i] : 1f;
                float w = whiten != null && i < whiten.Length ? whiten[i] : 0f;
                Color k = Color.Lerp(c, Color.white, w);
                k.a = a;
                targets[i].color = k;
            }
        }
    }
}
