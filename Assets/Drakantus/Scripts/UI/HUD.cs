using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Drakantus
{
    /// <summary>
    /// HUD do jogo (Canvas Screen Space Overlay criado em código):
    /// retrato + barras (sup. esq.), barra de ações + XP (centro inferior), minimapa (sup. dir., Minimap.cs),
    /// vida do chefe, combo, números de dano, faixas grandes, avisos, dica de interação e fade.
    /// Janelas modais ficam em Windows.cs.
    /// </summary>
    public partial class HUD : MonoBehaviour
    {
        public static HUD I;

        Canvas canvas;
        RectTransform canvasRt, hudRoot, popupLayer, bannerLayer, toastLayer, modalLayer, tipLayer, fadeLayer;

        static Player Hero => Game.I != null ? Game.I.player : null;

        // ------------------------------------------------------------------ API estática
        public static HUD Create()
        {
            if (I != null) return I;
            GameData.Load();
            Settings.Load();
            var go = new GameObject("HUD");
            go.layer = 5;
            var h = go.AddComponent<HUD>();
            I = h;
            h.Build();
            return h;
        }

        /// <summary>Número/texto flutuante que segue um ponto do mundo.</summary>
        public static void Popup(Vector3 worldPos, string text, Color color, bool big = false)
        {
            if (I != null) I.SpawnPopup(worldPos, text, color, big);
        }

        public static bool PointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void Awake()
        {
            if (I == null) I = this;
        }

        void Start()
        {
            if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        void OnDestroy()
        {
            GameState.Changed -= OnStateChanged;
            GameState.InventoryChanged -= OnInventoryChanged;
            GameState.LoadoutChanged -= OnLoadoutChanged;
            GameState.LeveledUp -= OnLevelUp;
            GameState.SkillsUnlocked -= OnSkillsUnlocked;
            GameState.Notified -= Toast;
            if (pausedByUI) { Time.timeScale = 1f; pausedByUI = false; }
            DestroyMinimap();
            if (I == this) I = null;
        }

        // ------------------------------------------------------------------ construção
        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var sc = gameObject.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // ~0,82 da escala antiga (1920x1080): HUD e janelas menores sem mexer no layout de cada uma
            sc.referenceResolution = new Vector2(ReferenceW, ReferenceH);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            sc.referencePixelsPerUnit = 100f;
            gameObject.AddComponent<GraphicRaycaster>();
            canvasRt = (RectTransform)transform;

            hudRoot = Layer("HUD_Jogo");
            popupLayer = Layer("Numeros");
            bannerLayer = Layer("Faixas");
            toastLayer = Layer("Avisos");
            modalLayer = Layer("Janelas");
            tipLayer = Layer("Dicas");
            fadeLayer = Layer("Fade");

            BuildPlayerFrame();
            BuildActionBar();
            BuildXpBar();
            BuildHint();
            BuildMinimap();
            BuildBossBar();
            BuildCombatHud();
            BuildClassHud();   // [Classes] ClassHud.cs
            BuildPopups();
            BuildBanner();
            BuildUltimate();
            BuildTooltip();
            BuildFade();

            GameState.Changed += OnStateChanged;
            GameState.InventoryChanged += OnInventoryChanged;
            GameState.LoadoutChanged += OnLoadoutChanged;
            GameState.LeveledUp += OnLevelUp;
            GameState.SkillsUnlocked += OnSkillsUnlocked;
            GameState.Notified += Toast;

            OnStateChanged();
            OnLoadoutChanged();
            hudRoot.gameObject.SetActive(false);

            PixelFilter.Ensure();
            GameCursor.Ensure();
        }

        /// <summary>Resolução de referência do CanvasScaler (também usada pelo cursor).</summary>
        public const float ReferenceW = 2340f, ReferenceH = 1316f;

        RectTransform Layer(string n)
        {
            var r = UIKit.R(transform, n);
            UIKit.Stretch(r);
            return r;
        }

        static readonly Vector2 TL = new Vector2(0f, 1f), TR = new Vector2(1f, 1f), BL = new Vector2(0f, 0f),
                                 BC = new Vector2(0.5f, 0f), TC = new Vector2(0.5f, 1f), MID = new Vector2(0.5f, 0.5f),
                                 ML = new Vector2(0f, 0.5f), MR = new Vector2(1f, 0.5f);

        // ================================================================== retrato e barras (sup. esq.)
        Image portraitRing, portraitIcon, sealRing;
        RectTransform portraitRt;
        Text nameText, levelText;
        UIBar hpBar, mpBar, stBar;
        float portraitFlash, portraitPop, lastHp = -1f;
        Color classColor = Color.white;
        /// <summary>Bronze levemente tingido pela cor da classe (aros e molduras metálicas).</summary>
        Color RingTint => Color.Lerp(UIKit.Bronze, classColor, 0.3f);
        Color MetalTint => Color.Lerp(UIKit.Bronze, classColor, 0.45f);

        class BuffIcon
        {
            public RectTransform rt; public Image frame, icon; public Text letter, time;
            public int lastSec = -1; public string tipTitle = "", tipBody = "";
        }
        readonly BuffIcon[] buffs = new BuffIcon[4];

        void BuildPlayerFrame()
        {
            // bloco compacto: placa de couro com moldura; retrato à esquerda, nome e barras à direita
            var panel = UIKit.Panel(hudRoot, "Retrato");
            var pr = UIKit.Place(panel.rectTransform, TL, TL, new Vector2(16f, -16f), new Vector2(424f, 108f));

            portraitRt = UIKit.Place(UIKit.R(pr, "retrato"), ML, MID, new Vector2(54f, 0f), new Vector2(84f, 84f));
            var inner = UIKit.Img(portraitRt, "fundo", U.CircleSprite(), new Color(0.09f, 0.065f, 0.045f, 1f));
            UIKit.Stretch(inner.rectTransform, 3f);
            var shine = UIKit.Img(inner.rectTransform, "luz", UIKit.SoftSprite(), new Color(1f, 0.85f, 0.6f, 0.14f));
            UIKit.Stretch(shine.rectTransform, 6f);
            portraitIcon = UIKit.Img(inner.rectTransform, "icone", null, Color.white);
            UIKit.Place(portraitIcon.rectTransform, MID, MID, Vector2.zero, new Vector2(50f, 50f));
            portraitIcon.preserveAspect = true;
            // aro ornamentado de bronze (pisca em vermelho ao levar dano)
            portraitRing = UIKit.Img(portraitRt, "aro", UltFrameSprite(), UIKit.Bronze);
            UIKit.Stretch(portraitRing.rectTransform, -5f);

            sealRing = UIKit.Img(portraitRt, "selo", UIKit.DiamondSprite(), UIKit.Bronze);
            UIKit.Place(sealRing.rectTransform, new Vector2(1f, 0f), MID, new Vector2(-8f, 8f), new Vector2(38f, 38f));
            levelText = UIKit.Txt(sealRing.rectTransform, "nivel", "1", 16, new Color(0.14f, 0.08f, 0.03f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(levelText.rectTransform);
            levelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var lvSh = levelText.GetComponent<Shadow>();
            if (lvSh != null) lvSh.effectColor = new Color(1f, 0.9f, 0.6f, 0.35f);

            nameText = UIKit.Txt(pr, "nome", "", 19, UIKit.TextCol, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(nameText.rectTransform, TL, TL, new Vector2(110f, -8f), new Vector2(300f, 26f));
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

            hpBar = UIBar.Create(pr, "vida", UIKit.HpCol, 14, 4f);
            UIKit.Place(hpBar.root, TL, TL, new Vector2(110f, -36f), new Vector2(298f, 21f));
            mpBar = UIBar.Create(pr, "mana", UIKit.MpCol, 12, 4f);
            UIKit.Place(mpBar.root, TL, TL, new Vector2(110f, -61f), new Vector2(298f, 16f));
            stBar = UIBar.Create(pr, "vigor", UIKit.StCol, 0, 6f);
            UIKit.Place(stBar.root, TL, TL, new Vector2(110f, -81f), new Vector2(298f, 9f));
            stBar.useTrail = false;
            hpBar.bg.raycastTarget = true; mpBar.bg.raycastTarget = true; stBar.bg.raycastTarget = true;
            UITip.Add(hpBar.bg.gameObject, () => "Vida", () => "Poção [1] ou santuário.");
            UITip.Add(mpBar.bg.gameObject, () => "Mana", () => "Gasta pelas habilidades. Poção [2].");
            UITip.Add(stBar.bg.gameObject, () => "Vigor", () => "Atacar, esquivar, defender e aparar.");

            for (int i = 0; i < buffs.Length; i++)
            {
                var b = new BuffIcon();
                b.frame = UIKit.MetalFrame(hudRoot, "efeito" + i, Color.white, true);
                b.rt = UIKit.Place(b.frame.rectTransform, TL, TL, new Vector2(16f + i * 44f, -132f), new Vector2(38f, 38f));
                var bin = UIKit.Round(b.rt, "fundo", UIKit.SlotBg, 2f);
                UIKit.Stretch(bin.rectTransform, 4f);
                b.icon = UIKit.Img(b.rt, "icone", null, Color.white);
                UIKit.Place(b.icon.rectTransform, MID, MID, new Vector2(0f, 1f), new Vector2(26f, 26f));
                b.icon.preserveAspect = true;
                b.letter = UIKit.Txt(b.rt, "letra", "", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(b.letter.rectTransform);
                b.time = UIKit.Txt(b.rt, "tempo", "", 13, Color.white, TextAnchor.LowerCenter, FontStyle.Bold, true);
                UIKit.Stretch(b.time.rectTransform, 0f, 0f, 0f, -13f);
                var bb = b;
                UITip.Add(b.frame.gameObject, () => bb.tipTitle, () => bb.tipBody);
                b.rt.gameObject.SetActive(false);
                buffs[i] = b;
            }
        }

        void UpdateBars(Player p, float dt)
        {
            var P = GameState.P;
            if (lastHp >= 0f && P.hp < lastHp - 0.5f) portraitFlash = 1f;
            lastHp = P.hp;
            hpBar.Set(P.hp, GameState.maxHp, dt);
            mpBar.Set(P.mp, GameState.maxMp, dt);
            stBar.Set(GameState.stamina, GameState.maxStamina, dt);
            xpBar.Set(P.xp, GameState.XpToNext(), dt);

            // vida baixa: barra pulsa
            float low = GameState.maxHp > 0f ? P.hp / GameState.maxHp : 1f;
            if (low < 0.3f && P.hp > 0f)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                hpBar.fill.color = Color.Lerp(UIKit.HpCol, new Color(1f, 0.55f, 0.5f), k);
            }
            else if (hpBar.fill.color != UIKit.HpCol) hpBar.fill.color = UIKit.HpCol;

            if (portraitFlash > 0f || portraitPop > 0f)
            {
                portraitFlash = Mathf.Max(0f, portraitFlash - dt * 3f);
                portraitPop = Mathf.Max(0f, portraitPop - dt * 2.5f);
                portraitRing.color = Color.Lerp(RingTint, new Color(1f, 0.25f, 0.2f), portraitFlash);
                portraitRt.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(portraitPop * Mathf.PI));
            }
        }

        // ------------------------------------------------------------------ efeitos ativos
        Sprite shieldIcon, tauntIcon; string iconsForClass = "";

        void FindBuffIcons()
        {
            if (iconsForClass == GameState.ClassId) return;
            iconsForClass = GameState.ClassId;
            shieldIcon = null; tauntIcon = null;
            foreach (var s in GameData.Skills.Values)
            {
                if (s.shield > 0 && (shieldIcon == null || s.classId == iconsForClass)) shieldIcon = U.Icon(s.icon);
                if (s.taunt > 0 && (tauntIcon == null || s.classId == iconsForClass)) tauntIcon = U.Icon(s.icon);
            }
        }

        void UpdateBuffs(Player p)
        {
            FindBuffIcons();
            int n = 0;
            if (p.buffTime > 0f && p.buffSkill != null)
                SetBuff(n++, U.Icon(p.buffSkill.icon), "+", p.buffTime, U.Hex(p.buffSkill.color), p.buffSkill.name, p.buffSkill.desc);
            if (p.shieldHp > 0f && n < buffs.Length)
                SetBuff(n++, shieldIcon, "E", p.shieldTime, new Color(0.6f, 0.77f, 1f), "Escudo", "Absorve " + Mathf.CeilToInt(p.shieldHp) + " de dano.");
            if (GameState.atkBuff > 0f && n < buffs.Length)
            {
                var it = GameData.Item("strength_elixir");
                SetBuff(n++, it != null ? U.Icon(it.icon) : null, "F", GameState.atkBuff, new Color(1f, 0.5f, 0.35f), it != null ? it.name : "Força", "Ataque +4.");
            }
            if (p.tauntTime > 0f && n < buffs.Length)
                SetBuff(n++, tauntIcon, "!", p.tauntTime, new Color(1f, 0.35f, 0.3f), "Provocação", "Os inimigos vêm atrás de você.");
            for (int i = n; i < buffs.Length; i++)
                if (buffs[i].rt.gameObject.activeSelf) buffs[i].rt.gameObject.SetActive(false);
        }

        void SetBuff(int i, Sprite icon, string letter, float secs, Color col, string title, string body)
        {
            var b = buffs[i];
            if (!b.rt.gameObject.activeSelf) b.rt.gameObject.SetActive(true);
            if (b.icon.sprite != icon) b.icon.sprite = icon;
            bool hasIcon = icon != null;
            if (b.icon.enabled != hasIcon) b.icon.enabled = hasIcon;
            UIKit.Set(b.letter, hasIcon ? "" : letter);
            if (b.frame.color != col) b.frame.color = col;
            int s = Mathf.CeilToInt(secs);
            if (s != b.lastSec) { b.lastSec = s; b.time.text = s + "s"; }
            b.tipTitle = title ?? ""; b.tipBody = body ?? "";
        }

        // ================================================================== barra de ações (centro inferior)
        class Slot
        {
            public RectTransform rt; public Image frame, inner, icon, cd, glow;
            public Text key, cdText, count, plus;
            public float pop, glowT, prevLeft; public Color glowCol = Color.white; public int lastCd = -999;
        }

        readonly Slot[] skillSlots = new Slot[4];
        readonly SkillDef[] slotSkills = new SkillDef[4];
        Slot atkSlot, dashSlot;
        readonly Slot[] potSlots = new Slot[2];
        static readonly string[] PotionIds = { "health_potion", "mana_potion" };
        float actionW;
        string cachedClass = null;
        static readonly Color NeutralFrame = new Color(0.62f, 0.47f, 0.3f, 1f);

        void BuildActionBar()
        {
            const float S = SlotSize, G = 6f, PAD = 12f;
            var panel = UIKit.Panel(hudRoot, "BarraDeAcoes");
            var pr = panel.rectTransform;
            float x = PAD;

            atkSlot = MakeSlot(pr, x, "Clique"); x += S + G;
            UIKit.RT(atkSlot.key.transform.parent).sizeDelta = new Vector2(44f, 15f);
            atkSlot.key.fontSize = 10;
            atkSlot.frame.gameObject.AddComponent<UIClick>().onLeft = () => { var p = Hero; if (p != null) p.TryAttack(); };
            UITip.Add(atkSlot.frame.gameObject, () => "Ataque", () =>
                (string.IsNullOrEmpty(GameState.Class.ranged) ? "Golpe em leque." : "Disparo na direção do mouse.") + "\nSegure para carregar.");

            x = Separator(pr, x);

            for (int i = 0; i < 4; i++)
            {
                string key = i < GameData.SkillKeys.Length ? GameData.SkillKeys[i] : (i + 1).ToString();
                var s = MakeSlot(pr, x, key); x += S + G;
                int idx = i;
                s.frame.gameObject.AddComponent<UIClick>().onLeft = () =>
                {
                    var p = Hero;
                    if (slotSkills[idx] == null) { SkillsWindow(); return; }
                    if (p != null) p.UseSkill(idx);
                };
                UITip.Add(s.frame.gameObject, () => slotSkills[idx] != null ? slotSkills[idx].name : "Slot vazio",
                    () => slotSkills[idx] != null ? SkillTipBody(slotSkills[idx]) : "Habilidades [K].");
                skillSlots[i] = s;
            }

            x = Separator(pr, x);

            dashSlot = MakeSlot(pr, x, "Q"); x += S + G;
            UIKit.Set(dashSlot.plus, "»");
            dashSlot.plus.color = new Color(0.95f, 0.88f, 0.72f, 0.9f);
            dashSlot.plus.fontSize = 36;
            dashSlot.frame.gameObject.AddComponent<UIClick>().onLeft = () => { var p = Hero; if (p != null) p.TryDash(Vector3.zero); };
            UITip.Add(dashSlot.frame.gameObject, () => "Esquiva", () => "Avanço invulnerável. 18 de vigor.");

            for (int k = 0; k < 2; k++)
            {
                var s = MakeSlot(pr, x, (k + 1).ToString()); x += S + G;
                string id = PotionIds[k];
                var it = GameData.Item(id);
                s.icon.sprite = it != null ? U.Icon(it.icon) : null;
                s.icon.enabled = s.icon.sprite != null;
                if (k == 1) s.icon.color = new Color(0.6f, 0.8f, 1f, 1f);
                s.frame.gameObject.AddComponent<UIClick>().onLeft = () =>
                {
                    if (Game.I != null && Hero != null) Game.I.UsePotion(id);
                };
                UITip.Add(s.frame.gameObject, () => it != null ? it.name : id,
                    () => it != null ? (it.stats + "\nVocê tem: " + GameState.Count(id)) : "");
                potSlots[k] = s;
            }

            actionW = x - G + PAD;
            UIKit.Place(pr, BC, BC, new Vector2(0f, ActionBottom), new Vector2(actionW, ActionH));
        }

        const float SlotSize = 58f, ActionBottom = 12f, ActionH = 78f;

        float Separator(RectTransform parent, float x)
        {
            var line = UIKit.Line(parent, "separador", new Color(UIKit.Bronze.r, UIKit.Bronze.g, UIKit.Bronze.b, 0.35f));
            UIKit.Place(line.rectTransform, ML, MID, new Vector2(x + 3f, 0f), new Vector2(1f, 40f));
            var gem = UIKit.Diamond(parent, "gema", UIKit.Bronze, 9f);
            UIKit.Place(gem.rectTransform, ML, MID, new Vector2(x + 3.5f, 0f), new Vector2(9f, 9f));
            return x + 12f;
        }

        Slot MakeSlot(RectTransform parent, float x, string key)
        {
            const float S = SlotSize;
            var s = new Slot();
            s.frame = UIKit.MetalFrame(parent, "slot_" + key, NeutralFrame, true);
            s.rt = UIKit.Place(s.frame.rectTransform, ML, MID, new Vector2(x + S * 0.5f, 3f), new Vector2(S, S));
            s.inner = UIKit.Round(s.rt, "fundo", UIKit.SlotBg, 1.8f);
            UIKit.Stretch(s.inner.rectTransform, 4f);
            var sheen = UIKit.Img(s.inner.rectTransform, "luz", UIKit.SoftSprite(), new Color(1f, 0.85f, 0.6f, 0.06f));
            UIKit.Stretch(sheen.rectTransform, -8f);
            s.icon = UIKit.Img(s.rt, "icone", null, Color.white);
            UIKit.Place(s.icon.rectTransform, MID, MID, Vector2.zero, new Vector2(46f, 46f));
            s.icon.preserveAspect = true;
            s.icon.enabled = false;
            s.plus = UIKit.Txt(s.rt, "mais", "", 30, new Color(1f, 0.9f, 0.75f, 0.25f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(s.plus.rectTransform);
            s.plus.horizontalOverflow = HorizontalWrapMode.Overflow;
            s.cd = UIKit.Round(s.rt, "recarga", new Color(0f, 0f, 0f, 0.7f), 1.8f);
            UIKit.Stretch(s.cd.rectTransform, 4f);
            s.cd.type = Image.Type.Filled;
            s.cd.fillMethod = Image.FillMethod.Radial360;
            s.cd.fillOrigin = (int)Image.Origin360.Top;
            s.cd.fillClockwise = true;
            s.cd.fillAmount = 0f;
            s.cd.enabled = false;
            s.cdText = UIKit.Txt(s.rt, "segundos", "", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Stretch(s.cdText.rectTransform);
            s.cdText.horizontalOverflow = HorizontalWrapMode.Overflow;
            s.glow = UIKit.Round(s.rt, "brilho", new Color(1f, 1f, 1f, 0f), 1.8f);
            UIKit.Stretch(s.glow.rectTransform, -3f);
            // tecla: plaquinha na base do slot
            var kb = UIKit.Round(s.rt, "tecla", new Color(0.05f, 0.035f, 0.025f, 0.95f), 4f);
            UIKit.Place(kb.rectTransform, BC, MID, new Vector2(0f, -1f), new Vector2(26f, 15f));
            s.key = UIKit.Txt(kb.rectTransform, "t", key, 11, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(s.key.rectTransform);
            s.key.horizontalOverflow = HorizontalWrapMode.Overflow;
            s.count = UIKit.Txt(s.rt, "quantidade", "", 14, Color.white, TextAnchor.UpperRight, FontStyle.Bold, true);
            UIKit.Stretch(s.count.rectTransform, 0f, 6f, 3f, 0f);
            return s;
        }

        static string Num(float v) => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.#");

        static string SkillTipBody(SkillDef s)
        {
            if (s == null) return "";
            return s.desc + "\n<color=#8fb4ff>Mana " + Num(s.mp) + "</color>  ·  <color=#FFD76A>Recarga " + Num(s.cd) + "s</color>";
        }

        void RefreshSlotSkills()
        {
            var lo = GameState.CurrentLoadout();
            for (int i = 0; i < 4; i++)
            {
                var sd = i < lo.Length ? GameData.Skill(lo[i]) : null;
                slotSkills[i] = sd;
                var s = skillSlots[i];
                s.icon.sprite = sd != null ? U.Icon(sd.icon) : null;
                s.icon.enabled = s.icon.sprite != null;
                string plus = sd == null ? "+" : (s.icon.sprite == null && !string.IsNullOrEmpty(sd.name) ? sd.name.Substring(0, 1) : "");
                UIKit.Set(s.plus, plus);
                s.plus.color = sd == null ? new Color(1f, 1f, 1f, 0.28f) : Color.white;
                Color mt = MetalTint;
                s.frame.color = sd != null ? mt : new Color(mt.r * 0.5f, mt.g * 0.5f, mt.b * 0.5f, 1f);
                s.prevLeft = 0f; s.lastCd = -999;
            }
        }

        void ApplyClassVisuals()
        {
            var cd = GameState.Class;
            classColor = U.Hex(cd.color);
            portraitRing.color = RingTint;
            portraitIcon.sprite = U.Icon(cd.icon);
            portraitIcon.enabled = portraitIcon.sprite != null;
            atkSlot.icon.sprite = portraitIcon.sprite;
            atkSlot.icon.enabled = atkSlot.icon.sprite != null;
            atkSlot.frame.color = MetalTint;
        }

        void UpdateSlots(Player p, float dt)
        {
            var P = GameState.P;
            for (int i = 0; i < 4; i++)
            {
                var s = skillSlots[i];
                var sd = slotSkills[i];
                float left = 0f, total = 0f;
                if (sd != null)
                {
                    p.cooldowns.TryGetValue(sd.id, out left);
                    total = Mathf.Max(sd.cd, left);
                }
                bool cooling = left > 0.01f;
                if (s.cd.enabled != cooling) s.cd.enabled = cooling;
                if (cooling) s.cd.fillAmount = total > 0f ? Mathf.Clamp01(left / total) : 0f;
                int key = !cooling ? 0 : left < 1f ? 1000 + Mathf.CeilToInt(left * 10f) : Mathf.CeilToInt(left);
                if (key != s.lastCd)
                {
                    s.lastCd = key;
                    s.cdText.text = !cooling ? "" : left < 1f ? left.ToString("0.0") : Mathf.CeilToInt(left).ToString();
                }
                if (s.prevLeft > 0.01f && !cooling) { s.pop = 1f; s.glowT = 1f; s.glowCol = UIKit.Gold; }
                s.prevLeft = left;
                bool noMana = sd != null && P.mp < sd.mp;
                Color ic = noMana ? new Color(0.42f, 0.55f, 1f, 1f) : cooling ? new Color(0.75f, 0.75f, 0.78f, 1f) : Color.white;
                if (s.icon.color != ic) s.icon.color = ic;
                AnimateSlot(s, dt);
            }

            Color atkC = GameState.stamina < 8f || !p.canFight ? new Color(0.5f, 0.5f, 0.55f, 1f) : Color.white;
            if (atkSlot.icon.color != atkC) atkSlot.icon.color = atkC;
            Color dC = GameState.stamina < 18f || !p.canFight ? new Color(0.7f, 0.64f, 0.55f, 0.35f) : new Color(0.98f, 0.9f, 0.74f, 0.95f);
            if (dashSlot.plus.color != dC) dashSlot.plus.color = dC;
            AnimateSlot(atkSlot, dt);
            AnimateSlot(dashSlot, dt);
            for (int k = 0; k < 2; k++) AnimateSlot(potSlots[k], dt);
        }

        static void AnimateSlot(Slot s, float dt)
        {
            if (s.pop <= 0f && s.glowT <= 0f) return;
            s.pop = Mathf.Max(0f, s.pop - dt * 3.2f);
            s.glowT = Mathf.Max(0f, s.glowT - dt * 2.4f);
            s.rt.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(s.pop * Mathf.PI));
            var g = s.glowCol; g.a = s.glowT * 0.6f;
            s.glow.color = g;
        }

        void RefreshPotions()
        {
            for (int k = 0; k < 2; k++)
            {
                var s = potSlots[k];
                int n = GameState.Count(PotionIds[k]);
                UIKit.Set(s.count, n > 0 ? n.ToString() : "0");
                s.count.color = n > 0 ? Color.white : new Color(1f, 0.45f, 0.4f, 1f);
                Color ic = n > 0 ? (k == 1 ? new Color(0.6f, 0.8f, 1f, 1f) : Color.white) : new Color(0.4f, 0.4f, 0.45f, 0.6f);
                s.icon.color = ic;
            }
        }

        /// <summary>Pisca um slot de habilidade (usada = branco, sem mana = azul).</summary>
        public void FlashSlot(int i, Color c)
        {
            if (i < 0 || i >= skillSlots.Length || skillSlots[i] == null) return;
            var s = skillSlots[i];
            s.pop = 1f; s.glowT = 1f; s.glowCol = c;
        }

        // ================================================================== barra de XP
        UIBar xpBar;
        Text xpText;
        readonly Text[] xpPops = new Text[4];
        readonly float[] xpPopT = new float[4];
        int xpPopNext, lastTotalXp = -1;
        const float XpY = ActionBottom + ActionH + 5f;

        void BuildXpBar()
        {
            xpBar = UIBar.Create(hudRoot, "XP", UIKit.XpCol, 0, 6f);
            UIKit.Place(xpBar.root, BC, BC, new Vector2(0f, XpY), new Vector2(actionW - 24f, 8f));
            xpBar.useTrail = false;
            xpBar.bg.raycastTarget = true;
            UITip.Add(xpBar.bg.gameObject, () => "Experiência", () => GameState.P.xp + " / " + GameState.XpToNext() + " XP");
            xpText = UIKit.Txt(hudRoot, "xp_texto", "", 13, new Color(0.84f, 0.78f, 0.92f, 0.9f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(xpText.rectTransform, BC, BC, new Vector2(0f, XpY + 10f), new Vector2(500f, 18f));
            for (int i = 0; i < xpPops.Length; i++)
            {
                var t = UIKit.Txt(hudRoot, "xp_ganho", "", 17, new Color(0.82f, 0.72f, 1f, 1f), TextAnchor.MiddleRight, FontStyle.Bold, true);
                UIKit.Place(t.rectTransform, BC, new Vector2(1f, 0.5f), new Vector2(actionW * 0.5f, XpY + 16f), new Vector2(240f, 26f));
                t.gameObject.SetActive(false);
                xpPops[i] = t;
            }
        }

        static int TotalXp(int level, int xp)
        {
            int t = xp;
            for (int l = 1; l < level; l++) t += 40 + (l - 1) * 35;
            return t;
        }

        void SpawnXpPop(int amount)
        {
            int i = xpPopNext;
            xpPopNext = (xpPopNext + 1) % xpPops.Length;
            xpPopT[i] = 0.0001f;
            xpPops[i].text = "+" + amount + " XP";
            xpPops[i].gameObject.SetActive(true);
        }

        void UpdateXpPops(float dt)
        {
            for (int i = 0; i < xpPops.Length; i++)
            {
                if (xpPopT[i] <= 0f) continue;
                xpPopT[i] += dt;
                float k = xpPopT[i] / 1.3f;
                if (k >= 1f) { xpPopT[i] = 0f; xpPops[i].gameObject.SetActive(false); continue; }
                xpPops[i].rectTransform.anchoredPosition = new Vector2(actionW * 0.5f, XpY + 16f + 36f * (1f - (1f - k) * (1f - k)));
                var c = xpPops[i].color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                xpPops[i].color = c;
                xpPops[i].rectTransform.localScale = Vector3.one * (k < 0.1f ? Mathf.Lerp(1.4f, 1f, k / 0.1f) : 1f);
            }
        }

        // ================================================================== dica de interação
        RectTransform hintRt;
        Text hintText;

        void BuildHint()
        {
            var p = UIKit.Panel(hudRoot, "Interacao");
            p.raycastTarget = false;
            hintRt = UIKit.Place(p.rectTransform, BC, BC, new Vector2(0f, XpY + 44f), new Vector2(260f, 36f));
            UIKit.HLayout(p.gameObject, 0f, new RectOffset(18, 18, 8, 8));
            UIKit.Fit(p.gameObject, true, true);
            hintText = UIKit.Txt(hintRt, "texto", "", 18, UIKit.TextCol, TextAnchor.MiddleCenter);
            hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            hintRt.gameObject.SetActive(false);
        }

        public void SetInteractHint(string text)
        {
            if (hintRt == null) return;
            if (string.IsNullOrEmpty(text)) { if (hintRt.gameObject.activeSelf) hintRt.gameObject.SetActive(false); return; }
            UIKit.Set(hintText, text.Replace("[E]", "<color=#FFD76A><b>[E]</b></color>"));
            if (!hintRt.gameObject.activeSelf) hintRt.gameObject.SetActive(true);
        }

        // ================================================================== vida do chefe (topo)
        CanvasGroup bossGroup;
        Text bossName;
        UIBar bossBar;
        Enemy bossTarget;

        void BuildBossBar()
        {
            var r = UIKit.R(hudRoot, "Chefe");
            UIKit.Place(r, TC, TC, new Vector2(0f, -16f), new Vector2(640f, 56f));
            bossGroup = r.gameObject.AddComponent<CanvasGroup>();
            bossGroup.alpha = 0f; bossGroup.blocksRaycasts = false; bossGroup.interactable = false;
            var band = UIKit.Img(r, "faixa", UIKit.BandSprite(), new Color(0.03f, 0.015f, 0.01f, 0.6f));
            UIKit.Stretch(band.rectTransform, -90f, -90f, -4f, -4f);
            bossName = UIKit.Txt(r, "nome", "", 21, new Color(1f, 0.86f, 0.7f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Place(bossName.rectTransform, TC, TC, new Vector2(0f, 0f), new Vector2(640f, 28f));
            bossName.horizontalOverflow = HorizontalWrapMode.Overflow;
            bossBar = UIBar.Create(r, "vida", new Color(0.72f, 0.14f, 0.12f, 1f), 13, 4f);
            UIKit.Place(bossBar.root, BC, BC, new Vector2(0f, 4f), new Vector2(620f, 19f));
            for (int k = 0; k < 2; k++)
            {
                var cap = UIKit.Diamond(r, "ponta", UIKit.Bronze, 16f);
                UIKit.Place(cap.rectTransform, BC, MID, new Vector2(k == 0 ? -318f : 318f, 13.5f), new Vector2(16f, 16f));
            }
        }

        void UpdateBoss(Player p, float dt)
        {
            Enemy boss = null;
            float best = 18f;
            var list = Game.I.enemies;
            if (list != null)
                foreach (var e in list)
                {
                    if (e == null || e.dead || e.def == null || !e.def.boss || !e.alerted) continue;
                    float d = Vector3.Distance(e.transform.position, p.transform.position);
                    if (d < best) { best = d; boss = e; }
                }
            if (boss != null)
            {
                if (boss != bossTarget) { bossTarget = boss; bossBar.Reset(); }
                UIKit.Set(bossName, boss.def.name);
                bossBar.Set(boss.hp, boss.maxHp, dt);
            }
            else if (bossTarget != null && bossGroup.alpha > 0f)
            {
                bossBar.Set(bossTarget.dead ? 0f : bossTarget.hp, bossTarget.maxHp, dt);
            }
            float target = boss != null ? 1f : 0f;
            if (!Mathf.Approximately(bossGroup.alpha, target))
                bossGroup.alpha = Mathf.MoveTowards(bossGroup.alpha, target, dt * (boss != null ? 4f : 1.2f));
        }

        // ================================================================== combo (contagem para o XP bônus; o visual fica em CombatHud.cs)
        int combo, bestCombo;
        float comboTimer;
        const float ComboWindow = 2.6f;

        /// <summary>Chamado pelo Enemy a cada acerto do jogador: conta o combo (XP bônus em 10/25/50/100...).</summary>
        public void OnHit(int dmg, bool crit, Enemy e)
        {
            combo++;
            comboTimer = ComboWindow;
            if (combo > bestCombo) bestCombo = combo;
            ComboHudPop(crit ? 1.3f : 1f);
            if (combo == 10 || combo == 25 || combo == 50 || combo == 100 || (combo > 100 && combo % 100 == 0))
            {
                int bonus = combo / 2;
                ComboHudBonus("+" + bonus + " XP");
                Sfx.Play("combo");
                GameState.AddXp(bonus);
            }
        }

        void UpdateCombo(float dt)
        {
            if (comboTimer > 0f)
            {
                comboTimer -= dt;
                if (comboTimer <= 0f) combo = 0;
            }
        }

        // ================================================================== números de dano (pool)
        class Pop
        {
            public Text t; public RectTransform rt; public Vector3 world;
            public float time, life, xoff; public bool big, active;
        }
        readonly List<Pop> pops = new List<Pop>();
        const int PopMax = 48;

        void BuildPopups()
        {
            for (int i = 0; i < PopMax; i++)
            {
                var t = UIKit.Txt(popupLayer, "num", "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                var rt = UIKit.Place(t.rectTransform, BL, MID, Vector2.zero, new Vector2(300f, 60f));
                t.gameObject.SetActive(false);
                pops.Add(new Pop { t = t, rt = rt });
            }
        }

        /// <summary>Número de dano puro ("123")? Esses somem com Opções &gt; Números de dano desligado.</summary>
        static bool IsDamageNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (!(ch >= '0' && ch <= '9') && !(i == 0 && ch == '-')) return false;
            }
            return true;
        }

        void SpawnPopup(Vector3 w, string text, Color c, bool big)
        {
            if (!Settings.DamageNumbers && IsDamageNumber(text)) return;
            Pop p = null;
            float oldest = -1f;
            Pop oldestP = null;
            for (int i = 0; i < pops.Count; i++)
            {
                var x = pops[i];
                if (!x.active) { p = x; break; }
                if (x.time > oldest) { oldest = x.time; oldestP = x; }
            }
            if (p == null) p = oldestP;
            if (p == null) return;
            p.active = true; p.world = w; p.time = 0f; p.big = big;
            p.life = big ? 1.0f : 0.8f;
            p.xoff = UnityEngine.Random.Range(-22f, 22f);
            p.t.text = text;
            p.t.color = c;
            p.t.fontSize = big ? 36 : 24;
            p.rt.localScale = Vector3.one;
            p.t.gameObject.SetActive(true);
            p.rt.SetAsLastSibling();
            PlacePopup(p, 0f);
        }

        void PlacePopup(Pop p, float k)
        {
            var cam = CameraRig.Cam;
            if (cam == null) { p.active = false; p.t.gameObject.SetActive(false); return; }
            Vector3 sp = cam.WorldToScreenPoint(p.world + Vector3.up * (k * 1.2f));
            if (sp.z < 0f) { p.active = false; p.t.gameObject.SetActive(false); return; }
            float sf = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            p.rt.anchoredPosition = new Vector2(sp.x / sf + p.xoff * k, sp.y / sf);
        }

        void UpdatePopups(float dt)
        {
            for (int i = 0; i < pops.Count; i++)
            {
                var p = pops[i];
                if (!p.active) continue;
                p.time += dt;
                if (p.time >= p.life) { p.active = false; p.t.gameObject.SetActive(false); continue; }
                float k = p.time / p.life;
                PlacePopup(p, k);
                if (!p.active) continue;
                float punch = p.big ? 1.6f : 1.35f;
                float s = k < 0.12f ? Mathf.Lerp(punch, 1f, k / 0.12f) : 1f;
                p.rt.localScale = new Vector3(s, s, 1f);
                var c = p.t.color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                p.t.color = c;
            }
        }

        // ================================================================== faixa grande central
        class Banner { public string title, sub, tag, sfx; public Color color; }
        readonly List<Banner> bannerQueue = new List<Banner>();
        Banner bannerCur;
        float bannerT;
        CanvasGroup bannerGroup;
        Text bannerTitle, bannerSub;
        Image lineTop, lineBot;
        const float BannerDur = 2.9f;

        void BuildBanner()
        {
            var r = UIKit.R(bannerLayer, "Faixa");
            bannerRt = UIKit.Place(r, new Vector2(0.5f, 0.68f), MID, Vector2.zero, new Vector2(1300f, 128f));
            bannerGroup = r.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f; bannerGroup.blocksRaycasts = false; bannerGroup.interactable = false;
            var band = UIKit.Img(r, "fundo", UIKit.BandSprite(), new Color(0.04f, 0.02f, 0.01f, 0.72f));
            UIKit.Stretch(band.rectTransform);
            lineTop = UIKit.Img(r, "linha1", UIKit.BandSprite(), UIKit.Bronze);
            UIKit.Place(lineTop.rectTransform, TC, MID, Vector2.zero, new Vector2(760f, 2f));
            lineBot = UIKit.Img(r, "linha2", UIKit.BandSprite(), UIKit.Bronze);
            UIKit.Place(lineBot.rectTransform, BC, MID, Vector2.zero, new Vector2(760f, 2f));
            for (int k = 0; k < 2; k++)
            {
                var gem = UIKit.Diamond(k == 0 ? lineTop.rectTransform : lineBot.rectTransform, "gema", UIKit.Gold, 12f);
                UIKit.Place(gem.rectTransform, MID, MID, Vector2.zero, new Vector2(12f, 12f));
            }
            bannerTitle = UIKit.Txt(r, "titulo", "", 52, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Place(bannerTitle.rectTransform, MID, MID, new Vector2(0f, 14f), new Vector2(1300f, 70f));
            bannerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            bannerSub = UIKit.Txt(r, "sub", "", 21, UIKit.TextCol, TextAnchor.MiddleCenter, FontStyle.Italic);
            UIKit.Place(bannerSub.rectTransform, MID, MID, new Vector2(0f, -34f), new Vector2(1200f, 30f));
            bannerSub.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        RectTransform bannerRt;

        static float EaseOutCubic(float k) { k = 1f - Mathf.Clamp01(k); return 1f - k * k * k; }
        static float EaseInOutCubic(float k)
        {
            k = Mathf.Clamp01(k);
            return k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) * 0.5f;
        }

        void QueueBanner(string title, string sub, Color col, string tag, string sfx)
        {
            if (!string.IsNullOrEmpty(tag))
                foreach (var b in bannerQueue)
                    if (b.tag == tag && tag != "area") { b.title = title; b.sub = sub; return; }
            if (tag == "area") bannerQueue.RemoveAll(b => b.tag == "area");
            bannerQueue.Add(new Banner { title = title, sub = sub, color = col, tag = tag, sfx = sfx });
        }

        public void ShowAreaTitle(string title, string subtitle)
        {
            if (!string.IsNullOrEmpty(title)) UIKit.Set(placeText, title);
            if (bannerCur != null && bannerCur.tag == "area") bannerCur = null;
            QueueBanner(title ?? "", subtitle ?? "", Color.white, "area", "");
        }

        void UpdateBanner(float dt)
        {
            if (bannerCur == null)
            {
                if (bannerQueue.Count == 0)
                {
                    if (bannerGroup.alpha > 0f) bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, 0f, dt * 3f);
                    return;
                }
                if (modalKind == "title") return;
                bannerCur = bannerQueue[0];
                bannerQueue.RemoveAt(0);
                bannerT = 0f;
                bannerTitle.text = bannerCur.title;
                bannerTitle.color = bannerCur.color;
                bannerSub.text = bannerCur.sub;
                bannerSub.color = bannerCur.tag == "area" ? UIKit.Gold : UIKit.TextCol;
                lineTop.color = lineBot.color = bannerCur.tag == "skill" ? bannerCur.color : UIKit.Gold;
                if (!string.IsNullOrEmpty(bannerCur.sfx)) Sfx.Play(bannerCur.sfx);
            }
            bannerT += dt;
            float a = bannerT < 0.25f ? bannerT / 0.25f : bannerT > BannerDur - 0.45f ? Mathf.Clamp01((BannerDur - bannerT) / 0.45f) : 1f;
            bannerGroup.alpha = a;
            // entra deslizando da esquerda e sai deslizando para a direita
            float slideIn = 1f - EaseOutCubic(bannerT / 0.4f);
            float slideOut = EaseInOutCubic((bannerT - (BannerDur - 0.45f)) / 0.45f);
            bannerRt.anchoredPosition = new Vector2(-90f * slideIn + 70f * slideOut, 0f);
            float e = EaseOutCubic(bannerT / 0.35f);
            bannerTitle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, e);
            float lw = Mathf.Lerp(0f, 820f, EaseOutCubic(bannerT / 0.6f));
            lineTop.rectTransform.sizeDelta = new Vector2(lw, 2f);
            lineBot.rectTransform.sizeDelta = new Vector2(lw, 2f);
            if (bannerT >= BannerDur) bannerCur = null;
        }

        // ================================================================== avisos (toasts)
        class ToastItem { public RectTransform rt; public CanvasGroup cg; public Text text; public float time, y; public string msg; }
        readonly List<ToastItem> toasts = new List<ToastItem>();
        const float ToastLife = 3.4f, ToastTop = -150f, ToastStep = 42f;

        public void Toast(string msg)
        {
            if (string.IsNullOrEmpty(msg) || toastLayer == null) return;
            if (toasts.Count > 0 && toasts[toasts.Count - 1].msg == msg) { toasts[toasts.Count - 1].time = 0.2f; return; }
            while (toasts.Count >= 4) { Destroy(toasts[0].rt.gameObject); toasts.RemoveAt(0); }
            var p = UIKit.Panel(toastLayer, "aviso", new Color(0.075f, 0.055f, 0.04f, 0.93f));
            p.raycastTarget = false;
            var rt = UIKit.Place(p.rectTransform, TC, TC, Vector2.zero, new Vector2(260f, 36f));
            UIKit.HLayout(p.gameObject, 8f, new RectOffset(14, 18, 7, 7));
            UIKit.Fit(p.gameObject, true, true);
            var dot = UIKit.Img(rt, "ponto", UIKit.DiamondSprite(), UIKit.Gold);
            UIKit.LE(dot, 10f, 10f);
            var t = UIKit.Txt(rt, "texto", msg, 18, UIKit.TextCol, TextAnchor.MiddleLeft);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var cg = p.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.blocksRaycasts = false;
            float y = ToastTop - toasts.Count * ToastStep;
            var item = new ToastItem { rt = rt, cg = cg, text = t, time = 0f, y = y + 20f, msg = msg };
            rt.anchoredPosition = new Vector2(0f, item.y);
            toasts.Add(item);
        }

        void UpdateToasts(float dt)
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                toasts[i].time += dt;
                if (toasts[i].time > ToastLife) { Destroy(toasts[i].rt.gameObject); toasts.RemoveAt(i); }
            }
            float lerp = 1f - Mathf.Exp(-12f * dt);
            for (int i = 0; i < toasts.Count; i++)
            {
                var t = toasts[i];
                float target = ToastTop - i * ToastStep;
                t.y = Mathf.Lerp(t.y, target, lerp);
                t.rt.anchoredPosition = new Vector2(0f, t.y);
                t.cg.alpha = t.time < 0.2f ? t.time / 0.2f : t.time > ToastLife - 0.4f ? Mathf.Clamp01((ToastLife - t.time) / 0.4f) : 1f;
            }
        }

        // ================================================================== dica (tooltip)
        RectTransform tipRt;
        Text tipTitle, tipBody;
        object tipOwner;

        void BuildTooltip()
        {
            var p = UIKit.Panel(tipLayer, "Dica", UIKit.PanelSolid);
            p.raycastTarget = false;
            tipRt = p.rectTransform;
            tipRt.anchorMin = tipRt.anchorMax = MID;
            tipRt.pivot = new Vector2(0f, 1f);
            tipRt.sizeDelta = new Vector2(300f, 80f);
            UIKit.VLayout(p.gameObject, 4f, new RectOffset(13, 13, 10, 11));
            var f = p.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            tipTitle = UIKit.Txt(tipRt, "titulo", "", 18, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            tipBody = UIKit.Txt(tipRt, "texto", "", 15, UIKit.TextCol, TextAnchor.UpperLeft);
            tipBody.lineSpacing = 1.05f;
            tipRt.gameObject.SetActive(false);
        }

        public void ShowTip(object owner, string title, string body)
        {
            if (tipRt == null) return;
            tipOwner = owner;
            tipTitle.text = title ?? "";
            tipBody.text = body ?? "";
            tipBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
            tipRt.gameObject.SetActive(true);
            tipRt.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(tipRt);
            PositionTip();
        }

        public void HideTip(object owner)
        {
            if (owner != null && owner != tipOwner) return;
            tipOwner = null;
            if (tipRt != null && tipRt.gameObject.activeSelf) tipRt.gameObject.SetActive(false);
        }

        void PositionTip()
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, InputW.MousePos, null, out local)) return;
            var r = canvasRt.rect;
            float w = tipRt.rect.width, h = tipRt.rect.height;
            float x = local.x + 22f;
            if (x + w > r.xMax - 8f) x = local.x - 16f - w;
            float y = local.y - 22f;
            if (y - h < r.yMin + 8f) y = local.y + 16f + h;
            tipRt.anchoredPosition = new Vector2(x, y);
        }

        // ------------------------------------------------------------------ arrastar habilidades (usado por SkillDragSource/SkillDropSlot)
        public RectTransform BeginSkillDrag(string skillId)
        {
            var s = GameData.Skill(skillId);
            if (s == null || !GameState.SkillUnlocked(skillId)) return null;
            HideTip(null);
            var g = UIKit.MetalFrame(tipLayer, "arrastando", MetalTint);
            var rt = g.rectTransform;
            rt.anchorMin = rt.anchorMax = MID; rt.pivot = MID; rt.sizeDelta = new Vector2(86f, 86f);
            var cg = g.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false; cg.alpha = 0.92f;
            var inner = UIKit.Round(rt, "fundo", UIKit.SlotBg, 1.6f);
            UIKit.Stretch(inner.rectTransform, 4f);
            var ic = UIKit.Img(rt, "icone", U.Icon(s.icon), Color.white);
            UIKit.Place(ic.rectTransform, MID, MID, Vector2.zero, new Vector2(64f, 64f));
            ic.preserveAspect = true;
            rt.localScale = Vector3.one * 1.08f;
            return rt;
        }

        public void MoveToPointer(RectTransform r, Vector2 screen)
        {
            if (r == null) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out local))
                r.anchoredPosition = local;
        }

        public void DropSkill(int index, string skillId)
        {
            if (!GameState.SkillUnlocked(skillId)) { Sfx.Play("ui_error"); return; }
            GameState.SetSlot(index, skillId);
            Sfx.Play("ui_click");
            FlashSlot(index, UIKit.Gold);
            windowDirty = true;
        }

        // ================================================================== fade
        CanvasGroup fadeGroup;
        Coroutine fadeCo;

        void BuildFade()
        {
            var img = UIKit.Img(fadeLayer, "preto", U.WhiteSprite(), new Color(0.02f, 0.012f, 0.008f, 1f), true);
            UIKit.Stretch(img.rectTransform);
            fadeGroup = img.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false; fadeGroup.interactable = false;
        }

        public void FadeOut(float t) { StartFade(1f, t); }
        public void FadeIn(float t) { StartFade(0f, t); }

        void StartFade(float target, float dur)
        {
            if (fadeCo != null) StopCoroutine(fadeCo);
            if (!isActiveAndEnabled) { fadeGroup.alpha = target; fadeGroup.blocksRaycasts = target > 0.5f; return; }
            fadeCo = StartCoroutine(FadeRoutine(target, dur));
        }

        IEnumerator FadeRoutine(float target, float dur)
        {
            float a0 = fadeGroup.alpha, t = 0f;
            fadeGroup.blocksRaycasts = true;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(a0, target, EaseInOutCubic(t / Mathf.Max(0.0001f, dur)));
                yield return null;
            }
            fadeGroup.alpha = target;
            fadeGroup.blocksRaycasts = target > 0.5f;
            fadeCo = null;
        }

        // ================================================================== eventos do GameState
        int lastLevel = -1;

        void OnStateChanged()
        {
            var P = GameState.P;
            if (cachedClass != GameState.ClassId)
            {
                cachedClass = GameState.ClassId;
                ApplyClassVisuals();
                RefreshSlotSkills();
            }
            var cd = GameState.Class;
            string cname = string.IsNullOrEmpty(P.classId) ? "Sem classe" : cd.name;
            UIKit.Set(nameText, P.playerName + "  <size=16><color=#" + UIKit.ColorHex(classColor) + ">" + cname + "</color></size>");
            UIKit.Set(levelText, P.level.ToString());
            if (lastLevel >= 0 && P.level != lastLevel) xpBar.Reset();
            lastLevel = P.level;
            UIKit.Set(xpText, "Nv " + P.level + "  ·  " + P.xp + " / " + GameState.XpToNext() + " XP");

            int total = TotalXp(P.level, P.xp);
            if (lastTotalXp >= 0 && total > lastTotalXp && Hero != null) SpawnXpPop(total - lastTotalXp);
            lastTotalXp = total;

            UpdateCoins(P.coins);
            RefreshPotions();
            if (modalKind == "shop" || modalKind == "inventory") windowDirty = true;
        }

        void OnInventoryChanged()
        {
            RefreshPotions();
            if (modalKind == "shop" || modalKind == "inventory") windowDirty = true;
        }

        void OnLoadoutChanged()
        {
            if (cachedClass != GameState.ClassId) { cachedClass = GameState.ClassId; ApplyClassVisuals(); }
            RefreshSlotSkills();
            if (modalKind == "skills" || modalKind == "class") windowDirty = true;
        }

        void OnLevelUp(int lv)
        {
            QueueBanner("NÍVEL " + lv, "Vida e mana restauradas  ·  você ficou mais forte", UIKit.Gold, "level", "levelup");
            portraitPop = 1f;
            xpBar.Reset();
        }

        void OnSkillsUnlocked(List<string> ids)
        {
            if (ids == null || ids.Count == 0) return;
            var names = new List<string>();
            foreach (var id in ids) { var s = GameData.Skill(id); if (s != null) names.Add(s.name); }
            if (names.Count == 0) return;
            string sub = string.Join("  ·  ", names) + "  —  veja em Habilidades [K]";
            QueueBanner("NOVA HABILIDADE", sub, U.Hex(GameState.Class.color), "skill", "skill_unlock");
            for (int i = 0; i < 4; i++)
                if (slotSkills[i] != null && ids.Contains(slotSkills[i].id)) FlashSlot(i, UIKit.Gold);
        }

        // ================================================================== loop
        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            var p = Hero;
            bool show = p != null && modalKind != "title";
            if (hudRoot.gameObject.activeSelf != show)
            {
                hudRoot.gameObject.SetActive(show);
                if (mmCam != null) mmCam.enabled = show;
                if (show) { hpBar.Reset(); mpBar.Reset(); stBar.Reset(); xpBar.Reset(); lastHp = -1f; }
            }
            if (show)
            {
                UpdateBars(p, udt);
                UpdateSlots(p, udt);
                UpdateBuffs(p);
                UpdateMinimap(p, udt);
                UpdateBoss(p, udt);
                UpdateXpPops(udt);
                UpdateCombo(Time.deltaTime);
                UpdateCombatHud(p, udt);
                UpdateClassHud(p, udt);
                UpdateUltimate(p, udt);
            }
            UpdateUltFx(udt);
            UpdatePopups(udt);
            UpdateBanner(udt);
            UpdateToasts(udt);
            if (tipRt != null && tipRt.gameObject.activeSelf)
            {
                if (tipOwner is Component c && (c == null || !c.gameObject.activeInHierarchy)) HideTip(null);
                else PositionTip();
            }
        }

        void LateUpdate()
        {
            if (!windowDirty) return;
            windowDirty = false;
            if (modalGo != null && modalRebuild != null)
            {
                var r = modalRebuild;
                r();
            }
        }
    }
}
