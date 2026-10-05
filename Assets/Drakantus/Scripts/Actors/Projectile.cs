using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Projéteis do jogador e dos inimigos. Tipos: "arrow", "pierce", "orb", "fireball", "frost", "arcane" e
    /// [Classes] "bullet", "bullet_explosive", "longarrow" (atravessa 2), "heavy_arrow" (atravessa tudo), "holy",
    /// "homing_orb" (teleguiado), "axe_spin" (bumerangue que volta ao herói), "pin_arrow".
    /// Se s != null usa s.aoe (explosão), s.stun, s.pierce, s.color, s.bounces (ricochete) e s.splits (fragmenta).
    /// O visual pode vir do SkillFX.ProjectileVisual (agente EFEITOS); senão monta o padrão.
    /// </summary>
    public static class Projectile
    {
        /// <summary>Cria o projétil e devolve o componente (para ajustes: MakeCharged, hitsEnemies).</summary>
        public static ProjectileMover Spawn(string kind, Vector3 pos, Vector3 dir, int dmg, bool fromPlayer, SkillDef s)
        {
            if (string.IsNullOrEmpty(kind)) kind = "arrow";
            dir = U.Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            var go = new GameObject("proj_" + kind);
            go.transform.SetParent(FX.Root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
            var m = go.AddComponent<ProjectileMover>();
            m.Setup(kind, dir, dmg, fromPlayer, s);
            string snd = kind == "arrow" || kind == "pierce" || kind == "longarrow" || kind == "heavy_arrow" || kind == "pin_arrow" ? "bow"
                : kind == "fireball" ? "fireball" : kind == "frost" ? "ice"
                : kind == "bullet" || kind == "bullet_explosive" ? (Sfx.Has("pistol") ? "pistol" : "bow")
                : kind == "axe_spin" ? "swing_heavy" : "magic_cast";
            Sfx.Play(snd, pos, fromPlayer ? 0.9f : 0.7f);
            return m;
        }

        /// <summary>true se o collider pertence a um personagem ou quebrável (não conta como parede).</summary>
        public static bool IsActor(Collider c)
        {
            return c.GetComponentInParent<Enemy>() != null || c.GetComponentInParent<Player>() != null || c.GetComponentInParent<ProjectileMover>() != null
                || c.GetComponentInParent<Breakable>() != null;
        }

        /// <summary>Raycast que ignora triggers e personagens; devolve a parede mais próxima.</summary>
        public static bool WallHit(Vector3 from, Vector3 dir, float dist, out RaycastHit hit)
        {
            hit = default;
            if (dist <= 0f) return false;
            var hits = Physics.RaycastAll(from, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider == null || IsActor(h.collider)) continue;
                if (h.distance < best) { best = h.distance; hit = h; found = true; }
            }
            return found;
        }

        /// <summary>true se há parede entre a e b.</summary>
        public static bool WallBetween(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return false;
            return WallHit(a, d / len, len, out _);
        }
    }

    /// <summary>Componente que move o projétil, detecta acertos e explode.</summary>
    public class ProjectileMover : MonoBehaviour
    {
        public string kind;
        public bool fromPlayer;
        public int dmg;
        public SkillDef skill;
        /// <summary>Projétil de armadilha: também acerta inimigos (sem crédito para o herói).</summary>
        public bool hitsEnemies;
        /// <summary>Ataque carregado: hitstop maior ao acertar.</summary>
        public bool charged;
        /// <summary>[Classes] Herói que disparou (crédito, roubo de vida, venenos). null = jogador local.</summary>
        public Player owner;
        /// <summary>[Classes] Inimigos que ainda pode atravessar (-1 = todos), ricochetes e fragmentos restantes.</summary>
        public int pierceLeft = -1, bouncesLeft, splitsLeft;
        bool homing, boomerang, returning;
        float age;

        Vector3 dir;
        float speed, life, aoe, stun;
        bool pierce, done, isArrow;
        Color color;
        readonly List<Enemy> hitList = new();
        readonly List<Breakable> breakHits = new();
        TrailRenderer trail;
        ParticleSystem sparks;
        Transform halo;

        public void Setup(string kind, Vector3 dir, int dmg, bool fromPlayer, SkillDef s)
        {
            this.kind = kind; this.dir = dir; this.dmg = Mathf.Max(1, dmg); this.fromPlayer = fromPlayer; skill = s;
            aoe = s != null ? s.aoe : 0f;
            stun = s != null ? s.stun : 0f;
            pierce = (s != null && s.pierce) || kind == "pierce" || kind == "heavy_arrow" || kind == "longarrow" || kind == "axe_spin";
            pierceLeft = kind == "longarrow" && !(s != null && s.pierce) ? 2 : -1;
            bouncesLeft = s != null ? s.bounces : 0;
            splitsLeft = s != null ? s.splits : 0;
            homing = kind == "homing_orb";
            boomerang = kind == "axe_spin";
            isArrow = kind == "arrow" || kind == "pierce" || kind == "longarrow" || kind == "heavy_arrow" || kind == "pin_arrow";
            Color sc = s != null && !string.IsNullOrEmpty(s.color) ? U.Hex(s.color) : Color.white;
            bool hasSc = s != null && !string.IsNullOrEmpty(s.color);
            switch (kind)
            {
                case "arrow": speed = fromPlayer ? 24f : 15f; color = fromPlayer ? U.Hex("fff2d0") : U.Hex("ff8a6a"); break;
                case "pierce": speed = 26f; color = U.Hex("ffe08a"); break;
                case "fireball": speed = 18f; color = U.Hex("ff7a2a"); break;
                case "frost": speed = 21f; color = U.Hex("9fe4ff"); break;
                case "arcane": speed = 19f; color = s != null && !string.IsNullOrEmpty(s.color) ? U.Hex(s.color) : U.Hex("b98aff"); break;
                case "bullet": speed = 34f; color = U.Hex("ffd08a"); break;
                case "bullet_explosive": speed = 28f; color = U.Hex("ff8a3a"); break;
                case "longarrow": speed = 30f; color = U.Hex("e8ffd8"); break;
                case "heavy_arrow": speed = 26f; color = U.Hex("ffe08a"); break;
                case "pin_arrow": speed = 28f; color = U.Hex("9b6bff"); break;
                case "holy": speed = 22f; color = U.Hex("fff2b0"); break;
                case "homing_orb": speed = 14f; color = hasSc ? sc : U.Hex("c070ff"); break;
                case "axe_spin": speed = 18f; color = hasSc ? sc : U.Hex("ff6a4a"); break;
                default: // "orb"
                    speed = fromPlayer ? 20f : 10f;
                    color = fromPlayer ? (s != null && !string.IsNullOrEmpty(s.color) ? U.Hex(s.color) : U.Hex("b98aff")) : U.Hex("ff5adc");
                    break;
            }
            life = fromPlayer ? 0.9f : Mathf.Clamp(13f / speed, 0.8f, 1.6f);
            if (fromPlayer)
            {
                if (kind == "bullet" || kind == "bullet_explosive") life = 0.6f;
                else if (kind == "longarrow" || kind == "heavy_arrow") life = 0.85f;
                else if (homing) life = 1.8f;
                else if (boomerang) life = 3f;
            }
            BuildVisual();
        }

        /// <summary>Ataque carregado: projétil maior, alcance um pouco maior e (aoe &gt; 0) explosão em área.</summary>
        public void MakeCharged(float area, float scale)
        {
            charged = true;
            if (area > 0f) aoe = Mathf.Max(aoe, area);
            scale = Mathf.Max(1f, scale);
            transform.localScale = Vector3.one * scale;
            if (trail != null) trail.widthMultiplier *= scale;
            life *= 1.25f;
        }

        void BuildVisual()
        {
            // [Classes] visual próprio do agente EFEITOS
            if (SkillFX.ProjectileVisual(kind, transform, color))
            {
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
                return;
            }
            if (isArrow)
            {
                var shaft = U.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0f, -0.1f), new Vector3(0.04f, 0.35f, 0.04f), U.Hex("8a6a42"));
                shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var tip = U.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 0f, 0.28f), new Vector3(0.07f, 0.07f, 0.14f), color);
                tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                tip.GetComponent<Renderer>().sharedMaterial = U.Lit(color, 0.6f, color * 2f);
                U.Prim(PrimitiveType.Cube, transform, new Vector3(0f, 0f, -0.42f), new Vector3(0.12f, 0.02f, 0.12f), Color.white);
                // flechas do jogador: rastro de vento verde/ciano; perfurante: dourado com espiral
                AddLight(kind == "pierce" ? 2.2f : fromPlayer ? 0.9f : 1.2f, kind == "pierce" ? 3.5f : 2.5f);
                if (kind == "pierce") BuildSparks(0.25f, 50f, U.Hex("ffe08a"), true);
            }
            else
            {
                float sz = kind == "fireball" ? 0.45f : kind == "bullet" || kind == "bullet_explosive" ? 0.16f : kind == "axe_spin" ? 0.5f : fromPlayer ? 0.32f : 0.4f;
                var core = U.Prim(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * sz, color);
                core.GetComponent<Renderer>().sharedMaterial = U.Lit(color, 0.5f, color * 3f);
                if (kind == "frost")
                {
                    // núcleo de gelo: cristais girando em volta
                    core.transform.localScale = Vector3.one * sz * 0.7f;
                    for (int i = 0; i < 4; i++)
                    {
                        var shard = U.Prim(PrimitiveType.Cube, transform, Quaternion.Euler(0f, 0f, i * 90f) * new Vector3(0f, sz * 0.45f, 0f), new Vector3(0.06f, 0.22f, 0.06f), color);
                        shard.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                        shard.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("d8f4ff"), 0.9f, color * 2f);
                        shard.name = "cristal";
                    }
                }
                // halo (billboard)
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "halo";
                q.transform.SetParent(transform, false);
                q.transform.localScale = Vector3.one * sz * 3.2f;
                var qr = q.GetComponent<Renderer>();
                qr.sharedMaterial = U.Fx(true);
                qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                FX.SetColor(qr, new Color(color.r, color.g, color.b, 0.8f));
                halo = q.transform;
                var lt = AddLight(kind == "fireball" ? 3.2f : 2.5f, kind == "fireball" ? 5f : 4f);
                if (kind == "fireball" && lt != null) { var fl = lt.gameObject.AddComponent<FxFlicker>(); fl.baseIntensity = 3.2f; fl.amount = 0.35f; }
                BuildSparks(sz, 70f, color, false);
                if (kind == "fireball") BuildSmoke(sz);
            }
            // as formas simples ainda têm collider até o fim do frame: desliga para não bloquear raycasts
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            // rastro
            Color tc = color;
            if (fromPlayer && kind == "arrow") tc = U.Hex("b8ffcf");
            if (kind == "frost") tc = U.Hex("bfeaff");
            var tg = new GameObject("rastro");
            tg.transform.SetParent(transform, false);
            trail = tg.AddComponent<TrailRenderer>();
            trail.sharedMaterial = U.Fx(true);
            trail.time = isArrow ? (kind == "pierce" ? 0.22f : 0.14f) : 0.25f;
            trail.widthMultiplier = isArrow ? (kind == "pierce" ? 0.14f : 0.08f) : (kind == "fireball" ? 0.42f : 0.3f);
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.startColor = new Color(tc.r, tc.g, tc.b, 0.85f);
            trail.endColor = new Color(tc.r, tc.g, tc.b, 0f);
            trail.minVertexDistance = 0.1f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        Light AddLight(float intensity, float range)
        {
            var lg = new GameObject("luz");
            lg.transform.SetParent(transform, false);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        ParticleSystem smoke;
        void BuildSmoke(float sz)
        {
            var go = new GameObject("fumaca");
            go.transform.SetParent(transform, false);
            smoke = go.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(sz * 0.8f, sz * 1.6f);
            main.startColor = new Color(0.15f, 0.12f, 0.1f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.1f;
            main.maxParticles = 100;
            var em = smoke.emission; em.rateOverTime = 30f;
            var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = sz * 0.3f;
            var sol = smoke.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            var col = smoke.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.3f, 0.2f, 0.15f), 0f), new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1f) },
                      new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = U.Fx(false);
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smoke.Play();
        }

        void BuildSparks(float sz, float rate, Color c, bool spiral)
        {
            var go = new GameObject("faiscas");
            go.transform.SetParent(transform, false);
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = c;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            if (kind == "fireball") main.gravityModifier = -0.3f;      // brasas sobem
            if (kind == "frost") main.gravityModifier = 0.6f;          // cristais caem
            var em = sparks.emission;
            em.rateOverTime = rate;
            var sh = sparks.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = sz * 0.4f;
            if (spiral)
            {
                // espiral de vento em volta da flecha perfurante (círculo no plano XY local = perpendicular ao voo)
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 0.18f;
                sh.radiusThickness = 0f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            }
            var sol = sparks.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = U.Fx(true);
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sparks.Play();
        }

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            age += dt;
            Vector3 prev = transform.position;
            if (homing) Steer(dt);
            if (boomerang) { if (UpdateBoomerang(dt)) return; }
            float step = speed * dt;
            if (!returning && Projectile.WallHit(prev, dir, step + 0.15f, out RaycastHit wh))
            {
                if (boomerang) { StartReturn(); return; }
                transform.position = wh.point - dir * 0.1f;
                if (aoe > 0f && fromPlayer) Explode(transform.position, null);
                else Impact();
                return;
            }
            Vector3 next = prev + dir * step;
            transform.position = next;
            if (fromPlayer || hitsEnemies) CheckEnemies(prev, next);
            if (done) return;
            if (!fromPlayer) CheckPlayer(prev, next);
            if (done) return;
            CheckBreakables(prev, next);
            if (done) return;
            life -= dt;
            if (boomerang && life > 0f) return;
            if (life <= 0f)
            {
                if (aoe > 0f && fromPlayer) Explode(next, null);
                else Finish();
            }
        }

        void LateUpdate()
        {
            if (halo != null && CameraRig.Cam != null) halo.rotation = CameraRig.Cam.transform.rotation;
            if (kind == "frost" && !done)
                foreach (Transform c in transform)
                    if (c.name == "cristal") c.RotateAround(transform.position, transform.forward, 540f * Time.deltaTime);
            if (kind == "pierce" && sparks != null && !done)
                sparks.transform.Rotate(0f, 0f, 900f * Time.deltaTime, Space.Self);   // gira o círculo de emissão: espiral
        }

        static float SegDist(Vector3 a, Vector3 b, Vector3 p)
        {
            a.y = 0f; b.y = 0f; p.y = 0f;
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
            return Vector3.Distance(a + ab * t, p);
        }

        void CheckEnemies(Vector3 prev, Vector3 next)
        {
            var g = Game.I;
            if (g == null) return;
            foreach (var e in g.enemies.ToArray())
            {
                if (e == null || e.dead || hitList.Contains(e)) continue;
                float ey = e.transform.position.y;
                if (next.y < ey - 0.5f || next.y > ey + 2.6f * Mathf.Max(1f, e.scale)) continue;
                if (SegDist(prev, next, e.transform.position) > 0.6f + e.radius) continue;
                HitEnemy(e);
                if (done) return;
            }
        }

        void HitEnemy(Enemy e)
        {
            hitList.Add(e);
            var own = Owner;
            bool crit = Random.value < 0.12f + (fromPlayer && own != null ? own.KitCrit() : 0f);
            int d = crit ? dmg * 2 : dmg;
            e.TakeHit(d, transform.position - dir * 1.5f, crit);
            Credit(e, d);
            if (fromPlayer && own != null && !hitsEnemiesOnly) own.KitOnHit(skill, e, d, skill == null);   // [Classes] roubo de vida, veneno, raiz...
            if (splitsLeft > 0) Split(e);
            if (crit) FX.FlashLight(transform.position, Color.Lerp(color, Color.white, 0.6f), 4f, 4f, 0.12f);
            // partículas de impacto na cor do elemento do projétil + hitstop (carregado/crítico)
            SkillFX.Sparks(e.transform.position + Vector3.up, color, crit ? 12 : 7, 2f, 5f, 0.8f);
            if (fromPlayer && Game.I != null)
            {
                if (charged) Game.I.Hitstop(crit ? 0.14f : 0.11f);
                else if (crit) Game.I.Hitstop(0.05f);
            }
            if (aoe > 0f) { Explode(transform.position, e); return; }
            if (stun > 0f && !e.dead) e.Stun(stun);
            SkillFX.ProjectileImpact(kind, transform.position, color, 0f, false);
            if (bouncesLeft > 0 && Ricochet(e)) return;
            if (pierce && pierceLeft != 0)
            {
                if (pierceLeft > 0) pierceLeft--;
                if (!boomerang) dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.85f));
                return;
            }
            Finish();
        }

        bool hitsEnemiesOnly => hitsEnemies && !fromPlayer;
        Player Owner => owner != null ? owner : (Game.I != null ? Game.I.player : null);

        /// <summary>Não acerta este inimigo (fragmentos nascem dentro do alvo original).</summary>
        public void Ignore(Enemy e) { if (e != null && !hitList.Contains(e)) hitList.Add(e); }

        // ------------------------------------------------------------------ [Classes] comportamentos novos
        void Steer(float dt)
        {
            var g = Game.I;
            if (g == null || age < 0.12f) return;
            Enemy best = null; float bd = 12f;
            Vector3 p = transform.position;
            foreach (var e in g.enemies)
            {
                if (e == null || e.dead || hitList.Contains(e)) continue;
                Vector3 to = U.Flat(e.transform.position - p);
                float d = to.magnitude;
                if (d > bd || (d > 1f && Vector3.Dot(to / d, dir) < -0.2f)) continue;
                bd = d; best = e;
            }
            if (best == null) return;
            Vector3 want = U.Flat(best.transform.position - p).normalized;
            dir = Vector3.RotateTowards(dir, want, 7f * dt, 0f);
            dir = U.Flat(dir).normalized;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            speed = Mathf.Min(22f, speed + 10f * dt);
        }

        /// <summary>Machado bumerangue: vai ~0,45 s e volta para quem lançou. true = terminou.</summary>
        bool UpdateBoomerang(float dt)
        {
            if (!returning && age >= 0.45f) StartReturn();
            if (!returning) return false;
            var own = Owner;
            if (own == null) { Finish(); return true; }
            Vector3 to = U.Flat(own.transform.position - transform.position);
            if (to.magnitude < 1.1f || age > 3f) { Finish(); return true; }
            dir = to.normalized;
            speed = Mathf.Min(26f, speed + 30f * dt);
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            return false;
        }

        void StartReturn()
        {
            if (returning) return;
            returning = true;
            hitList.Clear();   // acerta de novo na volta
            pierceLeft = -1;
        }

        bool Ricochet(Enemy from)
        {
            var g = Game.I;
            if (g == null) return false;
            Enemy best = null; float bd = 8f;
            foreach (var e in g.enemies)
            {
                if (e == null || e.dead || hitList.Contains(e)) continue;
                float d = U.Flat(e.transform.position - from.transform.position).magnitude;
                if (d < bd) { bd = d; best = e; }
            }
            if (best == null) return false;
            bouncesLeft--;
            Vector3 to = U.Flat(best.transform.position - transform.position);
            if (to.sqrMagnitude < 0.0001f) return false;
            dir = to.normalized;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            life = 0.9f;
            SkillFX.ProjectileImpact(kind, transform.position, color, 0f, false);
            Sfx.Play(Sfx.Has("ricochet") ? "ricochet" : "block", transform.position, 0.4f, 0.2f);
            return true;
        }

        void Split(Enemy hit)
        {
            int n = splitsLeft;
            splitsLeft = 0;
            Vector3 p = transform.position;
            int child = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.45f));
            for (int k = 0; k < n; k++)
            {
                float a = (k - (n - 1) * 0.5f) * (70f / Mathf.Max(1, n - 1));
                Vector3 d = Quaternion.Euler(0f, a, 0f) * dir;
                var m = Projectile.Spawn("arrow", p + d * 0.3f, d, child, true, skill);
                if (m == null) continue;
                m.owner = owner;
                m.splitsLeft = 0;
                m.bouncesLeft = 0;
                m.Ignore(hit);
            }
        }

        /// <summary>[Quebraveis] Barris/caixas/vasos no caminho levam o dano do projétil.</summary>
        void CheckBreakables(Vector3 prev, Vector3 next)
        {
            if (Breakable.All.Count == 0) return;
            var b = Breakable.OnSegment(prev, next, isArrow ? 0.25f : 0.35f);
            if (b == null || breakHits.Contains(b)) return;
            breakHits.Add(b);
            b.Hit(dmg, transform.position - dir);
            if (aoe > 0f && fromPlayer) { Explode(transform.position, null); return; }
            if (pierce) return;
            Impact();
        }

        void CheckPlayer(Vector3 prev, Vector3 next)
        {
            var g = Game.I;
            if (g == null || g.player == null) return;
            var p = g.player;
            if (p.state == "dead") return;
            float py = p.transform.position.y;
            if (next.y < py - 0.5f || next.y > py + 2.4f) return;
            if (SegDist(prev, next, p.transform.position) > 0.85f) return;
            Vector3 pos = transform.position;
            Player.IncomingRanged = true;   // [Classes] Espelho de Aço devolve 50% do dano à distância
            string r;
            try { r = p.TakeDamage(dmg, pos - dir * 2f); }
            finally { Player.IncomingRanged = false; }
            switch (r)
            {
                case "ignored":
                    return; // esquiva: o projétil passa
                case "parried":
                    Sfx.Play("parry", pos);
                    Reflect();
                    return;
                case "blocked":
                    Sfx.Play("block", pos);
                    FX.Burst(pos, Color.white, 0.5f, 10);
                    Finish();
                    return;
                default:
                    Sfx.Play("player_hurt", pos);
                    FX.Burst(pos, color, 0.6f, 14);
                    Finish();
                    return;
            }
        }

        /// <summary>Parry devolve o projétil contra os inimigos com dano 2x (mira no alvo travado, se houver).</summary>
        void Reflect()
        {
            fromPlayer = true;
            hitsEnemies = true;
            dir = -dir;
            var g = Game.I;
            var lo = g != null && g.player != null ? g.player.lockOn : null;
            if (lo != null && lo.Target != null && !lo.Target.dead)
            {
                Vector3 to = U.Flat(lo.Target.transform.position - transform.position);
                if (to.sqrMagnitude > 0.01f) dir = to.normalized;
            }
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            speed *= 1.3f;
            life = 0.9f;
            dmg *= 2;
            hitList.Clear();
            breakHits.Clear();
            FX.Burst(transform.position, U.Hex("b9a6ff"), 0.7f, 16);
            HUD.Popup(transform.position + Vector3.up * 0.8f, "REFLETIDO x2", U.Hex("b9a6ff"));
        }

        void Explode(Vector3 c, Enemy direct)
        {
            SkillFX.ProjectileImpact(kind, c, color, aoe, true);
            Sfx.Play(kind == "frost" ? "ice" : "explosion", c);
            var g = Game.I;
            if (g != null) g.Shake(0.22f);
            if (direct != null && !direct.dead && stun > 0f) direct.Stun(stun);
            int splash = Mathf.Max(1, Mathf.RoundToInt(dmg * 2f / 3f));
            if (g != null)
            {
                foreach (var e in g.enemies.ToArray())
                {
                    if (e == null || e.dead || e == direct) continue;
                    if (U.Flat(e.transform.position - c).magnitude > aoe + e.radius) continue;
                    e.TakeHit(splash, c, false);
                    Credit(e, splash);
                    if (stun > 0f && !e.dead) e.Stun(stun);
                }
                if (charged) g.Hitstop(0.1f);
            }
            if (fromPlayer) Breakable.HitArea(c, aoe, splash);   // [Quebraveis]
            Finish();
        }

        void Impact()
        {
            SkillFX.ProjectileImpact(kind, transform.position, color, 0f, false);
            Finish();
        }

        /// <summary>Credita o acerto na carga da ultimate do herói (ataque básico se não veio de habilidade).</summary>
        void Credit(Enemy e, int dealt)
        {
            if (!fromPlayer || e == null) return;
            var own = Owner;
            if (own == null) return;
            own.RegisterHit(e, e.dead, skill == null, dealt);
        }

        void Finish()
        {
            if (done) return;
            done = true;
            if (trail != null)
            {
                trail.transform.SetParent(FX.Root, true);
                trail.emitting = false;
                Destroy(trail.gameObject, trail.time + 0.1f);
            }
            if (sparks != null)
            {
                sparks.transform.SetParent(FX.Root, true);
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(sparks.gameObject, 0.6f);
            }
            if (smoke != null)
            {
                smoke.transform.SetParent(FX.Root, true);
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(smoke.gameObject, 0.8f);
            }
            Destroy(gameObject);
        }
    }
}
