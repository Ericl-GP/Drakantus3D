using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>[Classes] Utilitários dos efeitos novos (sem alocação por quadro): cor por MPB reaproveitado, sistemas em loop, formas.</summary>
    public static class FxUtil
    {
        static MaterialPropertyBlock mpb;

        /// <summary>Igual ao FX.SetColor, mas reaproveita o MaterialPropertyBlock (para animações por quadro).</summary>
        public static void Tint(Renderer r, Color c)
        {
            if (r == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            mpb.SetColor("_TintColor", c);
            r.SetPropertyBlock(mpb);
        }

        /// <summary>Sistema de partículas contínuo (loop), filho de "parent" (ou solto no FX.Root). Quem chama define forma/cor e dá Play.</summary>
        public static ParticleSystem LoopSys(string name, Transform parent, Vector3 local, Color c, bool additive,
                                             float life0, float life1, float spd0, float spd1, float sz0, float sz1, float gravity, float rate, int max = 80)
        {
            var ps = FX.NewSystem(name, parent != null ? parent.TransformPoint(local) : local, additive);
            if (parent != null)
            {
                ps.transform.SetParent(parent, false);
                ps.transform.localPosition = local;
                ps.transform.localRotation = Quaternion.identity;
            }
            var m = ps.main;
            m.loop = true;
            m.duration = 1f;
            m.stopAction = ParticleSystemStopAction.None;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
            m.startSpeed = new ParticleSystem.MinMaxCurve(spd0, spd1);
            m.startSize = new ParticleSystem.MinMaxCurve(sz0, sz1);
            m.startColor = c;
            m.gravityModifier = gravity;
            m.maxParticles = Mathf.Clamp(max, 4, 400);
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var em = ps.emission; em.rateOverTime = rate;
            return ps;
        }

        public static void Sphere(ParticleSystem ps, float r)
        {
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = Mathf.Max(0.01f, r); sh.rotation = Vector3.zero;
        }

        /// <summary>Círculo no plano XZ (relativo ao transform do sistema).</summary>
        public static void FlatRing(ParticleSystem ps, float r, float thickness = 0f)
        {
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = Mathf.Max(0.01f, r);
            sh.radiusThickness = Mathf.Clamp01(thickness);
            sh.rotation = new Vector3(90f, 0f, 0f);
        }

        /// <summary>Cone apontando para cima.</summary>
        public static void ConeUp(ParticleSystem ps, float angle, float r)
        {
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = Mathf.Max(0.01f, r);
            sh.rotation = new Vector3(-90f, 0f, 0f);
        }

        /// <summary>Órbita em torno do eixo Y do sistema (+ subida opcional).</summary>
        public static void Orbit(ParticleSystem ps, float orbitY, float rise)
        {
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0f); v.y = new ParticleSystem.MinMaxCurve(rise); v.z = new ParticleSystem.MinMaxCurve(0f);
            v.orbitalX = new ParticleSystem.MinMaxCurve(0f); v.orbitalY = new ParticleSystem.MinMaxCurve(orbitY); v.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
        }

        /// <summary>Gradiente de fogo em 3 tons (branco-quente → laranja → vermelho escuro) com fade-in curto.</summary>
        public static void FireGrad(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(SkillFX.FireCore, 0f), new GradientColorKey(SkillFX.Fire, 0.3f), new GradientColorKey(SkillFX.FireDeep, 0.75f), new GradientColorKey(new Color(0.4f, 0.08f, 0.04f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>Gradiente para partículas alpha (fumaça/névoa): entra suave, sai suave.</summary>
        public static void AlphaFade(ParticleSystem ps, Color c0, Color c1, float a)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(a, 0.2f), new GradientAlphaKey(a * 0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>Primitiva sem collider (desligado na hora e destruído), sombras desligadas.</summary>
        public static GameObject Shape(PrimitiveType type, Transform parent, Vector3 local, Vector3 scale)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Object.Destroy(col); }
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            if (r != null) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            return g;
        }

        /// <summary>Malha de cor sólida com brilho (emissão), sem collider.</summary>
        public static GameObject Solid(PrimitiveType type, Transform parent, Vector3 local, Vector3 scale, Color c, float emission, float smooth = 0.4f)
        {
            var g = Shape(type, parent, local, scale);
            g.GetComponent<Renderer>().sharedMaterial = emission > 0f ? U.Lit(c, smooth, c * emission) : U.Lit(c, smooth);
            return g;
        }

        /// <summary>Raio curto e fino (sem luz do pool).</summary>
        public static void MiniBolt(Vector3 a, Vector3 b, Color c, float width, float time)
        {
            var go = new GameObject("mini_raio");
            go.transform.SetParent(FX.Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            const int n = 6;
            lr.positionCount = n;
            Vector3 d = b - a;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / (n - 1);
                Vector3 p = a + d * u;
                if (i > 0 && i < n - 1) p += Random.insideUnitSphere * d.magnitude * 0.18f;
                lr.SetPosition(i, p);
            }
            lr.widthMultiplier = width;
            lr.sharedMaterial = U.Fx(true);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = Color.white; lr.endColor = c;
            go.AddComponent<FxAnim>().SetupLine(lr, time, c);
        }

        /// <summary>Linha (LineRenderer) com textura de faixa macia que some em "time".</summary>
        public static LineRenderer GlowLine(string name, Vector3[] pts, Color c0, Color c1, float width, float time, Texture tex = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(FX.Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            lr.widthMultiplier = width;
            lr.sharedMaterial = U.Fx(true, tex != null ? tex : FxTex.Band());
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = c0; lr.endColor = c1;
            if (time > 0f) go.AddComponent<FxAnim>().SetupLine(lr, time, c1);
            return lr;
        }
    }

    /// <summary>[Classes] Texturas procedurais dos efeitos novos (geradas uma vez).</summary>
    public static class FxTex
    {
        static Texture2D band, chain, star, shadow, swirl, cross;

        /// <summary>Faixa macia: alpha só depende de v (para linhas e rastros).</summary>
        public static Texture2D Band()
        {
            if (band != null) return band;
            const int w = 4, h = 32;
            band = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "fx_band" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float a = Mathf.Clamp01(1f - Mathf.Abs(v * 2f - 1f));
                a = a * a * (3f - 2f * a);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            band.SetPixels32(px); band.Apply();
            return band;
        }

        /// <summary>Elos de corrente vistos de lado (um elo "O" + meio elo "—" de cada lado): use LineTextureMode.RepeatPerSegment.</summary>
        public static Texture2D Chain()
        {
            if (chain != null) return chain;
            const int w = 64, h = 32;
            chain = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "fx_chain" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    // elo oval (contorno)
                    float ex = (fx - 32f) / 19f, ey = (fy - 16f) / 10.5f;
                    float r = Mathf.Sqrt(ex * ex + ey * ey);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.2f);
                    // elos de lado (barras) nas pontas
                    float bar = 0f;
                    if ((fx < 16f || fx > 48f) && Mathf.Abs(fy - 16f) < 3.4f) bar = 1f - Mathf.Clamp01((Mathf.Abs(fy - 16f) - 2.4f));
                    float a = Mathf.Max(ring, bar);
                    // brilho metálico no topo do elo
                    float shine = Mathf.Clamp01(1f - Mathf.Abs(fy - 22f) / 3f) * ring;
                    byte lum = (byte)Mathf.RoundToInt(Mathf.Lerp(200f, 255f, shine));
                    px[y * w + x] = new Color32(lum, lum, lum, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            chain.SetPixels32(px); chain.Apply(true);
            return chain;
        }

        /// <summary>Estrela de 4 pontas com brilho (cintilância, estrelas de atordoamento, clarões).</summary>
        public static Texture2D Star()
        {
            if (star != null) return star;
            const int n = 64;
            star = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "fx_star" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float rays = Mathf.Clamp01(1f - Mathf.Abs(dx) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(dy)) + Mathf.Clamp01(1f - Mathf.Abs(dy) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(dx));
                    float core = Mathf.Clamp01(1f - d * 2.2f);
                    float a = Mathf.Clamp01(rays * 0.9f + core * core * 1.2f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            star.SetPixels32(px); star.Apply();
            return star;
        }

        /// <summary>Faixa de sombra viva: núcleo escuro, bordas em fiapos (u repete ao longo do caminho).</summary>
        public static Texture2D Shadow()
        {
            if (shadow != null) return shadow;
            const int w = 64, h = 32;
            shadow = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "fx_shadow" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (y + 0.5f) / h;
                    float edge = 0.62f + 0.3f * Mathf.PerlinNoise(u * 6f + 3.1f, v < 0.5f ? 1.3f : 7.7f) - 0.15f * Mathf.Sin(u * Mathf.PI * 2f * 3f);
                    float dv = Mathf.Abs(v * 2f - 1f);
                    float a = Mathf.Clamp01((edge - dv) / 0.18f);
                    a *= 0.78f + 0.22f * Mathf.PerlinNoise(u * 12f, v * 4f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            shadow.SetPixels32(px); shadow.Apply(true);
            return shadow;
        }

        /// <summary>Espiral de 3 braços (disco de acreção da Singularidade).</summary>
        public static Texture2D Swirl()
        {
            if (swirl != null) return swirl;
            const int n = 128;
            swirl = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "fx_swirl" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float th = Mathf.Atan2(dy, dx);
                    float arm = Mathf.Pow(Mathf.Abs(Mathf.Sin((th * 3f + r * 9f) * 0.5f)), 6f);
                    float fall = Mathf.Clamp01(1f - r) * Mathf.Clamp01((r - 0.08f) / 0.15f);
                    float a = Mathf.Clamp01(arm * fall * 1.4f + fall * 0.15f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            swirl.SetPixels32(px); swirl.Apply(true);
            return swirl;
        }

        /// <summary>Cruz luminosa (cura sagrada).</summary>
        public static Texture2D Cross()
        {
            if (cross != null) return cross;
            const int n = 64;
            cross = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "fx_cross" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / n * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                    float bar = Mathf.Max(Mathf.Clamp01((0.2f - dx) / 0.08f) * Mathf.Clamp01((0.85f - dy) / 0.1f),
                                          Mathf.Clamp01((0.2f - dy) / 0.08f) * Mathf.Clamp01((0.85f - dx) / 0.1f));
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)) * 0.35f;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(bar + glow) * 255f));
                }
            cross.SetPixels32(px); cross.Apply();
            return cross;
        }

        static Mesh pyramid;
        /// <summary>Pirâmide de 4 lados (ponta em +Z, base em -Z), faces planas: ponta de flecha low-poly.</summary>
        public static Mesh Pyramid()
        {
            if (pyramid != null) return pyramid;
            Vector3 apex = new Vector3(0f, 0f, 0.5f);
            Vector3[] b = { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f) };
            var v = new List<Vector3>(); var t = new List<int>();
            Vector3 center = new Vector3(0f, 0f, -0.1f);
            for (int i = 0; i < 4; i++) AddTri(v, t, apex, b[i], b[(i + 1) % 4], center);
            AddTri(v, t, b[0], b[1], b[2], center);
            AddTri(v, t, b[0], b[2], b[3], center);
            pyramid = new Mesh { name = "fx_pyramid" };
            pyramid.SetVertices(v); pyramid.SetTriangles(t, 0);
            pyramid.RecalculateNormals(); pyramid.RecalculateBounds();
            return pyramid;
        }

        /// <summary>Adiciona um triângulo com a face virada para fora (em relação a "center").</summary>
        static void AddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 center)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            Vector3 mid = (a + b + c) / 3f;
            if (Vector3.Dot(n, mid - center) < 0f) { var tmp = b; b = c; c = tmp; }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
    }

    /// <summary>Faz o quad olhar para a câmera (com giro opcional no plano da tela).</summary>
    public class FxBillboard : MonoBehaviour
    {
        public float roll;
        float ang;
        void LateUpdate()
        {
            var cam = CameraRig.Cam != null ? CameraRig.Cam : Camera.main;
            if (cam == null) return;
            ang += roll * Time.deltaTime;
            transform.rotation = roll != 0f ? cam.transform.rotation * Quaternion.Euler(0f, 0f, ang) : cam.transform.rotation;
        }
    }

    /// <summary>Executa uma ação depois de um atraso (efeitos em sequência).</summary>
    public class FxDelay : MonoBehaviour
    {
        System.Action act;
        float t;

        public static void Run(float delay, System.Action a)
        {
            if (a == null) return;
            if (delay <= 0f) { a(); return; }
            var go = new GameObject("fx_atraso");
            go.transform.SetParent(FX.Root, false);
            var d = go.AddComponent<FxDelay>();
            d.act = a; d.t = delay;
        }

        void Update()
        {
            t -= Time.deltaTime;
            if (t > 0f) return;
            var a = act; act = null;
            Destroy(gameObject);
            if (a == null) return;
            try { a(); } catch (System.Exception e) { Debug.LogWarning("[FX] atraso: " + e.Message); }
        }
    }

    /// <summary>
    /// Efeito de área persistente (Fumaça, Santuário, Singularidade, Estandarte): vive até a fase "end" (ou um tempo de segurança),
    /// então para de emitir, apaga os renderers, encolhe os objetos sólidos e se destrói.
    /// </summary>
    public class ZoneFx : MonoBehaviour
    {
        public string id;
        public float safety = 30f;
        float t, endAt = -1f, fadeTime = 0.5f;
        readonly List<ParticleSystem> systems = new();
        readonly List<Renderer> rends = new();
        readonly List<Color> cols = new();
        readonly List<float> pulse = new();
        readonly List<Light> lights = new();
        readonly List<float> lightI = new();
        readonly List<Transform> shrink = new();
        readonly List<Vector3> shrinkBase = new();
        readonly List<Transform> spins = new();
        readonly List<float> spinSpeed = new();
        static readonly List<ZoneFx> active = new();

        public bool Ending => endAt >= 0f;

        public static ZoneFx Create(string id, Vector3 pos, float safety = 30f)
        {
            var go = new GameObject("zona_" + id);
            go.transform.SetParent(FX.Root, false);
            go.transform.position = pos;
            var z = go.AddComponent<ZoneFx>();
            z.id = id; z.safety = safety;
            active.Add(z);
            return z;
        }

        /// <summary>Zona ativa mais próxima com esse id (até maxDist).</summary>
        public static ZoneFx Find(string id, Vector3 pos, float maxDist = 4f)
        {
            ZoneFx best = null; float bd = maxDist;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var z = active[i];
                if (z == null) { active.RemoveAt(i); continue; }
                if (z.Ending || z.id != id) continue;
                float d = U.Flat(z.transform.position - pos).magnitude;
                if (d <= bd) { bd = d; best = z; }
            }
            return best;
        }

        public ParticleSystem Add(ParticleSystem ps) { if (ps != null) systems.Add(ps); return ps; }
        public void AddRenderer(Renderer r, Color c, float pulseAmt = 0f) { if (r == null) return; rends.Add(r); cols.Add(c); pulse.Add(pulseAmt); FxUtil.Tint(r, c); }
        public void AddLight(Light l) { if (l == null) return; lights.Add(l); lightI.Add(l.intensity); }
        public void AddShrink(Transform tr) { if (tr == null) return; shrink.Add(tr); shrinkBase.Add(tr.localScale); }
        public void AddSpin(Transform tr, float deg) { if (tr == null) return; spins.Add(tr); spinSpeed.Add(deg); }

        public Light NewLight(Vector3 local, Color c, float intensity, float range, float flicker)
        {
            var lg = new GameObject("luz_zona");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = local;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            if (flicker > 0f) { var f = lg.AddComponent<FxFlicker>(); f.baseIntensity = intensity; f.amount = flicker; }
            AddLight(l);
            return l;
        }

        public void End(float fade = 0.5f)
        {
            if (Ending) return;
            endAt = t;
            fadeTime = Mathf.Max(0.05f, fade);
            foreach (var ps in systems) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var l in lights) if (l != null) { var f = l.GetComponent<FxFlicker>(); if (f != null) f.enabled = false; }
            Destroy(gameObject, fadeTime + 2f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (!Ending && t >= safety) End();
            float fin = Mathf.Clamp01(t / 0.25f);
            float k = Ending ? Mathf.Clamp01(1f - (t - endAt) / fadeTime) : 1f;
            for (int i = 0; i < spins.Count; i++) if (spins[i] != null) spins[i].Rotate(Vector3.up, spinSpeed[i] * dt, Space.World);
            for (int i = 0; i < rends.Count; i++)
            {
                if (rends[i] == null) continue;
                var c = cols[i];
                float p = pulse[i] > 0f ? 1f - pulse[i] * 0.5f + pulse[i] * 0.5f * Mathf.Sin(t * 4f + i) : 1f;
                c.a *= fin * k * p;
                FxUtil.Tint(rends[i], c);
            }
            if (Ending)
            {
                for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].intensity = lightI[i] * k;
                for (int i = 0; i < shrink.Count; i++) if (shrink[i] != null) shrink[i].localScale = shrinkBase[i] * k;
            }
        }

        void OnDestroy() { active.Remove(this); }
    }

    /// <summary>
    /// Rastro do projétil: segue o projétil (posição/rotação/escala) e carrega TODO o visual dele
    /// (malhas, halos, rastros, partículas, luz). Quando o projétil some, esconde as malhas,
    /// deixa os rastros e partículas terminarem e se destrói.
    /// </summary>
    public class ProjTail : MonoBehaviour
    {
        public Transform target;
        public Transform spin;
        public Vector3 spinAxis = Vector3.forward;
        public float spinSpeed;
        public readonly List<Renderer> solids = new();
        public readonly List<ParticleSystem> systems = new();
        public readonly List<TrailRenderer> trails = new();
        readonly List<float> trailW = new();
        public readonly List<Light> lights = new();
        readonly List<float> lightI = new();
        public LineRenderer tether;
        public float tetherLen = 3.5f, tetherLink = 0.24f;
        bool released;
        float t, lastScale = 1f;
        Vector3 lastPos;

        public void AddTrail(TrailRenderer tr) { if (tr == null) return; trails.Add(tr); trailW.Add(tr.widthMultiplier); }
        public void AddLight(Light l) { if (l == null) return; lights.Add(l); lightI.Add(l.intensity); }

        public void Snap()
        {
            if (target == null) return;
            transform.SetPositionAndRotation(target.position, target.rotation);
            lastPos = target.position;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (!released)
            {
                if (target == null) { Release(); return; }
                transform.SetPositionAndRotation(target.position, target.rotation);
                lastPos = target.position;
                float s = target.lossyScale.x;
                if (Mathf.Abs(s - lastScale) > 0.001f)
                {
                    lastScale = s;
                    transform.localScale = Vector3.one * s;
                    for (int i = 0; i < trails.Count; i++) if (trails[i] != null) trails[i].widthMultiplier = trailW[i] * s;
                }
                if (spin != null && spinSpeed != 0f) spin.Rotate(spinAxis, spinSpeed * dt, Space.Self);
                if (tether != null) UpdateTether();
            }
            else
            {
                t += dt;
                float k = Mathf.Clamp01(1f - t / 0.15f);
                for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].intensity = lightI[i] * k;
                if (tether != null)
                {
                    var c = tether.startColor; c.a = Mathf.Clamp01(1f - t / 0.3f); tether.startColor = c;
                    var e = tether.endColor; e.a = 0f; tether.endColor = e;
                }
            }
        }

        void UpdateTether()
        {
            Vector3 fwd = transform.forward;
            Vector3 head = transform.position - fwd * 0.3f;
            int n = Mathf.Clamp(Mathf.CeilToInt(tetherLen / tetherLink), 1, 40);
            tether.positionCount = n + 1;
            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n;
                Vector3 p = head - fwd * tetherLen * u;
                p += Vector3.up * Mathf.Sin(u * Mathf.PI) * -0.12f;   // a corrente "pesa"
                tether.SetPosition(i, p);
            }
        }

        void Release()
        {
            if (released) return;
            released = true;
            transform.position = lastPos;
            foreach (var r in solids) if (r != null) r.enabled = false;
            foreach (var ps in systems) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var tr in trails) if (tr != null) tr.emitting = false;
            foreach (var l in lights) { if (l == null) continue; var f = l.GetComponent<FxFlicker>(); if (f != null) f.enabled = false; }
            Destroy(gameObject, 1.3f);
        }
    }
}
