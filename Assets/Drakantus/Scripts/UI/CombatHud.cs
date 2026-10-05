using UnityEngine;
using UnityEngine.UI;

namespace Drakantus
{
    /// <summary>
    /// Elementos de combate da HUD (partial do HUD):
    /// (a) anel pequeno no slot de ataque com a carga / recarga do ataque carregado (Player.charging, chargeLevel,
    ///     ChargedCooldownInfo);
    /// (b) contador de combo compacto lido das passivas (ComboPassives: Combo, TimeLeft, TierText, FrenzyLeft) —
    ///     substitui o contador antigo; o XP bônus de combo continua contado em HUD.OnHit;
    /// (c) mini-quadro do alvo travado (LockOn.Target) no topo: nome e vida.
    /// </summary>
    public partial class HUD
    {
        // (a) ataque carregado
        Image chgTrack, chgFill, chgGem;
        float chgReadyPop;
        bool chgWasCooling;

        // (b) combo
        CanvasGroup comboGroup;
        RectTransform comboNumRt;
        Text comboNum, comboTier, comboBonus, comboFrenzy;
        Image comboTimerFill, comboFrenzyFill;
        RectTransform comboFrenzyRow;
        float comboPop, bonusT;
        int comboShown = -1;

        // (c) alvo travado
        CanvasGroup lockGroup;
        RectTransform lockRt;
        Text lockName;
        UIBar lockBar;
        Enemy lockShown;

        static Sprite chgRingSpr;

        void BuildCombatHud()
        {
            // ---------------- (a) anel no canto do slot de ataque
            if (chgRingSpr == null) chgRingSpr = MakeUltRing(64, 0.58f, 0.98f);
            var holder = UIKit.R(atkSlot.rt, "carga");
            UIKit.Place(holder, TR, MID, new Vector2(-3f, -3f), new Vector2(22f, 22f));
            var back = UIKit.Img(holder, "fundo", U.CircleSprite(), new Color(0.05f, 0.035f, 0.025f, 0.95f));
            UIKit.Stretch(back.rectTransform, -1f);
            chgTrack = UIKit.Img(holder, "trilho", chgRingSpr, new Color(0f, 0f, 0f, 0.6f));
            UIKit.Stretch(chgTrack.rectTransform);
            chgFill = UIKit.Img(holder, "anel", chgRingSpr, UIKit.Bronze);
            UIKit.Stretch(chgFill.rectTransform);
            chgFill.type = Image.Type.Filled;
            chgFill.fillMethod = Image.FillMethod.Radial360;
            chgFill.fillOrigin = (int)Image.Origin360.Top;
            chgFill.fillClockwise = true;
            chgFill.fillAmount = 1f;
            chgGem = UIKit.Img(holder, "gema", UIKit.DiamondSprite(), UIKit.Gold);
            UIKit.Place(chgGem.rectTransform, MID, MID, Vector2.zero, new Vector2(9f, 9f));
            back.raycastTarget = true;
            UITip.Add(back.gameObject, () => "Ataque carregado", () =>
            {
                var p = Hero;
                if (p == null) return "";
                Vector2 cd = p.ChargedCooldownInfo;
                return "Segure o clique e solte." + (cd.x > 0.01f ? "\nRecarga: " + cd.x.ToString("0.0") + "s" : "\n<color=#FFD76A>Pronto</color>");
            });

            // ---------------- (b) combo (direita, compacto)
            var r = UIKit.R(hudRoot, "Combo");
            UIKit.Place(r, MR, MR, new Vector2(-28f, -10f), new Vector2(230f, 130f));
            comboGroup = r.gameObject.AddComponent<CanvasGroup>();
            comboGroup.alpha = 0f; comboGroup.blocksRaycasts = false; comboGroup.interactable = false;
            comboNum = UIKit.Txt(r, "numero", "0", 46, UIKit.Gold, TextAnchor.MiddleRight, FontStyle.Bold, true);
            comboNumRt = UIKit.Place(comboNum.rectTransform, TR, new Vector2(1f, 0.5f), new Vector2(-62f, -26f), new Vector2(160f, 54f));
            comboNum.horizontalOverflow = HorizontalWrapMode.Overflow;
            var lbl = UIKit.Txt(r, "rotulo", "COMBO", 14, new Color(1f, 0.92f, 0.8f, 0.9f), TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UIKit.Place(lbl.rectTransform, TR, TR, new Vector2(0f, -30f), new Vector2(58f, 20f));
            var barBg = UIKit.Round(r, "tempo_fundo", new Color(0.04f, 0.03f, 0.02f, 0.8f), 6f);
            UIKit.Place(barBg.rectTransform, TR, TR, new Vector2(0f, -56f), new Vector2(150f, 5f));
            comboTimerFill = UIKit.Img(barBg.rectTransform, "tempo", UIKit.BarFillSprite(), UIKit.Gold);
            comboTimerFill.type = Image.Type.Filled;
            comboTimerFill.fillMethod = Image.FillMethod.Horizontal;
            comboTimerFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            UIKit.Stretch(comboTimerFill.rectTransform);
            comboTier = UIKit.Txt(r, "patamar", "", 13, new Color(1f, 0.85f, 0.55f, 1f), TextAnchor.MiddleRight, FontStyle.Bold, true);
            UIKit.Place(comboTier.rectTransform, TR, TR, new Vector2(0f, -64f), new Vector2(260f, 18f));
            comboTier.horizontalOverflow = HorizontalWrapMode.Overflow;
            // frenesi: faixa com tempo restante
            comboFrenzyRow = UIKit.R(r, "frenesi");
            UIKit.Place(comboFrenzyRow, TR, TR, new Vector2(0f, -86f), new Vector2(150f, 16f));
            var fBg = UIKit.Round(comboFrenzyRow, "fundo", new Color(0.05f, 0.03f, 0.02f, 0.85f), 4f);
            UIKit.Stretch(fBg.rectTransform);
            comboFrenzyFill = UIKit.Img(fBg.rectTransform, "barra", UIKit.BarFillSprite(), new Color(1f, 0.4f, 0.2f, 1f));
            UIKit.Stretch(comboFrenzyFill.rectTransform, 2f);
            comboFrenzyFill.type = Image.Type.Filled;
            comboFrenzyFill.fillMethod = Image.FillMethod.Horizontal;
            comboFrenzyFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            UIKit.Frame(fBg.rectTransform, null, true, -1f);
            comboFrenzy = UIKit.Txt(comboFrenzyRow, "texto", "FRENESI", 11, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Stretch(comboFrenzy.rectTransform);
            comboFrenzy.horizontalOverflow = HorizontalWrapMode.Overflow;
            comboFrenzyRow.gameObject.SetActive(false);
            comboBonus = UIKit.Txt(r, "bonus", "", 16, new Color(0.85f, 0.75f, 1f, 1f), TextAnchor.MiddleRight, FontStyle.Bold, true);
            UIKit.Place(comboBonus.rectTransform, TR, TR, new Vector2(0f, -106f), new Vector2(260f, 22f));
            comboBonus.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ---------------- (c) alvo travado (topo)
            var lp = UIKit.Panel(hudRoot, "AlvoTravado", new Color(0.075f, 0.05f, 0.035f, 0.9f));
            lp.raycastTarget = false;
            lockRt = UIKit.Place(lp.rectTransform, TC, TC, new Vector2(0f, -16f), new Vector2(300f, 50f));
            lockGroup = lp.gameObject.AddComponent<CanvasGroup>();
            lockGroup.alpha = 0f; lockGroup.blocksRaycasts = false; lockGroup.interactable = false;
            var mark = UIKit.Diamond(lockRt, "trava", new Color(1f, 0.36f, 0.24f, 1f), 12f);
            UIKit.Place(mark.rectTransform, TL, MID, new Vector2(18f, -16f), new Vector2(12f, 12f));
            lockName = UIKit.Txt(lockRt, "nome", "", 15, UIKit.TextCol, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(lockName.rectTransform, TL, TL, new Vector2(30f, -6f), new Vector2(256f, 20f));
            lockName.horizontalOverflow = HorizontalWrapMode.Overflow;
            lockBar = UIBar.Create(lockRt, "vida", new Color(0.72f, 0.16f, 0.12f, 1f), 10, 5f);
            UIKit.Place(lockBar.root, TL, TL, new Vector2(14f, -28f), new Vector2(272f, 12f));
            lockRt.gameObject.SetActive(false);
        }

        /// <summary>Pulso do número de combo (chamado em OnHit).</summary>
        void ComboHudPop(float k) { comboPop = Mathf.Max(comboPop, k); }

        /// <summary>Linha de XP bônus de combo.</summary>
        void ComboHudBonus(string text)
        {
            if (comboBonus == null) return;
            comboBonus.text = text;
            comboBonus.color = new Color(0.85f, 0.75f, 1f, 1f);
            bonusT = 2.2f;
            comboPop = 1.6f;
        }

        void UpdateCombatHud(Player p, float udt)
        {
            if (p == null || chgFill == null) return;
            UpdateChargeRing(p, udt);
            UpdateComboHud(p, udt);
            UpdateLockFrame(p, udt);
        }

        // ------------------------------------------------------------------ (a)
        void UpdateChargeRing(Player p, float udt)
        {
            Vector2 cd = p.ChargedCooldownInfo;   // (restante, total)
            bool cooling = cd.x > 0.01f;
            float fill;
            Color col;
            if (p.charging)
            {
                fill = Mathf.Clamp01(p.chargeLevel);
                col = Color.Lerp(UIKit.Gold, Color.white, fill >= 1f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f) : 0f);
            }
            else if (cooling)
            {
                fill = cd.y > 0.0001f ? 1f - Mathf.Clamp01(cd.x / cd.y) : 0f;
                col = new Color(0.55f, 0.48f, 0.4f, 0.9f);
            }
            else
            {
                fill = 1f;
                col = UIKit.Bronze;
            }
            if (chgWasCooling && !cooling) chgReadyPop = 1f;
            chgWasCooling = cooling;
            if (Mathf.Abs(chgFill.fillAmount - fill) > 0.002f) chgFill.fillAmount = fill;
            if (chgFill.color != col) chgFill.color = col;
            bool gem = !cooling;
            if (chgGem.enabled != gem) chgGem.enabled = gem;
            if (chgReadyPop > 0f)
            {
                chgReadyPop = Mathf.Max(0f, chgReadyPop - udt * 3f);
                chgFill.rectTransform.parent.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(chgReadyPop * Mathf.PI));
            }
            // brilho no slot enquanto carrega
            if (p.charging && atkSlot.glowT <= 0f)
            {
                var g = UIKit.Gold; g.a = 0.12f + 0.3f * Mathf.Clamp01(p.chargeLevel);
                atkSlot.glow.color = g;
            }
            else if (!p.charging && atkSlot.glowT <= 0f && atkSlot.glow.color.a > 0f)
                atkSlot.glow.color = new Color(1f, 1f, 1f, 0f);
        }

        // ------------------------------------------------------------------ (b)
        void UpdateComboHud(Player p, float udt)
        {
            var cp = p.combo;
            int count = cp != null ? cp.Combo : combo;
            float left = cp != null ? cp.TimeLeft : comboTimer;
            bool frenzy = cp != null && cp.Frenzy;
            bool show = (count >= 2 && left > 0f) || frenzy || bonusT > 0f;
            float target = show ? 1f : 0f;
            if (!Mathf.Approximately(comboGroup.alpha, target))
                comboGroup.alpha = Mathf.MoveTowards(comboGroup.alpha, target, udt * (target > 0f ? 10f : 3f));
            if (comboGroup.alpha <= 0f) { comboShown = -1; return; }

            if (count != comboShown)
            {
                comboShown = count;
                comboNum.text = count.ToString();
                comboNum.color = count >= ComboPassives.TierFrenzy ? new Color(1f, 0.45f, 0.32f)
                               : count >= ComboPassives.TierDamage ? new Color(1f, 0.62f, 0.22f) : UIKit.Gold;
            }
            comboPop = Mathf.Max(0f, comboPop - udt * 5f);
            comboNumRt.localScale = Vector3.one * (1f + 0.3f * comboPop);
            comboTimerFill.fillAmount = Mathf.Clamp01(left / ComboPassives.Window);

            // patamar (sem repetir o nome do frenesi, que já aparece na faixa)
            string tier = cp == null ? "" : frenzy ? cp.ActivePassive : cp.TierText;
            UIKit.Set(comboTier, tier);

            if (comboFrenzyRow.gameObject.activeSelf != frenzy) comboFrenzyRow.gameObject.SetActive(frenzy);
            if (frenzy)
            {
                float fl = cp.FrenzyLeft;
                comboFrenzyFill.fillAmount = Mathf.Clamp01(fl / ComboPassives.FrenzyDuration);
                UIKit.Set(comboFrenzy, "FRENESI  " + Mathf.CeilToInt(fl) + "s");
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
                comboFrenzyFill.color = Color.Lerp(new Color(1f, 0.35f, 0.18f, 1f), new Color(1f, 0.75f, 0.3f, 1f), pulse);
            }

            if (bonusT > 0f)
            {
                bonusT -= udt;
                var c = comboBonus.color; c.a = Mathf.Clamp01(bonusT / 0.5f);
                comboBonus.color = c;
                if (bonusT <= 0f) comboBonus.text = "";
            }
        }

        // ------------------------------------------------------------------ (c)
        void UpdateLockFrame(Player p, float udt)
        {
            var lo = p.lockOn;
            Enemy t = lo != null && lo.Locked ? lo.Target : null;
            if (t != null && t.dead) t = null;
            bool bossShown = bossGroup != null && bossGroup.alpha > 0.05f;
            // o chefe já tem a barra grande no topo: não duplica
            if (t != null && bossShown && t == bossTarget) t = null;

            if (t != null)
            {
                if (t != lockShown)
                {
                    lockShown = t;
                    lockBar.Reset();
                    UIKit.Set(lockName, t.def != null ? t.def.name : "Alvo");
                }
                if (!lockRt.gameObject.activeSelf) lockRt.gameObject.SetActive(true);
                lockBar.Set(t.hp, t.maxHp, udt);
            }
            float y = bossShown ? -80f : -16f;
            var ap = lockRt.anchoredPosition;
            if (Mathf.Abs(ap.y - y) > 0.5f) lockRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(ap.y, y, 1f - Mathf.Exp(-12f * udt)));

            float target = t != null ? 1f : 0f;
            if (!Mathf.Approximately(lockGroup.alpha, target))
                lockGroup.alpha = Mathf.MoveTowards(lockGroup.alpha, target, udt * (t != null ? 8f : 4f));
            if (lockGroup.alpha <= 0f && lockRt.gameObject.activeSelf) { lockRt.gameObject.SetActive(false); lockShown = null; }
        }
    }
}
