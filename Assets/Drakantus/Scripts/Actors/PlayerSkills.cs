using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Habilidades do herói (Resources/Data/skills.json). Cada habilidade tem um "type"
    /// que define o comportamento; números e cores vêm do JSON.
    /// Tipos: nova, cleave, dash_strike, whirlwind, leap, buff, heal, projectile,
    /// ground, rain, line, chain, blink, trap.
    /// O visual de cada habilidade (por id) fica em SkillFX; ids desconhecidos usam os estilos "vfx" do JSON.
    /// </summary>
    public partial class Player
    {
        // estados das habilidades
        public float buffTime, shieldHp, shieldTime, tauntTime;
        public SkillDef buffSkill;
        float chargeSpeed = 16f, chargeTime = 0.28f;
        SkillDef chargeSkill;
        readonly List<Enemy> chargeHits = new();
        SkillDef whirlSkill; float whirlTime, whirlTick, whirlSpeed = 0.75f;
        SkillDef leapSkill; Vector3 leapFrom, leapTo;

        public float DamageTakenMult() => (buffTime > 0 && buffSkill != null ? buffSkill.dmgTaken : 1f) * UltDamageMult() * KitDamageTakenMult();

        public Vector2 SkillCooldown(int i)
        {
            var s = GameState.SlotSkill(i);
            if (s == null) return Vector2.zero;
            cooldowns.TryGetValue(s.id, out float left);
            return new Vector2(left, s.cd);
        }

        public void UseSkill(int i)
        {
            if (!canFight || inputLocked || state == "dead" || state == "dash" || state == "charge" || state == "leap") return;
            var slot = GameState.SlotSkill(i);
            if (slot == null) { GameState.Notify("Slot vazio: abra Habilidades [K] para equipar."); return; }
            // [Classes] Berserker em Fúria: a habilidade troca para a versão furiosa (a recarga fica no id do slot)
            var s = ResolveAlt(slot);
            if (cooldowns.TryGetValue(slot.id, out float left) && left > 0) { GameState.Notify(s.name + " em recarga."); return; }
            if (!KitCanCast(s)) { HUD.I?.FlashSlot(i, new Color(0.6f, 0.6f, 0.6f)); return; }
            if (s.hpCost > 0f)
            {
                // versões furiosas: custam vida atual, nunca deixam com menos de 1
                float cost = Mathf.Min(GameState.P.hp * s.hpCost, Mathf.Max(0f, GameState.P.hp - 1f));
                if (cost > 0f)
                {
                    GameState.P.hp -= cost;
                    HUD.Popup(transform.position + Vector3.up * 2.2f, "-" + Mathf.RoundToInt(cost), SkillFX.Blood);
                }
            }
            else
            {
                // Frenesi do Mago (passiva de combo x50): habilidades sem custo de mana
                bool freeMana = combo != null && combo.FreeMana;
                float mpCost = s.mp * KitManaCostMult();
                if (!freeMana && GameState.P.mp < mpCost) { GameState.Notify("Mana insuficiente!"); HUD.I?.FlashSlot(i, new Color(0.4f, 0.6f, 1f)); return; }
                if (!freeMana) GameState.P.mp -= mpCost;
            }
            if (charging) CancelCharge();
            cooldowns[slot.id] = s.cd;
            ultSkillHits = 0;   // a carga da ultimate conta no máx. 3 inimigos por uso de habilidade
            HUD.I?.FlashSlot(i, Color.white);
            if (Sfx.Has("sk_" + s.id)) Sfx.Play("sk_" + s.id, transform.position);
            else Sfx.Play(SkillSound(s), transform.position);
            Cast(s);
            // [Classes] usar habilidade revela o Assassino (exceto as de furtividade, que cuidam disso)
            if (Stealthed && s.type != "stealth" && s.type != "zone" && s.type != "stance" && s.type != "flurry") EndStealth();
            GameState.Emit();
        }

        /// <summary>Som principal de cada tipo de habilidade (o impacto dos projéteis toca no Projectile).</summary>
        static string SkillSound(SkillDef s)
        {
            switch (s.type)
            {
                case "nova": case "ground": return "explosion";
                case "cleave": case "whirlwind": return "swing_heavy";
                case "dash_strike": case "blink": case "leap": return "dash";
                case "buff": return "buff";
                case "heal": return "heal";
                case "chain": return "lightning";
                case "line": return "ice";
                case "trap": return "shield";
                case "kit_strike": case "flurry": case "combo3": return "swing_heavy";
                case "stance": case "party": case "rage": return "buff";
                case "backstab": case "dodgeshot": case "stealth": return "dash";
                case "skyfall": return "explosion";
                default: return "magic_cast";
            }
        }

        int Dmg(SkillDef s, float mult) => Mathf.Max(1, Mathf.RoundToInt(GameState.AttackPower() * mult * ComboDamageMult * KitSkillMult(s)));
        int Dmg(SkillDef s) => Dmg(s, s.mult);

        /// <summary>Estilos genéricos do JSON (fallback para habilidades sem efeito próprio).</summary>
        void PlayVfx(SkillDef s, Vector3 pos, float size)
        {
            if (s.vfx == null) return;
            foreach (var v in s.vfx) FX.Style(v.style, pos, U.Hex(v.color), size);
        }

        /// <summary>Efeito principal da habilidade: único por id (SkillFX) ou estilos do JSON.</summary>
        void SkillVfx(SkillDef s, Vector3 pos, Vector3 dir, float size)
        {
            if (s == null) return;
            if (SkillFX.Has(s.id)) SkillFX.Play(s.id, pos, dir, size, U.Hex(s.color));
            else PlayVfx(s, pos, size);
        }

        /// <summary>Acerto de habilidade: dano + crédito na carga da ultimate.</summary>
        void HitSkill(Enemy e, int dmg, Vector3 from, bool crit)
        {
            if (e == null || e.dead) return;
            e.TakeHit(dmg, from, crit);
            RegisterHit(e, e.dead, false, dmg);
        }

        void Shout(SkillDef s)
        {
            if (!string.IsNullOrEmpty(s.text)) HUD.Popup(transform.position + Vector3.up * 2.6f, s.text, U.Hex(s.color), true);
        }

        void CastAnim(string anim, float lockT = 0.45f)
        {
            SetState("cast");
            visual.Play(anim, 0.05f, lockT, true);
        }

        /// <summary>Cor da aura de buff por habilidade.</summary>
        static Color AuraColor(SkillDef s)
        {
            switch (s.id)
            {
                case "warcry": return SkillFX.Fire;
                case "fortress": return SkillFX.Gold;
                case "guardian": return SkillFX.Holy;
                case "roll": return SkillFX.Cyan;
                default: return U.Hex(s.color);
            }
        }

        void Cast(SkillDef s)
        {
            Vector3 aim = AimDir();
            Vector3 pos = transform.position;
            Color col = U.Hex(s.color);
            switch (s.type)
            {
                case "nova":
                {
                    CastAnim(s.radius > 3.5f ? "Cheer" : "Spin");
                    SkillVfx(s, pos, aim, s.radius);
                    var hit = AreaDamage(pos, s.radius, Dmg(s), col);
                    foreach (var e in hit) if (s.stun > 0 && e != null && !e.dead) e.Stun(s.stun);
                    if (s.taunt > 0) tauntTime = s.taunt;
                    if (s.shield > 0) { shieldHp = s.shield; shieldTime = 6f; }
                    Shout(s);
                    Game.I.Shake(Mathf.Clamp(s.shake * 0.07f, 0.1f, 0.5f));
                    if (hit.Count > 0) Game.I.Hitstop(0.05f);
                    break;
                }
                case "cleave":
                {
                    facing = aim; transform.rotation = Quaternion.LookRotation(aim);
                    CastAnim("Attack2");
                    SkillVfx(s, pos, aim, s.range);
                    bool any = false;
                    foreach (var e in Game.I.enemies.ToArray())
                    {
                        if (e == null || e.dead) continue;
                        Vector3 to = U.Flat(e.transform.position - pos);
                        if (to.magnitude > s.range + e.radius) continue;
                        if (to.magnitude > 0.6f && Vector3.Dot(aim, to.normalized) < s.arc) continue;
                        int d = Dmg(s);
                        if (e.hp <= e.maxHp * s.execute) d *= 2;
                        HitSkill(e, d, pos, true);
                        SkillFX.Sparks(e.transform.position + Vector3.up, col, 16, 3f, 7f, 1.2f);
                        any = true;
                    }
                    Breakable.HitCone(pos, aim, s.range, s.arc, Dmg(s));   // [Quebraveis]
                    Game.I.Shake(0.3f);
                    if (any) Game.I.Hitstop(0.08f);
                    break;
                }
                case "dash_strike":
                    dashDir = aim; facing = aim;
                    chargeSpeed = s.speed > 0 ? s.speed : 16f;
                    chargeTime = s.time > 0 ? s.time : 0.28f;
                    invuln = chargeTime + 0.08f;
                    chargeHits.Clear();
                    chargeSkill = s;
                    SetState("charge");
                    visual.Play("Attack1", 0.05f, chargeTime, true);
                    SkillVfx(s, pos, aim, 1f);
                    break;
                case "whirlwind":
                    whirlSkill = s; whirlTime = s.duration; whirlTick = 0; whirlSpeed = s.speedMult;
                    SetState("whirl");
                    visual.Play("Spin", 0.05f, s.duration, true);
                    break;
                case "leap":
                    facing = aim;
                    leapFrom = pos;
                    leapTo = ClampToWalls(pos, AimPoint(s.range));
                    leapSkill = s;
                    invuln = 0.6f;
                    SetState("leap");
                    visual.Play("Jump", 0.05f, 0.5f, true);
                    if (SkillFX.Has(s.id)) SkillFX.Phase(s.id, "start", pos, aim, s.radius, col);
                    else FX.Dust(pos, U.Hex("c8b090"), 1f);
                    break;
                case "buff":
                    CastAnim("Cheer");
                    SkillVfx(s, pos, aim, 1.5f);
                    buffTime = s.duration; buffSkill = s;
                    GameState.skillAtkBonus = s.atk;
                    GameState.skillSpeed = s.speedMult;
                    if (s.taunt > 0) tauntTime = s.taunt;
                    if (s.duration > 0) SkillFX.AttachAura(transform, AuraColor(s), s.duration, 30f, 0.5f, true);
                    Shout(s);
                    break;
                case "heal":
                {
                    CastAnim("Cast");
                    SkillVfx(s, pos, aim, 1.5f);
                    float amt = GameState.maxHp * s.percent + s.flat;
                    GameState.P.hp = Mathf.Min(GameState.maxHp, GameState.P.hp + amt);
                    HUD.Popup(pos + Vector3.up * 2.4f, "+" + Mathf.RoundToInt(amt), U.Hex("8fe08a"), true);
                    if (s.buffDuration > 0)
                    {
                        buffTime = s.buffDuration; buffSkill = s;
                        SkillFX.AttachAura(transform, AuraColor(s), s.buffDuration, 22f, 0.5f, true);
                    }
                    break;
                }
                case "projectile":
                {
                    facing = aim; transform.rotation = Quaternion.LookRotation(aim);
                    CastAnim(s.proj == "arrow" || s.proj == "pierce" || s.proj == "frost" || IsShotKind(s.proj) ? "Shoot" : s.proj == "axe_spin" ? "Attack2" : "Cast");
                    SkillVfx(s, pos, aim, 1f);
                    int n = Mathf.Max(1, s.count);
                    for (int k = 0; k < n; k++)
                    {
                        float off = (k - (n - 1) / 2f) * s.spread * Mathf.Rad2Deg;
                        Vector3 d = Quaternion.Euler(0, off, 0) * aim;
                        Shoot(s.proj, d, Dmg(s), s);
                    }
                    break;
                }
                case "ground":
                    facing = aim;
                    CastAnim("Cast");
                    StartCoroutine(GroundStrike(s, ClampToWalls(pos, AimPoint(s.range))));
                    break;
                case "rain":
                    facing = aim;
                    CastAnim("Shoot");
                    SkillVfx(s, pos, aim, s.area);
                    StartCoroutine(Rain(s, ClampToWalls(pos, AimPoint(s.range))));
                    break;
                case "line":
                    facing = aim;
                    CastAnim("Attack2");
                    SkillVfx(s, pos, aim, s.radius);
                    StartCoroutine(Line(s, pos, aim));
                    break;
                case "chain":
                    facing = aim;
                    CastAnim("Cast");
                    SkillVfx(s, pos, aim, 1f);
                    StartCoroutine(Chain(s, aim));
                    break;
                case "blink":
                {
                    Vector3 dir = s.distance >= 0 ? aim : -aim;
                    Vector3 dest = ClampToWalls(pos, pos + dir * Mathf.Abs(s.distance));
                    bool own = SkillFX.Has(s.id);
                    // dir = para onde o herói vai (no Rolamento é para trás da mira)
                    if (own) SkillFX.Play(s.id, pos, dir, Mathf.Abs(s.distance), col);
                    else { PlayVfx(s, pos, 1f); FX.Burst(pos + Vector3.up, col, 1f, 26); }
                    Teleport(dest);
                    invuln = s.invuln > 0 ? s.invuln : 0.3f;
                    visual.Play("Dodge", 0.05f, 0.25f, true);
                    if (own) SkillFX.Phase(s.id, "end", dest, dir, s.novaRadius, col);
                    else FX.Burst(dest + Vector3.up, col, 1f, 26);
                    if (s.novaRadius > 0)
                    {
                        if (!own) FX.Ring(dest, s.novaRadius, col, 0.4f);
                        AreaDamage(dest, s.novaRadius, Dmg(s, s.novaMult), col);
                    }
                    if (s.duration > 0)
                    {
                        buffTime = s.duration; buffSkill = s; GameState.skillSpeed = s.speedMult;
                        SkillFX.AttachAura(transform, AuraColor(s), s.duration, 20f, 0.4f, false);
                    }
                    break;
                }
                case "trap":
                    CastAnim("Interact", 0.35f);
                    Trap.Spawn(pos + facing * 0.6f, s, Dmg(s));
                    if (SkillFX.Has(s.id)) SkillFX.Play(s.id, pos + facing * 0.6f, facing, 1f, col);
                    else FX.Dust(pos, U.Hex("c8b090"), 0.6f);
                    break;
                default:
                    if (!CastKit(s)) Debug.LogWarning("[Drakantus] Tipo de habilidade desconhecido: " + s.type);   // [Classes] ClassKitSkills.cs
                    break;
            }
        }

        // ------------------------------------------------------------------ habilidades com tempo
        IEnumerator GroundStrike(SkillDef s, Vector3 tp)
        {
            Color c = !string.IsNullOrEmpty(s.telegraph) ? U.Hex(s.telegraph) : U.Hex(s.color);
            float delay = Mathf.Max(0.05f, s.delay);
            var mark = FX.Marker(tp, s.radius, c, delay);
            bool own = SkillFX.Has(s.id);
            if (own) SkillFX.Phase(s.id, "start", tp, facing, s.radius, c);
            GameObject fall = null;
            if (s.falling)
            {
                if (own) SkillFX.SpawnMeteor(tp + new Vector3(-3, 9, -3), tp + Vector3.up * 0.4f, delay, 1.1f);
                else
                {
                    fall = U.Prim(PrimitiveType.Sphere, FX.Root, tp + new Vector3(-3, 9, -3), Vector3.one * 1.1f, U.Hex("ff7a2a"));
                    fall.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("ff7a2a"), 0.2f, U.Hex("ff5a10") * 3f);
                }
            }
            float t = 0;
            while (t < delay)
            {
                t += Time.deltaTime;
                if (fall != null)
                {
                    fall.transform.position = Vector3.Lerp(tp + new Vector3(-3, 9, -3), tp + Vector3.up * 0.4f, t / delay);
                    if (Random.value < 0.6f) FX.Sparkle(fall.transform.position, U.Hex("ffb347"), 0.4f, 0.05f);
                }
                yield return null;
            }
            if (fall != null) Destroy(fall);
            if (mark != null) Destroy(mark);
            SkillVfx(s, tp, facing, s.radius);
            var hit = AreaDamage(tp, s.radius, Dmg(s), U.Hex(s.color));
            foreach (var e in hit) if (s.stun > 0 && e != null && !e.dead) e.Stun(s.stun);
            Game.I.Shake(Mathf.Clamp(s.shake * 0.07f, 0.15f, 0.7f));
            if (hit.Count > 0) Game.I.Hitstop(0.06f);
        }

        IEnumerator Rain(SkillDef s, Vector3 center)
        {
            Color c = !string.IsNullOrEmpty(s.telegraph) ? U.Hex(s.telegraph) : U.Hex(s.color);
            var mark = FX.Marker(center, s.area, c, s.drops * s.interval);
            bool own = SkillFX.Has(s.id);
            for (int k = 0; k < s.drops; k++)
            {
                yield return new WaitForSeconds(s.interval);
                Vector2 r = Random.insideUnitCircle * s.area * 0.85f;
                Vector3 p = center + new Vector3(r.x, 0, r.y);
                if (own) SkillFX.Phase(s.id, "drop", p, facing, s.hitRadius, c);
                else
                {
                    var arrow = U.Prim(PrimitiveType.Cube, FX.Root, p + Vector3.up * 0.4f, new Vector3(0.05f, 0.8f, 0.05f), U.Hex("d8c8a0"));
                    Destroy(arrow, 0.35f);
                    FX.Burst(p + Vector3.up * 0.2f, c, 0.4f, 8);
                }
                AreaDamage(p, s.hitRadius, Dmg(s), c);
            }
            if (mark != null) Destroy(mark);
        }

        IEnumerator Line(SkillDef s, Vector3 origin, Vector3 dir)
        {
            var hits = new List<Enemy>();
            bool own = SkillFX.Has(s.id);
            for (int k = 0; k < s.count; k++)
            {
                Vector3 p = origin + dir * s.spacing * (k + 1);
                if (Vector3.Distance(ClampToWalls(origin, p), p) > 0.2f) break;
                if (own) SkillFX.Phase(s.id, "tick", p, dir, s.radius, U.Hex(s.color));
                else
                {
                    FX.Dust(p, U.Hex("a08060"), 1.1f);
                    FX.Burst(p + Vector3.up * 0.2f, U.Hex("c8a070"), 0.7f, 12);
                }
                foreach (var e in Game.I.enemies.ToArray())
                {
                    if (e == null || e.dead || hits.Contains(e)) continue;
                    if (U.Flat(e.transform.position - p).magnitude <= s.radius + e.radius)
                    {
                        hits.Add(e);
                        HitSkill(e, Dmg(s), p, false);
                        if (s.stun > 0 && !e.dead) e.Stun(s.stun);
                    }
                }
                Breakable.HitArea(p, s.radius, Dmg(s));   // [Quebraveis]
                Game.I.Shake(0.12f);
                yield return new WaitForSeconds(s.interval);
            }
        }

        IEnumerator Chain(SkillDef s, Vector3 aim)
        {
            Vector3 from = transform.position + Vector3.up * 1.2f;
            Enemy target = null;
            float best = s.range;
            foreach (var e in Game.I.enemies)
            {
                if (e == null || e.dead) continue;
                Vector3 to = U.Flat(e.transform.position - transform.position);
                if (to.magnitude < best && (to.magnitude < 2.5f || Vector3.Dot(to.normalized, aim) > 0.5f)) { best = to.magnitude; target = e; }
            }
            if (target == null) { SkillFX.ChainBolt(from, from + aim * 4f); yield break; }
            var used = new List<Enemy>();
            int dmg = Dmg(s);
            for (int j = 0; j < s.jumps && target != null; j++)
            {
                Vector3 tp = target.transform.position + Vector3.up;
                SkillFX.ChainBolt(from, tp);
                if (j > 0) Sfx.Play("lightning", tp, 0.6f, 0.15f);
                HitSkill(target, dmg, from, false);
                used.Add(target);
                from = tp;
                Enemy next = null; float nd = s.jumpRange;
                foreach (var e in Game.I.enemies)
                {
                    if (e == null || e.dead || used.Contains(e)) continue;
                    float dd = Vector3.Distance(e.transform.position, target.transform.position);
                    if (dd < nd) { nd = dd; next = e; }
                }
                target = next;
                yield return new WaitForSeconds(0.07f);
            }
            Game.I.Shake(0.15f);
        }

        // ------------------------------------------------------------------ atualização por frame
        void UpdateSkillStates(float dt)
        {
            if (buffTime > 0)
            {
                buffTime -= dt;
                if (buffTime <= 0) { buffSkill = null; GameState.skillAtkBonus = 0; GameState.skillSpeed = 1f; }
            }
            if (shieldTime > 0) { shieldTime -= dt; if (shieldTime <= 0) shieldHp = 0; }
            tauntTime = Mathf.Max(0, tauntTime - dt);
        }

        void UpdateLeap()
        {
            float lt = 0.45f;
            float k = Mathf.Clamp01(stateTime / lt);
            Vector3 p = Vector3.Lerp(leapFrom, leapTo, k);
            p.y = leapFrom.y + Mathf.Sin(k * Mathf.PI) * 2.2f;
            cc.enabled = false; transform.position = p; cc.enabled = true;
            if (k >= 1f)
            {
                Teleport(new Vector3(leapTo.x, leapFrom.y, leapTo.z));
                SkillVfx(leapSkill, leapTo, facing, leapSkill.radius);
                // [Classes] Vingança, roubo de vida e provocação também no pouso
                int ld = Dmg(leapSkill) + VengeanceBonus(leapSkill);
                foreach (var e in AreaDamage(leapTo, leapSkill.radius, ld, U.Hex(leapSkill.color), leapSkill))
                    if (e != null && !e.dead && leapSkill.stun > 0) e.Stun(leapSkill.stun);
                ConsumeVengeance(leapSkill);
                if (leapSkill.taunt > 0f) TauntAround(leapTo, leapSkill.radius + 3f, leapSkill.taunt);
                Shout(leapSkill);
                Game.I.Shake(0.5f); Game.I.Hitstop(0.07f);
                visual.Play("Attack2", 0.05f, 0.3f, true);
                SetState("idle");
            }
        }

        void UpdateWhirl(float dt)
        {
            whirlTime -= dt; whirlTick -= dt;
            if (whirlTick <= 0)
            {
                whirlTick = whirlSkill.tick > 0 ? whirlSkill.tick : 0.25f;
                if (SkillFX.Has(whirlSkill.id)) SkillFX.Play(whirlSkill.id, transform.position, transform.forward, whirlSkill.radius, U.Hex(whirlSkill.color));
                else
                {
                    PlayVfx(whirlSkill, transform.position, whirlSkill.radius);
                    // o herói gira no sentido horário (visto de cima) → corte esquerda→direita
                    FX.SlashArc(transform.position + Vector3.up * 1.0f, transform.forward, U.Hex("e8f0ff"), whirlSkill.radius * 0.8f, 320f, false, 0.22f, 0.55f, 6);
                }
                AreaDamage(transform.position, whirlSkill.radius, Dmg(whirlSkill), U.Hex(whirlSkill.color), whirlSkill);
                Game.I.Shake(0.08f);
            }
            transform.Rotate(0, 900f * dt, 0, Space.World);
            if (whirlTime <= 0) SetState("idle");
        }

        void ChargeHits()
        {
            if (chargeSkill != null) Breakable.HitArea(transform.position, 1.1f, Dmg(chargeSkill));   // [Quebraveis]
            foreach (var e in Game.I.enemies.ToArray())
            {
                if (e == null || e.dead || chargeHits.Contains(e)) continue;
                if (U.Flat(e.transform.position - transform.position).magnitude < 1.1f + e.radius)
                {
                    chargeHits.Add(e);
                    HitSkill(e, Dmg(chargeSkill), transform.position - dashDir, false);
                    if (SkillFX.Has(chargeSkill.id)) SkillFX.Phase(chargeSkill.id, "hit", e.transform.position + Vector3.up, dashDir, 1f, U.Hex(chargeSkill.color));
                    else FX.Burst(e.transform.position + Vector3.up, U.Hex(chargeSkill.color), 0.8f, 16);
                    if (chargeSkill.stun > 0 && !e.dead) e.Stun(chargeSkill.stun);
                    Game.I.Shake(0.15f); Game.I.Hitstop(0.04f);
                }
            }
        }

        // ------------------------------------------------------------------ utilitários
        public List<Enemy> AreaDamage(Vector3 center, float radius, int dmg, Color hitColor)
        {
            var hit = new List<Enemy>();
            if (Game.I == null) return hit;
            foreach (var e in Game.I.enemies.ToArray())
            {
                if (e == null || e.dead) continue;
                if (U.Flat(e.transform.position - center).magnitude <= radius + e.radius)
                {
                    HitSkill(e, dmg, center, false);
                    FX.Burst(e.transform.position + Vector3.up, hitColor, 0.6f, 10);
                    hit.Add(e);
                }
            }
            Breakable.HitArea(center, radius, dmg);   // [Quebraveis]
            return hit;
        }

        /// <summary>Limita um destino para não atravessar paredes.</summary>
        public Vector3 ClampToWalls(Vector3 from, Vector3 to)
        {
            Vector3 a = from + Vector3.up * 0.8f, b = to + Vector3.up * 0.8f;
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return to;
            if (Physics.SphereCast(a, 0.35f, d / len, out RaycastHit h, len, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider != null && h.collider.transform.root != transform.root && h.collider.GetComponentInParent<Enemy>() == null)
                    return from + d / len * Mathf.Max(0, h.distance - 0.3f);
            }
            return to;
        }
    }
}
