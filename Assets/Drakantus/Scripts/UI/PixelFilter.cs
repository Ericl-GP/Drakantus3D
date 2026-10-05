using UnityEngine;
using UnityEngine.UI;

namespace Drakantus
{
    /// <summary>
    /// Filtro "pixel 8-bit" (Opções). Ligado: uma câmera auxiliar copia a CameraRig.Cam a cada quadro e renderiza
    /// numa RenderTexture baixa (altura Settings.PixelHeight, filtro Point), mostrada em tela cheia numa RawImage
    /// num Canvas ABAIXO da HUD. A câmera principal fica desligada mas continua sendo a referência de
    /// ScreenPointToRay/WorldToScreenPoint (mira, números de dano), que assim seguem em coordenadas de tela.
    /// Desligado: tudo volta ao normal. Trata mudança de resolução da janela.
    /// </summary>
    [DefaultExecutionOrder(10000)]   // depois do CameraRig.LateUpdate
    public class PixelFilter : MonoBehaviour
    {
        public static PixelFilter I;

        Canvas canvas;
        RawImage view;
        Camera pixCam, mainCam;
        RenderTexture rt;
        bool active;
        int lastW, lastH, lastPH;

        public static void Ensure()
        {
            if (I != null) return;
            var g = new GameObject("FiltroPixel");
            I = g.AddComponent<PixelFilter>();
        }

        void Awake()
        {
            if (I == null) I = this;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;   // abaixo da HUD (50)
            var r = UIKit.R(transform, "imagem");
            UIKit.Stretch(r);
            view = r.gameObject.AddComponent<RawImage>();
            view.raycastTarget = false;
            view.color = Color.white;
            canvas.enabled = false;
            Settings.Changed += OnSettingsChanged;
        }

        void OnDestroy()
        {
            Settings.Changed -= OnSettingsChanged;
            Deactivate();
            if (pixCam != null) Destroy(pixCam.gameObject);
            if (I == this) I = null;
        }

        void OnSettingsChanged() { lastPH = -1; }

        void LateUpdate()
        {
            var cam = CameraRig.Cam;
            bool want = Settings.PixelFilter && cam != null;
            if (!want) { if (active) Deactivate(); return; }
            if (mainCam != cam) { if (active) Deactivate(); mainCam = cam; }
            if (!active) Activate();
            EnsureTexture();
            Sync();
        }

        void Activate()
        {
            active = true;
            if (pixCam == null)
            {
                var g = new GameObject("CameraPixel");
                pixCam = g.AddComponent<Camera>();
                var urp = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(pixCam);
                if (urp != null)
                {
                    urp.renderPostProcessing = true;
                    urp.renderShadows = true;
                    urp.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                }
            }
            pixCam.CopyFrom(mainCam);
            pixCam.allowMSAA = false;
            pixCam.depth = mainCam.depth;
            pixCam.enabled = true;
            mainCam.enabled = false;
            lastPH = -1;
            EnsureTexture();
            canvas.enabled = true;
        }

        void Deactivate()
        {
            active = false;
            if (mainCam != null) mainCam.enabled = true;
            if (pixCam != null) { pixCam.targetTexture = null; pixCam.enabled = false; }
            if (canvas != null) canvas.enabled = false;
            if (view != null) view.texture = null;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        }

        void EnsureTexture()
        {
            int sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            int ph = Mathf.Clamp(Settings.PixelHeight, 120, 1080);
            if (rt != null && sw == lastW && sh == lastH && ph == lastPH) return;
            lastW = sw; lastH = sh; lastPH = ph;
            int h = Mathf.Min(ph, sh);
            int w = Mathf.Max(1, Mathf.RoundToInt(h * (sw / (float)sh)));
            if (pixCam != null) pixCam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "FiltroPixel", filterMode = FilterMode.Point, antiAliasing = 1 };
            rt.Create();
            if (pixCam != null) pixCam.targetTexture = rt;
            view.texture = rt;
        }

        /// <summary>Copia posição, rotação e lente da câmera principal (que o CameraRig acabou de mover).</summary>
        void Sync()
        {
            if (pixCam == null || mainCam == null) return;
            pixCam.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
            pixCam.fieldOfView = mainCam.fieldOfView;
            pixCam.nearClipPlane = mainCam.nearClipPlane;
            pixCam.farClipPlane = mainCam.farClipPlane;
            pixCam.backgroundColor = mainCam.backgroundColor;
            pixCam.clearFlags = mainCam.clearFlags;
            pixCam.cullingMask = mainCam.cullingMask;
            if (pixCam.targetTexture != rt) pixCam.targetTexture = rt;
        }
    }
}
