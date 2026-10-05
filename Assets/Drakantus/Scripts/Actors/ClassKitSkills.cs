using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// [Classes] Tipos de habilidade novos (skills.json "type"), chamados pelo default do PlayerSkills.Cast:
    ///   kit_strike (cone/círculo em volta; arc -1 = 360°), stance (buff próprio), party (cura/escudo/buff do grupo),
    ///   zone (área persistente: fumaça, santuário, singularidade, estandarte), stealth, flurry, backstab, deathmark,
    ///   combo3, dodgeshot, deadeye, shadowstep, skyfall, rage.
    /// Tipos antigos reaproveitados: projectile, line, rain, ground, leap, whirlwind (com os campos novos do kit).
    /// </summary>
    public partial class Player
    {
        Enemy kitTarget;

        /// <summary>Checagem antes de pagar o custo (alvo obrigatório). false = não usa e não gasta.</summary>
        bool KitCanCast(SkillDef s)
        {
            kitTarget = null;
            switch (s.type)
            {
                case "shadowstep":
                    kitTarget = PickTarget(Mathf.Max(0f, s.distance), s.range > 0f ? s.range : 14f, 0.55f);
                    if (kitTarget == null) { GameState.Notify("Nenhum inimigo mirado entre " + Num(s.distance) + " e " + Num(s.range) + " m."); return false; }
                    return true;
                case "backstab":
                case "deathmark":
                    kitTarget = PickTarget(0f, s.range > 0f ? s.range : 8f, 0.2f);
                    if (kitTarget == null) { GameState.Notify("Nenhum inimigo ao alcance (" + Num(s.range) + " m)."); return false; }
                    return true;
                case "deadeye":
                    if (FrontTargets(s.range, Mathf.Max(1, s.targets)).Count == 0) { GameState.Notify("Nenhum inimigo à frente."); return false; }
                    return true;
            }
            return true;
        }

        static string Num(float v) => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.#");

        /// <summary>Alvo: travado (Tab) se válido; senão o mais perto do cursor dentro do cone de mira.</summary>
        Enemy PickTarget(float minR, float maxR, float minDot)
        {
            Vector3 me = transform.position;
            var lt = LockedTarget;
            if (lt != null)
            {
                float d = U.Flat(lt.transform.position - me).magnitude;
                if (d >= minR - lt.radius && d <= maxR + lt.radius) return lt;
            }
            if (Game.I == null) return null;
            Vector3 aim = AimDir();
            Vector3 ap = AimPoint(maxR);
            Enemy best = null; float bestS = float.MaxValue;
            foreach (var e in Game.I.enemies)
            {
                if (e == null || e.dead) continue;
                Vector3 to = U.Flat(e.transform.position - me);
                float d = to.magnitude;
                if (d < minR - e.radius || d > maxR + e.radius) continue;
                if (d > 1.5f && Vector3.Dot(to / d, aim) < minDot) continue;
                if (Projectile.WallBetween(me + Vector3.up, e.transform.position + Vector3.up)) continue;
                float sc = U.Flat(e.transform.position - ap).magnitude;
                if (sc < bestS) { bestS = sc; best = e; }
            }
            return best;
        }

        List<Enemy> FrontTargets(float range, int max)
        {
            var l = new List<Enemy>();
            if (Game.I == null) return l;
            Vector3 me = transform.position, aim = AimDir();
            foreach (var e in Game.I.enemies)
            {
                if (e == null || e.dead) continue;
                Vector3 to = U.Flat(e.transform.position - me);
                float d = to.magnitude;
                if (d > range + e.radius || (d > 1f && Vector3.Dot(to / d, aim) < 0.35f)) continue;
                l.Add(e);
            }
            l.Sort((a, b) => U.Flat(a.transform.position - me).sqrMagnitude.CompareTo(U.Flat(b.transform.position - me).sqrMagnitude));
            if (l.Count > max) l.RemoveRange(max, l.Count - max);
            return l;
        }

        /// <summary>Efeito por fase (EFEITOS) com fallback.</summary>
        void KitPhase(SkillDef s, string phase, Vector3 pos, Vector3 dir, float r, System.Action fallback = null)
        {
            if (SkillFX.Has(s.id)) SkillFX.Phase(s.id, phase, pos, dir, r, U.Hex(s.color));
            else fallback?.Invoke();
        }

        bool BuffFields(SkillDef s) => s.atkPct > 0f || s.defPct > 0f || (s.dmgTaken > 0f && s.dmgTaken < 1f) || s.regen > 0f
            || s.crit > 0f || s.spellPct > 0f || s.manaCut > 0f || s.atkSpeed > 0f || s.reflect > 0f || s.manaShield > 0f
            || s.lifesteal > 0f || s.unstoppable || s.infiniteAmmo || !string.IsNullOrEmpty(s.dot) || !Mathf.Approximately(s.speedMult, 1f);

        /// <summary>Tipos novos. Devolve false se o tipo não é do kit.</summary>
        bool CastKit(SkillDef s)
        {
            Vector3 aim = AimDir();
            Vector3 pos = transform.position;
            Color col = U.Hex(s.color);
            switch (s.type)
            {
                case "kit_strike":
                {
                    facing = aim; transform.rotation = Quaternion.LookRotation(aim);
                    bool full = s.arc < -0.5f;
                    CastAnim(full ? "Spin" : "Attack2");
                    float r = s.range > 0f ? s.range : s.radius;
                    SkillVfx(s, pos, aim, r);
                    if (!SkillFX.Has(s.id))
                        FX.SlashArc(pos + Vector3.up, aim, col, r, full ? 350f : Mathf.Clamp(Mathf.Acos(Mathf.Clamp(s.arc, -1f, 1f)) * 2f * Mathf.Rad2Deg, 60f, 350f), true, 0.22f, 0.8f, 20);
                    int dmg = Dmg(s) + VengeanceBonus(s);
                    bool any = false;
                    foreach (var e in Game.I.enemies.ToArray())
                    {
                        if (e == null || e.dead) continue;
                        Vector3 to = U.Flat(e.transform.position - pos);
                        if (to.magnitude > r + e.radius) continue;
                        if (!full && to.magnitude > 0.6f && Vector3.Dot(aim, to.normalized) < s.arc) continue;
                        bool crit = TakeOpener() || Random.value < 0.12f + GameState.CritBonus() + KitCrit();
                        int d = crit ? dmg * 2 : dmg;
                        KitHit(s, e, d, pos, crit);
                        if (s.stun > 0f && !e.dead) e.Stun(s.stun);
                        SkillFX.Sparks(e.transform.position + Vector3.up, col, 14, 3f, 7f, 1.2f);
                        any = true;
                    }
                    if (full) Breakable.HitArea(pos, r, dmg); else Breakable.HitCone(pos, aim, r, s.arc, dmg);
                    ConsumeVengeance(s);
                    if (s.taunt > 0f) TauntAround(pos, Mathf.Max(r, 6f), s.taunt);
                    Shout(s);
                    Game.I.Shake(Mathf.Clamp(0.25f + s.shake * 0.05f, 0.2f, 0.6f));
                    if (any) Game.I.Hitstop(0.08f);
                    return true;
                }
                case "stance":
                {
                    CastAnim(s.taunt > 0f ? "Cheer" : "Block", 0.4f);
                    SkillVfx(s, pos, aim, 1.5f);
                    if (BuffFields(s) && s.duration > 0f) AddKitBuff(s, s.duration, this);
                    if (s.taunt > 0f) TauntAround(pos, s.radius > 0f ? s.radius : 7f, s.taunt);
                    Shout(s);
                    return true;
                }
                case "party":
                    CastAnim("Cheer", 0.45f);
                    SkillVfx(s, pos, aim, s.radius);
                    CastParty(s, pos);
                    Shout(s);
                    return true;
                case "zone":
                {
                    Vector3 c = s.range > 0f ? ClampToWalls(pos, AimPoint(s.range)) : pos;
                    CastAnim(s.id == "gd_banner" ? "Interact" : "Cast", 0.4f);
                    StartCoroutine(Zone(s, c));
                    return true;
                }
                case "stealth":
                    CastAnim("Dodge", 0.25f);
                    SkillVfx(s, pos, aim, 1.5f);
                    EnterStealth(s.stealth > 0f ? s.stealth : s.duration, true);
                    return true;
                case "flurry":
                    facing = aim; transform.rotation = Quaternion.LookRotation(aim);
                    StartCoroutine(Flurry(s));
                    return true;
                case "backstab":
                    Backstab(s, kitTarget);
                    return true;
                case "deathmark":
                {
                    var e = kitTarget;
                    if (e == null) return true;
                    facing = U.Flat(e.transform.position - pos).normalized; transform.rotation = Quaternion.LookRotation(facing);
                    CastAnim("Cast", 0.35f);
                    SkillVfx(s, e.transform.position, facing, 1.5f);
                    e.Mark(s.duration > 0f ? s.duration : 6f, s.markStore, s.execute, this);
                    if (s.mult > 0f) KitHit(s, e, Dmg(s), pos, false);
                    HUD.Popup(e.transform.position + Vector3.up * (e.Top + 0.6f), "MARCADO", col, true);
                    return true;
                }
                case "combo3":
                    facing = aim; transform.rotation = Quaternion.LookRotation(aim);
                    StartCoroutine(HeavyCombo(s));
                    return true;
                case "dodgeshot":
                    StartCoroutine(DodgeShot(s, aim));
                    return true;
                case "deadeye":
                    facing = aim; transform.rotation = Quaternion.LookRotation(aim);
                    StartCoroutine(Deadeye(s));
                    return true;
                case "shadowstep":
                    StartCoroutine(ShadowStep(s, kitTarget));
                    return true;
                case "skyfall":
                    facing = aim;
                    CastAnim("Shoot", 0.5f);
                    StartCoroutine(Skyfall(s, ClampToWalls(pos, AimPoint(s.range > 0f ? s.range : 12f)), aim));
                    return true;
                case "rage":
                    CastAnim("Cheer", 0.5f);
                    SkillVfx(s, pos, aim, 2f);
                    rageT = Mathf.Max(rageT, s.duration > 0f ? s.duration : 10f);
                    if (!Frenzy) EnterFrenzy();
                    Shout(s);
                    return true;
            }
            return false;
        }

        void TauntAround(Vector3 c, float r, float secs)
        {
            tauntTime = Mathf.Max(tauntTime, secs);
            if (Game.I == null) return;
            foreach (var e in Game.I.enemies)
                if (e != null && !e.dead && U.Flat(e.transform.position - c).magnitude <= r + e.radius) e.Alert(false);
        }

        // ------------------------------------------------------------------ grupo
        void CastParty(SkillDef s, Vector3 pos)
        {
            float r = s.radius > 0f ? s.radius : 8f;
            var members = s.cleanse ? Party.AllInRadius(pos, r) : Party.InRadius(pos, r);
            Color col = U.Hex(s.color);
            foreach (var m in members)
            {
                if (m == null) continue;
                if (m.state == "dead") { if (!s.cleanse) continue; m.Revive(); }
                if (m != this) SkillFX.HealBeam(pos + Vector3.up * 1.6f, m.transform.position + Vector3.up * 1.2f, col);
                if (s.percent > 0f || s.flat > 0f) m.Heal(m.MaxHp * s.percent + s.flat);
                if (s.shieldPct > 0f)
                {
                    float d = s.duration > 0f ? s.duration : 6f;
                    m.shieldHp = Mathf.Max(m.shieldHp, m.MaxHp * s.shieldPct);
                    m.shieldTime = Mathf.Max(m.shieldTime, d);
                    StatusFX.Attach(m.transform, "holy_shield", d);
                }
                if (s.invuln > 0f) m.invuln = Mathf.Max(m.invuln, s.invuln);
                if (s.cleanse) m.Cleanse();
                if (s.duration > 0f && BuffFields(s)) m.AddKitBuff(s, s.duration, this);
                SkillFX.Phase(s.id, "hit", m.transform.position, Vector3.forward, 1f, col);
            }
        }

        // ------------------------------------------------------------------ áreas persistentes
        IEnumerator Zone(SkillDef s, Vector3 c)
        {
            float r = s.radius > 0f ? s.radius : 4f, dur = s.duration > 0f ? s.duration : 4f;
            Color col = U.Hex(s.color);
            bool own = SkillFX.Has(s.id);
            GameObject mark = null;
            if (own) SkillFX.Play(s.id, c, facing, r, col);
            else { PlayVfx(s, c, r); mark = FX.Marker(c, r, col, dur); }
            float t = 0f, tick = 0f, dmgTick = 0f, fxTick = 1f, healPop = 0f;
            float dmgEvery = s.tick > 0f ? s.tick : 0.5f;
            while (t < dur)
            {
                float dt = Time.deltaTime;
                t += dt; tick -= dt; dmgTick -= dt; fxTick -= dt; healPop -= dt;
                if (s.pull > 0f && Game.I != null)
                    foreach (var e in Game.I.enemies)
                        if (e != null && !e.dead && U.Flat(e.transform.position - c).magnitude <= r + 2f) e.Pull(c, s.pull);
                if (tick <= 0f)
                {
                    tick = 0.25f;
                    if (Game.I != null)
                        foreach (var e in Game.I.enemies)
                        {
                            if (e == null || e.dead || U.Flat(e.transform.position - c).magnitude > r + e.radius) continue;
                            if (s.slow > 0f) e.Slow(1f - s.slow, 0.5f);
                            if (s.blind > 0f) e.Blind(0.5f);
                        }
                    foreach (var m in Party.InRadius(c, r))
                    {
                        if (s.regen > 0f && !BuffFields(s)) m.Heal(m.MaxHp * s.regen * 0.25f, false);
                        if (BuffFields(s)) m.AddKitBuff(s, 0.6f, this);
                        if (s.stealth > 0f && m == this) EnterStealth(0.4f, true);
                    }
                    if (s.regen > 0f && healPop <= 0f)
                    {
                        healPop = 1f;
                        foreach (var m in Party.InRadius(c, r))
                            HUD.Popup(m.transform.position + Vector3.up * 2.4f, "+" + Mathf.RoundToInt(m.MaxHp * s.regen), U.Hex("8fe08a"));
                        GameState.Emit();
                    }
                }
                if (s.mult > 0f && s.pull > 0f && dmgTick <= 0f)
                {
                    dmgTick = dmgEvery;
                    AreaDamage(c, r, Dmg(s), col, s);
                }
                if (fxTick <= 0f) { fxTick = 1f; if (own) SkillFX.Phase(s.id, "tick", c, facing, r, col); }
                yield return null;
            }
            if (mark != null) Destroy(mark);
            if (own) SkillFX.Phase(s.id, "end", c, facing, r, col);
        }

        // ------------------------------------------------------------------ Assassino
        IEnumerator Flurry(SkillDef s)
        {
            int n = Mathf.Max(1, s.hits > 0 ? s.hits : 6);
            float total = s.time > 0f ? s.time : 0.6f;
            float r = s.range > 0f ? s.range : 2.6f;
            Color col = U.Hex(s.color);
            SetState("cast");
            SkillVfx(s, transform.position, facing, r);
            for (int k = 0; k < n; k++)
            {
                if (state == "dead") yield break;
                SetState("cast");
                visual.Play(k % 2 == 0 ? "Attack1" : "Attack2", 0.03f, total / n + 0.05f, true);
                Vector3 pos = transform.position;
                if (!SkillFX.Has(s.id)) FX.SlashArc(pos + Vector3.up, facing, col, r * 0.9f, 120f, k % 2 == 0, 0.12f, 0.45f, 8);
                else SkillFX.Phase(s.id, "tick", pos, facing, r, col);
                bool any = false;
                foreach (var e in Game.I.enemies.ToArray())
                {
                    if (e == null || e.dead) continue;
                    Vector3 to = U.Flat(e.transform.position - pos);
                    if (to.magnitude > r + e.radius) continue;
                    if (to.magnitude > 0.6f && Vector3.Dot(facing, to.normalized) < s.arc) continue;
                    bool opener = TakeOpener();
                    bool crit = opener || Random.value < 0.15f + GameState.CritBonus() + KitCrit() + (BehindOf(e) ? BackstabCrit : 0f);
                    int d = Dmg(s);
                    if (crit) d = Mathf.RoundToInt(d * (opener ? 2.5f : 2f));
                    KitHit(s, e, d, pos, crit);
                    any = true;
                }
                if (any) { Game.I.Shake(0.08f); Sfx.Play("enemy_hit", pos, 0.5f, 0.2f); }
                Sfx.Play("swing", pos, 0.6f, 0.2f);
                yield return new WaitForSeconds(total / n);
            }
            if (Stealthed) EndStealth();
            if (state == "cast") SetState("idle");
        }

        void Backstab(SkillDef s, Enemy e)
        {
            if (e == null || e.dead) return;
            Vector3 from = transform.position;
            Vector3 back = U.Flat(-e.transform.forward);
            if (back.sqrMagnitude < 0.01f) back = U.Flat(e.transform.position - from);
            back.Normalize();
            Vector3 dest = e.transform.position + back * (e.radius + 0.9f);
            dest.y = from.y;
            Vector3 safe = ClampToWalls(e.transform.position, dest);
            Color col = U.Hex(s.color);
            KitPhase(s, "start", from, back, 1f, () => { FX.Burst(from + Vector3.up, col, 1f, 24); SkillFX.Afterimage(visual.transform, col, 0.35f); });
            Teleport(safe);
            facing = U.Flat(e.transform.position - safe).normalized;
            if (facing.sqrMagnitude < 0.01f) facing = -back;
            transform.rotation = Quaternion.LookRotation(facing);
            invuln = Mathf.Max(invuln, 0.25f);
            CastAnim("Attack2", 0.35f);
            bool opener = TakeOpener() || Stealthed;
            bool crit = opener || Random.value < 0.12f + GameState.CritBonus() + KitCrit() + BackstabCrit;
            int d = Dmg(s);
            if (crit) d = Mathf.RoundToInt(d * (opener ? Mathf.Max(2f, s.critMult) : 2f));
            KitHit(s, e, d, safe, crit);
            KitPhase(s, "hit", e.transform.position + Vector3.up, facing, 1f, () => FX.SlashArc(safe + Vector3.up, facing, col, 2f, 160f, true, 0.18f, 0.7f, 14));
            if (Stealthed) EndStealth();
            Game.I.Shake(0.25f); Game.I.Hitstop(crit ? 0.1f : 0.06f);
        }

        // ------------------------------------------------------------------ Cavaleiro
        IEnumerator HeavyCombo(SkillDef s)
        {
            int n = Mathf.Max(2, s.hits > 0 ? s.hits : 3);
            float r = s.range > 0f ? s.range : 3f;
            Color col = U.Hex(s.color);
            for (int k = 0; k < n; k++)
            {
                if (state == "dead") yield break;
                bool last = k == n - 1;
                SetState("cast");
                visual.Play(last ? "Attack2" : (k % 2 == 0 ? "Attack1" : "Attack2"), 0.04f, 0.32f, true);
                Vector3 pos = transform.position;
                if (last)
                {
                    float br = s.radius > 0f ? s.radius : 3.5f;
                    Vector3 c = pos + facing * 1.4f;
                    KitPhase(s, "end", c, facing, br, () => { SkillFX.Shockwave(c, br, col, 0.35f); FX.Dust(c, U.Hex("c8b090"), 1.4f); });
                    var hit = AreaDamage(c, br, Dmg(s, s.novaMult), col, s);
                    foreach (var e in hit) if (e != null && !e.dead && s.stun > 0f) e.Stun(s.stun);
                    Sfx.Play(Sfx.Has("hit_heavy") ? "hit_heavy" : "explosion", c);
                    Game.I.Shake(0.45f);
                    if (hit.Count > 0) Game.I.Hitstop(0.1f);
                }
                else
                {
                    KitPhase(s, "tick", pos, facing, r, () => FX.SlashArc(pos + Vector3.up, facing, col, r, 160f, k % 2 == 0, 0.2f, 0.75f, 16));
                    bool any = false;
                    foreach (var e in Game.I.enemies.ToArray())
                    {
                        if (e == null || e.dead) continue;
                        Vector3 to = U.Flat(e.transform.position - pos);
                        if (to.magnitude > r + e.radius || (to.magnitude > 0.6f && Vector3.Dot(facing, to.normalized) < s.arc)) continue;
                        bool crit = Random.value < 0.12f + GameState.CritBonus() + KitCrit();
                        int d = Dmg(s); if (crit) d *= 2;
                        KitHit(s, e, d, pos, crit);
                        any = true;
                    }
                    Breakable.HitCone(pos, facing, r, s.arc, Dmg(s));
                    Sfx.Play("swing_heavy", pos, 0.9f);
                    Game.I.Shake(0.2f);
                    if (any) Game.I.Hitstop(0.05f);
                }
                yield return new WaitForSeconds(0.3f);
            }
            if (state == "cast") SetState("idle");
        }

        // ------------------------------------------------------------------ Pistoleiro
        IEnumerator DodgeShot(SkillDef s, Vector3 aim)
        {
            Vector3 pos = transform.position;
            Vector3 dir = s.distance >= 0f ? aim : -aim;
            Vector3 dest = ClampToWalls(pos, pos + dir * Mathf.Abs(s.distance != 0f ? s.distance : 4.5f));
            Color col = U.Hex(s.color);
            KitPhase(s, "start", pos, dir, 1f, () => FX.Dust(pos, U.Hex("d8d0c0"), 0.8f));
            SpawnGhost();
            invuln = Mathf.Max(invuln, s.invuln > 0f ? s.invuln : 0.35f);
            visual.Play("Dodge", 0.05f, 0.25f, true);
            float t = 0f;
            const float T = 0.2f;
            while (t < T)
            {
                t += Time.deltaTime;
                Vector3 p = Vector3.Lerp(pos, dest, t / T);
                cc.enabled = false; transform.position = new Vector3(p.x, transform.position.y, p.z); cc.enabled = true;
                yield return null;
            }
            facing = aim; transform.rotation = Quaternion.LookRotation(aim);
            CastAnim("Shoot", 0.3f);
            int n = Mathf.Max(1, s.hits > 0 ? s.hits : 2);
            for (int k = 0; k < n; k++)
            {
                Vector3 d = Quaternion.Euler(0f, (k - (n - 1) * 0.5f) * 6f, 0f) * aim;
                Vector3 o = transform.position + Vector3.up * 1.1f + d * 0.6f;
                SkillFX.MuzzleFlash(o, d);
                Shoot("bullet", d, Dmg(s), s);
            }
            InstantReload();
            HUD.Popup(transform.position + Vector3.up * 2.4f, "recarregado", U.Hex("ffd08a"));
        }

        IEnumerator Deadeye(SkillDef s)
        {
            var targets = FrontTargets(s.range > 0f ? s.range : 14f, Mathf.Max(1, s.targets));
            float delay = s.delay > 0f ? s.delay : 0.8f;
            Color col = U.Hex(s.color);
            CastAnim("Shoot", delay + 0.3f);
            invuln = Mathf.Max(invuln, 0.2f);
            foreach (var e in targets) if (e != null) e.MarkVisual(delay + 0.2f);
            SkillVfx(s, transform.position, facing, 1f);
            yield return new WaitForSeconds(delay);
            foreach (var e in targets)
            {
                if (e == null || e.dead || state == "dead") continue;
                Vector3 o = transform.position + Vector3.up * 1.2f;
                Vector3 tp = e.transform.position + Vector3.up * 1.1f;
                Vector3 d = U.Flat(tp - o).normalized;
                facing = d; transform.rotation = Quaternion.LookRotation(d);
                SkillFX.MuzzleFlash(o + d * 0.5f, d);
                KitPhase(s, "hit", tp, d, 1f, () => SkillFX.Beam(o, tp, col, 0.08f, 0.15f));
                KitHit(s, e, Dmg(s), o, true);
                Sfx.Play(Sfx.Has("pistol") ? "pistol" : "hit_crit", o, 0.8f, 0.1f);
                Game.I.Shake(0.15f);
                yield return new WaitForSeconds(0.09f);
            }
            Game.I.Hitstop(0.08f);
        }

        // ------------------------------------------------------------------ Arqueiro Superior
        IEnumerator Skyfall(SkillDef s, Vector3 t, Vector3 aim)
        {
            float r = s.radius > 0f ? s.radius : 4f, delay = s.delay > 0f ? s.delay : 0.7f;
            Color col = U.Hex(s.color);
            var mark = FX.Marker(t, r, col, delay);
            bool own = SkillFX.Has(s.id);
            if (own) SkillFX.Phase(s.id, "start", t, aim, r, col);
            else SkillFX.SpawnGiantArrow(t - aim * 5f + Vector3.up * 16f, t + Vector3.up * 0.3f, delay);
            yield return new WaitForSeconds(delay);
            if (mark != null) Destroy(mark);
            if (own) SkillFX.Phase(s.id, "hit", t, aim, r, col); else SkillFX.GiantArrowImpact(t, r);
            Sfx.Play("explosion", t);
            var hit = AreaDamage(t, r, Dmg(s), col, s);
            foreach (var e in hit) if (e != null && !e.dead && s.stun > 0f) e.Stun(s.stun);
            Game.I.Shake(0.6f);
            if (hit.Count > 0) Game.I.Hitstop(0.08f);
            if (CameraRig.I != null) CameraRig.I.Punch(0.25f);
        }

        // ------------------------------------------------------------------ The Guard: Passo da Sombra
        IEnumerator ShadowStep(SkillDef s, Enemy e)
        {
            if (e == null) yield break;
            Vector3 from = transform.position;
            facing = U.Flat(e.transform.position - from).normalized;
            if (facing.sqrMagnitude < 0.01f) facing = transform.forward;
            transform.rotation = Quaternion.LookRotation(facing);
            CastAnim("Block", 0.4f);
            const float travel = 0.35f;
            float hold = Mathf.Max(0.5f, (e.def != null && e.def.boss ? s.root * BossCC : s.root));
            SkillFX.ShadowTendril(from, e.transform.position, travel, travel + hold);
            KitPhase(s, "start", from, facing, 1f);
            yield return new WaitForSeconds(travel);
            if (e == null || e.dead) yield break;
            e.Root(s.root > 0f ? s.root : 3f, "root_shadow");
            if (s.mult > 0f) KitHit(s, e, Dmg(s), from, false);
            KitPhase(s, "hit", e.transform.position, facing, 1f);
            HUD.Popup(e.transform.position + Vector3.up * (e.Top + 0.5f), "PRESO", U.Hex(s.color), true);
            Sfx.Play(Sfx.Has("sk_gd_shadowstep_hit") ? "sk_gd_shadowstep_hit" : "ice", e.transform.position, 0.7f);
        }
    }
}
