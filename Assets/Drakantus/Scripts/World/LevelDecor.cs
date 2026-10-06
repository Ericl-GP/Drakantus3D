using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Ferramentas do LevelBuilder: instanciar modelos KayKit já normalizados pelo tamanho real
    /// (medido em tempo de execução pelos meshes), colisores aproximados, luzes de tocha,
    /// chamas e vaga-lumes. Tudo funciona também com as formas simples (Models.Fallback).
    /// </summary>
    public static class LevelDecor
    {
        // ------------------------------------------------------------------ grupos
        public static Transform Group(Transform root, string name)
        {
            var t = root.Find(name);
            if (t != null) return t;
            var g = new GameObject(name);
            g.transform.SetParent(root, false);
            return g.transform;
        }

        // ------------------------------------------------------------------ medidas
        static readonly Vector3[] corners = new Vector3[8];

        static void Corners(Bounds b)
        {
            Vector3 mn = b.min, mx = b.max;
            corners[0] = new Vector3(mn.x, mn.y, mn.z); corners[1] = new Vector3(mx.x, mn.y, mn.z);
            corners[2] = new Vector3(mn.x, mx.y, mn.z); corners[3] = new Vector3(mx.x, mx.y, mn.z);
            corners[4] = new Vector3(mn.x, mn.y, mx.z); corners[5] = new Vector3(mx.x, mn.y, mx.z);
            corners[6] = new Vector3(mn.x, mx.y, mx.z); corners[7] = new Vector3(mx.x, mx.y, mx.z);
        }

        /// <summary>Caixa dos meshes no espaço LOCAL de g (não depende do frame atual: usa as matrizes).</summary>
        public static bool LocalBounds(GameObject g, out Bounds b) => MeshBounds(g, g.transform.worldToLocalMatrix, out b);

        /// <summary>Caixa dos meshes no espaço do MUNDO (equivale a Renderer.bounds, mas calculada agora).</summary>
        public static bool WorldBounds(GameObject g, out Bounds b) => MeshBounds(g, Matrix4x4.identity, out b);

        static bool MeshBounds(GameObject g, Matrix4x4 toSpace, out Bounds b)
        {
            b = new Bounds();
            bool any = false;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Accumulate(mf.sharedMesh.bounds, toSpace * mf.transform.localToWorldMatrix, ref b, ref any);
            }
            foreach (var sm in g.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (sm.sharedMesh == null) continue;
                Accumulate(sm.sharedMesh.bounds, toSpace * sm.transform.localToWorldMatrix, ref b, ref any);
            }
            return any;
        }

        static void Accumulate(Bounds mb, Matrix4x4 m, ref Bounds b, ref bool any)
        {
            Corners(mb);
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(corners[i]);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }

        // ------------------------------------------------------------------ instanciar
        /// <summary>
        /// Instancia (Models.SpawnOr) e ajusta a escala uniforme para que a altura medida
        /// seja targetHeight; a base do modelo fica em pos.y. tiltX deita o objeto (ex.: 90 para armas no chão).
        /// Se centerXZ, o centro da caixa vai para pos (para modelos com pivô deslocado).
        /// </summary>
        public static GameObject SpawnSized(string id, Transform parent, Vector3 pos, float yaw, float targetHeight, float tiltX = 0f, bool centerXZ = false)
        {
            var g = Models.SpawnOr(id, parent, pos, yaw, 1f);
            if (tiltX != 0f) g.transform.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(tiltX, 0, 0) * StripYaw(g.transform.localRotation, yaw);
            Bounds b;
            if (!WorldBounds(g, out b) || b.size.y < 0.0001f) return g;
            float s = targetHeight / b.size.y;
            Vector3 pivot = g.transform.position;
            g.transform.localScale = g.transform.localScale * s;
            // a caixa escala em torno do pivô: calcula a nova base/centro sem depender de bounds atualizados
            float newMinY = pivot.y + (b.min.y - pivot.y) * s;
            Vector3 want = parent != null ? parent.TransformPoint(pos) : pos;
            Vector3 delta = new Vector3(0, want.y - newMinY, 0);
            if (centerXZ)
            {
                float cx = pivot.x + (b.center.x - pivot.x) * s;
                float cz = pivot.z + (b.center.z - pivot.z) * s;
                delta.x = want.x - cx; delta.z = want.z - cz;
            }
            g.transform.position += delta;
            return g;
        }

        /// <summary>Objeto deitado no chão (armas/ossos): mede em pé (length = comprimento) e depois deita.</summary>
        public static GameObject SpawnLying(string id, Transform parent, Vector3 pos, float yaw, float length)
        {
            var g = SpawnSized(id, parent, pos, yaw, length);
            g.transform.rotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90f, 0, 0) * Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * g.transform.rotation;
            Bounds b;
            if (WorldBounds(g, out b))
            {
                Vector3 want = parent != null ? parent.TransformPoint(pos) : pos;
                g.transform.position += new Vector3(0, want.y + 0.01f - b.min.y, 0);
            }
            return g;
        }

        // remove o yaw já aplicado por Models.Spawn, preservando a rotação extra da biblioteca
        static Quaternion StripYaw(Quaternion q, float yaw) => Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * q;

        /// <summary>
        /// Instancia e estica (escala não uniforme) para ocupar exatamente a caixa center/size
        /// nos eixos locais girados por yaw. Ideal para blocos (BlockBits), muros e lajes.
        /// fallbackColor pinta a forma simples quando o modelo não existe.
        /// </summary>
        public static GameObject SpawnBox(string id, Transform parent, Vector3 center, Vector3 size, float yaw = 0f, Color? fallbackColor = null, Color? fallbackEmission = null)
        {
            bool has = Models.Has(id);
            var g = Models.SpawnOr(id, parent, center, yaw, 1f);
            if (!has && fallbackColor.HasValue)
            {
                var mat = U.Lit(fallbackColor.Value, 0.15f, fallbackEmission);
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            }
            Bounds lb;
            if (!LocalBounds(g, out lb)) return g;
            // LocalBounds ignora a escala do próprio g, então a nova escala é direta
            g.transform.localScale = new Vector3(
                lb.size.x > 0.0001f ? size.x / lb.size.x : 1f,
                lb.size.y > 0.0001f ? size.y / lb.size.y : 1f,
                lb.size.z > 0.0001f ? size.z / lb.size.z : 1f);
            // alinha o centro da caixa medida ao centro pedido
            Vector3 cWorld = g.transform.TransformPoint(lb.center);
            Vector3 want = parent != null ? parent.TransformPoint(center) : center;
            g.transform.position += want - cWorld;
            return g;
        }

        // ------------------------------------------------------------------ colisores
        /// <summary>BoxCollider aproximado pelos meshes (no espaço local do objeto). shrinkXZ &lt; 1 deixa passar rente.</summary>
        public static BoxCollider AddCollider(GameObject g, float shrinkXZ = 0.9f, float minHeight = 2.5f)
        {
            if (g == null) return null;
            Bounds lb;
            if (!LocalBounds(g, out lb)) return null;
            var bc = g.AddComponent<BoxCollider>();
            Vector3 size = new Vector3(lb.size.x * shrinkXZ, lb.size.y, lb.size.z * shrinkXZ);
            Vector3 center = lb.center;
            float sy = Mathf.Abs(g.transform.lossyScale.y);
            if (sy > 0.0001f && size.y * sy < minHeight)
            {
                float want = minHeight / sy;
                center.y += (want - size.y) * 0.5f;
                size.y = want;
            }
            bc.center = center;
            bc.size = size;
            return bc;
        }

        /// <summary>Colisor invisível (muros de borda, lava, bosque fechado).</summary>
        public static GameObject ColliderBox(Transform parent, Vector3 center, Vector3 size, float yaw = 0f, string name = "Colisor")
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var bc = g.AddComponent<BoxCollider>();
            bc.size = size;
            return g;
        }

        /// <summary>Laje colorida simples (caminhos, tapetes, chão). Sem colisor salvo pedido.</summary>
        public static GameObject Slab(Transform parent, Vector3 center, Vector3 size, Color c, float yaw = 0f, bool collider = false, Color? emission = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!collider) Object.Destroy(g.GetComponent<Collider>());
            g.name = "Laje";
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = U.Lit(c, 0.1f, emission);
            return g;
        }

        /// <summary>Disco achatado (clareiras, tapetes redondos).</summary>
        public static GameObject Disc(Transform parent, Vector3 center, float radius, Color c, float thickness = 0.02f)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "Disco";
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localScale = new Vector3(radius * 2f, thickness * 0.5f, radius * 2f);
            g.GetComponent<Renderer>().sharedMaterial = U.Lit(c, 0.05f);
            return g;
        }

        /// <summary>Chão principal: laje com BoxCollider cujo topo fica em y = 0.</summary>
        public static GameObject Ground(Transform parent, Vector3 center, Vector2 size, Color c)
        {
            var g = Slab(parent, new Vector3(center.x, -0.5f, center.z), new Vector3(size.x, 1f, size.y), c, 0f, true);
            g.name = "Chao";
            return g;
        }

        // ------------------------------------------------------------------ oclusão
        /// <summary>Marca um objeto grande (árvore, casa, muro, rocha) para o OcclusionFader deixá-lo transparente.</summary>
        public static GameObject MarkOccluder(GameObject g)
        {
            if (g != null && g.GetComponent<Occluder>() == null) g.AddComponent<Occluder>();
            return g;
        }

        // ------------------------------------------------------------------ luzes
        /// <summary>
        /// Quantas luzes com sombra ainda podem ser criadas neste mapa (o LevelBuilder zera a cada Build).
        /// Luz pontual com sombra custa 6 mapas no atlas do URP (cubemap) e gerava o aviso
        /// "Reduced additional punctual light shadows resolution"; por isso a luz com sombra vira um
        /// SPOT apontado para baixo (1 mapa só) e as demais ficam sem sombra.
        /// </summary>
        public static int shadowBudget = 2;
        /// <summary>Intensidade extra do tremor das tochas (masmorra = mais vivo).</summary>
        public static float flickerAmount = 0.18f;

        public static Light PointLight(Transform parent, Vector3 pos, Color c, float range, float intensity, bool shadows = false, bool flicker = true)
        {
            var g = new GameObject("Luz");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            if (shadows && shadowBudget > 0)
            {
                shadowBudget--;
                // spot bem aberto virado para baixo: ilumina o chão/paredes em volta e projeta sombra com 1 mapa
                l.type = LightType.Spot;
                l.spotAngle = 160f;
                l.innerSpotAngle = 110f;
                l.range = range * 1.1f;
                l.intensity = intensity * 1.15f;
                g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.8f;
                l.shadowBias = 0.05f;
                l.shadowNormalBias = 0.4f;
                l.shadowNearPlane = 0.2f;
                // luz fraca sem sombra para clarear acima da chama (o spot só ilumina para baixo)
                var fill = new GameObject("Luz_Preenchimento");
                fill.transform.SetParent(g.transform, false);
                var lf = fill.AddComponent<Light>();
                lf.type = LightType.Point;
                lf.color = c;
                lf.range = range * 0.7f;
                lf.intensity = intensity * 0.35f;
                lf.shadows = LightShadows.None;
            }
            if (flicker)
            {
                var f = g.AddComponent<TorchFlicker>();
                f.baseIntensity = l.intensity;
                f.baseRange = l.range;
                f.amount = flickerAmount;
                f.seed = pos.x * 0.37f + pos.z * 0.71f;
            }
            return l;
        }

        /// <summary>Muda a direção do sol do mapa (RenderSettings.sun, criado pelo Game). pitch baixo = fim de tarde.</summary>
        public static void SetSunAngles(float pitch, float yaw)
        {
            var s = RenderSettings.sun;
            if (s != null) s.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>Chama em partículas (loop) para tochas, braseiros e lava.</summary>
        public static ParticleSystem Flame(Transform parent, Vector3 pos, float size, Color c, int max = 14)
        {
            var go = new GameObject("Chama");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true;
            m.playOnAwake = true;
            m.duration = 1f;
            m.maxParticles = max;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f * size, 0.9f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(0.18f * size, 0.42f * size);
            m.startColor = c;
            m.gravityModifier = -0.15f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = max * 1.6f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.08f * size;
            sh.rotation = new Vector3(-90, 0, 0);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.1f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0), new GradientColorKey(c, 0.4f), new GradientColorKey(new Color(0.8f, 0.2f, 0.05f), 1) },
                       new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = U.Fx(true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        /// <summary>Vaga-lumes / poeira mágica / brasas flutuando numa caixa (loop leve).</summary>
        public static ParticleSystem Motes(Transform parent, Vector3 center, Vector3 size, Color c, int max = 30, float rise = 0.05f)
        {
            var go = new GameObject("Particulas");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true;
            m.playOnAwake = true;
            m.prewarm = true;
            m.duration = 6f;
            m.maxParticles = max;
            m.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            m.startColor = c;
            m.gravityModifier = -rise;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = max / 5f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size;
            var no = ps.noise; no.enabled = true; no.strength = 0.4f; no.frequency = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                       new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = U.Fx(true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ pisos com textura repetida
        static Texture2D stoneTex, gravelTex;
        static readonly Dictionary<string, Material> tiledCache = new Dictionary<string, Material>();

        /// <summary>
        /// Laje com textura repetida (piso da praça, caminhos). Um único objeto em vez de dezenas de blocos;
        /// tileMeters = tamanho no mundo de UMA repetição da textura. A textura dos BlockBits é um atlas
        /// (não pode ser repetida), então os padrões são gerados por código.
        /// </summary>
        public static GameObject TiledSlab(Transform parent, Vector3 center, Vector3 size, Texture2D tex, float tileMeters, Color tint, float yaw = 0f)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "Piso";
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            g.transform.localScale = size;
            Vector2 rep = new Vector2(size.x / Mathf.Max(0.1f, tileMeters), size.z / Mathf.Max(0.1f, tileMeters));
            string key = (tex != null ? tex.name : "null") + "|" + ColorUtility.ToHtmlStringRGB(tint) + "|" + rep.x.ToString("0.00") + "x" + rep.y.ToString("0.00");
            if (!tiledCache.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(U.Lit(Color.white, 0.12f));
                m.color = tint;
                m.mainTexture = tex;
                m.mainTextureScale = rep;
                tiledCache[key] = m;
            }
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        /// <summary>Lajotas de pedra 4x4 por repetição, em fileiras desencontradas, com rejunte e variação de tom.</summary>
        public static Texture2D StoneTileTexture()
        {
            if (stoneTex != null) return stoneTex;
            const int N = 256, CELLS = 4;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true);
            t.name = "Proc_Lajotas";
            var rnd = new System.Random(77);
            float[] tone = new float[CELLS * CELLS];
            for (int i = 0; i < tone.Length; i++) tone[i] = 0.82f + (float)rnd.NextDouble() * 0.22f;
            var px = new Color[N * N];
            float cell = N / (float)CELLS;
            for (int y = 0; y < N; y++)
            {
                int row = (int)(y / cell);
                float off = (row % 2 == 1) ? cell * 0.5f : 0f;
                float ly = y - row * cell;
                for (int x = 0; x < N; x++)
                {
                    float xx = x + off;
                    int col = (int)(xx / cell);
                    float lx = xx - col * cell;
                    float edge = Mathf.Min(Mathf.Min(lx, cell - lx), Mathf.Min(ly, cell - ly));
                    int ci = (row * CELLS + (col % CELLS)) % tone.Length;   // col % CELLS: a pedra cortada na borda continua igual do outro lado
                    float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.6f + Mathf.PerlinNoise(x * 0.23f + 40f, y * 0.23f) * 0.4f;
                    float v = tone[ci] * (0.86f + n * 0.2f);
                    if (edge < 2.2f) v = 0.42f + n * 0.08f;                    // rejunte
                    else if (edge < 5f) v *= 0.88f + (edge - 2.2f) / 2.8f * 0.12f;  // borda gasta
                    px[y * N + x] = new Color(v, v * 0.985f, v * 0.96f, 1f);
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true, true);
            stoneTex = t;
            return t;
        }

        /// <summary>Cascalho/terra batida (ruído com pedrinhas claras).</summary>
        public static Texture2D GravelTexture()
        {
            if (gravelTex != null) return gravelTex;
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true);
            t.name = "Proc_Cascalho";
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // ruído "repetível": usa o período N nas duas direções via mistura nas bordas
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.5f + Mathf.PerlinNoise(x * 0.31f + 13f, y * 0.31f + 7f) * 0.5f;
                    float pebble = Mathf.PerlinNoise(x * 0.55f + 91f, y * 0.55f + 3f);
                    float v = 0.78f + n * 0.25f;
                    if (pebble > 0.68f) v += (pebble - 0.68f) * 1.6f;
                    else if (pebble < 0.25f) v -= (0.25f - pebble) * 0.6f;
                    px[y * N + x] = new Color(v, v * 0.96f, v * 0.9f, 1f);
                }
            // suaviza a costura: mistura 16 px das bordas com o lado oposto
            const int B = 16;
            for (int y = 0; y < N; y++)
                for (int k = 0; k < B; k++)
                {
                    float w = 0.5f * (1f - k / (float)B);
                    int a = y * N + k, b = y * N + (N - 1 - k);
                    Color ca = px[a], cb = px[b];
                    px[a] = Color.Lerp(ca, cb, w); px[b] = Color.Lerp(cb, ca, w);
                }
            for (int x = 0; x < N; x++)
                for (int k = 0; k < B; k++)
                {
                    float w = 0.5f * (1f - k / (float)B);
                    int a = k * N + x, b = (N - 1 - k) * N + x;
                    Color ca = px[a], cb = px[b];
                    px[a] = Color.Lerp(ca, cb, w); px[b] = Color.Lerp(cb, ca, w);
                }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true, true);
            gravelTex = t;
            return t;
        }

        // ------------------------------------------------------------------ [Praca] caixas com textura em metros
        static Texture2D brickTex, plankTex;
        static readonly Dictionary<string, Mesh> boxMeshCache = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> boxMatCache = new Dictionary<string, Material>();

        /// <summary>Material URP/Lit com textura repetida (escala 1: a malha já traz UV em "repetições").</summary>
        public static Material TexturedMat(Texture2D tex, Color tint, float smooth = 0.12f)
        {
            string key = (tex != null ? tex.name : "null") + "|" + ColorUtility.ToHtmlStringRGBA(tint) + "|" + smooth.ToString("0.00");
            if (!boxMatCache.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(U.Lit(Color.white, smooth));
                m.color = tint;
                m.mainTexture = tex;
                m.mainTextureScale = Vector2.one;
                boxMatCache[key] = m;
            }
            return m;
        }

        /// <summary>
        /// Malha de caixa no tamanho real (centrada no pivô) com UV em metros/tileMeters em cada face:
        /// a textura fica com a mesma densidade em caixas de qualquer tamanho (sem esticar). Cache por tamanho.
        /// </summary>
        public static Mesh BoxMesh(Vector3 size, float tileMeters)
        {
            float t = Mathf.Max(0.05f, tileMeters);
            string key = size.x.ToString("0.000") + "x" + size.y.ToString("0.000") + "x" + size.z.ToString("0.000") + "@" + t.ToString("0.000");
            if (boxMeshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var verts = new List<Vector3>(24);
            var norms = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);
            Vector3 h = size * 0.5f;
            // (normal, eixo "u" da face). O eixo "v" sai de Cross(u, n): vertical nas faces laterais.
            BoxFace(Vector3.up, Vector3.right, h, t, verts, norms, uvs, tris);
            BoxFace(Vector3.down, Vector3.right, h, t, verts, norms, uvs, tris);
            BoxFace(Vector3.right, Vector3.forward, h, t, verts, norms, uvs, tris);
            BoxFace(Vector3.left, Vector3.back, h, t, verts, norms, uvs, tris);
            BoxFace(Vector3.forward, Vector3.left, h, t, verts, norms, uvs, tris);
            BoxFace(Vector3.back, Vector3.right, h, t, verts, norms, uvs, tris);
            var mesh = new Mesh { name = "Caixa_" + key };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            boxMeshCache[key] = mesh;
            return mesh;
        }

        static float AxisExtent(Vector3 axis, Vector3 h) => Mathf.Abs(axis.x) * h.x + Mathf.Abs(axis.y) * h.y + Mathf.Abs(axis.z) * h.z;

        static void BoxFace(Vector3 n, Vector3 u, Vector3 h, float tile, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
        {
            Vector3 v = Vector3.Cross(u, n);   // Cross(u, v) = -n → triângulos (0,1,2)(0,2,3) ficam no sentido horário vistos de fora
            float hu = AxisExtent(u, h), hv = AxisExtent(v, h), hn = AxisExtent(n, h);
            Vector3 c = n * hn, U_ = u * hu, V_ = v * hv;
            int b = verts.Count;
            verts.Add(c - U_ - V_); verts.Add(c - U_ + V_); verts.Add(c + U_ + V_); verts.Add(c + U_ - V_);
            float su = 2f * hu / tile, sv = 2f * hv / tile;
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, sv)); uvs.Add(new Vector2(su, sv)); uvs.Add(new Vector2(su, 0f));
            for (int i = 0; i < 4; i++) norms.Add(n);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
        }

        /// <summary>Caixa texturizada (malha própria com UV em metros). collider = BoxCollider do mesmo tamanho.</summary>
        public static GameObject TexturedBoxRot(Transform parent, Vector3 center, Vector3 size, Quaternion rot, Texture2D tex, float tileMeters, Color tint, bool collider = false, string name = "Bloco")
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localRotation = rot;
            g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, tileMeters);
            g.AddComponent<MeshRenderer>().sharedMaterial = TexturedMat(tex, tint);
            if (collider)
            {
                var bc = g.AddComponent<BoxCollider>();
                bc.center = Vector3.zero;
                bc.size = size;
            }
            return g;
        }

        /// <summary>Caixa texturizada girada só em yaw.</summary>
        public static GameObject TexturedBox(Transform parent, Vector3 center, Vector3 size, float yaw, Texture2D tex, float tileMeters, Color tint, bool collider = false, string name = "Bloco")
            => TexturedBoxRot(parent, center, size, Quaternion.Euler(0f, yaw, 0f), tex, tileMeters, tint, collider, name);

        /// <summary>Cantaria (blocos de pedra em fiadas desencontradas). Uma repetição ≈ 2 m: 4 fiadas x 3 blocos.</summary>
        public static Texture2D BrickTexture()
        {
            if (brickTex != null) return brickTex;
            const int N = 256, ROWS = 4, COLS = 3;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true);
            t.name = "Proc_Cantaria";
            var rnd = new System.Random(91);
            float[] tone = new float[ROWS * COLS];
            for (int i = 0; i < tone.Length; i++) tone[i] = 0.80f + (float)rnd.NextDouble() * 0.26f;
            var px = new Color[N * N];
            float rh = N / (float)ROWS, cw = N / (float)COLS;
            for (int y = 0; y < N; y++)
            {
                int row = (int)(y / rh);
                float ly = y - row * rh;
                float off = (row % 2 == 1) ? cw * 0.5f : 0f;
                for (int x = 0; x < N; x++)
                {
                    float xx = (x + off) % N;
                    int col = (int)(xx / cw);
                    float lx = xx - col * cw;
                    float edge = Mathf.Min(Mathf.Min(lx, cw - lx), Mathf.Min(ly, rh - ly));
                    float n = Mathf.PerlinNoise(x * 0.07f, y * 0.07f) * 0.55f + Mathf.PerlinNoise(x * 0.27f + 31f, y * 0.27f + 5f) * 0.45f;
                    float v = tone[(row * COLS + col % COLS) % tone.Length] * (0.84f + n * 0.24f);
                    if (edge < 2.5f) v = 0.40f + n * 0.08f;                         // argamassa
                    else if (edge < 6f) v *= 0.86f + (edge - 2.5f) / 3.5f * 0.14f;  // aresta gasta
                    px[y * N + x] = new Color(v, v * 0.975f, v * 0.94f, 1f);
                }
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true, true);
            brickTex = t;
            return t;
        }

        /// <summary>Tábuas de madeira (4 por repetição, ao longo de V) com veios, frestas e emendas.</summary>
        public static Texture2D PlankTexture()
        {
            if (plankTex != null) return plankTex;
            const int N = 256, PLANKS = 4;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true);
            t.name = "Proc_Tabuas";
            var rnd = new System.Random(57);
            float[] tone = new float[PLANKS];
            int[] seam = new int[PLANKS];
            for (int i = 0; i < PLANKS; i++) { tone[i] = 0.80f + (float)rnd.NextDouble() * 0.25f; seam[i] = rnd.Next(0, N); }
            var px = new Color[N * N];
            float pw = N / (float)PLANKS;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int p = (int)(x / pw);
                    float lx = x - p * pw;
                    float grain = Mathf.PerlinNoise(x * 0.11f + p * 17f, y * 0.012f) * 0.6f + Mathf.PerlinNoise(x * 0.5f, y * 0.05f + p * 9f) * 0.4f;
                    float v = tone[p] * (0.78f + grain * 0.3f);
                    float edge = Mathf.Min(lx, pw - lx);
                    int dy = Mathf.Abs(y - seam[p]); dy = Mathf.Min(dy, N - dy);
                    if (edge < 1.6f || dy < 1) v = 0.28f;
                    else if (edge < 3.5f) v *= 0.85f;
                    px[y * N + x] = new Color(v, v * 0.93f, v * 0.86f, 1f);
                }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true, true);
            plankTex = t;
            return t;
        }

        /// <summary>Jato de água (chafariz): sobe "height" m e cai mais "drop" m. Gotas aditivas, leves.</summary>
        public static ParticleSystem WaterSpray(Transform parent, Vector3 pos, float height, Color c, int max = 40, float spread = 8f, float drop = 0f)
        {
            var go = new GameObject("Jato_Agua");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            const float gMod = 0.7f;
            float g = 9.81f * gMod;
            float v = Mathf.Sqrt(2f * g * Mathf.Max(0.05f, height));
            float life = (v + Mathf.Sqrt(v * v + 2f * g * Mathf.Max(0f, drop))) / g;
            var m = ps.main;
            m.loop = true;
            m.playOnAwake = true;
            m.duration = 1f;
            m.maxParticles = max;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.9f, life);
            m.startSpeed = new ParticleSystem.MinMaxCurve(v * 0.85f, v);
            m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
            m.startColor = c;
            m.gravityModifier = gMod;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = max / Mathf.Max(0.2f, life);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = spread; sh.radius = 0.05f;
            sh.rotation = new Vector3(-90, 0, 0);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                       new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = U.Fx(true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ atmosfera
        /// <summary>
        /// Raios de luz (god rays falsos): poucas partículas enormes, alongadas e inclinadas, aditivas e
        /// bem fracas, que aparecem e somem devagar. Billboard: funcionam em qualquer ângulo de câmera.
        /// </summary>
        public static ParticleSystem LightShafts(Transform parent, Vector3 center, Vector3 area, Color c, int count = 5, float tiltDeg = 22f)
        {
            var go = new GameObject("Raios_de_Luz");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true;
            m.playOnAwake = true;
            m.prewarm = true;
            m.duration = 10f;
            m.maxParticles = count;
            m.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
            m.startSpeed = 0f;
            m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            m.startSizeY = new ParticleSystem.MinMaxCurve(11f, 15f);
            m.startSizeZ = 1f;
            m.startRotation = new ParticleSystem.MinMaxCurve((tiltDeg - 6f) * Mathf.Deg2Rad, (tiltDeg + 6f) * Mathf.Deg2Rad);
            m.startColor = c;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = count / 11f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = area;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                       new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.35f), new GradientAlphaKey(1, 0.65f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = U.Fx(true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 5f;   // deixa as partículas grandes ocuparem a tela quando a câmera chega perto
            ps.Play();
            return ps;
        }

        /// <summary>Poeira suspensa: muitas partículas minúsculas e brilhantes, quase paradas (flutuam no ar).</summary>
        public static ParticleSystem Dust(Transform parent, Vector3 center, Vector3 size, Color c, int max = 60)
        {
            var ps = Motes(parent, center, size, c, max, 0.004f);
            ps.gameObject.name = "Poeira";
            var m = ps.main;
            m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.08f);
            m.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            var em = ps.emission; em.rateOverTime = max / 8f;
            var no = ps.noise; no.strength = 0.15f; no.frequency = 0.15f;
            return ps;
        }

        // ------------------------------------------------------------------ peças prontas
        public static readonly Color TorchColor = new Color(1f, 0.62f, 0.3f);
        public static readonly Color FlameColor = new Color(1f, 0.55f, 0.15f);

        /// <summary>Poste com lanterna (cidade). Luz quente suave.</summary>
        public static GameObject LampPost(Transform props, Transform lights, Vector3 pos, float intensity = 1.6f, float range = 7f)
        {
            var g = new GameObject("Poste");
            g.transform.SetParent(props, false);
            g.transform.localPosition = pos;
            U.Prim(PrimitiveType.Cylinder, g.transform, new Vector3(0, 1.4f, 0), new Vector3(0.18f, 1.4f, 0.18f), U.Hex("3d2e22"));
            U.Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 0.12f, 0), new Vector3(0.45f, 0.24f, 0.45f), U.Hex("5a5560"));
            SpawnSized("lantern", g.transform, new Vector3(0, 2.75f, 0), 0f, 0.7f);
            U.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 3.05f, 0), Vector3.one * 0.22f, U.Hex("ffd27a"));
            var l = PointLight(lights, pos + new Vector3(0, 3.1f, 0), TorchColor, range, intensity, false, false);
            l.gameObject.name = "Luz_Poste";
            return g;
        }

        /// <summary>Tocha de pé (fincada no chão) com chama e luz.</summary>
        public static GameObject StandingTorch(Transform props, Transform lights, Vector3 pos, float intensity = 2.6f, float range = 7.5f, bool shadows = false)
        {
            var g = SpawnSized("torch", props, pos, 0f, 1.7f);
            Flame(props, pos + new Vector3(0, 1.75f, 0), 1f, FlameColor);
            PointLight(lights, pos + new Vector3(0, 2.1f, 0), TorchColor, range, intensity, shadows, true);
            return g;
        }

        /// <summary>Tocha presa na parede. dir = direção da parede para dentro da sala.</summary>
        public static GameObject WallTorch(Transform props, Transform lights, Vector3 wallPoint, Vector3 dir, float y = 1.9f, float intensity = 2.8f, float range = 8f, bool shadows = false)
        {
            dir = U.Flat(dir).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            Vector3 p = wallPoint + dir * 0.15f;
            p.y = y - 0.55f;
            // suporte de ferro
            var s = U.Prim(PrimitiveType.Cube, props, p + new Vector3(0, 0.35f, 0) - dir * 0.05f, new Vector3(0.12f, 0.12f, 0.35f), U.Hex("2c2a30"));
            s.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var g = SpawnSized("torch", props, p + dir * 0.15f, yaw, 1.0f, 18f);
            Flame(props, p + dir * 0.45f + new Vector3(0, 0.95f, 0), 0.8f, FlameColor, 12);
            PointLight(lights, p + dir * 0.9f + new Vector3(0, 1.1f, 0), TorchColor, range, intensity, shadows, true);
            return g;
        }

        /// <summary>Braseiro (balde de metal com fogo) — usado na masmorra.</summary>
        public static GameObject Brazier(Transform props, Transform lights, Vector3 pos, float intensity = 3.2f, float range = 9f, bool shadows = false)
        {
            var g = SpawnSized("bucket_metal", props, pos, 0f, 0.95f);
            AddCollider(g, 0.8f, 1f);
            Flame(props, pos + new Vector3(0, 1.0f, 0), 1.6f, FlameColor, 20);
            PointLight(lights, pos + new Vector3(0, 1.8f, 0), TorchColor, range, intensity, shadows, true);
            return g;
        }
    }

}
