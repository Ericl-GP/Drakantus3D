using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    // =====================================================================================================
    // [Classes] Mecânicas das 8 classes evoluídas (partial do Player). Habilidades novas em ClassKitSkills.cs,
    // status dos inimigos em ClassKitEnemy.cs, grupo em Core/Party.cs, HUD em UI/ClassHud.cs.
    //
    // BALANCEAMENTO (referência: nível 10, Ataque ~9–11, inimigos 6–75 de vida, chefes 120–240)
    // -----------------------------------------------------------------------------------------------------
    // Velocidade de ataque básico = ClassDef.attackSpeed × passiva de combo × buffs
    //   (Fúria ×1,30 · Sede de Sangue +50% · Saque Rápido +60%).
    //
    // BERSERKER (frenzy) ★
    //   Fúria: entra com vida ≤ 30% (ou Fúria Desperta); sai com vida > 35%, mas dura no MÍNIMO 4 s
    //   (evita entrar/sair a cada golpe por causa do roubo de vida).
    //   Na Fúria: ataque básico ×1,30 mais rápido; Z X C V trocam para as versões "_f":
    //     custo = % da vida ATUAL (nunca abaixo de 1): Carnificina 7% · Queda do Abismo 8% · Sede de Sangue 6%
    //             · Tornado Rubro 10% · Machados Gêmeos 8%.
    //     dano ≈ 2,1–2,3× a versão normal; roubo de vida 12% do dano (máx. 8% da vida máx. por golpe e
    //     no máx. 15% da vida máx. por segundo somando tudo). Sede de Sangue: 25% de roubo e +50% vel. de ataque por 6 s.
    //   Fúria Desperta (nv 18): paga 20% da vida atual, Fúria forçada por 10 s (recarga 40 s).
    //
    // THE GUARD (vengeance) ★
    //   Vingança: TODO dano recebido (antes de redução, inclusive bloqueado/absorvido) enche o medidor;
    //   máx. = 60% da vida máx. Dano que a Muralha de Escudos evita nos ALIADOS enche em dobro.
    //   Sem levar dano por 6 s o medidor esvazia 3% do máx./s.
    //   Conversão: cada 1 ponto de Vingança vale 0,35 de dano (VengeanceToDamage), × fator da habilidade:
    //     Retribuição ×1,0 (cone, +x1,2) · Sentença do Guardião ×2,0 (área 4 m, +x2,0, atordoa 1,5 s).
    //     Ex.: Guard nv 10 (~310 de vida) com medidor cheio (186) → Retribuição +65 de dano; Sentença +130.
    //   Muralha: grupo em 6 m recebe −40% por 6 s. Estandarte: 7 m, 10 s, +20% dano, −20% dano recebido, 1,5%/s de vida.
    //   Espelho de Aço: 3 s, −40% dano recebido, devolve 100% do corpo a corpo e 50% do à distância, anda a 50%.
    //   Passo da Sombra: alvo entre 4 e 14 m, imobiliza 3 s (chefes 1,2 s).
    // =====================================================================================================
    public partial class Player
    {
        public const float FrenzyEnter = 0.30f, FrenzyExit = 0.35f, FrenzyMinTime = 4f, FrenzyAtkSpeed = 1.3f;
        public const float LifestealHitCap = 0.08f, LifestealSecCap = 0.15f;
        public const float VengeanceCap = 0.6f, VengeanceToDamage = 0.35f, VengeanceDecayDelay = 6f, VengeanceDecay = 0.03f;
        public const float ReloadTime = 1.3f, LongshotMult = 1.6f, BackstabCrit = 0.5f, HeavyBlastRadius = 2.2f, HeavyBlastMult = 0.8f;
        public const float BossCC = Enemy.BossCC;

        /// <summary>Tecla de recarga manual do Pistoleiro.</summary>
        public const K ReloadKey = K.T;

        /// <summary>Marcado pelo Projectile enquanto chama TakeDamage (o Espelho de Aço devolve 50% do dano à distância).</summary>
        public static bool IncomingRanged;

        // ------------------------------------------------------------------ estado por herói
        class KitBuff { public SkillDef s; public float t, total; public Player src; }
        readonly List<KitBuff> kitBuffs = new();

        float stealthT; bool stealthOpener;
        public bool Stealthed => stealthT > 0f && state != "dead";
        public float StealthLeft => stealthT;

        public int Ammo, AmmoMax;
        public float ReloadLeft;
        public bool Reloading => ReloadLeft > 0f;
        bool leftHand;

        public bool Frenzy { get; private set; }
        float frenzyT, rageT;
        public float RageLeft => rageT;
        float lsBudget;

        public float Vengeance;
        public float VengeanceMax => MaxHp * VengeanceCap;
        float vengIdle;

        /// <summary>Mecânica da classe atual (stealth, heavy, ammo, longshot, support, arcane, frenzy, vengeance).</summary>
        public string Mechanic => GameState.Class.mechanic ?? "";
        /// <summary>Classe base (para ultimate/ataque carregado das evoluídas).</summary>
        public static string BaseClassId
        {
            get
            {
                var cd = GameState.Class;
                return cd.tier >= 2 && !string.IsNullOrEmpty(cd.parent) ? cd.parent : GameState.ClassId;
            }
        }

        /// <summary>Projéteis "de tiro" (animação Shoot).</summary>
        public static bool IsShotKind(string k) => k == "bullet" || k == "bullet_explosive" || k == "longarrow" || k == "heavy_arrow" || k == "pin_arrow";

        // vida/mana do herói (hoje o local vive no GameState; o multiplayer troca só estes acessores)
        public float MaxHp => GameState.maxHp;
        public float Hp { get => GameState.P.hp; set => GameState.P.hp = value; }

        void OnEnable() { Party.Register(this); }
        void OnDisable() { Party.Unregister(this); }

        /// <summary>Cura com número verde (usada pelas habilidades de grupo).</summary>
        public void Heal(float amount, bool popup = true)
        {
            if (state == "dead" || amount <= 0f) return;
            float before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            int got = Mathf.RoundToInt(Hp - before);
            if (popup && got > 0) HUD.Popup(transform.position + Vector3.up * 2.4f, "+" + got, U.Hex("8fe08a"));
            GameState.Emit();
        }

        /// <summary>Tira efeitos ruins (visuais de status no herói).</summary>
        public void Cleanse()
        {
            foreach (var k in new[] { "poison", "burn", "chill", "freeze", "bleed", "stun", "blind", "root_shadow" })
                StatusFX.Detach(transform, k);
        }

        void ResetClassKit()
        {
            foreach (var b in kitBuffs) StatusFX.Detach(transform, BuffFx(b.s));
            kitBuffs.Clear();
            if (Stealthed) EndStealth();
            stealthT = 0f;
            if (Frenzy) ExitFrenzy(true);
            rageT = 0f;
            Vengeance = 0f;
            var cd = GameState.Class;
            AmmoMax = cd.mechanic == "ammo" ? Mathf.Max(1, cd.ammo > 0 ? cd.ammo : 12) : 0;
            Ammo = AmmoMax;
            ReloadLeft = 0f;
        }

        // ------------------------------------------------------------------ por quadro
        void UpdateClassKit(float dt)
        {
            for (int i = kitBuffs.Count - 1; i >= 0; i--)
            {
                var b = kitBuffs[i];
                b.t -= dt;
                if (b.s.regen > 0f && state != "dead") Hp = Mathf.Min(MaxHp, Hp + MaxHp * b.s.regen * dt);
                if (b.t <= 0f) { kitBuffs.RemoveAt(i); if (!HasBuff(b.s.id)) StatusFX.Detach(transform, BuffFx(b.s)); }
            }
            if (stealthT > 0f) { stealthT -= dt; if (stealthT <= 0f) EndStealth(); }
            lsBudget = Mathf.Min(MaxHp * LifestealSecCap, lsBudget + MaxHp * LifestealSecCap * dt);

            string mech = Mechanic;
            // Pistoleiro: recarga
            if (AmmoMax > 0)
            {
                if (ReloadLeft > 0f)
                {
                    ReloadLeft -= dt;
                    if (ReloadLeft <= 0f) FinishReload();
                }
                else if (Ammo <= 0 && !InfiniteAmmo) StartReload();
                if (!inputLocked && canFight && state != "dead" && InputW.Down(ReloadKey)) ManualReload();
            }
            // Berserker: Fúria
            if (mech == "frenzy" && state != "dead")
            {
                float f = MaxHp > 0f ? Hp / MaxHp : 1f;
                if (rageT > 0f) rageT -= dt;
                if (Frenzy) frenzyT += dt;
                if (!Frenzy && (f <= FrenzyEnter || rageT > 0f)) EnterFrenzy();
                else if (Frenzy && rageT <= 0f && frenzyT >= FrenzyMinTime && f > FrenzyExit) ExitFrenzy(false);
            }
            else if (Frenzy) ExitFrenzy(true);
            // The Guard: Vingança esvazia devagar fora de combate
            if (Vengeance > 0f)
            {
                vengIdle += dt;
                if (vengIdle > VengeanceDecayDelay) Vengeance = Mathf.Max(0f, Vengeance - VengeanceMax * VengeanceDecay * dt);
                if (mech != "vengeance") Vengeance = 0f;
            }
        }

        // ------------------------------------------------------------------ buffs (próprios e de grupo)
        public void AddKitBuff(SkillDef s, float duration, Player src)
        {
            if (s == null || duration <= 0f || state == "dead") return;
            foreach (var b in kitBuffs)
                if (b.s == s) { b.t = Mathf.Max(b.t, duration); b.total = Mathf.Max(b.total, duration); b.src = src; StatusFX.Attach(transform, BuffFx(s), b.t); return; }
            kitBuffs.Add(new KitBuff { s = s, t = duration, total = duration, src = src });
            StatusFX.Attach(transform, BuffFx(s), duration);
        }

        public bool HasBuff(string id) { foreach (var b in kitBuffs) if (b.s.id == id && b.t > 0f) return true; return false; }

        /// <summary>Buff ativo mais longo (para a HUD): nome, restante, total.</summary>
        public bool TopBuff(out SkillDef s, out float left, out float total)
        {
            s = null; left = 0f; total = 1f;
            foreach (var b in kitBuffs)
                if (b.t > left && b.total > 1f) { s = b.s; left = b.t; total = b.total; }
            return s != null;
        }

        static string BuffFx(SkillDef s)
        {
            switch (s.id)
            {
                case "pr_blessing": return "bless";
                case "gd_shieldwall": case "gd_banner": case "kn_ironstance": return "guard_buff";
                case "gd_reflect": return "reflect";
                case "am_overload": return "overload";
                case "am_manashield": return "mana_shield";
                case "as_poison": return "poison";
                case "bk_roar_f": case "bk_roar": return "frenzy";
                default: return "bless";
            }
        }

        float BuffSum(System.Func<SkillDef, float> f) { float v = 0f; foreach (var b in kitBuffs) v += f(b.s); return v; }

        public float KitCrit() => BuffSum(s => s.crit);
        bool InfiniteAmmo { get { foreach (var b in kitBuffs) if (b.s.infiniteAmmo) return true; return false; } }
        public bool Unstoppable { get { foreach (var b in kitBuffs) if (b.s.unstoppable) return true; return false; } }

        float KitAttackSpeed()
        {
            float m = Mathf.Max(0.3f, GameState.Class.attackSpeed) * (1f + BuffSum(s => s.atkSpeed));
            if (Frenzy) m *= FrenzyAtkSpeed;
            return m;
        }

        float KitMoveMult()
        {
            float m = 1f;
            foreach (var b in kitBuffs) if (b.s.speedMult > 0f && !Mathf.Approximately(b.s.speedMult, 1f) && b.s.type == "stance") m *= b.s.speedMult;
            if (Stealthed) m *= 1.4f;
            return m;
        }

        /// <summary>Multiplicador do dano do ataque básico.</summary>
        float KitBasicMult() => 1f + BuffSum(s => s.atkPct);

        /// <summary>Multiplicador do dano das habilidades.</summary>
        float KitSkillMult(SkillDef s) => 1f + BuffSum(b => b.atkPct + b.spellPct);

        float KitManaCostMult() => Mathf.Clamp01(1f - BuffSum(s => s.manaCut));

        float KitDamageTakenMult()
        {
            float m = 1f;
            foreach (var b in kitBuffs)
            {
                if (b.s.dmgTaken > 0f && b.s.dmgTaken < 1f) m *= b.s.dmgTaken;
                if (b.s.defPct > 0f) m *= Mathf.Clamp01(1f - b.s.defPct);
            }
            return m;
        }

        // ------------------------------------------------------------------ dano recebido (chamado no TakeDamage)
        /// <summary>raw = dano antes das reduções; after = depois das reduções.</summary>
        void KitOnDamaged(float raw, float after, Vector3 fromPos)
        {
            if (Mechanic == "vengeance") AddVengeance(raw);
            // Muralha de Escudos: o dano evitado nos aliados enche a Vingança do Guard em dobro
            foreach (var b in kitBuffs)
                if (b.src != null && b.src != this && b.s.dmgTaken > 0f && b.s.dmgTaken < 1f && b.src.Mechanic == "vengeance")
                    b.src.AddVengeance(raw * (1f - b.s.dmgTaken) * 2f);
            // Espelho de Aço
            float melee = BuffSum(s => s.reflect), ranged = BuffSum(s => s.reflectRanged);
            float frac = IncomingRanged ? ranged : melee;
            if (frac > 0f) ReflectDamage(raw * frac, fromPos, IncomingRanged);
        }

        public void AddVengeance(float amount)
        {
            if (amount <= 0f || state == "dead") return;
            Vengeance = Mathf.Min(VengeanceMax, Vengeance + amount);
            vengIdle = 0f;
        }

        void ReflectDamage(float amount, Vector3 fromPos, bool ranged)
        {
            if (Game.I == null || amount < 1f) return;
            Enemy best = null;
            float bestScore = float.MaxValue;
            Vector3 me = transform.position;
            Vector3 toFrom = U.Flat(fromPos - me);
            foreach (var e in Game.I.enemies)
            {
                if (e == null || e.dead) continue;
                Vector3 to = U.Flat(e.transform.position - me);
                float d = to.magnitude;
                float score;
                if (!ranged) { score = U.Flat(e.transform.position - fromPos).magnitude; if (score > 3.5f + e.radius) continue; }
                else
                {
                    bool shooter = e.def != null && !string.IsNullOrEmpty(e.def.ranged);
                    if (d > 20f || toFrom.sqrMagnitude < 0.01f) continue;
                    float dot = Vector3.Dot(to / Mathf.Max(0.01f, d), toFrom.normalized);
                    if (dot < 0.6f) continue;
                    score = (1f - dot) * 20f + (shooter ? 0f : 10f);
                }
                if (score < bestScore) { bestScore = score; best = e; }
            }
            if (best == null) return;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(amount));
            SkillFX.Phase("gd_reflect", "hit", best.transform.position + Vector3.up, U.Flat(best.transform.position - me).normalized, 1f, SkillFX.Steel);
            best.TakeHit(dmg, me, false);
            RegisterHit(best, best.dead, false, dmg);
            HUD.Popup(me + Vector3.up * 2.6f, "REFLETIDO", SkillFX.Steel);
        }

        /// <summary>Escudo de Mana: o dano sai da mana (manaShield de mana por 1 de dano). Devolve o que sobra.</summary>
        float KitManaShield(float amount)
        {
            float per = BuffSum(s => s.manaShield);
            if (per <= 0f || amount <= 0f) return amount;
            float absorb = Mathf.Min(amount, GameState.P.mp / per);
            if (absorb <= 0f) return amount;
            GameState.P.mp -= absorb * per;
            HUD.Popup(transform.position + Vector3.up * 2.2f, "-" + Mathf.RoundToInt(absorb * per) + " mana", U.Hex("8fb4ff"));
            return amount - absorb;
        }

        // ------------------------------------------------------------------ acertos (roubo de vida, venenos, controle)
        /// <summary>Efeitos extras de um acerto do herói (s = habilidade, ou null no ataque básico).</summary>
        public void KitOnHit(SkillDef s, Enemy e, int dealt, bool basic)
        {
            if (e == null || dealt <= 0) return;
            float ls = (s != null ? s.lifesteal : 0f) + BuffSum(b => b.lifesteal);
            if (ls > 0f) Lifesteal(dealt * ls);
            if (e.dead) return;
            float ap = GameState.AttackPower() * ComboDamageMult;
            if (s != null)
            {
                if (!string.IsNullOrEmpty(s.dot)) e.Dot(s.dot, ap * Mathf.Max(0.1f, s.dotMult), s.dotTime > 0f ? s.dotTime : 4f, Mathf.Max(1, s.dotStacks), this);
                if (s.root > 0f) e.Root(s.root);
                if (s.slow > 0f) e.Slow(1f - s.slow, s.slowTime > 0f ? s.slowTime : 2f);
                if (s.blind > 0f) e.Blind(s.blind);
                if (s.freeze > 0f) e.Freeze(s.freeze);
                if (s.push > 0f) StartCoroutine(PushEnemy(e, U.Flat(e.transform.position - transform.position), s.push, 0.25f));
            }
            // Lâminas Envenenadas (buff): ataques e habilidades aplicam veneno
            foreach (var b in kitBuffs)
                if (!string.IsNullOrEmpty(b.s.dot) && b.s.type == "stance")
                    e.Dot(b.s.dot, ap * Mathf.Max(0.1f, b.s.dotMult), b.s.dotTime > 0f ? b.s.dotTime : 4f, Mathf.Max(1, b.s.dotStacks), this);
        }

        void Lifesteal(float amount)
        {
            amount = Mathf.Min(amount, MaxHp * LifestealHitCap, lsBudget);
            if (amount < 0.5f || state == "dead") return;
            lsBudget -= amount;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            HUD.Popup(transform.position + Vector3.up * 2.3f + Random.insideUnitSphere * 0.3f, "+" + Mathf.RoundToInt(amount), U.Hex("ff6a6a"));
            GameState.Emit();
        }

        /// <summary>Acerto de habilidade com os efeitos do kit.</summary>
        void KitHit(SkillDef s, Enemy e, int dmg, Vector3 from, bool crit)
        {
            if (e == null || e.dead) return;
            HitSkill(e, dmg, from, crit);
            KitOnHit(s, e, dmg, false);
        }

        /// <summary>AreaDamage com os efeitos do kit (roubo de vida, sangramento, controle).</summary>
        public List<Enemy> AreaDamage(Vector3 center, float radius, int dmg, Color hitColor, SkillDef s)
        {
            var hit = new List<Enemy>();
            if (Game.I == null) return hit;
            foreach (var e in Game.I.enemies.ToArray())
            {
                if (e == null || e.dead) continue;
                if (U.Flat(e.transform.position - center).magnitude <= radius + e.radius)
                {
                    KitHit(s, e, dmg, center, false);
                    FX.Burst(e.transform.position + Vector3.up, hitColor, 0.6f, 10);
                    hit.Add(e);
                }
            }
            Breakable.HitArea(center, radius, dmg);
            return hit;
        }

        // ------------------------------------------------------------------ Vingança
        /// <summary>Dano extra da Vingança para esta habilidade (não consome).</summary>
        int VengeanceBonus(SkillDef s) => s != null && s.vengeanceMult > 0f ? Mathf.RoundToInt(Vengeance * VengeanceToDamage * s.vengeanceMult) : 0;

        /// <summary>Consome o medidor (habilidades de ataque do Guard).</summary>
        void ConsumeVengeance(SkillDef s)
        {
            if (s == null || s.vengeanceMult <= 0f || Vengeance <= 0f) return;
            if (Vengeance > VengeanceMax * 0.3f)
                HUD.Popup(transform.position + Vector3.up * 2.9f, "VINGANÇA!", U.Hex("7fb0ff"), true);
            Vengeance = 0f;
        }

        // ------------------------------------------------------------------ Assassino: furtividade
        public void EnterStealth(float seconds, bool opener)
        {
            if (seconds <= 0f || state == "dead") return;
            bool was = Stealthed;
            stealthT = Mathf.Max(stealthT, seconds);
            if (opener) stealthOpener = true;
            if (!was)
            {
                StatusFX.Attach(transform, "stealth", stealthT);
                if (Game.I != null)
                    foreach (var e in Game.I.enemies)
                        if (e != null && !e.dead) { e.alerted = false; e.CancelAttack(); }
            }
            else StatusFX.Attach(transform, "stealth", stealthT);
        }

        public void EndStealth()
        {
            stealthT = 0f;
            stealthOpener = false;
            StatusFX.Detach(transform, "stealth");
        }

        /// <summary>Ataque saindo da furtividade: crítico garantido ×2,5 (consome o bônus).</summary>
        bool TakeOpener()
        {
            if (!Stealthed || !stealthOpener) return false;
            stealthOpener = false;
            return true;
        }

        bool BehindOf(Enemy e)
        {
            Vector3 to = U.Flat(e.transform.position - transform.position);
            if (to.sqrMagnitude < 0.01f) return false;
            return Vector3.Dot(U.Flat(e.transform.forward).normalized, to.normalized) > 0.35f;
        }

        // ------------------------------------------------------------------ Berserker: Fúria
        void EnterFrenzy()
        {
            Frenzy = true;
            frenzyT = 0f;
            StatusFX.Attach(transform, "frenzy", 3600f);
            HUD.Popup(transform.position + Vector3.up * 2.9f, "FÚRIA!", SkillFX.Blood, true);
            Sfx.Play(Sfx.Has("ult_ready") ? "ult_ready" : "buff", transform.position, 0.8f);
            FX.Ring(transform.position, 2.4f, SkillFX.Blood, 0.4f, 0.3f);
            FX.FlashLight(transform.position + Vector3.up * 1.2f, SkillFX.Blood, 4f, 6f, 0.35f);
            if (Game.I != null) Game.I.Shake(0.3f);
            GameState.Emit();
        }

        void ExitFrenzy(bool silent)
        {
            Frenzy = false;
            frenzyT = 0f;
            StatusFX.Detach(transform, "frenzy");
            if (!silent) HUD.Popup(transform.position + Vector3.up * 2.7f, "a fúria passa", U.Hex("d8a0a0"));
            GameState.Emit();
        }

        /// <summary>Versão furiosa da habilidade (se em Fúria e existir).</summary>
        public SkillDef ResolveAlt(SkillDef s)
        {
            if (s == null || !Frenzy || string.IsNullOrEmpty(s.alt)) return s;
            var a = GameData.Skill(s.alt);
            return a ?? s;
        }

        // ------------------------------------------------------------------ Pistoleiro: munição
        void StartReload()
        {
            if (AmmoMax <= 0 || ReloadLeft > 0f) return;
            ReloadLeft = ReloadTime;
            Transform hand = visual != null && visual.handL != null ? visual.handL : transform;
            SkillFX.ReloadFx(hand);
            Sfx.Play(Sfx.Has("reload") ? "reload" : "block", transform.position, 0.5f);
        }

        void FinishReload()
        {
            ReloadLeft = 0f;
            Ammo = AmmoMax;
            Sfx.Play(Sfx.Has("reload_done") ? "reload_done" : "coin", transform.position, 0.4f);
            HUD.Popup(transform.position + Vector3.up * 2.4f, "recarregado", U.Hex("ffd08a"));
        }

        /// <summary>Recarrega na hora (Rolamento Atirador).</summary>
        public void InstantReload() { if (AmmoMax > 0) { ReloadLeft = 0f; Ammo = AmmoMax; } }

        void ManualReload()
        {
            if (AmmoMax <= 0 || Reloading || Ammo >= AmmoMax) return;
            StartReload();
        }

        /// <summary>Pode atacar agora? (Pistoleiro sem balas / recarregando.)</summary>
        bool KitCanAttack()
        {
            if (AmmoMax <= 0 || InfiniteAmmo) return true;
            if (Reloading) return false;
            if (Ammo <= 0) { StartReload(); return false; }
            return true;
        }

        // ------------------------------------------------------------------ ataque básico
        /// <summary>Disparo do ataque básico das classes à distância (munição, mão alternada, flecha longa).</summary>
        void KitBasicShot(string kind, Vector3 dir, int dmg)
        {
            Vector3 origin = transform.position + Vector3.up * 1.1f + dir * 0.6f;
            if (kind == "bullet")
            {
                leftHand = !leftHand;
                Transform hand = visual != null ? (leftHand ? visual.handL : visual.handR) : null;
                if (hand != null) origin = new Vector3(hand.position.x, transform.position.y + 1.1f, hand.position.z) + dir * 0.35f;
                SkillFX.MuzzleFlash(origin, dir);
                if (AmmoMax > 0 && !InfiniteAmmo) { Ammo = Mathf.Max(0, Ammo - 1); if (Ammo <= 0) StartReload(); }
            }
            var m = Projectile.Spawn(kind, origin, dir, dmg, true, null);
            if (m != null) m.owner = this;
            if (Stealthed) EndStealth();
        }

        /// <summary>Cavaleiro: o 3º golpe do combo causa uma pequena explosão.</summary>
        void HeavyBlast(Vector3 c, float mult)
        {
            int d = Mathf.Max(1, Mathf.RoundToInt(GameState.AttackPower() * mult * HeavyBlastMult));
            SkillFX.Shockwave(c, HeavyBlastRadius, SkillFX.Gold, 0.3f);
            FX.Dust(c, U.Hex("c8b090"), 1f);
            foreach (var e in Game.I.enemies.ToArray())
            {
                if (e == null || e.dead) continue;
                if (U.Flat(e.transform.position - c).magnitude > HeavyBlastRadius + e.radius) continue;
                e.TakeHit(d, c, false);
                RegisterHit(e, e.dead, true, d);
            }
            Breakable.HitArea(c, HeavyBlastRadius, d);
            Game.I.Shake(0.25f);
        }
    }
}
