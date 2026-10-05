using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// [Quebraveis] Objeto quebrável do cenário: barril, caixa grande, caixa pequena, vaso e barril explosivo.
    /// Tipos (kind): "barrel", "crate", "crate_small", "vase", "explosive".
    /// Leva dano de golpes, projéteis, habilidades e explosões (o Player/Projectile chamam os helpers estáticos).
    /// Ao quebrar: estilhaços, som e às vezes moedas/poções (Loot.Spawn).
    /// Barril explosivo (vermelho, com pavio): explode em 3,5 m ferindo inimigos E o herói, em cadeia com outros barris.
    /// Pode ser posto num prefab de andar: o visual e o colisor são montados no Start (só em Play).
    /// </summary>
    public class Breakable : MonoBehaviour
    {
        /// <summary>Quebráveis inteiros na cena (registrados em OnEnable, só em Play).</summary>
        public static readonly List<Breakable> All = new();
        public const float ExplosionRadius = 3.5f;
        public static readonly string[] Kinds = { "barrel", "crate", "crate_small", "vase", "explosive" };

        [Tooltip("barrel, crate, crate_small, vase, explosive")]
        public string kind = "barrel";
        [Tooltip("Vida (0 = padrão do tipo).")]
        public float maxHp;

        [System.NonSerialized] public float hp;
        [System.NonSerialized] public bool broken;
        [System.NonSerialized] public float radius = 0.5f;
        [System.NonSerialized] public float height = 1f;

        bool built;
        Transform model;
        Vector3 modelScale = Vector3.one;
        Quaternion modelRot = Quaternion.identity;
        float wobble;
        Light fuseLight;
        Transform fuseSpark;
        BoxCollider box;

        public bool Explosive => kind == "explosive";

        // ------------------------------------------------------------------ criação
        /// <summary>Cria um quebrável. pos é LOCAL ao parent (mundo se parent == null).</summary>
        public static Breakable Spawn(string kind, Vector3 pos, Transform parent, float yaw = 0f)
        {
            var go = new GameObject("Quebravel_" + kind);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var b = go.AddComponent<Breakable>();
            b.kind = kind;
            b.Build();
            return b;
        }

        void OnEnable()
        {
            if (Application.isPlaying && built && !broken && !All.Contains(this)) All.Add(this);
        }

        void OnDisable() { All.Remove(this); }

        void Start()
        {
            if (!Application.isPlaying) return;
            Build();
        }

        /// <summary>Monta modelo, colisor e vida (uma vez só).</summary>
        public void Build()
        {
            if (built) return;
            built = true;
            string id;
            float h;
            switch (kind)
            {
                case "crate": id = "crate_A_big"; h = LevelBuilder.H_CRATE; hp = 4f; radius = 0.6f; break;
                case "crate_small": id = "crate_B_small"; h = LevelBuilder.H_CRATE_S; hp = 2f; radius = 0.42f; break;
                case "vase": id = ""; h = 0.9f; hp = 1f; radius = 0.35f; break;
                case "explosive": id = "barrel"; h = LevelBuilder.H_BARREL; hp = 1f; radius = 0.5f; break;
                default: kind = "barrel"; id = "barrel"; h = LevelBuilder.H_BARREL; hp = 3f; radius = 0.5f; break;
            }
            height = h;
            if (maxHp > 0f) hp = maxHp; else maxHp = hp;

            if (kind == "vase") model = BuildVase();
            else
            {
                var g = LevelDecor.SpawnSized(id, transform, Vector3.zero, 0f, h);
                g.name = "modelo";
                model = g.transform;
            }
            foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            modelScale = model.localScale;
            modelRot = model.localRotation;

            if (Explosive) BuildExplosiveParts();

            box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, h * 0.5f, 0f);
            box.size = new Vector3(radius * 1.8f, h, radius * 1.8f);
            if (Application.isPlaying && isActiveAndEnabled && !All.Contains(this)) All.Add(this);
        }

        Transform BuildVase()
        {
            var root = new GameObject("modelo").transform;
            root.SetParent(transform, false);
            Color clay = U.Hex("b86a3c"), dark = U.Hex("7a4024");
            U.Prim(PrimitiveType.Sphere, root, new Vector3(0f, 0.36f, 0f), new Vector3(0.62f, 0.66f, 0.62f), clay);
            U.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.74f, 0f), new Vector3(0.28f, 0.1f, 0.28f), clay);
            U.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.85f, 0f), new Vector3(0.38f, 0.04f, 0.38f), dark);
            U.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.36f, 0f), new Vector3(0.64f, 0.04f, 0.64f), dark);   // faixa
            return root;
        }

        void BuildExplosiveParts()
        {
            // pinta de vermelho (o _BaseColor multiplica a textura do barril KayKit)
            foreach (var r in model.GetComponentsInChildren<Renderer>()) FX.SetColor(r, U.Hex("ff4a3a"));
            // faixa de aviso amarela
            var band = U.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, height * 0.55f, 0f), new Vector3(radius * 2.05f, 0.05f, radius * 2.05f), U.Hex("ffd04a"));
            band.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("ffd04a"), 0.3f, U.Hex("ffb020") * 0.5f);
            band.transform.SetParent(model, true);
            // pavio
            var fuse = U.Prim(PrimitiveType.Cylinder, transform, new Vector3(0.12f, height + 0.1f, 0f), new Vector3(0.04f, 0.12f, 0.04f), U.Hex("3a2a1a"));
            fuse.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            fuse.transform.SetParent(model, true);
            var spark = U.Prim(PrimitiveType.Sphere, transform, new Vector3(0.17f, height + 0.22f, 0f), Vector3.one * 0.08f, U.Hex("ffd27a"));
            spark.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("ffd27a"), 0.5f, U.Hex("ff9a2a") * 4f);
            fuseSpark = spark.transform;
            var lg = new GameObject("luz_pavio");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0.17f, height + 0.3f, 0f);
            fuseLight = lg.AddComponent<Light>();
            fuseLight.type = LightType.Point;
            fuseLight.color = U.Hex("ff9a3a");
            fuseLight.range = 2.2f;
            fuseLight.intensity = 1f;
            fuseLight.shadows = LightShadows.None;
            spark.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (!built) return;
            float dt = Time.deltaTime;
            if (wobble > 0f && model != null)
            {
                wobble = Mathf.Max(0f, wobble - dt);
                float k = wobble / 0.18f;
                model.localScale = new Vector3(modelScale.x * (1f + 0.12f * k), modelScale.y * (1f - 0.1f * k), modelScale.z * (1f + 0.12f * k));
                model.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 60f) * 6f * k, 0f, Mathf.Cos(Time.time * 55f) * 6f * k) * modelRot;
                if (wobble <= 0f) { model.localScale = modelScale; model.localRotation = modelRot; }
            }
            if (fuseLight != null)
            {
                float n = Mathf.PerlinNoise(Time.time * 12f, transform.position.x);
                fuseLight.intensity = (broken ? 3f : 0.8f) + n * 1.2f;
                if (fuseSpark != null) fuseSpark.localScale = Vector3.one * (0.06f + 0.05f * n) * (broken ? 1.8f : 1f);
            }
        }

        // ------------------------------------------------------------------ dano
        /// <summary>Aplica dano. from = de onde veio o golpe (direção dos estilhaços).</summary>
        public void Hit(float dmg, Vector3 from)
        {
            if (broken || !built) return;
            hp -= Mathf.Max(0.5f, dmg);
            wobble = 0.18f;
            Vector3 c = transform.position + Vector3.up * height * 0.6f;
            SkillFX.Sparks(c, ChipColor(), 6, 1.5f, 4f, 1.2f);
            if (hp <= 0f) Break(from, 0f);
            else Sfx.Play(kind == "vase" ? "ice" : "hit", c, 0.45f, 0.15f);
        }

        Color ChipColor()
        {
            switch (kind)
            {
                case "vase": return U.Hex("c87a4a");
                case "explosive": return U.Hex("d84a3a");
                default: return U.Hex("a0703a");
            }
        }

        /// <summary>Quebra (delay > 0 só para o barril explosivo em cadeia).</summary>
        public void Break(Vector3 from, float delay)
        {
            if (broken) return;
            broken = true;
            All.Remove(this);
            if (Explosive && delay > 0f) { StartCoroutine(DelayedExplode(delay)); return; }
            Shatter(from);
        }

        IEnumerator DelayedExplode(float delay)
        {
            wobble = 0.18f;
            yield return new WaitForSeconds(delay);
            Shatter(transform.position);
        }

        void Shatter(Vector3 from)
        {
            Vector3 p = transform.position;
            Vector3 c = p + Vector3.up * height * 0.5f;
            if (box != null) box.enabled = false;
            Color chip = ChipColor();
            SkillFX.Rocks(p, radius * 0.6f, chip, kind == "vase" ? 10 : 14, kind == "crate_small" || kind == "vase" ? 0.6f : 0.8f);
            FX.Dust(p, U.Hex("b8a888"), radius * 1.6f);
            if (Explosive) Explode(c);
            else
            {
                string snd = kind == "vase" ? "ice" : Sfx.Has("break_wood") ? "break_wood" : "hit_heavy";
                Sfx.Play(snd, c, 0.8f, 0.12f);
                DropLoot(p);
            }
            Destroy(gameObject);
        }

        void DropLoot(Vector3 p)
        {
            if (Game.I == null || Game.I.levelRoot == null) return;
            int floor = Game.I.level != null ? Mathf.Max(0, Game.I.level.floor) : 0;
            float coinChance = kind == "vase" ? 0.75f : kind == "crate" ? 0.55f : 0.4f;
            if (Random.value < coinChance)
            {
                int n = kind == "vase" && Random.value < 0.4f ? 2 : 1;
                for (int i = 0; i < n; i++) Loot.Spawn("coin", "", Random.Range(1, 4) + floor, p);
            }
            float roll = Random.value;
            float hpChance = kind == "crate" ? 0.12f : 0.07f;
            if (roll < hpChance) Loot.Spawn("item", "health_potion", 1, p);
            else if (roll < hpChance + 0.05f) Loot.Spawn("item", "mana_potion", 1, p);
        }

        void Explode(Vector3 c)
        {
            Vector3 ground = transform.position;
            SkillFX.ProjectileImpact("fireball", c, SkillFX.Fire, ExplosionRadius, true);
            FX.Ring(ground, ExplosionRadius, SkillFX.Fire, 0.35f, 0.2f);
            Sfx.Play("explosion", c);
            var g = Game.I;
            if (g != null)
            {
                g.Shake(0.45f);
                int dmg = Mathf.Max(8, GameState.AttackPower() * 3);
                foreach (var e in g.enemies.ToArray())
                {
                    if (e == null || e.dead) continue;
                    if (U.Flat(e.transform.position - ground).magnitude > ExplosionRadius + e.radius) continue;
                    e.TakeHit(dmg, ground, false);
                }
                var pl = g.player;
                if (pl != null && pl.state != "dead" && U.Flat(pl.transform.position - ground).magnitude <= ExplosionRadius
                    && Mathf.Abs(pl.transform.position.y - ground.y) < 2.5f)
                    FloorRoot.HurtPlayer(GameState.maxHp * 0.18f, ground);
            }
            // cadeia: barris explosivos estouram com um pequeno atraso; o resto quebra
            foreach (var b in All.ToArray())
            {
                if (b == null || b == this || b.broken) continue;
                if (U.Flat(b.transform.position - ground).magnitude > ExplosionRadius + b.radius) continue;
                if (b.Explosive) b.Break(ground, 0.18f);
                else b.Hit(99f, ground);
            }
        }

        // ------------------------------------------------------------------ helpers de acerto (Player/Projectile/Trap)
        /// <summary>Acerta quebráveis num cone à frente (golpe corpo a corpo). minDot = cosseno do meio-ângulo.</summary>
        public static bool HitCone(Vector3 origin, Vector3 dir, float reach, float minDot, float dmg)
        {
            if (All.Count == 0) return false;
            dir = U.Flat(dir);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            bool any = false;
            foreach (var b in All.ToArray())
            {
                if (b == null || b.broken) continue;
                Vector3 to = U.Flat(b.transform.position - origin);
                float d = to.magnitude;
                if (d > reach + b.radius) continue;
                if (Mathf.Abs(b.transform.position.y - origin.y) > 2f) continue;
                if (d > 0.6f && Vector3.Dot(dir, to / d) < minDot) continue;
                b.Hit(dmg, origin);
                any = true;
            }
            return any;
        }

        /// <summary>Acerta quebráveis numa área circular (habilidades, explosões).</summary>
        public static int HitArea(Vector3 center, float r, float dmg)
        {
            if (All.Count == 0) return 0;
            int n = 0;
            foreach (var b in All.ToArray())
            {
                if (b == null || b.broken) continue;
                if (U.Flat(b.transform.position - center).magnitude > r + b.radius) continue;
                if (Mathf.Abs(b.transform.position.y - center.y) > 2.5f) continue;
                b.Hit(dmg, center);
                n++;
            }
            return n;
        }

        /// <summary>Quebrável mais próximo de a no segmento a→b (projéteis). width = meia-largura.</summary>
        public static Breakable OnSegment(Vector3 a, Vector3 b, float width)
        {
            if (All.Count == 0) return null;
            Breakable best = null;
            float bestT = float.MaxValue;
            Vector3 ab = U.Flat(b - a);
            float l2 = ab.sqrMagnitude;
            foreach (var q in All)
            {
                if (q == null || q.broken) continue;
                Vector3 p = q.transform.position;
                if (b.y < p.y - 0.5f || b.y > p.y + q.height + 0.7f) continue;
                Vector3 ap = U.Flat(p - a);
                float t = l2 > 0f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / l2) : 0f;
                if ((ap - ab * t).magnitude > width + q.radius) continue;
                if (t < bestT) { bestT = t; best = q; }
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == "explosive" ? new Color(1f, 0.25f, 0.2f, 0.9f) : new Color(0.9f, 0.65f, 0.35f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.9f, 1f, 0.9f));
        }
    }
}
