using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Parte visual de um personagem: modelo 3D (ou boneco simples), animações,
    /// arma na mão, brilho de raridade, piscar ao levar dano.
    /// Estados de animação (criados pelo menu Drakantus): Idle, Run, Walk, Attack1, Attack2,
    /// Spin, Cast, Shoot, Hit, Death, Dodge, Block, Jump, Cheer, Interact, Wave, Sit,
    /// SkelIdle, SkelWalk, SkelAttack, SkelDeath, SkelRise, Taunt, Summon.
    /// Esqueletos ("Skeleton_" no id do modelo) trocam sozinhos Idle/Walk/Run/Death pelas versões "Skel".
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        public Transform model;
        public Animator animator;
        public Transform handR, handL;
        public bool simple;          // true = boneco feito de formas
        public float height = 1.8f;

        GameObject weaponObj, offhandObj, glowObj;
        readonly List<Renderer> renderers = new();

        // ---- equipamento visível / aparência (preenchido por Equipment.Apply)
        /// <summary>Ossos do rig KayKit (null no boneco simples).</summary>
        public Transform headBone, chestBone, spineBone, hipsBone;
        /// <summary>Tinta multiplicada por parte (peitoral/calça/bota, peças de equipamento).</summary>
        public readonly Dictionary<Renderer, Color> partTint = new();
        /// <summary>Textura recolorida (aparência) por parte; aplicada como _BaseMap via MaterialPropertyBlock.</summary>
        public readonly Dictionary<Renderer, Texture> partMap = new();
        /// <summary>Objetos criados pelo equipamento (elmo, capa, asas) — destruídos a cada Equipment.Apply.</summary>
        public readonly List<GameObject> equipObjects = new();
        /// <summary>Partes do modelo escondidas pelo equipamento (ex.: chapéu original sob o elmo).</summary>
        public readonly List<Renderer> hiddenParts = new();
        static MaterialPropertyBlock sharedMpb;

        /// <summary>Renderers do corpo (sem armas). Útil para tingir por parte.</summary>
        public List<Renderer> BodyRenderers => renderers;

        /// <summary>Registra um renderer extra (peça de equipamento) para receber tinta/piscar junto com o corpo.</summary>
        public void AddPartRenderer(Renderer r, Color tintColor)
        {
            if (r == null) return;
            if (!renderers.Contains(r)) renderers.Add(r);
            partTint[r] = tintColor;
        }

        /// <summary>Remove o equipamento visual anterior (peças, tintas, texturas e partes escondidas).</summary>
        public void ClearEquipmentVisual()
        {
            foreach (var g in equipObjects)
            {
                if (g == null) continue;
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) renderers.Remove(r);
                Destroy(g);
            }
            equipObjects.Clear();
            foreach (var r in hiddenParts) if (r != null) r.enabled = true;
            hiddenParts.Clear();
            renderers.RemoveAll(r => r == null);
            partTint.Clear();
            partMap.Clear();
        }

        /// <summary>Reaplica tinta + tintas por parte + texturas de aparência.</summary>
        public void RefreshColors() => ApplyColor(tint);
        Color tint = Color.white;
        float flash, punch;
        string current = "";
        float lockUntil;
        // animação procedural (boneco simples ou sem clipes)
        Transform simpleBody, simpleArmR;
        float procAttack, procT;
        bool moving;
        // animação com clipes
        bool skeleton;               // modelo de esqueleto: usa SkelIdle/SkelWalk/SkelDeath
        float speed01 = 1f;          // velocidade relativa de locomoção (SetSpeed01)

        static readonly string[] HideKeywords = { "sword", "axe", "staff", "wand", "bow", "crossbow", "dagger", "knife", "shield", "spellbook", "book", "mug", "blade", "hammer", "scythe" };

        /// <summary>Monta o visual a partir do nome lógico do modelo (ex.: "hero_Knight", "enemy_Skeleton_Minion").</summary>
        public void Build(string modelId, Color bodyColor, float scale = 1f)
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            renderers.Clear();
            partTint.Clear(); partMap.Clear(); equipObjects.Clear(); hiddenParts.Clear();
            headBone = chestBone = spineBone = hipsBone = null;
            skeleton = !string.IsNullOrEmpty(modelId) && modelId.Contains("Skeleton_");
            current = "";
            lockUntil = 0f;
            var g = Models.Spawn(modelId, transform, Vector3.zero, 0, scale);
            if (g != null)
            {
                simple = false;
                model = g.transform;
                animator = g.GetComponentInChildren<Animator>();
                if (animator == null) animator = g.AddComponent<Animator>();
                if (ModelLibrary.I.characterAnimator != null) animator.runtimeAnimatorController = ModelLibrary.I.characterAnimator;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1f;
                // religa os ossos ao controller recém-atribuído (clipes Generic ligam por caminho "Rig_Medium/root/...")
                if (animator.runtimeAnimatorController != null) animator.Rebind();
                // esconde as armas que vêm grudadas no modelo (a arma equipada é colocada depois)
                foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.name.ToLowerInvariant();
                    bool hide = false;
                    foreach (var kw in HideKeywords) if (n.Contains(kw)) { hide = true; break; }
                    if (hide) r.gameObject.SetActive(false);
                    else renderers.Add(r);
                }
                handR = U.FindDeep(model, "handslot.r") ?? U.FindDeep(model, "handslotr") ?? U.FindDeep(model, "hand.r") ?? U.FindDeep(model, "righthand") ?? U.FindDeep(model, "hand_r");
                headBone = FindExact(model, "head");
                chestBone = FindExact(model, "chest");
                spineBone = FindExact(model, "spine");
                hipsBone = FindExact(model, "hips");
                handL = U.FindDeep(model, "handslot.l") ?? U.FindDeep(model, "handslotl") ?? U.FindDeep(model, "hand.l") ?? U.FindDeep(model, "lefthand") ?? U.FindDeep(model, "hand_l");
                var b = new Bounds(transform.position, Vector3.zero);
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                height = Mathf.Max(1f, b.size.y);
            }
            else BuildSimple(bodyColor, scale);
            SetTint(tint);
        }

        static Transform FindExact(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        void BuildSimple(Color bodyColor, float scale)
        {
            simple = true;
            var root = new GameObject("boneco").transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * scale;
            model = root;
            simpleBody = new GameObject("corpo").transform; simpleBody.SetParent(root, false);
            U.Prim(PrimitiveType.Capsule, simpleBody, new Vector3(0, 0.75f, 0), new Vector3(0.7f, 0.65f, 0.55f), bodyColor);
            U.Prim(PrimitiveType.Sphere, simpleBody, new Vector3(0, 1.6f, 0), Vector3.one * 0.55f, U.Hex("f0c8a0"));
            U.Prim(PrimitiveType.Sphere, simpleBody, new Vector3(-0.1f, 1.65f, 0.24f), Vector3.one * 0.08f, Color.black);
            U.Prim(PrimitiveType.Sphere, simpleBody, new Vector3(0.1f, 1.65f, 0.24f), Vector3.one * 0.08f, Color.black);
            simpleArmR = new GameObject("braco").transform; simpleArmR.SetParent(simpleBody, false); simpleArmR.localPosition = new Vector3(0.42f, 1.1f, 0);
            U.Prim(PrimitiveType.Capsule, simpleArmR, new Vector3(0, -0.3f, 0), new Vector3(0.22f, 0.32f, 0.22f), bodyColor * 0.9f);
            handR = new GameObject("handslot.r").transform; handR.SetParent(simpleArmR, false); handR.localPosition = new Vector3(0, -0.6f, 0.05f);
            var armL = new GameObject("bracoL").transform; armL.SetParent(simpleBody, false); armL.localPosition = new Vector3(-0.42f, 1.1f, 0);
            U.Prim(PrimitiveType.Capsule, armL, new Vector3(0, -0.3f, 0), new Vector3(0.22f, 0.32f, 0.22f), bodyColor * 0.9f);
            handL = new GameObject("handslot.l").transform; handL.SetParent(armL, false); handL.localPosition = new Vector3(0, -0.6f, 0.05f);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) renderers.Add(r);
            height = 1.9f * scale;
        }

        // ------------------------------------------------------------------ armas
        public void SetWeapon(string weaponModel, string offhandModel, string glowHex)
        {
            if (weaponObj) Destroy(weaponObj);
            if (offhandObj) Destroy(offhandObj);
            if (glowObj) Destroy(glowObj);
            if (!string.IsNullOrEmpty(weaponModel) && handR != null) weaponObj = SpawnWeapon(weaponModel, handR);
            if (!string.IsNullOrEmpty(offhandModel) && handL != null) offhandObj = SpawnWeapon(offhandModel, handL);
            if (weaponObj != null && !string.IsNullOrEmpty(glowHex))
            {
                var c = U.Hex(glowHex);
                foreach (var r in weaponObj.GetComponentsInChildren<Renderer>())
                {
                    foreach (var m in r.materials)
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", c * 1.6f);
                    }
                }
                glowObj = new GameObject("brilho");
                glowObj.transform.SetParent(weaponObj.transform, false);
                glowObj.transform.localPosition = new Vector3(0, 0.5f, 0);
                var l = glowObj.AddComponent<Light>();
                l.type = LightType.Point; l.color = c; l.range = 2.5f; l.intensity = 2.2f; l.shadows = LightShadows.None;
                glowObj.AddComponent<GlowPulse>();
            }
        }

        // Armas geradas no Blender (Tools/Blender/drakantus_weapons.py) em Resources/Models/Weapons.
        static readonly Dictionary<string, string> GenWeapons = new Dictionary<string, string>
        {
            { "sword", "gen_sword" }, { "sword2h", "gen_greatsword" }, { "dagger", "gen_dagger" },
            { "pistol", "gen_pistol" }, { "bow", "gen_bow" }, { "longbow", "gen_longbow" },
            { "staff", "gen_staff_arcane" }, { "staff_holy", "gen_staff_holy" }, { "hammer", "gen_hammer" },
            { "axe", "gen_axe" }, { "axe_blood", "gen_axe_blood" }, { "spear", "gen_spear" }, { "shield", "gen_tower_shield" },
        };
        static readonly Dictionary<string, GameObject> genCache = new Dictionary<string, GameObject>();

        static GameObject SpawnGenerated(string id, Transform hand)
        {
            if (!GenWeapons.TryGetValue(id, out var file)) return null;
            if (!genCache.TryGetValue(file, out var prefab))
            {
                prefab = Resources.Load<GameObject>("Models/Weapons/" + file);
                genCache[file] = prefab;
            }
            if (prefab == null) return null;
            var g = Object.Instantiate(prefab, hand);
            g.name = "arma_" + id;
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = Vector3.one;
            return g;
        }

        GameObject SpawnWeapon(string id, Transform hand)
        {
            var gen = SpawnGenerated(id, hand);
            if (gen != null) return gen;
            // arco longo = arco KayKit maior; pistola = modelo próprio (não existe no KayKit)
            if (id == "longbow")
            {
                var lb = SpawnWeapon("bow", hand);
                if (lb != null) { lb.name = "arma_longbow"; lb.transform.localScale = Vector3.one * 1.45f; }
                return lb;
            }
            if (id == "pistol") return BuildPistol(hand);
            // armas KayKit já vêm modeladas com o pivô na empunhadura:
            // presas ao osso handslot.r/handslot.l com posição e rotação zero ficam certas.
            var w = Models.Spawn("w_" + id, hand, Vector3.zero, 0, 1f);
            if (w != null)
            {
                w.transform.localPosition = Vector3.zero;
                w.transform.localRotation = Quaternion.identity;
                return w;
            }
            // arma simples
            var g = new GameObject("arma_" + id);
            g.transform.SetParent(hand, false);
            var t = g.transform;
            switch (id)
            {
                case "shield":
                    U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.1f), new Vector3(0.7f, 0.05f, 0.7f), U.Hex("8090b0")).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    break;
                case "bow":
                    U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.1f, 0), new Vector3(0.06f, 1.2f, 0.06f), U.Hex("8a5a32"));
                    break;
                case "staff":
                case "skel_staff":
                    U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.3f, 0), new Vector3(0.07f, 0.8f, 0.07f), U.Hex("6b4a32"));
                    U.Prim(PrimitiveType.Sphere, t, new Vector3(0, 1.15f, 0), Vector3.one * 0.22f, U.Hex("8fd8ff"));
                    break;
                case "wand":
                    U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.2f, 0), new Vector3(0.05f, 0.25f, 0.05f), U.Hex("6b4a32"));
                    break;
                case "axe":
                case "skel_axe":
                    U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.3f, 0), new Vector3(0.06f, 0.45f, 0.06f), U.Hex("6b4a32"));
                    U.Prim(PrimitiveType.Cube, t, new Vector3(0.15f, 0.65f, 0), new Vector3(0.3f, 0.25f, 0.05f), U.Hex("c0c4cc"));
                    break;
                default:
                    U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.45f, 0), new Vector3(0.08f, 0.8f, 0.03f), U.Hex("d8dce4"));
                    U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.05f, 0), new Vector3(0.28f, 0.05f, 0.06f), U.Hex("8a6a3a"));
                    break;
            }
            return g;
        }

        /// <summary>Pistola low poly feita de primitivas (cano, tambor, cabo e guarda dourada).</summary>
        static GameObject BuildPistol(Transform hand)
        {
            var g = new GameObject("arma_pistol");
            g.transform.SetParent(hand, false);
            var t = g.transform;
            Color metal = U.Hex("3a3f4a"), wood = U.Hex("7a4a2a"), brass = U.Hex("d8b060");
            // empunhadura na mão (eixo Y), cano para a frente (eixo Z)
            U.Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.02f, -0.02f), new Vector3(0.08f, 0.2f, 0.1f), wood).transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            U.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.1f, 0.08f), new Vector3(0.09f, 0.09f, 0.2f), metal);
            var barrel = U.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.12f, 0.26f), new Vector3(0.055f, 0.13f, 0.055f), metal);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var drum = U.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.1f, 0.06f), new Vector3(0.11f, 0.045f, 0.11f), brass);
            drum.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            U.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.035f, 0.07f), new Vector3(0.02f, 0.06f, 0.08f), brass);
            foreach (var c in g.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            return g;
        }

        // ------------------------------------------------------------------ cor / dano
        public void SetTint(Color c)
        {
            tint = c;
            ApplyColor(c);
        }

        void ApplyColor(Color c)
        {
            if (sharedMpb == null) sharedMpb = new MaterialPropertyBlock();
            var mpb = sharedMpb;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                mpb.Clear();   // bloco montado do zero: sem textura de aparência = textura original do material
                Color f = c;
                if (partTint.TryGetValue(r, out var pt)) f = new Color(c.r * pt.r, c.g * pt.g, c.b * pt.b, c.a * pt.a);
                mpb.SetColor("_BaseColor", f);
                mpb.SetColor("_Color", f);
                if (partMap.TryGetValue(r, out var tex) && tex != null)
                {
                    mpb.SetTexture("_BaseMap", tex);
                    mpb.SetTexture("_MainTex", tex);
                }
                r.SetPropertyBlock(mpb);
            }
        }

        public void Flash() { flash = 0.12f; punch = 1f; ApplyColor(new Color(1f, 0.45f, 0.45f)); }

        // ------------------------------------------------------------------ animação
        /// <summary>Toca um estado. "lockTime" impede que andar/parado substitua o golpe antes do fim.</summary>
        public void Play(string state, float fade = 0.08f, float lockTime = 0f, bool restart = false)
        {
            if (Time.time < lockUntil && lockTime <= 0f && (state == "Idle" || state == "Run" || state == "Walk")) return;
            if (state == current && !restart) return;
            current = state;
            if (lockTime > 0f) lockUntil = Time.time + lockTime;
            if (state.StartsWith("Attack") || state == "Spin" || state == "Cast" || state == "Shoot") procAttack = 1f;
            if (animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled)
            {
                ApplyAnimSpeed();
                string anim = MapState(state);
                int h = Animator.StringToHash(anim);
                if (animator.HasState(0, h)) { animator.CrossFadeInFixedTime(h, fade, 0, 0f); return; }
                h = Animator.StringToHash(state);
                if (anim != state && animator.HasState(0, h)) { animator.CrossFadeInFixedTime(h, fade, 0, 0f); return; }
                if (state == "Run" && animator.HasState(0, Animator.StringToHash("Walk"))) { animator.CrossFadeInFixedTime("Walk", fade); return; }
            }
        }

        /// <summary>Troca o nome lógico pelo estado específico do modelo (esqueletos têm idle/andar/morte próprios).</summary>
        string MapState(string state)
        {
            if (!skeleton) return state;
            switch (state)
            {
                case "Idle": return "SkelIdle";
                case "Run": return "SkelWalk";
                case "Walk": return "SkelWalk";
                case "Death": return "SkelDeath";
                default: return state;
            }
        }

        /// <summary>
        /// Opcional: velocidade real / velocidade máxima (0..1, aceita um pouco acima de 1).
        /// Ajusta a velocidade da animação só enquanto o estado atual é Run/Walk, para os pés não "patinarem".
        /// </summary>
        public void SetSpeed01(float v)
        {
            speed01 = Mathf.Clamp(v, 0f, 1.5f);
            ApplyAnimSpeed();
        }

        void ApplyAnimSpeed()
        {
            if (animator == null) return;
            if (current == "Run" || current == "Walk") animator.speed = Mathf.Clamp(speed01, 0.5f, 1.5f);
            else animator.speed = 1f;
        }

        public void SetMoving(bool m) => moving = m;

        void Update()
        {
            if (flash > 0f)
            {
                flash -= Time.deltaTime;
                if (flash <= 0f) ApplyColor(tint);
            }
            if (punch > 0f)
            {
                punch = Mathf.Max(0f, punch - Time.deltaTime * 6f);
            }
            if (model != null)
            {
                float s = 1f + 0.12f * punch;
                transform.localScale = new Vector3(s, 1f / Mathf.Sqrt(s), s);
            }
            // animação procedural para o boneco simples
            if (simple && simpleBody != null)
            {
                procT += Time.deltaTime * (moving ? 12f : 3f);
                float bob = moving ? Mathf.Abs(Mathf.Sin(procT)) * 0.12f : Mathf.Sin(procT) * 0.02f;
                simpleBody.localPosition = new Vector3(0, bob, 0);
                simpleBody.localRotation = Quaternion.Euler(moving ? 8f : 0f, 0, 0);
                procAttack = Mathf.Max(0f, procAttack - Time.deltaTime * 3.5f);
                if (simpleArmR != null)
                    simpleArmR.localRotation = Quaternion.Euler(-150f * Mathf.Sin(procAttack * Mathf.PI) + (moving ? Mathf.Sin(procT) * 25f : 0f), 0, 0);
                if (current == "Death") simpleBody.localRotation = Quaternion.Euler(-80, 0, 0);
            }
        }
    }

    /// <summary>Pulso suave da luz de raridade.</summary>
    public class GlowPulse : MonoBehaviour
    {
        Light l; float baseI;
        void Start() { l = GetComponent<Light>(); baseI = l != null ? l.intensity : 1f; }
        void Update() { if (l) l.intensity = baseI * (0.75f + 0.25f * Mathf.Sin(Time.time * 4f)); }
    }
}
