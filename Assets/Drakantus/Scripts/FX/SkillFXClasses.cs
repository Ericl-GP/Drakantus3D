using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// [Classes] Efeitos das 8 classes evoluídas (48 habilidades + 5 versões furiosas do Berserker) e a API pedida pelo gameplay:
    /// MuzzleFlash, ReloadFx, HealBeam, ShadowTendril. Os ids são registrados no "known" pelo construtor estático abaixo.
    /// Identidade: Assassino = roxo/sombra · Cavaleiro = ouro/aço pesado · Pistoleiro = latão/pólvora · Arqueiro Superior = verde/vento
    /// Sacerdote = luz dourada · Arqui-mago = arcano roxo/gelo/meteoro · Berserker = sangue/fúria · The Guard = aço azul + ouro, sombras.
    /// </summary>
    public static partial class SkillFX
    {
        static readonly string[] ClassIds =
        {
            "as_shadowcloak", "as_flurry", "as_shadowstep", "as_poison", "as_smoke", "as_deathmark",
            "kn_crescent", "kn_earthsplitter", "kn_heavycombo", "kn_ironstance", "kn_judgment", "kn_bladestorm",
            "gs_fanfire", "gs_dodgeshot", "gs_explosive", "gs_quickdraw", "gs_ricochet", "gs_deadeye",
            "ls_powershot", "ls_pin", "ls_volley", "ls_focus", "ls_split", "ls_skyfall",
            "pr_heal", "pr_blessing", "pr_aegis", "pr_sanctuary", "pr_smite", "pr_resurrection",
            "am_barrage", "am_overload", "am_singularity", "am_manashield", "am_frostnova", "am_cataclysm",
            "bk_cleave", "bk_cleave_f", "bk_leap", "bk_leap_f", "bk_roar", "bk_roar_f", "bk_whirl", "bk_whirl_f", "bk_throw", "bk_throw_f", "bk_rage",
            "gd_retribution", "gd_shieldwall", "gd_shadowstep", "gd_banner", "gd_reflect", "gd_sentence"
        };

        static SkillFX()
        {
            foreach (var id in ClassIds) known.Add(id);
        }

        // ------------------------------------------------------------------ paletas das classes
        public static readonly Color Venom = new Color(0.5f, 1f, 0.28f);
        public static readonly Color ShadowInk = new Color(0.06f, 0.01f, 0.1f);
        public static readonly Color ShadowPurple = new Color(0.58f, 0.28f, 1f);
        public static readonly Color Crimson = new Color(1f, 0.1f, 0.06f);
        public static readonly Color BloodDark = new Color(0.4f, 0f, 0.02f);
        public static readonly Color GuardBlue = new Color(0.5f, 0.69f, 1f);
        public static readonly Color KnightGold = new Color(0.88f, 0.69f, 0.31f);
        public static readonly Color Brass = new Color(1f, 0.76f, 0.36f);
        public static readonly Color Muzzle = new Color(1f, 0.86f, 0.55f);
        public static readonly Color HunterGreen = new Color(0.37f, 0.82f, 0.48f);

        static int flip;

        /// <summary>Efeito principal dos ids das classes. false = id não é daqui.</summary>
        static bool PlayClasses(string id, Vector3 pos, Vector3 dir, float r, Color col)
        {
            switch (id)
            {
                // Assassino
                case "as_shadowcloak": AsCloak(pos); return true;
                case "as_flurry": AsFlurryStart(pos, dir); return true;
                case "as_shadowstep": AsStepOut(pos, dir); return true;
                case "as_poison": AsPoison(pos); return true;
                case "as_smoke": AsSmokeStart(pos, r); return true;
                case "as_deathmark": AsDeathmark(pos); return true;
                // Cavaleiro
                case "kn_crescent": KnCrescent(pos, dir, r); return true;
                case "kn_earthsplitter": KnSplitCast(pos, dir); return true;
                case "kn_heavycombo": KnComboHit(pos, dir, r); return true;
                case "kn_ironstance": KnIronStance(pos); return true;
                case "kn_judgment": KnJudgment(pos, r); return true;
                case "kn_bladestorm": KnBladestorm(pos, dir, r); return true;
                // Pistoleiro
                case "gs_fanfire": GsFan(pos, dir); return true;
                case "gs_dodgeshot": GsDodge(pos, dir); return true;
                case "gs_explosive": GsExplosiveCast(pos, dir); return true;
                case "gs_quickdraw": GsQuickdraw(pos); return true;
                case "gs_ricochet": GsRicochetCast(pos, dir); return true;
                case "gs_deadeye": GsDeadeyeStart(pos, dir); return true;
                // Arqueiro Superior
                case "ls_powershot": LsPowerCast(pos, dir); return true;
                case "ls_pin": LsPinCast(pos, dir); return true;
                case "ls_volley": LsVolleyCast(pos, dir); return true;
                case "ls_focus": LsFocus(pos); return true;
                case "ls_split": LsSplitCast(pos, dir); return true;
                case "ls_skyfall": LsSkyfallHit(pos, r); return true;
                // Sacerdote
                case "pr_heal": PrHealCast(pos, r); return true;
                case "pr_blessing": PrBlessCast(pos, r); return true;
                case "pr_aegis": PrAegisCast(pos, r); return true;
                case "pr_sanctuary": PrSanctuaryStart(pos, r); return true;
                case "pr_smite": PrSmite(pos, r); return true;
                case "pr_resurrection": PrResurrection(pos, r); return true;
                // Arqui-mago
                case "am_barrage": AmBarrage(pos, dir); return true;
                case "am_overload": AmOverload(pos); return true;
                case "am_singularity": AmSingularityStart(pos, r); return true;
                case "am_manashield": AmManaShield(pos); return true;
                case "am_frostnova": AmFrostNova(pos, r); return true;
                case "am_cataclysm": AmCataclysmCast(pos); return true;
                // Berserker
                case "bk_cleave": BkCleave(pos, dir, r, false); return true;
                case "bk_cleave_f": BkCleave(pos, dir, r, true); return true;
                case "bk_leap": BkLand(pos, r, false); return true;
                case "bk_leap_f": BkLand(pos, r, true); return true;
                case "bk_roar": BkRoar(pos, false); return true;
                case "bk_roar_f": BkRoar(pos, true); return true;
                case "bk_whirl": BkWhirl(pos, dir, r, false); return true;
                case "bk_whirl_f": BkWhirl(pos, dir, r, true); return true;
                case "bk_throw": BkThrow(pos, dir, false); return true;
                case "bk_throw_f": BkThrow(pos, dir, true); return true;
                case "bk_rage": BkRage(pos); return true;
                // The Guard
                case "gd_retribution": GdRetribution(pos, dir, r); return true;
                case "gd_shieldwall": GdShieldwall(pos, r); return true;
                case "gd_shadowstep": GdStepStart(pos, dir); return true;
                case "gd_banner": GdBannerStart(pos, r); return true;
                case "gd_reflect": GdReflect(pos); return true;
                case "gd_sentence": GdSentence(pos, r); return true;
            }
            return false;
        }

        /// <summary>Fases dos ids das classes. false = combinação não tratada.</summary>
        static bool PhaseClasses(string id, string phase, Vector3 pos, Vector3 dir, float r, Color col)
        {
            switch (id + ":" + phase)
            {
                case "as_flurry:tick": AsFlurryTick(pos, dir, r); return true;
                case "as_shadowstep:start": AsStepOut(pos, dir); return true;
                case "as_shadowstep:hit": AsStepHit(pos, dir); return true;
                case "as_smoke:tick": AsSmokeTick(pos, r); return true;
                case "as_smoke:end": AsSmokeEnd(pos, r); return true;
                case "as_deathmark:end": AsDeathmarkBoom(pos, r); return true;

                case "kn_earthsplitter:tick": KnSplitTick(pos, dir, r); return true;
                case "kn_heavycombo:tick": KnComboHit(pos, dir, r); return true;
                case "kn_heavycombo:end": KnComboFinal(pos, dir, r); return true;
                case "kn_judgment:start": KnJumpStart(pos, dir); return true;

                case "gs_dodgeshot:start": GsDodge(pos, dir); return true;
                case "gs_deadeye:hit": GsDeadeyeHit(pos, dir); return true;

                case "ls_volley:drop": LsVolleyDrop(pos, r); return true;
                case "ls_skyfall:start": LsSkyfallStart(pos, dir, r); return true;
                case "ls_skyfall:hit": LsSkyfallHit(pos, r); return true;

                case "pr_heal:hit": PrHealHit(pos); return true;
                case "pr_blessing:hit": PrBlessHit(pos); return true;
                case "pr_aegis:hit": PrAegisHit(pos); return true;
                case "pr_resurrection:hit": PrResHit(pos); return true;
                case "pr_sanctuary:tick": PrSanctuaryTick(pos, r); return true;
                case "pr_sanctuary:end": ZoneEnd("pr_sanctuary", pos, Gold, r); return true;
                case "pr_smite:start": PrSmiteStart(pos, r); return true;

                case "am_singularity:tick": AmSingularityTick(pos, r); return true;
                case "am_singularity:end": AmSingularityEnd(pos, r); return true;
                case "am_cataclysm:drop": AmCataclysmDrop(pos, r); return true;

                case "bk_leap:start": BkJump(pos, dir, false); return true;
                case "bk_leap_f:start": BkJump(pos, dir, true); return true;

                case "gd_shieldwall:hit": GdShieldHit(pos); return true;
                case "gd_shadowstep:start": GdStepStart(pos, dir); return true;
                case "gd_shadowstep:hit": GdStepHit(pos); return true;
                case "gd_banner:tick": GdBannerTick(pos, r); return true;
                case "gd_banner:end": GdBannerEnd(pos, r); return true;
                case "gd_reflect:hit": GdReflectHit(pos, dir); return true;
                case "gd_sentence:start": GdSentenceStart(pos, dir); return true;
            }
            return false;
        }

        // ==================================================================================== API pedida pelo gameplay
        /// <summary>Tiro de pistola: clarão em estrela, jato de fogo, faíscas, fumacinha e cápsula ejetada.</summary>
        public static void MuzzleFlash(Vector3 pos, Vector3 dir)
        {
            dir = U.Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            var fl = Sys("tiro_clarao", pos + dir * 0.12f, Muzzle, true, 0.05f, 0.08f, 0f, 0f, 0.5f, 0.75f);
            fl.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            var m = fl.main; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Bursts(fl, 2); Grad(fl, Color.white, Muzzle, 1f); fl.Play();
            var jet = Sys("tiro_jato", pos, Muzzle, true, 0.05f, 0.1f, 6f, 12f, 0.07f, 0.15f);
            jet.transform.rotation = Quaternion.LookRotation(dir);
            Cone(jet, 12f, 0.02f); Bursts(jet, 8); Stretch(jet, 2.5f, 0.03f); Grad(jet, Color.white, Fire, 1f); jet.Play();
            var sm = Sys("tiro_fumaca", pos + dir * 0.2f, new Color(0.7f, 0.68f, 0.65f, 0.45f), false, 0.4f, 0.7f, 0.3f, 0.9f, 0.15f, 0.3f, -0.15f);
            sm.transform.rotation = Quaternion.LookRotation(dir);
            Cone(sm, 25f, 0.05f); Bursts(sm, 4); FxUtil.AlphaFade(sm, new Color(0.85f, 0.82f, 0.78f), new Color(0.5f, 0.48f, 0.46f), 0.45f); sm.Play();
            // cápsula de latão pulando para o lado
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            var cs = Sys("capsula", pos - dir * 0.15f, Brass, false, 0.45f, 0.6f, 2f, 3f, 0.04f, 0.05f, 2.2f, 0.2f);
            cs.transform.rotation = Quaternion.LookRotation((side + Vector3.up).normalized);
            Cone(cs, 18f, 0.01f);
            MeshParticles(cs, U.Lit(Brass, 0.85f, Brass * 0.3f));
            var cm = cs.main; cm.startSize3D = true;
            cm.startSizeX = new ParticleSystem.MinMaxCurve(0.03f); cm.startSizeY = new ParticleSystem.MinMaxCurve(0.03f); cm.startSizeZ = new ParticleSystem.MinMaxCurve(0.07f);
            var csz = cs.sizeOverLifetime; csz.enabled = false;
            Bursts(cs, 1); cs.Play();
            FX.FlashLight(pos, Muzzle, 2.4f, 3.5f, 0.07f);
        }

        /// <summary>Recarga: anel de latão girando na mão, cápsulas caindo e um "clique" brilhante no fim.</summary>
        public static void ReloadFx(Transform hand)
        {
            if (hand == null) return;
            var ring = FxUtil.LoopSys("recarga_anel", hand, Vector3.zero, Brass, true, 0.25f, 0.35f, 0f, 0f, 0.04f, 0.07f, 0f, 36f, 30);
            var m = ring.main; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            FxUtil.FlatRing(ring, 0.22f);
            FxUtil.Orbit(ring, 9f, 0f);
            Grad(ring, Color.white, Brass, 1f);
            ring.Play();
            ring.gameObject.AddComponent<FxAutoEnd>().life = 1.15f;
            var cs = Sys("recarga_capsulas", hand.position, Brass, false, 0.5f, 0.7f, 0.5f, 1.5f, 0.04f, 0.05f, 2f, 0.5f);
            Sphere(cs, 0.08f);
            MeshParticles(cs, U.Lit(Brass, 0.85f, Brass * 0.3f));
            var cm = cs.main; cm.startSize3D = true;
            cm.startSizeX = new ParticleSystem.MinMaxCurve(0.03f); cm.startSizeY = new ParticleSystem.MinMaxCurve(0.03f); cm.startSizeZ = new ParticleSystem.MinMaxCurve(0.07f);
            var csz = cs.sizeOverLifetime; csz.enabled = false;
            Rate(cs, 12f); cs.Play();
            FX.Sparkle(hand.position, Brass, 0.3f, 0.3f);
            FxDelay.Run(1.1f, () =>
            {
                if (hand == null) return;
                Sparks(hand.position, Brass, 8, 1.5f, 3.5f, 0.5f);
                var fl = Sys("recarga_clique", hand.position, Color.white, true, 0.08f, 0.12f, 0f, 0f, 0.35f, 0.45f);
                fl.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
                Bursts(fl, 1); Grad(fl, Color.white, Brass, 1f); fl.Play();
            });
        }

        /// <summary>Feixe de cura em arco do curandeiro até o aliado + brilhos correndo pelo arco.</summary>
        public static void HealBeam(Vector3 from, Vector3 to, Color col)
        {
            const int n = 14;
            var pts = new Vector3[n];
            float dist = Vector3.Distance(from, to);
            float lift = 0.7f + dist * 0.08f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / (n - 1);
                pts[i] = Vector3.Lerp(from, to, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * lift;
            }
            Color c = Color.Lerp(col, Color.white, 0.15f);
            FxUtil.GlowLine("cura_feixe", pts, new Color(1f, 1f, 1f, 0.95f), new Color(c.r, c.g, c.b, 0.9f), 0.13f, 0.45f);
            FxUtil.GlowLine("cura_brilho", pts, new Color(c.r, c.g, c.b, 0.45f), new Color(c.r, c.g, c.b, 0.3f), 0.5f, 0.4f);
            var ps = Sys("cura_faiscas", from, c, true, 0.4f, 0.7f, 0f, 0f, 0.06f, 0.13f, -0.2f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            var em = ps.emission; em.enabled = false;
            ps.Play();
            for (int i = 0; i < n; i++)
            {
                var ep = new ParticleSystem.EmitParams { position = pts[i], velocity = Vector3.up * Random.Range(0.2f, 0.8f) + Random.insideUnitSphere * 0.3f, applyShapeToPosition = false };
                ps.Emit(ep, 2);
            }
            Grad(ps, Color.white, c, 1f);
            FX.Sparkle(to, c, 0.6f, 0.3f);
            FX.FlashLight(to, c, 1.8f, 3f, 0.25f);
        }

        /// <summary>
        /// Passo da Sombra: sombra viva roxo-escura que corre pelo chão de "from" até "to" em "travel" s, com fiapos subindo;
        /// fica até "hold" s (tempo total) e some. Ao chegar, a sombra irrompe no alvo.
        /// </summary>
        public static GameObject ShadowTendril(Vector3 from, Vector3 to, float travel, float hold)
        {
            var go = new GameObject("sombra_tentaculo");
            go.transform.SetParent(FX.Root, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var st = go.AddComponent<ShadowTendrilFx>();
            st.Setup(from, to, Mathf.Max(0.05f, travel), Mathf.Max(travel + 0.1f, hold));
            return go;
        }

        // ==================================================================================== blocos novos
        /// <summary>Spray de sangue (gotas escuras com gravidade + névoa vermelha).</summary>
        public static void BloodSpray(Vector3 pos, Vector3 dir, int count, float angle, float speed)
        {
            var ps = Sys("sangue_spray", pos, new Color(0.65f, 0.02f, 0.03f, 0.95f), false, 0.35f, 0.7f, speed * 0.4f, speed, 0.05f, 0.11f, 1.6f);
            if (dir.sqrMagnitude > 0.0001f) { ps.transform.rotation = Quaternion.LookRotation(dir.normalized); Cone(ps, angle, 0.1f); }
            else Sphere(ps, 0.2f);
            Bursts(ps, count); Stretch(ps, 1.5f, 0.05f);
            FxUtil.AlphaFade(ps, new Color(0.85f, 0.06f, 0.05f), new Color(0.35f, 0f, 0f), 0.95f); ps.Play();
            var mist = Sys("sangue_nevoa", pos, new Color(0.7f, 0.05f, 0.05f, 0.4f), false, 0.4f, 0.7f, 0.5f, 1.5f, 0.3f, 0.6f, 0.1f);
            Sphere(mist, 0.25f); Bursts(mist, Mathf.Max(3, count / 5));
            FxUtil.AlphaFade(mist, new Color(0.8f, 0.1f, 0.08f), new Color(0.3f, 0f, 0f), 0.4f); mist.Play();
        }

        /// <summary>Espinhos que brotam do chão em círculo (gelo/pedra), ficam um pouco e afundam.</summary>
        public static void GroundSpikes(Vector3 pos, float radius, int count, float height, Material mat, Color tint, bool alphaTint)
        {
            var holder = new GameObject("espinhos");
            holder.transform.SetParent(FX.Root, false);
            holder.transform.position = pos;
            var rise = holder.AddComponent<FxRise>();
            for (int i = 0; i < count; i++)
            {
                float a = (i * 360f / count + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float rr = radius * Random.Range(0.75f, 1f);
                float h = height * Random.Range(0.6f, 1.15f);
                var pivot = new GameObject("espinho").transform;
                pivot.SetParent(holder.transform, false);
                pivot.localPosition = d * rr;
                pivot.localRotation = Quaternion.AngleAxis(Random.Range(15f, 32f), new Vector3(d.z, 0f, -d.x)) * Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
                var sh = FxUtil.Shape(PrimitiveType.Cube, pivot, Vector3.up * h * 0.5f, new Vector3(0.22f, h, 0.22f) * Mathf.Lerp(0.8f, 1.2f, Random.value));
                var r = sh.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                if (alphaTint) FxUtil.Tint(r, tint);
                rise.Add(pivot, alphaTint ? r : null, tint);
            }
            rise.Setup(0.1f, 0.7f, 0.3f);
        }

        /// <summary>Lâmina/arma de luz que cai do céu e crava no chão (Sentença do Guardião).</summary>
        static void LightSword(Vector3 pos, Color blade, Color hilt, float size)
        {
            var go = new GameObject("espada_luz");
            go.transform.SetParent(FX.Root, false);
            var bm = U.Lit(Color.Lerp(blade, Color.white, 0.4f), 0.9f, blade * 2.8f);
            var hm = U.Lit(hilt, 0.7f, hilt * 1.6f);
            FxUtil.Solid(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.6f * size, 0f), new Vector3(0.35f * size, 3.2f * size, 0.08f * size), Color.white, 0f).GetComponent<Renderer>().sharedMaterial = bm;
            FxUtil.Solid(PrimitiveType.Cube, go.transform, new Vector3(0f, 3.3f * size, 0f), new Vector3(1.3f * size, 0.18f * size, 0.18f * size), Color.white, 0f).GetComponent<Renderer>().sharedMaterial = hm;
            FxUtil.Solid(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 3.75f * size, 0f), new Vector3(0.14f * size, 0.4f * size, 0.14f * size), Color.white, 0f).GetComponent<Renderer>().sharedMaterial = hm;
            var gem = FxUtil.Solid(PrimitiveType.Sphere, go.transform, new Vector3(0f, 4.2f * size, 0f), Vector3.one * 0.26f * size, Color.white, 0f);
            gem.GetComponent<Renderer>().sharedMaterial = U.Lit(GuardBlue, 0.9f, GuardBlue * 3f);
            var drop = go.AddComponent<FxDropStick>();
            // a lâmina entra ~1 m no chão (o pé da lâmina fica abaixo do chão)
            drop.Setup(pos + Vector3.up * 9f - Vector3.up * 1f * size, pos - Vector3.up * 1.1f * size, 0.12f, 0.7f, 0.3f);
        }

        /// <summary>Flecha grande cravada no chão (some encolhendo).</summary>
        static void StuckArrow(Vector3 p, Color glow)
        {
            var go = new GameObject("flecha_cravada");
            go.transform.SetParent(FX.Root, false);
            var shaft = FxUtil.Solid(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.06f, 0.55f, 0.06f), Wood, 0f, 0.2f);
            for (int i = 0; i < 3; i++)
            {
                var f = FxUtil.Solid(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.015f, 0.26f, 0.14f), glow, 0.8f);
                var rot = Quaternion.Euler(0f, i * 120f, 0f);
                f.transform.localRotation = rot;
                f.transform.localPosition = new Vector3(0f, 1f, 0f) + rot * new Vector3(0f, 0f, 0.06f);
            }
            go.transform.rotation = Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(0f, 360f), Random.Range(-14f, 14f));
            var drop = go.AddComponent<FxDropStick>();
            drop.Setup(p + Vector3.up * 3.5f, p - Vector3.up * 0.25f, 0.06f, 0.8f, 0.25f);
        }

        static void ZoneEnd(string id, Vector3 pos, Color c, float r)
        {
            var z = ZoneFx.Find(id, pos, Mathf.Max(3f, r));
            if (z != null) z.End(0.5f);
            FX.Ring(pos, r, c, 0.4f, 0.9f);
        }

        static Vector3 PlayerChest()
        {
            var g = Game.I;
            if (g == null || g.player == null) return Vector3.zero;
            return g.player.transform.position + Vector3.up * 1.2f;
        }

        // ==================================================================================== ASSASSINO (roxo / sombra)
        static void AsCloak(Vector3 pos)
        {
            var sm = Sys("manto_fumaca", pos + Vector3.up * 0.3f, new Color(0.12f, 0.06f, 0.2f, 0.75f), false, 0.6f, 1.1f, 1.5f, 3.5f, 0.6f, 1.1f, -0.1f);
            FlatCircle(sm, 0.3f, 0f); Bursts(sm, 22); Noise(sm, 0.6f, 1f);
            FxUtil.AlphaFade(sm, new Color(0.3f, 0.15f, 0.45f), new Color(0.04f, 0.01f, 0.07f), 0.75f); sm.Play();
            Implode(pos + Vector3.up * 0.9f, 1.6f, ShadowPurple, 0.25f, 26);
            Afterimage(PlayerVisual(), ShadowPurple, 0.45f);
            FX.Ring(pos, 1.6f, ShadowPurple, 0.35f, 0.9f);
            var sp = Sys("manto_brilho", pos + Vector3.up * 0.9f, ShadowPurple, true, 0.4f, 0.8f, 0.5f, 2f, 0.04f, 0.09f, -0.2f);
            Sphere(sp, 0.5f); Bursts(sp, 20); Grad(sp, Color.white, ShadowPurple, 1f); sp.Play();
            FX.FlashLight(pos + Vector3.up, ShadowPurple, 2.5f, 4f, 0.3f);
        }

        static void AsFlurryStart(Vector3 pos, Vector3 dir)
        {
            Afterimage(PlayerVisual(), ShadowPurple, 0.3f);
            WindRing(pos + Vector3.up * 1f + dir * 0.6f, dir, ShadowPurple, 0.45f, 18);
        }

        static void AsFlurryTick(Vector3 pos, Vector3 dir, float r)
        {
            flip++;
            bool rtl = flip % 2 == 0;
            float yaw = Random.Range(-18f, 18f);
            Vector3 d = Quaternion.Euler(0f, yaw, 0f) * dir;
            float h = 0.8f + (flip % 3) * 0.18f;
            FX.SlashArc(pos + Vector3.up * h, d, flip % 3 == 0 ? Color.white : ShadowPurple, r * 0.85f, 110f, rtl, 0.1f, 0.3f, 5);
            FX.SlashArc(pos + Vector3.up * (h + 0.04f), d, new Color(0.3f, 0.1f, 0.5f), r * 0.7f, 90f, rtl, 0.12f, 0.2f, 0);
            Sparks(pos + Vector3.up * h + d * r * 0.7f, Color.Lerp(ShadowPurple, Color.white, 0.5f), 6, 2f, 5f, 0.8f);
        }

        static void AsStepOut(Vector3 pos, Vector3 dir)
        {
            var sm = Sys("passo_fumaca", pos + Vector3.up * 0.6f, new Color(0.1f, 0.04f, 0.16f, 0.8f), false, 0.4f, 0.8f, 1f, 2.5f, 0.5f, 0.9f, -0.1f);
            Sphere(sm, 0.4f); Bursts(sm, 16);
            FxUtil.AlphaFade(sm, new Color(0.3f, 0.12f, 0.45f), new Color(0.03f, 0f, 0.06f), 0.8f); sm.Play();
            Afterimage(PlayerVisual(), ShadowPurple, 0.4f);
            Implode(pos + Vector3.up * 0.9f, 1.2f, ShadowPurple, 0.18f, 18);
            FX.FlashLight(pos + Vector3.up, ShadowPurple, 2f, 3.5f, 0.2f);
        }

        static void AsStepHit(Vector3 pos, Vector3 dir)
        {
            // pos = peito do alvo; corte em X (dois arcos cruzados) + estouro de sombra
            Vector3 feet = pos - Vector3.up * 1f;
            Vector3 me = feet - dir * 1.2f;
            FX.SlashArc(me + Vector3.up * 1.15f, dir, ShadowPurple, 1.8f, 150f, true, 0.14f, 0.55f, 10);
            FX.SlashArc(me + Vector3.up * 0.75f, dir, Color.white, 1.7f, 140f, false, 0.16f, 0.3f, 6);
            var ps = Sys("passo_golpe", pos, ShadowPurple, true, 0.2f, 0.4f, 3f, 7f, 0.05f, 0.12f, 0.6f);
            ps.transform.rotation = Quaternion.LookRotation(dir); Cone(ps, 35f, 0.15f);
            Bursts(ps, 26); Stretch(ps, 1.8f, 0.04f); Grad(ps, Color.white, ShadowPurple, 1f); ps.Play();
            BloodSpray(pos, dir, 10, 30f, 4f);
            FX.Ring(feet, 1.3f, ShadowPurple, 0.25f, 0.3f);
            FX.FlashLight(pos, ShadowPurple, 3.5f, 4f, 0.18f);
        }

        static void AsPoison(Vector3 pos)
        {
            Vector3 hands = pos + Vector3.up * 1f;
            var ps = Sys("lamina_veneno", hands, Venom, true, 0.3f, 0.6f, 1.5f, 3.5f, 0.06f, 0.14f, 1f);
            Sphere(ps, 0.4f); Bursts(ps, 26); Stretch(ps, 1.3f, 0.05f); Grad(ps, Color.white, Venom, 1f); ps.Play();
            var bub = Sys("veneno_bolhas", pos + Vector3.up * 0.2f, Venom, true, 0.6f, 1.1f, 0.4f, 1.2f, 0.08f, 0.2f, -0.3f);
            bub.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, U.RingTexture());
            FlatCircle(bub, 0.8f, 1f); Bursts(bub, 18); Grad(bub, new Color(0.85f, 1f, 0.7f), Venom, 0.9f); bub.Play();
            Mist(pos + Vector3.up * 0.3f, new Color(0.45f, 0.9f, 0.25f), 10, 0.5f);
            FX.Ring(pos, 1.4f, Venom, 0.35f, 0.4f);
            FX.FlashLight(hands, Venom, 2.2f, 3.5f, 0.3f);
        }

        static void AsSmokeStart(Vector3 c, float r)
        {
            var z = ZoneFx.Create("as_smoke", c, 20f);
            var sm = z.Add(FxUtil.LoopSys("nuvem", z.transform, Vector3.up * 0.4f, new Color(0.3f, 0.28f, 0.34f, 0.7f), false, 1.4f, 2.2f, 0.1f, 0.5f, 1.2f, 2.2f, -0.03f, 26f, 90));
            FlatCircle(sm, r * 0.8f, 1f); Noise(sm, 0.4f, 0.5f);
            FxUtil.AlphaFade(sm, new Color(0.42f, 0.38f, 0.48f), new Color(0.18f, 0.15f, 0.22f), 0.7f);
            var rot = sm.main; rot.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var rol = sm.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            sm.Play();
            var mt = z.Add(FxUtil.LoopSys("nuvem_brilho", z.transform, Vector3.up * 0.3f, ShadowPurple, true, 0.6f, 1.2f, 0.1f, 0.5f, 0.04f, 0.08f, -0.1f, 14f));
            FlatCircle(mt, r * 0.8f, 1f); Grad(mt, Color.white, ShadowPurple, 0.7f); mt.Play();
            // explosão inicial
            var puff = Sys("bomba_fumaca", c + Vector3.up * 0.3f, new Color(0.35f, 0.32f, 0.4f, 0.8f), false, 0.6f, 1.2f, r * 1.2f, r * 2f, 0.8f, 1.6f, -0.05f);
            FlatCircle(puff, 0.3f, 0f); Bursts(puff, 34); Noise(puff, 0.5f, 0.8f);
            FxUtil.AlphaFade(puff, new Color(0.5f, 0.46f, 0.55f), new Color(0.2f, 0.18f, 0.24f), 0.8f); puff.Play();
            Sparks(c + Vector3.up * 0.3f, ShadowPurple, 18, 3f, 7f, 1f);
            FX.Ring(c, r, ShadowPurple, 0.35f, 0.3f);
            FX.FlashLight(c + Vector3.up, Color.white, 3f, r + 3f, 0.15f);
        }

        static void AsSmokeTick(Vector3 c, float r)
        {
            var z = ZoneFx.Find("as_smoke", c, r);
            if (z == null) AsSmokeStart(c, r);
        }

        static void AsSmokeEnd(Vector3 c, float r)
        {
            var z = ZoneFx.Find("as_smoke", c, r);
            if (z != null) z.End(0.8f);
            var ps = Sys("fumaca_some", c + Vector3.up * 0.6f, new Color(0.35f, 0.32f, 0.4f, 0.5f), false, 0.8f, 1.3f, 0.5f, 1.5f, 1f, 1.8f, -0.1f);
            FlatCircle(ps, r * 0.7f, 1f); Bursts(ps, 14);
            FxUtil.AlphaFade(ps, new Color(0.45f, 0.42f, 0.5f), new Color(0.2f, 0.18f, 0.22f), 0.5f); ps.Play();
        }

        static void AsDeathmark(Vector3 pos)
        {
            RuneCircle(pos, 1.5f, ShadowPurple, 0.8f, 260f);
            LightColumn(pos, 0.35f, 5f, ShadowPurple, 0.45f);
            Implode(pos + Vector3.up * 1.2f, 1.8f, ShadowPurple, 0.25f, 30);
            var ps = Sys("marca_sinal", pos + Vector3.up * 2.4f, ShadowPurple, true, 0.25f, 0.4f, 0f, 0f, 1f, 1.3f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            Bursts(ps, 1); Grad(ps, Color.white, ShadowPurple, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up * 1.5f, ShadowPurple, 3f, 4.5f, 0.3f);
        }

        static void AsDeathmarkBoom(Vector3 pos, float r)
        {
            Vector3 c = pos + Vector3.up * 1f;
            var ps = Sys("marca_explode", c, ShadowPurple, true, 0.3f, 0.6f, 4f, 9f, 0.1f, 0.25f, 0.3f);
            Sphere(ps, 0.3f); Bursts(ps, 50); Stretch(ps, 1.6f, 0.04f); Grad(ps, Color.white, ShadowPurple, 1f); ps.Play();
            var dk = Sys("marca_sombra", c, new Color(0.08f, 0.02f, 0.12f, 0.85f), false, 0.4f, 0.8f, 2f, 4f, 0.5f, 1f, -0.1f);
            Sphere(dk, 0.4f); Bursts(dk, 18);
            FxUtil.AlphaFade(dk, new Color(0.25f, 0.08f, 0.35f), new Color(0.02f, 0f, 0.04f), 0.85f); dk.Play();
            for (int i = 0; i < 4; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 90f + Random.Range(-20f, 20f), 0f) * Vector3.forward;
                FxUtil.MiniBolt(c, c + d * 2f + Vector3.up * Random.Range(-0.5f, 0.8f), ShadowPurple, 0.08f, 0.2f);
            }
            Shockwave(pos, Mathf.Max(2.2f, r * 1.6f), ShadowPurple, 0.35f);
            Crack(pos, 2.6f, ShadowPurple, 1.4f, Random.Range(0f, 360f));
            FX.FlashLight(c, ShadowPurple, 5f, 6f, 0.35f);
        }

        // ==================================================================================== CAVALEIRO (ouro / aço pesado)
        static void KnCrescent(Vector3 pos, Vector3 dir, float r)
        {
            Vector3 chest = pos + Vector3.up * 1f;
            FX.SlashArc(chest, dir, KnightGold, r, 185f, true, 0.24f, 1.1f, 26);
            FX.SlashArc(chest + Vector3.up * 0.05f, dir, Steel, r * 0.9f, 170f, true, 0.2f, 0.35f, 0);
            FX.SlashArc(chest - Vector3.up * 0.2f, dir, Color.Lerp(KnightGold, Color.white, 0.5f), r * 1.08f, 150f, true, 0.3f, 0.2f, 0);
            ConeBurst(chest + dir * 0.6f, dir, Wind, 55f, 6f, 11f, 30, 0f);
            var dust = Sys("crescente_poeira", pos + Vector3.up * 0.1f, new Color(0.8f, 0.72f, 0.6f, 0.45f), false, 0.4f, 0.7f, 3f, 6f, 0.4f, 0.8f, -0.05f);
            dust.transform.rotation = Quaternion.LookRotation(dir);
            Cone(dust, 70f, 0.4f); Bursts(dust, 16);
            FxUtil.AlphaFade(dust, new Color(0.85f, 0.78f, 0.65f), new Color(0.6f, 0.52f, 0.44f), 0.45f); dust.Play();
            Crack(pos + dir * r * 0.55f, r * 1.2f, KnightGold, 1.3f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 90f, 0.4f);
            FX.FlashLight(chest + dir * 1.5f, KnightGold, 4f, r + 3f, 0.25f);
        }

        static void KnSplitCast(Vector3 pos, Vector3 dir)
        {
            Vector3 p = pos + dir * 0.9f;
            Crack(p, 2f, KnightGold, 1.6f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0.5f);
            ImpactPhysical(p + Vector3.up * 0.2f, KnightGold, 1f);
            FX.Dust(p, U.Hex("c8b090"), 1.2f);
            FX.Ring(p, 1.4f, KnightGold, 0.25f, 0.3f);
        }

        static void KnSplitTick(Vector3 p, Vector3 dir, float r)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            Crack(p, r * 2.2f, KnightGold, 1.8f, yaw + Random.Range(-15f, 15f), 0.5f);
            // lascas de pedra brotando (espinhos baixos) + pedras + brilho dourado
            GroundSpikes(p, r * 0.5f, 4, 1f, U.Lit(U.Hex("7a6a5a"), 0.1f), Color.white, false);
            Rocks(p, r * 0.3f, U.Hex("7a6a5a"), 5, 0.8f);
            FX.Dust(p, U.Hex("a08060"), 1f);
            var ps = Sys("racha_brilho", p + Vector3.up * 0.1f, KnightGold, true, 0.3f, 0.6f, 2f, 5f, 0.05f, 0.12f, 0.4f);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(ps, 15f, r * 0.4f); Bursts(ps, 12); Stretch(ps, 1.6f, 0.04f); Grad(ps, Holy, KnightGold, 1f); ps.Play();
            FX.FlashLight(p + Vector3.up * 0.5f, KnightGold, 2f, 3.5f, 0.2f);
        }

        static void KnComboHit(Vector3 pos, Vector3 dir, float r)
        {
            flip++;
            bool rtl = flip % 2 == 0;
            Vector3 chest = pos + Vector3.up * 1f;
            FX.SlashArc(chest, dir, KnightGold, r, 165f, rtl, 0.22f, 0.9f, 16);
            FX.SlashArc(chest + Vector3.up * 0.04f, dir, Steel, r * 0.92f, 150f, rtl, 0.18f, 0.28f, 0);
            Sparks(chest + dir * r * 0.7f, Color.Lerp(KnightGold, Color.white, 0.4f), 12, 3f, 7f, 1.2f);
            FX.Dust(pos + dir * r * 0.6f, U.Hex("c8b090"), 0.7f);
            FX.FlashLight(chest + dir, KnightGold, 2.5f, 4f, 0.15f);
        }

        static void KnComboFinal(Vector3 c, Vector3 dir, float r)
        {
            Crack(c, r * 1.7f, KnightGold, 2.4f, Random.Range(0f, 360f));
            Shockwave(c, r * 1.2f, KnightGold, 0.4f);
            DelayedRing(c, r * 0.9f, Steel, 0.35f, 0.08f);
            LightColumn(c, r * 0.35f, 5f, KnightGold, 0.45f);
            Rocks(c, r * 0.4f, U.Hex("7a6a5a"), 14, 1f);
            var ps = Sys("juramento_estouro", c + Vector3.up * 0.3f, KnightGold, true, 0.3f, 0.7f, 5f, 10f, 0.08f, 0.18f, 0.6f);
            Hemisphere(ps, 0.4f); Bursts(ps, 50); Stretch(ps, 1.8f, 0.04f); Grad(ps, Holy, KnightGold, 1f); ps.Play();
            FX.Dust(c, U.Hex("c8b090"), 1.8f);
            FX.FlashLight(c + Vector3.up, KnightGold, 6f, r + 5f, 0.4f);
        }

        static void KnIronStance(Vector3 pos)
        {
            Bubble(pos, 1.5f, Steel, 0.55f);
            RuneCircle(pos, 1.6f, KnightGold, 0.9f, 80f);
            var ps = Sys("ferro_faiscas", pos + Vector3.up * 1f, Steel, true, 0.25f, 0.5f, 2f, 5f, 0.04f, 0.09f, 1f);
            Sphere(ps, 0.6f); Bursts(ps, 30); Stretch(ps, 1.4f, 0.05f); Grad(ps, Color.white, KnightGold, 1f); ps.Play();
            Shockwave(pos, 2f, Steel, 0.3f);
            FX.FlashLight(pos + Vector3.up, Steel, 3.5f, 5f, 0.3f);
        }

        static void KnJumpStart(Vector3 pos, Vector3 dir)
        {
            FX.Dust(pos, U.Hex("c8b090"), 1.3f);
            FX.Ring(pos, 1.5f, KnightGold, 0.3f, 0.4f);
            Afterimage(PlayerVisual(), KnightGold, 0.35f);
            var ps = Sys("julgamento_sobe", pos + Vector3.up * 0.3f, KnightGold, true, 0.3f, 0.5f, 5f, 9f, 0.05f, 0.1f);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(ps, 12f, 0.5f); Bursts(ps, 18); Stretch(ps, 2.5f, 0.04f); Grad(ps, Color.white, KnightGold, 1f); ps.Play();
        }

        static void KnJudgment(Vector3 pos, float r)
        {
            LightColumn(pos, r * 0.45f, 9f, Holy, 0.6f);
            LightColumn(pos, r * 0.2f, 9f, KnightGold, 0.5f);
            Crack(pos, r * 1.8f, KnightGold, 2.6f, Random.Range(0f, 360f));
            Shockwave(pos, r * 1.3f, KnightGold, 0.45f);
            DelayedRing(pos, r * 1.7f, Holy, 0.5f, 0.1f);
            Rocks(pos, r * 0.45f, U.Hex("7a6a5a"), 18, 1.1f);
            var ps = Sys("julgamento_luz", pos + Vector3.up * 0.3f, KnightGold, true, 0.4f, 0.8f, 4f, 9f, 0.08f, 0.18f, 0.3f);
            Hemisphere(ps, 0.5f); Bursts(ps, 60); Stretch(ps, 1.6f, 0.04f); Grad(ps, Holy, KnightGold, 1f); ps.Play();
            FX.Dust(pos, U.Hex("c8b090"), 2f);
            FX.FlashLight(pos + Vector3.up * 1.5f, KnightGold, 7f, r + 6f, 0.5f);
        }

        static void KnBladestorm(Vector3 pos, Vector3 dir, float r)
        {
            FX.SlashArc(pos + Vector3.up * 1.05f, dir, Steel, r * 0.95f, 330f, false, 0.22f, 0.7f, 5);
            FX.SlashArc(pos + Vector3.up * 0.7f, -dir, KnightGold, r * 0.8f, 280f, false, 0.2f, 0.4f, 4);
            var ps = Sys("tempestade_faiscas", pos + Vector3.up * 0.9f, KnightGold, true, 0.2f, 0.4f, 5f, 9f, 0.03f, 0.07f, 0.8f);
            FlatCircle(ps, r * 0.6f, 0f); Bursts(ps, 10); Stretch(ps, 1.6f, 0.04f); Grad(ps, Color.white, KnightGold, 1f); ps.Play();
            var dust = Sys("tempestade_poeira", pos + Vector3.up * 0.1f, new Color(0.8f, 0.72f, 0.6f, 0.35f), false, 0.3f, 0.6f, 2.5f, 4.5f, 0.3f, 0.6f, -0.05f);
            FlatCircle(dust, r * 0.5f, 0f); Bursts(dust, 6);
            FxUtil.AlphaFade(dust, new Color(0.85f, 0.78f, 0.65f), new Color(0.6f, 0.52f, 0.44f), 0.35f); dust.Play();
            FX.FlashLight(pos + Vector3.up, KnightGold, 1.8f, r + 2f, 0.2f);
        }

        // ==================================================================================== PISTOLEIRO (latão / pólvora)
        static Vector3 Gun(Vector3 pos, Vector3 dir) => pos + Vector3.up * 1.1f + dir * 0.7f;

        static void GsFan(Vector3 pos, Vector3 dir)
        {
            Vector3 g = Gun(pos, dir);
            MuzzleFlash(g, dir);
            ConeBurst(g, dir, Muzzle, 40f, 6f, 12f, 26, 0.2f);
            var sm = Sys("leque_fumaca", g, new Color(0.7f, 0.68f, 0.65f, 0.4f), false, 0.5f, 0.9f, 1f, 2.5f, 0.3f, 0.6f, -0.1f);
            sm.transform.rotation = Quaternion.LookRotation(dir);
            Cone(sm, 45f, 0.1f); Bursts(sm, 10);
            FxUtil.AlphaFade(sm, new Color(0.85f, 0.82f, 0.78f), new Color(0.5f, 0.48f, 0.46f), 0.4f); sm.Play();
        }

        static void GsDodge(Vector3 pos, Vector3 dir)
        {
            FX.Dust(pos, U.Hex("d8d0c0"), 0.9f);
            Afterimage(PlayerVisual(), Brass, 0.35f);
            var ps = Sys("rolar_vento", pos + Vector3.up * 0.6f, Muzzle, true, 0.15f, 0.3f, 6f, 10f, 0.03f, 0.06f);
            ps.transform.rotation = Quaternion.LookRotation(dir);
            Cone(ps, 10f, 0.5f); Bursts(ps, 14); Stretch(ps, 3f); Grad(ps, Color.white, Brass, 0.8f); ps.Play();
        }

        static void GsExplosiveCast(Vector3 pos, Vector3 dir)
        {
            Vector3 g = Gun(pos, dir);
            MuzzleFlash(g, dir);
            WindRing(g + dir * 0.3f, dir, Fire, 0.35f, 22);
            ConeBurst(g, dir, Fire, 20f, 5f, 10f, 20, 0.4f);
            Embers(g, 0.2f, Ember, 10, 0.5f, 1.5f);
            FX.FlashLight(g, Fire, 3.5f, 4.5f, 0.15f);
        }

        static void GsQuickdraw(Vector3 pos)
        {
            RuneCircle(pos, 1.5f, Brass, 0.8f, 540f);
            Spiral(pos, 0.7f, Brass, 0.8f, 2.4f, 40);
            var ps = Sys("saque_faiscas", pos + Vector3.up * 1f, Muzzle, true, 0.2f, 0.45f, 3f, 6f, 0.04f, 0.08f, 0.8f);
            FlatCircle(ps, 0.4f, 0f); Bursts(ps, 24); Stretch(ps, 1.6f, 0.04f); Grad(ps, Color.white, Brass, 1f); ps.Play();
            FX.Ring(pos, 1.6f, Brass, 0.3f, 0.3f);
            FX.FlashLight(pos + Vector3.up, Brass, 3f, 4.5f, 0.3f);
        }

        static void GsRicochetCast(Vector3 pos, Vector3 dir)
        {
            Vector3 g = Gun(pos, dir);
            MuzzleFlash(g, dir);
            WindRing(g + dir * 0.3f, dir, Steel, 0.3f, 16);
            Sparks(g, Steel, 10, 3f, 6f, 0.6f);
        }

        static void GsDeadeyeStart(Vector3 pos, Vector3 dir)
        {
            Vector3 eye = pos + Vector3.up * 1.5f;
            Implode(eye, 1.4f, Brass, 0.3f, 24);
            RuneCircle(pos, 2f, Brass, 1f, -90f);
            DelayedRing(pos, 3.5f, Brass, 0.6f, 0f);
            var ps = Sys("olho_brilho", eye + dir * 0.2f, Color.white, true, 0.6f, 0.8f, 0f, 0f, 0.6f, 0.8f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            Bursts(ps, 1); Grad(ps, Color.white, Brass, 1f); ps.Play();
            FX.FlashLight(eye, Brass, 2f, 4f, 0.6f);
        }

        static void GsDeadeyeHit(Vector3 tp, Vector3 dir)
        {
            Vector3 o = PlayerChest();
            if (o == Vector3.zero) o = tp - dir * 6f;
            Beam(o, tp, Brass, 0.1f, 0.18f);
            Beam(o, tp, new Color(Brass.r, Brass.g, Brass.b, 0.35f), 0.4f, 0.14f);
            ImpactPhysical(tp, Brass, 1f);
            var ps = Sys("olho_acerto", tp, Color.white, true, 0.1f, 0.15f, 0f, 0f, 1f, 1.3f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            Bursts(ps, 1); Grad(ps, Color.white, Brass, 1f); ps.Play();
        }

        // ==================================================================================== ARQUEIRO SUPERIOR (verde / vento pesado)
        static void LsPowerCast(Vector3 pos, Vector3 dir)
        {
            Vector3 m = pos + Vector3.up * 1.1f + dir * 0.8f;
            WindRing(m, dir, Gold, 0.6f, 40);
            WindRing(m + dir * 0.9f, dir, HunterGreen, 0.45f, 28);
            WindRing(m + dir * 1.8f, dir, Wind, 0.3f, 18);
            ConeBurst(m, dir, Wind, 15f, 9f, 16f, 30, 0f);
            Leaves(m, dir, 14);
            FX.Dust(pos - dir * 0.3f, U.Hex("d8e0c8"), 1f);
            FX.FlashLight(m, Gold, 4.5f, 5f, 0.25f);
        }

        static void LsPinCast(Vector3 pos, Vector3 dir)
        {
            Vector3 m = pos + Vector3.up * 1.1f + dir * 0.8f;
            WindRing(m, dir, ShadowPurple, 0.4f, 22);
            Sparks(m, ShadowPurple, 10, 2f, 5f, 0.4f);
            FX.FlashLight(m, ShadowPurple, 2.5f, 3.5f, 0.2f);
        }

        static void LsSplitCast(Vector3 pos, Vector3 dir)
        {
            Vector3 m = pos + Vector3.up * 1.1f + dir * 0.8f;
            WindRing(m, dir, HunterGreen, 0.45f, 26);
            ConeBurst(m, dir, Leaf, 30f, 5f, 10f, 18, 0f);
            Leaves(m, dir, 8);
            FX.FlashLight(m, HunterGreen, 2.5f, 4f, 0.2f);
        }

        static void LsVolleyCast(Vector3 pos, Vector3 dir)
        {
            var ps = Sys("saraivada_sobe", pos + Vector3.up * 1.4f + dir * 0.4f, HunterGreen, true, 0.4f, 0.6f, 20f, 28f, 0.07f, 0.12f);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(ps, 12f, 0.2f); Bursts(ps, 20); Stretch(ps, 5f); Grad(ps, Color.white, HunterGreen, 1f); ps.Play();
            WindRing(pos + Vector3.up * 1.6f, Vector3.up, Wind, 0.5f, 24);
            FX.Dust(pos, U.Hex("d8e0c8"), 0.8f);
            FX.FlashLight(pos + Vector3.up * 2f, HunterGreen, 3f, 5f, 0.3f);
        }

        static void LsVolleyDrop(Vector3 p, float r)
        {
            Vector3 top = p + new Vector3(Random.Range(-0.5f, 0.5f), 7f, Random.Range(-0.5f, 0.5f));
            Beam(top, p, new Color(0.85f, 1f, 0.85f), 0.1f, 0.14f);
            Beam(top, p, new Color(HunterGreen.r, HunterGreen.g, HunterGreen.b, 0.35f), 0.38f, 0.12f);
            StuckArrow(p, HunterGreen);
            Sparks(p + Vector3.up * 0.1f, Leaf, 8, 2f, 4.5f, 1.2f);
            FX.Dust(p, U.Hex("c8c0a0"), 0.6f);
            FX.Ring(p, Mathf.Max(0.8f, r), HunterGreen, 0.25f, 0.2f);
        }

        static void LsFocus(Vector3 pos)
        {
            RuneCircle(pos, 1.6f, HunterGreen, 1f, 60f);
            Implode(pos + Vector3.up * 1.4f, 1.8f, Wind, 0.3f, 26);
            LightColumn(pos, 0.5f, 4f, HunterGreen, 0.6f);
            Leaves(pos + Vector3.up * 0.3f, Vector3.up, 12);
            FX.FlashLight(pos + Vector3.up * 1.4f, HunterGreen, 3f, 4.5f, 0.4f);
        }

        static void LsSkyfallStart(Vector3 t, Vector3 aim, float r)
        {
            SpawnGiantArrow(t - aim * 5f + Vector3.up * 16f, t + Vector3.up * 0.3f, 0.7f);
            RuneCircle(t, r * 1.05f, HunterGreen, 0.9f, 120f);
            LightColumn(t, r * 0.25f, 12f, HunterGreen, 0.8f);
        }

        static void LsSkyfallHit(Vector3 t, float r)
        {
            GiantArrowImpact(t, r);
            Crack(t, r * 2.1f, HunterGreen, 3f, Random.Range(0f, 360f));
            DelayedRing(t, r * 2.4f, Wind, 0.6f, 0.15f);
            Rocks(t, r * 0.35f, U.Hex("7a6a5a"), 14, 1.1f);
        }

        // ==================================================================================== SACERDOTE (luz dourada)
        static void PrHealCast(Vector3 pos, float r)
        {
            RuneCircle(pos, 2f, Gold, 1.1f, 60f);
            LightColumn(pos, 0.9f, 5f, Holy, 0.8f);
            DelayedRing(pos, r, Heal, 0.6f, 0f);
            DelayedRing(pos, r * 0.7f, Gold, 0.5f, 0.1f);
            Feathers(pos + Vector3.up * 2.5f, 14);
            FX.FlashLight(pos + Vector3.up * 1.5f, Holy, 4f, r, 0.7f);
        }

        static void PrHealHit(Vector3 pos)
        {
            Spiral(pos, 0.7f, Heal, 1f, 2.5f, 30);
            var ps = Sys("cura_cruz", pos + Vector3.up * 1.5f, Heal, true, 0.6f, 0.9f, 0.6f, 1.2f, 0.25f, 0.4f, -0.1f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Cross());
            Sphere(ps, 0.5f); Bursts(ps, 5); Grad(ps, Color.white, Heal, 1f); ps.Play();
            FX.Sparkle(pos + Vector3.up * 0.4f, Color.Lerp(Heal, Color.white, 0.5f), 0.9f, 0.5f);
            FX.Ring(pos, 1.1f, Heal, 0.4f, 0.4f);
        }

        static void PrBlessCast(Vector3 pos, float r)
        {
            LightColumn(pos, 0.7f, 5f, Gold, 0.7f);
            DelayedRing(pos, r, Gold, 0.6f, 0f);
            var ps = Sys("bencao_explode", pos + Vector3.up * 1.2f, Gold, true, 0.4f, 0.8f, 3f, 6f, 0.06f, 0.14f, -0.3f);
            Sphere(ps, 0.4f); Bursts(ps, 40); Grad(ps, Color.white, Gold, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up * 1.5f, Gold, 4f, r, 0.5f);
        }

        static void PrBlessHit(Vector3 pos)
        {
            Spiral(pos, 0.6f, Gold, 0.9f, 2.6f, 26);
            var ps = Sys("bencao_forca", pos + Vector3.up * 1.2f, Fire, true, 0.3f, 0.5f, 1f, 2.5f, 0.08f, 0.16f, -0.5f);
            Sphere(ps, 0.4f); Bursts(ps, 14); Grad(ps, Holy, Gold, 1f); ps.Play();
            FX.Ring(pos, 1f, Gold, 0.35f, 0.4f);
        }

        static void PrAegisCast(Vector3 pos, float r)
        {
            Bubble(pos, 2f, Gold, 0.6f);
            DelayedRing(pos, r, Holy, 0.6f, 0f);
            RuneCircle(pos, 1.8f, Gold, 0.9f, -70f);
            FX.FlashLight(pos + Vector3.up, Gold, 4f, r, 0.5f);
        }

        static void PrAegisHit(Vector3 pos)
        {
            var ps = Sys("egide_brilho", pos + Vector3.up * 1f, Gold, true, 0.3f, 0.6f, 1.5f, 3f, 0.05f, 0.11f, 0f);
            Sphere(ps, 0.9f); var sh = ps.shape; sh.radiusThickness = 0f;
            Bursts(ps, 20); Grad(ps, Color.white, Gold, 1f); ps.Play();
            FX.Ring(pos, 1.3f, Gold, 0.35f, 0.8f);
        }

        static void PrSanctuaryStart(Vector3 c, float r)
        {
            var z = ZoneFx.Create("pr_sanctuary", c, 30f);
            var rune = FX.FlatQuad("santuario_runas", c + Vector3.up * 0.05f, RuneTexture(), new Color(Gold.r, Gold.g, Gold.b, 0.55f), true);
            rune.transform.SetParent(z.transform, true);
            rune.transform.localScale = Vector3.one * r * 2f;
            z.AddRenderer(rune.GetComponent<Renderer>(), new Color(Gold.r, Gold.g, Gold.b, 0.55f), 0.3f);
            z.AddSpin(rune.transform, 20f);
            var rim = FX.FlatQuad("santuario_borda", c + Vector3.up * 0.06f, U.RingTexture(), new Color(Holy.r, Holy.g, Holy.b, 0.7f), true);
            rim.transform.SetParent(z.transform, true);
            rim.transform.localScale = Vector3.one * r * 2.05f;
            z.AddRenderer(rim.GetComponent<Renderer>(), new Color(Holy.r, Holy.g, Holy.b, 0.7f), 0.4f);
            var fill = FX.FlatQuad("santuario_luz", c + Vector3.up * 0.04f, U.SoftTexture(), new Color(Gold.r, Gold.g, Gold.b, 0.25f), true);
            fill.transform.SetParent(z.transform, true);
            fill.transform.localScale = Vector3.one * r * 2.2f;
            z.AddRenderer(fill.GetComponent<Renderer>(), new Color(Gold.r, Gold.g, Gold.b, 0.25f), 0f);
            var mot = z.Add(FxUtil.LoopSys("santuario_motas", z.transform, Vector3.up * 0.1f, Gold, true, 1.2f, 2f, 0.5f, 1.2f, 0.05f, 0.11f, -0.15f, 26f, 90));
            FlatCircle(mot, r * 0.95f, 1f); Noise(mot, 0.3f, 0.8f); Grad(mot, Color.white, Gold, 1f); mot.Play();
            var wall = z.Add(FxUtil.LoopSys("santuario_parede", z.transform, Vector3.up * 0.05f, Holy, true, 0.6f, 1f, 1.5f, 2.5f, 0.05f, 0.09f, 0f, 40f, 90));
            FlatCircle(wall, r, 0f);
            var sh = wall.shape; sh.rotation = new Vector3(-90f, 0f, 0f); sh.shapeType = ParticleSystemShapeType.Circle;
            var v = wall.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(0f); v.y = new ParticleSystem.MinMaxCurve(1.6f); v.z = new ParticleSystem.MinMaxCurve(0f);
            var mm = wall.main; mm.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            Stretch(wall, 2f, 0.1f); Grad(wall, Color.white, Gold, 0.8f); wall.Play();
            z.NewLight(Vector3.up * 1.5f, Gold, 2.2f, r * 1.8f, 0.15f);
            LightColumn(c, r * 0.5f, 6f, Holy, 0.7f);
            Shockwave(c, r, Gold, 0.4f);
            FX.FlashLight(c + Vector3.up, Holy, 4f, r + 3f, 0.5f);
        }

        static void PrSanctuaryTick(Vector3 c, float r)
        {
            if (ZoneFx.Find("pr_sanctuary", c, r) == null) PrSanctuaryStart(c, r);
            DelayedRing(c, r, new Color(Heal.r, Heal.g, Heal.b, 0.8f), 0.7f, 0f);
            Spiral(c, r * 0.4f, Heal, 0.8f, 2f, 16);
        }

        static void PrSmiteStart(Vector3 c, float r)
        {
            RuneCircle(c, r * 1.1f, Holy, 0.6f, 200f);
            Beam(c + Vector3.up * 12f, c, new Color(Holy.r, Holy.g, Holy.b, 0.6f), 0.15f, 0.35f);
            Implode(c + Vector3.up * 0.5f, r, Gold, 0.3f, 24);
        }

        static void PrSmite(Vector3 c, float r)
        {
            LightColumn(c, r * 0.7f, 12f, Holy, 0.7f);
            LightColumn(c, r * 0.35f, 12f, Color.white, 0.55f);
            Crack(c, r * 1.8f, Gold, 2.2f, Random.Range(0f, 360f));
            Shockwave(c, r * 1.3f, Gold, 0.4f);
            var ps = Sys("juizo_luz", c + Vector3.up * 0.3f, Gold, true, 0.4f, 0.8f, 3f, 8f, 0.06f, 0.14f, -0.2f);
            Hemisphere(ps, r * 0.4f); Bursts(ps, 50); Stretch(ps, 1.5f, 0.04f); Grad(ps, Color.white, Gold, 1f); ps.Play();
            Feathers(c + Vector3.up * 3f, 16);
            FX.FlashLight(c + Vector3.up * 2f, Holy, 8f, r + 6f, 0.5f);
        }

        static void PrResurrection(Vector3 pos, float r)
        {
            LightColumn(pos, 1.6f, 14f, Holy, 1.4f);
            LightColumn(pos, 0.7f, 14f, Gold, 1.2f);
            RuneCircle(pos, 3f, Gold, 1.6f, 40f);
            for (int i = 0; i < 4; i++) DelayedRing(pos, r * (0.35f + 0.22f * i), i % 2 == 0 ? Holy : Gold, 0.7f, i * 0.12f);
            Feathers(pos + Vector3.up * 4f, 40);
            Spiral(pos, 1.4f, Holy, 1.4f, 5f, 70);
            var ps = Sys("ressurreicao_sol", pos + Vector3.up * 2f, Holy, true, 0.5f, 1f, 4f, 9f, 0.08f, 0.2f, 0f);
            Sphere(ps, 0.5f); Bursts(ps, 70); Stretch(ps, 2f, 0.03f); Grad(ps, Color.white, Gold, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up * 2.5f, Holy, 9f, r + 4f, 1.2f);
        }

        static void PrResHit(Vector3 pos)
        {
            LightColumn(pos, 0.8f, 7f, Holy, 0.9f);
            Spiral(pos, 0.8f, Gold, 1.1f, 3f, 36);
            FX.Sparkle(pos + Vector3.up * 0.5f, Holy, 1.2f, 0.8f);
        }

        // ==================================================================================== ARQUI-MAGO (arcano)
        static void AmBarrage(Vector3 pos, Vector3 dir)
        {
            Vector3 hand = pos + Vector3.up * 1.3f + dir * 0.5f;
            RuneCircle(pos, 1.5f, Arcane, 0.7f, -260f);
            var ps = Sys("barragem", hand, Arcane, true, 0.25f, 0.5f, 2f, 5f, 0.06f, 0.14f);
            ps.transform.rotation = Quaternion.LookRotation(dir);
            Cone(ps, 50f, 0.2f); Bursts(ps, 30); Grad(ps, Color.white, Arcane, 1f); ps.Play();
            WindRing(hand, dir, Arcane, 0.5f, 24);
            FX.FlashLight(hand, Arcane, 3f, 4.5f, 0.25f);
        }

        static void AmOverload(Vector3 pos)
        {
            Implode(pos + Vector3.up * 1f, 2.2f, Arcane, 0.3f, 40);
            RuneCircle(pos, 2f, ArcaneDeep, 0.9f, 300f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 90f + Random.Range(-25f, 25f), 0f) * Vector3.forward;
                FX.Lightning(pos + d * 2f + Vector3.up * 0.1f, pos + Vector3.up * 1.4f, Arcane, 0.08f, 1, 0.25f);
            }
            Shockwave(pos, 2.4f, Arcane, 0.35f);
            FX.FlashLight(pos + Vector3.up, Arcane, 5f, 6f, 0.35f);
        }

        static void AmSingularityStart(Vector3 c, float r)
        {
            var z = ZoneFx.Create("am_singularity", c, 20f);
            Vector3 core = Vector3.up * 1.1f;
            var ball = FxUtil.Solid(PrimitiveType.Sphere, z.transform, core, Vector3.one * 0.7f, new Color(0.02f, 0f, 0.04f), 0f, 0.9f);
            z.AddShrink(ball.transform);
            var halo = FX.FlatQuad("singularidade_halo", c + core, U.SoftTexture(), new Color(Arcane.r, Arcane.g, Arcane.b, 0.8f), true);
            halo.transform.SetParent(z.transform, true);
            halo.transform.localScale = Vector3.one * 2f;
            halo.AddComponent<FxBillboard>();
            z.AddRenderer(halo.GetComponent<Renderer>(), new Color(Arcane.r, Arcane.g, Arcane.b, 0.8f), 0.5f);
            var disk = FX.FlatQuad("singularidade_disco", c + core, FxTex.Swirl(), new Color(ArcaneDeep.r, ArcaneDeep.g, ArcaneDeep.b, 0.9f), true);
            disk.transform.SetParent(z.transform, true);
            disk.transform.localScale = Vector3.one * r * 1.6f;
            z.AddRenderer(disk.GetComponent<Renderer>(), new Color(ArcaneDeep.r, ArcaneDeep.g, ArcaneDeep.b, 0.9f), 0.2f);
            z.AddSpin(disk.transform, -320f);
            var floor = FX.FlatQuad("singularidade_chao", c + Vector3.up * 0.05f, FxTex.Swirl(), new Color(Arcane.r, Arcane.g, Arcane.b, 0.5f), true);
            floor.transform.SetParent(z.transform, true);
            floor.transform.localScale = Vector3.one * r * 2.2f;
            z.AddRenderer(floor.GetComponent<Renderer>(), new Color(Arcane.r, Arcane.g, Arcane.b, 0.5f), 0.3f);
            z.AddSpin(floor.transform, -150f);
            float life = 0.7f;
            var pull = z.Add(FxUtil.LoopSys("singularidade_puxa", z.transform, core, Arcane, true, life, life, -r / life, -r / life * 0.85f, 0.05f, 0.12f, 0f, 70f, 120));
            Sphere(pull, r); var sh = pull.shape; sh.radiusThickness = 0f;
            Stretch(pull, 1.6f, 0.03f); Grad(pull, ArcaneDeep, Color.white, 1f);
            var szo = pull.sizeOverLifetime; szo.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1f));
            pull.Play();
            var dust = z.Add(FxUtil.LoopSys("singularidade_poeira", z.transform, Vector3.up * 0.15f, new Color(0.25f, 0.15f, 0.35f, 0.5f), false, 1f, 1.4f, 0f, 0f, 0.3f, 0.6f, 0f, 16f, 40));
            FlatCircle(dust, r, 0.3f);
            FxUtil.Orbit(dust, -2.5f, 0f);
            var dv = dust.velocityOverLifetime; dv.radial = new ParticleSystem.MinMaxCurve(-r * 0.7f);
            FxUtil.AlphaFade(dust, new Color(0.4f, 0.25f, 0.55f), new Color(0.1f, 0.05f, 0.15f), 0.5f); dust.Play();
            z.NewLight(core, Arcane, 3f, r * 2f, 0.35f);
            Implode(c + core, r, Arcane, 0.3f, 40);
            Shockwave(c, r, ArcaneDeep, 0.35f);
        }

        static void AmSingularityTick(Vector3 c, float r)
        {
            if (ZoneFx.Find("am_singularity", c, r) == null) AmSingularityStart(c, r);
            // anel que encolhe (puxão)
            var q = FX.FlatQuad("singularidade_anel", c + Vector3.up * 0.06f, U.RingTexture(), Arcane, true);
            q.AddComponent<FxTween>().Set(0.5f, Vector3.one * r * 2.2f, Vector3.one * 0.3f, Arcane, 0.9f, 0f);
        }

        static void AmSingularityEnd(Vector3 c, float r)
        {
            var z = ZoneFx.Find("am_singularity", c, r);
            if (z != null) z.End(0.25f);
            Vector3 core = c + Vector3.up * 1.1f;
            var ps = Sys("singularidade_explode", core, Arcane, true, 0.3f, 0.7f, 5f, 11f, 0.1f, 0.24f, 0.2f);
            Sphere(ps, 0.3f); Bursts(ps, 70); Stretch(ps, 1.8f, 0.04f); Grad(ps, Color.white, ArcaneDeep, 1f); ps.Play();
            Shockwave(c, r * 1.3f, Arcane, 0.4f);
            RuneCircle(c, r, ArcaneDeep, 0.6f, 300f);
            FX.FlashLight(core, Arcane, 7f, r + 5f, 0.4f);
        }

        static void AmManaShield(Vector3 pos)
        {
            Bubble(pos, 1.7f, new Color(0.45f, 0.62f, 1f), 0.6f);
            RuneCircle(pos, 1.7f, Arcane, 1f, -120f);
            Spiral(pos, 0.9f, new Color(0.55f, 0.7f, 1f), 0.9f, 2.5f, 40);
            FX.FlashLight(pos + Vector3.up, new Color(0.5f, 0.6f, 1f), 3.5f, 5f, 0.4f);
        }

        static void AmFrostNova(Vector3 pos, float r)
        {
            Shockwave(pos, r, Ice, 0.4f);
            DelayedRing(pos, r * 0.7f, Color.white, 0.35f, 0.05f);
            Crack(pos, r * 1.9f, Ice, 2.4f, Random.Range(0f, 360f));
            GroundSpikes(pos, r * 0.75f, 14, 1.3f, U.Fx(false, U.WhiteTexture()), new Color(0.72f, 0.92f, 1f, 0.6f), true);
            GroundSpikes(pos, r * 0.45f, 8, 0.9f, U.Fx(true, U.WhiteTexture()), new Color(0.5f, 0.85f, 1f, 0.35f), true);
            var ps = Sys("nova_estilhacos", pos + Vector3.up * 0.4f, Ice, true, 0.4f, 0.8f, r * 1.6f, r * 2.4f, 0.1f, 0.2f, 0.6f);
            FlatCircle(ps, 0.4f, 0f);
            MeshParticles(ps, U.Fx(true, U.WhiteTexture()));
            var m = ps.main; m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(0.05f, 0.1f); m.startSizeY = new ParticleSystem.MinMaxCurve(0.05f, 0.1f); m.startSizeZ = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            Bursts(ps, 40); Grad(ps, Color.white, Ice, 1f); ps.Play();
            Mist(pos + Vector3.up * 0.3f, Ice, 24, r * 0.6f);
            FX.FlashLight(pos + Vector3.up, Ice, 6f, r + 4f, 0.45f);
        }

        static void AmCataclysmCast(Vector3 pos)
        {
            RuneCircle(pos, 2.4f, Fire, 1.2f, 120f);
            LightColumn(pos, 0.8f, 10f, FireDeep, 0.9f);
            Implode(pos + Vector3.up * 1.2f, 2.5f, Fire, 0.3f, 36);
            Embers(pos, 1f, Ember, 30, 1f, 4f);
            FX.FlashLight(pos + Vector3.up * 1.5f, Fire, 5f, 7f, 0.6f);
        }

        static void AmCataclysmDrop(Vector3 p, float r)
        {
            Vector3 top = p + new Vector3(-2.5f, 10f, -2.5f) + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
            Beam(top, p, FireCore, 0.35f, 0.16f);
            Beam(top, p, new Color(Fire.r, Fire.g, Fire.b, 0.4f), 1f, 0.14f);
            var fire = Sys("cataclismo_fogo", p + Vector3.up * 0.3f, Fire, true, 0.3f, 0.6f, 3f, 7f, 0.3f, 0.7f, -0.3f);
            Hemisphere(fire, 0.3f); Bursts(fire, 26); Noise(fire, 1.2f, 1.5f); FxUtil.FireGrad(fire); fire.Play();
            Embers(p, r * 0.3f, Ember, 16, 1f, 3.5f);
            Smoke(p, r * 0.3f, 6, 1f);
            Crack(p, r * 1.6f, Fire, 1.6f, Random.Range(0f, 360f));
            Shockwave(p, r, Fire, 0.3f);
            Rocks(p, r * 0.25f, U.Hex("4a3a32"), 5, 0.9f);
            FX.FlashLight(p + Vector3.up, Fire, 4f, r + 4f, 0.3f);
        }

        // ==================================================================================== BERSERKER (sangue / fúria)
        static Color BkMain(bool f) => f ? Crimson : new Color(1f, 0.29f, 0.23f);

        static void BkCleave(Vector3 pos, Vector3 dir, float r, bool frenzy)
        {
            Vector3 chest = pos + Vector3.up * 1f;
            Color main = BkMain(frenzy);
            if (!frenzy)
            {
                // dois machados: corte direita→esquerda e, logo depois, esquerda→direita
                FX.SlashArc(chest + Vector3.up * 0.1f, dir, main, r, 150f, true, 0.2f, 0.85f, 16);
                FX.SlashArc(chest + Vector3.up * 0.14f, dir, Steel, r * 0.92f, 135f, true, 0.17f, 0.25f, 0);
                FxDelay.Run(0.09f, () =>
                {
                    FX.SlashArc(chest - Vector3.up * 0.15f, dir, main, r * 0.95f, 150f, false, 0.2f, 0.75f, 12);
                    BloodSpray(chest + dir * r * 0.6f, dir, 14, 35f, 5f);
                });
                FX.Dust(pos + dir * r * 0.5f, U.Hex("c8b090"), 0.8f);
                FX.FlashLight(chest + dir, main, 3f, r + 2f, 0.2f);
                return;
            }
            // Carnificina: dois giros completos vermelho-escuros, sangue para todo lado, chão rachado
            FX.SlashArc(chest + Vector3.up * 0.15f, dir, Crimson, r, 355f, true, 0.26f, 1.1f, 24);
            FX.SlashArc(chest - Vector3.up * 0.2f, -dir, BloodDark, r * 0.9f, 340f, false, 0.28f, 0.8f, 10);
            FX.SlashArc(chest, dir, new Color(1f, 0.6f, 0.5f), r * 1.05f, 330f, true, 0.22f, 0.18f, 0);
            for (int i = 0; i < 4; i++) BloodSpray(chest, Quaternion.Euler(0f, i * 90f + 45f, 0f) * dir + Vector3.up * 0.3f, 10, 40f, 6f);
            Crack(pos, r * 1.7f, Crimson, 2f, Random.Range(0f, 360f));
            Shockwave(pos, r * 1.15f, Crimson, 0.35f);
            var ps = Sys("carnificina_brasas", chest, Crimson, true, 0.3f, 0.6f, 5f, 10f, 0.05f, 0.11f, 0.8f);
            FlatCircle(ps, 0.6f, 0f); Bursts(ps, 40); Stretch(ps, 1.8f, 0.04f); Grad(ps, new Color(1f, 0.6f, 0.5f), Crimson, 1f); ps.Play();
            FX.FlashLight(chest, Crimson, 5f, r + 4f, 0.35f);
        }

        static void BkJump(Vector3 pos, Vector3 dir, bool frenzy)
        {
            Color main = BkMain(frenzy);
            FX.Dust(pos, U.Hex("c8b090"), frenzy ? 1.5f : 1.2f);
            FX.Ring(pos, 1.6f, main, 0.3f, 0.4f);
            Afterimage(PlayerVisual(), main, 0.4f);
            if (frenzy)
            {
                Crack(pos, 2.2f, Crimson, 1.4f, Random.Range(0f, 360f));
                BloodSpray(pos + Vector3.up * 0.3f, Vector3.up, 16, 45f, 4f);
            }
        }

        static void BkLand(Vector3 pos, float r, bool frenzy)
        {
            Color main = BkMain(frenzy);
            Crack(pos, r * (frenzy ? 2f : 1.6f), main, frenzy ? 3f : 2.2f, Random.Range(0f, 360f));
            Shockwave(pos, r * 1.2f, main, 0.4f);
            Rocks(pos, r * 0.45f, U.Hex("6a5a4a"), frenzy ? 22 : 14, frenzy ? 1.3f : 1f);
            FX.Dust(pos, U.Hex("c8b090"), frenzy ? 2.2f : 1.6f);
            Sparks(pos + Vector3.up * 0.3f, main, 26, 4f, 9f, 1.5f);
            if (frenzy)
            {
                // Queda do Abismo: cratera vermelha, gêiseres de sangue e anéis em sequência
                for (int i = 0; i < 3; i++) DelayedRing(pos, r * (0.7f + 0.35f * i), i == 1 ? BloodDark : Crimson, 0.5f, i * 0.08f);
                for (int i = 0; i < 5; i++)
                {
                    Vector3 o = pos + Quaternion.Euler(0f, i * 72f + Random.Range(-15f, 15f), 0f) * Vector3.forward * r * Random.Range(0.4f, 0.8f);
                    BloodSpray(o + Vector3.up * 0.1f, Vector3.up, 12, 15f, 7f);
                }
                LightColumn(pos, r * 0.4f, 6f, Crimson, 0.5f);
                var dk = Sys("abismo_fumaca", pos + Vector3.up * 0.4f, new Color(0.25f, 0.02f, 0.02f, 0.7f), false, 0.8f, 1.4f, 1f, 3f, 0.8f, 1.5f, -0.1f);
                Hemisphere(dk, r * 0.4f); Bursts(dk, 20);
                FxUtil.AlphaFade(dk, new Color(0.4f, 0.05f, 0.03f), new Color(0.08f, 0f, 0f), 0.7f); dk.Play();
                FX.FlashLight(pos + Vector3.up, Crimson, 8f, r + 6f, 0.5f);
            }
            else FX.FlashLight(pos + Vector3.up * 0.8f, main, 5f, r + 4f, 0.4f);
        }

        static void BkRoar(Vector3 pos, bool frenzy)
        {
            Color main = BkMain(frenzy);
            Vector3 head = pos + Vector3.up * 1.7f;
            for (int i = 0; i < 3; i++) DelayedRing(pos, 3f + i * 1.5f, i == 1 ? Ember : main, 0.45f, i * 0.1f);
            var ps = Sys("rugido", head, main, true, 0.3f, 0.6f, 4f, 8f, 0.1f, 0.25f, 0f);
            FlatCircle(ps, 0.3f, 0f); Bursts(ps, 40); Stretch(ps, 1.6f, 0.04f); Grad(ps, Color.white, main, 1f); ps.Play();
            FX.Dust(pos, U.Hex("c8b090"), 1.6f);
            if (frenzy)
            {
                // Sede de Sangue: vórtice de sangue entrando no Berserker
                Implode(pos + Vector3.up * 1f, 3.5f, Crimson, 0.4f, 60);
                var bl = Sys("sede_sangue", pos + Vector3.up * 1f, new Color(0.7f, 0.02f, 0.03f, 0.9f), false, 0.4f, 0.4f, -8f, -7f, 0.08f, 0.14f, 0f);
                Sphere(bl, 3.2f); var sh = bl.shape; sh.radiusThickness = 0f;
                Bursts(bl, 40); Stretch(bl, 1.6f, 0.05f);
                FxUtil.AlphaFade(bl, new Color(0.9f, 0.08f, 0.06f), new Color(0.4f, 0f, 0f), 0.9f); bl.Play();
                LightColumn(pos, 0.9f, 5f, Crimson, 0.7f);
                FX.FlashLight(pos + Vector3.up * 1.5f, Crimson, 6f, 8f, 0.6f);
            }
            else
            {
                LightColumn(pos, 0.8f, 4.5f, main, 0.6f);
                FX.FlashLight(pos + Vector3.up * 1.5f, main, 5f, 7f, 0.45f);
            }
        }

        static void BkWhirl(Vector3 pos, Vector3 dir, float r, bool frenzy)
        {
            Color main = BkMain(frenzy);
            FX.SlashArc(pos + Vector3.up * 1.05f, dir, main, r * 0.95f, 330f, false, 0.22f, frenzy ? 0.85f : 0.65f, 5);
            FX.SlashArc(pos + Vector3.up * 0.7f, -dir, frenzy ? BloodDark : Steel, r * 0.8f, 300f, false, 0.2f, 0.45f, 4);
            if (frenzy)
            {
                // Tornado Rubro: espiral subindo e respingos para fora
                var sp = Sys("tornado_rubro", pos + Vector3.up * 0.1f, Crimson, true, 0.4f, 0.6f, 0f, 0f, 0.1f, 0.22f, 0f, 0.15f);
                FlatCircle(sp, r * 0.6f, 0f); Rate(sp, 120f);
                var v = sp.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
                v.x = new ParticleSystem.MinMaxCurve(0f); v.y = new ParticleSystem.MinMaxCurve(5f); v.z = new ParticleSystem.MinMaxCurve(0f);
                v.orbitalY = new ParticleSystem.MinMaxCurve(-9f);
                Grad(sp, new Color(1f, 0.55f, 0.45f), BloodDark, 1f); sp.Play();
                BloodSpray(pos + Vector3.up * 1f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward, 8, 50f, 5f);
                FX.FlashLight(pos + Vector3.up, Crimson, 2.5f, r + 3f, 0.22f);
            }
            else
            {
                var ps = Sys("redemoinho_faiscas", pos + Vector3.up * 0.9f, main, true, 0.2f, 0.4f, 4f, 8f, 0.03f, 0.07f, 0.8f);
                FlatCircle(ps, r * 0.6f, 0f); Bursts(ps, 8); Stretch(ps, 1.6f, 0.04f); Grad(ps, Color.white, main, 1f); ps.Play();
                FX.FlashLight(pos + Vector3.up, main, 1.8f, r + 2f, 0.2f);
            }
            var dust = Sys("redemoinho_poeira", pos + Vector3.up * 0.1f, new Color(0.8f, 0.72f, 0.6f, 0.35f), false, 0.3f, 0.6f, 2.5f, 4.5f, 0.3f, 0.6f, -0.05f);
            FlatCircle(dust, r * 0.5f, 0f); Bursts(dust, 5);
            FxUtil.AlphaFade(dust, new Color(0.85f, 0.78f, 0.65f), new Color(0.6f, 0.52f, 0.44f), 0.35f); dust.Play();
        }

        static void BkThrow(Vector3 pos, Vector3 dir, bool frenzy)
        {
            Color main = BkMain(frenzy);
            Vector3 chest = pos + Vector3.up * 1.1f;
            FX.SlashArc(chest, dir, main, 1.6f, 120f, true, 0.16f, 0.5f, 8);
            if (frenzy)
            {
                FX.SlashArc(chest - Vector3.up * 0.15f, dir, BloodDark, 1.5f, 120f, false, 0.18f, 0.45f, 6);
                BloodSpray(chest + dir * 0.6f, dir, 12, 30f, 6f);
            }
            WindRing(chest + dir * 0.8f, dir, main, 0.4f, 18);
            FX.FlashLight(chest + dir, main, 2.5f, 4f, 0.2f);
        }

        static void BkRage(Vector3 pos)
        {
            LightColumn(pos, 1.2f, 8f, Crimson, 1f);
            LightColumn(pos, 0.5f, 8f, BloodDark, 0.9f);
            Crack(pos, 4f, Crimson, 3f, Random.Range(0f, 360f));
            Shockwave(pos, 4f, Crimson, 0.45f);
            for (int i = 0; i < 3; i++) DelayedRing(pos, 2.5f + i * 1.2f, i == 1 ? BloodDark : Crimson, 0.5f, 0.1f + i * 0.1f);
            Implode(pos + Vector3.up * 1f, 3f, Crimson, 0.3f, 50);
            BloodSpray(pos + Vector3.up * 1f, Vector3.zero, 30, 0f, 5f);
            var ps = Sys("furia_desperta", pos + Vector3.up * 0.2f, Crimson, true, 0.5f, 0.9f, 3f, 7f, 0.2f, 0.45f, -0.4f);
            Hemisphere(ps, 0.6f); Bursts(ps, 50); Noise(ps, 1f, 1.5f); Grad(ps, new Color(1f, 0.6f, 0.45f), BloodDark, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up * 1.5f, Crimson, 8f, 9f, 0.7f);
        }

        // ==================================================================================== THE GUARD (aço azul + ouro; sombras)
        static void GdRetribution(Vector3 pos, Vector3 dir, float r)
        {
            Vector3 chest = pos + Vector3.up * 1f;
            FX.SlashArc(chest, dir, GuardBlue, r, 160f, true, 0.2f, 1f, 16);
            FX.SlashArc(chest + Vector3.up * 0.06f, dir, Gold, r * 0.9f, 140f, true, 0.18f, 0.3f, 0);
            // escudo de luz na frente (estalo) + jato dourado + rachadura em leque
            var sh = Sys("retribuicao_escudo", chest + dir * 0.8f, GuardBlue, true, 0.15f, 0.2f, 0f, 0f, 1.6f, 1.9f);
            sh.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, RuneTexture());
            Bursts(sh, 1); Grad(sh, Color.white, GuardBlue, 0.9f); sh.Play();
            ConeBurst(chest + dir * 0.6f, dir, Gold, 35f, 6f, 12f, 34, 0.4f);
            Crack(pos + dir * r * 0.55f, r * 1.3f, GuardBlue, 1.6f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0.55f);
            Shockwave(pos + dir * r * 0.5f, r * 0.8f, GuardBlue, 0.3f);
            FX.FlashLight(chest + dir * 1.2f, GuardBlue, 5f, r + 3f, 0.3f);
        }

        static void GdShieldwall(Vector3 pos, float r)
        {
            // anel de painéis de luz subindo em volta (muralha)
            var holder = new GameObject("muralha");
            holder.transform.SetParent(FX.Root, false);
            holder.transform.position = pos;
            int n = 10;
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 360f / n, 0f) * Vector3.forward;
                for (int side = 0; side < 2; side++)
                {
                    var q = FxUtil.Shape(PrimitiveType.Quad, holder.transform, d * 1.8f + Vector3.up * 0.8f, new Vector3(1.15f, 1.6f, 1f));
                    q.transform.localRotation = Quaternion.LookRotation(side == 0 ? d : -d, Vector3.up);
                    var rr = q.GetComponent<Renderer>();
                    rr.sharedMaterial = U.Fx(true, StripeTexture());
                    var tw = q.AddComponent<FxTween>();
                    tw.fadeIn = 0.1f; tw.hold = 0.5f;
                    tw.Set(0.9f, new Vector3(1.15f, 0.2f, 1f), new Vector3(1.15f, 1.6f, 1f), new Color(GuardBlue.r, GuardBlue.g, GuardBlue.b, 0.55f), 1f, 0f);
                }
            }
            Object.Destroy(holder, 1.2f);
            RuneCircle(pos, 2f, Gold, 1f, 45f);
            DelayedRing(pos, r, GuardBlue, 0.6f, 0f);
            Shockwave(pos, 2.2f, Steel, 0.35f);
            FX.FlashLight(pos + Vector3.up, GuardBlue, 4f, r + 2f, 0.5f);
        }

        static void GdShieldHit(Vector3 pos)
        {
            FX.Ring(pos, 1.2f, GuardBlue, 0.35f, 0.7f);
            var ps = Sys("muralha_brilho", pos + Vector3.up * 1.1f, Steel, true, 0.2f, 0.3f, 0f, 0f, 0.4f, 0.6f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            Sphere(ps, 0.6f); Bursts(ps, 3); Grad(ps, Color.white, GuardBlue, 1f); ps.Play();
            FX.Sparkle(pos + Vector3.up * 0.3f, Gold, 0.6f, 0.3f);
        }

        static void GdStepStart(Vector3 pos, Vector3 dir)
        {
            // a sombra do Guard "acorda" aos pés dele
            var pool = FX.FlatQuad("guarda_sombra", pos + Vector3.up * 0.03f, U.SoftTexture(), new Color(0.04f, 0f, 0.07f, 0.85f), false);
            pool.AddComponent<FxTween>().Set(0.7f, Vector3.one * 0.6f, Vector3.one * 2.4f, new Color(0.04f, 0f, 0.07f, 0.85f), 1f, 0f);
            var wisp = Sys("guarda_fiapos", pos + Vector3.up * 0.1f, new Color(0.1f, 0.03f, 0.16f, 0.75f), false, 0.5f, 0.9f, 0.6f, 1.6f, 0.3f, 0.5f, -0.2f);
            FlatCircle(wisp, 0.6f, 0.5f); Bursts(wisp, 16); Noise(wisp, 0.5f, 1.2f);
            FxUtil.AlphaFade(wisp, new Color(0.3f, 0.12f, 0.45f), new Color(0.02f, 0f, 0.04f), 0.75f); wisp.Play();
            FX.Ring(pos, 1.3f, ShadowPurple, 0.3f, 0.3f);
            Sparks(pos + Vector3.up * 1f + dir * 0.4f, Gold, 8, 1.5f, 3.5f, 0.5f);
            FX.FlashLight(pos + Vector3.up * 0.5f, ShadowPurple, 2.5f, 3.5f, 0.3f);
        }

        static void GdStepHit(Vector3 pos)
        {
            // as correntes travam: tinido de aço e estouro de sombra
            Vector3 c = pos + Vector3.up * 1f;
            Sparks(c, Steel, 16, 2f, 5f, 1f);
            Sparks(c, Gold, 8, 1.5f, 4f, 0.8f);
            Implode(c, 1.6f, ShadowPurple, 0.2f, 24);
            var dk = Sys("prende_sombra", pos + Vector3.up * 0.2f, new Color(0.1f, 0.02f, 0.15f, 0.8f), false, 0.4f, 0.8f, 1.5f, 3.5f, 0.4f, 0.8f, -0.2f);
            dk.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(dk, 25f, 0.8f); Bursts(dk, 18);
            FxUtil.AlphaFade(dk, new Color(0.3f, 0.12f, 0.45f), new Color(0.02f, 0f, 0.04f), 0.8f); dk.Play();
            FX.Ring(pos, 1.8f, ShadowPurple, 0.3f, 0.2f);
            FX.FlashLight(c, ShadowPurple, 3.5f, 4.5f, 0.3f);
        }

        static void GdBannerStart(Vector3 c, float r)
        {
            var z = ZoneFx.Create("gd_banner", c, 30f);
            var banner = new GameObject("estandarte").transform;
            banner.SetParent(z.transform, false);
            var steel = U.Lit(new Color(0.7f, 0.74f, 0.8f), 0.8f);
            var gold = U.Lit(Gold, 0.8f, Gold * 0.6f);
            FxUtil.Solid(PrimitiveType.Cylinder, banner, new Vector3(0f, 1.6f, 0f), new Vector3(0.09f, 1.6f, 0.09f), Color.white, 0f).GetComponent<Renderer>().sharedMaterial = steel;
            FxUtil.Solid(PrimitiveType.Cube, banner, new Vector3(0f, 3.05f, 0f), new Vector3(1.05f, 0.07f, 0.07f), Color.white, 0f).GetComponent<Renderer>().sharedMaterial = gold;
            var tip = FxUtil.Solid(PrimitiveType.Cube, banner, new Vector3(0f, 3.35f, 0f), new Vector3(0.16f, 0.32f, 0.16f), Color.white, 0f);
            tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            tip.GetComponent<Renderer>().sharedMaterial = U.Lit(Gold, 0.9f, Gold * 2f);
            var cloth = new GameObject("pano").transform;
            cloth.SetParent(banner, false);
            cloth.localPosition = new Vector3(0f, 3f, 0f);
            FxUtil.Solid(PrimitiveType.Cube, cloth, new Vector3(0f, -0.7f, 0f), new Vector3(0.95f, 1.4f, 0.04f), GuardBlue, 0.35f, 0.2f);
            FxUtil.Solid(PrimitiveType.Cube, cloth, new Vector3(0f, -1.45f, 0f), new Vector3(0.95f, 0.1f, 0.05f), Gold, 0.5f, 0.6f);
            var emblem = FxUtil.Solid(PrimitiveType.Cube, cloth, new Vector3(0f, -0.65f, -0.03f), new Vector3(0.32f, 0.32f, 0.02f), Gold, 1.5f, 0.8f);
            emblem.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            cloth.gameObject.AddComponent<FxSway>();
            z.AddShrink(banner);
            // aura no chão
            var rune = FX.FlatQuad("estandarte_runas", c + Vector3.up * 0.05f, RuneTexture(), new Color(GuardBlue.r, GuardBlue.g, GuardBlue.b, 0.45f), true);
            rune.transform.SetParent(z.transform, true);
            rune.transform.localScale = Vector3.one * r * 2f;
            z.AddRenderer(rune.GetComponent<Renderer>(), new Color(GuardBlue.r, GuardBlue.g, GuardBlue.b, 0.45f), 0.3f);
            z.AddSpin(rune.transform, 15f);
            var rim = FX.FlatQuad("estandarte_borda", c + Vector3.up * 0.06f, U.RingTexture(), new Color(Gold.r, Gold.g, Gold.b, 0.6f), true);
            rim.transform.SetParent(z.transform, true);
            rim.transform.localScale = Vector3.one * r * 2.05f;
            z.AddRenderer(rim.GetComponent<Renderer>(), new Color(Gold.r, Gold.g, Gold.b, 0.6f), 0.4f);
            var mot = z.Add(FxUtil.LoopSys("estandarte_motas", z.transform, Vector3.up * 0.1f, Gold, true, 1.2f, 2f, 0.4f, 1f, 0.05f, 0.1f, -0.1f, 22f, 80));
            FlatCircle(mot, r * 0.9f, 1f); Grad(mot, Color.white, Gold, 1f); mot.Play();
            var glow = z.Add(FxUtil.LoopSys("estandarte_brilho", z.transform, Vector3.up * 3.35f, GuardBlue, true, 0.5f, 0.9f, 0.2f, 0.6f, 0.08f, 0.16f, -0.1f, 12f, 30));
            Sphere(glow, 0.2f); Grad(glow, Color.white, GuardBlue, 1f); glow.Play();
            z.NewLight(Vector3.up * 3f, GuardBlue, 2f, r * 1.4f, 0.1f);
            // fincada
            Crack(c, 2.4f, Gold, 1.6f, Random.Range(0f, 360f));
            Shockwave(c, r * 0.8f, Gold, 0.4f);
            FX.Dust(c, U.Hex("c8b090"), 1.4f);
            Rocks(c, 0.4f, U.Hex("7a6a5a"), 8, 0.8f);
            FX.FlashLight(c + Vector3.up * 2f, Gold, 4f, r + 2f, 0.4f);
        }

        static void GdBannerTick(Vector3 c, float r)
        {
            if (ZoneFx.Find("gd_banner", c, r) == null) GdBannerStart(c, r);
            DelayedRing(c, r, new Color(Gold.r, Gold.g, Gold.b, 0.7f), 0.8f, 0f);
        }

        static void GdBannerEnd(Vector3 c, float r)
        {
            var z = ZoneFx.Find("gd_banner", c, r);
            if (z != null) z.End(0.4f);
            FX.Dust(c, U.Hex("c8b090"), 1f);
            var ps = Sys("estandarte_some", c + Vector3.up * 1.5f, Gold, true, 0.4f, 0.8f, 1f, 3f, 0.05f, 0.1f, -0.2f);
            Sphere(ps, 0.6f); Bursts(ps, 24); Grad(ps, Color.white, Gold, 1f); ps.Play();
        }

        static void GdReflect(Vector3 pos)
        {
            Bubble(pos, 1.6f, Steel, 0.45f);
            Shockwave(pos, 2.2f, Steel, 0.3f);
            var ps = Sys("espelho_estalo", pos + Vector3.up * 1f, Color.white, true, 0.2f, 0.35f, 0f, 0f, 0.5f, 0.8f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            Sphere(ps, 0.8f); Bursts(ps, 6); Grad(ps, Color.white, GuardBlue, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up, Steel, 3.5f, 4.5f, 0.3f);
        }

        static void GdReflectHit(Vector3 target, Vector3 dir)
        {
            Vector3 o = PlayerChest();
            if (o == Vector3.zero) o = target - dir * 3f;
            FX.Lightning(o, target, GuardBlue, 0.1f, 1, 0.2f);
            ImpactPhysical(target, Steel, 1f);
        }

        static void GdSentenceStart(Vector3 pos, Vector3 dir)
        {
            FX.Dust(pos, U.Hex("c8b090"), 1.3f);
            FX.Ring(pos, 1.6f, GuardBlue, 0.3f, 0.4f);
            Afterimage(PlayerVisual(), GuardBlue, 0.4f);
            var ps = Sys("sentenca_sobe", pos + Vector3.up * 0.3f, Gold, true, 0.3f, 0.5f, 5f, 9f, 0.05f, 0.1f);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(ps, 12f, 0.5f); Bursts(ps, 18); Stretch(ps, 2.5f, 0.04f); Grad(ps, Color.white, GuardBlue, 1f); ps.Play();
        }

        static void GdSentence(Vector3 pos, float r)
        {
            LightSword(pos, GuardBlue, Gold, 1f);
            FxDelay.Run(0.12f, () =>
            {
                Crack(pos, r * 1.9f, GuardBlue, 2.8f, Random.Range(0f, 360f));
                Shockwave(pos, r * 1.3f, GuardBlue, 0.45f);
                DelayedRing(pos, r * 1.6f, Gold, 0.5f, 0.08f);
                RuneCircle(pos, r, Gold, 1f, 90f);
                Rocks(pos, r * 0.4f, U.Hex("7a6a5a"), 16, 1.1f);
                var ps = Sys("sentenca_luz", pos + Vector3.up * 0.3f, GuardBlue, true, 0.4f, 0.8f, 4f, 10f, 0.08f, 0.18f, 0.4f);
                Hemisphere(ps, 0.5f); Bursts(ps, 60); Stretch(ps, 1.7f, 0.04f); Grad(ps, Color.white, GuardBlue, 1f); ps.Play();
                LightColumn(pos, r * 0.35f, 8f, Gold, 0.5f);
                FX.Dust(pos, U.Hex("c8b090"), 2f);
                FX.FlashLight(pos + Vector3.up * 1.5f, GuardBlue, 8f, r + 6f, 0.5f);
            });
        }
    }

    // ======================================================================================== componentes de animação
    /// <summary>Peças que brotam do chão (escala Y 0→1), ficam e afundam; destrói no fim.</summary>
    public class FxRise : MonoBehaviour
    {
        readonly List<Transform> parts = new();
        readonly List<Renderer> rends = new();
        readonly List<Color> cols = new();
        float up = 0.1f, hold = 0.7f, down = 0.3f, t;

        public void Add(Transform p, Renderer r, Color c) { parts.Add(p); rends.Add(r); cols.Add(c); p.localScale = new Vector3(1f, 0.01f, 1f); }
        public void Setup(float up, float hold, float down) { this.up = Mathf.Max(0.02f, up); this.hold = hold; this.down = Mathf.Max(0.02f, down); }

        void Update()
        {
            t += Time.deltaTime;
            float s;
            if (t < up) { float k = t / up; s = 1f - (1f - k) * (1f - k); s *= 1f + 0.15f * Mathf.Sin(k * Mathf.PI); }
            else if (t < up + hold) s = 1f;
            else s = Mathf.Clamp01(1f - (t - up - hold) / down);
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null) continue;
                parts[i].localScale = new Vector3(1f, Mathf.Max(0.01f, s), 1f);
                if (rends[i] != null && t > up + hold) { var c = cols[i]; c.a *= s; FxUtil.Tint(rends[i], c); }
            }
            if (t >= up + hold + down) Destroy(gameObject);
        }
    }

    /// <summary>Cai de "from" até "to" (acelerando), fica cravado e some encolhendo.</summary>
    public class FxDropStick : MonoBehaviour
    {
        Vector3 from, to, baseScale;
        float fall, hold, shrink, t;

        public void Setup(Vector3 from, Vector3 to, float fall, float hold, float shrink)
        {
            this.from = from; this.to = to; this.fall = Mathf.Max(0.01f, fall); this.hold = hold; this.shrink = Mathf.Max(0.02f, shrink);
            baseScale = transform.localScale;
            transform.position = from;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t < fall) { float k = t / fall; transform.position = Vector3.LerpUnclamped(from, to, k * k); return; }
            transform.position = to;
            float e = t - fall - hold;
            if (e > 0f) transform.localScale = baseScale * Mathf.Clamp01(1f - e / shrink);
            if (e >= shrink) Destroy(gameObject);
        }
    }

    /// <summary>Balanço suave (pano do estandarte).</summary>
    public class FxSway : MonoBehaviour
    {
        Quaternion baseRot; float seed;
        void Start() { baseRot = transform.localRotation; seed = Random.value * 10f; }
        void Update()
        {
            float tt = Time.time + seed;
            transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(tt * 1.7f) * 6f, Mathf.Sin(tt * 2.3f) * 14f, 0f);
        }
    }

    /// <summary>
    /// Sombra do Passo da Sombra: faixa viva no chão (malha gerada com bordas em fiapos e UV correndo),
    /// brilho roxo por cima, fiapos de sombra subindo, ponta afiada correndo até o alvo; ao chegar, a sombra irrompe.
    /// </summary>
    public class ShadowTendrilFx : MonoBehaviour
    {
        const int N = 40;
        Vector3 from, to, side, dirN;
        float travel, hold, t, len;
        Mesh mesh;
        Vector3[] verts;
        Color[] cols;
        Vector2[] uvs;
        Vector3[] glowPts;
        LineRenderer glow;
        ParticleSystem wisps, motes;
        bool arrived;
        float y;

        public void Setup(Vector3 from, Vector3 to, float travel, float hold)
        {
            this.travel = travel; this.hold = hold;
            y = from.y + 0.05f;
            this.from = new Vector3(from.x, y, from.z);
            this.to = new Vector3(to.x, y, to.z);
            Vector3 d = this.to - this.from;
            len = Mathf.Max(0.1f, d.magnitude);
            dirN = d / len;
            side = Vector3.Cross(Vector3.up, dirN).normalized;

            mesh = new Mesh { name = "sombra_faixa" };
            mesh.MarkDynamic();
            verts = new Vector3[(N + 1) * 2];
            cols = new Color[(N + 1) * 2];
            uvs = new Vector2[(N + 1) * 2];
            var tris = new int[N * 12];
            int k = 0;
            for (int i = 0; i < N; i++)
            {
                int a0 = i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                tris[k++] = a0; tris[k++] = b0; tris[k++] = a1; tris[k++] = a1; tris[k++] = b0; tris[k++] = b1;
                tris[k++] = a0; tris[k++] = a1; tris[k++] = b0; tris[k++] = a1; tris[k++] = b1; tris[k++] = b0;
            }
            Fill(0f);
            mesh.vertices = verts; mesh.colors = cols; mesh.uv = uvs; mesh.triangles = tris;
            mesh.bounds = new Bounds((this.from + this.to) * 0.5f, new Vector3(len + 4f, 2f, len + 4f));
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = U.Fx(false, FxTex.Shadow());
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            FX.SetColor(mr, new Color(0.07f, 0.01f, 0.12f, 0.95f));

            var lg = new GameObject("sombra_brilho");
            lg.transform.SetParent(transform, false);
            glow = lg.AddComponent<LineRenderer>();
            glow.useWorldSpace = true;
            glow.positionCount = N + 1;
            glow.widthMultiplier = 1.1f;
            glow.sharedMaterial = U.Fx(true, FxTex.Band());
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.receiveShadows = false;
            glowPts = new Vector3[N + 1];

            wisps = FxUtil.LoopSys("sombra_fiapos", null, this.from, new Color(0.1f, 0.03f, 0.16f, 0.8f), false, 0.6f, 1.1f, 0.3f, 0.9f, 0.25f, 0.5f, -0.25f, 70f, 120);
            wisps.transform.SetParent(transform, true);
            FxUtil.Sphere(wisps, 0.2f);
            SkillFX.Noise(wisps, 0.5f, 1.4f);
            FxUtil.AlphaFade(wisps, new Color(0.3f, 0.12f, 0.45f), new Color(0.02f, 0f, 0.04f), 0.8f);
            wisps.Play();
            motes = FxUtil.LoopSys("sombra_motas", null, this.from, SkillFX.ShadowPurple, true, 0.4f, 0.8f, 0.3f, 1.2f, 0.04f, 0.09f, -0.3f, 40f, 80);
            motes.transform.SetParent(transform, true);
            FxUtil.Sphere(motes, 0.25f);
            SkillFX.Grad(motes, new Color(0.85f, 0.7f, 1f), SkillFX.ShadowPurple, 1f);
            motes.Play();
            FX.FlashLight(this.from + Vector3.up * 0.5f, SkillFX.ShadowPurple, 2.5f, 4f, travel + 0.1f);
        }

        float Head()
        {
            float k = Mathf.Clamp01(t / travel);
            return 1f - (1f - k) * (1f - k);
        }

        void Fill(float alpha)
        {
            float head = Head();
            for (int i = 0; i <= N; i++)
            {
                float u = (float)i / N;
                float shown = u <= head ? 1f : 0f;
                float wob = Mathf.Sin(u * 11f - t * 7f) * 0.12f * Mathf.Sin(u * Mathf.PI);
                Vector3 p = Vector3.Lerp(from, to, Mathf.Min(u, head)) + side * wob;
                // largura: grossa perto do Guard, afinando; ponta afiada na cabeça; respiração lenta
                float w = Mathf.Lerp(0.75f, 0.45f, u) * (1f + 0.18f * Mathf.Sin(u * 23f - t * 9f));
                float tip = Mathf.Clamp01((head - u) / 0.12f);
                w *= Mathf.Lerp(0.15f, 1f, tip);
                float a = alpha * shown * Mathf.Clamp01(u / 0.04f + 0.3f);
                verts[i * 2] = p - side * w * 0.5f;
                verts[i * 2 + 1] = p + side * w * 0.5f;
                cols[i * 2] = new Color(1f, 1f, 1f, a);
                cols[i * 2 + 1] = new Color(1f, 1f, 1f, a);
                float uu = u * len * 0.45f - t * 1.6f;
                uvs[i * 2] = new Vector2(uu, 0f);
                uvs[i * 2 + 1] = new Vector2(uu, 1f);
                if (glowPts != null) glowPts[i] = p + Vector3.up * 0.02f;
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            float fade = t > hold - 0.35f ? Mathf.Clamp01((hold - t) / 0.35f) : 1f;
            float breathe = 0.85f + 0.15f * Mathf.Sin(t * 5f);
            Fill(fade);
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.uv = uvs;
            if (glow != null)
            {
                glow.SetPositions(glowPts);
                float ga = 0.5f * fade * breathe;
                glow.startColor = new Color(0.45f, 0.18f, 0.85f, ga * 0.6f);
                glow.endColor = new Color(0.6f, 0.3f, 1f, ga);
            }
            float head = Head();
            Vector3 hp = Vector3.Lerp(from, to, head);
            if (!arrived)
            {
                if (wisps != null) wisps.transform.position = hp;
                if (motes != null) motes.transform.position = hp;
                if (head >= 1f) { arrived = true; Arrive(); }
            }
            else
            {
                // depois de chegar: fiapos saindo ao longo de toda a sombra
                Vector3 rp = Vector3.Lerp(from, to, Random.value);
                if (wisps != null) { wisps.transform.position = rp; var em = wisps.emission; em.rateOverTime = 22f * fade; }
                if (motes != null) { motes.transform.position = Vector3.Lerp(from, to, Random.value); var em = motes.emission; em.rateOverTime = 12f * fade; }
            }
            if (t >= hold) Finish();
        }

        void Arrive()
        {
            Vector3 p = to;
            var burst = SkillFX.Sys("sombra_irrompe", p, new Color(0.1f, 0.03f, 0.15f, 0.85f), false, 0.4f, 0.7f, 3f, 6f, 0.3f, 0.6f, -0.2f);
            burst.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            SkillFX.Cone(burst, 18f, 0.7f); SkillFX.Bursts(burst, 22); SkillFX.Stretch(burst, 1.8f, 0.05f);
            FxUtil.AlphaFade(burst, new Color(0.3f, 0.12f, 0.45f), new Color(0.02f, 0f, 0.04f), 0.85f); burst.Play();
            var sp = SkillFX.Sys("sombra_irrompe_brilho", p + Vector3.up * 0.2f, SkillFX.ShadowPurple, true, 0.3f, 0.6f, 2f, 6f, 0.05f, 0.11f, -0.2f);
            SkillFX.Hemisphere(sp, 0.6f); SkillFX.Bursts(sp, 26); SkillFX.Grad(sp, Color.white, SkillFX.ShadowPurple, 1f); sp.Play();
            FX.Ring(p, 1.8f, SkillFX.ShadowPurple, 0.3f, 0.2f);
            FX.FlashLight(p + Vector3.up * 0.8f, SkillFX.ShadowPurple, 4f, 5f, 0.35f);
        }

        bool finished;
        void Finish()
        {
            if (finished) return;
            finished = true;
            if (wisps != null) { wisps.transform.SetParent(FX.Root, true); wisps.Stop(true, ParticleSystemStopBehavior.StopEmitting); Destroy(wisps.gameObject, 1.3f); }
            if (motes != null) { motes.transform.SetParent(FX.Root, true); motes.Stop(true, ParticleSystemStopBehavior.StopEmitting); Destroy(motes.gameObject, 1f); }
            Destroy(gameObject);
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
