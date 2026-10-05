using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Efeitos visuais procedurais (partículas, anéis no chão, pilares de luz, raios, cortes em arco).
    /// Não dependem de nenhum pacote: dá para trocar por prefabs de VFX depois
    /// (ver FX.Prefab: se existir Resources/VFX/&lt;nome&gt;, ele é usado no lugar).
    /// Efeitos específicos de cada habilidade ficam em SkillFX.cs.
    /// </summary>
    public static class FX
    {
        static Transform root;
        public static Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("FX").transform;
                return root;
            }
        }

        /// <summary>Se houver um prefab em Resources/VFX/&lt;id&gt;, instancia ele (para trocar os efeitos por VFX prontos).</summary>
        public static bool Prefab(string id, Vector3 pos, float scale = 1f)
        {
            var p = Resources.Load<GameObject>("VFX/" + id);
            if (p == null) return false;
            var g = Object.Instantiate(p, pos, Quaternion.identity, Root);
            g.transform.localScale *= scale;
            Object.Destroy(g, 4f);
            return true;
        }

        /// <summary>Cria um ParticleSystem "de uma vez só" (não repete, se destrói sozinho no fim).</summary>
        public static ParticleSystem NewSystem(string name, Vector3 pos, bool additive = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 1500;
            var em = ps.emission; em.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = U.Fx(additive);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>Cor ao longo da vida: começa branca/quente, vai para a cor e some.</summary>
        public static void Fade(ParticleSystem ps, Color c)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(c, 0.35f), new GradientColorKey(c, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>Explosão de faíscas/luz (impacto, magia).</summary>
        public static void Burst(Vector3 pos, Color c, float size = 1f, int count = 24)
        {
            var ps = NewSystem("burst", pos);
            var m = ps.main;
            m.duration = 0.5f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f * size, 7f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(0.12f * size, 0.35f * size);
            m.startColor = c;
            m.gravityModifier = 0.4f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f * size;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)Mathf.Clamp(count, 1, 500)) });
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            Fade(ps, c);
            ps.Play();
            if (size >= 0.75f) FlashLight(pos + Vector3.up * 0.3f, c, 2.5f * size, 3.5f + 2f * size, 0.25f);
            if (size >= 0.6f) ElementFlavor(pos, c, size);
        }

        /// <summary>
        /// [Efeitos] Tempero por elemento, deduzido da cor do Burst (venenos verdes, fogo laranja, gelo/água ciano-claro):
        /// assim os efeitos antigos de inimigos/armadilhas que usam FX.Burst ganham bolhas, brasas ou estilhaços sem mudar quem chama.
        /// </summary>
        static void ElementFlavor(Vector3 pos, Color c, float size)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (v < 0.6f) return;
            if (h >= 0.22f && h <= 0.36f && s >= 0.6f)
            {
                // veneno: bolhas subindo, gotas pingando, névoa verde
                var bub = SkillFX.Sys("veneno_bolhas", pos, c, true, 0.6f, 1.1f, 0.4f, 1.4f, 0.08f * size, 0.18f * size, -0.35f);
                bub.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, U.RingTexture());
                SkillFX.Sphere(bub, 0.4f * size); SkillFX.Bursts(bub, Mathf.RoundToInt(10 * size)); SkillFX.Noise(bub, 0.4f, 1.2f);
                SkillFX.Grad(bub, Color.Lerp(c, Color.white, 0.5f), c, 0.9f); bub.Play();
                var drip = SkillFX.Sys("veneno_gotas", pos, c, true, 0.4f, 0.7f, 1f, 3f, 0.05f, 0.09f, 1.6f);
                SkillFX.Hemisphere(drip, 0.2f); SkillFX.Bursts(drip, Mathf.RoundToInt(10 * size)); SkillFX.Stretch(drip, 1.3f, 0.05f);
                SkillFX.Grad(drip, Color.white, c, 1f); drip.Play();
                var mist = SkillFX.Sys("veneno_nevoa", pos, new Color(c.r * 0.7f, c.g * 0.85f, c.b * 0.5f, 0.4f), false, 0.8f, 1.3f, 0.3f, 0.9f, 0.5f * size, 0.9f * size, -0.05f);
                SkillFX.Sphere(mist, 0.4f * size); SkillFX.Bursts(mist, Mathf.RoundToInt(5 * size));
                FxUtil.AlphaFade(mist, Color.Lerp(c, Color.white, 0.2f), c * 0.5f, 0.4f); mist.Play();
            }
            else if (h >= 0.04f && h <= 0.105f && s >= 0.6f)
            {
                // fogo: língua de chama, brasas subindo, fumacinha
                var fl = SkillFX.Sys("fogo_puff", pos, c, true, 0.3f, 0.6f, 1f, 2.5f, 0.25f * size, 0.5f * size, -0.5f);
                SkillFX.Sphere(fl, 0.3f * size); SkillFX.Bursts(fl, Mathf.RoundToInt(10 * size)); SkillFX.Noise(fl, 1f, 2f);
                FxUtil.FireGrad(fl); fl.Play();
                SkillFX.Embers(pos - Vector3.up * 0.2f, 0.4f * size, SkillFX.Ember, Mathf.RoundToInt(14 * size), 0.9f, 3f);
                SkillFX.Smoke(pos - Vector3.up * 0.4f, 0.3f * size, Mathf.RoundToInt(4 * size), 0.7f * size);
            }
            else if (h >= 0.52f && h <= 0.556f && s >= 0.15f && v >= 0.85f)
            {
                // gelo/água: estilhaços, gotas e névoa fria
                SkillFX.IceShards(pos, Vector3.zero, Mathf.RoundToInt(8 * size), 0.5f * size);
                var drops = SkillFX.Sys("agua_gotas", pos, Color.Lerp(c, Color.white, 0.3f), true, 0.3f, 0.6f, 2f, 4.5f, 0.04f, 0.08f, 1.6f);
                SkillFX.Hemisphere(drops, 0.15f); SkillFX.Bursts(drops, Mathf.RoundToInt(14 * size)); SkillFX.Stretch(drops, 1.3f, 0.05f);
                SkillFX.Grad(drops, Color.white, c, 1f); drops.Play();
                SkillFX.Mist(pos, c, Mathf.RoundToInt(5 * size), 0.4f * size);
            }
        }

        /// <summary>Poeira no chão (passos, investidas, impactos pesados).</summary>
        public static void Dust(Vector3 pos, Color c, float size = 1f)
        {
            var ps = NewSystem("dust", pos + Vector3.up * 0.1f, false);
            var m = ps.main;
            m.duration = 0.6f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * size, 2.4f * size);
            m.startSize = new ParticleSystem.MinMaxCurve(0.35f * size, 0.8f * size);
            m.startColor = new Color(c.r, c.g, c.b, 0.55f);
            m.gravityModifier = -0.05f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.4f * size;
            sh.rotation = new Vector3(90, 0, 0);
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)Mathf.Clamp(Mathf.RoundToInt(14 * size), 1, 200)) });
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.4f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(0.6f, 0), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ps.Play();
        }

        /// <summary>Brilhos subindo (cura, nível, bênçãos).</summary>
        public static void Sparkle(Vector3 pos, Color c, float size = 1f, float time = 0.8f)
        {
            var ps = NewSystem("sparkle", pos);
            var m = ps.main;
            m.duration = Mathf.Max(0.05f, time);
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.08f * size, 0.22f * size);
            m.startColor = c;
            m.gravityModifier = -0.35f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.7f * size; sh.rotation = new Vector3(90, 0, 0);
            var em = ps.emission; em.rateOverTime = 40 * size;
            Fade(ps, c);
            ps.Play();
        }

        /// <summary>Coluna de luz (buffs, cura forte, subida de nível).</summary>
        public static void Pillar(Vector3 pos, Color c, float size = 1f)
        {
            Sparkle(pos, c, size, 0.6f);
            var ps = NewSystem("pillar", pos);
            var m = ps.main;
            m.duration = 0.5f;
            m.startLifetime = 0.7f;
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.15f * size, 0.3f * size);
            m.startColor = c;
            // cone de ângulo zero apontado para cima = partículas saem de um disco e sobem retas
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 2f; sh.radius = 0.5f * size; sh.rotation = new Vector3(-90, 0, 0);
            var em = ps.emission; em.rateOverTime = 70;
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 3f;
            Fade(ps, c);
            ps.Play();
            Ring(pos, 1.2f * size, c, 0.5f);
            FlashLight(pos + Vector3.up, c, 4f * size, 6f, 0.5f);
        }

        /// <summary>Anel que se expande no chão (ondas de choque, área de habilidades).</summary>
        public static void Ring(Vector3 pos, float radius, Color c, float time = 0.4f, float startScale = 0.2f)
        {
            var q = FlatQuad("ring", pos + Vector3.up * 0.05f, U.RingTexture(), c, true);
            var a = q.AddComponent<FxAnim>();
            a.Setup(time, startScale * radius * 2f, radius * 2f, c, 1f, 0f);
        }

        /// <summary>Marca no chão que enche até o impacto (telegrafa a habilidade).</summary>
        public static GameObject Marker(Vector3 pos, float radius, Color c, float time)
        {
            var outer = FlatQuad("marker", pos + Vector3.up * 0.04f, U.RingTexture(), c, false);
            outer.transform.localScale = Vector3.one * radius * 2f;
            var inner = FlatQuad("fill", pos + Vector3.up * 0.045f, U.SoftTexture(), new Color(c.r, c.g, c.b, 0.45f), false);
            inner.transform.SetParent(outer.transform, false);
            inner.transform.localPosition = new Vector3(0, 0, -0.002f);
            inner.transform.localRotation = Quaternion.identity;
            var a = inner.AddComponent<FxAnim>();
            a.Setup(time, 0.05f, 1f, new Color(c.r, c.g, c.b, 0.5f), 0.5f, 0.5f);
            a.keep = true;
            return outer;
        }

        // ------------------------------------------------------------------ corte em arco (malha gerada)
        /// <summary>Corte em arco (~140°) à frente do personagem, centrado em <paramref name="dir"/>, varrendo da direita para a esquerda.</summary>
        public static void Slash(Vector3 pos, Vector3 dir, Color c, float radius = 1.6f)
        {
            SlashArc(pos, dir, c, radius, 140f, true, 0.18f, 0.55f, 14);
        }

        /// <summary>
        /// Corte em arco horizontal feito com uma malha curva (faixa com UV) + faíscas.
        ///
        /// ALINHAMENTO (por que o arco fica centrado em dir):
        ///  - A malha é gerada no espaço LOCAL com o centro do arco no eixo +Z:
        ///    um vértice no ângulo a (graus, medido a partir de +Z, positivo para +X) fica em
        ///    (sin a, 0, cos a) * r. Para a = 0 → (0,0,1)*r, ou seja, exatamente +Z.
        ///  - O arco vai de a = -arc/2 até +arc/2, simétrico em torno de a = 0.
        ///  - O objeto recebe rotation = Quaternion.LookRotation(dir, up), que por definição leva
        ///    o +Z local para dir (achatado no plano). Logo o meio do arco aponta para dir.
        ///  - LookRotation(dir, up) leva o +X local para Cross(up, dir) = a DIREITA de quem olha para dir.
        ///    Então a &gt; 0 é o lado direito e a &lt; 0 o esquerdo: "direita→esquerda" = varrer de +arc/2 até -arc/2.
        /// </summary>
        public static void SlashArc(Vector3 pos, Vector3 dir, Color c, float radius, float arcDeg = 140f, bool rightToLeft = true,
                                    float time = 0.18f, float width = 0.55f, int sparks = 14)
        {
            dir = U.Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            radius = Mathf.Max(0.4f, radius);
            width = Mathf.Clamp(width, 0.1f, radius * 0.9f);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            // camada 1: faixa larga na cor do golpe
            var outer = SlashLayer("slash", pos, rot, radius, width, arcDeg, rightToLeft, new Color(c.r, c.g, c.b, 0.85f), time);
            // camada 2: fio quente e fino na borda externa (branco puxado para a cor)
            Color hot = Color.Lerp(c, Color.white, 0.7f);
            SlashLayer("slash_core", pos + Vector3.up * 0.01f, rot, radius * 1.01f, width * 0.32f, arcDeg * 0.96f, rightToLeft, hot, time * 0.9f);

            // faíscas emitidas na "cabeça" do corte enquanto ele varre
            if (sparks > 0)
            {
                var ps = NewSystem("slash_sparks", pos);
                var m = ps.main;
                m.duration = 0.6f;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                m.startColor = hot;
                m.gravityModifier = 1.4f;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 1.5f; r.velocityScale = 0.04f;
                Fade(ps, c);
                ps.Play();
                outer.sparks = ps;
                outer.sparkCount = sparks;
            }
            FlashLight(pos + dir * radius * 0.7f, c, 2.2f, 3.5f + radius, Mathf.Max(0.12f, time));
        }

        static SlashAnim SlashLayer(string name, Vector3 pos, Quaternion rot, float radius, float width, float arcDeg, bool rightToLeft, Color c, float time)
        {
            const int N = 28;                         // colunas ao longo do arco
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var mesh = new Mesh { name = "slash_arc" };
            mesh.MarkDynamic();
            var verts = new Vector3[(N + 1) * 2];
            var uvs = new Vector2[(N + 1) * 2];
            var cols = new Color[(N + 1) * 2];
            float half = arcDeg * 0.5f;
            float start = rightToLeft ? half : -half;  // +X local = direita (ver comentário de SlashArc)
            float end = -start;
            for (int i = 0; i <= N; i++)
            {
                float u = (float)i / N;                // u = 0 no início da varredura, 1 no fim
                float a = Mathf.Lerp(start, end, u) * Mathf.Deg2Rad;
                // meia-lua: mais grossa no meio, fina nas pontas
                float taper = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp(u * 0.92f + 0.04f, 0f, 1f)), 0.7f);
                float rIn = radius - width * taper;
                float rOut = radius + width * 0.08f * taper;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts[i * 2] = d * rIn;
                verts[i * 2 + 1] = d * rOut;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
                cols[i * 2] = new Color(1, 1, 1, 0);
                cols[i * 2 + 1] = new Color(1, 1, 1, 0);
            }
            // dois lados (a câmera vê de cima, mas assim não depende do "Render Face" do material)
            var tris = new int[N * 12];
            int t = 0;
            for (int i = 0; i < N; i++)
            {
                int a0 = i * 2, b0 = i * 2 + 1, a1 = i * 2 + 2, b1 = i * 2 + 3;
                tris[t++] = a0; tris[t++] = b0; tris[t++] = a1;
                tris[t++] = a1; tris[t++] = b0; tris[t++] = b1;
                tris[t++] = a0; tris[t++] = a1; tris[t++] = b0;
                tris[t++] = a1; tris[t++] = b1; tris[t++] = b0;
            }
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = U.Fx(true, SlashTexture());
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            SetColor(mr, c);
            var anim = go.AddComponent<SlashAnim>();
            anim.Setup(mesh, cols, N, Mathf.Max(0.06f, time), start, end, radius);
            return anim;
        }

        static Texture2D slashTex;
        /// <summary>Gradiente da faixa do corte: transparente na borda interna (v=0), forte na externa (v=1).</summary>
        public static Texture2D SlashTexture()
        {
            if (slashTex != null) return slashTex;
            const int w = 8, h = 64;
            slashTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "slash_grad" };
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float a = Mathf.Pow(v, 2.2f);
                if (v > 0.9f) a *= Mathf.Lerp(1f, 0.25f, (v - 0.9f) / 0.1f);   // borda externa macia
                for (int x = 0; x < w; x++) slashTex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            slashTex.Apply();
            return slashTex;
        }

        // ------------------------------------------------------------------ raios
        /// <summary>Raio entre dois pontos (Corrente de Raios).</summary>
        public static void Bolt(Vector3 a, Vector3 b, Color c)
        {
            Lightning(a, b, c, 0.14f, 2, 0.25f);
        }

        /// <summary>Raio em ziguezague (subdivisão de ponto médio) com ramificações e brilho por baixo.</summary>
        public static void Lightning(Vector3 a, Vector3 b, Color c, float width = 0.14f, int branches = 2, float time = 0.25f)
        {
            var pts = ZigZag(a, b, 0.22f * Mathf.Max(1f, Vector3.Distance(a, b) * 0.25f), 5);
            LineFx("bolt", pts, Color.white, c, width, time);
            LineFx("bolt_glow", pts, new Color(c.r, c.g, c.b, 0.35f), new Color(c.r, c.g, c.b, 0.2f), width * 4f, time * 0.8f);
            for (int k = 0; k < branches && pts.Count > 4; k++)
            {
                int i = Random.Range(1, pts.Count - 2);
                Vector3 p = pts[i];
                Vector3 main = (b - a).normalized;
                Vector3 bd = (main + Random.insideUnitSphere * 1.2f).normalized;
                float len = Vector3.Distance(a, b) * Random.Range(0.15f, 0.35f);
                var bp = ZigZag(p, p + bd * len, 0.12f, 3);
                LineFx("bolt_branch", bp, new Color(1, 1, 1, 0.8f), c, width * 0.5f, time * 0.8f);
            }
            FlashLight(b, c, 3f, 5f, Mathf.Max(0.15f, time));
        }

        static List<Vector3> ZigZag(Vector3 a, Vector3 b, float amp, int levels)
        {
            var pts = new List<Vector3> { a, b };
            for (int l = 0; l < levels; l++)
            {
                var np = new List<Vector3>(pts.Count * 2);
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    np.Add(pts[i]);
                    Vector3 mid = (pts[i] + pts[i + 1]) * 0.5f + Random.insideUnitSphere * amp;
                    np.Add(mid);
                }
                np.Add(pts[pts.Count - 1]);
                pts = np;
                amp *= 0.55f;
            }
            return pts;
        }

        static void LineFx(string name, List<Vector3> pts, Color c0, Color c1, float width, float time)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            lr.widthMultiplier = width;
            lr.sharedMaterial = U.Fx(true);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = c0; lr.endColor = c1;
            var fa = go.AddComponent<FxAnim>();
            fa.SetupLine(lr, time, c1);
        }

        // ------------------------------------------------------------------ luzes temporárias (pool)
        /// <summary>Clarão de luz curto (compatibilidade: usa o pool de luzes).</summary>
        public static void Flash(Vector3 pos, Color c, float intensity, float time)
        {
            FlashLight(pos, c, intensity, 5f, time);
        }

        public const int MaxLights = 8;
        static readonly List<FxLight> lights = new();

        /// <summary>
        /// Luz pontual temporária com fade (sem sombras). Usa um pool de no máximo 8 luzes:
        /// se todas estiverem ocupadas, reaproveita a que está mais perto de acabar.
        /// </summary>
        public static void FlashLight(Vector3 pos, Color c, float intensity, float range, float duration)
        {
            FxLight pick = null;
            float bestLeft = float.MaxValue;
            for (int i = lights.Count - 1; i >= 0; i--)
            {
                var l = lights[i];
                if (l == null) { lights.RemoveAt(i); continue; }
                if (!l.Busy) { pick = l; break; }
                float left = l.Remaining;
                if (left < bestLeft) { bestLeft = left; pick = l; }
            }
            if ((pick == null || pick.Busy) && lights.Count < MaxLights)
            {
                var go = new GameObject("luz_fx");
                go.transform.SetParent(Root, false);
                var lc = go.AddComponent<Light>();
                lc.type = LightType.Point;
                lc.shadows = LightShadows.None;
                lc.enabled = false;
                pick = go.AddComponent<FxLight>();
                pick.Init(lc);
                lights.Add(pick);
            }
            if (pick == null) return;
            pick.Play(pos, c, intensity, range, duration);
        }

        // ------------------------------------------------------------------ utilitários
        public static GameObject FlatQuad(string name, Vector3 pos, Texture tex, Color c, bool additive)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(Root, false);
            q.transform.position = pos;
            q.transform.rotation = Quaternion.Euler(90, 0, 0);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = U.Fx(additive, tex);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetColor(r, c);
            return q;
        }

        public static void SetColor(Renderer r, Color c)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            mpb.SetColor("_TintColor", c);
            r.SetPropertyBlock(mpb);
        }

        static Mesh cubeMesh;
        /// <summary>Malha de cubo (para partículas em modo Mesh: estilhaços, pedras).</summary>
        public static Mesh CubeMesh()
        {
            if (cubeMesh != null) return cubeMesh;
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = g.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(g);
            return cubeMesh;
        }

        // ------------------------------------------------------------------ efeitos por estilo (data/skills.json -> "vfx")
        public static void Style(string style, Vector3 pos, Color c, float size)
        {
            if (Prefab(style, pos, size)) return;
            switch (style)
            {
                case "ring": Ring(pos, size, c, 0.4f); break;
                case "burst": Burst(pos + Vector3.up * 0.6f, c, Mathf.Max(0.6f, size * 0.6f), 30); break;
                case "dust": Dust(pos, c, Mathf.Max(0.8f, size * 0.6f)); break;
                case "pillar": Pillar(pos, c, Mathf.Max(0.8f, size * 0.5f)); break;
                case "sparkle": Sparkle(pos + Vector3.up * 0.3f, c, 1f); break;
                case "shield": Ring(pos, 1.3f, c, 0.6f, 0.8f); Sparkle(pos + Vector3.up, c, 1f); FlashLight(pos + Vector3.up, c, 3f, 5f, 0.6f); break;
                case "slash": Burst(pos + Vector3.up * 0.8f, c, 1f, 26); break;
                default: Burst(pos + Vector3.up * 0.6f, c, 1f, 20); break;
            }
        }
    }

    /// <summary>Anima escala/cor de um efeito e o destrói no fim.</summary>
    public class FxAnim : MonoBehaviour
    {
        float t, time, s0, s1, a0, a1;
        Color c;
        Renderer rend;
        LineRenderer line;
        Light lightC;
        float li;
        public bool keep;

        public void Setup(float time, float s0, float s1, Color c, float a0, float a1)
        {
            this.time = Mathf.Max(0.01f, time); this.s0 = s0; this.s1 = s1; this.c = c; this.a0 = a0; this.a1 = a1;
            rend = GetComponent<Renderer>();
            transform.localScale = Vector3.one * s0;
        }

        public void SetupLine(LineRenderer l, float time, Color c) { line = l; this.time = Mathf.Max(0.01f, time); this.c = c; }
        public void SetupLight(Light l, float time, float intensity) { lightC = l; this.time = Mathf.Max(0.01f, time); li = intensity; }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            if (rend != null)
            {
                float e = 1f - Mathf.Pow(1f - k, 3f);
                transform.localScale = Vector3.one * Mathf.Lerp(s0, s1, e);
                var col = c; col.a = c.a * Mathf.Lerp(a0, a1, k);
                FX.SetColor(rend, col);
            }
            if (line != null)
            {
                var col = c; col.a = c.a * (1f - k);
                line.startColor = new Color(1, 1, 1, 1f - k); line.endColor = col;
            }
            if (lightC != null) lightC.intensity = li * (1f - k);
            if (k >= 1f && !keep) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Animação genérica de efeito: escala (vetor) de s0 para s1, alpha de a0 para a1 (com fade-in opcional),
    /// giro em torno do eixo Y do mundo, deslocamento, atraso. Destrói o objeto (e a malha própria) no fim.
    /// </summary>
    public class FxTween : MonoBehaviour
    {
        public float time = 0.5f, delay, fadeIn, spin, hold;
        public Vector3 s0 = Vector3.one, s1 = Vector3.one, move;
        public Color color = Color.white;
        public float a0 = 1f, a1 = 0f;
        public bool keep, unscaled;
        public Mesh ownedMesh;
        Renderer rend;
        float t;

        public FxTween Set(float time, Vector3 s0, Vector3 s1, Color c, float a0 = 1f, float a1 = 0f)
        {
            this.time = Mathf.Max(0.01f, time); this.s0 = s0; this.s1 = s1; color = c; this.a0 = a0; this.a1 = a1;
            rend = GetComponent<Renderer>();
            transform.localScale = s0;
            if (rend != null) { var cc = c; cc.a = delay > 0f || fadeIn > 0f ? 0f : c.a * a0; FX.SetColor(rend, cc); }
            return this;
        }

        void Update()
        {
            float dt = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            if (rend == null) rend = GetComponent<Renderer>();
            if (t < delay)
            {
                if (rend != null) { var z = color; z.a = 0f; FX.SetColor(rend, z); }
                return;
            }
            float lt = t - delay;
            float k = Mathf.Clamp01(lt / time);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            transform.localScale = Vector3.LerpUnclamped(s0, s1, e);
            if (rend != null)
            {
                var col = color;
                // "hold": fração do tempo em que o alpha fica em a0 antes de começar a ir para a1
                float ka = hold > 0f ? Mathf.Clamp01((k - hold) / Mathf.Max(0.001f, 1f - hold)) : k;
                col.a = color.a * Mathf.Lerp(a0, a1, ka) * (fadeIn > 0f ? Mathf.Clamp01(lt / fadeIn) : 1f);
                FX.SetColor(rend, col);
            }
            if (spin != 0f) transform.Rotate(Vector3.up, spin * dt, Space.World);
            if (move != Vector3.zero) transform.position += move * dt;
            if (k >= 1f && !keep) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
        }
    }

    /// <summary>Anima a malha do corte: a "cabeça" varre de u=0 até u=1 deixando um rastro que some; emite faíscas na cabeça.</summary>
    public class SlashAnim : MonoBehaviour
    {
        Mesh mesh;
        Color[] cols;
        int n;
        float time, t, start, end, radius;
        const float Trail = 0.75f;          // comprimento do rastro (em fração do arco)
        public ParticleSystem sparks;
        public int sparkCount;
        int emitted;

        public void Setup(Mesh mesh, Color[] cols, int n, float time, float start, float end, float radius)
        {
            this.mesh = mesh; this.cols = cols; this.n = n; this.time = time; this.start = start; this.end = end; this.radius = radius;
            Apply(0f);
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            Apply(k);
            // leve "abertura" do corte para fora
            transform.localScale = Vector3.one * (1f + 0.08f * k);
            if (sparks != null && emitted < sparkCount)
            {
                float head = Mathf.Clamp01(Head(k));
                int want = Mathf.Min(sparkCount, Mathf.CeilToInt(head * sparkCount));
                for (; emitted < want; emitted++)
                {
                    float u = Mathf.Clamp01(head - Random.Range(0f, 0.1f));
                    float a = Mathf.Lerp(start, end, u) * Mathf.Deg2Rad;
                    Vector3 local = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                    // tangente no sentido da varredura (derivada de (sin a, 0, cos a) em a, com o sinal de end-start)
                    Vector3 tangLocal = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)) * Mathf.Sign(end - start);
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = transform.TransformPoint(local),
                        velocity = transform.TransformDirection(tangLocal * Random.Range(2f, 5f) + local.normalized * Random.Range(0.5f, 2f)) + Vector3.up * Random.Range(0.5f, 2.5f),
                        applyShapeToPosition = false
                    };
                    sparks.Emit(ep, 1);
                }
            }
            if (k >= 1f)
            {
                Destroy(gameObject);
            }
        }

        // cabeça com ease-out: rápida no começo, desacelera
        static float Head(float k) => (1f - (1f - k) * (1f - k)) * (1f + Trail);

        void Apply(float k)
        {
            if (mesh == null) return;
            float head = Head(k);
            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n;
                float d = head - u;                               // quanto a cabeça já passou deste ponto
                float a = d < 0f ? 0f : Mathf.Clamp01(1f - d / Trail);
                a *= a;
                if (d >= 0f && d < 0.06f) a = 1f;                  // cabeça brilhante
                cols[i * 2] = new Color(1f, 1f, 1f, a);
                cols[i * 2 + 1] = new Color(1f, 1f, 1f, a);
            }
            mesh.colors = cols;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }

    /// <summary>Luz do pool de FX.FlashLight: acende, apaga com fade e fica livre para reuso.</summary>
    public class FxLight : MonoBehaviour
    {
        Light l;
        float t, dur, i0;
        bool busy;
        public bool Busy => busy;
        public float Remaining => busy ? (dur - t) * i0 : 0f;

        public void Init(Light light) { l = light; }

        public void Play(Vector3 pos, Color c, float intensity, float range, float duration)
        {
            if (l == null) l = GetComponent<Light>();
            if (l == null) return;
            transform.position = pos;
            l.color = c;
            l.range = Mathf.Max(0.5f, range);
            i0 = Mathf.Max(0f, intensity);
            l.intensity = i0;
            dur = Mathf.Max(0.03f, duration);
            t = 0f;
            busy = true;
            l.enabled = true;
        }

        void Update()
        {
            if (!busy || l == null) return;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float f = 1f - k;
            l.intensity = i0 * f * f;
            if (k >= 1f) { busy = false; l.enabled = false; }
        }
    }

    /// <summary>Tremulação de luz (fogo, magia) com ruído Perlin.</summary>
    public class FxFlicker : MonoBehaviour
    {
        public float baseIntensity = 2f, amount = 0.35f, speed = 9f;
        Light l; float seed;
        void Start() { l = GetComponent<Light>(); seed = Random.value * 50f; if (l != null && baseIntensity <= 0f) baseIntensity = l.intensity; }
        void Update()
        {
            if (l == null) return;
            float n = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;
            l.intensity = Mathf.Max(0f, baseIntensity * (1f + n * amount));
        }
    }
}
