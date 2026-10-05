using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Itens no chão (moedas extras, poções, equipamentos) soltos pelos inimigos.
    /// Pulam do corpo, quicam e brilham; o herói recolhe ao encostar (ímã de 1,5 m)
    /// e o pet ativo vai buscar os que estão no raio de coleta dele.
    /// </summary>
    public class Loot : MonoBehaviour
    {
        public static readonly List<Loot> All = new();
        public const float MagnetRadius = 1.5f;
        const float Life = 120f;

        public string kind;        // "coin" | "item"
        public string itemId;
        public int amount = 1;
        public bool claimed;
        public bool reserved;      // pet já está indo buscar

        Vector3 from, to;
        float t, arcTime = 0.5f, life, baseY;
        bool landed, magnet;
        Transform spin;
        Color glow = Color.white;
        Renderer[] rends;

        public bool Ready => landed && !claimed;

        // ------------------------------------------------------------------ drop
        /// <summary>Chamado pelo Enemy ao morrer.</summary>
        public static void Drop(Vector3 pos, EnemyDef def)
        {
            if (def == null || Game.I == null || Game.I.levelRoot == null) return;
            GameData.Load();
            bool boss = def.boss;
            int lo = def.coins != null && def.coins.Length > 0 ? def.coins[0] : 1;
            int hi = def.coins != null && def.coins.Length > 1 ? def.coins[1] : lo;
            int value = Mathf.Max(1, Mathf.RoundToInt((lo + hi) * 0.15f));
            int coinDrops = boss ? 6 : (Random.value < 0.6f ? 1 : 2);
            for (int i = 0; i < coinDrops; i++) Spawn("coin", "", value, pos);

            if (boss)
            {
                Spawn("item", "great_potion", 1, pos);
                if (Random.value < 0.4f) { var r = RandomGear("raro"); if (r != null) Spawn("item", r, 1, pos); }
            }
            else
            {
                float roll = Random.value;
                if (roll < 0.12f) Spawn("item", "health_potion", 1, pos);
                else if (roll < 0.20f) Spawn("item", "mana_potion", 1, pos);
                else if (roll < 0.215f) { var r = RandomGear("raro"); if (r != null) Spawn("item", r, 1, pos); }
            }
        }

        static string RandomGear(string rarity)
        {
            var l = new List<string>();
            foreach (var d in GameData.ItemOrder)
                if (GameData.IsEquipSlotType(d.slot) && d.slot != "asa" && d.rarity == rarity) l.Add(d.id);
            return l.Count > 0 ? l[Random.Range(0, l.Count)] : null;
        }

        public static Loot Spawn(string kind, string itemId, int amount, Vector3 origin)
        {
            if (Game.I == null || Game.I.levelRoot == null) return null;
            if (kind == "item" && GameData.Item(itemId) == null) return null;
            var go = new GameObject(kind == "coin" ? "Loot_moeda" : "Loot_" + itemId);
            go.transform.SetParent(Game.I.levelRoot, false);
            go.transform.position = origin;
            var l = go.AddComponent<Loot>();
            l.kind = kind; l.itemId = itemId; l.amount = Mathf.Max(1, amount);
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(0.7f, 1.8f);
            l.from = origin + Vector3.up * 0.9f;
            l.to = new Vector3(origin.x + r.x, origin.y, origin.z + r.y);
            l.baseY = origin.y;
            l.arcTime = Random.Range(0.4f, 0.6f);
            l.Build();
            go.transform.position = l.from;
            All.Add(l);
            return l;
        }

        // ------------------------------------------------------------------ visual
        void Build()
        {
            var v = new GameObject("visual").transform;
            v.SetParent(transform, false);
            spin = v;
            if (kind == "coin")
            {
                glow = U.Hex("ffd04a");
                var c = U.Prim(PrimitiveType.Cylinder, v, Vector3.zero, new Vector3(0.34f, 0.025f, 0.34f), glow);
                c.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                c.GetComponent<Renderer>().sharedMaterial = U.Lit(glow, 0.85f, glow * 0.9f);
                var inner = U.Prim(PrimitiveType.Cylinder, c.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.6f, 0.4f, 0.6f), U.Hex("ffe9a0"));
                inner.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("ffe9a0"), 0.9f, U.Hex("ffd04a") * 0.6f);
            }
            else
            {
                var d = GameData.Item(itemId);
                bool consum = d != null && d.slot == "consumivel";
                if (consum)
                {
                    glow = itemId == "mana_potion" ? U.Hex("4a9aff") : itemId == "great_potion" ? U.Hex("ff6ad0") : U.Hex("ff4a4a");
                    var b = U.Prim(PrimitiveType.Sphere, v, Vector3.zero, Vector3.one * 0.3f, glow);
                    b.GetComponent<Renderer>().sharedMaterial = U.Lit(glow, 0.9f, glow * 1.2f);
                    var neck = U.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 0.19f, 0f), new Vector3(0.1f, 0.06f, 0.1f), U.Hex("d8e8f0"));
                    neck.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("d8e8f0"), 0.9f);
                    U.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 0.27f, 0f), new Vector3(0.08f, 0.03f, 0.08f), U.Hex("8a5a3a"));
                }
                else
                {
                    glow = d != null ? GameData.RarityColor(d) : Color.white;
                    var g = U.Prim(PrimitiveType.Cube, v, Vector3.zero, Vector3.one * 0.32f, glow);
                    g.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    g.GetComponent<Renderer>().sharedMaterial = U.Lit(glow, 0.9f, glow * 1.5f);
                    // feixe de luz vertical (raro/lendário)
                    var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(beam.GetComponent<Collider>());
                    beam.name = "feixe";
                    beam.transform.SetParent(transform, false);
                    beam.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                    beam.transform.localScale = new Vector3(0.18f, 1.6f, 0.18f);
                    var br = beam.GetComponent<Renderer>();
                    br.sharedMaterial = U.Fx(true);
                    br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    br.receiveShadows = false;
                    FX.SetColor(br, new Color(glow.r, glow.g, glow.b, 0.35f));
                    var lg = new GameObject("luz");
                    lg.transform.SetParent(transform, false);
                    lg.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                    var lc = lg.AddComponent<Light>();
                    lc.type = LightType.Point; lc.color = glow; lc.intensity = 1.6f; lc.range = 3f; lc.shadows = LightShadows.None;
                }
            }
            // brilho no chão
            var q = FX.FlatQuad("brilho", transform.position, U.SoftTexture(), new Color(glow.r, glow.g, glow.b, 0.55f), true);
            q.transform.SetParent(transform, false);
            q.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = Vector3.one * (kind == "coin" ? 0.8f : 1.2f);
            q.name = "brilho_chao";
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rends = GetComponentsInChildren<Renderer>();
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            if (claimed) return;
            if (spin != null) spin.Rotate(0f, (kind == "coin" ? 240f : 90f) * dt, 0f, Space.World);

            if (!landed)
            {
                t += dt;
                float k = Mathf.Clamp01(t / arcTime);
                Vector3 p = Vector3.Lerp(from, to + Vector3.up * 0.35f, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 1.3f;
                transform.position = p;
                if (k >= 1f)
                {
                    landed = true;
                    FX.Dust(to, U.Hex("c8b8a0"), 0.25f);
                    if (kind == "item") Sfx.Play(RarityOf() == "lendario" ? "item_legendary" : RarityOf() == "raro" ? "item_rare" : "coin", to, 0.5f);
                }
                return;
            }

            // quique suave
            var pos = transform.position;
            var pl = Game.I != null ? Game.I.player : null;
            if (!magnet)
            {
                pos.y = baseY + 0.35f + Mathf.Sin(life * 3f + to.x) * 0.08f;
                transform.position = pos;
                if (pl != null && pl.state != "dead" && U.Flat(pl.transform.position - pos).magnitude <= MagnetRadius) magnet = true;
            }
            else if (pl != null)
            {
                Vector3 target = pl.transform.position + Vector3.up * 1f;
                float sp = 6f + life * 0.5f;
                transform.position = Vector3.MoveTowards(pos, target, Mathf.Min(sp, 18f) * dt);
                if ((transform.position - target).magnitude < 0.45f) { Collect(false); return; }
            }

            // some depois de um tempo (pisca nos últimos 5 s)
            if (life > Life - 5f && rends != null)
            {
                bool on = Mathf.Repeat(life * 6f, 1f) > 0.35f;
                foreach (var r in rends) if (r != null) r.enabled = on;
            }
            if (life > Life) Destroy(gameObject);
        }

        string RarityOf()
        {
            var d = GameData.Item(itemId);
            return d != null ? d.rarity : "comum";
        }

        /// <summary>Entrega o conteúdo ao herói. byPet = recolhido pelo pet (dá XP ao pet).</summary>
        public void Collect(bool byPet)
        {
            if (claimed) return;
            claimed = true;
            Vector3 p = transform.position;
            if (kind == "coin")
            {
                GameState.AddCoins(amount);
                HUD.Popup(p + Vector3.up * 0.4f, "+" + amount + " ◈", U.Hex("ffd04a"), false);
                Sfx.Play("coin", p, 0.55f);
            }
            else
            {
                var d = GameData.Item(itemId);
                if (d != null)
                {
                    GameState.Grant(itemId);
                    GameState.Notify("Pegou: " + d.name + (byPet ? " (trazido pelo pet)" : ""));
                    HUD.Popup(p + Vector3.up * 0.4f, d.name, GameData.RarityColor(d), d.rarity != "comum");
                    Sfx.Play(d.rarity == "lendario" ? "item_legendary" : d.rarity == "raro" ? "item_rare" : "coin", p, 0.8f);
                }
            }
            FX.Sparkle(p, glow, 0.35f, 0.15f);
            Quests.OnItemCollected(kind);
            if (byPet) Pets.OnPetCollected();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        /// <summary>Loot pronto mais próximo de "from", dentro de "radius" de "center" (para o pet).</summary>
        public static Loot NearestFor(Vector3 from, Vector3 center, float radius)
        {
            Loot best = null;
            float bd = float.MaxValue;
            foreach (var l in All)
            {
                if (l == null || !l.Ready || l.magnet) continue;
                if (U.Flat(l.transform.position - center).magnitude > radius) continue;
                float d = U.Flat(l.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = l; }
            }
            return best;
        }
    }
}
