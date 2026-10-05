using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Herói controlado pelo jogador: andar (WASD, relativo à câmera), mirar com o mouse,
    /// atacar (clique/Espaço; segurar o clique carrega um golpe especial), defender (botão direito/Shift),
    /// parry (F), esquiva (Q), habilidades (Z X C V), ultimate (R), travar alvo (Tab).
    /// As habilidades ficam em PlayerSkills.cs, a ultimate em PlayerUltimate.cs,
    /// as passivas de combo em ComboPassives.cs e a trava de mira em LockOn.cs.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public partial class Player : MonoBehaviour
    {
        public const float AttackTime = 0.45f, HitMoment = 0.18f, DashTime = 0.22f;

        public CharacterVisual visual;
        public CharacterController cc;
        public string state = "idle";
        public float stateTime;
        public Vector3 facing = Vector3.forward;
        public bool canFight;
        public bool inputLocked;
        public bool blocking;
        public float invuln, parryTime, hurtFlash;
        public readonly Dictionary<string, float> cooldowns = new();

        Vector3 velocity;
        float vy;
        // combo de 3 golpes: 0 = Attack1, 1 = Attack2, 2 = golpe forte (Attack2, dano 1,6x, corte maior)
        public const float ComboReset = 0.8f, ComboFinisherMult = 1.6f;
        int comboStep, attackStep;
        float lastAttackTime = -10f;
        public int ComboStep => attackStep;
        bool attackResolved = true;
        Vector3 dashDir;
        float stepClock;
        LineRenderer aimLine;
        GameObject footRing;
        Light torch;
        string builtClass = "";

        // ------------------------------------------------------------------ [Combate] passivas, trava, morte
        /// <summary>Passivas de combo (10/25/50 acertos). Criado no Rebuild.</summary>
        public ComboPassives combo;
        /// <summary>Trava de mira (Tab). Criado no Rebuild.</summary>
        public LockOn lockOn;
        /// <summary>Disparado quando o herói morre (depois da penalidade de XP).</summary>
        public event System.Action<Player> Died;
        public int deathCount;
        /// <summary>XP perdido na última morte.</summary>
        public int lastXpLost;
        public const float DeathXpLoss = 0.5f;
        float ghostClock;
        int shotCount;

        // ------------------------------------------------------------------ [Combate] ataque carregado (segurar o clique)
        public const float ChargeDelay = 0.35f, ChargeFull = 1.2f, ChargedCooldown = 1.5f, ChargedStamina = 12f;
        /// <summary>Carregando agora (segurando o botão esquerdo).</summary>
        public bool charging;
        /// <summary>0..1 (1 = carga máxima).</summary>
        public float chargeLevel;
        /// <summary>Recarga restante do ataque carregado (s).</summary>
        public float chargedCd;
        /// <summary>(restante, total) da recarga do ataque carregado, para a HUD.</summary>
        public Vector2 ChargedCooldownInfo => new Vector2(chargedCd, ChargedCooldown);
        bool pressing, pressAttacked, chargeMaxed;
        float pressT, chargeFxClock;
        GameObject chargeRing;
        Light chargeLight;

        public static Player Create(Vector3 pos)
        {
            var go = new GameObject("Jogador");
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.4f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0); cc.slopeLimit = 50; cc.stepOffset = 0.35f;
            var p = go.AddComponent<Player>();
            p.cc = cc;
            var v = new GameObject("visual");
            v.transform.SetParent(go.transform, false);
            p.visual = v.AddComponent<CharacterVisual>();
            p.Rebuild();
            return p;
        }

        /// <summary>Recria o modelo conforme a classe e o equipamento.</summary>
        public void Rebuild()
        {
            var cd = GameState.Class;
            builtClass = GameState.ClassId;
            visual.Build("hero_" + cd.model, U.Hex(cd.color));
            visual.transform.localScale = Vector3.one * Mathf.Max(0.5f, cd.scale);   // Berserker / The Guard são maiores
            cooldowns.Clear();
            CancelCharge();
            chargedCd = 0f;
            if (combo == null) combo = GetComponent<ComboPassives>();
            if (combo == null) combo = gameObject.AddComponent<ComboPassives>();
            else combo.ResetAll();
            if (lockOn == null) lockOn = GetComponent<LockOn>();
            if (lockOn == null) lockOn = gameObject.AddComponent<LockOn>();
            ResetClassKit();   // [Classes] munição, Fúria, Vingança, buffs (ClassKit.cs)
            RefreshEquipment();
            if (aimLine == null)
            {
                var a = new GameObject("mira");
                a.transform.SetParent(transform, false);
                aimLine = a.AddComponent<LineRenderer>();
                aimLine.sharedMaterial = U.Fx(true);
                aimLine.widthMultiplier = 0.06f;
                aimLine.useWorldSpace = true;
                aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (footRing == null)
            {
                footRing = FX.FlatQuad("anel", transform.position, U.RingTexture(), U.Hex(cd.color, 0.6f), true);
                footRing.transform.SetParent(transform, true);
                footRing.transform.localPosition = new Vector3(0, 0.04f, 0);
                footRing.transform.localScale = Vector3.one * 1.3f;
            }
            FX.SetColor(footRing.GetComponent<Renderer>(), U.Hex(cd.color, 0.55f));
            if (torch == null)
            {
                var t = new GameObject("tocha");
                t.transform.SetParent(transform, false);
                t.transform.localPosition = new Vector3(0, 2.6f, 0);
                torch = t.AddComponent<Light>();
                torch.type = LightType.Point; torch.range = 9f; torch.intensity = 2.2f; torch.color = U.Hex("ffc98a");
                torch.shadows = LightShadows.None;
            }
        }

        public void RefreshEquipment()
        {
            Equipment.WeaponFor(out string wm, out string off, out string glow);
            visual.SetWeapon(wm, off, glow);
            visual.SetTint(Color.white);
            // capacete, capa, asas, tintas de peitoral/calça/bota e cores de aparência (Equipment.cs)
            Equipment.Apply(visual);
            SetArmorAura(GameState.Equipped("peitoral"));
        }

        ParticleSystem aura;
        void SetArmorAura(ItemDef a)
        {
            if (aura != null) Destroy(aura.gameObject);
            string g = GameData.RarityGlow(a);
            if (string.IsNullOrEmpty(g)) return;
            var go = new GameObject("aura");
            go.transform.SetParent(transform, false);
            aura = go.AddComponent<ParticleSystem>();
            aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = aura.main;
            m.loop = true; m.startLifetime = 1.2f; m.startSpeed = 0.4f; m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            m.startColor = U.Hex(g); m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.15f;
            var em = aura.emission; em.rateOverTime = a.rarity == "lendario" ? 26 : 12;
            var sh = aura.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.55f; sh.rotation = new Vector3(90, 0, 0);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = U.Fx(true);
            aura.Play();
        }

        public void SetTorch(bool on) { if (torch) torch.enabled = on; }

        void SetState(string s) { if (state == s) return; state = s; stateTime = 0; }

        public bool Busy => state == "attack" || state == "cast" || state == "dash" || state == "charge" || state == "leap" || state == "whirl" || state == "hurt" || state == "dead";

        /// <summary>Multiplicador de velocidade de ataque (passiva de combo x10).</summary>
        float AtkSpeed => (combo != null ? combo.AttackSpeedMult : 1f) * KitAttackSpeed();   // [Classes] ClassDef.attackSpeed, Fúria, buffs
        /// <summary>Multiplicador de dano da passiva de combo (x25).</summary>
        public float ComboDamageMult => combo != null ? combo.DamageMult : 1f;
        void ComboHit(int dmg) { if (combo != null) combo.OnHit(dmg); }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (builtClass != GameState.ClassId) Rebuild();
            float dt = Time.deltaTime;
            stateTime += dt;
            invuln = Mathf.Max(0, invuln - dt);
            parryTime = Mathf.Max(0, parryTime - dt);
            chargedCd = Mathf.Max(0, chargedCd - dt);
            var keys = new List<string>(cooldowns.Keys);
            foreach (var k in keys) cooldowns[k] = Mathf.Max(0, cooldowns[k] - dt);
            UpdateSkillStates(dt);
            UpdateUltimate(dt);
            UpdateClassKit(dt);
            if (GameState.atkBuff > 0) GameState.atkBuff = Mathf.Max(0, GameState.atkBuff - dt);

            Vector2 mv = (inputLocked || state == "dead") ? Vector2.zero : InputW.Move();
            Vector3 input = CameraRig.ToWorld(mv);
            blocking = canFight && !inputLocked && !charging && (InputW.Held(K.Shift) || InputW.MouseHeld(1)) && GameState.stamina > 1f && (state == "idle" || state == "run");
            if (!inputLocked && canFight && state != "dead")
            {
                UpdateAttackButton(dt);
                if (InputW.Down(K.Space)) TryAttack();
                if (InputW.Down(K.F)) TryParry();
                if (InputW.Down(K.Q)) TryDash(input);
                for (int i = 0; i < 4; i++)
                {
                    K key = i == 0 ? K.Z : i == 1 ? K.X : i == 2 ? K.C : K.V;
                    if (InputW.Down(key)) UseSkill(i);
                }
                if (InputW.Down(K.R)) UseUltimate();
            }
            else
            {
                pressing = false;
                if (charging) CancelCharge();
            }

            Vector3 move = Vector3.zero;
            switch (state)
            {
                case "dash":
                    move = dashDir * 13f * (1f - stateTime / DashTime * 0.6f);
                    // i-frames da esquiva: rastro fantasma (afterimage)
                    ghostClock -= dt;
                    if (ghostClock <= 0f) { ghostClock = 0.05f; SpawnGhost(); }
                    if (stateTime >= DashTime) SetState("idle");
                    break;
                case "charge":
                    move = dashDir * chargeSpeed;
                    ChargeHits();
                    if (stepClock <= 0)
                    {
                        FX.Dust(transform.position, U.Hex("c8b090"), 0.6f);
                        if (chargeSkill != null) SkillFX.Phase(chargeSkill.id, "trail", transform.position, dashDir, 1f, U.Hex(chargeSkill.color));
                        stepClock = 0.05f;
                    }
                    if (stateTime >= chargeTime) SetState("idle");
                    break;
                case "leap":
                    UpdateLeap();
                    break;
                case "whirl":
                    move = input * GameState.MoveSpeed() * whirlSpeed * KitMoveMult();
                    if (input != Vector3.zero) facing = input;
                    UpdateWhirl(dt);
                    break;
                case "cast":
                    if (stateTime >= 0.45f) SetState("idle");
                    break;
                case "attack":
                {
                    // velocidade de ataque (passiva de combo): o golpe acontece e termina mais cedo
                    float st = stateTime * AtkSpeed;
                    if (st < 0.12f) move = facing * 1.2f;
                    if (!attackResolved && st >= HitMoment) ResolveAttack();
                    if (st >= AttackTime) SetState("idle");
                    break;
                }
                case "hurt":
                    move = velocity;
                    velocity = Vector3.MoveTowards(velocity, Vector3.zero, dt * 30f);
                    if (stateTime >= 0.22f) SetState("idle");
                    break;
                case "dead":
                    break;
                default:
                    float speed = GameState.MoveSpeed() * KitMoveMult() * (blocking ? 0.45f : 1f) * (charging ? 0.5f : 1f);
                    if (input != Vector3.zero) { facing = charging ? AimDir() : input; SetState("run"); }
                    else SetState("idle");
                    velocity = Vector3.MoveTowards(velocity, input * speed, dt * 60f);
                    move = velocity;
                    break;
            }
            stepClock -= dt;
            // gravidade
            if (cc.isGrounded) vy = -1f; else vy -= 25f * dt;
            if (state != "leap") cc.Move((move + Vector3.up * vy) * dt);

            // vira para onde está indo/mirando
            if (facing.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(U.Flat(facing).normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-18f * dt));
            }

            // recursos
            if (blocking) GameState.stamina = Mathf.Max(0, GameState.stamina - dt * 6f);
            else if (state != "dash" && !charging) GameState.stamina = Mathf.Min(GameState.maxStamina, GameState.stamina + dt * (state == "idle" ? 22f : 14f));
            GameState.P.mp = Mathf.Min(GameState.maxMp, GameState.P.mp + dt * 2f);

            UpdateAnim();
            UpdateIndicators();
            if (state == "run")
            {
                stepClock -= dt;
                if (stepClock < -0.3f) { stepClock = 0; Sfx.Play("step", transform.position, 0.6f, 0.15f); FX.Dust(transform.position - facing * 0.2f, U.Hex("c8b8a0"), 0.35f); }
            }
        }

        void UpdateAnim()
        {
            visual.SetMoving(state == "run" || state == "charge" || state == "whirl");
            if (state == "run") visual.SetSpeed01(velocity.magnitude / Mathf.Max(0.1f, GameState.MoveSpeed()));
            bool chargeStance = charging && string.IsNullOrEmpty(GameState.Class.ranged);
            switch (state)
            {
                case "run": visual.Play(blocking || charging ? "Walk" : "Run"); break;
                case "idle": visual.Play(blocking || chargeStance ? "Block" : "Idle"); break;
                case "dash": visual.Play("Dodge", 0.05f, 0.2f); break;
                case "charge": visual.Play("Run"); break;
                case "dead": visual.Play("Death", 0.1f, 99f); break;
            }
        }

        // ------------------------------------------------------------------ indicadores (onde o golpe vai acertar)
        void UpdateIndicators()
        {
            if (aimLine == null) return;
            bool show = canFight && state != "dead";
            aimLine.enabled = show;
            if (!show) return;
            var cd = GameState.Class;
            Vector3 aim = AimDir();
            Vector3 o = transform.position + Vector3.up * 0.08f;
            bool attacking = state == "attack" || state == "cast" || state == "whirl";
            if (!string.IsNullOrEmpty(cd.ranged))
            {
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, o + aim * 0.6f);
                aimLine.SetPosition(1, o + aim * 7f);
                var c = new Color(1f, 0.95f, 0.7f, attacking ? 0.9f : 0.45f);
                aimLine.startColor = c; aimLine.endColor = new Color(c.r, c.g, c.b, 0f);
            }
            else
            {
                const int n = 14;
                aimLine.positionCount = n;
                float reach = cd.reach + 0.3f;
                float ang = Mathf.Atan2(aim.x, aim.z);
                for (int i = 0; i < n; i++)
                {
                    float a = ang - 0.9f + 1.8f * i / (n - 1);
                    aimLine.SetPosition(i, o + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * reach);
                }
                var c = new Color(1f, 0.9f, 0.6f, attacking ? 0.9f : 0.4f);
                aimLine.startColor = c; aimLine.endColor = c;
            }
            // inimigos ao alcance ficam marcados
            if (Game.I != null)
                foreach (var e in Game.I.enemies)
                    if (e != null) e.SetTargeted(!e.dead && string.IsNullOrEmpty(cd.ranged) && InMeleeCone(e, aim, cd.reach));
        }

        bool InMeleeCone(Enemy e, Vector3 aim, float reach)
        {
            Vector3 to = U.Flat(e.transform.position - transform.position);
            float d = to.magnitude;
            if (d > reach + e.radius) return false;
            return d < 0.6f || Vector3.Dot(aim, to.normalized) > 0.2f;
        }

        /// <summary>Alvo travado (Tab) válido, ou null.</summary>
        Enemy LockedTarget => lockOn != null && lockOn.Target != null && !lockOn.Target.dead ? lockOn.Target : null;

        /// <summary>Direção de mira: alvo travado (Tab); senão mouse no chão; sem mouse, para a frente.</summary>
        public Vector3 AimDir()
        {
            var lt = LockedTarget;
            if (lt != null)
            {
                Vector3 d = U.Flat(lt.transform.position - transform.position);
                if (d.sqrMagnitude > 0.01f) return d.normalized;
            }
            if (U.MouseOnGround(CameraRig.Cam, transform.position.y, out var p))
            {
                Vector3 d = U.Flat(p - transform.position);
                if (d.sqrMagnitude > 0.05f) return d.normalized;
            }
            return U.Flat(facing).normalized;
        }

        public Vector3 AimPoint(float maxRange)
        {
            var lt = LockedTarget;
            if (lt != null)
            {
                Vector3 d = U.Flat(lt.transform.position - transform.position);
                if (d.magnitude > maxRange) d = d.normalized * maxRange;
                return transform.position + d;
            }
            if (U.MouseOnGround(CameraRig.Cam, transform.position.y, out var p))
            {
                Vector3 d = U.Flat(p - transform.position);
                if (d.magnitude > maxRange) d = d.normalized * maxRange;
                return transform.position + d;
            }
            return transform.position + U.Flat(facing).normalized * maxRange;
        }

        // ------------------------------------------------------------------ ações básicas
        public void TryAttack()
        {
            if (!canFight || inputLocked || Busy) return;
            if (!KitCanAttack()) return;   // [Classes] Pistoleiro sem balas / recarregando
            if (GameState.stamina < 8f) { GameState.Notify("Sem fôlego!"); return; }
            GameState.stamina -= 8f;
            // combo: reinicia se ficou mais de ComboReset s sem atacar depois do fim do último golpe
            if (Time.time - lastAttackTime > AttackTime + ComboReset) comboStep = 0;
            attackStep = comboStep;
            comboStep = (comboStep + 1) % 3;
            lastAttackTime = Time.time;
            attackResolved = false;
            facing = AimDir();
            transform.rotation = Quaternion.LookRotation(facing);
            SetState("attack");
            var cd = GameState.Class;
            string anim = cd.ranged == "arrow" || IsShotKind(cd.ranged) ? "Shoot" : cd.ranged == "orb" || cd.ranged == "holy" ? "Cast" : (attackStep == 0 ? "Attack1" : "Attack2");
            visual.Play(anim, 0.05f, AttackTime / AtkSpeed, true);
            if (string.IsNullOrEmpty(cd.ranged))
            {
                // som por golpe do combo (swing / swing2 / swing3), com fallback para os sons antigos
                bool heavy = cd.weapon == "axe" || cd.weapon == "sword_2handed";
                string snd = attackStep == 0 ? (heavy ? "swing_heavy" : "swing") : attackStep == 1 ? "swing2" : "swing3";
                if (!Sfx.Has(snd)) snd = attackStep == 2 || heavy ? "swing_heavy" : "swing";
                Sfx.Play(snd, transform.position, attackStep == 2 ? 1f : 0.9f);
            }
        }

        /// <summary>Cor do corte: brilho de raridade da arma equipada, senão a cor da classe clareada.</summary>
        Color WeaponSlashColor()
        {
            string glow = GameData.RarityGlow(GameState.Equipped("arma"));
            Color baseC = !string.IsNullOrEmpty(glow) ? U.Hex(glow) : U.Hex(GameState.Class.color);
            return Color.Lerp(baseC, Color.white, 0.35f);
        }

        public void TryParry()
        {
            if (!canFight || inputLocked || state == "dash" || state == "dead") return;
            if (GameState.stamina < 6f) return;
            GameState.stamina -= 6f;
            parryTime = 0.25f;
            visual.Play("Block", 0.05f, 0.3f, true);
            FX.Ring(transform.position, 1.2f, U.Hex("d8c8ff"), 0.25f, 0.5f);
            Sfx.Play("block", transform.position, 0.5f);
        }

        public void TryDash(Vector3 input)
        {
            if (!canFight || inputLocked || state == "dash" || state == "dead" || state == "leap") return;
            if (GameState.stamina < 18f) return;
            GameState.stamina -= 18f;
            if (charging) CancelCharge();
            dashDir = input != Vector3.zero ? input : U.Flat(facing).normalized;
            facing = dashDir;
            invuln = DashTime + 0.05f;
            SetState("dash");
            FX.Dust(transform.position, U.Hex("d8d0c0"), 0.8f);
            Sfx.Play("dash", transform.position);
            SpawnGhost();
            ghostClock = 0.05f;
        }

        /// <summary>Cópia translúcida da silhueta (rastro da esquiva).</summary>
        void SpawnGhost()
        {
            if (visual == null) return;
            Color c = Color.Lerp(U.Hex(GameState.Class.color), new Color(0.75f, 0.9f, 1f), 0.4f);
            SkillFX.Afterimage(visual.transform, c, 0.3f);
        }

        // ------------------------------------------------------------------ [Combate] ataque carregado
        /// <summary>Clique rápido = combo de 3 golpes; segurar &gt; 0,35 s carrega; soltar = ataque carregado da classe.</summary>
        void UpdateAttackButton(float dt)
        {
            if (charging && state != "idle" && state != "run") CancelCharge();
            bool held = InputW.MouseHeld(0);
            if (!pressing)
            {
                if (held && InputW.MouseDown(0) && !HUD.PointerOverUI())
                {
                    pressing = true;
                    pressT = 0f;
                    pressAttacked = AttackNow();
                }
                return;
            }
            if (!held)
            {
                pressing = false;
                if (charging) ReleaseCharge();
                else if (!pressAttacked && pressT < ChargeDelay) AttackNow();   // clique durante outro golpe: tenta mais uma vez
                return;
            }
            pressT += dt;
            if (charging) { UpdateCharge(dt); return; }
            if (pressT < ChargeDelay)
            {
                // clique feito no fim de um golpe: o próximo do combo sai assim que o herói ficar livre
                if (!pressAttacked && !Busy && GameState.stamina >= 8f) pressAttacked = AttackNow();
                return;
            }
            if (chargedCd <= 0f && GameState.stamina >= ChargedStamina)
            {
                if (!Busy) BeginCharge();
            }
            else TryAttack();   // carga em recarga/sem fôlego: segurar volta a atacar em sequência (como antes)
        }

        /// <summary>TryAttack que diz se o golpe saiu.</summary>
        bool AttackNow()
        {
            float before = lastAttackTime;
            TryAttack();
            return !Mathf.Approximately(before, lastAttackTime) && state == "attack";
        }

        void BeginCharge()
        {
            charging = true;
            chargeLevel = 0f;
            chargeMaxed = false;
            chargeFxClock = 0f;
            facing = AimDir();
            Color c = ChargeColor();
            if (chargeRing == null)
            {
                chargeRing = FX.FlatQuad("carga", transform.position + Vector3.up * 0.05f, U.RingTexture(), c, true);
                chargeRing.transform.SetParent(transform, true);
            }
            chargeRing.SetActive(true);
            chargeRing.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            chargeRing.transform.localScale = Vector3.one * 0.6f;
            if (chargeLight == null)
            {
                var lg = new GameObject("luz_carga");
                chargeLight = lg.AddComponent<Light>();
                chargeLight.type = LightType.Point;
                chargeLight.shadows = LightShadows.None;
            }
            Transform hand = visual != null && visual.handR != null ? visual.handR : transform;
            chargeLight.transform.SetParent(hand, false);
            chargeLight.transform.localPosition = hand == transform ? new Vector3(0f, 1.2f, 0.4f) : Vector3.zero;
            chargeLight.color = c;
            chargeLight.intensity = 0.3f;
            chargeLight.range = 2f;
            chargeLight.enabled = true;
            Sfx.Play(Sfx.Has("charge") ? "charge" : "magic_cast", transform.position, 0.55f);
        }

        void UpdateCharge(float dt)
        {
            chargeLevel = Mathf.Clamp01(Mathf.InverseLerp(ChargeDelay, ChargeFull, pressT));
            if (state == "idle") facing = AimDir();
            Color c = ChargeColor();
            float k = chargeLevel;
            float reach = Mathf.Max(1.2f, GameState.Class.reach);
            if (chargeRing != null)
            {
                float pulse = chargeMaxed ? 1f + 0.06f * Mathf.Sin(Time.time * 18f) : 1f;
                chargeRing.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, reach * 2.4f, k) * pulse;
                chargeRing.transform.rotation = Quaternion.Euler(90f, Time.time * 140f, 0f);
                FX.SetColor(chargeRing.GetComponent<Renderer>(), new Color(c.r, c.g, c.b, 0.35f + 0.5f * k));
            }
            if (chargeLight != null)
            {
                chargeLight.intensity = 0.4f + 3.6f * k + (chargeMaxed ? Mathf.Sin(Time.time * 20f) * 0.6f : 0f);
                chargeLight.range = 2f + 2.5f * k;
            }
            chargeFxClock -= dt;
            if (chargeFxClock <= 0f)
            {
                chargeFxClock = Mathf.Lerp(0.16f, 0.06f, k);
                Vector3 hp = chargeLight != null ? chargeLight.transform.position : transform.position + Vector3.up * 1.2f;
                SkillFX.Sparks(hp, c, 2 + Mathf.RoundToInt(4 * k), 0.4f, 1.6f, -0.3f);
            }
            if (!chargeMaxed && k >= 1f)
            {
                chargeMaxed = true;
                FX.Ring(transform.position, reach * 1.2f, c, 0.3f, 0.4f);
                FX.FlashLight(transform.position + Vector3.up * 1.2f, c, 3.5f, 5f, 0.25f);
                Sfx.Play("buff", transform.position, 0.6f);
            }
        }

        void EndChargeFx()
        {
            if (chargeRing != null) chargeRing.SetActive(false);
            if (chargeLight != null) chargeLight.enabled = false;
        }

        /// <summary>Interrompe a carga sem atacar (dano, esquiva, troca de mapa...).</summary>
        public void CancelCharge()
        {
            charging = false;
            chargeLevel = 0f;
            chargeMaxed = false;
            EndChargeFx();
        }

        void ReleaseCharge()
        {
            float k = chargeLevel;
            CancelCharge();
            if (!canFight || inputLocked || state == "dead" || Busy) return;
            chargedCd = ChargedCooldown;
            GameState.stamina = Mathf.Max(0f, GameState.stamina - ChargedStamina);
            ChargedAttack(k);
        }

        Color ChargeColor()
        {
            switch (BaseClassId)
            {
                case "guerreiro": return SkillFX.Fire;
                case "tank": return SkillFX.TankBlue;
                case "arqueiro": return SkillFX.Leaf;
                case "mago": return SkillFX.Arcane;
                default: return U.Hex(GameState.Class.color);
            }
        }

        /// <summary>Ataque carregado por classe. k = carga 0..1 (escala o dano).</summary>
        void ChargedAttack(float k)
        {
            string cls = BaseClassId;   // [Classes] evoluídas usam o ataque carregado da classe base
            facing = AimDir();
            transform.rotation = Quaternion.LookRotation(facing);
            float maxMult = cls == "guerreiro" ? 2.2f : cls == "arqueiro" ? 2.5f : cls == "mago" ? 2.2f : 1.8f;
            float mult = Mathf.Lerp(1.2f, maxMult, k) * ComboDamageMult;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(GameState.AttackPower() * mult));
            Color c = ChargeColor();
            Vector3 pos = transform.position;
            HUD.Popup(pos + Vector3.up * 2.5f, k >= 1f ? "CARGA MÁXIMA!" : "Carregado", c, k >= 1f);
            switch (cls)
            {
                case "arqueiro":
                {
                    // flecha perfurante carregada (atravessa todos)
                    CastAnim("Shoot", 0.4f);
                    var pm = Projectile.Spawn("pierce", pos + Vector3.up * 1.1f + facing * 0.6f, facing, dmg, true, null);
                    if (pm != null) { pm.owner = this; pm.MakeCharged(0f, 1.2f + 0.5f * k); }
                    SkillFX.WindRing(pos + Vector3.up * 1.1f + facing * 0.9f, facing, SkillFX.Leaf, 0.45f + 0.3f * k, 24);
                    FX.FlashLight(pos + Vector3.up * 1.2f + facing * 0.7f, c, 3f, 4f, 0.15f);
                    if (Game.I != null) Game.I.Shake(0.15f + 0.15f * k);
                    break;
                }
                case "mago":
                {
                    // orbe grande que explode em área
                    CastAnim("Cast", 0.45f);
                    var pm = Projectile.Spawn("orb", pos + Vector3.up * 1.1f + facing * 0.7f, facing, dmg, true, null);
                    if (pm != null) { pm.owner = this; pm.MakeCharged(2.2f + 1.3f * k, 1.5f + 0.9f * k); }
                    SkillFX.Implode(pos + Vector3.up * 1.2f + facing * 0.7f, 1.2f, c, 0.2f, 20);
                    FX.FlashLight(pos + Vector3.up * 1.2f + facing * 0.7f, c, 3.5f, 5f, 0.2f);
                    if (Game.I != null) Game.I.Shake(0.15f + 0.15f * k);
                    break;
                }
                case "tank":
                    ChargedShieldBash(k, dmg, c);
                    break;
                default:
                    ChargedWhirl(k, dmg, c);
                    break;
            }
        }

        /// <summary>Guerreiro (e classes corpo a corpo sem golpe próprio): golpe giratório amplo.</summary>
        void ChargedWhirl(float k, int dmg, Color c)
        {
            var cd = GameState.Class;
            Vector3 pos = transform.position;
            float radius = cd.reach * (1.3f + 0.4f * k);
            CastAnim("Spin", 0.45f);
            Sfx.Play("swing_heavy", pos, 1f);
            FX.SlashArc(pos + Vector3.up * 1.0f, facing, c, radius, 350f, true, 0.26f, 0.85f, 26);
            FX.SlashArc(pos + Vector3.up * 0.9f, -facing, Color.Lerp(c, Color.white, 0.4f), radius * 0.8f, 300f, false, 0.3f, 0.5f, 10);
            SkillFX.Shockwave(pos, radius, c, 0.35f);
            bool any = false, anyCrit = false;
            foreach (var e in Game.I.enemies.ToArray())
            {
                if (e == null || e.dead) continue;
                if (U.Flat(e.transform.position - pos).magnitude > radius + e.radius) continue;
                bool crit = Random.value < 0.12f + GameState.CritBonus();
                int d = crit ? dmg * 2 : dmg;
                e.TakeHit(d, pos, crit);
                RegisterHit(e, e.dead, true, d);
                SkillFX.Sparks(e.transform.position + Vector3.up, c, 14, 3f, 7f, 1f);
                if (crit) anyCrit = true;
                any = true;
            }
            Breakable.HitArea(pos, radius, dmg);
            Game.I.Shake(0.35f + 0.2f * k);
            if (any)
            {
                Sfx.Play(Sfx.Has("hit_heavy") ? "hit_heavy" : "hit_crit", pos, 1f);
                Game.I.Hitstop(anyCrit ? 0.15f : 0.12f);
            }
        }

        /// <summary>Tank: pancada de escudo que empurra em cone e atordoa.</summary>
        void ChargedShieldBash(float k, int dmg, Color c)
        {
            var cd = GameState.Class;
            Vector3 pos = transform.position;
            float range = cd.reach + 1.2f + 0.8f * k;
            CastAnim("Attack1", 0.45f);
            Sfx.Play(Sfx.Has("sk_bash") ? "sk_bash" : "block", pos, 1f);
            SkillFX.ConeBurst(pos + Vector3.up * 0.9f + facing * 0.6f, facing, c, 35f, 6f, 12f, 30, 0f);
            SkillFX.Shockwave(pos + facing * 1.5f, 1.5f + k, c, 0.3f);
            FX.Dust(pos + facing * 1.2f, U.Hex("c8b8a0"), 0.9f);
            bool any = false, anyCrit = false;
            foreach (var e in Game.I.enemies.ToArray())
            {
                if (e == null || e.dead) continue;
                Vector3 to = U.Flat(e.transform.position - pos);
                float dist = to.magnitude;
                if (dist > range + e.radius) continue;
                if (dist > 0.6f && Vector3.Dot(facing, to / dist) < 0.3f) continue;
                bool crit = Random.value < 0.12f + GameState.CritBonus();
                int d = crit ? dmg * 2 : dmg;
                e.TakeHit(d, pos, crit);
                RegisterHit(e, e.dead, true, d);
                if (!e.dead)
                {
                    e.Stun(0.6f + 1.0f * k);
                    StartCoroutine(PushEnemy(e, dist > 0.01f ? to : facing, 2f + 2.5f * k, 0.25f));
                }
                FX.Burst(e.transform.position + Vector3.up, c, 0.7f, 14);
                if (crit) anyCrit = true;
                any = true;
            }
            Breakable.HitCone(pos, facing, range, 0.3f, dmg);
            Game.I.Shake(0.35f + 0.2f * k);
            if (any)
            {
                Sfx.Play(Sfx.Has("hit_heavy") ? "hit_heavy" : "hit_crit", pos + facing, 1f);
                Game.I.Hitstop(anyCrit ? 0.15f : 0.12f);
            }
        }

        void ResolveAttack()
        {
            attackResolved = true;
            var cd = GameState.Class;
            bool finisher = attackStep == 2;
            float mult = (finisher ? ComboFinisherMult : 1f) * ComboDamageMult;
            if (!string.IsNullOrEmpty(cd.ranged))
            {
                float bm = KitBasicMult() * (cd.mechanic == "longshot" ? LongshotMult : 1f);   // [Classes]
                int pd = Mathf.Max(1, Mathf.RoundToInt((GameState.AttackPower() + (cd.ranged == "orb" ? 1 : 0)) * mult * bm));
                KitBasicShot(cd.ranged, facing, pd);
                // Frenesi do Arqueiro: cada 3º tiro dispara 3 flechas
                shotCount++;
                if (combo != null && combo.TripleShot && shotCount % 3 == 0)
                {
                    Shoot(cd.ranged, Quaternion.Euler(0f, -12f, 0f) * facing, pd, null);
                    Shoot(cd.ranged, Quaternion.Euler(0f, 12f, 0f) * facing, pd, null);
                    SkillFX.WindRing(transform.position + Vector3.up * 1.1f + facing * 0.8f, facing, SkillFX.Leaf, 0.4f, 16);
                }
                if (finisher) FX.FlashLight(transform.position + Vector3.up * 1.2f + facing * 0.7f, U.Hex(cd.color), 2.5f, 3.5f, 0.15f);
                return;
            }
            bool berserk = Berserk;
            if (berserk) mult *= 1.6f;
            mult *= KitBasicMult();   // [Classes] buffs de ataque (Bênção, Estandarte, Foco...)
            Color sc = berserk ? SkillFX.Fire : WeaponSlashColor();
            // alterna o lado: 1º golpe direita→esquerda, 2º esquerda→direita, 3º (forte) direita→esquerda
            bool rightToLeft = attackStep != 1;
            float rad = cd.reach * (finisher ? 0.95f : 0.8f) * (berserk ? 1.2f : 1f);
            FX.SlashArc(transform.position + Vector3.up * 1.0f, facing, sc, rad, finisher ? 170f : 140f, rightToLeft,
                        finisher ? 0.22f : 0.18f, finisher ? 0.75f : 0.55f, finisher ? 22 : 14);
            if (finisher) FX.Dust(transform.position + facing * 1.2f, U.Hex("c8b8a0"), 0.6f);
            bool any = false, anyCrit = false;
            var hitList = new List<Enemy>();
            foreach (var e in Game.I.enemies.ToArray())
            {
                if (e == null || e.dead) continue;
                if (!InMeleeCone(e, facing, cd.reach)) continue;
                hitList.Add(e);
            }
            // Fúria do Berserker: o golpe também acerta em área em volta do ponto do corte
            Vector3 blastC = transform.position + facing * 1.2f;
            if (berserk)
            {
                foreach (var e in Game.I.enemies.ToArray())
                {
                    if (e == null || e.dead || hitList.Contains(e)) continue;
                    if (U.Flat(e.transform.position - blastC).magnitude <= cd.reach + e.radius) hitList.Add(e);
                }
                SkillFX.Embers(blastC, 0.8f, SkillFX.Ember, 14, 0.6f, 3f);
                FX.Ring(blastC, cd.reach, SkillFX.Fire, 0.25f, 0.3f);
            }
            foreach (var e in hitList)
            {
                if (e == null || e.dead) continue;
                // [Classes] golpe pelas costas (+50% crítico do Assassino), saída da furtividade (crítico x2,5)
                bool opener = TakeOpener();
                float backC = cd.mechanic == "stealth" && BehindOf(e) ? BackstabCrit : 0f;
                bool crit = opener || Random.value < 0.12f + GameState.CritBonus() + KitCrit() + backC;
                int dmg = Mathf.Max(1, Mathf.RoundToInt((GameState.AttackPower() + Random.Range(0, 2)) * mult));
                if (crit) dmg = Mathf.RoundToInt(dmg * (opener ? 2.5f : 2f));
                e.TakeHit(dmg, transform.position, crit);
                RegisterHit(e, e.dead, true, dmg);
                KitOnHit(null, e, dmg, true);
                // partículas de impacto na cor do elemento da arma
                SkillFX.Sparks(e.transform.position + Vector3.up, sc, crit ? 14 : 8, 2.5f, 6f, 1f);
                if (crit) { anyCrit = true; FX.FlashLight(e.transform.position + Vector3.up, U.Hex("fff0c0"), 4.5f, 4f, 0.1f); }
                if (berserk) FX.Burst(e.transform.position + Vector3.up, SkillFX.Fire, 0.7f, 12);
                any = true;
            }
            // [Quebraveis] barris, caixas e vasos no corte
            float bdmg = GameState.AttackPower() * mult;
            if (Breakable.HitCone(transform.position, facing, cd.reach, 0.2f, bdmg) && !any) Game.I.Shake(0.1f);
            if (berserk) Breakable.HitArea(blastC, cd.reach, bdmg);
            if (finisher && cd.mechanic == "heavy") HeavyBlast(blastC, mult);   // [Classes] passiva do Cavaleiro
            if (any && Stealthed) EndStealth();
            if (any)
            {
                if (finisher) Sfx.Play(Sfx.Has("hit_heavy") ? "hit_heavy" : "hit_crit", blastC, 0.9f);
                Game.I.Shake(finisher || anyCrit ? 0.26f : 0.18f);
                // crítico: hitstop maior
                Game.I.Hitstop(anyCrit ? 0.1f : finisher ? 0.08f : 0.05f);
            }
        }

        public ProjectileMover Shoot(string kind, Vector3 dir, int dmg, SkillDef s)
        {
            var m = Projectile.Spawn(kind, transform.position + Vector3.up * 1.1f + dir * 0.6f, dir, dmg, true, s);
            if (m != null) m.owner = this;
            return m;
        }

        // ------------------------------------------------------------------ dano recebido
        /// <summary>Retorna "parried", "blocked", "hit" ou "ignored".</summary>
        public string TakeDamage(float amount, Vector3 fromPos)
        {
            if (state == "dead" || invuln > 0) return "ignored";
            Vector3 toAttacker = U.Flat(fromPos - transform.position).normalized;
            if (parryTime > 0)
            {
                FX.Burst(transform.position + toAttacker * 0.6f + Vector3.up, U.Hex("b9a6ff"), 1f, 30);
                HUD.Popup(transform.position + Vector3.up * 2.2f, "PARRY!", U.Hex("b9a6ff"), true);
                Game.I.Shake(0.25f); Game.I.Hitstop(0.12f);
                GameState.stamina = Mathf.Min(GameState.maxStamina, GameState.stamina + 15);
                return "parried";
            }
            float raw = amount;
            amount *= DamageTakenMult();
            KitOnDamaged(raw, amount, fromPos);   // [Classes] Vingança, Muralha de Escudos, Espelho de Aço
            amount = KitManaShield(amount);       // [Classes] Escudo de Mana
            if (amount <= 0f) { GameState.Emit(); return "blocked"; }
            if (shieldHp > 0)
            {
                float ab = Mathf.Min(shieldHp, amount);
                shieldHp -= ab; amount -= ab;
                HUD.Popup(transform.position + Vector3.up * 2.2f, "escudo", U.Hex("9fc4ff"));
                if (amount <= 0) return "blocked";
            }
            if (blocking && Vector3.Dot(facing, toAttacker) > -0.2f)
            {
                float dmg = Mathf.Max(1, amount * GameState.Class.block);
                GameState.P.hp = Mathf.Max(0, GameState.P.hp - dmg);
                GameState.stamina = Mathf.Max(0, GameState.stamina - 14);
                FX.Burst(transform.position + toAttacker * 0.5f + Vector3.up, Color.white, 0.6f, 12);
                HUD.Popup(transform.position + Vector3.up * 2.2f, "-" + Mathf.RoundToInt(dmg), U.Hex("c7d6e8"));
                Game.I.Shake(0.1f);
                AddUltCharge(UltPerDamage);
                if (combo != null) combo.Reflect(amount, fromPos);   // Frenesi do Tank (reflete o golpe inteiro)
                GameState.Emit();
                CheckDeath();
                return "blocked";
            }
            float real = Mathf.Max(1, amount - GameState.Defense() * 0.5f);
            GameState.P.hp = Mathf.Max(0, GameState.P.hp - real);
            invuln = 0.35f;
            visual.Flash();
            if (!Unstoppable)   // [Classes] Postura de Ferro: não é interrompido
            {
                velocity = -toAttacker * 7f;
                SetState("hurt");
                if (charging) CancelCharge();
                visual.Play("Hit", 0.05f, 0.25f, true);
            }
            FX.Burst(transform.position + Vector3.up, U.Hex("ff5a4a"), 0.7f, 16);
            HUD.Popup(transform.position + Vector3.up * 2.2f, "-" + Mathf.RoundToInt(real), U.Hex("ff7a6a"), true);
            Game.I.Shake(0.3f);
            AddUltCharge(UltPerDamage);
            if (combo != null) { combo.Reflect(amount, fromPos); combo.ResetCombo(); }   // levar dano zera o combo
            GameState.Emit();
            CheckDeath();
            return "hit";
        }

        void CheckDeath()
        {
            if (GameState.P.hp <= 0 && state != "dead")
            {
                SetState("dead");
                EndUltimates();
                CancelCharge();
                pressing = false;
                if (combo != null) combo.ResetAll();
                if (lockOn != null) lockOn.Release();
                visual.Play("Death", 0.1f, 99f, true);
                Sfx.Play("death");
                ApplyDeathPenalty();
                Game.I.OnPlayerDied();
            }
        }

        /// <summary>Penalidade por morte: perde 50% do XP do nível atual (nunca desce de nível).
        /// As moedas (10%) são cobradas pelo Game.Respawn.</summary>
        void ApplyDeathPenalty()
        {
            deathCount++;
            int lost = 0;
            if (GameState.P != null && GameState.P.xp > 0)
            {
                lost = Mathf.FloorToInt(GameState.P.xp * DeathXpLoss);
                GameState.P.xp = Mathf.Max(0, GameState.P.xp - lost);
            }
            lastXpLost = lost;
            if (lost > 0)
            {
                GameState.Notify("Você perdeu " + lost + " XP (metade do progresso do nível).");
                GameState.Emit();
                GameState.Save();
            }
            try { Died?.Invoke(this); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }

        public void Revive()
        {
            SetState("idle");
            visual.Play("Idle", 0.1f, 0f, true);
            invuln = 1.5f;
            comboStep = 0;
            RestoreUltAura();
        }

        public void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            velocity = Vector3.zero;
        }
    }
}
