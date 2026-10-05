using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Ultimate de cada classe (tecla R). Carga em ultCharge (0..1):
    /// +3,5% por acerto de ataque básico, +2% por acerto de habilidade (máx. 3 inimigos por uso),
    /// +8% por abate, +5% ao receber dano. Não carrega enquanto uma ultimate está ativa.
    ///   Guerreiro "Fúria do Berserker" · Arqueiro "Chuva de Mil Flechas"
    ///   Mago "Meteoro Arcano"          · Tank "Bastião Divino"
    /// A HUD lê ultCharge / UltReady / UltName() / UltDesc() e chama UseUltimate().
    /// </summary>
    public partial class Player
    {
        public const float UltPerBasicHit = 0.035f, UltPerSkillHit = 0.02f, UltPerKill = 0.08f, UltPerDamage = 0.05f;
        public const int UltSkillHitsPerCast = 3;

        public float ultCharge;
        public bool UltReady => ultCharge >= 1f;
        public bool UltActive => ultActive;

        bool ultActive;
        int ultSkillHits;
        GameObject ultReadyFx;

        // Guerreiro
        float berserkTime;
        float berserkScale = 1f;
        Transform scaledModel;
        Vector3 scaledBase = Vector3.one;
        GameObject berserkAura, berserkAura2;
        public bool Berserk => berserkTime > 0f;

        // Tank
        float bastionTime, bastionTick;
        Vector3 bastionCenter;
        const float BastionRadius = 6f;

        // ------------------------------------------------------------------ textos
        public string UltName()
        {
            switch (BaseClassId)
            {
                case "guerreiro": return "Fúria do Berserker";
                case "arqueiro": return "Chuva de Mil Flechas";
                case "mago": return "Meteoro Arcano";
                case "tank": return "Bastião Divino";
                default: return "Ultimate";
            }
        }

        public string UltDesc()
        {
            switch (BaseClassId)
            {
                case "guerreiro": return "Ruge com uma onda de choque (dano 4x em 5 m, arremessa os inimigos). Por 8s você cresce, pega fogo e seus ataques causam +60% de dano em área.";
                case "arqueiro": return "Uma tempestade de flechas cobre 9 m no cursor por 3s, causando dano contínuo e retardando os inimigos. Termina com uma flecha gigante que explode.";
                case "mago": return "O céu escurece e um meteoro colossal cai no cursor: dano 8x em 7 m e o chão fica em chamas por 4s.";
                case "tank": return "Cúpula sagrada de 6 m por 6s: puxa e atordoa inimigos por 2s. Dentro dela você recebe 70% menos dano e regenera vida.";
                default: return "";
            }
        }

        Color UltColor()
        {
            switch (BaseClassId)
            {
                case "guerreiro": return SkillFX.Fire;
                case "arqueiro": return SkillFX.Leaf;
                case "mago": return SkillFX.Arcane;
                case "tank": return SkillFX.Gold;
                default: return U.Hex(GameState.Class.color);
            }
        }

        // ------------------------------------------------------------------ carga
        /// <summary>Chamado a cada acerto do herói (melee, habilidades e projéteis).</summary>
        public void RegisterHit(Enemy e, bool killed, bool basic) => RegisterHit(e, killed, basic, 0);

        /// <summary>Acerto do herói com o dano causado (conta no combo das passivas e no roubo de vida).</summary>
        public void RegisterHit(Enemy e, bool killed, bool basic, int dealt)
        {
            if (basic) AddUltCharge(UltPerBasicHit);
            else if (ultSkillHits < UltSkillHitsPerCast) { ultSkillHits++; AddUltCharge(UltPerSkillHit); }
            if (killed) AddUltCharge(UltPerKill);
            ComboHit(dealt);
        }

        public void AddUltCharge(float amount)
        {
            if (ultActive || state == "dead" || amount <= 0f) return;
            bool was = UltReady;
            ultCharge = Mathf.Clamp01(ultCharge + amount);
            if (!was && UltReady) OnUltReady();
        }

        void OnUltReady()
        {
            Sfx.Play("ult_ready");
            GameState.Notify("ULTIMATE PRONTA! [R]");
            Color c = UltColor();
            if (ultReadyFx != null) Destroy(ultReadyFx);
            ultReadyFx = SkillFX.UltReadyAura(transform, c);
            FX.Ring(transform.position, 2f, c, 0.5f, 0.3f);
            FX.FlashLight(transform.position + Vector3.up * 1.2f, c, 4f, 6f, 0.5f);
        }

        /// <summary>Multiplicador de dano recebido vindo das ultimates (Bastião: -70% dentro da cúpula).</summary>
        float UltDamageMult()
        {
            if (bastionTime > 0f && U.Flat(transform.position - bastionCenter).magnitude <= BastionRadius + 0.3f) return 0.3f;
            return 1f;
        }

        // ------------------------------------------------------------------ uso
        public void UseUltimate()
        {
            if (!canFight || inputLocked || state == "dead") return;
            if (Time.timeScale < 0.05f) return;   // pausado
            if (ultActive) return;
            if (!UltReady)
            {
                GameState.Notify("Ultimate carregando: " + Mathf.FloorToInt(ultCharge * 100f) + "%");
                return;
            }
            if (state == "leap" || state == "charge" || state == "dash") return;
            StartCoroutine(UltRoutine());
        }

        IEnumerator UltRoutine()
        {
            ultActive = true;
            ultCharge = 0f;
            if (ultReadyFx != null) { Destroy(ultReadyFx); ultReadyFx = null; }
            string cls = BaseClassId;   // [Classes] evoluídas usam a ultimate da classe base
            Color c = UltColor();
            Vector3 pos = transform.position;
            Vector3 aim = AimDir();
            facing = aim;
            transform.rotation = Quaternion.LookRotation(aim);
            Vector3 target = ClampToWalls(pos, AimPoint(cls == "arqueiro" ? 14f : 12f));

            string anim = cls == "arqueiro" ? "Shoot" : cls == "mago" ? "Summon" : cls == "tank" ? "Block" : "Cheer";
            CastAnim(anim, 0.6f);
            invuln = Mathf.Max(invuln, 0.7f);
            Sfx.Play("ult_" + cls, pos);
            HUD.Popup(pos + Vector3.up * 2.9f, UltName().ToUpperInvariant() + "!", c, true);
            SkillFX.UltCinematic(pos, c);
            if (Game.I != null) Game.I.Shake(0.55f);
            if (CameraRig.I != null) CameraRig.I.Punch(0.6f);

            // hitstop cinemático: 0,25 s reais em câmera lenta
            yield return StartCoroutine(UltHitstop());

            switch (cls)
            {
                case "guerreiro": yield return StartCoroutine(UltBerserker()); break;
                case "arqueiro": yield return StartCoroutine(UltArrowStorm(target, aim)); break;
                case "mago": yield return StartCoroutine(UltMeteor(target)); break;
                case "tank": yield return StartCoroutine(UltBastion()); break;
                default: break;
            }
            ultActive = false;
            ultSkillHits = 0;
        }

        IEnumerator UltHitstop()
        {
            if (Time.timeScale < 0.5f) yield break;    // já está em hitstop/pausa
            Time.timeScale = 0.1f;
            yield return new WaitForSecondsRealtime(0.25f);
            if (Mathf.Approximately(Time.timeScale, 0.1f)) Time.timeScale = 1f;
        }

        /// <summary>Dano de ultimate (não carrega a própria ultimate).</summary>
        List<Enemy> UltArea(Vector3 center, float radius, int dmg, float stun, bool crit)
        {
            var hit = new List<Enemy>();
            if (Game.I == null) return hit;
            foreach (var e in Game.I.enemies.ToArray())
            {
                if (e == null || e.dead) continue;
                if (U.Flat(e.transform.position - center).magnitude > radius + e.radius) continue;
                e.TakeHit(dmg, center, crit);
                ComboHit(dmg);
                if (stun > 0f && !e.dead) e.Stun(stun);
                hit.Add(e);
            }
            Breakable.HitArea(center, radius, dmg);   // [Quebraveis]
            return hit;
        }

        // ------------------------------------------------------------------ Guerreiro: Fúria do Berserker
        IEnumerator UltBerserker()
        {
            Vector3 p = transform.position;
            SkillFX.UltRoar(p, 5f);
            Sfx.Play("explosion", p);
            var hit = UltArea(p, 5f, Dmg(null, 4f), 0f, true);
            foreach (var e in hit)
                if (e != null && !e.dead) StartCoroutine(PushEnemy(e, U.Flat(e.transform.position - p), 3.5f, 0.3f));
            if (Game.I != null) { Game.I.Shake(0.8f); if (hit.Count > 0) Game.I.Hitstop(0.08f); }

            berserkTime = 8f;
            berserkAura = SkillFX.AttachAura(transform, SkillFX.Fire, 8f, 70f, 0.5f, true);
            berserkAura2 = SkillFX.AttachAura(transform, SkillFX.FireDeep, 8f, 35f, 0.9f, false);
            while (berserkTime > 0f) yield return null;
            if (berserkAura != null) { var a = berserkAura.GetComponent<FxAutoEnd>(); if (a != null) a.EndNow(); }
            if (berserkAura2 != null) { var a = berserkAura2.GetComponent<FxAutoEnd>(); if (a != null) a.EndNow(); }
            FX.Ring(transform.position, 1.8f, SkillFX.Ember, 0.4f, 0.8f);
        }

        // ------------------------------------------------------------------ Arqueiro: Chuva de Mil Flechas
        IEnumerator UltArrowStorm(Vector3 t, Vector3 aim)
        {
            const float R = 9f, Dur = 3f, Tick = 0.25f;
            var mark = FX.Marker(t, R, SkillFX.Leaf, Dur);
            SkillFX.RuneCircle(t, R, SkillFX.Leaf, Dur + 0.4f, 15f);
            SkillFX.ArrowStorm(t, R, Dur);
            int dmg = Dmg(null, 0.6f);
            int n = 0;
            for (float el = 0f; el < Dur; el += Tick, n++)
            {
                if (Game.I != null)
                {
                    foreach (var e in Game.I.enemies.ToArray())
                    {
                        if (e == null || e.dead) continue;
                        if (U.Flat(e.transform.position - t).magnitude > R + e.radius) continue;
                        // "de frente": o empurrão do golpe vai contra o movimento do inimigo → ficam lentos
                        e.TakeHit(dmg, e.transform.position + e.transform.forward * 0.6f, false);
                        ComboHit(dmg);
                        if (!e.dead && n % 2 == 0) e.Stun(0.2f);
                        // TODO(contrato): Enemy não tem Slow(); usamos micro-atordoamentos + empurrão contra o movimento
                    }
                }
                if (n % 2 == 0) { Sfx.Play("bow", t + Random.insideUnitSphere * 3f, 0.7f, 0.2f); Breakable.HitArea(t, R, dmg); }
                if (Game.I != null) Game.I.Shake(0.06f);
                yield return new WaitForSeconds(Tick);
            }
            if (mark != null) Destroy(mark);
            // flecha gigante final
            Vector3 from = t - aim * 7f + Vector3.up * 16f;
            const float fall = 0.4f;
            SkillFX.SpawnGiantArrow(from, t + Vector3.up * 0.3f, fall);
            yield return new WaitForSeconds(fall);
            SkillFX.GiantArrowImpact(t, 4.5f);
            Sfx.Play("explosion", t);
            var hit = UltArea(t, 4.5f, Dmg(null, 5f), 1f, true);
            if (Game.I != null) { Game.I.Shake(0.7f); if (hit.Count > 0) Game.I.Hitstop(0.08f); }
            if (CameraRig.I != null) CameraRig.I.Punch(0.3f);
        }

        // ------------------------------------------------------------------ Mago: Meteoro Arcano
        IEnumerator UltMeteor(Vector3 t)
        {
            const float R = 7f, Delay = 1.2f, Burn = 4f;
            // escurece o céu (roda no Game para restaurar mesmo se este objeto sumir)
            MonoBehaviour host = Game.I != null ? (MonoBehaviour)Game.I : this;
            host.StartCoroutine(DimSky(0.3f, Delay + 1.6f, Game.I != null ? Game.I.mapId : ""));
            var mark = FX.Marker(t, R, SkillFX.Fire, Delay);
            SkillFX.RuneCircle(t, R * 1.05f, SkillFX.ArcaneDeep, Delay + 0.6f, 40f);
            SkillFX.Implode(t + Vector3.up * 1f, R, SkillFX.Arcane, Delay, 60);
            SkillFX.SpawnMeteor(t + new Vector3(-7f, 24f, -7f), t + Vector3.up * 0.6f, Delay, 2.6f);
            Sfx.Play("fireball", t);
            yield return new WaitForSeconds(Delay);
            if (mark != null) Destroy(mark);
            SkillFX.MeteorImpact(t, 4.5f, 2.2f);
            FX.FlashLight(t + Vector3.up * 3f, SkillFX.FireCore, 14f, 22f, 1.2f);
            Sfx.Play("explosion", t);
            var hit = UltArea(t, R, Dmg(null, 8f), 1.2f, true);
            if (Game.I != null) { Game.I.Shake(1f); if (hit.Count > 0) Game.I.Hitstop(0.1f); }
            if (CameraRig.I != null) CameraRig.I.Punch(0.4f);
            // chão em chamas
            SkillFX.FireField(t, R * 0.85f, Burn);
            int burn = Dmg(null, 0.5f);
            for (float el = 0f; el < Burn; el += 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                UltArea(t, R * 0.85f, burn, 0f, false);
            }
        }

        /// <summary>Escurece luz ambiente e o sol, segura, e volta (não restaura se o mapa mudou).</summary>
        static IEnumerator DimSky(float factor, float hold, string mapId)
        {
            Color amb0 = RenderSettings.ambientLight;
            Light sun = RenderSettings.sun;
            float sun0 = sun != null ? sun.intensity : 0f;
            const float fadeIn = 0.35f, fadeOut = 0.9f;
            float t = 0f;
            while (t < fadeIn)
            {
                if (MapChanged(mapId)) yield break;
                t += Time.unscaledDeltaTime;
                ApplySky(amb0, sun, sun0, Mathf.Lerp(1f, factor, t / fadeIn));
                yield return null;
            }
            float h = 0f;
            while (h < hold)
            {
                if (MapChanged(mapId)) yield break;
                h += Time.deltaTime;
                yield return null;
            }
            t = 0f;
            while (t < fadeOut)
            {
                if (MapChanged(mapId)) yield break;
                t += Time.unscaledDeltaTime;
                ApplySky(amb0, sun, sun0, Mathf.Lerp(factor, 1f, t / fadeOut));
                yield return null;
            }
            if (!MapChanged(mapId)) ApplySky(amb0, sun, sun0, 1f);
        }

        static bool MapChanged(string mapId) => Game.I != null && Game.I.mapId != mapId;

        static void ApplySky(Color amb0, Light sun, float sun0, float k)
        {
            k = Mathf.Clamp01(k);
            RenderSettings.ambientLight = new Color(amb0.r * k, amb0.g * k, amb0.b * k, amb0.a);
            if (sun != null) sun.intensity = sun0 * k;
        }

        // ------------------------------------------------------------------ Tank: Bastião Divino
        IEnumerator UltBastion()
        {
            const float Dur = 6f;
            Vector3 c = transform.position;
            bastionCenter = c;
            bastionTime = Dur;
            bastionTick = 0f;
            // cúpula: cresce rápido, fica, e some nos últimos 10%
            var dome = SkillFX.Dome(c, BastionRadius, new Color(SkillFX.Gold.r, SkillFX.Gold.g, SkillFX.Gold.b, 0.5f));
            var tw = dome.AddComponent<FxTween>();
            tw.hold = 0.9f; tw.spin = 12f;
            tw.Set(Dur + 0.3f, Vector3.one * BastionRadius * 0.15f, Vector3.one * BastionRadius, new Color(SkillFX.Gold.r, SkillFX.Gold.g, SkillFX.Gold.b, 0.5f), 1f, 0f);
            SkillFX.RuneCircle(c, BastionRadius * 1.05f, SkillFX.Gold, Dur + 0.3f, 18f);
            SkillFX.Shockwave(c, BastionRadius, SkillFX.Holy, 0.5f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Vector3 pp = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * BastionRadius;
                var col = SkillFX.LightColumn(pp, 0.4f, 7f, SkillFX.Holy, Dur);
                var ct = col != null ? col.GetComponent<FxTween>() : null;
                if (ct != null) ct.hold = 0.85f;
            }
            SkillFX.StaticLight(c + Vector3.up * 3f, SkillFX.Gold, 3.5f, BastionRadius * 2.2f, Dur);
            Sfx.Play("shield", c);

            // puxa os inimigos para o centro e atordoa por 2 s
            var pulled = new List<Enemy>();
            if (Game.I != null)
                foreach (var e in Game.I.enemies.ToArray())
                {
                    if (e == null || e.dead) continue;
                    if (U.Flat(e.transform.position - c).magnitude > BastionRadius + 4f) continue;
                    pulled.Add(e);
                    StartCoroutine(PullEnemy(e, c, 0.35f));
                }
            yield return new WaitForSeconds(0.35f);
            int dmg = Dmg(null, 1.5f);
            foreach (var e in pulled)
            {
                if (e == null || e.dead) continue;
                e.TakeHit(dmg, c, false);
                ComboHit(dmg);
                if (!e.dead) e.Stun(2f);
                SkillFX.Phase("bash", "hit", e.transform.position + Vector3.up, Vector3.forward, 1f, SkillFX.Gold);
            }
            if (Game.I != null) Game.I.Shake(0.4f);
            while (bastionTime > 0f) yield return null;
        }

        // ------------------------------------------------------------------ deslocamento forçado de inimigos
        IEnumerator PullEnemy(Enemy e, Vector3 center, float time)
        {
            float t = 0f;
            while (t < time && e != null && !e.dead)
            {
                Vector3 to = U.Flat(center - e.transform.position);
                float d = to.magnitude;
                if (d < 1.2f) break;
                float step = Mathf.Min(d - 1.2f, d / Mathf.Max(0.02f, time - t) * Time.deltaTime);
                if (e.cc != null && e.cc.enabled) e.cc.Move(to / d * step);
                t += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator PushEnemy(Enemy e, Vector3 dir, float dist, float time)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir = U.Flat(dir).normalized;
            float t = 0f;
            while (t < time && e != null && !e.dead)
            {
                float dt = Time.deltaTime;
                float k = 1f - t / time;                      // desacelera
                if (e.cc != null && e.cc.enabled) e.cc.Move(dir * (dist / time) * 2f * k * dt);
                t += dt;
                yield return null;
            }
        }

        // ------------------------------------------------------------------ por frame
        void UpdateUltimate(float dt)
        {
            // Berserker: escala do modelo (o CharacterVisual mexe só na escala do objeto "visual", não do modelo)
            if (berserkTime > 0f) berserkTime = Mathf.Max(0f, berserkTime - dt);
            float target = berserkTime > 0f ? 1.35f : 1f;
            Transform m = visual != null ? visual.model : null;
            if (m != scaledModel)
            {
                scaledModel = m;
                if (m != null) scaledBase = m.localScale;
                berserkScale = 1f;
            }
            if (m != null && (berserkScale != target || berserkScale != 1f))
            {
                berserkScale = Mathf.MoveTowards(berserkScale, target, dt * 2.5f);
                m.localScale = scaledBase * berserkScale;
            }

            // Bastião: regenera dentro da cúpula
            if (bastionTime > 0f)
            {
                bastionTime = Mathf.Max(0f, bastionTime - dt);
                if (state != "dead" && U.Flat(transform.position - bastionCenter).magnitude <= BastionRadius + 0.3f)
                {
                    GameState.P.hp = Mathf.Min(GameState.maxHp, GameState.P.hp + GameState.maxHp * 0.04f * dt);
                    bastionTick -= dt;
                    if (bastionTick <= 0f)
                    {
                        bastionTick = 1f;
                        HUD.Popup(transform.position + Vector3.up * 2.4f, "+" + Mathf.RoundToInt(GameState.maxHp * 0.04f), U.Hex("ffe08a"));
                        FX.Sparkle(transform.position + Vector3.up * 0.3f, SkillFX.Gold, 0.8f, 0.3f);
                        GameState.Emit();
                    }
                }
            }
        }

        /// <summary>Corta efeitos contínuos das ultimates (morte, renascer).</summary>
        void EndUltimates()
        {
            berserkTime = 0f;
            bastionTime = 0f;
            if (ultReadyFx != null && state == "dead") { Destroy(ultReadyFx); ultReadyFx = null; }
        }

        /// <summary>Ao renascer: devolve a aura de "pronta" se a carga continua cheia.</summary>
        void RestoreUltAura()
        {
            if (UltReady && !ultActive && ultReadyFx == null) ultReadyFx = SkillFX.UltReadyAura(transform, UltColor());
        }
    }
}
