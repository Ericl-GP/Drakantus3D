using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Drakantus
{
    /// <summary>
    /// Janela "Aparência" (botão na Bolsa): cor do cabelo, roupa primária e secundária.
    /// Paletas de 12 cores + "Original" + slider de tom. Prévia no boneco (PreviewRig).
    /// Salvo em GameState.P.hairColor/clothColor1/clothColor2 (hex, "" = original) e hairTone/cloth1Tone/cloth2Tone (0..1).
    /// A troca de cor em si é feita por AppearanceTex (Actors/Equipment.cs), recolorindo uma cópia da textura atlas.
    /// </summary>
    public partial class HUD
    {
        static readonly string[] AppHairPalette =
        {
            "1c1612", "3b2a20", "6b4228", "b05a2c", "d8452a", "e3b45c",
            "f3e6b8", "9c9c9c", "f5f5f5", "3b62d8", "8d43d8", "e8609e"
        };
        static readonly string[] AppClothPalette =
        {
            "c23a3a", "e07a2a", "e8c840", "4caf50", "1f7a5a", "2aa8c8",
            "3a62c8", "283a78", "7a48c8", "c84890", "f0ece0", "3a3a42"
        };

        public void AppearanceWindow()
        {
            PreviewRig.Get();
            var c = OpenModal("appearance", "Aparência", new Vector2(1240f, 820f), true);

            // ---- boneco
            var left = UIKit.R(c, "boneco");
            UIKit.Place(left, TL, TL, Vector2.zero, new Vector2(420f, 654f));
            var bg = UIKit.Round(left, "fundo", new Color(0f, 0f, 0f, 0.22f), 1.4f);
            UIKit.Stretch(bg.rectTransform);
            var halo = UIKit.Img(left, "halo", UIKit.SoftSprite(), new Color(1f, 0.8f, 0.55f, 0.09f));
            UIKit.Place(halo.rectTransform, MID, MID, Vector2.zero, new Vector2(400f, 560f));
            var raw = UIKit.R(left, "previa").gameObject.AddComponent<RawImage>();
            raw.texture = PreviewRig.Get().Texture;
            raw.raycastTarget = true;
            UIKit.Place(raw.rectTransform, MID, MID, new Vector2(0f, 10f), new Vector2(380f, 559f));
            raw.gameObject.AddComponent<DollRotate>();
            Label(left, "Arraste para girar", 14, UIKit.DimText, BC, BC, new Vector2(0f, 8f), new Vector2(380f, 20f), TextAnchor.MiddleCenter, FontStyle.Italic);

            // ---- seções
            var right = UIKit.R(c, "cores");
            UIKit.Place(right, TL, TL, new Vector2(448f, 0f), new Vector2(736f, 654f));
            var P = GameState.P;
            AppSection(right, 0f, "CABELO", AppHairPalette, P.hairColor, P.hairTone,
                hex => P.hairColor = hex, t => P.hairTone = t);
            AppSection(right, -200f, "ROUPA PRIMÁRIA", AppClothPalette, P.clothColor1, P.cloth1Tone,
                hex => P.clothColor1 = hex, t => P.cloth1Tone = t);
            AppSection(right, -400f, "ROUPA SECUNDÁRIA", AppClothPalette, P.clothColor2, P.cloth2Tone,
                hex => P.clothColor2 = hex, t => P.cloth2Tone = t);

            var back = UIKit.Btn(right, "Voltar à bolsa", new Vector2(230f, 48f), InventoryWindow, UIKit.BtnCol, 20);
            UIKit.Place(UIKit.RT(back), BL, BL, new Vector2(0f, -64f), new Vector2(230f, 48f));
            var reset = UIKit.Btn(right, "Restaurar tudo", new Vector2(230f, 48f), () =>
            {
                P.hairColor = ""; P.clothColor1 = ""; P.clothColor2 = "";
                P.hairTone = 0.5f; P.cloth1Tone = 0.5f; P.cloth2Tone = 0.5f;
                PreviewRig.AppearanceChanged();
                windowDirty = true;
            }, UIKit.DangerCol, 20);
            UIKit.Place(UIKit.RT(reset), BL, BL, new Vector2(246f, -64f), new Vector2(230f, 48f));

            modalRebuild = AppearanceWindow;
        }

        void AppSection(RectTransform parent, float y, string title, string[] palette, string current, float tone,
                        Action<string> setHex, Action<float> setTone)
        {
            var sec = UIKit.Round(parent, "secao_" + title, new Color(0f, 0f, 0f, 0.25f), 1.4f);
            var sr = UIKit.Place(sec.rectTransform, TL, TL, new Vector2(0f, y), new Vector2(736f, 186f));
            Label(sr, title, 16, UIKit.Gold, TL, TL, new Vector2(18f, -10f), new Vector2(400f, 26f), TextAnchor.MiddleLeft, FontStyle.Bold);

            bool original = string.IsNullOrEmpty(current);
            var bo = UIKit.Btn(sr, original ? "Original (atual)" : "Original", new Vector2(170f, 34f), () =>
            {
                setHex("");
                PreviewRig.AppearanceChanged();
                windowDirty = true;
            }, original ? UIKit.Gold : UIKit.BtnCol, 17);
            UIKit.Place(UIKit.RT(bo), TR, TR, new Vector2(-14f, -8f), new Vector2(170f, 34f));

            // amostras
            const float sw = 50f, gap = 8f;
            for (int i = 0; i < palette.Length; i++)
            {
                string hex = palette[i];
                bool sel = !original && string.Equals(hex, current, StringComparison.OrdinalIgnoreCase);
                var frame = UIKit.Round(sr, "cor_" + hex, sel ? UIKit.Gold : new Color(1f, 1f, 1f, 0.16f), 1.6f, true);
                UIKit.Place(frame.rectTransform, TL, TL, new Vector2(18f + i * (sw + gap), -48f), new Vector2(sw, sw));
                var fill = UIKit.Round(frame.rectTransform, "cor", U.Hex(hex), 1.6f);
                UIKit.Stretch(fill.rectTransform, sel ? 4f : 2f);
                frame.gameObject.AddComponent<UIHover>().scale = 1.1f;
                frame.gameObject.AddComponent<UIClick>().onLeft = () =>
                {
                    Sfx.Play("ui_click");
                    setHex(hex);
                    PreviewRig.AppearanceChanged();
                    windowDirty = true;
                };
            }

            // tom
            Label(sr, "Tom", 16, UIKit.DimText, TL, TL, new Vector2(18f, -122f), new Vector2(60f, 40f));
            Label(sr, "escuro", 13, UIKit.DimText, TL, TL, new Vector2(80f, -122f), new Vector2(60f, 40f), TextAnchor.MiddleRight);
            Color fillCol = original ? new Color(0.45f, 0.47f, 0.53f, 1f) : AppearanceTex.ApplyTone(U.Hex(current), tone);
            var slider = UIKit.MakeSlider(sr, "tom", tone, v =>
            {
                setTone(v);
                PreviewRig.AppearanceChanged();
            }, fillCol);
            UIKit.Place(UIKit.RT(slider), TL, TL, new Vector2(150f, -122f), new Vector2(460f, 40f));
            if (original) slider.interactable = false;
            Label(sr, "claro", 13, UIKit.DimText, TL, TL, new Vector2(620f, -122f), new Vector2(60f, 40f));
        }
    }
}
