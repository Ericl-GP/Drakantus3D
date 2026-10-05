using UnityEngine;
using UnityEngine.UI;

namespace Drakantus
{
    /// <summary>
    /// Menu principal (partial do HUD): logo, Novo jogo / Continuar / Opções / Sair com a cidade ao fundo
    /// (montada pelo Game.BuildBackdrop) e música "menu". Também a janela de Opções (aberta pelo menu ou pela Pausa).
    /// O fluxo de início continua em StartFromTitle (Windows.cs) → Game.StartGame().
    /// </summary>
    public partial class HUD
    {
        bool titleAskName;

        // ================================================================== tela de título
        public void ShowTitleScreen()
        {
            Sfx.Music("menu");
            var c = OpenModal("title", "", Vector2.zero, false, MStyle.Full, new Color(0.02f, 0.012f, 0.008f, 0.18f));

            // escurece só o lado esquerdo (a cidade continua visível à direita)
            var shade = UIKit.Img(c, "sombra", U.WhiteSprite(), new Color(0.03f, 0.02f, 0.012f, 0.72f));
            shade.rectTransform.anchorMin = new Vector2(0f, 0f); shade.rectTransform.anchorMax = new Vector2(0.36f, 1f);
            shade.rectTransform.offsetMin = Vector2.zero; shade.rectTransform.offsetMax = Vector2.zero;
            var fadeR = UIKit.Img(c, "sombra_borda", UIKit.BandSprite(), new Color(0.03f, 0.02f, 0.012f, 0.72f));
            fadeR.rectTransform.anchorMin = new Vector2(0.36f, 0f); fadeR.rectTransform.anchorMax = new Vector2(0.36f, 1f);
            fadeR.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            fadeR.rectTransform.sizeDelta = new Vector2(520f, 0f);
            fadeR.rectTransform.anchoredPosition = Vector2.zero;
            var line = UIKit.Img(c, "filete", U.WhiteSprite(), new Color(UIKit.Bronze.r, UIKit.Bronze.g, UIKit.Bronze.b, 0.25f));
            line.rectTransform.anchorMin = new Vector2(0.36f, 0.08f); line.rectTransform.anchorMax = new Vector2(0.36f, 0.92f);
            line.rectTransform.sizeDelta = new Vector2(1f, 0f); line.rectTransform.anchoredPosition = new Vector2(-150f, 0f);

            // logo
            var col = UIKit.R(c, "coluna");
            col.anchorMin = new Vector2(0f, 0f); col.anchorMax = new Vector2(0f, 1f); col.pivot = new Vector2(0f, 0.5f);
            col.sizeDelta = new Vector2(620f, 0f); col.anchoredPosition = new Vector2(110f, 0f);

            var glow = UIKit.Img(col, "brilho", UIKit.SoftSprite(), new Color(1f, 0.65f, 0.25f, 0.12f));
            UIKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(900f, 360f));
            var logo = UIKit.Txt(col, "logo", "DRAKANTUS", 96, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIKit.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(900f, 120f));
            logo.horizontalOverflow = HorizontalWrapMode.Overflow;
            var deep = logo.gameObject.AddComponent<Shadow>();
            deep.effectColor = new Color(0.4f, 0.16f, 0.03f, 0.85f); deep.effectDistance = new Vector2(0f, -6f);
            for (int k = 0; k < 2; k++)
            {
                var ln = UIKit.Img(col, "linha", UIKit.BandSprite(), UIKit.Bronze);
                UIKit.Place(ln.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, k == 0 ? 238f : 362f), new Vector2(560f, 2f));
                var gem = UIKit.Diamond(ln.rectTransform, "gema", UIKit.Gold, 12f);
                UIKit.Place(gem.rectTransform, MID, MID, Vector2.zero, new Vector2(12f, 12f));
            }
            var sub = UIKit.Txt(col, "sub", "A Torre de Aster", 24, UIKit.TextCol, TextAnchor.MiddleCenter, FontStyle.Italic);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 212f), new Vector2(600f, 34f));

            if (titleAskName) BuildNamePrompt(col);
            else BuildTitleButtons(col);

            Label(c, "v3D  ·  Esc: pausa e opções no jogo", 13, new Color(UIKit.DimText.r, UIKit.DimText.g, UIKit.DimText.b, 0.7f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 16f), new Vector2(500f, 20f));
            modalRebuild = ShowTitleScreen;
        }

        void BuildTitleButtons(RectTransform col)
        {
            bool hasSave = GameState.HasSave();
            float y = 90f;
            const float W = 380f, H = 52f, STEP = 64f;
            var mid = new Vector2(0.5f, 0.5f);
            if (hasSave)
            {
                var bCont = UIKit.Btn(col, "Continuar", new Vector2(W, H), () =>
                {
                    if (!GameState.Load())
                    {
                        Sfx.Play("ui_error");
                        Toast("Não foi possível carregar o jogo salvo.");
                        return;
                    }
                    StartFromTitle();
                }, UIKit.Gold, 24);
                UIKit.Place(UIKit.RT(bCont), mid, mid, new Vector2(0f, y), new Vector2(W, H)); y -= STEP;
            }
            var bNew = UIKit.Btn(col, "Novo jogo", new Vector2(W, H), () => { titleAskName = true; windowDirty = true; },
                hasSave ? UIKit.BtnCol : UIKit.Gold, 24);
            UIKit.Place(UIKit.RT(bNew), mid, mid, new Vector2(0f, y), new Vector2(W, H)); y -= STEP;
            var bOpt = UIKit.Btn(col, "Opções", new Vector2(W, H), () => OptionsWindow(true), UIKit.BtnCol, 22);
            UIKit.Place(UIKit.RT(bOpt), mid, mid, new Vector2(0f, y), new Vector2(W, H)); y -= STEP;
            var bQuit = UIKit.Btn(col, "Sair", new Vector2(W, H), QuitGame, UIKit.DangerCol, 22);
            UIKit.Place(UIKit.RT(bQuit), mid, mid, new Vector2(0f, y), new Vector2(W, H));
        }

        void BuildNamePrompt(RectTransform col)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var panel = UIKit.Panel(col, "novo_jogo", UIKit.PanelSolid);
            var pr = UIKit.Place(panel.rectTransform, mid, mid, new Vector2(0f, 0f), new Vector2(440f, 250f));
            Label(pr, "Nome do herói", 17, UIKit.DimText, TC, TC, new Vector2(0f, -20f), new Vector2(380f, 24f), TextAnchor.MiddleCenter);
            var field = UIKit.MakeInput(pr, "nome", "Aventureiro", 16);
            UIKit.Place(UIKit.RT(field), TC, TC, new Vector2(0f, -50f), new Vector2(380f, 50f));
            bool hasSave = GameState.HasSave();
            if (hasSave)
                Label(pr, "Substitui o jogo salvo.", 13, new Color(1f, 0.6f, 0.5f, 0.9f), TC, TC, new Vector2(0f, -106f), new Vector2(380f, 20f), TextAnchor.MiddleCenter, FontStyle.Italic);
            var bGo = UIKit.Btn(pr, "Começar", new Vector2(220f, 50f), () =>
            {
                titleAskName = false;
                GameState.NewGame(field.text);
                StartFromTitle();
            }, UIKit.Gold, 22);
            UIKit.Place(UIKit.RT(bGo), BC, BC, new Vector2(56f, 22f), new Vector2(220f, 50f));
            var bBack = UIKit.Btn(pr, "Voltar", new Vector2(120f, 50f), () => { titleAskName = false; windowDirty = true; }, UIKit.BtnCol, 20);
            UIKit.Place(UIKit.RT(bBack), BC, BC, new Vector2(-122f, 22f), new Vector2(120f, 50f));
        }

        void QuitGame()
        {
            if (Game.I != null && Game.I.Started) GameState.Save();
            Settings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ================================================================== opções
        /// <summary>Opções (volumes, tremor, números de dano, filtro pixel, tela cheia, qualidade). fromTitle: "Voltar" volta ao menu.</summary>
        public void OptionsWindow(bool fromTitle)
        {
            var c = OpenModal("options", "Opções", new Vector2(640f, 640f), !fromTitle, MStyle.Center, null, false);
            float y = -4f;
            const float LW = 200f, CW = 330f;

            Text AddRow(string label)
            {
                return Label(c, label, 17, UIKit.TextCol, TL, TL, new Vector2(10f, y), new Vector2(LW, 32f));
            }

            void AddSlider(string label, float v, System.Action<float> set, Color fill)
            {
                AddRow(label);
                var s = UIKit.MakeSlider(c, label, v, val => { set(val); Settings.SaveSoon(); }, fill);
                UIKit.Place(UIKit.RT(s), TL, TL, new Vector2(LW + 20f, y - 2f), new Vector2(CW, 34f));
                y -= 44f;
            }

            void AddToggle(string label, bool on, System.Action<bool> set)
            {
                AddRow(label);
                var b = UIKit.Btn(c, on ? "Ligado" : "Desligado", new Vector2(150f, 34f), () =>
                {
                    set(!on);
                    Settings.Apply();
                    windowDirty = true;
                }, on ? UIKit.Gold : UIKit.BtnCol, 16);
                UIKit.Place(UIKit.RT(b), TL, TL, new Vector2(LW + 20f, y), new Vector2(150f, 34f));
                y -= 44f;
            }

            Label(c, "SOM", 13, UIKit.Gold, TL, TL, new Vector2(10f, y), new Vector2(300f, 18f), TextAnchor.MiddleLeft, FontStyle.Bold); y -= 24f;
            AddSlider("Volume geral", Sfx.MasterVolume, v => Sfx.MasterVolume = v, UIKit.Gold);
            AddSlider("Música", Sfx.MusicVolume, v => Sfx.MusicVolume = v, UIKit.MpCol);
            AddSlider("Efeitos", Sfx.SfxVolume, v => Sfx.SfxVolume = v, UIKit.HpCol);

            y -= 6f;
            Label(c, "JOGO", 13, UIKit.Gold, TL, TL, new Vector2(10f, y), new Vector2(300f, 18f), TextAnchor.MiddleLeft, FontStyle.Bold); y -= 24f;
            AddToggle("Tremor de tela", Settings.ScreenShake, v => Settings.ScreenShake = v);
            AddToggle("Números de dano", Settings.DamageNumbers, v => Settings.DamageNumbers = v);

            y -= 6f;
            Label(c, "VÍDEO", 13, UIKit.Gold, TL, TL, new Vector2(10f, y), new Vector2(300f, 18f), TextAnchor.MiddleLeft, FontStyle.Bold); y -= 24f;
            AddToggle("Filtro pixel 8-bit", Settings.PixelFilter, v => Settings.PixelFilter = v);
            if (Settings.PixelFilter)
            {
                AddRow("Resolução do pixel");
                float x = LW + 20f;
                foreach (int h in Settings.PixelHeights)
                {
                    int hh = h;
                    bool sel = Settings.PixelHeight == h;
                    var b = UIKit.Btn(c, h + "p", new Vector2(100f, 34f), () => { Settings.PixelHeight = hh; Settings.Apply(); windowDirty = true; },
                        sel ? UIKit.Gold : UIKit.BtnCol, 16);
                    UIKit.Place(UIKit.RT(b), TL, TL, new Vector2(x, y), new Vector2(100f, 34f));
                    x += 108f;
                }
                y -= 44f;
            }
            AddToggle("Tela cheia", Settings.Fullscreen, v => Settings.SetFullscreen(v));

            AddRow("Qualidade");
            var names = QualitySettings.names;
            int q = QualitySettings.GetQualityLevel();
            if (names.Length > 0)
            {
                var bq = UIKit.Btn(c, "‹  " + names[Mathf.Clamp(q, 0, names.Length - 1)] + "  ›", new Vector2(CW, 34f), () =>
                {
                    Settings.SetQuality((QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length);
                    windowDirty = true;
                }, UIKit.BtnCol, 16);
                UIKit.Place(UIKit.RT(bq), TL, TL, new Vector2(LW + 20f, y), new Vector2(CW, 34f));
            }

            var back = UIKit.Btn(c, "Voltar", new Vector2(200f, 46f), () =>
            {
                Settings.Save();
                if (fromTitle) ShowTitleScreen();
                else if (Hero != null) PauseWindow();
                else CloseModal();
            }, UIKit.Gold, 20);
            UIKit.Place(UIKit.RT(back), BC, BC, new Vector2(0f, 0f), new Vector2(200f, 46f));
            modalRebuild = () => OptionsWindow(fromTitle);
        }
    }
}
