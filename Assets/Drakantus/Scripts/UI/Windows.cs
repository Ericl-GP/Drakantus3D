using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Drakantus
{
    /// <summary>
    /// Janelas modais do HUD: título, classe, habilidades, bolsa, loja, torre, santuário, morte, pausa e diálogo.
    /// Cada janela é recriada ao abrir e depois de cada ação (windowDirty -> LateUpdate).
    /// </summary>
    public partial class HUD
    {
        GameObject modalGo;
        RectTransform modalWin;
        string modalKind = "";
        bool modalClosable;
        Action modalRebuild;
        bool windowDirty;
        bool pausedByUI;
        Coroutine typeCo;
        Text typingText;
        string typingFull;

        public bool HasModal => modalGo != null;
        public string ModalKind => modalGo != null ? modalKind : "";

        enum MStyle { Center, Bottom, Full }

        // ================================================================== base
        RectTransform OpenModal(string kind, string title, Vector2 size, bool closable, MStyle style = MStyle.Center,
                                Color? dimColor = null, bool closeButton = true)
        {
            bool same = modalGo != null && modalKind == kind;
            if (modalGo != null) { modalGo.SetActive(false); Destroy(modalGo); }
            if (typeCo != null) { StopCoroutine(typeCo); typeCo = null; }
            HideTip(null);
            modalRebuild = null;
            modalKind = kind;
            modalClosable = closable;

            var root = UIKit.R(modalLayer, "Janela_" + kind);
            UIKit.Stretch(root);
            modalGo = root.gameObject;
            var dim = UIKit.Img(root, "escuro", U.WhiteSprite(),
                dimColor ?? new Color(0.03f, 0.02f, 0.012f, style == MStyle.Bottom ? 0.25f : 0.55f), true);
            UIKit.Stretch(dim.rectTransform);

            if (style == MStyle.Full)
            {
                var full = UIKit.R(root, "conteudo");
                UIKit.Stretch(full);
                modalWin = full;
                if (!same) { StartCoroutine(PopIn(full, root, 0.3f)); Sfx.Play("ui_open"); }
                return full;
            }

            var win = UIKit.Panel(root, "painel", UIKit.PanelSolid);
            var wr = win.rectTransform;
            if (style == MStyle.Bottom) UIKit.Place(wr, BC, BC, new Vector2(0f, 28f), size);
            else UIKit.Place(wr, MID, MID, Vector2.zero, size);
            var glow = UIKit.Img(wr, "brilho", UIKit.SoftSprite(), new Color(1f, 0.75f, 0.4f, 0.04f));
            UIKit.Place(glow.rectTransform, TC, MID, Vector2.zero, new Vector2(size.x * 0.8f, 180f));

            float top = 22f;
            if (!string.IsNullOrEmpty(title))
            {
                // cabeçalho em faixa (fita de couro com pontas recortadas) e losangos nas pontas do título
                float bw = Mathf.Min(size.x - 120f, Mathf.Max(320f, title.Length * 17f + 140f));
                var band = UIKit.Img(wr, "faixa", UIKit.HeaderSprite(), UIKit.HeaderCol);
                UIKit.Place(band.rectTransform, TC, TC, new Vector2(0f, -12f), new Vector2(bw, 40f));
                var t = UIKit.Txt(band.rectTransform, "titulo", title, 25, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, true);
                UIKit.Stretch(t.rectTransform, 24f, 24f, 0f, 2f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                for (int k = 0; k < 2; k++)
                {
                    var gem = UIKit.Diamond(wr, "gema", UIKit.Bronze, 11f);
                    UIKit.Place(gem.rectTransform, TC, MID, new Vector2((k == 0 ? -1f : 1f) * (bw * 0.5f + 16f), -32f), new Vector2(11f, 11f));
                    var ln = UIKit.Img(wr, "filete", UIKit.BandSprite(), new Color(UIKit.Bronze.r, UIKit.Bronze.g, UIKit.Bronze.b, 0.45f));
                    float lw = Mathf.Max(0f, (size.x - bw) * 0.5f - 60f);
                    UIKit.Place(ln.rectTransform, TC, MID, new Vector2((k == 0 ? -1f : 1f) * (bw * 0.5f + 26f + lw * 0.5f), -32f), new Vector2(lw, 2f));
                }
                top = 64f;
            }
            Text closeX = null;
            if (closable && closeButton)
            {
                // X discreto no canto (vai para o topo da hierarquia depois do conteúdo)
                var x = closeX = UIKit.Txt(wr, "fechar", "×", 30, UIKit.DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
                x.raycastTarget = true;
                UIKit.Place(x.rectTransform, TR, MID, new Vector2(-24f, -24f), new Vector2(34f, 34f));
                x.gameObject.AddComponent<UIHover>().scale = 1.2f;
                x.gameObject.AddComponent<UIClick>().onLeft = () => { Sfx.Play("ui_click"); CloseModal(); };
                UITip.Add(x.gameObject, () => "Fechar", () => "Esc");
            }
            var c = UIKit.R(wr, "conteudo");
            UIKit.Stretch(c, 28f, 28f, top, 22f);
            if (closeX != null) closeX.transform.SetAsLastSibling();
            modalWin = wr;
            if (!same) { StartCoroutine(PopIn(wr, root, 0.17f)); Sfx.Play("ui_open"); }
            return c;
        }

        IEnumerator PopIn(RectTransform win, RectTransform root, float dur)
        {
            var cg = root.gameObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            float t = 0f;
            while (t < dur && win != null && cg != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                cg.alpha = EaseOutCubic(k);
                win.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, EaseOutCubic(k));
                yield return null;
            }
            if (win != null) win.localScale = Vector3.one;
            if (cg != null) cg.alpha = 1f;
        }

        /// <summary>Fecha com fade + leve redução (0,15 s, tempo não escalado); o objeto já saiu do estado do HUD.</summary>
        IEnumerator FadeOutAndDestroy(GameObject go, RectTransform win, float dur)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false;
            float a0 = cg.alpha, t = 0f;
            while (t < dur && go != null)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseInOutCubic(t / dur);
                cg.alpha = Mathf.Lerp(a0, 0f, k);
                if (win != null) win.localScale = Vector3.one * Mathf.Lerp(1f, 0.95f, k);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        public void CloseModal() { CloseModalInternal(false); }

        void ForceClose() { CloseModalInternal(true); }

        void CloseModalInternal(bool force)
        {
            if (modalGo == null) return;
            if (!modalClosable && !force) return;
            var go = modalGo;
            var win = modalWin;
            modalGo = null; modalWin = null; modalKind = ""; modalRebuild = null;
            if (typeCo != null) { StopCoroutine(typeCo); typeCo = null; }
            HideTip(null);
            if (pausedByUI) { Time.timeScale = 1f; pausedByUI = false; }
            if (!force && isActiveAndEnabled)
            {
                go.name += "_fechando";
                StartCoroutine(FadeOutAndDestroy(go, win, 0.15f));
                Sfx.Play("ui_close");
            }
            else
            {
                go.SetActive(false);
                Destroy(go);
            }
        }

        // ------------------------------------------------------------------ helpers de janela
        Text Label(RectTransform p, string s, int size, Color c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sz,
                   TextAnchor a = TextAnchor.MiddleLeft, FontStyle st = FontStyle.Normal)
        {
            var t = UIKit.Txt(p, "texto", s, size, c, a, st);
            UIKit.Place(t.rectTransform, anchor, pivot, pos, sz);
            return t;
        }

        /// <summary>Caixa de ícone com moldura (cor de raridade/classe).</summary>
        Image IconBox(RectTransform p, Sprite icon, Color frame, Vector2 anchor, Vector2 pivot, Vector2 pos, float size, float iconSize, bool legendary = false)
        {
            var f = UIKit.MetalFrame(p, "moldura", frame);
            UIKit.Place(f.rectTransform, anchor, pivot, pos, new Vector2(size, size));
            var inner = UIKit.Round(f.rectTransform, "fundo", UIKit.SlotBg, 1.6f);
            UIKit.Stretch(inner.rectTransform, 4f);
            var ic = UIKit.Img(f.rectTransform, "icone", icon, Color.white);
            UIKit.Place(ic.rectTransform, MID, MID, Vector2.zero, new Vector2(iconSize, iconSize));
            ic.preserveAspect = true;
            ic.enabled = icon != null;
            if (legendary) AddLegendGlow(f);
            return ic;
        }

        static void AddLegendGlow(Image frame)
        {
            var o = frame.gameObject.AddComponent<Outline>();
            o.effectDistance = new Vector2(3f, -3f);
            o.useGraphicAlpha = false;
            var pulse = frame.gameObject.AddComponent<UIPulse>();
            pulse.target = frame; pulse.effect = o;
            pulse.a = UIKit.LegendCol; pulse.b = Color.Lerp(UIKit.LegendCol, Color.white, 0.55f);
            pulse.fxA = new Color(1f, 0.6f, 0.18f, 0.1f); pulse.fxB = new Color(1f, 0.6f, 0.18f, 0.75f);
            pulse.speed = 3.2f;
        }

        static string ItemStats(ItemDef d)
        {
            if (d == null) return "";
            if (!string.IsNullOrEmpty(d.stats)) return d.stats;
            var parts = new List<string>();
            if (d.atk != 0) parts.Add("ATQ +" + d.atk);
            if (d.def != 0) parts.Add("DEF +" + d.def);
            if (d.hp != 0) parts.Add("HP +" + d.hp);
            if (d.mp != 0) parts.Add("MP +" + d.mp);
            if (d.crit != 0) parts.Add("CRIT +" + d.crit + "%");
            if (d.speed != 0) parts.Add("VEL +" + d.speed + "%");
            return string.Join(" · ", parts);
        }

        static string SlotName(string slot) => GameData.SlotLabel(slot);

        static bool Equippable(ItemDef d) => d != null && GameData.IsEquipSlotType(d.slot);

        // ================================================================== tela de título: ver MainMenu.cs

        void StartFromTitle()
        {
            ForceClose();
            lastTotalXp = -1; lastCoins = -1; lastLevel = -1; cachedClass = null;
            OnStateChanged();
            OnLoadoutChanged();
            if (Game.I != null) Game.I.StartGame();
        }

        // ================================================================== classe
        /// <summary>
        /// Escolha da classe inicial (só uma vez). Depois disso a Mestra de Classes abre a evolução.
        /// </summary>
        public void ClassWindow(bool firstTime)
        {
            if (GameState.HasChosenClass) { EvolveWindow(); return; }
            var c = OpenModal("class", "Escolha sua classe", new Vector2(1500f, 820f), !firstTime);
            Label(c, "Escolha com cuidado: a classe é definitiva. No nível 10, a Mestra de Classes da Guilda evolui você para uma de duas especializações.",
                18, UIKit.DimText, TC, TC, Vector2.zero, new Vector2(1400f, 26f), TextAnchor.MiddleCenter);
            var list = GameData.BaseClasses();
            int n = list.Count;
            float cw = 330f, gap = 22f;
            float total = n * cw + (n - 1) * gap;
            for (int i = 0; i < n; i++)
                BuildClassCard(c, list[i], -total / 2f + cw / 2f + i * (cw + gap), "first", true);
            modalRebuild = () => ClassWindow(firstTime);
        }

        /// <summary>Mestra de Classes: evoluir a classe base para uma das duas especializações.</summary>
        public void EvolveWindow()
        {
            var cur = GameState.Class;
            var c = OpenModal("class", "Evolução de Classe", new Vector2(1100f, 860f), true);
            bool can = GameState.CanEvolve(out string why);
            if (GameState.IsEvolved)
            {
                Label(c, "Você já trilhou seu caminho. A evolução é definitiva.", 18, UIKit.DimText, TC, TC, Vector2.zero, new Vector2(1000f, 26f), TextAnchor.MiddleCenter);
                BuildClassCard(c, cur, 0f, "current", true);
            }
            else
            {
                Label(c, can ? "Escolha sua especialização. <b>Esta escolha é definitiva.</b> Você mantém as habilidades de " + cur.name + " e ganha novas."
                             : why + " Veja abaixo os dois caminhos de " + cur.name + ".",
                    18, can ? UIKit.TextCol : UIKit.DimText, TC, TC, Vector2.zero, new Vector2(1040f, 26f), TextAnchor.MiddleCenter);
                var list = GameData.EvolutionsOf(cur.id);
                int n = list.Count;
                float cw = 330f, gap = 60f;
                float total = n * cw + (n - 1) * gap;
                for (int i = 0; i < n; i++)
                    BuildClassCard(c, list[i], -total / 2f + cw / 2f + i * (cw + gap), "evolve", can);
            }
            modalRebuild = EvolveWindow;
        }

        /// <summary>mode: "first" (escolha inicial), "evolve" (evolução), "current" (só mostrar).</summary>
        void BuildClassCard(RectTransform parent, ClassDef cd, float x, string mode, bool enabled)
        {
            Color col = U.Hex(cd.color);
            bool current = mode == "current";
            bool adv = cd.tier >= 2;
            float extra = adv ? 40f : 0f;   // descrição maior nas classes evoluídas
            var card = UIKit.Round(parent, "classe_" + cd.id, UIKit.CardCol, 1f, true);
            var cr = UIKit.Place(card.rectTransform, TC, TC, new Vector2(x, -40f), new Vector2(330f, 640f + extra));
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = current ? col : new Color(col.r, col.g, col.b, 0.35f);
            o.effectDistance = new Vector2(2f, -2f); o.useGraphicAlpha = false;
            card.gameObject.AddComponent<UIHover>().scale = 1.025f;

            var band = UIKit.Round(cr, "faixa", new Color(col.r, col.g, col.b, 0.2f), 1f);
            UIKit.Place(band.rectTransform, TC, TC, Vector2.zero, new Vector2(330f, 150f));
            var halo = UIKit.Img(cr, "halo", UIKit.SoftSprite(), new Color(col.r, col.g, col.b, 0.35f));
            UIKit.Place(halo.rectTransform, TC, MID, new Vector2(0f, -78f), new Vector2(220f, 220f));
            var ring = UIKit.Img(cr, "aro", U.CircleSprite(), col);
            UIKit.Place(ring.rectTransform, TC, TC, new Vector2(0f, -20f), new Vector2(116f, 116f));
            var inner = UIKit.Img(ring.rectTransform, "fundo", U.CircleSprite(), new Color(0.09f, 0.065f, 0.045f, 1f));
            UIKit.Stretch(inner.rectTransform, 5f);
            var ic = UIKit.Img(inner.rectTransform, "icone", U.Icon(cd.icon), Color.white);
            UIKit.Place(ic.rectTransform, MID, MID, Vector2.zero, new Vector2(72f, 72f));
            ic.preserveAspect = true; ic.enabled = ic.sprite != null;

            Label(cr, cd.name, 32, col, TC, TC, new Vector2(0f, -146f), new Vector2(300f, 40f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var dl = Label(cr, cd.desc, adv ? 15 : 17, UIKit.TextCol, TC, TC, new Vector2(0f, -190f), new Vector2(294f, 52f + extra), TextAnchor.UpperCenter);
            dl.verticalOverflow = VerticalWrapMode.Truncate;

            // atributos
            string style = string.IsNullOrEmpty(cd.ranged) ? "corpo a corpo" : "à distância";
            string arms = "";
            if (cd.weapons != null) { var wn = new List<string>(); foreach (var w in cd.weapons) wn.Add(GameData.WeaponTypeName(w)); arms = string.Join(", ", wn.ToArray()); }
            var lines = new List<string>
            {
                Attr("Vida", Mathf.RoundToInt(cd.hp * 100f) + "%"),
                Attr("Ataque", "+" + cd.atk),
                Attr("Defesa", "+" + cd.def),
                Attr("Velocidade", Mathf.RoundToInt(cd.speed * 100f) + "%"),
                Attr("Estilo", style),
            };
            if (arms != "") lines.Add(Attr("Arma", arms));
            if (cd.mpBonus > 0) lines.Add(Attr("Mana", "+" + cd.mpBonus));
            var at = Label(cr, string.Join("\n", lines.ToArray()), 17, UIKit.TextCol, TC, TC, new Vector2(0f, -252f - extra), new Vector2(270f, 170f), TextAnchor.UpperCenter);
            at.lineSpacing = 1.15f;

            Label(cr, adv ? "NOVAS HABILIDADES" : "HABILIDADES", 14, UIKit.DimText, TC, TC, new Vector2(0f, -432f - extra), new Vector2(300f, 20f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var skills = new List<SkillDef>();
            foreach (var s0 in GameData.SkillsOf(cd.id)) if (s0.classId == cd.id) skills.Add(s0);
            int ns = Mathf.Min(6, skills.Count);
            float sw = 44f, sg = 6f;
            float tw = ns * sw + (ns - 1) * sg;
            for (int i = 0; i < ns; i++)
            {
                var s = skills[i];
                var f = UIKit.Round(cr, "hab", col, 1.8f, true);
                UIKit.Place(f.rectTransform, TC, TC, new Vector2(-tw / 2f + sw / 2f + i * (sw + sg), -458f - extra), new Vector2(sw, sw));
                var fi = UIKit.Round(f.rectTransform, "fundo", UIKit.SlotBg, 1.8f);
                UIKit.Stretch(fi.rectTransform, 2f);
                var si = UIKit.Img(f.rectTransform, "icone", U.Icon(s.icon), Color.white);
                UIKit.Place(si.rectTransform, MID, MID, Vector2.zero, new Vector2(32f, 32f));
                si.enabled = si.sprite != null;
                var sk = s;
                UITip.Add(f.gameObject, () => sk.name, () => SkillTipBody(sk) + "\n<color=#9aa3b5>Libera no nível " + sk.level + "</color>");
            }

            Button b;
            string id = cd.id;
            if (current)
            {
                b = UIKit.Btn(cr, "Sua classe", new Vector2(280f, 54f), null);
                UIKit.Disable(b);
            }
            else if (mode == "evolve")
            {
                b = UIKit.Btn(cr, "Evoluir", new Vector2(280f, 54f), () =>
                {
                    if (!GameState.Evolve(id)) { Sfx.Play("ui_error"); return; }
                    Sfx.Play("levelup");
                    ForceClose();
                    QueueBanner(GameData.Class(id).name.ToUpperInvariant(), "Nova especialização! Novas habilidades em [K].", U.Hex(GameData.Class(id).color), "classe", "");
                }, UIKit.Gold, 24);
                if (!enabled) UIKit.Disable(b);
            }
            else
            {
                b = UIKit.Btn(cr, "Escolher", new Vector2(280f, 54f), () =>
                {
                    if (!GameState.SetClass(id)) { Sfx.Play("ui_error"); return; }
                    Sfx.Play("levelup");
                    ForceClose();
                    QueueBanner(GameData.Class(id).name.ToUpperInvariant(), "Sua jornada começa. Habilidades em [K].", U.Hex(GameData.Class(id).color), "classe", "");
                }, UIKit.Gold, 24);
            }
            UIKit.Place(UIKit.RT(b), BC, BC, new Vector2(0f, 22f), new Vector2(280f, 54f));
        }

        static string Attr(string label, string value) => "<color=#9aa3b5>" + label + "</color>   <b>" + value + "</b>";

        // ================================================================== habilidades (K)
        public void SkillsWindow()
        {
            var c = OpenModal("skills", "Habilidades", new Vector2(1580f, 870f), true);
            var cd = GameState.Class;
            Color col = U.Hex(cd.color);
            var skills = GameData.SkillsOf(GameState.ClassId);
            var lo = GameState.CurrentLoadout();

            Label(c, "Arraste um card para a barra à direita ou clique em " + string.Join(" ", GameData.SkillKeys) + " para equipar.",
                17, UIKit.DimText, TL, TL, Vector2.zero, new Vector2(1080f, 24f));

            float availH = 870f - 82f - 84f - 36f;
            int rows = Mathf.Max(1, (skills.Count + 2) / 3);
            float cw = 346f, gap = 16f;
            float ch = Mathf.Min(300f, (availH - (rows - 1) * gap) / rows);
            for (int i = 0; i < skills.Count; i++)
            {
                int ci = i % 3, ri = i / 3;
                BuildSkillCard(c, skills[i], lo, col, new Vector2(ci * (cw + gap), -36f - ri * (ch + gap)), new Vector2(cw, ch));
            }
            if (skills.Count == 0)
                Label(c, "Nenhuma habilidade para esta classe.", 20, UIKit.DimText, TL, TL, new Vector2(0f, -60f), new Vector2(600f, 30f));

            // barra (alvos de soltar)
            var right = UIKit.R(c, "barra");
            UIKit.Place(right, TR, TR, Vector2.zero, new Vector2(420f, 700f));
            Label(right, "BARRA DE HABILIDADES", 16, UIKit.Gold, TL, TL, Vector2.zero, new Vector2(420f, 24f), TextAnchor.MiddleLeft, FontStyle.Bold);
            for (int k = 0; k < 4 && k < lo.Length; k++) BuildBarSlot(right, k, lo[k], col, -36f - k * 136f);
            Label(right, "Dica: soltar uma habilidade sobre outra troca as duas de lugar.", 15, UIKit.DimText, TL, TL,
                new Vector2(0f, -36f - 4 * 136f), new Vector2(420f, 44f), TextAnchor.UpperLeft, FontStyle.Italic);

            modalRebuild = SkillsWindow;
        }

        void BuildSkillCard(RectTransform parent, SkillDef s, string[] lo, Color col, Vector2 pos, Vector2 size)
        {
            bool unlocked = GameState.SkillUnlocked(s.id);
            int eqAt = Array.IndexOf(lo, s.id);
            var card = UIKit.Round(parent, "hab_" + s.id, unlocked ? UIKit.CardCol : new Color(0.07f, 0.055f, 0.042f, 0.95f), 1f, true);
            var cr = UIKit.Place(card.rectTransform, TL, TL, pos, size);
            var o = card.gameObject.AddComponent<Outline>();
            o.useGraphicAlpha = false; o.effectDistance = new Vector2(2f, -2f);
            o.effectColor = eqAt >= 0 ? col : new Color(1f, 1f, 1f, 0.07f);

            var ic = IconBox(cr, U.Icon(s.icon), unlocked ? col : new Color(0.36f, 0.31f, 0.26f, 1f), TL, TL, new Vector2(16f, -16f), 78f, 64f);
            if (!unlocked) ic.color = new Color(0.35f, 0.36f, 0.42f, 1f);
            Label(cr, s.name, 22, unlocked ? Color.white : new Color(0.6f, 0.62f, 0.68f), TL, TL, new Vector2(108f, -16f), new Vector2(size.x - 122f, 30f), TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(cr, "<color=#8fb4ff>Mana " + Num(s.mp) + "</color>  ·  <color=#FFD76A>Recarga " + Num(s.cd) + "s</color>", 16, UIKit.DimText, TL, TL,
                new Vector2(108f, -48f), new Vector2(size.x - 122f, 24f));
            Label(cr, "Nível " + s.level, 14, UIKit.DimText, TL, TL, new Vector2(108f, -72f), new Vector2(size.x - 122f, 20f));
            var desc = Label(cr, s.desc, 16, unlocked ? UIKit.TextCol : UIKit.DimText, TL, TL, new Vector2(16f, -104f), new Vector2(size.x - 32f, size.y - 104f - 62f), TextAnchor.UpperLeft);
            desc.verticalOverflow = VerticalWrapMode.Truncate;

            if (unlocked)
            {
                int nk = GameData.SkillKeys.Length;
                float bw = (size.x - 32f - (nk - 1) * 8f) / nk;
                for (int k = 0; k < nk; k++)
                {
                    int slot = k;
                    string id = s.id;
                    bool on = eqAt == k;
                    var b = UIKit.Btn(cr, GameData.SkillKeys[k] + (on ? " ✓" : ""), new Vector2(bw, 40f), () =>
                    {
                        GameState.SetSlot(slot, id);
                        FlashSlot(slot, UIKit.Gold);
                        windowDirty = true;
                    }, on ? UIKit.Gold : UIKit.BtnCol, 20);
                    UIKit.Place(UIKit.RT(b), BL, BL, new Vector2(16f + k * (bw + 8f), 16f), new Vector2(bw, 40f));
                }
                card.gameObject.AddComponent<SkillDragSource>().skillId = s.id;
            }
            else
            {
                var lockBg = UIKit.Round(cr, "bloqueio", new Color(0f, 0f, 0f, 0.35f), 1.6f);
                UIKit.Place(lockBg.rectTransform, BC, BC, new Vector2(0f, 16f), new Vector2(size.x - 32f, 40f));
                Label(lockBg.rectTransform, "Libera no nível " + s.level, 18, new Color(0.75f, 0.76f, 0.8f), MID, MID, Vector2.zero, new Vector2(size.x - 40f, 36f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        void BuildBarSlot(RectTransform parent, int k, string skillId, Color col, float y)
        {
            var sd = GameData.Skill(skillId);
            var slot = UIKit.Round(parent, "barra_" + k, new Color(0.1f, 0.075f, 0.055f, 1f), 1f, true);
            var sr = UIKit.Place(slot.rectTransform, TL, TL, new Vector2(0f, y), new Vector2(420f, 122f));
            var o = slot.gameObject.AddComponent<Outline>();
            o.useGraphicAlpha = false; o.effectDistance = new Vector2(1.5f, -1.5f);
            o.effectColor = sd != null ? new Color(col.r, col.g, col.b, 0.6f) : new Color(1f, 1f, 1f, 0.12f);
            var hl = UIKit.Round(sr, "alvo", new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.22f), 1f);
            UIKit.Stretch(hl.rectTransform);
            hl.enabled = false;

            var kb = UIKit.Round(sr, "tecla", new Color(0.05f, 0.035f, 0.025f, 1f), 1.8f);
            UIKit.Place(kb.rectTransform, ML, ML, new Vector2(14f, 0f), new Vector2(60f, 60f));
            var ko = kb.gameObject.AddComponent<Outline>();
            ko.effectColor = new Color(1f, 0.84f, 0.42f, 0.5f); ko.effectDistance = new Vector2(1.5f, -1.5f); ko.useGraphicAlpha = false;
            string key = k < GameData.SkillKeys.Length ? GameData.SkillKeys[k] : (k + 1).ToString();
            Label(kb.rectTransform, key, 30, UIKit.Gold, MID, MID, Vector2.zero, new Vector2(60f, 60f), TextAnchor.MiddleCenter, FontStyle.Bold);

            if (sd != null)
            {
                IconBox(sr, U.Icon(sd.icon), col, ML, ML, new Vector2(88f, 0f), 88f, 64f);
                Label(sr, sd.name, 22, Color.white, TL, TL, new Vector2(192f, -28f), new Vector2(216f, 30f), TextAnchor.MiddleLeft, FontStyle.Bold);
                Label(sr, "Mana " + Num(sd.mp) + "  ·  " + Num(sd.cd) + "s", 16, UIKit.DimText, TL, TL, new Vector2(192f, -62f), new Vector2(216f, 24f));
                slot.gameObject.AddComponent<SkillDragSource>().skillId = sd.id;
                var skc = sd;
                UITip.Add(slot.gameObject, () => skc.name, () => SkillTipBody(skc));
            }
            else
            {
                var empty = UIKit.Round(sr, "vazio", new Color(1f, 1f, 1f, 0.05f), 1.6f);
                UIKit.Place(empty.rectTransform, ML, ML, new Vector2(88f, 0f), new Vector2(88f, 88f));
                Label(empty.rectTransform, "+", 40, new Color(1f, 1f, 1f, 0.3f), MID, MID, Vector2.zero, new Vector2(88f, 88f), TextAnchor.MiddleCenter, FontStyle.Bold);
                Label(sr, "Vazio — arraste uma habilidade", 17, UIKit.DimText, ML, ML, new Vector2(192f, 0f), new Vector2(216f, 50f), TextAnchor.MiddleLeft, FontStyle.Italic);
            }
            var drop = slot.gameObject.AddComponent<SkillDropSlot>();
            drop.index = k; drop.highlight = hl;
        }

        // ================================================================== bolsa (I): ver Inventory.cs

        // ================================================================== loja
        string shopTab = "comprar";
        string shopTabCat = "";

        public void ShopWindow(string category)
        {
            string cat = category ?? "";
            if (shopTabCat != cat) { shopTab = "comprar"; shopTabCat = cat; }
            string title = cat == "armas" ? "Armaria" : cat == "armaduras" ? "Armaduras" : cat == "joias" ? "Joalheria" : cat == "pocoes" ? "Poções e Elixires" : "Loja";
            var c = OpenModal("shop", title, new Vector2(1080f, 840f), true);

            var top = UIKit.R(c, "topo");
            UIKit.Place(top, TL, TL, Vector2.zero, new Vector2(1024f, 50f));
            var ci = UIKit.Img(top, "moeda", U.Icon(2, 8), Color.white);
            UIKit.Place(ci.rectTransform, ML, ML, new Vector2(0f, 0f), new Vector2(34f, 34f));
            Label(top, "Suas moedas: <b><color=#FFD76A>" + GameState.P.coins + "</color></b>", 22, UIKit.TextCol, ML, ML, new Vector2(44f, 0f), new Vector2(300f, 40f));
            // abas Comprar / Vender
            bool selling = shopTab == "vender";
            var tb = UIKit.Btn(top, "Comprar", new Vector2(150f, 44f), () => { shopTab = "comprar"; windowDirty = true; }, !selling ? UIKit.Gold : UIKit.BtnCol, 20);
            UIKit.Place(UIKit.RT(tb), MID, MID, new Vector2(-30f, 0f), new Vector2(150f, 44f));
            var ts = UIKit.Btn(top, "Vender", new Vector2(150f, 44f), () => { shopTab = "vender"; windowDirty = true; }, selling ? UIKit.Gold : UIKit.BtnCol, 20);
            UIKit.Place(UIKit.RT(ts), MID, MID, new Vector2(130f, 0f), new Vector2(150f, 44f));
            var bag = UIKit.Btn(top, "Abrir bolsa [I]", new Vector2(200f, 46f), InventoryWindow, UIKit.BtnCol, 20);
            UIKit.Place(UIKit.RT(bag), MR, MR, Vector2.zero, new Vector2(200f, 46f));

            ScrollRect sr;
            var content = UIKit.ScrollArea(c, "lista", out sr);
            UIKit.Place(UIKit.RT(sr), TL, TL, new Vector2(0f, -62f), new Vector2(1024f, 612f));
            var vl = UIKit.VLayout(content.gameObject, 8f, new RectOffset(4, 4, 4, 4));
            vl.childControlHeight = false;
            UIKit.Fit(content.gameObject, false, true);

            int n = 0;
            if (selling)
            {
                var owned = new List<OwnedItem>();
                foreach (var o in GameState.P.items)
                    if (!GameState.IsEquipped(o.uid) && GameState.ShopBuys(cat, GameData.Item(o.itemId))) owned.Add(o);
                owned.Sort((x, y) =>
                {
                    var dx = GameData.Item(x.itemId); var dy = GameData.Item(y.itemId);
                    int sx = Array.IndexOf(GameData.EquipSlots, GameState.DefaultSlotFor(dx)), sy = Array.IndexOf(GameData.EquipSlots, GameState.DefaultSlotFor(dy));
                    if (sx != sy) return sx.CompareTo(sy);
                    return dy.sell.CompareTo(dx.sell);
                });
                foreach (var o in owned) { SellRow(content, o, cat); n++; }
                if (n == 0)
                {
                    string what = cat == "armas" ? "armas" : cat == "armaduras" ? "armaduras, capas ou asas" : cat == "joias" ? "joias" : "itens";
                    var t = UIKit.Txt(content, "vazio", "Você não tem " + what + " para vender (itens equipados não aparecem).", 20, UIKit.DimText, TextAnchor.MiddleCenter, FontStyle.Italic);
                    t.rectTransform.sizeDelta = new Vector2(0f, 80f);
                }
            }
            else
            {
                var list = new List<ItemDef>();
                foreach (var d in GameData.ItemOrder)
                    if (d.category == cat && d.rarity != "lendario" && d.price > 0 && GameData.ClassCanUse(GameState.Class, d)) list.Add(d);
                // agrupa por slot (capacete, peitoral, calça...) e depois por preço
                list.Sort((x, y) =>
                {
                    int sx = Array.IndexOf(GameData.EquipSlots, GameState.DefaultSlotFor(x)), sy = Array.IndexOf(GameData.EquipSlots, GameState.DefaultSlotFor(y));
                    if (sx != sy) return sx.CompareTo(sy);
                    return x.price.CompareTo(y.price);
                });
                foreach (var d in list)
                {
                    ShopRow(content, d);
                    n++;
                }
                if (n == 0)
                {
                    var t = UIKit.Txt(content, "vazio", "Nada à venda no momento.", 20, UIKit.DimText, TextAnchor.MiddleCenter, FontStyle.Italic);
                    t.rectTransform.sizeDelta = new Vector2(0f, 80f);
                }
            }
            modalRebuild = () => ShopWindow(cat);
        }

        void SellRow(RectTransform parent, OwnedItem o, string cat)
        {
            var d = GameData.Item(o.itemId);
            var row = UIKit.Round(parent, "venda_" + o.uid, UIKit.RowCol, 1.2f, true);
            var rr = row.rectTransform;
            rr.sizeDelta = new Vector2(0f, 96f);
            IconBox(rr, U.Icon(d.icon), UIKit.RarityBorder(d), ML, ML, new Vector2(12f, 0f), 76f, 60f);
            Label(rr, d.name + (o.qty > 1 ? "  <color=#9aa3b5>x" + o.qty + "</color>" : ""), 22, GameData.RarityColor(d), TL, TL, new Vector2(104f, -12f), new Vector2(480f, 28f), TextAnchor.MiddleLeft, FontStyle.Bold);
            string extra = d.slot == "arma" && !string.IsNullOrEmpty(d.wtype) ? " · " + GameData.WeaponTypeName(d.wtype) : "";
            Label(rr, ItemStats(d) + "   <color=#9aa3b5><size=14>" + GameData.RarityName(d) + " · " + SlotName(d.slot) + extra + "</size></color>", 17, UIKit.Gold, TL, TL,
                new Vector2(104f, -42f), new Vector2(560f, 24f));
            var pc = UIKit.Img(rr, "moeda", U.Icon(2, 8), Color.white);
            UIKit.Place(pc.rectTransform, MR, MR, new Vector2(-330f, 6f), new Vector2(28f, 28f));
            Label(rr, "+" + d.sell + (o.qty > 1 ? " cada" : ""), 22, UIKit.Gold, MR, ML, new Vector2(-324f, 6f), new Vector2(130f, 32f), TextAnchor.MiddleLeft, FontStyle.Bold);
            int uid = o.uid;
            var b = UIKit.Btn(rr, o.qty > 1 ? "Vender 1" : "Vender", new Vector2(150f, 52f), () =>
            {
                var cur = GameState.Owned(uid);
                if (cur == null) return;
                if (cur.qty > 1) GameState.Sell(uid); else GameState.SellAll(uid);
                Sfx.Play("coin");
                windowDirty = true;
            }, UIKit.Gold, 21);
            UIKit.Place(UIKit.RT(b), MR, MR, new Vector2(o.qty > 1 ? -176f : -16f, 0f), new Vector2(150f, 52f));
            if (o.qty > 1)
            {
                var b2 = UIKit.Btn(rr, "Todos", new Vector2(150f, 52f), () =>
                {
                    if (GameState.Owned(uid) == null) return;
                    GameState.SellAll(uid);
                    Sfx.Play("coin");
                    windowDirty = true;
                }, UIKit.BtnCol, 21);
                UIKit.Place(UIKit.RT(b2), MR, MR, new Vector2(-16f, 0f), new Vector2(150f, 52f));
            }
            UITip.Add(row.gameObject, () => d.name, () => ItemTipCompare(d, uid));
        }

        void ShopRow(RectTransform parent, ItemDef d)
        {
            var row = UIKit.Round(parent, "loja_" + d.id, UIKit.RowCol, 1.2f, true);
            var rr = row.rectTransform;
            rr.sizeDelta = new Vector2(0f, 96f);
            IconBox(rr, U.Icon(d.icon), UIKit.RarityBorder(d), ML, ML, new Vector2(12f, 0f), 76f, 60f);
            Label(rr, d.name, 22, GameData.RarityColor(d), TL, TL, new Vector2(104f, -12f), new Vector2(480f, 28f), TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(rr, ItemStats(d) + "   <color=#9aa3b5><size=14>" + GameData.RarityName(d) + " · " + SlotName(d.slot) + "</size></color>", 17, UIKit.Gold, TL, TL,
                new Vector2(104f, -42f), new Vector2(560f, 24f));
            var desc = Label(rr, d.description, 15, UIKit.DimText, TL, TL, new Vector2(104f, -66f), new Vector2(560f, 22f));
            desc.verticalOverflow = VerticalWrapMode.Truncate;

            int have = GameState.Count(d.id);
            if (have > 0)
                Label(rr, d.slot == "consumivel" ? "Você tem: " + have : "Você já possui", 14, new Color(0.6f, 0.9f, 0.6f), MR, MR, new Vector2(-196f, -26f), new Vector2(160f, 20f), TextAnchor.MiddleRight);

            bool afford = GameState.P.coins >= d.price;
            var pc = UIKit.Img(rr, "moeda", U.Icon(2, 8), Color.white);
            UIKit.Place(pc.rectTransform, MR, MR, new Vector2(-300f, 6f), new Vector2(28f, 28f));
            Label(rr, d.price.ToString(), 24, afford ? UIKit.Gold : new Color(1f, 0.45f, 0.4f), MR, ML, new Vector2(-294f, 6f), new Vector2(110f, 32f), TextAnchor.MiddleLeft, FontStyle.Bold);

            string id = d.id;
            var b = UIKit.Btn(rr, "Comprar", new Vector2(160f, 52f), () =>
            {
                if (GameState.Buy(id)) Sfx.Play("coin");
                else Sfx.Play("ui_error");
                windowDirty = true;
            }, UIKit.Gold, 22);
            if (!afford) UIKit.Disable(b);
            UIKit.Place(UIKit.RT(b), MR, MR, new Vector2(-16f, 0f), new Vector2(160f, 52f));
            UITip.Add(row.gameObject, () => d.name, () => ItemTipCompare(d, -1));
        }

        // ================================================================== torre
        public void TowerWindow()
        {
            var c = OpenModal("tower", "Torre de Aster", new Vector2(1120f, 680f), true);
            modalRebuild = TowerWindow;
            if (!GameState.P.registered)
            {
                Label(c, "Somente aventureiros registrados podem entrar na Torre.", 26, UIKit.TextCol, MID, MID, new Vector2(0f, 40f), new Vector2(1000f, 40f), TextAnchor.MiddleCenter, FontStyle.Bold);
                Label(c, "Fale com a recepcionista da Guilda para fazer seu registro.", 20, UIKit.DimText, MID, MID, new Vector2(0f, -10f), new Vector2(1000f, 30f), TextAnchor.MiddleCenter);
                return;
            }
            Label(c, "Cada andar é uma instância só sua. Derrote as criaturas, alcance o santuário e vença o chefe.", 18, UIKit.DimText,
                TC, TC, Vector2.zero, new Vector2(1060f, 26f), TextAnchor.MiddleCenter);
            FloorCard(c, 1, "Catacumbas Esquecidas", "Criptas de pedra cheias de esqueletos. No trono, o Rei Esqueleto Grumak aguarda.", 1, "tower_f1", true, -266f);
            bool f2 = GameState.P.floorsCleared != null && GameState.P.floorsCleared.Contains(1);
            FloorCard(c, 2, "Cripta do Necromante", "Velas roxas, sarcófagos e magos. O Necromante Ancião ataca de longe.", 4, "tower_f2", f2, 266f);

            // andares criados no Criador de Andares
            var extra = new List<FloorEntry>();
            foreach (var fe in FloorRegistry.List()) if (!fe.builtIn) extra.Add(fe);
            extra.Sort((a, b) => a.floor.CompareTo(b.floor));
            for (int i = 0; i < extra.Count && i < 4; i++)
            {
                var fe = extra[i];
                bool unlocked = GameState.P.floorsCleared != null && GameState.P.floorsCleared.Contains(fe.floor - 1);
                string label = "Andar " + fe.floor + ": " + fe.name + "  (Nv " + fe.recommendedLevel + "+)";
                string mapId = fe.id;
                var b = UIKit.Btn(c, unlocked ? label : label + "  [bloqueado]", new Vector2(250f, 50f), () =>
                {
                    ForceClose();
                    if (Game.I != null) Game.I.EnterTower(mapId);
                }, unlocked ? UIKit.Gold : new Color(0.35f, 0.36f, 0.42f), 14);
                if (!unlocked) UIKit.Disable(b);
                UIKit.Place(UIKit.RT(b), BC, BC, new Vector2(-405f + i * 270f, 30f), new Vector2(250f, 50f));
            }
        }

        void FloorCard(RectTransform parent, int floor, string name, string desc, int recLevel, string map, bool unlocked, float x)
        {
            var card = UIKit.Round(parent, "andar" + floor, unlocked ? UIKit.CardCol : new Color(0.07f, 0.055f, 0.042f, 0.95f), 1f, true);
            var cr = UIKit.Place(card.rectTransform, TC, TC, new Vector2(x, -44f), new Vector2(510f, 440f));
            var o = card.gameObject.AddComponent<Outline>();
            o.useGraphicAlpha = false; o.effectDistance = new Vector2(2f, -2f);
            o.effectColor = unlocked ? new Color(1f, 0.84f, 0.42f, 0.45f) : new Color(1f, 1f, 1f, 0.08f);
            if (unlocked) card.gameObject.AddComponent<UIHover>().scale = 1.02f;

            var seal = UIKit.Img(cr, "selo", U.CircleSprite(), unlocked ? UIKit.Gold : new Color(0.35f, 0.36f, 0.42f));
            UIKit.Place(seal.rectTransform, TC, TC, new Vector2(0f, -22f), new Vector2(90f, 90f));
            var si = UIKit.Img(seal.rectTransform, "in", U.CircleSprite(), new Color(0.1f, 0.08f, 0.05f, 1f));
            UIKit.Stretch(si.rectTransform, 4f);
            Label(si.rectTransform, floor.ToString(), 44, unlocked ? UIKit.Gold : UIKit.DimText, MID, MID, Vector2.zero, new Vector2(80f, 80f), TextAnchor.MiddleCenter, FontStyle.Bold);

            Label(cr, "ANDAR " + floor, 15, UIKit.DimText, TC, TC, new Vector2(0f, -122f), new Vector2(460f, 22f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Label(cr, name, 28, unlocked ? Color.white : UIKit.DimText, TC, TC, new Vector2(0f, -146f), new Vector2(460f, 36f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Label(cr, desc, 18, unlocked ? UIKit.TextCol : UIKit.DimText, TC, TC, new Vector2(0f, -192f), new Vector2(440f, 80f), TextAnchor.UpperCenter);
            bool under = GameState.P.level < recLevel;
            Label(cr, "Nível recomendado: " + recLevel + "+" + (under ? "  (você: " + GameState.P.level + ")" : ""), 17,
                under ? new Color(1f, 0.55f, 0.45f) : new Color(0.6f, 0.9f, 0.6f), TC, TC, new Vector2(0f, -280f), new Vector2(460f, 24f), TextAnchor.MiddleCenter);
            if (GameState.P.floorsCleared != null && GameState.P.floorsCleared.Contains(floor))
                Label(cr, "✓ Concluído", 17, UIKit.Gold, TC, TC, new Vector2(0f, -308f), new Vector2(460f, 24f), TextAnchor.MiddleCenter, FontStyle.Bold);

            Button b;
            if (unlocked)
            {
                b = UIKit.Btn(cr, "Entrar", new Vector2(300f, 56f), () =>
                {
                    ForceClose();
                    if (Game.I != null) Game.I.EnterTower(map);
                }, UIKit.Gold, 24);
            }
            else
            {
                Label(cr, "Derrote o chefe do Andar " + (floor - 1) + " para liberar.", 16, new Color(1f, 0.55f, 0.45f), BC, BC, new Vector2(0f, 88f), new Vector2(460f, 24f), TextAnchor.MiddleCenter, FontStyle.Italic);
                b = UIKit.Btn(cr, "Bloqueado", new Vector2(300f, 56f), null);
                UIKit.Disable(b);
            }
            UIKit.Place(UIKit.RT(b), BC, BC, new Vector2(0f, 24f), new Vector2(300f, 56f));
        }

        // ================================================================== santuário
        public void SanctuaryWindow(bool hasNext)
        {
            var c = OpenModal("sanctuary", "Santuário", new Vector2(700f, hasNext ? 520f : 450f), true, MStyle.Center, null, false);
            var halo = UIKit.Img(c, "halo", UIKit.SoftSprite(), new Color(0.45f, 0.95f, 1f, 0.18f));
            UIKit.Place(halo.rectTransform, TC, MID, new Vector2(0f, -40f), new Vector2(420f, 200f));
            Label(c, "A luz do santuário restaura sua vida e sua mana.", 22, UIKit.TextCol, TC, TC, new Vector2(0f, -6f), new Vector2(640f, 32f), TextAnchor.MiddleCenter);
            Label(c, hasNext ? "O caminho para o próximo andar está aberto." : "Este é o topo conhecido da Torre... por enquanto.", 17, UIKit.DimText,
                TC, TC, new Vector2(0f, -42f), new Vector2(640f, 26f), TextAnchor.MiddleCenter, FontStyle.Italic);
            float y = -96f;
            if (hasNext)
            {
                var bn = UIKit.Btn(c, "Subir ao próximo andar", new Vector2(440f, 58f), () =>
                {
                    ForceClose();
                    if (Game.I != null) Game.I.NextFloor();
                }, UIKit.Gold, 24);
                UIKit.Place(UIKit.RT(bn), TC, TC, new Vector2(0f, y), new Vector2(440f, 58f));
                y -= 72f;
            }
            var bt = UIKit.Btn(c, "Voltar à Praça", new Vector2(440f, 54f), () =>
            {
                ForceClose();
                if (Game.I != null) Game.I.LeaveTower();
            }, UIKit.BtnCol, 22);
            UIKit.Place(UIKit.RT(bt), TC, TC, new Vector2(0f, y), new Vector2(440f, 54f));
            y -= 68f;
            var bs = UIKit.Btn(c, "Ficar", new Vector2(440f, 50f), CloseModal, new Color(0.16f, 0.12f, 0.085f, 1f), 20);
            UIKit.Place(UIKit.RT(bs), TC, TC, new Vector2(0f, y), new Vector2(440f, 50f));
        }

        // ================================================================== morte
        static readonly string[] DeathTips =
        {
            "Dica: segure Shift para defender e aperte F no instante do golpe para aparar.",
            "Dica: a marca vermelha no chão mostra onde o golpe inimigo vai cair. Saia dela!",
            "Dica: Q faz uma esquiva invulnerável. Use para atravessar ataques.",
            "Dica: compre poções na Praça e use com 1 (vida) e 2 (mana).",
            "Dica: combos de 10, 25, 50 e 100 acertos dão XP bônus.",
        };

        public void DeathWindow()
        {
            // closable = true para Game.Respawn() poder fechar; sem botão Fechar (Esc é ignorado pelo Game enquanto morto)
            var c = OpenModal("death", "", new Vector2(780f, 400f), true, MStyle.Center, new Color(0.18f, 0f, 0f, 0.55f), false);
            var t = UIKit.Txt(c, "titulo", "Você caiu...", 64, new Color(0.95f, 0.32f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Place(t.rectTransform, TC, TC, new Vector2(0f, -10f), new Vector2(720f, 80f));
            Label(c, "A Torre não perdoa os descuidados. Levante-se e tente outra vez.", 20, UIKit.TextCol, TC, TC, new Vector2(0f, -100f), new Vector2(700f, 30f), TextAnchor.MiddleCenter);
            Label(c, DeathTips[UnityEngine.Random.Range(0, DeathTips.Length)], 16, UIKit.DimText, TC, TC, new Vector2(0f, -140f), new Vector2(700f, 44f), TextAnchor.UpperCenter, FontStyle.Italic);
            var b = UIKit.Btn(c, "Levantar", new Vector2(320f, 62f), () =>
            {
                ForceClose();
                if (Game.I != null) Game.I.Respawn();
            }, UIKit.Gold, 26);
            UIKit.Place(UIKit.RT(b), BC, BC, new Vector2(0f, 8f), new Vector2(320f, 62f));
        }

        // ================================================================== pausa
        public void PauseWindow()
        {
            var c = OpenModal("pause", "Pausado", new Vector2(460f, 470f), true, MStyle.Center, null, false);
            if (!pausedByUI) { pausedByUI = true; Time.timeScale = 0f; }
            float y = -6f;
            var b1 = UIKit.Btn(c, "Continuar", new Vector2(340f, 50f), CloseModal, UIKit.Gold, 22);
            UIKit.Place(UIKit.RT(b1), TC, TC, new Vector2(0f, y), new Vector2(340f, 50f)); y -= 62f;
            var b2 = UIKit.Btn(c, "Salvar jogo", new Vector2(340f, 46f), () => { GameState.Save(); Toast("Jogo salvo."); }, UIKit.BtnCol, 20);
            UIKit.Place(UIKit.RT(b2), TC, TC, new Vector2(0f, y), new Vector2(340f, 46f)); y -= 56f;
            var b4 = UIKit.Btn(c, "Opções", new Vector2(340f, 46f), () => OptionsWindow(false), UIKit.BtnCol, 20);
            UIKit.Place(UIKit.RT(b4), TC, TC, new Vector2(0f, y), new Vector2(340f, 46f)); y -= 70f;

            var help = Label(c,
                "<color=#FFD76A>WASD</color> mover  <color=#FFD76A>Clique</color> atacar  <color=#FFD76A>Q</color> esquiva\n" +
                "<color=#FFD76A>Shift</color> defender  <color=#FFD76A>F</color> aparar  <color=#FFD76A>Tab</color> travar alvo\n" +
                "<color=#FFD76A>Z X C V</color> habilidades  <color=#FFD76A>R</color> ultimate  <color=#FFD76A>1 2</color> poções\n" +
                "<color=#FFD76A>E</color> interagir  <color=#FFD76A>I</color> bolsa  <color=#FFD76A>K</color> habilidades",
                15, UIKit.DimText, TC, TC, new Vector2(0f, y), new Vector2(400f, 90f), TextAnchor.UpperCenter);
            help.lineSpacing = 1.15f;

            var b3 = UIKit.Btn(c, "Sair do jogo", new Vector2(340f, 44f), QuitGame, UIKit.DangerCol, 19);
            UIKit.Place(UIKit.RT(b3), BC, BC, new Vector2(0f, 0f), new Vector2(340f, 44f));
        }

        // ================================================================== diálogo
        public void Dialog(string npcName, string role, string text, List<(string label, System.Action action)> buttons)
        {
            var c = OpenModal("dialog", "", new Vector2(1240f, 310f), true, MStyle.Bottom, null, false);
            var nm = Label(c, npcName ?? "", 30, UIKit.Gold, TL, TL, new Vector2(0f, 2f), new Vector2(800f, 38f), TextAnchor.MiddleLeft, FontStyle.Bold);
            nm.horizontalOverflow = HorizontalWrapMode.Overflow;
            Label(c, role ?? "", 17, UIKit.DimText, TL, TL, new Vector2(0f, -36f), new Vector2(800f, 24f), TextAnchor.MiddleLeft, FontStyle.Italic);
            var div = UIKit.Img(c, "divisor", UIKit.BandSprite(), new Color(1f, 0.84f, 0.42f, 0.35f));
            UIKit.Place(div.rectTransform, TL, TL, new Vector2(0f, -66f), new Vector2(700f, 2f));
            var body = Label(c, "", 23, UIKit.TextCol, TL, TL, new Vector2(0f, -80f), new Vector2(1184f, 110f), TextAnchor.UpperLeft);
            body.lineSpacing = 1.12f;

            // botões da direita para a esquerda: "Até mais" sempre por último
            float x = 0f;
            bool hasBye = false;
            if (buttons != null)
                foreach (var bt in buttons)
                    if (bt.label != null && bt.label.StartsWith("Até", StringComparison.OrdinalIgnoreCase)) hasBye = true;
            if (!hasBye)
            {
                var bye = UIKit.Btn(c, "Até mais", new Vector2(170f, 50f), CloseModal, UIKit.BtnCol, 20);
                UIKit.Place(UIKit.RT(bye), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-x, 0f), new Vector2(170f, 50f));
                x += 170f + 12f;
            }
            if (buttons != null)
            {
                for (int i = buttons.Count - 1; i >= 0; i--)
                {
                    string label = buttons[i].label ?? "";
                    System.Action act = buttons[i].action;
                    bool isBye = label.StartsWith("Até", StringComparison.OrdinalIgnoreCase);
                    float w = Mathf.Clamp(label.Length * 12f + 56f, 170f, 360f);
                    var b = UIKit.Btn(c, label, new Vector2(w, 50f), () =>
                    {
                        ForceClose();
                        act?.Invoke();
                    }, isBye ? UIKit.BtnCol : i == 0 ? UIKit.Gold : new Color(0.34f, 0.24f, 0.15f, 1f), 20);
                    UIKit.Place(UIKit.RT(b), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-x, 0f), new Vector2(w, 50f));
                    x += w + 12f;
                }
            }

            var click = modalWin.gameObject.AddComponent<UIClick>();
            click.onLeft = FinishTyping;
            string full = text ?? "";
            if (full.IndexOf('<') >= 0) body.text = full;
            else typeCo = StartCoroutine(TypeText(body, full));
        }

        IEnumerator TypeText(Text t, string full)
        {
            typingText = t; typingFull = full;
            t.text = "";
            float shown = 0f;
            while (t != null && shown < full.Length)
            {
                shown += Time.unscaledDeltaTime * 60f;
                int n = Mathf.Min(full.Length, (int)shown);
                t.text = full.Substring(0, n);
                yield return null;
            }
            if (t != null) t.text = full;
            typeCo = null;
        }

        void FinishTyping()
        {
            if (typeCo == null) return;
            StopCoroutine(typeCo);
            typeCo = null;
            if (typingText != null) typingText.text = typingFull;
        }
    }
}
