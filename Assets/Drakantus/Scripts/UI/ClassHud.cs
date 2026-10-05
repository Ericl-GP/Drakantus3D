using UnityEngine;
using UnityEngine.UI;

namespace Drakantus
{
    /// <summary>
    /// [Classes] Widget compacto de recurso da classe evoluída (partial do HUD), logo abaixo do retrato, à direita dos ícones de efeito:
    ///   Pistoleiro: balas (pips) + barra de recarga [T] · The Guard: barra de Vingança · Berserker: FÚRIA (e ícones vermelhos)
    ///   Assassino: furtividade · qualquer classe: tempo do buff ativo mais longo (Foco, Sobrecarga, Bênção...).
    /// </summary>
    public partial class HUD
    {
        const int MaxPips = 16;
        RectTransform chRoot, chBarRt, chPipRow, chSubRow;
        Text chLabel, chValue, chSub;
        Image chFill, chSubFill;
        readonly Image[] chPips = new Image[MaxPips];
        static readonly Color FuryRed = new Color(1f, 0.32f, 0.26f, 1f);
        static readonly Color VengeCol = new Color(0.5f, 0.72f, 1f, 1f);

        void BuildClassHud()
        {
            chRoot = UIKit.R(hudRoot, "RecursoClasse");
            UIKit.Place(chRoot, TL, TL, new Vector2(204f, -134f), new Vector2(236f, 40f));

            var bg = UIKit.Round(chRoot, "fundo", new Color(0.06f, 0.045f, 0.032f, 0.86f), 3f);
            UIKit.Place(bg.rectTransform, TL, TL, Vector2.zero, new Vector2(236f, 22f));
            UIKit.Frame(bg.rectTransform, null, true, -1f);

            chLabel = UIKit.Txt(bg.rectTransform, "rotulo", "", 11, UIKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UIKit.Place(chLabel.rectTransform, ML, ML, new Vector2(7f, 0f), new Vector2(70f, 18f));
            chLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            // barra (Vingança / recarga / furtividade)
            var barBg = UIKit.Round(bg.rectTransform, "barra_fundo", new Color(0.02f, 0.015f, 0.01f, 0.95f), 6f);
            chBarRt = UIKit.Place(barBg.rectTransform, ML, ML, new Vector2(78f, 0f), new Vector2(122f, 10f));
            chFill = UIKit.Img(chBarRt, "barra", UIKit.BarFillSprite(), VengeCol);
            UIKit.Stretch(chFill.rectTransform, 1f);
            chFill.type = Image.Type.Filled;
            chFill.fillMethod = Image.FillMethod.Horizontal;
            chFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            // balas
            chPipRow = UIKit.R(bg.rectTransform, "balas");
            UIKit.Place(chPipRow, ML, ML, new Vector2(78f, 0f), new Vector2(122f, 12f));
            for (int i = 0; i < MaxPips; i++)
            {
                chPips[i] = UIKit.Round(chPipRow, "bala" + i, UIKit.Gold, 8f);
                UIKit.Place(chPips[i].rectTransform, ML, ML, new Vector2(i * 7.6f, 0f), new Vector2(5f, 11f));
            }

            chValue = UIKit.Txt(bg.rectTransform, "valor", "", 11, UIKit.TextCol, TextAnchor.MiddleRight, FontStyle.Bold, true);
            UIKit.Place(chValue.rectTransform, MR, MR, new Vector2(-6f, 0f), new Vector2(40f, 18f));
            chValue.horizontalOverflow = HorizontalWrapMode.Overflow;

            // linha do buff ativo
            chSubRow = UIKit.R(chRoot, "buff");
            UIKit.Place(chSubRow, TL, TL, new Vector2(0f, -24f), new Vector2(236f, 14f));
            var sbg = UIKit.Round(chSubRow, "fundo", new Color(0.04f, 0.03f, 0.02f, 0.75f), 6f);
            UIKit.Place(sbg.rectTransform, BL, BL, new Vector2(0f, 0f), new Vector2(236f, 3f));
            chSubFill = UIKit.Img(sbg.rectTransform, "tempo", UIKit.BarFillSprite(), UIKit.Gold);
            UIKit.Stretch(chSubFill.rectTransform);
            chSubFill.type = Image.Type.Filled;
            chSubFill.fillMethod = Image.FillMethod.Horizontal;
            chSub = UIKit.Txt(chSubRow, "texto", "", 11, UIKit.TextCol, TextAnchor.UpperLeft, FontStyle.Bold, true);
            UIKit.Place(chSub.rectTransform, TL, TL, new Vector2(4f, 1f), new Vector2(232f, 12f));
            chSub.horizontalOverflow = HorizontalWrapMode.Overflow;

            bg.raycastTarget = true;
            UITip.Add(bg.gameObject, ClassTipTitle, ClassTipBody);
            chRoot.gameObject.SetActive(false);
        }

        static string ClassTipTitle()
        {
            switch (GameState.Class.mechanic)
            {
                case "ammo": return "Munição";
                case "vengeance": return "Vingança";
                case "frenzy": return "Fúria";
                case "stealth": return "Furtividade";
                default: return GameState.Class.name;
            }
        }

        static string ClassTipBody()
        {
            switch (GameState.Class.mechanic)
            {
                case "ammo": return "Cada tiro gasta 1 bala. Sem balas recarrega sozinho; [T] recarrega antes.";
                case "vengeance": return "Todo dano recebido enche o medidor (até 60% da vida). Retribuição e Sentença usam e gastam a Vingança.";
                case "frenzy": return "Com 30% de vida ou menos você entra em Fúria: ataques mais rápidos e habilidades furiosas que custam vida e roubam vida.";
                case "stealth": return "Invisível, os inimigos perdem você. O próximo golpe é crítico x2,5. Golpes pelas costas: +50% de crítico.";
                default: return "";
            }
        }

        void UpdateClassHud(Player p, float udt)
        {
            if (chRoot == null || p == null) return;
            string mech = GameState.Class.mechanic ?? "";
            bool main = true, bar = false, pips = false;
            string label = "", value = "";
            Color lc = UIKit.Gold, fc = VengeCol;
            float fill = 0f;
            switch (mech)
            {
                case "ammo":
                    pips = !p.Reloading;
                    bar = p.Reloading;
                    label = p.Reloading ? "RECARGA" : "BALAS";
                    value = p.Reloading ? p.ReloadLeft.ToString("0.0") + "s" : p.Ammo + "  [T]";
                    fill = p.Reloading ? 1f - Mathf.Clamp01(p.ReloadLeft / Player.ReloadTime) : 0f;
                    fc = new Color(1f, 0.75f, 0.35f, 1f);
                    break;
                case "vengeance":
                    bar = true;
                    label = "VINGANÇA";
                    fill = p.VengeanceMax > 0f ? Mathf.Clamp01(p.Vengeance / p.VengeanceMax) : 0f;
                    value = Mathf.RoundToInt(p.Vengeance).ToString();
                    fc = fill >= 0.999f ? Color.Lerp(VengeCol, Color.white, 0.4f + 0.3f * Mathf.Sin(Time.unscaledTime * 8f)) : VengeCol;
                    break;
                case "frenzy":
                {
                    bar = true;
                    float hpF = GameState.maxHp > 0f ? GameState.P.hp / GameState.maxHp : 1f;
                    if (p.Frenzy)
                    {
                        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
                        label = "FÚRIA!";
                        lc = Color.Lerp(FuryRed, new Color(1f, 0.7f, 0.4f), k);
                        fill = p.RageLeft > 0f ? Mathf.Clamp01(p.RageLeft / 10f) : 1f;
                        value = p.RageLeft > 0f ? Mathf.CeilToInt(p.RageLeft) + "s" : "";
                        fc = Color.Lerp(FuryRed, new Color(1f, 0.55f, 0.3f), k);
                    }
                    else
                    {
                        label = "FÚRIA";
                        lc = new Color(0.75f, 0.55f, 0.5f, 1f);
                        // quanto falta para a Fúria: barra enche conforme a vida cai até 30%
                        fill = Mathf.Clamp01((1f - hpF) / (1f - Player.FrenzyEnter));
                        value = "30%";
                        fc = new Color(0.6f, 0.2f, 0.16f, 1f);
                    }
                    break;
                }
                case "stealth":
                    bar = true;
                    label = p.Stealthed ? "FURTIVO" : "VISÍVEL";
                    lc = p.Stealthed ? new Color(0.75f, 0.6f, 1f, 1f) : UIKit.DimText;
                    fill = p.Stealthed ? Mathf.Clamp01(p.StealthLeft / 5f) : 0f;
                    value = p.Stealthed ? p.StealthLeft.ToString("0.0") + "s" : "";
                    fc = new Color(0.6f, 0.42f, 1f, 1f);
                    break;
                default:
                    main = false;
                    break;
            }

            bool sub = p.TopBuff(out SkillDef bs, out float bl, out float bt);
            bool show = main || sub;
            if (chRoot.gameObject.activeSelf != show) chRoot.gameObject.SetActive(show);
            if (!show) { TintFurySlots(p); return; }

            var bgGo = chLabel.rectTransform.parent.gameObject;
            if (bgGo.activeSelf != main) bgGo.SetActive(main);
            if (main)
            {
                UIKit.Set(chLabel, label);
                if (chLabel.color != lc) chLabel.color = lc;
                UIKit.Set(chValue, value);
                if (chBarRt.gameObject.activeSelf != bar) chBarRt.gameObject.SetActive(bar);
                if (bar)
                {
                    if (Mathf.Abs(chFill.fillAmount - fill) > 0.002f) chFill.fillAmount = fill;
                    if (chFill.color != fc) chFill.color = fc;
                }
                if (chPipRow.gameObject.activeSelf != pips) chPipRow.gameObject.SetActive(pips);
                if (pips)
                {
                    int max = Mathf.Min(MaxPips, p.AmmoMax);
                    float w = max > 0 ? 122f / max : 7f;
                    for (int i = 0; i < MaxPips; i++)
                    {
                        var im = chPips[i];
                        bool on = i < max;
                        if (im.gameObject.activeSelf != on) im.gameObject.SetActive(on);
                        if (!on) continue;
                        im.rectTransform.anchoredPosition = new Vector2(i * w, 0f);
                        im.rectTransform.sizeDelta = new Vector2(Mathf.Max(3f, w - 2.5f), 11f);
                        Color c = i < p.Ammo ? UIKit.Gold : new Color(0.25f, 0.2f, 0.15f, 0.9f);
                        if (im.color != c) im.color = c;
                    }
                }
            }

            if (chSubRow.gameObject.activeSelf != sub) chSubRow.gameObject.SetActive(sub);
            if (sub)
            {
                UIKit.Set(chSub, bs.name + "  " + Mathf.CeilToInt(bl) + "s");
                Color bc = U.Hex(bs.color);
                if (chSub.color != bc) chSub.color = Color.Lerp(bc, Color.white, 0.35f);
                chSubFill.fillAmount = bt > 0f ? Mathf.Clamp01(bl / bt) : 0f;
                if (chSubFill.color != bc) chSubFill.color = bc;
            }
            TintFurySlots(p);
        }

        /// <summary>Na Fúria, os slots com versão furiosa ficam vermelhos (custam vida, não mana).</summary>
        bool chFuryWas;
        void TintFurySlots(Player p)
        {
            if (!p.Frenzy)
            {
                if (chFuryWas)
                    for (int i = 0; i < skillSlots.Length; i++)
                        if (skillSlots[i] != null && skillSlots[i].glowT <= 0f) skillSlots[i].glow.color = new Color(1f, 1f, 1f, 0f);
                chFuryWas = false;
                return;
            }
            chFuryWas = true;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int i = 0; i < skillSlots.Length; i++)
            {
                var sd = slotSkills[i];
                var s = skillSlots[i];
                if (sd == null || s == null || string.IsNullOrEmpty(sd.alt)) continue;
                p.cooldowns.TryGetValue(sd.id, out float left);
                Color c = left > 0.01f ? new Color(0.6f, 0.3f, 0.28f, 1f) : Color.Lerp(new Color(1f, 0.45f, 0.4f, 1f), new Color(1f, 0.7f, 0.6f, 1f), k);
                s.icon.color = c;
                if (s.glowT <= 0f) s.glow.color = new Color(1f, 0.2f, 0.12f, 0.12f + 0.12f * k);
            }
        }
    }
}
