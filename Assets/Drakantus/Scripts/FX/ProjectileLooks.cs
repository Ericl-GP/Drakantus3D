using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// [Classes] Visual dos projéteis (chamado pelo ProjectileMover.BuildVisual). Tudo fica num "ProjTail" que segue o projétil:
    /// quando o projétil é destruído (acerto/fim), as malhas somem na hora e os rastros/partículas terminam suavemente.
    /// Convenção: o projétil voa para +Z do root, a ~1,1 m do chão.
    /// </summary>
    public static partial class SkillFX
    {
        static readonly Color Wood = new Color(0.45f, 0.32f, 0.2f);
        static readonly Color WoodDark = new Color(0.3f, 0.2f, 0.12f);
        static readonly Color SteelDark = new Color(0.55f, 0.58f, 0.64f);

        /// <summary>Monta o visual do projétil. true = tratado (o Projectile não monta o padrão).</summary>
        public static bool ProjectileVisual(string kind, Transform root, Color col)
        {
            if (root == null || string.IsNullOrEmpty(kind)) return false;
            var mover = root.GetComponent<ProjectileMover>();
            bool fromPlayer = mover == null || mover.fromPlayer;
            ProjTail tail = null;
            try
            {
                switch (kind)
                {
                    case "arrow":
                        tail = NewTail(root, kind);
                        ArrowLook(tail, col, 0.75f, 1f, fromPlayer ? Wind : col, fromPlayer ? Leaf : new Color(0.9f, 0.3f, 0.2f), false);
                        break;
                    case "pierce":
                        tail = NewTail(root, kind);
                        ArrowLook(tail, col, 0.95f, 1.3f, U.Hex("fff2b0"), Gold, true);
                        break;
                    case "longarrow":
                        tail = NewTail(root, kind);
                        ArrowLook(tail, col, 1.1f, 1.05f, U.Hex("e8ffd8"), U.Hex("8fe0a0"), false);
                        LongWind(tail, U.Hex("c8ffd8"), 0.18f);
                        break;
                    case "heavy_arrow":
                        tail = NewTail(root, kind);
                        HeavyArrowLook(tail, col);
                        break;
                    case "pin_arrow":
                        tail = NewTail(root, kind);
                        PinArrowLook(tail, col);
                        break;
                    case "bullet":
                        tail = NewTail(root, kind);
                        BulletLook(tail, col);
                        break;
                    case "bullet_explosive":
                        tail = NewTail(root, kind);
                        ExplosiveBulletLook(tail, col);
                        break;
                    case "holy":
                        tail = NewTail(root, kind);
                        HolyLook(tail, col);
                        break;
                    case "homing_orb":
                        tail = NewTail(root, kind);
                        OrbLook(tail, col, 0.24f, 2, true);
                        break;
                    case "orb":
                        tail = NewTail(root, kind);
                        OrbLook(tail, col, fromPlayer ? 0.26f : 0.32f, fromPlayer ? 2 : 1, false);
                        break;
                    case "arcane":
                        tail = NewTail(root, kind);
                        OrbLook(tail, col, 0.28f, 3, true);
                        break;
                    case "fireball":
                        tail = NewTail(root, kind);
                        FireballLook(tail, col);
                        break;
                    case "frost":
                        tail = NewTail(root, kind);
                        FrostLook(tail, col);
                        break;
                    case "axe_spin":
                        tail = NewTail(root, kind);
                        AxeLook(tail, col);
                        break;
                    default:
                        return false;
                }
                tail.Snap();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SkillFX] visual do projétil '" + kind + "': " + e.Message);
                if (tail != null) Object.Destroy(tail.gameObject);
                return false;
            }
        }

        // ------------------------------------------------------------------ peças
        static ProjTail NewTail(Transform root, string kind)
        {
            var go = new GameObject("visual_" + kind);
            go.transform.SetParent(FX.Root, false);
            go.transform.SetPositionAndRotation(root.position, root.rotation);
            var t = go.AddComponent<ProjTail>();
            t.target = root;
            return t;
        }

        static GameObject Part(ProjTail tail, Transform parent, PrimitiveType type, Vector3 local, Vector3 scale, Material mat)
        {
            var g = FxUtil.Shape(type, parent != null ? parent : tail.transform, local, scale);
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            tail.solids.Add(r);
            return g;
        }

        static GameObject Head(ProjTail tail, Transform parent, Vector3 local, Vector3 scale, Material mat)
        {
            var g = new GameObject("ponta");
            g.transform.SetParent(parent != null ? parent : tail.transform, false);
            g.transform.localPosition = local;
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = FxTex.Pyramid();
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            tail.solids.Add(r);
            return g;
        }

        static GameObject Glow(ProjTail tail, Transform parent, Vector3 local, float size, Color c, Texture tex = null, float roll = 0f)
        {
            var q = FxUtil.Shape(PrimitiveType.Quad, parent != null ? parent : tail.transform, local, Vector3.one * size);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = U.Fx(true, tex != null ? tex : U.SoftTexture());
            FX.SetColor(r, c);
            var bb = q.AddComponent<FxBillboard>(); bb.roll = roll;
            tail.solids.Add(r);
            return q;
        }

        static TrailRenderer Trail(ProjTail tail, Transform parent, Vector3 local, Color c0, Color c1, float width, float time, bool additive = true, Texture tex = null)
        {
            var g = new GameObject("rastro");
            g.transform.SetParent(parent != null ? parent : tail.transform, false);
            g.transform.localPosition = local;
            var tr = g.AddComponent<TrailRenderer>();
            tr.sharedMaterial = U.Fx(additive, tex != null ? tex : FxTex.Band());
            tr.time = time;
            tr.widthMultiplier = width;
            tr.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            tr.startColor = c0; tr.endColor = c1;
            tr.minVertexDistance = 0.08f;
            tr.numCapVertices = 2;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tail.AddTrail(tr);
            return tr;
        }

        static ParticleSystem TailSys(ProjTail tail, string name, Vector3 local, Color c, bool additive, float life0, float life1, float spd0, float spd1, float sz0, float sz1, float gravity, float rate, int max = 120)
        {
            var ps = FxUtil.LoopSys(name, tail.transform, local, c, additive, life0, life1, spd0, spd1, sz0, sz1, gravity, rate, max);
            tail.systems.Add(ps);
            return ps;
        }

        static Light TailLight(ProjTail tail, Color c, float intensity, float range, float flicker = 0f)
        {
            var lg = new GameObject("luz");
            lg.transform.SetParent(tail.transform, false);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            if (flicker > 0f) { var f = lg.AddComponent<FxFlicker>(); f.baseIntensity = intensity; f.amount = flicker; f.speed = 14f; }
            tail.AddLight(l);
            return l;
        }

        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ------------------------------------------------------------------ flechas
        /// <summary>Flecha low-poly: haste de madeira, ponta piramidal brilhante, 3 penas, brilho na ponta, rastro fino + rastro de vento.</summary>
        static void ArrowLook(ProjTail tail, Color tipCol, float len, float thick, Color trailCol, Color windCol, bool spiral)
        {
            Transform t = tail.transform;
            float w = 0.035f * thick;
            var shaft = Part(tail, t, PrimitiveType.Cylinder, new Vector3(0f, 0f, -len * 0.18f), new Vector3(w, len * 0.42f, w), U.Lit(Wood, 0.2f));
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Head(tail, t, new Vector3(0f, 0f, len * 0.3f), new Vector3(0.1f * thick, 0.1f * thick, 0.2f * thick), U.Lit(Color.Lerp(tipCol, Color.white, 0.3f), 0.8f, tipCol * 1.6f));
            var fl = U.Lit(windCol, 0.3f, windCol * 0.6f);
            for (int i = 0; i < 3; i++)
            {
                var f = Part(tail, t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.012f, 0.08f * thick, 0.16f * thick), fl);
                var rot = Quaternion.Euler(0f, 0f, i * 120f);
                f.transform.localRotation = rot;
                f.transform.localPosition = new Vector3(0f, 0f, -len * 0.58f) + rot * new Vector3(0f, 0.045f * thick, 0f);
            }
            Glow(tail, t, new Vector3(0f, 0f, len * 0.3f), 0.3f * thick, A(tipCol, 0.75f));
            Trail(tail, t, new Vector3(0f, 0f, -len * 0.5f), A(Color.Lerp(trailCol, Color.white, 0.5f), 0.9f), A(trailCol, 0f), 0.05f * thick, 0.12f);
            Trail(tail, t, new Vector3(0f, 0f, -len * 0.3f), A(windCol, 0.35f), A(windCol, 0f), 0.22f * thick, 0.2f);
            var sp = TailSys(tail, "faiscas", new Vector3(0f, 0f, -len * 0.4f), windCol, true, 0.15f, 0.35f, 0f, 0.3f, 0.03f, 0.06f, 0.1f, 22f, 40);
            FxUtil.Sphere(sp, 0.05f);
            Grad(sp, Color.white, windCol, 0.8f);
            sp.Play();
            if (spiral) Spiral2(tail, 0.22f, 1080f, Gold, 0.05f, 0.16f);
        }

        /// <summary>Dois pontos orbitando o eixo do voo, cada um com rastro (espiral de vento).</summary>
        static void Spiral2(ProjTail tail, float radius, float speed, Color c, float width, float time)
        {
            var pivot = new GameObject("espiral").transform;
            pivot.SetParent(tail.transform, false);
            for (int i = 0; i < 2; i++)
            {
                var p = new GameObject("orbita").transform;
                p.SetParent(pivot, false);
                p.localPosition = (i == 0 ? Vector3.up : Vector3.down) * radius;
                Trail(tail, p, Vector3.zero, A(Color.Lerp(c, Color.white, 0.4f), 0.85f), A(c, 0f), width, time);
            }
            tail.spin = pivot; tail.spinAxis = Vector3.forward; tail.spinSpeed = speed;
        }

        static void LongWind(ProjTail tail, Color c, float r)
        {
            var ring = TailSys(tail, "vento_aneis", new Vector3(0f, 0f, -0.3f), c, true, 0.18f, 0.3f, 1f, 1.8f, 0.03f, 0.06f, 0f, 45f, 60);
            var sh = ring.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = r; sh.radiusThickness = 0f; sh.rotation = Vector3.zero;
            Stretch(ring, 1.4f, 0.04f);
            Grad(ring, Color.white, c, 0.6f);
            ring.Play();
        }

        /// <summary>Tiro Potente: flecha enorme dourada, rastro largo, espiral dupla e ondas de vento.</summary>
        static void HeavyArrowLook(ProjTail tail, Color col)
        {
            Transform t = tail.transform;
            float len = 1.7f;
            var shaft = Part(tail, t, PrimitiveType.Cylinder, new Vector3(0f, 0f, -len * 0.15f), new Vector3(0.08f, len * 0.42f, 0.08f), U.Lit(WoodDark, 0.25f, Gold * 0.25f));
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Head(tail, t, new Vector3(0f, 0f, len * 0.33f), new Vector3(0.24f, 0.24f, 0.46f), U.Lit(Holy, 0.9f, Gold * 2.6f));
            var fl = U.Lit(Gold, 0.4f, Gold * 1.2f);
            for (int i = 0; i < 3; i++)
            {
                var f = Part(tail, t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.02f, 0.2f, 0.36f), fl);
                var rot = Quaternion.Euler(0f, 0f, i * 120f);
                f.transform.localRotation = rot;
                f.transform.localPosition = new Vector3(0f, 0f, -len * 0.55f) + rot * new Vector3(0f, 0.1f, 0f);
            }
            Glow(tail, t, new Vector3(0f, 0f, len * 0.33f), 0.9f, A(Gold, 0.9f));
            Glow(tail, t, new Vector3(0f, 0f, len * 0.33f), 0.55f, A(Color.white, 0.8f), FxTex.Star(), 360f);
            Trail(tail, t, Vector3.zero, A(Color.white, 0.95f), A(Gold, 0f), 0.14f, 0.22f);
            Trail(tail, t, Vector3.zero, A(Gold, 0.45f), A(col, 0f), 0.7f, 0.3f);
            Spiral2(tail, 0.32f, 1260f, Gold, 0.07f, 0.2f);
            LongWind(tail, U.Hex("fff2b0"), 0.35f);
            var emb = TailSys(tail, "brasas_ouro", new Vector3(0f, 0f, -0.3f), Gold, true, 0.3f, 0.6f, 0.2f, 0.8f, 0.04f, 0.09f, 0.4f, 40f, 60);
            FxUtil.Sphere(emb, 0.15f);
            Grad(emb, Color.white, Gold, 1f);
            emb.Play();
            TailLight(tail, Gold, 2.6f, 4.5f);
        }

        /// <summary>Flecha Âncora: ponta violeta, corrente espectral arrastando atrás, runas.</summary>
        static void PinArrowLook(ProjTail tail, Color col)
        {
            Transform t = tail.transform;
            ArrowLook(tail, col, 0.95f, 1.15f, Color.Lerp(col, Color.white, 0.3f), col, false);
            var ring = Glow(tail, t, new Vector3(0f, 0f, 0.1f), 0.7f, A(col, 0.55f), RuneTexture(), 240f);
            ring.name = "runa";
            var go = new GameObject("corrente");
            go.transform.SetParent(t, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = 0.12f;
            lr.textureMode = LineTextureMode.RepeatPerSegment;
            lr.sharedMaterial = U.Fx(false, FxTex.Chain());
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Color cc = Color.Lerp(col, new Color(0.2f, 0.12f, 0.3f), 0.45f);
            lr.startColor = A(cc, 1f); lr.endColor = A(cc, 0f);
            tail.tether = lr; tail.tetherLen = 2.6f; tail.tetherLink = 0.2f;
            TailLight(tail, col, 1.6f, 3f);
        }

        // ------------------------------------------------------------------ balas
        static void BulletLook(ProjTail tail, Color col)
        {
            Transform t = tail.transform;
            Part(tail, t, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.07f, 0.07f, 0.34f), U.Lit(Color.Lerp(col, Color.white, 0.5f), 0.9f, col * 3f));
            Glow(tail, t, new Vector3(0f, 0f, 0.05f), 0.32f, A(col, 0.8f));
            Trail(tail, t, Vector3.zero, A(Color.white, 0.95f), A(col, 0f), 0.06f, 0.08f);
            Trail(tail, t, Vector3.zero, A(col, 0.3f), A(col, 0f), 0.2f, 0.06f);
        }

        static void ExplosiveBulletLook(ProjTail tail, Color col)
        {
            Transform t = tail.transform;
            Part(tail, t, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.17f, 0.17f, 0.3f), U.Lit(FireCore, 0.5f, Fire * 3f));
            Glow(tail, t, Vector3.zero, 0.7f, A(Fire, 0.85f));
            Glow(tail, t, Vector3.zero, 0.45f, A(Color.white, 0.7f), FxTex.Star(), 720f);
            Trail(tail, t, Vector3.zero, A(FireCore, 0.95f), A(FireDeep, 0f), 0.14f, 0.12f);
            var sp = TailSys(tail, "faiscas", Vector3.zero, Ember, true, 0.2f, 0.45f, 0.5f, 2f, 0.03f, 0.07f, 0.6f, 60f, 60);
            FxUtil.Sphere(sp, 0.06f);
            Stretch(sp, 1.4f, 0.04f);
            Grad(sp, FireCore, Fire, 1f);
            sp.Play();
            var sm = TailSys(tail, "fumaca", new Vector3(0f, 0f, -0.1f), SmokeCol, false, 0.4f, 0.7f, 0.1f, 0.3f, 0.15f, 0.3f, -0.1f, 25f, 40);
            FxUtil.Sphere(sm, 0.05f);
            FxUtil.AlphaFade(sm, new Color(0.35f, 0.25f, 0.2f), new Color(0.12f, 0.1f, 0.1f), 0.5f);
            sm.Play();
            TailLight(tail, Fire, 2f, 3.2f, 0.35f);
        }

        // ------------------------------------------------------------------ magias
        static void HolyLook(ProjTail tail, Color col)
        {
            Transform t = tail.transform;
            Part(tail, t, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.22f, U.Lit(Holy, 0.8f, Holy * 2.4f));
            Glow(tail, t, Vector3.zero, 1f, A(Gold, 0.7f));
            Glow(tail, t, Vector3.zero, 0.85f, A(Color.white, 0.9f), FxTex.Star(), 200f);
            Glow(tail, t, Vector3.zero, 0.6f, A(Holy, 0.6f), FxTex.Cross(), -120f);
            Trail(tail, t, Vector3.zero, A(Holy, 0.9f), A(Gold, 0f), 0.24f, 0.22f);
            var fe = TailSys(tail, "penas", Vector3.zero, Gold, true, 0.5f, 0.8f, 0.1f, 0.5f, 0.05f, 0.1f, 0.25f, 30f, 50);
            FxUtil.Sphere(fe, 0.15f);
            Noise(fe, 0.6f, 1.2f);
            Grad(fe, Color.white, Gold, 1f);
            fe.Play();
            TailLight(tail, Gold, 2.2f, 4f);
        }

        /// <summary>Orbe arcano / teleguiado / inimigo: núcleo, halo, motas orbitando com rastro, faíscas.</summary>
        static void OrbLook(ProjTail tail, Color col, float size, int orbiters, bool crackle)
        {
            Transform t = tail.transform;
            Color core = Color.Lerp(col, Color.white, 0.55f);
            Part(tail, t, PrimitiveType.Sphere, Vector3.zero, Vector3.one * size, U.Lit(core, 0.6f, col * 3f));
            Glow(tail, t, Vector3.zero, size * 3.4f, A(col, 0.75f));
            Glow(tail, t, Vector3.zero, size * 2.2f, A(Color.white, 0.45f), FxTex.Star(), 300f);
            Trail(tail, t, Vector3.zero, A(core, 0.85f), A(col, 0f), size * 0.8f, 0.24f);
            if (orbiters > 0)
            {
                var pivot = new GameObject("orbitas").transform;
                pivot.SetParent(t, false);
                for (int i = 0; i < orbiters; i++)
                {
                    var p = new GameObject("mota").transform;
                    p.SetParent(pivot, false);
                    float a = i * Mathf.PI * 2f / orbiters;
                    p.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * size * 1.4f;
                    Part(tail, p, PrimitiveType.Sphere, Vector3.zero, Vector3.one * size * 0.25f, U.Lit(Color.white, 0.5f, col * 2f));
                    Trail(tail, p, Vector3.zero, A(Color.white, 0.8f), A(col, 0f), size * 0.18f, 0.16f);
                }
                tail.spin = pivot; tail.spinAxis = Vector3.forward; tail.spinSpeed = 720f;
            }
            var sp = TailSys(tail, "faiscas", Vector3.zero, col, true, 0.2f, 0.45f, 0.2f, crackle ? 1.6f : 0.8f, 0.05f, 0.12f, 0f, crackle ? 55f : 35f, 70);
            FxUtil.Sphere(sp, size * 0.45f);
            if (crackle) Stretch(sp, 1.3f, 0.05f);
            Grad(sp, Color.white, col, 1f);
            sp.Play();
            TailLight(tail, col, 2f, 3.6f, crackle ? 0.3f : 0f);
        }

        /// <summary>Bola de fogo em camadas: núcleo branco-quente, chamas em world space, brasas, fumaça, luz tremulando.</summary>
        static void FireballLook(ProjTail tail, Color col)
        {
            Transform t = tail.transform;
            Part(tail, t, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.3f, U.Lit(FireCore, 0.4f, FireCore * 3f));
            Glow(tail, t, Vector3.zero, 0.95f, A(FireCore, 0.85f));
            Glow(tail, t, Vector3.zero, 1.7f, A(Fire, 0.4f));
            Trail(tail, t, Vector3.zero, A(Fire, 0.8f), A(FireDeep, 0f), 0.42f, 0.16f);
            var fl = TailSys(tail, "chamas", Vector3.zero, Fire, true, 0.25f, 0.5f, 0.2f, 0.9f, 0.32f, 0.65f, -0.3f, 75f, 120);
            FxUtil.Sphere(fl, 0.16f);
            Noise(fl, 0.9f, 2.2f);
            FxUtil.FireGrad(fl);
            var rot = fl.main; rot.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            fl.Play();
            var emb = TailSys(tail, "brasas", Vector3.zero, Ember, true, 0.35f, 0.7f, 0.5f, 1.8f, 0.03f, 0.07f, -0.4f, 30f, 60);
            FxUtil.Sphere(emb, 0.2f);
            Noise(emb, 1.2f, 1.5f);
            Stretch(emb, 1.2f, 0.04f);
            Grad(emb, FireCore, Ember, 1f);
            emb.Play();
            var sm = TailSys(tail, "fumaca", new Vector3(0f, 0f, -0.15f), SmokeCol, false, 0.6f, 1f, 0.1f, 0.5f, 0.35f, 0.75f, -0.15f, 18f, 40);
            FxUtil.Sphere(sm, 0.1f);
            FxUtil.AlphaFade(sm, new Color(0.35f, 0.22f, 0.15f), new Color(0.1f, 0.1f, 0.1f), 0.5f);
            sm.Play();
            TailLight(tail, Fire, 3.2f, 5f, 0.4f);
        }

        /// <summary>Gelo: cristal facetado girando com estilhaços em volta, névoa fria, flocos caindo.</summary>
        static void FrostLook(ProjTail tail, Color col)
        {
            Transform t = tail.transform;
            var pivot = new GameObject("cristais").transform;
            pivot.SetParent(t, false);
            var iceMat = U.Lit(U.Hex("d8f4ff"), 0.95f, col * 1.6f);
            var c = Part(tail, pivot, PrimitiveType.Cube, Vector3.zero, new Vector3(0.18f, 0.18f, 0.32f), iceMat);
            c.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0f, 0f, i * 90f + 45f);
                var sh = Part(tail, pivot, PrimitiveType.Cube, rot * new Vector3(0f, 0.2f, -0.05f), new Vector3(0.05f, 0.2f, 0.05f), iceMat);
                sh.transform.localRotation = rot * Quaternion.Euler(-25f, 0f, 0f);
            }
            tail.spin = pivot; tail.spinAxis = Vector3.forward; tail.spinSpeed = 540f;
            Glow(tail, t, Vector3.zero, 0.85f, A(col, 0.7f));
            Glow(tail, t, Vector3.zero, 0.55f, A(Color.white, 0.6f), FxTex.Star(), -260f);
            Trail(tail, t, Vector3.zero, A(Color.white, 0.85f), A(Ice, 0f), 0.26f, 0.2f);
            var mist = TailSys(tail, "nevoa", Vector3.zero, A(Ice, 0.5f), false, 0.45f, 0.8f, 0.05f, 0.3f, 0.25f, 0.5f, -0.05f, 22f, 40);
            FxUtil.Sphere(mist, 0.12f);
            FxUtil.AlphaFade(mist, Color.white, Ice, 0.45f);
            mist.Play();
            var snow = TailSys(tail, "flocos", Vector3.zero, Color.white, true, 0.35f, 0.7f, 0.2f, 0.8f, 0.03f, 0.07f, 0.6f, 32f, 60);
            FxUtil.Sphere(snow, 0.2f);
            Grad(snow, Color.white, Ice, 1f);
            snow.Play();
            TailLight(tail, Ice, 2.2f, 4f);
        }

        /// <summary>Machado bumerangue: machado low-poly deitado girando, arco de corte nas pontas, sangue na versão furiosa.</summary>
        static void AxeLook(ProjTail tail, Color col)
        {
            Transform t = tail.transform;
            bool frenzy = col.g < 0.2f;
            var pivot = new GameObject("machado").transform;
            pivot.SetParent(t, false);
            var wood = U.Lit(WoodDark, 0.2f);
            var steel = U.Lit(SteelDark, 0.7f, col * 0.35f);
            var edge = U.Lit(Color.Lerp(col, Color.white, 0.4f), 0.9f, col * 2.6f);
            // cabo ao longo de X, lâmina deitada (plano XZ) na ponta +X: a câmera de cima vê a silhueta
            var handle = Part(tail, pivot, PrimitiveType.Cylinder, new Vector3(-0.05f, 0f, 0f), new Vector3(0.06f, 0.36f, 0.06f), wood);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var blade = Part(tail, pivot, PrimitiveType.Cube, new Vector3(0.3f, 0f, 0.1f), new Vector3(0.16f, 0.05f, 0.34f), steel);
            blade.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            var blade2 = Part(tail, pivot, PrimitiveType.Cube, new Vector3(0.3f, 0f, -0.08f), new Vector3(0.14f, 0.05f, 0.24f), steel);
            blade2.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
            var e1 = Part(tail, pivot, PrimitiveType.Cube, new Vector3(0.39f, 0f, 0.02f), new Vector3(0.035f, 0.055f, 0.5f), edge);
            e1.transform.localRotation = Quaternion.Euler(0f, -4f, 0f);
            Part(tail, pivot, PrimitiveType.Cube, new Vector3(-0.38f, 0f, 0f), new Vector3(0.07f, 0.07f, 0.07f), steel);
            var tip = new GameObject("ponta").transform; tip.SetParent(pivot, false); tip.localPosition = new Vector3(0.42f, 0f, 0f);
            Trail(tail, tip, Vector3.zero, A(Color.Lerp(col, Color.white, 0.35f), 0.85f), A(col, 0f), 0.2f, 0.14f);
            var butt = new GameObject("cabo").transform; butt.SetParent(pivot, false); butt.localPosition = new Vector3(-0.38f, 0f, 0f);
            Trail(tail, butt, Vector3.zero, A(col, 0.4f), A(col, 0f), 0.08f, 0.1f);
            Glow(tail, t, Vector3.zero, 1.3f, A(col, frenzy ? 0.45f : 0.3f));
            tail.spin = pivot; tail.spinAxis = Vector3.up; tail.spinSpeed = 1500f;
            if (frenzy)
            {
                var bl = TailSys(tail, "sangue", Vector3.zero, new Color(0.6f, 0.02f, 0.03f, 0.95f), false, 0.3f, 0.55f, 0.5f, 2f, 0.05f, 0.09f, 1.3f, 30f, 60);
                FxUtil.Sphere(bl, 0.35f);
                Stretch(bl, 1.3f, 0.05f);
                FxUtil.AlphaFade(bl, new Color(0.8f, 0.06f, 0.05f), new Color(0.35f, 0f, 0f), 0.95f);
                bl.Play();
                var aura = TailSys(tail, "furia", Vector3.zero, Blood, true, 0.2f, 0.4f, 0.2f, 0.8f, 0.15f, 0.3f, -0.3f, 30f, 50);
                FxUtil.Sphere(aura, 0.3f);
                Grad(aura, new Color(1f, 0.5f, 0.4f), new Color(0.6f, 0f, 0f), 0.8f);
                aura.Play();
            }
            else
            {
                var sp = TailSys(tail, "faiscas", Vector3.zero, Steel, true, 0.15f, 0.3f, 0.5f, 1.5f, 0.03f, 0.06f, 0.6f, 20f, 40);
                FxUtil.Sphere(sp, 0.35f);
                Grad(sp, Color.white, col, 1f);
                sp.Play();
            }
            TailLight(tail, col, 1.8f, 3.5f, frenzy ? 0.3f : 0f);
        }
    }
}
