using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Efeitos únicos de cada habilidade (24 ids) + blocos de construção reutilizáveis
    /// (ondas de choque, rachaduras, runas, colunas de luz, brasas, fumaça, estilhaços, pedras, afterimage...).
    /// Identidade por classe:
    ///   Guerreiro = vermelho/laranja, fogo e impacto físico
    ///   Arqueiro  = verde/ciano, vento e precisão
    ///   Mago      = roxo/azul, arcano / gelo / fogo
    ///   Tank      = dourado/azul, luz sagrada e terra
    /// SkillFX.Play(id, ...) é o efeito principal; SkillFX.Phase(id, fase, ...) cobre fases extras
    /// ("start", "tick", "trail", "end", "drop", "bolt").
    /// </summary>
    public static partial class SkillFX
    {
        // ------------------------------------------------------------------ paletas
        public static readonly Color FireCore = new Color(1f, 0.94f, 0.75f);
        public static readonly Color Fire = new Color(1f, 0.48f, 0.16f);
        public static readonly Color FireDeep = new Color(1f, 0.25f, 0.1f);
        public static readonly Color Ember = new Color(1f, 0.66f, 0.26f);
        public static readonly Color Blood = new Color(0.95f, 0.16f, 0.12f);
        public static readonly Color Steel = new Color(0.9f, 0.94f, 1f);
        public static readonly Color Wind = new Color(0.82f, 1f, 0.9f);
        public static readonly Color Leaf = new Color(0.56f, 0.88f, 0.54f);
        public static readonly Color Cyan = new Color(0.42f, 0.96f, 0.86f);
        public static readonly Color Arcane = new Color(0.72f, 0.54f, 1f);
        public static readonly Color ArcaneDeep = new Color(0.45f, 0.25f, 1f);
        public static readonly Color Ice = new Color(0.62f, 0.9f, 1f);
        public static readonly Color Spark = new Color(0.62f, 0.86f, 1f);
        public static readonly Color Heal = new Color(0.56f, 1f, 0.66f);
        public static readonly Color Gold = new Color(1f, 0.83f, 0.42f);
        public static readonly Color Holy = new Color(1f, 0.96f, 0.82f);
        public static readonly Color TankBlue = new Color(0.56f, 0.77f, 1f);
        public static readonly Color Earth = new Color(0.63f, 0.5f, 0.38f);
        public static readonly Color SmokeCol = new Color(0.16f, 0.13f, 0.12f, 0.75f);

        static readonly HashSet<string> known = new HashSet<string>
        {
            "spin", "charge", "warcry", "leap", "whirlwind", "execute",
            "volley", "pierce", "frost_arrow", "trap", "roll", "arrow_rain",
            "fireball", "heal", "blast", "chain", "blink", "meteor",
            "slam", "fortress", "bash", "taunt", "quake", "guardian"
        };

        /// <summary>true se a habilidade tem efeito próprio aqui.</summary>
        public static bool Has(string skillId) => !string.IsNullOrEmpty(skillId) && known.Contains(skillId);

        /// <summary>Efeito principal de cada habilidade.</summary>
        public static void Play(string skillId, Vector3 pos, Vector3 dir, float radius, Color col)
        {
            dir = U.Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            radius = Mathf.Max(0.5f, radius);
            if (FX.Prefab("sk_" + skillId, pos, radius)) return;
            switch (skillId)
            {
                // Guerreiro
                case "spin": Spin(pos, dir, radius); break;
                case "charge": ChargeStart(pos, dir); break;
                case "warcry": Warcry(pos); break;
                case "leap": LeapLand(pos, radius); break;
                case "whirlwind": WhirlTick(pos, dir, radius); break;
                case "execute": Execute(pos, dir, radius); break;
                // Arqueiro
                case "volley": Volley(pos, dir); break;
                case "pierce": Pierce(pos, dir); break;
                case "frost_arrow": FrostArrow(pos, dir); break;
                case "trap": TrapPlace(pos); break;
                case "roll": RollStart(pos, dir, radius); break;
                case "arrow_rain": RainCast(pos, dir); break;
                // Mago
                case "fireball": FireballCast(pos, dir); break;
                case "heal": HealFx(pos, Heal, Arcane); break;
                case "blast": ArcaneBlast(pos, radius); break;
                case "chain": ChainCast(pos, dir); break;
                case "blink": BlinkOut(pos, dir); break;
                case "meteor": MeteorImpact(pos, radius, 1f); break;
                // Tank
                case "slam": Slam(pos, radius); break;
                case "fortress": Fortress(pos); break;
                case "bash": Bash(pos, dir); break;
                case "taunt": TauntFx(pos, radius); break;
                case "quake": QuakeCast(pos, dir); break;
                case "guardian": GuardianFx(pos); break;
                default:
                    if (PlayClasses(skillId, pos, dir, radius, col)) break;   // [Classes] SkillFXClasses.cs
                    FX.Burst(pos + Vector3.up * 0.8f, col, 1f, 24);
                    FX.Ring(pos, radius, col, 0.4f);
                    break;
            }
        }

        /// <summary>Fases extras: "start" (antes), "trail" (durante deslocamento), "tick", "drop", "bolt" (b = pos + dir), "end" (chegada).</summary>
        public static void Phase(string skillId, string phase, Vector3 pos, Vector3 dir, float radius, Color col)
        {
            dir = U.Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            switch (skillId + ":" + phase)
            {
                case "charge:trail": DashTrail(pos, dir, Fire, Ember); break;
                case "bash:trail": DashTrail(pos, dir, TankBlue, Gold); break;
                case "charge:hit": ImpactPhysical(pos, Fire, 1f); break;
                case "bash:hit": ImpactPhysical(pos, Gold, 1.1f); FX.Ring(pos - Vector3.up * 1f, 1.2f, Gold, 0.25f); break;
                case "leap:start": LeapTakeoff(pos, dir); break;
                case "roll:end": RollEnd(pos, dir); break;
                case "blink:end": BlinkIn(pos, radius); break;
                case "arrow_rain:drop": RainDrop(pos); break;
                case "quake:tick": QuakeSegment(pos, dir, radius); break;
                case "chain:bolt": ChainBolt(pos, pos + dir * radius); break;
                case "meteor:start": MeteorWarn(pos, radius); break;
                case "blast:start": BlastCharge(pos, radius); break;
                default: PhaseClasses(skillId, phase, pos, dir, radius, col); break;   // [Classes] SkillFXClasses.cs
            }
        }

        /// <summary>Raio da Corrente (de a até b): ziguezague com ramificações + faíscas.</summary>
        public static void ChainBolt(Vector3 a, Vector3 b)
        {
            FX.Lightning(a, b, Spark, 0.16f, 3, 0.28f);
            Sparks(b, Spark, 14, 3f, 6f, 0.6f);
            var ps = Sys("bolt_hit", b, Color.white, true, 0.08f, 0.15f, 0f, 0f, 0.8f, 1.2f);
            Bursts(ps, 1); Grad(ps, Color.white, Spark, 1f); ps.Play();
        }

        // ==================================================================================== GUERREIRO
        static void Spin(Vector3 pos, Vector3 dir, float r)
        {
            // dois cortes circulares em alturas diferentes, girando para lados opostos
            FX.SlashArc(pos + Vector3.up * 1.0f, dir, Fire, r * 0.95f, 350f, false, 0.26f, 0.8f, 24);
            FX.SlashArc(pos + Vector3.up * 0.6f, -dir, Ember, r * 0.75f, 300f, true, 0.22f, 0.45f, 8);
            Shockwave(pos, r, Fire, 0.35f);
            // brasas espirrando para fora num anel horizontal
            var ps = Sys("spin_embers", pos + Vector3.up * 0.8f, Ember, true, 0.3f, 0.7f, 5f, 9f, 0.05f, 0.12f, 0.9f);
            FlatCircle(ps, 0.8f, 0f);
            Bursts(ps, 40); Stretch(ps, 1.6f); Grad(ps, FireCore, Fire, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up, Fire, 4.5f, r + 4f, 0.35f);
        }

        static void ChargeStart(Vector3 pos, Vector3 dir)
        {
            // explosão de poeira e brasas para trás (impulso)
            FX.Dust(pos - dir * 0.4f, U.Hex("c8b090"), 1.1f);
            ConeBurst(pos + Vector3.up * 0.5f - dir * 0.3f, -dir, Ember, 30f, 3f, 7f, 26, 0.6f);
            Afterimage(PlayerVisual(), Fire, 0.3f);
            FX.FlashLight(pos + Vector3.up, Fire, 3f, 5f, 0.25f);
        }

        /// <summary>Rastro durante investidas: cópia translúcida do herói + faíscas no chão.</summary>
        static void DashTrail(Vector3 pos, Vector3 dir, Color ghost, Color spark)
        {
            Afterimage(PlayerVisual(), ghost, 0.28f);
            var ps = Sys("dash_trail", pos + Vector3.up * 0.15f, spark, true, 0.2f, 0.45f, 1f, 3f, 0.05f, 0.1f, 1f);
            ps.transform.rotation = Quaternion.LookRotation(-dir);
            Cone(ps, 35f, 0.3f);
            Bursts(ps, 6); Stretch(ps, 1.4f); Grad(ps, FireCore, spark, 1f); ps.Play();
        }

        static void ImpactPhysical(Vector3 pos, Color c, float size)
        {
            Sparks(pos, Color.Lerp(c, Color.white, 0.4f), Mathf.RoundToInt(18 * size), 3f, 8f, 1.2f);
            var ps = Sys("impact_flash", pos, Color.white, true, 0.06f, 0.1f, 0f, 0f, 1.4f * size, 1.8f * size);
            Bursts(ps, 1); Grad(ps, Color.white, c, 1f); ps.Play();
            FX.FlashLight(pos, c, 3.5f * size, 4f, 0.15f);
        }

        static void Warcry(Vector3 pos)
        {
            LightColumn(pos, 1.1f, 6f, FireDeep, 0.9f);
            LightColumn(pos, 0.6f, 4.5f, Ember, 0.7f);
            for (int i = 0; i < 3; i++) DelayedRing(pos, 3.2f + i * 1.3f, i == 0 ? Fire : FireDeep, 0.45f, i * 0.12f);
            Embers(pos, 1.2f, Ember, 50, 1.0f, 3.5f);
            var ps = Sys("warcry_burst", pos + Vector3.up * 1.2f, Fire, true, 0.4f, 0.8f, 3f, 7f, 0.15f, 0.35f, -0.2f);
            Sphere(ps, 0.4f); Bursts(ps, 40); Noise(ps, 1.2f, 1.5f); Grad(ps, FireCore, FireDeep, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up * 1.5f, FireDeep, 6f, 9f, 0.6f);
        }

        static void LeapTakeoff(Vector3 pos, Vector3 dir)
        {
            FX.Dust(pos, U.Hex("c8b090"), 1.2f);
            FX.Ring(pos, 1.4f, Ember, 0.3f, 0.4f);
            Afterimage(PlayerVisual(), Fire, 0.35f);
        }

        static void LeapLand(Vector3 pos, float r)
        {
            Crack(pos, r * 1.5f, FireDeep, 2.2f, Random.Range(0f, 360f));
            Shockwave(pos, r * 1.2f, Ember, 0.4f);
            Rocks(pos, r * 0.5f, U.Hex("7a6a5a"), 16, 1f);
            FX.Dust(pos, U.Hex("c8b090"), 1.6f);
            Sparks(pos + Vector3.up * 0.3f, Ember, 30, 4f, 9f, 1.5f);
            FX.FlashLight(pos + Vector3.up * 0.8f, Fire, 6f, r + 5f, 0.4f);
        }

        static void WhirlTick(Vector3 pos, Vector3 dir, float r)
        {
            // a rotação do herói é horária vista de cima (+Z para +X) → varre da esquerda para a direita
            FX.SlashArc(pos + Vector3.up * 1.0f, dir, Steel, r * 0.9f, 320f, false, 0.22f, 0.6f, 6);
            FX.SlashArc(pos + Vector3.up * 0.75f, -dir, Ember, r * 0.75f, 260f, false, 0.2f, 0.35f, 4);
            var ps = Sys("whirl_dust", pos + Vector3.up * 0.1f, new Color(0.8f, 0.72f, 0.6f, 0.4f), false, 0.3f, 0.6f, 2f, 4f, 0.3f, 0.6f, -0.05f);
            FlatCircle(ps, r * 0.6f, 0f); Bursts(ps, 8); Grad(ps, new Color(0.8f, 0.72f, 0.6f), new Color(0.6f, 0.5f, 0.4f), 0.45f); ps.Play();
            FX.FlashLight(pos + Vector3.up, Ember, 2f, r + 2.5f, 0.2f);
        }

        static void Execute(Vector3 pos, Vector3 dir, float range)
        {
            Vector3 chest = pos + Vector3.up * 1.0f;
            FX.SlashArc(chest, dir, Blood, range * 0.95f, 165f, true, 0.28f, 1.0f, 30);
            FX.SlashArc(chest + Vector3.up * 0.05f, dir, FireCore, range * 0.85f, 150f, true, 0.2f, 0.25f, 0);
            // rachadura no chão na direção do golpe
            Crack(pos + dir * range * 0.6f, range * 1.1f, Blood, 1.8f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0.55f);
            ConeBurst(chest + dir * 0.6f, dir, Blood, 40f, 5f, 11f, 34, 1.2f);
            Shockwave(pos + dir * range * 0.5f, range * 0.8f, FireDeep, 0.3f);
            FX.FlashLight(chest + dir * 1.2f, Blood, 6f, 7f, 0.3f);
        }

        // ==================================================================================== ARQUEIRO
        static void Volley(Vector3 pos, Vector3 dir)
        {
            Vector3 muzzle = pos + Vector3.up * 1.1f + dir * 0.7f;
            ConeBurst(muzzle, dir, Wind, 28f, 6f, 12f, 30, 0f);
            WindRing(muzzle, dir, Leaf, 0.4f, 22);
            Leaves(muzzle, dir, 12);
            FX.FlashLight(muzzle, Leaf, 2.5f, 4f, 0.2f);
        }

        static void Pierce(Vector3 pos, Vector3 dir)
        {
            Vector3 muzzle = pos + Vector3.up * 1.1f + dir * 0.7f;
            WindRing(muzzle, dir, U.Hex("ffe08a"), 0.5f, 36);
            WindRing(muzzle + dir * 0.8f, dir, Cyan, 0.35f, 24);
            Beam(muzzle, muzzle + dir * 14f, new Color(1f, 0.95f, 0.6f), 0.12f, 0.3f);
            Beam(muzzle, muzzle + dir * 14f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f), 0.45f, 0.22f);
            Sparks(muzzle, U.Hex("ffe08a"), 12, 2f, 5f, 0.4f);
            FX.FlashLight(muzzle, U.Hex("ffe08a"), 4f, 5f, 0.25f);
        }

        static void FrostArrow(Vector3 pos, Vector3 dir)
        {
            Vector3 muzzle = pos + Vector3.up * 1.1f + dir * 0.7f;
            Mist(muzzle, Ice, 14, 0.5f);
            IceShards(muzzle, dir, 10, 0.4f);
            WindRing(muzzle, dir, Ice, 0.35f, 18);
            FX.FlashLight(muzzle, Ice, 3f, 4.5f, 0.3f);
        }

        static void TrapPlace(Vector3 pos)
        {
            RuneCircle(pos, 1.3f, Leaf, 0.9f, 140f);
            FX.Dust(pos, U.Hex("c8b090"), 0.7f);
            var ps = Sys("trap_glint", pos + Vector3.up * 0.2f, Leaf, true, 0.4f, 0.8f, 0.5f, 1.5f, 0.05f, 0.12f, -0.3f);
            FlatCircle(ps, 0.6f, 1f); Bursts(ps, 16); Grad(ps, Color.white, Leaf, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up * 0.5f, Leaf, 2f, 3f, 0.4f);
        }

        static void RollStart(Vector3 pos, Vector3 dir, float dist)
        {
            Afterimage(PlayerVisual(), Leaf, 0.4f);
            FX.Dust(pos, U.Hex("d8e0c8"), 1f);
            // rajadas de vento no sentido do rolamento (dir já aponta para onde o herói vai)
            var ps = Sys("roll_wind", pos + Vector3.up * 0.6f, Wind, true, 0.2f, 0.4f, 8f, 14f, 0.04f, 0.08f);
            ps.transform.rotation = Quaternion.LookRotation(dir);
            Cone(ps, 8f, 0.6f); Bursts(ps, 18); Stretch(ps, 3f); Grad(ps, Color.white, Cyan, 0.8f); ps.Play();
        }

        static void RollEnd(Vector3 pos, Vector3 dir)
        {
            FX.Dust(pos, U.Hex("d8e0c8"), 0.8f);
            Leaves(pos + Vector3.up * 0.6f, -dir, 8);
            FX.Ring(pos, 1.1f, Cyan, 0.3f, 0.3f);
        }

        static void RainCast(Vector3 pos, Vector3 dir)
        {
            // flechas de luz disparadas para o céu
            var ps = Sys("rain_up", pos + Vector3.up * 1.4f + dir * 0.4f, Leaf, true, 0.4f, 0.6f, 18f, 26f, 0.05f, 0.08f);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(ps, 10f, 0.15f); Bursts(ps, 16); Stretch(ps, 4f); Grad(ps, Color.white, Leaf, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up * 2f, Leaf, 3f, 5f, 0.3f);
        }

        /// <summary>Uma flecha de luz caindo do céu num ponto (Chuva de Flechas).</summary>
        public static void RainDrop(Vector3 p)
        {
            Vector3 top = p + new Vector3(Random.Range(-0.4f, 0.4f), 6f, Random.Range(-0.4f, 0.4f));
            Beam(top, p, new Color(0.75f, 1f, 0.75f), 0.06f, 0.16f);
            Beam(top, p, new Color(Leaf.r, Leaf.g, Leaf.b, 0.3f), 0.25f, 0.12f);
            Sparks(p + Vector3.up * 0.1f, Leaf, 6, 1.5f, 3.5f, 1f);
            FX.Ring(p, 0.7f, Leaf, 0.22f, 0.2f);
        }

        // ==================================================================================== MAGO
        static void FireballCast(Vector3 pos, Vector3 dir)
        {
            Vector3 hand = pos + Vector3.up * 1.2f + dir * 0.7f;
            var ps = Sys("fire_cast", hand, Fire, true, 0.25f, 0.5f, 2f, 5f, 0.2f, 0.45f, -0.4f);
            ps.transform.rotation = Quaternion.LookRotation(dir);
            Cone(ps, 22f, 0.15f); Bursts(ps, 26); Noise(ps, 1f, 2f); Grad(ps, FireCore, FireDeep, 1f); ps.Play();
            Embers(hand, 0.3f, Ember, 12, 0.3f, 1.5f);
            FX.FlashLight(hand, Fire, 3.5f, 5f, 0.3f);
        }

        static void HealFx(Vector3 pos, Color main, Color accent)
        {
            RuneCircle(pos, 1.8f, accent, 1.2f, 70f);
            LightColumn(pos, 0.9f, 4.5f, main, 1.0f);
            Spiral(pos, 0.9f, main, 1.2f, 3f, 60);
            FX.Sparkle(pos + Vector3.up * 0.4f, Color.Lerp(main, Color.white, 0.5f), 1.2f, 0.7f);
            FX.FlashLight(pos + Vector3.up * 1.5f, main, 4f, 6f, 0.9f);
        }

        static void BlastCharge(Vector3 pos, float r)
        {
            Implode(pos + Vector3.up * 0.6f, r, Arcane, 0.25f, 30);
        }

        static void ArcaneBlast(Vector3 pos, float r)
        {
            RuneCircle(pos, r * 1.15f, Arcane, 0.7f, -220f);
            var ps = Sys("arcane_burst", pos + Vector3.up * 0.6f, Arcane, true, 0.3f, 0.7f, 4f, 10f, 0.12f, 0.3f, 0.2f);
            Hemisphere(ps, 0.3f); Bursts(ps, 50); Stretch(ps, 1.2f); Grad(ps, Color.white, ArcaneDeep, 1f); ps.Play();
            // bolha de energia que estoura
            Bubble(pos, r * 0.9f, Arcane, 0.3f);
            Shockwave(pos, r * 1.2f, ArcaneDeep, 0.35f);
            LightColumn(pos, r * 0.45f, 3.5f, Arcane, 0.4f);
            FX.FlashLight(pos + Vector3.up, Arcane, 6f, r + 5f, 0.45f);
        }

        static void ChainCast(Vector3 pos, Vector3 dir)
        {
            Vector3 hand = pos + Vector3.up * 1.3f + dir * 0.5f;
            Sparks(hand, Spark, 16, 2f, 5f, 0.2f);
            FX.FlashLight(hand, Spark, 3f, 4f, 0.2f);
        }

        static void BlinkOut(Vector3 pos, Vector3 dir)
        {
            Afterimage(PlayerVisual(), Arcane, 0.5f);
            Implode(pos + Vector3.up * 0.9f, 1.4f, Arcane, 0.22f, 24);
            RuneCircle(pos, 1.2f, ArcaneDeep, 0.5f, 300f);
            FX.FlashLight(pos + Vector3.up, Arcane, 3f, 4f, 0.25f);
        }

        static void BlinkIn(Vector3 pos, float novaR)
        {
            var ps = Sys("blink_in", pos + Vector3.up * 0.9f, Arcane, true, 0.25f, 0.55f, 3f, 7f, 0.08f, 0.2f);
            Sphere(ps, 0.3f); Bursts(ps, 36); Grad(ps, Color.white, ArcaneDeep, 1f); ps.Play();
            if (novaR > 0f) { Shockwave(pos, novaR, Arcane, 0.35f); RuneCircle(pos, novaR, Arcane, 0.5f, -200f); }
            LightColumn(pos, 0.6f, 3f, Arcane, 0.35f);
            FX.FlashLight(pos + Vector3.up, Arcane, 4f, 5f, 0.3f);
        }

        static void MeteorWarn(Vector3 pos, float r)
        {
            RuneCircle(pos, r * 1.1f, Fire, 1.0f, 90f);
        }

        /// <summary>Impacto de meteoro (habilidade = escala 1; ultimate = escala maior).</summary>
        public static void MeteorImpact(Vector3 pos, float r, float scale)
        {
            Crack(pos, r * 1.7f, Fire, 3f * scale, Random.Range(0f, 360f));
            var fire = Sys("meteor_fire", pos + Vector3.up * 0.4f, Fire, true, 0.4f, 0.9f, 4f * scale, 11f * scale, 0.4f * scale, 0.9f * scale, -0.3f);
            Hemisphere(fire, 0.5f * scale); Bursts(fire, Mathf.RoundToInt(60 * scale)); Noise(fire, 1.5f, 1.2f); Grad(fire, FireCore, FireDeep, 1f); fire.Play();
            Smoke(pos, r * 0.6f, Mathf.RoundToInt(22 * scale), 1.6f * scale);
            Embers(pos, r * 0.5f, Ember, Mathf.RoundToInt(50 * scale), 1.4f, 5f * scale);
            Rocks(pos, r * 0.4f, U.Hex("4a3a32"), Mathf.RoundToInt(18 * scale), 1.3f * scale);
            Shockwave(pos, r * 1.4f, Fire, 0.45f);
            DelayedRing(pos, r * 2f, Ember, 0.6f, 0.1f);
            LightColumn(pos, r * 0.5f, 5f * scale, Fire, 0.5f);
            FX.FlashLight(pos + Vector3.up * 1.5f, Fire, 8f * scale, r * 2.5f + 4f, 0.9f * scale);
        }

        // ==================================================================================== TANK
        static void Slam(Vector3 pos, float r)
        {
            Crack(pos, r * 1.6f, Gold, 2.2f, Random.Range(0f, 360f));
            Rocks(pos, r * 0.5f, U.Hex("7a6a5a"), 14, 1f);
            Shockwave(pos, r * 1.1f, Gold, 0.4f);
            DelayedRing(pos, r * 0.8f, TankBlue, 0.35f, 0.08f);
            FX.Dust(pos, U.Hex("c8b090"), 1.5f);
            FX.FlashLight(pos + Vector3.up * 0.8f, Gold, 5f, r + 5f, 0.4f);
        }

        static void Fortress(Vector3 pos)
        {
            Bubble(pos, 1.6f, Gold, 0.7f);
            RuneCircle(pos, 1.7f, TankBlue, 1.1f, 60f);
            var ps = Sys("fortress_hex", pos + Vector3.up * 0.2f, Gold, true, 0.7f, 1.1f, 1.5f, 3f, 0.1f, 0.2f, -0.1f);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(ps, 2f, 1.3f); Bursts(ps, 40); Grad(ps, Holy, Gold, 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up, Gold, 4f, 6f, 0.7f);
        }

        static void Bash(Vector3 pos, Vector3 dir)
        {
            Vector3 chest = pos + Vector3.up * 1.0f;
            FX.SlashArc(chest + dir * 0.2f, dir, Gold, 1.5f, 110f, true, 0.2f, 0.5f, 10);
            ConeBurst(chest + dir * 0.5f, dir, Holy, 25f, 4f, 8f, 20, 0.3f);
            FX.FlashLight(chest + dir, Gold, 3f, 4.5f, 0.25f);
        }

        static void TauntFx(Vector3 pos, float r)
        {
            for (int i = 0; i < 3; i++) DelayedRing(pos, r * (0.55f + 0.25f * i), i == 1 ? Gold : U.Hex("ff5a4a"), 0.5f, i * 0.12f);
            LightColumn(pos, 0.8f, 4f, U.Hex("ff6a4a"), 0.6f);
            var ps = Sys("taunt_rage", pos + Vector3.up * 1.8f, U.Hex("ff8a6a"), true, 0.3f, 0.6f, 3f, 6f, 0.08f, 0.16f, 0.4f);
            Hemisphere(ps, 0.3f); Bursts(ps, 30); Stretch(ps, 1.5f); Grad(ps, Holy, U.Hex("ff4a3a"), 1f); ps.Play();
            FX.FlashLight(pos + Vector3.up * 1.5f, U.Hex("ff6a4a"), 5f, r + 3f, 0.5f);
        }

        static void QuakeCast(Vector3 pos, Vector3 dir)
        {
            FX.Ring(pos, 1.5f, Gold, 0.3f, 0.3f);
            FX.Dust(pos, U.Hex("a08060"), 1f);
            FX.FlashLight(pos + Vector3.up, Gold, 3f, 4f, 0.3f);
        }

        /// <summary>Um segmento do Terremoto: rachadura orientada na direção da linha + pedras.</summary>
        static void QuakeSegment(Vector3 p, Vector3 dir, float r)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            Crack(p, r * 2.4f, Gold, 1.6f, yaw + Random.Range(-25f, 25f), 0.6f);
            Rocks(p, r * 0.4f, U.Hex("7a6a5a"), 6, 0.8f);
            FX.Dust(p, U.Hex("a08060"), 1f);
            var ps = Sys("quake_glow", p + Vector3.up * 0.1f, Gold, true, 0.3f, 0.6f, 1f, 3f, 0.06f, 0.12f, -0.2f);
            FlatCircle(ps, r * 0.6f, 1f); Bursts(ps, 12); Grad(ps, Holy, Gold, 1f); ps.Play();
            FX.FlashLight(p + Vector3.up * 0.5f, Gold, 2f, 3.5f, 0.25f);
        }

        static void GuardianFx(Vector3 pos)
        {
            LightColumn(pos, 1.2f, 9f, Holy, 1.2f);
            LightColumn(pos, 0.5f, 9f, Gold, 1.0f);
            RuneCircle(pos, 2.2f, Gold, 1.4f, 50f);
            Feathers(pos + Vector3.up * 3f, 24);
            Spiral(pos, 1.0f, Gold, 1.2f, 3.5f, 50);
            FX.FlashLight(pos + Vector3.up * 2f, Holy, 6f, 8f, 1.0f);
        }

        // ==================================================================================== ULTIMATES (visual)
        /// <summary>Abertura cinemática de ultimate: coluna de luz, anéis em sequência e clarão forte.</summary>
        public static void UltCinematic(Vector3 pos, Color c)
        {
            LightColumn(pos, 1.4f, 10f, c, 0.9f);
            LightColumn(pos, 0.6f, 10f, Color.Lerp(c, Color.white, 0.6f), 0.7f);
            for (int i = 0; i < 3; i++) DelayedRing(pos, 2.5f + i * 1.5f, c, 0.5f, i * 0.08f);
            Implode(pos + Vector3.up * 1f, 3f, Color.Lerp(c, Color.white, 0.4f), 0.3f, 40);
            FX.FlashLight(pos + Vector3.up * 1.5f, c, 9f, 12f, 0.7f);
        }

        /// <summary>Anel pulsante + aura enquanto a ultimate está pronta. Devolve o objeto raiz (destrua para remover).</summary>
        public static GameObject UltReadyAura(Transform t, Color c)
        {
            if (t == null) return null;
            var root = new GameObject("ult_pronta");
            root.transform.SetParent(t, false);
            var ring = FX.FlatQuad("ult_anel", t.position + Vector3.up * 0.06f, U.RingTexture(), c, true);
            ring.transform.SetParent(root.transform, true);
            var pulse = ring.AddComponent<FxPulse>();
            pulse.color = c; pulse.baseScale = 1.9f; pulse.amount = 0.18f; pulse.speed = 5f;
            var aura = AttachAura(root.transform, c, 99999f, 18f, 0.55f, true);
            if (aura != null) aura.name = "ult_aura";
            return root;
        }

        /// <summary>Rugido do Berserker: onda de fogo gigante.</summary>
        public static void UltRoar(Vector3 pos, float r)
        {
            Shockwave(pos, r, Fire, 0.45f);
            for (int i = 0; i < 4; i++) DelayedRing(pos, r * (0.6f + 0.2f * i), i % 2 == 0 ? FireDeep : Ember, 0.5f, i * 0.07f);
            Crack(pos, r * 1.4f, FireDeep, 3f, Random.Range(0f, 360f));
            var ps = Sys("roar_fire", pos + Vector3.up * 0.4f, Fire, true, 0.35f, 0.7f, r * 1.6f, r * 2.4f, 0.4f, 0.9f, -0.3f);
            FlatCircle(ps, 0.5f, 0f); Bursts(ps, 90); Noise(ps, 1f, 1.5f); Grad(ps, FireCore, FireDeep, 1f); ps.Play();
            Embers(pos, r * 0.5f, Ember, 70, 1.4f, 5f);
            Rocks(pos, r * 0.4f, U.Hex("5a4a3a"), 18, 1.1f);
            FX.FlashLight(pos + Vector3.up * 1.5f, Fire, 10f, r * 3f, 0.8f);
        }

        /// <summary>Tempestade de flechas: centenas de traços de luz verdes caindo num disco (raio r) por "dur" segundos.</summary>
        public static void ArrowStorm(Vector3 center, float r, float dur)
        {
            // cone apontado para baixo: Euler(90,0,0) leva o +Z do cone para -Y
            var rain = Sys("storm_rain", center + Vector3.up * 12f, new Color(0.75f, 1f, 0.78f), true, 0.36f, 0.42f, 28f, 32f, 0.05f, 0.09f, 0f, dur);
            rain.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Cone(rain, 4f, r);
            Rate(rain, 340f);
            Stretch(rain, 6f, 0.02f);
            var sz = rain.sizeOverLifetime; sz.enabled = false;
            Grad(rain, Color.white, Leaf, 1f);
            rain.Play();
            // faíscas onde as flechas batem
            var hits = Sys("storm_hits", center + Vector3.up * 0.05f, Leaf, true, 0.15f, 0.3f, 1.5f, 4f, 0.04f, 0.08f, 1.2f, dur);
            hits.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(hits, 40f, r);
            Rate(hits, 160f);
            Stretch(hits, 1.4f, 0.04f);
            Grad(hits, Color.white, Cyan, 1f);
            hits.Play();
            // vento/poeira rasteira
            var dust = Sys("storm_dust", center + Vector3.up * 0.2f, new Color(0.8f, 0.85f, 0.7f, 0.3f), false, 0.6f, 1f, 0.5f, 1.5f, 0.6f, 1.2f, -0.02f, dur);
            dust.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(dust, 70f, r * 0.9f); Rate(dust, 25f); Grad(dust, new Color(0.85f, 0.9f, 0.75f), new Color(0.7f, 0.75f, 0.6f), 0.35f); dust.Play();
            StaticLight(center + Vector3.up * 2.5f, Leaf, 3.5f, r * 1.6f, dur + 0.3f);
        }

        /// <summary>Flecha gigante caindo do céu (só visual). Chega em "time" segundos.</summary>
        public static GameObject SpawnGiantArrow(Vector3 from, Vector3 to, float time)
        {
            var go = new GameObject("flecha_gigante");
            go.transform.SetParent(FX.Root, false);
            Color glow = new Color(0.55f, 1f, 0.6f);
            var shaft = U.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0f, -1.4f), new Vector3(0.22f, 1.6f, 0.22f), U.Hex("6a4a2a"));
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // eixo Y do cilindro → +Z (direção do voo)
            var tip = U.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, 0.35f), new Vector3(0.5f, 0.5f, 0.9f), glow);
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tip.GetComponent<Renderer>().sharedMaterial = U.Lit(glow, 0.6f, glow * 4f);
            for (int i = 0; i < 4; i++)
            {
                var f = U.Prim(PrimitiveType.Cube, go.transform, Quaternion.Euler(0f, 0f, i * 90f) * new Vector3(0f, 0.25f, -2.8f), new Vector3(0.04f, 0.5f, 0.7f), glow);
                f.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                f.GetComponent<Renderer>().sharedMaterial = U.Lit(glow, 0.4f, glow * 2f);
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lt = new GameObject("luz"); lt.transform.SetParent(go.transform, false);
            var l = lt.AddComponent<Light>(); l.type = LightType.Point; l.color = glow; l.intensity = 5f; l.range = 9f; l.shadows = LightShadows.None;
            var tr = TrailFor(go.transform, glow, 1.1f, 0.35f);
            var mv = go.AddComponent<FxMover>();
            mv.faceMotion = true; mv.trailR = tr; mv.spinAxis = Vector3.forward; mv.spinSpeed = 720f;
            mv.Setup(from, to, time);
            return go;
        }

        /// <summary>Explosão da flecha gigante.</summary>
        public static void GiantArrowImpact(Vector3 pos, float r)
        {
            Crack(pos, r * 1.6f, Leaf, 2.5f, Random.Range(0f, 360f));
            Shockwave(pos, r * 1.3f, Cyan, 0.45f);
            DelayedRing(pos, r * 1.8f, Leaf, 0.5f, 0.1f);
            var ps = Sys("arrow_blast", pos + Vector3.up * 0.4f, Leaf, true, 0.3f, 0.7f, 6f, 13f, 0.08f, 0.18f, 0.6f);
            Hemisphere(ps, 0.4f); Bursts(ps, 80); Stretch(ps, 2f, 0.04f); Grad(ps, Color.white, Cyan, 1f); ps.Play();
            Leaves(pos + Vector3.up * 0.5f, Vector3.up, 30);
            LightColumn(pos, r * 0.4f, 8f, Leaf, 0.6f);
            FX.Dust(pos, U.Hex("c8c0a0"), 2f);
            FX.FlashLight(pos + Vector3.up * 1.5f, Leaf, 9f, r * 3f, 0.7f);
        }

        /// <summary>Meteoro caindo (só visual). Chega em "time" segundos.</summary>
        public static GameObject SpawnMeteor(Vector3 from, Vector3 to, float time, float size)
        {
            var go = new GameObject("meteoro");
            go.transform.SetParent(FX.Root, false);
            var mat = U.Lit(U.Hex("5a2a1a"), 0.2f, FireDeep * 2.5f);
            var core = U.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * size, Fire);
            core.GetComponent<Renderer>().sharedMaterial = mat;
            for (int i = 0; i < 4; i++)
            {
                var b = U.Prim(PrimitiveType.Cube, go.transform, Random.onUnitSphere * size * 0.38f, Vector3.one * size * Random.Range(0.35f, 0.5f), Fire);
                b.transform.localRotation = Random.rotation;
                b.GetComponent<Renderer>().sharedMaterial = mat;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // halo
            var halo = FX.NewSystem("meteoro_fogo", from, true);
            halo.transform.SetParent(go.transform, false);
            halo.transform.localPosition = Vector3.zero;
            var m = halo.main;
            m.loop = true; m.duration = 1f; m.stopAction = ParticleSystemStopAction.None;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 1.1f);
            m.startColor = Fire;
            Sphere(halo, size * 0.4f); Rate(halo, 90f); Noise(halo, 0.8f, 1.5f); Grad(halo, FireCore, FireDeep, 1f);
            var sol = halo.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            halo.Play();
            // fumaça escura no rastro (filha do halo: some junto)
            var smoke = FX.NewSystem("meteoro_fumaca", from, false);
            smoke.transform.SetParent(halo.transform, false);
            var sm = smoke.main;
            sm.loop = true; sm.duration = 1f; sm.stopAction = ParticleSystemStopAction.None;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            sm.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f);
            sm.startColor = SmokeCol;
            Sphere(smoke, size * 0.4f); Rate(smoke, 35f); Grad(smoke, new Color(0.3f, 0.2f, 0.15f), new Color(0.1f, 0.1f, 0.1f), 0.6f);
            smoke.Play();
            var lt = new GameObject("luz"); lt.transform.SetParent(go.transform, false);
            var l = lt.AddComponent<Light>(); l.type = LightType.Point; l.color = Fire; l.intensity = 4f + size * 2f; l.range = 6f + size * 3f; l.shadows = LightShadows.None;
            var fl = lt.AddComponent<FxFlicker>(); fl.baseIntensity = l.intensity; fl.amount = 0.25f;
            var tr = TrailFor(go.transform, Fire, size * 0.9f, 0.4f);
            var mv = go.AddComponent<FxMover>();
            mv.trail = halo; mv.trailR = tr; mv.spinAxis = new Vector3(1f, 1f, 0f); mv.spinSpeed = 300f;
            mv.Setup(from, to, time);
            return go;
        }

        /// <summary>Chão em chamas por "dur" segundos (só visual; o dano é de quem chama).</summary>
        public static void FireField(Vector3 center, float r, float dur)
        {
            var fl = Sys("fire_field", center + Vector3.up * 0.1f, Fire, true, 0.5f, 1.0f, 0.8f, 2.2f, 0.3f, 0.8f, -0.25f, dur);
            fl.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(fl, 10f, r); Rate(fl, 120f); Noise(fl, 0.8f, 1.4f); FxUtil.FireGrad(fl); fl.Play();
            // línguas de fogo altas e esparsas por cima (fogo em camadas)
            var tg = Sys("fire_field_tongues", center + Vector3.up * 0.1f, Fire, true, 0.5f, 0.9f, 2.5f, 4f, 0.5f, 0.9f, -0.3f, dur);
            tg.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(tg, 6f, r * 0.85f); Rate(tg, 18f); Noise(tg, 1f, 2f); FxUtil.FireGrad(tg); tg.Play();
            var em = Sys("fire_field_embers", center + Vector3.up * 0.1f, Ember, true, 0.8f, 1.5f, 1.5f, 3.5f, 0.04f, 0.09f, -0.1f, dur);
            em.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(em, 20f, r); Rate(em, 60f); Noise(em, 1.2f, 1.2f); Grad(em, FireCore, Ember, 1f); em.Play();
            var sm = Sys("fire_field_smoke", center + Vector3.up * 0.6f, SmokeCol, false, 1.2f, 2f, 0.6f, 1.4f, 1f, 2f, -0.06f, dur);
            sm.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(sm, 15f, r * 0.8f); Rate(sm, 10f); Grad(sm, new Color(0.25f, 0.18f, 0.14f), new Color(0.1f, 0.1f, 0.1f), 0.5f); sm.Play();
            StaticLight(center + Vector3.up * 1.2f, Fire, 4f, r * 2f, dur + 0.3f);
        }

        /// <summary>Luz pontual fixa que tremula por "life" segundos (fora do pool: dura bastante).</summary>
        public static void StaticLight(Vector3 pos, Color c, float intensity, float range, float life)
        {
            var go = new GameObject("luz_area");
            go.transform.SetParent(FX.Root, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            var fl = go.AddComponent<FxFlicker>(); fl.baseIntensity = intensity; fl.amount = 0.3f;
            var e = go.AddComponent<FxAutoEnd>(); e.life = life;
        }

        static TrailRenderer TrailFor(Transform parent, Color c, float width, float time)
        {
            var tg = new GameObject("rastro");
            tg.transform.SetParent(parent, false);
            var tr = tg.AddComponent<TrailRenderer>();
            tr.sharedMaterial = U.Fx(true);
            tr.time = time;
            tr.widthMultiplier = width;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.startColor = new Color(c.r, c.g, c.b, 0.9f);
            tr.endColor = new Color(c.r, c.g, c.b, 0f);
            tr.minVertexDistance = 0.15f;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            return tr;
        }

        // ==================================================================================== projéteis
        /// <summary>Impacto/explosão visual de um projétil (o dano é do Projectile).</summary>
        public static void ProjectileImpact(string kind, Vector3 pos, Color color, float aoe, bool explode)
        {
            Vector3 ground = new Vector3(pos.x, pos.y - 1.05f, pos.z);
            switch (kind)
            {
                case "fireball":
                case "bullet_explosive":
                    if (explode || kind == "bullet_explosive") FireExplosion(pos, ground, Mathf.Max(aoe, kind == "bullet_explosive" ? 2.5f : 1.5f), kind == "fireball" ? 1f : 0.85f);
                    else FireSplash(pos, 1f);
                    break;
                case "frost":
                    FrostImpact(pos, ground, aoe, explode);
                    break;
                case "arrow":
                case "pierce":
                case "longarrow":
                case "heavy_arrow":
                case "pin_arrow":
                {
                    float sz = kind == "heavy_arrow" ? 1.6f : kind == "longarrow" || kind == "pierce" ? 1.15f : 1f;
                    Color c = kind == "pin_arrow" ? ShadowPurple : color;
                    ArrowImpact(pos, c, sz);
                    if (kind == "heavy_arrow") { WindRing(pos, Vector3.up, Gold, 0.6f, 24); FX.FlashLight(pos, Gold, 3f, 4f, 0.15f); }
                    if (kind == "pin_arrow")
                    {
                        FX.Ring(ground, 1.2f, ShadowPurple, 0.3f, 0.3f);
                        Sparks(pos, Steel, 8, 1.5f, 3.5f, 1f);
                    }
                    if (explode) { FX.Burst(pos, color, 1.2f, 30); Shockwave(ground, aoe, color, 0.3f); }
                    break;
                }
                case "bullet":
                {
                    Sparks(pos, Muzzle, 9, 3f, 7f, 1.4f);
                    var fl = Sys("bala_impacto", pos, Color.white, true, 0.05f, 0.08f, 0f, 0f, 0.45f, 0.6f);
                    fl.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
                    Bursts(fl, 1); Grad(fl, Color.white, Muzzle, 1f); fl.Play();
                    var d = Sys("bala_poeira", pos, new Color(0.75f, 0.7f, 0.62f, 0.4f), false, 0.25f, 0.45f, 0.5f, 1.5f, 0.15f, 0.3f, -0.05f);
                    Sphere(d, 0.1f); Bursts(d, 4); FxUtil.AlphaFade(d, new Color(0.8f, 0.75f, 0.68f), new Color(0.55f, 0.5f, 0.45f), 0.4f); d.Play();
                    if (explode) { FX.Burst(pos, color, 1.2f, 30); Shockwave(ground, aoe, color, 0.3f); }
                    break;
                }
                case "holy":
                {
                    var ps = Sys("sagrado_impacto", pos, Gold, true, 0.3f, 0.55f, 1.5f, 4f, 0.06f, 0.13f, -0.2f);
                    Sphere(ps, 0.15f); Bursts(ps, explode ? 36 : 16); Grad(ps, Color.white, Gold, 1f); ps.Play();
                    var cr = Sys("sagrado_cruz", pos, Holy, true, 0.25f, 0.3f, 0f, 0f, 0.8f, 1f);
                    cr.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Cross());
                    Bursts(cr, 1); Grad(cr, Color.white, Gold, 1f); cr.Play();
                    if (explode) { Shockwave(ground, aoe, Gold, 0.35f); LightColumn(ground, aoe * 0.4f, 4f, Holy, 0.4f); }
                    FX.FlashLight(pos, Gold, explode ? 4.5f : 2.4f, explode ? aoe + 4f : 3.2f, 0.22f);
                    break;
                }
                case "axe_spin":
                {
                    bool frenzy = color.g < 0.2f;
                    Sparks(pos, Steel, 10, 3f, 7f, 1.3f);
                    BloodSpray(pos, Vector3.zero, frenzy ? 16 : 8, 0f, frenzy ? 4.5f : 3f);
                    FX.FlashLight(pos, color, 2.5f, 3.5f, 0.15f);
                    if (explode) Shockwave(ground, aoe, color, 0.3f);
                    break;
                }
                default: // orb / arcane / homing_orb
                {
                    var ps = Sys("orb_pop", pos, color, true, 0.2f, 0.45f, 2f, 5f, 0.08f, 0.18f);
                    Sphere(ps, 0.15f); Bursts(ps, explode ? 36 : 16); Grad(ps, Color.white, color, 1f); ps.Play();
                    var st = Sys("orb_estrela", pos, Color.white, true, 0.12f, 0.16f, 0f, 0f, 0.8f, 1.1f);
                    st.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
                    var stm = st.main; stm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
                    Bursts(st, 1); Grad(st, Color.white, color, 1f); st.Play();
                    if (explode) { Shockwave(ground, aoe, color, 0.35f); RuneCircle(ground, aoe, color, 0.4f, 200f); }
                    FX.FlashLight(pos, color, explode ? 4.5f : 2.2f, explode ? aoe + 4f : 3f, 0.22f);
                    break;
                }
            }
        }

        /// <summary>Flecha acertando: faíscas, lascas de madeira, poeirinha e brilho na cor.</summary>
        static void ArrowImpact(Vector3 pos, Color c, float size)
        {
            Sparks(pos, Color.Lerp(c, Color.white, 0.4f), Mathf.RoundToInt(10 * size), 2f, 5.5f * size, 1f);
            var sp = Sys("lascas", pos, U.Hex("8a6a42"), false, 0.35f, 0.6f, 1.5f * size, 3.5f * size, 0.03f, 0.06f, 2f, 0.2f);
            Sphere(sp, 0.05f);
            MeshParticles(sp, U.Lit(U.Hex("8a6a42"), 0.2f));
            var m = sp.main; m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(0.02f, 0.03f); m.startSizeY = new ParticleSystem.MinMaxCurve(0.02f, 0.03f); m.startSizeZ = new ParticleSystem.MinMaxCurve(0.08f * size, 0.14f * size);
            var sz = sp.sizeOverLifetime; sz.enabled = false;
            Bursts(sp, Mathf.RoundToInt(4 * size)); sp.Play();
            var fl = Sys("flecha_brilho", pos, c, true, 0.08f, 0.12f, 0f, 0f, 0.5f * size, 0.7f * size);
            fl.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true, FxTex.Star());
            Bursts(fl, 1); Grad(fl, Color.white, c, 1f); fl.Play();
            if (size > 1.2f) FX.Dust(new Vector3(pos.x, pos.y - 1f, pos.z), U.Hex("c8c0a0"), 0.6f);
        }

        /// <summary>Explosão de fogo em camadas: clarão, bola de fogo, línguas de chama, brasas, fumaça, chão queimado.</summary>
        public static void FireExplosion(Vector3 pos, Vector3 ground, float aoe, float scale)
        {
            var flash = Sys("fogo_clarao", pos, FireCore, true, 0.08f, 0.12f, 0f, 0f, 2.2f * scale, 2.8f * scale);
            Bursts(flash, 1); Grad(flash, Color.white, FireCore, 1f); flash.Play();
            var ball = Sys("fogo_bola", pos, Fire, true, 0.3f, 0.6f, 2.5f * scale, 6f * scale, 0.45f * scale, 0.9f * scale, -0.4f);
            Sphere(ball, 0.3f); Bursts(ball, Mathf.RoundToInt(36 * scale)); Noise(ball, 1.2f, 1.5f); FxUtil.FireGrad(ball);
            var br = ball.main; br.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ball.Play();
            var tongues = Sys("fogo_linguas", ground + Vector3.up * 0.1f, Fire, true, 0.4f, 0.8f, 2f, 4.5f, 0.3f * scale, 0.6f * scale, -0.3f, 0.35f);
            tongues.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(tongues, 12f, aoe * 0.5f); Rate(tongues, 70f * scale); Noise(tongues, 0.8f, 2f); FxUtil.FireGrad(tongues); tongues.Play();
            Smoke(ground, aoe * 0.4f, Mathf.RoundToInt(12 * scale), 1f * scale);
            Embers(ground, aoe * 0.4f, Ember, Mathf.RoundToInt(28 * scale), 1.1f, 4.5f);
            Crack(ground, aoe * 1.4f, Fire, 1.6f, Random.Range(0f, 360f), 0.7f);
            var scorch = FX.FlatQuad("chao_queimado", ground + Vector3.up * 0.025f, U.SoftTexture(), new Color(0.06f, 0.04f, 0.03f, 0.7f), false);
            var tw = scorch.AddComponent<FxTween>(); tw.hold = 0.6f;
            tw.Set(2.5f, Vector3.one * aoe * 1.6f, Vector3.one * aoe * 1.9f, new Color(0.06f, 0.04f, 0.03f, 0.7f), 1f, 0f);
            Shockwave(ground, aoe, Fire, 0.35f);
            FX.FlashLight(pos, Fire, 6f * scale, aoe + 5f, 0.5f);
        }

        /// <summary>Respingo de fogo (acerto sem explosão).</summary>
        public static void FireSplash(Vector3 pos, float scale)
        {
            var fl = Sys("fogo_respingo", pos, Fire, true, 0.2f, 0.45f, 1.5f, 3.5f, 0.2f * scale, 0.45f * scale, -0.4f);
            Sphere(fl, 0.15f); Bursts(fl, Mathf.RoundToInt(14 * scale)); Noise(fl, 1f, 2f); FxUtil.FireGrad(fl); fl.Play();
            Sparks(pos, Ember, Mathf.RoundToInt(12 * scale), 2f, 5f, 0.6f);
            var sm = Sys("fogo_fumacinha", pos, SmokeCol, false, 0.5f, 0.9f, 0.3f, 1f, 0.3f, 0.55f, -0.15f);
            Sphere(sm, 0.1f); Bursts(sm, 4); FxUtil.AlphaFade(sm, new Color(0.3f, 0.2f, 0.15f), new Color(0.1f, 0.1f, 0.1f), 0.5f); sm.Play();
            FX.FlashLight(pos, Fire, 3f, 4f, 0.2f);
        }

        /// <summary>Gelo/água: estilhaços, respingo de gotas, névoa fria e geada no chão.</summary>
        public static void FrostImpact(Vector3 pos, Vector3 ground, float aoe, bool explode)
        {
            IceShards(pos, Vector3.zero, explode ? 26 : 10, explode ? 1f : 0.5f);
            var drops = Sys("gelo_gotas", pos, new Color(0.75f, 0.92f, 1f), true, 0.3f, 0.6f, 2f, explode ? 6f : 4f, 0.04f, 0.08f, 1.6f);
            Hemisphere(drops, 0.1f); Bursts(drops, explode ? 30 : 14); Stretch(drops, 1.3f, 0.05f); Grad(drops, Color.white, Ice, 1f); drops.Play();
            Mist(ground + Vector3.up * 0.3f, Ice, explode ? 18 : 6, explode ? aoe * 0.5f : 0.4f);
            var frost = FX.FlatQuad("geada", ground + Vector3.up * 0.03f, U.SoftTexture(), new Color(0.7f, 0.92f, 1f, 0.5f), true);
            var tw = frost.AddComponent<FxTween>(); tw.hold = 0.5f;
            float fs = explode ? Mathf.Max(1.5f, aoe) * 2f : 1.2f;
            tw.Set(1.6f, Vector3.one * fs * 0.6f, Vector3.one * fs, new Color(0.7f, 0.92f, 1f, 0.5f), 1f, 0f);
            if (explode)
            {
                Crack(ground, aoe * 1.3f, Ice, 1.8f, Random.Range(0f, 360f), 0.5f);
                Shockwave(ground, aoe, Ice, 0.35f);
            }
            FX.FlashLight(pos, Ice, explode ? 5f : 2.5f, explode ? aoe + 4f : 3.5f, 0.35f);
        }

        // ==================================================================================== BLOCOS DE CONSTRUÇÃO
        static Transform PlayerVisual()
        {
            var g = Game.I;
            if (g == null || g.player == null || g.player.visual == null) return null;
            return g.player.visual.transform;
        }

        /// <summary>Sistema de partículas configurado (one-shot). Faixas: vida, velocidade, tamanho.</summary>
        public static ParticleSystem Sys(string name, Vector3 pos, Color c, bool additive, float life0, float life1,
                                         float spd0, float spd1, float sz0, float sz1, float gravity = 0f, float duration = 0.5f)
        {
            var ps = FX.NewSystem(name, pos, additive);
            var m = ps.main;
            m.duration = duration;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
            m.startSpeed = new ParticleSystem.MinMaxCurve(spd0, spd1);
            m.startSize = new ParticleSystem.MinMaxCurve(sz0, sz1);
            m.startColor = c;
            m.gravityModifier = gravity;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            return ps;
        }

        public static void Bursts(ParticleSystem ps, int count, float time = 0f)
        {
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(time, (short)Mathf.Clamp(count, 1, 1000)) });
        }

        public static void Rate(ParticleSystem ps, float perSecond)
        {
            var em = ps.emission; em.rateOverTime = perSecond;
        }

        public static void Sphere(ParticleSystem ps, float r)
        {
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = Mathf.Max(0.01f, r);
        }

        public static void Hemisphere(ParticleSystem ps, float r)
        {
            // "meia esfera" para cima: cone bem aberto (75°) cujo eixo +Z é girado -90° em X → aponta para +Y
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 75f; sh.radius = Mathf.Max(0.01f, r);
            sh.rotation = new Vector3(-90f, 0f, 0f);
        }

        /// <summary>Cone ao longo do +Z do objeto (gire o transform do sistema para apontar).</summary>
        public static void Cone(ParticleSystem ps, float angle, float radius)
        {
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = Mathf.Max(0.01f, radius);
        }

        /// <summary>Círculo deitado no chão (plano XZ); partículas saem radialmente para fora. thickness 0 = só a borda.</summary>
        public static void FlatCircle(ParticleSystem ps, float radius, float thickness)
        {
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = Mathf.Max(0.01f, radius);
            sh.radiusThickness = Mathf.Clamp01(thickness);
            sh.rotation = new Vector3(90f, 0f, 0f);   // XY → XZ
        }

        public static void Stretch(ParticleSystem ps, float length, float velScale = 0.03f)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = length; r.velocityScale = velScale;
        }

        public static void Noise(ParticleSystem ps, float strength, float freq)
        {
            var n = ps.noise; n.enabled = true; n.strength = strength; n.frequency = freq; n.scrollSpeed = 0.6f;
        }

        public static void Grad(ParticleSystem ps, Color c0, Color c1, float a0)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 0.4f), new GradientColorKey(c1, 1f) },
                      new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(a0 * 0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        static void MeshParticles(ParticleSystem ps, Material mat)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = FX.CubeMesh();
            r.alignment = ParticleSystemRenderSpace.World;
            if (mat != null) r.sharedMaterial = mat;
            var m = ps.main;
            m.startRotation3D = true;
            m.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var rol = ps.rotationOverLifetime; rol.enabled = true; rol.separateAxes = true;
            rol.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rol.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rol.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        }

        // ------------------------------------------------------------------ efeitos compostos
        /// <summary>Faíscas com gravidade (metal, impacto).</summary>
        public static void Sparks(Vector3 pos, Color c, int count, float spd0, float spd1, float gravity)
        {
            var ps = Sys("sparks", pos, c, true, 0.2f, 0.5f, spd0, spd1, 0.03f, 0.08f, gravity);
            Sphere(ps, 0.1f); Bursts(ps, count); Stretch(ps, 1.6f, 0.05f); Grad(ps, Color.white, c, 1f); ps.Play();
        }

        /// <summary>Jato em cone numa direção do chão (vento, sangue, fogo).</summary>
        public static void ConeBurst(Vector3 pos, Vector3 dir, Color c, float angle, float spd0, float spd1, int count, float gravity)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            var ps = Sys("cone", pos, c, true, 0.15f, 0.4f, spd0, spd1, 0.04f, 0.1f, gravity);
            ps.transform.rotation = Quaternion.LookRotation(dir.normalized);
            Cone(ps, angle, 0.1f); Bursts(ps, count); Stretch(ps, 2.2f, 0.04f); Grad(ps, Color.white, c, 1f); ps.Play();
        }

        /// <summary>Anel de partículas perpendicular à direção (estrondo sônico do disparo).</summary>
        public static void WindRing(Vector3 pos, Vector3 dir, Color c, float radius, int count)
        {
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            var ps = Sys("wind_ring", pos, c, true, 0.15f, 0.3f, 2.5f, 4f, 0.05f, 0.09f);
            ps.transform.rotation = Quaternion.LookRotation(dir.normalized);   // círculo no plano XY local = perpendicular a dir
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius; sh.radiusThickness = 0f;
            Bursts(ps, count); Stretch(ps, 1.4f, 0.03f); Grad(ps, Color.white, c, 0.9f); ps.Play();
        }

        /// <summary>Folhas verdes (partículas alpha que flutuam).</summary>
        public static void Leaves(Vector3 pos, Vector3 dir, int count)
        {
            var ps = Sys("leaves", pos, Leaf, false, 0.6f, 1.1f, 1.5f, 4f, 0.08f, 0.15f, 0.15f);
            ps.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward);
            Cone(ps, 45f, 0.2f); Bursts(ps, count); Noise(ps, 1.5f, 1f);
            var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            Grad(ps, U.Hex("b8f0a0"), U.Hex("5aa04a"), 0.9f); ps.Play();
        }

        /// <summary>Brasas subindo com turbulência.</summary>
        public static void Embers(Vector3 pos, float radius, Color c, int count, float life, float rise)
        {
            var ps = Sys("embers", pos + Vector3.up * 0.2f, c, true, life * 0.6f, life, rise * 0.4f, rise, 0.04f, 0.1f, -0.2f);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(ps, 25f, Mathf.Max(0.05f, radius)); Bursts(ps, count); Noise(ps, 1.2f, 1.2f); Grad(ps, FireCore, c, 1f); ps.Play();
        }

        /// <summary>Fumaça escura (material alpha).</summary>
        public static void Smoke(Vector3 pos, float radius, int count, float size)
        {
            var ps = Sys("smoke", pos + Vector3.up * 0.4f, SmokeCol, false, 1.0f, 1.8f, 0.5f, 1.8f, 0.8f * size, 1.6f * size, -0.08f);
            Hemisphere(ps, Mathf.Max(0.1f, radius));
            var sz = ps.sizeOverLifetime; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));
            Bursts(ps, count); Noise(ps, 0.4f, 0.6f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.35f, 0.22f, 0.15f), 0f), new GradientColorKey(new Color(0.12f, 0.1f, 0.1f), 0.3f), new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ps.Play();
        }

        /// <summary>Névoa fria (alpha, clara).</summary>
        public static void Mist(Vector3 pos, Color c, int count, float radius)
        {
            var ps = Sys("mist", pos, new Color(c.r, c.g, c.b, 0.5f), false, 0.6f, 1.1f, 0.3f, 1.2f, 0.5f, 1.1f, -0.05f);
            Sphere(ps, Mathf.Max(0.05f, radius)); Bursts(ps, count); Noise(ps, 0.3f, 0.8f);
            Grad(ps, Color.white, c, 0.45f); ps.Play();
        }

        /// <summary>Estilhaços de gelo (cubos alongados brilhantes, com gravidade). dir zero = todas as direções.</summary>
        public static void IceShards(Vector3 pos, Vector3 dir, int count, float size)
        {
            var ps = Sys("ice_shards", pos, Ice, true, 0.4f, 0.8f, 3f, 7f, 0.1f, 0.2f, 1.4f);
            if (dir.sqrMagnitude > 0.0001f) { ps.transform.rotation = Quaternion.LookRotation(dir.normalized); Cone(ps, 35f, 0.1f); }
            else Sphere(ps, 0.2f);
            MeshParticles(ps, U.Fx(true, U.WhiteTexture()));
            var m = ps.main; m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(0.05f * size, 0.1f * size);
            m.startSizeY = new ParticleSystem.MinMaxCurve(0.05f * size, 0.1f * size);
            m.startSizeZ = new ParticleSystem.MinMaxCurve(0.25f * size, 0.5f * size);
            Bursts(ps, count); Grad(ps, Color.white, Ice, 1f); ps.Play();
        }

        /// <summary>Pedras/destroços saltando do chão (colidem com o chão).</summary>
        public static void Rocks(Vector3 pos, float radius, Color c, int count, float size)
        {
            var ps = Sys("rocks", pos + Vector3.up * 0.15f, c, false, 0.8f, 1.2f, 4f * size, 8f * size, 0.12f * size, 0.32f * size, 2.2f, 0.3f);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Cone(ps, 40f, Mathf.Max(0.05f, radius));
            MeshParticles(ps, U.Lit(c, 0.1f));
            var sz = ps.sizeOverLifetime; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.6f));
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
            col.bounce = 0.3f; col.dampen = 0.5f; col.quality = ParticleSystemCollisionQuality.Medium; col.radiusScale = 0.6f;
            Bursts(ps, count); ps.Play();
        }

        /// <summary>Partículas que convergem para o centro (carga de energia).</summary>
        public static void Implode(Vector3 pos, float radius, Color c, float time, int count)
        {
            float t = Mathf.Max(0.08f, time);
            var ps = Sys("implode", pos, c, true, t, t, -radius / t, -radius / t * 0.9f, 0.06f, 0.14f);
            Sphere(ps, radius);
            var sh = ps.shape; sh.radiusThickness = 0f;
            Bursts(ps, count); Stretch(ps, 1.6f, 0.03f);
            var sz = ps.sizeOverLifetime; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1f));
            Grad(ps, c, Color.white, 1f); ps.Play();
        }

        /// <summary>Partículas em espiral subindo (cura, bênção).</summary>
        public static void Spiral(Vector3 pos, float radius, Color c, float time, float height, int count)
        {
            var ps = Sys("spiral", pos + Vector3.up * 0.1f, c, true, time * 0.7f, time, 0f, 0f, 0.07f, 0.15f, 0f, time * 0.5f);
            FlatCircle(ps, radius, 0f);
            Rate(ps, count / Mathf.Max(0.1f, time * 0.5f));
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0f); v.y = new ParticleSystem.MinMaxCurve(height / time); v.z = new ParticleSystem.MinMaxCurve(0f);
            v.orbitalY = new ParticleSystem.MinMaxCurve(4f);
            Grad(ps, Color.white, c, 1f); ps.Play();
        }

        /// <summary>Penas de luz descendo devagar (Bênção do Guardião).</summary>
        public static void Feathers(Vector3 pos, int count)
        {
            var ps = Sys("feathers", pos, Holy, true, 1.2f, 1.8f, 0.3f, 1f, 0.1f, 0.2f, 0.05f);
            Sphere(ps, 1.5f); Bursts(ps, count); Noise(ps, 0.8f, 0.5f);
            var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            Grad(ps, Color.white, Gold, 0.9f); ps.Play();
        }

        /// <summary>Onda de choque: anel brilhante + anel escuro atrasado (falsa distorção) + poeira radial.</summary>
        public static void Shockwave(Vector3 pos, float radius, Color c, float time)
        {
            FX.Ring(pos, radius, c, time, 0.15f);
            // anel escuro um pouco maior e atrasado: "dobra" a imagem por contraste
            var dark = FX.FlatQuad("shock_dark", pos + Vector3.up * 0.045f, U.RingTexture(), new Color(0f, 0f, 0f, 0.45f), false);
            var tw = dark.AddComponent<FxTween>();
            tw.delay = time * 0.12f;
            tw.Set(time * 1.1f, Vector3.one * radius * 0.4f, Vector3.one * radius * 2.15f, new Color(0f, 0f, 0f, 0.45f), 1f, 0f);
            // anel fino quente
            var hot = FX.FlatQuad("shock_hot", pos + Vector3.up * 0.06f, U.RingTexture(), Color.Lerp(c, Color.white, 0.6f), true);
            hot.AddComponent<FxTween>().Set(time * 0.7f, Vector3.one * radius * 0.2f, Vector3.one * radius * 1.7f, Color.Lerp(c, Color.white, 0.6f), 1f, 0f);
            var ps = Sys("shock_dust", pos + Vector3.up * 0.15f, new Color(0.75f, 0.68f, 0.58f, 0.45f), false, 0.3f, 0.55f, radius * 2.5f, radius * 3.5f, 0.3f, 0.6f);
            FlatCircle(ps, 0.3f, 0f); Bursts(ps, Mathf.Clamp(Mathf.RoundToInt(radius * 10f), 8, 80));
            Grad(ps, new Color(0.8f, 0.72f, 0.6f), new Color(0.6f, 0.52f, 0.44f), 0.45f); ps.Play();
        }

        /// <summary>Anel expandindo com atraso.</summary>
        public static void DelayedRing(Vector3 pos, float radius, Color c, float time, float delay)
        {
            var q = FX.FlatQuad("ring_d", pos + Vector3.up * 0.05f, U.RingTexture(), c, true);
            var tw = q.AddComponent<FxTween>();
            tw.delay = delay;
            tw.Set(time, Vector3.one * radius * 0.3f, Vector3.one * radius * 2f, c, 1f, 0f);
        }

        /// <summary>Rachaduras no chão (decal escuro + brilho colorido que apaga antes). stretch &lt; 1 alonga na direção do yaw.</summary>
        public static void Crack(Vector3 pos, float size, Color glow, float life, float yawDeg, float stretch = 1f)
        {
            Vector3 p = pos + Vector3.up * 0.03f;
            Vector3 scale = new Vector3(size * stretch, size, 1f);   // quad deitado: X = largura, Y local = profundidade (vira Z no mundo)
            var dark = FX.FlatQuad("crack", p, CrackTexture(), new Color(0.05f, 0.04f, 0.04f, 0.85f), false);
            dark.transform.rotation = Quaternion.Euler(90f, yawDeg, 0f);
            var td = dark.AddComponent<FxTween>();
            td.hold = 0.6f;
            td.Set(life, scale * 0.85f, scale, new Color(0.05f, 0.04f, 0.04f, 0.85f), 1f, 0f);
            var lit = FX.FlatQuad("crack_glow", p + Vector3.up * 0.005f, CrackTexture(), glow, true);
            lit.transform.rotation = Quaternion.Euler(90f, yawDeg, 0f);
            var tl = lit.AddComponent<FxTween>();
            tl.hold = 0.25f;
            tl.Set(life * 0.55f, scale * 0.85f, scale, glow, 1f, 0f);
        }

        /// <summary>Círculo de runas girando no chão.</summary>
        public static void RuneCircle(Vector3 pos, float radius, Color c, float time, float spinDegPerSec)
        {
            var q = FX.FlatQuad("runes", pos + Vector3.up * 0.06f, RuneTexture(), c, true);
            q.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            var tw = q.AddComponent<FxTween>();
            tw.spin = spinDegPerSec; tw.fadeIn = 0.08f; tw.hold = 0.6f;
            tw.Set(time, Vector3.one * radius * 1.5f, Vector3.one * radius * 2f, c, 1f, 0f);
        }

        /// <summary>Coluna de luz (cilindro aberto com degradê de alpha para cima).</summary>
        public static GameObject LightColumn(Vector3 pos, float radius, float height, Color c, float time)
        {
            var go = new GameObject("light_column");
            go.transform.SetParent(FX.Root, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = CylinderMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = U.Fx(true, U.WhiteTexture());
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var tw = go.AddComponent<FxTween>();
            tw.fadeIn = 0.06f; tw.hold = 0.35f; tw.spin = 90f;
            tw.Set(time, new Vector3(radius * 0.3f, height * 0.6f, radius * 0.3f), new Vector3(radius, height, radius), c, 0.9f, 0f);
            return go;
        }

        /// <summary>Bolha/cúpula translúcida que cresce e some.</summary>
        public static GameObject Bubble(Vector3 pos, float radius, Color c, float time)
        {
            var go = Dome(pos, radius, c);
            var tw = go.AddComponent<FxTween>();
            tw.hold = 0.3f;
            tw.Set(time, Vector3.one * radius * 0.5f, Vector3.one * radius, c, 0.9f, 0f);
            return go;
        }

        /// <summary>Cúpula (hemisfério) translúcida; sem animação (quem chama controla).</summary>
        public static GameObject Dome(Vector3 pos, float radius, Color c)
        {
            var go = new GameObject("dome");
            go.transform.SetParent(FX.Root, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * radius;
            go.AddComponent<MeshFilter>().sharedMesh = HemisphereMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = U.Fx(true, StripeTexture());
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            FX.SetColor(mr, c);
            return go;
        }

        /// <summary>Feixe reto (LineRenderer) que some.</summary>
        public static void Beam(Vector3 a, Vector3 b, Color c, float width, float time)
        {
            var go = new GameObject("beam");
            go.transform.SetParent(FX.Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.widthMultiplier = width;
            lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);
            lr.sharedMaterial = U.Fx(true);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = Color.white; lr.endColor = c;
            go.AddComponent<FxAnim>().SetupLine(lr, time, c);
        }

        /// <summary>
        /// Rastro de "afterimage": cópias translúcidas da silhueta (SkinnedMeshRenderer.BakeMesh + malhas estáticas, ex.: arma).
        /// </summary>
        public static void Afterimage(Transform root, Color c, float time)
        {
            if (root == null) return;
            var mat = U.Fx(true, U.WhiteTexture());
            Color gc = new Color(c.r, c.g, c.b, 0.55f);
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr == null || !smr.enabled || !smr.gameObject.activeInHierarchy || smr.sharedMesh == null) continue;
                var m = new Mesh { name = "ghost" };
                smr.BakeMesh(m, true);                       // com escala: a cópia fica com escala 1
                var cols = new Color32[m.vertexCount];
                for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
                m.colors32 = cols;
                var tw = Ghost(m, smr.transform.position, smr.transform.rotation, Vector3.one, mat, gc, time);
                tw.ownedMesh = m;
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                Ghost(mf.sharedMesh, mf.transform.position, mf.transform.rotation, mf.transform.lossyScale, mat, gc, time * 0.8f);
            }
        }

        static FxTween Ghost(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Material mat, Color c, float time)
        {
            var go = new GameObject("afterimage");
            go.transform.SetParent(FX.Root, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            int sub = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[sub];
            for (int i = 0; i < sub; i++) mats[i] = mat;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var tw = go.AddComponent<FxTween>();
            tw.Set(Mathf.Max(0.05f, time), scale, scale * 1.03f, c, 1f, 0f);
            return tw;
        }

        /// <summary>
        /// Aura que segue um Transform (partículas subindo + luz tremulando) por "duration" segundos.
        /// Devolve o objeto (destrua antes se quiser cortar).
        /// </summary>
        public static GameObject AttachAura(Transform t, Color c, float duration, float rate, float radius, bool light = true)
        {
            if (t == null) return null;
            var ps = FX.NewSystem("aura", t.position, true);
            ps.transform.SetParent(t, false);
            ps.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var m = ps.main;
            m.loop = true;
            m.duration = 1f;
            m.stopAction = ParticleSystemStopAction.None;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            m.startColor = c;
            m.gravityModifier = -0.1f;
            Cone(ps, 8f, radius);
            Rate(ps, rate);
            Noise(ps, 0.8f, 1.2f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            Grad(ps, Color.white, c, 1f);
            ps.Play();
            if (light)
            {
                var lg = new GameObject("aura_luz");
                lg.transform.SetParent(ps.transform.parent, false);
                lg.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = c; l.range = 4.5f; l.intensity = 1.8f; l.shadows = LightShadows.None;
                var fl = lg.AddComponent<FxFlicker>(); fl.baseIntensity = 1.8f; fl.amount = 0.3f;
                var lgEnd = lg.AddComponent<FxAutoEnd>(); lgEnd.life = duration;
            }
            var end = ps.gameObject.AddComponent<FxAutoEnd>(); end.life = duration;
            return ps.gameObject;
        }

        // ------------------------------------------------------------------ malhas e texturas procedurais
        static Mesh cylMesh, hemiMesh;
        static Texture2D crackTex, runeTex, stripeTex;

        /// <summary>Cilindro aberto (raio 1, altura 1), alpha 1 embaixo → 0 no topo, dois lados.</summary>
        public static Mesh CylinderMesh()
        {
            if (cylMesh != null) return cylMesh;
            const int seg = 24;
            float[] ys = { 0f, 0.15f, 1f };
            float[] al = { 0.9f, 1f, 0f };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
            for (int r = 0; r < ys.Length; r++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    v.Add(new Vector3(Mathf.Cos(a), ys[r], Mathf.Sin(a)));
                    uv.Add(new Vector2((float)i / seg, ys[r]));
                    col.Add(new Color(1f, 1f, 1f, al[r]));
                }
            int w = seg + 1;
            for (int r = 0; r < ys.Length - 1; r++)
                for (int i = 0; i < seg; i++)
                {
                    int a0 = r * w + i, a1 = a0 + 1, b0 = a0 + w, b1 = b0 + 1;
                    tri.Add(a0); tri.Add(b0); tri.Add(a1); tri.Add(a1); tri.Add(b0); tri.Add(b1);
                    tri.Add(a0); tri.Add(a1); tri.Add(b0); tri.Add(a1); tri.Add(b1); tri.Add(b0);
                }
            cylMesh = new Mesh { name = "fx_cylinder" };
            cylMesh.SetVertices(v); cylMesh.SetUVs(0, uv); cylMesh.SetColors(col); cylMesh.SetTriangles(tri, 0);
            cylMesh.RecalculateBounds();
            return cylMesh;
        }

        /// <summary>Hemisfério (raio 1) com alpha mais forte na base, dois lados; UV v = latitude.</summary>
        public static Mesh HemisphereMesh()
        {
            if (hemiMesh != null) return hemiMesh;
            const int seg = 32, rings = 10;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float lat = (float)r / rings * Mathf.PI * 0.5f;     // 0 = base, 90° = topo
                float y = Mathf.Sin(lat), rad = Mathf.Cos(lat);
                float a = Mathf.Lerp(0.85f, 0.12f, Mathf.Pow((float)r / rings, 0.7f));
                for (int i = 0; i <= seg; i++)
                {
                    float th = i * Mathf.PI * 2f / seg;
                    v.Add(new Vector3(Mathf.Cos(th) * rad, y, Mathf.Sin(th) * rad));
                    uv.Add(new Vector2((float)i / seg, (float)r / rings));
                    col.Add(new Color(1f, 1f, 1f, a));
                }
            }
            int w = seg + 1;
            for (int r = 0; r < rings; r++)
                for (int i = 0; i < seg; i++)
                {
                    int a0 = r * w + i, a1 = a0 + 1, b0 = a0 + w, b1 = b0 + 1;
                    tri.Add(a0); tri.Add(b0); tri.Add(a1); tri.Add(a1); tri.Add(b0); tri.Add(b1);
                    tri.Add(a0); tri.Add(a1); tri.Add(b0); tri.Add(a1); tri.Add(b1); tri.Add(b0);
                }
            hemiMesh = new Mesh { name = "fx_hemisphere" };
            hemiMesh.SetVertices(v); hemiMesh.SetUVs(0, uv); hemiMesh.SetColors(col); hemiMesh.SetTriangles(tri, 0);
            hemiMesh.RecalculateBounds();
            return hemiMesh;
        }

        /// <summary>Faixas horizontais (linhas de energia da cúpula), por v.</summary>
        public static Texture2D StripeTexture()
        {
            if (stripeTex != null) return stripeTex;
            const int w = 4, h = 64;
            stripeTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "fx_stripes" };
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float band = Mathf.Abs(Mathf.Sin(v * Mathf.PI * 6f));
                float a = 0.35f + 0.65f * Mathf.Pow(band, 12f);
                for (int x = 0; x < w; x++) stripeTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            stripeTex.Apply();
            return stripeTex;
        }

        /// <summary>Rachaduras: galhos aleatórios (passeio aleatório) saindo do centro, com cratera no meio.</summary>
        public static Texture2D CrackTexture()
        {
            if (crackTex != null) return crackTex;
            const int n = 256;
            var a = new float[n * n];
            var rnd = new System.Random(7331);
            // cratera central
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - n * 0.5f, dy = y - n * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (n * 0.09f);
                    if (d < 1f) a[y * n + x] = Mathf.Max(a[y * n + x], (1f - d) * 0.9f);
                }
            int branches = 10;
            for (int b = 0; b < branches; b++)
            {
                float ang = b * Mathf.PI * 2f / branches + (float)rnd.NextDouble() * 0.5f;
                CrackWalk(a, n, rnd, n * 0.5f, n * 0.5f, ang, 70f + (float)rnd.NextDouble() * 50f, 3.4f, 0);
            }
            crackTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "fx_crack" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01(1f - Mathf.Pow(r, 4f));
                    float v = Mathf.Clamp01(a[y * n + x]) * edge;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(v * 255f));
                }
            crackTex.SetPixels32(px);
            crackTex.Apply(true);
            return crackTex;
        }

        static void CrackWalk(float[] a, int n, System.Random rnd, float x, float y, float ang, float len, float w, int depth)
        {
            int steps = Mathf.Max(2, Mathf.RoundToInt(len / 2f));
            for (int i = 0; i < steps; i++)
            {
                ang += ((float)rnd.NextDouble() - 0.5f) * 0.7f;
                x += Mathf.Cos(ang) * 2f;
                y += Mathf.Sin(ang) * 2f;
                float ww = Mathf.Max(0.8f, w * (1f - (float)i / steps * 0.75f));
                Stamp(a, n, x, y, ww);
                if (depth < 2 && rnd.NextDouble() < 0.05)
                {
                    float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                    CrackWalk(a, n, rnd, x, y, ang + side * (0.5f + (float)rnd.NextDouble() * 0.6f), len * 0.45f, ww * 0.6f, depth + 1);
                }
            }
        }

        static void Stamp(float[] a, int n, float cx, float cy, float r)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1)), x1 = Mathf.Min(n - 1, Mathf.CeilToInt(cx + r + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1)), y1 = Mathf.Min(n - 1, Mathf.CeilToInt(cy + r + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float v = Mathf.Clamp01(1f - d / r);
                    if (v <= 0f) continue;
                    v = Mathf.Sqrt(v);
                    int k = y * n + x;
                    if (v > a[k]) a[k] = v;
                }
        }

        /// <summary>Círculo mágico: anéis concêntricos, faixa de glifos, hexagrama e pequenos círculos nos vértices.</summary>
        public static Texture2D RuneTexture()
        {
            if (runeTex != null) return runeTex;
            const int n = 256;
            var P = new Vector2[6];
            for (int k = 0; k < 6; k++)
            {
                float ang = (90f + 60f * k) * Mathf.Deg2Rad;
                P[k] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.62f;
            }
            // dois triângulos do hexagrama
            int[,] segs = { { 0, 2 }, { 2, 4 }, { 4, 0 }, { 1, 3 }, { 3, 5 }, { 5, 1 } };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                    float r = p.magnitude;
                    float a = 0f;
                    if (r < 1f)
                    {
                        a = Mathf.Max(a, Band(r, 0.96f, 0.018f));
                        a = Mathf.Max(a, Band(r, 0.80f, 0.012f));
                        a = Mathf.Max(a, Band(r, 0.62f, 0.010f));
                        a = Mathf.Max(a, Band(r, 0.22f, 0.012f));
                        // glifos entre os anéis 0.80 e 0.96
                        if (r > 0.83f && r < 0.93f)
                        {
                            float th = Mathf.Atan2(p.y, p.x);
                            float s = (th / (Mathf.PI * 2f) + 0.5f) * 32f;
                            int si = Mathf.FloorToInt(s);
                            float f = s - si;
                            int h = Hash(si);
                            float rr = (r - 0.83f) / 0.10f;
                            if ((h & 1) != 0 && Mathf.Abs(f - 0.5f) < 0.07f) a = Mathf.Max(a, 0.95f);
                            if ((h & 2) != 0 && rr > 0.78f && f > 0.2f && f < 0.8f) a = Mathf.Max(a, 0.9f);
                            if ((h & 4) != 0 && Mathf.Abs(rr - 0.4f) < 0.12f && f > 0.25f && f < 0.75f) a = Mathf.Max(a, 0.9f);
                            if ((h & 8) != 0 && Mathf.Abs(f - 0.2f - rr * 0.6f) < 0.07f) a = Mathf.Max(a, 0.85f);
                            if ((h & 16) != 0 && rr < 0.2f && f > 0.15f && f < 0.85f) a = Mathf.Max(a, 0.85f);
                        }
                        for (int k = 0; k < 6; k++)
                        {
                            float d = SegDist(p, P[segs[k, 0]], P[segs[k, 1]]);
                            a = Mathf.Max(a, Mathf.Clamp01(1f - (d - 0.005f) / 0.01f));
                            a = Mathf.Max(a, Band((p - P[k]).magnitude, 0.06f, 0.01f));
                        }
                        a = Mathf.Max(a, 0.12f * (1f - r));   // brilho de fundo
                    }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            runeTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "fx_runes" };
            runeTex.SetPixels32(px);
            runeTex.Apply(true);
            return runeTex;
        }

        static float Band(float r, float c, float w) => Mathf.Clamp01(1f - Mathf.Abs(r - c) / w);

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (a + ab * t - p).magnitude;
        }

        static int Hash(int i)
        {
            unchecked
            {
                int h = i * 73856093 ^ 0x5bd1e995;
                h ^= h >> 13;
                h *= 0x27d4eb2d;
                h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }
    }

    /// <summary>Pulso contínuo de escala/alpha (anel de "ultimate pronta").</summary>
    public class FxPulse : MonoBehaviour
    {
        public Color color = Color.white;
        public float baseScale = 1f, amount = 0.15f, speed = 4f;
        Renderer r;
        void Start() { r = GetComponent<Renderer>(); }
        void Update()
        {
            float s = Mathf.Sin(Time.time * speed);
            transform.localScale = Vector3.one * baseScale * (1f + amount * s);
            if (r != null) { var c = color; c.a = color.a * (0.55f + 0.45f * s); FX.SetColor(r, c); }
        }
    }

    /// <summary>Encerra um efeito contínuo depois de "life" segundos (para de emitir, apaga a luz e destrói).</summary>
    public class FxAutoEnd : MonoBehaviour
    {
        public float life = 1f;
        float t;
        bool ending;
        public void EndNow() { t = life; }
        void Update()
        {
            t += Time.deltaTime;
            if (!ending && t >= life)
            {
                ending = true;
                var ps = GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    transform.SetParent(FX.Root, true);
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    Destroy(gameObject, 1.5f);
                }
                else Destroy(gameObject);
            }
        }
    }

    /// <summary>Move um objeto de "from" até "to" (acelerando), girando; solta o rastro no fim e se destrói.</summary>
    public class FxMover : MonoBehaviour
    {
        Vector3 from, to;
        float time, t;
        public Vector3 spinAxis = Vector3.zero;
        public float spinSpeed;
        public ParticleSystem trail;
        public TrailRenderer trailR;
        public bool faceMotion;

        public void Setup(Vector3 from, Vector3 to, float time)
        {
            this.from = from; this.to = to; this.time = Mathf.Max(0.05f, time);
            transform.position = from;
            if (faceMotion && (to - from).sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation((to - from).normalized);
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            transform.position = Vector3.LerpUnclamped(from, to, k * k);
            if (spinSpeed != 0f && spinAxis != Vector3.zero) transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
            if (k >= 1f)
            {
                if (trail != null)
                {
                    trail.transform.SetParent(FX.Root, true);
                    trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    Destroy(trail.gameObject, 2f);
                }
                if (trailR != null)
                {
                    trailR.transform.SetParent(FX.Root, true);
                    trailR.emitting = false;
                    Destroy(trailR.gameObject, trailR.time + 0.1f);
                }
                Destroy(gameObject);
            }
        }
    }
}
