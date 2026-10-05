using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Inimigo (Resources/Data/enemies.json). IA: parado/patrulha → alerta ("!") → persegue →
    /// telegrafa o golpe com marca vermelha no chão → ataca → recupera. Atiradores ("arrow"/"orb")
    /// mantêm distância de 5–8 m e disparam projéteis. Chefes: golpe em área, nome sobre a cabeça.
    /// </summary>
    public partial class Enemy : MonoBehaviour
    {
        public EnemyDef def;
        public float hp, maxHp, radius;
        public bool dead;
        public bool alerted;
        public float scale = 1f;
        public string state = "idle";
        public CharacterVisual visual;
        public CharacterController cc;

        float stateT, attackCd, stunT, vy, knockT, barShowT, alertIconT, wanderT, recoverTime, windupTime, animLockUntil, strafeT;
        float strikeRadius, strafeSign = 1f, top = 2f;
        Vector3 knock, home, wanderTarget, aimDir = Vector3.forward, strikeCenter;
        bool wandering, bossSlam, skeleton;
        int attackCount;
        GameObject marker, targetRing, stunRing;
        LineRenderer aimLine;
        Transform barRoot, barFill;
        TextMesh nameText, alertText;
        static Font font;

        bool Ranged => def != null && !string.IsNullOrEmpty(def.ranged);

        // [Inimigos] ganchos para arquétipos (EnemyArchetype), chefes (BossPatterns) e eventos (RandomEvents)
        /// <summary>true = um "cérebro" externo controla movimento/ataques via <see cref="Drive"/> (a IA base só cuida de física/animação).</summary>
        public bool hold;
        /// <summary>Até este Time.time o inimigo ignora dano (transição de fase dos chefes).</summary>
        public float invulnUntil;
        /// <summary>true = Die() não dá moedas/XP/loot (quem criou o inimigo cuida da recompensa).</summary>
        public bool customRewards;
        /// <summary>Velocidade de giro para encarar o alvo (escudeiros giram devagar: dá para pegá-los pelas costas).</summary>
        public float turnRate = 10f;
        /// <summary>Filtro de dano (dano, posição do atacante) → dano final; 0 = bloqueado.</summary>
        public System.Func<int, Vector3, int> hitFilter;
        /// <summary>Disparado uma vez quando o inimigo morre (depois das recompensas).</summary>
        public event System.Action<Enemy> Died;
        public float Top => top;
        public bool Stunned => stunT > 0f;
        public string Ai => def != null && !string.IsNullOrEmpty(def.ai) ? def.ai : "melee";
        Vector3 driveMove, driveFace;
        bool driveMoving;
        float immuneMsgT;

        // ------------------------------------------------------------------ criação
        public static Enemy Spawn(string enemyId, Vector3 pos, Transform parent, float scaleMult = 1f)
        {
            GameData.Load();
            var d = GameData.Enemy(enemyId);
            if (d == null) { Debug.LogWarning("[Drakantus] Inimigo desconhecido: " + enemyId); return null; }
            if (scaleMult <= 0f) scaleMult = 1f;
            float sc = d.scale * scaleMult;
            var go = new GameObject("Inimigo_" + d.id);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var c = go.AddComponent<CharacterController>();
            c.radius = 0.45f * sc;
            c.height = Mathf.Max(c.radius * 2f + 0.1f, 1.8f * sc);
            c.center = new Vector3(0f, c.height * 0.5f, 0f);
            c.stepOffset = Mathf.Min(0.35f, c.height * 0.4f);
            c.slopeLimit = 50f;
            c.skinWidth = 0.04f;
            c.minMoveDistance = 0f;
            var e = go.AddComponent<Enemy>();
            e.Init(d, sc, scaleMult, c);
            return e;
        }

        void Init(EnemyDef d, float sc, float mult, CharacterController c)
        {
            def = d; scale = sc; cc = c;
            maxHp = hp = Mathf.Max(1f, d.hp * Mathf.Max(1f, mult));
            radius = 0.5f * sc;
            home = transform.position;
            skeleton = (d.model ?? "").StartsWith("Skeleton");
            attackCd = Random.Range(0.3f, 1.0f);
            wanderT = Random.Range(0.5f, 3f);

            var v = new GameObject("visual");
            v.transform.SetParent(transform, false);
            visual = v.AddComponent<CharacterVisual>();
            Color tint = string.IsNullOrEmpty(d.tint) ? Color.white : U.Hex(d.tint);
            visual.Build("enemy_" + d.model, U.Hex("d8d0c0") * tint, sc);
            visual.SetWeapon(d.weapon, d.offhand ?? "", "");   // [Inimigos] offhand opcional (escudo)
            visual.SetTint(tint);
            visual.Play(Anim("SkelIdle", "Idle"), 0.1f, 0f, true);
            top = Mathf.Max(visual.height, 1.6f * sc);

            BuildBar();
            if (d.boss) nameText = MakeText(d.name, top + 0.75f, U.Hex("ffd27a"), 0.07f, 64);
            alertText = MakeText("!", top + 0.6f, U.Hex("ffd04a"), 0.06f, 96);
            alertText.fontStyle = FontStyle.Bold;
            alertText.gameObject.SetActive(false);

            // [Inimigos] comportamento por arquétipo/elemento e padrões de chefe
            EnemyArchetype.Attach(this);
            if (d.boss) BossPatterns.Attach(this);
        }

        TextMesh MakeText(string txt, float y, Color c, float size, int fontSize)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var g = new GameObject("texto");
            g.transform.SetParent(transform, false);
            g.transform.localPosition = new Vector3(0f, y, 0f);
            var tm = g.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = txt;
            tm.fontSize = fontSize;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            var mr = g.GetComponent<MeshRenderer>();
            if (font != null) mr.sharedMaterial = font.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return tm;
        }

        void BuildBar()
        {
            barRoot = new GameObject("vida").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(0f, top + 0.3f, 0f);
            float w = def.boss ? 1.6f : 0.9f;
            Quad(barRoot, new Color(0f, 0f, 0f, 0.7f), new Vector3(w + 0.06f, 0.15f, 1f), Vector3.zero);
            var pivot = new GameObject("pivo").transform;
            pivot.SetParent(barRoot, false);
            pivot.localPosition = new Vector3(-w * 0.5f, 0f, -0.01f);
            Quad(pivot, def.boss ? U.Hex("ff7a2a") : U.Hex("e8453a"), new Vector3(w, 0.09f, 1f), new Vector3(w * 0.5f, 0f, 0f));
            barFill = pivot;
            barRoot.gameObject.SetActive(false);
        }

        static void Quad(Transform parent, Color c, Vector3 scl, Vector3 localPos)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = localPos;
            q.transform.localScale = scl;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = U.Fx(false, U.WhiteTexture());
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            FX.SetColor(r, c);
        }

        // ------------------------------------------------------------------ animação
        bool HasState(string s)
        {
            var an = visual != null ? visual.animator : null;
            return an != null && an.runtimeAnimatorController != null && an.isActiveAndEnabled && an.HasState(0, Animator.StringToHash(s));
        }

        string Anim(string skel, string fallback) => skeleton && HasState(skel) ? skel : fallback;

        void LockAnim(float t) { animLockUntil = Time.time + t; }

        void SetState(string s) { state = s; stateT = 0f; }

        // ------------------------------------------------------------------ IA
        void Update()
        {
            float dt = Time.deltaTime;
            if (alertIconT > 0f)
            {
                alertIconT -= dt;
                if (alertIconT <= 0f && alertText != null) alertText.gameObject.SetActive(false);
            }
            if (barShowT > 0f) barShowT -= dt;
            if (dead) return;
            TickStatus(dt);   // [Classes] veneno/queimadura/sangramento, raízes, lentidão... (ClassKitEnemy.cs)
            if (dead) return;

            stateT += dt;
            attackCd -= dt;
            var g = Game.I;
            Player p = g != null ? g.player : null;
            bool pOk = p != null && p.state != "dead" && p.canFight && !p.Stealthed;   // [Classes] furtividade
            Vector3 toP = pOk ? U.Flat(p.transform.position - transform.position) : Vector3.zero;
            float dist = toP.magnitude;
            Vector3 dirP = dist > 0.001f ? toP / dist : transform.forward;
            float detect = def.boss ? 12f : 9f;
            float speed = def.speed;
            Vector3 move = Vector3.zero, face = Vector3.zero;
            bool moving = false;

            if (knockT > 0f)
            {
                knockT -= dt;
                move += knock;
                knock = Vector3.MoveTowards(knock, Vector3.zero, dt * 40f);
            }

            if (stunT > 0f)
            {
                stunT -= dt;
                if (stunRing != null) stunRing.transform.Rotate(0f, 260f * dt, 0f, Space.World);
                if (stunT <= 0f)
                {
                    if (stunRing != null) stunRing.SetActive(false);
                    SetState(pOk ? "chase" : "idle");
                }
            }
            else if (hold)
            {
                // [Inimigos] cérebro externo no controle
                if (driveMoving) { move += driveMove; moving = driveMove.sqrMagnitude > 0.01f; }
                face = driveFace;
            }
            else
            {
                switch (state)
                {
                    case "idle":
                    {
                        if (pOk && (dist < detect || (p.tauntTime > 0f && dist < 14f))) { Alert(true); break; }
                        wanderT -= dt;
                        if (wanderT <= 0f)
                        {
                            wanderT = Random.Range(2f, 4.5f);
                            wandering = Random.value < 0.6f;
                            Vector2 r = Random.insideUnitCircle * 2.5f;
                            wanderTarget = home + new Vector3(r.x, 0f, r.y);
                        }
                        if (wandering)
                        {
                            Vector3 to = U.Flat(wanderTarget - transform.position);
                            if (to.magnitude > 0.3f) { move += to.normalized * speed * 0.4f; face = to; moving = true; }
                            else wandering = false;
                        }
                        break;
                    }
                    case "hurt":
                        if (stateT >= 0.25f) SetState(pOk ? "chase" : "idle");
                        break;
                    case "chase":
                    {
                        if (!pOk) { alerted = false; SetState("idle"); break; }
                        if (dist > detect * 2.2f && p.tauntTime <= 0f) { alerted = false; SetState("idle"); break; }
                        face = toP;
                        if (Ranged)
                        {
                            float far = Mathf.Min(8f, def.reach), near = Mathf.Min(5f, far - 1f);
                            if (dist > far) { move += dirP * speed; moving = true; }
                            else if (dist < near) { move -= dirP * speed * 0.8f; moving = true; }
                            else
                            {
                                strafeT -= dt;
                                if (strafeT <= 0f) { strafeT = Random.Range(1f, 2.2f); strafeSign = Random.value < 0.5f ? -1f : 1f; }
                                move += Vector3.Cross(Vector3.up, dirP) * strafeSign * speed * 0.45f;
                                moving = true;
                            }
                            if (attackCd <= 0f && dist <= def.reach + 1f && ClearShot(p)) BeginWindup(dirP);
                        }
                        else
                        {
                            if (dist > def.reach * 0.8f + 0.2f) { move += dirP * speed; moving = true; }
                            // [Inimigos] enxame: aproxima em zigue-zague
                            if (def.ai == "swarm" && dist > def.reach + 0.8f)
                                move += Vector3.Cross(Vector3.up, dirP) * Mathf.Sin(Time.time * 5f + home.x * 3.1f) * speed * 0.6f;
                            if (attackCd <= 0f && dist <= def.reach + 0.3f) BeginWindup(dirP);
                        }
                        break;
                    }
                    case "windup":
                        if (Ranged)
                        {
                            if (pOk && stateT < windupTime * 0.7f) aimDir = dirP;
                            UpdateAimLine();
                        }
                        face = aimDir;
                        if (stateT >= windupTime) Strike(p);
                        break;
                    case "recover":
                        if (pOk) face = toP;
                        if (stateT >= recoverTime) SetState(pOk ? "chase" : "idle");
                        break;
                    default:
                        SetState(pOk && alerted ? "chase" : "idle");
                        break;
                }
            }

            driveMove = Vector3.zero; driveMoving = false;   // [Inimigos] comando vale 1 quadro

            // separação entre inimigos
            if (g != null)
            {
                Vector3 sep = Vector3.zero;
                foreach (var o in g.enemies)
                {
                    if (o == null || o == this || o.dead) continue;
                    Vector3 d = U.Flat(transform.position - o.transform.position);
                    float min = radius + o.radius;
                    float m = d.magnitude;
                    if (m >= min) continue;
                    if (m < 0.001f) sep += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.5f;
                    else sep += d / m * (min - m);
                }
                move += sep * 4f;
            }

            move = StatusMove(move, dt);   // [Classes] raiz/congelado/lento/puxão
            if (cc != null && cc.enabled)
            {
                if (cc.isGrounded) vy = -1f; else vy -= 25f * dt;
                cc.Move((move + Vector3.up * vy) * dt);
            }

            Vector3 ff = U.Flat(face);
            if (ff.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ff.normalized, Vector3.up), 1f - Mathf.Exp(-turnRate * dt));

            if (Time.time >= animLockUntil)
            {
                visual.SetMoving(moving);
                visual.Play(moving ? Anim("SkelWalk", "Run") : Anim("SkelIdle", "Idle"));
            }
        }

        bool ClearShot(Player p)
        {
            Vector3 a = transform.position + Vector3.up * 1.2f;
            Vector3 b = p.transform.position + Vector3.up * 1.0f;
            return !Projectile.WallBetween(a, b);
        }

        void BeginWindup(Vector3 dirP)
        {
            aimDir = dirP;
            attackCount++;
            windupTime = Mathf.Max(0.2f, def.windup);
            bossSlam = false;
            SetState("windup");
            if (Ranged)
            {
                Color rc = def.ranged == "orb" ? U.Hex("c86aff") : U.Hex("ff6a4a");
                FX.Flash(transform.position + Vector3.up * 1.4f + aimDir * 0.5f, rc, 2f, windupTime);
                UpdateAimLine();
                if (def.ranged == "orb") { visual.Play("Cast", 0.1f, windupTime, true); LockAnim(windupTime); }
            }
            else
            {
                bossSlam = def.boss && attackCount % 3 == 0;
                if (bossSlam)
                {
                    windupTime *= 1.35f;
                    strikeCenter = transform.position;
                    strikeRadius = def.reach * 1.5f + 0.5f;
                    visual.Play("Taunt", 0.1f, windupTime, true);
                    LockAnim(windupTime);
                }
                else
                {
                    strikeRadius = Mathf.Max(0.9f, def.reach * 0.6f);
                    strikeCenter = transform.position + aimDir * (def.reach * 0.6f);
                }
                marker = FX.Marker(strikeCenter, strikeRadius, new Color(1f, 0.2f, 0.15f, 0.85f), windupTime);
            }
        }

        void UpdateAimLine()
        {
            if (aimLine == null)
            {
                var go = new GameObject("mira");
                go.transform.SetParent(transform, false);
                aimLine = go.AddComponent<LineRenderer>();
                aimLine.sharedMaterial = U.Fx(true);
                aimLine.useWorldSpace = true;
                aimLine.positionCount = 2;
                aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                aimLine.receiveShadows = false;
            }
            aimLine.enabled = true;
            float k = windupTime > 0f ? Mathf.Clamp01(stateT / windupTime) : 1f;
            Vector3 o = transform.position + Vector3.up * 0.15f;
            aimLine.SetPosition(0, o + aimDir * 0.5f);
            aimLine.SetPosition(1, o + aimDir * Mathf.Max(4f, def.reach));
            var c = new Color(1f, 0.25f, 0.2f, 0.25f + 0.65f * k);
            aimLine.startColor = c;
            aimLine.endColor = new Color(c.r, c.g, c.b, 0f);
            aimLine.widthMultiplier = 0.05f + 0.1f * k;
        }

        void Strike(Player p)
        {
            KillMarker();
            if (aimLine != null) aimLine.enabled = false;
            recoverTime = def.boss ? 0.8f : 0.55f;
            attackCd = (def.boss ? 0.7f : 1.0f) + Random.Range(0f, 0.6f);
            bool pOk = p != null && p.state != "dead" && p.canFight && !p.Stealthed;

            if (Ranged)
            {
                visual.Play(def.ranged == "orb" ? "Cast" : "Shoot", 0.05f, 0.5f, true);
                LockAnim(0.5f);
                Vector3 origin = transform.position + Vector3.up * 1.2f + aimDir * (radius + 0.3f);
                int dmg = Mathf.Max(1, Mathf.RoundToInt(def.dmg));
                int n = def.boss ? 3 : 1;
                for (int k = 0; k < n; k++)
                {
                    Vector3 d = Quaternion.Euler(0f, (k - (n - 1) * 0.5f) * 14f + BlindSpread(), 0f) * aimDir;
                    Projectile.Spawn(def.ranged, origin, d, dmg, false, null);
                }
            }
            else
            {
                visual.Play(Anim("SkelAttack", "Attack1"), 0.05f, 0.5f, true);
                LockAnim(0.5f);
                Sfx.Play("enemy_attack", transform.position);
                if (bossSlam)
                {
                    FX.Ring(strikeCenter, strikeRadius, U.Hex("ff7a4a"), 0.35f);
                    FX.Dust(strikeCenter, U.Hex("a08060"), 1.6f);
                    if (Game.I != null) Game.I.Shake(0.35f);
                }
                else FX.Slash(transform.position + Vector3.up * 0.9f * scale, aimDir, U.Hex("ffb0a0"), def.reach * 0.7f);

                if (pOk)
                {
                    float d = U.Flat(p.transform.position - strikeCenter).magnitude;
                    if (d <= strikeRadius + 0.4f && !BlindMiss())
                    {
                        bool ice = def.element == "ice";   // [Inimigos] golpe gelado: +25% de dano e efeito de gelo
                        string r = p.TakeDamage(ice ? def.dmg * 1.25f : def.dmg, transform.position);
                        if (ice && r == "hit") EnemyArchetype.FrostHit(p);
                        if (r == "parried")
                        {
                            Sfx.Play("parry", p.transform.position);
                            Stun(1.2f);
                            return;
                        }
                        if (r == "blocked") Sfx.Play("block", p.transform.position);
                        else if (r == "hit") Sfx.Play("player_hurt", p.transform.position);
                    }
                }
            }
            SetState("recover");
        }

        void KillMarker()
        {
            if (marker != null) Destroy(marker);
            marker = null;
        }

        void CancelWindup()
        {
            KillMarker();
            if (aimLine != null) aimLine.enabled = false;
            if (state == "windup") SetState(alerted ? "chase" : "idle");
        }

        /// <summary>Entra em combate (mostra "!", toca som e avisa aliados próximos).</summary>
        public void Alert(bool spread = true)
        {
            if (dead) return;
            bool was = alerted;
            alerted = true;
            if (state == "idle") SetState("chase");
            if (was) return;
            alertIconT = 0.9f;
            if (alertText != null) alertText.gameObject.SetActive(true);
            Sfx.Play(def.boss ? "boss_roar" : "enemy_alert", transform.position, def.boss ? 1f : 0.7f);
            if (def.boss)
            {
                if (Game.I != null) Game.I.Shake(0.3f);
                visual.Play("Taunt", 0.1f, 1f, true);
                LockAnim(1f);
                attackCd = Mathf.Max(attackCd, 1f);
            }
            if (spread && Game.I != null)
            {
                foreach (var o in Game.I.enemies)
                    if (o != null && o != this && !o.dead && !o.alerted && Vector3.Distance(o.transform.position, transform.position) < 6f)
                        o.Alert(false);
            }
        }

        // ------------------------------------------------------------------ dano
        public void TakeHit(int dmg, Vector3 fromPos, bool crit)
        {
            if (dead) return;
            // [Inimigos] invulnerável (transição de fase) / filtro (escudo frontal)
            if (Time.time < invulnUntil)
            {
                if (Time.time >= immuneMsgT)
                {
                    immuneMsgT = Time.time + 0.4f;
                    HUD.Popup(transform.position + Vector3.up * (top + 0.2f), "IMUNE", U.Hex("9fd0ff"));
                    FX.Sparkle(transform.position + Vector3.up * top * 0.5f, U.Hex("9fd0ff"), 0.6f, 0.15f);
                }
                return;
            }
            if (hitFilter != null)
            {
                dmg = hitFilter(dmg, fromPos);
                if (dmg <= 0) { Alert(true); return; }
            }
            hp -= dmg;
            OnStatusHit(dmg);   // [Classes] Marca da Morte guarda parte do dano
            barShowT = 3.5f;
            visual.Flash();
            Vector3 away = U.Flat(transform.position - fromPos);
            away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
            float kb = def.boss ? 1.5f : 5.5f;
            if (crit) kb *= 1.4f;
            knock = away * kb;
            knockT = 0.15f;

            Vector3 head = transform.position + Vector3.up * (top + 0.2f);
            HUD.Popup(head, dmg.ToString(), crit ? U.Hex("ffd04a") : Color.white, crit);
            if (HUD.I != null) HUD.I.OnHit(dmg, crit, this);
            Sfx.Play(crit ? "hit_crit" : "enemy_hit", transform.position);
            Color bc = crit ? U.Hex("fff0c0") : (Random.value < 0.5f ? Color.white : U.Hex("ff6a5a"));
            FX.Burst(transform.position + Vector3.up * top * 0.55f, bc, crit ? 0.9f : 0.55f, crit ? 22 : 12);

            if (hp <= 0f) { Die(); return; }
            Alert(true);
            if (!def.boss && stunT <= 0f && (state != "windup" || crit))
            {
                CancelWindup();
                SetState("hurt");
                visual.Play("Hit", 0.05f, 0.25f, true);
                LockAnim(0.25f);
            }
        }

        public void Stun(float seconds)
        {
            if (dead || seconds <= 0f) return;
            if (def.boss) seconds *= 0.6f;
            Alert(true);
            stunT = Mathf.Max(stunT, seconds);
            CancelWindup();
            SetState("stunned");
            if (stunRing == null)
            {
                stunRing = FX.FlatQuad("atordoado", transform.position + Vector3.up * (top + 0.15f), U.RingTexture(), U.Hex("ffe08a"), true);
                stunRing.transform.SetParent(transform, true);
                stunRing.transform.localPosition = new Vector3(0f, top + 0.15f, 0f);
                stunRing.transform.localScale = Vector3.one * 0.8f * Mathf.Max(1f, scale);
            }
            stunRing.SetActive(true);
            FX.Sparkle(transform.position + Vector3.up * top, U.Hex("ffe08a"), 0.5f, 0.3f);
            visual.Play("Hit", 0.05f, 0.3f, true);
            LockAnim(0.3f);
        }

        public void SetTargeted(bool on)
        {
            if (dead) on = false;
            if (targetRing == null)
            {
                if (!on) return;
                targetRing = FX.FlatQuad("alvo", transform.position + Vector3.up * 0.05f, U.RingTexture(), new Color(1f, 0.25f, 0.2f, 0.9f), true);
                targetRing.transform.SetParent(transform, true);
                targetRing.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                targetRing.transform.localScale = Vector3.one * radius * 2.8f;
            }
            if (targetRing.activeSelf != on) targetRing.SetActive(on);
        }

        void Die()
        {
            dead = true;
            hp = 0f;
            ClearStatus();
            CancelWindup();
            alerted = false;
            SetTargeted(false);
            if (stunRing != null) stunRing.SetActive(false);
            stunT = 0f;
            SetState("dead");
            if (cc != null) cc.enabled = false;
            visual.SetMoving(false);
            visual.Play(Anim("SkelDeath", "Death"), 0.08f, 99f, true);
            LockAnim(99f);

            if (!customRewards)   // [Inimigos]
            {
                int coins = Random.Range(def.coins[0], def.coins[1] + 1);
                GameState.AddCoins(coins);
                GameState.AddXp(def.xp);
                Vector3 head = transform.position + Vector3.up * (top + 0.2f);
                HUD.Popup(head + Vector3.up * 0.4f, "+" + coins + " ◈", U.Hex("ffd04a"), false);
                HUD.Popup(head + Vector3.up * 0.9f, "+" + def.xp + " XP", U.Hex("c9a6ff"), false);
            }
            FX.Burst(transform.position + Vector3.up * 0.8f, U.Hex("e8e0d0"), 1.1f * Mathf.Max(1f, scale), 30);
            FX.Dust(transform.position, U.Hex("b0a898"), 0.9f * scale);
            Sfx.Play("enemy_die", transform.position);
            Sfx.Play("coin", transform.position, 0.6f);
            if (def.boss) FX.Pillar(transform.position, U.Hex("ffd04a"), 1.6f);
            if (Game.I != null) Game.I.OnEnemyDied(this);
            // [Guilda] progresso de missões (+XP do pet) e itens no chão
            Quests.OnEnemyKilled(def.id, def.boss);
            if (!customRewards) Loot.Drop(transform.position, def);
            // [Inimigos] avisa arquétipos/eventos (fogo no chão, goblin, horda...)
            var died = Died;
            Died = null;
            if (died != null)
            {
                try { died(this); }
                catch (System.Exception ex) { Debug.LogException(ex); }
            }
            StartCoroutine(Vanish());
        }

        // ------------------------------------------------------------------ [Inimigos] API para arquétipos, chefes e eventos
        /// <summary>Comando de movimento deste quadro (só vale com <see cref="hold"/> = true).</summary>
        public void Drive(Vector3 velocity, Vector3 faceDir)
        {
            driveMove = U.Flat(velocity);
            driveMoving = driveMove.sqrMagnitude > 0.0001f;
            if (U.Flat(faceDir).sqrMagnitude > 0.0001f) driveFace = U.Flat(faceDir);
        }

        /// <summary>Toca uma animação e impede que andar/parado a substitua por lockTime segundos.</summary>
        public void Act(string anim, float lockTime)
        {
            if (dead || visual == null) return;
            visual.Play(anim, 0.08f, lockTime, true);
            LockAnim(lockTime);
        }

        /// <summary>Cancela o golpe que estava sendo telegrafado (marca no chão / mira).</summary>
        public void CancelAttack() => CancelWindup();

        /// <summary>Mata na hora (bombardeiro explodindo, etc.).</summary>
        public void Kill()
        {
            if (dead) return;
            Die();
        }

        /// <summary>Cura com número verde.</summary>
        public void Heal(float amount)
        {
            if (dead || amount <= 0f) return;
            float before = hp;
            hp = Mathf.Min(maxHp, hp + amount);
            int got = Mathf.RoundToInt(hp - before);
            if (got <= 0) return;
            barShowT = 3.5f;
            HUD.Popup(transform.position + Vector3.up * (top + 0.2f), "+" + got, U.Hex("7aff8a"), false);
        }

        /// <summary>Teleporta (desliga o CharacterController durante a troca).</summary>
        public void TeleportTo(Vector3 pos)
        {
            bool on = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            transform.position = pos;
            if (cc != null && on && !dead) cc.enabled = true;
        }

        IEnumerator Vanish()
        {
            yield return new WaitForSeconds(1.6f);
            float t = 0f;
            Vector3 s = transform.position;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                transform.position = s + Vector3.down * (t * 2f);
                yield return null;
            }
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ barra de vida / textos (sempre virados para a câmera)
        void LateUpdate()
        {
            var cam = CameraRig.Cam;
            if (cam == null) return;
            Quaternion r = cam.transform.rotation;
            if (barRoot != null)
            {
                bool show = !dead && (barShowT > 0f || (def.boss && alerted));
                if (barRoot.gameObject.activeSelf != show) barRoot.gameObject.SetActive(show);
                if (show)
                {
                    barRoot.rotation = r;
                    barFill.localScale = new Vector3(Mathf.Clamp01(hp / Mathf.Max(1f, maxHp)), 1f, 1f);
                }
            }
            if (nameText != null)
            {
                if (nameText.gameObject.activeSelf == dead) nameText.gameObject.SetActive(!dead);
                nameText.transform.rotation = r;
            }
            if (alertText != null && alertText.gameObject.activeSelf) alertText.transform.rotation = r;
        }

        void OnDestroy()
        {
            KillMarker();
            if (Game.I != null) Game.I.enemies.Remove(this);
        }
    }
}
