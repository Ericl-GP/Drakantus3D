using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>NPC lido de Resources/Data/npcs.json.</summary>
    [System.Serializable]
    public class NpcDef
    {
        public string id, name, role, model, tint, weapon, anim, function;
        public string[] lines;
    }

    [System.Serializable]
    public class NpcList
    {
        public NpcDef[] npcs;
    }

    /// <summary>
    /// Objeto com que o herói interage pela tecla E: NPC (diálogo/lojas/janelas),
    /// portal (viagem entre mapas) e santuário (cura e conclui o andar).
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        public string kind;     // "npc", "portal", "sanctuary"
        public string label;    // "[E] Falar com Sera"
        public float radius = 2f;

        public NpcDef npc;
        public PortalSpawn portal;
        public CharacterVisual visual;

        float baseYaw, talkT, wanderClock, animLockT, fxClock;
        Vector3 home, walkTarget;
        bool walking, playerInside;
        TextMesh nameText, roleText;
        Light lightC;
        float lightBase = 2f;
        Transform spin, bob;

        static Dictionary<string, NpcDef> npcDefs;
        static Font font;

        string IdleAnim => npc != null && !string.IsNullOrEmpty(npc.anim) ? npc.anim : "Idle";

        // ------------------------------------------------------------------ dados
        static NpcDef FindNpc(string id)
        {
            if (npcDefs == null)
            {
                npcDefs = new Dictionary<string, NpcDef>();
                var ta = Resources.Load<TextAsset>("Data/npcs");
                if (ta != null)
                {
                    try
                    {
                        var l = JsonUtility.FromJson<NpcList>(ta.text);
                        if (l != null && l.npcs != null)
                            foreach (var n in l.npcs)
                                if (n != null && !string.IsNullOrEmpty(n.id)) npcDefs[n.id] = n;
                    }
                    catch (System.Exception ex) { Debug.LogError("[Drakantus] Erro lendo npcs.json: " + ex.Message); }
                }
                else Debug.LogWarning("[Drakantus] Faltando Resources/Data/npcs.json");
            }
            if (id != null && npcDefs.TryGetValue(id, out var d)) return d;
            return new NpcDef
            {
                id = id ?? "npc", name = id ?? "Aldeão", role = "", model = "hero_Rogue", tint = "", weapon = "",
                anim = "Idle", function = "talk", lines = new[] { "Olá, aventureiro!" }
            };
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

        static Interactable Create(string name, string kind, Vector3 pos, float yaw, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var it = go.AddComponent<Interactable>();
            it.kind = kind;
            it.baseYaw = yaw;
            it.home = pos;
            return it;
        }

        void AddLight(Color c, float intensity, float range, float height)
        {
            var lg = new GameObject("luz");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, height, 0f);
            lightC = lg.AddComponent<Light>();
            lightC.type = LightType.Point;
            lightC.color = c;
            lightC.intensity = intensity;
            lightC.range = range;
            lightC.shadows = LightShadows.None;
            lightBase = intensity;
        }

        GameObject GroundQuad(string name, Texture tex, Color c, float size, float y)
        {
            var q = FX.FlatQuad(name, transform.position + Vector3.up * y, tex, c, true);
            q.transform.SetParent(transform, true);
            q.transform.localScale = Vector3.one * size;
            return q;
        }

        void NoColliders()
        {
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        // ------------------------------------------------------------------ fábricas
        public static Interactable SpawnNpc(string npcId, Vector3 pos, float yaw, Transform parent)
        {
            var d = FindNpc(npcId);
            var it = Create("NPC_" + d.id, "npc", pos, yaw, parent);
            it.npc = d;
            it.radius = 2.2f;
            it.label = "[E] Falar com " + d.name;
            it.wanderClock = Random.Range(3f, 7f);

            var col = it.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.4f; col.height = 1.8f; col.center = new Vector3(0f, 0.9f, 0f);

            var v = new GameObject("visual");
            v.transform.SetParent(it.transform, false);
            it.visual = v.AddComponent<CharacterVisual>();
            string mid = d.model ?? "";
            if (!Models.Has(mid) && Models.Has("hero_" + mid)) mid = "hero_" + mid;
            Color body = !string.IsNullOrEmpty(d.tint) ? U.Hex(d.tint) : Color.HSVToRGB(Mathf.Abs((d.id ?? "").GetHashCode() % 360) / 360f, 0.45f, 0.8f);
            it.visual.Build(string.IsNullOrEmpty(mid) ? "hero_Rogue" : mid, body, 1f);
            it.visual.SetWeapon(d.weapon, "", "");
            it.visual.SetTint(string.IsNullOrEmpty(d.tint) ? Color.white : U.Hex(d.tint));
            it.visual.Play(it.IdleAnim, 0.1f, 0f, true);

            float h = Mathf.Max(it.visual.height, 1.7f);
            it.nameText = it.MakeText(d.name ?? "", h + 0.55f, Color.white, 0.05f, 64);
            if (!string.IsNullOrEmpty(d.role)) it.roleText = it.MakeText(d.role, h + 0.25f, U.Hex("ffd27a"), 0.035f, 64);
            return it;
        }

        public static Interactable SpawnPortal(PortalSpawn p, Transform parent)
        {
            var it = Create("Portal_" + p.targetMap, "portal", p.pos, 0f, parent);
            it.portal = p;
            it.radius = p.radius > 0f ? p.radius : 1.6f;
            it.label = "[E] " + (string.IsNullOrEmpty(p.label) ? "Entrar" : p.label);
            Color c = U.Hex("b07aff");

            var ring = it.GroundQuad("anel", U.RingTexture(), new Color(c.r, c.g, c.b, 0.9f), it.radius * 2f, 0.05f);
            it.spin = ring.transform;
            it.GroundQuad("brilho", U.SoftTexture(), new Color(c.r, c.g, c.b, 0.5f), it.radius * 2.4f, 0.04f);

            var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "coluna";
            column.transform.SetParent(it.transform, false);
            column.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            column.transform.localScale = new Vector3(it.radius * 1.3f, 1.6f, it.radius * 1.3f);
            var cr = column.GetComponent<Renderer>();
            cr.sharedMaterial = U.Fx(true);
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cr.receiveShadows = false;
            FX.SetColor(cr, new Color(c.r, c.g, c.b, 0.3f));

            it.AddLight(c, 2.5f, 7f, 1.2f);
            it.NoColliders();
            it.nameText = it.MakeText(string.IsNullOrEmpty(p.label) ? "Portal" : p.label, 3.6f, U.Hex("e0ccff"), 0.045f, 64);
            return it;
        }

        public static Interactable SpawnSanctuary(Vector3 pos, Transform parent)
        {
            var it = Create("Santuario", "sanctuary", pos, 0f, parent);
            it.radius = 2.4f;
            it.label = "[E] Santuário";
            Color c = U.Hex("b8f08a"), gold = U.Hex("ffd870");

            var ring = it.GroundQuad("anel", U.RingTexture(), new Color(c.r, c.g, c.b, 0.9f), 4.4f, 0.05f);
            it.spin = ring.transform;
            it.GroundQuad("brilho", U.SoftTexture(), new Color(gold.r, gold.g, gold.b, 0.45f), 5f, 0.04f);

            U.Prim(PrimitiveType.Cylinder, it.transform, new Vector3(0f, 0.35f, 0f), new Vector3(1.1f, 0.35f, 1.1f), U.Hex("8a8490"));
            U.Prim(PrimitiveType.Cylinder, it.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.06f, 0.7f), U.Hex("a8a2b0"));
            var b = new GameObject("cristal").transform;
            b.SetParent(it.transform, false);
            b.localPosition = new Vector3(0f, 1.6f, 0f);
            var crystal = U.Prim(PrimitiveType.Cube, b, Vector3.zero, new Vector3(0.45f, 0.75f, 0.45f), c);
            crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 35f);
            crystal.GetComponent<Renderer>().sharedMaterial = U.Lit(c, 0.8f, c * 2.5f);
            it.bob = b;

            it.AddLight(Color.Lerp(c, gold, 0.4f), 3f, 8f, 1.8f);
            it.NoColliders();
            it.nameText = it.MakeText("Santuário", 3f, U.Hex("e8ffd0"), 0.045f, 64);
            return it;
        }

        // ------------------------------------------------------------------ interação
        public virtual void Interact()
        {
            switch (kind)
            {
                case "npc": TalkNpc(); break;
                case "portal": UsePortal(); break;
                case "sanctuary": UseSanctuary(); break;
            }
        }

        void TalkNpc()
        {
            if (npc == null || HUD.I == null) return;
            talkT = 5f;
            walking = false;
            visual.Play("Wave", 0.1f, 1.2f, true);
            animLockT = 1.2f;

            string line = npc.lines != null && npc.lines.Length > 0 ? npc.lines[Random.Range(0, npc.lines.Length)] : "...";
            var buttons = new List<(string label, System.Action action)>();
            string fn = string.IsNullOrEmpty(npc.function) ? "talk" : npc.function;
            foreach (var raw in fn.Split(new[] { '|', ',' }))
            {
                string f = raw.Trim();
                if (f.StartsWith("shop:"))
                {
                    string cat = f.Substring(5);
                    buttons.Add(("Ver loja", () => { if (HUD.I != null) HUD.I.ShopWindow(cat); }));
                }
                else if (f == "class_master")
                    buttons.Add(("Evoluir classe", () => { if (HUD.I != null) HUD.I.EvolveWindow(); }));
                else if (f == "skills")
                    buttons.Add(("Habilidades", () => { if (HUD.I != null) HUD.I.SkillsWindow(); }));
                else if (f == "tower")
                    buttons.Add(("Entrar na Torre", () => { if (HUD.I != null) HUD.I.TowerWindow(); }));
                else if (f == "register")
                {
                    if (!GameState.P.registered)
                        buttons.Add(("Registrar", () =>
                        {
                            GameState.P.registered = true;
                            GameState.Save();
                            Sfx.Play("levelup");
                            GameState.Notify("Registro concluído! A Torre está liberada.");
                        }));
                    else line = "Você já está registrado na Guilda. Boa sorte na Torre!";
                }
                // [Guilda] quadro de missões, rank e loja de pets
                else if (f == "quests")
                {
                    int ready = Quests.ReadyCount();
                    if (ready > 0)
                        buttons.Add(("Entregar missões (" + ready + ")", () => { Quests.TurnInAll(); }));
                    buttons.Add(("Quadro de missões", () => { if (HUD.I != null) HUD.I.QuestBoardWindow(); }));
                }
                else if (f == "rank")
                    buttons.Add(("Rank da Guilda", () => { if (HUD.I != null) HUD.I.RankWindow(); }));
                else if (f == "pets")
                {
                    buttons.Add(("Loja de pets", () => { if (HUD.I != null) HUD.I.PetShopWindow(); }));
                    buttons.Add(("Meus pets", () => { if (HUD.I != null) HUD.I.PetsWindow(); }));
                }
                // [/Guilda]
                // "talk": só conversa
            }
            buttons.Add(("Até logo", () => { if (HUD.I != null && HUD.I.HasModal) HUD.I.CloseModal(); }));
            HUD.I.Dialog(npc.name, npc.role ?? "", line, buttons);
        }

        void UsePortal()
        {
            bool needReg = portal.needsRegistration;
            string tm = portal.targetMap ?? "";
            if (tm == "tower" || tm == "torre") needReg = true;
            if (needReg && !GameState.P.registered)
            {
                Sfx.Play("ui_error");
                if (HUD.I != null) HUD.I.Toast("Você precisa se registrar na Guilda antes (fale com a recepcionista).");
                return;
            }
            if (tm == "tower" || tm == "torre")
            {
                if (HUD.I != null) HUD.I.TowerWindow();
                return;
            }
            if (Game.I == null || string.IsNullOrEmpty(tm)) return;
            Sfx.Play("portal", transform.position);
            FX.Pillar(transform.position, U.Hex("b07aff"), 1.2f);
            Game.I.Travel(tm, portal.targetSpawn);
        }

        void UseSanctuary()
        {
            var g = Game.I;
            if (g == null) return;
            var boss = g.AliveBoss();
            if (boss != null)
            {
                Sfx.Play("ui_error");
                if (HUD.I != null) HUD.I.Toast("O santuário está selado. Derrote " + boss.def.name + " primeiro.");
                return;
            }
            FX.Pillar(transform.position, U.Hex("b8f08a"), 1.6f);
            g.CompleteFloor();
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            float dt = Time.deltaTime;
            var g = Game.I;
            Player p = g != null ? g.player : null;

            if (spin != null) spin.Rotate(0f, (kind == "portal" ? 90f : 30f) * dt, 0f, Space.World);
            if (bob != null)
            {
                bob.localPosition = new Vector3(0f, 1.6f + Mathf.Sin(Time.time * 2f) * 0.12f, 0f);
                bob.Rotate(0f, 50f * dt, 0f, Space.Self);
            }
            if (lightC != null) lightC.intensity = lightBase * (0.8f + 0.2f * Mathf.Sin(Time.time * 3f));

            switch (kind)
            {
                case "npc": UpdateNpc(dt, p); break;
                case "portal":
                    fxClock -= dt;
                    if (fxClock <= 0f) { fxClock = 0.3f; FX.Sparkle(transform.position + Vector3.up * 0.2f, U.Hex("c8a0ff"), radius * 0.6f, 0.25f); }
                    break;
                case "sanctuary":
                {
                    fxClock -= dt;
                    if (fxClock <= 0f) { fxClock = 0.4f; FX.Sparkle(transform.position + Vector3.up * 0.2f, U.Hex("d8ffb0"), 1.2f, 0.25f); }
                    bool sealedNow = g != null && g.AliveBoss() != null;
                    label = sealedNow ? "[E] Santuário (selado)" : "[E] Santuário — concluir andar";
                    // ao entrar no círculo ativa sozinho
                    bool inside = p != null && p.state != "dead" && U.Flat(p.transform.position - transform.position).magnitude <= radius * 0.7f;
                    if (inside && !playerInside && !sealedNow && g != null && !g.Traveling && (HUD.I == null || !HUD.I.HasModal)) UseSanctuary();
                    playerInside = inside;
                    break;
                }
            }
        }

        void UpdateNpc(float dt, Player p)
        {
            if (visual == null) return;
            bool canWander = npc != null && npc.function == "talk" && IdleAnim == "Idle";
            if (talkT > 0f)
            {
                talkT -= dt;
                walking = false;
                if (p != null) Face(p.transform.position - transform.position, dt, 8f);
            }
            else if (canWander)
            {
                bool playerNear = p != null && U.Flat(p.transform.position - transform.position).magnitude < 3f;
                if (walking)
                {
                    Vector3 to = U.Flat(walkTarget - transform.position);
                    if (to.magnitude < 0.15f || playerNear) { walking = false; wanderClock = Random.Range(4f, 8f); }
                    else { transform.position += to.normalized * 1.2f * dt; Face(to, dt, 6f); }
                }
                else
                {
                    wanderClock -= dt;
                    if (wanderClock <= 0f && !playerNear)
                    {
                        Vector2 r = Random.insideUnitCircle * 2f;
                        walkTarget = home + new Vector3(r.x, 0f, r.y);
                        walking = true;
                    }
                }
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, baseYaw, 0f), 1f - Mathf.Exp(-3f * dt));
            }

            if (animLockT > 0f) animLockT -= dt;
            else
            {
                visual.SetMoving(walking);
                visual.Play(walking ? "Walk" : IdleAnim);
            }
        }

        void Face(Vector3 dir, float dt, float sharp)
        {
            dir = U.Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 1f - Mathf.Exp(-sharp * dt));
        }

        void LateUpdate()
        {
            var cam = CameraRig.Cam;
            if (cam == null) return;
            Quaternion r = cam.transform.rotation;
            if (nameText != null) nameText.transform.rotation = r;
            if (roleText != null) roleText.transform.rotation = r;
        }

        void OnDestroy()
        {
            if (Game.I != null) Game.I.interactables.Remove(this);
        }
    }
}
