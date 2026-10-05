using System.Reflection;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Opções do jogador (PlayerPrefs): volumes, tremor de tela, números de dano, filtro pixel 8-bit,
    /// tela cheia e qualidade. Load() é chamado pelo HUD.Create; Save() grava; Changed avisa quem depende
    /// (PixelFilter). Para gravar sem travar sliders, use SaveSoon() (grava ~0,5 s depois, tempo real).
    /// </summary>
    public static class Settings
    {
        public static bool ScreenShake = true, DamageNumbers = true, PixelFilter = false;
        public static int PixelHeight = 320;
        public static bool Fullscreen = true;
        public static int Quality = -1;   // -1 = não mexe (usa a do projeto)

        public static readonly int[] PixelHeights = { 240, 320, 480 };

        public static event System.Action Changed;

        static bool loaded;
        static float saveAt = -1f;

        const string P = "opt_";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; saveAt = -1f; Changed = null; }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            ScreenShake = PlayerPrefs.GetInt(P + "shake", 1) != 0;
            DamageNumbers = PlayerPrefs.GetInt(P + "dmgnum", 1) != 0;
            PixelFilter = PlayerPrefs.GetInt(P + "pixel", 0) != 0;
            PixelHeight = SnapHeight(PlayerPrefs.GetInt(P + "pixel_h", 320));
            Sfx.MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "vol_master", Sfx.MasterVolume));
            Sfx.MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "vol_music", Sfx.MusicVolume));
            Sfx.SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "vol_sfx", Sfx.SfxVolume));
            Fullscreen = Screen.fullScreen;
            Quality = PlayerPrefs.GetInt(P + "quality", -1);
            if (Quality >= 0 && Quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != Quality)
                QualitySettings.SetQualityLevel(Quality, true);
            SettingsRunner.Ensure();
            Changed?.Invoke();
        }

        public static void Save()
        {
            saveAt = -1f;
            PlayerPrefs.SetInt(P + "shake", ScreenShake ? 1 : 0);
            PlayerPrefs.SetInt(P + "dmgnum", DamageNumbers ? 1 : 0);
            PlayerPrefs.SetInt(P + "pixel", PixelFilter ? 1 : 0);
            PlayerPrefs.SetInt(P + "pixel_h", PixelHeight);
            PlayerPrefs.SetFloat(P + "vol_master", Sfx.MasterVolume);
            PlayerPrefs.SetFloat(P + "vol_music", Sfx.MusicVolume);
            PlayerPrefs.SetFloat(P + "vol_sfx", Sfx.SfxVolume);
            PlayerPrefs.SetInt(P + "quality", Quality);
            PlayerPrefs.Save();
        }

        /// <summary>Grava daqui a pouco (evita escrever a cada frame enquanto um slider é arrastado).</summary>
        public static void SaveSoon() { saveAt = Time.unscaledTime + 0.5f; SettingsRunner.Ensure(); }

        /// <summary>Avisa os ouvintes e grava.</summary>
        public static void Apply()
        {
            Changed?.Invoke();
            Save();
        }

        public static void SetFullscreen(bool on)
        {
            Fullscreen = on;
            Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.fullScreen = on;
        }

        public static void SetQuality(int level)
        {
            if (level < 0 || level >= QualitySettings.names.Length) return;
            Quality = level;
            QualitySettings.SetQualityLevel(level, true);
            Apply();
        }

        static int SnapHeight(int h)
        {
            int best = PixelHeights[0];
            foreach (int v in PixelHeights) if (Mathf.Abs(v - h) < Mathf.Abs(best - h)) best = v;
            return best;
        }

        internal static void Tick()
        {
            if (saveAt > 0f && Time.unscaledTime >= saveAt) Save();
        }
    }

    /// <summary>
    /// Ajudante das opções: grava com atraso e, com "Tremor de tela" desligado, zera o trauma do CameraRig
    /// antes do LateUpdate dele (ordem -1000). TODO(contrato): trocar a reflexão por uma flag pública no
    /// CameraRig/Game.Shake quando o dono desses arquivos puder editar.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("")]
    internal class SettingsRunner : MonoBehaviour
    {
        static SettingsRunner inst;
        static FieldInfo traumaField;
        static bool searched;

        public static void Ensure()
        {
            if (inst != null || !Application.isPlaying) return;
            var g = new GameObject("Opcoes");
            DontDestroyOnLoad(g);
            inst = g.AddComponent<SettingsRunner>();
        }

        void Update() { Settings.Tick(); }

        void LateUpdate()
        {
            if (Settings.ScreenShake || CameraRig.I == null) return;
            if (!searched)
            {
                searched = true;
                traumaField = typeof(CameraRig).GetField("trauma", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            if (traumaField != null) traumaField.SetValue(CameraRig.I, 0f);
        }

        void OnApplicationQuit() { Settings.Save(); }

        void OnDestroy() { if (inst == this) inst = null; }
    }
}
