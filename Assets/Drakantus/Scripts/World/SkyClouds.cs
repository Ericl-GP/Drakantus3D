using UnityEngine;
using UnityEngine.Rendering;

namespace Drakantus
{
    /// <summary>
    /// Nuvens 3D low-poly em duas camadas. Adicionado sozinho pela CameraRig (não precisa colocar na cena).
    ///
    /// 1) CÉU: nuvens grandes e distantes, girando devagar em volta do herói (aparecem na vista de 3ª pessoa,
    ///    perto do horizonte). Só em mapas ao ar livre (não em "interior" nem em mapas escuros).
    ///
    /// 2) ANEL QUE FECHA: quando a câmera se afasta além da vista isométrica (CameraRig.cloudClose 0 → 1),
    ///    bancos de nuvens nascem pelas bordas (escondendo o limite do mapa) e vão se fechando em espiral
    ///    como uma íris até cobrir a tela inteira. Aí a CameraRig volta sozinha à distância padrão e as
    ///    nuvens se abrem de novo.
    ///
    /// Sem colisores (não atrapalha mira/cliques). Material: shader Drakantus/CloudPuff (sem neblina);
    /// se o shader não for encontrado usa o URP Lit.
    /// </summary>
    [DefaultExecutionOrder(100)]   // depois da CameraRig.LateUpdate
    public class SkyClouds : MonoBehaviour
    {
        public static SkyClouds I;

        [Header("Anel que fecha (zoom-out)")]
        public int bankCount = 140;               // bancos de nuvem no disco (cada um tem 3–4 "bolas")
        public float sizeFactor = 0.28f;          // tamanho do banco = distância da câmera × isto
        public float minHeightFactor = 0.10f;     // altura mínima acima do chão = distância × isto
        public float heightRangeFactor = 0.20f;   // variação de altura = distância × isto

        [Header("Céu (nuvens distantes)")]
        public bool ambientSky = true;
        public int skyCount = 48;
        public float skyRadiusMin = 150f, skyRadiusMax = 230f;
        public float skyHeightMin = 15f, skyHeightMax = 55f;
        public float skySize = 38f;
        public float skyDrift = 0.004f;           // rad/s

        class Bank
        {
            public GameObject go;
            public Transform t;
            public float u, ang, swirl, h, size, seed, r, speed;
            public bool on;
        }

        static Mesh sphere;
        Material mat;
        bool hasShade;
        GameObject ringRoot, skyRoot;
        Bank[] ring, sky;
        LevelInfo lastLevel;
        bool skyOn = true;

        void Awake()
        {
            I = this;
            sphere = GetSphere();
            BuildMaterial();
            ApplyColors(null);

            var cam = GetComponent<Camera>();
            if (cam != null && cam.farClipPlane < 330f) cam.farClipPlane = 330f;   // nuvens do céu ficam a ~240 m

            ringRoot = new GameObject("Nuvens_Anel");
            skyRoot = new GameObject("Nuvens_Ceu");
            ring = Build(ringRoot.transform, bankCount, false);
            sky = Build(skyRoot.transform, skyCount, true);
            ringRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (ringRoot != null) Destroy(ringRoot);
            if (skyRoot != null) Destroy(skyRoot);
            if (mat != null) Destroy(mat);
        }

        // ------------------------------------------------------------------ montagem
        static Mesh GetSphere()
        {
            if (sphere != null) return sphere;
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere = g.GetComponent<MeshFilter>().sharedMesh;
            Destroy(g);   // o colisor vai junto
            return sphere;
        }

        void BuildMaterial()
        {
            var sh = Shader.Find("Drakantus/CloudPuff");
            if (sh != null) { mat = new Material(sh); hasShade = true; }
            else { mat = new Material(U.Lit(Color.white, 0f)); hasShade = false; }
            mat.name = "Nuvem";
        }

        static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        Bank[] Build(Transform parent, int n, bool isSky)
        {
            var rng = new System.Random(isSky ? 91 : 17);
            var arr = new Bank[n];
            for (int i = 0; i < n; i++)
            {
                var b = new Bank();
                b.go = new GameObject("n" + i);
                b.t = b.go.transform;
                b.t.SetParent(parent, false);
                b.u = Mathf.Sqrt((i + 0.5f) / n);                    // raio normalizado: espiral de girassol cobre o disco por igual
                b.ang = i * 2.39996323f + R(rng, -0.25f, 0.25f);     // ângulo de ouro
                b.swirl = R(rng, 0.3f, 0.8f);
                b.h = (float)rng.NextDouble();
                b.size = R(rng, 0.75f, 1.25f);
                b.seed = R(rng, 0f, 6.28f);
                b.r = R(rng, skyRadiusMin, skyRadiusMax);
                b.speed = skyDrift * R(rng, 0.6f, 1.4f);

                int puffs = 3 + rng.Next(0, 2);
                for (int k = 0; k < puffs; k++)
                {
                    var g = new GameObject("p");
                    g.transform.SetParent(b.t, false);
                    float s = k == 0 ? 1f : R(rng, 0.5f, 0.85f);
                    g.transform.localPosition = k == 0 ? Vector3.zero
                        : new Vector3(R(rng, -0.55f, 0.55f), R(rng, -0.05f, 0.18f), R(rng, -0.55f, 0.55f));
                    g.transform.localScale = new Vector3(s, s * 0.6f, s);   // achatadas: parecem nuvem, não bola
                    g.AddComponent<MeshFilter>().sharedMesh = sphere;
                    var mr = g.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = LightProbeUsage.Off;
                    mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }

                if (isSky)
                {
                    b.t.localScale = Vector3.one * (skySize * b.size);
                    b.on = true;
                }
                else b.go.SetActive(false);
                arr[i] = b;
            }
            return arr;
        }

        // ------------------------------------------------------------------ mapa atual
        void SyncLevel()
        {
            var lv = Game.I != null ? Game.I.level : null;
            if (lv == lastLevel) return;
            lastLevel = lv;
            ApplyColors(lv);
            skyOn = ambientSky && (lv == null || (lv.kind != "interior" && !lv.dark));
            skyRoot.SetActive(skyOn);
        }

        void ApplyColors(LevelInfo lv)
        {
            Color lit = Color.white;
            Color shade = new Color(0.64f, 0.70f, 0.82f);
            if (lv != null)
            {
                lit = Color.Lerp(Color.white, lv.sun, 0.35f);
                shade = Color.Lerp(shade, lv.fog, 0.35f);
                float k = lv.dark ? 0.4f : 1f;       // masmorra escura: nuvens escuras
                lit *= k; shade *= k;
            }
            lit.a = 1f; shade.a = 1f;
            if (hasShade) { mat.SetColor("_BaseColor", lit); mat.SetColor("_ShadeColor", shade); }
            else mat.color = lit;
        }

        // ------------------------------------------------------------------ por quadro
        void LateUpdate()
        {
            var rig = CameraRig.I;
            if (rig == null) return;
            SyncLevel();

            float t = Time.time;
            Vector3 c = rig.focusPoint;
            float gy = rig.target != null ? rig.target.position.y : c.y - 1f;
            UpdateRing(rig, c, gy, t);
            UpdateSky(c, gy, t);
        }

        /// <summary>Disco de nuvens que se fecha em espiral: as de fora surgem primeiro, as do centro por último.</summary>
        void UpdateRing(CameraRig rig, Vector3 c, float gy, float t)
        {
            float close = rig.cloudClose;
            bool on = close > 0.002f;
            if (ringRoot.activeSelf != on) ringRoot.SetActive(on);
            if (!on) return;

            float D = rig.distance;
            float R0 = Mathf.Max(20f, D);          // raio do disco ≈ o que a câmera enxerga
            float sz = D * sizeFactor;

            for (int i = 0; i < ring.Length; i++)
            {
                var b = ring[i];
                float p = Mathf.Clamp01((close - (1f - b.u) * 0.6f) / 0.4f);   // u=1 (borda) começa em 0; u=0 (centro) em 0,6
                float e = p * p * (3f - 2f * p);
                bool show = e > 0.004f;
                if (show != b.on) { b.on = show; b.go.SetActive(show); }
                if (!show) continue;

                float r = b.u * R0 * Mathf.Lerp(1.5f, 1f, e);                  // desliza para dentro enquanto cresce
                float a = b.ang + (1f - e) * b.swirl + t * 0.02f;               // gira um pouco: efeito de íris
                float h = D * (minHeightFactor + heightRangeFactor * b.h) + Mathf.Sin(t * 0.5f + b.seed) * 0.02f * D;
                b.t.position = new Vector3(c.x + Mathf.Cos(a) * r, gy + h, c.z + Mathf.Sin(a) * r);
                b.t.localScale = Vector3.one * (sz * b.size * e);
            }
        }

        /// <summary>Nuvens distantes: seguem o herói e giram devagar em volta dele.</summary>
        void UpdateSky(Vector3 c, float gy, float t)
        {
            if (!skyOn) return;
            for (int i = 0; i < sky.Length; i++)
            {
                var b = sky[i];
                float a = b.ang + t * b.speed;
                float h = Mathf.Lerp(skyHeightMin, skyHeightMax, b.h);
                b.t.position = new Vector3(c.x + Mathf.Cos(a) * b.r, gy + h, c.z + Mathf.Sin(a) * b.r);
            }
        }
    }
}
