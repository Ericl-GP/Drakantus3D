using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Drakantus
{
    /// <summary>
    /// Progressão/meta (partial do HUD): carimbo de nota ao entregar missão, aviso animado de conquista no topo,
    /// e as janelas Conquistas, Coleção e Códex (livro de lore). Animado por UpdateCodexFx (chamado no GuildTick).
    /// </summary>
    public partial class HUD
    {
        // ------------------------------------------------------------------ carimbo de nota
        class StampReq { public QuestDef d; public int grade; public string rewards, stats; public bool best; }
        readonly List<StampReq> stampQueue = new List<StampReq>();
        RectTransform stampRt, stampScrollRt, stampMarkRt;
        CanvasGroup stampCg, stampMarkCg;
        Text stampTitle, stampTypeTxt, stampRewards, stampStats, stampLetter, stampBest, stampNota;
        Image stampRing, stampRing2, stampGlow;
        float stampT = -1f;
        int stampGrade;
        bool stampHit;

        // ------------------------------------------------------------------ aviso de conquista
        readonly List<AchievementDef> achQueue = new List<AchievementDef>();
        RectTransform achRt, achShineRt;
        CanvasGroup achCg;
        Text achName, achDesc, achReward;
        float achT = -1f;

        // ------------------------------------------------------------------ códex / botões
        int codexSpread = -1, codexFlip;
        Image codexBadge;

        // ================================================================== loop
        internal void UpdateCodexFx(float udt)
        {
            if (stampT < 0f && stampQueue.Count > 0 && guildCereT < 0f) StartStamp();
            if (stampT >= 0f) UpdateStamp(udt);
            if (achT < 0f && achQueue.Count > 0) StartAchievementPopup();
            if (achT >= 0f) UpdateAchievementPopup(udt);
        }

        // ================================================================== CARIMBO DE NOTA
        /// <summary>Pergaminho com a nota grande "carimbada" (fila: espera a cerimônia de rank terminar).</summary>
        public void GradeStamp(QuestDef d, int grade, string rewards, string stats, bool newBest)
        {
            if (d == null) return;
            stampQueue.Add(new StampReq { d = d, grade = grade, rewards = rewards ?? "", stats = stats ?? "", best = newBest });
        }

        void BuildStamp()
        {
            var r = UIKit.R(transform, "Nota_Missao");
            stampRt = UIKit.Place(r, MID, MID, new Vector2(0f, 30f), new Vector2(760f, 400f));
            stampCg = r.gameObject.AddComponent<CanvasGroup>();
            stampCg.alpha = 0f; stampCg.blocksRaycasts = false; stampCg.interactable = false;

            stampGlow = UIKit.Img(r, "brilho", UIKit.SoftSprite(), new Color(1f, 0.85f, 0.5f, 0.35f));
            UIKit.Place(stampGlow.rectTransform, MID, MID, Vector2.zero, new Vector2(1200f, 760f));

            stampScrollRt = UIKit.R(r, "pergaminho");
            UIKit.Stretch(stampScrollRt);
            var sh = UIKit.Round(stampScrollRt, "sombra", new Color(0f, 0f, 0f, 0.4f), 2.5f);
            UIKit.Stretch(sh.rectTransform, 9f, -9f, 9f, -9f);
            var paper = UIKit.Round(stampScrollRt, "papel", PaperCol, 2.5f);
            var pr = UIKit.Stretch(paper.rectTransform);
            for (int k = 0; k < 2; k++)
            {
                var rod = UIKit.Round(stampScrollRt, "rolo", new Color(0.45f, 0.28f, 0.13f, 1f), 1.5f);
                UIKit.Place(rod.rectTransform, k == 0 ? TC : BC, MID, Vector2.zero, new Vector2(800f, 30f));
                for (int s = -1; s <= 1; s += 2)
                {
                    var knob = UIKit.Img(rod.rectTransform, "ponta", U.CircleSprite(), new Color(0.32f, 0.18f, 0.08f, 1f));
                    UIKit.Place(knob.rectTransform, MID, MID, new Vector2(s * 404f, 0f), new Vector2(38f, 38f));
                }
            }

            stampTypeTxt = GInk(pr, "MISSÃO ENTREGUE", 16, InkDim, TL, TL, new Vector2(44f, -36f), new Vector2(440f, 22f), TextAnchor.MiddleLeft, FontStyle.Bold);
            stampTitle = GInk(pr, "", 30, InkCol, TL, TL, new Vector2(44f, -62f), new Vector2(440f, 84f), TextAnchor.UpperLeft, FontStyle.Bold);
            stampRewards = GInk(pr, "", 19, InkCol, TL, TL, new Vector2(44f, -156f), new Vector2(440f, 96f), TextAnchor.UpperLeft);
            stampStats = GInk(pr, "", 15, InkDim, TL, TL, new Vector2(44f, -262f), new Vector2(440f, 60f), TextAnchor.UpperLeft, FontStyle.Italic);
            stampBest = GInk(pr, "★ Novo recorde nesta missão!", 17, new Color(0.62f, 0.38f, 0.05f, 1f), BL, BL, new Vector2(44f, 36f), new Vector2(440f, 24f), TextAnchor.MiddleLeft, FontStyle.Bold);

            // o carimbo
            var m = UIKit.R(pr, "carimbo");
            stampMarkRt = UIKit.Place(m, MID, MID, new Vector2(214f, -6f), new Vector2(236f, 236f));
            stampMarkRt.localRotation = Quaternion.Euler(0f, 0f, -12f);
            stampMarkCg = m.gameObject.AddComponent<CanvasGroup>();
            stampRing = UIKit.Img(m, "aro", U.CircleSprite(), Color.red);
            UIKit.Stretch(stampRing.rectTransform);
            var in1 = UIKit.Img(stampRing.rectTransform, "in", U.CircleSprite(), PaperCol);
            UIKit.Stretch(in1.rectTransform, 11f);
            stampRing2 = UIKit.Img(in1.rectTransform, "aro2", U.CircleSprite(), Color.red);
            UIKit.Stretch(stampRing2.rectTransform, 8f);
            var in2 = UIKit.Img(stampRing2.rectTransform, "in2", U.CircleSprite(), PaperCol);
            UIKit.Stretch(in2.rectTransform, 4f);
            stampNota = UIKit.Txt(in2.rectTransform, "rotulo", "NOTA", 18, Color.red, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(stampNota.rectTransform, TC, TC, new Vector2(0f, -22f), new Vector2(160f, 24f));
            stampLetter = UIKit.Txt(in2.rectTransform, "letra", "S", 120, Color.red, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(stampLetter.rectTransform, MID, MID, new Vector2(0f, -10f), new Vector2(220f, 150f));
            stampLetter.horizontalOverflow = HorizontalWrapMode.Overflow;
            stampLetter.verticalOverflow = VerticalWrapMode.Overflow;
            foreach (var t in new[] { stampNota, stampLetter })
            {
                var s = t.GetComponent<Shadow>();
                if (s != null) s.effectColor = new Color(0f, 0f, 0f, 0.18f);
            }
            r.gameObject.SetActive(false);
        }

        void StartStamp()
        {
            if (stampRt == null) BuildStamp();
            var q = stampQueue[0];
            stampQueue.RemoveAt(0);
            stampGrade = q.grade;
            Color gc = QuestGrading.GradeColor(q.grade);
            Color ink = Color.Lerp(gc, Color.black, 0.18f);
            stampRing.color = ink;
            stampRing2.color = new Color(ink.r, ink.g, ink.b, 0.8f);
            stampNota.color = ink;
            stampLetter.color = ink;
            string l = QuestGrading.Letter(q.grade);
            stampLetter.text = l;
            stampLetter.fontSize = l.Length > 1 ? 96 : 124;
            stampTypeTxt.text = q.d.type == "chain" ? "SEQUÊNCIA ENTREGUE" : q.d.type == "promo" ? "PROVA ENTREGUE" : "MISSÃO ENTREGUE";
            stampTitle.text = q.d.title;
            stampRewards.text = "<b>Recompensas:</b> " + q.rewards;
            stampStats.text = q.stats;
            stampBest.gameObject.SetActive(q.best);
            stampGlow.color = new Color(gc.r, gc.g, gc.b, q.grade >= QuestGrading.S ? 0.5f : 0.22f);
            stampMarkCg.alpha = 0f;
            stampMarkRt.localScale = Vector3.one * 2.6f;
            stampRt.anchoredPosition = new Vector2(0f, 30f);
            stampRt.SetAsLastSibling();
            stampRt.gameObject.SetActive(true);
            stampT = 0f;
            stampHit = false;
            Sfx.Play("quest_complete");
        }

        void UpdateStamp(float udt)
        {
            const float Dur = 4.2f, HitAt = 0.55f, Slam = 0.16f;
            stampT += udt;
            float t = stampT;
            stampCg.alpha = t < 0.3f ? t / 0.3f : t > Dur - 0.5f ? Mathf.Clamp01((Dur - t) / 0.5f) : 1f;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.35f), 3f);
            stampScrollRt.localScale = new Vector3(1f, Mathf.Lerp(0.08f, 1f, e), 1f);

            float k = Mathf.Clamp01((t - HitAt) / Slam);
            stampMarkCg.alpha = k;
            stampMarkRt.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, k * k);
            if (k >= 1f && !stampHit)
            {
                stampHit = true;
                if (stampGrade >= QuestGrading.S) Sfx.Play("grade_s");
                else Sfx.Play("hit_heavy", null, 0.55f);
                if (Game.I != null && stampGrade >= QuestGrading.S) Game.I.Shake(0.15f);
            }
            // tremidinha do impacto
            float since = t - HitAt - Slam;
            if (since > 0f && since < 0.22f)
            {
                float a = (1f - since / 0.22f) * 7f;
                stampRt.anchoredPosition = new Vector2(Random.Range(-a, a), 30f + Random.Range(-a, a));
            }
            else stampRt.anchoredPosition = new Vector2(0f, 30f);

            if (stampGrade >= QuestGrading.SPlus)
            {
                Color c = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.35f, 1f), 0.55f, 1f);
                stampGlow.color = new Color(c.r, c.g, c.b, 0.45f + 0.15f * Mathf.Sin(t * 7f));
            }
            stampMarkRt.localRotation = Quaternion.Euler(0f, 0f, -12f + (stampGrade >= QuestGrading.S && k >= 1f ? Mathf.Sin(t * 3f) * 1.5f : 0f));

            if (t >= Dur)
            {
                stampT = -1f;
                stampRt.gameObject.SetActive(false);
            }
        }

        // ================================================================== AVISO DE CONQUISTA
        /// <summary>Faixa animada no topo da tela (fila). Toca "achievement" ao aparecer.</summary>
        public void AchievementPopup(AchievementDef d)
        {
            if (d != null) achQueue.Add(d);
        }

        void BuildAchievementPopup()
        {
            var p = UIKit.Panel(transform, "Conquista_Aviso", new Color(0.08f, 0.065f, 0.03f, 0.96f), 1.4f);
            p.raycastTarget = false;
            achRt = UIKit.Place(p.rectTransform, TC, TC, new Vector2(0f, 140f), new Vector2(620f, 100f));
            var o = p.GetComponent<Outline>();
            if (o != null) { o.effectColor = new Color(1f, 0.8f, 0.35f, 0.85f); o.effectDistance = new Vector2(2f, -2f); }
            p.gameObject.AddComponent<RectMask2D>();
            achCg = p.gameObject.AddComponent<CanvasGroup>();
            achCg.alpha = 0f; achCg.blocksRaycasts = false; achCg.interactable = false;

            var ring = UIKit.Img(achRt, "aro", U.CircleSprite(), UIKit.Gold);
            UIKit.Place(ring.rectTransform, ML, ML, new Vector2(16f, 0f), new Vector2(72f, 72f));
            var inner = UIKit.Img(ring.rectTransform, "in", U.CircleSprite(), new Color(0.12f, 0.09f, 0.03f, 1f));
            UIKit.Stretch(inner.rectTransform, 5f);
            var star = UIKit.Txt(inner.rectTransform, "estrela", "★", 40, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(star.rectTransform);
            star.horizontalOverflow = HorizontalWrapMode.Overflow;

            var head = UIKit.Txt(achRt, "rotulo", "CONQUISTA DESBLOQUEADA", 14, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(head.rectTransform, TL, TL, new Vector2(104f, -10f), new Vector2(500f, 18f));
            achName = UIKit.Txt(achRt, "nome", "", 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(achName.rectTransform, TL, TL, new Vector2(104f, -28f), new Vector2(500f, 32f));
            achDesc = UIKit.Txt(achRt, "desc", "", 15, UIKit.TextCol, TextAnchor.MiddleLeft);
            UIKit.Place(achDesc.rectTransform, TL, TL, new Vector2(104f, -58f), new Vector2(500f, 18f));
            achReward = UIKit.Txt(achRt, "recompensa", "", 14, UIKit.DimText, TextAnchor.MiddleLeft);
            UIKit.Place(achReward.rectTransform, TL, TL, new Vector2(104f, -77f), new Vector2(500f, 18f));
            achReward.horizontalOverflow = HorizontalWrapMode.Overflow;

            var shine = UIKit.Img(achRt, "reflexo", U.WhiteSprite(), new Color(1f, 1f, 1f, 0.16f));
            achShineRt = UIKit.Place(shine.rectTransform, ML, MID, new Vector2(-80f, 0f), new Vector2(60f, 180f));
            achShineRt.localRotation = Quaternion.Euler(0f, 0f, -20f);
            achRt.gameObject.SetActive(false);
        }

        void StartAchievementPopup()
        {
            if (achRt == null) BuildAchievementPopup();
            var d = achQueue[0];
            achQueue.RemoveAt(0);
            achName.text = d.name;
            achDesc.text = d.desc;
            string rw = Achievements.RewardText(d, true);
            achReward.text = rw == "—" ? "" : "Recompensa: " + rw;
            achRt.SetAsLastSibling();
            achRt.gameObject.SetActive(true);
            achT = 0f;
            Sfx.Play("achievement");
        }

        void UpdateAchievementPopup(float udt)
        {
            const float Dur = 4.2f;
            achT += udt;
            float t = achT;
            float inK = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.35f), 3f);
            float outK = Mathf.Clamp01((t - (Dur - 0.35f)) / 0.35f);
            float y = Mathf.Lerp(140f, -24f, inK);
            y = Mathf.Lerp(y, 140f, outK * outK);
            achRt.anchoredPosition = new Vector2(0f, y);
            achCg.alpha = Mathf.Clamp01(inK * 1.5f) * (1f - outK);
            float sk = Mathf.Clamp01((t - 0.4f) / 0.7f);
            achShineRt.anchoredPosition = new Vector2(Mathf.Lerp(-80f, 700f, sk), 0f);
            if (t >= Dur)
            {
                achT = -1f;
                achRt.gameObject.SetActive(false);
            }
        }

        // ================================================================== botões discretos (abaixo de "Pets")
        void BuildMetaButtons()
        {
            MetaButton(new Vector2(474f, -170f), "conquistas", AchievementsWindow, () => "Conquistas",
                () => "Desbloqueadas: " + Achievements.UnlockedCount + " / " + Achievements.Total + "\nClique para ver o progresso e as recompensas.");
            MetaButton(new Vector2(512f, -170f), "colecao", CollectionWindow, () => "Coleção",
                () => "Itens raros encontrados nos andares da Torre: " + GuildState.G.collected.Count + " / " + Collectibles.Total);
            var cb = MetaButton(new Vector2(550f, -170f), "codex", CodexWindow, () => "Códex",
                () => "Páginas descobertas: " + Lore.UnlockedCount + " / " + Lore.Total + (Lore.NewCount > 0 ? "\n" + Lore.NewCount + " página(s) nova(s)!" : ""));
            codexBadge = UIKit.Img(cb, "nova", U.CircleSprite(), new Color(1f, 0.32f, 0.3f, 1f));
            UIKit.Place(codexBadge.rectTransform, TR, MID, new Vector2(-3f, -3f), new Vector2(11f, 11f));
            codexBadge.gameObject.SetActive(false);
        }

        RectTransform MetaButton(Vector2 pos, string kind, System.Action open, System.Func<string> title, System.Func<string> body)
        {
            var b = UIKit.Btn(hudRoot, "", new Vector2(34f, 30f), open, UIKit.BtnCol, 13);
            var rt = UIKit.Place(UIKit.RT(b), TL, MID, pos, new Vector2(34f, 30f));
            var ic = UIKit.R(rt, "icone");
            UIKit.Place(ic, MID, MID, Vector2.zero, new Vector2(30f, 26f));
            MiniIcon(ic, kind == "conquistas" ? "trofeu" : kind == "colecao" ? "estrela" : "livro", true, 0.26f);
            UITip.Add(b.gameObject, title, body);
            return rt;
        }

        void RefreshCodexBadge()
        {
            if (codexBadge == null) return;
            bool on = Lore.NewCount > 0;
            if (codexBadge.gameObject.activeSelf != on) codexBadge.gameObject.SetActive(on);
        }

        /// <summary>Ícone feito de formas simples. shape: "estrela" | "livro" | "moeda" | "trofeu". u = escala (100 = tamanho grande).</summary>
        void MiniIcon(RectTransform p, string shape, bool owned, float u)
        {
            Color dark = new Color(0.22f, 0.24f, 0.3f, 1f);
            switch (shape)
            {
                case "livro":
                {
                    Color cv = owned ? U.Hex("7a3aa8") : dark;
                    GDot(p, cv, 0f, 0f, 58f, 74f, u, 0f, true);
                    GDot(p, owned ? U.Hex("f4ead2") : new Color(0.3f, 0.32f, 0.38f, 1f), 5f, 0f, 44f, 66f, u, 0f, true);
                    GDot(p, owned ? U.Hex("ffd04a") : dark, -26f, 0f, 8f, 74f, u, 0f, true);
                    if (owned) GDot(p, U.Hex("c88aff"), 5f, 8f, 24f, 4f, u, 0f, true);
                    break;
                }
                case "moeda":
                {
                    GDot(p, owned ? U.Hex("c8843a") : dark, 0f, 0f, 70f, 70f, u);
                    GDot(p, owned ? U.Hex("6fae8a") : new Color(0.3f, 0.32f, 0.38f, 1f), 0f, 0f, 50f, 50f, u);
                    GDot(p, owned ? U.Hex("ffd27a") : dark, 0f, 0f, 20f, 20f, u, 45f, true);
                    break;
                }
                case "trofeu":
                {
                    Color g = owned ? UIKit.Gold : dark;
                    GDot(p, g, 0f, 14f, 56f, 44f, u);
                    GDot(p, g, 0f, 30f, 60f, 14f, u, 0f, true);
                    GDot(p, g, 0f, -14f, 12f, 26f, u, 0f, true);
                    GDot(p, g, 0f, -30f, 40f, 10f, u, 0f, true);
                    GDot(p, owned ? Color.white : dark, -10f, 18f, 8f, 16f, u);
                    break;
                }
                default: // estrela
                {
                    Color c = owned ? U.Hex("ffe98a") : dark;
                    GDot(p, c, 0f, 0f, 48f, 48f, u, 0f, true);
                    GDot(p, c, 0f, 0f, 48f, 48f, u, 45f, true);
                    GDot(p, owned ? Color.white : new Color(0.3f, 0.32f, 0.38f, 1f), 0f, 0f, 18f, 18f, u);
                    break;
                }
            }
        }

        // ================================================================== CONQUISTAS
        public void AchievementsWindow()
        {
            Achievements.Load();
            var c = OpenModal("achievements", "Conquistas", new Vector2(1600f, 900f), true);
            modalRebuild = AchievementsWindow;
            var G = GuildState.G;

            int got = Achievements.UnlockedCount, total = Achievements.Total;
            var top = UIKit.R(c, "topo");
            UIKit.Place(top, TL, TL, Vector2.zero, new Vector2(1544f, 44f));
            Label(top, "Desbloqueadas: <b><color=#FFD76A>" + got + "</color></b> / " + total, 22, UIKit.TextCol, ML, ML, Vector2.zero, new Vector2(320f, 40f));
            GBar(top, ML, ML, new Vector2(320f, 0f), new Vector2(380f, 18f), got / (float)Mathf.Max(1, total), UIKit.Gold, new Color(0.02f, 0.03f, 0.06f, 0.9f));
            Label(top, "<color=#9aa3b5>As recompensas são entregues na hora em que a conquista é desbloqueada.</color>", 16, UIKit.TextCol, ML, ML,
                new Vector2(720f, 0f), new Vector2(820f, 30f));

            // ---- lista
            ScrollRect sr;
            var list = UIKit.ScrollArea(c, "lista", out sr);
            UIKit.Place(UIKit.RT(sr), TL, TL, new Vector2(0f, -54f), new Vector2(1040f, 680f));
            var vl = UIKit.VLayout(list.gameObject, 8f, new RectOffset(6, 6, 6, 6));
            vl.childControlHeight = false;
            UIKit.Fit(list.gameObject, false, true);
            // desbloqueadas por último? não: em andamento primeiro, depois as concluídas
            var order = new List<AchievementDef>();
            foreach (var d in Achievements.All) if (!Achievements.Unlocked(d.id)) order.Add(d);
            foreach (var d in Achievements.All) if (Achievements.Unlocked(d.id)) order.Add(d);
            foreach (var d in order) AchievementRow(list, d);
            if (order.Count == 0)
                Label(c, "Nenhuma conquista cadastrada (faltando achievements.json).", 20, UIKit.DimText, MID, MID, Vector2.zero, new Vector2(800f, 40f), TextAnchor.MiddleCenter);

            // ---- melhores notas
            var right = UIKit.Round(c, "notas", UIKit.CardCol, 1f, true);
            var rr = UIKit.Place(right.rectTransform, TR, TR, new Vector2(0f, -54f), new Vector2(490f, 680f));
            Label(rr, "MELHORES NOTAS", 18, UIKit.Gold, TC, TC, new Vector2(0f, -12f), new Vector2(460f, 26f), TextAnchor.MiddleCenter, FontStyle.Bold);
            for (int g = 0; g <= QuestGrading.SPlus; g++)
            {
                float x = -198f + g * 66f;
                var box = UIKit.Round(rr, "nota_" + g, new Color(0f, 0f, 0f, 0.3f), 1.4f);
                UIKit.Place(box.rectTransform, TC, TC, new Vector2(x, -48f), new Vector2(58f, 66f));
                Label(box.rectTransform, QuestGrading.Letter(g), 24, QuestGrading.GradeColor(g), TC, TC, new Vector2(0f, -4f), new Vector2(58f, 32f), TextAnchor.MiddleCenter, FontStyle.Bold);
                Label(box.rectTransform, QuestGrading.CountOf(g).ToString(), 16, UIKit.TextCol, BC, BC, new Vector2(0f, 4f), new Vector2(58f, 24f), TextAnchor.MiddleCenter);
            }
            ScrollRect sr2;
            var bl = UIKit.ScrollArea(rr, "recordes", out sr2);
            UIKit.Place(UIKit.RT(sr2), TC, TC, new Vector2(0f, -126f), new Vector2(466f, 540f));
            var vl2 = UIKit.VLayout(bl.gameObject, 4f, new RectOffset(4, 4, 4, 4));
            vl2.childControlHeight = false;
            UIKit.Fit(bl.gameObject, false, true);
            var recs = new List<QuestGradeRecord>(G.bestGrades);
            recs.Sort((a, b) => b.grade != a.grade ? b.grade.CompareTo(a.grade) : a.time.CompareTo(b.time));
            foreach (var rec in recs)
            {
                var qd = Quests.Def(rec.id);
                if (qd == null) continue;
                var row = UIKit.Round(bl, "rec_" + rec.id, UIKit.RowCol, 1.2f);
                var rw = row.rectTransform;
                rw.sizeDelta = new Vector2(0f, 46f);
                Label(rw, QuestGrading.Letter(rec.grade), 24, QuestGrading.GradeColor(rec.grade), ML, ML, new Vector2(10f, 0f), new Vector2(50f, 40f), TextAnchor.MiddleCenter, FontStyle.Bold);
                var tt = Label(rw, qd.title, 16, UIKit.TextCol, ML, ML, new Vector2(64f, 0f), new Vector2(300f, 40f));
                tt.horizontalOverflow = HorizontalWrapMode.Wrap;
                tt.verticalOverflow = VerticalWrapMode.Truncate;
                Label(rw, rec.time > 0f ? QuestGrading.FormatTime(rec.time) : "—", 15, UIKit.DimText, MR, MR, new Vector2(-10f, 0f), new Vector2(80f, 30f), TextAnchor.MiddleRight);
            }
            if (recs.Count == 0)
            {
                var t = UIKit.Txt(bl, "vazio", "Entregue missões para registrar suas notas.\nTerminar rápido, sem levar dano e abatendo depressa dá S+!", 16, UIKit.DimText, TextAnchor.MiddleCenter, FontStyle.Italic);
                t.rectTransform.sizeDelta = new Vector2(0f, 120f);
            }
        }

        void AchievementRow(RectTransform parent, AchievementDef d)
        {
            bool got = Achievements.Unlocked(d.id);
            bool secret = d.hidden && !got;
            var row = UIKit.Round(parent, "conq_" + d.id, got ? new Color(0.14f, 0.13f, 0.08f, 0.98f) : UIKit.RowCol, 1.2f, true);
            var rr = row.rectTransform;
            rr.sizeDelta = new Vector2(0f, 88f);
            if (got)
            {
                var o = row.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(1f, 0.84f, 0.42f, 0.5f); o.effectDistance = new Vector2(2f, -2f); o.useGraphicAlpha = false;
            }
            var ring = UIKit.Img(rr, "aro", U.CircleSprite(), got ? UIKit.Gold : new Color(0.3f, 0.33f, 0.4f, 1f));
            UIKit.Place(ring.rectTransform, ML, ML, new Vector2(14f, 0f), new Vector2(64f, 64f));
            var inner = UIKit.Img(ring.rectTransform, "in", U.CircleSprite(), new Color(0.08f, 0.08f, 0.1f, 1f));
            UIKit.Stretch(inner.rectTransform, 4f);
            if (secret)
            {
                var q = UIKit.Txt(inner.rectTransform, "q", "?", 34, UIKit.DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(q.rectTransform);
            }
            else
            {
                var ic = UIKit.R(inner.rectTransform, "icone");
                UIKit.Place(ic, MID, MID, Vector2.zero, new Vector2(50f, 50f));
                MiniIcon(ic, "trofeu", got, 0.5f);
            }

            Label(rr, secret ? "Conquista secreta" : d.name, 21, got ? UIKit.Gold : Color.white, TL, TL, new Vector2(92f, -10f), new Vector2(580f, 28f), TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(rr, secret ? "Continue jogando para descobrir." : d.desc, 15, UIKit.DimText, TL, TL, new Vector2(92f, -38f), new Vector2(580f, 20f));
            int have, need;
            float frac = Achievements.Progress(d, out have, out need);
            if (!secret)
            {
                GBar(rr, TL, TL, new Vector2(92f, -64f), new Vector2(330f, 14f), frac, got ? U.Hex("5fbf5a") : UIKit.Gold, new Color(0.02f, 0.03f, 0.06f, 0.9f));
                Label(rr, have + " / " + need, 14, UIKit.TextCol, TL, TL, new Vector2(432f, -62f), new Vector2(160f, 18f));
            }
            var rw = Label(rr, secret ? "" : "<color=#9aa3b5>Recompensa:</color> " + Achievements.RewardText(d), 14, UIKit.TextCol, MR, MR, new Vector2(-16f, 14f), new Vector2(330f, 40f), TextAnchor.MiddleRight);
            rw.verticalOverflow = VerticalWrapMode.Truncate;
            Label(rr, got ? "✓ Desbloqueada" : Mathf.FloorToInt(frac * 100f) + "%", 17, got ? new Color(0.55f, 0.9f, 0.55f) : UIKit.DimText, MR, MR,
                new Vector2(-16f, -22f), new Vector2(200f, 24f), TextAnchor.MiddleRight, FontStyle.Bold);
        }

        // ================================================================== COLEÇÃO
        public void CollectionWindow()
        {
            Collectibles.Load();
            var c = OpenModal("collection", "Coleção da Torre", new Vector2(1560f, 900f), true);
            modalRebuild = CollectionWindow;
            var G = GuildState.G;

            var top = UIKit.R(c, "topo");
            UIKit.Place(top, TL, TL, Vector2.zero, new Vector2(1504f, 44f));
            Label(top, "Encontrados: <b><color=#FFD76A>" + G.collected.Count + "</color></b> / " + Collectibles.Total +
                       "     <color=#9aa3b5>Procure brilhos com feixe de luz nos andares da Torre (1–3 por andar). Livros liberam páginas do Códex.</color>",
                20, UIKit.TextCol, ML, ML, Vector2.zero, new Vector2(1300f, 40f));
            var bc = UIKit.Btn(top, "Abrir Códex", new Vector2(190f, 42f), CodexWindow, UIKit.BtnCol, 18);
            UIKit.Place(UIKit.RT(bc), MR, MR, Vector2.zero, new Vector2(190f, 42f));

            ScrollRect sr;
            var list = UIKit.ScrollArea(c, "grade", out sr);
            UIKit.Place(UIKit.RT(sr), TL, TL, new Vector2(0f, -54f), new Vector2(1504f, 680f));
            UIKit.VLayout(list.gameObject, 10f, new RectOffset(12, 12, 10, 16));
            UIKit.Fit(list.gameObject, false, true);

            foreach (var kind in Collectibles.Kinds)
            {
                int have = Collectibles.CountKind(kind, true), total = Collectibles.CountKind(kind, false);
                if (total == 0) continue;
                Color kc = Collectibles.KindColor(kind);
                var h = UIKit.Txt(list, "titulo_" + kind, "<color=#" + UIKit.ColorHex(kc) + ">" + Collectibles.KindName(kind).ToUpper() + "</color>   <color=#9aa3b5>" + have + " / " + total + "</color>",
                    20, UIKit.TextCol, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIKit.LE(h, -1f, 34f);
                var gridRt = UIKit.R(list, "itens_" + kind);
                var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(166f, 160f);
                grid.spacing = new Vector2(12f, 12f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 8;
                grid.childAlignment = TextAnchor.UpperLeft;
                foreach (var d in Collectibles.All) if (d.kind == kind) CollectionCell(gridRt, d);
            }
            if (Collectibles.Total == 0)
                Label(c, "Nada para colecionar ainda (faltando collectibles.json).", 20, UIKit.DimText, MID, MID, Vector2.zero, new Vector2(800f, 40f), TextAnchor.MiddleCenter);
        }

        void CollectionCell(RectTransform parent, CollectibleDef d)
        {
            bool owned = Collectibles.Has(d.id);
            Color kc = Collectibles.KindColor(d.kind);
            var card = UIKit.Round(parent, "item_" + d.id, owned ? UIKit.CardCol : new Color(0.05f, 0.06f, 0.09f, 0.95f), 1.2f, true);
            var cr = card.rectTransform;
            if (owned)
            {
                var o = card.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(kc.r, kc.g, kc.b, 0.55f); o.effectDistance = new Vector2(2f, -2f); o.useGraphicAlpha = false;
                var glow = UIKit.Img(cr, "brilho", UIKit.SoftSprite(), new Color(kc.r, kc.g, kc.b, 0.3f));
                UIKit.Place(glow.rectTransform, TC, MID, new Vector2(0f, -54f), new Vector2(120f, 120f));
            }
            var ic = UIKit.R(cr, "icone");
            UIKit.Place(ic, TC, MID, new Vector2(0f, -54f), new Vector2(80f, 80f));
            MiniIcon(ic, d.kind, owned, 0.8f);
            if (!owned)
            {
                var q = UIKit.Txt(ic, "q", "?", 30, UIKit.DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(q.rectTransform);
            }
            var nm = Label(cr, owned ? d.name : "???", 14, owned ? Color.white : UIKit.DimText, BC, BC, new Vector2(0f, 26f), new Vector2(156f, 40f), TextAnchor.MiddleCenter, owned ? FontStyle.Bold : FontStyle.Normal);
            nm.verticalOverflow = VerticalWrapMode.Truncate;
            Label(cr, owned ? "✓ Encontrado" : "Andar " + d.minFloor + "+", 12, owned ? new Color(0.55f, 0.9f, 0.55f) : UIKit.DimText, BC, BC, new Vector2(0f, 6f), new Vector2(156f, 18f), TextAnchor.MiddleCenter);
            card.gameObject.AddComponent<UIHover>().scale = 1.04f;
            UITip.Add(card.gameObject, () => owned ? d.name : "Ainda não encontrado", () =>
            {
                if (!owned) return Collectibles.KindName(d.kind) + "\nPode aparecer a partir do Andar " + d.minFloor + " da Torre.";
                string s = d.desc;
                if (d.coins > 0 || d.xp > 0) s += "\n\nRendeu: " + (d.coins > 0 ? d.coins + " moedas " : "") + (d.xp > 0 ? d.xp + " XP" : "");
                var lp = Lore.Def(d.lore);
                if (lp != null) s += "\nPágina do Códex: \"" + lp.title + "\"";
                return s;
            });
        }

        // ================================================================== CÓDEX (livro)
        public void CodexWindow()
        {
            Lore.Load();
            var pages = Lore.Pages;
            int n = pages.Count;
            var c = OpenModal("codex", "Códex de Aster", new Vector2(1560f, 920f), true);
            modalRebuild = CodexWindow;
            if (n == 0)
            {
                Label(c, "O Códex está em branco (faltando lore.json).", 22, UIKit.DimText, MID, MID, Vector2.zero, new Vector2(900f, 40f), TextAnchor.MiddleCenter);
                return;
            }
            int lastSpread = (n - 1) - (n - 1) % 2;
            if (codexSpread < 0)
            {
                codexSpread = 0;
                for (int i = 0; i < n; i++) if (Lore.IsNew(pages[i].id)) { codexSpread = i - i % 2; break; }
            }
            codexSpread = Mathf.Clamp(codexSpread - codexSpread % 2, 0, lastSpread);

            Label(c, "Páginas descobertas: <b><color=#FFD76A>" + Lore.UnlockedCount + "</color></b> / " + n +
                     (Lore.NewCount > 0 ? "     <color=#ff8a80>" + Lore.NewCount + " nova(s)</color>" : "") +
                     "     <color=#9aa3b5>Livros de Lore, chefes, rank e pets revelam novas páginas.</color>",
                19, UIKit.TextCol, TL, TL, Vector2.zero, new Vector2(1500f, 30f));

            // capa
            var cover = UIKit.Round(c, "capa", new Color(0.33f, 0.18f, 0.09f, 1f), 1.2f, true);
            var cv = UIKit.Place(cover.rectTransform, TC, TC, new Vector2(0f, -40f), new Vector2(1470f, 680f));
            var co = cover.gameObject.AddComponent<Outline>();
            co.effectColor = new Color(0f, 0f, 0f, 0.6f); co.effectDistance = new Vector2(3f, -3f); co.useGraphicAlpha = false;
            var trim = UIKit.Round(cv, "friso", new Color(0.78f, 0.58f, 0.22f, 0.55f), 1.2f);
            UIKit.Stretch(trim.rectTransform, 8f);
            var trimIn = UIKit.Round(trim.rectTransform, "in", new Color(0.33f, 0.18f, 0.09f, 1f), 1.2f);
            UIKit.Stretch(trimIn.rectTransform, 3f);

            var left = UIKit.Round(cv, "pagina_esq", PaperCol, 2f);
            var lp = UIKit.Place(left.rectTransform, MID, MR, new Vector2(-5f, 0f), new Vector2(700f, 640f));
            var right = UIKit.Round(cv, "pagina_dir", PaperCol, 2f);
            var rp = UIKit.Place(right.rectTransform, MID, ML, new Vector2(5f, 0f), new Vector2(700f, 640f));
            var spine = UIKit.Img(cv, "lombada", U.WhiteSprite(), new Color(0.2f, 0.1f, 0.04f, 1f));
            UIKit.Place(spine.rectTransform, MID, MID, Vector2.zero, new Vector2(12f, 660f));
            var shL = UIKit.Img(lp, "sombra", UIKit.SoftSprite(), new Color(0.35f, 0.22f, 0.1f, 0.25f));
            UIKit.Place(shL.rectTransform, MR, MID, new Vector2(30f, 0f), new Vector2(110f, 760f));
            var shR = UIKit.Img(rp, "sombra", UIKit.SoftSprite(), new Color(0.35f, 0.22f, 0.1f, 0.25f));
            UIKit.Place(shR.rectTransform, ML, MID, new Vector2(-30f, 0f), new Vector2(110f, 760f));

            CodexPage(lp, codexSpread, pages);
            CodexPage(rp, codexSpread + 1, pages);
            if (codexFlip != 0)
            {
                var flip = (codexFlip > 0 ? rp : lp).gameObject.AddComponent<CodexFlip>();
                flip.dur = 0.3f;
                codexFlip = 0;
            }

            // navegação
            var prev = UIKit.Btn(c, "◄ Anterior", new Vector2(200f, 46f), () => CodexTurn(-2), UIKit.BtnCol, 19);
            UIKit.Place(UIKit.RT(prev), TC, TC, new Vector2(-330f, -732f), new Vector2(200f, 46f));
            if (codexSpread <= 0) UIKit.Disable(prev);
            var next = UIKit.Btn(c, "Próxima ►", new Vector2(200f, 46f), () => CodexTurn(2), UIKit.BtnCol, 19);
            UIKit.Place(UIKit.RT(next), TC, TC, new Vector2(330f, -732f), new Vector2(200f, 46f));
            if (codexSpread >= lastSpread) UIKit.Disable(next);
            Label(c, "Páginas " + (codexSpread + 1) + "–" + Mathf.Min(n, codexSpread + 2) + " de " + n, 17, UIKit.DimText, TC, TC,
                new Vector2(0f, -744f), new Vector2(400f, 24f), TextAnchor.MiddleCenter);
            // pular para a próxima página nova
            int nextNew = -1;
            for (int i = 0; i < n; i++) if (Lore.IsNew(pages[i].id) && (i < codexSpread || i > codexSpread + 1)) { nextNew = i; break; }
            if (nextNew >= 0)
            {
                int target = nextNew - nextNew % 2;
                var bn = UIKit.Btn(c, "Ir para página nova", new Vector2(240f, 40f), () => CodexTurn(target - codexSpread), UIKit.Gold, 17);
                UIKit.Place(UIKit.RT(bn), BL, BL, new Vector2(0f, 0f), new Vector2(240f, 40f));
            }

            // marca como lidas as páginas abertas
            for (int i = codexSpread; i <= codexSpread + 1 && i < n; i++) Lore.MarkRead(pages[i].id);
            guildDirty = true;   // atualiza o selo "nova" do botão
        }

        void CodexTurn(int delta)
        {
            if (delta == 0) return;
            codexSpread += delta;
            codexFlip = delta > 0 ? 1 : -1;
            Sfx.Play("page_turn");
            windowDirty = true;
        }

        void CodexPage(RectTransform page, int i, List<LoreDef> pages)
        {
            if (i >= pages.Count)
            {
                GInk(page, "~ fim do Códex ~\n\nNovas páginas aparecem conforme a Torre cresce.", 18, InkDim, MID, MID, Vector2.zero, new Vector2(560f, 160f), TextAnchor.MiddleCenter, FontStyle.Italic);
                return;
            }
            var d = pages[i];
            bool open = Lore.IsUnlocked(d.id);
            GInk(page, (d.chapter ?? "").ToUpper(), 14, InkDim, TC, TC, new Vector2(0f, -30f), new Vector2(600f, 20f), TextAnchor.MiddleCenter, FontStyle.Bold);
            GInk(page, open ? d.title : "???", 30, open ? InkCol : InkDim, TC, TC, new Vector2(0f, -54f), new Vector2(620f, 44f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var line = UIKit.Img(page, "linha", UIKit.BandSprite(), new Color(InkCol.r, InkCol.g, InkCol.b, 0.35f));
            UIKit.Place(line.rectTransform, TC, TC, new Vector2(0f, -106f), new Vector2(420f, 2f));
            if (open)
            {
                if (Lore.IsNew(d.id))
                {
                    var nb = GInk(page, "NOVA!", 16, new Color(0.75f, 0.15f, 0.1f, 1f), TR, TR, new Vector2(-26f, -22f), new Vector2(100f, 22f), TextAnchor.MiddleRight, FontStyle.Bold);
                    nb.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
                }
                var body = GInk(page, d.text, 19, InkCol, TC, TC, new Vector2(0f, -124f), new Vector2(600f, 470f), TextAnchor.UpperLeft);
                body.lineSpacing = 1.12f;
                body.verticalOverflow = VerticalWrapMode.Truncate;
            }
            else
            {
                var seal = UIKit.Img(page, "lacre", U.CircleSprite(), new Color(0.55f, 0.18f, 0.12f, 0.85f));
                UIKit.Place(seal.rectTransform, MID, MID, new Vector2(0f, 20f), new Vector2(110f, 110f));
                var sq = UIKit.Txt(seal.rectTransform, "q", "?", 54, new Color(1f, 0.85f, 0.7f, 0.9f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(sq.rectTransform);
                GInk(page, "Página ainda não descoberta.\n" + Lore.HintFor(d), 18, InkDim, MID, MID, new Vector2(0f, -100f), new Vector2(560f, 90f), TextAnchor.MiddleCenter, FontStyle.Italic);
            }
            GInk(page, "— " + (i + 1) + " —", 15, InkDim, BC, BC, new Vector2(0f, 16f), new Vector2(200f, 22f), TextAnchor.MiddleCenter);
        }
    }

    /// <summary>Animação simples de virar página (escala X a partir da lombada).</summary>
    public class CodexFlip : MonoBehaviour
    {
        public float dur = 0.3f;
        float t;

        void Start() { transform.localScale = new Vector3(0.05f, 1f, 1f); }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, dur));
            float e = 1f - (1f - k) * (1f - k);
            transform.localScale = new Vector3(Mathf.Lerp(0.05f, 1f, e), 1f, 1f);
            if (k >= 1f)
            {
                transform.localScale = Vector3.one;
                Destroy(this);
            }
        }
    }
}
